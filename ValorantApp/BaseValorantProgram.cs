using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;
using ValorantApp.HenrikJson;
using ValorantNET.Models;

namespace ValorantApp
{
    public class BaseValorantProgram
    {
        // TODO add a db lock.
        private static readonly object DbLock = new object();

        public BaseValorantProgram() 
        {
            // initialize users here to something? maybe create all users??
            Users = new();
            CreateAllUsers();
        }

        #region Globals

        private Dictionary<string, BaseValorantUser> Users { get; set; }

        #endregion

        #region Methods

        #region Get

        public BaseValorantUser? GetValorantUser(string puuid)
        {
            return Users.GetValueOrDefault(puuid);
        }

        #endregion

        #region Create users

        /// <summary>
        /// Will query the ValorantUsers DB for all users and initialize tracking for them.
        /// Expensive!
        /// </summary>
        /// <returns></returns>
        private bool CreateAllUsers()
        {
            if (Users == null)
            {
                return false;
            }

            try
            {
                List<ValorantUsers> allValorantUsers = ValorantUsersExtension.GetAllRows();

                foreach (ValorantUsers user in allValorantUsers)
                {
                    if (Users.ContainsKey(user.Val_puuid))
                    {
                        continue;
                    }

                    Users.Add(user.Val_puuid, new BaseValorantUser(user.Val_username, user.Val_tagname, user.Val_affinity, user.Val_puuid));
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        public bool CreateUser(string puuid)
        {
            if (Users == null)
            {
                return false;
            }

            try
            {
                ValorantUsers? valorantUser = ValorantUsersExtension.GetRow(puuid);
                if (valorantUser == null ||  Users.ContainsKey(valorantUser.Val_puuid))
                {
                    return false;
                }

                Users.Add(valorantUser.Val_puuid, new BaseValorantUser(valorantUser.Val_username, valorantUser.Val_tagname, valorantUser.Val_affinity, valorantUser.Val_puuid));

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        #endregion

        #region Reloads

        public void ReloadFromDB()
        {
            ReloadUsers();
        }

        private void ReloadUsers()
        {
            Users.Clear();
            CreateAllUsers();
        }

        #endregion

        #region Check matches

        public bool UpdateMatchAllUsers(out Dictionary<string, MatchStats> userMatchStats)
        {
            userMatchStats = new Dictionary<string, MatchStats>();
            if (Users == null || !Users.Any())
            {
                return false;
            }

            HashSet<string> updatedUsers = new();

            foreach (BaseValorantUser user in Users.Values)
            {
                if (user == null || updatedUsers.Contains(user.UserInfo.Val_puuid))
                {
                    continue;
                }

                MatchJson? match = user.GetLastCompMatch();

                if (match == null)
                {
                    continue;
                }

                IEnumerable<BaseValorantUser> usersInMatch = CheckValorantUsersInMatch(match, updatedUsers);

                foreach (BaseValorantUser userInMatch in usersInMatch)
                {
                    if (user == null
                        || MatchStatsExtension.MatchIdExistsForUser(match.Metadata.MatchId, userInMatch.UserInfo.Val_puuid)
                        // Just look at if match id does not exist for now.
                        //|| DateTime.UtcNow > DateTimeOffset.FromUnixTimeSeconds(match.Metadata.Game_Start).DateTime.ToUniversalTime().AddMinutes(30)
                        )
                    {
                        continue;
                    }

                    MmrHistoryJson? mmrHistory = userInMatch.GetMatchMMR(match?.Metadata.MatchId);

                    if (mmrHistory == null)
                    {
                        continue;
                    }

                    if (CheckMatch(match, mmrHistory, userInMatch.UserInfo.Val_puuid, userMatchStats))
                    {
                        updatedUsers.Add(userInMatch.UserInfo.Val_puuid);
                        Console.WriteLine($"Match stats updated for {userInMatch.UserInfo.Val_username}#{userInMatch.UserInfo.Val_tagname}. Match ID: {match.Metadata.MatchId}, Match Date: {match.Metadata.Game_Start_Patched}");
                    }
                    else
                    {
                        Console.WriteLine($"Match stats did not update for {userInMatch.UserInfo.Val_username}#{userInMatch.UserInfo.Val_tagname}.");
                    }
                }
            }

            return true;
        }

        private static bool CheckMatch(MatchJson? match, MmrHistoryJson? MmrHistory, string puuid, Dictionary<string, MatchStats> userMatchStats)
        {
            if (match == null || MmrHistory == null || string.IsNullOrEmpty(puuid))
            {
                return false;
            }

            MatchStats? matchStats = MatchStatsExtension.CreateFromJson(match, MmrHistory, puuid);

            if (matchStats == null)
            {
                return false;
            }

            userMatchStats.Add(puuid, matchStats);
            MatchStatsExtension.InsertRow(matchStats);
            return true;
        }

        private IEnumerable<BaseValorantUser> CheckValorantUsersInMatch(MatchJson? match, HashSet<string> updatedPuuids)
        {
            if (match == null)
            {
                return Enumerable.Empty<BaseValorantUser>();
            }

            List<BaseValorantUser> usersInMatch = new();

            foreach (MatchPlayerJson matchPlayer in match.Players.All_Players)
            {
                if (matchPlayer == null || updatedPuuids.Contains(matchPlayer.Puuid) || !Users.ContainsKey(matchPlayer.Puuid))
                {
                    continue;
                }

                usersInMatch.Add(Users[matchPlayer.Puuid]);
            }

            return usersInMatch;
        }

        #endregion

        #endregion
    }
}
