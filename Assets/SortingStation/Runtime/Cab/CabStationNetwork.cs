using System;

namespace SortingStation
{
    [Serializable]
    public sealed class CabStationDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string LineName { get; }
        public int RouteIndex { get; }
        public bool IsTerminal { get; }

        public CabStationDefinition(string id, string displayName, string lineName, int routeIndex, bool isTerminal = false)
        {
            Id = id;
            DisplayName = displayName;
            LineName = lineName;
            RouteIndex = routeIndex;
            IsTerminal = isTerminal;
        }
    }

    /// <summary>Examples from the Санкт-Петербург—Выборг and Санкт-Петербург—Приозерск directions.</summary>
    public static class CabStationNetwork
    {
        private static readonly CabStationDefinition[] Stations =
        {
            new CabStationDefinition("spb-fin", "Санкт-Петербург-Финляндский", "Выборгское направление", 0, true),
            new CabStationDefinition("udel", "Удельная", "Выборгское направление", 1),
            new CabStationDefinition("pargolovo", "Парголово", "Выборгское направление", 2),
            new CabStationDefinition("levashovo", "Левашово", "Выборгское направление", 3),
            new CabStationDefinition("zelenogorsk", "Зеленогорск", "Выборгское направление", 4),
            new CabStationDefinition("vyborg", "Выборг", "Выборгское направление", 5, true),
            new CabStationDefinition("devyatkino", "Девяткино", "Приозерское направление", 6),
            new CabStationDefinition("toksovo", "Токсово", "Приозерское направление", 7),
            new CabStationDefinition("sosnovo", "Сосново", "Приозерское направление", 8),
            new CabStationDefinition("losevo", "Лосево", "Приозерское направление", 9),
            new CabStationDefinition("priozersk", "Приозерск", "Приозерское направление", 10, true)
        };

        public static CabStationDefinition AtSequence(int sequence)
        {
            if (Stations.Length == 0) return null;
            int index = ((sequence % Stations.Length) + Stations.Length) % Stations.Length;
            return Stations[index];
        }

        public static CabStationDefinition ByRouteIndex(int routeIndex) => AtSequence(routeIndex);

        public static int NextDestinationIndex(int fromRouteIndex, System.Random random)
        {
            int maxHops = Math.Min(5, Stations.Length - 1);
            int hop = 1 + random.Next(0, Math.Max(1, maxHops));
            return AtSequence(fromRouteIndex + hop).RouteIndex;
        }
    }
}
