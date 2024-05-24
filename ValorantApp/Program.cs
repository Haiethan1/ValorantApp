using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using System.Configuration;
using System.Reflection;
using ValorantApp.Database.Extensions;
using ValorantApp.Valorant;

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
                    .AddOption("username", ApplicationCommandOptionType.User, "The username of the user to get MMR for", isRequired: false)
                    .WithDMPermission(false),
                new SlashCommandBuilder()
                    .WithName("addme")
                    .WithDescription("Add your Valorant account to the bot")
                    .AddOption("username", ApplicationCommandOptionType.String, "Your Valorant Riot ID", isRequired: true)
                    .AddOption("tagname", ApplicationCommandOptionType.String, "Your Valorant Riot tag", isRequired: true)
                    .WithDMPermission(false),
                new SlashCommandBuilder()
                    .WithName("addchannel")
                    .WithDescription("Add this channel to shamebot's send messages for you")
                    .WithDMPermission(false),
                new SlashCommandBuilder()
                    .WithName("deletechannel")
                    .WithDescription("Delete this channel for your account in shamebot, and your account if channels is empty.")
                    .WithDMPermission(false),
                new SlashCommandBuilder()
                    .WithName("deleteuser")
                    .WithDescription("Delete a discord user from shamebot (Admins only)")
                    .AddOption("username", ApplicationCommandOptionType.User, "The username of the user to delete", isRequired: true)
                    .WithDefaultMemberPermissions(GuildPermission.KickMembers)
                    .WithDMPermission(false),

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
            //_client.MessageReceived += HandleCommandAsync;
            _client.SlashCommandExecuted += HandleInteractionAsync;

            //await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _servicesProvider);
            await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _servicesProvider);
        }

        /// <summary>
        /// Handles the commands asynchronously
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        //private async Task HandleCommandAsync(SocketMessage messageParam)
        //{
        //    if (!(messageParam is SocketUserMessage message)) return;
        //    if (message.Author.IsBot) return;

        //    int argPos = 0;
        //    if (message.HasStringPrefix("!", ref argPos))
        //    {
        //        _logger.LogInformation($"Starting discord command async. Command {message.Content}");
        //        var context = new SocketCommandContext(_client, message);

        //        var result = await _commands.ExecuteAsync(context, argPos, _servicesProvider);
        //        if (!result.IsSuccess)
        //        {
        //            await context.Channel.SendMessageAsync($"Error: {result.ErrorReason}");
        //        }
        //    }
        //}

        private async Task HandleInteractionAsync(SocketInteraction arg)
        {
            try
            {
                SocketInteractionContext interactionContext = new SocketInteractionContext(_client, arg);
                await _interactions.ExecuteCommandAsync(interactionContext, _servicesProvider);
            }
            catch (Exception ex)
            {
                await arg.RespondAsync($"Error when executing command {arg.Data}");
                _logger.LogError($"HandleInteractionAsync exception: {ex}");
            }
        }

        public async void SendScheduledMessage(object? state)
        {
            StopTimer();
            try
            {
                await _program.SendScheduledMessage(_client);
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