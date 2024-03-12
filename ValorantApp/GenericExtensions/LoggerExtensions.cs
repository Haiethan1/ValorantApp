using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValorantApp.GenericExtensions
{
    public static class LoggerExtensions
    {
        public static void ApiInformation<T>(this ILogger<T> logger, string message)
        {
            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["ApiLog"] = true,
            }))
            {
                logger.LogInformation(message);
            }
        }

        public static void ApiWarning<T>(this ILogger<T> logger, string message)
        {
            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["ApiLog"] = true,
            }))
            {
                logger.LogWarning(message);
            }
        }

        public static void ApiError<T>(this ILogger<T> logger, string message)
        {
            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["ApiLog"] = true,
            }))
            {
                logger.LogError(message);
            }
        }
    }
}
