using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using ValorantApp;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;
using ValorantApp.HenrikJson;
using ValorantApp.Valorant.Enums;

namespace ValorantAppTests
{
    /// <summary>
    /// Dual writes against a real SQL Server (the Testcontainers instance from <see cref="SqlServerContainer"/>, never
    /// the NAS). Each test enables SQL Server only inside its scope and starts from empty tables.
    /// </summary>
    [TestClass]
    [TestCategory(SqlServerContainer.Category)]
    [DoNotParallelize]
    public class SqlServerDualWriteTests
    {
        private IDisposable? _sqlServer;

        [TestInitialize]
        public void Init()
        {
            _sqlServer = SqlServerContainer.Enable();
            SqlServerContainer.ClearAllTables();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _sqlServer?.Dispose();
            Assert.IsFalse(DbSettings.SqlServerEnabled, "SQL Server must be disabled again after each test");
        }

        [TestMethod]
        public void SqlServer_IsTheLocalContainer()
        {
            Assert.IsTrue(DbSettings.SqlServerEnabled);
            Assert.AreEqual(SqlServerContainer.ConnectionString, DbSettings.SqlServer);
            Assert.AreEqual("Valorant", SqlServerContainer.Scalar("SELECT DB_NAME()"));
        }

        [TestMethod]
        public void ValorantUsers_InsertAndDelete_AreMirroredToSqlServer()
        {
            var user = new ValorantUsers("sqltester", "0001", "na", "puuid-sqlserver-users", 123456789012345678UL);

            try
            {
                Assert.IsTrue(ValorantUsersExtension.InsertRow(user));

                Assert.AreEqual(1, SqlServerContainer.Scalar("SELECT COUNT(*) FROM lu_valorant_users WHERE val_puuid = @p", ("@p", user.Val_puuid)));
                Assert.AreEqual("sqltester", SqlServerContainer.Scalar("SELECT val_username FROM lu_valorant_users WHERE val_puuid = @p", ("@p", user.Val_puuid)));
                Assert.AreEqual(123456789012345678L, SqlServerContainer.Scalar("SELECT disc_id FROM lu_valorant_users WHERE val_puuid = @p", ("@p", user.Val_puuid)));

                // Inserting the same user again must not duplicate the SQL Server row.
                ValorantUsersExtension.InsertRow(user);
                Assert.AreEqual(1, SqlServerContainer.Scalar("SELECT COUNT(*) FROM lu_valorant_users WHERE val_puuid = @p", ("@p", user.Val_puuid)));

                Assert.IsTrue(ValorantUsersExtension.DeleteRow(user.Val_puuid, user.Disc_id));
                Assert.AreEqual(0, SqlServerContainer.Scalar("SELECT COUNT(*) FROM lu_valorant_users WHERE val_puuid = @p", ("@p", user.Val_puuid)));
            }
            finally
            {
                ValorantUsersExtension.DeleteRow(user.Val_puuid, user.Disc_id);
            }
        }

        [TestMethod]
        public void ValorantUsers_Update_IsMirroredToSqlServer()
        {
            var user = new ValorantUsers("origName", "0001", "na", "puuid-sqlserver-update", 123456789012345678UL);

            try
            {
                Assert.IsTrue(ValorantUsersExtension.InsertRow(user));
                Assert.AreEqual("origName", SqlServerContainer.Scalar("SELECT val_username FROM lu_valorant_users WHERE val_puuid = @p", ("@p", user.Val_puuid)));
                Assert.AreEqual("0001", SqlServerContainer.Scalar("SELECT val_tagname FROM lu_valorant_users WHERE val_puuid = @p", ("@p", user.Val_puuid)));

                var updatedUser = new ValorantUsers("updatedName", "9999", "na", user.Val_puuid, user.Disc_id);
                Assert.IsTrue(ValorantUsersExtension.UpdateRow(updatedUser));

                Assert.AreEqual("updatedName", SqlServerContainer.Scalar("SELECT val_username FROM lu_valorant_users WHERE val_puuid = @p", ("@p", user.Val_puuid)));
                Assert.AreEqual("9999", SqlServerContainer.Scalar("SELECT val_tagname FROM lu_valorant_users WHERE val_puuid = @p", ("@p", user.Val_puuid)));
            }
            finally
            {
                ValorantUsersExtension.DeleteRow(user.Val_puuid, user.Disc_id);
            }
        }

