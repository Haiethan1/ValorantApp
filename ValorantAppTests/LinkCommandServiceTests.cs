using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
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
    /// Component tests for LinkCommandService (/addme, /addchannel, /deletechannel, /deleteuser logic).
    /// Note: These tests share static DB state (SQLite connection), so MSTest must run them sequentially.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class LinkCommandServiceTests
    {
        #region Helpers

        private static string CreateAccountJson(string puuid, string name, string tag) =>
            $"{{\"status\":200,\"data\":{{\"puuid\":\"{puuid}\",\"region\":\"na\",\"account_level\":50,\"name\":\"{name}\",\"tag\":\"{tag}\"}}}}";

        private class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly string? _accountJson;
            private readonly HttpStatusCode _statusCode;

            public StubHttpMessageHandler(string? accountJson = null, HttpStatusCode statusCode = HttpStatusCode.OK)
            {
                _accountJson = accountJson;
                _statusCode = statusCode;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            {
                string url = req.RequestUri?.ToString() ?? string.Empty;
                if (_statusCode == HttpStatusCode.OK && _accountJson != null && url.Contains("v1/account", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(_accountJson, System.Text.Encoding.UTF8, "application/json")
                    });
                }
                return Task.FromResult(new HttpResponseMessage(_statusCode == HttpStatusCode.OK ? HttpStatusCode.NotFound : _statusCode)
                {
                    Content = new StringContent("{\"status\":404,\"message\":\"Not Found\"}", System.Text.Encoding.UTF8, "application/json")
                });
            }
        }

        private static (LinkCommandService Service, BaseValorantProgram Program) CreateService(HttpMessageHandler? handler = null)
        {
            var httpClient = new HttpClient(handler ?? new StubHttpMessageHandler())
            {
                BaseAddress = new Uri("https://api.henrikdev.xyz/valorant/")
            };
            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(x => x.CreateClient("HenrikApiClient")).Returns(httpClient);

            var services = new ServiceCollection();
            services.AddSingleton<ILogger<BaseValorantProgram>>(NullLogger<BaseValorantProgram>.Instance);
            var serviceProvider = services.BuildServiceProvider();

            var program = new BaseValorantProgram(new RecordingNotifier(), factoryMock.Object, NullLogger<BaseValorantProgram>.Instance);
            var service = new LinkCommandService(program, factoryMock.Object, serviceProvider, NullLogger<LinkCommandService>.Instance);
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

        #region AddChannel Tests

        [TestMethod]
        public void AddChannel_UnknownUser_ReturnsError()
        {
            var (service, _) = CreateService();
            var result = service.AddChannel(100001UL, "GhostUser", 200001UL);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("Could not find Valorant User for Discord User GhostUser", result.Message);
        }

        [TestMethod]
        public void AddChannel_KnownUser_NewChannel_ReturnsOkAndStoresMapping()
        {
            const string puuid = "10000000-0000-0000-0000-000000000002";
            const ulong discordId = 100002UL, channel1 = 200001UL, channel2 = 200002UL;
            SeedUser(puuid, "TestUser", "NA1", discordId, channel1);
            var (service, _) = CreateService();
            try
            {
                var result = service.AddChannel(discordId, "TestDiscord", channel2);
                Assert.IsTrue(result.Success);
                Assert.AreEqual("Channel is added for Discord User TestDiscord", result.Message);
                Assert.IsTrue(ValorantChannelMappingsExtension.GetRowDiscordId(puuid).ToHashSet().Contains(channel2));
            }
            finally
            {
                CleanupUser(puuid, discordId);
            }
        }

        [TestMethod]
        public void AddChannel_SameChannelAgain_ReturnsError()
        {
            const string puuid = "10000000-0000-0000-0000-000000000003";
            const ulong discordId = 100003UL, channelId = 200003UL;
            SeedUser(puuid, "TestUser", "NA1", discordId, channelId);
            var (service, _) = CreateService();
            try
            {
                var result = service.AddChannel(discordId, "TestDiscord", channelId);
                Assert.IsFalse(result.Success);
                Assert.AreEqual("Channel is already present or could not be added for Discord User TestDiscord", result.Message);
            }
            finally
            {
                CleanupUser(puuid, discordId);
            }
        }

        #endregion

        #region RemoveChannel Tests

        [TestMethod]
        public void RemoveChannel_UnknownUser_ReturnsError()
        {
            var (service, _) = CreateService();
            var result = service.RemoveChannel(100004UL, "GhostUser", 200001UL);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("Could not find Valorant User for Discord User GhostUser", result.Message);
        }

        [TestMethod]
        public void RemoveChannel_UserNotInChannel_ReturnsError()
        {
            const string puuid = "10000000-0000-0000-0000-000000000005";
            const ulong discordId = 100005UL, userChannel = 200005UL, nonUserChannel = 200099UL;
            SeedUser(puuid, "TestUser", "NA1", discordId, userChannel);
            var (service, _) = CreateService();
            try
            {
                var result = service.RemoveChannel(discordId, "TestDiscord", nonUserChannel);
                Assert.IsFalse(result.Success);
                Assert.AreEqual("Channel is not present or could not be removed for Discord User TestDiscord", result.Message);
            }
            finally
            {
                CleanupUser(puuid, discordId);
            }
        }

        [TestMethod]
        public void RemoveChannel_UserWithTwoChannels_RemovesOneAndKeepsUser()
        {
            const string puuid = "10000000-0000-0000-0000-000000000006";
            const ulong discordId = 100006UL, channel1 = 200006UL, channel2 = 200007UL;
            SeedUser(puuid, "TestUser", "NA1", discordId, channel1, channel2);
            var (service, program) = CreateService();
            try
            {
                var result = service.RemoveChannel(discordId, "TestDiscord", channel1);
                Assert.IsTrue(result.Success);
                Assert.AreEqual("Channel is deleted for Discord User TestDiscord", result.Message);
                Assert.IsNotNull(ValorantUsersExtension.GetRow(puuid));
                Assert.IsNotNull(program.GetValorantUser(puuid));
                var remaining = ValorantChannelMappingsExtension.GetRowDiscordId(puuid).ToHashSet();
                Assert.IsFalse(remaining.Contains(channel1));
                Assert.IsTrue(remaining.Contains(channel2));
            }
            finally
            {
                CleanupUser(puuid, discordId);
            }
        }

        [TestMethod]
        public void RemoveChannel_UserWithOneChannel_RemovesChannelAndUnlinksUser()
        {
            const string puuid = "10000000-0000-0000-0000-000000000007";
            const ulong discordId = 100007UL, channelId = 200008UL;
            SeedUser(puuid, "TestUser", "NA1", discordId, channelId);
            var (service, program) = CreateService();
            try
            {
                var result = service.RemoveChannel(discordId, "TestDiscord", channelId);
                Assert.IsTrue(result.Success);
                Assert.AreEqual("Discord User TestDiscord is no longer associated to Shamebot.", result.Message);
                Assert.IsNull(ValorantUsersExtension.GetRow(puuid));
                Assert.IsNull(program.GetValorantUser(puuid));
            }
            finally
            {
                CleanupUser(puuid, discordId);
            }
        }

        #endregion

        #region AddUser Tests

        [TestMethod]
        [DataRow(null, "NA1")]
        [DataRow("", "NA1")]
        [DataRow("TestUser", null)]
        [DataRow("TestUser", "")]
        public void AddUser_NullOrEmptyUsernameOrTag_ReturnsError(string? username, string? tagname)
        {
            var (service, _) = CreateService();
            var result = service.AddUser(100008UL, username, tagname, 200001UL);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("Riot username or tagname cannot be empty", result.Message);
        }

        [TestMethod]
        public void AddUser_DiscordUserAlreadyLinked_ReturnsError()
        {
            const string puuid = "10000000-0000-0000-0000-000000000009";
            const ulong discordId = 100009UL, channelId = 200009UL;
            SeedUser(puuid, "OldName", "OldTag", discordId, channelId);
            var (service, _) = CreateService();
            try
            {
                var result = service.AddUser(discordId, "NewName", "NewTag", channelId);
                Assert.IsFalse(result.Success);
                Assert.AreEqual("Shamebot only accepts one valorant account per discord user!", result.Message);
            }
            finally
            {
                CleanupUser(puuid, discordId);
            }
        }

        [TestMethod]
        public void AddUser_HappyPath_CreatesUserAndMapping()
        {
            const string puuid = "10000000-0000-0000-0000-000000000010";
            const ulong discordId = 100010UL, channelId = 200010UL;
            const string username = "NewGamer", tagname = "NA1";
            var handler = new StubHttpMessageHandler(CreateAccountJson(puuid, username, tagname));
            var (service, program) = CreateService(handler);
            try
            {
                var result = service.AddUser(discordId, username, tagname, channelId);
                Assert.IsTrue(result.Success);
                Assert.AreEqual($"Valorant User {username}#{tagname} created!", result.Message);

                var userRow = ValorantUsersExtension.GetRow(puuid);
                Assert.IsNotNull(userRow);
                Assert.AreEqual(username, userRow.Val_username);
                Assert.AreEqual(tagname, userRow.Val_tagname);
                Assert.AreEqual(discordId, userRow.Disc_id);

                Assert.IsTrue(ValorantChannelMappingsExtension.GetRowDiscordId(puuid).ToHashSet().Contains(channelId));
                var programUser = program.GetValorantUser(puuid);
                Assert.IsNotNull(programUser);
                Assert.AreEqual(username, programUser.UserInfo.Val_username);
            }
            finally
            {
                CleanupUser(puuid, discordId);
            }
        }

        [TestMethod]
        public void AddUser_PuuidAlreadyLinkedToDifferentDiscordUser_ReturnsError()
        {
            const string puuid = "10000000-0000-0000-0000-000000000011";
            const ulong discordId1 = 100011UL, discordId2 = 100012UL, channelId = 200011UL;
            const string username = "SharedAcc", tagname = "NA1";
            SeedUser(puuid, username, tagname, discordId1, channelId);
            var handler = new StubHttpMessageHandler(CreateAccountJson(puuid, username, tagname));
            var (service, _) = CreateService(handler);
            try
            {
                var result = service.AddUser(discordId2, username, tagname, channelId);
                Assert.IsFalse(result.Success);
                Assert.AreEqual("Shamebot only accepts one instance of a valorant account! Please ensure no one else is using your account.", result.Message);
                Assert.IsNull(ValorantUsersExtension.GetRowDiscordId(discordId2));
            }
            finally
            {
                CleanupUser(puuid, discordId1);
                CleanupUser(puuid, discordId2);
            }
        }

        [TestMethod]
        public void AddUser_AccountLookupFails_ReturnsError()
        {
            const ulong discordId = 100013UL, channelId = 200013UL;
            var handler = new StubHttpMessageHandler(statusCode: HttpStatusCode.NotFound);
            var (service, _) = CreateService(handler);

            var result = service.AddUser(discordId, "NotFoundUser", "0000", channelId);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("Valorant user cannot be found or API failed to respond. Please check the user info again.", result.Message);
            Assert.IsNull(ValorantUsersExtension.GetRowDiscordId(discordId));
        }

        #endregion
    }
}
