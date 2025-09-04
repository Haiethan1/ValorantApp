using System.ComponentModel;

namespace ValorantApp.Valorant.Enums
{
    public enum Team
    {
        [Description("Unknown")]
        Unknown,

        [Description("Blue")]
        Blue,

        [Description("Red")]
        Red,
    }

    public static class TeamExtension
    {
        public static Team TeamFromString(string str)
        {
            return str.ToLower().Replace(" ", "") switch
            {
                "blue" => Team.Blue,
                "red" => Team.Red,
                _ => Team.Unknown,
            };
        }

        public static string StringFromTeam(this Team team)
        {
            return team switch
            {
                Team.Blue => "Blue",
                Team.Red => "Red",
                _ => "",
            };
        }
    }
}
