using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValorantApp
{
    public class MmrHistoryJson
    {
        public int Currenttier { get; set; }
        public string? Currenttierpatched { get; set; }
        public Images? Images { get; set; }
        public string? Match_id { get; set; }
        public Map? Map { get; set; }
        public string? Season_id { get; set; }
        public int Ranking_in_tier { get; set; }
        public int Mmr_change_to_last_game { get; set; }
        public int Elo { get; set; }
        public string? Date { get; set; }
        public long Date_raw { get; set; }
    }

    public class Images
    {
        public string? Small { get; set; }
        public string? Large { get; set; }
        public string? Triangle_down { get; set; }
        public string? Triangle_up { get; set; }
    }

    public class Map
    {
        public string? Name { get; set; }
        public string? Id { get; set; }
    }
}
