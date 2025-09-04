using Microsoft.Data.Sqlite;

namespace ValorantApp.Database.Tables
{
    public class PlayerLocations
    {
        public int Event_Index { get; private set; }
        public int Player_Index { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public double View_Radians { get; private set; }

        public PlayerLocations(
            int event_Index
            , int player_Index
            , int x
            , int y
            , double view_Radians
            )
        {
            Event_Index = event_Index;
            Player_Index = player_Index;
            X = x;
            Y = y;
            View_Radians = view_Radians;
        }

        public static PlayerLocations CreateFromRow(SqliteDataReader reader)
        {
            return new PlayerLocations(
                reader.GetInt32(reader.GetOrdinal("event_index")),
                reader.GetInt32(reader.GetOrdinal("player_index")),
                reader.GetInt32(reader.GetOrdinal("x")),
                reader.GetInt32(reader.GetOrdinal("y")),
                reader.GetDouble(reader.GetOrdinal("view_radians"))
                );
        }

        public override string ToString()
        {
            return $"";
        }
    }
}
