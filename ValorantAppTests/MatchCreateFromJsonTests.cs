using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using ValorantApp;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;
using ValorantApp.GenericExtensions;
using ValorantApp.HenrikJson;

namespace ValorantAppTests
{
    [TestClass]
    public class MatchCreateFromJsonTests
    {
        private const string EthanPuuid = "8c7276bd-a70b-56bf-803b-7e0fd9dba287";
        private const string LawnmowerPuuid = "75bb7e82-e8f4-539e-96b3-8ebf547009c4";

        internal static MatchJson LoadMatch(string filename)
        {
            string json = File.ReadAllText(Path.Combine("../../../MockJsonResponse", filename));
            var obj = json.TryParse<JsonObjectHenrik<List<MatchJson>>>(out string error);
            Assert.IsNotNull(obj?.Data, $"Failed to parse fixture {filename}: {error}");
            return obj.Data[0];
        }

        [TestMethod]
        public void CreateFromJson_Competitive_Success()
        {
            var matchJson = LoadMatch("Matches_11_4_2023.json");
            var mmr = new MmrHistoryJson { Mmr_change_to_last_game = 18, Currenttier = 19 };

            var matches = MatchesExtension.CreateFromJson(matchJson);
            Assert.IsNotNull(matches);
            Assert.AreEqual("26e19cf3-4eec-42a5-864e-1b31a0f630de", matches.Match_Id);
            Assert.AreEqual("Lotus", matches.Map);
            Assert.AreEqual("Competitive", matches.Mode);
            Assert.AreEqual("competitive", matches.Mode_Id);
            Assert.AreEqual(1871u, matches.Game_Length);
            Assert.AreEqual(1699144485L, matches.Game_Start);
            // NOTE: possible bug: MatchesExtension.cs:173 parses Game_Start_Patched without converting to UTC
            Assert.AreEqual(new DateTime(2023, 11, 5, 0, 34, 0), matches.Game_Start_Patched_UTC);
            Assert.AreEqual(21, matches.Rounds_Played);
            Assert.AreEqual((byte)13, matches.Blue_Team_Rounds_Won);
            Assert.AreEqual((byte)8, matches.Red_Team_Rounds_Won);
            Assert.AreEqual((short)18, matches.Blue_Team_Average_Rank);
            Assert.AreEqual((short)17, matches.Red_Team_Average_Rank);
            Assert.AreEqual(true, matches.Blue_Team_Win);
            Assert.AreEqual("4401f9fd-4170-2e4c-4bc3-f3b4d7d150d1", matches.Season_Id);

            var stats = MatchStatsExtension.CreateFromJson(matchJson, mmr, EthanPuuid);
            Assert.IsNotNull(stats);
            Assert.AreEqual("26e19cf3-4eec-42a5-864e-1b31a0f630de", stats.Match_id);
            Assert.AreEqual(EthanPuuid, stats.Val_puuid);
            Assert.AreEqual("Lotus", stats.Map);
            Assert.AreEqual("Competitive", stats.Mode);
            Assert.AreEqual((byte)21, stats.Rounds);
            Assert.AreEqual("Skye", stats.Character);
            Assert.AreEqual(18, stats.Rr_change);
            Assert.AreEqual((byte)3, stats.Double_Kills);
            Assert.AreEqual((byte)2, stats.Triple_Kills);
            Assert.AreEqual((byte)0, stats.Quad_Kills);
            Assert.AreEqual((byte)0, stats.Aces);
            Assert.AreEqual((byte)19, stats.Kills);
            // NOTE: possible bug: MatchStatsExtension.cs:515-518 knife kills/deaths and ally damage hardcoded to 0
            Assert.AreEqual((byte)0, stats.Knife_Kills);
            Assert.AreEqual((byte)15, stats.Deaths);
            Assert.AreEqual((byte)0, stats.Knife_Deaths);
            Assert.AreEqual((byte)13, stats.Assists);
            Assert.AreEqual(52.5, stats.Bodyshots, 0.001);
            Assert.AreEqual(42.5, stats.Headshots, 0.001);
            Assert.AreEqual((short)5840, stats.Score);
            Assert.AreEqual((short)4028, stats.Damage);
            Assert.AreEqual((byte)5, stats.C_casts);
            Assert.AreEqual((byte)9, stats.Q_casts);
            Assert.AreEqual((byte)36, stats.E_casts);
            Assert.AreEqual((byte)4, stats.X_casts);
            Assert.AreEqual((short)0, stats.Damage_To_Allies);
            Assert.AreEqual((short)0, stats.Damage_From_Allies);
            Assert.AreEqual(1871u, stats.Game_Length);
            // NOTE: possible bug: MatchStatsExtension.cs:487-488 converts parsed UTC timestamp to Eastern Standard Time
            Assert.AreEqual(new DateTime(2023, 11, 4, 20, 34, 0), stats.Game_Start_Patched);
            Assert.IsFalse(stats.MVP);
            Assert.AreEqual((byte)22, stats.Current_Tier);
            Assert.AreEqual("Blue", stats.Team);
            Assert.AreEqual((byte)19, stats.New_Tier);
        }

