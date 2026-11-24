using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    }
}
