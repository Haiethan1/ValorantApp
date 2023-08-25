using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValorantApp.HenrikJson
{
    public class MatchObserversJson
    {
        public MatchObserverJson[] Observer { get; set; }
    }

    public class MatchObserverJson
    {
        public string Puuid { get; set; }
        public string Name { get; set; }
        public string Tag { get; set; }
        public PlayerPlatformJson Platform { get; set; }
        public PlayerSessionPlaytimeJson Session_Playtime { get; set; }
        public string Team { get; set; }
        public int Level { get; set; }
        public string Player_Card { get; set; }
        public string Player_Title { get; set; }
        public string Party_Id { get; set; }
    }
}
