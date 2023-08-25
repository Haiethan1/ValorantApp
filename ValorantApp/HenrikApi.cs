
using Newtonsoft.Json;
using System.Collections.Specialized;
using System.Net.Http;
using ValorantApp;
using ValorantApp.ValorantEnum;

namespace ValorantApp
{
    public class HenrikApi
    {
        private HttpClient httpClient = new HttpClient();


        private string username;
        private string tagName;
        private string affinity;
        private string? puuid;

        public HenrikApi(string username, string tagName, string affinity)
        {
            this.username = username;
            this.tagName = tagName;
            this.affinity = affinity;

            var account = AccountQuery().Result.Data;
            if (account?.Puuid == null)
            {
                throw new Exception("Puuid cannot be null");
            }

            puuid = account.Puuid;
        }

        private async Task<JsonObjectHenrik<AccountJson>> AccountQuery()
        {
            var response = await httpClient.GetStringAsync($"https://api.henrikdev.xyz/valorant/v1/account/{username}/{tagName}");
            return JsonConvert.DeserializeObject<JsonObjectHenrik<AccountJson>>(response);
        }

        public async Task<JsonObjectHenrik<MmrJson>> Mmr()
        {
            var response = await httpClient.GetStringAsync($"https://api.henrikdev.xyz/valorant/v1/by-puuid/mmr/{affinity}/{puuid}");
            return JsonConvert.DeserializeObject<JsonObjectHenrik<MmrJson>>(response);
        }

        public async Task<JsonObjectHenrik<List<MatchJson>>> Match(Modes mode = Modes.Unknown, Maps map = Maps.Unknown, int size = 1)
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
            //var query = new UriBuilder($"https://api.henrikdev.xyz/valorant/v3/by-puuid/matches/{affinity}/{puuid}?size=1");
            //var queryParam = new Dictionary<string, string>
            //{
            //    { "map", "Ascent" },
            //    { "size", "1" },
            //};
            //NameValueCollection collection = new NameValueCollection();
            //collection.Add("map", "Ascent");
            //collection.Add("size", "1");
            //query.Query = collection.ToString();
            var response = await httpClient.GetStringAsync(query);

            return JsonConvert.DeserializeObject<JsonObjectHenrik<List<MatchJson>>>(response);
        }
    }
}