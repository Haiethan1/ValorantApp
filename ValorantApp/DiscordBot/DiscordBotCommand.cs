using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;
using ValorantApp.GenericExtensions;

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
            ValorantUsers? valorantUserDB = ValorantUsersExtension.GetRowDiscordId(userInfo.Id);
            if (valorantUserDB == null)
            {
                await ReplyAsync($"Could not find Valorant User for Discord User {userInfo.Username}");
                return;
            }

            BaseValorantProgram program = _servicesProvider.GetRequiredService<BaseValorantProgram>();
            BaseValorantUser? valorantUser = program.GetValorantUser(valorantUserDB.Val_puuid);
            if (valorantUser == null)
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
                .WithDescription($"Current RR: {mmr.Current_Data.CurrentTier%100}")
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
    }
}
