﻿using Microsoft.Data.Sqlite;
using ValorantApp.Database.Tables;
using ValorantApp.GenericExtensions;
using ValorantApp.HenrikJson;

namespace ValorantApp.Database.Extensions
{
    public class MatchStatsExtension : BaseTable
    {
        public MatchStatsExtension() { }

        public new static string CreateTable()
        {
            string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS MatchStats (
                    match_id TEXT NOT NULL,
                    val_puuid TEXT NOT NULL,
                    map TEXT NOT NULL,
                    mode TEXT NOT NULL,
                    rounds TINYTINT NOT NULL,
                    character TEXT NOT NULL,
                    rr_change TINYINT,
                    double_kills TINYINT NOT NULL,
                    triple_kills TINYINT NOT NULL,
                    quad_kills TINYINT NOT NULL,
                    aces TINYINT NOT NULL,
                    kills TINYINT NOT NULL,
                    knife_kills TINYINT NOT NULL,
                    deaths TINYINT NOT NULL,
                    knife_deaths TINYINT NOT NULL,
                    assists TINYINT NOT NULL,
                    bodyshots REAL NOT NULL,
                    headshots REAL NOT NULL,
                    score SMALLINT NOT NULL,
                    damage SMALLINT NOT NULL,
                    c_casts TINYINT NOT NULL,
                    q_casts TINYINT NOT NULL,
                    e_casts TINYINT NOT NULL,
                    x_casts TINYINT NOT NULL,
                    damage_to_allies SMALLINT NOT NULL,
                    damage_from_allies SMALLINT NOT NULL,
                    game_length INTEGER NOT NULL DEFAULT 0 CHECK(game_length >= 0),
                    game_start_patched TEXT,
                    mvp INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (match_id, val_puuid)
                );";

            return createTableQuery;
        }

        public static void InsertRow(MatchStats matchStats)
        {
            using SqliteConnection connection = new(connectionString);
            connection.Open();

            string InsertRowQuery = @"
                INSERT OR IGNORE INTO MatchStats (
                    match_id, val_puuid, map, mode, rounds, character,
                    rr_change, double_kills, triple_kills, quad_kills, aces, kills, knife_kills, 
                    deaths, knife_deaths, assists, bodyshots, headshots, score, damage,
                    c_casts, q_casts, e_casts, x_casts, damage_to_allies, damage_from_allies, game_length, game_start_patched, mvp
                ) 
                VALUES (
                    @Match_id, @Val_puuid, @Map, @Mode, @Rounds, @Character, 
                    @Rr_change, @Double_kills, @Triple_kills, @Quad_kills, @Aces, @Kills, @Knife_kills,
                    @Deaths, @Knife_deaths, @Assists, @Bodyshots, @Headshots, @Score, @Damage, 
                    @C_casts, @Q_casts, @E_casts, @X_casts, @Damage_to_allies, @Damage_from_allies, @Game_length, @Game_start_patched, @MVP
                )";

            using SqliteCommand command = new SqliteCommand(InsertRowQuery, connection);
            command.Parameters.AddWithValue("@Match_id", matchStats.Match_id);
            command.Parameters.AddWithValue("@Val_puuid", matchStats.Val_puuid);
            command.Parameters.AddWithValue("@Map", matchStats.Map);
            command.Parameters.AddWithValue("@Mode", matchStats.Mode);
            command.Parameters.AddWithValue("@Rounds", matchStats.Rounds);
            command.Parameters.AddWithValue("@Character", matchStats.Character);
            command.Parameters.AddWithValue("@Rr_change", matchStats.Rr_change);
            command.Parameters.AddWithValue("@Double_kills", matchStats.Double_Kills);
            command.Parameters.AddWithValue("@Triple_kills", matchStats.Triple_Kills);
            command.Parameters.AddWithValue("@Quad_kills", matchStats.Quad_Kills);
            command.Parameters.AddWithValue("@Aces", matchStats.Aces);
            command.Parameters.AddWithValue("@Kills", matchStats.Kills);
            command.Parameters.AddWithValue("@Knife_kills", matchStats.Knife_Kills);
            command.Parameters.AddWithValue("@Deaths", matchStats.Deaths);
            command.Parameters.AddWithValue("@Knife_deaths", matchStats.Knife_Deaths);
            command.Parameters.AddWithValue("@Assists", matchStats.Assists);
            command.Parameters.AddWithValue("@Bodyshots", matchStats.Bodyshots);
            command.Parameters.AddWithValue("@Headshots", matchStats.Headshots);
            command.Parameters.AddWithValue("@Score", matchStats.Score);
            command.Parameters.AddWithValue("@Damage", matchStats.Damage);
            command.Parameters.AddWithValue("@C_casts", matchStats.C_casts);
            command.Parameters.AddWithValue("@Q_casts", matchStats.Q_casts);
            command.Parameters.AddWithValue("@E_casts", matchStats.E_casts);
            command.Parameters.AddWithValue("@X_casts", matchStats.X_casts);
            command.Parameters.AddWithValue("@Damage_to_allies", matchStats.Damage_To_Allies);
            command.Parameters.AddWithValue("@Damage_from_allies", matchStats.Damage_From_Allies);
            command.Parameters.AddWithValue("@Game_length", matchStats.Game_Length);
            command.Parameters.AddWithValue("@Game_start_patched", matchStats.Game_Start_Patched);
            command.Parameters.AddWithValue("@MVP", matchStats.MVP);

            command.ExecuteNonQuery();
        }

