using Microsoft.Extensions.Logging;
using ValorantApp.Database;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Repositories.Interfaces;
using ValorantApp.Database.Tables;
using ValorantApp.GenericExtensions;
using ValorantApp.Valorant.Enums;
using ValorantApp.Valorant.Helpers;

namespace ValorantApp.Valorant
{
    public class BaseValorantUser
    {
        public BaseValorantUser(
            string username
            , string tagName
            , string affinity
            , IHttpClientFactory httpClientFactory
            , ILogger<BaseValorantProgram> logger
            , IMatchesRepository matchesRepository
            , IMatchStatsRepository matchStatsRepository
            , IValorantUsersRepository valorantUsersRepository
            , string? puuid = null
            )
        {
            HenrikApi = new HenrikApi(username, tagName, affinity, puuid, httpClientFactory, logger);
            this.puuid = HenrikApi.puuid;
            Logger = logger;
            MatchesRepository = matchesRepository;
            MatchStatsRepository = matchStatsRepository;
            ValorantUsersRepository = valorantUsersRepository;

            Console.WriteLine("Valorant user created");
        }

        #region Globals

        private ILogger<BaseValorantProgram> Logger { get; set; }

        private IMatchesRepository MatchesRepository { get; set; }

        private IMatchStatsRepository MatchStatsRepository { get; set; }

        private IValorantUsersRepository ValorantUsersRepository { get; set; }

        private string puuid;

        public string Puuid
        {
            get { return puuid; }
        }

        private ValorantUsers? userInfo;

        public ValorantUsers UserInfo
        {
            get
            {
                if (userInfo == null)
                {
                    userInfo = ValorantUsersExtension.GetRow(puuid);
                }

                return userInfo;
            }
        }

        private HenrikApi HenrikApi { get; set; }

        private int? currentTier { get; set; }

        public int? CurrentTier
        {
            get
            {
                if (currentTier == null)
                {
                    currentTier = MatchStatsExtension.GetLastCompMatchStats(Puuid)?.New_Tier;
                }

                return currentTier;
            }
        }

        #endregion

        #region Methods

        #region Database

        public IEnumerable<BaseValorantMatch> GetBaseValorantMatch(EpisodeActInfos season)
        {
            IEnumerable<MatchStats> matchStats = GetCompMatchStats(season);
            IEnumerable<Matches> matches = GetMatches(matchStats.Select(x => x.Match_id));

            return matchStats.Join(matches, stats => stats.Match_id, match => match.Match_Id, (stats, match) => new BaseValorantMatch(stats, match, UserInfo, Logger));
        }

        public IEnumerable<BaseValorantMatch> GetBaseValorantMatch(DateTime startDate, DateTime endDate)
        {
            IEnumerable<(Matches, MatchStats)> temp = MatchesRepository.GetCompMatchStats(Puuid, startDate, endDate);
            return temp.Select(x => new BaseValorantMatch(x.Item2, x.Item1, UserInfo, Logger));
            //IEnumerable<MatchStats> matchStats = GetCompMatchStats(startDate, endDate);
            //IEnumerable<Matches> matches = GetMatches(matchStats.Select(x => x.Match_id));

            //return matchStats.Join(matches, stats => stats.Match_id, match => match.Match_Id, (stats, match) => new BaseValorantMatch(stats, match, UserInfo, Logger));
        }

        /// <summary>
        /// Get all comp match stats for the specified season.
        /// Slightly expensive query.
        /// </summary>
        /// <param name="season"></param>
        /// <returns></returns>
        private IEnumerable<MatchStats> GetCompMatchStats(EpisodeActInfos season)
        {
            return MatchStatsExtension.GetCompMatchStats(Puuid, season.StartDate, season.EndDate);
        }

