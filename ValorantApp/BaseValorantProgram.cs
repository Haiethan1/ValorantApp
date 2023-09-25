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

        public bool CreateUser(string username, string tagname)
        {
            if (Users == null)
            {
                return false;
            }

            try
            {
                ValorantUsers? valorantUser = ValorantUsersExtension.GetRow(username, tagname);
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

        public bool UpdateMatchAllUsers()
        {
            if (Users == null || !Users.Any())
            {
                return false;
            }

            HashSet<string> updatedUsers = new();

            foreach (BaseValorantUser user in Users.Values)
            {
                if (user == null || updatedUsers.Contains(user.Puuid))
                {
                    // should check for time here
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
                        || MatchStatsExtension.MatchIdExistsForUser(match.Metadata.MatchId, userInMatch.Puuid)
                        || DateTimeOffset.FromUnixTimeSeconds(match.Metadata.Game_Start).DateTime.ToUniversalTime().AddMinutes(30) > DateTime.UtcNow
                        )
                    {
                        continue;
                    }

                    MmrHistoryJson? mmrHistory = userInMatch.GetMatchMMR(match?.Metadata.MatchId);

                    if (mmrHistory == null)
                    {
                        continue;
                    }

                    if (CheckMatch(match, mmrHistory, userInMatch.Puuid))
                    {
                        updatedUsers.Add(userInMatch.Puuid);
                        Console.WriteLine($"Match stats updated for {userInMatch.UserName}#{userInMatch.TagName}. Match ID: {match.Metadata.MatchId}, Match Date: {match.Metadata.Game_Start_Patched}");
                    }
                    else
                    {
                        Console.WriteLine($"Match stats did not update for {userInMatch.UserName}#{userInMatch.TagName}.");
                    }
                }
            }

            return true;
        }


        /// <summary>
        /// OBSOLETE -- don't use.
        /// </summary>
        /// <param name="puuid"></param>
        /// <returns></returns>
        public bool UpdateMatchForUser(string puuid)
        {
            if (String.IsNullOrEmpty(puuid) || !Users.ContainsKey(puuid))
            {
                return false;
            }

            BaseValorantUser user = Users[puuid];

            if (user == null)
            {
                // should check for time here
                return false;
            }

            MatchJson? match = user.GetLastCompMatch();
            MmrHistoryJson? mmrHistory = user.GetMatchMMR(match?.Metadata.MatchId);

            bool checkMatch = CheckMatch(match, mmrHistory, puuid);

            if (checkMatch)
            {
                Console.WriteLine($"Match stats updated for {user.UserName}#{user.TagName}. Match ID: {match.Metadata.MatchId}, Match Date: {match.Metadata.Game_Start_Patched}");
            }
            else
            {
                Console.WriteLine($"Match stats did not update for {user.UserName}#{user.TagName}.");
            }

            return checkMatch;
        }

        private bool CheckMatch(MatchJson? match, MmrHistoryJson? MmrHistory, string puuid)
        {
            if (match == null || MmrHistory == null || string.IsNullOrEmpty(puuid))
            {
                return false;
            }

            // check match id if already in db?..

            MatchStats? matchStats = MatchStatsExtension.CreateFromJson(match, MmrHistory, puuid);

            if (matchStats == null)
            {
                return false;
            }

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
