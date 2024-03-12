using Microsoft.Data.Sqlite;

namespace ValorantApp.Database.Tables
{
    public class MatchStats
    {
        public string Match_id { get; private set; }
        public string Val_puuid { get; private set; }
        [Obsolete]
        public string Map { get; private set; }
        [Obsolete]
        public string Mode { get; private set; }
        [Obsolete]
        public byte Rounds { get; private set; }
        public string Character { get; private set; }
        public int Rr_change { get; private set; }
        public byte Double_Kills { get; private set; }
        public byte Triple_Kills { get; private set; }
        public byte Quad_Kills { get; private set; }
        public byte Aces { get; private set; }
        public byte Kills { get; private set; }
        public byte Knife_Kills { get; private set; }
        public byte Deaths { get; private set; }
        public byte Knife_Deaths { get; private set; }
        public byte Assists { get; private set; }
        public double Bodyshots { get; private set; }
        public double Headshots { get; private set; }
        public short Score { get; private set; }
        public short Damage { get; private set; }
        public byte C_casts { get; private set; }
        public byte Q_casts { get; private set; }
        public byte E_casts { get; private set; }
        public byte X_casts { get; private set; }
        public short Damage_To_Allies { get; private set; }
        public short Damage_From_Allies { get; private set; }
        [Obsolete]
        public uint Game_Length { get; private set; }
        [Obsolete]
        public DateTime? Game_Start_Patched { get; private set; }
        public bool MVP { get; private set; }
        public byte? Current_Tier { get; private set; }
        public string? Team { get; private set; }

        public MatchStats(
            string matchId,
            string valPuuid,
            string map,
            string mode,
            byte rounds,
            string character,
            int rrChange,
            byte doubleKills,
            byte tripleKills,
            byte quadKills,
            byte aces,
            byte kills,
            byte knifeKills,
            byte deaths,
            byte knifeDeaths,
            byte assists,
            double bodyshots,
            double headshots,
            short score,
            short damage,
            byte cCasts,
            byte qCasts,
            byte eCasts,
            byte xCasts,
            short damageToAllies,
            short damageFromAllies,
            uint gameLength,
            DateTime? gameStartPatched,
            bool mvp,
            byte? currentTier,
            string? team
            )
        {
            Match_id = matchId;
            Val_puuid = valPuuid;
            Map = map;
            Mode = mode;
            Rounds = rounds;
            Character = character;
            Rr_change = rrChange;
            Double_Kills = doubleKills;
            Triple_Kills = tripleKills;
            Quad_Kills = quadKills;
            Aces = aces;
            Kills = kills;
            Knife_Kills = knifeKills;
            Deaths = deaths;
            Knife_Deaths = knifeDeaths;
            Assists = assists;
            Bodyshots = bodyshots;
            Headshots = headshots;
            Score = score;
            Damage = damage;
            C_casts = cCasts;
            Q_casts = qCasts;
            E_casts = eCasts;
            X_casts = xCasts;
            Damage_To_Allies = damageToAllies;
            Damage_From_Allies = damageFromAllies;
            Game_Length = gameLength;
            Game_Start_Patched = gameStartPatched;
            MVP = mvp;
            Current_Tier = currentTier;
            Team = team;
        }

