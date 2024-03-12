using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValorantApp.GenericExtensions
{
    public static class StringExtensions
    {
        public static string Safe(this string? input)
        {
            // TODO - log this if it is null. Log the name of the variable.
            return input ?? "";
        }

        public static bool IsNullOrEmpty(this string? input)
        {
            if (input == null) return true;
            if (input.Length == 0) return true;

            return false;
        }
    }
}
