using Microsoft.Data.Sqlite;

namespace ValorantApp.Database.Tables
{
    public class MatchStats
    {
        public string Match_id { get; private set; }
        public string Val_puuid { get; private set; }
        public string Map { get; private set; }
        public string Mode { get; private set; }
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
        public uint Game_Length { get; private set; }
        public DateTime? Game_Start_Patched { get; private set; }

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
            DateTime? gameStartPatched
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
                reader.IsDBNull(reader.GetOrdinal("game_start_patched")) ? null : reader.GetDateTime(reader.GetOrdinal("game_start_patched"))
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
                   $"Damage_From_Allies: {Damage_From_Allies}, Game_Length: {Game_Length}, Game_Start_Patched: {Game_Start_Patched ?? DateTime.MinValue}";
        }
    }
}
