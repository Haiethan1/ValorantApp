﻿using Microsoft.Extensions.Logging;
using Serilog.Core;
using System.Collections.Concurrent;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;
using ValorantApp.GenericExtensions;
using ValorantApp.HenrikJson;

namespace ValorantApp.Valorant
{
    public class BaseValorantProgram
    {
        // TODO add a db lock.
        private static readonly object DbLock = new object();

        private readonly IHttpClientFactory _httpClientFactory;

        public BaseValorantProgram(IHttpClientFactory httpClientFactory, ILogger<BaseValorantProgram> logger)
        {
            // initialize users here to something? maybe create all users??
            Users = new();
            _httpClientFactory = httpClientFactory;
            Logger = logger;
            CreateAllUsers();
        }

        #region Globals

        private ConcurrentDictionary<string, BaseValorantUser> Users { get; set; }

        private ILogger<BaseValorantProgram> Logger { get; set; }

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

                    Users.TryAdd(user.Val_puuid, new BaseValorantUser(user.Val_username, user.Val_tagname, user.Val_affinity, _httpClientFactory, Logger, user.Val_puuid));
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
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
                if (valorantUser == null || Users.ContainsKey(valorantUser.Val_puuid))
                {
                    return false;
                }

                Users.TryAdd(valorantUser.Val_puuid, new BaseValorantUser(valorantUser.Val_username, valorantUser.Val_tagname, valorantUser.Val_affinity, _httpClientFactory, Logger, valorantUser.Val_puuid));

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
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

