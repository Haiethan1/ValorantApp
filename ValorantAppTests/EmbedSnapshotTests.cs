using Discord;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ValorantApp;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;
using ValorantApp.HenrikJson;
using ValorantApp.Valorant;

namespace ValorantAppTests
{
    /// <summary>
    /// Snapshot (approval-style) tests of full Discord embeds across all primary bot notification scenarios.
    /// Runs with an injected FixedTimeProvider and deterministic fixtures.
    /// Run with VALORANTAPP_UPDATE_SNAPSHOTS=1 to create or update approved snapshots.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class EmbedSnapshotTests
    {
        private const string EthanPuuid = "8c7276bd-a70b-56bf-803b-7e0fd9dba287";
        private const string OreoZPuuid = "1a13d7a1-d988-5e78-80e8-1cdd6531bcb3";
        private const ulong TestChannelId = 987654321UL;
        private const ulong EthanDiscordId = 111222333UL;
        private const ulong OreoZDiscordId = 444555666UL;

        #region Helpers

        private static (string Json, string MatchId, long Epoch) LoadRewrittenNonCompFixture(DateTime matchTime)
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

        private static string LoadRewrittenMmrHistoryFixture(string matchId, int newTier, int rrChange)
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

        private static (Matches Match, MatchStats Stats, string MatchId) CreateTestDailyMatch(
            DateTime matchTime,
            string? customMatchId = null,
            int rrChange = 18,
            int newTier = 19)
        {
            string path = File.Exists(Path.Combine("../../../MockJsonResponse", "Matches_11_4_2023.json"))
                ? Path.Combine("../../../MockJsonResponse", "Matches_11_4_2023.json")
                : Path.Combine(AppContext.BaseDirectory, "../../../MockJsonResponse", "Matches_11_4_2023.json");

            string raw = File.ReadAllText(path);
            var root = JObject.Parse(raw);
            var metadata = root["data"]![0]!["metadata"]!;
            string matchId = customMatchId ?? metadata["matchid"]!.ToString();
            if (customMatchId != null)
            {
                metadata["matchid"] = customMatchId;
            }

            long epoch = new DateTimeOffset(matchTime).ToUnixTimeSeconds();
            string gameStartPatched = matchTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            metadata["game_start"] = epoch;
            metadata["game_start_patched"] = gameStartPatched;

            var matchJson = root["data"]![0]!.ToObject<MatchJson>()!;
            var mmr = new MmrHistoryJson { Mmr_change_to_last_game = rrChange, Currenttier = newTier };

            var matches = MatchesExtension.CreateFromJson(matchJson)!;
            var stats = MatchStatsExtension.CreateFromJson(matchJson, mmr, EthanPuuid)!;

            return (matches, stats, matchId);
        }

        private class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly string _matchesJson;
            private readonly string? _mmrHistoryJson;

            public StubHttpMessageHandler(string matchesJson, string? mmrHistoryJson = null)
            {
                _matchesJson = matchesJson;
                _mmrHistoryJson = mmrHistoryJson;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri?.ToString() ?? string.Empty;

                if (url.Contains("v3/by-puuid/matches", StringComparison.OrdinalIgnoreCase))
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(_matchesJson, System.Text.Encoding.UTF8, "application/json")
                    };
                    return Task.FromResult(response);
                }

                if (_mmrHistoryJson != null && url.Contains("v1/by-puuid/mmr-history", StringComparison.OrdinalIgnoreCase))
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(_mmrHistoryJson, System.Text.Encoding.UTF8, "application/json")
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

