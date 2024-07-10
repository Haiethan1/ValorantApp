using Microsoft.Data.Sqlite;

namespace ValorantApp.Database.Tables
{
    public class ValorantChannelMappings
    {
        public string Val_puuid { get; set; }
        public ulong Disc_channel_id { get; set; }

        public ValorantChannelMappings(string val_puuid, ulong disc_channel_id)
        {
            Val_puuid = val_puuid;
            Disc_channel_id = disc_channel_id;
        }

        public static ValorantChannelMappings CreateFromRow(SqliteDataReader reader)
        {
            return new ValorantChannelMappings(
                val_puuid: reader.GetString(reader.GetOrdinal("val_puuid")),
                disc_channel_id: (ulong)reader.GetInt64(reader.GetOrdinal("disc_channel_id"))
                );
        }

        public override string ToString()
        {
            return $"Valorant User: puuid={Val_puuid}, discord channel ID={Disc_channel_id}";
        }
    }
}
