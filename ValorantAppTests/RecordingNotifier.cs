using Discord;
using System.Collections.Generic;
using System.Threading.Tasks;
using ValorantApp.DiscordBot;

namespace ValorantAppTests
{
    /// <summary>Test fake for <see cref="IDiscordNotifier"/>: records everything instead of talking to Discord.</summary>
    public class RecordingNotifier : IDiscordNotifier
    {
        public record SentEmbed(ulong ChannelId, string? Text, Embed Embed);

        public List<SentEmbed> Sent { get; } = new();
        public List<string> Statuses { get; } = new();

        public int GuildCount { get; set; } = 1;

        public Task SendEmbedAsync(ulong channelId, string? text, EmbedBuilder embed)
        {
            Sent.Add(new SentEmbed(channelId, text, embed.Build()));
            return Task.CompletedTask;
        }

        public Task SetStatusAsync(string text)
        {
            Statuses.Add(text);
            return Task.CompletedTask;
        }
    }
}
