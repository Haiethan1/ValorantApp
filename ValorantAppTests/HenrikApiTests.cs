using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System;
using System.Configuration;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using ValorantApp;
using ValorantApp.HenrikJson;
using ValorantApp.Valorant;
using ValorantApp.Valorant.Enums;

namespace ValorantAppTests
{
    [TestClass]
    public class HenrikApiTests
    {
        private readonly string _username = "Ehtan";
        private readonly string _tagName = "NA1";
        private readonly string _affinity = "na";
        private readonly string _puuid = "8c7276bd-a70b-56bf-803b-7e0fd9dba287";

        private MockRepository _mockRepository;

        private Mock<HttpMessageHandler> _handlerMock;
        private HttpClient _httpClient;

        #region Initialize / Helpers

        [TestInitialize]
        public void Initialize()
        {
            _mockRepository = new(MockBehavior.Default);
            _handlerMock = _mockRepository.Create<HttpMessageHandler>();
        }

        private HenrikApi CreateHenrikApi()
        {
            _httpClient = new HttpClient(_handlerMock.Object);
            var mock = new Mock<ILogger<BaseValorantProgram>>();
            return new HenrikApi(_username, _tagName, _affinity, _puuid, _httpClient, null, mock.Object);
        }

        private void SetEndpoint(string endpoint, string content)
        {
            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri == new Uri(endpoint)),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(content)
                })
                .Verifiable();
        }

        private void TestNotNullableProperties<T>(T myObject)
        {
            Type type = typeof(T);
            PropertyInfo[] properties = type.GetProperties();

            foreach (PropertyInfo property in properties)
            {
                // Check if the property is not nullable and not a string
                if (!IsNullable(property.PropertyType) && property.PropertyType != typeof(string))
                {
                    var propertyValue = property.GetValue(myObject);

                    // Use Xunit.Assert to check if the property is not null
                    Assert.IsNotNull(propertyValue, property.Name);
                }
            }
        }

        private static bool IsNullable(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                // It's a nullable value type, e.g., int?
                return true;
            }

            // It's a reference type (class or interface)
            return !type.IsValueType;
        }

        #endregion

        #region Test Methods

        #region v1 Account

        [TestMethod]
        public void AccountQueryTest_Success()
        {
            string endpoint = $"https://api.henrikdev.xyz/valorant/v1/account/{_username}/{_tagName}";
            SetEndpoint(endpoint, V1AccountMock);
            HenrikApi api = CreateHenrikApi();
            
            var accountJson = api.AccountQuery()?.Result.Data;
            Assert.IsNotNull(accountJson);
            Assert.AreEqual(_puuid, accountJson.Puuid);
            Assert.AreEqual(_username, accountJson.Name);
            Assert.AreEqual(_tagName, accountJson.Tag);
            Assert.AreEqual(_affinity, accountJson.Region);
        }

        #endregion

        #region v3 Matches

        [TestMethod]
        public void MatchTest_Size1_Success()
        {
            string endpoint = $"https://api.henrikdev.xyz/valorant/v3/by-puuid/matches/{_affinity}/{_puuid}?&size=1";
            SetEndpoint(endpoint, V3Matches_Size_1);
            HenrikApi api = CreateHenrikApi();

            var matchJson = api.Match()?.Result.Data;
            Assert.IsNotNull(matchJson);
            CheckMatchNulls(matchJson);
        }

        [TestMethod]
        public void MatchTest_Size3_Success()
        {
            string endpoint = $"https://api.henrikdev.xyz/valorant/v3/by-puuid/matches/{_affinity}/{_puuid}?&size=3";
            SetEndpoint(endpoint, V3Matches_Size_3);
            HenrikApi api = CreateHenrikApi();

            var matchJson = api.Match(size: 3)?.Result.Data;
            Assert.IsNotNull(matchJson);
            CheckMatchNulls(matchJson);
        }

        [TestMethod]
        public void MatchTest_Premier_Success()
        {
            string endpoint = $"https://api.henrikdev.xyz/valorant/v3/by-puuid/matches/{_affinity}/{_puuid}?&mode=Premier&size=1";
            SetEndpoint(endpoint, V3Matches_Premier);
            HenrikApi api = CreateHenrikApi();

            var matchJson = api.Match(mode: Modes.Premier)?.Result.Data;
            Assert.IsNotNull(matchJson);
            CheckMatchNulls(matchJson);
        }

        private void CheckMatchNulls(List<MatchJson> matchList)
        {
            foreach (MatchJson match in matchList)
            {
                Assert.IsNotNull(match);
                TestNotNullableProperties(match);
                
                TestNotNullableProperties(match.Metadata);
                TestNotNullableProperties(match.Metadata.Premier_Info);

                TestNotNullableProperties(match.Players);
                CheckMatchPlayerNulls(match.Players.All_Players);
                CheckMatchPlayerNulls(match.Players.Red);
                CheckMatchPlayerNulls(match.Players.Blue);

                CheckMatchObserverNulls(match.Observers);

                CheckMatchCoachesNulls(match.Coaches);

                TestNotNullableProperties(match.Teams);
                CheckMatchTeamNulls(match.Teams.Red);
                CheckMatchTeamNulls(match.Teams.Blue);

                CheckMatchRoundsNulls(match.Rounds);
            }
        }

        private void CheckMatchPlayerNulls(MatchPlayerJson[] players)
        {
            foreach (MatchPlayerJson player in players)
            {
                TestNotNullableProperties(player);
                TestNotNullableProperties(player.Session_Playtime);
                TestNotNullableProperties(player.Assets);
                TestNotNullableProperties(player.Assets.Card);
                TestNotNullableProperties(player.Assets.Agent);
                TestNotNullableProperties(player.Behavior);
                TestNotNullableProperties(player.Behavior.Friendly_Fire);
                TestNotNullableProperties(player.Platform);
                TestNotNullableProperties(player.Platform.OS);
                TestNotNullableProperties(player.Ability_Casts);
                TestNotNullableProperties(player.Stats);
                TestNotNullableProperties(player.Economy);
                TestNotNullableProperties(player.Economy.Spent);
                TestNotNullableProperties(player.Economy.Loadout_Value);
            }
        }

        private void CheckMatchObserverNulls(MatchObserverJson[] observers)
        {
            foreach (MatchObserverJson observer in observers)
            {
                TestNotNullableProperties(observer);
                TestNotNullableProperties(observer.Platform);
                TestNotNullableProperties(observer.Platform.OS);
                TestNotNullableProperties(observer.Session_Playtime);
            }
        }

        private void CheckMatchCoachesNulls(MatchCoachJson[] coaches)
        {
            foreach (MatchCoachJson coach in coaches)
            {
                TestNotNullableProperties(coach);
            }
        }

        private void CheckMatchTeamNulls(MatchTeamJson team)
        {
            TestNotNullableProperties(team);
            if (team.Roster == null)
            {
                return;
            }

            TestNotNullableProperties(team.Roster);
            TestNotNullableProperties(team.Roster.Customization);
        }

        private void CheckMatchRoundsNulls(MatchRoundsJson[] rounds)
        {
            foreach(MatchRoundsJson round in rounds)
            {
                TestNotNullableProperties(round);

                if (round.Plant_Events != null)
                {
                    TestNotNullableProperties(round.Plant_Events);
                    if(round.Plant_Events.Plant_Location != null)
                    {
                        TestNotNullableProperties(round.Plant_Events.Plant_Location);
                    }
                    if (round.Plant_Events.Planted_By != null)
                    {
                        TestNotNullableProperties(round.Plant_Events.Planted_By);
                    }
                    if (round.Plant_Events.Player_Locations_On_Plant != null)
                    {
                        TestNotNullableProperties(round.Plant_Events.Player_Locations_On_Plant);
                        foreach (RoundPlayerLocationOnEventJson playerLocation in round.Plant_Events.Player_Locations_On_Plant)
                        {
                            TestNotNullableProperties(playerLocation.Location);
                        }
                    }
                }

                if (round.Defuse_Events != null)
                {
                    TestNotNullableProperties(round.Defuse_Events);

                    if (round.Defuse_Events.Defuse_Location != null)
                    {
                        TestNotNullableProperties(round.Defuse_Events.Defuse_Location);
                    }

                    if (round.Defuse_Events.Defused_By != null)
                    {
                        TestNotNullableProperties(round.Defuse_Events.Defused_By);
                    }

                    if (round.Defuse_Events.Player_Locations_On_Defuse != null)
                    {
                        TestNotNullableProperties(round.Defuse_Events.Player_Locations_On_Defuse);
                        foreach (RoundPlayerLocationOnEventJson playerLocation in round.Defuse_Events.Player_Locations_On_Defuse)
                        {
                            TestNotNullableProperties(playerLocation.Location);
                        }
                    }
                }

                if (round.Player_Stats != null)
                {
                    TestNotNullableProperties(round.Player_Stats);

                    foreach (MatchRoundPlayerStatsJson playerStat in round.Player_Stats)
                    {
                        // todo write tests.
                        TestNotNullableProperties(playerStat.Ability_Casts);

                        foreach (RoundDamageEventsJson roundDamage in playerStat.Damage_Events)
                        {
                            TestNotNullableProperties(roundDamage);
                        }

                        foreach (RoundKillEventsJson killEvent in playerStat.Kill_Events)
                        {
                            TestNotNullableProperties(killEvent);
                            TestNotNullableProperties(killEvent.Victim_Death_Location);
                            TestNotNullableProperties(killEvent.Damage_Weapon_Assets);
                            TestNotNullableProperties(killEvent.Damage_Weapon_Assets.Card);
                            TestNotNullableProperties(killEvent.Damage_Weapon_Assets.Agent);

                            foreach (RoundPlayerLocationOnEventJson playerLocation in killEvent.Player_Locations_On_kill)
                            {
                                TestNotNullableProperties(playerLocation);
                                TestNotNullableProperties(playerLocation.Location);
                            }

                            foreach (RoundPlayerStatsAssistantsJson playerStats in killEvent.Assistants)
                            {
                                TestNotNullableProperties(playerStats);
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #endregion

        #region Api responses

        #region v1 Account

        private readonly string V1AccountMock = $@"
        {{
            ""status"": 200,
            ""data"": {{
                ""puuid"": ""8c7276bd-a70b-56bf-803b-7e0fd9dba287"",
                ""region"": ""na"",
                ""account_level"": 314,
                ""name"": ""Ehtan"",
                ""tag"": ""NA1"",
                ""card"": {{
                    ""small"": ""https://media.valorant-api.com/playercards/5a33d85c-42c9-9b75-44c3-e0a447a8a894/smallart.png"",
                    ""large"": ""https://media.valorant-api.com/playercards/5a33d85c-42c9-9b75-44c3-e0a447a8a894/largeart.png"",
                    ""wide"": ""https://media.valorant-api.com/playercards/5a33d85c-42c9-9b75-44c3-e0a447a8a894/wideart.png"",
                    ""id"": ""5a33d85c-42c9-9b75-44c3-e0a447a8a894""
                }},
                ""last_update"": ""Now"",
                ""last_update_raw"": 1698807788
            }}
        }}";

        #endregion

        #region v3 Matches

        private readonly string V3Matches_Size_1 = File.ReadAllText("../../.././MockJsonResponse/Matches_11_4_2023.json");
        private readonly string V3Matches_Size_3 = File.ReadAllText("../../.././MockJsonResponse/Matches_10_31_2023.json");
        private readonly string V3Matches_Premier = File.ReadAllText("../../.././MockJsonResponse/Matches_11_2_2023.json");

        #endregion

        #endregion
    }
}