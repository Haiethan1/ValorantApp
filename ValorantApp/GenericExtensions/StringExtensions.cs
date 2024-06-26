﻿using Newtonsoft.Json;
using System.Text;

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

        public static T? TryParse<T>(this string? input, out string errormsg)
        {
            errormsg = "";
            try
            {
                if (input == null || input.Length == 0)
                {
                    errormsg = "TryParse json input is null or empty";
                    return default;
                }
                return JsonConvert.DeserializeObject<T>(input);
            }
            catch (Exception ex)
            {
                errormsg = ex.Message;
            }

            return default;
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