        [TestMethod]
        public void ValorantChannelMappings_InsertAndRemove_AreMirroredToSqlServer()
        {
            var mapping = new ValorantChannelMappings("puuid-sqlserver-channel", 987654321098765432UL);

            try
            {
                Assert.IsTrue(ValorantChannelMappingsExtension.InsertRow(mapping));

                Assert.AreEqual(1, SqlServerContainer.Scalar(
                    "SELECT COUNT(*) FROM lu_valorant_channel_mappings WHERE val_puuid = @p AND disc_channel_id = @c",
                    ("@p", mapping.Val_puuid), ("@c", (long)mapping.Disc_channel_id)));

                // Duplicate insert must not duplicate the SQL Server row.
                ValorantChannelMappingsExtension.InsertRow(mapping);
                Assert.AreEqual(1, SqlServerContainer.Scalar(
                    "SELECT COUNT(*) FROM lu_valorant_channel_mappings WHERE val_puuid = @p AND disc_channel_id = @c",
                    ("@p", mapping.Val_puuid), ("@c", (long)mapping.Disc_channel_id)));

                Assert.IsTrue(ValorantChannelMappingsExtension.RemoveRow(mapping));
                Assert.AreEqual(0, SqlServerContainer.Scalar(
                    "SELECT COUNT(*) FROM lu_valorant_channel_mappings WHERE val_puuid = @p AND disc_channel_id = @c",
                    ("@p", mapping.Val_puuid), ("@c", (long)mapping.Disc_channel_id)));
            }
            finally
            {
                ValorantChannelMappingsExtension.RemoveRow(mapping);
            }
        }

