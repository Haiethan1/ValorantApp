using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using ValorantApp.Valorant.Helpers;

namespace ValorantAppTests
{
    [TestClass]
    public class EpisodeActTests
    {
        [TestMethod]
        public void GetEpisodeActInfo_KnownKeys_ReturnsExpected()
        {
            var e1a1 = EpisodeActExtension.GetEpisodeActInfo("E1A1");
            Assert.IsNotNull(e1a1);
            Assert.AreEqual(1, e1a1.Episode);
            Assert.AreEqual(1, e1a1.Act);
            Assert.AreEqual(new DateTime(2020, 6, 1, 7, 0, 0, DateTimeKind.Utc), e1a1.StartDate);
            Assert.AreEqual(new DateTime(2020, 8, 4, 0, 0, 0, DateTimeKind.Utc), e1a1.EndDate);

            // NOTE: possible bug: EpisodeActExtension.Mapping: E25A2 has Act = 1 hardcoded instead of 2
            var e25a2 = EpisodeActExtension.GetEpisodeActInfo("E25A2");
            Assert.IsNotNull(e25a2);
            Assert.AreEqual(25, e25a2.Episode);
            Assert.AreEqual(1, e25a2.Act);

            // NOTE: possible bug: EpisodeActExtension.Mapping: E25A3 has Act = 1 hardcoded instead of 3
            var e25a3 = EpisodeActExtension.GetEpisodeActInfo("E25A3");
            Assert.IsNotNull(e25a3);
            Assert.AreEqual(25, e25a3.Episode);
            Assert.AreEqual(1, e25a3.Act);
        }

        [TestMethod]
        public void GetEpisodeActInfo_UnknownOrEmpty_ReturnsNull()
        {
            Assert.IsNull(EpisodeActExtension.GetEpisodeActInfo("Unknown"));
            Assert.IsNull(EpisodeActExtension.GetEpisodeActInfo(""));
        }

        [TestMethod]
        public void GetEpisodeActInfo_NullInput_ThrowsArgumentNullException()
        {
            // NOTE: possible bug: EpisodeActExtension.GetEpisodeActInfo does not check null before Dictionary.TryGetValue
            Assert.ThrowsException<ArgumentNullException>(() => EpisodeActExtension.GetEpisodeActInfo(null!));
        }

        [TestMethod]
        public void GetStartAndEndDates_ValidAndInvertedRanges_Handled()
        {
            var e1a1 = EpisodeActExtension.GetEpisodeActInfo("E1A1")!;
            var e1a2 = EpisodeActExtension.GetEpisodeActInfo("E1A2")!;

            var (validStart, validEnd) = EpisodeActExtension.GetStartAndEndDates(e1a1, e1a2);
            Assert.AreEqual(e1a1.StartDate, validStart);
            Assert.AreEqual(e1a2.EndDate, validEnd);

            var (fallbackStart, fallbackEnd) = EpisodeActExtension.GetStartAndEndDates(e1a2, e1a1);
            Assert.AreEqual(e1a1.StartDate, fallbackStart);
            Assert.IsTrue((DateTime.UtcNow - fallbackEnd).TotalSeconds < 5);
        }

        [DataTestMethod]
        [DataRow(2020, 6, 1, 7, 1, 1)]      // E1A1 start boundary
        [DataRow(2020, 7, 1, 0, 1, 1)]      // Inside E1A1
        [DataRow(2020, 8, 4, 0, 1, 2)]      // E1A2 start boundary / E1A1 end boundary
        [DataRow(2024, 2, 1, 0, 8, 1)]      // Inside E8A1
        public void GetEpisodeActInfosForDate_StandardDates_ReturnsCorrectAct(
            int year, int month, int day, int hour, int expectedEpisode, int expectedAct)
        {
            var date = new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Utc);
            var act = EpisodeActExtension.GetEpisodeActInfosForDate(date);
            Assert.AreEqual(expectedEpisode, act.Episode);
            Assert.AreEqual(expectedAct, act.Act);
        }

        [TestMethod]
        public void GetEpisodeActInfosForDate_StaleAndBoundaryDates_PinsCurrentBehavior()
        {
            // NOTE: possible bug: EpisodeActExtension.GetEpisodeActInfosForDate: dates before E1A1 fall back to Mapping.Last() (E25A3) instead of null
            var beforeFirst = EpisodeActExtension.GetEpisodeActInfosForDate(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            Assert.AreEqual(25, beforeFirst.Episode);
            Assert.AreEqual(1, beforeFirst.Act);

            // NOTE: possible bug: EpisodeActExtension.GetEpisodeActInfosForDate: gap between E9A3 and E25A1 falls back to Mapping.Last() (E25A3)
            var inGap = EpisodeActExtension.GetEpisodeActInfosForDate(new DateTime(2025, 1, 7, 12, 0, 0, DateTimeKind.Utc));
            Assert.AreEqual(25, inGap.Episode);
            Assert.AreEqual(1, inGap.Act);

            // NOTE: possible bug: EpisodeActExtension.GetEpisodeActInfosForDate: E25A2 and E25A3 ranges overlap; FirstOrDefault returns E25A2 with Act = 1
            var overlap = EpisodeActExtension.GetEpisodeActInfosForDate(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
            Assert.AreEqual(25, overlap.Episode);
            Assert.AreEqual(1, overlap.Act);
            Assert.AreEqual(new DateTime(2030, 4, 30, 0, 0, 0, DateTimeKind.Utc), overlap.EndDate);

            // NOTE: possible bug: EpisodeActExtension.GetEpisodeActInfosForDate: dates after 2030 fall back to Mapping.Last() (E25A3) instead of null
            var afterLast = EpisodeActExtension.GetEpisodeActInfosForDate(new DateTime(2035, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            Assert.AreEqual(25, afterLast.Episode);
            Assert.AreEqual(1, afterLast.Act);
        }

        [DataTestMethod]
        [DataRow(2020, 8, 4, 0, 1, 1)]       // E1A1 exact end date
        [DataRow(2020, 8, 4, 18, 1, 1)]      // E1A1 end date with non-zero time (truncated to midnight)
        [DataRow(2024, 1, 9, 0, 7, 3)]       // E7A3 exact end date
        public void GetEpisodeActInfosForEndDate_MatchingEndDates_ReturnsExpected(
            int year, int month, int day, int hour, int expectedEpisode, int expectedAct)
        {
            var date = new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Utc);
            var act = EpisodeActExtension.GetEpisodeActInfosForEndDate(date);
            Assert.IsNotNull(act);
            Assert.AreEqual(expectedEpisode, act.Episode);
            Assert.AreEqual(expectedAct, act.Act);
        }

        [DataTestMethod]
        [DataRow(2020, 8, 5)]  // Non-matching day
        [DataRow(2019, 1, 1)]  // Before table
        [DataRow(2035, 1, 1)]  // After table
        public void GetEpisodeActInfosForEndDate_NonMatchingDates_ReturnsNull(int year, int month, int day)
        {
            var date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
            Assert.IsNull(EpisodeActExtension.GetEpisodeActInfosForEndDate(date));
        }
    }
}
