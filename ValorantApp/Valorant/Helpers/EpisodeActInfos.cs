namespace ValorantApp.Valorant.Helpers
{
    public class EpisodeActInfos
    {
        public int Episode { get; }
        public int Act { get; }
        public DateTime StartDate { get; }
        public DateTime EndDate { get; }

        public EpisodeActInfos(int episode, int act, DateTime startDate, DateTime endDate)
        {
            Episode = episode;
            Act = act;
            StartDate = startDate;
            EndDate = endDate;
        }

        public override string ToString()
        {
            return $"Episode {Episode} Act {Act}";
        }
    }

    public class EpisodeActExtension
    {
        // change this to using the api. this should be polled daily
        private static readonly Dictionary<string, EpisodeActInfos> Mapping = new()
        {
            { "E1A1", new EpisodeActInfos(1, 1, UTCHours(2020, 06, 01, 7), UTCHours(2020, 08, 04, 0)) },
            { "E1A2", new EpisodeActInfos(1, 2, UTCHours(2020, 08, 04, 0), UTCHours(2020, 10, 13, 0)) },
            { "E1A3", new EpisodeActInfos(1, 3, UTCHours(2020, 10, 13, 0), UTCHours(2021, 01, 12, 0)) },
            { "E2A1", new EpisodeActInfos(2, 1, UTCHours(2021, 01, 12, 0), UTCHours(2021, 03, 02, 0)) },
            { "E2A2", new EpisodeActInfos(2, 2, UTCHours(2021, 03, 02, 0), UTCHours(2021, 04, 27, 0)) },
            { "E2A3", new EpisodeActInfos(2, 3, UTCHours(2021, 04, 27, 0), UTCHours(2021, 06, 22, 0)) },
            { "E3A1", new EpisodeActInfos(3, 1, UTCHours(2021, 06, 22, 0), UTCHours(2021, 09, 08, 0)) },
            { "E3A2", new EpisodeActInfos(3, 2, UTCHours(2021, 09, 08, 0), UTCHours(2021, 11, 02, 0)) },
            { "E3A3", new EpisodeActInfos(3, 3, UTCHours(2021, 11, 02, 0), UTCHours(2022, 01, 11, 0)) },
            { "E4A1", new EpisodeActInfos(4, 1, UTCHours(2022, 01, 11, 0), UTCHours(2022, 03, 01, 0)) },
            { "E4A2", new EpisodeActInfos(4, 2, UTCHours(2022, 03, 01, 0), UTCHours(2022, 04, 26, 0)) },
            { "E4A3", new EpisodeActInfos(4, 3, UTCHours(2022, 04, 26, 0), UTCHours(2022, 06, 21, 0)) },
            { "E5A1", new EpisodeActInfos(5, 1, UTCHours(2022, 06, 21, 0), UTCHours(2022, 08, 23, 0)) },
            { "E5A2", new EpisodeActInfos(5, 2, UTCHours(2022, 08, 23, 0), UTCHours(2022, 10, 18, 0)) },
            { "E5A3", new EpisodeActInfos(5, 3, UTCHours(2022, 10, 18, 0), UTCHours(2023, 01, 10, 0)) },
            { "E6A1", new EpisodeActInfos(6, 1, UTCHours(2023, 01, 10, 0), UTCHours(2023, 03, 07, 0)) },
            { "E6A2", new EpisodeActInfos(6, 2, UTCHours(2023, 03, 07, 0), UTCHours(2023, 04, 25, 0)) },
            { "E6A3", new EpisodeActInfos(6, 3, UTCHours(2023, 04, 25, 0), UTCHours(2023, 06, 27, 0)) },
            { "E7A1", new EpisodeActInfos(7, 1, UTCHours(2023, 06, 27, 0), UTCHours(2023, 08, 29, 0)) },
            { "E7A2", new EpisodeActInfos(7, 2, UTCHours(2023, 08, 29, 0), UTCHours(2023, 10, 31, 0)) },
            { "E7A3", new EpisodeActInfos(7, 3, UTCHours(2023, 10, 31, 0), UTCHours(2024, 01, 09, 0)) },
            { "E8A1", new EpisodeActInfos(8, 1, UTCHours(2024, 01, 09, 0), UTCHours(2024, 03, 05, 0)) },
            { "E8A2", new EpisodeActInfos(8, 2, UTCHours(2024, 03, 05, 0), UTCHours(2024, 04, 30, 0)) },
            { "E8A3", new EpisodeActInfos(8, 3, UTCHours(2024, 04, 30, 0), UTCHours(2024, 06, 25, 0)) },
            { "E9A1", new EpisodeActInfos(9, 1, UTCHours(2024, 06, 25, 0), UTCHours(2024, 08, 27, 0)) },
            { "E9A2", new EpisodeActInfos(9, 2, UTCHours(2024, 08, 27, 0), UTCHours(2024, 10, 23, 0)) },
            { "E9A3", new EpisodeActInfos(9, 3, UTCHours(2024, 10, 23, 0), UTCHours(2025, 01, 07, 0)) },
            { "E25A1", new EpisodeActInfos(25, 1, UTCHours(2025, 01, 08, 0), UTCHours(2025, 03, 05, 0)) },
            { "E25A2", new EpisodeActInfos(25, 1, UTCHours(2025, 03, 05, 0), UTCHours(2030, 04, 30, 0)) },
            { "E25A3", new EpisodeActInfos(25, 1, UTCHours(2025, 04, 30, 0), UTCHours(2030, 01, 08, 0)) },
        };

        private static DateTime UTCHours(int year, int month, int day, int hours)
        {
            return new DateTime(year, month, day, hours, 0, 0, DateTimeKind.Utc);
        }

        public static EpisodeActInfos GetEpisodeActInfo(string identifier)
        {
            if (Mapping.TryGetValue(identifier, out EpisodeActInfos? result))
            {
                return result;
            }

            return null;
        }

        public static (DateTime, DateTime) GetStartAndEndDates(EpisodeActInfos start, EpisodeActInfos end)
        {
            if (start.StartDate < end.EndDate)
            {
                return (start.StartDate, end.EndDate);
            }

            return (Mapping["E1A1"].StartDate, DateTime.UtcNow);
        }

        public static EpisodeActInfos GetEpisodeActInfosForDate(DateTime dateUTC)
        {
            return Mapping.FirstOrDefault(
                x => x.Value.StartDate <= dateUTC && x.Value.EndDate > dateUTC, Mapping.Last()
                ).Value;
        }

        public static EpisodeActInfos? GetEpisodeActInfosForEndDate(DateTime endDateUTC)
        {
            DateTime endDate = new DateTime(endDateUTC.Year, endDateUTC.Month, endDateUTC.Day, 0, 0, 0, DateTimeKind.Utc);
            KeyValuePair<string, EpisodeActInfos>? kvp = Mapping.FirstOrDefault(
                x => x.Value.EndDate == endDate
                );
            return kvp.HasValue ? kvp.Value.Value : null;
        }
    }
}
