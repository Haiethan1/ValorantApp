using Discord;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using ValorantApp;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;
using ValorantApp.HenrikJson;
using ValorantApp.Valorant;

namespace ValorantAppTests
{
    /// <summary>
    /// Deterministic end-to-end tests driving BaseValorantProgram through daily report jobs.
    /// Uses temp SQLite DB, RecordingNotifier, and injected FixedTimeProvider.
    /// Note: These tests share static DB state (SQLite connection), so MSTest runs them sequentially.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class DailyReportE2ETests
    {
        private const string EthanPuuid = "8c7276bd-a70b-56bf-803b-7e0fd9dba287";
        private const ulong TestChannelId = 987654321UL;
        private const ulong EthanDiscordId = 111222333UL;

        #region Helpers

        private static (Matches Match, MatchStats Stats, string MatchId) CreateTestMatch(
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
            // Format with seconds so second-level boundary tests are preserved by DateTime.TryParse
            string gameStartPatched = matchTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            metadata["game_start"] = epoch;
            metadata["game_start_patched"] = gameStartPatched;

            var matchJson = root["data"]![0]!.ToObject<MatchJson>()!;
            var mmr = new MmrHistoryJson { Mmr_change_to_last_game = rrChange, Currenttier = newTier };

            var matches = MatchesExtension.CreateFromJson(matchJson)!;
            var stats = MatchStatsExtension.CreateFromJson(matchJson, mmr, EthanPuuid)!;

            return (matches, stats, matchId);
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

        private static (BaseValorantProgram Program, RecordingNotifier Notifier) CreateProgram(TimeProvider timeProvider)
        {
            var notifier = new RecordingNotifier();
            var factoryMock = new Mock<IHttpClientFactory>();
            var program = new BaseValorantProgram(notifier, factoryMock.Object, NullLogger<BaseValorantProgram>.Instance, timeProvider);
            return (program, notifier);
        }

        #endregion

        #region Tests

        [TestMethod]
        public async Task DailyReport_MatchInsideWindow_PostsSummaryToChannel()
        {
            // Clock on 2026-11-23 (not an act end date). Window: 2026-11-22 11:00:01 to 2026-11-23 11:00:00 UTC.
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 23, 11, 30, 0, TimeSpan.Zero));
            DateTime matchTime = new(2026, 11, 23, 5, 0, 0, DateTimeKind.Utc);
            var (match, stats, matchId) = CreateTestMatch(matchTime);

            try
            {
                SeedUser();
                MatchesExtension.InsertRow(match);
                MatchStatsExtension.InsertRow(stats);

                var (program, notifier) = CreateProgram(clock);

                await program.RunDailyCheckAsync();

                Assert.AreEqual(1, notifier.Sent.Count, "Exactly one daily report embed should be sent");
                var sent = notifier.Sent[0];
                Assert.AreEqual(TestChannelId, sent.ChannelId);
                Assert.IsNull(sent.Text);

                var embed = sent.Embed;
                Assert.AreEqual("Daily Report Summary - Page 1", embed.Title);
                Assert.AreEqual("Let's see who played today..", embed.Description);
                Assert.AreEqual(Color.DarkBlue, embed.Color);

                Assert.AreEqual(1, embed.Fields.Length);
                Assert.AreEqual("Ehtan#NA1 <:asc2:1214384834936569898> :arrow_right: <:dia2:1214384335063490620> +18 RR", embed.Fields[0].Name);
                Assert.AreEqual("Matches: 1 | Minutes: 31 | Aces: 0\nK/D/A: 19/15/13 | Most Played: <:skye:1251244001063993366>\nHeadshot: 42.5% | Bodyshot: 52.5%", embed.Fields[0].Value);

                Assert.AreEqual(1, notifier.Statuses.Count);
                // NOTE: possible bug: BaseValorantProgram.UpdateDiscordBotConfig pluralizes serverCount backwards: (serverCount == 1 ? 's' : string.Empty) -> "1 Servers"
                Assert.AreEqual("Matches Found 1 | 1 Servers | Events Processed (New) 0", notifier.Statuses[0]);
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser();
            }
        }

