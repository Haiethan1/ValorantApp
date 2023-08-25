
namespace ValorantApp
{
    public class MmrJson
    {
        public int CurrentTier { get; set; }
        public string CurrentTier_Patched { get; set; }
        public MmrImagesJson Images { get; set; }
        public int Ranking_In_Tier { get; set; }
        public int Mmr_Change_To_Last_Game { get; set; }
        public int Elo { get; set; }
        public string name { get; set; }
        public string tag { get; set; }
        public bool Old { get; set; }
    }
}