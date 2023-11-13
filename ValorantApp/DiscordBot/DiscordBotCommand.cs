using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;
using ValorantApp.GenericExtensions;
using ValorantApp.Valorant;

namespace ValorantApp.DiscordBot
{
    public class ValorantModule : ModuleBase<SocketCommandContext>
    {
        private DiscordSocketClient _client;
        private CommandService _commands;
        private IServiceProvider _servicesProvider;
        public ValorantModule(DiscordSocketClient client, CommandService commands, IServiceProvider servicesProvider)
        {
            _client = client;
            _commands = commands;
            _servicesProvider = servicesProvider;
        }

        [Command("Hello")]
        [Summary("Tester hello world")]
        public async Task HeadshotCommand()
        {
            string result = "HelloWorld!";
            await ReplyAsync(result);
        }

        [Command("mmr")]
        [Summary("Get the mmr of the user")]
        public async Task GetMMROfDiscordUser()
        {
            SocketUser userInfo = Context.User;

            if (!GetUserAndProgram(userInfo, out BaseValorantProgram? program, out BaseValorantUser? valorantUser) || program == null || valorantUser == null)
            {
                await ReplyAsync($"Could not find Valorant User for Discord User {userInfo.Username}");
                return;
            }

            MmrV2Json? mmr = valorantUser.GetMMR();
            if (mmr == null)
            {
                await ReplyAsync($"Could not find mmr stats for Discord User {userInfo.Username}");
                return;
            }
            var embed = new EmbedBuilder()
                .WithThumbnailUrl($"{mmr.Current_Data.Images?.Small.Safe() ?? ""}")
                .WithAuthor
                (new EmbedAuthorBuilder
                    {
                        Name = $"{valorantUser.UserInfo.Val_username}#{valorantUser.UserInfo.Val_tagname}"
                    }
                )
                .WithTitle(mmr.Current_Data.CurrentTierPatched.Safe())
                .WithDescription($"Current RR: {mmr.Current_Data.Ranking_In_Tier%100}")
                .WithFooter
                (new EmbedFooterBuilder
                    {
                        Text = $"RR Change to last game: {mmr.Current_Data.Mmr_Change_To_Last_Game}"
                    }
                );
            await ReplyAsync(embed: embed.Build());
        }

        [Command("AddMe")]
        [Summary("Add user")]
        public async Task AddUser(
            [Summary("The riotID")] string riotID
            )
        {
            string result, username, tagname;
            if(!IsUserAndTag(riotID, out username, out tagname))
            {
                result = "Valorant user was unable to be created";
                await ReplyAsync(result);
                return;
            }

            SocketUser userInfo = Context.User;
            string? puuid = BaseValorantUser.CreateUser(username, tagname, "na", userInfo.Id)?.Puuid;
            
            if (puuid == null)
            {
                result = "Valorant user was unable to be created";
                await ReplyAsync(result);
                return;
            }

            BaseValorantProgram program = _servicesProvider.GetRequiredService<BaseValorantProgram>();

            program.ReloadFromDB();
            var user = program.GetValorantUser(puuid);

            if (user == null)
            {
                result = "Valorant user was unable to be created";
                await ReplyAsync(result);
                return;
            }

            result = $"Valorant User {user.UserInfo.Val_username}#{user.UserInfo.Val_tagname} created!";
            await ReplyAsync(result);
        }

        private bool IsUserAndTag(string riotID, out string username, out string tagname)
        {
            username = "";
            tagname = "";
            if (riotID == null)
            {
                return false;
            }

            int splitHashTag = riotID.IndexOf("#");
            if (splitHashTag < 0)
            {
                return false;
            }

            username = riotID.Substring(0, splitHashTag);
            tagname = riotID.Substring(splitHashTag + 1);

            return true;
        }

        private bool GetUserAndProgram(SocketUser userInfo, out BaseValorantProgram? program, out BaseValorantUser? valorantUser)
        {
            program = null;
            valorantUser = null;

            ValorantUsers? valorantUserDB = ValorantUsersExtension.GetRowDiscordId(userInfo.Id);
            if (valorantUserDB == null)
            {
                return false;
            }

            program = _servicesProvider.GetRequiredService<BaseValorantProgram>();
            valorantUser = program.GetValorantUser(valorantUserDB.Val_puuid);
            if (valorantUser == null)
            {
                return false;
            }

            return true;
        }

        #region Testing

