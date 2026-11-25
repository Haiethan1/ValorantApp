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
using ValorantApp.Valorant.Enums;

namespace ValorantAppTests
{
    /// <summary>
    /// Deterministic end-to-end tests driving BaseValorantProgram through poll ticks for COMPETITIVE matches.
    /// Uses a stubbed HenrikDev HTTP client returning competitive match + mmr-history responses,
    /// temp SQLite DB, and RecordingNotifier.
    /// Note: These tests share static DB state (SQLite connection), so MSTest runs them sequentially.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class CompetitiveE2ETests
    {
        private const string EthanPuuid = "aaaaaaaa-0000-4000-8000-000000000501";
        private const ulong TestChannelId = 987654321UL;
        private const ulong EthanDiscordId = 111222333UL;

        #region Helpers

        private static (string Json, string MatchId, long Epoch) LoadRewrittenMatchFixture(DateTime matchTime)
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

        private class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly string _matchesJson;
            private readonly string _mmrHistoryJson;
            public List<string> RequestedUrls { get; } = new();

            public StubHttpMessageHandler(string matchesJson, string mmrHistoryJson)
            {
                _matchesJson = matchesJson;
                _mmrHistoryJson = mmrHistoryJson;
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

                if (url.Contains("v1/by-puuid/mmr-history", StringComparison.OrdinalIgnoreCase))
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(_mmrHistoryJson, System.Text.Encoding.UTF8, "application/json")
                    };
                    return Task.FromResult(response);
                }

