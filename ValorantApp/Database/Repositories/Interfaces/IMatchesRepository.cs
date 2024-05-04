using ValorantApp.Database.Tables;

namespace ValorantApp.Database.Repositories.Interfaces
{
    public interface IMatchesRepository
    {
        IEnumerable<(Matches, MatchStats)> GetCompMatchStats(string puuid, DateTime startdate, DateTime enddate);
    }
}
