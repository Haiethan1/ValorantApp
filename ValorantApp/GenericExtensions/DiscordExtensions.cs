using Discord;
using Discord.WebSocket;
using ValorantApp.GenericUtils;

namespace ValorantApp.GenericExtensions
{
    public static class DiscordExtensions
    {
        public static async Task SendMessageAsyncWrapper(this ISocketMessageChannel channel, string? text = null, Embed? embed = null)
        {
            await channel.SendMessageAsync(text: text, embed: embed);
            await Task.Delay(TimerConstants.QUARTER_SECOND_MS);
        }
    }
}
