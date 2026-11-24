using Discord;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;
using ValorantApp.Valorant;

namespace ValorantAppTests
{
    /// <summary>
    /// Failure modes simulated when calling the HenrikDev API.
    /// </summary>
    public enum FailureMode
    {
        Http404,
        Http429,
        Http500,
        Http503,
        TaskCanceled,
        HttpRequestException,
        MalformedJson,
        EmptyData,
        NullData
    }

    /// <summary>
    /// Deterministic end-to-end tests driving BaseValorantProgram through poll ticks when the HenrikDev API fails.
    /// Covers matches and mmr-history endpoints across HTTP error status codes, network timeouts, malformed JSON,
    /// and empty/null data envelopes, verifying non-posting, clean SQLite state, crash resilience, and next-tick recovery.
    /// Note: These tests share static DB state (SQLite connection), so MSTest runs them sequentially.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class ApiFailureE2ETests
    {
        private const string EthanPuuid = "8c7276bd-a70b-56bf-803b-7e0fd9dba287";
        private const ulong TestChannelId = 987654321UL;
        private const ulong EthanDiscordId = 111222333UL;

        #region Helpers

        private static (string Json, string MatchId, long Epoch) LoadRewrittenSwiftplayFixture(DateTime matchTime)
        {
            string path = File.Exists(Path.Combine("../../../MockJsonResponse", "Matches_10_31_2023.json"))
                ? Path.Combine("../../../MockJsonResponse", "Matches_10_31_2023.json")
                : Path.Combine(AppContext.BaseDirectory, "../../../MockJsonResponse", "Matches_10_31_2023.json");

            string raw = File.ReadAllText(path);
            var root = JObject.Parse(raw);
            var metadata = root["data"]![0]!["metadata"]!;
            string matchId = metadata["matchid"]!.ToString();

            long epoch = new DateTimeOffset(matchTime).ToUnixTimeSeconds();
            string gameStartPatched = matchTime.ToString("dddd, MMMM d, yyyy h:mm tt", CultureInfo.InvariantCulture);

            metadata["game_start"] = epoch;
            metadata["game_start_patched"] = gameStartPatched;

            return (root.ToString(Newtonsoft.Json.Formatting.None), matchId, epoch);
        }

        private static (string Json, string MatchId, long Epoch) LoadRewrittenCompMatchFixture(DateTime matchTime)
        {
            string path = File.Exists(Path.Combine("../../../MockJsonResponse", "Matches_11_4_2023.json"))
                ? Path.Combine("../../../MockJsonResponse", "Matches_11_4_2023.json")
                : Path.Combine(AppContext.BaseDirectory, "../../../MockJsonResponse", "Matches_11_4_2023.json");

            string raw = File.ReadAllText(path);
            var root = JObject.Parse(raw);
            var metadata = root["data"]![0]!["metadata"]!;
            string matchId = metadata["matchid"]!.ToString();

            long epoch = new DateTimeOffset(matchTime).ToUnixTimeSeconds();
            string gameStartPatched = matchTime.ToString("dddd, MMMM d, yyyy h:mm tt", CultureInfo.InvariantCulture);

            metadata["game_start"] = epoch;
            metadata["game_start_patched"] = gameStartPatched;

            return (root.ToString(Newtonsoft.Json.Formatting.None), matchId, epoch);
        }

        private static string LoadRewrittenMmrHistoryFixture(string matchId, int newTier = 22, int rrChange = 18)
        {
            string path = File.Exists(Path.Combine("../../../MockJsonResponse", "MmrHistory.json"))
                ? Path.Combine("../../../MockJsonResponse", "MmrHistory.json")
                : Path.Combine(AppContext.BaseDirectory, "../../../MockJsonResponse", "MmrHistory.json");

            string raw = File.ReadAllText(path);
            var root = JObject.Parse(raw);
            var item = root["data"]![0]!;
            item["match_id"] = matchId;
            item["currenttier"] = newTier;
            item["mmr_change_to_last_game"] = rrChange;

            return root.ToString(Newtonsoft.Json.Formatting.None);
        }

        private class ApiFailureHttpMessageHandler : HttpMessageHandler
        {
            public FailureMode? MatchesFailure { get; set; }
            public FailureMode? MmrHistoryFailure { get; set; }
            public string? HealthyMatchesJson { get; set; }
            public string? HealthyMmrHistoryJson { get; set; }
            public Dictionary<string, FailureMode> PerUserMatchesFailures { get; } = new();
            public List<string> RequestedUrls { get; } = new();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri?.ToString() ?? string.Empty;
                RequestedUrls.Add(url);

                if (url.Contains("v3/by-puuid/matches", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var kvp in PerUserMatchesFailures)
                    {
                        if (url.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            return HandleFailure(kvp.Value);
                        }
                    }

                    if (MatchesFailure.HasValue)
                    {
                        return HandleFailure(MatchesFailure.Value);
                    }

                    if (HealthyMatchesJson != null)
                    {
                        var response = new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(HealthyMatchesJson, System.Text.Encoding.UTF8, "application/json")
                        };
                        return Task.FromResult(response);
                    }
                }

                if (url.Contains("v1/by-puuid/mmr-history", StringComparison.OrdinalIgnoreCase))
                {
                    if (MmrHistoryFailure.HasValue)
                    {
                        return HandleFailure(MmrHistoryFailure.Value);
                    }

                    if (HealthyMmrHistoryJson != null)
                    {
                        var response = new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(HealthyMmrHistoryJson, System.Text.Encoding.UTF8, "application/json")
                        };
                        return Task.FromResult(response);
                    }

                    // Any other unconfigured mmr-history request returns 404 cleanly
                    var notFound = new HttpResponseMessage(HttpStatusCode.NotFound)
                    {
                        Content = new StringContent("{\"status\":404,\"message\":\"Not Found\"}", System.Text.Encoding.UTF8, "application/json")
                    };
                    return Task.FromResult(notFound);
                }