        public static void UpdateRow(MatchStats matchStats)
        {
            using (SqliteConnection connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                string UpdateRowQuery = @"
                UPDATE MatchStats
                SET
                    val_puuid = @Val_puuid,
                    map = @Map,
                    mode = @Mode,
                    rounds = @Rounds
                    character = @Character,
                    rr_change = @Rr_change,
                    double_kills = @Double_kills,
                    triple_kills = @Triple_kills,
                    quad_kills = @Quad_kills,
                    aces = @Aces,
                    kills = @Kills,
                    knife_kills = @Knife_kills,
                    deaths = @Deaths,
                    knife_deaths = @Knife_deaths,
                    assists = @Assists,
                    bodyshots = @Bodyshots,
                    headshots = @Headshots,
                    score = @Score,
                    damage = @Damage,
                    c_casts = @C_casts,
                    q_casts = @Q_casts,
                    e_casts = @E_casts,
                    x_casts = @X_casts,
                    damage_to_allies = @Damage_to_allies,
                    damage_from_allies = @Damage_from_allies,
                    game_length = @Game_length,
                    game_start_patched = @Game_start_patched,
                    mvp = @MVP
                WHERE match_id = @Match_id";

                using (SqliteCommand command = new(UpdateRowQuery, connection))
                {
                    command.Parameters.AddWithValue("@Match_id", matchStats.Match_id);
                    command.Parameters.AddWithValue("@Val_puuid", matchStats.Val_puuid);
                    command.Parameters.AddWithValue("@Map", matchStats.Map);
                    command.Parameters.AddWithValue("@Mode", matchStats.Mode);
                    command.Parameters.AddWithValue("@Rounds", matchStats.Rounds);
                    command.Parameters.AddWithValue("@Character", matchStats.Character);
                    command.Parameters.AddWithValue("@Rr_change", matchStats.Rr_change);
                    command.Parameters.AddWithValue("@Double_kills", matchStats.Double_Kills);
                    command.Parameters.AddWithValue("@Triple_kills", matchStats.Triple_Kills);
                    command.Parameters.AddWithValue("@Quad_kills", matchStats.Quad_Kills);
                    command.Parameters.AddWithValue("@Aces", matchStats.Aces);
                    command.Parameters.AddWithValue("@Kills", matchStats.Kills);
                    command.Parameters.AddWithValue("@Knife_kills", matchStats.Knife_Kills);
                    command.Parameters.AddWithValue("@Deaths", matchStats.Deaths);
                    command.Parameters.AddWithValue("@Knife_deaths", matchStats.Knife_Deaths);
                    command.Parameters.AddWithValue("@Assists", matchStats.Assists);
                    command.Parameters.AddWithValue("@Bodyshots", matchStats.Bodyshots);
                    command.Parameters.AddWithValue("@Headshots", matchStats.Headshots);
                    command.Parameters.AddWithValue("@Score", matchStats.Score);
                    command.Parameters.AddWithValue("@Damage", matchStats.Damage);
                    command.Parameters.AddWithValue("@C_casts", matchStats.C_casts);
                    command.Parameters.AddWithValue("@Q_casts", matchStats.Q_casts);
                    command.Parameters.AddWithValue("@E_casts", matchStats.E_casts);
                    command.Parameters.AddWithValue("@X_casts", matchStats.X_casts);
                    command.Parameters.AddWithValue("@Damage_to_allies", matchStats.Damage_To_Allies);
                    command.Parameters.AddWithValue("@Damage_from_allies", matchStats.Damage_From_Allies);
                    command.Parameters.AddWithValue("@Game_length", matchStats.Game_Length);
                    command.Parameters.AddWithValue("@Game_start_patched", matchStats.Game_Start_Patched);
                    command.Parameters.AddWithValue("@MVP", matchStats.MVP);

                    command.ExecuteNonQuery();
                }
            }
        }

