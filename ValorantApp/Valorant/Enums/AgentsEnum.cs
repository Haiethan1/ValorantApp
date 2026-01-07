using System.ComponentModel;

namespace ValorantApp.Valorant.Enums
{
    public enum Agents
    {
        [Description("Unknown")]
        Unknown,

        [Description("Brimstone")]
        Brimstone,

        [Description("Viper")]
        Viper,

        [Description("Omen")]
        Omen,

        [Description("Killjoy")]
        Killjoy,

        [Description("Cypher")]
        Cypher,

        [Description("Sova")]
        Sova,

        [Description("Sage")]
        Sage,

        [Description("Phoenix")]
        Phoenix,

        [Description("Jett")]
        Jett,

        [Description("Reyna")]
        Reyna,

        [Description("Raze")]
        Raze,

        [Description("Breach")]
        Breach,

        [Description("Skye")]
        Skye,

        [Description("Yoru")]
        Yoru,

        [Description("Astra")]
        Astra,

        [Description("KAY/O")]
        Kayo,

        [Description("Chamber")]
        Chamber,

        [Description("Neon")]
        Neon,

        [Description("Fade")]
        Fade,

        [Description("Harbor")]
        Harbor,

        [Description("Gekko")]
        Gekko,

        [Description("Deadlock")]
        Deadlock,

        [Description("Iso")]
        Iso,

        [Description("Clove")]
        Clove,

        [Description("Vyse")]
        Vyse,

        [Description("Tejo")]
        Tejo,

        [Description("Waylay")]
        Waylay,

        [Description("Veto")]
        Veto
    }

    public static class AgentsExtension
    {
        public static Agents AgentFromString(string str)
        {
            foreach (Agents agent in Enum.GetValues(typeof(Agents)))
            {
                if (agent.ToDescriptionString().Equals(str, StringComparison.OrdinalIgnoreCase))
                {
                    return agent;
                }
            }

            return Agents.Unknown;
        }

        public static string StringFromAgent(this Agents agent)
        {
            return agent.ToDescriptionString();
        }

        /// <summary>
        /// Gets the image URL for an agent.
        /// Images from https://valorant.fandom.com/wiki/Agents
        /// </summary>
        /// <param name="agent"></param>
        /// <returns></returns>
        public static string ImageURLFromAgent(this Agents agent)
        {
            return agent switch
            {
                Agents.Brimstone => "https://static.wikia.nocookie.net/valorant/images/8/81/Brimstone_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202719",
                Agents.Viper => "https://static.wikia.nocookie.net/valorant/images/8/85/Viper_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202837",
                Agents.Omen => "https://static.wikia.nocookie.net/valorant/images/0/0e/Omen_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202807",
                Agents.Killjoy => "https://static.wikia.nocookie.net/valorant/images/8/81/Killjoy_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202751",
                Agents.Cypher => "https://static.wikia.nocookie.net/valorant/images/5/55/Cypher_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202731",
                Agents.Sova => "https://static.wikia.nocookie.net/valorant/images/c/c5/Sova_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202832",
                Agents.Sage => "https://static.wikia.nocookie.net/valorant/images/7/7e/Sage_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202824",
                Agents.Phoenix => "https://static.wikia.nocookie.net/valorant/images/9/90/Phoenix_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202811",
                Agents.Jett => "https://static.wikia.nocookie.net/valorant/images/e/e3/Jett_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202742",
                Agents.Reyna => "https://static.wikia.nocookie.net/valorant/images/3/36/Reyna_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202820",
                Agents.Raze => "https://static.wikia.nocookie.net/valorant/images/6/6f/Raze_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202815",
                Agents.Breach => "https://static.wikia.nocookie.net/valorant/images/2/24/Breach_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202713",
                Agents.Skye => "https://static.wikia.nocookie.net/valorant/images/7/7f/Skye_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202828",
                Agents.Yoru => "https://static.wikia.nocookie.net/valorant/images/1/1e/Yoru_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202841",
                Agents.Astra => "https://static.wikia.nocookie.net/valorant/images/e/e0/Astra_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202708",
                Agents.Kayo => "https://static.wikia.nocookie.net/valorant/images/5/57/KAYO_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202747",
                Agents.Chamber => "https://static.wikia.nocookie.net/valorant/images/5/5d/Chamber_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202725",
                Agents.Neon => "https://static.wikia.nocookie.net/valorant/images/f/fe/Neon_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202800",
                Agents.Fade => "https://static.wikia.nocookie.net/valorant/images/e/e8/Fade_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202738",
                Agents.Harbor => "https://static.wikia.nocookie.net/valorant/images/5/5c/Harbor_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20221018133900",
                Agents.Gekko => "https://static.wikia.nocookie.net/valorant/images/a/a4/Gekko_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20230304203025",
                Agents.Deadlock => "https://static.wikia.nocookie.net/valorant/images/a/aa/Deadlock_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20230627132700",
                Agents.Iso => "https://static.wikia.nocookie.net/valorant/images/5/5f/Iso_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20231031131018",
                Agents.Clove => "https://static.wikia.nocookie.net/valorant/images/0/0b/Clove_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20240326163704",
                Agents.Vyse => "https://static.wikia.nocookie.net/valorant/images/d/d4/Vyse_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20240827165746",
                Agents.Tejo => "https://static.wikia.nocookie.net/valorant/images/c/cc/Tejo_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20250107192526",
                Agents.Waylay => "https://static.wikia.nocookie.net/valorant/images/4/4f/Waylay_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20250304181227",
                Agents.Veto => "https://static.wikia.nocookie.net/valorant/images/0/04/Veto_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20251007182704",
                _ => "",
            };
        }

