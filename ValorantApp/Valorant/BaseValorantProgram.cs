using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;
using ValorantApp.GenericExtensions;
using ValorantApp.GenericUtils;
using ValorantApp.HenrikJson;
using ValorantApp.Valorant.Enums;
using ValorantApp.Valorant.Helpers;

namespace ValorantApp.Valorant
{
    public class BaseValorantProgram
    {
        private static readonly object DbLock = new object();

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly DiscordSocketClient _client;
        private readonly ILogger<BaseValorantProgram> Logger;

        public BaseValorantProgram(DiscordSocketClient client, IHttpClientFactory httpClientFactory, ILogger<BaseValorantProgram> logger)
        {
            Users = new();
            QueueUsers = new();
            _httpClientFactory = httpClientFactory;
            _client = client;
            Logger = logger;
            ReloadFromDB();

            ScheduledMessageTimer = new Timer(SendScheduledMessageTimer, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(0.5));
            DailyCheckTimer = new Timer(SendDailyCheckTimer, null, TimerUtils.TimeSpanUntilUTC(11, 0, 0), TimeSpan.FromDays(1));
        }

        #region Globals

        private ConcurrentDictionary<string, BaseValorantUser> Users { get; set; }

        private Queue<string> QueueUsers { get; set; }

        private static readonly object QueueUsersLock = new();

        private readonly Timer ScheduledMessageTimer;
        private readonly object SendMessageTimerLock = new();

        private readonly Timer DailyCheckTimer;

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

