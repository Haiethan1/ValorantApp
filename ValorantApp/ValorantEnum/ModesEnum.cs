using System.ComponentModel;

namespace ValorantApp.ValorantEnum
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


        [Description("Unknown")]
        Unknown,
    }

    public static class ModesExtension
    {
        public static Modes ModeFromString(string str)
        {
            switch(str)
            {
                case "Competitive":
                    return Modes.Competitive;
                case "Unrated":
                    return Modes.Unrated;
                case "Custom":
                    return Modes.Custom;
                case "Deathmatch":
                    return Modes.Deathmatch;
                case "Escalation":
                    return Modes.Escalation;
                case "TeamDeathmatch":
                    return Modes.TeamDeathmatch;
                case "NewMap":
                    return Modes.NewMap;
                case "Replication":
                    return Modes.Replication;
                case "SnowBallFight":
                    return Modes.SnowBallFight;
                case "Spikerush":
                    return Modes.Spikerush;
                case "SwiftPlay":
                    return Modes.SwiftPlay;
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
                default:
                    return "";
            }
        }
    }
}