using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Configuration;
using System.Data.SQLite;
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
        private DiscordSocketClient _client;
        private CommandService _commands;
        private IServiceProvider _services;

        static void Main(string[] args)
        {
            string connectionString = "Data Source=myDatabase.db;Version=3;";
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS ValorantUsers (
                    Id INTEGER PRIMARY KEY NOT NULL,
                    val_username TEXT NOT NULL,
                    val_tagname TEXT NOT NULL,
                    val_affinity TEXT NOT NULL,
                    val_puuid TEXT
                    
                )";
                using (var createTableCommand = new SQLiteCommand(createTableQuery, connection))
                {
                    createTableCommand.ExecuteNonQuery();
                }

                string insertDataQuery = "INSERT INTO ValorantUsers  (val_username, val_tagname, val_affinity) VALUES (@val_username, @val_tagname, @val_affinity)";
                using (var insertDataCommand = new SQLiteCommand(insertDataQuery, connection))
                {
                    insertDataCommand.Parameters.AddWithValue("@val_username", "Ehtan");
                    insertDataCommand.Parameters.AddWithValue("@val_tagname", "NA1");
                    insertDataCommand.Parameters.AddWithValue("@val_affinity", "na");
                    insertDataCommand.ExecuteNonQuery();
                }

                // You can query and manipulate data using similar commands.
            }

            new ValorantApp().RunBotAsync().GetAwaiter().GetResult();
        }

        public async Task RunBotAsync()
        {
            var token = ConfigurationManager.AppSettings["BotToken"];
            var discordSocketConfig = new DiscordSocketConfig()
            {
                // Other config options can be presented here.
                GatewayIntents = GatewayIntents.All
            };
            _client = new DiscordSocketClient(discordSocketConfig);
            _commands = new CommandService();

            _client.Log += LogAsync;

            await RegisterCommandsAsync();

            await _client.LoginAsync(TokenType.Bot, token);
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