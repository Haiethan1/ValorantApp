using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using System.Data;
using ValorantApp.Database.Tables;
using ValorantApp.GenericExtensions;

namespace ValorantApp.Database.Extensions
{
    public class ValorantUsersExtension : BaseTable
    {
        public ValorantUsersExtension() { }

        public new static string CreateTable()
        {
            string createTableQuery =
                @"CREATE TABLE IF NOT EXISTS ValorantUsers (
                    val_username TEXT NOT NULL,
                    val_tagname TEXT NOT NULL,
                    val_affinity TEXT NOT NULL,
                    val_puuid TEXT NOT NULL,
                    disc_id INTEGER NOT NULL,
                    PRIMARY KEY (val_puuid)
                );";

            return createTableQuery;
        }

        public static bool InsertRow(ValorantUsers user)
        {
            using SqliteConnection connection = new SqliteConnection(connectionString);
            connection.Open();
            string InsertRowQuery =
                @"INSERT OR IGNORE INTO ValorantUsers (val_username, val_tagname, val_affinity, val_puuid, disc_id)
                VALUES (@val_username, @val_tagname, @val_affinity, @val_puuid, @disc_id)";

            using SqliteCommand insertCommand = new SqliteCommand(InsertRowQuery, connection);
            insertCommand.Parameters.AddWithValue("@val_username", user.Val_username);
            insertCommand.Parameters.AddWithValue("@val_tagname", user.Val_tagname);
            insertCommand.Parameters.AddWithValue("@val_affinity", user.Val_affinity);
            insertCommand.Parameters.AddWithValue("@val_puuid", user.Val_puuid);
            insertCommand.Parameters.AddWithValue("@disc_id", user.Disc_id);
            int result = insertCommand.ExecuteNonQuery();
            connection.Close();

            using SqlConnection sqlConnection = new(sqlConnectionString);
            sqlConnection.Open();
            string InsertSqlRowQuery =
                @"INSERT INTO lu_valorant_users (val_username, val_tagname, val_affinity, val_puuid, disc_id)
                SELECT @val_username, @val_tagname, @val_affinity, @val_puuid, @disc_id
                WHERE NOT EXISTS (
                    SELECT 1 
                    FROM lu_valorant_users WITH(NOLOCK)
                    WHERE val_puuid = @val_puuid
                );";

            using SqlCommand insertSqlCommand = new(InsertSqlRowQuery, sqlConnection);
            insertSqlCommand.AddParameter("@val_username", SqlDbType.VarChar, user.Val_username);
            insertSqlCommand.AddParameter("@val_tagname", SqlDbType.VarChar, user.Val_tagname);
            insertSqlCommand.AddParameter("@val_affinity", SqlDbType.VarChar, user.Val_affinity);
            insertSqlCommand.AddParameter("@val_puuid", SqlDbType.VarChar, user.Val_puuid);
            insertSqlCommand.AddParameter("@disc_id", SqlDbType.BigInt, user.Disc_id);
            SqlExtensions.ExecuteNonQuery(insertSqlCommand);

            return result > 0;
        }

