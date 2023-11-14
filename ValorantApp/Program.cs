using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Configuration;
using System.Reflection;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;
using ValorantApp.Valorant;
using ValorantApp.Valorant.Enums;

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

        static void Main(string[] args)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["Database"].ConnectionString;

            ITable.CreateTables(connectionString);
            var services = new ServiceCollection();
            // clean this up -> https://stackoverflow.com/questions/66598644/discord-net-bot-sharing-the-same-databasecontext-between-all-modules-when-using
            services.AddSingleton<BaseValorantProgram>();
            var discordSocketConfig = new DiscordSocketConfig()
            {
                // Other config options can be presented here.
                GatewayIntents = GatewayIntents.All
            };
            services.AddSingleton(new DiscordSocketClient(discordSocketConfig));
            services.AddSingleton<CommandService>();
            var serviceProvider = services.BuildServiceProvider();
            var program = new ValorantApp(serviceProvider.GetRequiredService<BaseValorantProgram>(), serviceProvider.GetRequiredService<DiscordSocketClient>(), serviceProvider.GetRequiredService<CommandService>(), serviceProvider);

            program.RunBotAsync().GetAwaiter().GetResult();
        }

        public ValorantApp(BaseValorantProgram program, DiscordSocketClient client, CommandService commands, IServiceProvider servicesProvider)
        {
            _program = program;
            _servicesProvider = servicesProvider;
            _channelToMessage = ulong.Parse(ConfigurationManager.AppSettings["ChannelToMessage"] ?? "");
            _commands = commands;
            _client = client;
        }

        public async Task RunBotAsync()
        {
            var token = ConfigurationManager.AppSettings["BotToken"];
            
            _client.Log += LogAsync;

            await RegisterCommandsAsync();

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            _client.Ready += ReadyAsync;

            _timer = new Timer(SendScheduledMessage, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

            // Block the program until it is closed
            await Task.Delay(-1);
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log);
            return Task.CompletedTask;
        }

        private Task ReadyAsync()
        {
            Console.WriteLine($"{_client.CurrentUser.Username} is connected!");
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
            try
            {
                ISocketMessageChannel? channel = (ISocketMessageChannel)_client.GetChannel(_channelToMessage);
                if (channel == null)
                {
                    return;
                }

                Dictionary<string, MatchStats> usersMatchStats;
                _program.UpdateMatchAllUsers(out usersMatchStats);
                
                if (usersMatchStats == null || usersMatchStats.Count == 0)
                {
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
                        continue;
                    }

                    if (sortedList.Count == 1)
                    {
                        KeyValuePair<string, MatchStats> match = sortedList.First();
                        MatchStats stats = match.Value;

                        BaseValorantUser? user = _program.GetValorantUser(match.Key);
                        if (user == null)
                        {
                            continue;
                        }

                        string userUpdated = $"<@{user.UserInfo.Disc_id}>";
                        
                        EmbedBuilder embed = new EmbedBuilder()
                            .WithThumbnailUrl($"{AgentsExtension.AgentFromString(stats.Character).ImageURLFromAgent()}")
                            .WithAuthor
                            (new EmbedAuthorBuilder
                            {
                                Name = $"{ModesExtension.ModeFromString(stats.Mode.ToLower()).StringFromMode()} - {stats.Map}"
                            }
                            )
                            .WithTitle($"{user.UserInfo.Val_username} - {AgentsExtension.AgentFromString(stats.Character).StringFromAgent()}")
                            .WithDescription($"Combat Score: {stats.Score/stats.Rounds}, K/D/A: {stats.Kills}/{stats.Deaths}/{stats.Assists}\nHeadshot: {stats.Headshots:0.00}%, RR: {stats.Rr_change}")
                            .WithColor(stats.Rr_change >= 0 && stats.Rr_change < 5 ? Color.DarkerGrey : stats.Rr_change > 0 ? Color.Green : Color.Red);

                        if (channel != null)
                        {
                            await channel.SendMessageAsync(userUpdated, embed: embed.Build());
                        }
                    }
                    else
                    {
                        string userUpdated = "";
                        MatchStats setupMatchStats = sortedList.First().Value;
                        int rrChange = 0;
                        EmbedBuilder embed = new EmbedBuilder()
                            .WithThumbnailUrl(MapsExtension.MapFromString(setupMatchStats.Map).ImageUrlFromMap())
                            .WithAuthor
                            (new EmbedAuthorBuilder
                            {
                                Name = $"{ModesExtension.ModeFromString(setupMatchStats.Mode.ToLower()).StringFromMode()} - {setupMatchStats.Map}"
                            }
                            );

                        foreach (KeyValuePair<string, MatchStats> matchStats in sortedList)
                        {
                            BaseValorantUser? user = _program.GetValorantUser(matchStats.Key);
                            if (user == null)
                            {
                                continue;
                            }

                            userUpdated += $"<@{user.UserInfo.Disc_id}> ";

                            EmbedFieldBuilder embedField = new EmbedFieldBuilder();

                            MatchStats stats = matchStats.Value;
                            embedField.Name = $"{user.UserInfo.Val_username} - {AgentsExtension.AgentFromString(stats.Character).StringFromAgent()}";
                            embedField.Value = $"Combat Score: {stats.Score / stats.Rounds}, K/D/A: {stats.Kills}/{stats.Deaths}/{stats.Assists}\nHeadshot: {stats.Headshots:0.00}%, RR: {stats.Rr_change}";
                            embed.AddField(embedField);
                            rrChange += stats.Rr_change;
                        }

                        rrChange = rrChange / sortedList.Count;
                        embed.WithColor(rrChange >= 0 && rrChange < 5 ? Color.DarkerGrey : rrChange > 0 ? Color.Green : Color.Red);

                        if (channel != null && !string.IsNullOrEmpty(userUpdated))
                        {
                            await channel.SendMessageAsync(userUpdated, embed: embed.Build());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    public static class DiscordPlayerNames
    {
        public static string[] PlayerNames = { "Ehtan", "Tokage", "Rivnar", "Zeo", "Maddyy", "Iso Ico", "Żérø", "Heejeh" };
    }
}