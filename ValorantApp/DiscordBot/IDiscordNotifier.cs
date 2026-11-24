using Discord;

namespace ValorantApp.DiscordBot
{
    /// <summary>
    /// Outbound Discord operations used by the polling/daily engine. Production uses
    /// <see cref="DiscordNotifier"/>; tests substitute a recording fake.
    /// </summary>
    public interface IDiscordNotifier
    {
        /// <summary>Number of guilds (servers) the bot is in.</summary>
        int GuildCount { get; }

        /// <summary>Posts an embed (and optional text) to a channel. Logs and returns if the channel isn't found.</summary>
        Task SendEmbedAsync(ulong channelId, string? text, EmbedBuilder embed);

        /// <summary>Sets the bot's custom status text.</summary>
        Task SetStatusAsync(string text);
    }
}
