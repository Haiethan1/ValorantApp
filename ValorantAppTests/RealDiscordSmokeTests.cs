using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading.Tasks;
using ValorantApp.DiscordBot;

namespace ValorantAppTests
{
    /// <summary>
    /// LOCAL-ONLY smoke test against real Discord. Skipped (Inconclusive) unless you opt in with environment
    /// variables, so CI, cloud agents and a plain `dotnet test` never touch Discord. See docs/testing.md.
    ///
    ///   VALORANTAPP_SMOKE_BOT_TOKEN   token of the DEV bot (never the production token)
    ///   VALORANTAPP_SMOKE_CHANNEL_ID  a channel in your dev server where the bot may post
    ///
    /// It checks the one thing the fakes cannot: that the real DiscordNotifier posts an embed and sets a status.
    /// It deletes the message it posts. Slash commands cannot be triggered by a bot, so they are covered through
    /// LinkCommandService instead.
    /// </summary>
    [TestClass]
    [TestCategory("RealDiscord")]
    public class RealDiscordSmokeTests
    {
        private const string TokenVar = "VALORANTAPP_SMOKE_BOT_TOKEN";
        private const string ChannelVar = "VALORANTAPP_SMOKE_CHANNEL_ID";
        private const string AnyBotVar = "VALORANTAPP_SMOKE_ALLOW_ANY_BOT";

        [TestMethod]
        public async Task Notifier_PostsEmbedAndSetsStatusOnRealDiscord()
        {
            string? token = Environment.GetEnvironmentVariable(TokenVar);
            if (string.IsNullOrWhiteSpace(token) || !ulong.TryParse(Environment.GetEnvironmentVariable(ChannelVar), out ulong channelId))
            {
                Assert.Inconclusive($"Real Discord smoke test skipped: set {TokenVar} (a DEV bot token) and {ChannelVar} to run it.");
                return;
            }

            var client = new DiscordSocketClient(new DiscordSocketConfig { GatewayIntents = GatewayIntents.Guilds });
            var ready = new TaskCompletionSource();
            client.Ready += () =>
            {
                ready.TrySetResult();
                return Task.CompletedTask;
            };

            IUserMessage? posted = null;
            try
            {
                await client.LoginAsync(TokenType.Bot, token);
                await client.StartAsync();
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));

                // Safety: the dev bot's name contains "Develop". Refuse to post as anything else unless overridden.
                if (!client.CurrentUser.Username.Contains("develop", StringComparison.OrdinalIgnoreCase)
                    && Environment.GetEnvironmentVariable(AnyBotVar) != "1")
                {
                    Assert.Fail($"Refusing to run as bot '{client.CurrentUser.Username}': it does not look like the dev bot. Set {AnyBotVar}=1 to override.");
                }

                var notifier = new DiscordNotifier(client, NullLogger<DiscordNotifier>.Instance);
                Assert.IsTrue(notifier.GuildCount >= 1, "The dev bot should be in at least one server");

                string title = $"shamebot smoke test {Guid.NewGuid():N}";
                await notifier.SendEmbedAsync(channelId, null, new EmbedBuilder().WithTitle(title).WithDescription("Posted by RealDiscordSmokeTests; safe to delete."));
                await notifier.SetStatusAsync("shamebot smoke test");

                var channel = await client.GetChannelAsync(channelId) as IMessageChannel;
                Assert.IsNotNull(channel, "Channel not found or not a text channel the bot can see");

                var recent = await channel.GetMessagesAsync(10).FlattenAsync();
                posted = recent.OfType<IUserMessage>().FirstOrDefault(m => m.Embeds.Any(e => e.Title == title));

                Assert.IsNotNull(posted, "The embed was not found in the channel's recent messages");
                Assert.AreEqual(client.CurrentUser.Id, posted.Author.Id);
            }
            finally
            {
                if (posted != null)
                {
                    await posted.DeleteAsync();
                }

                await client.StopAsync();
                await client.LogoutAsync();
                await client.DisposeAsync();
            }
        }
    }
}
