namespace ValorantApp.GenericUtils
{
    internal static class TimerUtils
    {
        internal const int HALF_SECOND_MS = 500;
        internal const int QUARTER_SECOND_MS = 250;

        public static TimeSpan TimeSpanUntilUTC(int hourUTC, int minuteUTC, int secondUTC)
        {
            DateTime now = DateTime.UtcNow;
            DateTime target = new(now.Year, now.Month, now.Day, hourUTC, minuteUTC, secondUTC, DateTimeKind.Utc);

            // If it's already past 11:00 AM UTC today, schedule for 11:00 AM UTC tomorrow
            if (now > target)
            {
                target = target.AddDays(1);
            }

            return target - now;
        }
    }
}
