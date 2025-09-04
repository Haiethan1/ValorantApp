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

        public BaseValorantUser(ValorantUsers valorantUser, IHttpClientFactory httpClientFactory, ILogger<BaseValorantProgram> logger)
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
        /// <param name="startDateUTC"></param>
        /// <param name="endDateUTC"></param>
        /// <returns></returns>
        private IEnumerable<MatchStats> GetCompMatchStats(DateTime startDateUTC, DateTime endDateUTC)
        {
            return MatchStatsExtension.GetCompMatchStats(Puuid, startDateUTC, endDateUTC);
        }

        private IEnumerable<MatchStats> GetMatchStatsExceptForDeathMatch(DateTime startDateUTC, DateTime endDateUTC)
        {
            return MatchStatsExtension.GetMatchStatsExceptForDeathMatch(Puuid, startDateUTC, endDateUTC);
        }

        private static IEnumerable<Matches> GetMatches(IEnumerable<string> matchIds)
        {
            return MatchesExtension.GetListOfRows(matchIds);
        }

        public IEnumerable<BaseValorantMatch> GetBaseValorantMatchBySeason(EpisodeActInfos season)
        {
            return GetBaseValorantMatch(season.StartDate, season.EndDate, true);
        }

        public IEnumerable<BaseValorantMatch> GetBaseValorantMatch(DateTime startDateUTC, DateTime endDateUTC, bool competitiveOnly)
        {
            IEnumerable<MatchStats> matchStats = competitiveOnly ? GetCompMatchStats(startDateUTC, endDateUTC) : GetMatchStatsExceptForDeathMatch(startDateUTC, endDateUTC);
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

        /// <summary>
        /// Update the user. A user's username and tagname can change.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="tagname"></param>
        /// <returns></returns>
        public bool UpdateUser(string username, string tagname)
        {
            if (username.IsNullOrEmpty() || tagname.IsNullOrEmpty())
            {
                Logger.LogWarning($"{nameof(UpdateUser)}: Username and tagname cannot be null");
                return false;
            }

            // Check if either the username or tagname is different (case-insensitive)
            if (!string.Equals(UserInfo.Val_username, username, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(UserInfo.Val_tagname, tagname, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInformation($"{UserInfo.Val_username}#{UserInfo.Val_tagname} -> {username}#{tagname}");

                UserInfo.Val_username = username;
                UserInfo.Val_tagname = tagname;

                return ValorantUsersExtension.UpdateRow(UserInfo);
            }

            // If no updates were needed, return false
            return false;

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
            previousTier = 0;

            if (stats == null
                || matches == null
                || stats.Val_puuid != Puuid
                || stats.Match_id != matches.Match_Id
                || ModesExtension.ModeFromString(matches.Mode.Safe()) != Modes.Competitive
                || stats.New_Tier == null
                || stats.New_Tier == 0
                || stats.Current_Tier == null
                || stats.Current_Tier == 0)
            {
                return false;
            }

            currentTier = stats.Current_Tier.Value;
            previousTier = CurrentTier ?? stats.Current_Tier.Value;

            if (stats.New_Tier == CurrentTier)
            {
                return false;
            }

            currentTier = stats.New_Tier.Value;
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
