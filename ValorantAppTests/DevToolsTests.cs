using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using ValorantApp.Database.Extensions;

namespace ValorantAppTests
{
    /// <summary>
    /// Component tests for DevToolsExtension against the temp SQLite DB (SQL Server disabled, see TestSetup).
    /// </summary>
    [TestClass]
    public class DevToolsTests
    {
        [TestMethod]
        public void DeleteMatchEverywhere_ForgetsMatchSoPollerWouldReprocessIt()
        {
            var matchJson = MatchCreateFromJsonTests.LoadMatch("Matches_10_31_2023.json");
            string matchId = matchJson.Metadata!.MatchId!;
            string puuid = matchJson.Players!.All_Players!.First().Puuid!;

            var matches = MatchesExtension.CreateFromJson(matchJson)!;
            var stats = MatchStatsExtension.CreateFromJson(matchJson, null, puuid)!;
            MatchesExtension.InsertRow(matches);
            MatchStatsExtension.InsertRow(stats);

            try
            {
                Assert.IsTrue(MatchesExtension.MatchIdExists(matchId));
                Assert.IsTrue(MatchStatsExtension.MatchIdExistsForUser(matchId, puuid));
                Assert.AreEqual(matchId, DevToolsExtension.GetLatestMatchIdForUser(puuid));

                Assert.IsTrue(DevToolsExtension.DeleteMatchEverywhere(matchId));

                // These are the exact checks the poller uses to decide a match is new.
                Assert.IsFalse(MatchesExtension.MatchIdExists(matchId));
                Assert.IsFalse(MatchStatsExtension.MatchIdExistsForUser(matchId, puuid));
                Assert.IsNull(DevToolsExtension.GetLatestMatchIdForUser(puuid));
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
            }
        }

        [TestMethod]
        public void DeleteMatchEverywhere_UnknownMatch_ReturnsFalse()
        {
            Assert.IsFalse(DevToolsExtension.DeleteMatchEverywhere("no-such-match"));
        }
    }
}
