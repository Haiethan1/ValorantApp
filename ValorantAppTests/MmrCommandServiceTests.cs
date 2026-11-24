using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;
using ValorantApp.DiscordBot;
using ValorantApp.Valorant;

namespace ValorantAppTests
{
    /// <summary>
    /// Component tests for MmrCommandService (/mmr command logic).
    /// Note: These tests share static DB state (SQLite connection), so MSTest must run them sequentially.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class MmrCommandServiceTests
    {
        private const string TestPuuid = "11111111-2222-3333-4444-555555555555";
        private const ulong TestDiscordId = 123456789012345678UL;
        private const ulong TestChannelId = 987654321098765432UL;

        #region Helpers

        private static string LoadMmrFixture(string fixtureName = "MmrV2.json")
        {
            string path = File.Exists(Path.Combine("../../../MockJsonResponse", fixtureName))
                ? Path.Combine("../../../MockJsonResponse", fixtureName)
                : Path.Combine(AppContext.BaseDirectory, "../../../MockJsonResponse", fixtureName);
            return File.ReadAllText(path);
        }

        private class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly string? _responseJson;
            private readonly HttpStatusCode _statusCode;

            public StubHttpMessageHandler(string? responseJson = null, HttpStatusCode statusCode = HttpStatusCode.OK)
            {
                _responseJson = responseJson;
                _statusCode = statusCode;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            {
                string url = req.RequestUri?.ToString() ?? string.Empty;
                if (_statusCode == HttpStatusCode.OK && _responseJson != null && url.Contains("v2/by-puuid/mmr", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(_responseJson, System.Text.Encoding.UTF8, "application/json")
                    });
                }
                return Task.FromResult(new HttpResponseMessage(_statusCode == HttpStatusCode.OK ? HttpStatusCode.NotFound : _statusCode)
                {
                    Content = new StringContent("{\"status\":404,\"message\":\"Not Found\"}", System.Text.Encoding.UTF8, "application/json")
                });
            }
        }

        private static (MmrCommandService Service, BaseValorantProgram Program) CreateService(HttpMessageHandler? handler = null)
        {
            var httpClient = new HttpClient(handler ?? new StubHttpMessageHandler())
            {
                BaseAddress = new Uri("https://api.henrikdev.xyz/valorant/")
            };
            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(x => x.CreateClient("HenrikApiClient")).Returns(httpClient);

            var program = new BaseValorantProgram(new RecordingNotifier(), factoryMock.Object, NullLogger<BaseValorantProgram>.Instance);
            var service = new MmrCommandService(program, NullLogger<MmrCommandService>.Instance);
            return (service, program);
        }

        private static void SeedUser(string puuid, string username, string tag, ulong discordId, params ulong[] channelIds)
        {
            ValorantUsersExtension.InsertRow(new ValorantUsers(username, tag, "na", puuid, discordId));
            foreach (ulong channelId in channelIds)
            {
                ValorantChannelMappingsExtension.InsertRow(new ValorantChannelMappings(puuid, channelId));
            }
        }

        private static void CleanupUser(string puuid, ulong discordId)
        {
            foreach (ulong channelId in ValorantChannelMappingsExtension.GetRowDiscordId(puuid).ToList())
            {
                ValorantChannelMappingsExtension.RemoveRow(new ValorantChannelMappings(puuid, channelId));
            }
            ValorantUsersExtension.DeleteRow(puuid, discordId);
        }

        #endregion

        #region Tests

        [TestMethod]
        public void GetMmr_UserNotLinked_ReturnsError()
        {
            var (service, _) = CreateService();

            var result = service.GetMmr(9999999999UL, "UnlinkedUser", TestChannelId);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Could not find Valorant User for Discord User UnlinkedUser", result.ErrorMessage);
            Assert.IsNull(result.Data);
        }

        [TestMethod]
        public void GetMmr_UserNotInChannel_ReturnsError()
        {
            const ulong wrongChannelId = 8888888888UL;
            try
            {
                SeedUser(TestPuuid, "FakePlayer", "NA1", TestDiscordId, TestChannelId);
                var (service, _) = CreateService();

                var result = service.GetMmr(TestDiscordId, "FakePlayer", wrongChannelId);

                Assert.IsFalse(result.Success);
                Assert.AreEqual("Valorant user must be connected to this channel to use this command.", result.ErrorMessage);
                Assert.IsNull(result.Data);
            }
            finally
            {
                CleanupUser(TestPuuid, TestDiscordId);
            }
        }

