using Microsoft.Data.SqlClient;
using System.Data;
using ValorantApp.GenericExtensions;

namespace ValorantApp.Database.Extensions
{
    public class PlayerLocationsExtension : BaseTable
    {
        public PlayerLocationsExtension() { }

        public static void InsertRow(long eventIndex
            , string puuid
            , int x
            , int y
            , double? viewRadians)
        {
            using SqlConnection sqlConnection = new(sqlConnectionString);
            sqlConnection.Open();

            string insertQuery =
                @"INSERT INTO lu_player_locations (event_index, player_index, x, y, view_radians)
                SELECT @event_index, vp.player_index, @x, @y, @view_radians
                FROM lu_valorant_players vp WITH(NOLOCK)
                WHERE vp.val_puuid = @puuid
                AND NOT EXISTS (
                    SELECT 1 
                    FROM lu_player_locations lpl
                    WHERE lpl.event_index = @event_index AND lpl.player_index = vp.player_index
                );";

            using SqlCommand command = new(insertQuery, sqlConnection);
            command.AddParameter("@event_index", SqlDbType.Int, eventIndex);
            command.AddParameter("@puuid", SqlDbType.VarChar, puuid);
            command.AddParameter("@x", SqlDbType.Int, x);
            command.AddParameter("@y", SqlDbType.Int, y);
            command.AddParameter("@view_radians", SqlDbType.Float, viewRadians);

            int rowsAffected = SqlExtensions.ExecuteNonQuery(command);
            if (rowsAffected == 0)
            {
                throw new Exception("Insert failed into MatchEvents");
            }
        }
    }
}