                // Any other endpoint: HenrikApi tolerates 404 cleanly by returning null
                var notFound = new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{\"status\":404,\"message\":\"Not Found\"}", System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(notFound);
            }
        }

        private static (BaseValorantProgram Program, RecordingNotifier Notifier, StubHttpMessageHandler Handler) CreateProgram(
            string matchesJson,
            string mmrHistoryJson,
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
            return (program, notifier, handler);
        }

        private static void SeedUser(string puuid = EthanPuuid, string username = "Player01", string tag = "NA1", string affinity = "na", ulong discordId = EthanDiscordId, ulong channelId = TestChannelId)
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

        #region Tests

        [TestMethod]
        public async Task PollTick_CompetitiveMatch_StoredAndPostedOnce()
        {
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero));
            DateTime matchTime = new(2026, 11, 23, 11, 30, 0, DateTimeKind.Utc);
            var (matchesJson, matchId, epoch) = LoadRewrittenMatchFixture(matchTime);
            // Player in Matches_11_4_2023.json has CurrentTier = 22. Same tier => no tier change.
            string mmrHistoryJson = LoadRewrittenMmrHistoryFixture(matchId, newTier: 22, rrChange: 18);

            try
            {
                SeedUser(EthanPuuid, "Player01", "NA1", "na", EthanDiscordId, TestChannelId);
                var (program, notifier, handler) = CreateProgram(matchesJson, mmrHistoryJson, clock);
                int MatchesRequests() => handler.RequestedUrls.Count(u => u.Contains("v3/by-puuid/matches", StringComparison.OrdinalIgnoreCase));

                await program.RunPollTickAsync();

                // DB state assertions
                Assert.IsTrue(MatchesExtension.MatchIdExists(matchId), "Matches row should exist in SQLite");
                Assert.IsTrue(MatchStatsExtension.MatchIdExistsForUser(matchId, EthanPuuid), "MatchStats row should exist for user");

                // Outbound notification assertions
                Assert.AreEqual(1, notifier.Sent.Count, "Exactly one recap embed should be sent when there is no tier change");
                var sent = notifier.Sent[0];
                Assert.AreEqual(TestChannelId, sent.ChannelId);
                Assert.AreEqual($"<@{EthanDiscordId}>", sent.Text);

                var embed = sent.Embed;
                Assert.AreEqual("Competitive - Lotus", embed.Title);
                Assert.AreEqual($"https://tracker.gg/valorant/match/{matchId}", embed.Url);
                Assert.AreEqual(Color.Green, embed.Color, "Blue team won the match");
                Assert.AreEqual(AgentsExtension.AgentFromString("Skye").ImageURLFromAgent(), embed.Thumbnail?.Url);

                Assert.AreEqual(2, embed.Fields.Length);
                StringAssert.Contains(embed.Fields[0].Name, "Player01");
                StringAssert.Contains(embed.Fields[0].Name, RankEmojis.Ascendant2.Id());
                Assert.AreEqual("Combat Score: 278, K/D/A: 19/15/13\nHeadshot: 42.50%, RR: 18", embed.Fields[0].Value);

                Assert.AreEqual($"__{new string(' ', 40)}__\n\nMatch Stats", embed.Fields[1].Name);
                StringAssert.Contains(embed.Fields[1].Value, $"<t:{epoch}:f>, 31 minutes");
                StringAssert.Contains(embed.Fields[1].Value, "Rounds 13 : 8");

                // HTTP endpoints requested
                Assert.IsTrue(handler.RequestedUrls.Any(u => u.Contains($"v3/by-puuid/matches/na/{EthanPuuid}")), "Expected matches endpoint request");
                Assert.IsTrue(handler.RequestedUrls.Any(u => u.Contains($"v1/by-puuid/mmr-history/na/{EthanPuuid}")), "Expected mmr-history endpoint request");

                // Second poll tick should skip the already stored match
                await program.RunPollTickAsync();
                Assert.AreEqual(2, MatchesRequests(), "Tick 2 must poll the user again");
                Assert.AreEqual(1, notifier.Sent.Count, "Second poll tick must not re-post the match");
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, TestChannelId);
            }
        }

        [TestMethod]
        public async Task PollTick_CompetitiveMatch_Promotion_PostsTierChangeMessage()
        {
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero));
            DateTime matchTime = new(2026, 11, 23, 11, 30, 0, DateTimeKind.Utc);
            var (matchesJson, matchId, _) = LoadRewrittenMatchFixture(matchTime);
            // Player in Matches_11_4_2023.json has CurrentTier = 22 (Ascendant 2).
            // Promotion to 23 (Ascendant 3)
            string mmrHistoryJson = LoadRewrittenMmrHistoryFixture(matchId, newTier: 23, rrChange: 21);

            try
            {
                SeedUser(EthanPuuid, "Player01", "NA1", "na", EthanDiscordId, TestChannelId);
                var (program, notifier, _) = CreateProgram(matchesJson, mmrHistoryJson, clock);

                await program.RunPollTickAsync();

                // Expect 2 embeds: match recap and tier promotion
                Assert.AreEqual(2, notifier.Sent.Count, "Promotion must post both recap embed and tier-change embed");

                // Embed 1: Match recap
                Assert.AreEqual(TestChannelId, notifier.Sent[0].ChannelId);
                Assert.AreEqual("Competitive - Lotus", notifier.Sent[0].Embed.Title);

                // Embed 2: Tier promotion
                var tierMsg = notifier.Sent[1];
                Assert.AreEqual(TestChannelId, tierMsg.ChannelId);
                Assert.AreEqual($"<@{EthanDiscordId}>", tierMsg.Text);

                var embed = tierMsg.Embed;
                Assert.AreEqual($"Player01#TAG01 {MemeEmojisEnum.Sunglasses.Id()}", embed.Title);
                Assert.AreEqual(
                    $"{RankEmojis.Ascendant2.Id()} {MemeEmojisEnum.ArrowUp.Id()} {RankEmojis.Ascendant3.Id()}",
                    embed.Description);
                Assert.AreEqual(AgentsExtension.AgentFromString("Skye").ImageURLFromAgent(), embed.Thumbnail?.Url);

                Assert.AreEqual(1, embed.Fields.Length);
                Assert.AreEqual($"{RankEmojis.Ascendant2.ToDescriptionString()} Competitive Stats", embed.Fields[0].Name);
                Assert.AreEqual("Matches: 1 | Minutes: 31\nK/D/A: 19/15/13\nHeadshot: 42.5% | Bodyshot: 52.5%", embed.Fields[0].Value);
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, TestChannelId);
            }
        }

        [TestMethod]
        public async Task PollTick_CompetitiveMatch_Demotion_PostsTierChangeMessage()
        {
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero));
            DateTime matchTime = new(2026, 11, 23, 11, 30, 0, DateTimeKind.Utc);
            var (matchesJson, matchId, _) = LoadRewrittenMatchFixture(matchTime);
            // Player in Matches_11_4_2023.json has CurrentTier = 22 (Ascendant 2).
            // Demotion to 21 (Ascendant 1)
            string mmrHistoryJson = LoadRewrittenMmrHistoryFixture(matchId, newTier: 21, rrChange: -19);

            try
            {
                SeedUser(EthanPuuid, "Player01", "NA1", "na", EthanDiscordId, TestChannelId);
                var (program, notifier, _) = CreateProgram(matchesJson, mmrHistoryJson, clock);

                await program.RunPollTickAsync();

                // Expect 2 embeds: match recap and tier demotion
                Assert.AreEqual(2, notifier.Sent.Count, "Demotion must post both recap embed and tier-change embed");

                // Embed 1: Match recap
                Assert.AreEqual(TestChannelId, notifier.Sent[0].ChannelId);
                Assert.AreEqual("Competitive - Lotus", notifier.Sent[0].Embed.Title);

                // Embed 2: Tier demotion
                var tierMsg = notifier.Sent[1];
                Assert.AreEqual(TestChannelId, tierMsg.ChannelId);
                Assert.AreEqual($"<@{EthanDiscordId}>", tierMsg.Text);

                var embed = tierMsg.Embed;
                Assert.AreEqual($"Player01#TAG01 {MemeEmojisEnum.Clown.Id()}", embed.Title);
                Assert.AreEqual(
                    $"{RankEmojis.Ascendant2.Id()} {MemeEmojisEnum.ArrowDown.Id()} {RankEmojis.Ascendant1.Id()}",
                    embed.Description);
                Assert.AreEqual(AgentsExtension.AgentFromString("Skye").ImageURLFromAgent(), embed.Thumbnail?.Url);

                Assert.AreEqual(1, embed.Fields.Length);
                Assert.AreEqual($"{RankEmojis.Ascendant2.ToDescriptionString()} Competitive Stats", embed.Fields[0].Name);
                Assert.AreEqual("Matches: 1 | Minutes: 31\nK/D/A: 19/15/13\nHeadshot: 42.5% | Bodyshot: 52.5%", embed.Fields[0].Value);
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, TestChannelId);
            }
        }

        [TestMethod]
        public async Task PollTick_CompetitiveMatch_NoTierChange_DoesNotPostTierChangeMessage()
        {
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero));
            DateTime matchTime = new(2026, 11, 23, 11, 30, 0, DateTimeKind.Utc);
            var (matchesJson, matchId, _) = LoadRewrittenMatchFixture(matchTime);
            // Player in Matches_11_4_2023.json has CurrentTier = 22 (Ascendant 2).
            // Same tier 22 => no tier change
            string mmrHistoryJson = LoadRewrittenMmrHistoryFixture(matchId, newTier: 22, rrChange: 14);

            try
            {
                SeedUser(EthanPuuid, "Player01", "NA1", "na", EthanDiscordId, TestChannelId);
                var (program, notifier, _) = CreateProgram(matchesJson, mmrHistoryJson, clock);

                await program.RunPollTickAsync();

                // Only 1 embed sent (the match recap); no tier change message
                Assert.AreEqual(1, notifier.Sent.Count, "No tier change must only post the recap embed");
                Assert.AreEqual(TestChannelId, notifier.Sent[0].ChannelId);
                Assert.AreEqual("Competitive - Lotus", notifier.Sent[0].Embed.Title);
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(EthanPuuid, EthanDiscordId, TestChannelId);
            }
        }

        #endregion
    }
}
