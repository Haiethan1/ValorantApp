using Discord;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace ValorantAppTests
{
    public class RecordedEmbedAuthor
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("url")]
        public string? Url { get; set; }

        [JsonProperty("iconUrl")]
        public string? IconUrl { get; set; }
    }

    public class RecordedEmbedFooter
    {
        [JsonProperty("text")]
        public string? Text { get; set; }

        [JsonProperty("iconUrl")]
        public string? IconUrl { get; set; }
    }

    public class RecordedEmbedField
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("value")]
        public string? Value { get; set; }

        [JsonProperty("inline")]
        public bool Inline { get; set; }
    }

    /// <summary>
    /// Captures all visible Discord embed fields for deterministic snapshot (approval-style) testing.
    /// </summary>
    public class RecordedEmbed
    {
        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("url")]
        public string? Url { get; set; }

        [JsonProperty("color")]
        public string? Color { get; set; }

        [JsonProperty("thumbnailUrl")]
        public string? ThumbnailUrl { get; set; }

        [JsonProperty("imageUrl")]
        public string? ImageUrl { get; set; }

        [JsonProperty("author")]
        public RecordedEmbedAuthor? Author { get; set; }

        [JsonProperty("footer")]
        public RecordedEmbedFooter? Footer { get; set; }

        [JsonProperty("timestampPresence")]
        public bool TimestampPresence { get; set; }

        [JsonProperty("fields")]
        public List<RecordedEmbedField> Fields { get; set; } = new();

        public static RecordedEmbed FromEmbed(Embed embed)
        {
            return new RecordedEmbed
            {
                Title = embed.Title,
                Description = embed.Description,
                Url = embed.Url,
                Color = embed.Color.HasValue ? $"#{embed.Color.Value.RawValue:X6}" : null,
                ThumbnailUrl = embed.Thumbnail?.Url,
                ImageUrl = embed.Image?.Url,
                Author = embed.Author.HasValue ? new RecordedEmbedAuthor
                {
                    Name = embed.Author.Value.Name,
                    Url = embed.Author.Value.Url,
                    IconUrl = embed.Author.Value.IconUrl
                } : null,
                Footer = embed.Footer.HasValue ? new RecordedEmbedFooter
                {
                    Text = embed.Footer.Value.Text,
                    IconUrl = embed.Footer.Value.IconUrl
                } : null,
                TimestampPresence = embed.Timestamp.HasValue,
                Fields = embed.Fields.Select(f => new RecordedEmbedField
                {
                    Name = f.Name,
                    Value = f.Value,
                    Inline = f.Inline
                }).ToList()
            };
        }
    }
}
