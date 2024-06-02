using System.ComponentModel;

namespace ValorantApp.Valorant.Enums
{
    public enum RankEmojis
    {
        [Description("Iron 1")]
        Iron1 = 3,
        [Description("Iron 2")]
        Iron2 = 4,
        [Description("Iron 3")]
        Iron3 = 5,
        [Description("Bronze 1")]
        Bronze1 = 6,
        [Description("Bronze 2")]
        Bronze2 = 7,
        [Description("Bronze 3")]
        Bronze3 = 8,
        [Description("Silver 1")]
        Silver1 = 9,
        [Description("Silver 2")]
        Silver2 = 10,
        [Description("Silver 3")]
        Silver3 = 11,
        [Description("Gold 1")]
        Gold1 = 12,
        [Description("Gold 2")]
        Gold2 = 13,
        [Description("Gold 3")]
        Gold3 = 14,
        [Description("Plat 1")]
        Plat1 = 15,
        [Description("Plat 2")]
        Plat2 = 16,
        [Description("Plat 3")]
        Plat3 = 17,
        [Description("Diamond 1")]
        Diamond1 = 18,
        [Description("Diamond 2")]
        Diamond2 = 19,
        [Description("Diamond 3")]
        Diamond3 = 20,
        [Description("Ascendant 1")]
        Ascendant1 = 21,
        [Description("Ascendant 2")]
        Ascendant2 = 22,
        [Description("Ascendant 3")]
        Ascendant3 = 23,
        [Description("Immortal 1")]
        Immortal1 = 24,
        [Description("Immortal 2")]
        Immortal2 = 25,
        [Description("Immortal 3")]
        Immortal3 = 26,
        [Description("Radiant")]
        Radiant = 27,
    }

    public static class RankEmojisEnumExtensions
    {
        /// <summary>
        /// This method will be hard coded values of emoji's
        /// </summary>
        /// <param name="emoji"></param>
        /// <returns></returns>
        public static string Id(this RankEmojis emoji)
        {
            return emoji switch
            {
                RankEmojis.Iron1 => "<:iro1:1214384551418269737>",
                RankEmojis.Iron2 => "<:iro2:1214384552269578332>",
                RankEmojis.Iron3 => "<:iro3:1214384325815181332>",
                RankEmojis.Bronze1 => "<:bro1:1214384603939217490>",
                RankEmojis.Bronze2 => "<:bro2:1214384338775449692>",
                RankEmojis.Bronze3 => "<:bro3:1214384342105587832>",
                RankEmojis.Silver1 => "<:sil1:1214384663326629978>",
                RankEmojis.Silver2 => "<:sil2:1214384360166268998>",
                RankEmojis.Silver3 => "<:sil3:1214384331406180383>",
                RankEmojis.Gold1 => "<:gol1:1214384711665844234>",
                RankEmojis.Gold2 => "<:gol2:1214384326691790908>",
                RankEmojis.Gold3 => "<:gol3:1214384330323795978>",
                RankEmojis.Plat1 => "<:pla1:1214384345750573157>",
                RankEmojis.Plat2 => "<:pla2:1214384329006911518>",
                RankEmojis.Plat3 => "<:pla3:1214384349076529202>",
                RankEmojis.Diamond1 => "<:dia1:1214384772147581008>",
                RankEmojis.Diamond2 => "<:dia2:1214384335063490620>",
                RankEmojis.Diamond3 => "<:dia3:1214384356169351230>",
                RankEmojis.Ascendant1 => "<:asc1:1214384834223276092>",
                RankEmojis.Ascendant2 => "<:asc2:1214384834936569898>",
                RankEmojis.Ascendant3 => "<:asc3:1214384327664861184>",
                RankEmojis.Immortal1 => "<:imm1:1214384324175069235>",
                RankEmojis.Immortal2 => "<:imm2:1214384324913266749>",
                RankEmojis.Immortal3 => "<:imm3:1214384959251419136>",
                RankEmojis.Radiant => "<:rad:1214384352432226385>",
                _ => "<:unr:1214734089165475850>",
            };
        }
    }
}
