using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;
using ValorantApp.GenericExtensions;
using ValorantApp.Valorant;

namespace ValorantApp.DiscordBot
{
    /// <summary>Outcome of a command: a message to show the user and whether it was a success.</summary>
    public readonly record struct CommandResult(bool Success, string Message)
    {
        public static CommandResult Ok(string message) => new(true, message);

        public static CommandResult Error(string message) => new(false, message);
    }

    /// <summary>
    /// The logic of the account/channel linking slash commands (/addme, /addchannel, /deletechannel, /deleteuser),
    /// free of Discord.Net types so it can be tested. The slash command handlers in
    /// <see cref="DiscordBotSlashCommands"/> only translate the interaction into plain arguments and show the result.
    /// </summary>
    public class LinkCommandService
    {
        private readonly BaseValorantProgram _program;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceProvider _servicesProvider;
        private readonly ILogger<LinkCommandService> _logger;

        public LinkCommandService(BaseValorantProgram program, IHttpClientFactory httpClientFactory, IServiceProvider servicesProvider, ILogger<LinkCommandService> logger)
        {
            _program = program;
            _httpClientFactory = httpClientFactory;
            _servicesProvider = servicesProvider;
            _logger = logger;
        }

        /// <summary>/addme: link a Riot account to the calling Discord user and subscribe the current channel.</summary>
        public CommandResult AddUser(ulong discordUserId, string? username, string? tagname, ulong channelId)
        {
            if (username.IsNullOrEmpty() || tagname.IsNullOrEmpty())
            {
                return CommandResult.Error("Riot username or tagname cannot be empty");
            }

            // Keep it to a discord user can only have one account linked for now.
            if (ValorantUsersExtension.GetRowDiscordId(discordUserId) != null)
            {
                return CommandResult.Error("Shamebot only accepts one valorant account per discord user!");
            }

            BaseValorantUser? valorantUser = null;
            try
            {
                // Try creating the user. This can throw an exception if the username, tagname, and affinity don't match to a valorant account
                valorantUser = new BaseValorantUser(username!, tagname!, "na", discordUserId, _httpClientFactory, _servicesProvider.GetRequiredService<ILogger<BaseValorantProgram>>());
            }
            catch (Exception e)
            {
                _logger.LogWarning($"{nameof(AddUser)} - Exception: {e.Message}");
            }

            // Could not find the valorant account
            if (valorantUser == null)
            {
                return CommandResult.Error("Valorant user cannot be found or API failed to respond. Please check the user info again.");
            }

            // Keep it to a valorant user can only be added once in shamebot.
            if (ValorantUsersExtension.GetRow(valorantUser.Puuid) != null)
            {
                return CommandResult.Error("Shamebot only accepts one instance of a valorant account! Please ensure no one else is using your account.");
            }

            // The valorant user can be added to shamebot now.
            // Persist the user info and add the channel id of the current channel.
            valorantUser.PersistUser();
            valorantUser.AddChannelId(channelId);

            // Reload the program. This will restart the queue of users.
            // Grab the reloaded user to ensure it created in the valorant program
            _program.ReloadFromDB();
            valorantUser = _program.GetValorantUser(valorantUser.Puuid);

            if (valorantUser == null)
            {
                _logger.LogError($"{username}#{tagname} was not created after reloading the DB.");
                return CommandResult.Error("Valorant user was unable to be created after being added to the database. Error.");
            }

            return CommandResult.Ok($"Valorant User {valorantUser.UserInfo.Val_username}#{valorantUser.UserInfo.Val_tagname} created!");
        }

        /// <summary>/addchannel: also post this user's updates to the current channel.</summary>
        public CommandResult AddChannel(ulong discordUserId, string discordUsername, ulong channelId)
        {
            if (!TryGetUser(discordUserId, out BaseValorantUser? valorantUser) || valorantUser == null)
            {
                return CommandResult.Error($"Could not find Valorant User for Discord User {discordUsername}");
            }

            if (!valorantUser.AddChannelId(channelId))
            {
                return CommandResult.Error($"Channel is already present or could not be added for Discord User {discordUsername}");
            }

            return CommandResult.Ok($"Channel is added for Discord User {discordUsername}");
        }

        /// <summary>
        /// /deletechannel and /deleteuser: stop posting this user's updates to the current channel, and unlink the
        /// account entirely if that was its only channel.
        /// </summary>
        public CommandResult RemoveChannel(ulong discordUserId, string discordUsername, ulong channelId)
        {
            if (!TryGetUser(discordUserId, out BaseValorantUser? valorantUser) || valorantUser == null)
            {
                return CommandResult.Error($"Could not find Valorant User for Discord User {discordUsername}");
            }

            // Remove the channel from the user
            if (!valorantUser.RemoveChannelId(channelId))
            {
                return CommandResult.Error($"Channel is not present or could not be removed for Discord User {discordUsername}");
            }

            // Remove user if this is their only channel.
            if (valorantUser.ChannelIds.IsNullOrEmpty())
            {
                _program.DeleteUser(valorantUser.Puuid);
                return CommandResult.Ok($"Discord User {discordUsername} is no longer associated to Shamebot.");
            }

            return CommandResult.Ok($"Channel is deleted for Discord User {discordUsername}");
        }

        private bool TryGetUser(ulong discordUserId, out BaseValorantUser? valorantUser)
        {
            valorantUser = null;

            ValorantUsers? valorantUserDB = ValorantUsersExtension.GetRowDiscordId(discordUserId);
            if (valorantUserDB == null)
            {
                return false;
            }

            valorantUser = _program.GetValorantUser(valorantUserDB.Val_puuid);
            return valorantUser != null;
        }
    }
}
