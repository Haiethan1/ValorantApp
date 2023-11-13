using System;
using System.ComponentModel;
using System.Reflection;

public static class EnumExtensions
{
    public static string ToDescriptionString(this Enum value)
    {
        FieldInfo? field = value.GetType().GetField(value.ToString());

        if (field == null)
        {
            return value.ToString();
        }

        if (field.GetCustomAttribute(typeof(DescriptionAttribute)) is DescriptionAttribute attribute)
        {
            return attribute.Description;
        }

        return value.ToString();
    }
}
