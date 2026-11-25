using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using ValorantApp.GenericUtils;

namespace ValorantAppTests
{
    [TestClass]
    public class TimerUtilsTests
    {
        [TestMethod]
        public void TimeSpanUntilUTC_BeforeTarget_ReturnsSameDayRemainingTime()
        {
            // At 08:30:00 UTC, target 11:00:00 UTC is on the same day (2.5 hours remaining)
            DateTime nowUtc = new(2026, 11, 23, 8, 30, 0, DateTimeKind.Utc);
            TimeSpan result = TimerUtils.TimeSpanUntilUTC(11, 0, 0, nowUtc);

            Assert.AreEqual(TimeSpan.FromHours(2.5), result);
        }

        [TestMethod]
        public void TimeSpanUntilUTC_AfterTarget_ReturnsNextDayRemainingTime()
        {
            // At 14:00:00 UTC, target 11:00:00 UTC today has passed; scheduled for 11:00 tomorrow (21 hours remaining)
            DateTime nowUtc = new(2026, 11, 23, 14, 0, 0, DateTimeKind.Utc);
            TimeSpan result = TimerUtils.TimeSpanUntilUTC(11, 0, 0, nowUtc);

            Assert.AreEqual(TimeSpan.FromHours(21), result);
        }

        [TestMethod]
        public void TimeSpanUntilUTC_ExactlyAtTarget_ReturnsZero()
        {
            // NOTE: possible bug: when now == target (11:00:00 UTC), strict inequality `now > target` is false,
            // so target is not advanced to tomorrow and TimeSpan.Zero is returned instead of 24 hours.
            DateTime nowUtc = new(2026, 11, 23, 11, 0, 0, DateTimeKind.Utc);
            TimeSpan result = TimerUtils.TimeSpanUntilUTC(11, 0, 0, nowUtc);

            Assert.AreEqual(TimeSpan.Zero, result);
        }

        [DataTestMethod]
        [DataRow(23, 45, 0, 11, 15, DisplayName = "Before midnight (23:45) -> 11h 15m until tomorrow 11:00")]
        [DataRow(0, 15, 0, 10, 45, DisplayName = "After midnight (00:15) -> 10h 45m until today 11:00")]
        public void TimeSpanUntilUTC_NearMidnight_PinsActualBehavior(int hour, int minute, int second, int expectedHours, int expectedMinutes)
        {
            // NOTE: possible bug: TimerUtils does not convert nowUtc to UTC when given Unspecified or Local Kind;
            // it constructs target from now.Year/Month/Day and subtracts ticks directly without timezone conversion.
            DateTime nowNearMidnight = new(2026, 11, 23, hour, minute, second, DateTimeKind.Unspecified);
            TimeSpan result = TimerUtils.TimeSpanUntilUTC(11, 0, 0, nowNearMidnight);

            TimeSpan expected = new(expectedHours, expectedMinutes, 0);
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void TimeSpanUntilUTC_NullNowUtc_UsesUtcNow()
        {
            // When nowUtc is omitted (null), default behavior is non-negative and <= 24 hours
            TimeSpan result = TimerUtils.TimeSpanUntilUTC(11, 0, 0);

            Assert.IsTrue(result >= TimeSpan.Zero, "TimeSpan should be non-negative");
            Assert.IsTrue(result <= TimeSpan.FromDays(1), "TimeSpan should not exceed 24 hours");
        }
    }
}
