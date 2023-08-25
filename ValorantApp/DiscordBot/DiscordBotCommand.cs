using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ValorantApp.DiscordBot
{
    public class ValorantModule : ModuleBase<SocketCommandContext>
    {
        [Command("Headshot")]
        [Summary("Prints the headshots of all players in order.")]
        public async Task HeadshotCommand()
        {
            HenrikApi henrikApi = new HenrikApi("Ehtan", "NA1", "na");

            var temp = henrikApi.Match().Result.Data;
            //var highestHeadshot = temp[0].Players.All_Players[0];

            List<MatchPlayerJson> matchPlayers = new();

            foreach (var player in temp[0].Players.All_Players)
            {
                if (DiscordPlayerNames.PlayerNames.Any(x => string.Equals(x, player.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    matchPlayers.Add(player);
                }
            }

            string result = "";
            foreach (var player in matchPlayers)
            {
                var stats = player.Stats;
                double headshot = stats.Headshots / (double)(stats.Headshots + stats.Bodyshots + stats.Legshots) * 100.0;
                result += $"{player.Name} Headshot = {headshot.ToString("F")}%\n";
                //var statsMax = highestHeadshot.Stats;
                //double headshotMax = statsMax.Headshots / (double)(statsMax.Headshots + statsMax.Bodyshots + statsMax.Legshots) * 100.0;

                //highestHeadshot = headshot > headshotMax ? player : highestHeadshot;
                //Console.WriteLine($"{player.Name} Headshot = {headshot.ToString("F")}%");
            }

            await ReplyAsync(result);
        }
    }
}
