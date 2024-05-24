using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValorantApp.Database.Extensions;
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

        #region Slash Commands

        [SlashCommand("mmr", "Get user's Valorant MMR")]
        public async Task SlashGetMMROfDiscordUser()
        {
            SocketUser user = ((SocketSlashCommand)Context.Interaction).Data.Options.FirstOrDefault()?.Value as SocketUser ?? Context.User;

            if (!GetUserAndProgram(user, out BaseValorantProgram? program, out BaseValorantUser? valorantUser) || program == null || valorantUser == null)
            {
                await RespondErrorAsync($"Could not find Valorant User for Discord User {user.Username}");
                return;
            }

            // Ensure a user can only query shamebot's users of the same guild.
            if (!valorantUser.IsInChannel(Context.Channel.Id))
            {
                await RespondErrorAsync($"Valorant user must be connected to this channel to use this command.");
                return;
            }

            MmrV2Json? mmr = valorantUser.GetMMR();
            if (mmr == null)
            {
                await RespondErrorAsync($"API failed to respond.");
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
                await RespondErrorAsync(result);
                return;
            }

            SocketUser userInfo = Context.User;

            // Keep it to a discord user can only have one account linked for now.
            if (ValorantUsersExtension.GetRowDiscordId(userInfo.Id) != null)
            {
                result = "Shamebot only accepts one valorant account per discord user!";
                await RespondErrorAsync(result);
                return;
            }

            BaseValorantUser? valorantUser = null;
            try
            {
                // Try creating the user. This can throw an exception if the username, tagname, and affinity don't match to a valorant account
                valorantUser = new BaseValorantUser(username, tagname, "na", userInfo.Id, _httpClientFactory, _servicesProvider.GetService<ILogger<BaseValorantProgram>>());
            }
            catch (Exception e)
            {
                _logger.LogWarning($"{nameof(AddUser)} - Exception: {e.Message}");
            }

            // Could not find the valorant account
            if (valorantUser == null)
            {
                result = "Valorant user cannot be found. Please check the user info again.";
                await RespondErrorAsync(result);
                return;
            }

            // Keep it to a valorant user can only be added once in shamebot.
            if (ValorantUsersExtension.GetRow(valorantUser.Puuid) != null)
            {
                result = "Shamebot only accepts one instance of a valorant account! Please ensure no one else is using your account.";
                await RespondErrorAsync(result);
                return;
            }

            // The valorant user can be added to shamebot now.
            // Persist the user info and add the channel id of the current channel.
            valorantUser.PersistUser();
            valorantUser.AddChannelId(Context.Channel.Id);

            BaseValorantProgram program = _servicesProvider.GetRequiredService<BaseValorantProgram>();

            // Reload the program. This will restart the queue of users.
            // Grab the reloaded user to ensure it created in the valorant program
            program.ReloadFromDB();
            valorantUser = program.GetValorantUser(valorantUser.Puuid);

            if (valorantUser == null)
            {
                result = "Valorant user was unable to be created after being added to the database. Error.";
                _logger.LogError($"{username}#{tagname} was not created after reloading the DB.");
                await RespondErrorAsync(result);
                return;
            }

            result = $"Valorant User {valorantUser.UserInfo.Val_username}#{valorantUser.UserInfo.Val_tagname} created!";
            await RespondSuccessAsync(result);
        }

        [SlashCommand("addchannel", "Add this channel to shamebot's send messages for you")]
        public async Task AddChannel()
        {
            SocketUser user = Context.User;

            if (!GetUserAndProgram(user, out BaseValorantProgram? program, out BaseValorantUser? valorantUser) || program == null || valorantUser == null)
            {
                await RespondErrorAsync($"Could not find Valorant User for Discord User {user.Username}");
                return;
            }

            if (!valorantUser.AddChannelId(Context.Channel.Id))
            {
                await RespondErrorAsync($"Channel is already present or could not be added for Discord User {user.Username}");
                return;
            }

            await RespondSuccessAsync($"Channel is added for Discord User {user.Username}");
            return;
        }

        [SlashCommand("deletechannel", "Delete this channel to shamebot's send messages for you. This will delete shamebot's link to your account if this is the only channel it is linked to.")]
        public async Task DeleteChannel()
        {
            SocketUser user = Context.User;

            if (!GetUserAndProgram(user, out BaseValorantProgram? program, out BaseValorantUser? valorantUser) || program == null || valorantUser == null)
            {
                await RespondErrorAsync($"Could not find Valorant User for Discord User {user.Username}");
                return;
            }

            // Remove the channel from the user
            if (!valorantUser.RemoveChannelId(Context.Channel.Id))
            {
                await RespondErrorAsync($"Channel is not present or could not be removed for Discord User {user.Username}");
                return;
            }

            // Remove user if this is their only channel.
            if (valorantUser.ChannelIds.IsNullOrEmpty())
            {
                program.DeleteUser(valorantUser.Puuid);
                await RespondSuccessAsync($"Discord User {user.Username} is no longer associated to Shamebot.");
                return;
            }

            await RespondSuccessAsync($"Channel is deleted for Discord User {user.Username}");
            return;
        }

        [SlashCommand("deleteuser", "Delete a discord user from shamebot (Admins only)")]
        public async Task DeleteUser()
        {
            SocketUser? user = ((SocketSlashCommand)Context.Interaction).Data.Options.FirstOrDefault()?.Value as SocketUser;
            if (user == null)
            {
                await RespondErrorAsync($"Invalid Discord User");
                return;
            }

            if (!GetUserAndProgram(user, out BaseValorantProgram? program, out BaseValorantUser? valorantUser) || program == null || valorantUser == null)
            {
                await RespondErrorAsync($"Could not find Valorant User for Discord User {user.Username}");
                return;
            }

            // Remove the channel from the user
            if (!valorantUser.RemoveChannelId(Context.Channel.Id))
            {
                await RespondErrorAsync($"Channel is not present or could not be removed for Discord User {user.Username}");
                return;
            }

            // Remove user if this is their only channel.
            if (valorantUser.ChannelIds.IsNullOrEmpty())
            {
                program.DeleteUser(valorantUser.Puuid);
                await RespondSuccessAsync($"Discord User {user.Username} is no longer associated to Shamebot.");
                return;
            }

            await RespondSuccessAsync($"Channel is deleted for Discord User {user.Username}");
            return;
        }

        #endregion Slash Commands

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

        private async Task RespondErrorAsync(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            var embed = new EmbedBuilder()
                .WithDescription(message)
                .WithColor(Color.DarkRed);

            await RespondAsync(embed: embed.Build());
        }

        private async Task RespondSuccessAsync(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            var embed = new EmbedBuilder()
                .WithDescription(message)
                .WithColor(Color.DarkGreen);

            await RespondAsync(embed: embed.Build());
        }

        #endregion Helpers

    }
}
