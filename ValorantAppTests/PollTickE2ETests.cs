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
    /// Deterministic end-to-end tests driving BaseValorantProgram through poll ticks.
    /// Uses a stubbed HenrikDev HTTP client, temp SQLite DB, and RecordingNotifier.
    /// Note: These tests share static DB state (SQLite connection), so MSTest runs them sequentially.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class PollTickE2ETests
    {
        private const string EthanPuuid = "8c7276bd-a70b-56bf-803b-7e0fd9dba287";
        private const string OreoZPuuid = "1a13d7a1-d988-5e78-80e8-1cdd6531bcb3";
        private const ulong TestChannelId = 987654321UL;
        private const ulong EthanDiscordId = 111222333UL;
        private const ulong OreoZDiscordId = 444555666UL;

        #region Helpers

        private static (string Json, string MatchId, long Epoch) LoadRewrittenFixture(DateTime matchTime)
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

        private class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly string _matchesJson;
            public List<string> RequestedUrls { get; } = new();

            public StubHttpMessageHandler(string matchesJson)
            {
                _matchesJson = matchesJson;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri?.ToString() ?? string.Empty;
                RequestedUrls.Add(url);

                if (url.Contains("v3/by-puuid/matches", StringComparison.OrdinalIgnoreCase))
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(_matchesJson, System.Text.Encoding.UTF8, "application/json")
                    };
                    return Task.FromResult(response);
                }

                // Any other endpoint (e.g. mmr-history): HenrikApi tolerates 404 cleanly by returning null
                var notFound = new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{\"status\":404,\"message\":\"Not Found\"}", System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(notFound);
            }
        }

        private static (BaseValorantProgram Program, RecordingNotifier Notifier, StubHttpMessageHandler Handler) CreateProgram(string matchesJson)
        {
            var handler = new StubHttpMessageHandler(matchesJson);
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.henrikdev.xyz/valorant/")
            };

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(x => x.CreateClient("HenrikApiClient")).Returns(httpClient);

            var notifier = new RecordingNotifier();
            var program = new BaseValorantProgram(notifier, factoryMock.Object, NullLogger<BaseValorantProgram>.Instance);
            return (program, notifier, handler);
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

        [TestMethod]
        public async Task PollTick_NewMatch_StoresRowsAndPostsOneRecap()
        {
            DateTime matchTime = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, DateTime.UtcNow.Minute, 0, DateTimeKind.Utc);
            var (fixtureJson, matchId, epoch) = LoadRewrittenFixture(matchTime);

            SeedUser(EthanPuuid, "Ehtan", "NA1", "na", EthanDiscordId, TestChannelId);

            try
            {
                var (program, notifier, handler) = CreateProgram(fixtureJson);

                await program.RunPollTickAsync();

                // DB state assertions
                Assert.IsTrue(MatchesExtension.MatchIdExists(matchId), "Matches row should exist in SQLite");
                Assert.IsTrue(MatchStatsExtension.MatchIdExistsForUser(matchId, EthanPuuid), "MatchStats row should exist for user");

                // Outbound notification assertions
                Assert.AreEqual(1, notifier.Sent.Count, "Exactly one embed should be sent");
                var sent = notifier.Sent[0];
                Assert.AreEqual(TestChannelId, sent.ChannelId);
                Assert.AreEqual($"<@{EthanDiscordId}>", sent.Text);

                // Golden values characterization lock
                var embed = sent.Embed;
                // NOTE: possible bug: ModesEnum.cs:37 uses Description("SwiftPlay") with capital 'P', whereas API returns "Swiftplay"
                Assert.AreEqual("SwiftPlay - Ascent", embed.Title);
                Assert.IsNull(embed.Author);
                Assert.AreEqual($"https://tracker.gg/valorant/match/{matchId}", embed.Url);
                Assert.AreEqual(Color.Red, embed.Color);
                Assert.AreEqual("https://static.wikia.nocookie.net/valorant/images/3/36/Reyna_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202820", embed.Thumbnail?.Url);

                Assert.AreEqual(2, embed.Fields.Length);
                Assert.AreEqual("<:reyna:1251243990582300712> Ehtan | <:unr:1214734089165475850>", embed.Fields[0].Name);
                Assert.AreEqual("Combat Score: 227, K/D/A: 6/6/1\nHeadshot: 63.64%, RR: 0", embed.Fields[0].Value);

                Assert.AreEqual($"__{new string(' ', 40)}__\n\nMatch Stats", embed.Fields[1].Name);
                // NOTE: possible bug: BaseValorantProgram.cs:795-800 compares matchStats.Team to "blue", ordering rounds Red : Blue for Red team
                Assert.AreEqual($"<t:{epoch}:f>, 11 minutes\nRounds 3 : 5\nAverage Ranks <:unr:1214734089165475850> : <:unr:1214734089165475850>", embed.Fields[1].Value);

                // HTTP endpoints requested
                Assert.IsTrue(handler.RequestedUrls.Any(u => u.Contains($"v3/by-puuid/matches/na/{EthanPuuid}")), "Expected matches endpoint request");
                Assert.IsTrue(handler.RequestedUrls.Any(u => u.Contains($"v1/by-puuid/mmr-history/na/{EthanPuuid}")), "Expected mmr-history endpoint request");
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, TestChannelId);
            }
        }

        [TestMethod]
        public async Task PollTick_SameMatchTwice_PostsOnlyOnce()
        {
            DateTime matchTime = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, DateTime.UtcNow.Minute, 0, DateTimeKind.Utc);
            var (fixtureJson, matchId, _) = LoadRewrittenFixture(matchTime);

            SeedUser(EthanPuuid, "Ehtan", "NA1", "na", EthanDiscordId, TestChannelId);

            try
            {
                var (program, notifier, handler) = CreateProgram(fixtureJson);
                int MatchesRequests() => handler.RequestedUrls.Count(u => u.Contains("v3/by-puuid/matches", StringComparison.OrdinalIgnoreCase));

                // Tick 1 processes the match and re-enqueues user in finally block
                await program.RunPollTickAsync();
                Assert.AreEqual(1, notifier.Sent.Count);
                Assert.AreEqual(1, MatchesRequests());

                // Tick 2 polls the user again, sees match already in DB, and skips it
                await program.RunPollTickAsync();
                Assert.AreEqual(2, MatchesRequests(), "Tick 2 must actually poll the user again, otherwise this test proves nothing");
                Assert.AreEqual(1, notifier.Sent.Count, "Second poll tick must not re-post the match");
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, TestChannelId);
            }
        }

        [TestMethod]
        public async Task PollTick_TwoTrackedUsersInSameMatch_PostsOneCombinedRecap()
        {
            DateTime matchTime = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, DateTime.UtcNow.Minute, 0, DateTimeKind.Utc);
            var (fixtureJson, matchId, epoch) = LoadRewrittenFixture(matchTime);

            SeedUser(EthanPuuid, "Ehtan", "NA1", "na", EthanDiscordId, TestChannelId);
            SeedUser(OreoZPuuid, "iiTzOreoZ", "6642", "na", OreoZDiscordId, TestChannelId);

            try
            {
                var (program, notifier, _) = CreateProgram(fixtureJson);

                await program.RunPollTickAsync();

                Assert.IsTrue(MatchesExtension.MatchIdExists(matchId));
                Assert.IsTrue(MatchStatsExtension.MatchIdExistsForUser(matchId, EthanPuuid));
                Assert.IsTrue(MatchStatsExtension.MatchIdExistsForUser(matchId, OreoZPuuid));

                // SendMultipleInMatch sends exactly ONE combined recap embed
                Assert.AreEqual(1, notifier.Sent.Count, "Exactly one combined embed should be posted");
                var sent = notifier.Sent[0];
                Assert.AreEqual(TestChannelId, sent.ChannelId);
                StringAssert.Contains(sent.Text, $"<@{EthanDiscordId}>");
                StringAssert.Contains(sent.Text, $"<@{OreoZDiscordId}>");

                var embed = sent.Embed;
                Assert.AreEqual("SwiftPlay - Ascent", embed.Title);
                // Combined embed contains 2 player fields (sorted by score descending: OreoZ first, Ethan second) + 1 match info field
                Assert.AreEqual(3, embed.Fields.Length);
                StringAssert.Contains(embed.Fields[0].Name, "iiTzOreoZ");
                StringAssert.Contains(embed.Fields[1].Name, "Ehtan");
                Assert.AreEqual($"__{new string(' ', 40)}__\n\nMatch Stats", embed.Fields[2].Name);
                Assert.AreEqual($"<t:{epoch}:f>, 11 minutes\nRounds 3 : 5\nAverage Ranks <:unr:1214734089165475850> : <:unr:1214734089165475850>", embed.Fields[2].Value);
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, TestChannelId);
                CleanUser(OreoZPuuid, OreoZDiscordId, TestChannelId);
            }
        }

        #endregion
    }
}
