using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ValorantApp.ValorantEnum
{
    public enum Maps
    {
        #region Competitive Rotating

        [Description("Ascent")]
        Ascent,

        [Description("Split")]
        Split,

        [Description("Fracture")]
        Fracture,

        [Description("Bind")]
        Bind,

        [Description("Breeze")]
        Breeze,

        [Description("Lotus")]
        Lotus,

        [Description("Pearl")]
        Pearl,

        [Description("Icebox")]
        Icebox,

        [Description("Haven")]
        Haven,

        #endregion

        #region Team Deathmatch

        [Description("District")]
        District,

        [Description("Kasbah")]
        Kasbah,

        [Description("Piazza")]
        Piazza,
        #endregion

        #region Unknown

        [Description("Unknown")]
        Unknown,

        #endregion
    }

    public static class MapsExtension
    {
        public static Maps MapFromString(string str)
        {
            switch(str)
            {
                case "Ascent":
                    return Maps.Ascent;
                case "Split":
                    return Maps.Split;
                case "Fracture":
                    return Maps.Fracture;
                case "Bind":
                    return Maps.Bind;
                case "Breeze":
                    return Maps.Breeze;
                case "Lotus":
                    return Maps.Lotus;
                case "Pearl":
                    return Maps.Pearl;
                case "Icebox":
                    return Maps.Icebox;
                case "Haven":
                    return Maps.Haven;
                case "District":
                    return Maps.District;
                case "Kasbah":
                    return Maps.Kasbah;
                case "Piazza":
                    return Maps.Piazza;
                default:
                    return Maps.Unknown;
            }
        }

        public static string StringFromMap(this Maps map)
        {
            switch (map)
            {
                case Maps.Ascent:
                    return "Ascent";
                case Maps.Split:
                    return "Split";
                case Maps.Fracture:
                    return "Fracture";
                case Maps.Bind:
                    return "Bind";
                case Maps.Breeze:
                    return "Breeze";
                case Maps.Lotus:
                    return "Lotus";
                case Maps.Pearl:
                    return "Pearl";
                case Maps.Icebox:
                    return "Icebox";
                case Maps.Haven:
                    return "Haven";
                case Maps.District:
                    return "District";
                case Maps.Kasbah:
                    return "Kasbah";
                case Maps.Piazza:
                    return "Piazza";
                default:
                    return "";
            }
        }
    }
}