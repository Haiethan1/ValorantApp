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
using ValorantApp.GenericExtensions;
using ValorantApp.HenrikJson;
using ValorantApp.Valorant;
using ValorantApp.Valorant.Enums;

namespace ValorantAppTests
{
    /// <summary>
    /// Contract tests verifying that HenrikDev 2026 recorded API fixtures parse and process correctly
    /// through the bot's real deserialization and polling pipelines.
    /// If HenrikDev changes its response shape or schema, these tests fail.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class RecordedFixtureContractTests
    {
        private const string TrackedPuuid = "aaaaaaaa-0000-4000-8000-000000000001";
        private const string TrackedUsername = "Player01";
        private const string TrackedTag = "TAG01";
        private const string TrackedAffinity = "na";
        private const ulong TrackedDiscordId = 111222333UL;
        private const ulong TrackedChannelId = 987654321UL;

        private const string SecondUserPuuid = "aaaaaaaa-0000-4000-8000-000000000002";
        private const string SecondUsername = "Player02";
        private const string SecondTag = "TAG02";
        private const ulong SecondDiscordId = 111222334UL;
        private const ulong SecondChannelId = 987654322UL;

        #region Helpers

        private static string LoadRecordedFixture(string filename)
        {
            string[] candidatePaths =
            [
                Path.Combine("../../../MockJsonResponse/Recorded", filename),
                Path.Combine(AppContext.BaseDirectory, "../../../MockJsonResponse/Recorded", filename),
                Path.Combine(AppContext.BaseDirectory, "MockJsonResponse/Recorded", filename),
                Path.Combine(Directory.GetCurrentDirectory(), "ValorantAppTests/MockJsonResponse/Recorded", filename),
            ];

            foreach (string candidate in candidatePaths)
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            throw new FileNotFoundException($"Could not find recorded fixture '{filename}'. Checked paths: {string.Join(", ", candidatePaths)}");
        }

        private class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<string, HttpRequestMessage, HttpResponseMessage> _responder;
            public List<string> RequestedUrls { get; } = new();

            public StubHttpMessageHandler(Func<string, HttpRequestMessage, HttpResponseMessage> responder)
            {
                _responder = responder;
            }

            public StubHttpMessageHandler(string matchesJson, string mmrHistoryJson)
            {
                _responder = (url, _) =>
                {
                    if (url.Contains("v3/by-puuid/matches", StringComparison.OrdinalIgnoreCase))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(matchesJson, System.Text.Encoding.UTF8, "application/json")
                        };
                    }

                    if (url.Contains("v1/by-puuid/mmr-history", StringComparison.OrdinalIgnoreCase))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(mmrHistoryJson, System.Text.Encoding.UTF8, "application/json")
                        };
                    }

                    return new HttpResponseMessage(HttpStatusCode.NotFound)
                    {
                        Content = new StringContent("{\"status\":404,\"message\":\"Not Found\"}", System.Text.Encoding.UTF8, "application/json")
                    };
                };
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri?.ToString() ?? string.Empty;
                RequestedUrls.Add(url);
                return Task.FromResult(_responder(url, request));
            }
        }

        private static (BaseValorantProgram Program, RecordingNotifier Notifier, StubHttpMessageHandler Handler) CreateProgram(
            StubHttpMessageHandler handler,
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
            return (program, notifier, handler);
        }

        private static (BaseValorantProgram Program, RecordingNotifier Notifier, StubHttpMessageHandler Handler) CreateProgram(
            string matchesJson,
            string mmrHistoryJson,
            TimeProvider? timeProvider = null)
        {
            var handler = new StubHttpMessageHandler(matchesJson, mmrHistoryJson);
            return CreateProgram(handler, timeProvider);
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

        private static FixedTimeProvider CreateClockShortlyAfterMatch(MatchJson match)
        {
            DateTime matchTimeUtc;
            if (DateTime.TryParse(match.Metadata?.Game_Start_Patched, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
            {
                matchTimeUtc = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }
            else if (match.Metadata?.Game_Start > 0)
            {
                matchTimeUtc = DateTimeOffset.FromUnixTimeSeconds(match.Metadata.Game_Start).UtcDateTime;
            }
            else
            {
                matchTimeUtc = DateTime.UtcNow;
            }

            return new FixedTimeProvider(new DateTimeOffset(matchTimeUtc).AddMinutes(30));
        }

        #endregion

        #region 1. Deserialization Contract

        [DataTestMethod]
        [DataRow("matches_competitive.json")]
        [DataRow("matches_competitive_overtime.json")]
        [DataRow("matches_competitive_shortgame.json")]
        [DataRow("matches_deathmatch.json")]
        [DataRow("matches_empty.json")]
        [DataRow("matches_premier.json")]
        [DataRow("matches_swiftplay.json")]
        [DataRow("matches_unavailable.json")]
        [DataRow("matches_unrated.json")]
        public void DeserializationContract_Matches_ParsesExpectedFields(string filename)
        {
            string rawJson = LoadRecordedFixture(filename);

            // Parse using the identical code path as HenrikApi (StringExtensions.TryParse)
            var parsed = rawJson.TryParse<JsonObjectHenrik<List<MatchJson>>>(out string error);
            Assert.IsNotNull(parsed, $"Failed to deserialize {filename}: {error}");
            Assert.AreEqual(string.Empty, error, $"Unexpected error deserializing {filename}: {error}");
            Assert.IsNotNull(parsed.Data, $"Data payload was null for {filename}");

            if (filename == "matches_empty.json")
            {
                Assert.AreEqual(0, parsed.Data.Count, "matches_empty.json should parse as empty match list");
                return;
            }

            if (filename == "matches_unavailable.json")
            {
                Assert.AreEqual(1, parsed.Data.Count, "matches_unavailable.json contains 1 unavailable match item");
                var unavailable = parsed.Data[0];
                Assert.IsNull(unavailable.Metadata, "matches_unavailable.json should have null metadata");
                return;
            }

            // For matches with real data, assert all fields that MatchesExtension.CreateFromJson,
            // MatchStatsExtension.CreateFromJson, and BaseValorantProgram (events/locations) read:
            Assert.IsTrue(parsed.Data.Count > 0, $"{filename} should contain at least 1 match");
            MatchJson match = parsed.Data[0];

            // 1. Metadata members read by CreateFromJson & BaseValorantProgram
            Assert.IsNotNull(match.Metadata, $"{filename}: match.Metadata is required");
            Assert.IsFalse(string.IsNullOrEmpty(match.Metadata.MatchId), $"{filename}: match.Metadata.MatchId must not be empty");
            Assert.IsFalse(string.IsNullOrEmpty(match.Metadata.Map), $"{filename}: match.Metadata.Map must not be empty");
            Assert.IsFalse(string.IsNullOrEmpty(match.Metadata.Mode), $"{filename}: match.Metadata.Mode must not be empty");
            Assert.IsFalse(string.IsNullOrEmpty(match.Metadata.Mode_Id), $"{filename}: match.Metadata.Mode_Id must not be empty");
            Assert.IsTrue(match.Metadata.Game_Length > 0, $"{filename}: match.Metadata.Game_Length must be > 0");
            Assert.IsTrue(match.Metadata.Game_Start > 0, $"{filename}: match.Metadata.Game_Start must be > 0");
            Assert.IsFalse(string.IsNullOrEmpty(match.Metadata.Game_Start_Patched), $"{filename}: match.Metadata.Game_Start_Patched must not be empty");
            Assert.IsTrue(DateTime.TryParse(match.Metadata.Game_Start_Patched, out _), $"{filename}: match.Metadata.Game_Start_Patched must parse as DateTime");
            Assert.IsTrue(match.Metadata.Rounds_Played > 0, $"{filename}: match.Metadata.Rounds_Played must be > 0");

            bool isDeathmatch = ModesExtension.ModeFromString(match.Metadata.Mode.Safe()) == Modes.Deathmatch;

            // 2. Teams members read by MatchesExtension.CreateFromJson
            Assert.IsNotNull(match.Teams, $"{filename}: match.Teams must not be null");
            if (!isDeathmatch)
            {
                Assert.IsNotNull(match.Teams.Blue, $"{filename}: match.Teams.Blue must not be null for team modes");
                Assert.IsTrue(match.Teams.Blue.Rounds_Won.HasValue, $"{filename}: match.Teams.Blue.Rounds_Won must have value");
                Assert.IsTrue(match.Teams.Blue.Has_Won.HasValue, $"{filename}: match.Teams.Blue.Has_Won must have value");
                Assert.IsNotNull(match.Teams.Red, $"{filename}: match.Teams.Red must not be null for team modes");
                Assert.IsTrue(match.Teams.Red.Rounds_Won.HasValue, $"{filename}: match.Teams.Red.Rounds_Won must have value");
                Assert.IsTrue(match.Teams.Red.Has_Won.HasValue, $"{filename}: match.Teams.Red.Has_Won must have value");
            }

            // 3. Players members read by MatchesExtension and MatchStatsExtension
            Assert.IsNotNull(match.Players, $"{filename}: match.Players must not be null");
            Assert.IsNotNull(match.Players.All_Players, $"{filename}: match.Players.All_Players must not be null");
            Assert.IsTrue(match.Players.All_Players.Length > 0, $"{filename}: match.Players.All_Players must not be empty");

            if (!isDeathmatch)
            {
                Assert.IsNotNull(match.Players.Blue, $"{filename}: match.Players.Blue must not be null for team modes");
                Assert.IsTrue(match.Players.Blue.Length > 0, $"{filename}: match.Players.Blue must not be empty");
                Assert.IsNotNull(match.Players.Red, $"{filename}: match.Players.Red must not be null for team modes");
                Assert.IsTrue(match.Players.Red.Length > 0, $"{filename}: match.Players.Red must not be empty");
            }

            foreach (MatchPlayerJson player in match.Players.All_Players)
            {
                Assert.IsFalse(string.IsNullOrEmpty(player.Puuid), $"{filename}: player.Puuid must not be empty");
                Assert.IsFalse(string.IsNullOrEmpty(player.Name), $"{filename}: player.Name must not be empty");
                Assert.IsFalse(string.IsNullOrEmpty(player.Tag), $"{filename}: player.Tag must not be empty");
                Assert.IsFalse(string.IsNullOrEmpty(player.Character), $"{filename}: player.Character must not be empty");
                Assert.IsFalse(string.IsNullOrEmpty(player.Team), $"{filename}: player.Team must not be empty");
                Assert.IsTrue(player.CurrentTier >= 0, $"{filename}: player.CurrentTier must be >= 0");
                Assert.IsNotNull(player.Stats, $"{filename}: player.Stats must not be null");
                Assert.IsTrue(player.Stats.Kills >= 0, $"{filename}: player.Stats.Kills must be >= 0");
                Assert.IsTrue(player.Stats.Deaths >= 0, $"{filename}: player.Stats.Deaths must be >= 0");
                Assert.IsTrue(player.Stats.Assists >= 0, $"{filename}: player.Stats.Assists must be >= 0");
                Assert.IsTrue(player.Stats.Score >= 0, $"{filename}: player.Stats.Score must be >= 0");
                Assert.IsTrue(player.Stats.Headshots >= 0, $"{filename}: player.Stats.Headshots must be >= 0");
                Assert.IsTrue(player.Stats.Bodyshots >= 0, $"{filename}: player.Stats.Bodyshots must be >= 0");
                Assert.IsTrue(player.Stats.Legshots >= 0, $"{filename}: player.Stats.Legshots must be >= 0");
                Assert.IsTrue(player.Damage_Made >= 0, $"{filename}: player.Damage_Made must be >= 0");
                Assert.IsNotNull(player.Ability_Casts, $"{filename}: player.Ability_Casts must not be null");
            }

            // 4. Rounds members read by MatchStatsExtension and BaseValorantProgram (events/locations)
            Assert.IsNotNull(match.Rounds, $"{filename}: match.Rounds must not be null");
            Assert.IsTrue(match.Rounds.Length > 0, $"{filename}: match.Rounds must not be empty");

            foreach (MatchRoundsJson round in match.Rounds)
            {
                Assert.IsNotNull(round.Player_Stats, $"{filename}: round.Player_Stats must not be null");
                Assert.IsTrue(round.Player_Stats.Length > 0, $"{filename}: round.Player_Stats must not be empty");

                foreach (MatchRoundPlayerStatsJson playerStat in round.Player_Stats)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(playerStat.Player_Puuid), $"{filename}: round playerStat.Player_Puuid must not be empty");
                    Assert.IsNotNull(playerStat.Kill_Events, $"{filename}: round playerStat.Kill_Events must not be null");

                    foreach (RoundKillEventsJson killEvent in playerStat.Kill_Events)
                    {
                        Assert.IsTrue(killEvent.Kill_Time_In_Round.HasValue, $"{filename}: killEvent.Kill_Time_In_Round must have value");
                        Assert.IsTrue(killEvent.Kill_Time_In_Match.HasValue, $"{filename}: killEvent.Kill_Time_In_Match must have value");
                        Assert.IsFalse(string.IsNullOrEmpty(killEvent.Killer_Puuid), $"{filename}: killEvent.Killer_Puuid must not be empty");
                        Assert.IsFalse(string.IsNullOrEmpty(killEvent.Victim_Puuid), $"{filename}: killEvent.Victim_Puuid must not be empty");
                        Assert.IsFalse(string.IsNullOrEmpty(killEvent.Killer_Team), $"{filename}: killEvent.Killer_Team must not be empty");
                        Assert.IsFalse(string.IsNullOrEmpty(killEvent.Victim_Team), $"{filename}: killEvent.Victim_Team must not be empty");
                        Assert.IsNotNull(killEvent.Victim_Death_Location, $"{filename}: killEvent.Victim_Death_Location must not be null");
                        Assert.IsTrue(killEvent.Victim_Death_Location.X.HasValue, $"{filename}: killEvent.Victim_Death_Location.X must have value");
                        Assert.IsTrue(killEvent.Victim_Death_Location.Y.HasValue, $"{filename}: killEvent.Victim_Death_Location.Y must have value");
                        Assert.IsNotNull(killEvent.Player_Locations_On_kill, $"{filename}: killEvent.Player_Locations_On_kill must not be null");
                    }
                }

                if (round.Bomb_Planted == true)
                {
                    Assert.IsNotNull(round.Plant_Events, $"{filename}: plant event must not be null when Bomb_Planted is true");
                    Assert.IsFalse(string.IsNullOrEmpty(round.Plant_Events.Planted_By?.Puuid), $"{filename}: Planted_By.Puuid must not be empty");
                    Assert.IsTrue(round.Plant_Events.Plant_Time_In_Round.HasValue, $"{filename}: Plant_Time_In_Round must have value");
                    Assert.IsNotNull(round.Plant_Events.Player_Locations_On_Plant, $"{filename}: Player_Locations_On_Plant must not be null");
                }

                if (round.Bomb_Defused == true)
                {
                    Assert.IsNotNull(round.Defuse_Events, $"{filename}: defuse event must not be null when Bomb_Defused is true");
                    Assert.IsFalse(string.IsNullOrEmpty(round.Defuse_Events.Defused_By?.Puuid), $"{filename}: Defused_By.Puuid must not be empty");
                    Assert.IsTrue(round.Defuse_Events.Defuse_Time_In_Round.HasValue, $"{filename}: Defuse_Time_In_Round must have value");
                    Assert.IsNotNull(round.Defuse_Events.Player_Locations_On_Defuse, $"{filename}: Player_Locations_On_Defuse must not be null");
                }
            }
        }

        [TestMethod]
        public void DeserializationContract_Account_ParsesSuccessfully()
        {
            string raw = LoadRecordedFixture("account.json");
            var parsed = raw.TryParse<JsonObjectHenrik<AccountJson>>(out string error);
            Assert.IsNotNull(parsed, $"Failed to deserialize account.json: {error}");
            Assert.AreEqual(string.Empty, error, $"Unexpected error deserializing account.json: {error}");
            Assert.AreEqual("200", parsed.Status);
            Assert.IsNotNull(parsed.Data);
            Assert.IsFalse(string.IsNullOrEmpty(parsed.Data.Puuid), "Account Puuid must not be empty");
            Assert.IsFalse(string.IsNullOrEmpty(parsed.Data.Name), "Account Name must not be empty");
            Assert.IsFalse(string.IsNullOrEmpty(parsed.Data.Tag), "Account Tag must not be empty");
            Assert.IsFalse(string.IsNullOrEmpty(parsed.Data.Region), "Account Region must not be empty");
            Assert.IsTrue(parsed.Data.Account_Level > 0, "Account_Level must be > 0");
            Assert.IsNotNull(parsed.Data.Card, "Account Card must not be null");
            Assert.IsFalse(string.IsNullOrEmpty(parsed.Data.Card.Small), "Card.Small must not be empty");
        }

        [TestMethod]
        public void DeserializationContract_Mmr_ParsesSuccessfully()
        {
            string raw = LoadRecordedFixture("mmr.json");
            var parsed = raw.TryParse<JsonObjectHenrik<MmrV2Json>>(out string error);
            Assert.IsNotNull(parsed, $"Failed to deserialize mmr.json: {error}");
            Assert.AreEqual(string.Empty, error, $"Unexpected error deserializing mmr.json: {error}");
            Assert.AreEqual("200", parsed.Status);
            Assert.IsNotNull(parsed.Data);
            Assert.IsNotNull(parsed.Data.Current_Data, "Current_Data must not be null");
            Assert.IsTrue(parsed.Data.Current_Data.CurrentTier > 0, "Current_Data.CurrentTier must be > 0");
            Assert.IsFalse(string.IsNullOrEmpty(parsed.Data.Current_Data.CurrentTierPatched), "CurrentTierPatched must not be empty");
            Assert.IsNotNull(parsed.Data.Current_Data.Images, "Current_Data.Images must not be null");
            Assert.IsNotNull(parsed.Data.Highest_Rank, "Highest_Rank must not be null");
            Assert.IsTrue(parsed.Data.Highest_Rank.Tier > 0, "Highest_Rank.Tier must be > 0");
        }

        [TestMethod]
        public void DeserializationContract_MmrHistory_ParsesSuccessfully()
        {
            string raw = LoadRecordedFixture("mmr_history.json");
            var parsed = raw.TryParse<JsonObjectHenrik<List<MmrHistoryJson>>>(out string error);
            Assert.IsNotNull(parsed, $"Failed to deserialize mmr_history.json: {error}");
            Assert.AreEqual(string.Empty, error, $"Unexpected error deserializing mmr_history.json: {error}");
            Assert.AreEqual("200", parsed.Status);
            Assert.IsNotNull(parsed.Data);
            Assert.IsTrue(parsed.Data.Count > 0, "mmr_history.json must contain at least 1 history item");

            foreach (MmrHistoryJson item in parsed.Data)
            {
                Assert.IsFalse(string.IsNullOrEmpty(item.Match_id), "MmrHistory Match_id must not be empty");
                Assert.IsTrue(item.Currenttier > 0, "MmrHistory Currenttier must be > 0");
                Assert.IsFalse(string.IsNullOrEmpty(item.Currenttierpatched), "MmrHistory Currenttierpatched must not be empty");
                Assert.IsNotNull(item.Images, "MmrHistory Images must not be null");
                Assert.IsNotNull(item.Map, "MmrHistory Map must not be null");
                Assert.IsFalse(string.IsNullOrEmpty(item.Season_id), "MmrHistory Season_id must not be empty");
                Assert.IsTrue(item.Date_raw > 0, "MmrHistory Date_raw must be > 0");
            }
        }

        #endregion

        #region 2. Processing Contract

        [DataTestMethod]
        [DataRow("matches_competitive.json")]
        [DataRow("matches_competitive_overtime.json")]
        [DataRow("matches_competitive_shortgame.json")]
        [DataRow("matches_deathmatch.json")]
        [DataRow("matches_premier.json")]
        [DataRow("matches_swiftplay.json")]
        [DataRow("matches_unrated.json")]
        public void ProcessingContract_RealMatches_ProducesSaneRows(string filename)
        {
            string rawJson = LoadRecordedFixture(filename);
            var parsedMatch = rawJson.TryParse<JsonObjectHenrik<List<MatchJson>>>(out string matchError);
            Assert.IsNotNull(parsedMatch?.Data, $"Failed to deserialize {filename}: {matchError}");
            MatchJson match = parsedMatch.Data[0];

            string rawHistory = LoadRecordedFixture("mmr_history.json");
            var parsedHistory = rawHistory.TryParse<JsonObjectHenrik<List<MmrHistoryJson>>>(out string histError);
            Assert.IsNotNull(parsedHistory?.Data, $"Failed to deserialize mmr_history.json: {histError}");

            MmrHistoryJson mmrHistory = parsedHistory.Data.FirstOrDefault(h => h.Match_id == match.Metadata?.MatchId) ?? new MmrHistoryJson();

            // Run processing through bot's DAO factories
            Matches? matchesRow = MatchesExtension.CreateFromJson(match);
            Assert.IsNotNull(matchesRow, $"MatchesExtension.CreateFromJson produced null for {filename}");

            MatchStats? matchStatsRow = MatchStatsExtension.CreateFromJson(match, mmrHistory, TrackedPuuid);
            Assert.IsNotNull(matchStatsRow, $"MatchStatsExtension.CreateFromJson produced null for {filename} and player {TrackedPuuid}");

            MatchPlayerJson? trackedPlayer = match.Players?.All_Players?.FirstOrDefault(p => p.Puuid == TrackedPuuid);
            Assert.IsNotNull(trackedPlayer, $"Tracked player {TrackedPuuid} not found in {filename}");

            // Assert sane values
            Assert.AreEqual(match.Metadata!.Rounds_Played, matchesRow.Rounds_Played, "Matches.Rounds_Played must match JSON");
            Assert.AreEqual((byte)match.Metadata.Rounds_Played, matchStatsRow.Rounds, "MatchStats.Rounds must match JSON");
            Assert.AreEqual((byte)(trackedPlayer.Stats?.Kills ?? 0), matchStatsRow.Kills, "MatchStats.Kills must match player stats");
            Assert.AreEqual((byte)(trackedPlayer.Stats?.Deaths ?? 0), matchStatsRow.Deaths, "MatchStats.Deaths must match player stats");

            // Mode enum resolution: must map to a known Modes enum, NOT Unknown
            Modes modeEnum = ModesExtension.ModeFromString(matchesRow.Mode.Safe());
            Assert.AreNotEqual(Modes.Unknown, modeEnum, $"Mode '{matchesRow.Mode}' in {filename} resolved to Modes.Unknown");

            // Map enum resolution:
            // "Summit" (in matches_competitive.json) is 2026 content not in MapsEnum; assert current behavior (Unknown)
            // Other maps (Sunset, Abyss, Bind, Icebox) must resolve to known enum values
            Maps mapEnum = MapsExtension.MapFromString(matchesRow.Map.Safe());
            if (matchesRow.Map == "Summit")
            {
                Assert.AreEqual(Maps.Unknown, mapEnum, "Current behavior: map 'Summit' resolves to Maps.Unknown until enum is updated");
            }
            else
            {
                Assert.AreNotEqual(Maps.Unknown, mapEnum, $"Map '{matchesRow.Map}' in {filename} resolved to Maps.Unknown");
            }

            // Tracked player agent enum resolution: must map to known enum value
            Agents agentEnum = AgentsExtension.AgentFromString(matchStatsRow.Character);
            Assert.AreNotEqual(Agents.Unknown, agentEnum, $"Agent '{matchStatsRow.Character}' for tracked player in {filename} resolved to Agents.Unknown");
        }

        [TestMethod]
        [Ignore("Stale game content: Map 'Summit' in recorded matches_competitive.json is not defined in Maps enum")]
        public void ProcessingContract_StaleGameContent_SummitMap_ShouldMapToKnownMap()
        {
            Assert.AreNotEqual(Maps.Unknown, MapsExtension.MapFromString("Summit"), "Map 'Summit' should have a dedicated member in Maps enum");
        }

        [TestMethod]
        [Ignore("Stale game content: Agent 'Hush' in recorded matches_deathmatch.json is not defined in Agents enum")]
        public void ProcessingContract_StaleGameContent_HushAgent_ShouldMapToKnownAgent()
        {
            Assert.AreNotEqual(Agents.Unknown, AgentsExtension.AgentFromString("Hush"), "Agent 'Hush' should have a dedicated member in Agents enum");
        }

        #endregion

        #region 3. End to End Poll Tick

        [DataTestMethod]
        [DataRow("matches_competitive.json")]
        [DataRow("matches_competitive_overtime.json")]
        [DataRow("matches_competitive_shortgame.json")]
        [DataRow("matches_deathmatch.json")]
        [DataRow("matches_premier.json")]
        [DataRow("matches_swiftplay.json")]
        [DataRow("matches_unrated.json")]
        public async Task EndToEnd_PollTick_StoresMatchAndPostsRecap(string filename)
        {
            string matchesJson = LoadRecordedFixture(filename);
            string mmrHistoryJson = LoadRecordedFixture("mmr_history.json");

            var parsedMatch = matchesJson.TryParse<JsonObjectHenrik<List<MatchJson>>>(out _);
            Assert.IsNotNull(parsedMatch?.Data, $"Failed to parse {filename}");
            MatchJson match = parsedMatch.Data[0];
            string matchId = match.Metadata!.MatchId!;

            FixedTimeProvider clock = CreateClockShortlyAfterMatch(match);

            try
            {
                SeedUser(TrackedPuuid, TrackedUsername, TrackedTag, TrackedAffinity, TrackedDiscordId, TrackedChannelId);
                var (program, notifier, _) = CreateProgram(matchesJson, mmrHistoryJson, clock);

                await program.RunPollTickAsync();

                // DB state assertions: match and player stats row must be stored
                Assert.IsTrue(MatchesExtension.MatchIdExists(matchId), $"{filename}: Matches row should be stored in SQLite");
                Assert.IsTrue(MatchStatsExtension.MatchIdExistsForUser(matchId, TrackedPuuid), $"{filename}: MatchStats row should be stored for tracked user");

                // Outbound notification assertions: recap embed must be posted to user's channel
                Assert.IsTrue(notifier.Sent.Count >= 1, $"{filename}: At least one embed should be sent");
                var recap = notifier.Sent.FirstOrDefault(s => s.ChannelId == TrackedChannelId);
                Assert.IsNotNull(recap, $"{filename}: Notification should be sent to the tracked player's channel");
                Assert.IsNotNull(recap.Embed, $"{filename}: Sent notification must contain an embed");
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
                CleanUser(TrackedPuuid, TrackedDiscordId, TrackedChannelId);
            }
        }

        #endregion

        #region 4. Edge Files

        [TestMethod]
        public async Task EdgeCase_PollTick_MatchesEmpty_NoCrashNoPostNothingStored()
        {
            string emptyJson = LoadRecordedFixture("matches_empty.json");
            string mmrHistoryJson = LoadRecordedFixture("mmr_history.json");
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));

            try
            {
                SeedUser(TrackedPuuid, TrackedUsername, TrackedTag, TrackedAffinity, TrackedDiscordId, TrackedChannelId);
                var (program, notifier, _) = CreateProgram(emptyJson, mmrHistoryJson, clock);

                await program.RunPollTickAsync();

                Assert.AreEqual(0, notifier.Sent.Count, "No notifications should be sent when matches list is empty");
            }
            finally
            {
                CleanUser(TrackedPuuid, TrackedDiscordId, TrackedChannelId);
            }
        }

        [TestMethod]
        public async Task EdgeCase_PollTick_MatchesUnavailable_NoCrashNoPostNothingStored()
        {
            string unavailableJson = LoadRecordedFixture("matches_unavailable.json");
            string mmrHistoryJson = LoadRecordedFixture("mmr_history.json");
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));

            try
            {
                SeedUser(TrackedPuuid, TrackedUsername, TrackedTag, TrackedAffinity, TrackedDiscordId, TrackedChannelId);
                var (program, notifier, _) = CreateProgram(unavailableJson, mmrHistoryJson, clock);

                await program.RunPollTickAsync();

                Assert.AreEqual(0, notifier.Sent.Count, "No notifications should be sent when match metadata is null");
            }
            finally
            {
                CleanUser(TrackedPuuid, TrackedDiscordId, TrackedChannelId);
            }
        }

        [TestMethod]
        public async Task EdgeCase_PollTick_MatchesUnavailable_DoesNotSkipOtherUsers()
        {
            const string unavailableUserPuuid = "99999999-9999-4999-8999-999999999999";
            const ulong unavailableChannelId = 987654323UL;
            const ulong unavailableDiscordId = 111222335UL;

            string unavailableJson = LoadRecordedFixture("matches_unavailable.json");
            string swiftplayJson = LoadRecordedFixture("matches_swiftplay.json");
            string mmrHistoryJson = LoadRecordedFixture("mmr_history.json");

            var parsedSwiftplay = swiftplayJson.TryParse<JsonObjectHenrik<List<MatchJson>>>(out _);
            Assert.IsNotNull(parsedSwiftplay?.Data);
            MatchJson swiftplayMatch = parsedSwiftplay.Data[0];
            string swiftplayMatchId = swiftplayMatch.Metadata!.MatchId!;

            FixedTimeProvider clock = CreateClockShortlyAfterMatch(swiftplayMatch);

            var handler = new StubHttpMessageHandler((url, _) =>
            {
                if (url.Contains(unavailableUserPuuid, StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(unavailableJson, System.Text.Encoding.UTF8, "application/json")
                    };
                }

                if (url.Contains(TrackedPuuid, StringComparison.OrdinalIgnoreCase))
                {
                    if (url.Contains("v3/by-puuid/matches", StringComparison.OrdinalIgnoreCase))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(swiftplayJson, System.Text.Encoding.UTF8, "application/json")
                        };
                    }
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{\"status\":404,\"message\":\"Not Found\"}", System.Text.Encoding.UTF8, "application/json")
                };
            });

            try
            {
                SeedUser(unavailableUserPuuid, "UnavailableUser", "0001", TrackedAffinity, unavailableDiscordId, unavailableChannelId);
                SeedUser(TrackedPuuid, TrackedUsername, TrackedTag, TrackedAffinity, TrackedDiscordId, TrackedChannelId);

                var (program, notifier, _) = CreateProgram(handler, clock);

                await program.RunPollTickAsync();

                // User with unavailable match should have no post
                Assert.IsFalse(notifier.Sent.Any(s => s.ChannelId == unavailableChannelId), "User with unavailable match should have no post");

                // Valid user following the unavailable match must still receive recap post
                Assert.IsTrue(notifier.Sent.Any(s => s.ChannelId == TrackedChannelId), "User following an unavailable match must still receive recap post");
                Assert.IsTrue(MatchesExtension.MatchIdExists(swiftplayMatchId), "Valid match for second user must be stored in SQLite");
                Assert.IsTrue(MatchStatsExtension.MatchIdExistsForUser(swiftplayMatchId, TrackedPuuid), "MatchStats for second user must be stored in SQLite");
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(swiftplayMatchId);
                CleanUser(unavailableUserPuuid, unavailableDiscordId, unavailableChannelId);
                CleanUser(TrackedPuuid, TrackedDiscordId, TrackedChannelId);
            }
        }

        #endregion

        #region 5. Account Not Found (404 Error Envelope)

        [TestMethod]
        public void AccountNotFound_ViaTryParse_DoesNotThrowAndReturnsNullWithErrorMessage()
        {
            string raw404 = LoadRecordedFixture("account_notfound.json");

            // HenrikDev's 404 response has errors: [{"code": 22, ...}] (an array).
            // ValorantApp's JsonObjectHenrik defines Errors as ErrorsJson? (single object).
            // Characterizing behavior: TryParse catches the JsonSerializationException,
            // records the error message, and returns null without throwing.
            var parsed = raw404.TryParse<JsonObjectHenrik<AccountJson>>(out string errorMessage);

            Assert.IsNull(parsed, "TryParse should return null when errors array cannot map to single ErrorsJson object");
            Assert.IsFalse(string.IsNullOrEmpty(errorMessage), "TryParse should capture deserialization error message");
            StringAssert.Contains(errorMessage, "Cannot deserialize the current JSON array", "Error should characterize array vs object mismatch on Errors");
        }

        [TestMethod]
        public async Task AccountNotFound_ViaHenrikApiAccountQuery_ReturnsNullOn404()
        {
            string raw404 = LoadRecordedFixture("account_notfound.json");

            var handler = new StubHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(raw404, System.Text.Encoding.UTF8, "application/json")
            });

            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.henrikdev.xyz/valorant/")
            };

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(x => x.CreateClient("HenrikApiClient")).Returns(httpClient);

            var api = new HenrikApi("UnknownUser", "0000", "na", TrackedPuuid, factoryMock.Object, NullLogger<BaseValorantProgram>.Instance);

            var result = await api.AccountQuery()!;

            Assert.IsNull(result, "HenrikApi.AccountQuery() must cleanly return null when response status is 404");
        }

        #endregion
    }
}