        [TestMethod]
        public void GetMmr_ApiReturnsNull_ReturnsError()
        {
            var handler = new StubHttpMessageHandler(statusCode: HttpStatusCode.InternalServerError);
            try
            {
                SeedUser(TestPuuid, "FakePlayer", "NA1", TestDiscordId, TestChannelId);
                var (service, _) = CreateService(handler);

                var result = service.GetMmr(TestDiscordId, "FakePlayer", TestChannelId);

                Assert.IsFalse(result.Success);
                Assert.AreEqual("API failed to respond.", result.ErrorMessage);
                Assert.IsNull(result.Data);
            }
            finally
            {
                CleanupUser(TestPuuid, TestDiscordId);
            }
        }

        [TestMethod]
        public void GetMmr_HappyPath_ReturnsExpectedEmbedData()
        {
            string fixtureJson = LoadMmrFixture("MmrV2.json");
            var handler = new StubHttpMessageHandler(fixtureJson);
            try
            {
                SeedUser(TestPuuid, "FakePlayer", "NA1", TestDiscordId, TestChannelId);
                var (service, _) = CreateService(handler);

                var result = service.GetMmr(TestDiscordId, "FakePlayer", TestChannelId);

                Assert.IsTrue(result.Success);
                Assert.IsNull(result.ErrorMessage);
                Assert.IsNotNull(result.Data);

                var data = result.Data.Value;
                Assert.AreEqual("https://media.valorant-api.com/competitivetiers/03621f52-342b-cf4e-4f86-9350a49c6d04/22/smallicon.png", data.ThumbnailUrl);
                Assert.AreEqual("FakePlayer#NA1", data.AuthorName);
                Assert.AreEqual("Ascendant 2", data.Title);
                Assert.AreEqual("Current RR: 50", data.Description);
                Assert.AreEqual("RR Change to last game: 18", data.FooterText);
            }
            finally
            {
                CleanupUser(TestPuuid, TestDiscordId);
            }
        }

        [TestMethod]
        public void GetMmr_NullMissingNestedFields_DoesNotThrow()
        {
            string jsonWithNulls = "{\"status\":200,\"data\":{\"name\":\"FakePlayer\",\"tag\":\"NA1\",\"current_data\":{\"currenttier\":0,\"ranking_in_tier\":0,\"mmr_change_to_last_game\":0,\"images\":null,\"currenttierpatched\":null}}}";
            var handler = new StubHttpMessageHandler(jsonWithNulls);
            try
            {
                SeedUser(TestPuuid, "FakePlayer", "NA1", TestDiscordId, TestChannelId);
                var (service, _) = CreateService(handler);

                var result = service.GetMmr(TestDiscordId, "FakePlayer", TestChannelId);

                Assert.IsTrue(result.Success);
                Assert.IsNull(result.ErrorMessage);
                Assert.IsNotNull(result.Data);

                var data = result.Data.Value;
                Assert.AreEqual(string.Empty, data.ThumbnailUrl);
                Assert.AreEqual("FakePlayer#NA1", data.AuthorName);
                Assert.AreEqual(string.Empty, data.Title);
                Assert.AreEqual("Current RR: 0", data.Description);
                Assert.AreEqual("RR Change to last game: 0", data.FooterText);
            }
            finally
            {
                CleanupUser(TestPuuid, TestDiscordId);
            }
        }

        [TestMethod]
        public void GetMmr_NullCurrentData_DoesNotThrow()
        {
            string jsonWithNullCurrentData = "{\"status\":200,\"data\":{\"name\":null,\"tag\":null,\"current_data\":null}}";
            var handler = new StubHttpMessageHandler(jsonWithNullCurrentData);
            try
            {
                SeedUser(TestPuuid, "FakePlayer", "NA1", TestDiscordId, TestChannelId);
                var (service, _) = CreateService(handler);

                var result = service.GetMmr(TestDiscordId, "FakePlayer", TestChannelId);

                Assert.IsTrue(result.Success);
                Assert.IsNull(result.ErrorMessage);
                Assert.IsNotNull(result.Data);

                var data = result.Data.Value;
                Assert.AreEqual(string.Empty, data.ThumbnailUrl);
                Assert.AreEqual("#", data.AuthorName);
                Assert.AreEqual(string.Empty, data.Title);
            }
            finally
            {
                CleanupUser(TestPuuid, TestDiscordId);
            }
        }

        #endregion
    }
}
