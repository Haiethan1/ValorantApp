using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using System.Collections.Concurrent;
using System.Configuration;
using System.Reflection;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;
using ValorantApp.GenericExtensions;
using ValorantApp.Valorant;
using ValorantApp.Valorant.Enums;

namespace ValorantApp
{
    public class ValorantApp
    {
        private DiscordSocketClient _client;
        private CommandService _commands;
        private InteractionService _interactions;
        private IServiceProvider _servicesProvider;
        private Timer _timer;
        private readonly object timerLock = new object();
        private bool timerIsRunning;
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
            services.AddHttpClient("HenrikApiClient", client =>
            {
                client.BaseAddress = new Uri("https://api.henrikdev.xyz/valorant/");
                client.DefaultRequestHeaders.Add("Authorization", ConfigurationManager.AppSettings["HenrikToken"]);
            });
            services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));
            services.UseMinimalHttpLogger();
            services.AddSingleton<BaseValorantProgram>();
            services.AddSingleton(new DiscordSocketClient(discordSocketConfig));
            services.AddSingleton<CommandService>();
            services.AddSingleton<InteractionService>();
            services.AddSingleton<ValorantApp>();
            
            //services.AddLogging()

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            ValorantApp program = serviceProvider.GetRequiredService<ValorantApp>();

            program.RunBotAsync().GetAwaiter().GetResult();
        }

        public ValorantApp(BaseValorantProgram program, DiscordSocketClient client, CommandService commands, InteractionService interaction, IServiceProvider servicesProvider, ILogger<ValorantApp> logger)
        {
            _program = program;
            _servicesProvider = servicesProvider;
            _channelToMessage = ulong.Parse(ConfigurationManager.AppSettings["ChannelToMessage"] ?? "");
            _commands = commands;
            _interactions = interaction;
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
            timerIsRunning = true;

            // Block the program until it is closed
            await Task.Delay(-1);
        }

        private Task LogAsync(LogMessage log)
        {
            _logger.LogInformation($"Discord LogAsync {log}");
            return Task.CompletedTask;
        }

        private async Task ReadyAsync()
        {
            _logger.LogInformation($"{_client.CurrentUser.Username} is connected!");

            // Let's do our global command
            List<SlashCommandBuilder> globalCommandList = new()
            {
                new SlashCommandBuilder()
                    .WithName("mmr")
                    .WithDescription("Get user's Valorant MMR")
                    .AddOption("username", ApplicationCommandOptionType.User, "The username of the user to get MMR for", isRequired: false),
                new SlashCommandBuilder()
                    .WithName("addme")
                    .WithDescription("Add your Valorant account to the bot")
                    .AddOption("username", ApplicationCommandOptionType.String, "Your Valorant Riot ID", isRequired: true)
                    .AddOption("tagname", ApplicationCommandOptionType.String, "Your Valorant Riot tag", isRequired: true),
            };

            //var commands = await _client.GetGlobalApplicationCommandsAsync();
            //List<SlashCommandBuilder> newGlobalCommands = new();
            //foreach (SlashCommandBuilder scb in globalCommandList)
            //{
            //    _logger.LogInformation($"Checking command {scb.Name}");
            //    if (commands.Any(cmd => cmd.Name == scb.Name))
            //    {
            //        _logger.LogInformation($"Adding command {scb.Name}");
            //        newGlobalCommands.Add(scb);
            //    }
            //}


            try
            {
                // With global commands we don't need the guild.
                await _client.BulkOverwriteGlobalApplicationCommandsAsync(globalCommandList.Select(gcmd => gcmd.Build() as ApplicationCommandProperties).ToArray());
                // Using the ready event is a simple implementation for the sake of the example. Suitable for testing and development.
                // For a production bot, it is recommended to only run the CreateGlobalApplicationCommandAsync() once for each command.
            }
            catch (HttpException exception)
            {
                // If our command was invalid, we should catch an ApplicationCommandException. This exception contains the path of the error as well as the error message.
                // You can serialize the Error field in the exception to get a visual of where your error is.
                string json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);

                // You can send this error somewhere or just print it to the console, for this example we're just going to print it.
                _logger.LogError(json);
            }
            //return Task.CompletedTask;
        }

        public async Task RegisterCommandsAsync()
        {
            _client.MessageReceived += HandleCommandAsync;
            _client.SlashCommandExecuted += HandleInteractionAsync;

            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _servicesProvider);
            await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _servicesProvider);
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

        private async Task HandleInteractionAsync(SocketInteraction arg)
        {
            try
            {
                var temp = new SocketInteractionContext(_client, arg);
                await _interactions.ExecuteCommandAsync(temp, _servicesProvider);
            }
            catch (Exception ex)
            {
                await arg.RespondAsync($"Error when executing command {arg.Data}");
                _logger.LogError($"HandleInteractionAsync exception: {ex}");
            }
        }

        // TODO investigate moving most of this out of here and into BaseValorantProgram
        public async void SendScheduledMessage(object? state)
        {
            // TODO add a lock for running this. don't want this to run multiple threads.
            // probs want to move this to base valorant program???
            // will need to send in discord bot information.

            // this should resolve the timer
            StopTimer();
            try
            {
                _logger.LogInformation("Starting Send of scheduled messages.");
                ISocketMessageChannel? channel = (ISocketMessageChannel)_client.GetChannel(_channelToMessage);
                if (channel == null)
                {
                    _logger.LogWarning($"{nameof(SendScheduledMessage)}: Could not find channel {_channelToMessage}");
                    return;
                }

                ConcurrentDictionary<string, BaseValorantMatch> usersMatchStats;
                _program.UpdateMatchAllUsers(out usersMatchStats);
                
                if (usersMatchStats == null || usersMatchStats.Count == 0)
                {
                    _logger.LogWarning($"{nameof(SendScheduledMessage)}: Could not find user match stats");
                    return;
                }

                HashSet<string> matchIds = new HashSet<string>();
                foreach (BaseValorantMatch match in usersMatchStats.Values)
                {
                    matchIds.Add(match.Matches.Match_Id);
                }

                foreach (string matchid in matchIds)
                {
                    List<KeyValuePair<string, BaseValorantMatch>> sortedList = usersMatchStats.Where(x => x.Value.Matches.Match_Id == matchid).ToList();
                    sortedList.Sort((x, y) => y.Value.MatchStats.Score.CompareTo(x.Value.MatchStats.Score));

                    if (sortedList.Count == 0)
                    {
                        _logger.LogError($"{nameof(SendScheduledMessage)}: Found 0 match stats for match id - {matchid}");
                        continue;
                    }
                    
                    if (sortedList.Count == 1)
                    {
                        _logger.LogInformation($"{nameof(SendScheduledMessage)}: Single user in match");
                        KeyValuePair<string, BaseValorantMatch> match = sortedList.First();
                        MatchStats stats = match.Value.MatchStats;
                        Matches matches = match.Value.Matches;

                        string userUpdated = $"<@{match.Value.UserInfo.Disc_id}>";
                        string rounds = string.Equals(stats.Team, "blue", StringComparison.InvariantCultureIgnoreCase) 
                            ? $"{matches.Blue_Team_Rounds_Won ?? 0} : {matches.Red_Team_Rounds_Won ?? 0}" 
                            : $"{matches.Red_Team_Rounds_Won ?? 0} : {matches.Blue_Team_Rounds_Won ?? 0}";
                        string averageRank = string.Equals(stats.Team, "blue", StringComparison.InvariantCultureIgnoreCase)
                            ? $"<{((RankEmojis)(matches.Blue_Team_Average_Rank ?? 0)).EmojiIdFromEnum()}> : <{((RankEmojis)(matches.Red_Team_Average_Rank ?? 0)).EmojiIdFromEnum()}>"
                            : $"<{((RankEmojis)(matches.Red_Team_Average_Rank ?? 0)).EmojiIdFromEnum()}> : <{((RankEmojis)(matches.Blue_Team_Average_Rank ?? 0)).EmojiIdFromEnum()}>";
                        match.Value.LogMatch();

                        EmbedFieldBuilder matchInfo = new EmbedFieldBuilder();
                        matchInfo.Name = Format.Sanitize("_".Repeat(40)) + "\n\nMatch Stats";
                        matchInfo.Value = $"<t:{matches.Game_Start ?? 0}:f>, {Math.Floor(TimeSpan.FromSeconds(matches.Game_Length).TotalMinutes)} minutes\nRounds {rounds}\nAverage Ranks {averageRank}";

                        EmbedBuilder embed = new EmbedBuilder()
                            .WithThumbnailUrl($"{AgentsExtension.AgentFromString(stats.Character).ImageURLFromAgent()}")
                            .WithAuthor
                            (new EmbedAuthorBuilder
                            {
                                Name = $"\n{ModesExtension.ModeFromString(matches.Mode.Safe().ToLower()).StringFromMode()} - {matches.Map}"
                            }
                            )
                            .WithTitle($"{match.Value.UserInfo.Val_username} - {AgentsExtension.AgentFromString(stats.Character).StringFromAgent()} <{((RankEmojis)(stats.Current_Tier ?? 0)).EmojiIdFromEnum()}> {(stats.MVP ? " :sparkles:" : "")}")
                            .AddField(matchInfo)
                            .WithDescription($"Combat Score: {stats.Score / matches.Rounds_Played}, K/D/A: {stats.Kills}/{stats.Deaths}/{stats.Assists}\nHeadshot: {stats.Headshots:0.00}%, RR: {stats.Rr_change}");

                        bool didTeamWin = string.Equals(stats.Team, "blue", StringComparison.InvariantCultureIgnoreCase)
                            ? matches.Blue_Team_Win ?? false
                            : !matches.Blue_Team_Win ?? false;
                        embed.WithColor(matches.Blue_Team_Rounds_Won == matches.Red_Team_Rounds_Won ? Color.DarkerGrey : didTeamWin ? Color.Green : Color.Red);

                        if (channel != null)
                        {
                            _logger.LogInformation($"{nameof(SendScheduledMessage)}: Successfully sending user data for {match.Value.UserInfo.Val_username}#{match.Value.UserInfo.Val_tagname}");
                            await channel.SendMessageAsync(userUpdated, embed: embed.Build());
                        }
                    }
                    else
                    {
                        string userUpdated = "";
                        MatchStats setupMatchStats = sortedList.First().Value.MatchStats;
                        Matches setupMatches = sortedList.First().Value.Matches;

                        string rounds = string.Equals(setupMatchStats.Team, "blue", StringComparison.InvariantCultureIgnoreCase)
                            ? $"{setupMatches.Blue_Team_Rounds_Won ?? 0} : {setupMatches.Red_Team_Rounds_Won ?? 0}"
                            : $"{setupMatches.Red_Team_Rounds_Won ?? 0} : {setupMatches.Blue_Team_Rounds_Won ?? 0}";

                        string averageRank = string.Equals(setupMatchStats.Team, "blue", StringComparison.InvariantCultureIgnoreCase)
                            ? $"<{((RankEmojis)(setupMatches.Blue_Team_Average_Rank ?? 0)).EmojiIdFromEnum()}> : <{((RankEmojis)(setupMatches.Red_Team_Average_Rank ?? 0)).EmojiIdFromEnum()}>"
                            : $"<{((RankEmojis)(setupMatches.Red_Team_Average_Rank ?? 0)).EmojiIdFromEnum()}> : <{((RankEmojis)(setupMatches.Blue_Team_Average_Rank ?? 0)).EmojiIdFromEnum()}>";

                        _logger.LogInformation($"{nameof(SendScheduledMessage)}: Multiple users in match");

                        EmbedBuilder embed = new EmbedBuilder()
                            .WithThumbnailUrl(MapsExtension.MapFromString(setupMatches.Map.Safe()).ImageUrlFromMap())
                            .WithAuthor
                            (new EmbedAuthorBuilder
                            {
                                Name = $"\n{ModesExtension.ModeFromString(setupMatches.Mode.Safe().ToLower()).StringFromMode()} - {setupMatches.Map.Safe()}"
                            }
                            );

                        EmbedFieldBuilder matchInfo = new EmbedFieldBuilder();
                        matchInfo.Name = Format.Sanitize("_".Repeat(40)) + "\n\nMatch Stats";
                        matchInfo.Value = $"<t:{setupMatches.Game_Start ?? 0}:f>, {Math.Floor(TimeSpan.FromSeconds(setupMatches.Game_Length).TotalMinutes)} minutes\nRounds {rounds}\nAverage Ranks {averageRank}";

                        foreach (KeyValuePair<string, BaseValorantMatch> match in sortedList)
                        {
                            userUpdated += $"<@{match.Value.UserInfo.Disc_id}> ";

                            match.Value.LogMatch();

                            EmbedFieldBuilder embedField = new EmbedFieldBuilder();

                            MatchStats stats = match.Value.MatchStats;
                            Matches matches = match.Value.Matches;

                            embedField.Name = $"{match.Value.UserInfo.Val_username} - {AgentsExtension.AgentFromString(stats.Character).StringFromAgent()} <{((RankEmojis)(stats.Current_Tier ?? 0)).EmojiIdFromEnum()}> {(stats.MVP ? " :sparkles:" : "")}";
                            embedField.Value = $"Combat Score: {stats.Score / matches.Rounds_Played}, K/D/A: {stats.Kills}/{stats.Deaths}/{stats.Assists}\nHeadshot: {stats.Headshots:0.00}%, RR: {stats.Rr_change}";
                            embed.AddField(embedField);
                        }

                        bool didTeamWin = string.Equals(setupMatchStats.Team, "blue", StringComparison.InvariantCultureIgnoreCase)
                            ? setupMatches.Blue_Team_Win ?? false
                            : !setupMatches.Blue_Team_Win ?? false;
                        embed.WithColor(setupMatches.Blue_Team_Rounds_Won == setupMatches.Red_Team_Rounds_Won ? Color.DarkerGrey : didTeamWin ? Color.Green : Color.Red);
                        embed.AddField(matchInfo);

                        if (channel != null && !string.IsNullOrEmpty(userUpdated))
                        {
                            _logger.LogInformation($"{nameof(SendScheduledMessage)}: Successfully sending users data for match id {matchid}");
                            await channel.SendMessageAsync(userUpdated, embed: embed.Build());
                        }
                    }
                }

                if (channel == null)
                {
                    _logger.LogWarning($"{nameof(SendScheduledMessage)}: Could not find channel {_channelToMessage}");
                    return;
                }
                _program.UpdateCurrentTierAllUsers([.. usersMatchStats.Values], channel);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {nameof(SendScheduledMessage)} - {ex.Message}");
            }
            finally
            {
                StartTimer();
            }
        }

        private void StopTimer()
        {
            lock(timerLock)
            {
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
                timerIsRunning = false;
            }
        }

        private void StartTimer()
        {
            lock (timerLock)
            {
                _timer.Change(TimeSpan.FromMinutes(0.5), TimeSpan.FromMinutes(0.5));
                timerIsRunning = true;
            }
        }

        private bool TimerIsRunning()
        {
            lock(timerLock)
            {
                return timerIsRunning;
            }
        }

        public bool TimedFunctionIsRunning()
        {
            return !TimerIsRunning();
        }
    }
}