        [TestMethod]
        public void CreateFromJson_Swiftplay_Success()
        {
            var matchJson = LoadMatch("Matches_10_31_2023.json");

            var matches = MatchesExtension.CreateFromJson(matchJson);
            Assert.IsNotNull(matches);
            Assert.AreEqual("a492c7d9-4ecc-4ac1-94d7-cc25256e8a8f", matches.Match_Id);
            Assert.AreEqual("Ascent", matches.Map);
            Assert.AreEqual("Swiftplay", matches.Mode);
            Assert.AreEqual("swiftplay", matches.Mode_Id);
            Assert.AreEqual(676u, matches.Game_Length);
            Assert.AreEqual(1698852319L, matches.Game_Start);
            Assert.AreEqual(new DateTime(2023, 11, 1, 15, 25, 0), matches.Game_Start_Patched_UTC);
            Assert.AreEqual(8, matches.Rounds_Played);
            Assert.AreEqual((byte)5, matches.Blue_Team_Rounds_Won);
            Assert.AreEqual((byte)3, matches.Red_Team_Rounds_Won);
            Assert.AreEqual((short)0, matches.Blue_Team_Average_Rank);
            Assert.AreEqual((short)0, matches.Red_Team_Average_Rank);
            Assert.AreEqual(true, matches.Blue_Team_Win);
            Assert.AreEqual("4401f9fd-4170-2e4c-4bc3-f3b4d7d150d1", matches.Season_Id);

            // Null mmr verified: Rr_change defaults to 0 and New_Tier defaults to 0
            var stats = MatchStatsExtension.CreateFromJson(matchJson, null, EthanPuuid);
            Assert.IsNotNull(stats);
            Assert.AreEqual("a492c7d9-4ecc-4ac1-94d7-cc25256e8a8f", stats.Match_id);
            Assert.AreEqual(EthanPuuid, stats.Val_puuid);
            Assert.AreEqual("Ascent", stats.Map);
            Assert.AreEqual("Swiftplay", stats.Mode);
            Assert.AreEqual((byte)8, stats.Rounds);
            Assert.AreEqual("Reyna", stats.Character);
            Assert.AreEqual(0, stats.Rr_change);
            Assert.AreEqual((byte)1, stats.Double_Kills);
            Assert.AreEqual((byte)1, stats.Triple_Kills);
            Assert.AreEqual((byte)0, stats.Quad_Kills);
            Assert.AreEqual((byte)0, stats.Aces);
            Assert.AreEqual((byte)6, stats.Kills);
            Assert.AreEqual((byte)0, stats.Knife_Kills);
            Assert.AreEqual((byte)6, stats.Deaths);
            Assert.AreEqual((byte)0, stats.Knife_Deaths);
            Assert.AreEqual((byte)1, stats.Assists);
            Assert.AreEqual(36.3636, stats.Bodyshots, 0.001);
            Assert.AreEqual(63.6363, stats.Headshots, 0.001);
            Assert.AreEqual((short)1817, stats.Score);
            Assert.AreEqual((short)1255, stats.Damage);
            Assert.AreEqual((byte)4, stats.C_casts);
            Assert.AreEqual((byte)2, stats.Q_casts);
            Assert.AreEqual((byte)2, stats.E_casts);
            Assert.AreEqual((byte)1, stats.X_casts);
            Assert.AreEqual((short)0, stats.Damage_To_Allies);
            Assert.AreEqual((short)0, stats.Damage_From_Allies);
            Assert.AreEqual(676u, stats.Game_Length);
            Assert.AreEqual(new DateTime(2023, 11, 1, 11, 25, 0), stats.Game_Start_Patched);
            Assert.IsFalse(stats.MVP);
            Assert.AreEqual((byte)0, stats.Current_Tier);
            Assert.AreEqual("Red", stats.Team);
            Assert.AreEqual((byte)0, stats.New_Tier);
        }

