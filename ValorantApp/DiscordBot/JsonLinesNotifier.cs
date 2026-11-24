using Discord;
using Newtonsoft.Json;

namespace ValorantApp.DiscordBot
{
    /// <summary>
    /// Test-mode notifier (see <see cref="AppOptions"/>): appends every embed and status as one JSON line to a file
    /// instead of talking to Discord, so an out-of-process test can read what the bot "posted".
    /// </summary>
    public class JsonLinesNotifier : IDiscordNotifier
    {
        private readonly string _path;
        private readonly object _lock = new();

        public JsonLinesNotifier(string path)
        {
            _path = path;
        }

        public int GuildCount => 1;

        public Task SendEmbedAsync(ulong channelId, string? text, EmbedBuilder embed)
        {
            Embed built = embed.Build();
            Append(new
            {
                type = "embed",
                channelId,
                text,
                title = built.Title,
                description = built.Description,
                url = built.Url,
                fields = built.Fields.Select(f => new { name = f.Name, value = f.Value, inline = f.Inline }).ToArray(),
            });
            return Task.CompletedTask;
        }

        public Task SetStatusAsync(string text)
        {
            Append(new { type = "status", text });
            return Task.CompletedTask;
        }

        private void Append(object record)
        {
            string line = JsonConvert.SerializeObject(record, Formatting.None) + Environment.NewLine;
            lock (_lock)
            {
                File.AppendAllText(_path, line);
            }
        }
    }
}
