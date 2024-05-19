using Discord;
using Microsoft.Extensions.Logging;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;
using ValorantApp.GenericExtensions;
using ValorantApp.Valorant.Enums;
using ValorantApp.Valorant.Helpers;

namespace ValorantApp.Valorant
{
    public class BaseValorantUser
    {
        public BaseValorantUser(string username, string tagName, string affinity, ulong discId, IHttpClientFactory httpClientFactory, ILogger<BaseValorantProgram> logger, string? puuid = null)
        {
            HenrikApi = new HenrikApi(username, tagName, affinity, puuid, httpClientFactory, logger);
            this.puuid = HenrikApi.puuid;
            Logger = logger;
            userInfo = new ValorantUsers(username, tagName, affinity, Puuid, discId);

            Console.WriteLine("Valorant user created");
        }

        public  BaseValorantUser(ValorantUsers valorantUser, IHttpClientFactory httpClientFactory, ILogger<BaseValorantProgram> logger)
        {
            HenrikApi = new HenrikApi(valorantUser.Val_username, valorantUser.Val_tagname, valorantUser.Val_affinity, valorantUser.Val_puuid, httpClientFactory, logger);
            puuid = HenrikApi.puuid;
            Logger = logger;
            userInfo = valorantUser;

            Console.WriteLine("Valorant user created");
        }

        #region Globals

        private ILogger<BaseValorantProgram> Logger { get; set; }

        private HenrikApi HenrikApi { get; set; }

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
                    userInfo = ValorantUsersExtension.GetRow(Puuid);
                }

                return userInfo;
            }
        }

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

        private HashSet<ulong>? channelIds { get; set; }

        public HashSet<ulong> ChannelIds
        {
            get
            {
                if (channelIds == null)
                {
                    channelIds = ValorantChannelMappingsExtension.GetRowDiscordId(Puuid).ToHashSet();
                }

                return channelIds;
            }
        }

        #endregion Globals

        #region Methods

        #region Database

        #region Database - Matches

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

        private IEnumerable<Matches> GetMatches(IEnumerable<string> matchIds)
        {
            return MatchesExtension.GetListOfRows(matchIds);
        }

        public IEnumerable<BaseValorantMatch> GetBaseValorantMatch(EpisodeActInfos season)
        {
            IEnumerable<MatchStats> matchStats = GetCompMatchStats(season);
            IEnumerable<Matches> matches = GetMatches(matchStats.Select(x => x.Match_id));

            return matchStats.Join(matches, stats => stats.Match_id, match => match.Match_Id, (stats, match) => new BaseValorantMatch(stats, match, UserInfo, Logger));
        }

        #endregion Database - Matches

        #region Database - Channel Mappings

        public bool AddChannelId(ulong channelId)
        {
            if (!ChannelIds.Add(channelId))
            {
                return false;
            }

            return ValorantChannelMappingsExtension.InsertRow(new ValorantChannelMappings(Puuid, channelId));
        }

        public bool RemoveChannelId(ulong channelId)
        {
            if (!ChannelIds.Remove(channelId))
            {
                return false;
            }

            return ValorantChannelMappingsExtension.RemoveRow(new ValorantChannelMappings(Puuid, channelId));
        }

        #endregion Database - Channel Mappings

        #region Database - Valorant User
        
        /// <summary>
        /// Persist the user. UserInfo must be set if it is a new user.
        /// </summary>
        /// <returns></returns>
        public bool PersistUser()
        {
            ValorantUsers userDb = new ValorantUsers(UserInfo.Val_username, UserInfo.Val_tagname, UserInfo.Val_affinity, UserInfo.Val_puuid, UserInfo.Disc_id);

            return ValorantUsersExtension.InsertRow(userDb);
        }

        public bool DeleteUser()
        {
            return ValorantUsersExtension.DeleteRow(UserInfo.Val_puuid, UserInfo.Disc_id);
        }

        #endregion Database - Valorant User

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

        #endregion Henrik API

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

        #region Channels

        public bool IsInChannel(ulong channelId)
        {
            return ChannelIds.Contains(channelId);
        }

        #endregion Channels

        #endregion Methodsd
    }
}
