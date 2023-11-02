using Moq;
using Moq.Protected;
using System.Configuration;
using System.Net;
using ValorantApp;

namespace ValorantAppTests
{
    [TestClass]
    public class HenrikApiTests
    {
        private readonly string _username = "Ehtan";
        private readonly string _tagName = "NA1";
        private readonly string _affinity = "na";
        private readonly string _puuid = "8c7276bd-a70b-56bf-803b-7e0fd9dba287";

        private MockRepository _mockRepository;

        private Mock<HttpMessageHandler> _handlerMock;
        private HttpClient _httpClient;

        [TestInitialize]
        public void Initialize()
        {
            _mockRepository = new(MockBehavior.Default);
            _handlerMock = _mockRepository.Create<HttpMessageHandler>();
        }

        private HenrikApi CreateHenrikApi()
        {
            _httpClient = new HttpClient(_handlerMock.Object);
            return new HenrikApi(_username, _tagName, _affinity, _puuid, _httpClient);
        }

        [TestMethod]
        public void AccountQueryTest_Success()
        {
            string endpoint = $"https://api.henrikdev.xyz/valorant/v1/account/{_username}/{_tagName}";
            // Arrange
            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri == new Uri(endpoint)),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(V1AccountMock)
                })
                .Verifiable();

            HenrikApi api = CreateHenrikApi();
            

            // Act
            var accountJson = api.AccountQuery()?.Result.Data;
            Assert.IsNotNull(accountJson);
            Assert.AreEqual(_puuid, accountJson.Puuid);
            Assert.AreEqual(_username, accountJson.Name);
            Assert.AreEqual(_tagName, accountJson.Tag);
            Assert.AreEqual(_affinity, accountJson.Region);
        }

        #region Api responses

        #region v1 Account

        private readonly string V1AccountMock = $@"
        {{
            ""status"": 200,
            ""data"": {{
                ""puuid"": ""8c7276bd-a70b-56bf-803b-7e0fd9dba287"",
                ""region"": ""na"",
                ""account_level"": 314,
                ""name"": ""Ehtan"",
                ""tag"": ""NA1"",
                ""card"": {{
                    ""small"": ""https://media.valorant-api.com/playercards/5a33d85c-42c9-9b75-44c3-e0a447a8a894/smallart.png"",
                    ""large"": ""https://media.valorant-api.com/playercards/5a33d85c-42c9-9b75-44c3-e0a447a8a894/largeart.png"",
                    ""wide"": ""https://media.valorant-api.com/playercards/5a33d85c-42c9-9b75-44c3-e0a447a8a894/wideart.png"",
                    ""id"": ""5a33d85c-42c9-9b75-44c3-e0a447a8a894""
                }},
                ""last_update"": ""Now"",
                ""last_update_raw"": 1698807788
            }}
        }}";

        #endregion

        #endregion
    }
}