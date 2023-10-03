using Microsoft.Data.Sqlite;
using ValorantApp.Database.Tables;

namespace ValorantApp.Database.Extensions
{
    public class ValorantUsersExtension : BaseTable
    {
        public ValorantUsersExtension() { }

        public new static string CreateTable()
        {
            string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS ValorantUsers (
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
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            string InsertRowQuery = @"
                INSERT OR IGNORE INTO ValorantUsers (val_username, val_tagname, val_affinity, val_puuid, disc_id)
                VALUES (@val_username, @val_tagname, @val_affinity, @val_puuid, @disc_id)";

            using var insertCommand = new SqliteCommand(InsertRowQuery, connection);
            insertCommand.Parameters.AddWithValue("@val_username", user.Val_username);
            insertCommand.Parameters.AddWithValue("@val_tagname", user.Val_tagname);
            insertCommand.Parameters.AddWithValue("@val_affinity", user.Val_affinity);
            insertCommand.Parameters.AddWithValue("@val_puuid", user.Val_puuid);
            insertCommand.Parameters.AddWithValue("@disc_id", user.Disc_id);
            int result = insertCommand.ExecuteNonQuery();

            return result > 0;
        }

        public static bool UpdateRow(ValorantUsers newUser, string oldPuuid)
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            string UpdateRowQuery = @"
                UPDATE ValorantUsers SET val_username = @val_username, val_tagname = @val_tagname, val_affinity = @val_affinity, disc_id = @disc_id
                WHERE val_puuid = @oldpuuid";

            using var insertCommand = new SqliteCommand(UpdateRowQuery, connection);
            insertCommand.Parameters.AddWithValue("@val_username", newUser.Val_username);
            insertCommand.Parameters.AddWithValue("@val_tagname", newUser.Val_tagname);
            insertCommand.Parameters.AddWithValue("@val_affinity", newUser.Val_affinity);
            insertCommand.Parameters.AddWithValue("@oldPuuid", oldPuuid);
            insertCommand.Parameters.AddWithValue("@disc_id", newUser.Disc_id);
            int result = insertCommand.ExecuteNonQuery();

            return result > 0;
        }

        public static ValorantUsers GetRow(string puuid)
        {
            using SqliteConnection connection = new(connectionString);
            connection.Open();
            // TODO make sql queries not lock db
            string sql = "SELECT * FROM ValorantUsers WHERE val_puuid = @val_puuid";

            using SqliteCommand command = new(sql, connection);
            command.Parameters.AddWithValue("@val_puuid", puuid);

            using SqliteDataReader reader = command.ExecuteReader();

            reader.Read();

            return ValorantUsers.CreateFromRow(reader);
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
