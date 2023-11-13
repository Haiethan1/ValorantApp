using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ValorantApp.Valorant.Enums
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

        [Description("Sunset")]
        Sunset,

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
            switch (str)
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
                case "Sunset":
                    return Maps.Sunset;
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
                case Maps.Sunset:
                    return "Sunset";
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

        public static string ImageUrlFromMap(this Maps map)
        {
            switch (map)
            {
                case Maps.Ascent:
                    return "https://static.wikia.nocookie.net/valorant/images/e/e7/Loading_Screen_Ascent.png/revision/latest/scale-to-width-down/1000?cb=20200607180020";
                case Maps.Split:
                    return "https://static.wikia.nocookie.net/valorant/images/d/d6/Loading_Screen_Split.png/revision/latest/scale-to-width-down/1000?cb=20230411161807";
                case Maps.Fracture:
                    return "https://static.wikia.nocookie.net/valorant/images/f/fc/Loading_Screen_Fracture.png/revision/latest/scale-to-width-down/1000?cb=20210908143656";
                case Maps.Bind:
                    return "https://static.wikia.nocookie.net/valorant/images/2/23/Loading_Screen_Bind.png/revision/latest/scale-to-width-down/1000?cb=20200620202316";
                case Maps.Breeze:
                    return "https://static.wikia.nocookie.net/valorant/images/1/10/Loading_Screen_Breeze.png/revision/latest/scale-to-width-down/1000?cb=20210427160616";
                case Maps.Lotus:
                    return "https://static.wikia.nocookie.net/valorant/images/d/d0/Loading_Screen_Lotus.png/revision/latest/scale-to-width-down/1000?cb=20230106163526";
                case Maps.Pearl:
                    return "https://static.wikia.nocookie.net/valorant/images/a/af/Loading_Screen_Pearl.png/revision/latest/scale-to-width-down/1000?cb=20220622132842";
                case Maps.Icebox:
                    return "https://static.wikia.nocookie.net/valorant/images/1/13/Loading_Screen_Icebox.png/revision/latest/scale-to-width-down/1000?cb=20201015084446";
                case Maps.Haven:
                    return "https://static.wikia.nocookie.net/valorant/images/7/70/Loading_Screen_Haven.png/revision/latest/scale-to-width-down/1000?cb=20200620202335";
                case Maps.Sunset:
                    return "https://static.wikia.nocookie.net/valorant/images/5/5c/Loading_Screen_Sunset.png/revision/latest/scale-to-width-down/1000?cb=20230829125442";
                case Maps.District:
                    return "https://static.wikia.nocookie.net/valorant/images/c/ca/District_Splash.png/revision/latest/scale-to-width-down/1000?cb=20230627133038";
                case Maps.Kasbah:
                    return "https://static.wikia.nocookie.net/valorant/images/7/7f/Kasbah_Splash.png/revision/latest/scale-to-width-down/1000?cb=20230627133041";
                case Maps.Piazza:
                    return "https://static.wikia.nocookie.net/valorant/images/e/e2/Piazza_Splash.png/revision/latest/scale-to-width-down/1000?cb=20230627133044";
                default:
                    return "";
            }
        }
    }
}