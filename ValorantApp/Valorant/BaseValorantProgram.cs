using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;
using ValorantApp.GenericExtensions;
using ValorantApp.HenrikJson;
using ValorantApp.Valorant.Enums;
using ValorantApp.Valorant.Helpers;

namespace ValorantApp.Valorant
{
    public class BaseValorantProgram
    {
        private static readonly object DbLock = new object();

        private readonly IHttpClientFactory _httpClientFactory;

        public BaseValorantProgram(IHttpClientFactory httpClientFactory, ILogger<BaseValorantProgram> logger)
        {
            Users = new();
            QueueUsers = new();
            _httpClientFactory = httpClientFactory;
            Logger = logger;
            ReloadFromDB();
        }

        #region Globals

        private ConcurrentDictionary<string, BaseValorantUser> Users { get; set; }

        private ILogger<BaseValorantProgram> Logger { get; set; }

        private Queue<string> QueueUsers { get; set; }

        private static readonly object lockObject = new();

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
            lock (lockObject)
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

        public async Task<bool> SendScheduledMessage(DiscordSocketClient client)
        {
            Logger.LogInformation("Starting Send of scheduled messages.");

            ConcurrentDictionary<string, BaseValorantMatch> usersMatchStats;
            UpdateMatchQueueUsers(out usersMatchStats);

            if (usersMatchStats == null || usersMatchStats.IsEmpty)
            {
                Logger.LogWarning($"{nameof(SendScheduledMessage)}: Could not find user match stats");
                return false;
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
                sortedByMatch.ForEach(x =>
                {
                    var channelIds = GetValorantUser(x.UserInfo.Val_puuid)?.ChannelIds;
                    if (channelIds != null)
                    {
                        foreach (var channelId in channelIds)
                        {
                            channelsToSend.Add(channelId);
                        }
                    }
                });
                
                foreach (var channelId in channelsToSend)
                {
                    if (client.GetChannelAsync(channelId).Result is not ISocketMessageChannel channel)
                    {
                        Logger.LogWarning($"{nameof(SendScheduledMessage)}: Could not find channel {channelId} for match id {matchid}");
                        continue;
                    }

                    List<BaseValorantMatch> sortedByMatchAndChannel = sortedByMatch.Where(x => GetValorantUser(x.UserInfo.Val_puuid)?.IsInChannel(channelId) ?? false).ToList();
                    sortedByMatchAndChannel.Sort((x, y) => y.MatchStats.Score.CompareTo(x.MatchStats.Score));

                    if (sortedByMatchAndChannel.Count == 0)
                    {
                        Logger.LogError($"{nameof(SendScheduledMessage)}: Found 0 match stats for match id - {matchid}. Should never get to this.");
                        continue;
                    }

                    if (sortedByMatchAndChannel.Count == 1)
                    {
                        await SendSingleMatch(sortedByMatchAndChannel.First(), channel);
                    }
                    else
                    {
                        await SendMultipleInMatch(sortedByMatchAndChannel, channel);
                    }
                }
            }

            await UpdateCurrentTierAllUsers([.. usersMatchStats.Values], client);

            return true;
        }

