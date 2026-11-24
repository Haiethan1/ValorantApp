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
using System.Diagnostics;
using System.Reflection;
using ValorantApp.Database.Extensions;
using ValorantApp.DiscordBot;
using ValorantApp.Valorant;

namespace ValorantApp
{
    public class ValorantApp
    {
        private DiscordSocketClient _client;
        private CommandService _commands;
        private InteractionService _interactions;
        private IServiceProvider _servicesProvider;
        private readonly BaseValorantProgram _program;
        private readonly ILogger<ValorantApp> _logger;

        static void Main(string[] args)
        {
            AppOptions options = AppOptions.FromEnvironment();

            if (options.TestMode)
            {
                // Fail closed: never the configured database, never SQL Server.
                DbSettings.Sqlite = $"Data Source={options.SqlitePath}";
                DbSettings.SqlServerEnabled = false;
            }

            ITable.CreateTables(DbSettings.Sqlite);

            ConfigureLogging();

            ServiceProvider serviceProvider = BuildServices(options);

            if (options.TestMode)
            {
                RunTestMode(serviceProvider, options);
                return;
            }

            ValorantApp program = serviceProvider.GetRequiredService<ValorantApp>();

            program.RunBotAsync().GetAwaiter().GetResult();
        }

        private static void ConfigureLogging()
        {
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
        }

        /// <summary>
        /// Builds the service container. Creating it has no side effects on Discord (the client is constructed but
        /// never connected until <see cref="RunBotAsync"/>), so tests can call it too.
        /// </summary>
        internal static ServiceProvider BuildServices(AppOptions options)
        {
            DiscordSocketConfig discordSocketConfig = new DiscordSocketConfig()
            {
                // Other config options can be presented here.
                GatewayIntents = GatewayIntents.All
            };

            //DiscordRestConfig discordRestConfig = new DiscordRestConfig()
            //{
            //    // Other config options can be presented here.
            //    GatewayIntents = GatewayIntents.All
            //};

            ServiceCollection services = new ServiceCollection();
            services.AddHttpClient("HenrikApiClient", client =>
            {
                client.BaseAddress = new Uri(options.HenrikBaseUrl);
                // Test mode never reads real credentials.
                client.DefaultRequestHeaders.Add("Authorization", options.TestMode ? "test-mode-token" : ConfigurationManager.AppSettings["HenrikToken"]);
            });
            services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));
            services.UseMinimalHttpLogger();
            services.AddSingleton(TimeProvider.System);
            if (options.TestMode)
            {
                services.AddSingleton<IDiscordNotifier>(new JsonLinesNotifier(options.RecordPath!));
            }
            else
            {
                services.AddSingleton<IDiscordNotifier, DiscordNotifier>();
            }
            services.AddSingleton<BaseValorantProgram>();
            services.AddSingleton(new DiscordSocketClient(discordSocketConfig));
            //services.AddSingleton(new DiscordRestClient(discordRestConfig));
            services.AddSingleton<CommandService>();
            services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));
            //services.AddSingleton<InteractionHandler>();
            services.AddSingleton<ValorantApp>();
            services.AddSingleton<LinkCommandService>();
            services.AddSingleton<DiscordBotSlashCommands>();

            //services.AddLogging()

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Test mode: run the real polling engine with the fakes from <see cref="BuildServices"/>, without ever
        /// logging into Discord. Prints READY once the timers are running, then blocks until stdin is closed.
        /// </summary>
        private static void RunTestMode(ServiceProvider serviceProvider, AppOptions options)
        {
            BaseValorantProgram program = serviceProvider.GetRequiredService<BaseValorantProgram>();
            program.Start(options.PollInitialDelay, options.PollInterval);

            Console.WriteLine("READY");

            while (Console.In.ReadLine() != null)
            {
            }
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
            string? token = ConfigurationManager.AppSettings[Debugger.IsAttached ? "DevelopBotToken" : "BotToken"];

            // Timers used to start in BaseValorantProgram's constructor; start them here, at the same point in startup.
            _program.Start();

            _client.Log += LogAsync;

            await RegisterCommandsAsync();

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            _client.Ready += ReadyAsync;

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
                    .WithContextTypes(InteractionContextType.Guild),
                new SlashCommandBuilder()
                    .WithName("addme")
                    .WithDescription("Add your Valorant account to the bot")
                    .AddOption("username", ApplicationCommandOptionType.String, "Your Valorant Riot ID", isRequired: true)
                    .AddOption("tagname", ApplicationCommandOptionType.String, "Your Valorant Riot tag", isRequired: true)
                    .WithContextTypes(InteractionContextType.Guild),
                new SlashCommandBuilder()
                    .WithName("addchannel")
                    .WithDescription("Add this channel to shamebot's send messages for you")
                    .WithContextTypes(InteractionContextType.Guild),
                new SlashCommandBuilder()
                    .WithName("deletechannel")
                    .WithDescription("Delete this channel for your account in shamebot, and your account if channels is empty.")
                    .WithContextTypes(InteractionContextType.Guild),
                new SlashCommandBuilder()
                    .WithName("deleteuser")
                    .WithDescription("Delete a discord user from shamebot (Admins only)")
                    .AddOption("username", ApplicationCommandOptionType.User, "The username of the user to delete", isRequired: true)
                    .WithDefaultMemberPermissions(GuildPermission.KickMembers)
                    .WithContextTypes(InteractionContextType.Guild),
                new SlashCommandBuilder()
                    .WithName("heatmap")
                    .WithDescription("Generate a heatmap of the selected game")
                    .WithContextTypes(InteractionContextType.Guild),

            };

            // Dev-only commands: only registered with a debugger attached (dev bot token). Because registration is a
            // bulk overwrite, a prod start removes them from the prod bot.
            if (Debugger.IsAttached)
            {
                globalCommandList.Add(new SlashCommandBuilder()
                    .WithName("devdeletematch")
                    .WithDescription("DEV ONLY: forget your latest stored match so it is re-posted on the next poll")
                    .WithContextTypes(InteractionContextType.Guild));
            }

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
                var registeredCommands = await _client.BulkOverwriteGlobalApplicationCommandsAsync(globalCommandList.Select(gcmd => gcmd.Build() as ApplicationCommandProperties).ToArray());
                _logger.LogInformation($"Registered global commands: {string.Join(", ", registeredCommands.Select(c => c.Name))}");
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

            await _program.UpdateDiscordBotConfig();
        }

        public async Task RegisterCommandsAsync()
        {
            //_client.MessageReceived += HandleCommandAsync;
            _client.SlashCommandExecuted += HandleInteractionAsync;
            _client.SelectMenuExecuted += HandleInteractionAsync;

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
                _logger.LogError($"{nameof(HandleInteractionAsync)} exception: {ex}");
            }
        }

        //private async Task SelectMenuHandlerAsync(SocketMessageComponent arg)
        //{
        //    try
        //    {
        //        SocketInteractionContext interactionContext = new SocketInteractionContext(_client, arg);
        //        await _interactions.inter(interactionContext, _servicesProvider);
        //    }
        //    catch (Exception ex)
        //    {
        //        await arg.RespondAsync($"Error when executing command {arg.Data}");
        //        _logger.LogError($"{nameof(HandleInteractionAsync)} exception: {ex}");
        //    }
        //}
    }
}