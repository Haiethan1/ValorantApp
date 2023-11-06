using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Configuration;
using System.Reflection;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;

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
            _channelToMessage = 1158083743278432378;
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
                Dictionary<string, MatchStats> usersMatchStats = new();
                _program.UpdateMatchAllUsers(out usersMatchStats);

                string messageStats = "";
                foreach (KeyValuePair<string, MatchStats> matchStats in usersMatchStats)
                {
                    BaseValorantUser? user = _program.GetValorantUser(matchStats.Key);
                    if (user == null)
                    {
                        continue;
                    }

                    MatchStats stats = matchStats.Value;

                    messageStats += $"<@{user.UserInfo.Disc_id}> Match stats - Map: {stats.Map}, RR change: {stats.Rr_change}, Headshot: {stats.Headshots:0.00}%, Score: {stats.Score/stats.Rounds}\n";
                }

                var channel = _client.GetChannel(_channelToMessage) as ISocketMessageChannel;
                if (channel != null && !string.IsNullOrEmpty(messageStats))
                {
                    await channel.SendMessageAsync(messageStats);
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