                var defaultNotFound = new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{\"status\":404,\"message\":\"Not Found\"}", System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(defaultNotFound);
            }

            private static Task<HttpResponseMessage> HandleFailure(FailureMode mode)
            {
                switch (mode)
                {
                    case FailureMode.Http404:
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                        {
                            Content = new StringContent("{\"status\":404,\"message\":\"Not Found\"}", System.Text.Encoding.UTF8, "application/json")
                        });
                    case FailureMode.Http429:
                        return Task.FromResult(new HttpResponseMessage((HttpStatusCode)429)
                        {
                            Content = new StringContent("{\"status\":429,\"message\":\"Rate Limited\"}", System.Text.Encoding.UTF8, "application/json")
                        });
                    case FailureMode.Http500:
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                        {
                            Content = new StringContent("{\"status\":500,\"message\":\"Internal Server Error\"}", System.Text.Encoding.UTF8, "application/json")
                        });
                    case FailureMode.Http503:
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                        {
                            Content = new StringContent("{\"status\":503,\"message\":\"Service Unavailable\"}", System.Text.Encoding.UTF8, "application/json")
                        });
                    case FailureMode.TaskCanceled:
                        return Task.FromException<HttpResponseMessage>(new TaskCanceledException("The request timed out."));
                    case FailureMode.HttpRequestException:
                        return Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused."));
                    case FailureMode.MalformedJson:
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("<html><body>502 Bad Gateway</body></html>", System.Text.Encoding.UTF8, "application/json")
                        });
                    case FailureMode.EmptyData:
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"status\":200,\"data\":[]}", System.Text.Encoding.UTF8, "application/json")
                        });
                    case FailureMode.NullData:
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"status\":200,\"data\":null}", System.Text.Encoding.UTF8, "application/json")
                        });
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
                }
            }
        }

        private static (BaseValorantProgram Program, RecordingNotifier Notifier) CreateProgram(
            ApiFailureHttpMessageHandler handler,
            TimeProvider? timeProvider = null)
        {
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.henrikdev.xyz/valorant/")
            };

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(x => x.CreateClient("HenrikApiClient")).Returns(httpClient);

            var notifier = new RecordingNotifier();
            var program = new BaseValorantProgram(notifier, factoryMock.Object, NullLogger<BaseValorantProgram>.Instance, timeProvider);
            return (program, notifier);
        }

        private static void SeedUser(string puuid, string username, string tag, string affinity, ulong discordId, ulong channelId)
        {
            ValorantUsersExtension.InsertRow(new ValorantUsers(username, tag, affinity, puuid, discordId));
            ValorantChannelMappingsExtension.InsertRow(new ValorantChannelMappings(puuid, channelId));
        }

        private static void CleanUser(string puuid, ulong discordId, ulong channelId)
        {
            ValorantChannelMappingsExtension.RemoveRow(new ValorantChannelMappings(puuid, channelId));
            ValorantUsersExtension.DeleteRow(puuid, discordId);
        }

        #endregion

        #region Tests

        [DataTestMethod]
        [DataRow(FailureMode.Http404, DisplayName = "Matches endpoint HTTP 404: no post, recovered on next tick")]
        [DataRow(FailureMode.Http429, DisplayName = "Matches endpoint HTTP 429: no post, recovered on next tick")]
        [DataRow(FailureMode.Http500, DisplayName = "Matches endpoint HTTP 500: no post, recovered on next tick")]
        [DataRow(FailureMode.Http503, DisplayName = "Matches endpoint HTTP 503: no post, recovered on next tick")]
        [DataRow(FailureMode.TaskCanceled, DisplayName = "Matches endpoint timeout (TaskCanceledException): no post, recovered on next tick")]
        [DataRow(FailureMode.HttpRequestException, DisplayName = "Matches endpoint network error (HttpRequestException): no post, recovered on next tick")]
        [DataRow(FailureMode.MalformedJson, DisplayName = "Matches endpoint malformed JSON: no post, recovered on next tick")]
        [DataRow(FailureMode.EmptyData, DisplayName = "Matches endpoint empty data: no post, recovered on next tick")]
        [DataRow(FailureMode.NullData, DisplayName = "Matches endpoint null data: no post, recovered on next tick")]
        public async Task PollTick_MatchesEndpointFailure_DoesNotPostAndRecovers(FailureMode failureMode)
        {
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero));
            DateTime matchTime = new(2026, 11, 23, 11, 30, 0, DateTimeKind.Utc);
            var (fixtureJson, matchId, _) = LoadRewrittenSwiftplayFixture(matchTime);

            try
            {
                SeedUser(EthanPuuid, "Ehtan", "NA1", "na", EthanDiscordId, TestChannelId);

                var handler = new ApiFailureHttpMessageHandler
                {
                    MatchesFailure = failureMode,
                    HealthyMatchesJson = fixtureJson
                };
                var (program, notifier) = CreateProgram(handler, clock);

                // Tick 1: Matches endpoint fails
                await program.RunPollTickAsync();

                Assert.AreEqual(0, notifier.Sent.Count, "No Discord post should be made when matches endpoint fails");
                Assert.IsFalse(MatchesExtension.MatchIdExists(matchId), "Matches row should not exist in SQLite after failure");
                Assert.IsFalse(MatchStatsExtension.MatchIdExistsForUser(matchId, EthanPuuid), "MatchStats row should not exist in SQLite after failure");

                // Tick 2: Recovery (API healthy)
                handler.MatchesFailure = null;
                await program.RunPollTickAsync();

                Assert.AreEqual(1, notifier.Sent.Count, "Match should be posted after matches endpoint recovers");
                Assert.AreEqual(TestChannelId, notifier.Sent[0].ChannelId);
                Assert.AreEqual($"<@{EthanDiscordId}>", notifier.Sent[0].Text);
                Assert.IsTrue(MatchesExtension.MatchIdExists(matchId), "Matches row should exist in SQLite after recovery");
                Assert.IsTrue(MatchStatsExtension.MatchIdExistsForUser(matchId, EthanPuuid), "MatchStats row should exist for user after recovery");
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, TestChannelId);
            }
        }

        [DataTestMethod]
        [DataRow(FailureMode.Http404, DisplayName = "MMR-history endpoint HTTP 404: no post, recovered on next tick")]
        [DataRow(FailureMode.Http429, DisplayName = "MMR-history endpoint HTTP 429: no post, recovered on next tick")]
        [DataRow(FailureMode.Http500, DisplayName = "MMR-history endpoint HTTP 500: no post, recovered on next tick")]
        [DataRow(FailureMode.Http503, DisplayName = "MMR-history endpoint HTTP 503: no post, recovered on next tick")]
        [DataRow(FailureMode.TaskCanceled, DisplayName = "MMR-history endpoint timeout (TaskCanceledException): no post, recovered on next tick")]
        [DataRow(FailureMode.HttpRequestException, DisplayName = "MMR-history endpoint network error (HttpRequestException): no post, recovered on next tick")]
        [DataRow(FailureMode.MalformedJson, DisplayName = "MMR-history endpoint malformed JSON: no post, recovered on next tick")]
        [DataRow(FailureMode.EmptyData, DisplayName = "MMR-history endpoint empty data: no post, recovered on next tick")]
        [DataRow(FailureMode.NullData, DisplayName = "MMR-history endpoint null data: no post, recovered on next tick")]
        public async Task PollTick_MmrHistoryEndpointFailure_DoesNotPostAndRecovers(FailureMode failureMode)
        {
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero));
            DateTime matchTime = new(2026, 11, 23, 11, 30, 0, DateTimeKind.Utc);
            var (matchesJson, matchId, _) = LoadRewrittenCompMatchFixture(matchTime);
            string mmrHistoryJson = LoadRewrittenMmrHistoryFixture(matchId, newTier: 22, rrChange: 18);

            try
            {
                SeedUser(EthanPuuid, "Ehtan", "NA1", "na", EthanDiscordId, TestChannelId);

                var handler = new ApiFailureHttpMessageHandler
                {
                    HealthyMatchesJson = matchesJson,
                    MmrHistoryFailure = failureMode,
                    HealthyMmrHistoryJson = mmrHistoryJson
                };
                var (program, notifier) = CreateProgram(handler, clock);

                // Tick 1: MMR-history fails on a competitive match
                await program.RunPollTickAsync();

                Assert.AreEqual(0, notifier.Sent.Count, "No Discord post should be made when mmr-history fails on a competitive match");
                Assert.IsFalse(MatchesExtension.MatchIdExists(matchId), "Matches row should not exist in SQLite after mmr-history failure");
                Assert.IsFalse(MatchStatsExtension.MatchIdExistsForUser(matchId, EthanPuuid), "MatchStats row should not exist in SQLite after mmr-history failure");

                // Tick 2: Recovery (API healthy)
                handler.MmrHistoryFailure = null;
                await program.RunPollTickAsync();

                Assert.AreEqual(1, notifier.Sent.Count, "Competitive match should be posted after mmr-history recovers");
                Assert.AreEqual(TestChannelId, notifier.Sent[0].ChannelId);
                Assert.AreEqual($"<@{EthanDiscordId}>", notifier.Sent[0].Text);
                Assert.IsTrue(MatchesExtension.MatchIdExists(matchId), "Matches row should exist in SQLite after recovery");
                Assert.IsTrue(MatchStatsExtension.MatchIdExistsForUser(matchId, EthanPuuid), "MatchStats row should exist for user after recovery");
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, TestChannelId);
            }
        }

        [TestMethod]
        public async Task PollTick_OneUserFailsOneUserSucceeds_HealthyUserIsPosted()
        {
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero));
            DateTime matchTime = new(2026, 11, 23, 11, 30, 0, DateTimeKind.Utc);
            var (fixtureJson, matchId, _) = LoadRewrittenSwiftplayFixture(matchTime);

            const string failingPuuid = "99999999-9999-9999-9999-999999999999";
            const ulong failingDiscordId = 999888777UL;

            try
            {
                SeedUser(EthanPuuid, "Ehtan", "NA1", "na", EthanDiscordId, TestChannelId);
                SeedUser(failingPuuid, "FailUser", "NA1", "na", failingDiscordId, TestChannelId);

                var handler = new ApiFailureHttpMessageHandler
                {
                    HealthyMatchesJson = fixtureJson
                };
                handler.PerUserMatchesFailures[failingPuuid] = FailureMode.Http500;

                var (program, notifier) = CreateProgram(handler, clock);

                await program.RunPollTickAsync();

                // Both users were polled in this tick
                Assert.IsTrue(handler.RequestedUrls.Any(u => u.Contains(failingPuuid)), "Failing user should have been polled");
                Assert.IsTrue(handler.RequestedUrls.Any(u => u.Contains(EthanPuuid)), "Healthy user should have been polled");

                // Exactly one recap embed sent, and it belongs to the healthy user
                Assert.AreEqual(1, notifier.Sent.Count, "Exactly one recap embed should be sent for the healthy user");
                Assert.AreEqual(TestChannelId, notifier.Sent[0].ChannelId);
                Assert.AreEqual($"<@{EthanDiscordId}>", notifier.Sent[0].Text);
                Assert.IsFalse(notifier.Sent.Any(s => s.Text != null && s.Text.Contains(failingDiscordId.ToString())), "No Discord message should mention failing user");

                // Healthy user match is stored; failing user has no match stats
                Assert.IsTrue(MatchesExtension.MatchIdExists(matchId), "Match row should exist for healthy user");
                Assert.IsTrue(MatchStatsExtension.MatchIdExistsForUser(matchId, EthanPuuid), "MatchStats row should exist for healthy user");
                Assert.IsFalse(MatchStatsExtension.MatchIdExistsForUser(matchId, failingPuuid), "MatchStats row should not exist for failing user");
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, TestChannelId);
                CleanUser(failingPuuid, failingDiscordId, TestChannelId);
            }
        }

        #endregion
    }
}