        public bool UpdateMatchAllUsers(out ConcurrentDictionary<string, (MatchStats, Matches)> userMatchStats)
        {
            userMatchStats = new ConcurrentDictionary<string, (MatchStats, Matches)>();
            if (Users == null || !Users.Any())
            {
                return false;
            }

            HashSet<string> updatedUsers = new();
            var MatchStatsAndMMRHistories = GetAllUsersMatchStats().Result;
            Dictionary<string, Task<MatchJson?>> matchTasks = MatchStatsAndMMRHistories.Item1;
            Dictionary<string, MmrHistoryJson> matchHistories = MatchStatsAndMMRHistories.Item2;

            foreach (BaseValorantUser user in Users.Values)
            {
                if (user == null || updatedUsers.Contains(user.UserInfo.Val_puuid))
                {
                    continue;
                }
                try
                {
                    if (!matchTasks.ContainsKey(user.Puuid))
                    {
                        continue;
                    }
                    MatchJson? match = matchTasks[user.Puuid].Result;

                    if (match == null
                        || match.Metadata?.MatchId == null)
                    {
                        continue;
                    }

                    IEnumerable<BaseValorantUser> usersInMatch = CheckValorantUsersInMatch(match, updatedUsers);

                    foreach (BaseValorantUser userInMatch in usersInMatch)
                    {
                        if (user == null
                            || match == null
                            || MatchStatsExtension.MatchIdExistsForUser(match.Metadata.MatchId, userInMatch.UserInfo.Val_puuid)
                            // Just look at if match id does not exist for now.
                            //|| DateTime.UtcNow > DateTimeOffset.FromUnixTimeSeconds(match.Metadata.Game_Start).DateTime.ToUniversalTime().AddMinutes(30)
                            )
                        {
                            continue;
                        }

                        // this should be in a loop
                        //for (int i = 0; i < 5; i++)
                        //{

                        //}
                        MmrHistoryJson? mmrHistory = matchHistories.ContainsKey(userInMatch.Puuid) ? matchHistories[userInMatch.Puuid] : userInMatch.GetMatchMMR(match?.Metadata.MatchId);

                        if (CheckMatch(match, mmrHistory, userInMatch.UserInfo.Val_puuid, userMatchStats))
                        {
                            updatedUsers.Add(userInMatch.UserInfo.Val_puuid);
                            Logger.LogInformation($"Match stats updated for {userInMatch.UserInfo.Val_username}#{userInMatch.UserInfo.Val_tagname}. Match ID: {match.Metadata.MatchId}, Match Date: {match.Metadata.Game_Start_Patched.Safe()}");
                        }
                        else
                        {
                            Logger.LogInformation($"Match stats did not update for {userInMatch.UserInfo.Val_username}#{userInMatch.UserInfo.Val_tagname}.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error: {ex.Message} - when updating user {user.UserInfo.Val_username}");
                }
            }

            return true;
        }

        private static bool CheckMatch(MatchJson? match, MmrHistoryJson? MmrHistory, string puuid, ConcurrentDictionary<string, (MatchStats, Matches)> userMatchStats)
        {
            if (match == null
                || match.Metadata?.Mode == null
                || string.IsNullOrEmpty(puuid)
                )
            {
                return false;
            }

            MatchStats? matchStats = MatchStatsExtension.CreateFromJson(match, MmrHistory, puuid);

            if (matchStats == null)
            {
                return false;
            }

            Matches? matches = null;
            if (MatchesExtension.MatchIdExistsForUser(matchStats.Match_id))
            {
                matches = MatchesExtension.GetRow(matchStats.Match_id);
            }
            else
            {
                matches = MatchesExtension.CreateFromJson(match);

                if (matches == null)
                {
                    return false;
                }
                MatchesExtension.InsertRow(matches);
            }

            if (matches == null)
            {
                return false;
            }

            userMatchStats.TryAdd(puuid, (matchStats, matches));
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

            foreach (MatchPlayerJson matchPlayer in match.Players?.All_Players ?? Array.Empty<MatchPlayerJson>())
            {
                if (matchPlayer == null
                    || matchPlayer.Puuid == null
                    || updatedPuuids.Contains(matchPlayer.Puuid)
                    || !Users.ContainsKey(matchPlayer.Puuid)
                    )
                {
                    continue;
                }

                usersInMatch.Add(Users[matchPlayer.Puuid]);
            }

            return usersInMatch;
        }

        private async Task<(Dictionary<string, Task<MatchJson?>>, Dictionary<string, MmrHistoryJson>)> GetAllUsersMatchStats()
        {
            //Dictionary<string,MmrHistoryJson> mmrHistories = new Dictionary<string, MmrHistoryJson>();
            //Dictionary<string, Task<MmrHistoryJson?>> mmrHistoryTasks = new();
            //foreach (BaseValorantUser user in Users.Values)
            //{
            //    mmrHistoryTasks.Add(user.Puuid, Task.Run(() => user.GetLastMatchMMR()));
            //}

            //await Task.WhenAll(mmrHistoryTasks.Values.ToArray());

            //HashSet<string> usersInNewMatch = new HashSet<string>();

            //foreach(KeyValuePair<string, Task<MmrHistoryJson?>> mmrHistoryTask in mmrHistoryTasks)
            //{
            //    MmrHistoryJson? mmr = mmrHistoryTask.Value.Result;
            //    if (mmr == null 
            //        || mmr.Match_id == null 
            //        || MatchStatsExtension.MatchIdExistsForUser(mmr.Match_id, mmrHistoryTask.Key))
            //    {
            //        continue;
            //    }
            //    usersInNewMatch.Add(mmrHistoryTask.Key);
            //    mmrHistories.Add(mmrHistoryTask.Key, mmr);
            //}

            //Dictionary<string, Task<MatchJson?>> matchTasks = new();
            //foreach (BaseValorantUser user in Users.Values)
            //{
            //    if (!usersInNewMatch.Contains(user.Puuid))
            //    {
            //        continue;
            //    }
            //    matchTasks.Add(user.Puuid, Task.Run(() => user.GetLastMatch()));
            //}

            //await Task.WhenAll(matchTasks.Values.ToArray());
            //return (matchTasks, mmrHistories);
            Dictionary<string, Task<MatchJson?>> matchTasks = new();
            foreach (BaseValorantUser user in Users.Values)
            {
                matchTasks.Add(user.Puuid, Task.Run(() => user.GetLastMatch()));
            }

            await Task.WhenAll(matchTasks.Values.ToArray());
            return (matchTasks, new Dictionary<string, MmrHistoryJson>() );
        }

        #endregion

        #endregion
    }
}