        private static (BaseValorantProgram Program, RecordingNotifier Notifier) CreateProgram(
            string matchesJson,
            string? mmrHistoryJson = null,
            TimeProvider? timeProvider = null)
        {
            var handler = new StubHttpMessageHandler(matchesJson, mmrHistoryJson);
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

        private static (BaseValorantProgram Program, RecordingNotifier Notifier) CreateReportProgram(TimeProvider timeProvider)
        {
            var notifier = new RecordingNotifier();
            var factoryMock = new Mock<IHttpClientFactory>();
            var program = new BaseValorantProgram(notifier, factoryMock.Object, NullLogger<BaseValorantProgram>.Instance, timeProvider);
            return (program, notifier);
        }

        private static void SeedUser(string puuid = EthanPuuid, string username = "Ehtan", string tag = "NA1", string affinity = "na", ulong discordId = EthanDiscordId, ulong channelId = TestChannelId)
        {
            ValorantUsersExtension.InsertRow(new ValorantUsers(username, tag, affinity, puuid, discordId));
            ValorantChannelMappingsExtension.InsertRow(new ValorantChannelMappings(puuid, channelId));
        }

        private static void CleanUser(string puuid = EthanPuuid, ulong discordId = EthanDiscordId, ulong channelId = TestChannelId)
        {
            ValorantChannelMappingsExtension.RemoveRow(new ValorantChannelMappings(puuid, channelId));
            ValorantUsersExtension.DeleteRow(puuid, discordId);
        }

        #endregion

        #region Scenarios

        [TestMethod]
        public async Task NonCompetitiveRecap_OnePlayer_MatchesSnapshot()
        {
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero));
            DateTime matchTime = new(2026, 11, 23, 11, 30, 0, DateTimeKind.Utc);
            var (fixtureJson, matchId, _) = LoadRewrittenNonCompFixture(matchTime);

            SeedUser(EthanPuuid, "Ehtan", "NA1", "na", EthanDiscordId, TestChannelId);

