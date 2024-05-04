using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ValorantApp.Database.Tables
{
    public class ValorantUsers
    {
        public string Val_username { get; set; }
        public string Val_tagname { get; set; }
        public string Val_affinity { get; set; }
        public string Val_puuid { get; set; }
        public ulong Disc_id { get; set; }

        public ValorantUsers() { }

        public ValorantUsers(string val_username, string val_tagname, string val_affinity, string val_puuid, ulong disc_id)
        {
            Val_username = val_username;
            Val_tagname = val_tagname;
            Val_affinity = val_affinity;
            Val_puuid = val_puuid;
            Disc_id = disc_id;
        }

        public static ValorantUsers CreateFromRow(SqliteDataReader reader)
        {
            return new ValorantUsers(
                val_username: reader.GetString(reader.GetOrdinal("val_username")),
                val_tagname: reader.GetString(reader.GetOrdinal("val_tagname")),
                val_affinity: reader.GetString(reader.GetOrdinal("val_affinity")),
                val_puuid: reader.GetString(reader.GetOrdinal("val_puuid")),
                disc_id: (ulong)reader.GetInt64(reader.GetOrdinal("disc_id"))
                );
        }

        public override string ToString()
        {
            return $"Valorant User: username={Val_username}, tagname={Val_tagname}, puuid={Val_puuid}, discord ID={Disc_id}";
        }
    }
}
