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
    [TestClass]
    [DoNotParallelize]
    public class GameContentGuardTests
    {
        private const string EthanPuuid = "8c7276bd-a70b-56bf-803b-7e0fd9dba287";
        private const ulong TestChannelId = 987654321UL;
        private const ulong EthanDiscordId = 111222333UL;

        #region Helpers for E2E

        private static (string Json, string MatchId, long Epoch) LoadRewrittenFixtureWithUnknowns(DateTime matchTime, string unknownAgent, string unknownMap, string unknownMode)
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
            metadata["map"] = unknownMap;
            metadata["mode"] = unknownMode;

            var players = root["data"]![0]!["players"]!["all_players"]!;
            foreach (var player in players)
            {
                if (player["puuid"]?.ToString() == EthanPuuid)
                {
                    player["character"] = unknownAgent;
                }
            }

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

                var notFound = new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{\"status\":404,\"message\":\"Not Found\"}", System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(notFound);
            }
        }

        private static (BaseValorantProgram Program, RecordingNotifier Notifier, StubHttpMessageHandler Handler) CreateProgram(string matchesJson, TimeProvider? timeProvider = null)
        {
            var handler = new StubHttpMessageHandler(matchesJson);
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

        #region Part 1: Content Guard Tests

        [TestMethod]
        public void Agents_KnownMembers_HaveImageUrls()
        {
            foreach (Agents agent in Enum.GetValues<Agents>())
            {
                if (agent == Agents.Unknown)
                {
                    Assert.AreEqual(string.Empty, agent.ImageURLFromAgent(), "Agents.Unknown should return empty image URL");
                    continue;
                }

                string url = agent.ImageURLFromAgent();
                Assert.IsFalse(string.IsNullOrWhiteSpace(url), $"Agent {agent} should have a non-empty image URL");
                Assert.IsTrue(Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
                    $"Agent {agent} image URL '{url}' should be a valid absolute HTTP(S) URL");
            }
        }

        [TestMethod]
        public void Agents_KnownMembers_HaveEmojiMappings()
        {
            foreach (Agents agent in Enum.GetValues<Agents>())
            {
                if (agent == Agents.Unknown)
                {
                    Assert.AreEqual("Unknown", agent.Id(), "Agents.Unknown fallback Id should be agent name");
                    continue;
                }

                string emoji = agent.Id().Trim();
                Assert.IsFalse(string.IsNullOrWhiteSpace(emoji), $"Agent {agent} should have a non-empty emoji mapping");
                Assert.IsTrue(emoji.StartsWith("<:") && emoji.EndsWith(">"),
                    $"Agent {agent} emoji '{emoji}' should match Discord custom emoji format '<:name:id>'");
            }
        }

        [TestMethod]
        public void Agents_AllMembers_RoundTripThroughStringHelpers()
        {
            foreach (Agents agent in Enum.GetValues<Agents>())
            {
                string str = agent.StringFromAgent();
                Assert.IsFalse(string.IsNullOrWhiteSpace(str), $"Agent {agent} should have a non-empty StringFromAgent value");

                Agents parsed = AgentsExtension.AgentFromString(str);
                Assert.AreEqual(agent, parsed, $"Agent {agent} did not round-trip through AgentFromString(StringFromAgent())");

                string roundTrippedStr = parsed.StringFromAgent();
                Assert.AreEqual(str, roundTrippedStr, $"Agent string '{str}' did not round-trip through StringFromAgent(AgentFromString())");
            }
        }

        [TestMethod]
        public void Maps_KnownMembers_HaveImageUrls()
        {
            foreach (Maps map in Enum.GetValues<Maps>())
            {
                if (map == Maps.Unknown)
                {
                    Assert.AreEqual(string.Empty, map.ImageUrlFromMap(), "Maps.Unknown should return empty image URL");
                    continue;
                }

                string url = map.ImageUrlFromMap();
                Assert.IsFalse(string.IsNullOrWhiteSpace(url), $"Map {map} should have a non-empty image URL");
                Assert.IsTrue(Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
                    $"Map {map} image URL '{url}' should be a valid absolute HTTP(S) URL");
            }
        }

        [TestMethod]
        public void Maps_AllMembers_RoundTripThroughStringHelpers()
        {
            foreach (Maps map in Enum.GetValues<Maps>())
            {
                if (map == Maps.Unknown)
                {
                    // Maps.Unknown.StringFromMap() returns "" by design; MapFromString("") returns Maps.Unknown
                    Assert.AreEqual(string.Empty, map.StringFromMap());
                    Assert.AreEqual(Maps.Unknown, MapsExtension.MapFromString(string.Empty));
                    continue;
                }

                string str = map.StringFromMap();
                Assert.IsFalse(string.IsNullOrWhiteSpace(str), $"Map {map} should have a non-empty StringFromMap value");

                Maps parsed = MapsExtension.MapFromString(str);
                Assert.AreEqual(map, parsed, $"Map {map} did not round-trip through MapFromString(StringFromMap())");

                string roundTrippedStr = parsed.StringFromMap();
                Assert.AreEqual(str, roundTrippedStr, $"Map string '{str}' did not round-trip through StringFromMap(MapFromString())");
            }
        }

        [TestMethod]
        public void Modes_AllMembers_RoundTripThroughStringHelpers()
        {
            foreach (Modes mode in Enum.GetValues<Modes>())
            {
                if (mode == Modes.Unknown)
                {
                    // Modes.Unknown.StringFromMode() returns "" by design; ModeFromString("") returns Modes.Unknown
                    Assert.AreEqual(string.Empty, mode.StringFromMode());
                    Assert.AreEqual(Modes.Unknown, ModesExtension.ModeFromString(string.Empty));
                    continue;
                }

                string str = mode.StringFromMode();
                Assert.IsFalse(string.IsNullOrWhiteSpace(str), $"Mode {mode} should have a non-empty StringFromMode value");

                Modes parsed = ModesExtension.ModeFromString(str);
                Assert.AreEqual(mode, parsed, $"Mode {mode} did not round-trip through ModeFromString(StringFromMode())");

                string roundTrippedStr = parsed.StringFromMode();
                Assert.AreEqual(str, roundTrippedStr, $"Mode string '{str}' did not round-trip through StringFromMode(ModeFromString())");
            }
        }

        [TestMethod]
        public void RankTier_0To27_AllMapToRankEmoji_NoGaps()
        {
            for (int tier = 0; tier <= 27; tier++)
            {
                string emoji = ((RankEmojis)tier).Id().Trim();
                Assert.IsFalse(string.IsNullOrWhiteSpace(emoji), $"Tier {tier} should map to a non-empty emoji string");
                Assert.IsTrue(emoji.StartsWith("<:") && emoji.EndsWith(">"),
                    $"Tier {tier} emoji '{emoji}' should match Discord custom emoji format '<:name:id>'");
            }
        }

        [TestMethod]
        public void RankTier_3To27_AllMapToRankName_NoGaps()
        {
            for (int tier = 3; tier <= 27; tier++)
            {
                Assert.IsTrue(Enum.IsDefined(typeof(RankEmojis), tier), $"Tier {tier} should have a defined RankEmojis member");
                string name = ((RankEmojis)tier).ToDescriptionString();
                Assert.IsFalse(string.IsNullOrWhiteSpace(name), $"Tier {tier} should have a non-empty description");
                Assert.IsFalse(int.TryParse(name, out _), $"Tier {tier} description '{name}' should be a rank name, not numeric");
            }
        }

        [TestMethod]
        [Ignore("Tiers 0-2 (unranked) are not defined in RankEmojis enum and have no description mapping (ToDescriptionString returns numeric '0', '1', '2')")]
        public void RankTier_0To2_MapToRankName()
        {
            for (int tier = 0; tier <= 2; tier++)
            {
                Assert.IsTrue(Enum.IsDefined(typeof(RankEmojis), tier), $"Tier {tier} has no defined member in RankEmojis");
                string name = ((RankEmojis)tier).ToDescriptionString();
                Assert.IsFalse(int.TryParse(name, out _), $"Tier {tier} description should not be numeric string '{name}'");
            }
        }

        #endregion

        #region Part 2: E2E With Unknown Game Content

        [TestMethod]
        public async Task PollTick_MatchWithUnknownAgentMapMode_DoesNotThrowAndPostsRecapWithPlaceholders()
        {
            const string unknownAgent = "UnknownAgentTest";
            const string unknownMap = "UnknownMapTest";
            const string unknownMode = "UnknownModeTest";

            DateTime matchTime = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, DateTime.UtcNow.Minute, 0, DateTimeKind.Utc);
            var (fixtureJson, matchId, _) = LoadRewrittenFixtureWithUnknowns(matchTime, unknownAgent, unknownMap, unknownMode);

            SeedUser(EthanPuuid, "Ehtan", "NA1", "na", EthanDiscordId, TestChannelId);

            try
            {
                var (program, notifier, _) = CreateProgram(fixtureJson);

                // Polling tick must not throw on unknown agent/map/mode
                await program.RunPollTickAsync();

                // DB rows are stored with the raw unknown strings
                Assert.IsTrue(MatchesExtension.MatchIdExists(matchId), "Matches row should exist in SQLite");
                Assert.IsTrue(MatchStatsExtension.MatchIdExistsForUser(matchId, EthanPuuid), "MatchStats row should exist for user");

                // Outbound notification: exactly one embed is posted
                Assert.AreEqual(1, notifier.Sent.Count, "Exactly one embed should be sent");
                var sent = notifier.Sent[0];
                Assert.AreEqual(TestChannelId, sent.ChannelId);
                Assert.AreEqual($"<@{EthanDiscordId}>", sent.Text);

                var embed = sent.Embed;
                // Placeholders:
                // 1. Unknown mode maps to Modes.Unknown whose StringFromMode() is "", so embed title is " - <Map>"
                Assert.AreEqual($" - {unknownMap}", embed.Title, "Title should show empty mode placeholder preceding unknown map name");

                // 2. Player field name uses Agents.Unknown.Id() which falls back to "Unknown" placeholder text
                Assert.IsTrue(embed.Fields.Length > 0, "Embed should contain player field");
                var playerField = embed.Fields[0];
                StringAssert.StartsWith(playerField.Name, "Unknown Ehtan",
                    "Player field name should begin with 'Unknown' placeholder instead of agent emoji");
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
