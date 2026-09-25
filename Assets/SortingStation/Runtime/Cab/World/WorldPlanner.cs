using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    /// <summary>What a 120 m piece of the endless route is made of.</summary>
    public enum WorldChunkKind
    {
        Meadow,
        Field,
        Forest,
        Village,
        Town,
        Industrial,
        Foothills,
        Tunnel,
        Water,
        City
    }

    /// <summary>What a tunnel is lined with (or not): cast concrete, old brick, or a natural cave.</summary>
    public enum TunnelStyle
    {
        Concrete,
        Brick,
        Cave
    }

    public enum TunnelSize
    {
        Small,
        Normal,
        Large
    }

    /// <summary>Rare things happening at a station.</summary>
    public enum StationEvent
    {
        None,
        Festival,
        Police,
        Market,
        Wedding,
        TrackWorks,
        BrassBand,
        FireDrill,
        Circus,
        Hikers,
        Welcome,
        FilmCrew,
        Concert,
        Ambulance,
        Fireworks
    }

    /// <summary>How grand a station is: a bare halt in the fields up to a city terminal with a train shed.</summary>
    public enum StationStyle
    {
        Halt,
        Village,
        Town,
        Terminal,
        /// <summary>An underground halt in a miners' cave, kept by gnomes.</summary>
        Gnome
    }

    public struct WorldChunkPlan
    {
        public int Index;
        public WorldChunkKind Kind;
        /// <summary>What lies to the right of the line; usually the same as <see cref="Kind"/> (left side).</summary>
        public WorldChunkKind RightKind;
        /// <summary>Track curvature (1/m) reached after the chunk's ease-in; 0 is straight.</summary>
        public float EndCurvature;
        /// <summary>Station number along the journey (names come from CabStationNetwork), or -1.</summary>
        public int StationNumber;
        public bool Crossing;
        public StationStyle Station;
        /// <summary>A road runs beside the line on this side (left / right of the track).</summary>
        public bool RoadLeft;
        public bool RoadRight;
        /// <summary>City chunks: small houses packed right up to the line instead of tall blocks.</summary>
        public bool DenseLowRise;
        public TunnelStyle Tunnel;
        public TunnelSize Bore;
        /// <summary>A tunnel chunk that opens into a great cavern with a miners' settlement.</summary>
        public bool CaveHall;
        public StationEvent Event;
        /// <summary>True for the first and last tunnel chunk: they hold the portals.</summary>
        public bool TunnelEntrance;
        public bool TunnelExit;
        public int Seed;

        public bool IsStation => StationNumber >= 0;
        public WorldChunkKind KindOnSide(int side) => side < 0 ? Kind : RightKind;
        public bool Has(WorldChunkKind kind) => Kind == kind || RightKind == kind;
        public float Start => Index * WorldPlanner.ChunkLength;
        public float End => Start + WorldPlanner.ChunkLength;
    }

    /// <summary>
    /// Deterministic plan of the endless route: a seeded sequence of scenery "runs" (a forest
    /// several chunks long, a village with a station, foothills around a tunnel...) cut into
    /// 120 m chunks. The same seed always gives the same world.
    /// </summary>
    public sealed class WorldPlanner
    {
        public const float ChunkLength = 120f;
        /// <summary>The platform centre sits in the middle of a station chunk; the cab stops past it.</summary>
        public const float PlatformCentre = ChunkLength * 0.5f;
        public const float StopOffsetPastPlatformCentre = 23f;
        public const float PlatformLength = 52f;
        public const float PortalInset = 6f;
        public const int MinimumChunksBetweenStations = 20;
        public const int MaximumChunksBetweenStations = 32;

        private readonly List<WorldChunkPlan> plans = new List<WorldChunkPlan>();
        private readonly System.Random random;
        private readonly int seed;
        private readonly Dictionary<WorldChunkKind, int> lastRunEnd = new Dictionary<WorldChunkKind, int>();
        private WorldChunkKind lastRun = WorldChunkKind.Meadow;
        private int lastStationChunk = -30;
        private int lastCrossingChunk = -10;
        private int stationCount;
        private float curvatureSign = 1f;

        public WorldPlanner(int routeSeed)
        {
            seed = routeSeed;
            random = new System.Random(routeSeed * 7919 + 17);
            // The ride starts in a meadow; a village with the first station follows shortly.
            AddRun(WorldChunkKind.Meadow, 2);
            AddRun(WorldChunkKind.Village, 3);
        }

        public WorldChunkPlan Get(int index)
        {
            if (index < 0) index = 0;
            while (plans.Count <= index + 4) AddNextRun();
            return plans[index];
        }

        public WorldChunkPlan At(float distance) => Get(Mathf.FloorToInt(Mathf.Max(0f, distance) / ChunkLength));

        /// <summary>Route distance of the next stopping point at or after the given distance.</summary>
        public float NextStopAfter(float distance, out WorldChunkPlan station)
        {
            int index = Mathf.Max(0, Mathf.FloorToInt((distance - StopOffsetPastPlatformCentre - PlatformCentre) / ChunkLength));
            for (int i = index; i < index + MaximumChunksBetweenStations * 3; i++)
            {
                WorldChunkPlan plan = Get(i);
                if (!plan.IsStation) continue;
                float stop = StopDistance(plan);
                if (stop < distance - 0.01f) continue;
                station = plan;
                return stop;
            }
            station = default;
            return -1f;
        }

        public static float StopDistance(WorldChunkPlan station) => station.Start + PlatformCentre + StopOffsetPastPlatformCentre;

        /// <summary>Distance range of the tunnel bore containing the distance, if any.</summary>
        public bool TryGetTunnel(float distance, out float entrance, out float exit)
        {
            entrance = exit = 0f;
            WorldChunkPlan plan = At(distance);
            if (plan.Kind != WorldChunkKind.Tunnel) return false;
            int first = plan.Index;
            while (first > 0 && Get(first - 1).Kind == WorldChunkKind.Tunnel) first--;
            int last = plan.Index;
            while (Get(last + 1).Kind == WorldChunkKind.Tunnel) last++;
            entrance = first * ChunkLength + PortalInset;
            exit = (last + 1) * ChunkLength - PortalInset;
            return distance >= entrance && distance <= exit;
        }

        /// <summary>0 outside, 1 deep inside a tunnel; eases over the first and last 60 m of the bore.</summary>
        public float TunnelBlend(float distance)
        {
            if (!TryGetTunnel(distance, out float entrance, out float exit)) return 0f;
            float inside = Mathf.Min(distance - entrance, exit - distance);
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((inside + 4f) / 60f));
        }

        private static readonly string[] GnomeStations = { "Самоцветная", "Гномья Штольня", "Рудник Глубинный", "Кристальная", "Малахитовая", "Серебряная Жила" };

        /// <summary>The station a planned stop belongs to (gnome halts have their own names).</summary>
        public static CabStationDefinition StationDefinition(WorldChunkPlan plan)
        {
            if (plan.Station == StationStyle.Gnome)
                return new CabStationDefinition("gnome-" + plan.Index, GnomeStations[plan.Index % GnomeStations.Length], "Подземная линия", 100 + plan.Index);
            return CabStationNetwork.AtSequence(plan.StationNumber);
        }

        public static string KindName(WorldChunkKind kind)
        {
            switch (kind)
            {
                case WorldChunkKind.Field: return "Поля";
                case WorldChunkKind.Forest: return "Лес";
                case WorldChunkKind.Village: return "Деревня";
                case WorldChunkKind.Town: return "Город";
                case WorldChunkKind.Industrial: return "Промзона";
                case WorldChunkKind.Foothills: return "Предгорья";
                case WorldChunkKind.Tunnel: return "Тоннель";
                case WorldChunkKind.Water: return "Река";
                case WorldChunkKind.City: return "Большой город";
                default: return "Луга";
            }
        }

        private void AddNextRun()
        {
            int next = plans.Count;
            int sinceStation = next - lastStationChunk;
            WorldChunkKind kind;
            if (sinceStation >= MaximumChunksBetweenStations - 3)
            {
                // Overdue: a village or a town always brings the next station.
                kind = random.NextDouble() < 0.7 ? WorldChunkKind.Village : WorldChunkKind.Town;
            }
            else
            {
                kind = PickWeighted(next, sinceStation);
            }
            switch (kind)
            {
                case WorldChunkKind.Foothills:
                case WorldChunkKind.Tunnel:
                {
                    double style = random.NextDouble();
                    TunnelStyle tunnel = style < 0.4 ? TunnelStyle.Concrete : style < 0.65 ? TunnelStyle.Brick : TunnelStyle.Cave;
                    TunnelSize bore = tunnel == TunnelStyle.Cave ? TunnelSize.Large : (TunnelSize)random.Next(0, 3);
                    AddRun(WorldChunkKind.Foothills, 1);
                    int start = plans.Count;
                    int length = tunnel == TunnelStyle.Cave ? 3 + random.Next(0, 3) : 1 + random.Next(0, 3);
                    AddRun(WorldChunkKind.Tunnel, length);
                    for (int i = start; i < plans.Count; i++)
                    {
                        WorldChunkPlan plan = plans[i];
                        plan.Tunnel = tunnel;
                        plan.Bore = bore;
                        // A cave opens into a miners' hall in its middle, with a gnome halt when a stop is due.
                        if (tunnel == TunnelStyle.Cave && i == start + length / 2)
                        {
                            plan.CaveHall = true;
                            if (i - lastStationChunk >= 12)
                            {
                                plan.StationNumber = ++stationCount;
                                plan.Station = StationStyle.Gnome;
                                lastStationChunk = i;
                            }
                        }
                        plans[i] = plan;
                    }
                    AddRun(WorldChunkKind.Foothills, 1);
                    lastRun = WorldChunkKind.Foothills;
                    lastRunEnd[WorldChunkKind.Tunnel] = plans.Count;
                    return;
                }
                case WorldChunkKind.Forest: AddRun(kind, 2 + random.Next(0, 3)); break;
                case WorldChunkKind.Village: AddRun(kind, 2 + random.Next(0, 2)); break;
                case WorldChunkKind.Town: AddRun(kind, 2 + random.Next(0, 2)); break;
                case WorldChunkKind.Industrial: AddRun(kind, 1 + random.Next(0, 2)); break;
                case WorldChunkKind.Water: AddRun(kind, 1); break;
                case WorldChunkKind.City: AddRun(kind, 4 + random.Next(0, 3)); break;
                default: AddRun(kind, 1 + random.Next(0, 3)); break;
            }
        }

        private WorldChunkKind PickWeighted(int next, int sinceStation)
        {
            bool stationSoon = sinceStation >= MinimumChunksBetweenStations - 2;
            (WorldChunkKind kind, float weight, int gap)[] options =
            {
                (WorldChunkKind.Meadow, 1.2f, 0),
                (WorldChunkKind.Field, 1.0f, 0),
                (WorldChunkKind.Forest, 1.4f, 0),
                (WorldChunkKind.Village, stationSoon ? 2.4f : 0.6f, 6),
                (WorldChunkKind.Town, stationSoon ? 1.2f : 0.3f, 12),
                (WorldChunkKind.Industrial, lastRun == WorldChunkKind.Town ? 1.4f : 0.25f, 10),
                (WorldChunkKind.Tunnel, 0.45f, 14),
                (WorldChunkKind.Water, 0.4f, 8),
                (WorldChunkKind.City, stationSoon ? 0.7f : 0.2f, 24)
            };
            float total = 0f;
            for (int i = 0; i < options.Length; i++)
                if (Allowed(options[i].kind, options[i].gap, next)) total += options[i].weight;
            double pick = random.NextDouble() * total;
            for (int i = 0; i < options.Length; i++)
            {
                if (!Allowed(options[i].kind, options[i].gap, next)) continue;
                pick -= options[i].weight;
                if (pick <= 0d) return options[i].kind;
            }
            return WorldChunkKind.Meadow;
        }

        private bool Allowed(WorldChunkKind kind, int gap, int next)
        {
            if (kind == lastRun) return false;
            // Mountains need open country around them, not a town next door.
            if (kind == WorldChunkKind.Tunnel && (lastRun == WorldChunkKind.Town || lastRun == WorldChunkKind.Industrial || lastRun == WorldChunkKind.City)) return false;
            if (kind == WorldChunkKind.City && lastRun == WorldChunkKind.Town) return false;
            return !lastRunEnd.TryGetValue(kind, out int end) || next - end >= gap;
        }

        private void AddRun(WorldChunkKind kind, int length)
        {
            int first = plans.Count;
            // Settlements get their station on the second chunk, so the chunk before it can
            // straighten the line first.
            bool settlement = kind == WorldChunkKind.Village || kind == WorldChunkKind.Town || kind == WorldChunkKind.City;
            int stationAt = -1;
            // A big city always has its terminal in the middle; smaller places respect the spacing.
            if (kind == WorldChunkKind.City) stationAt = first + length / 2;
            else if (settlement && length >= 2 && first + 1 - lastStationChunk >= MinimumChunksBetweenStations) stationAt = first + 1;
            // Roads: settlements always have one, open country often, never in the mountains or at rivers.
            bool mountain = kind == WorldChunkKind.Foothills || kind == WorldChunkKind.Tunnel || kind == WorldChunkKind.Water;
            float roadChance = settlement || kind == WorldChunkKind.Industrial ? 1f : kind == WorldChunkKind.Forest ? 0.3f : 0.55f;
            bool road = !mountain && random.NextDouble() < roadChance;
            bool roadRight = random.NextDouble() < 0.5;
            bool bothSides = kind == WorldChunkKind.City || (kind == WorldChunkKind.Town && random.NextDouble() < 0.5);
            bool dense = kind == WorldChunkKind.City && random.NextDouble() < 0.5;
            // A third of the runs show a different world on the right: a town facing a forest, a village facing fields...
            WorldChunkKind right = kind;
            if (random.NextDouble() < 0.33) right = PartnerOf(kind);
            for (int i = 0; i < length; i++)
            {
                int index = first + i;
                WorldChunkPlan plan = new WorldChunkPlan
                {
                    Index = index,
                    Kind = kind,
                    RightKind = right,
                    StationNumber = -1,
                    Seed = Hash(seed, index)
                };
                bool straight = kind == WorldChunkKind.Tunnel || kind == WorldChunkKind.Water ||
                                index == stationAt || index + 1 == stationAt ||
                                // The foothills before a tunnel end straight, so the bore is straight.
                                (kind == WorldChunkKind.Foothills && lastRun != WorldChunkKind.Tunnel);
                if (index == stationAt)
                {
                    plan.StationNumber = ++stationCount;
                    lastStationChunk = index;
                    plan.Station = kind == WorldChunkKind.City ? StationStyle.Terminal
                        : kind == WorldChunkKind.Town ? StationStyle.Town
                        : random.NextDouble() < 0.4 ? StationStyle.Halt : StationStyle.Village;
                    // Something special happens at roughly one station in six.
                    if (random.NextDouble() < 0.17) plan.Event = (StationEvent)(1 + random.Next(0, 14));
                }
                plan.RoadLeft = road && (bothSides || !roadRight);
                plan.RoadRight = road && (bothSides || roadRight);
                plan.DenseLowRise = dense;
                bool crossingCountry = kind == WorldChunkKind.Meadow || kind == WorldChunkKind.Field || kind == WorldChunkKind.Village;
                if (crossingCountry && index != stationAt && index + 1 != stationAt && index - lastCrossingChunk >= 5 && random.NextDouble() < 0.35)
                {
                    plan.Crossing = true;
                    lastCrossingChunk = index;
                }
                plan.TunnelEntrance = kind == WorldChunkKind.Tunnel && i == 0;
                plan.TunnelExit = kind == WorldChunkKind.Tunnel && i == length - 1;
                plan.EndCurvature = straight || index < 2 ? 0f : PickCurvature();
                plans.Add(plan);
            }
            lastRun = kind;
            lastRunEnd[kind] = plans.Count;
        }

        private WorldChunkKind PartnerOf(WorldChunkKind kind)
        {
            WorldChunkKind[] options;
            switch (kind)
            {
                case WorldChunkKind.Town: options = new[] { WorldChunkKind.Forest, WorldChunkKind.Field, WorldChunkKind.Meadow, WorldChunkKind.Industrial }; break;
                case WorldChunkKind.City: options = new[] { WorldChunkKind.Forest, WorldChunkKind.Industrial, WorldChunkKind.Town }; break;
                case WorldChunkKind.Village: options = new[] { WorldChunkKind.Forest, WorldChunkKind.Field, WorldChunkKind.Meadow }; break;
                case WorldChunkKind.Industrial: options = new[] { WorldChunkKind.Meadow, WorldChunkKind.Town, WorldChunkKind.Forest }; break;
                case WorldChunkKind.Meadow: options = new[] { WorldChunkKind.Forest, WorldChunkKind.Village, WorldChunkKind.Field }; break;
                case WorldChunkKind.Field: options = new[] { WorldChunkKind.Forest, WorldChunkKind.Village, WorldChunkKind.Meadow }; break;
                case WorldChunkKind.Forest: options = new[] { WorldChunkKind.Field, WorldChunkKind.Meadow, WorldChunkKind.Village }; break;
                default: return kind;
            }
            return options[random.Next(0, options.Length)];
        }

        private float PickCurvature()
        {
            if (random.NextDouble() < 0.35) return 0f;
            // Radii of 900-2500 m: gentle main-line curves, never tighter than the terrain allows.
            float radius = Mathf.Lerp(900f, 2500f, (float)random.NextDouble());
            if (random.NextDouble() < 0.6) curvatureSign = -curvatureSign;
            return curvatureSign / radius;
        }

        public static int Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u ^ ((uint)b + 0x7F4A7C15u) * 0x85EBCA77u;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                return (int)(h & 0x7FFFFFFF);
            }
        }
    }
}
