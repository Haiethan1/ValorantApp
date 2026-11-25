using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;
using ValorantApp.Valorant.Enums;

namespace ValorantAppTests
{
    /// <summary>
    /// Component tests for DbSettings: with SQL Server disabled (see TestSetup) the DAOs work purely
    /// against the temp SQLite file and never open a SQL Server connection.
    /// </summary>
    [TestClass]
    public class DbSettingsTests
    {
        [TestMethod]
        public void SqlServer_IsDisabledInTests()
        {
            Assert.IsFalse(DbSettings.SqlServerEnabled);
        }

        [TestMethod]
        public void DualWriteDao_WorksAgainstSqliteOnly()
        {
            var user = new ValorantUsers("tester", "0001", "na", "puuid-dbsettings-test", 1234567890UL);
            var mapping = new ValorantChannelMappings(user.Val_puuid, 42UL);

            Assert.IsTrue(ValorantUsersExtension.InsertRow(user));
            Assert.IsTrue(ValorantChannelMappingsExtension.InsertRow(mapping));

            Assert.AreEqual("tester", ValorantUsersExtension.GetRow(user.Val_puuid)?.Val_username);
            CollectionAssert.AreEqual(new[] { 42UL }, new System.Collections.Generic.List<ulong>(ValorantChannelMappingsExtension.GetRowDiscordId(user.Val_puuid)));

            Assert.IsTrue(ValorantChannelMappingsExtension.RemoveRow(mapping));
            Assert.IsTrue(ValorantUsersExtension.DeleteRow(user.Val_puuid, user.Disc_id));
            Assert.IsNull(ValorantUsersExtension.GetRow(user.Val_puuid));
        }

        [TestMethod]
        public void SqlServerConnectionString_ThrowsWhenDisabled()
        {
            Assert.IsFalse(DbSettings.SqlServerEnabled);
            Assert.ThrowsException<InvalidOperationException>(() => DbSettings.SqlServer);
        }

        [TestMethod]
        public void AppConfig_IsNotCopiedIntoTestOutput()
        {
            // ValorantApp.dll.config is built from App.config, which holds real secrets locally. The test csproj
            // excludes it (AllowedReferenceRelatedFileExtensions) so no test can ever pick up prod settings.
            string path = Path.Combine(AppContext.BaseDirectory, "ValorantApp.dll.config");
            Assert.IsFalse(File.Exists(path), $"{path} should not be in the test output. Check ValorantAppTests.csproj.");
        }

        [DataTestMethod]
        [DataRow("Server=nas-host,1433;Database=Valorant;User Id=sa;Password=x")]
        [DataRow("Server=192.168.1.20,1433;Database=Valorant")]
        [DataRow("Server=tcp:valorant.example.com,1433;Database=Valorant")]
        public void SqlServerContainerGuard_RejectsNonLocalServers(string connectionString)
        {
            // The only way a test may enable SQL Server is SqlServerContainer.Enable, which must refuse anything that
            // isn't the local Testcontainers instance.
            Assert.ThrowsException<AssertFailedException>(() => SqlServerContainer.AssertLocal(connectionString));
        }

        [DataTestMethod]
        [DataRow("Server=127.0.0.1,49153;Database=Valorant")]
        [DataRow("Server=localhost,49153;Database=Valorant")]
        public void SqlServerContainerGuard_AcceptsLocalServers(string connectionString)
        {
            SqlServerContainer.AssertLocal(connectionString);
        }

        [TestMethod]
        public void SqlServerOnlyDaos_AreNoOps()
        {
            Assert.AreEqual(0, MatchEventsExtension.EventTotalCount());
            Assert.IsFalse(MatchEventsExtension.InsertRow("match", 1, EventType.Kill, 0, null, null, null, out long eventIndex));
            Assert.AreEqual(0L, eventIndex);

            MatchEventsExtension.DeleteRows(90);
            ValorantPlayersExtension.InsertRow("puuid");
            MatchPlayersExtension.InsertRow("match", "puuid", 0, Agents.Jett);
            PlayerLocationsExtension.InsertRow(1, "puuid", 0, 0, null);
        }

        [TestMethod]
        public void GetListOfRows_MultipleIds_ReturnsMatchingMatches()
        {
            string matchId1 = "match-getlist-1";
            string matchId2 = "match-getlist-2";
            var match1 = new Matches(matchId1, "Lotus", "Competitive", "competitive", 1800u, 1699144485L, DateTime.UtcNow, 20, 13, 7, 18, 17, true, "season-1");
            var match2 = new Matches(matchId2, "Bind", "Competitive", "competitive", 1900u, 1699144485L, DateTime.UtcNow, 24, 11, 13, 15, 16, false, "season-1");

            try
            {
                MatchesExtension.InsertRow(match1);
                MatchesExtension.InsertRow(match2);

                var results = MatchesExtension.GetListOfRows(new[] { matchId1, matchId2 }).ToList();
                Assert.AreEqual(2, results.Count);
                Assert.IsTrue(results.Any(m => m.Match_Id == matchId1));
                Assert.IsTrue(results.Any(m => m.Match_Id == matchId2));
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId1);
                DevToolsExtension.DeleteMatchEverywhere(matchId2);
            }
        }
    }
}
