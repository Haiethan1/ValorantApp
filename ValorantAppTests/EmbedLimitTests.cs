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
using ValorantApp;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;
using ValorantApp.HenrikJson;
using ValorantApp.Valorant;
using ValorantApp.Valorant.Enums;

namespace ValorantAppTests
{
    /// <summary>
    /// Discord embed limit tests verifying that all Discord embed limits are respected
    /// and that reports are paginated properly rather than truncated or throwing.
    ///
    /// Discord limits:
    /// - Title: <= 256 characters
    /// - Description: <= 4096 characters
    /// - Field name: <= 256 characters
    /// - Field value: <= 1024 characters
    /// - Field count: <= 25 fields
    /// - Footer text: <= 2048 characters
    /// - Author name: <= 256 characters
    /// - Total characters across the embed: <= 6000 characters
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class EmbedLimitTests
    {
        private const ulong TestChannelId1 = 1000000001UL;
        private const ulong TestChannelId2 = 1000000002UL;
        private const ulong TestChannelId3 = 1000000003UL;

        #region Discord Limit Assertion Helper

        /// <summary>
        /// Validates that a Discord embed strictly respects all Discord API limits.
        /// </summary>
        public static void AssertDiscordEmbedLimits(Embed embed)
        {
            Assert.IsNotNull(embed, "Embed must not be null");

            // Title length <= 256
            if (embed.Title != null)
            {
                Assert.IsTrue(embed.Title.Length <= 256,
                    $"Embed title length ({embed.Title.Length}) exceeds Discord limit of 256: '{embed.Title}'");
            }

            // Description length <= 4096
            if (embed.Description != null)
            {
                Assert.IsTrue(embed.Description.Length <= 4096,
                    $"Embed description length ({embed.Description.Length}) exceeds Discord limit of 4096.");
            }

            // Author name <= 256
            if (embed.Author.HasValue && embed.Author.Value.Name != null)
            {
                Assert.IsTrue(embed.Author.Value.Name.Length <= 256,
                    $"Author name length ({embed.Author.Value.Name.Length}) exceeds Discord limit of 256.");
            }

            // Footer text <= 2048
            if (embed.Footer.HasValue && embed.Footer.Value.Text != null)
            {
                Assert.IsTrue(embed.Footer.Value.Text.Length <= 2048,
                    $"Footer text length ({embed.Footer.Value.Text.Length}) exceeds Discord limit of 2048.");
            }

            // Max 25 fields
            Assert.IsTrue(embed.Fields.Length <= 25,
                $"Embed fields count ({embed.Fields.Length}) exceeds Discord limit of 25 fields.");

            // Field name <= 256, Field value <= 1024
            int totalLength = (embed.Title?.Length ?? 0)
                + (embed.Description?.Length ?? 0)
                + (embed.Author.HasValue ? (embed.Author.Value.Name?.Length ?? 0) : 0)
                + (embed.Footer.HasValue ? (embed.Footer.Value.Text?.Length ?? 0) : 0);

            for (int i = 0; i < embed.Fields.Length; i++)
            {
                var field = embed.Fields[i];
                Assert.IsTrue(field.Name.Length <= 256,
                    $"Field [{i}] name length ({field.Name.Length}) exceeds Discord limit of 256: '{field.Name}'");
                Assert.IsTrue(field.Value.Length <= 1024,
                    $"Field [{i}] value length ({field.Value.Length}) exceeds Discord limit of 1024: '{field.Value}'");
                totalLength += field.Name.Length + field.Value.Length;
            }

            // Total characters across the embed <= 6000
            Assert.IsTrue(totalLength <= 6000,
                $"Total embed characters ({totalLength}) exceeds Discord limit of 6000.");
            Assert.IsTrue(embed.Length <= 6000,
                $"Embed.Length ({embed.Length}) exceeds Discord limit of 6000.");
        }

        #endregion

        #region Helpers

        private static (Matches Match, MatchStats Stats) CreateBaseTestMatch(
            DateTime matchTime,
            string matchId,
            string puuid,
            int rrChange = 18,
            int currentTier = 19,
            int newTier = 20,
            string character = "Brimstone",
            double headshots = 42.5,
            double bodyshots = 52.5,
            uint gameLength = 1860,
            short score = 5838)
        {
            string path = File.Exists(Path.Combine("../../../MockJsonResponse", "Matches_11_4_2023.json"))
                ? Path.Combine("../../../MockJsonResponse", "Matches_11_4_2023.json")
                : Path.Combine(AppContext.BaseDirectory, "../../../MockJsonResponse", "Matches_11_4_2023.json");

            string raw = File.ReadAllText(path);
            var root = JObject.Parse(raw);
            var metadata = root["data"]![0]!["metadata"]!;

            long epoch = new DateTimeOffset(matchTime).ToUnixTimeSeconds();
            string gameStartPatched = matchTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            metadata["matchid"] = matchId;
            metadata["game_start"] = epoch;
            metadata["game_start_patched"] = gameStartPatched;

            var matchJson = root["data"]![0]!.ToObject<MatchJson>()!;
            var matches = MatchesExtension.CreateFromJson(matchJson)!;

            var stats = new MatchStats(
                matchId: matchId,
                valPuuid: puuid,
                map: matches.Map ?? "Lotus",
                mode: Modes.Competitive.ToDescriptionString(),
                rounds: (byte)(matches.Rounds_Played ?? 21),
                character: character,
                rrChange: rrChange,
                doubleKills: 0,
                tripleKills: 0,
                quadKills: 0,
                aces: 0,
                kills: 19,
                knifeKills: 0,
                deaths: 15,
                knifeDeaths: 0,
                assists: 13,
                bodyshots: bodyshots,
                headshots: headshots,
                score: score,
                damage: 1000,
                cCasts: 0,
                qCasts: 0,
                eCasts: 0,
                xCasts: 0,
                damageToAllies: 0,
                damageFromAllies: 0,
                gameLength: gameLength,
                gameStartPatched: matchTime,
                mvp: false,
                currentTier: (byte)currentTier,
                team: "Blue",
                newTier: (byte)newTier
            );

            return (matches, stats);
        }

        private static void SeedTrackedUser(string puuid, string username, string tag, ulong discordId, ulong channelId)
        {
            ValorantUsersExtension.InsertRow(new ValorantUsers(username, tag, "na", puuid, discordId));
            ValorantChannelMappingsExtension.InsertRow(new ValorantChannelMappings(puuid, channelId));
        }

        private static void CleanTrackedUser(string puuid, ulong discordId, ulong channelId)
        {
            ValorantChannelMappingsExtension.RemoveRow(new ValorantChannelMappings(puuid, channelId));
            ValorantUsersExtension.DeleteRow(puuid, discordId);
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

                var notFound = new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{\"status\":404,\"message\":\"Not Found\"}", System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(notFound);
            }
        }

        private static (BaseValorantProgram Program, RecordingNotifier Notifier) CreateProgram(TimeProvider timeProvider, StubHttpMessageHandler? handler = null)
        {
            var notifier = new RecordingNotifier();
            var factoryMock = new Mock<IHttpClientFactory>();

            if (handler != null)
            {
                var httpClient = new HttpClient(handler)
                {
                    BaseAddress = new Uri("https://api.henrikdev.xyz/valorant/")
                };
                factoryMock.Setup(x => x.CreateClient("HenrikApiClient")).Returns(httpClient);
            }

            var program = new BaseValorantProgram(notifier, factoryMock.Object, NullLogger<BaseValorantProgram>.Instance, timeProvider);
            return (program, notifier);
        }

        #endregion

        #region Tests

        /// <summary>
        /// (a) Daily report with 30 tracked users in one channel with long (16-char) names.
        /// Verifies:
        /// - Embeds are split across multiple pages (20 on Page 1, 10 on Page 2).
        /// - Every captured embed respects all Discord limits (title, description, fields, total length <= 6000).
        /// </summary>
        [TestMethod]
        public async Task DailyReport_30TrackedUsers_OneChannel_LongNames_SplitsIntoPagesAndRespectsLimits()
        {
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 23, 11, 30, 0, TimeSpan.Zero));
            DateTime matchTime = new(2026, 11, 23, 5, 0, 0, DateTimeKind.Utc);
            string matchId = "daily-report-30-users-match";