            try
            {
                var (program, notifier) = CreateProgram(fixtureJson, timeProvider: clock);

                await program.RunPollTickAsync();

                Assert.AreEqual(1, notifier.Sent.Count, "Expected exactly 1 recap embed.");
                SnapshotVerifier.Verify("non_competitive_recap_one_player", notifier.Sent[0].Embed);
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, TestChannelId);
            }
        }

        [TestMethod]
        public async Task TwoTrackedPlayersCombinedRecap_MatchesSnapshot()
        {
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero));
            DateTime matchTime = new(2026, 11, 23, 11, 30, 0, DateTimeKind.Utc);
            var (fixtureJson, matchId, _) = LoadRewrittenNonCompFixture(matchTime);

            SeedUser(EthanPuuid, "Ehtan", "NA1", "na", EthanDiscordId, TestChannelId);
            SeedUser(OreoZPuuid, "iiTzOreoZ", "6642", "na", OreoZDiscordId, TestChannelId);

            try
            {
                var (program, notifier) = CreateProgram(fixtureJson, timeProvider: clock);

                await program.RunPollTickAsync();

                Assert.AreEqual(1, notifier.Sent.Count, "Expected exactly 1 combined recap embed.");
                SnapshotVerifier.Verify("two_tracked_players_combined_recap", notifier.Sent[0].Embed);
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, TestChannelId);
                CleanUser(OreoZPuuid, OreoZDiscordId, TestChannelId);
            }
        }

        [TestMethod]
        public async Task CompetitiveRecap_MatchesSnapshot()
        {
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero));
            DateTime matchTime = new(2026, 11, 23, 11, 30, 0, DateTimeKind.Utc);
            var (matchesJson, matchId, _) = LoadRewrittenCompMatchFixture(matchTime);
            string mmrHistoryJson = LoadRewrittenMmrHistoryFixture(matchId, newTier: 22, rrChange: 18);

            SeedUser(EthanPuuid, "Ehtan", "NA1", "na", EthanDiscordId, TestChannelId);

            try
            {
                var (program, notifier) = CreateProgram(matchesJson, mmrHistoryJson, clock);

                await program.RunPollTickAsync();

                Assert.AreEqual(1, notifier.Sent.Count, "Expected exactly 1 competitive recap embed.");
                SnapshotVerifier.Verify("competitive_recap", notifier.Sent[0].Embed);
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, TestChannelId);
            }
        }

        [TestMethod]
        public async Task CompetitivePromotion_TierChange_MatchesSnapshot()
        {
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero));
            DateTime matchTime = new(2026, 11, 23, 11, 30, 0, DateTimeKind.Utc);
            var (matchesJson, matchId, _) = LoadRewrittenCompMatchFixture(matchTime);
            // CurrentTier in fixture is 22; promotion to 23
            string mmrHistoryJson = LoadRewrittenMmrHistoryFixture(matchId, newTier: 23, rrChange: 21);

            SeedUser(EthanPuuid, "Ehtan", "NA1", "na", EthanDiscordId, TestChannelId);

            try
            {
                var (program, notifier) = CreateProgram(matchesJson, mmrHistoryJson, clock);

                await program.RunPollTickAsync();

                Assert.AreEqual(2, notifier.Sent.Count, "Expected recap and tier-change embeds.");
                SnapshotVerifier.Verify("competitive_promotion_tier_change", notifier.Sent[1].Embed);
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, TestChannelId);
            }
        }

        [TestMethod]
        public async Task CompetitiveDemotion_TierChange_MatchesSnapshot()
        {
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero));
            DateTime matchTime = new(2026, 11, 23, 11, 30, 0, DateTimeKind.Utc);
            var (matchesJson, matchId, _) = LoadRewrittenCompMatchFixture(matchTime);
            // CurrentTier in fixture is 22; demotion to 21
            string mmrHistoryJson = LoadRewrittenMmrHistoryFixture(matchId, newTier: 21, rrChange: -19);

            SeedUser(EthanPuuid, "Ehtan", "NA1", "na", EthanDiscordId, TestChannelId);

            try
            {
                var (program, notifier) = CreateProgram(matchesJson, mmrHistoryJson, clock);

                await program.RunPollTickAsync();

                Assert.AreEqual(2, notifier.Sent.Count, "Expected recap and tier-change embeds.");
                SnapshotVerifier.Verify("competitive_demotion_tier_change", notifier.Sent[1].Embed);
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, TestChannelId);
            }
        }

        [TestMethod]
        public async Task DailyReport_MatchesSnapshot()
        {
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 23, 11, 30, 0, TimeSpan.Zero));
            DateTime matchTime = new(2026, 11, 23, 5, 0, 0, DateTimeKind.Utc);
            var (match, stats, matchId) = CreateTestDailyMatch(matchTime);

            SeedUser();
            MatchesExtension.InsertRow(match);
            MatchStatsExtension.InsertRow(stats);

            try
            {
                var (program, notifier) = CreateReportProgram(clock);

                await program.RunDailyCheckAsync();

                Assert.AreEqual(1, notifier.Sent.Count, "Expected exactly 1 daily report embed.");
                SnapshotVerifier.Verify("daily_report", notifier.Sent[0].Embed);
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser();
            }
        }

        [TestMethod]
        public async Task ActEndReport_MatchesSnapshot()
        {
            var clock = new FixedTimeProvider(new DateTimeOffset(2024, 1, 9, 11, 0, 0, TimeSpan.Zero));
            DateTime matchTime = new(2023, 11, 4, 20, 0, 0, DateTimeKind.Utc);
            var (match, stats, matchId) = CreateTestDailyMatch(matchTime, "act-report-match");

            SeedUser();
            MatchesExtension.InsertRow(match);
            MatchStatsExtension.InsertRow(stats);

            try
            {
                var (program, notifier) = CreateReportProgram(clock);

                await program.RunDailyCheckAsync();

                Assert.AreEqual(1, notifier.Sent.Count, "Expected exactly 1 act-end report embed.");
                SnapshotVerifier.Verify("act_end_report", notifier.Sent[0].Embed);
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser();
            }
        }

        #endregion
    }
}
