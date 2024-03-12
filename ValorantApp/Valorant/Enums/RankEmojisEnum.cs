using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public static string EmojiIdFromEnum(this RankEmojis emoji)
        {
            switch(emoji)
            {
                case RankEmojis.Iron1:
                    return ":iro1:1214384551418269737";
                case RankEmojis.Iron2:
                    return ":iro2:1214384552269578332";
                case RankEmojis.Iron3:
                    return ":iro3:1214384325815181332";
                case RankEmojis.Bronze1:
                    return ":bro1:1214384603939217490";
                case RankEmojis.Bronze2:
                    return ":bro2:1214384338775449692";
                case RankEmojis.Bronze3:
                    return ":bro3:1214384342105587832";
                case RankEmojis.Silver1:
                    return ":sil1:1214384663326629978";
                case RankEmojis.Silver2:
                    return ":sil2:1214384360166268998";
                case RankEmojis.Silver3:
                    return ":sil3:1214384331406180383";
                case RankEmojis.Gold1:
                    return ":gol1:1214384711665844234";
                case RankEmojis.Gold2:
                    return ":gol2:1214384326691790908";
                case RankEmojis.Gold3:
                    return ":gol3:1214384330323795978";
                case RankEmojis.Plat1:
                    return ":pla1:1214384345750573157";
                case RankEmojis.Plat2:
                    return ":pla2:1214384329006911518";
                case RankEmojis.Plat3:
                    return ":pla3:1214384349076529202";
                case RankEmojis.Diamond1:
                    return ":dia1:1214384772147581008";
                case RankEmojis.Diamond2:
                    return ":dia2:1214384335063490620";
                case RankEmojis.Diamond3:
                    return ":dia3:1214384356169351230";
                case RankEmojis.Ascendant1:
                    return ":asc1:1214384834223276092";
                case RankEmojis.Ascendant2:
                    return ":asc2:1214384834936569898";
                case RankEmojis.Ascendant3:
                    return ":asc3:1214384327664861184";
                case RankEmojis.Immortal1:
                    return ":imm1:1214384324175069235";
                case RankEmojis.Immortal2:
                    return ":imm2:1214384324913266749";
                case RankEmojis.Immortal3:
                    return ":imm3:1214384959251419136";
                case RankEmojis.Radiant:
                    return ":rad:1214384352432226385";
                default:
                    return ":unr:1214734089165475850";
            }
        }
    }
}
