using System.ComponentModel;

namespace ValorantApp.Valorant.Enums
{
    public enum EventType
    {
        [Description("Unknown")]
        Unknown,

        [Description("Kill")]
        Kill,

        [Description("Plant")]
        Plant,

        [Description("Defuse")]
        Defuse,

        [Description("Damage")]
        Damage,
    }

    public static class EventTypeExtension
    {


        public static string StringFromEventType(this EventType eventType)
        {
            return eventType switch
            {
                EventType.Kill => "Kill",
                EventType.Plant => "Plant",
                EventType.Defuse => "Defuse",
                EventType.Damage => "Damage",
                _ => "",
            };
        }
    }
}
