using System.ComponentModel;

namespace ValorantApp.Valorant.Enums
{
    public enum Modes
    {
        [Description("Competitive")]
        Competitive,

        [Description("Unrated")]
        Unrated,

        [Description("Custom")]
        Custom,

        [Description("Deathmatch")]
        Deathmatch,

        [Description("Escalation")]
        Escalation,

        [Description("TeamDeathmatch")]
        TeamDeathmatch,

        [Description("NewMap")]
        NewMap,

        [Description("Replication")]
        Replication,

        [Description("SnowBallFight")]
        SnowBallFight,

        [Description("Spikerush")]
        Spikerush,

        [Description("SwiftPlay")]
        SwiftPlay,

        [Description("Premier")]
        Premier,


        [Description("Unknown")]
        Unknown,
    }

    public static class ModesExtension
    {
        public static Modes ModeFromString(string str)
        {
            switch (str)
            {
                case "competitive":
                    return Modes.Competitive;
                case "unrated":
                    return Modes.Unrated;
                case "custom":
                    return Modes.Custom;
                case "deathmatch":
                    return Modes.Deathmatch;
                case "escalation":
                    return Modes.Escalation;
                case "teamdeathmatch":
                    return Modes.TeamDeathmatch;
                case "newmap":
                    return Modes.NewMap;
                case "replication":
                    return Modes.Replication;
                case "snowballfight":
                    return Modes.SnowBallFight;
                case "spikerush":
                    return Modes.Spikerush;
                case "swiftplay":
                    return Modes.SwiftPlay;
                case "premier":
                    return Modes.Premier;
                default:
                    return Modes.Unknown;
            }
        }

        public static string StringFromMode(this Modes mode)
        {
            switch (mode)
            {
                case Modes.Competitive:
                    return "Competitive";
                case Modes.Unrated:
                    return "Unrated";
                case Modes.Custom:
                    return "Custom";
                case Modes.Deathmatch:
                    return "Deathmatch";
                case Modes.Escalation:
                    return "Escalation";
                case Modes.TeamDeathmatch:
                    return "TeamDeathmatch";
                case Modes.NewMap:
                    return "NewMap";
                case Modes.Replication:
                    return "Replication";
                case Modes.SnowBallFight:
                    return "SnowBallFight";
                case Modes.Spikerush:
                    return "Spikerush";
                case Modes.SwiftPlay:
                    return "SwiftPlay";
                case Modes.Premier:
                    return "Premier";
                default:
                    return "";
            }
        }
    }
}