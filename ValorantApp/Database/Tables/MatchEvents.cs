using Microsoft.Data.Sqlite;

namespace ValorantApp.Database.Tables
{
    public class MatchEvents
    {
        public int Match_Index { get; private set; }
        public byte Round_Number { get; private set; }
        public byte Event_Type { get; private set; }
        public int Event_Time_Round_Ms { get; private set; } // time in ms
        public int? Event_Time_Match_Ms { get; private set; } // time in ms
        public int? Initiator_Player_Index { get; private set; }
        public int? Target_Player_Index { get; private set; }

        public MatchEvents(
            int match_Index
            , byte round_Number
            , byte event_Type
            , int event_Time_Round_Ms
            , int? event_Time_Match_Ms
            , int? initiator_Player_Index
            , int? target_Player_Index
            )
        {
            Match_Index = match_Index;
            Round_Number = round_Number;
            Event_Type = event_Type;
            Event_Time_Round_Ms = event_Time_Round_Ms;
            Event_Time_Match_Ms = event_Time_Match_Ms;
            Initiator_Player_Index = initiator_Player_Index;
            Target_Player_Index = target_Player_Index;
        }

        public static MatchEvents CreateFromRow(SqliteDataReader reader)
        {
            return new MatchEvents(
                reader.GetInt32(reader.GetOrdinal("match_index")),
                reader.GetByte(reader.GetOrdinal("round_number")),
                reader.GetByte(reader.GetOrdinal("event_type")),
                reader.GetInt32(reader.GetOrdinal("event_time_round_ms")),
                reader.IsDBNull(reader.GetOrdinal("event_time_match_ms")) ? null : reader.GetInt32(reader.GetOrdinal("event_time_match_ms")),
                reader.IsDBNull(reader.GetOrdinal("initiator_player_index")) ? null : reader.GetInt32(reader.GetOrdinal("initiator_player_index")),
                reader.IsDBNull(reader.GetOrdinal("target_player_index")) ? null : reader.GetInt32(reader.GetOrdinal("target_player_index"))
                );
        }

        public override string ToString()
        {
            return $"";
        }
    }
}
