using Microsoft.Data.Sqlite;

namespace ValorantApp.Database.Tables
{
    public class MatchPlayers
    {
        public int Match_Index { get; private set; }
        public int Player_Index { get; private set; }
        public byte? Team { get; private set; }
        public byte Agent { get; private set; }

        public MatchPlayers(int match_Index, int player_Index, byte? team, byte agent)
        {
            Match_Index = match_Index;
            Player_Index = player_Index;
            Team = team;
            Agent = agent;
        }

        public static MatchPlayers CreateFromRow(SqliteDataReader reader)
        {
            return new MatchPlayers(
                reader.GetInt32(reader.GetOrdinal("match_index")),
                reader.GetInt32(reader.GetOrdinal("player_index")),
                reader.IsDBNull(reader.GetOrdinal("team")) ? null : reader.GetByte(reader.GetOrdinal("team")),
                reader.GetByte(reader.GetOrdinal("agent"))
                );
        }

        public override string ToString()
        {
            return $"";
        }
    }
}