        public static MatchStats CreateFromRow(SqliteDataReader reader)
        {
            return new MatchStats(
                reader.GetString(reader.GetOrdinal("match_id")),
                reader.GetString(reader.GetOrdinal("val_puuid")),
                reader.GetString(reader.GetOrdinal("map")),
                reader.GetString(reader.GetOrdinal("mode")),
                reader.GetByte(reader.GetOrdinal("rounds")),
                reader.GetString(reader.GetOrdinal("character")),
                reader.GetInt32(reader.GetOrdinal("rr_change")),
                reader.GetByte(reader.GetOrdinal("double_kills")),
                reader.GetByte(reader.GetOrdinal("triple_kills")),
                reader.GetByte(reader.GetOrdinal("quad_kills")),
                reader.GetByte(reader.GetOrdinal("aces")),
                reader.GetByte(reader.GetOrdinal("kills")),
                reader.GetByte(reader.GetOrdinal("knife_kills")),
                reader.GetByte(reader.GetOrdinal("deaths")),
                reader.GetByte(reader.GetOrdinal("knife_deaths")),
                reader.GetByte(reader.GetOrdinal("assists")),
                reader.GetDouble(reader.GetOrdinal("bodyshots")),
                reader.GetDouble(reader.GetOrdinal("headshots")),
                reader.GetInt16(reader.GetOrdinal("score")),
                reader.GetInt16(reader.GetOrdinal("damage")),
                reader.GetByte(reader.GetOrdinal("c_casts")),
                reader.GetByte(reader.GetOrdinal("q_casts")),
                reader.GetByte(reader.GetOrdinal("e_casts")),
                reader.GetByte(reader.GetOrdinal("x_casts")),
                reader.GetInt16(reader.GetOrdinal("damage_to_allies")),
                reader.GetInt16(reader.GetOrdinal("damage_from_allies")),
                (uint)reader.GetInt32(reader.GetOrdinal("game_length")),
                reader.IsDBNull(reader.GetOrdinal("game_start_patched")) ? null : reader.GetDateTime(reader.GetOrdinal("game_start_patched")),
                reader.GetBoolean(reader.GetOrdinal("mvp")),
                reader.IsDBNull(reader.GetOrdinal("current_tier")) ? null : reader.GetByte(reader.GetOrdinal("current_tier")),
                reader.IsDBNull(reader.GetOrdinal("team")) ? null : reader.GetString(reader.GetOrdinal("team"))
            );
        }

        public override string ToString()
        {
            return $"Match_id: {Match_id}, Val_puuid: {Val_puuid}, Map: {Map}, Mode: {Mode}, Rounds: {Rounds}, Character: {Character}, " +
                   $"Rr_change: {Rr_change}, Double_Kills: {Double_Kills}, Triple_Kills: {Triple_Kills}, " +
                   $"Quad_Kills: {Quad_Kills}, Aces: {Aces}, Kills: {Kills}, Knife_Kills: {Knife_Kills}, " +
                   $"Deaths: {Deaths}, Knife_Deaths: {Knife_Deaths}, Assists: {Assists}, Bodyshots: {Bodyshots}, " +
                   $"Headshots: {Headshots}, Score: {Score}, Damage: {Damage}, C_casts: {C_casts}, Q_casts: {Q_casts}, " +
                   $"E_casts: {E_casts}, X_casts: {X_casts}, Damage_To_Allies: {Damage_To_Allies}, " +
                   $"Damage_From_Allies: {Damage_From_Allies}, Game_Length: {Game_Length}, Game_Start_Patched: {Game_Start_Patched ?? DateTime.MinValue}" +
                   $", MVP: {MVP}, Current_Tier: {Current_Tier}, Team: {Team}";
        }
    }

    public class OverallMatchStats
    {
        public string Val_puuid { get; private set; }
        public int Rounds { get; private set; }
        public int Rr_change { get; private set; }
        public int Double_Kills { get; private set; }
        public int Triple_Kills { get; private set; }
        public int Quad_Kills { get; private set; }
        public int Aces { get; private set; }
        public int Kills { get; private set; }
        public int Knife_Kills { get; private set; }
        public int Deaths { get; private set; }
        public int Knife_Deaths { get; private set; }
        public int Assists { get; private set; }
        public double Bodyshots { get; private set; }
        public double Headshots { get; private set; }
        public long Score { get; private set; }
        public long Damage { get; private set; }
        public int C_casts { get; private set; }
        public int Q_casts { get; private set; }
        public int E_casts { get; private set; }
        public int X_casts { get; private set; }
        public int Damage_To_Allies { get; private set; }
        public int Damage_From_Allies { get; private set; }
        public long Game_Length { get; private set; }
        public short MVP { get; private set; }

