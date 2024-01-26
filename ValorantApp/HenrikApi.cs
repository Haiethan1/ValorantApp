
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net.Http.Headers;
using ValorantApp.GenericExtensions;
using ValorantApp.Valorant;
using ValorantApp.Valorant.Enums;

namespace ValorantApp
{
    public class HenrikApi
    {
        private HttpClient httpClient { get; set; }
        private ILogger<BaseValorantProgram> Logger { get; set; }

        public string username;
        public string tagName;
        public string affinity;
        public string puuid;

        public HenrikApi(string username, string tagName, string affinity, string? puuid, HttpClient HttpClient, string? apiToken, ILogger<BaseValorantProgram> logger)
        {
            this.username = username;
            this.tagName = tagName;
            this.affinity = affinity;
            httpClient = HttpClient;
            Logger = logger;

            if (apiToken != null && apiToken.Length > 0)
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", apiToken);
            }

            if (puuid != null)
            {
                this.puuid = puuid;
            }
            else
            {
                AccountJson? account = AccountQuery()?.Result.Data ?? null;
                if (account?.Puuid == null)
                {
                    throw new Exception("Puuid cannot be null");
                }
                this.puuid = account.Puuid;
            }
            
        }

        public async Task<JsonObjectHenrik<AccountJson>>? AccountQuery()
        {
            string url = $"https://api.henrikdev.xyz/valorant/v1/account/{username}/{tagName}";
            var response = await httpClient.GetAsync(url);

            Logger.ApiInformation(url + $". Response code {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<JsonObjectHenrik<AccountJson>>(response.Content.ReadAsStringAsync().Result);
        }

        public async Task<JsonObjectHenrik<MmrV2Json>>? Mmr()
        {
            string url = $"https://api.henrikdev.xyz/valorant/v2/by-puuid/mmr/{affinity}/{puuid}";
            var response = await httpClient.GetAsync(url);

            Logger.ApiInformation(url + $". Response code {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<JsonObjectHenrik<MmrV2Json>>(response.Content.ReadAsStringAsync().Result);
        }

        public async Task<JsonObjectHenrik<List<MmrHistoryJson>>>? MmrHistory()
        {
            string url = $"https://api.henrikdev.xyz/valorant/v1/by-puuid/mmr-history/{affinity}/{puuid}";
            var response = await httpClient.GetAsync(url);

            Logger.ApiInformation(url + $". Response code {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<JsonObjectHenrik<List<MmrHistoryJson>>>(response.Content.ReadAsStringAsync().Result);
        }

        public async Task<JsonObjectHenrik<List<MatchJson>>>? Match(Modes mode = Modes.Unknown, Maps map = Maps.Unknown, int size = 1)
        {
            string url = $"https://api.henrikdev.xyz/valorant/v3/by-puuid/matches/{affinity}/{puuid}?";
            if (mode != Modes.Unknown)
            {
                url += $"&mode={mode.StringFromMode()}";
            }
            if (map != Maps.Unknown)
            {
                url += $"&map={map.StringFromMap()}";
            }
            url += $"&size={size}";

            var response = await httpClient.GetAsync(url);

            Logger.ApiInformation(url + $". Response code {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<JsonObjectHenrik<List<MatchJson>>>(response.Content.ReadAsStringAsync().Result);
        }
    }
}