        [TestMethod]
        public async Task DailyReport_MatchOutsideWindow_PostsNothing()
        {
            // Clock on 2026-11-23. Window: 2026-11-22 11:00:01 to 2026-11-23 11:00:00 UTC.
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 23, 11, 30, 0, TimeSpan.Zero));
            // Match 1: 1 second before window start (start + 0 s: 11:00:00)
            DateTime beforeStart = new(2026, 11, 22, 11, 0, 0, DateTimeKind.Utc);
            var (match1, stats1, id1) = CreateTestMatch(beforeStart, "outside-before-start");

            // Match 2: 1 second after window end (11:00:01)
            DateTime afterEnd = new(2026, 11, 23, 11, 0, 1, DateTimeKind.Utc);
            var (match2, stats2, id2) = CreateTestMatch(afterEnd, "outside-after-end");

            try
            {
                SeedUser();
                MatchesExtension.InsertRow(match1);
                MatchStatsExtension.InsertRow(stats1);
                MatchesExtension.InsertRow(match2);
                MatchStatsExtension.InsertRow(stats2);

                var (program, notifier) = CreateProgram(clock);

                await program.RunDailyCheckAsync();

                Assert.AreEqual(0, notifier.Sent.Count, "Matches outside the window must not trigger report posts");
                Assert.AreEqual(1, notifier.Statuses.Count, "Status must still be updated even when no report is sent");
                Assert.AreEqual("Matches Found 2 | 1 Servers | Events Processed (New) 0", notifier.Statuses[0]);
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(id1);
                DevToolsExtension.DeleteMatchEverywhere(id2);
                CleanUser();
            }
        }

        [DataTestMethod]
        [DataRow("2026-11-22 11:00:00", false, DisplayName = "start + 0 s (just before window): excluded")]
        [DataRow("2026-11-22 11:00:01", true, DisplayName = "start + 1 s (exact window start): included")]
        [DataRow("2026-11-23 11:00:00", true, DisplayName = "end (exact window end): included")]
        [DataRow("2026-11-23 11:00:01", false, DisplayName = "end + 1 s (just after window): excluded")]
        public async Task DailyReport_WindowBoundaries_ProbesExactSeconds(string matchTimeStr, bool expectIncluded)
        {
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 23, 11, 30, 0, TimeSpan.Zero));
            DateTime matchTime = DateTime.ParseExact(matchTimeStr, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            var (match, stats, matchId) = CreateTestMatch(matchTime, $"boundary-{matchTime:yyyyMMddHHmmss}");

            try
            {
                SeedUser();
                MatchesExtension.InsertRow(match);
                MatchStatsExtension.InsertRow(stats);

                var (program, notifier) = CreateProgram(clock);

                await program.RunDailyCheckAsync();

                Assert.AreEqual(expectIncluded ? 1 : 0, notifier.Sent.Count, $"Match at {matchTimeStr} inclusion mismatch");
                Assert.AreEqual(1, notifier.Statuses.Count);
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser();
            }
        }

        [TestMethod]
        public async Task DailyReport_OnActEndDate_PostsActReport()
        {
            // E7A3 in EpisodeActInfos: Start 2023-10-31 00:00:00 UTC, End 2024-01-09 00:00:00 UTC
            // On 2024-01-09 11:00:00 UTC, GetEpisodeActInfosForEndDate returns E7A3
            var clock = new FixedTimeProvider(new DateTimeOffset(2024, 1, 9, 11, 0, 0, TimeSpan.Zero));
            DateTime matchTime = new(2023, 11, 4, 20, 0, 0, DateTimeKind.Utc);
            var (match, stats, matchId) = CreateTestMatch(matchTime, "act-report-match");

            try
            {
                SeedUser();
                MatchesExtension.InsertRow(match);
                MatchStatsExtension.InsertRow(stats);

                var (program, notifier) = CreateProgram(clock);

                await program.RunDailyCheckAsync();

                Assert.AreEqual(1, notifier.Sent.Count, "Act end date should trigger an act report embed");
                var sent = notifier.Sent[0];
                Assert.AreEqual(TestChannelId, sent.ChannelId);

                var embed = sent.Embed;
                Assert.AreEqual("Episode 7 Act 3 Report Summary - Page 1", embed.Title);
                Assert.AreEqual("Congratulations on another Episode/Act finished!\nGood luck on the next split!", embed.Description);
                Assert.AreEqual(Color.DarkBlue, embed.Color);

                Assert.AreEqual(1, embed.Fields.Length);
                Assert.AreEqual(1, notifier.Statuses.Count);
                Assert.AreEqual("Matches Found 1 | 1 Servers | Events Processed (New) 0", notifier.Statuses[0]);
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
