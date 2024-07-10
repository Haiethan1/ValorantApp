using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using ValorantApp.GenericUtils;

namespace ValorantApp.GenericExtensions
{
    public static class DiscordExtensions
    {
        public static async Task SendMessageAsyncWrapper(this ISocketMessageChannel channel, string? text = null, Embed? embed = null)
        {
            await channel.SendMessageAsync(text: text, embed: embed);
            await Task.Delay(TimerUtils.QUARTER_SECOND_MS);
        }

        public static async Task CheckChannelAndSendMessageAsync<T>(this DiscordSocketClient client, ulong channelId, string? message, EmbedBuilder embed, ILogger<T> logger)
        {
            IChannel? channel = await client.GetChannelAsync(channelId);
            if (channel is null || channel is not ISocketMessageChannel socketMessageChannel)
            {
                logger.LogWarning($"{nameof(CheckChannelAndSendMessageAsync)}: Could not find channel {channelId}");
                return;
            }
            await socketMessageChannel.SendMessageAsyncWrapper(message, embed: embed.Build());
        }
    }
}
