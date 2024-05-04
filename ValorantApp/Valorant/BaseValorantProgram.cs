using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using ValorantApp.Database;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Repositories.Interfaces;
using ValorantApp.Database.Tables;
using ValorantApp.GenericExtensions;
using ValorantApp.HenrikJson;
using ValorantApp.Valorant.Enums;
using ValorantApp.Valorant.Helpers;

namespace ValorantApp.Valorant
{
    public class BaseValorantProgram
    {
        // TODO add a db lock.
        private static readonly object DbLock = new object();

        private readonly IHttpClientFactory _httpClientFactory;

        public BaseValorantProgram(
            IHttpClientFactory httpClientFactory
            , ILogger<BaseValorantProgram> logger
            , IMatchesRepository matchesRepository
            , IMatchStatsRepository matchStatsRepository
            , IValorantUsersRepository valorantUsersRepository
            )
        {
            // initialize users here to something? maybe create all users??
            Users = new();
            _httpClientFactory = httpClientFactory;
            Logger = logger;
            MatchesRepository = matchesRepository;
            MatchStatsRepository = matchStatsRepository;
            ValorantUsersRepository = valorantUsersRepository;
            CreateAllUsers();
        }

        #region Globals

        private ConcurrentDictionary<string, BaseValorantUser> Users { get; set; }

        private ILogger<BaseValorantProgram> Logger { get; set; }

        private IMatchesRepository MatchesRepository { get; set; }

        private IMatchStatsRepository MatchStatsRepository { get; set; }

        private IValorantUsersRepository ValorantUsersRepository { get; set; }
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

                    Users.TryAdd(user.Val_puuid, 
                        new BaseValorantUser(
                            user.Val_username
                            , user.Val_tagname
                            , user.Val_affinity
                            , _httpClientFactory
                            , Logger
                            , MatchesRepository
                            , MatchStatsRepository
                            , ValorantUsersRepository
                            , user.Val_puuid
                            )
                        );
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

                Users.TryAdd(valorantUser.Val_puuid,
                    new BaseValorantUser(
                        valorantUser.Val_username
                        , valorantUser.Val_tagname
                        , valorantUser.Val_affinity
                        , _httpClientFactory
                        , Logger
                        , MatchesRepository
                        , MatchStatsRepository
                        , ValorantUsersRepository
                        , valorantUser.Val_puuid
                        )
                    );

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

