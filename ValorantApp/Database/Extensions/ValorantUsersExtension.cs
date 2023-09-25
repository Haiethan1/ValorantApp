using Discord;
using System;
using System.Collections.Generic;
using System.Data.Entity.Migrations.Model;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                    PRIMARY KEY (val_puuid)
                );";

            return createTableQuery;
        }

        public static bool InsertRow(ValorantUsers user)
        {
            using var connection = new SQLiteConnection(connectionString);
            connection.Open();
            string InsertRowQuery = @"
                INSERT OR IGNORE INTO ValorantUsers (val_username, val_tagname, val_affinity, val_puuid)
                VALUES (@val_username, @val_tagname, @val_affinity, @val_puuid)";

            using var insertCommand = new SQLiteCommand(InsertRowQuery, connection);
            insertCommand.Parameters.AddWithValue("@val_username", user.Val_username);
            insertCommand.Parameters.AddWithValue("@val_tagname", user.Val_tagname);
            insertCommand.Parameters.AddWithValue("@val_affinity", user.Val_affinity);
            insertCommand.Parameters.AddWithValue("@val_puuid", user.Val_puuid);
            int result = insertCommand.ExecuteNonQuery();

            return result > 0;
        }

        public static bool UpdateRow(ValorantUsers newUser, string oldPuuid)
        {
            using var connection = new SQLiteConnection(connectionString);
            connection.Open();
            string UpdateRowQuery = @"
                UPDATE ValorantUsers SET val_username = @val_username, val_tagname = @val_tagname, val_affinity = @val_affinity
                WHERE val_puuid = @oldpuuid";

            using var insertCommand = new SQLiteCommand(UpdateRowQuery, connection);
            insertCommand.Parameters.AddWithValue("@val_username", newUser.Val_username);
            insertCommand.Parameters.AddWithValue("@val_tagname", newUser.Val_tagname);
            insertCommand.Parameters.AddWithValue("@val_affinity", newUser.Val_affinity);
            insertCommand.Parameters.AddWithValue("@oldPuuid", oldPuuid);
            int result = insertCommand.ExecuteNonQuery();

            return result > 0;
        }

        public static ValorantUsers? GetRow(string val_username, string val_tagname)
        {
            using SQLiteConnection connection = new(connectionString);
            connection.Open();

            string sql = "SELECT * FROM ValorantUsers WHERE val_username = @val_username AND val_tagname = @val_tagname";

            using SQLiteCommand command = new(sql, connection);
            command.Parameters.AddWithValue("@val_username", val_username);
            command.Parameters.AddWithValue("@val_tagname", val_tagname);

            using SQLiteDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                return ValorantUsers.CreateFromRow(reader);
            }
            else
            {
                // No matching row found.
                return null;
            }
        }

        public static List<ValorantUsers> GetAllRows()
        {
            List<ValorantUsers> users = new List<ValorantUsers>();

            using (SQLiteConnection connection = new(connectionString))
            {
                connection.Open();

                string sql = "SELECT * FROM ValorantUsers";

                using SQLiteCommand command = new SQLiteCommand(sql, connection);
                using SQLiteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    users.Add(ValorantUsers.CreateFromRow(reader));
                }
            }

            return users;
        }
    }
}
