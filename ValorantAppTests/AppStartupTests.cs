using Discord;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ValorantApp;
using ValorantApp.DiscordBot;
using ValorantApp.Valorant;

namespace ValorantAppTests
{
    /// <summary>Startup options (incl. the fail-closed test mode guards) and service composition.</summary>
    [TestClass]
    public class AppStartupTests
    {
        private static AppOptions Parse(params (string Name, string Value)[] vars)
        {
            var env = vars.ToDictionary(v => v.Name, v => v.Value);
            return AppOptions.FromEnvironment(name => env.TryGetValue(name, out string? v) ? v : null);
        }

        private static (string, string)[] ValidTestMode() =>
        [
            (AppOptions.TestModeVar, "1"),
            (AppOptions.HenrikUrlVar, "http://127.0.0.1:5000"),
            (AppOptions.SqlitePathVar, Path.Combine(Path.GetTempPath(), "e2e.db")),
            (AppOptions.RecordPathVar, Path.Combine(Path.GetTempPath(), "rec.jsonl")),
        ];

        #region AppOptions

        [TestMethod]
        public void NoEnvironment_UsesProductionDefaults()
        {
            AppOptions options = Parse();

            Assert.IsFalse(options.TestMode);
            Assert.AreEqual("https://api.henrikdev.xyz/valorant/", options.HenrikBaseUrl);
            Assert.AreEqual(TimeSpan.FromMinutes(1), options.PollInitialDelay);
            Assert.AreEqual(TimeSpan.FromMinutes(0.5), options.PollInterval);
            Assert.IsNull(options.SqlitePath);
            Assert.IsNull(options.RecordPath);
        }

        [DataTestMethod]
        [DataRow(AppOptions.HenrikUrlVar, "http://127.0.0.1:5000")]
        [DataRow(AppOptions.SqlitePathVar, "x.db")]
        [DataRow(AppOptions.RecordPathVar, "r.jsonl")]
        [DataRow(AppOptions.PollIntervalMsVar, "100")]
        [DataRow(AppOptions.PollInitialDelayMsVar, "0")]
        public void TestOnlyVariableWithoutTestMode_Throws(string name, string value)
        {
            Assert.ThrowsException<InvalidOperationException>(() => Parse((name, value)));
            Assert.ThrowsException<InvalidOperationException>(() => Parse((AppOptions.TestModeVar, "0"), (name, value)));
        }

        [DataTestMethod]
        [DataRow("1")]
        [DataRow("true")]
        [DataRow("TRUE")]
        public void TestMode_AcceptsOneAndTrue(string value)
        {
            var vars = ValidTestMode().Where(v => v.Item1 != AppOptions.TestModeVar).Append((AppOptions.TestModeVar, value)).ToArray();

            Assert.IsTrue(Parse(vars).TestMode);
        }

        [DataTestMethod]
        [DataRow(AppOptions.HenrikUrlVar)]
        [DataRow(AppOptions.SqlitePathVar)]
        [DataRow(AppOptions.RecordPathVar)]
        public void TestMode_MissingRequiredVariable_Throws(string missing)
        {
            var vars = ValidTestMode().Where(v => v.Item1 != missing).ToArray();

            Assert.ThrowsException<InvalidOperationException>(() => Parse(vars));
        }

        [DataTestMethod]
        [DataRow("https://api.henrikdev.xyz/valorant/")]
        [DataRow("http://192.168.1.10:5000")]
        [DataRow("http://example.com")]
        [DataRow("ftp://127.0.0.1")]
        [DataRow("not a url")]
        public void TestMode_NonLoopbackOrNonHttpUrl_Throws(string url)
        {
            var vars = ValidTestMode().Where(v => v.Item1 != AppOptions.HenrikUrlVar).Append((AppOptions.HenrikUrlVar, url)).ToArray();

            Assert.ThrowsException<InvalidOperationException>(() => Parse(vars));
        }

        [DataTestMethod]
        [DataRow("http://127.0.0.1:5000", "http://127.0.0.1:5000/")]
        [DataRow("http://localhost:8080/valorant", "http://localhost:8080/valorant/")]
        [DataRow("http://[::1]:9/", "http://[::1]:9/")]
        public void TestMode_LoopbackUrl_IsAcceptedWithTrailingSlash(string url, string expected)
        {
            var vars = ValidTestMode().Where(v => v.Item1 != AppOptions.HenrikUrlVar).Append((AppOptions.HenrikUrlVar, url)).ToArray();

            Assert.AreEqual(expected, Parse(vars).HenrikBaseUrl);
        }

