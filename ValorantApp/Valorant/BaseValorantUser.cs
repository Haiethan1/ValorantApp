using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;
using ValorantApp.Valorant.Enums;

namespace ValorantApp.Valorant
{
    public class BaseValorantUser
    {
        public BaseValorantUser(string username, string tagName, string affinity, string? puuid = null)
        {
            HenrikApi = new HenrikApi(username, tagName, affinity, puuid, new HttpClient());
            this.puuid = HenrikApi.puuid;

            Console.WriteLine("Valorant user created");
        }

        #region Globals

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

        #endregion

        #region Methods

        #region Henrik API

        public MmrV2Json? GetMMR()
        {
            return HenrikApi.Mmr()?.Result.Data;
        }

        private List<MmrHistoryJson>? GetMMRHistory()
        {
            return HenrikApi.MmrHistory()?.Result.Data;
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

        private List<MatchJson>? GetMatch(Modes mode = Modes.Unknown, Maps map = Maps.Unknown, int size = 1)
        {
            return HenrikApi.Match(mode, map, size)?.Result.Data;
        }

        public MatchJson? GetLastMatch()
        {
            // don't add this to the commit.
            List<MatchJson>? matchJsons = GetMatch(Modes.Competitive, Maps.Unknown, 1);

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
        /// <param name="puuid"></param>
        /// <returns></returns>
        public static BaseValorantUser? CreateUser(string username, string tagName, string affinity, ulong discId, string? puuid = null)
        {
            BaseValorantUser user = new BaseValorantUser(username, tagName, affinity, puuid);
            ValorantUsers userDb = new ValorantUsers(username, tagName, affinity, user.Puuid, discId);

            bool inserted = ValorantUsersExtension.InsertRow(userDb);

            return inserted ? user : null;
        }

        #endregion

        #endregion
    }
}
