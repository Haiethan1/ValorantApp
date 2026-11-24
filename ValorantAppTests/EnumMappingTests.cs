using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using ValorantApp.Valorant.Enums;

namespace ValorantAppTests
{
    [TestClass]
    public class EnumMappingTests
    {
        [DataTestMethod]
        [DataRow("Brimstone", Agents.Brimstone)]
        [DataRow("brimstone", Agents.Brimstone)]
        [DataRow("Jett", Agents.Jett)]
        [DataRow("KAY/O", Agents.Kayo)]
        [DataRow("kay/o", Agents.Kayo)]
        [DataRow("Kayo", Agents.Unknown)] // Description is "KAY/O", so member name does not match
        [DataRow("Unknown", Agents.Unknown)]
        [DataRow("NotAnAgent", Agents.Unknown)]
        [DataRow("", Agents.Unknown)]
        [DataRow(null, Agents.Unknown)]
        public void AgentFromString_MapsExpected(string? input, Agents expected)
        {
            Assert.AreEqual(expected, AgentsExtension.AgentFromString(input!));
        }

        [DataTestMethod]
        [DataRow(Agents.Brimstone, "Brimstone")]
        [DataRow(Agents.Kayo, "KAY/O")]
        [DataRow(Agents.Unknown, "Unknown")]
        public void StringFromAgent_ReturnsDescription(Agents agent, string expected)
        {
            Assert.AreEqual(expected, agent.StringFromAgent());
        }

        [DataTestMethod]
        [DataRow("Ascent", Maps.Ascent)]
        [DataRow("Bind", Maps.Bind)]
        [DataRow("Corrode", Maps.Corrode)]
        [DataRow("ascent", Maps.Unknown)] // Exact case-sensitive match in switch
        [DataRow("BIND", Maps.Unknown)]
        [DataRow("InvalidMap", Maps.Unknown)]
        [DataRow("", Maps.Unknown)]
        [DataRow(null, Maps.Unknown)]
        public void MapFromString_MapsExpected(string? input, Maps expected)
        {
            Assert.AreEqual(expected, MapsExtension.MapFromString(input!));
        }

        [DataTestMethod]
        [DataRow(Maps.Ascent, "Ascent")]
        [DataRow(Maps.Bind, "Bind")]
        [DataRow(Maps.Unknown, "")]
        public void StringFromMap_ReturnsExpected(Maps map, string expected)
        {
            Assert.AreEqual(expected, map.StringFromMap());
        }

        [DataTestMethod]
        [DataRow("Competitive", Modes.Competitive)]
        [DataRow("competitive", Modes.Competitive)]
        [DataRow("Swift Play", Modes.SwiftPlay)]
        [DataRow("swiftplay", Modes.SwiftPlay)]
        [DataRow("Snowball Fight", Modes.SnowBallFight)]
        [DataRow("InvalidMode", Modes.Unknown)]
        [DataRow("", Modes.Unknown)]
        public void ModeFromString_MapsExpected(string input, Modes expected)
        {
            Assert.AreEqual(expected, ModesExtension.ModeFromString(input));
        }

        [TestMethod]
        public void ModeFromString_NullInput_ThrowsNullReferenceException()
        {
            Assert.ThrowsException<NullReferenceException>(() => ModesExtension.ModeFromString(null!));
        }

        [DataTestMethod]
        [DataRow(Modes.Competitive, "Competitive")]
        [DataRow(Modes.SwiftPlay, "SwiftPlay")]
        [DataRow(Modes.Unknown, "")]
        public void StringFromMode_ReturnsExpected(Modes mode, string expected)
        {
            Assert.AreEqual(expected, mode.StringFromMode());
        }

        [DataTestMethod]
        [DataRow("Blue", Team.Blue)]
        [DataRow("blue", Team.Blue)]
        [DataRow("Red", Team.Red)]
        [DataRow("RED", Team.Red)]
        [DataRow(" blue ", Team.Blue)]
        [DataRow("InvalidTeam", Team.Unknown)]
        [DataRow("", Team.Unknown)]
        public void TeamFromString_MapsExpected(string input, Team expected)
        {
            Assert.AreEqual(expected, TeamExtension.TeamFromString(input));
        }

        [TestMethod]
        public void TeamFromString_NullInput_ThrowsNullReferenceException()
        {
            Assert.ThrowsException<NullReferenceException>(() => TeamExtension.TeamFromString(null!));
        }

        [DataTestMethod]
        [DataRow(Team.Blue, "Blue")]
        [DataRow(Team.Red, "Red")]
        [DataRow(Team.Unknown, "")]
        public void StringFromTeam_ReturnsExpected(Team team, string expected)
        {
            Assert.AreEqual(expected, team.StringFromTeam());
        }
    }
}