        [TestMethod]
        public void CreateFromJson_Deathmatch_Success()
        {
            var matchJson = LoadMatch("Deathmatch_1_10_2024.json");

            var matches = MatchesExtension.CreateFromJson(matchJson);
            Assert.IsNotNull(matches);
            Assert.AreEqual("c2c4a94d-6750-4e16-b9bf-ed7236b35a56", matches.Match_Id);
            Assert.AreEqual("Bind", matches.Map);
            Assert.AreEqual("Deathmatch", matches.Mode);
            Assert.AreEqual("deathmatch", matches.Mode_Id);
            Assert.AreEqual(517u, matches.Game_Length);
            Assert.AreEqual(1704942720L, matches.Game_Start);
            Assert.AreEqual(new DateTime(2024, 1, 11, 3, 11, 0), matches.Game_Start_Patched_UTC);
            Assert.AreEqual(1, matches.Rounds_Played);
            Assert.AreEqual((byte)0, matches.Blue_Team_Rounds_Won);
            Assert.AreEqual((byte)0, matches.Red_Team_Rounds_Won);
            // NOTE: possible bug: MatchesExtension.cs:220 assigns all-player avg rank to Blue_Team_Average_Rank in DM
            Assert.AreEqual((short)0, matches.Blue_Team_Average_Rank);
            Assert.AreEqual((short)0, matches.Red_Team_Average_Rank);
            Assert.AreEqual(false, matches.Blue_Team_Win);
            Assert.AreEqual("ec876e6c-43e8-fa63-ffc1-2e8d4db25525", matches.Season_Id);

            var stats = MatchStatsExtension.CreateFromJson(matchJson, null, LawnmowerPuuid);
            Assert.IsNotNull(stats);
            Assert.AreEqual("c2c4a94d-6750-4e16-b9bf-ed7236b35a56", stats.Match_id);
            Assert.AreEqual(LawnmowerPuuid, stats.Val_puuid);
            Assert.AreEqual("Bind", stats.Map);
            Assert.AreEqual("Deathmatch", stats.Mode);
            Assert.AreEqual((byte)1, stats.Rounds);
            Assert.AreEqual("Skye", stats.Character);
            Assert.AreEqual(0, stats.Rr_change);
            Assert.AreEqual((byte)0, stats.Double_Kills);
            Assert.AreEqual((byte)0, stats.Triple_Kills);
            Assert.AreEqual((byte)0, stats.Quad_Kills);
            // NOTE: possible bug: MatchStatsExtension.cs:556 kills >= 5 in deathmatch round is counted as an ace
            Assert.AreEqual((byte)1, stats.Aces);
            Assert.AreEqual((byte)25, stats.Kills);
            Assert.AreEqual((byte)0, stats.Knife_Kills);
            Assert.AreEqual((byte)30, stats.Deaths);
            Assert.AreEqual((byte)0, stats.Knife_Deaths);
            Assert.AreEqual((byte)3, stats.Assists);
            Assert.AreEqual(0.0, stats.Bodyshots, 0.001);
            Assert.AreEqual(0.0, stats.Headshots, 0.001);
            Assert.AreEqual((short)7734, stats.Score);
            Assert.AreEqual((short)0, stats.Damage);
            Assert.AreEqual((byte)0, stats.C_casts);
            Assert.AreEqual((byte)0, stats.Q_casts);
            Assert.AreEqual((byte)0, stats.E_casts);
            Assert.AreEqual((byte)0, stats.X_casts);
            Assert.AreEqual((short)0, stats.Damage_To_Allies);
            Assert.AreEqual((short)0, stats.Damage_From_Allies);
            Assert.AreEqual(517u, stats.Game_Length);
            Assert.AreEqual(new DateTime(2024, 1, 10, 22, 11, 0), stats.Game_Start_Patched);
            Assert.IsFalse(stats.MVP);
            Assert.AreEqual((byte)0, stats.Current_Tier);
            Assert.AreEqual(LawnmowerPuuid, stats.Team);
            Assert.AreEqual((byte)0, stats.New_Tier);
        }

        [TestMethod]
        public void CreateFromJson_NullOrEmptyInputs_ReturnsNull()
        {
            Assert.IsNull(MatchesExtension.CreateFromJson(null));
            Assert.IsNull(MatchesExtension.CreateFromJson(new MatchJson()));
            Assert.IsNull(MatchesExtension.CreateFromJson(new MatchJson { Metadata = new MatchMetadataJson() }));

            Assert.IsNull(MatchStatsExtension.CreateFromJson(null, null, EthanPuuid));
            Assert.IsNull(MatchStatsExtension.CreateFromJson(new MatchJson(), null, EthanPuuid));
            Assert.IsNull(MatchStatsExtension.CreateFromJson(new MatchJson { Players = new MatchPlayersJson() }, null, EthanPuuid));
        }

        [TestMethod]
        public void CreateFromJson_UnknownPuuid_ReturnsNull()
        {
            var matchJson = LoadMatch("Matches_11_4_2023.json");
            var stats = MatchStatsExtension.CreateFromJson(matchJson, null, "unknown-puuid-00000000");
            Assert.IsNull(stats);
        }
    }
}
