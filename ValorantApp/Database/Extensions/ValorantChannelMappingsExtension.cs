using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ValorantApp.Database.Tables;

namespace ValorantApp.Database.Extensions
{
    public class ValorantChannelMappingsExtension : BaseTable
    {
        public ValorantChannelMappingsExtension() { }

        public new static string CreateTable()
        {
            string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS ValorantChannelMapping (
                    val_puuid TEXT NOT NULL,
                    disc_channel_id ULONG NOT NULL,
                    PRIMARY KEY (val_puuid, disc_channel_id)
                );";

            return createTableQuery;
        }

        public static bool InsertRow(ValorantChannelMappings channelMappings)
        {
            using SqliteConnection connection = new SqliteConnection(connectionString);
            connection.Open();
            string InsertRowQuery = @"
                INSERT OR IGNORE INTO ValorantChannelMapping (val_puuid, disc_channel_id)
                VALUES (@val_puuid, @disc_channel_id)";

            using SqliteCommand insertCommand = new SqliteCommand(InsertRowQuery, connection);
            insertCommand.Parameters.AddWithValue("@val_puuid", channelMappings.Val_puuid);
            insertCommand.Parameters.AddWithValue("@disc_channel_id", channelMappings.Disc_channel_id);
            int result = insertCommand.ExecuteNonQuery();

            return result > 0;
        }

        public static bool RemoveRow(ValorantChannelMappings channelMappings)
        {
            using SqliteConnection connection = new(connectionString);
            connection.Open();

            string sql = "DELETE FROM ValorantChannelMapping WHERE val_puuid = @val_puuid AND disc_channel_id = @disc_channel_id;";

            using SqliteCommand command = new(sql, connection);
            command.Parameters.AddWithValue("@val_puuid", channelMappings.Val_puuid);
            command.Parameters.AddWithValue("@disc_channel_id", channelMappings.Disc_channel_id);

            int result = command.ExecuteNonQuery();

            return result > 0;
        }

        public static IEnumerable<ulong> GetRowDiscordId(string puuid)
        {
            HashSet<ulong> channelIds = new HashSet<ulong>();
            using SqliteConnection connection = new(connectionString);
            connection.Open();
            string sql = "SELECT disc_channel_id FROM ValorantChannelMapping WHERE val_puuid = @val_puuid";

            using SqliteCommand command = new(sql, connection);
            command.Parameters.AddWithValue("@val_puuid", puuid);

            using SqliteDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                if (ulong.TryParse(reader["disc_channel_id"].ToString(), out ulong channelId))
                {
                    channelIds.Add(channelId);
                }
            }

            return channelIds;
        }
    }
}
