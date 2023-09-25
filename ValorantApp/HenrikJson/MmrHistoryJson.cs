using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValorantApp
{
    public class MmrHistoryJson
    {
        public int currenttier { get; set; }
        public string currenttierpatched { get; set; }
        public Images images { get; set; }
        public string match_id { get; set; }
        public Map map { get; set; }
        public string season_id { get; set; }
        public int ranking_in_tier { get; set; }
        public int mmr_change_to_last_game { get; set; }
        public int elo { get; set; }
        public string date { get; set; }
        public long date_raw { get; set; }
    }

    public class Images
    {
        public string small { get; set; }
        public string large { get; set; }
        public string triangle_down { get; set; }
        public string triangle_up { get; set; }
    }

    public class Map
    {
        public string name { get; set; }
        public string id { get; set; }
    }
}
