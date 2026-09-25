using System;
using System.Collections.Generic;
using System.Linq;

namespace SortingStation
{
    /// <summary>
    /// A small deterministic passenger simulation.  It intentionally runs only when a train calls at
    /// a station, so it is cheap on tablets and its results stay stable for the same route seed.
    /// </summary>
    public sealed class CabPassengerService
    {
        private readonly List<PassengerJourney> onboard = new List<PassengerJourney>();
        private readonly System.Random random;
        private readonly int capacity;

        public int Capacity => capacity;
        public int OnboardCount => onboard.Count;

        public CabPassengerService(int routeSeed, int passengerCapacity = 76)
        {
            capacity = Math.Max(12, passengerCapacity);
            random = new System.Random(routeSeed ^ 0x4B1D);
            int initial = Math.Min(capacity, 11 + random.Next(0, 11));
            for (int i = 0; i < initial; i++)
                onboard.Add(new PassengerJourney(0, CabStationNetwork.NextDestinationIndex(0, random)));
        }

        public CabPassengerStopReport CallAt(CabStationDefinition station)
        {
            if (station == null) return new CabPassengerStopReport(null, 0, 0, onboard.Count, capacity);

            int alighted = 0;
            for (int i = onboard.Count - 1; i >= 0; i--)
            {
                if (onboard[i].DestinationIndex != station.RouteIndex) continue;
                onboard.RemoveAt(i);
                alighted++;
            }

            // Busy through stations receive more people, terminals fewer.  The fixed random source
            // makes this repeatable after restarting the same journey.
            int desiredBoarding = station.IsTerminal ? 2 + random.Next(0, 6) : 4 + random.Next(0, 11);
            int boarded = Math.Min(desiredBoarding, capacity - onboard.Count);
            List<string> destinations = new List<string>(boarded);
            for (int i = 0; i < boarded; i++)
            {
                int destinationIndex = CabStationNetwork.NextDestinationIndex(station.RouteIndex, random);
                onboard.Add(new PassengerJourney(station.RouteIndex, destinationIndex));
                CabStationDefinition destination = CabStationNetwork.ByRouteIndex(destinationIndex);
                if (destination != null && !destinations.Contains(destination.DisplayName)) destinations.Add(destination.DisplayName);
            }

            return new CabPassengerStopReport(station, boarded, alighted, onboard.Count, capacity, destinations);
        }

        private readonly struct PassengerJourney
        {
            public readonly int OriginIndex;
            public readonly int DestinationIndex;
            public PassengerJourney(int originIndex, int destinationIndex)
            {
                OriginIndex = originIndex;
                DestinationIndex = destinationIndex;
            }
        }
    }

    public sealed class CabPassengerStopReport
    {
        public CabStationDefinition Station { get; }
        public int Boarded { get; }
        public int Alighted { get; }
        public int Onboard { get; }
        public int Capacity { get; }
        public IReadOnlyList<string> BoardedDestinations { get; }
        public float Occupancy01 => Capacity > 0 ? Onboard / (float)Capacity : 0f;

        public CabPassengerStopReport(CabStationDefinition station, int boarded, int alighted, int onboard, int capacity,
            IReadOnlyList<string> boardedDestinations = null)
        {
            Station = station;
            Boarded = boarded;
            Alighted = alighted;
            Onboard = onboard;
            Capacity = capacity;
            BoardedDestinations = boardedDestinations ?? Array.Empty<string>();
        }

        public string CompactText => $"ПАССАЖИРЫ {Onboard}/{Capacity}";
        public string StopText => Station == null
            ? CompactText
            : $"{Station.DisplayName}: +{Boarded}  −{Alighted}\n{(BoardedDestinations.Count > 0 ? "До: " + string.Join(", ", BoardedDestinations.Take(2)) + "\n" : string.Empty)}{CompactText}";
    }
}
