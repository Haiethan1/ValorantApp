using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValorantApp
{
    public class MatchPlayersJson
    {
        public MatchPlayerJson[] All_Players { get; set; }
        public MatchPlayerJson[] Red { get; set; }
        public MatchPlayerJson[] Blue { get; set; }
    }

    public class MatchPlayerJson
    {
        public string Puuid { get; set; }
        public string Name { get; set; }
        public string Tag { get; set; }
        public string Team { get; set; }
        public int Level { get; set; }
        public string Character { get; set; }
        public int CurrentTier { get; set; }
        public string CurrentTier_Patched { get; set; }
        public string Player_Card { get; set; }
        public string Player_Title { get; set; }
        public string Party_Id { get; set; }
        public PlayerSessionPlaytimeJson Session_Playtime { get; set; }
        public PlayerAssetsJson Assets { get; set; }
        public PlayerBehaviorJson Behavior { get; set; }
        public PlayerPlatformJson Platform { get; set; }
        public PlayerAbilityCastsJson Ability_Casts { get; set; }
        public PlayerStatsJson Stats { get; set; }
        public PlayerEconomyJson Economy { get; set; }
        public int Damage_Made { get; set; }
        public int Damage_Received { get; set; }
    }

    #region Session
    public class PlayerSessionPlaytimeJson
    {
        public int Minutes{ get; set; }
        public int Seconds { get; set; }
        public int Milliseconds { get; set; }
    }
    #endregion

    #region Assets
    public class PlayerAssetsJson
    {
        public PlayerAssetsCardJson Card { get; set; }
        public PlayerAssetsAgentJson Agent { get; set; }
    }

    public class PlayerAssetsCardJson
    {
        public string Small { get; set; }
        public string Large { get; set; }
        public string Wide { get; set; }
    }

    public class PlayerAssetsAgentJson
    {
        public string Small { get; set; }
        public string Full { get; set; }
        public string Bust { get; set; }
        public string KillFeed { get; set; }
    }
    #endregion

    #region Behavior
    public class PlayerBehaviorJson
    {
        public int Afk_Rounds { get; set; }
        public PlayerBehaviorFriendlyFireJson Friendly_Fire { get; set; }
        public int Rounds_In_Spawn { get; set; }
    }

    public class PlayerBehaviorFriendlyFireJson
    {
        public int Incoming { get; set; }
        public int Outgoing { get; set; }
    }
    #endregion

    #region Platform
    public class PlayerPlatformJson
    {
        public string Type { get; set; }
        public PlayerPlatformOSJson OS { get; set; }
    }

    public class PlayerPlatformOSJson
    {
        public string Name { get; set; }
        public string Version { get; set; }
    }
    #endregion

    #region Ability Casts
    public class PlayerAbilityCastsJson
    {
        public int? C_Cast { get; set; }
        public int? Q_Cast { get; set; }
        public int? E_Cast { get; set; }
        public int? X_Cast { get; set; }
    }
    #endregion

    #region Stats
    public class PlayerStatsJson
    {
        public int Score { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Assists { get; set; }
        public int Bodyshots { get; set; }
        public int Headshots { get; set; }
        public int Legshots { get; set; }
    }
    #endregion

    #region Economy
    public class PlayerEconomyJson
    {
        public PlayerEconomyMoneyJson Spent { get; set; }
        public PlayerEconomyMoneyJson Loadout_Value { get; set; }
    }

    public class PlayerEconomyMoneyJson
    {
        public int Overall { get; set; }
        public int Average { get; set; }
    }
    #endregion
}
