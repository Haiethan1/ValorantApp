using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        private static readonly Dictionary<string, EpisodeActInfos> Mapping = new Dictionary<string, EpisodeActInfos>
        {
            { "E1A1", new EpisodeActInfos(1, 1, new DateTime(2020, 6, 2), new DateTime(2020, 8, 3)) },
            { "E1A2", new EpisodeActInfos(1, 2, new DateTime(2020, 8, 4), new DateTime(2020, 10, 13)) },
            { "E1A3", new EpisodeActInfos(1, 3, new DateTime(2020, 10, 13), new DateTime(2021, 1, 12)) },
            { "E2A1", new EpisodeActInfos(2, 1, new DateTime(2021, 1, 12), new DateTime(2021, 3, 2)) },
            { "E2A2", new EpisodeActInfos(2, 2, new DateTime(2021, 3, 2), new DateTime(2021, 4, 27)) },
            { "E2A3", new EpisodeActInfos(2, 3, new DateTime(2021, 4, 27), new DateTime(2021, 6, 22)) },
            { "E3A1", new EpisodeActInfos(3, 1, new DateTime(2021, 6, 22), new DateTime(2021, 9, 8)) },
            { "E3A2", new EpisodeActInfos(3, 2, new DateTime(2021, 9, 8), new DateTime(2021, 11, 2)) },
            { "E3A3", new EpisodeActInfos(3, 3, new DateTime(2021, 11, 2), new DateTime(2022, 1, 11)) },
            { "E4A1", new EpisodeActInfos(4, 1, new DateTime(2022, 1, 11), new DateTime(2022, 3, 1)) },
            { "E4A2", new EpisodeActInfos(4, 2, new DateTime(2022, 3, 1), new DateTime(2022, 4, 27)) },
            { "E4A3", new EpisodeActInfos(4, 3, new DateTime(2022, 4, 27), new DateTime(2022, 6, 22)) },
            { "E5A1", new EpisodeActInfos(5, 1, new DateTime(2022, 6, 22), new DateTime(2023, 8, 23)) },
            { "E5A2", new EpisodeActInfos(5, 2, new DateTime(2022, 8, 23), new DateTime(2022, 10, 18)) },
            { "E5A3", new EpisodeActInfos(5, 3, new DateTime(2022, 10, 18), new DateTime(2023, 1, 10)) },
            { "E6A1", new EpisodeActInfos(6, 1, new DateTime(2023, 1, 10), new DateTime(2023, 3, 7)) },
            { "E6A2", new EpisodeActInfos(6, 2, new DateTime(2023, 3, 7), new DateTime(2023, 4, 25)) },
            { "E6A3", new EpisodeActInfos(6, 3, new DateTime(2023, 4, 25), new DateTime(2023, 6, 27)) },
            { "E7A1", new EpisodeActInfos(7, 1, new DateTime(2023, 6, 27), new DateTime(2023, 8, 29)) },
            { "E7A2", new EpisodeActInfos(7, 2, new DateTime(2023, 8, 29), new DateTime(2023, 10, 31)) },
            { "E7A3", new EpisodeActInfos(7, 3, new DateTime(2023, 10, 31), new DateTime(2024, 1, 9)) },
            { "E8A1", new EpisodeActInfos(8, 1, new DateTime(2023, 1, 10), new DateTime(2024, 3, 7)) }
        };

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
    }
}
