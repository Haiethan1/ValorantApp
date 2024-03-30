using Discord;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ValorantApp.Database.Tables;
using ValorantApp.GenericExtensions;
using ValorantApp.Valorant.Enums;

namespace ValorantApp.Valorant
{
    public class BaseValorantMatch
    {
        public BaseValorantMatch(MatchStats matchStats, Matches matches, ValorantUsers userInfo, ILogger<BaseValorantProgram> logger)
        {
            MatchStats = matchStats;
            Matches = matches;
            UserInfo = userInfo;
            Logger = logger;
        }

        #region Globals

        public Matches Matches { get; private set; }

        public MatchStats MatchStats { get; private set; }

        public ValorantUsers UserInfo { get; private set; }

        private ILogger<BaseValorantProgram> Logger { get; set; }

        #endregion Globals

        #region Methods

        public void LogMatch()
        {
            string rounds = string.Equals(MatchStats.Team, "blue", StringComparison.InvariantCultureIgnoreCase)
                ? $"{Matches.Blue_Team_Rounds_Won ?? 0} : {Matches.Red_Team_Rounds_Won ?? 0}"
                : $"{Matches.Red_Team_Rounds_Won ?? 0} : {Matches.Blue_Team_Rounds_Won ?? 0}";
            string averageRank = string.Equals(MatchStats.Team, "blue", StringComparison.InvariantCultureIgnoreCase)
                ? $"{Matches.Blue_Team_Average_Rank ?? 0} : {Matches.Red_Team_Average_Rank ?? 0}"
                : $"{Matches.Red_Team_Average_Rank ?? 0} : {Matches.Blue_Team_Average_Rank ?? 0}";
            Logger.LogInformation($@"{nameof(LogMatch)}: Match data for {UserInfo.Val_username}#{UserInfo.Val_tagname} - 
                            Agent = {AgentsExtension.AgentFromString(MatchStats.Character).StringFromAgent()},
                            Map = {MapsExtension.MapFromString(Matches.Map.Safe()).StringFromMap()}, 
                            Team = {MatchStats.Team}
                            Rounds = {rounds}
                            Average Ranks = {averageRank}
                            Game_Start_patched = {Matches.Game_Start_Patched_UTC?.ToString("MMM. d\\t\\h, h:mm tt")},
                            Game_Length.TotalMinutes = {Math.Floor(TimeSpan.FromSeconds(Matches.Game_Length).TotalMinutes)},
                            Mode = {ModesExtension.ModeFromString(Matches.Mode.Safe().ToLower()).StringFromMode()},
                            Score / Rounds = {MatchStats.Score} / {Matches.Rounds_Played},
                            K/D/A = {MatchStats.Kills}/{MatchStats.Deaths}/{MatchStats.Assists},
                            MVP = {MatchStats.MVP},
                            Headshot = {MatchStats.Headshots:0.00}%,
                            RR = {MatchStats.Rr_change}");
        }

        #endregion Methods
    }
}