        [TestMethod]
        public void TestMode_PollTimingDefaultsAreFast_AndCanBeOverridden()
        {
            AppOptions defaults = Parse(ValidTestMode());
            Assert.AreEqual(TimeSpan.FromMilliseconds(200), defaults.PollInitialDelay);
            Assert.AreEqual(TimeSpan.FromMilliseconds(500), defaults.PollInterval);

            AppOptions custom = Parse(ValidTestMode().Append((AppOptions.PollInitialDelayMsVar, "0")).Append((AppOptions.PollIntervalMsVar, "1500")).ToArray());
            Assert.AreEqual(TimeSpan.Zero, custom.PollInitialDelay);
            Assert.AreEqual(TimeSpan.FromMilliseconds(1500), custom.PollInterval);
        }

        [DataTestMethod]
        [DataRow(AppOptions.PollIntervalMsVar, "0")]
        [DataRow(AppOptions.PollIntervalMsVar, "-5")]
        [DataRow(AppOptions.PollIntervalMsVar, "abc")]
        [DataRow(AppOptions.PollInitialDelayMsVar, "-1")]
        public void TestMode_InvalidPollTiming_Throws(string name, string value)
        {
            Assert.ThrowsException<InvalidOperationException>(() => Parse(ValidTestMode().Append((name, value)).ToArray()));
        }

        #endregion

        #region Service composition

        [TestMethod]
        public void BuildServices_Production_ResolvesFullGraphWithRealNotifier()
        {
            using ServiceProvider provider = global::ValorantApp.ValorantApp.BuildServices(new AppOptions());

            Assert.IsInstanceOfType<DiscordNotifier>(provider.GetRequiredService<IDiscordNotifier>());
            Assert.IsNotNull(provider.GetRequiredService<BaseValorantProgram>());
            // Constructs the Discord client and interaction service but never connects.
            Assert.IsNotNull(provider.GetRequiredService<global::ValorantApp.ValorantApp>());
        }

        [TestMethod]
        public async Task BuildServices_TestMode_UsesJsonLinesNotifierAndLoopbackClient()
        {
            string recordPath = Path.Combine(Path.GetTempPath(), $"valorantapp-rec-{Guid.NewGuid():N}.jsonl");
            AppOptions options = Parse(ValidTestMode().Where(v => v.Item1 != AppOptions.RecordPathVar).Append((AppOptions.RecordPathVar, recordPath)).ToArray());

            try
            {
                using ServiceProvider provider = global::ValorantApp.ValorantApp.BuildServices(options);

                Assert.IsInstanceOfType<JsonLinesNotifier>(provider.GetRequiredService<IDiscordNotifier>());
                Assert.IsNotNull(provider.GetRequiredService<global::ValorantApp.ValorantApp>());

                HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("HenrikApiClient");
                Assert.AreEqual(new Uri("http://127.0.0.1:5000/"), client.BaseAddress);
                Assert.AreEqual("test-mode-token", client.DefaultRequestHeaders.GetValues("Authorization").Single());

                // The recorder writes exactly what the bot "posted", one JSON object per line.
                IDiscordNotifier notifier = provider.GetRequiredService<IDiscordNotifier>();
                await notifier.SendEmbedAsync(42UL, "<@1>", new EmbedBuilder().WithTitle("T").WithDescription("D").AddField("n", "v"));
                await notifier.SetStatusAsync("status text");

                string[] lines = File.ReadAllLines(recordPath);
                Assert.AreEqual(2, lines.Length);

                JObject embed = JObject.Parse(lines[0]);
                Assert.AreEqual("embed", (string?)embed["type"]);
                Assert.AreEqual(42UL, (ulong?)embed["channelId"]);
                Assert.AreEqual("<@1>", (string?)embed["text"]);
                Assert.AreEqual("T", (string?)embed["title"]);
                Assert.AreEqual("v", (string?)embed["fields"]![0]!["value"]);

                JObject status = JObject.Parse(lines[1]);
                Assert.AreEqual("status", (string?)status["type"]);
                Assert.AreEqual("status text", (string?)status["text"]);
            }
            finally
            {
                File.Delete(recordPath);
            }
        }

        #endregion
    }
}
