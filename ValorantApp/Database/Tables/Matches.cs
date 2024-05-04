using Microsoft.Data.Sqlite;
using System.ComponentModel.DataAnnotations;

namespace ValorantApp.Database.Tables
{
    public class Matches
    {
        public string Match_Id { get; set; }
        public string? Map { get; set; }
        public string? Mode { get; set; }
        public string? Mode_Id { get; set; }
        public uint Game_Length { get; set; }
        public long? Game_Start { get; set; }
        public DateTime? Game_Start_Patched_UTC { get; set; }
        public int? Rounds_Played { get; set; }
        public byte? Blue_Team_Rounds_Won { get; set; }
        public byte? Red_Team_Rounds_Won { get; set; }
        public short? Blue_Team_Average_Rank { get; set; }
        public short? Red_Team_Average_Rank { get; set; }
        public bool? Blue_Team_Win { get; set; }
        public string? Season_Id { get; set; }

        public Matches() { }

        public Matches(
            string match_Id
            , string? map
            , string? mode
            , string? mode_Id
            , uint game_Length
            , long? game_Start
            , DateTime? game_Start_Patched_UTC
            , int? rounds_Played
            , byte? blue_Team_Rounds_Won
            , byte? red_Team_Rounds_Won
            , short? blue_Team_Average_Rank
            , short? red_Team_Average_Rank
            , bool? blue_Team_Win
            , string? season_Id)
        {
            Match_Id = match_Id;
            Map = map;
            Mode = mode;
            Mode_Id = mode_Id;
            Game_Length = game_Length;
            Game_Start = game_Start;
            Game_Start_Patched_UTC = game_Start_Patched_UTC;
            Rounds_Played = rounds_Played;
            Blue_Team_Rounds_Won = blue_Team_Rounds_Won;
            Red_Team_Rounds_Won = red_Team_Rounds_Won;
            Blue_Team_Average_Rank = blue_Team_Average_Rank;
            Red_Team_Average_Rank = red_Team_Average_Rank;
            Blue_Team_Win = blue_Team_Win;
            Season_Id = season_Id;
        }

        public static Matches CreateFromRow(SqliteDataReader reader)
        {
            return new Matches(
                reader.GetString(reader.GetOrdinal("match_id")),
                reader.IsDBNull(reader.GetOrdinal("map")) ? null : reader.GetString(reader.GetOrdinal("map")),
                reader.IsDBNull(reader.GetOrdinal("mode")) ? null : reader.GetString(reader.GetOrdinal("mode")),
                reader.IsDBNull(reader.GetOrdinal("mode_id")) ? null : reader.GetString(reader.GetOrdinal("mode_id")),
                (uint)reader.GetInt32(reader.GetOrdinal("game_length")),
                reader.IsDBNull(reader.GetOrdinal("game_start")) ? null : reader.GetInt64(reader.GetOrdinal("game_start")),
                reader.IsDBNull(reader.GetOrdinal("game_start_patched_utc")) ? null : reader.GetDateTime(reader.GetOrdinal("game_start_patched_utc")),
                reader.IsDBNull(reader.GetOrdinal("rounds_played")) ? null : reader.GetInt32(reader.GetOrdinal("rounds_played")),
                reader.IsDBNull(reader.GetOrdinal("blue_team_rounds_won")) ? null : reader.GetByte(reader.GetOrdinal("blue_team_rounds_won")),
                reader.IsDBNull(reader.GetOrdinal("red_team_rounds_won")) ? null : reader.GetByte(reader.GetOrdinal("red_team_rounds_won")),
                reader.IsDBNull(reader.GetOrdinal("blue_team_average_rank")) ? null : reader.GetByte(reader.GetOrdinal("blue_team_average_rank")),
                reader.IsDBNull(reader.GetOrdinal("red_team_average_rank")) ? null : reader.GetByte(reader.GetOrdinal("red_team_average_rank")),
                reader.IsDBNull(reader.GetOrdinal("blue_team_win")) ? null : reader.GetBoolean(reader.GetOrdinal("blue_team_win")),
                reader.IsDBNull(reader.GetOrdinal("season_id")) ? null : reader.GetString(reader.GetOrdinal("season_id"))
            );
        }

        public override string ToString()
        {
            return $"Match_Id: {Match_Id}, Map: {Map}, Mode: {Mode}, Mode_Id: {Mode_Id}, Game_Length: {Game_Length}, Game_Start: {Game_Start}, "
                    + $"Game_Start_Patched: {Game_Start_Patched_UTC}, Rounds_Played: {Rounds_Played}, Blue_Team_Rounds_Won: {Blue_Team_Rounds_Won}, "
                    + $"Red_Team_Rounds_Won: {Red_Team_Rounds_Won}, Blue_Team_Average_Rank: {Blue_Team_Average_Rank}, Red_Team_Average_Rank: {Red_Team_Average_Rank}, "
                    + $"Blue_Team_Win: {Blue_Team_Win}, Season_Id: {Season_Id}";
        }
    }
}