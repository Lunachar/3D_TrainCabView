using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// The endless immersive world. Chunks are built ahead of the train and dropped behind it;
    /// the world is moved under the fixed driver's camera, and the coordinates are re-centred
    /// near the train every kilometre so they never lose precision. Also answers the ride's
    /// questions about the route: the scenery here, the next station, the tunnel.
    /// </summary>
    public sealed class CabStreamedWorld
    {
        public const float RebaseDistance = 1000f;
        private const float BehindReach = 150f;
        private const float NearDetailReach = 230f;

        private readonly WorldPlanner planner;
        private readonly TrackPath path;
        private readonly WorldTerrain terrain;
        private readonly WorldChunkBuilder builder;
        private readonly Transform routeRoot;
        private readonly MonoBehaviour host;
        private readonly Dictionary<int, WorldChunk> chunks = new Dictionary<int, WorldChunk>();
        private readonly Dictionary<WorldChunkKind, RouteSegmentDefinition> segments = new Dictionary<WorldChunkKind, RouteSegmentDefinition>();
        private readonly List<Light> lightScratch = new List<Light>();
        private readonly int chunksAhead;
        private readonly int lightBudget;
        private double originX;
        private double originZ;
        private float originDistance;
        private Coroutine building;
        private int buildingIndex = -1;
        private SeasonType season = SeasonType.Summer;
        private float density = 1f;
        private WorldChunkKind currentKind;
        private int runStartIndex;
        private bool nightLights;
        private bool tunnelLights;
        private float nextLightSort;
        private WorldChunk stationChunk;
        private CabTrainConsist consist;
        private WorldTraffic traffic;
        private float trafficDistance;
        public WorldTraffic Traffic => traffic;

        public event Action<RouteSegmentDefinition> SegmentChanged;
        public float Distance { get; private set; }
        public WorldPlanner Planner => planner;
        public TrackPath Path => path;
        public WorldTerrain Terrain => terrain;
        public Transform RouteRoot => routeRoot;
        public int LoadedChunkCount => chunks.Count;
        public RouteSegmentDefinition CurrentSegment => SegmentFor(currentKind);
        public string CurrentSegmentName => WorldPlanner.KindName(currentKind);
        public float ViewDistance => chunksAhead * WorldPlanner.ChunkLength;

        public float SegmentProgress
        {
            get
            {
                int end = planner.At(Distance).Index;
                while (planner.Get(end + 1).Kind == currentKind) end++;
                float start = runStartIndex * WorldPlanner.ChunkLength;
                return Mathf.Clamp01((Distance - start) / Mathf.Max(1f, (end + 1) * WorldPlanner.ChunkLength - start));
            }
        }

        public CabStreamedWorld(Transform parent, MonoBehaviour coroutineHost, int seed, int aheadChunks, int lights)
        {
            planner = new WorldPlanner(seed);
            path = new TrackPath(planner);
            terrain = new WorldTerrain(planner, seed);
            builder = new WorldChunkBuilder(path, terrain);
            host = coroutineHost;
            chunksAhead = Mathf.Max(3, aheadChunks);
            lightBudget = Mathf.Max(2, lights);
            routeRoot = new GameObject("StreamedWorld").transform;
            routeRoot.SetParent(parent, false);
            currentKind = planner.Get(0).Kind;
            traffic = new WorldTraffic(this, seed, aheadChunks > 5 ? 14 : 9);
        }

        /// <summary>Moves cars, barriers and trains on the other track.</summary>
        public void UpdateTraffic(float deltaTime, bool night)
        {
            traffic?.Update(deltaTime, Distance, trafficDistance, night);
            trafficDistance = Distance;
        }

        /// <summary>Season and density for new chunks; rebuilds what is already there.</summary>
        public void Configure(SeasonType currentSeason, float sceneryDensity)
        {
            bool changed = currentSeason != season || !Mathf.Approximately(sceneryDensity, density) || chunks.Count == 0;
            season = currentSeason;
            density = sceneryDensity;
            builder.Configure(season, density);
            TreeImpostors.Bake(season);
            WorldMaterials.Terrain(season);
            if (!changed) return;
            StopBuilding();
            foreach (WorldChunk chunk in chunks.Values) chunk.Destroy();
            chunks.Clear();
            stationChunk = null;
            Stream(true);
        }

        public void SetDistance(float distance)
        {
            Distance = Mathf.Max(0f, distance);
            Rebase();
            Pose();
            UpdateSegment();
            Stream(false);
        }

        public void Advance(float delta)
        {
            SetDistance(Distance + Mathf.Max(0f, delta));
        }

        /// <summary>Loads every chunk the view needs right now (used at start and in previews).</summary>
        public void LoadAll()
        {
            Stream(true);
            traffic?.Reset(Distance);
            trafficDistance = Distance;
        }

        // ---- Streaming --------------------------------------------------------------------------

        private void Stream(bool immediate)
        {
            int current = planner.At(Distance).Index;
            int first = Mathf.Max(0, Mathf.FloorToInt((Distance - BehindReach) / WorldPlanner.ChunkLength));
            int last = current + chunksAhead;

            List<int> drop = null;
            foreach (int index in chunks.Keys)
                if (index < first || index > last + 1) (drop ??= new List<int>()).Add(index);
            if (drop != null)
                foreach (int index in drop)
                {
                    if (stationChunk == chunks[index]) stationChunk = null;
                    chunks[index].Destroy();
                    chunks.Remove(index);
                }

            for (int index = first; index <= last; index++)
            {
                if (chunks.TryGetValue(index, out WorldChunk existing))
                {
                    if (existing.Complete)
                        existing.SetNear(Mathf.Abs(existing.Plan.Start + WorldPlanner.ChunkLength * 0.5f - Distance) < NearDetailReach);
                    continue;
                }
                if (immediate || index <= current + 1)
                {
                    // The view must never show a hole: nearby chunks are built on the spot.
                    if (buildingIndex == index) StopBuilding();
                    BuildNow(index);
                }
                else if (building == null && host != null && host.isActiveAndEnabled && Application.isPlaying)
                {
                    buildingIndex = index;
                    building = host.StartCoroutine(BuildLater(index));
                }
            }
        }

        private void BuildNow(int index)
        {
            WorldChunk chunk = new WorldChunk();
            IEnumerator steps = builder.Build(planner.Get(index), originX, originZ, routeRoot, chunk);
            while (steps.MoveNext()) { }
            Register(index, chunk);
        }

        private IEnumerator BuildLater(int index)
        {
            WorldChunk chunk = new WorldChunk();
            IEnumerator steps = builder.Build(planner.Get(index), originX, originZ, routeRoot, chunk);
            double builtForX = originX, builtForZ = originZ;
            while (steps.MoveNext()) yield return null;
            building = null;
            buildingIndex = -1;
            if (chunks.ContainsKey(index))
            {
                chunk.Destroy();
                yield break;
            }
            // A rebase may have happened while the chunk was being built.
            if (chunk.Root != null && (builtForX != originX || builtForZ != originZ))
                chunk.Root.transform.localPosition -= new Vector3((float)(originX - builtForX), 0f, (float)(originZ - builtForZ));
            Register(index, chunk);
        }

        private void Register(int index, WorldChunk chunk)
        {
            chunks[index] = chunk;
            CabWorldText.Apply(chunk.Root.transform);
            chunk.SetNear(Mathf.Abs(chunk.Plan.Start + WorldPlanner.ChunkLength * 0.5f - Distance) < NearDetailReach);
            SetLightsOn(chunk.Lamps, false);
            SetLightsOn(chunk.TunnelLights, false);
            nextLightSort = 0f;
        }

        private void StopBuilding()
        {
            if (building != null && host != null) host.StopCoroutine(building);
            building = null;
            buildingIndex = -1;
        }

        // ---- Pose and origin --------------------------------------------------------------------

        private void Rebase()
        {
            if (Mathf.Abs(Distance - originDistance) < RebaseDistance) return;
            path.Point(Distance, out double x, out double z);
            Vector3 shift = new Vector3((float)(x - originX), 0f, (float)(z - originZ));
            originX = x;
            originZ = z;
            originDistance = Distance;
            foreach (WorldChunk chunk in chunks.Values)
                if (chunk.Root != null) chunk.Root.transform.localPosition -= shift;
            if (consist != null)
                foreach (Transform car in consist.Cars) car.localPosition -= shift;
        }

        private void Pose()
        {
            Vector3 point = path.Local(Distance, 0f, 0f, originX, originZ);
            Quaternion heading = path.Rotation(Distance);
            routeRoot.localRotation = Quaternion.Inverse(heading);
            routeRoot.localPosition = routeRoot.localRotation * -point;
            consist?.UpdatePose(Distance, d => path.Local(d, 0f, 0f, originX, originZ));
        }

        /// <summary>Route point in the streamed world's local coordinates (for the train and other movers).</summary>
        public Vector3 LocalPoint(float distance, float lateral = 0f, float height = 0f) => path.Local(distance, lateral, height, originX, originZ);

        public void AttachConsist(CabTrainConsist trainConsist)
        {
            consist = trainConsist;
            Pose();
        }

        // ---- Route information -----------------------------------------------------------------

        private void UpdateSegment()
        {
            WorldChunkPlan here = planner.At(Distance);
            if (here.Kind == currentKind) return;
            currentKind = here.Kind;
            runStartIndex = here.Index;
            SegmentChanged?.Invoke(SegmentFor(currentKind));
        }

        public RouteSegmentDefinition SegmentFor(WorldChunkKind kind)
        {
            if (segments.TryGetValue(kind, out RouteSegmentDefinition segment) && segment != null) return segment;
            RouteSegmentType type = kind switch
            {
                WorldChunkKind.Forest => RouteSegmentType.Forest,
                WorldChunkKind.Village => RouteSegmentType.Village,
                WorldChunkKind.Town => RouteSegmentType.Town,
                WorldChunkKind.City => RouteSegmentType.Town,
                WorldChunkKind.Industrial => RouteSegmentType.Road,
                WorldChunkKind.Tunnel => RouteSegmentType.MountainTunnel,
                WorldChunkKind.Water => RouteSegmentType.Water,
                _ => RouteSegmentType.Meadow
            };
            segment = ScriptableObject.CreateInstance<RouteSegmentDefinition>();
            segment.Configure(WorldPlanner.KindName(kind), type, WorldPlanner.ChunkLength, 1f, 0, 1f, Color.white,
                kind == WorldChunkKind.Village || kind == WorldChunkKind.Town, false);
            segment.name = "Streamed_" + kind;
            segments[kind] = segment;
            return segment;
        }

        public float TunnelBlend => planner.TunnelBlend(Distance);

        /// <summary>0 in open country, 1 in a big city: towns light up the night sky.</summary>
        public float Urban
        {
            get
            {
                float sum = 0f;
                for (int i = -2; i <= 4; i++)
                {
                    float d = Mathf.Max(0f, Distance + i * 60f);
                    sum += terrain.Weight(d, WorldChunkKind.City) + 0.55f * terrain.Weight(d, WorldChunkKind.Town) +
                           0.3f * terrain.Weight(d, WorldChunkKind.Industrial) + 0.15f * terrain.Weight(d, WorldChunkKind.Village);
                }
                return Mathf.Clamp01(sum / 7f);
            }
        }

        /// <summary>Distance of the next stopping point at or after <paramref name="distance"/>, and its station.</summary>
        public float NextStopAfter(float distance, out CabStationDefinition station)
        {
            float stop = planner.NextStopAfter(distance, out WorldChunkPlan plan);
            station = stop >= 0f ? WorldPlanner.StationDefinition(plan) : null;
            return stop;
        }

        // ---- Stations ---------------------------------------------------------------------------

        private WorldChunk StationChunk()
        {
            if (stationChunk != null && stationChunk.Root != null) return stationChunk;
            float stop = planner.NextStopAfter(Distance - 60f, out WorldChunkPlan plan);
            if (stop < 0f || !chunks.TryGetValue(plan.Index, out WorldChunk chunk)) return null;
            stationChunk = chunk;
            return chunk;
        }

        public void SetStationPhase(CabStationPhase phase)
        {
            if (phase == CabStationPhase.Idle || phase == CabStationPhase.Complete || phase == CabStationPhase.Cancelled) stationChunk = null;
            WorldChunk chunk = StationChunk();
            if (chunk == null) return;
            bool visible = phase == CabStationPhase.Approaching || phase == CabStationPhase.WaitingForDoors || phase == CabStationPhase.DoorsOpen ||
                           phase == CabStationPhase.Releasing;
            foreach (Cab3DPassengerAgent agent in chunk.Passengers)
            {
                if (agent == null) continue;
                agent.SetPlatformVisible(visible);
                agent.SetDoorsOpen(phase == CabStationPhase.DoorsOpen);
            }
        }

        public void SetPassengerReport(CabPassengerStopReport report)
        {
            WorldChunk chunk = StationChunk();
            if (chunk == null || report == null) return;
            int departing = Mathf.Min(chunk.Passengers.Count, report.Boarded);
            int arriving = Mathf.Min(Mathf.Max(0, chunk.Passengers.Count - departing), report.Alighted);
            for (int i = 0; i < chunk.Passengers.Count; i++)
                chunk.Passengers[i]?.SetFlow(i < departing, i >= departing && i < departing + arriving);
        }

        public void SetPassengerWeather(WeatherType weather)
        {
            foreach (WorldChunk chunk in chunks.Values)
                foreach (Cab3DPassengerAgent agent in chunk.Passengers)
                    agent?.SetWetWeather(weather == WeatherType.Rain);
        }

        public IEnumerable<Cab3DInteractiveObject> Interactives()
        {
            foreach (WorldChunk chunk in chunks.Values)
                foreach (Cab3DPassengerAgent agent in chunk.Passengers)
                {
                    Cab3DInteractiveObject item = agent != null ? agent.GetComponent<Cab3DInteractiveObject>() : null;
                    if (item != null) yield return item;
                }
        }

        // ---- Lights ----------------------------------------------------------------------------

        /// <summary>Street and platform lamps at night, tunnel lamps in the tunnel; only the nearest few get a real light.</summary>
        public void UpdateLights(bool night, float night01, Vector3 eye)
        {
            WorldMaterials.SetNight(night ? Mathf.Max(0.6f, night01) : night01);
            bool inTunnel = TunnelBlend > 0.02f;
            if (night == nightLights && inTunnel == tunnelLights && Time.unscaledTime < nextLightSort) return;
            nightLights = night;
            tunnelLights = inTunnel;
            nextLightSort = Time.unscaledTime + 0.25f;
            lightScratch.Clear();
            foreach (WorldChunk chunk in chunks.Values)
            {
                foreach (GameObject item in chunk.NightOnly) if (item != null && item.activeSelf != night) item.SetActive(night);
                SetLightsOn(chunk.Lamps, false);
                SetLightsOn(chunk.TunnelLights, false);
                if (night) lightScratch.AddRange(chunk.Lamps);
                if (inTunnel) lightScratch.AddRange(chunk.TunnelLights);
            }
            lightScratch.RemoveAll(light => light == null || !light.gameObject.activeInHierarchy);
            lightScratch.Sort((a, b) => (a.transform.position - eye).sqrMagnitude.CompareTo((b.transform.position - eye).sqrMagnitude));
            for (int i = 0; i < lightScratch.Count && i < lightBudget; i++) lightScratch[i].enabled = true;
        }

        public int EnabledLightCount
        {
            get
            {
                int count = 0;
                foreach (WorldChunk chunk in chunks.Values)
                {
                    foreach (Light light in chunk.Lamps) if (light != null && light.enabled) count++;
                    foreach (Light light in chunk.TunnelLights) if (light != null && light.enabled) count++;
                }
                return count;
            }
        }

        private static void SetLightsOn(List<Light> lights, bool on)
        {
            for (int i = 0; i < lights.Count; i++) if (lights[i] != null) lights[i].enabled = on;
        }

        public void Dispose()
        {
            traffic?.Dispose();
            StopBuilding();
            foreach (WorldChunk chunk in chunks.Values) chunk.Destroy();
            chunks.Clear();
            foreach (RouteSegmentDefinition segment in segments.Values)
                if (segment != null) UnityEngine.Object.Destroy(segment);
            segments.Clear();
        }

        public IEnumerable<WorldChunk> Chunks => chunks.Values;
    }
}