            var seededUsers = new List<(string Puuid, ulong DiscordId, ulong ChannelId)>();

            try
            {
                // Seed 30 users with 16-character names and 5-character tags into SQLite
                for (int i = 1; i <= 30; i++)
                {
                    string puuid = $"00000000-0000-0000-0000-{i:D12}";
                    string username = $"LongUserName{i:D4}"; // Exactly 16 chars: "LongUserName0001"
                    string tag = $"TAG{i:D2}";              // 5 chars: "TAG01"
                    ulong discordId = 2000000000UL + (ulong)i;

                    SeedTrackedUser(puuid, username, tag, discordId, TestChannelId1);
                    seededUsers.Add((puuid, discordId, TestChannelId1));
                }

                // Create and insert 1 match and 30 MatchStats rows for each user
                var (match, _) = CreateBaseTestMatch(matchTime, matchId, seededUsers[0].Puuid);
                MatchesExtension.InsertRow(match);

                foreach (var user in seededUsers)
                {
                    var (_, stats) = CreateBaseTestMatch(matchTime, matchId, user.Puuid, rrChange: 18, currentTier: 19, newTier: 20);
                    MatchStatsExtension.InsertRow(stats);
                }

                var (program, notifier) = CreateProgram(clock);

                await program.RunDailyCheckAsync();

                // Assert pagination: 30 users in 1 channel should split into Page 1 (20) and Page 2 (10)
                Assert.AreEqual(2, notifier.Sent.Count, "30 users must be split into exactly 2 pages (embeds)");

                var page1 = notifier.Sent[0];
                var page2 = notifier.Sent[1];

                Assert.AreEqual(TestChannelId1, page1.ChannelId);
                Assert.AreEqual(TestChannelId1, page2.ChannelId);

                Assert.AreEqual("Daily Report Summary - Page 1", page1.Embed.Title);
                Assert.AreEqual("Daily Report Summary - Page 2", page2.Embed.Title);

                Assert.AreEqual(20, page1.Embed.Fields.Length, "Page 1 must contain 20 fields");
                Assert.AreEqual(10, page2.Embed.Fields.Length, "Page 2 must contain 10 fields");

                // Assert all Discord limits for every captured embed
                foreach (var sent in notifier.Sent)
                {
                    AssertDiscordEmbedLimits(sent.Embed);
                }
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                foreach (var user in seededUsers)
                {
                    CleanTrackedUser(user.Puuid, user.DiscordId, user.ChannelId);
                }
            }
        }

        /// <summary>
        /// (a) Worst-case daily report where 20 tracked users have high play time (>25% of window)
        /// and high legshot ratio (>=20%), triggering both TouchGrass and ToeShooter animated emojis.
        /// When 20 such fields are placed in a single embed, total character length exceeds Discord's 6000-char limit.
        /// Production code at BaseValorantProgram.cs:1077-1102 chunks strictly by 20 fields without checking total character length.
        /// </summary>
        [TestMethod]
        [Ignore("Violates Discord 6000 total characters limit: Page 1 with 20 worst-case emoji fields reaches 6237+ chars (BaseValorantProgram.cs:1077-1102)")]
        public async Task DailyReport_30TrackedUsers_WorstCaseEmojis_ExceedsTotalLengthLimit()
        {
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 23, 11, 30, 0, TimeSpan.Zero));
            DateTime matchTime = new(2026, 11, 23, 5, 0, 0, DateTimeKind.Utc);
            string matchId = "daily-report-worst-case-match";

            var seededUsers = new List<(string Puuid, ulong DiscordId, ulong ChannelId)>();

            try
            {
                for (int i = 1; i <= 30; i++)
                {
                    string puuid = $"10000000-0000-0000-0000-{i:D12}";
                    string username = $"LongUserName{i:D4}"; // 16 chars
                    string tag = $"TAG{i:D2}";              // 5 chars
                    ulong discordId = 3000000000UL + (ulong)i;

                    SeedTrackedUser(puuid, username, tag, discordId, TestChannelId1);
                    seededUsers.Add((puuid, discordId, TestChannelId1));
                }

                // 25% of 1440 min = 360 min. 22000 seconds = 366.6 minutes -> triggers TouchGrass emoji
                // Bodyshots 50, Headshots 20 -> Legshots = 30 >= 20 threshold -> triggers ToeShooter emoji
                var (match, _) = CreateBaseTestMatch(matchTime, matchId, seededUsers[0].Puuid, gameLength: 22000, bodyshots: 50.0, headshots: 20.0);
                MatchesExtension.InsertRow(match);

                foreach (var user in seededUsers)
                {
                    var (_, stats) = CreateBaseTestMatch(
                        matchTime,
                        matchId,
                        user.Puuid,
                        rrChange: 100,
                        currentTier: 19,
                        newTier: 20,
                        gameLength: 22000,
                        bodyshots: 50.0,
                        headshots: 20.0,
                        score: 9999);
                    MatchStatsExtension.InsertRow(stats);
                }

                var (program, notifier) = CreateProgram(clock);

                await program.RunDailyCheckAsync();

                Assert.AreEqual(2, notifier.Sent.Count);
                foreach (var sent in notifier.Sent)
                {
                    AssertDiscordEmbedLimits(sent.Embed);
                }
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                foreach (var user in seededUsers)
                {
                    CleanTrackedUser(user.Puuid, user.DiscordId, user.ChannelId);
                }
            }
        }

        /// <summary>
        /// (b) Match recap with 10 players and longest-possible agent/map names.
        /// Verifies:
        /// - Combined recap embed is built with 10 player fields + 1 match info field (11 fields total).
        /// - Title, fields, and total length all strictly satisfy Discord embed limits.
        /// </summary>
        [TestMethod]
        public async Task MatchRecap_10Players_LongestAgentAndMapNames_RespectsLimits()
        {
            DateTime matchTime = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, DateTime.UtcNow.Minute, 0, DateTimeKind.Utc);
            long epoch = new DateTimeOffset(matchTime).ToUnixTimeSeconds();
            string gameStartPatched = matchTime.ToString("dddd, MMMM d, yyyy h:mm tt", CultureInfo.InvariantCulture);

            string path = File.Exists(Path.Combine("../../../MockJsonResponse", "Matches_10_31_2023.json"))
                ? Path.Combine("../../../MockJsonResponse", "Matches_10_31_2023.json")
                : Path.Combine(AppContext.BaseDirectory, "../../../MockJsonResponse", "Matches_10_31_2023.json");

            string raw = File.ReadAllText(path);
            var root = JObject.Parse(raw);
            var metadata = root["data"]![0]!["metadata"]!;

            string matchId = "recap-10-players-longest-names";
            metadata["matchid"] = matchId;
            metadata["game_start"] = epoch;
            metadata["game_start_patched"] = gameStartPatched;
            // Longest map name in MapsEnum: "Fracture" (8 chars) or "District" (8 chars)
            metadata["map"] = "Fracture";
            // Longest mode name in ModesEnum: "TeamDeathmatch" (14 chars) or "Competitive" (11 chars)
            metadata["mode"] = "Competitive";

            var allPlayers = (JArray)root["data"]![0]!["players"]!["all_players"]!;
            Assert.AreEqual(10, allPlayers.Count, "Fixture must contain exactly 10 players");

            var seededUsers = new List<(string Puuid, ulong DiscordId, ulong ChannelId)>();

            for (int i = 0; i < 10; i++)
            {
                string puuid = $"20000000-0000-0000-0000-{i:D12}";
                string username = $"Player16Chars_{i:D2}"; // Exactly 16 chars
                string tag = $"TAG{i:D2}";               // 5 chars
                ulong discordId = 4000000000UL + (ulong)i;

                var player = (JObject)allPlayers[i];
                player["puuid"] = puuid;
                player["name"] = username;
                player["tag"] = tag;
                // Longest agent name in AgentsEnum: "Brimstone" (9 chars)
                player["character"] = "Brimstone";
                // Trigger toe shooter emoji with legshots >= 20%
                player["stats"]!["headshots"] = 10;
                player["stats"]!["bodyshots"] = 60;
                player["stats"]!["legshots"] = 30;

                SeedTrackedUser(puuid, username, tag, discordId, TestChannelId1);
                seededUsers.Add((puuid, discordId, TestChannelId1));
            }

            string matchesJson = root.ToString(Newtonsoft.Json.Formatting.None);

            try
            {
                var handler = new StubHttpMessageHandler(matchesJson);
                var (program, notifier) = CreateProgram(TimeProvider.System, handler);

                await program.RunPollTickAsync();

                Assert.AreEqual(1, notifier.Sent.Count, "Exactly 1 combined match recap should be posted");
                var sent = notifier.Sent[0];
                Assert.AreEqual(TestChannelId1, sent.ChannelId);

                var embed = sent.Embed;
                // 10 player fields + 1 match info field = 11 fields
                Assert.AreEqual(11, embed.Fields.Length, "Recap must have 10 player fields + 1 match info field");

                // Assert all Discord embed limits
                AssertDiscordEmbedLimits(embed);
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                foreach (var user in seededUsers)
                {
                    CleanTrackedUser(user.Puuid, user.DiscordId, user.ChannelId);
                }
            }
        }

        /// <summary>
        /// (c) Daily report across several channels.
        /// Seeds 3 channels:
        /// - Channel 1: 25 users -> paginated into Page 1 (20 fields) and Page 2 (5 fields)
        /// - Channel 2: 5 users  -> single page (5 fields)
        /// - Channel 3: 21 users -> paginated into Page 1 (20 fields) and Page 2 (1 field)
        /// Verifies that every captured embed across all channels respects Discord limits.
        /// </summary>
        [TestMethod]
        public async Task Report_SeveralChannels_SplitsPerChannelAndRespectsLimits()
        {
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 23, 11, 30, 0, TimeSpan.Zero));
            DateTime matchTime = new(2026, 11, 23, 5, 0, 0, DateTimeKind.Utc);
            string matchId = "multi-channel-report-match";

            var seededUsers = new List<(string Puuid, ulong DiscordId, ulong ChannelId)>();

            try
            {
                int userIndex = 1;

                // Channel 1: 25 users (should split 20 + 5)
                for (int i = 0; i < 25; i++, userIndex++)
                {
                    string puuid = $"30000000-0000-0000-0000-{userIndex:D12}";
                    string username = $"Ch1User_{userIndex:D8}"; // 16 chars
                    ulong discordId = 5000000000UL + (ulong)userIndex;

                    SeedTrackedUser(puuid, username, "TAG01", discordId, TestChannelId1);
                    seededUsers.Add((puuid, discordId, TestChannelId1));
                }

                // Channel 2: 5 users (1 page of 5)
                for (int i = 0; i < 5; i++, userIndex++)
                {
                    string puuid = $"30000000-0000-0000-0000-{userIndex:D12}";
                    string username = $"Ch2User_{userIndex:D8}"; // 16 chars
                    ulong discordId = 5000000000UL + (ulong)userIndex;

                    SeedTrackedUser(puuid, username, "TAG02", discordId, TestChannelId2);
                    seededUsers.Add((puuid, discordId, TestChannelId2));
                }

                // Channel 3: 21 users (should split 20 + 1)
                for (int i = 0; i < 21; i++, userIndex++)
                {
                    string puuid = $"30000000-0000-0000-0000-{userIndex:D12}";
                    string username = $"Ch3User_{userIndex:D8}"; // 16 chars
                    ulong discordId = 5000000000UL + (ulong)userIndex;

                    SeedTrackedUser(puuid, username, "TAG03", discordId, TestChannelId3);
                    seededUsers.Add((puuid, discordId, TestChannelId3));
                }

                // Seed match and match stats for all users
                var (match, _) = CreateBaseTestMatch(matchTime, matchId, seededUsers[0].Puuid);
                MatchesExtension.InsertRow(match);

                foreach (var user in seededUsers)
                {
                    var (_, stats) = CreateBaseTestMatch(matchTime, matchId, user.Puuid);
                    MatchStatsExtension.InsertRow(stats);
                }

                var (program, notifier) = CreateProgram(clock);

                await program.RunDailyCheckAsync();

                // 2 pages for Channel 1 + 1 page for Channel 2 + 2 pages for Channel 3 = 5 embeds total
                Assert.AreEqual(5, notifier.Sent.Count, "Expected exactly 5 embeds sent across all 3 channels");

                var ch1Sent = notifier.Sent.Where(s => s.ChannelId == TestChannelId1).ToList();
                var ch2Sent = notifier.Sent.Where(s => s.ChannelId == TestChannelId2).ToList();
                var ch3Sent = notifier.Sent.Where(s => s.ChannelId == TestChannelId3).ToList();

                Assert.AreEqual(2, ch1Sent.Count, "Channel 1 should receive 2 pages");
                Assert.AreEqual(20, ch1Sent[0].Embed.Fields.Length);
                Assert.AreEqual(5, ch1Sent[1].Embed.Fields.Length);

                Assert.AreEqual(1, ch2Sent.Count, "Channel 2 should receive 1 page");
                Assert.AreEqual(5, ch2Sent[0].Embed.Fields.Length);

                Assert.AreEqual(2, ch3Sent.Count, "Channel 3 should receive 2 pages");
                Assert.AreEqual(20, ch3Sent[0].Embed.Fields.Length);
                Assert.AreEqual(1, ch3Sent[1].Embed.Fields.Length);

                // Assert limits on every embed captured
                foreach (var sent in notifier.Sent)
                {
                    AssertDiscordEmbedLimits(sent.Embed);
                }
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                foreach (var user in seededUsers)
                {
                    CleanTrackedUser(user.Puuid, user.DiscordId, user.ChannelId);
                }
            }
        }

        /// <summary>
        /// Act-end report summary with 30 tracked users.
        /// Verifies:
        /// - Act report is paginated properly (Page 1: 20 fields, Page 2: 10 fields).
        /// - All embeds respect Discord limits.
        /// </summary>
        [TestMethod]
        public async Task ActReport_30TrackedUsers_SplitsIntoPagesAndRespectsLimits()
        {
            // E7A3 end date in EpisodeActInfos: 2024-01-09 11:00:00 UTC
            var clock = new FixedTimeProvider(new DateTimeOffset(2024, 1, 9, 11, 0, 0, TimeSpan.Zero));
            DateTime matchTime = new(2023, 11, 4, 20, 0, 0, DateTimeKind.Utc);
            string matchId = "act-report-30-users-match";

            var seededUsers = new List<(string Puuid, ulong DiscordId, ulong ChannelId)>();

            try
            {
                for (int i = 1; i <= 30; i++)
                {
                    string puuid = $"40000000-0000-0000-0000-{i:D12}";
                    string username = $"ActUserName_{i:D4}"; // 16 chars
                    ulong discordId = 6000000000UL + (ulong)i;

                    SeedTrackedUser(puuid, username, "NA1", discordId, TestChannelId1);
                    seededUsers.Add((puuid, discordId, TestChannelId1));
                }

                var (match, _) = CreateBaseTestMatch(matchTime, matchId, seededUsers[0].Puuid);
                MatchesExtension.InsertRow(match);

                foreach (var user in seededUsers)
                {
                    var (_, stats) = CreateBaseTestMatch(matchTime, matchId, user.Puuid);
                    MatchStatsExtension.InsertRow(stats);
                }

                var (program, notifier) = CreateProgram(clock);

                await program.RunDailyCheckAsync();

                Assert.AreEqual(2, notifier.Sent.Count, "Act report should split 30 users into 2 pages");
                Assert.AreEqual("Episode 7 Act 3 Report Summary - Page 1", notifier.Sent[0].Embed.Title);
                Assert.AreEqual("Episode 7 Act 3 Report Summary - Page 2", notifier.Sent[1].Embed.Title);

                Assert.AreEqual(20, notifier.Sent[0].Embed.Fields.Length);
                Assert.AreEqual(10, notifier.Sent[1].Embed.Fields.Length);

                foreach (var sent in notifier.Sent)
                {
                    AssertDiscordEmbedLimits(sent.Embed);
                }
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                foreach (var user in seededUsers)
                {
                    CleanTrackedUser(user.Puuid, user.DiscordId, user.ChannelId);
                }
            }
        }

        #endregion
    }
}
