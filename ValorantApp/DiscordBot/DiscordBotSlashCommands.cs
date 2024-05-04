using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Repositories.Interfaces;
using ValorantApp.Database.Tables;
using ValorantApp.GenericExtensions;
using ValorantApp.Valorant;

namespace ValorantApp.DiscordBot
{
    public class DiscordBotSlashCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private DiscordSocketClient _client;
        private InteractionService _interaction;
        private IServiceProvider _servicesProvider;
        private readonly IHttpClientFactory _httpClientFactory;
        private ILogger<ValorantApp> _logger;
        public DiscordBotSlashCommands(DiscordSocketClient client, InteractionService interaction, IServiceProvider servicesProvider, IHttpClientFactory httpClientFactory, ILogger<ValorantApp> logger)
        {
            _client = client;
            _interaction = interaction;
            _servicesProvider = servicesProvider;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [SlashCommand("mmr", "Get user's Valorant MMR")]
        public async Task SlashGetMMROfDiscordUser()
        {
            SocketUser? user = ((SocketSlashCommand)Context.Interaction).Data.Options.FirstOrDefault()?.Value as SocketUser ?? null;
            if (user == null)
            {
                user = Context.User;
            }
            if (!GetUserAndProgram(user, out BaseValorantProgram? program, out BaseValorantUser? valorantUser) || program == null || valorantUser == null)
            {
                await RespondAsync($"Could not find Valorant User for Discord User {user.Username}");
                return;
            }

            MmrV2Json? mmr = valorantUser.GetMMR();
            if (mmr == null)
            {
                await RespondAsync($"Could not find mmr stats for Discord User {user.Username}");
                return;
            }
            var embed = new EmbedBuilder()
                .WithThumbnailUrl($"{mmr.Current_Data.Images?.Small.Safe() ?? ""}")
                .WithAuthor
                (new EmbedAuthorBuilder
                {
                    Name = $"{mmr.Name.Safe()}#{mmr.Tag.Safe()}"
                }
                )
                .WithTitle(mmr.Current_Data.CurrentTierPatched.Safe())
                .WithDescription($"Current RR: {mmr.Current_Data.Ranking_In_Tier % 100}")
                .WithFooter
                (new EmbedFooterBuilder
                {
                    Text = $"RR Change to last game: {mmr.Current_Data.Mmr_Change_To_Last_Game}"
                }
                );
            await RespondAsync(embed: embed.Build());
        }

        [SlashCommand("addme", "Add your Valorant account to the bot")]
        public async Task AddUser()
        {
            var slashCommandOptions = ((SocketSlashCommand)Context.Interaction).Data.Options;
            string username = (string)slashCommandOptions.FirstOrDefault();
            string tagname = (string)slashCommandOptions.ElementAtOrDefault(1);
            string result;
            if (username.IsNullOrEmpty() || tagname.IsNullOrEmpty())
            {
                result = "Riot username or tagname cannot be empty";
                await RespondAsync(result);
                return;
            }

            SocketUser userInfo = Context.User;
            string? puuid = BaseValorantUser.CreateUser(
                username
                , tagname
                , "na"
                , userInfo.Id
                , _httpClientFactory
                , _servicesProvider.GetService<ILogger<BaseValorantProgram>>()
                , _servicesProvider.GetRequiredService<IMatchesRepository>()
                , _servicesProvider.GetRequiredService<IMatchStatsRepository>()
                , _servicesProvider.GetRequiredService<IValorantUsersRepository>()
                )?.Puuid;

            if (puuid == null)
            {
                result = "Valorant user was either in the database already or cannot be found";
                await RespondAsync(result);
                return;
            }

            BaseValorantProgram program = _servicesProvider.GetRequiredService<BaseValorantProgram>();

            program.ReloadFromDB();
            var user = program.GetValorantUser(puuid);

            if (user == null)
            {
                result = "Valorant user was unable to be created after being added to the database. Error.";
                _logger.LogError($"{username}#{tagname} was not created after reloading the DB.");
                await RespondAsync(result);
                return;
            }

            result = $"Valorant User {user.UserInfo.Val_username}#{user.UserInfo.Val_tagname} created!";
            await RespondAsync(result);
        }

        #region Helpers

        private bool IsUserAndTag(string riotID, out string username, out string tagname)
        {
            username = "";
            tagname = "";
            if (riotID == null)
            {
                return false;
            }

            int splitHashTag = riotID.IndexOf("#");
            if (splitHashTag < 0)
            {
                return false;
            }

            username = riotID.Substring(0, splitHashTag);
            tagname = riotID.Substring(splitHashTag + 1);

            return true;
        }

        private bool GetUserAndProgram(SocketUser userInfo, out BaseValorantProgram? program, out BaseValorantUser? valorantUser)
        {
            program = null;
            valorantUser = null;

            ValorantUsers? valorantUserDB = ValorantUsersExtension.GetRowDiscordId(userInfo.Id);
            if (valorantUserDB == null)
            {
                return false;
            }

            program = _servicesProvider.GetRequiredService<BaseValorantProgram>();
            valorantUser = program.GetValorantUser(valorantUserDB.Val_puuid);
            if (valorantUser == null)
            {
                return false;
            }

            return true;
        }

        #endregion Helpers

    }
}
