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
    /// Deterministic end-to-end tests characterizing how match recaps are routed
    /// to Discord channels and how tracked users rotate across consecutive poll ticks.
    /// Uses a stubbed HenrikDev HTTP client, temp SQLite DB, and RecordingNotifier.
    /// Note: These tests share static DB state (SQLite connection), so MSTest runs them sequentially.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class RoutingE2ETests
    {
        private const string EthanPuuid = "8c7276bd-a70b-56bf-803b-7e0fd9dba287";
        private const string OreoZPuuid = "1a13d7a1-d988-5e78-80e8-1cdd6531bcb3";
        private const ulong EthanDiscordId = 111222333UL;
        private const ulong OreoZDiscordId = 444555666UL;

        private const ulong ChannelAlpha = 100000001UL;
        private const ulong ChannelBeta = 200000002UL;

        // Fixed clock so recency checks are deterministic (the match is 30 minutes old).
        private static readonly FixedTimeProvider Clock = new(new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero));
        private static readonly DateTime MatchTime = new(2026, 11, 23, 11, 30, 0, DateTimeKind.Utc);

        #region Helpers

        private static (string Json, string MatchId, long Epoch) LoadRewrittenFixture(DateTime matchTime, string? overrideMatchId = null)
        {
            string path = File.Exists(Path.Combine("../../../MockJsonResponse", "Matches_10_31_2023.json"))
                ? Path.Combine("../../../MockJsonResponse", "Matches_10_31_2023.json")
                : Path.Combine(AppContext.BaseDirectory, "../../../MockJsonResponse", "Matches_10_31_2023.json");

            string raw = File.ReadAllText(path);
            var root = JObject.Parse(raw);
            var metadata = root["data"]![0]!["metadata"]!;
            string matchId = overrideMatchId ?? metadata["matchid"]!.ToString();
            metadata["matchid"] = matchId;

            long epoch = new DateTimeOffset(matchTime).ToUnixTimeSeconds();
            string gameStartPatched = matchTime.ToString("dddd, MMMM d, yyyy h:mm tt", CultureInfo.InvariantCulture);

            metadata["game_start"] = epoch;
            metadata["game_start_patched"] = gameStartPatched;

            return (root.ToString(Newtonsoft.Json.Formatting.None), matchId, epoch);
        }

        private class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly string? _defaultMatchesJson;
            private readonly Func<HttpRequestMessage, HttpResponseMessage?>? _responseProvider;
            public List<string> RequestedUrls { get; } = new();
            public Action<HttpRequestMessage>? OnRequest { get; set; }

            public StubHttpMessageHandler(string? defaultMatchesJson = null, Func<HttpRequestMessage, HttpResponseMessage?>? responseProvider = null)
            {
                _defaultMatchesJson = defaultMatchesJson;
                _responseProvider = responseProvider;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri?.ToString() ?? string.Empty;
                lock (RequestedUrls)
                {
                    RequestedUrls.Add(url);
                }

                OnRequest?.Invoke(request);

                if (_responseProvider != null)
                {
                    var customResponse = _responseProvider(request);
                    if (customResponse != null)
                    {
                        return Task.FromResult(customResponse);
                    }
                }

                if (url.Contains("v3/by-puuid/matches", StringComparison.OrdinalIgnoreCase))
                {
                    string content = _defaultMatchesJson ?? "{\"status\":200,\"data\":[]}";
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
                    };
                    return Task.FromResult(response);
                }

                var notFound = new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{\"status\":404,\"message\":\"Not Found\"}", System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(notFound);
            }
        }

        private static (BaseValorantProgram Program, RecordingNotifier Notifier, StubHttpMessageHandler Handler) CreateProgram(
            string? matchesJson = null,
            TimeProvider? timeProvider = null,
            Func<HttpRequestMessage, HttpResponseMessage?>? responseProvider = null,
            Action<HttpRequestMessage>? onRequest = null)
        {
            var handler = new StubHttpMessageHandler(matchesJson, responseProvider)
            {
                OnRequest = onRequest
            };
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.henrikdev.xyz/valorant/")
            };

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(x => x.CreateClient("HenrikApiClient")).Returns(httpClient);

            var notifier = new RecordingNotifier();
            var program = new BaseValorantProgram(notifier, factoryMock.Object, NullLogger<BaseValorantProgram>.Instance, timeProvider ?? Clock);
            return (program, notifier, handler);
        }

        private static void SeedUser(string puuid, string username, string tag, string affinity, ulong discordId, params ulong[] channelIds)
        {
            ValorantUsersExtension.InsertRow(new ValorantUsers(username, tag, affinity, puuid, discordId));
            foreach (ulong ch in channelIds)
            {
                ValorantChannelMappingsExtension.InsertRow(new ValorantChannelMappings(puuid, ch));
            }
        }

        private static void CleanUser(string puuid, ulong discordId, params ulong[] channelIds)
        {
            foreach (ulong ch in channelIds)
            {
                ValorantChannelMappingsExtension.RemoveRow(new ValorantChannelMappings(puuid, ch));
            }
            ValorantUsersExtension.DeleteRow(puuid, discordId);
        }

        private static string ExtractPuuidFromMatchesUrl(string url)
        {
            const string marker = "v3/by-puuid/matches/";
            int markerIndex = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0) return string.Empty;
            string afterMarker = url.Substring(markerIndex + marker.Length);
            int slashIndex = afterMarker.IndexOf('/');
            if (slashIndex < 0) return string.Empty;
            string afterAffinity = afterMarker.Substring(slashIndex + 1);
            int questionIndex = afterAffinity.IndexOf('?');
            return questionIndex >= 0 ? afterAffinity.Substring(0, questionIndex) : afterAffinity;
        }

        #endregion

        #region Routing Tests

        [TestMethod]
        public async Task PollTick_OneUserTrackedInTwoChannels_PostsRecapToBothChannelsAndStoresMatchOnce()
        {
            DateTime matchTime = MatchTime;
            var (fixtureJson, matchId, _) = LoadRewrittenFixture(matchTime);

            try
            {
                SeedUser(EthanPuuid, "Ehtan", "NA1", "na", EthanDiscordId, ChannelAlpha, ChannelBeta);

                var (program, notifier, _) = CreateProgram(fixtureJson);

                await program.RunPollTickAsync();

                // DB state assertions: match stored once
                Assert.IsTrue(MatchesExtension.MatchIdExists(matchId), "Matches row should exist in SQLite");
                Assert.IsTrue(MatchStatsExtension.MatchIdExistsForUser(matchId, EthanPuuid), "MatchStats row should exist for user");

                // Outbound notification assertions: recap posted to both channels
                Assert.AreEqual(2, notifier.Sent.Count, "Recap should be posted to both channels");

                var sentAlpha = notifier.Sent.SingleOrDefault(s => s.ChannelId == ChannelAlpha);
                Assert.IsNotNull(sentAlpha, "Recap should be sent to ChannelAlpha");
                Assert.AreEqual($"<@{EthanDiscordId}>", sentAlpha.Text);
                Assert.AreEqual("SwiftPlay - Ascent", sentAlpha.Embed.Title);

                var sentBeta = notifier.Sent.SingleOrDefault(s => s.ChannelId == ChannelBeta);
                Assert.IsNotNull(sentBeta, "Recap should be sent to ChannelBeta");
                Assert.AreEqual($"<@{EthanDiscordId}>", sentBeta.Text);
                Assert.AreEqual("SwiftPlay - Ascent", sentBeta.Embed.Title);
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, ChannelAlpha, ChannelBeta);
            }
        }

        [TestMethod]
        public async Task PollTick_TwoUsersInSameMatchTrackedInDifferentChannels_EachChannelGetsContentForItsUserOnly()
        {
            DateTime matchTime = MatchTime;
            var (fixtureJson, matchId, _) = LoadRewrittenFixture(matchTime);

            try
            {
                SeedUser(EthanPuuid, "Ehtan", "NA1", "na", EthanDiscordId, ChannelAlpha);
                SeedUser(OreoZPuuid, "iiTzOreoZ", "6642", "na", OreoZDiscordId, ChannelBeta);

                var (program, notifier, _) = CreateProgram(fixtureJson);

                await program.RunPollTickAsync();

                // DB state assertions: match stored once, stats for both users
                Assert.IsTrue(MatchesExtension.MatchIdExists(matchId), "Matches row should exist in SQLite");
                Assert.IsTrue(MatchStatsExtension.MatchIdExistsForUser(matchId, EthanPuuid), "MatchStats row should exist for Ethan");
                Assert.IsTrue(MatchStatsExtension.MatchIdExistsForUser(matchId, OreoZPuuid), "MatchStats row should exist for OreoZ");

                // Exactly one recap per channel
                Assert.AreEqual(2, notifier.Sent.Count, "Exactly 2 messages should be sent (one per channel)");

                // ChannelAlpha recap has Ethan only
                var sentAlpha = notifier.Sent.Single(s => s.ChannelId == ChannelAlpha);
                Assert.AreEqual($"<@{EthanDiscordId}>", sentAlpha.Text);
                Assert.AreEqual(2, sentAlpha.Embed.Fields.Length, "ChannelAlpha recap should have 1 player field and 1 match stats field");
                StringAssert.Contains(sentAlpha.Embed.Fields[0].Name, "Ehtan");
                Assert.IsFalse(sentAlpha.Embed.Fields.Any(f => f.Name.Contains("iiTzOreoZ")), "ChannelAlpha must not include OreoZ");

                // ChannelBeta recap has OreoZ only
                var sentBeta = notifier.Sent.Single(s => s.ChannelId == ChannelBeta);
                Assert.AreEqual($"<@{OreoZDiscordId}>", sentBeta.Text);
                Assert.AreEqual(2, sentBeta.Embed.Fields.Length, "ChannelBeta recap should have 1 player field and 1 match stats field");
                StringAssert.Contains(sentBeta.Embed.Fields[0].Name, "iiTzOreoZ");
                Assert.IsFalse(sentBeta.Embed.Fields.Any(f => f.Name.Contains("Ehtan")), "ChannelBeta must not include Ethan");
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, ChannelAlpha);
                CleanUser(OreoZPuuid, OreoZDiscordId, ChannelBeta);
            }
        }

        [TestMethod]
        public async Task PollTick_MultipleUsersInSameMatchInOneChannel_PostsExactlyOneCombinedRecap()
        {
            DateTime matchTime = MatchTime;
            var (fixtureJson, matchId, _) = LoadRewrittenFixture(matchTime);

            try
            {
                SeedUser(EthanPuuid, "Ehtan", "NA1", "na", EthanDiscordId, ChannelAlpha);
                SeedUser(OreoZPuuid, "iiTzOreoZ", "6642", "na", OreoZDiscordId, ChannelAlpha);

                var (program, notifier, _) = CreateProgram(fixtureJson);

                await program.RunPollTickAsync();

                // DB state assertions
                Assert.IsTrue(MatchesExtension.MatchIdExists(matchId));
                Assert.IsTrue(MatchStatsExtension.MatchIdExistsForUser(matchId, EthanPuuid));
                Assert.IsTrue(MatchStatsExtension.MatchIdExistsForUser(matchId, OreoZPuuid));

                // Exactly one post for that channel, not one per user
                Assert.AreEqual(1, notifier.Sent.Count, "Exactly one combined post should be sent for ChannelAlpha");
                var sent = notifier.Sent[0];
                Assert.AreEqual(ChannelAlpha, sent.ChannelId);

                // Both users mentioned
                StringAssert.Contains(sent.Text, $"<@{EthanDiscordId}>");
                StringAssert.Contains(sent.Text, $"<@{OreoZDiscordId}>");

                // 2 player fields + 1 match stats field
                Assert.AreEqual(3, sent.Embed.Fields.Length);
                Assert.IsTrue(sent.Embed.Fields.Any(f => f.Name.Contains("Ehtan")));
                Assert.IsTrue(sent.Embed.Fields.Any(f => f.Name.Contains("iiTzOreoZ")));
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, ChannelAlpha);
                CleanUser(OreoZPuuid, OreoZDiscordId, ChannelAlpha);
            }
        }

        [TestMethod]
        public async Task PollTick_UserWithNoChannelMappings_StoresMatchAndPostsNothing()
        {
            DateTime matchTime = MatchTime;
            var (fixtureJson, matchId, _) = LoadRewrittenFixture(matchTime);

            // User seeded in ValorantUsers table with NO rows in ValorantChannelMappings
            ValorantUsersExtension.InsertRow(new ValorantUsers("Ehtan", "NA1", "na", EthanPuuid, EthanDiscordId));

            try
            {
                var (program, notifier, _) = CreateProgram(fixtureJson);

                await program.RunPollTickAsync();

                // Match and match stats are stored
                Assert.IsTrue(MatchesExtension.MatchIdExists(matchId), "Match should still be stored in DB");
                Assert.IsTrue(MatchStatsExtension.MatchIdExistsForUser(matchId, EthanPuuid), "MatchStats should be stored for user");

                // But 0 posts sent since user has no channels
                Assert.AreEqual(0, notifier.Sent.Count, "Nothing should be posted when user has no mapped channels");
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                ValorantUsersExtension.DeleteRow(EthanPuuid, EthanDiscordId);
            }
        }

        #endregion

        #region User Rotation Tests (16+ tracked users)

        [TestMethod]
        public async Task PollTick_SixteenTrackedUsers_RotatesAcrossConsecutiveTicksWithoutStarvation()
        {
            // Seed 16 users: capacity per tick is 15 users, so 1 user overflows per tick.
            const int userCount = 16;
            var seededUsers = new List<ValorantUsers>();
            for (int i = 0; i < userCount; i++)
            {
                var user = new ValorantUsers($"RotUser{i:D2}", "NA1", "na", $"rot-puuid-{i:D2}", 1000UL + (ulong)i);
                seededUsers.Add(user);
                ValorantUsersExtension.InsertRow(user);
                ValorantChannelMappingsExtension.InsertRow(new ValorantChannelMappings(user.Val_puuid, ChannelAlpha));
            }

            var allPuuids = seededUsers.Select(u => u.Val_puuid).ToHashSet();

            try
            {
                var (program, _, handler) = CreateProgram();

                // Tick 1: dequeues 15 users
                await program.RunPollTickAsync();

                var tick1Urls = handler.RequestedUrls.Where(u => u.Contains("v3/by-puuid/matches")).ToList();
                var tick1Puuids = tick1Urls.Select(ExtractPuuidFromMatchesUrl).ToHashSet();

                Assert.AreEqual(15, tick1Puuids.Count, "Tick 1 should poll exactly 15 users (batch size)");
                var skippedOnTick1 = allPuuids.Except(tick1Puuids).Single();

                // Tick 2: dequeues next 15 users (the 1 overflow user from tick 1 + 14 users from tick 1)
                await program.RunPollTickAsync();

                var tick2Urls = handler.RequestedUrls.Skip(tick1Urls.Count).Where(u => u.Contains("v3/by-puuid/matches")).ToList();
                var tick2Puuids = tick2Urls.Select(ExtractPuuidFromMatchesUrl).ToHashSet();

                Assert.AreEqual(15, tick2Puuids.Count, "Tick 2 should poll exactly 15 users");

                // Assert that the user skipped on tick 1 was polled on tick 2 (no user starved)
                Assert.IsTrue(tick2Puuids.Contains(skippedOnTick1), $"User {skippedOnTick1} skipped on tick 1 must be polled on tick 2 (no starvation)");

                // Exactly 1 user skipped on tick 2 (the 15th user from tick 1)
                var skippedOnTick2 = allPuuids.Except(tick2Puuids).Single();
                Assert.AreNotEqual(skippedOnTick1, skippedOnTick2, "Different user should be skipped on tick 2");
                Assert.IsTrue(tick1Puuids.Contains(skippedOnTick2), "The user skipped on tick 2 was already polled on tick 1 (not polled twice in a row unnecessarily)");

                // Across tick 1 and tick 2, all 16 users have been polled
                var polledAcrossBothTicks = tick1Puuids.Union(tick2Puuids).ToHashSet();
                Assert.AreEqual(16, polledAcrossBothTicks.Count, "All 16 users should be polled across 2 consecutive ticks");
                CollectionAssert.AreEquivalent(allPuuids.ToList(), polledAcrossBothTicks.ToList());
            }
            finally
            {
                foreach (var user in seededUsers)
                {
                    ValorantChannelMappingsExtension.RemoveRow(new ValorantChannelMappings(user.Val_puuid, ChannelAlpha));
                    ValorantUsersExtension.DeleteRow(user.Val_puuid, user.Disc_id);
                }
            }
        }

        [TestMethod]
        public async Task PollTick_ThirtyTrackedUsers_RotatesDisjointBatchesAcrossConsecutiveTicks()
        {
            // Seed 30 users: exactly 2 full batches of 15
            const int userCount = 30;
            var seededUsers = new List<ValorantUsers>();
            for (int i = 0; i < userCount; i++)
            {
                var user = new ValorantUsers($"Rot30User{i:D2}", "NA1", "na", $"rot30-puuid-{i:D2}", 2000UL + (ulong)i);
                seededUsers.Add(user);
                ValorantUsersExtension.InsertRow(user);
                ValorantChannelMappingsExtension.InsertRow(new ValorantChannelMappings(user.Val_puuid, ChannelAlpha));
            }

            var allPuuids = seededUsers.Select(u => u.Val_puuid).ToHashSet();

            try
            {
                var (program, _, handler) = CreateProgram();

                // Tick 1: dequeues batch 1 (15 users)
                await program.RunPollTickAsync();

                var tick1Urls = handler.RequestedUrls.Where(u => u.Contains("v3/by-puuid/matches")).ToList();
                var tick1Puuids = tick1Urls.Select(ExtractPuuidFromMatchesUrl).ToHashSet();
                Assert.AreEqual(15, tick1Puuids.Count, "Tick 1 must poll exactly 15 users");

                // Tick 2: dequeues batch 2 (the other 15 users)
                await program.RunPollTickAsync();

                var tick2Urls = handler.RequestedUrls.Skip(tick1Urls.Count).Where(u => u.Contains("v3/by-puuid/matches")).ToList();
                var tick2Puuids = tick2Urls.Select(ExtractPuuidFromMatchesUrl).ToHashSet();
                Assert.AreEqual(15, tick2Puuids.Count, "Tick 2 must poll exactly 15 users");

                // Disjoint batches: no user polled twice in a row
                var overlap = tick1Puuids.Intersect(tick2Puuids).ToList();
                Assert.AreEqual(0, overlap.Count, "With 30 users, tick 1 and tick 2 batches should be completely disjoint (no duplicate polling)");

                // Every user polled across the two ticks: zero starvation
                var combined = tick1Puuids.Union(tick2Puuids).ToHashSet();
                Assert.AreEqual(30, combined.Count, "All 30 users should be polled across 2 ticks");
                CollectionAssert.AreEquivalent(allPuuids.ToList(), combined.ToList());
            }
            finally
            {
                foreach (var user in seededUsers)
                {
                    ValorantChannelMappingsExtension.RemoveRow(new ValorantChannelMappings(user.Val_puuid, ChannelAlpha));
                    ValorantUsersExtension.DeleteRow(user.Val_puuid, user.Disc_id);
                }
            }
        }

        #endregion

        #region User Deletion and Unlinking Tests

        [TestMethod]
        public async Task PollTick_UserDeletedBetweenTicks_ContinuesCleanlyWithoutPostForDeletedUser()
        {
            DateTime matchTime = MatchTime;
            var (fixtureJson, matchId, _) = LoadRewrittenFixture(matchTime);

            try
            {
                SeedUser(EthanPuuid, "Ehtan", "NA1", "na", EthanDiscordId, ChannelAlpha);
                SeedUser(OreoZPuuid, "iiTzOreoZ", "6642", "na", OreoZDiscordId, ChannelBeta);

                var (program, notifier, _) = CreateProgram(fixtureJson);

                // Tick 1: both users are tracked and polled; recaps sent
                await program.RunPollTickAsync();
                Assert.AreEqual(2, notifier.Sent.Count);

                notifier.Sent.Clear();
                // Forget match so it would be posted again if tracked
                DevToolsExtension.DeleteMatchEverywhere(matchId);

                // Delete Ethan between ticks
                bool deleted = program.DeleteUser(EthanPuuid);
                Assert.IsTrue(deleted, "DeleteUser should succeed");

                // Tick 2: Ethan deleted, OreoZ remains
                await program.RunPollTickAsync();

                Assert.AreEqual(1, notifier.Sent.Count, "Only OreoZ should receive a recap on tick 2");
                var sent = notifier.Sent[0];
                Assert.AreEqual(ChannelBeta, sent.ChannelId);
                Assert.AreEqual($"<@{OreoZDiscordId}>", sent.Text);
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, ChannelAlpha);
                CleanUser(OreoZPuuid, OreoZDiscordId, ChannelBeta);
            }
        }

        [TestMethod]
        public async Task PollTick_UserUnlinkedBetweenTicks_PostsOnlyToRemainingChannel()
        {
            DateTime matchTime = MatchTime;
            var (fixtureJson, matchId, _) = LoadRewrittenFixture(matchTime);

            try
            {
                SeedUser(EthanPuuid, "Ehtan", "NA1", "na", EthanDiscordId, ChannelAlpha, ChannelBeta);

                var (program, notifier, _) = CreateProgram(fixtureJson);

                // Unlink Ethan from ChannelBeta before the tick
                var user = program.GetValorantUser(EthanPuuid);
                Assert.IsNotNull(user);
                bool unlinked = user.RemoveChannelId(ChannelBeta);
                Assert.IsTrue(unlinked, "RemoveChannelId should succeed");

                await program.RunPollTickAsync();

                Assert.AreEqual(1, notifier.Sent.Count, "Recap should be sent to ChannelAlpha only");
                Assert.AreEqual(ChannelAlpha, notifier.Sent[0].ChannelId);
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, ChannelAlpha, ChannelBeta);
            }
        }

        [TestMethod]
        public async Task PollTick_UserDeletedMidTick_DoesNotCrashAndOtherUsersContinue()
        {
            DateTime matchTime = MatchTime;
            var (fixtureJson, matchId, _) = LoadRewrittenFixture(matchTime);

            try
            {
                SeedUser(EthanPuuid, "Ehtan", "NA1", "na", EthanDiscordId, ChannelAlpha);
                SeedUser(OreoZPuuid, "iiTzOreoZ", "6642", "na", OreoZDiscordId, ChannelBeta);

                BaseValorantProgram? programRef = null;
                Task<bool>? deleteTask = null;

                var (program, notifier, _) = CreateProgram(
                    matchesJson: fixtureJson,
                    onRequest: request =>
                    {
                        string url = request.RequestUri?.ToString() ?? string.Empty;
                        if (deleteTask != null || !url.Contains(EthanPuuid, StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        // Simulate /unlink arriving from the Discord gateway thread while the tick is fetching matches.
                        // It must run on its own thread: DeleteUser -> ReloadFromDB -> EnqueueAllUsers waits for
                        // QueueUsersLock, which the tick holds while it blocks on these HTTP calls. Calling it inline here
                        // deadlocks the test (not possible in prod, where the command thread just waits for the tick).
                        deleteTask = Task.Run(() => programRef!.DeleteUser(EthanPuuid));

                        // ReloadUsers() runs before the lock, so wait until the user map has been rebuilt without Ethan.
                        // This makes the interleaving deterministic: the rest of the tick sees the post-delete user map.
                        // (Users.Clear() drops everyone first, so also wait for OreoZ to be re-created.)
                        SpinWait.SpinUntil(
                            () => programRef!.GetValorantUser(EthanPuuid) == null && programRef.GetValorantUser(OreoZPuuid) != null,
                            TimeSpan.FromSeconds(5));
                    });

                programRef = program;

                await program.RunPollTickAsync();

                Assert.IsNotNull(deleteTask, "Ethan's matches request should have triggered the delete");
                Assert.IsTrue(deleteTask!.Wait(TimeSpan.FromSeconds(5)), "DeleteUser should finish once the tick releases QueueUsersLock");
                Assert.IsTrue(deleteTask.Result, "DeleteUser should report success");

                // OreoZ continues cleanly and gets their recap; Ethan gets nothing.
                Assert.AreEqual(1, notifier.Sent.Count, "OreoZ's recap should be posted even if Ethan was deleted mid-tick");
                var sent = notifier.Sent[0];
                Assert.AreEqual(ChannelBeta, sent.ChannelId);
                Assert.AreEqual($"<@{OreoZDiscordId}>", sent.Text);
                Assert.IsNull(program.GetValorantUser(EthanPuuid), "Ethan should be gone from the program after the delete");
                Assert.IsNull(ValorantUsersExtension.GetRow(EthanPuuid), "Ethan should be gone from SQLite after the delete");
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, ChannelAlpha);
                CleanUser(OreoZPuuid, OreoZDiscordId, ChannelBeta);
            }
        }

        [TestMethod]
        public async Task PollTick_UserUnlinkedMidTick_DoesNotCrashAndExcludesUnlinkedUser()
        {
            DateTime matchTime = MatchTime;
            var (fixtureJson, matchId, _) = LoadRewrittenFixture(matchTime);

            // Both users mapped to ChannelAlpha
            try
            {
                SeedUser(EthanPuuid, "Ehtan", "NA1", "na", EthanDiscordId, ChannelAlpha);
                SeedUser(OreoZPuuid, "iiTzOreoZ", "6642", "na", OreoZDiscordId, ChannelAlpha);

                BaseValorantProgram? programRef = null;

                var (program, notifier, _) = CreateProgram(
                    matchesJson: fixtureJson,
                    onRequest: request =>
                    {
                        string url = request.RequestUri?.ToString() ?? string.Empty;
                        // Mid-tick: unlink OreoZ from ChannelAlpha
                        if (url.Contains(EthanPuuid, StringComparison.OrdinalIgnoreCase))
                        {
                            programRef?.GetValorantUser(OreoZPuuid)?.RemoveChannelId(ChannelAlpha);
                        }
                    });

                programRef = program;

                await program.RunPollTickAsync();

                // Exactly one recap should be posted to ChannelAlpha, containing Ethan only
                Assert.AreEqual(1, notifier.Sent.Count, "Exactly 1 post should be sent to ChannelAlpha");
                var sent = notifier.Sent[0];
                Assert.AreEqual(ChannelAlpha, sent.ChannelId);
                Assert.AreEqual($"<@{EthanDiscordId}>", sent.Text, "Recap should mention Ethan only (OreoZ was unlinked mid-tick)");
                Assert.AreEqual(2, sent.Embed.Fields.Length, "Recap should have 1 player field for Ethan + 1 match stats field");
                StringAssert.Contains(sent.Embed.Fields[0].Name, "Ehtan");
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, ChannelAlpha);
                CleanUser(OreoZPuuid, OreoZDiscordId, ChannelAlpha);
            }
        }

        #endregion
    }
}
