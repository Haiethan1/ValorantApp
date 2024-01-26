using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Configuration;
using System.Linq;
using System.Reflection;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;
using ValorantApp.Valorant;
using ValorantApp.Valorant.Enums;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ValorantApp
{
    public class ValorantApp
    {
        private DiscordSocketClient _client;
        private CommandService _commands;
        private IServiceProvider _servicesProvider;
        private Timer _timer;
        private readonly BaseValorantProgram _program;
        private ulong _channelToMessage;
        private readonly ILogger<ValorantApp> _logger;

        static void Main(string[] args)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["Database"].ConnectionString;
            ITable.CreateTables(connectionString);

            var discordSocketConfig = new DiscordSocketConfig()
            {
                // Other config options can be presented here.
                GatewayIntents = GatewayIntents.All
            };

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Conditional(
                    x => !x.Properties.ContainsKey("ApiLog"),
                    y => y.File(
                        "logs.txt",
                        rollOnFileSizeLimit: true,
                        fileSizeLimitBytes: 10485760, // 10 MB
                        retainedFileCountLimit: 5
                    )
                )
                .WriteTo.Conditional(
                    x => x.Properties.ContainsKey("ApiLog"),
                    y => y.File(
                        "apilogs.txt",
                        rollOnFileSizeLimit: true,
                        fileSizeLimitBytes: 10485760, // 10 MB
                        retainedFileCountLimit: 5
                    )
                )
                .CreateLogger();

            var services = new ServiceCollection();
            services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));
            services.AddSingleton<BaseValorantProgram>();
            services.AddSingleton(new DiscordSocketClient(discordSocketConfig));
            services.AddSingleton<CommandService>();
            services.AddSingleton<ValorantApp>();
            
            //services.AddLogging()

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            ValorantApp program = serviceProvider.GetRequiredService<ValorantApp>();

            program.RunBotAsync().GetAwaiter().GetResult();
        }

        public ValorantApp(BaseValorantProgram program, DiscordSocketClient client, CommandService commands, IServiceProvider servicesProvider, ILogger<ValorantApp> logger)
        {
            _program = program;
            _servicesProvider = servicesProvider;
            _channelToMessage = ulong.Parse(ConfigurationManager.AppSettings["ChannelToMessage"] ?? "");
            _commands = commands;
            _client = client;
            _logger = logger;

            _logger.LogInformation("Starting ValorantApp");
        }

        public async Task RunBotAsync()
        {
            var token = ConfigurationManager.AppSettings["BotToken"];
            
            _client.Log += LogAsync;

            await RegisterCommandsAsync();

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            _client.Ready += ReadyAsync;

            _logger.LogInformation("Starting timed messages");
            _timer = new Timer(SendScheduledMessage, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(0.5));

            // Block the program until it is closed
            await Task.Delay(-1);
        }

        private Task LogAsync(LogMessage log)
        {
            _logger.LogInformation($"Discord LogAsync {log}");
            return Task.CompletedTask;
        }

        private Task ReadyAsync()
        {
            _logger.LogInformation($"{_client.CurrentUser.Username} is connected!");
            return Task.CompletedTask;
        }

        public async Task RegisterCommandsAsync()
        {
            _client.MessageReceived += HandleCommandAsync;

            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _servicesProvider);
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            if (!(messageParam is SocketUserMessage message)) return;
            if (message.Author.IsBot) return;

            int argPos = 0;
            if (message.HasStringPrefix("!", ref argPos))
            {
                _logger.LogInformation($"Starting discord command async. Command {message.Content}");
                var context = new SocketCommandContext(_client, message);

                var result = await _commands.ExecuteAsync(context, argPos, _servicesProvider);
                if (!result.IsSuccess)
                {
                    await context.Channel.SendMessageAsync($"Error: {result.ErrorReason}");
                }
            }
        }

        private async void SendScheduledMessage(object? state)
        {
            // TODO add a lock for running this. don't want this to run multiple threads.
            // probs want to move this to base valorant program???
            // will need to send in discord bot information.

            // this should resolve the timer
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            try
            {
                _logger.LogInformation("Starting Send of scheduled messages.");
                ISocketMessageChannel? channel = (ISocketMessageChannel)_client.GetChannel(_channelToMessage);
                if (channel == null)
                {
                    _logger.LogWarning($"{nameof(SendScheduledMessage)}: Could not find channel {_channelToMessage}");
                    return;
                }

                Dictionary<string, MatchStats> usersMatchStats;
                _program.UpdateMatchAllUsers(out usersMatchStats);
                
                if (usersMatchStats == null || usersMatchStats.Count == 0)
                {
                    _logger.LogWarning($"{nameof(SendScheduledMessage)}: Could not find user match stats");
                    return;
                }

                HashSet<string> matchIds = new HashSet<string>();
                foreach (MatchStats matchStats in usersMatchStats.Values)
                {
                    matchIds.Add(matchStats.Match_id);
                }

                foreach (string matchid in matchIds)
                {
                    List<KeyValuePair<string, MatchStats>> sortedList = usersMatchStats.Where(x => x.Value.Match_id == matchid).ToList();
                    sortedList.Sort((x, y) => y.Value.Score.CompareTo(x.Value.Score));

                    if (sortedList.Count == 0)
                    {
                        _logger.LogError($"{nameof(SendScheduledMessage)}: Found 0 match stats for match id - {matchid}");
                        continue;
                    }

                    if (sortedList.Count == 1)
                    {
                        _logger.LogInformation($"{nameof(SendScheduledMessage)}: Single user in match");
                        KeyValuePair<string, MatchStats> match = sortedList.First();
                        MatchStats stats = match.Value;

                        BaseValorantUser? user = _program.GetValorantUser(match.Key);
                        if (user == null)
                        {
                            _logger.LogError($"{nameof(SendScheduledMessage)}: BaseValorantUser not found after finding match stats - {match.Key}");
                            continue;
                        }

                        string userUpdated = $"<@{user.UserInfo.Disc_id}>";

                        _logger.LogInformation($@"{nameof(SendScheduledMessage)}: Setting up all match data for single user {user.UserInfo.Val_username}#{user.UserInfo.Val_tagname} - 
                            Agent = {AgentsExtension.AgentFromString(stats.Character).StringFromAgent()},
                            Map = {MapsExtension.MapFromString(stats.Map).StringFromMap()}, 
                            Game_Start_patched = {stats.Game_Start_Patched?.ToString("MMM. d\\t\\h, h:mm tt")},
                            Game_Length.TotalMinutes = {TimeSpan.FromSeconds(stats.Game_Length).TotalMinutes},
                            Mode = {ModesExtension.ModeFromString(stats.Mode.ToLower()).StringFromMode()},
                            Score / Rounds = {stats.Score} / {stats.Rounds},
                            K/D/A = {stats.Kills}/{stats.Deaths}/{stats.Assists},
                            MVP = {stats.MVP},
                            Headshot = {stats.Headshots:0.00}%,
                            RR = {stats.Rr_change}");

                        EmbedBuilder embed = new EmbedBuilder()
                            .WithThumbnailUrl($"{AgentsExtension.AgentFromString(stats.Character).ImageURLFromAgent()}")
                            .WithAuthor
                            (new EmbedAuthorBuilder
                            {
                                Name = $"{stats.Game_Start_Patched?.ToString("MMM. d\\t\\h, h:mm tt")}, {(int)TimeSpan.FromSeconds(stats.Game_Length).TotalMinutes}:{TimeSpan.FromSeconds(stats.Game_Length).Seconds:00} minutes\n{ModesExtension.ModeFromString(stats.Mode.ToLower()).StringFromMode()} - {stats.Map}"
                            }
                            )
                            .WithTitle($"{user.UserInfo.Val_username} - {AgentsExtension.AgentFromString(stats.Character).StringFromAgent()}{(stats.MVP ? " :sparkles:" : "")}")
                            .WithDescription($"Combat Score: {stats.Score/stats.Rounds}, K/D/A: {stats.Kills}/{stats.Deaths}/{stats.Assists}\nHeadshot: {stats.Headshots:0.00}%, RR: {stats.Rr_change}")
                            .WithColor(stats.Rr_change >= 0 && stats.Rr_change < 5 ? Color.DarkerGrey : stats.Rr_change > 0 ? Color.Green : Color.Red);

                        if (channel != null)
                        {
                            _logger.LogInformation($"{nameof(SendScheduledMessage)}: Successfully sending user data for {user.UserInfo.Val_username}#{user.UserInfo.Val_tagname}");
                            await channel.SendMessageAsync(userUpdated, embed: embed.Build());
                        }
                    }
                    else
                    {
                        string userUpdated = "";
                        int rrChange = 0;
                        MatchStats setupMatchStats = sortedList.First().Value;

                        _logger.LogInformation($"{nameof(SendScheduledMessage)}: Multiple users in match");
                        _logger.LogInformation($@"{nameof(SendScheduledMessage)}: Setting up base match data. - 
                            Map = {MapsExtension.MapFromString(setupMatchStats.Map).StringFromMap()}, 
                            Game_Start_patched = {setupMatchStats.Game_Start_Patched?.ToString("MMM. d\\t\\h, h:mm tt")},
                            Game_Length.TotalMinutes = {TimeSpan.FromSeconds(setupMatchStats.Game_Length).TotalMinutes},
                            Mode = {ModesExtension.ModeFromString(setupMatchStats.Mode.ToLower()).StringFromMode()}");

                        EmbedBuilder embed = new EmbedBuilder()
                            .WithThumbnailUrl(MapsExtension.MapFromString(setupMatchStats.Map).ImageUrlFromMap())
                            .WithAuthor
                            (new EmbedAuthorBuilder
                            {
                                Name = $"{setupMatchStats.Game_Start_Patched?.ToString("MMM. d\\t\\h, h:mm tt")}, {TimeSpan.FromSeconds(setupMatchStats.Game_Length).TotalMinutes} minutes\n{ModesExtension.ModeFromString(setupMatchStats.Mode.ToLower()).StringFromMode()} - {setupMatchStats.Map}"
                            }
                            );

                        foreach (KeyValuePair<string, MatchStats> matchStats in sortedList)
                        {
                            BaseValorantUser? user = _program.GetValorantUser(matchStats.Key);
                            if (user == null)
                            {
                                _logger.LogWarning($"{nameof(SendScheduledMessage)}: BaseValorantUser not found after finding match stats - {matchStats.Key}");
                                continue;
                            }

                            userUpdated += $"<@{user.UserInfo.Disc_id}> ";

                            EmbedFieldBuilder embedField = new EmbedFieldBuilder();

                            MatchStats stats = matchStats.Value;

                            _logger.LogInformation($@"{nameof(SendScheduledMessage)}: Setting up match data for single user {user.UserInfo.Val_username}#{user.UserInfo.Val_tagname} - 
                                Score / Rounds = {stats.Score} / {stats.Rounds},
                                K/D/A = {stats.Kills}/{stats.Deaths}/{stats.Assists},
                                MVP = {stats.MVP},
                                Headshot = {stats.Headshots:0.00}%,
                                RR = {stats.Rr_change}");

                            embedField.Name = $"{user.UserInfo.Val_username} - {AgentsExtension.AgentFromString(stats.Character).StringFromAgent()}{(stats.MVP ? " :sparkles:" : "")}";
                            embedField.Value = $"Combat Score: {stats.Score / stats.Rounds}, K/D/A: {stats.Kills}/{stats.Deaths}/{stats.Assists}\nHeadshot: {stats.Headshots:0.00}%, RR: {stats.Rr_change}";
                            embed.AddField(embedField);
                            rrChange += stats.Rr_change;
                        }

                        rrChange = rrChange / sortedList.Count;
                        embed.WithColor(rrChange >= 0 && rrChange < 5 ? Color.DarkerGrey : rrChange > 0 ? Color.Green : Color.Red);

                        if (channel != null && !string.IsNullOrEmpty(userUpdated))
                        {
                            _logger.LogInformation($"{nameof(SendScheduledMessage)}: Successfully sending users data for match id {matchid}");
                            await channel.SendMessageAsync(userUpdated, embed: embed.Build());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {nameof(SendScheduledMessage)} - {ex.Message}");
            }
            finally
            {
                _timer.Change(TimeSpan.FromMinutes(0.5), TimeSpan.FromMinutes(0.5));
            }
        }
    }
}