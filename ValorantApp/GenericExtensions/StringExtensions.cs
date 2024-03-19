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

        public static string Repeat(this string s, int n)
        {
            if (string.IsNullOrEmpty(s) || n <= 0)
            {
                return "";
            }
            return new StringBuilder(s.Length * n).Insert(0, s, n).ToString();
        }
    }
}
