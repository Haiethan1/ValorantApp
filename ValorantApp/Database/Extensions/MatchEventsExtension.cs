using Microsoft.Data.SqlClient;
using System.Data;
using ValorantApp.GenericExtensions;
using ValorantApp.Valorant.Enums;

namespace ValorantApp.Database.Extensions
{
    public class MatchEventsExtension : BaseTable
    {
        public MatchEventsExtension() { }

        public static bool InsertRow(string matchId
            , int round
            , EventType eventType
            , int eventTimeRoundMs
            , int? eventTimeMatchMs
            , string? initiatorPuuid
            , string? targetPuuid
            , out long eventIndex)
        {
            using SqlConnection sqlConnection = new(sqlConnectionString);
            sqlConnection.Open();

            string insertQuery =
                @"INSERT INTO lu_match_events (
                    match_index, round_number, event_type, event_time_round_ms, event_time_match_ms, 
                    initiator_player_index, target_player_index
                )
                OUTPUT INSERTED.event_index
                SELECT 
                    m.match_index, 
                    @round_number, 
                    @event_type, 
                    @event_time_round_ms, 
                    @event_time_match_ms, 
                    initiator.player_index, 
                    target.player_index
                FROM lu_matches m WITH(NOLOCK)
                LEFT JOIN lu_valorant_players AS initiator WITH(NOLOCK) ON initiator.val_puuid = @initiator_puuid
                LEFT JOIN lu_valorant_players AS target WITH(NOLOCK) ON target.val_puuid = @target_puuid
                WHERE m.match_id = @matchId
                  AND NOT EXISTS (
                      SELECT 1
                      FROM lu_match_events me WITH(NOLOCK)
                      WHERE 
                          me.match_index = m.match_index 
                          AND me.round_number = @round_number 
                          AND me.event_type = @event_type 
                          AND me.event_time_round_ms = @event_time_round_ms
                          AND me.initiator_player_index = initiator.player_index
						  AND me.target_player_index = target.player_index
                  );
                ";

            using SqlCommand sqlCommand = new SqlCommand(insertQuery, sqlConnection);
            sqlCommand.AddParameter("@matchId", SqlDbType.VarChar, matchId);
            sqlCommand.AddParameter("@round_number", SqlDbType.TinyInt, round);
            sqlCommand.AddParameter("@event_type", SqlDbType.TinyInt, (byte)eventType);
            sqlCommand.AddParameter("@event_time_round_ms", SqlDbType.Int, eventTimeRoundMs);
            sqlCommand.AddParameter("@event_time_match_ms", SqlDbType.Int, eventTimeMatchMs);
            sqlCommand.AddParameter("@initiator_puuid", SqlDbType.VarChar, initiatorPuuid);
            sqlCommand.AddParameter("@target_puuid", SqlDbType.VarChar, targetPuuid);

            object? response = SqlExtensions.ExecuteScalar(sqlCommand);
            eventIndex = Convert.ToInt64(response);

            return response != null;
        }

        public static void DeleteRows(int days)
        {
            using SqlConnection sqlConnection = new(sqlConnectionString);
            sqlConnection.Open();

            string insertQuery =
                @"DELETE FROM lu_match_events
                WHERE match_index IN (
                    SELECT m.match_index
                    FROM lu_matches m
                    WHERE m.game_start < (DATEDIFF(SECOND, '1970-01-01', GETUTCDATE()) - @days * 24 * 60 * 60)
                );";

            using SqlCommand sqlCommand = new SqlCommand(insertQuery, sqlConnection);
            sqlCommand.AddParameter("@days", SqlDbType.Int, days);

            SqlExtensions.ExecuteNonQuery(sqlCommand);
        }

        public static int EventTotalCount()
        {
            using SqlConnection sqlConnection = new(sqlConnectionString);
            sqlConnection.Open();

            string sql = "SELECT COUNT(*) FROM lu_match_events";

            using SqlCommand command = new(sql, sqlConnection);

            return Convert.ToInt32(SqlExtensions.ExecuteScalar(command));
        }
    }
}