        public bool UpdateMatchQueueUsers(out ConcurrentDictionary<string, BaseValorantMatch> userMatchStats)
        {
            lock (lockObject)
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

        private async Task<bool> SendSingleMatch(BaseValorantMatch baseValorantMatch, ISocketMessageChannel channel)
        {
            if (baseValorantMatch == null || channel == null)
            {
                Logger.LogWarning($"{nameof(SendScheduledMessage)}: Match stats or channel is null, stopping send.");
                return false;
            }

            Logger.LogInformation($"{nameof(SendScheduledMessage)}: Single user in match");
            
            MatchStats stats = baseValorantMatch.MatchStats;
            Matches matches = baseValorantMatch.Matches;
            string userUpdated = $"<@{baseValorantMatch.UserInfo.Disc_id}>";
            string rounds = string.Equals(stats.Team, "blue", StringComparison.InvariantCultureIgnoreCase)
                ? $"{matches.Blue_Team_Rounds_Won ?? 0} : {matches.Red_Team_Rounds_Won ?? 0}"
                : $"{matches.Red_Team_Rounds_Won ?? 0} : {matches.Blue_Team_Rounds_Won ?? 0}";
            string averageRank = string.Equals(stats.Team, "blue", StringComparison.InvariantCultureIgnoreCase)
                ? $"<{((RankEmojis)(matches.Blue_Team_Average_Rank ?? 0)).Id()}> : <{((RankEmojis)(matches.Red_Team_Average_Rank ?? 0)).Id()}>"
                : $"<{((RankEmojis)(matches.Red_Team_Average_Rank ?? 0)).Id()}> : <{((RankEmojis)(matches.Blue_Team_Average_Rank ?? 0)).Id()}>";
            baseValorantMatch.LogMatch();

            EmbedFieldBuilder matchInfo = new EmbedFieldBuilder();
            matchInfo.Name = "__" + " ".Repeat(40) + "__" + "\n\nMatch Stats";
            matchInfo.Value = $"<t:{matches.Game_Start ?? 0}:f>, {Math.Floor(TimeSpan.FromSeconds(matches.Game_Length).TotalMinutes)} minutes\nRounds {rounds}\nAverage Ranks {averageRank}";

            EmbedBuilder embed = new EmbedBuilder()
                .WithThumbnailUrl($"{AgentsExtension.AgentFromString(stats.Character).ImageURLFromAgent()}")
                .WithAuthor
                (new EmbedAuthorBuilder
                {
                    Name = $"\n{ModesExtension.ModeFromString(matches.Mode.Safe().ToLower()).StringFromMode()} - {matches.Map}"
                }
                )
                .WithTitle($"{baseValorantMatch.UserInfo.Val_username} - {AgentsExtension.AgentFromString(stats.Character).StringFromAgent()} <{((RankEmojis)(stats.Current_Tier ?? 0)).Id()}> {(stats.MVP ? $" {MemeEmojisEnum.Sparkles.Id()}" : "")}")
                .AddField(matchInfo)
                .WithDescription($"Combat Score: {stats.Score / matches.Rounds_Played}, K/D/A: {stats.Kills}/{stats.Deaths}/{stats.Assists}\nHeadshot: {stats.Headshots:0.00}%, RR: {stats.Rr_change}");

            bool didTeamWin = string.Equals(stats.Team, "blue", StringComparison.InvariantCultureIgnoreCase)
                ? matches.Blue_Team_Win ?? false
                : !matches.Blue_Team_Win ?? false;
            embed.WithColor(matches.Blue_Team_Rounds_Won == matches.Red_Team_Rounds_Won ? Color.DarkerGrey : didTeamWin ? Color.Green : Color.Red);

            Logger.LogInformation($"{nameof(SendScheduledMessage)}: Successfully sending user data for {baseValorantMatch.UserInfo.Val_username}#{baseValorantMatch.UserInfo.Val_tagname}");
            await channel.SendMessageAsyncWrapper(userUpdated, embed: embed.Build());
            return true;
        }

        private async Task<bool> SendMultipleInMatch(List<BaseValorantMatch> baseValorantMatches, ISocketMessageChannel channel)
        {
            if (baseValorantMatches == null || channel == null)
            {
                Logger.LogWarning($"{nameof(SendScheduledMessage)}: Match stats or channel is null, stopping send.");
                return false;
            }

            Logger.LogInformation($"{nameof(SendScheduledMessage)}: Multiple users in match");
            
            string userUpdated = "";
            MatchStats setupMatchStats = baseValorantMatches.First().MatchStats;
            Matches setupMatches = baseValorantMatches.First().Matches;

            string rounds = string.Equals(setupMatchStats.Team, "blue", StringComparison.InvariantCultureIgnoreCase)
                ? $"{setupMatches.Blue_Team_Rounds_Won ?? 0} : {setupMatches.Red_Team_Rounds_Won ?? 0}"
                : $"{setupMatches.Red_Team_Rounds_Won ?? 0} : {setupMatches.Blue_Team_Rounds_Won ?? 0}";

            string averageRank = string.Equals(setupMatchStats.Team, "blue", StringComparison.InvariantCultureIgnoreCase)
                ? $"<{((RankEmojis)(setupMatches.Blue_Team_Average_Rank ?? 0)).Id()}> : <{((RankEmojis)(setupMatches.Red_Team_Average_Rank ?? 0)).Id()}>"
                : $"<{((RankEmojis)(setupMatches.Red_Team_Average_Rank ?? 0)).Id()}> : <{((RankEmojis)(setupMatches.Blue_Team_Average_Rank ?? 0)).Id()}>";

            EmbedBuilder embed = new EmbedBuilder()
                .WithThumbnailUrl(MapsExtension.MapFromString(setupMatches.Map.Safe()).ImageUrlFromMap())
                .WithAuthor
                (new EmbedAuthorBuilder
                {
                    Name = $"\n{ModesExtension.ModeFromString(setupMatches.Mode.Safe().ToLower()).StringFromMode()} - {setupMatches.Map.Safe()}"
                }
                );

            EmbedFieldBuilder matchInfo = new EmbedFieldBuilder();
            matchInfo.Name = "__" + " ".Repeat(40) + "__" + "\n\nMatch Stats";
            matchInfo.Value = $"<t:{setupMatches.Game_Start ?? 0}:f>, {Math.Floor(TimeSpan.FromSeconds(setupMatches.Game_Length).TotalMinutes)} minutes\nRounds {rounds}\nAverage Ranks {averageRank}";

            foreach (BaseValorantMatch match in baseValorantMatches)
            {
                userUpdated += $"<@{match.UserInfo.Disc_id}> ";

                match.LogMatch();

                EmbedFieldBuilder embedField = new EmbedFieldBuilder();

                MatchStats stats = match.MatchStats;
                Matches matches = match.Matches;

                embedField.Name = $"{match.UserInfo.Val_username} - {AgentsExtension.AgentFromString(stats.Character).StringFromAgent()} <{((RankEmojis)(stats.Current_Tier ?? 0)).Id()}> {(stats.MVP ? $" {MemeEmojisEnum.Sparkles.Id()}" : "")}";
                embedField.Value = $"Combat Score: {stats.Score / matches.Rounds_Played}, K/D/A: {stats.Kills}/{stats.Deaths}/{stats.Assists}\nHeadshot: {stats.Headshots:0.00}%, RR: {stats.Rr_change}";
                embed.AddField(embedField);
            }

            bool didTeamWin = string.Equals(setupMatchStats.Team, "blue", StringComparison.InvariantCultureIgnoreCase)
                ? setupMatches.Blue_Team_Win ?? false
                : !setupMatches.Blue_Team_Win ?? false;
            embed.WithColor(setupMatches.Blue_Team_Rounds_Won == setupMatches.Red_Team_Rounds_Won ? Color.DarkerGrey : didTeamWin ? Color.Green : Color.Red);
            embed.AddField(matchInfo);

            Logger.LogInformation($"{nameof(SendScheduledMessage)}: Successfully sending users data for match id {setupMatches.Match_Id}");
            await channel.SendMessageAsyncWrapper(userUpdated, embed: embed.Build());

            return true;
        }

        #endregion

        #region Check users

        /// <summary>
        /// Updates and send a message to all valorant users if their currentTier changed.
        /// </summary>
        /// <param name="matches"></param>
        /// <param name="channel"></param>
        public async Task<bool> UpdateCurrentTierAllUsers(ConcurrentBag<BaseValorantMatch> matches, DiscordSocketClient client)
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
                    IEnumerable<BaseValorantMatch> seasonMatchStats = valorantUser.GetBaseValorantMatch(EpisodeActExtension.GetEpisodeActInfosForDate(DateTime.UtcNow));
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
                    string averageHeadshots = seasonMatchStatsPreviousTier.Average(x => x.MatchStats.Headshots).ToString("0.##");
                    string averageBodyshots = seasonMatchStatsPreviousTier.Average(x => x.MatchStats.Bodyshots).ToString("0.##");
                    string clown = previousTier > currentTier ? $" {MemeEmojisEnum.Clown.Id()}" : $" {MemeEmojisEnum.Sunglasses.Id()}";

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
                        .WithDescription($"<{((RankEmojis)previousTier).Id()}> {arrowIcon} <{((RankEmojis)(valorantUser.CurrentTier ?? 0)).Id()}>")
                        .AddField($"{((RankEmojis)previousTier).ToDescriptionString()} Competitive Stats", $"Matches: {numberOfMatchesAtPreviousTier} Minutes: {numberOfMinutesAtPreviousTier}\nK/D/A: {kda}\nHeadshot: {averageHeadshots}% Bodyshot: {averageBodyshots}%");

                    HashSet<ulong> channelIds = valorantUser.ChannelIds ?? [];
                    foreach(ulong channelId in channelIds)
                    {
                        if (client.GetChannelAsync(channelId).Result is not ISocketMessageChannel channel)
                        {
                            Logger.LogWarning($"{nameof(UpdateCurrentTierAllUsers)}: Could not find channel {channelId}");
                            continue;
                        }
                        await channel.SendMessageAsyncWrapper(userUpdated, embed: embed.Build());
                    }
                }
            }

            return true;
        }

        #endregion Check users

        #endregion
    }
}
