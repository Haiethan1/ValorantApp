using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValorantApp
{
    public class MatchMetadataJson
    {
        public string? Map { get; set; }
        public string? Game_Version { get; set; }
        public int Game_Length { get; set; }
        public int Game_Start { get; set; }
        public string? Game_Start_Patched { get; set; }
        public int Rounds_Played { get; set; }
        public string? Mode { get; set; }
        public string? Mode_Id{ get; set; }
        public string? Queue { get; set; }
        public string? Season_Id { get; set; }
        public string? Platform { get; set; }
        public string? MatchId{ get; set; }
        public PremierInfoJson? Premier_Info { get; set; }
        public string? Region { get; set; }
        public string? Cluster { get; set; }
    }

    public class PremierInfoJson
    {
        public string? Tournament_Id { get; set; }
        public string? Matchup_Id { get; set; }
    }
}
