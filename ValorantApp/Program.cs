using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Web;
using ValorantApp.DiscordBot;
using ValorantApp.ValorantEnum;

namespace ValorantApp
{
    public class ValorantApp
    {

        //public static void Main(string[] args)
        //{
        //    HenrikApi henrikApi = new HenrikApi("Ehtan", "NA1", "na");

        //    var temp = henrikApi.Match().Result.Data;
        //    var highestHeadshot = temp[0].Players.All_Players[0];

        //    foreach (var player in temp[0].Players.All_Players)
        //    {
        //        var stats = player.Stats;
        //        double headshot = stats.Headshots / (double)(stats.Headshots + stats.Bodyshots + stats.Legshots) * 100.0;
        //        var statsMax = highestHeadshot.Stats;
        //        double headshotMax = statsMax.Headshots / (double)(statsMax.Headshots + statsMax.Bodyshots + statsMax.Legshots) * 100.0;

        //        highestHeadshot = headshot > headshotMax ? player : highestHeadshot;
        //        Console.WriteLine($"{player.Name} Headshot = {headshot.ToString("F")}%");
        //    }

        //    Console.WriteLine($"Highest Headshot player is {highestHeadshot.Name}");
        //}

        //private DiscordSocketClient _client;
        //public static Task Main(string[] args) => new ValorantApp().MainAsync();

        //public async Task MainAsync()
        //{
        //    _client = new DiscordSocketClient();

        //    _client.Log += Log;

        //    //  You can assign your bot token to a string, and pass that in to connect.
        //    //  This is, however, insecure, particularly if you plan to have your code hosted in a public repository.
        //    var token = "MTE0NDA0MDg5OTc5MDI1ODIxNg.GnBZAQ.dwaQ3SHhlJaKkmoqHU2QLpdKQPpoJx2h8wmt9o";

        //    // Some alternative options would be to keep your token in an Environment Variable or a standalone file.
        //    // var token = Environment.GetEnvironmentVariable("NameOfYourEnvironmentVariable");
        //    // var token = File.ReadAllText("token.txt");
        //    // var token = JsonConvert.DeserializeObject<AConfigurationClass>(File.ReadAllText("config.json")).Token;

        //    await _client.LoginAsync(TokenType.Bot, token);
        //    await _client.StartAsync();

        //    CommandHandler _commandHandler = new CommandHandler(_client,)
        //    //await 

        //    // Block this task until the program is closed.
        //    await Task.Delay(-1);
        //}

        //private Task Log(LogMessage msg)
        //{
        //    Console.WriteLine(msg.ToString());
        //    return Task.CompletedTask;
        //}

        private DiscordSocketClient _client;
        private CommandService _commands;
        private IServiceProvider _services;

        static void Main(string[] args)
            => new ValorantApp().RunBotAsync().GetAwaiter().GetResult();

        public async Task RunBotAsync()
        {
            var config = new DiscordSocketConfig()
            {
                // Other config options can be presented here.
                GatewayIntents = GatewayIntents.All
            };
            _client = new DiscordSocketClient(config);
            _commands = new CommandService();

            _client.Log += LogAsync;

            await RegisterCommandsAsync();

            await _client.LoginAsync(TokenType.Bot, "");
            await _client.StartAsync();

            // Block the program until it is closed
            await Task.Delay(-1);
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log);
            return Task.CompletedTask;
        }

        public async Task RegisterCommandsAsync()
        {
            _client.MessageReceived += HandleCommandAsync;

            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            if (!(messageParam is SocketUserMessage message)) return;
            if (message.Author.IsBot) return;

            int argPos = 0;
            if (message.HasStringPrefix("!", ref argPos))
            {
                var context = new SocketCommandContext(_client, message);

                var result = await _commands.ExecuteAsync(context, argPos, _services);
                if (!result.IsSuccess)
                {
                    await context.Channel.SendMessageAsync($"Error: {result.ErrorReason}");
                }
            }
        }
    }

    public static class DiscordPlayerNames
    {
        public static string[] PlayerNames = { "Ehtan", "Tokage", "Rivnar", "Zeo", "Maddyy", "Iso Ico", "Żérø", "Heejeh" };
    }
}