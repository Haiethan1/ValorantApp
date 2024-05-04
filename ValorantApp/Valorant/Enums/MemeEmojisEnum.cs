namespace ValorantApp.Valorant.Enums
{
    public enum MemeEmojisEnum
    {
        TouchGrass,
    }

    public static class MemeEmojisEnumExtensions
    {
        public static string EnumToEmojiString(this MemeEmojisEnum memeEmojisEnum)
        {
            switch (memeEmojisEnum)
            {
                case MemeEmojisEnum.TouchGrass:
                    return "a:touchgrass:1224024506482425946";
                default:
                    return "";
            }
        }
    }
}
