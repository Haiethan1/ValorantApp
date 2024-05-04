using System.ComponentModel;
using System.Reflection;

public static class IEnumerableExtensions
{
    public static bool IsNullOrEmpty<T>(this IEnumerable<T> value)
    {
        if (value == null
            || value.Count() == 0)
        {
            return true;
        }

        return false;
    }
}
