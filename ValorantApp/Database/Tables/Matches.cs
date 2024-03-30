using Microsoft.Data.Sqlite;

namespace ValorantApp.Database.Tables
{
    public class Matches
    {
        public string Match_Id { get; private set; }
        public string? Map { get; private set; }
        public string? Mode { get; private set; }
        public string? Mode_Id { get; private set; }
        public uint Game_Length { get; private set; }
        public long? Game_Start { get; private set; }
        public DateTime? Game_Start_Patched_UTC { get; private set; }
        public int? Rounds_Played { get; private set; }
        public byte? Blue_Team_Rounds_Won { get; private set; }
        public byte? Red_Team_Rounds_Won { get; private set; }
        public short? Blue_Team_Average_Rank { get; private set; }
        public short? Red_Team_Average_Rank { get; private set; }
        public bool? Blue_Team_Win { get; private set; }
        public string? Season_Id { get; private set; }

        public Matches(
            string matchId
            , string? map
            , string? mode
            , string? modeId
            , uint gameLength
            , long? gameStart
            , DateTime? gameStartPatchedUTC
            , int? roundsPlayed
            , byte? blueTeamRoundsWon
            , byte? redTeamRoundsWon
            , short? blueTeamAverageRank
            , short? redTeamAverageRank
            , bool? blueTeamWin
            , string? seasonId)
        {
            Match_Id = matchId;
            Map = map;
            Mode = mode;
            Mode_Id = modeId;
            Game_Length = gameLength;
            Game_Start = gameStart;
            Game_Start_Patched_UTC = gameStartPatchedUTC;
            Rounds_Played = roundsPlayed;
            Blue_Team_Rounds_Won = blueTeamRoundsWon;
            Red_Team_Rounds_Won = redTeamRoundsWon;
            Blue_Team_Average_Rank = blueTeamAverageRank;
            Red_Team_Average_Rank = redTeamAverageRank;
            Blue_Team_Win = blueTeamWin;
            Season_Id = seasonId;
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