        /// <summary>
        /// Get all comp match stats for the specified dates.
        /// Slightly expensive query.
        /// </summary>
        /// <param name="season"></param>
        /// <returns></returns>
        private IEnumerable<MatchStats> GetCompMatchStats(DateTime startDate, DateTime endDate)
        {
            return MatchStatsExtension.GetCompMatchStats(Puuid, startDate, endDate);
        }

        private IEnumerable<Matches> GetMatches(IEnumerable<string> matchIds)
        {
            return MatchesExtension.GetListOfRows(matchIds);
        }

        #endregion Database

        #region Henrik API

        public MmrV2Json? GetMMR()
        {
            return HenrikApi.Mmr()?.Result?.Data;
        }

        private List<MmrHistoryJson>? GetMMRHistory()
        {
            return HenrikApi.MmrHistory()?.Result?.Data;
        }

        public MmrHistoryJson? GetMatchMMR(string? matchId)
        {
            if (string.IsNullOrEmpty(matchId))
            {
                return null;
            }

            List<MmrHistoryJson>? mmrHistoryJsons = GetMMRHistory();

            if (mmrHistoryJsons == null || mmrHistoryJsons.Count == 0)
            {
                return null;
            }

            return mmrHistoryJsons.FirstOrDefault(mmrHistory => mmrHistory.Match_id == matchId);
        }

        public MmrHistoryJson? GetLastMatchMMR()
        {
            List<MmrHistoryJson>? mmrHistoryJsons = GetMMRHistory();

            if (mmrHistoryJsons == null || mmrHistoryJsons.Count == 0)
            {
                return null;
            }

            return mmrHistoryJsons.MaxBy(mmrHistory => mmrHistory.Date_raw);
        }

        private List<MatchJson>? GetMatch(Modes mode = Modes.Unknown, Maps map = Maps.Unknown, int size = 1)
        {
            return HenrikApi.Match(mode, map, size)?.Result?.Data;
        }

        public MatchJson? GetLastMatch()
        {
            List<MatchJson>? matchJsons = GetMatch(Modes.Unknown, Maps.Unknown, 1);

            if (matchJsons == null || matchJsons.Count == 0)
            {
                return null;
            }

            return matchJsons.FirstOrDefault();
        }

        #endregion

        #region Create user

        /// <summary>
        /// This will create a NEW user. Use this method when the user is not in the DB yet.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="tagName"></param>
        /// <param name="affinity"></param>
        /// <param name="discId"></param>
        /// <param name="logger"></param>
        /// <param name="puuid"></param>
        /// <returns></returns>
        public static BaseValorantUser? CreateUser(
            string username
            , string tagName
            , string affinity
            , ulong discId
            , IHttpClientFactory httpClientFactory
            , ILogger<BaseValorantProgram> logger
            , IMatchesRepository matchesRepository
            , IMatchStatsRepository matchStatsRepository
            , IValorantUsersRepository valorantUsersRepository
            , string? puuid = null
            )
        {
            BaseValorantUser user = new BaseValorantUser(
                username
                , tagName
                , affinity
                , httpClientFactory
                , logger
                , matchesRepository
                , matchStatsRepository
                , valorantUsersRepository
                , puuid
                );
            ValorantUsers userDb = new ValorantUsers(username, tagName, affinity, user.Puuid, discId);

            bool inserted = ValorantUsersExtension.InsertRow(userDb);

            return inserted ? user : null;
        }

        #endregion

        #region Update user

        public bool UpdateCurrentTier(MatchStats stats, Matches matches, out int previousTier)
        {
            previousTier = CurrentTier ?? 0;
            if (stats == null
                || matches == null
                || stats.Val_puuid != Puuid
                || stats.Match_id != matches.Match_Id
                || ModesExtension.ModeFromString(matches.Mode.Safe()) != Modes.Competitive
                || stats.New_Tier == CurrentTier
                || currentTier == 0
                || stats.New_Tier == 0)
            {
                return false;
            }

            currentTier = stats.New_Tier;
            return true;
        }

        #endregion Update user

        #endregion
    }
}