        [Summary("Developer only delete last match")]
        private async Task GetLastMatch()
        {
            SocketUser userInfo = Context.User;
            if (userInfo.Id != 158031143231422466)
            {
                return;
            }

            if (!GetUserAndProgram(userInfo, out BaseValorantProgram? program, out BaseValorantUser? valorantUser) || program == null || valorantUser == null)
            {
                await ReplyAsync($"Could not find Valorant User for Discord User {userInfo.Username}");
                return;
            }

            Dictionary<string, MatchStats> matchStats;
            program.UpdateMatchAllUsers(out matchStats);
            if (matchStats == null)
            {
                await ReplyAsync("No match stats were updated");
                return;
            }

            var embed = new EmbedBuilder()
                .WithThumbnailUrl("https://static.wikia.nocookie.net/valorant/images/f/fe/Neon_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202800")
                .WithAuthor
                (new EmbedAuthorBuilder
                {
                    Name = "DATE -- November 7th, 2023"
                }
                )
                .WithTitle($"{matchStats.First().Value.Map}")
                .WithDescription($"<@{userInfo.Id}> Match data {matchStats.First().Value.Match_id}")
                .AddField($"Ehtan", "KDA, combat, headshot, rr change", inline: false)
                .WithFooter
                (new EmbedFooterBuilder
                {
                    Text = $"Ethan's testing :)))))"
                }
                );
            await ReplyAsync(embed: embed.Build());
            var embed1 = new EmbedBuilder()
                .WithThumbnailUrl("https://static.wikia.nocookie.net/valorant/images/7/7f/Skye_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202828")
                .WithAuthor
                (new EmbedAuthorBuilder
                {
                    Name = "DATE -- November 7th, 2023"
                }
                )
                .WithTitle($"{matchStats.First().Value.Map}")
                .WithDescription($"Match data {matchStats.First().Value.Match_id}")
                .AddField($"Tokage", "KDA, combat, headshot, rr change", inline: false)
                .WithFooter
                (new EmbedFooterBuilder
                {
                    Text = "Ethan's testing :)))))"
                }
                );
            await ReplyAsync(embed: embed1.Build());
            var embed2 = new EmbedBuilder()
                .WithThumbnailUrl("https://static.wikia.nocookie.net/valorant/images/2/24/Breach_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202713")
                .WithAuthor
                (new EmbedAuthorBuilder
                {
                    Name = "DATE -- November 7th, 2023"
                }
                )
                .WithTitle($"{matchStats.First().Value.Map}")
                .WithDescription($"Match data {matchStats.First().Value.Match_id}")
                .AddField($"bot1", "KDA, combat, headshot, rr change", inline: false)
                .WithFooter
                (new EmbedFooterBuilder
                {
                    Text = "Ethan's testing :)))))"
                }
                );
            await ReplyAsync(embed: embed2.Build());
            var embed3 = new EmbedBuilder()
                .WithThumbnailUrl("https://static.wikia.nocookie.net/valorant/images/1/1e/Yoru_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202841")
                .WithAuthor
                (new EmbedAuthorBuilder
                {
                    Name = "DATE -- November 7th, 2023"
                }
                )
                .WithTitle($"{matchStats.First().Value.Map}")
                .WithDescription($"Match data {matchStats.First().Value.Match_id}")
                .AddField($"bot2", "KDA, combat, headshot, rr change", inline: false)
                .WithFooter
                (new EmbedFooterBuilder
                {
                    Text = "Ethan's testing :)))))"
                }
                );
            await ReplyAsync(embed: embed3.Build());
            var embed4 = new EmbedBuilder()
                .WithThumbnailUrl("https://static.wikia.nocookie.net/valorant/images/6/6f/Raze_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202815")
                .WithAuthor
                (new EmbedAuthorBuilder
                {
                    Name = "DATE -- November 7th, 2023"
                }
                )
                .WithTitle($"{matchStats.First().Value.Map}")
                .WithDescription($"Match data {matchStats.First().Value.Match_id}")
                .AddField($"bot3", "KDA, combat, headshot, rr change", inline: false)
                .WithFooter
                (new EmbedFooterBuilder
                {
                    Text = "Ethan's testing :)))))"
                }
                );
            await ReplyAsync(embed: embed4.Build());

            //var embed = new EmbedBuilder()
            //    .WithThumbnailUrl("https://static.wikia.nocookie.net/valorant/images/2/23/Loading_Screen_Bind.png/revision/latest/scale-to-width-down/200?cb=20200620202316")
            //    .WithAuthor
            //    (new EmbedAuthorBuilder
            //    {
            //        Name = "DATE -- November 7th, 2023"
            //    }
            //    )
            //    .WithTitle($"{matchStats.First().Value.Map}")
            //    .WithDescription($"Match data {matchStats.First().Value.Match_id}")
            //    .AddField($"Ehtan", "KDA, combat, headshot, rr change", inline: false)
            //    .AddField("Tokage", "KDA, combat, headshot, rr change", inline: false)
            //    .AddField($"bot1", "KDA, combat, headshot, rr change", inline: false)
            //    .AddField("bot2", "KDA, combat, headshot, rr change", inline: false)
            //    .AddField($"bot3", "KDA, combat, headshot, rr change", inline: false)
            //    .WithFooter
            //    (new EmbedFooterBuilder
            //    {
            //        Text = "Ethan's testing :)))))"
            //    }
            //    );
            //embed.Fields[0].

            //await ReplyAsync(embed: embed.Build());
        }

        [Summary("Developer only delete last match")]
        private async Task DeleteLastMatch()
        {
            SocketUser userInfo = Context.User;
            if (userInfo.Id != 158031143231422466)
            {
                return;
            }

            if (!GetUserAndProgram(userInfo, out BaseValorantProgram? program, out BaseValorantUser? valorantUser) || program == null || valorantUser == null)
            {
                await ReplyAsync($"Could not find Valorant User for Discord User {userInfo.Username}");
                return;
            }

            MatchJson? lastMatch = valorantUser.GetLastMatch();
            if (lastMatch == null)
            {
                await ReplyAsync("Could not find last match");
                return;
            }

            await ReplyAsync(MatchStatsExtension.DeleteMatch(lastMatch?.Metadata?.MatchId ?? "") ? "Deleted last match" : "Could not find last match in DB");
        }

        #endregion
    }
}
