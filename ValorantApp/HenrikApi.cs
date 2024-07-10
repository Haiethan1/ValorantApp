using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using ValorantApp.GenericExtensions;
using ValorantApp.Valorant;
using ValorantApp.Valorant.Enums;

namespace ValorantApp
{
    public class HenrikApi
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private ILogger<BaseValorantProgram> Logger { get; set; }

        public string username;
        public string tagName;
        public string affinity;
        public string puuid;

        private long DownloadSize { get; set; }

        public HenrikApi(string username, string tagName, string affinity, string? puuid, IHttpClientFactory httpClientFactory, ILogger<BaseValorantProgram> logger)
        {
            this.username = username;
            this.tagName = tagName;
            this.affinity = affinity;
            _httpClientFactory = httpClientFactory;
            Logger = logger;

            if (puuid != null)
            {
                this.puuid = puuid;
            }
            else
            {
                AccountJson? account = AccountQuery()?.Result.Data ?? null;
                if (account?.Puuid == null)
                {
                    throw new Exception($"Puuid cannot be null, user info: {username}#{tagName} {affinity}");
                }
                this.puuid = account.Puuid;
            }
            
        }

        public async Task<JsonObjectHenrik<AccountJson>>? AccountQuery()
        {
            string endpoint = $"v1/account/{username}/{tagName}";
            HttpClient httpClient = _httpClientFactory.CreateClient("HenrikApiClient");
            HttpResponseMessage response = await httpClient.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            LogRateLimitWarningAndErrors(response.Headers);
            return ParseAndLogJson<JsonObjectHenrik<AccountJson>>(response.Content.ReadAsStringAsync().Result, endpoint, nameof(AccountQuery));
        }

        public async Task<JsonObjectHenrik<MmrV2Json>>? Mmr()
        {
            string endpoint = $"v2/by-puuid/mmr/{affinity}/{puuid}";
            HttpClient httpClient = _httpClientFactory.CreateClient("HenrikApiClient");
            HttpResponseMessage response = await httpClient.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            LogRateLimitWarningAndErrors(response.Headers);
            return ParseAndLogJson<JsonObjectHenrik<MmrV2Json>>(response.Content.ReadAsStringAsync().Result, endpoint, nameof(Mmr));
        }

        public async Task<JsonObjectHenrik<List<MmrHistoryJson>>>? MmrHistory()
        {
            string endpoint = $"v1/by-puuid/mmr-history/{affinity}/{puuid}";
            HttpClient httpClient = _httpClientFactory.CreateClient("HenrikApiClient");
            HttpResponseMessage response = await httpClient.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            LogRateLimitWarningAndErrors(response.Headers);
            return ParseAndLogJson<JsonObjectHenrik<List<MmrHistoryJson>>>(response.Content.ReadAsStringAsync().Result, endpoint, nameof(MmrHistory));
        }

        public async Task<JsonObjectHenrik<List<MatchJson>>>? Match(Modes mode = Modes.Unknown, Maps map = Maps.Unknown, int size = 1)
        {
            string endpoint = $"v3/by-puuid/matches/{affinity}/{puuid}?";
            if (mode != Modes.Unknown)
            {
                endpoint += $"&mode={mode.StringFromMode()}";
            }
            if (map != Maps.Unknown)
            {
                endpoint += $"&map={map.StringFromMap()}";
            }
            endpoint += $"&size={size}";

            HttpClient httpClient = _httpClientFactory.CreateClient("HenrikApiClient");
            HttpResponseMessage response = await httpClient.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            LogRateLimitWarningAndErrors(response.Headers);
            return ParseAndLogJson<JsonObjectHenrik<List<MatchJson>>>(response.Content.ReadAsStringAsync().Result, endpoint, nameof(Match));
        }

        private T ParseAndLogJson<T>(string json, string endpoint, string caller) 
        {
            T? jsonObject = json.TryParse<T>(out string errormsg);

            if (jsonObject == null)
            {
                Logger.LogError($"{caller}: {errormsg} - {endpoint}");
            }

            return jsonObject;
        }

        private void LogRateLimitWarningAndErrors(HttpResponseHeaders responseHeaders)
        {
            // Check if the rate limit headers exist and retrieve them
            if (responseHeaders.TryGetValues("x-ratelimit-limit", out IEnumerable<string>? rateLimitValues) &&
                responseHeaders.TryGetValues("x-ratelimit-remaining", out IEnumerable<string>? rateRemainingValues) &&
                responseHeaders.TryGetValues("x-ratelimit-reset", out IEnumerable<string>? rateResetValues))
            {
                int rateLimit = int.Parse(rateLimitValues.First());
                int rateRemaining = int.Parse(rateRemainingValues.First());
                int rateReset = int.Parse(rateResetValues.First());
                int rateLimitTotal = rateLimit + rateRemaining;
                double percentRateLimit = rateLimit * 1.0 / rateLimitTotal;
                if (percentRateLimit >= 0.6)
                {
                    Logger.LogWarning($"Current {rateLimit} / Remaining {rateRemaining}. Usage at {percentRateLimit*100:00}% - reset in {rateReset}");
                }
                else if (percentRateLimit >= 0.8)
                {
                    Logger.LogError($"Current {rateLimit} / Remaining {rateRemaining}. Usage at {percentRateLimit * 100:00}% - reset in {rateReset}");
                }
            }
        }
    }
}