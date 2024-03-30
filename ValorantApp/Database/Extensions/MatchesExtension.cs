using Microsoft.Data.Sqlite;
using ValorantApp.Database.Tables;
using ValorantApp.GenericExtensions;
using ValorantApp.HenrikJson;
using ValorantApp.Valorant.Enums;

namespace ValorantApp.Database.Extensions
{
    public class MatchesExtension : BaseTable
    {
        public MatchesExtension() { }

        public new static string CreateTable()
        {
            string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS Matches (
                    match_id TEXT PRIMARY KEY,
                    map TEXT,
                    mode TEXT,
                    mode_id TEXT,
                    game_length INTEGER NOT NULL DEFAULT 0 CHECK(game_length >= 0),
                    game_start INTEGER,
                    game_start_patched_utc INTEGER,
                    rounds_played INTEGER,
                    blue_team_rounds_won INTEGER,
                    red_team_rounds_won INTEGER,
                    blue_team_average_rank INTEGER,
                    red_team_average_rank INTEGER,
                    blue_team_win BOOLEAN,
                    season_id TEXT
                );"
            ;
            return createTableQuery;
        }

        public static void InsertRow(Matches match)
        {
            using SqliteConnection connection = new(connectionString);
            connection.Open();
            string InsertRowQuery = @"
                INSERT INTO Matches 
                    (match_id, map, mode, mode_id, game_length, game_start, game_start_patched_utc, 
                    rounds_played, blue_team_rounds_won, blue_team_average_rank, red_team_average_rank, 
                    red_team_rounds_won, blue_team_win, season_id)
                VALUES 
                    (@Match_Id, @Map, @Mode, @Mode_Id, @Game_Length, @Game_Start, @Game_Start_Patched_UTC, 
                    @Rounds_Played, @Blue_Team_Rounds_Won, @Blue_Team_Average_Rank, @Red_Team_Average_Rank, 
                    @Red_Team_Rounds_Won, @Blue_Team_Win, @Season_Id
                    )";

            using SqliteCommand command = new SqliteCommand(InsertRowQuery, connection);
            command.Parameters.AddWithValue("@Match_Id", match.Match_Id);
            command.Parameters.AddWithValue("@Map", match.Map);
            command.Parameters.AddWithValue("@Mode", match.Mode);
            command.Parameters.AddWithValue("@Mode_Id", match.Mode_Id);
            command.Parameters.AddWithValue("@Game_Length", match.Game_Length);
            command.Parameters.AddWithValue("@Game_Start", match.Game_Start);
            command.Parameters.AddWithValue("@Game_Start_Patched_UTC", match.Game_Start_Patched_UTC);
            command.Parameters.AddWithValue("@Rounds_Played", match.Rounds_Played);
            command.Parameters.AddWithValue("@Blue_Team_Rounds_Won", match.Blue_Team_Rounds_Won);
            command.Parameters.AddWithValue("@Blue_Team_Average_Rank", match.Blue_Team_Average_Rank);
            command.Parameters.AddWithValue("@Red_Team_Average_Rank", match.Red_Team_Average_Rank);
            command.Parameters.AddWithValue("@Red_Team_Rounds_Won", match.Red_Team_Rounds_Won);
            command.Parameters.AddWithValue("@Blue_Team_Win", match.Blue_Team_Win);
            command.Parameters.AddWithValue("@Season_Id", match.Season_Id);

            command.ExecuteNonQuery();
        }

        public static Matches? GetRow(string matchId)
        {
            using SqliteConnection connection = new(connectionString);
            connection.Open();

            string sql = "SELECT * FROM Matches WHERE match_id = @match_id";

            using SqliteCommand command = new(sql, connection);
            command.Parameters.AddWithValue("@match_id", matchId);

            using SqliteDataReader reader = command.ExecuteReader();

            if (!reader.Read())
            {
                return null;
            }

            return Matches.CreateFromRow(reader);
        }

