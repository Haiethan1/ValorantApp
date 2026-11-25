using Microsoft.Extensions.Logging;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;
using ValorantApp.GenericExtensions;
using ValorantApp.Valorant;

namespace ValorantApp.DiscordBot
{
    /// <summary>
    /// Embed field values for a user's MMR response, free of Discord.Net types.
    /// </summary>
    public readonly record struct MmrEmbedData(
        string ThumbnailUrl,
        string AuthorName,
        string Title,
        string Description,
        string FooterText
    );

    /// <summary>Outcome of an MMR command: an error message or the data needed to build the embed.</summary>
    public readonly record struct MmrCommandResult(bool Success, string? ErrorMessage, MmrEmbedData? Data)
    {
        public static MmrCommandResult Ok(MmrEmbedData data) => new(true, null, data);

        public static MmrCommandResult Error(string message) => new(false, message, null);
    }

    /// <summary>
    /// The logic of the /mmr slash command, free of Discord.Net types so it can be tested.
    /// The slash command handler in <see cref="DiscordBotSlashCommands"/> only translates
    /// the interaction and builds the Discord embed.
    /// </summary>
    public class MmrCommandService
    {
        private readonly BaseValorantProgram _program;
        private readonly ILogger<MmrCommandService> _logger;

        public MmrCommandService(BaseValorantProgram program, ILogger<MmrCommandService> logger)
        {
            _program = program;
            _logger = logger;
        }

        public MmrCommandResult GetMmr(ulong discordUserId, string discordUsername, ulong channelId)
        {
            if (!TryGetUser(discordUserId, out BaseValorantUser? valorantUser) || valorantUser == null)
            {
                return MmrCommandResult.Error($"Could not find Valorant User for Discord User {discordUsername}");
            }

            // Ensure a user can only query shamebot's users of the same guild.
            if (!valorantUser.IsInChannel(channelId))
            {
                return MmrCommandResult.Error("Valorant user must be connected to this channel to use this command.");
            }

            MmrV2Json? mmr = valorantUser.GetMMR();
            if (mmr == null)
            {
                return MmrCommandResult.Error("API failed to respond.");
            }

            MmrEmbedData data = new(
                ThumbnailUrl: mmr.Current_Data?.Images?.Small.Safe() ?? "",
                AuthorName: $"{mmr.Name.Safe()}#{mmr.Tag.Safe()}",
                Title: mmr.Current_Data?.CurrentTierPatched.Safe() ?? "",
                Description: $"Current RR: {mmr.Current_Data?.Ranking_In_Tier}",
                FooterText: $"RR Change to last game: {mmr.Current_Data?.Mmr_Change_To_Last_Game}"
            );

            return MmrCommandResult.Ok(data);
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