        public static bool UpdateRow(ValorantUsers updatedUser)
        {
            using SqliteConnection connection = new SqliteConnection(connectionString);
            connection.Open();
            string UpdateRowQuery =
                @"UPDATE ValorantUsers SET val_username = @val_username, val_tagname = @val_tagname, val_affinity = @val_affinity, disc_id = @disc_id
                WHERE val_puuid = @oldPuuid";

            using SqliteCommand insertCommand = new SqliteCommand(UpdateRowQuery, connection);
            insertCommand.Parameters.AddWithValue("@val_username", updatedUser.Val_username);
            insertCommand.Parameters.AddWithValue("@val_tagname", updatedUser.Val_tagname);
            insertCommand.Parameters.AddWithValue("@val_affinity", updatedUser.Val_affinity);
            insertCommand.Parameters.AddWithValue("@oldPuuid", updatedUser.Val_puuid);
            insertCommand.Parameters.AddWithValue("@disc_id", updatedUser.Disc_id);
            int result = insertCommand.ExecuteNonQuery();
            connection.Close();

            using SqlConnection sqlConnection = new(sqlConnectionString);
            sqlConnection.Open();
            string UpdateSqlRowQuery =
                @"UPDATE lu_valorant_users SET val_username = @val_username, val_tagname = @val_tagname, val_affinity = @val_affinity, disc_id = @disc_id
                WHERE val_puuid = @oldPuuid";

            using SqlCommand insertSqlCommand = new(UpdateSqlRowQuery, sqlConnection);
            insertSqlCommand.AddParameter("@val_username", SqlDbType.VarChar, updatedUser.Val_username);
            insertSqlCommand.AddParameter("@val_tagname", SqlDbType.VarChar, updatedUser.Val_tagname);
            insertSqlCommand.AddParameter("@val_affinity", SqlDbType.VarChar, updatedUser.Val_affinity);
            insertSqlCommand.AddParameter("@oldPuuid", SqlDbType.VarChar, updatedUser.Val_puuid);
            insertSqlCommand.AddParameter("@disc_id", SqlDbType.BigInt, updatedUser.Disc_id);
            SqlExtensions.ExecuteNonQuery(insertSqlCommand);

            return result > 0;
        }

        public static ValorantUsers? GetRow(string puuid)
        {
            using SqliteConnection connection = new(connectionString);
            connection.Open();
            string sql = "SELECT * FROM ValorantUsers WHERE val_puuid = @val_puuid";

            using SqliteCommand command = new(sql, connection);
            command.Parameters.AddWithValue("@val_puuid", puuid);

            using SqliteDataReader reader = command.ExecuteReader();

            if (!reader.Read())
            {
                return null;
            }

            return ValorantUsers.CreateFromRow(reader);
        }

        public static ValorantUsers? GetRowDiscordId(ulong id)
        {
            using SqliteConnection connection = new(connectionString);
            connection.Open();
            string sql = "SELECT * FROM ValorantUsers WHERE disc_id = @disc_id LIMIT 1";

            using SqliteCommand command = new(sql, connection);
            command.Parameters.AddWithValue("@disc_id", id);

            using SqliteDataReader reader = command.ExecuteReader();

            if (!reader.Read())
            {
                return null;
            }

            return ValorantUsers.CreateFromRow(reader);
        }

        public static bool DeleteRow(string puuid, ulong discId)
        {
            using SqliteConnection connection = new(connectionString);
            connection.Open();
            string query = "DELETE FROM ValorantUsers WHERE val_puuid = @val_puuid AND disc_id = @disc_id";

            using SqliteCommand command = new(query, connection);
            command.Parameters.AddWithValue("@val_puuid", puuid);
            command.Parameters.AddWithValue("@disc_id", discId);
            int result = command.ExecuteNonQuery();
            connection.Close();

            using SqlConnection sqlConnection = new(sqlConnectionString);
            sqlConnection.Open();
            string sql = @"DELETE FROM lu_valorant_users
                WHERE val_puuid = @val_puuid AND disc_id = @disc_id;";

            using SqlCommand sqlCommand = new(sql, sqlConnection);
            sqlCommand.AddParameter("@val_puuid", SqlDbType.VarChar, puuid);
            sqlCommand.AddParameter("@disc_id", SqlDbType.BigInt, discId);
            SqlExtensions.ExecuteNonQuery(sqlCommand);

            return result > 0;
        }

        public static List<ValorantUsers> GetAllRows()
        {
            List<ValorantUsers> users = new List<ValorantUsers>();

            using (SqliteConnection connection = new(connectionString))
            {
                connection.Open();

                string sql = "SELECT * FROM ValorantUsers";

                using SqliteCommand command = new SqliteCommand(sql, connection);
                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    users.Add(ValorantUsers.CreateFromRow(reader));
                }
            }

            return users;
        }
    }
}