        public static IEnumerable<Matches> GetListOfRows(IEnumerable<string> matchIds)
        {
            using SqliteConnection connection = new(connectionString);
            connection.Open();

            string sql = $"SELECT * FROM Matches WHERE match_id IN ({string.Join(",", matchIds.Select((_, index) => $"@match_id{index}"))})";

            using SqliteCommand command = new(sql, connection);
            for (int i = 0; i < matchIds.Count(); i++)
            {
                command.Parameters.AddWithValue($"@match_id{i}", matchIds.ElementAt(i));
            }

            using SqliteDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                yield return Matches.CreateFromRow(reader);
            }
        }

        public static bool MatchIdExistsForUser(string matchId)
        {
            using SqliteConnection connection = new(connectionString);
            connection.Open();

            string sql = "SELECT * FROM Matches WHERE match_id = @match_id";

            using SqliteCommand command = new(sql, connection);
            command.Parameters.AddWithValue("@match_id", matchId);

            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read();
        }

        public static Matches? CreateFromJson(MatchJson? match)
        {
            if (match == null || match.Metadata == null || match.Metadata.MatchId == null)
            {
                return null;
            }

            MatchMetadataJson metadata = match.Metadata;
            MatchTeamJson? blueTeam = match.Teams?.Blue;
            MatchTeamJson? redTeam = match.Teams?.Red;
            MatchPlayerJson[]? blueTeamPlayers = match.Players?.Blue;
            MatchPlayerJson[]? redTeamPlayers = match.Players?.Red;

            DateTime.TryParse(metadata.Game_Start_Patched, out DateTime gameStartPatched);

            byte blueTeamRoundsWon = 0;
            byte redTeamRoundsWon = 0;
            short blueTeamAverageRank = 0;
            short redTeamAverageRank = 0;
            bool blueTeamHasWon = false;

            if (blueTeam != null && blueTeamPlayers != null)
            {
                blueTeamRoundsWon = (byte)(blueTeam.Rounds_Won ?? 0);
                int playersWithTier = 0;
                foreach (var player in blueTeamPlayers)
                {
                    if (player.CurrentTier >= (int)RankEmojis.Iron1)
                    {
                        playersWithTier++;
                        blueTeamAverageRank += (byte)player.CurrentTier;
                    }
                }
                blueTeamAverageRank = (byte)Math.Round(blueTeamAverageRank / (double)playersWithTier, MidpointRounding.AwayFromZero);
                blueTeamHasWon = blueTeam.Has_Won.HasValue ? blueTeam.Has_Won.Value : false;
            }

            if (redTeam != null && redTeamPlayers != null)
            {
                redTeamRoundsWon = (byte)(redTeam.Rounds_Won ?? 0);
                int playersWithTier = 0;
                foreach (var player in redTeamPlayers)
                {
                    if (player.CurrentTier >= (int)RankEmojis.Iron1)
                    {
                        playersWithTier++;
                        redTeamAverageRank += (byte)player.CurrentTier;
                    }
                }
                redTeamAverageRank = (byte)Math.Round(redTeamAverageRank / (double)playersWithTier, MidpointRounding.AwayFromZero);
            }

            if (ModesExtension.ModeFromString(metadata.Mode.Safe()) == Modes.Deathmatch && match.Players?.All_Players != null)
            {
                int playersWithTier = 0;
                foreach (var player in match.Players.All_Players)
                {
                    if (player.CurrentTier >= (int)RankEmojis.Iron1)
                    {
                        playersWithTier++;
                        blueTeamAverageRank += (byte)player.CurrentTier;
                    }
                }
                blueTeamAverageRank = (byte)Math.Round(blueTeamAverageRank / (double)playersWithTier, MidpointRounding.AwayFromZero);
            }

            return new Matches(metadata.MatchId, metadata.Map, metadata.Mode, metadata.Mode_Id, (uint)metadata.Game_Length
                , metadata.Game_Start, gameStartPatched, metadata.Rounds_Played, blueTeamRoundsWon, redTeamRoundsWon
                , blueTeamAverageRank, redTeamAverageRank, blueTeamHasWon, metadata.Season_Id);
        }
    }
}
