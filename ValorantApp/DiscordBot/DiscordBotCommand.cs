using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ValorantApp.HenrikJson;

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
