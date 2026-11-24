using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using ValorantApp.Valorant.Enums;

namespace ValorantAppTests
{
    [TestClass]
    public class PersistedEnumTests
    {
        // Every pinned member must still exist with the same integer value (catches reorder, insert-in-middle, delete,
        // rename). Members appended at the end are allowed: they must be greater than every pinned value.
        private static void AssertEnumValues<TEnum>(Dictionary<string, int> expected) where TEnum : struct, Enum
        {
            string enumName = typeof(TEnum).Name;
            foreach (var (name, value) in expected)
            {
                Assert.IsTrue(Enum.TryParse<TEnum>(name, out var member), $"{enumName}.{name} was removed or renamed");
                Assert.AreEqual(value, Convert.ToInt32(member), $"Value mismatch for {enumName}.{name}");
            }

            int maxPinned = 0;
            foreach (int value in expected.Values) maxPinned = Math.Max(maxPinned, value);

            foreach (var name in Enum.GetNames<TEnum>())
            {
                if (expected.ContainsKey(name)) continue;
                int actual = Convert.ToInt32(Enum.Parse<TEnum>(name));
                Assert.IsTrue(actual > maxPinned, $"New member {enumName}.{name} = {actual} must be appended after the last pinned value ({maxPinned}). Add it to the pinned list when you add it.");
            }
        }

        // Persisted as integers. Only append new members at the end.
        [TestMethod]
        public void Agents_PersistedValues_DoNotChange()
        {
            var expected = new Dictionary<string, int>
            {
                { "Unknown", 0 },
                { "Brimstone", 1 },
                { "Viper", 2 },
                { "Omen", 3 },
                { "Killjoy", 4 },
                { "Cypher", 5 },
                { "Sova", 6 },
                { "Sage", 7 },
                { "Phoenix", 8 },
                { "Jett", 9 },
                { "Reyna", 10 },
                { "Raze", 11 },
                { "Breach", 12 },
                { "Skye", 13 },
                { "Yoru", 14 },
                { "Astra", 15 },
                { "Kayo", 16 },
                { "Chamber", 17 },
                { "Neon", 18 },
                { "Fade", 19 },
                { "Harbor", 20 },
                { "Gekko", 21 },
                { "Deadlock", 22 },
                { "Iso", 23 },
                { "Clove", 24 },
                { "Vyse", 25 },
                { "Tejo", 26 },
                { "Waylay", 27 },
                { "Veto", 28 }
            };

            AssertEnumValues<Agents>(expected);
        }

        // Persisted as integers. Only append new members at the end.
        [TestMethod]
        public void EventType_PersistedValues_DoNotChange()
        {
            var expected = new Dictionary<string, int>
            {
                { "Unknown", 0 },
                { "Kill", 1 },
                { "Plant", 2 },
                { "Defuse", 3 },
                { "Damage", 4 }
            };

            AssertEnumValues<EventType>(expected);
        }

        // Persisted as integers. Only append new members at the end.
        [TestMethod]
        public void Team_PersistedValues_DoNotChange()
        {
            var expected = new Dictionary<string, int>
            {
                { "Unknown", 0 },
                { "Blue", 1 },
                { "Red", 2 }
            };

            AssertEnumValues<Team>(expected);
        }
    }
}
