using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using ValorantApp.GenericExtensions;

namespace ValorantApp.DiscordBot
{
    /// <summary>Real implementation backed by the Discord gateway client.</summary>
    public class DiscordNotifier : IDiscordNotifier
    {
        private readonly DiscordSocketClient _client;
        private readonly ILogger<DiscordNotifier> _logger;

        public DiscordNotifier(DiscordSocketClient client, ILogger<DiscordNotifier> logger)
        {
            _client = client;
            _logger = logger;
        }

        public int GuildCount => _client.Guilds.Count;

        public Task SendEmbedAsync(ulong channelId, string? text, EmbedBuilder embed)
        {
            return _client.CheckChannelAndSendMessageAsync(channelId, text, embed, _logger);
        }

        public Task SetStatusAsync(string text)
        {
            return _client.SetGameAsync(text, type: ActivityType.CustomStatus);
        }
    }
}
