using System.ComponentModel;
using System;
using System.Runtime.CompilerServices;

namespace ValorantApp.Valorant.Enums
{
    public enum Agents
    {
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
        Clove
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

            throw new ArgumentException("Invalid agent name: " + str, nameof(str));
        }

        public static string StringFromAgent(this Agents agent)
        {
            return agent.ToDescriptionString();
        }

        public static string ImageURLFromAgent(this Agents agent)
        {
            switch (agent)
            {
                case Agents.Brimstone:
                    return "https://static.wikia.nocookie.net/valorant/images/8/81/Brimstone_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202719";
                case Agents.Viper:
                    return "https://static.wikia.nocookie.net/valorant/images/8/85/Viper_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202837";
                case Agents.Omen:
                    return "https://static.wikia.nocookie.net/valorant/images/0/0e/Omen_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202807";
                case Agents.Killjoy:
                    return "https://static.wikia.nocookie.net/valorant/images/8/81/Killjoy_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202751";
                case Agents.Cypher:
                    return "https://static.wikia.nocookie.net/valorant/images/5/55/Cypher_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202731";
                case Agents.Sova:
                    return "https://static.wikia.nocookie.net/valorant/images/c/c5/Sova_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202832";
                case Agents.Sage:
                    return "https://static.wikia.nocookie.net/valorant/images/7/7e/Sage_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202824";
                case Agents.Phoenix:
                    return "https://static.wikia.nocookie.net/valorant/images/9/90/Phoenix_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202811";
                case Agents.Jett:
                    return "https://static.wikia.nocookie.net/valorant/images/e/e3/Jett_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202742";
                case Agents.Reyna:
                    return "https://static.wikia.nocookie.net/valorant/images/3/36/Reyna_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202820";
                case Agents.Raze:
                    return "https://static.wikia.nocookie.net/valorant/images/6/6f/Raze_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202815";
                case Agents.Breach:
                    return "https://static.wikia.nocookie.net/valorant/images/2/24/Breach_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202713";
                case Agents.Skye:
                    return "https://static.wikia.nocookie.net/valorant/images/7/7f/Skye_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202828";
                case Agents.Yoru:
                    return "https://static.wikia.nocookie.net/valorant/images/1/1e/Yoru_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202841";
                case Agents.Astra:
                    return "https://static.wikia.nocookie.net/valorant/images/e/e0/Astra_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202708";
                case Agents.Kayo:
                    return "https://static.wikia.nocookie.net/valorant/images/5/57/KAYO_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202747";
                case Agents.Chamber:
                    return "https://static.wikia.nocookie.net/valorant/images/5/5d/Chamber_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202725";
                case Agents.Neon:
                    return "https://static.wikia.nocookie.net/valorant/images/f/fe/Neon_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202800";
                case Agents.Fade:
                    return "https://static.wikia.nocookie.net/valorant/images/e/e8/Fade_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20220810202738";
                case Agents.Harbor:
                    return "https://static.wikia.nocookie.net/valorant/images/5/5c/Harbor_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20221018133900";
                case Agents.Gekko:
                    return "https://static.wikia.nocookie.net/valorant/images/a/a4/Gekko_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20230304203025";
                case Agents.Deadlock:
                    return "https://static.wikia.nocookie.net/valorant/images/a/aa/Deadlock_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20230627132700";
                case Agents.Iso:
                    return "https://static.wikia.nocookie.net/valorant/images/5/5f/Iso_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20231031131018";
                case Agents.Clove:
                    return "https://static.wikia.nocookie.net/valorant/images/0/0b/Clove_Artwork_Full.png/revision/latest/scale-to-width-down/1000?cb=20240326163704";
                default:
                    return "";
            }
        }
    }
}