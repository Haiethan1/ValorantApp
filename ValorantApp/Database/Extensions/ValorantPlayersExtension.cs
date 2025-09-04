using Microsoft.Data.SqlClient;
using System.Data;
using ValorantApp.GenericExtensions;

namespace ValorantApp.Database.Extensions
{
    public class ValorantPlayersExtension : BaseTable
    {
        public ValorantPlayersExtension() { }

        public static void InsertRow(string puuid)
        {
            using SqlConnection sqlConnection = new(sqlConnectionString);
            sqlConnection.Open();
            string InsertRowQuery =
                @"IF EXISTS (SELECT 1 FROM lu_valorant_players WITH(NOLOCK) WHERE val_puuid = @val_puuid)
                BEGIN
                    UPDATE lu_valorant_players
                    SET last_access_time_utc = GETUTCDATE()
                    WHERE val_puuid = @val_puuid;
                END
                ELSE
                BEGIN
                    INSERT INTO lu_valorant_players (val_puuid, last_access_time_utc)
                    VALUES (@val_puuid, GETUTCDATE());
                END";

            using SqlCommand command = new SqlCommand(InsertRowQuery, sqlConnection);
            command.AddParameter("@val_puuid", SqlDbType.VarChar, puuid);

            SqlExtensions.ExecuteNonQuery(command);
        }
    }
}
