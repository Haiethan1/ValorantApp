using ValorantApp.HenrikJson;

namespace ValorantApp
{
    public class MatchJson
    {
        public MatchMetadataJson Metadata { get; set; }
        public MatchPlayersJson Players { get; set; }
        public MatchObserversJson[] Observers { get; set; }
        public MatchCoachesJson[] Coaches { get; set; }
        public MatchTeamsJson Teams { get; set; }
        public MatchRoundsJson[] Rounds { get; set; }

        // finish up.
    }
}