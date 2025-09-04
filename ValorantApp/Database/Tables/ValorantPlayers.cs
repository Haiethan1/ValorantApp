using Microsoft.Data.Sqlite;

namespace ValorantApp.Database.Tables
{
    public class ValorantPlayers
    {
        public string Puuid { get; private set; }
        public long Last_Access_Time_Utc { get; private set; }

        public ValorantPlayers(string puuid, long last_Access_Time_Utc)
        {
            Puuid = puuid;
            Last_Access_Time_Utc = last_Access_Time_Utc;
        }

        public static ValorantPlayers CreateFromRow(SqliteDataReader reader)
        {
            return new ValorantPlayers(
                reader.GetString(reader.GetOrdinal("match_id"))
                , reader.GetInt64(reader.GetOrdinal("last_access_time_utc")));
        }

        public override string ToString()
        {
            return $"";
        }
    }
}
