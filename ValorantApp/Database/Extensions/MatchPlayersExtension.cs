using Microsoft.Data.SqlClient;
using System.Data;
using ValorantApp.GenericExtensions;
using ValorantApp.Valorant.Enums;

namespace ValorantApp.Database.Extensions
{
    public class MatchPlayersExtension : BaseTable
    {
        public MatchPlayersExtension() { }

        public static void InsertRow(string matchId, string puuid, byte? team, Agents agent)
        {
            using SqlConnection sqlConnection = new(sqlConnectionString);
            sqlConnection.Open();

            string insertQuery =
                @"INSERT INTO lu_match_players (match_index, player_index, team, agent)
                SELECT 
                    m.match_index, 
                    vp.player_index, 
                    @team, 
                    @agent
                FROM lu_matches m WITH(NOLOCK)
                JOIN lu_valorant_players vp WITH(NOLOCK) ON vp.val_puuid = @val_puuid
                WHERE m.match_id = @matchId
                  AND NOT EXISTS (
                      SELECT 1
                      FROM lu_match_players mp WITH(NOLOCK)
                      WHERE 
                          mp.match_index = m.match_index 
                          AND mp.player_index = vp.player_index
                  );";

            using SqlCommand command = new SqlCommand(insertQuery, sqlConnection);
            command.AddParameter("@matchId", SqlDbType.VarChar, matchId);
            command.AddParameter("@val_puuid", SqlDbType.VarChar, puuid);
            command.AddParameter("@team", SqlDbType.TinyInt, team);
            command.AddParameter("@agent", SqlDbType.TinyInt, (byte)agent);

            int rowsAffected = SqlExtensions.ExecuteNonQuery(command);
            if (rowsAffected == 0)
            {
                throw new Exception("Insert failed: either Match ID or Player ID was not found.");
            }
        }
    }
}
