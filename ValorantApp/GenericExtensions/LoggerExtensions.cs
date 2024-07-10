using Microsoft.Extensions.Logging;

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
            logger.LogWarning(message);
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
            logger.LogError(message);
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