        public OverallMatchStats(
            string val_puuid,
            int rounds,
            int rr_change,
            int double_Kills,
            int triple_Kills,
            int quad_Kills,
            int aces,
            int kills,
            int knife_Kills,
            int deaths,
            int knife_Deaths,
            int assists,
            double bodyshots,
            double headshots,
            long score,
            long damage,
            int c_casts,
            int q_casts,
            int e_casts,
            int x_casts,
            int damage_To_Allies,
            int damage_From_Allies,
            long game_Length,
            short mvp
            )
        {
            Val_puuid = val_puuid;
            Rounds = rounds;
            Rr_change = rr_change;
            Double_Kills = double_Kills;
            Triple_Kills = triple_Kills;
            Quad_Kills = quad_Kills;
            Aces = aces;
            Kills = kills;
            Knife_Kills = knife_Kills;
            Deaths = deaths;
            Knife_Deaths = knife_Deaths;
            Assists = assists;
            Bodyshots = bodyshots;
            Headshots = headshots;
            Score = score;
            Damage = damage;
            C_casts = c_casts;
            Q_casts = q_casts;
            E_casts = e_casts;
            X_casts = x_casts;
            Damage_To_Allies = damage_To_Allies;
            Damage_From_Allies = damage_From_Allies;
            Game_Length = game_Length;
            MVP = mvp;
        }

        public static OverallMatchStats CreateFromRow(SqliteDataReader reader)
        {
            return new OverallMatchStats(
                reader.GetString(reader.GetOrdinal("val_puuid")),
                reader.GetInt32(reader.GetOrdinal("sum_of_rounds")),
                reader.GetInt32(reader.GetOrdinal("sum_of_rr_change")),
                reader.GetInt32(reader.GetOrdinal("sum_of_double_kills")),
                reader.GetInt32(reader.GetOrdinal("sum_of_triple_kills")),
                reader.GetInt32(reader.GetOrdinal("sum_of_quad_kills")),
                reader.GetInt32(reader.GetOrdinal("sum_of_aces")),
                reader.GetInt32(reader.GetOrdinal("sum_of_kills")),
                reader.GetInt32(reader.GetOrdinal("sum_of_knife_kills")),
                reader.GetInt32(reader.GetOrdinal("sum_of_deaths")),
                reader.GetInt32(reader.GetOrdinal("sum_of_knife_deaths")),
                reader.GetInt32(reader.GetOrdinal("sum_of_assists")),
                reader.GetFloat(reader.GetOrdinal("sum_of_bodyshots")),
                reader.GetFloat(reader.GetOrdinal("sum_of_headshots")),
                reader.GetInt64(reader.GetOrdinal("sum_of_score")),
                reader.GetInt64(reader.GetOrdinal("sum_of_damage")),
                reader.GetInt32(reader.GetOrdinal("sum_of_c_casts")),
                reader.GetInt32(reader.GetOrdinal("sum_of_q_casts")),
                reader.GetInt32(reader.GetOrdinal("sum_of_e_casts")),
                reader.GetInt32(reader.GetOrdinal("sum_of_x_casts")),
                reader.GetInt32(reader.GetOrdinal("sum_of_damage_to_allies")),
                reader.GetInt32(reader.GetOrdinal("sum_of_damage_from_allies")),
                reader.GetInt64(reader.GetOrdinal("sum_of_game_length")),
                reader.GetInt16(reader.GetOrdinal("sum_of_mvp"))
            );
        }

        public override string ToString()
        {
            return $"Val_puuid: {Val_puuid}, " +
                   $"Rounds: {Rounds}, " +
                   $"RR Change: {Rr_change}, " +
                   $"Double Kills: {Double_Kills}, " +
                   $"Triple Kills: {Triple_Kills}, " +
                   $"Quad Kills: {Quad_Kills}, " +
                   $"Aces: {Aces}, " +
                   $"Kills: {Kills}, " +
                   $"Knife Kills: {Knife_Kills}, " +
                   $"Deaths: {Deaths}, " +
                   $"Knife Deaths: {Knife_Deaths}, " +
                   $"Assists: {Assists}, " +
                   $"Bodyshots: {Bodyshots}, " +
                   $"Headshots: {Headshots}, " +
                   $"Score: {Score}, " +
                   $"Damage: {Damage}, " +
                   $"C casts: {C_casts}, " +
                   $"Q casts: {Q_casts}, " +
                   $"E casts: {E_casts}, " +
                   $"X casts: {X_casts}, " +
                   $"Damage to Allies: {Damage_To_Allies}, " +
                   $"Damage from Allies: {Damage_From_Allies}, " +
                   $"Game Length: {Game_Length}, " +
                   $"MVP: {MVP}";
        }
    }
}
