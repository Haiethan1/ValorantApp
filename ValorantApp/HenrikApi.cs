
using Newtonsoft.Json;
using ValorantApp.Valorant.Enums;

namespace ValorantApp
{
    public class HenrikApi
    {
        private HttpClient httpClient;


        public string username;
        public string tagName;
        public string affinity;
        public string puuid;

        public HenrikApi(string username, string tagName, string affinity, string? puuid, HttpClient HttpClient)
        {
            this.username = username;
            this.tagName = tagName;
            this.affinity = affinity;
            this.httpClient = HttpClient;

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
            var response = await httpClient.GetAsync($"https://api.henrikdev.xyz/valorant/v1/account/{username}/{tagName}");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<JsonObjectHenrik<AccountJson>>(response.Content.ReadAsStringAsync().Result);
        }

        public async Task<JsonObjectHenrik<MmrV2Json>>? Mmr()
        {
            var response = await httpClient.GetAsync($"https://api.henrikdev.xyz/valorant/v2/by-puuid/mmr/{affinity}/{puuid}");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<JsonObjectHenrik<MmrV2Json>>(response.Content.ReadAsStringAsync().Result);
        }

        public async Task<JsonObjectHenrik<List<MmrHistoryJson>>>? MmrHistory()
        {
            var response = await httpClient.GetAsync($"https://api.henrikdev.xyz/valorant/v1/by-puuid/mmr-history/{affinity}/{puuid}");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<JsonObjectHenrik<List<MmrHistoryJson>>>(response.Content.ReadAsStringAsync().Result);
        }

        public async Task<JsonObjectHenrik<List<MatchJson>>>? Match(Modes mode = Modes.Unknown, Maps map = Maps.Unknown, int size = 1)
        {
            string query = $"https://api.henrikdev.xyz/valorant/v3/by-puuid/matches/{affinity}/{puuid}?";
            if (mode != Modes.Unknown)
            {
                query += $"&mode={mode.StringFromMode()}";
            }
            if (map != Maps.Unknown)
            {
                query += $"&map={map.StringFromMap()}";
            }
            query += $"&size={size}";

            var response = await httpClient.GetAsync(query);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<JsonObjectHenrik<List<MatchJson>>>(response.Content.ReadAsStringAsync().Result);
        }
    }
}