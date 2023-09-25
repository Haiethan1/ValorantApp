using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ValorantApp.ValorantEnum;

namespace ValorantApp
{
    public class BaseValorantUser
    {
        public BaseValorantUser(string username, string tagName, string affinity, string? puuid = null) {
            HenrikApi = new HenrikApi(username, tagName, affinity, puuid);

            Console.WriteLine("Valorant user created");
        }

        #region Globals

        private HenrikApi HenrikApi { get; set; }

        public string Puuid
        {
            get
            {
                if (HenrikApi == null)
                {
                    return string.Empty;
                }

                return HenrikApi.puuid;
            }
        }

        public string UserName
        {
            get
            {
                if (HenrikApi == null)
                {
                    return string.Empty;
                }

                return HenrikApi.username;
            }
        }

        public string TagName
        {
            get
            {
                if (HenrikApi == null)
                {
                    return string.Empty;
                }

                return HenrikApi.tagName;
            }
        }

        #endregion

        #region Henrik API

        public MmrJson? GetMMR()
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

            return mmrHistoryJsons.FirstOrDefault(mmrHistory => mmrHistory.match_id == matchId);
        }

        private List<MatchJson>? GetMatch(Modes mode = Modes.Unknown, Maps map = Maps.Unknown, int size = 1)
        {
            return HenrikApi.Match(mode, map, size)?.Result.Data;
        }

        public MatchJson? GetLastCompMatch()
        {
            List<MatchJson>? matchJsons = GetMatch(Modes.Competitive, Maps.Unknown, 1);

            if (matchJsons == null || matchJsons.Count == 0)
            {
                return null;
            }

            return matchJsons.FirstOrDefault();
        }

        #endregion
    }
}