        /// <summary>
        /// Gets the global id of an emoji.
        /// Agent emojis from server 1251242809684529286.
        /// </summary>
        /// <param name="agent"></param>
        /// <returns></returns>
        public static string Id(this Agents agent)
        {
            return agent switch
            {
                Agents.Astra => "<:astra:1251243976485240912> ",
                Agents.Breach => "<:breach:1251244287954522154>",
                Agents.Brimstone => "<:brimstone:1251243973192712223>",
                Agents.Chamber => "<:chamber:1251245477123326062>",
                Agents.Clove => "<:clove:1251243972412575836>",
                Agents.Cypher => "<:cypher:1251244292270329856>",
                Agents.Deadlock => "<:deadlock:1251244291313897523>",
                Agents.Fade => "<:fade:1251243993975750707>",
                Agents.Gekko => "<:gekko:1251243981069619310>",
                Agents.Harbor => "<:harbor:1251243976787230925>",
                Agents.Iso => "<:iso:1251245480005079061>",
                Agents.Jett => "<:jett:1251244293067243521>",
                Agents.Kayo => "<:kayo:1251243984072871946>",
                Agents.Killjoy => "<:killjoy:1251244288906367167>",
                Agents.Neon => "<:neon:1251244294300237924>",
                Agents.Omen => "<:omen:1251243974576967771>",
                Agents.Phoenix => "<:phoenix:1251245479157563413>",
                Agents.Raze => "<:raze:1251243986857754715>",
                Agents.Reyna => "<:reyna:1251243990582300712>",
                Agents.Sage => "<:sage:1251244290160726139>",
                Agents.Skye => "<:skye:1251244001063993366>",
                Agents.Sova => "<:sova:1251243977852846162>",
                Agents.Viper => "<:viper:1251243975587794975>",
                Agents.Vyse => "<:vyze:1280527291704807486>",
                Agents.Yoru => "<:yoru:1251245478029299845>",
                Agents.Tejo => "<:tejo:1347600071621087323>",
                Agents.Waylay => "<:waylay:1347600072673595584>",
                Agents.Veto => "<:veto:1425646755235758141>",
                _ => agent.StringFromAgent(),
            };
        }
    }
}