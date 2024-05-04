using ValorantApp.Database.Repositories.Interfaces;
using ValorantApp.Database.Tables;

namespace ValorantApp.Database.Repositories
{
    public class MatchesRepository : IMatchesRepository
    {
        private readonly ValorantDbContext _context;
        public MatchesRepository(ValorantDbContext context)
        {
            _context = context;
        }

        public IEnumerable<(Matches, MatchStats)> GetCompMatchStats(string puuid, DateTime startDate, DateTime endDate)
        {
            return _context.MatchStats
                .Join(
                    _context.Matches,
                    matchStats => matchStats.Match_id,
                    matches => matches.Match_Id,
                    (matchStats, matches) => new { MatchStats = matchStats, Matches = matches })
                .Where(x => x.MatchStats.Val_puuid == puuid && x.Matches.Game_Start_Patched_UTC >= startDate && x.Matches.Game_Start_Patched_UTC <= endDate)
                .Select(x => new { x.Matches, x.MatchStats })
                .AsEnumerable() // Execute the query
                .Select(x => (x.Matches, x.MatchStats)); ;
        }
    }
}
