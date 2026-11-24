using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using System.Data;
using ValorantApp.GenericExtensions;

namespace ValorantApp.Database.Extensions
{
    /// <summary>
    /// Development/test helpers. Not used by the production flow. Used by the dev-only /devdeletematch
    /// command and by tests to "forget" a match so the poller re-processes it.
    /// </summary>
    public class DevToolsExtension : BaseTable
    {
        public DevToolsExtension() { }

        /// <summary>Most recent stored match (by game start) for a player, or null if none.</summary>
        public static string? GetLatestMatchIdForUser(string puuid)
        {
            using SqliteConnection connection = new(connectionString);
            connection.Open();

            string sql =
                @"SELECT s.match_id FROM MatchStats s
                JOIN Matches m ON m.match_id = s.match_id
                WHERE s.val_puuid = @val_puuid
                ORDER BY m.game_start DESC
                LIMIT 1";

            using SqliteCommand command = new(sql, connection);
            command.Parameters.AddWithValue("@val_puuid", puuid);

            return command.ExecuteScalar() as string;
        }

        /// <summary>
        /// Deletes a match from SQLite (MatchStats + Matches) and, if enabled, SQL Server
        /// (lu_match_stats + lu_matches, which cascades to players, events and locations).
        /// Returns true if the Matches row existed in SQLite.
        /// </summary>
        public static bool DeleteMatchEverywhere(string matchId)
        {
            int matchesDeleted;
            using (SqliteConnection connection = new(connectionString))
            {
                connection.Open();

                using SqliteCommand deleteStats = new("DELETE FROM MatchStats WHERE match_id = @match_id", connection);
                deleteStats.Parameters.AddWithValue("@match_id", matchId);
                deleteStats.ExecuteNonQuery();

                using SqliteCommand deleteMatch = new("DELETE FROM Matches WHERE match_id = @match_id", connection);
                deleteMatch.Parameters.AddWithValue("@match_id", matchId);
                matchesDeleted = deleteMatch.ExecuteNonQuery();
            }

            if (SqlServerEnabled)
            {
                using SqlConnection sqlConnection = new(sqlConnectionString);
                sqlConnection.Open();

                using SqlCommand deleteStats = new("DELETE FROM lu_match_stats WHERE match_id = @match_id", sqlConnection);
                deleteStats.AddParameter("@match_id", SqlDbType.VarChar, matchId);
                SqlExtensions.ExecuteNonQuery(deleteStats);

                using SqlCommand deleteMatch = new("DELETE FROM lu_matches WHERE match_id = @match_id", sqlConnection);
                deleteMatch.AddParameter("@match_id", SqlDbType.VarChar, matchId);
                SqlExtensions.ExecuteNonQuery(deleteMatch);
            }

            return matchesDeleted > 0;
        }
    }
}