        // Shouldn't really be used.. just for testing
        public static bool DeleteMatch(string matchId)
        {
            using SqliteConnection connection = new(connectionString);
            connection.Open();

            string sql = "DELETE FROM MatchStats WHERE match_id LIKE @matchid;";

            using SqliteCommand command = new(sql, connection);
            command.Parameters.AddWithValue("@matchid", matchId);

            return command.ExecuteNonQuery() > 0;
        }

        public static bool MatchIdExistsForUser(string matchId, string puuid)
        {
            using SqliteConnection connection = new(connectionString);
            connection.Open();

            string sql = "SELECT * FROM MatchStats WHERE val_puuid = @val_puuid AND match_id = @match_id";

            using SqliteCommand command = new(sql, connection);
            command.Parameters.AddWithValue("@val_puuid", puuid);
            command.Parameters.AddWithValue("@match_id", matchId);

            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read();
        }

        public static MatchStats? CreateFromJson(MatchJson? match, MmrHistoryJson? mmr, string puuid)
        {
            if (match == null
                || match.Players == null
                || match.Players.All_Players == null
                || match.Metadata?.MatchId == null
                )
            {
                return null;
            }

            MatchPlayerJson? player = match.Players.All_Players.FirstOrDefault(x => x.Puuid == puuid);
            if (player == null)
            {
                return null;
            }

            MatchMetadataJson? metadata = match.Metadata;
            PlayerAbilityCastsJson? abilities = player.Ability_Casts;

            DateTime? gameStartPatched = null;
            if (!string.IsNullOrEmpty(metadata.Game_Start_Patched) && DateTime.TryParse(metadata.Game_Start_Patched, out DateTime parsedStartPatched))
            {
                TimeZoneInfo sourceTimeZone = TimeZoneInfo.Utc; // Source timezone (UTC)
                TimeZoneInfo targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); // Target timezone
                gameStartPatched = TimeZoneInfo.ConvertTime(parsedStartPatched, sourceTimeZone, targetTimeZone); ;
            }

            PlayerStatsJson? stats = player.Stats;
            
            double headshot = stats == null ? 0 : stats.Headshots / (double)(stats.Headshots + stats.Bodyshots + stats.Legshots) * 100.0;
            double bodyshot = stats == null ? 0 : stats.Bodyshots / (double)(stats.Headshots + stats.Bodyshots + stats.Legshots) * 100.0;

            int score = stats?.Score ?? 0;
            bool mvp = true;
            foreach (var mvpPlayer in match.Players.All_Players)
            {
                if (mvpPlayer == null)
                {
                    continue;
                }
                if ((mvpPlayer.Stats?.Score ?? 0) > score)
                {
                    mvp = false;
                }
            }

            byte doubleKills = 0;
            byte tripleKills = 0;
            byte quadKills = 0;
            byte aces = 0;
            byte knifeKills = 0;
            byte knifeDeaths = 0;
            short damageToAllies = 0;
            short damageFromAllies = 0;
            uint gameLength = (uint)metadata.Game_Length;

            foreach (var temp in match.Rounds ?? Array.Empty<MatchRoundsJson>())
            {
                var playerRoundStats = temp.Player_Stats?.FirstOrDefault(x => x.Player_Puuid == puuid);
                if (playerRoundStats == null)
                {
                    continue;
                }

                switch(playerRoundStats.Kills)
                {
                    case 2:
                        doubleKills++;
                        break;
                    case 3:
                        tripleKills++;
                        break;
                    case 4:
                        quadKills++;
                        break;
                    case 5:
                        aces++;
                        break;
                    default:
                        break;
                }

                //foreach (var killEvents in playerRoundStats.Kill_Events)
                //{
                //    if (string.Equals(killEvents.Damage_Weapon_Id, "knife", StringComparison.OrdinalIgnoreCase))
                //    {
                //        knifeKills++;
                //    }
                //  // and add damage from / to allies
                //}
            }

            return new MatchStats(
                metadata.MatchId, puuid, metadata.Map.Safe(), metadata.Mode_Id.Safe(), (byte)metadata.Rounds_Played, player.Character.Safe(), mmr?.Mmr_change_to_last_game ?? 0, doubleKills, tripleKills, quadKills, aces
                , (byte)(stats?.Kills ?? 0), knifeKills, (byte)(stats?.Deaths ?? 0), knifeDeaths, (byte)(stats?.Assists ?? 0), bodyshot, headshot, (short)score
                , (short)player.Damage_Made, (byte)(abilities?.C_Cast ?? 0), (byte)(abilities?.Q_Cast ?? 0), (byte)(abilities?.E_Cast ?? 0), (byte)(abilities?.X_Cast ?? 0), damageToAllies, damageFromAllies, gameLength
                , gameStartPatched, mvp
                );
        }
    }
}