        [TestMethod]
        public void MatchesAndMatchStats_InsertRow_AreMirroredToSqlServer()
        {
            var matchJson = MatchCreateFromJsonTests.LoadMatch("Matches_11_4_2023.json");
            var mmr = new MmrHistoryJson { Mmr_change_to_last_game = 18, Currenttier = 19 };
            var matches = MatchesExtension.CreateFromJson(matchJson);
            Assert.IsNotNull(matches);
            var stats = MatchStatsExtension.CreateFromJson(matchJson, mmr, "8c7276bd-a70b-56bf-803b-7e0fd9dba287");
            Assert.IsNotNull(stats);

            try
            {
                MatchesExtension.InsertRow(matches);
                MatchStatsExtension.InsertRow(stats);

                // lu_matches assertions
                Assert.AreEqual(1, SqlServerContainer.Scalar(
                    "SELECT COUNT(*) FROM lu_matches WHERE match_id = @m", ("@m", matches.Match_Id)));
                Assert.AreEqual("Lotus", SqlServerContainer.Scalar(
                    "SELECT map FROM lu_matches WHERE match_id = @m", ("@m", matches.Match_Id)));
                Assert.AreEqual("Competitive", SqlServerContainer.Scalar(
                    "SELECT mode FROM lu_matches WHERE match_id = @m", ("@m", matches.Match_Id)));
                Assert.AreEqual((byte)21, SqlServerContainer.Scalar(
                    "SELECT rounds_played FROM lu_matches WHERE match_id = @m", ("@m", matches.Match_Id)));
                Assert.AreEqual((byte)13, SqlServerContainer.Scalar(
                    "SELECT blue_team_rounds_won FROM lu_matches WHERE match_id = @m", ("@m", matches.Match_Id)));
                Assert.AreEqual((byte)8, SqlServerContainer.Scalar(
                    "SELECT red_team_rounds_won FROM lu_matches WHERE match_id = @m", ("@m", matches.Match_Id)));
                Assert.AreEqual(true, SqlServerContainer.Scalar(
                    "SELECT blue_team_win FROM lu_matches WHERE match_id = @m", ("@m", matches.Match_Id)));

                // lu_match_stats assertions
                Assert.AreEqual(1, SqlServerContainer.Scalar(
                    "SELECT COUNT(*) FROM lu_match_stats WHERE match_id = @m AND val_puuid = @p",
                    ("@m", stats.Match_id), ("@p", stats.Val_puuid)));
                Assert.AreEqual("Skye", SqlServerContainer.Scalar(
                    "SELECT val_character FROM lu_match_stats WHERE match_id = @m AND val_puuid = @p",
                    ("@m", stats.Match_id), ("@p", stats.Val_puuid)));
                Assert.AreEqual((short)18, SqlServerContainer.Scalar(
                    "SELECT rr_change FROM lu_match_stats WHERE match_id = @m AND val_puuid = @p",
                    ("@m", stats.Match_id), ("@p", stats.Val_puuid)));
                Assert.AreEqual((byte)19, SqlServerContainer.Scalar(
                    "SELECT kills FROM lu_match_stats WHERE match_id = @m AND val_puuid = @p",
                    ("@m", stats.Match_id), ("@p", stats.Val_puuid)));
                Assert.AreEqual((byte)15, SqlServerContainer.Scalar(
                    "SELECT deaths FROM lu_match_stats WHERE match_id = @m AND val_puuid = @p",
                    ("@m", stats.Match_id), ("@p", stats.Val_puuid)));
                Assert.AreEqual((byte)13, SqlServerContainer.Scalar(
                    "SELECT assists FROM lu_match_stats WHERE match_id = @m AND val_puuid = @p",
                    ("@m", stats.Match_id), ("@p", stats.Val_puuid)));
                Assert.AreEqual((short)5840, SqlServerContainer.Scalar(
                    "SELECT score FROM lu_match_stats WHERE match_id = @m AND val_puuid = @p",
                    ("@m", stats.Match_id), ("@p", stats.Val_puuid)));
                Assert.AreEqual((short)4028, SqlServerContainer.Scalar(
                    "SELECT damage FROM lu_match_stats WHERE match_id = @m AND val_puuid = @p",
                    ("@m", stats.Match_id), ("@p", stats.Val_puuid)));

                // Duplicate stats insert does not duplicate or throw.
                MatchStatsExtension.InsertRow(stats);
                Assert.AreEqual(1, SqlServerContainer.Scalar(
                    "SELECT COUNT(*) FROM lu_match_stats WHERE match_id = @m AND val_puuid = @p",
                    ("@m", stats.Match_id), ("@p", stats.Val_puuid)));
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matches.Match_Id);
            }
        }

