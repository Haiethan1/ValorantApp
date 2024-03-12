
using Newtonsoft.Json;

namespace ValorantApp
{
    public class MmrV2Json
    {
        public string? Name { get; set; }
        public string? Tag { get; set; }
        public MmrCurrentDataJson Current_Data { get; set; }
        public MmrHighestRankJson Highest_Rank { get; set; }
        
        [JsonIgnore]
        public string? By_Season { get; set; }
    }

    public class MmrCurrentDataJson
    {
        public int CurrentTier { get; set; }
        public string? CurrentTierPatched { get; set; }
        public MmrImagesJson? Images { get; set; }
        public int Ranking_In_Tier { get; set; }
        public int Mmr_Change_To_Last_Game { get; set; }
        public int Elo { get; set; }
        public int? Games_Needed_For_Rating { get; set; }
        public bool Old { get; set; }
    }

    public class MmrHighestRankJson
    {
        public bool Old { get; set; }
        public int Tier { get; set; }
        public string? Patched_Tier { get; set; }
        public string? Season { get; set; }
        public int? Converted {  get; set; }
    }

    public class MmrJson
    {
        public int CurrentTier { get; set; }
        public string? CurrentTier_Patched { get; set; }
        public MmrImagesJson? Images { get; set; }
        public int Ranking_In_Tier { get; set; }
        public int Mmr_Change_To_Last_Game { get; set; }
        public int Elo { get; set; }
        public string? name { get; set; }
        public string? tag { get; set; }
        public bool Old { get; set; }
    }
}