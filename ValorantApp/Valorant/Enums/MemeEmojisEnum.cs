using System.ComponentModel;

namespace ValorantApp.Valorant.Enums
{
    public enum MemeEmojisEnum
    {
        [Description("Toe Shooter")]
        ToeShooter,

        [Description("Clown")]
        Clown,

        [Description("Sunglasses")]
        Sunglasses,

        [Description("Arrow Down")]
        ArrowDown,

        [Description("Arrow Up")]
        ArrowUp,

        [Description("ArrowDoubleUp")]
        ArrowDoubleUp,

        [Description("Sparkles")]
        Sparkles,
    }

    public static class MemeEmojisEnumExtensions
    {
        /// <summary>
        /// This method will be hard coded values of emoji's
        /// </summary>
        /// <param name="emoji"></param>
        /// <returns></returns>
        public static string Id(this MemeEmojisEnum emoji)
        {
            return emoji switch
            {
                MemeEmojisEnum.ToeShooter => "a:toeshooter:1243584122153336912",
                MemeEmojisEnum.Clown => ":clown:",
                MemeEmojisEnum.Sunglasses => ":sunglasses:",
                MemeEmojisEnum.ArrowDown => ":arrow_down:",
                MemeEmojisEnum.ArrowUp => ":arrow_up:",
                MemeEmojisEnum.ArrowDoubleUp => ":arrow_double_up:",
                MemeEmojisEnum.Sparkles => ":sparkles:",
                _ => "",
            };
        }
    }
}
