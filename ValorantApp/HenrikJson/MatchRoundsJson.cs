using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValorantApp.HenrikJson
{
    public class MatchRoundsJson
    {
        public string? Winning_Team { get; set; }
        public string? End_Type { get; set; }
        public bool? Bomb_Plated { get; set; }
        public bool? Bomb_Defused { get; set; }
        public MatchRoundsPlantEventsJson? Plant_Events { get; set; }
        public MatchRoundsDefuseEventsJson? Defuse_Events { get; set; }
        public MatchRoundPlayerStatsJson[]? Player_Stats { get; set; }
    }

    #region SpikeEvents
    public class MatchRoundsPlantEventsJson
    {
        public RoundLocationJson? Plant_Location { get; set; }
        public RoundPlantedByJson? Planted_By { get; set; }
        public string? Plant_Site { get; set; }
        public int? Plant_Time_In_Round { get; set; }
        public RoundPlayerLocationOnEventJson[]? Player_Locations_On_Plant { get; set; }
    }

    public class MatchRoundsDefuseEventsJson
    {
        public RoundLocationJson? Defuse_Location { get; set; }
        public RoundPlantedByJson? Defused_By { get; set; }
        public int? Defuse_Time_In_Round { get; set; }
        public RoundPlayerLocationOnEventJson[]? Player_Locations_On_Defuse { get; set; }
    }

    public class RoundPlantedByJson
    {
        public string? Puuid { get; set; }
        public string? Display_Name { get; set; }
        public string? Team { get; set; }
    }
    #endregion

    #region Player Stats
    public class MatchRoundPlayerStatsJson
    {
        public PlayerAbilityCastsJson? Ability_Casts { get; set; }
        public string? Player_Puuid { get; set; }
        public string? Player_Display_Name { get; set; }
        public string? Player_Team { get; set; }
        public RoundDamageEventsJson[]? Damage_Events { get; set; }
        public int Damage { get; set; }
        public int Bodyshots { get; set; }
        public int Headshots { get; set; }
        public int Legshots { get; set; }
        public RoundKillEventsJson[]? Kill_Events { get; set; }
        public int Kills { get; set; }
        public int Score { get; set; }
        public RoundEconomyJson? Economy { get; set; }
        public bool Was_Afk { get; set; }
        public bool Was_Penalized { get; set; }
        public bool Stayed_In_Spawn { get; set; }
    }

    public class RoundDamageEventsJson
    {
        public string? Receiver_Puuid { get; set; }
        public string? Receiver_Display_Name { get; set; }
        public string? Receiver_Team { get; set; }
        public int Bodyshots { get; set; }
        public int Damage { get; set; }
        public int Headshots { get; set; }
        public int Legshots { get; set; }
    }

    public class RoundKillEventsJson
    {
        public int Kill_Time_In_Round { get; set; }
        public int Kill_Time_In_Match { get; set; }
        public string? Killer_Puuid { get; set; }
        public string? Killer_Display_Name { get; set; }
        public string? Killer_Team { get; set; }
        public string? Victim_Puuid { get; set; }
        public string? Victim_Display_Name { get; set; }
        public string? Victim_Team { get; set; }
        public RoundLocationJson? Victim_Death_Location { get; set; }
        public string? Damage_Weapon_Id { get; set; }
        public string? Damage_Weapon_Name { get; set; }
        public PlayerAssetsJson? Damage_Weapon_Assets { get; set; }
        public bool Secondary_Fire_Mode { get; set; }
        public RoundPlayerLocationOnEventJson[]? Player_Locations_On_kill { get; set; }
        public RoundPlayerStatsAssistantsJson[]? Assistants { get; set; }
    }

    public class RoundEconomyJson
    {
        public int Loadout_Value { get; set; }
        public RoundShopAssetsJson? Weapon { get; set; }
        public RoundShopAssetsJson? Armor { get; set; }
        public int Remaining { get; set; }
        public int Spent { get; set; }
    }

    public class RoundShopJson
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public RoundShopAssetsJson? Assets { get; set; }
    }

    public class RoundShopAssetsJson
    {
        public string? Display_Icon { get; set; }
        public string? Killfeed_Icon { get; set; }
    }

    public class RoundPlayerStatsAssistantsJson
    {
        public string? Assistant_Puuid { get; set; }
        public string? Assistant_Display_Name { get; set; }
        public string? Assistant_Team { get; set; }
    }
    #endregion

    #region Round Helpers
    public class RoundPlayerLocationOnEventJson
    {
        public string? Player_Puuid { get; set; }
        public string? Player_Display_Name { get; set; }
        public string? Player_Team { get; set; }
        public RoundLocationJson? Location { get; set; }
        public double View_Radians { get; set; }
    }

    public class RoundLocationJson
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
    #endregion
}