        [TestMethod]
        public void HeatmapExtensions_InsertAndQuery_AreMirroredToSqlServer()
        {
            string matchId = "match-heatmap-dualwrite";
            string initiatorPuuid = "puuid-heatmap-initiator";
            string targetPuuid = "puuid-heatmap-target";
            var match = new Matches(matchId, "Ascent", "Competitive", "competitive", 1800, (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTime.UtcNow, 20, 13, 7, 15, 14, true, "season-id");

            try
            {
                MatchesExtension.InsertRow(match);

                // ValorantPlayersExtension
                ValorantPlayersExtension.InsertRow(initiatorPuuid);
                ValorantPlayersExtension.InsertRow(targetPuuid);
                Assert.AreEqual(1, SqlServerContainer.Scalar(
                    "SELECT COUNT(*) FROM lu_valorant_players WHERE val_puuid = @p", ("@p", initiatorPuuid)));
                Assert.AreEqual(1, SqlServerContainer.Scalar(
                    "SELECT COUNT(*) FROM lu_valorant_players WHERE val_puuid = @p", ("@p", targetPuuid)));

                // Re-inserting updates last_access_time_utc without duplicating
                ValorantPlayersExtension.InsertRow(initiatorPuuid);
                Assert.AreEqual(1, SqlServerContainer.Scalar(
                    "SELECT COUNT(*) FROM lu_valorant_players WHERE val_puuid = @p", ("@p", initiatorPuuid)));

                // MatchPlayersExtension
                MatchPlayersExtension.InsertRow(matchId, initiatorPuuid, 1, Agents.Jett);
                Assert.AreEqual(1, SqlServerContainer.Scalar(
                    "SELECT COUNT(*) FROM lu_match_players mp JOIN lu_matches m ON mp.match_index = m.match_index WHERE m.match_id = @m",
                    ("@m", matchId)));
                Assert.AreEqual((byte)Agents.Jett, SqlServerContainer.Scalar(
                    "SELECT mp.agent FROM lu_match_players mp JOIN lu_matches m ON mp.match_index = m.match_index WHERE m.match_id = @m",
                    ("@m", matchId)));

                // MatchEventsExtension: InsertRow returns true and non-zero event index
                bool inserted = MatchEventsExtension.InsertRow(
                    matchId, 1, EventType.Kill, 15000, 45000, initiatorPuuid, targetPuuid, out long eventIndex);
                Assert.IsTrue(inserted);
                Assert.IsTrue(eventIndex > 0);
                Assert.AreEqual(1, MatchEventsExtension.EventTotalCount());

                // Duplicate event returns false and does not increase count
                bool duplicate = MatchEventsExtension.InsertRow(
                    matchId, 1, EventType.Kill, 15000, 45000, initiatorPuuid, targetPuuid, out long _);
                Assert.IsFalse(duplicate);
                Assert.AreEqual(1, MatchEventsExtension.EventTotalCount());

                // PlayerLocationsExtension: InsertRow satisfies FKs and inserts correctly
                PlayerLocationsExtension.InsertRow(eventIndex, initiatorPuuid, 120, -340, 1.57);
                Assert.AreEqual(1, SqlServerContainer.Scalar(
                    "SELECT COUNT(*) FROM lu_player_locations WHERE event_index = @e", ("@e", eventIndex)));
                Assert.AreEqual((short)120, SqlServerContainer.Scalar(
                    "SELECT x FROM lu_player_locations WHERE event_index = @e", ("@e", eventIndex)));
                Assert.AreEqual((short)-340, SqlServerContainer.Scalar(
                    "SELECT y FROM lu_player_locations WHERE event_index = @e", ("@e", eventIndex)));
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
            }
        }

        [TestMethod]
        public void HeatmapExtensions_MissingForeignKeys_ThrowException()
        {
            // MatchPlayersExtension throws if match or player does not exist
            Assert.ThrowsException<Exception>(() =>
                MatchPlayersExtension.InsertRow("non-existent-match", "non-existent-player", 1, Agents.Jett));

            // PlayerLocationsExtension throws if event or player does not exist
            Assert.ThrowsException<Exception>(() =>
                PlayerLocationsExtension.InsertRow(999999, "non-existent-player", 0, 0, 0.0));
        }

        [TestMethod]
        public void MatchEvents_DeleteRows_OnlyDeletesOldRows()
        {
            string oldMatchId = "match-events-old";
            string newMatchId = "match-events-new";
            string puuid = "puuid-delete-events";

            long nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long oldEpoch = nowEpoch - (40L * 24 * 60 * 60); // 40 days old

            var oldMatch = new Matches(oldMatchId, "Ascent", "Competitive", "competitive", 1800, (int)oldEpoch, DateTime.UtcNow.AddDays(-40), 20, 13, 7, 15, 14, true, "season-id");
            var newMatch = new Matches(newMatchId, "Ascent", "Competitive", "competitive", 1800, (int)nowEpoch, DateTime.UtcNow, 20, 13, 7, 15, 14, true, "season-id");

            try
            {
                MatchesExtension.InsertRow(oldMatch);
                MatchesExtension.InsertRow(newMatch);
                ValorantPlayersExtension.InsertRow(puuid);

                Assert.IsTrue(MatchEventsExtension.InsertRow(oldMatchId, 1, EventType.Plant, 10000, null, puuid, null, out _));
                Assert.IsTrue(MatchEventsExtension.InsertRow(newMatchId, 1, EventType.Defuse, 12000, null, puuid, null, out _));
                Assert.AreEqual(2, MatchEventsExtension.EventTotalCount());

                // Deleting events older than 30 days should only remove the old match's event
                MatchEventsExtension.DeleteRows(30);

                Assert.AreEqual(1, MatchEventsExtension.EventTotalCount());
                Assert.AreEqual(0, SqlServerContainer.Scalar(
                    "SELECT COUNT(*) FROM lu_match_events me JOIN lu_matches m ON me.match_index = m.match_index WHERE m.match_id = @m",
                    ("@m", oldMatchId)));
                Assert.AreEqual(1, SqlServerContainer.Scalar(
                    "SELECT COUNT(*) FROM lu_match_events me JOIN lu_matches m ON me.match_index = m.match_index WHERE m.match_id = @m",
                    ("@m", newMatchId)));
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(oldMatchId);
                DevToolsExtension.DeleteMatchEverywhere(newMatchId);
            }
        }

        [TestMethod]
        public void DevTools_DeleteMatchEverywhere_RemovesFromSqlServerAndCascades()
        {
            string matchId = "match-delete-everywhere";
            string puuid = "puuid-delete-everywhere";
            var match = new Matches(matchId, "Ascent", "Competitive", "competitive", 1800, (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTime.UtcNow, 20, 13, 7, 15, 14, true, "season-id");
            var stats = new MatchStats(
                matchId, puuid, "Ascent", "Competitive", 20,
                "Jett", 15, 1, 0, 0, 0, 15, 0, 10, 0, 5,
                20.0, 30.0, 3000, 2500, 1, 2, 3, 4,
                0, 0, 1800, DateTime.UtcNow, false, 15, "Blue", 15);

            try
            {
                MatchesExtension.InsertRow(match);
                MatchStatsExtension.InsertRow(stats);
                ValorantPlayersExtension.InsertRow(puuid);
                MatchPlayersExtension.InsertRow(matchId, puuid, 1, Agents.Jett);
                Assert.IsTrue(MatchEventsExtension.InsertRow(matchId, 1, EventType.Kill, 10000, null, puuid, null, out long eventIndex));
                PlayerLocationsExtension.InsertRow(eventIndex, puuid, 50, 60, 0.5);

                // Verify all exist in SQL Server
                Assert.AreEqual(1, SqlServerContainer.Scalar("SELECT COUNT(*) FROM lu_matches WHERE match_id = @m", ("@m", matchId)));
                Assert.AreEqual(1, SqlServerContainer.Scalar("SELECT COUNT(*) FROM lu_match_stats WHERE match_id = @m", ("@m", matchId)));
                Assert.AreEqual(1, SqlServerContainer.Scalar(
                    "SELECT COUNT(*) FROM lu_match_players mp JOIN lu_matches m ON mp.match_index = m.match_index WHERE m.match_id = @m", ("@m", matchId)));
                Assert.AreEqual(1, SqlServerContainer.Scalar(
                    "SELECT COUNT(*) FROM lu_match_events me JOIN lu_matches m ON me.match_index = m.match_index WHERE m.match_id = @m", ("@m", matchId)));
                Assert.AreEqual(1, SqlServerContainer.Scalar("SELECT COUNT(*) FROM lu_player_locations WHERE event_index = @e", ("@e", eventIndex)));

                // Delete everywhere
                Assert.IsTrue(DevToolsExtension.DeleteMatchEverywhere(matchId));

                // In SQLite: match and stats deleted
                Assert.IsNull(MatchesExtension.GetRow(matchId));
                Assert.IsNull(MatchStatsExtension.GetRow(matchId, puuid));

                // In SQL Server: match, stats, match_players, match_events, player_locations all removed
                Assert.AreEqual(0, SqlServerContainer.Scalar("SELECT COUNT(*) FROM lu_matches WHERE match_id = @m", ("@m", matchId)));
                Assert.AreEqual(0, SqlServerContainer.Scalar("SELECT COUNT(*) FROM lu_match_stats WHERE match_id = @m", ("@m", matchId)));
                Assert.AreEqual(0, SqlServerContainer.Scalar(
                    "SELECT COUNT(*) FROM lu_match_players mp JOIN lu_matches m ON mp.match_index = m.match_index WHERE m.match_id = @m", ("@m", matchId)));
                Assert.AreEqual(0, SqlServerContainer.Scalar(
                    "SELECT COUNT(*) FROM lu_match_events me JOIN lu_matches m ON me.match_index = m.match_index WHERE m.match_id = @m", ("@m", matchId)));
                Assert.AreEqual(0, SqlServerContainer.Scalar("SELECT COUNT(*) FROM lu_player_locations WHERE event_index = @e", ("@e", eventIndex)));

                // Valorant player identity row remains
                Assert.AreEqual(1, SqlServerContainer.Scalar("SELECT COUNT(*) FROM lu_valorant_players WHERE val_puuid = @p", ("@p", puuid)));
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
            }
        }

        [TestMethod]
        public void TypeEdgeCases_ProdDiscordId_And_MaxLengthUsernameAndTag_FitSqlServerSchema()
        {
            ulong discId = 1234567890123456789UL;
            string maxUsername = new string('u', 20); // max 20 chars per schema
            string maxTag = new string('t', 5);       // max 5 chars per schema
            string puuid = "puuid-edge-case-max";

            var user = new ValorantUsers(maxUsername, maxTag, "na", puuid, discId);
            var mapping = new ValorantChannelMappings(puuid, discId);

            try
            {
                Assert.IsTrue(ValorantUsersExtension.InsertRow(user));
                Assert.AreEqual(maxUsername, SqlServerContainer.Scalar("SELECT val_username FROM lu_valorant_users WHERE val_puuid = @p", ("@p", puuid)));
                Assert.AreEqual(maxTag, SqlServerContainer.Scalar("SELECT val_tagname FROM lu_valorant_users WHERE val_puuid = @p", ("@p", puuid)));
                Assert.AreEqual(1234567890123456789L, SqlServerContainer.Scalar("SELECT disc_id FROM lu_valorant_users WHERE val_puuid = @p", ("@p", puuid)));

                Assert.IsTrue(ValorantChannelMappingsExtension.InsertRow(mapping));
                Assert.AreEqual(1234567890123456789L, SqlServerContainer.Scalar(
                    "SELECT disc_channel_id FROM lu_valorant_channel_mappings WHERE val_puuid = @p", ("@p", puuid)));
            }
            finally
            {
                ValorantUsersExtension.DeleteRow(puuid, discId);
                ValorantChannelMappingsExtension.RemoveRow(mapping);
            }
        }

        [TestMethod]
        public void TypeEdgeCases_NegativeRrChange_IsStored()
        {
            // rr_change must hold RR losses. The original schema used TINYINT (0-255); prod is SMALLINT and
            // db/sqlserver/2026-11_match_stats_rr_change_smallint.sql brings fresh databases in line.
            string matchId = "match-negative-rr-test";
            string puuid = "puuid-negative-rr-test";

            var matchStats = new MatchStats(
                matchId, puuid, "Lotus", "Competitive", 21,
                "Skye", -18, 0, 0, 0, 0, 10, 0, 15, 0, 5,
                10.0, 20.0, 2000, 3000, 0, 0, 0, 0,
                0, 0, 1800, DateTime.UtcNow, false, 18, "Blue", 18);

            try
            {
                MatchStatsExtension.InsertRow(matchStats);

                Assert.AreEqual((short)-18, SqlServerContainer.Scalar(
                    "SELECT rr_change FROM lu_match_stats WHERE match_id = @m AND val_puuid = @p",
                    ("@m", matchId), ("@p", puuid)));
            }
            finally
            {
                DevToolsExtension.DeleteMatchEverywhere(matchId);
            }
        }

        [TestMethod]
        public void TypeEdgeCases_DiscordIdAboveLongMax_ThrowsInvalidCastException()
        {
            // NOTE: possible bug: ValorantUsersExtension.InsertRow fails when Disc_id > long.MaxValue because lu_valorant_users.disc_id is BIGINT in SQL Server.
            ulong oversizedId = ulong.MaxValue;
            var user = new ValorantUsers("overid", "0001", "na", "puuid-over-id", oversizedId);

            try
            {
                Assert.ThrowsException<InvalidCastException>(() => ValorantUsersExtension.InsertRow(user));
            }
            finally
            {
                using var connection = new SqliteConnection(DbSettings.Sqlite);
                connection.Open();
                using var cmd = new SqliteCommand("DELETE FROM ValorantUsers WHERE val_puuid = @p", connection);
                cmd.Parameters.AddWithValue("@p", user.Val_puuid);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
