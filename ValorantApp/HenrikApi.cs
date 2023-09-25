﻿
using Newtonsoft.Json;
using System.Collections.Specialized;
using System.Net.Http;
using ValorantApp;
using ValorantApp.Database.Tables;
using ValorantApp.HenrikJson;
using ValorantApp.ValorantEnum;
using ValorantNET.Models;

namespace ValorantApp
{
    public class HenrikApi
    {
        private HttpClient httpClient = new HttpClient();


        public string username;
        public string tagName;
        public string affinity;
        public string puuid;

        public HenrikApi(string username, string tagName, string affinity, string? puuid)
        {
            this.username = username;
            this.tagName = tagName;
            this.affinity = affinity;

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

        private async Task<JsonObjectHenrik<AccountJson>>? AccountQuery()
        {
            var response = await httpClient.GetAsync($"https://api.henrikdev.xyz/valorant/v1/account/{username}/{tagName}");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<JsonObjectHenrik<AccountJson>>(response.Content.ReadAsStringAsync().Result);
        }

        public async Task<JsonObjectHenrik<MmrJson>>? Mmr()
        {
            var response = await httpClient.GetAsync($"https://api.henrikdev.xyz/valorant/v1/by-puuid/mmr/{affinity}/{puuid}");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<JsonObjectHenrik<MmrJson>>(response.Content.ReadAsStringAsync().Result);
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