                    Users.TryAdd(user.Val_puuid, new BaseValorantUser(user, _httpClientFactory, Logger));
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
                return false;
            }
        }

        [Obsolete]
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

                Users.TryAdd(valorantUser.Val_puuid, new BaseValorantUser(valorantUser, _httpClientFactory, Logger));

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
                return false;
            }
        }

        #endregion

        #region Delete users

        public bool DeleteUser(string puuid)
        {
            BaseValorantUser? valorantUser = GetValorantUser(puuid);
            if (valorantUser == null)
            {
                return false;
            }

            valorantUser.DeleteUser();
            ReloadFromDB();
            return true;
        }

        #endregion Delete users

        #region Queue

        public void EnqueueAllUsers()
        {
            lock (QueueUsersLock)
            {
                QueueUsers.Clear();
                if (Users == null || Users.IsEmpty)
                {
                    return;
                }

                foreach (KeyValuePair<string, BaseValorantUser> user in Users)
                {
                    QueueUsers.Enqueue(user.Key);
                }
            }
        }

        #endregion Queue

        #region Reloads

        public void ReloadFromDB()
        {
            ReloadUsers();
            ReloadQueue();
        }

        private void ReloadUsers()
        {
            Users.Clear();
            CreateAllUsers();
        }

        private void ReloadQueue()
        {
            EnqueueAllUsers();
        }

        #endregion

        #region Check matches

        public async void SendScheduledMessageTimer(object? state)
        {
            StopScheduledMessageTimer();
            try
            {
                await SendScheduledMessage();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error: {nameof(SendScheduledMessageTimer)} - {ex.Message}");
            }
            finally
            {
                StartScheduledMessageTimer();
            }
        }

        private async Task SendScheduledMessage()
        {
            Logger.LogInformation("Starting Send of scheduled messages.");

            ConcurrentDictionary<string, BaseValorantMatch> usersMatchStats;
            UpdateMatchQueueUsers(out usersMatchStats);

            if (usersMatchStats == null || usersMatchStats.IsEmpty)
            {
                Logger.LogWarning($"{nameof(SendScheduledMessage)}: Could not find user match stats");
                return;
            }

            HashSet<string> matchIds = [];
            foreach (BaseValorantMatch match in usersMatchStats.Values)
            {
                matchIds.Add(match.Matches.Match_Id);
            }

            foreach (string matchid in matchIds)
            {
                List<BaseValorantMatch> sortedByMatch = usersMatchStats.Values.Where(x => x.Matches.Match_Id == matchid).ToList();

                HashSet<ulong> channelsToSend = [];
                sortedByMatch.ForEach(x => channelsToSend.UnionWith(GetValorantUser(x.UserInfo.Val_puuid)?.ChannelIds ?? []));
                
                foreach (var channelId in channelsToSend)
                {
                    List<BaseValorantMatch> sortedByMatchAndChannel = sortedByMatch.Where(x => GetValorantUser(x.UserInfo.Val_puuid)?.IsInChannel(channelId) ?? false).ToList();
                    sortedByMatchAndChannel.Sort((x, y) => y.MatchStats.Score.CompareTo(x.MatchStats.Score));

                    if (sortedByMatchAndChannel.Count == 0)
                    {
                        Logger.LogError($"{nameof(SendScheduledMessage)}: Found 0 match stats for match id - {matchid}. Should never get to this.");
                        continue;
                    }

                    if (sortedByMatchAndChannel.Count == 1)
                    {
                        await SendSingleMatch(sortedByMatchAndChannel.First(), channelId);
                    }
                    else
                    {
                        await SendMultipleInMatch(sortedByMatchAndChannel, channelId);
                    }
                }
            }

            await UpdateCurrentTierAllUsers([.. usersMatchStats.Values]);

            return;
        }

        public bool UpdateMatchQueueUsers(out ConcurrentDictionary<string, BaseValorantMatch> userMatchStats)
        {
            lock (QueueUsersLock)
            {
                userMatchStats = new ConcurrentDictionary<string, BaseValorantMatch>();
                if (Users.IsNullOrEmpty() || QueueUsers.IsNullOrEmpty())
                {
                    return false;
                }

                IEnumerable<string> queueUsers = QueueUsers.DequeueCount(15).ToList();
                if (queueUsers.IsNullOrEmpty())
                {
                    return false;
                }

                try
                {
                    var MatchStatsAndMMRHistories = GetQueueUsersMatchStats(queueUsers).Result;
                    // TODO: this will skip a users match if they have multiple entries
                    // an example of multiple entries - restarts app, 3 games have passed with different users.
                    Dictionary<string, Task<MatchJson?>> matchTasks = MatchStatsAndMMRHistories.Item1;
                    Dictionary<string, MmrHistoryJson> matchHistories = MatchStatsAndMMRHistories.Item2;

                    foreach (string userId in queueUsers)
                    {
                        BaseValorantUser? user = GetValorantUser(userId);
                        if (user == null)
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

                            IEnumerable<BaseValorantUser> usersInMatch = CheckValorantUsersInMatch(match, null);

                            foreach (BaseValorantUser userInMatch in usersInMatch)
                            {
                                if (user == null
                                    || match == null
                                    || MatchStatsExtension.MatchIdExistsForUser(match.Metadata.MatchId, userInMatch.UserInfo.Val_puuid)
                                    )
                                {
                                    continue;
                                }

                                MmrHistoryJson? mmrHistory = matchHistories.ContainsKey(userInMatch.Puuid) ? matchHistories[userInMatch.Puuid] : userInMatch.GetMatchMMR(match.Metadata.MatchId);

                                if (CheckMatch(match, mmrHistory, userInMatch.UserInfo.Val_puuid, userMatchStats))
                                {
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
                }
                catch (Exception e)
                {
                    Logger.LogError($"Error: {e.Message} - in {nameof(UpdateMatchQueueUsers)}");
                }
                finally
                {
                    foreach (string queueUser in queueUsers)
                    {
                        QueueUsers.Enqueue(queueUser);
                    }
                }
            }

            return true;
        }

        [Obsolete]
        public bool UpdateMatchAllUsers(out ConcurrentDictionary<string, BaseValorantMatch> userMatchStats)
        {
            userMatchStats = new ConcurrentDictionary<string, BaseValorantMatch>();
            if (Users == null || !Users.Any())
            {
                return false;
            }

            HashSet<string> updatedUsers = [];
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
                            )
                        {
                            continue;
                        }

                        MmrHistoryJson? mmrHistory = matchHistories.ContainsKey(userInMatch.Puuid) ? matchHistories[userInMatch.Puuid] : userInMatch.GetMatchMMR(match.Metadata.MatchId);

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

        private bool CheckMatch(MatchJson? match, MmrHistoryJson? mmrHistory, string puuid, ConcurrentDictionary<string, BaseValorantMatch> userMatchStats)
        {
            if (match == null
                || match.Metadata?.Mode == null
                || string.IsNullOrEmpty(puuid)
                || (ModesExtension.ModeFromString(match.Metadata?.Mode ?? "") == Modes.Competitive && mmrHistory == null)
                )
            {
                return false;
            }

            MatchStats? matchStats = MatchStatsExtension.CreateFromJson(match, mmrHistory, puuid);

            if (matchStats == null)
            {
                return false;
            }

            Matches? matches;
            if (MatchesExtension.MatchIdExists(matchStats.Match_id))
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

            // Check if the user is still in the program.
            BaseValorantUser? valorantUser = GetValorantUser(puuid);
            if (valorantUser == null)
            {
                return true;
            }

            userMatchStats.TryAdd(puuid, new BaseValorantMatch(matchStats, matches, valorantUser.UserInfo, Logger));
            MatchStatsExtension.InsertRow(matchStats);
            return true;
        }

        private IEnumerable<BaseValorantUser> CheckValorantUsersInMatch(MatchJson? match, HashSet<string>? updatedPuuids)
        {
            if (match == null)
            {
                return [];
            }

            List<BaseValorantUser> usersInMatch = new();
            if (updatedPuuids == null)
            {
                updatedPuuids = new();
            }

            foreach (MatchPlayerJson matchPlayer in match.Players?.All_Players ?? Array.Empty<MatchPlayerJson>())
            {
                
                if (matchPlayer == null
                    || matchPlayer.Puuid == null
                    || updatedPuuids.Contains(matchPlayer.Puuid)
                    )
                {
                    continue;
                }

                BaseValorantUser? valorantUser = GetValorantUser(matchPlayer.Puuid);
                if (valorantUser == null)
                {
                    continue;
                }

                usersInMatch.Add(valorantUser);
            }

            return usersInMatch;
        }

        private async Task<(Dictionary<string, Task<MatchJson?>>, Dictionary<string, MmrHistoryJson>)> GetQueueUsersMatchStats(IEnumerable<string> users)
        {
            Dictionary<string, Task<MatchJson?>> matchTasks = new();
            foreach (string userId in users)
            {
                BaseValorantUser? user = GetValorantUser(userId);
                if (user == null)
                {
                    continue;
                }

                matchTasks.Add(user.Puuid, Task.Run(user.GetLastMatch));
            }

            await Task.WhenAll(matchTasks.Values.ToArray());
            return (matchTasks, new Dictionary<string, MmrHistoryJson>());
        }

        [Obsolete]
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
                matchTasks.Add(user.Puuid, Task.Run(user.GetLastMatch));
            }

            await Task.WhenAll(matchTasks.Values.ToArray());
            return (matchTasks, new Dictionary<string, MmrHistoryJson>() );
        }

        private async Task<bool> SendSingleMatch(BaseValorantMatch baseValorantMatch, ulong channelId)
        {
            if (baseValorantMatch == null)
            {
                Logger.LogWarning($"{nameof(SendScheduledMessage)}: Match stats or channel is null, stopping send.");
                return false;
            }

            Logger.LogInformation($"{nameof(SendScheduledMessage)}: Single user in match");
            baseValorantMatch.LogMatch();
            
            string userUpdated = $"<@{baseValorantMatch.UserInfo.Disc_id}>";
            MatchStats stats = baseValorantMatch.MatchStats;
            Matches matches = baseValorantMatch.Matches;

            EmbedBuilder embed = new EmbedBuilder()
                .WithThumbnailUrl($"{AgentsExtension.AgentFromString(stats.Character).ImageURLFromAgent()}")
                .WithAuthor(
                    new EmbedAuthorBuilder
                    {
                        Name = $"\n{ModesExtension.ModeFromString(matches.Mode.Safe().ToLower()).StringFromMode()} - {matches.Map}"
                    }
                );

            SetUpPlayerField(embed, stats, matches, baseValorantMatch.UserInfo);
            SetUpMatchInfo(embed, stats, matches);

            Logger.LogInformation($"{nameof(SendScheduledMessage)}: Successfully sending user data for {baseValorantMatch.UserInfo.Val_username}#{baseValorantMatch.UserInfo.Val_tagname}");
            await DiscordExtensions.CheckChannelAndSendMessageAsync(_client, channelId, userUpdated, embed, Logger);

            return true;
        }

        private async Task<bool> SendMultipleInMatch(List<BaseValorantMatch> baseValorantMatches, ulong channelId)
        {
            if (baseValorantMatches == null)
            {
                Logger.LogWarning($"{nameof(SendScheduledMessage)}: Match stats or channel is null, stopping send.");
                return false;
            }

            Logger.LogInformation($"{nameof(SendScheduledMessage)}: Multiple users in match");
            
            string userUpdated = "";
            MatchStats setupMatchStats = baseValorantMatches.First().MatchStats;
            Matches setupMatches = baseValorantMatches.First().Matches;

            EmbedBuilder embed = new EmbedBuilder()
                .WithThumbnailUrl(MapsExtension.MapFromString(setupMatches.Map.Safe()).ImageUrlFromMap())
                .WithAuthor(
                    new EmbedAuthorBuilder
                    {
                        Name = $"\n{ModesExtension.ModeFromString(setupMatches.Mode.Safe().ToLower()).StringFromMode()} - {setupMatches.Map.Safe()}"
                    }
                );

            foreach (BaseValorantMatch match in baseValorantMatches)
            {
                userUpdated += $"<@{match.UserInfo.Disc_id}> ";

                match.LogMatch();

                SetUpPlayerField(embed, match.MatchStats, match.Matches, match.UserInfo);
            }

            SetUpMatchInfo(embed, setupMatchStats, setupMatches);

            Logger.LogInformation($"{nameof(SendScheduledMessage)}: Successfully sending users data for match id {setupMatches.Match_Id}");
            await DiscordExtensions.CheckChannelAndSendMessageAsync(_client, channelId, userUpdated, embed, Logger);

            return true;
        }

        private static void SetUpPlayerField(EmbedBuilder embed, MatchStats stats, Matches matches, ValorantUsers userInfo)
        {
            double legshots = 100.0 - (stats.Headshots + stats.Bodyshots);
            EmbedFieldBuilder embedField = new()
            {
                Name = $"{userInfo.Val_username} - {AgentsExtension.AgentFromString(stats.Character).StringFromAgent()} {((RankEmojis)(stats.Current_Tier ?? 0)).Id()}" +
                $"{(stats.MVP ? $" {MemeEmojisEnum.Sparkles.Id()}" : "")}" +
                $"{(legshots >= ValorantConstants.LEGSHOT_THRESHOLD_PERCENT ? $" {MemeEmojisEnum.ToeShooter.Id()}" : "")}",
                Value = $"Combat Score: {stats.Score / matches.Rounds_Played}, K/D/A: {stats.Kills}/{stats.Deaths}/{stats.Assists}\nHeadshot: {stats.Headshots:0.00}%, RR: {stats.Rr_change}"
            };
            embed.AddField(embedField);
        }

        private static void SetUpMatchInfo(EmbedBuilder embed, MatchStats matchStats, Matches matches)
        {
            string rounds = string.Equals(matchStats.Team, "blue", StringComparison.InvariantCultureIgnoreCase)
                ? $"{matches.Blue_Team_Rounds_Won ?? 0} : {matches.Red_Team_Rounds_Won ?? 0}"
                : $"{matches.Red_Team_Rounds_Won ?? 0} : {matches.Blue_Team_Rounds_Won ?? 0}";

            string averageRank = string.Equals(matchStats.Team, "blue", StringComparison.InvariantCultureIgnoreCase)
                ? $"{((RankEmojis)(matches.Blue_Team_Average_Rank ?? 0)).Id()} : {((RankEmojis)(matches.Red_Team_Average_Rank ?? 0)).Id()}"
                : $"{((RankEmojis)(matches.Red_Team_Average_Rank ?? 0)).Id()} : {((RankEmojis)(matches.Blue_Team_Average_Rank ?? 0)).Id()}";

            EmbedFieldBuilder matchInfo = new()
            {
                Name = "__" + " ".Repeat(40) + "__" + "\n\nMatch Stats",
                Value = $"<t:{matches.Game_Start ?? 0}:f>, {Math.Floor(TimeSpan.FromSeconds(matches.Game_Length).TotalMinutes)} minutes\nRounds {rounds}\nAverage Ranks {averageRank}"
            };
            embed.AddField(matchInfo);

            bool didTeamWin = string.Equals(matchStats.Team, "blue", StringComparison.InvariantCultureIgnoreCase)
                ? matches.Blue_Team_Win ?? false
                : !matches.Blue_Team_Win ?? false;
            embed.WithColor(matches.Blue_Team_Rounds_Won == matches.Red_Team_Rounds_Won ? Color.DarkerGrey : didTeamWin ? Color.Green : Color.Red);
        }

        #endregion

        #region Check users

        /// <summary>
        /// Updates and send a message to all valorant users if their currentTier changed.
        /// TODO: refactor this to use the setupReport function.
        /// </summary>
        /// <param name="matches"></param>
        /// <param name="channel"></param>
        public async Task<bool> UpdateCurrentTierAllUsers(ConcurrentBag<BaseValorantMatch> matches)
        {
            if (matches == null || matches.IsEmpty)
            {
                return false;
            }

            Logger.LogInformation($"Starting {nameof(UpdateCurrentTierAllUsers)}");

            foreach (BaseValorantMatch match in matches)
            {
                BaseValorantUser? valorantUser = GetValorantUser(match.UserInfo.Val_puuid);
                if (valorantUser == null)
                {
                    Logger.LogError($"{nameof(UpdateCurrentTierAllUsers)}: Could not find Valorant User {match.UserInfo.Val_username}#{match.UserInfo.Val_tagname}");
                    continue;
                }

                Logger.LogInformation($"{nameof(UpdateCurrentTierAllUsers)}: Valorant user {valorantUser.UserInfo.Val_username}#{valorantUser.UserInfo.Val_tagname} current tier = {valorantUser.CurrentTier ?? 0}, new tier = {match.MatchStats.New_Tier ?? 0}");

                if (valorantUser.UpdateCurrentTier(match.MatchStats, match.Matches, out int previousTier))
                {
                    int currentTier = valorantUser.CurrentTier ?? 0;
                    IEnumerable<BaseValorantMatch> seasonMatchStats = valorantUser.GetBaseValorantMatchBySeason(EpisodeActExtension.GetEpisodeActInfosForDate(DateTime.UtcNow));
                    IEnumerable<BaseValorantMatch> seasonMatchStatsPreviousTier = seasonMatchStats.Where(x => x.MatchStats.Current_Tier?.Equals((byte)previousTier) ?? false);

                    string userUpdated = $"<@{match.UserInfo.Disc_id}>";
                    string arrowIcon = currentTier - previousTier == 2 ? $"{MemeEmojisEnum.ArrowDoubleUp.Id()}"
                        : currentTier > previousTier
                            ? $"{MemeEmojisEnum.ArrowUp.Id()}"
                            : $"{MemeEmojisEnum.ArrowDown.Id()}";
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
                    double averageHeadshots = seasonMatchStatsPreviousTier.Average(x => x.MatchStats.Headshots);
                    double averageBodyshots = seasonMatchStatsPreviousTier.Average(x => x.MatchStats.Bodyshots);
                    double averageLegshots = 100.0 - (averageHeadshots + averageBodyshots);
                    string clown = previousTier > currentTier ? $" {MemeEmojisEnum.Clown.Id()}" : $" {MemeEmojisEnum.Sunglasses.Id()}";
                    string toeShooter = averageLegshots >= ValorantConstants.LEGSHOT_THRESHOLD_PERCENT ? $" {MemeEmojisEnum.ToeShooter.Id()}" : "";

                    Logger.LogInformation($@"{nameof(UpdateCurrentTierAllUsers)}: {match.UserInfo.Val_username}#{match.UserInfo.Val_tagname}
                        PreviousTier = {previousTier}
                        CurrentTier = {currentTier}
                        KDA = {kda}
                        MostSelectedAgent = {mostSelectedAgent}
                        NumberOfMatchesAtPreviousTier = {numberOfMatchesAtPreviousTier}
                        NumberOfMinutesAtPreviousTier = {numberOfMinutesAtPreviousTier}
                        AverageHeadshots = {averageHeadshots:0.##}
                        AverageBodyshots = {averageBodyshots:0.##}");

                    EmbedBuilder embed = new EmbedBuilder()
                        .WithThumbnailUrl($"{AgentsExtension.AgentFromString(mostSelectedAgent).ImageURLFromAgent()}")
                        .WithTitle($"{match.UserInfo.Val_username}#{match.UserInfo.Val_tagname}{clown}{toeShooter}")
                        .WithDescription($"{((RankEmojis)previousTier).Id()} {arrowIcon} {((RankEmojis)(valorantUser.CurrentTier ?? 0)).Id()}")
                        .AddField($"{((RankEmojis)previousTier).ToDescriptionString()} Competitive Stats", $"Matches: {numberOfMatchesAtPreviousTier} | Minutes: {numberOfMinutesAtPreviousTier}\nK/D/A: {kda}\nHeadshot: {averageHeadshots}% | Bodyshot: {averageBodyshots}%");

                    HashSet<ulong> channelIds = valorantUser.ChannelIds ?? [];
                    foreach (ulong channelId in channelIds)
                    {
                        await DiscordExtensions.CheckChannelAndSendMessageAsync(_client, channelId, userUpdated, embed, Logger);
                    }
                }
            }

            return true;
        }

        #endregion Check users

        #region Daily Check

        /// <summary>
        /// Daily timer function.
        /// </summary>
        /// <param name="state"></param>
        public async void SendDailyCheckTimer(object? state)
        {
            try
            {
                EpisodeActInfos? episodeActInfos = EpisodeActExtension.GetEpisodeActInfosForEndDate(DateTime.UtcNow);
                if (episodeActInfos == null)
                {
                    await SendDailyReport();
                }
                else
                {
                    await SendEpisodeActInfoReport(episodeActInfos);
                }

                await UpdateDiscordBotConfig();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error: {nameof(SendDailyCheckTimer)} - {ex.Message}");
            }
        }

        private async Task SendDailyReport()
        {
            DateTime now = DateTime.UtcNow;
            DateTime endDate = new(now.Year, now.Month, now.Day, 11, 0, 0, DateTimeKind.Utc);
            DateTime startDate = endDate.AddDays(-1).AddSeconds(1);

            Logger.LogInformation($"Starting {nameof(SendDailyReport)}: {startDate} - {endDate} UTC");

            Dictionary<string, EmbedFieldBuilder> embedFieldBuildersPuuid = SetupReport(startDate, endDate);

            await SendReport("Daily Report Summary", "Let's see who played today..", embedFieldBuildersPuuid);
        }

        private async Task SendEpisodeActInfoReport(EpisodeActInfos episodeActInfos)
        {
            Logger.LogInformation($"Starting {nameof(SendEpisodeActInfoReport)}: {episodeActInfos}");

            Dictionary<string, EmbedFieldBuilder> embedFieldBuildersPuuid = SetupReport(episodeActInfos.StartDate, episodeActInfos.EndDate);

            await SendReport($"{episodeActInfos} Report Summary", "Congratulations on another Episode/Act finished!\nGood luck on the next split!", embedFieldBuildersPuuid);
        }

        /// <summary>
        /// Set up report fields for every user in a specified time period
        /// </summary>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        private Dictionary<string, EmbedFieldBuilder> SetupReport(DateTime startDate, DateTime endDate)
        {
            IEnumerable<string> userPuuids = Users.Keys;
            Dictionary<string, EmbedFieldBuilder> embedFieldBuildersPuuid = [];

            foreach (string puuid in userPuuids)
            {
                BaseValorantUser? user = GetValorantUser(puuid);
                if (user == null)
                {
                    continue;
                }

                IEnumerable<BaseValorantMatch> seasonMatchStats = user.GetBaseValorantMatch(startDate, endDate);
                if (seasonMatchStats.IsNullOrEmpty())
                {
                    continue;
                }

                IEnumerable<BaseValorantMatch> sortedSeasonMatchStats = seasonMatchStats
                    .OrderBy(matchStat => matchStat.Matches.Game_Start_Patched_UTC == null)
                    .ThenBy(matchStat => matchStat.Matches.Game_Start_Patched_UTC);

                // Field properties
                int startingTier = sortedSeasonMatchStats.FirstOrDefault(x => (x.MatchStats.Current_Tier ?? 0) != 0)?.MatchStats.Current_Tier ?? 0;
                int endTier = sortedSeasonMatchStats.LastOrDefault(x => (x.MatchStats.New_Tier ?? 0) != 0)?.MatchStats?.Current_Tier ?? 0;
                int rrChange = sortedSeasonMatchStats.Sum(x => x.MatchStats.Rr_change);
                int kills = sortedSeasonMatchStats.Sum(x => x.MatchStats.Kills);
                int deaths = sortedSeasonMatchStats.Sum(x => x.MatchStats.Deaths);
                int assists = sortedSeasonMatchStats.Sum(x => x.MatchStats.Assists);
                int aces = sortedSeasonMatchStats.Sum(x => x.MatchStats.Aces);
                int numMatches = sortedSeasonMatchStats.Count();
                int numMinutes = (int)Math.Floor(TimeSpan.FromSeconds(sortedSeasonMatchStats.Sum(x => x.Matches.Game_Length)).TotalMinutes);
                string mostSelectedAgent = sortedSeasonMatchStats
                        .GroupBy(match => match.MatchStats.Character) // Group matches by agent
                        .OrderByDescending(group => group.Count()) // Order groups by count in descending order
                        .FirstOrDefault()?.Key ?? string.Empty;
                string kda = $"{kills}/{deaths}/{assists}";
                double averageHeadshots = sortedSeasonMatchStats.Average(x => x.MatchStats.Headshots);
                double averageBodyshots = sortedSeasonMatchStats.Average(x => x.MatchStats.Bodyshots);
                double averageLegshots = 100.0 - (averageHeadshots + averageBodyshots);
                string touchGrass = numMinutes/(endDate - startDate).TotalMinutes * 100 > ValorantConstants.TOUCH_GRASS_THRESHOLD_PERCENT ? $" {MemeEmojisEnum.TouchGrass.Id()}" : string.Empty;
                string toeShooter = averageLegshots >= ValorantConstants.LEGSHOT_THRESHOLD_PERCENT ? $" {MemeEmojisEnum.ToeShooter.Id()}" : string.Empty;

                EmbedFieldBuilder field = new()
                {
                    Name = $"{user.UserInfo.Val_username}#{user.UserInfo.Val_tagname}{touchGrass}{toeShooter}" +
                    $" {((RankEmojis)startingTier).Id()} {MemeEmojisEnum.ArrowRight.Id()} {((RankEmojis)endTier).Id()}" +
                    $" {(rrChange >= 0 ? "+" : string.Empty)}{rrChange} RR",
                    Value = $"Matches: {numMatches} | Minutes: {numMinutes} | Aces: {aces}" +
                    $"\nK/D/A: {kda} | Most Played: {mostSelectedAgent}" +
                    $"\nHeadshot: {averageHeadshots:0.##}% | Bodyshot: {averageBodyshots:0.##}%"
                };

                embedFieldBuildersPuuid.Add(puuid, field);
            }

            return embedFieldBuildersPuuid;
        }

        /// <summary>
        /// Sends a message to all users
        /// Embeds cap at 25 fields. If there are more than 20, create a new embed (or page with pagination)
        /// TODO: Add pagination here
        /// </summary>
        private async Task SendReport(string title, string description, Dictionary<string, EmbedFieldBuilder> embedFieldBuildersPuuid)
        {
            HashSet<ulong> channelsToSend = [];
            embedFieldBuildersPuuid.Keys.ToList().ForEach(x => channelsToSend.UnionWith(GetValorantUser(x)?.ChannelIds ?? Enumerable.Empty<ulong>()));

            foreach (ulong channelId in channelsToSend)
            {
                List<EmbedFieldBuilder> fieldByChannels = embedFieldBuildersPuuid
                    .Where(x => GetValorantUser(x.Key)?.IsInChannel(channelId) ?? false)
                    .Select(x => x.Value)
                    .ToList();

                int fieldCount = 0;
                int pageNumber = 1;
                EmbedBuilder embed = new EmbedBuilder()
                    .WithTitle($"{title} - Page {pageNumber}")
                    .WithDescription(description)
                    .WithColor(Color.DarkBlue);

                foreach (EmbedFieldBuilder field in fieldByChannels)
                {
                    embed.AddField(field);
                    fieldCount++;

                    // Check if we have reached 20 fields
                    if (fieldCount % 20 == 0 || fieldCount == fieldByChannels.Count)
                    {
                        await DiscordExtensions.CheckChannelAndSendMessageAsync(_client, channelId, null, embed, Logger);

                        // Re-initialize the embed for the next page if there are more fields
                        if (fieldCount < fieldByChannels.Count)
                        {
                            pageNumber++;
                            embed = new EmbedBuilder()
                                .WithTitle($"{title} - Page {pageNumber}")
                                .WithDescription(description)
                                .WithColor(Color.DarkBlue);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Update discord config.
        /// This should contain any statuses of the bot that are variable.
        /// </summary>
        /// <returns></returns>
        public async Task UpdateDiscordBotConfig()
        {
            int serverCount = _client.Guilds.Count;
            await _client.SetGameAsync($"Valorant | {serverCount} Server{(serverCount == 1 ? 's' : string.Empty)}", type: ActivityType.Watching);
        }

        #endregion Daily Check

        #region Timer Utils

        private void StopScheduledMessageTimer()
        {
            lock (SendMessageTimerLock)
            {
                ScheduledMessageTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        private void StartScheduledMessageTimer()
        {
            lock (SendMessageTimerLock)
            {
                ScheduledMessageTimer.Change(TimeSpan.FromMinutes(0.5), TimeSpan.FromMinutes(0.5));
            }
        }

        #endregion Timer Utils

        #endregion
    }
}