        public bool UpdateMatchAllUsers(out ConcurrentDictionary<string, BaseValorantMatch> userMatchStats)
        {
            userMatchStats = new ConcurrentDictionary<string, BaseValorantMatch>();
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

        private bool CheckMatch(MatchJson? match, MmrHistoryJson? MmrHistory, string puuid, ConcurrentDictionary<string, BaseValorantMatch> userMatchStats)
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

            userMatchStats.TryAdd(puuid, new BaseValorantMatch(matchStats, matches, Users[puuid].UserInfo, Logger));
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

        #region Check users

        /// <summary>
        /// Updates and send a message to all valorant users if their currentTier changed.
        /// TODO: When channel id is included in BaseValorantUsers, change param to include DiscordSocketClient instead of channel
        /// </summary>
        /// <param name="matches"></param>
        /// <param name="channel"></param>
        public void UpdateCurrentTierAllUsers(ConcurrentBag<BaseValorantMatch> matches, ISocketMessageChannel channel)
        {
            if (matches == null || matches.IsEmpty)
            {
                return;
            }

            foreach (BaseValorantMatch match in matches)
            {
                if (Users[match.UserInfo.Val_puuid].UpdateCurrentTier(match.MatchStats, match.Matches, out int previousTier))
                {
                    int currentTier = Users[match.UserInfo.Val_puuid].CurrentTier ?? 0;
                    IEnumerable<BaseValorantMatch> seasonMatchStats = Users[match.UserInfo.Val_puuid].GetBaseValorantMatch(EpisodeActExtension.GetEpisodeActInfosForDate(DateTime.UtcNow));
                    IEnumerable<BaseValorantMatch> seasonMatchStatsPreviousTier = seasonMatchStats.Where(x => x.MatchStats.Current_Tier?.Equals((byte)previousTier) ?? false);

                    string userUpdated = $"<@{match.UserInfo.Disc_id}>";
                    string arrowIcon = currentTier - previousTier == 2 ? ":arrow_double_up:"
                        : currentTier > previousTier
                            ? ":arrow_up:"
                            : ":arrow_down:";
                    int kills = seasonMatchStatsPreviousTier.Sum(x => x.MatchStats.Kills);
                    int deaths = seasonMatchStatsPreviousTier.Sum(x => x.MatchStats.Deaths);
                    int assists = seasonMatchStatsPreviousTier.Sum(x => x.MatchStats.Assists);
                    string numberOfMatchesAtPreviousTier = seasonMatchStatsPreviousTier.Count().ToString();
                    string numberOfMinutesAtPreviousTier = Math.Floor(TimeSpan.FromSeconds(seasonMatchStatsPreviousTier.Sum(x => x.Matches.Game_Length)).TotalMinutes).ToString();
                    string mostSelectedAgent = seasonMatchStatsPreviousTier
                        .GroupBy(match => match.MatchStats.Character) // Group matches by agent
                        .OrderByDescending(group => group.Count()) // Order groups by count in descending order
                        .FirstOrDefault()?.Key ?? "";
                    string kda = $"{kills}/{deaths}/{assists}";
                    string averageHeadshots = seasonMatchStatsPreviousTier.Average(x => x.MatchStats.Headshots).ToString("0.##");
                    string averageBodyshots = seasonMatchStatsPreviousTier.Average(x => x.MatchStats.Bodyshots).ToString("0.##");
                    string clown = previousTier > currentTier ? " :clown:" : " :sunglasses:";

                    Logger.LogInformation($@"{nameof(UpdateCurrentTierAllUsers)}: {match.UserInfo.Val_username}#{match.UserInfo.Val_tagname}
                        PreviousTier = {previousTier}
                        CurrentTier = {currentTier}
                        KDA = {kda}
                        MostSelectedAgent = {mostSelectedAgent}
                        NumberOfMatchesAtPreviousTier = {numberOfMatchesAtPreviousTier}
                        NumberOfMinutesAtPreviousTier = {numberOfMinutesAtPreviousTier}
                        AverageHeadshots = {averageHeadshots}
                        AverageBodyshots = {averageBodyshots}");




                    EmbedBuilder embed = new EmbedBuilder()
                        .WithThumbnailUrl($"{AgentsExtension.AgentFromString(mostSelectedAgent).ImageURLFromAgent()}")
                        .WithTitle($"{match.UserInfo.Val_username}#{match.UserInfo.Val_tagname}{clown}")
                        .WithDescription($"<{((RankEmojis)previousTier).EmojiIdFromEnum()}> {arrowIcon} <{((RankEmojis)(Users[match.UserInfo.Val_puuid].CurrentTier ?? 0)).EmojiIdFromEnum()}>")
                        .AddField($"{((RankEmojis)previousTier).ToDescriptionString()} Competitive Stats", $"Matches: {numberOfMatchesAtPreviousTier} Minutes: {numberOfMinutesAtPreviousTier}\nK/D/A: {kda}\nHeadshot: {averageHeadshots}% Bodyshot: {averageBodyshots}%");

                    channel.SendMessageAsync(userUpdated, embed: embed.Build());
                }
            }
        }

        public void UpdateDailyReportAllUsers(ISocketMessageChannel channel)
        {
            if (channel == null
                || Users.IsNullOrEmpty())
            {
                return;
            }

            DateTime utcNow = DateTime.UtcNow;
            DateTime utc24Hours = utcNow.AddHours(-24).AddSeconds(1);
            EmbedBuilder embed = new EmbedBuilder()
                //.WithThumbnailUrl($"{AgentsExtension.AgentFromString(mostSelectedAgent).ImageURLFromAgent()}")
                .WithTitle($"Daily Report")
                .WithDescription($"Summary of the match stats from the past 24 hours");

            Logger.LogInformation($"{nameof(UpdateDailyReportAllUsers)}: Starting daily report summary of {utc24Hours} - {utcNow}");

            List<IEnumerable<BaseValorantMatch>> usersMatches = new List<IEnumerable<BaseValorantMatch>>();

            foreach (BaseValorantUser user in Users.Values)
            {
                IEnumerable<BaseValorantMatch> dailyMatchStats = user.GetBaseValorantMatch(utc24Hours, utcNow);

                if (dailyMatchStats.IsNullOrEmpty())
                {
                    continue;
                }

                usersMatches.Add(dailyMatchStats);
            }

            if (usersMatches.IsNullOrEmpty())
            {
                return;
            }

            List<IEnumerable<BaseValorantMatch>> sortedUsersMatches = usersMatches.OrderByDescending(x => x.Count()).ToList();
            int highestMatches = sortedUsersMatches.First().Count();

            foreach (IEnumerable<BaseValorantMatch> userMatches in sortedUsersMatches)
            {
                ValorantUsers userInfo = userMatches.First().UserInfo;
                int kills = userMatches.Sum(x => x.MatchStats.Kills);
                int deaths = userMatches.Sum(x => x.MatchStats.Deaths);
                int assists = userMatches.Sum(x => x.MatchStats.Assists);
                string kda = $"{kills}/{deaths}/{assists}";
                string averageHeadshots = userMatches.Average(x => x.MatchStats.Headshots).ToString("0.##");
                string averageBodyshots = userMatches.Average(x => x.MatchStats.Bodyshots).ToString("0.##");
                string numberOfMatches = userMatches.Count().ToString();
                string numberOfMinutes = Math.Floor(TimeSpan.FromSeconds(userMatches.Sum(x => x.Matches.Game_Length)).TotalMinutes).ToString();
                string shouldTouchGrass = userMatches.Count() == highestMatches ? $"<{MemeEmojisEnum.TouchGrass.EnumToEmojiString()}>" : "";

                EmbedFieldBuilder embedField = new EmbedFieldBuilder();
                embedField.Name = $"{userInfo.Val_username}#{userInfo.Val_tagname}{shouldTouchGrass}";
                embedField.Value = $"Matches: {numberOfMatches} Minutes: {numberOfMinutes}\nK/D/A: {kda}\nHeadshot: {averageHeadshots}% Bodyshot: {averageBodyshots}%";
                embed.AddField(embedField);
            }

            channel.SendMessageAsync(embed: embed.Build());
        }

        #endregion Check users

        #endregion
    }
}
