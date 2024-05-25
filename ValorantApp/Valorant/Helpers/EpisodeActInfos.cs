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
    }

    public class EpisodeActExtension
    {
        private static readonly Dictionary<string, EpisodeActInfos> Mapping = new()
        {
            { "E1A1", new EpisodeActInfos(1, 1, UTCHours(2020, 6, 1, 7), UTCHours(2020, 8, 4, 0)) },
            { "E1A2", new EpisodeActInfos(1, 2, UTCHours(2020, 8, 4, 0), UTCHours(2020, 10, 13, 0)) },
            { "E1A3", new EpisodeActInfos(1, 3, UTCHours(2020, 10, 13, 0), UTCHours(2021, 1, 12, 0)) },
            { "E2A1", new EpisodeActInfos(2, 1, UTCHours(2021, 1, 12, 0), UTCHours(2021, 3, 2, 0)) },
            { "E2A2", new EpisodeActInfos(2, 2, UTCHours(2021, 3, 2, 0), UTCHours(2021, 4, 27, 0)) },
            { "E2A3", new EpisodeActInfos(2, 3, UTCHours(2021, 4, 27, 0), UTCHours(2021, 6, 22, 0)) },
            { "E3A1", new EpisodeActInfos(3, 1, UTCHours(2021, 6, 22, 0), UTCHours(2021, 9, 8, 0)) },
            { "E3A2", new EpisodeActInfos(3, 2, UTCHours(2021, 9, 8, 0), UTCHours(2021, 11, 2, 0)) },
            { "E3A3", new EpisodeActInfos(3, 3, UTCHours(2021, 11, 2, 0), UTCHours(2022, 1, 11, 0)) },
            { "E4A1", new EpisodeActInfos(4, 1, UTCHours(2022, 1, 11, 0), UTCHours(2022, 3, 1, 0)) },
            { "E4A2", new EpisodeActInfos(4, 2, UTCHours(2022, 3, 1, 0), UTCHours(2022, 4, 26, 0)) },
            { "E4A3", new EpisodeActInfos(4, 3, UTCHours(2022, 4, 26, 0), UTCHours(2022, 6, 21, 0)) },
            { "E5A1", new EpisodeActInfos(5, 1, UTCHours(2022, 6, 21, 0), UTCHours(2022, 8, 23, 0)) },
            { "E5A2", new EpisodeActInfos(5, 2, UTCHours(2022, 8, 23, 0), UTCHours(2022, 10, 18, 0)) },
            { "E5A3", new EpisodeActInfos(5, 3, UTCHours(2022, 10, 18, 0), UTCHours(2023, 1, 10, 0)) },
            { "E6A1", new EpisodeActInfos(6, 1, UTCHours(2023, 1, 10, 0), UTCHours(2023, 3, 7, 0)) },
            { "E6A2", new EpisodeActInfos(6, 2, UTCHours(2023, 3, 7, 0), UTCHours(2023, 4, 25, 0)) },
            { "E6A3", new EpisodeActInfos(6, 3, UTCHours(2023, 4, 25, 0), UTCHours(2023, 6, 27, 0)) },
            { "E7A1", new EpisodeActInfos(7, 1, UTCHours(2023, 6, 27, 0), UTCHours(2023, 8, 29, 0)) },
            { "E7A2", new EpisodeActInfos(7, 2, UTCHours(2023, 8, 29, 0), UTCHours(2023, 10, 31, 0)) },
            { "E7A3", new EpisodeActInfos(7, 3, UTCHours(2023, 10, 31, 0), UTCHours(2024, 1, 9, 0)) },
            { "E8A1", new EpisodeActInfos(8, 1, UTCHours(2024, 1, 9, 0), UTCHours(2024, 3, 5, 0)) },
            { "E8A2", new EpisodeActInfos(8, 2, UTCHours(2024, 3, 5, 0), UTCHours(2024, 4, 30, 0)) },
            { "E8A3", new EpisodeActInfos(8, 3, UTCHours(2024, 4, 30, 0), UTCHours(2024, 6, 25, 0)) },
            { "E9A1", new EpisodeActInfos(9, 1, UTCHours(2024, 6, 25, 0), UTCHours(2024, 8, 20, 0)) }, // TODO: Fill in info
            { "E9A2", new EpisodeActInfos(9, 2, UTCHours(2030, 4, 30, 0), UTCHours(2030, 6, 25, 0)) }, // Fill in info
            { "E9A3", new EpisodeActInfos(9, 3, UTCHours(2030, 4, 30, 0), UTCHours(2030, 6, 25, 0)) }, // Fill in info
        };

        private static DateTime UTCHours(int year, int month, int day, int hours)
        {
            return new DateTime(year, month, day, hours, 0, 0, DateTimeKind.Utc);
        }

        public static EpisodeActInfos GetEpisodeActInfo(string identifier)
        {
            if (Mapping.TryGetValue(identifier, out var result))
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
    }
}
