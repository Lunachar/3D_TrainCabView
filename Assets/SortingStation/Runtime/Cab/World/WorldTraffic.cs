using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// Life on and around the line: cars on the roads beside the track (right-hand traffic,
    /// keeping their distance), level crossings whose barriers close before a train and the
    /// queues of cars that wait at them, and trains met on the other track.
    /// </summary>
    public sealed class WorldTraffic
    {
        private sealed class Car
        {
            public GameObject Body;
            public VehicleType Type;
            public int Side;          // road on the left (-1) or right (+1) of the line; 0 = crossing road
            public int Direction;     // +1 along the route, -1 against it (crossing: +1 toward +x)
            public float Position;    // route distance, or lateral position on a crossing road
            public float Speed;
            public float Cruise;
            public float Length;
            public int CrossingIndex; // chunk index of its crossing
            public float WaitUntil;
        }

        private sealed class MeetingTrain
        {
            public Transform Root;
            public readonly List<Transform> Cars = new List<Transform>();
            public readonly List<float> Offsets = new List<float>();
            public readonly List<float> Lengths = new List<float>();
            public float Front;
            public float Speed;
            public float Length;
            public Light Headlight;
        }

        public const float CrossingStopGap = 7f;
        private readonly CabStreamedWorld world;
        private readonly Transform root;
        private readonly List<Car> cars = new List<Car>();
        private readonly System.Random random;
        private readonly int roadCarBudget;
        private MeetingTrain meeting;
        private float nextMeetingAt;
        private float trainSpeed;
        private float nextSpawnCheck;
        private bool night;

        public int CarCount => cars.Count;
        public bool HasMeetingTrain => meeting != null;
        public float MeetingTrainFront => meeting != null ? meeting.Front : -1f;

        public WorldTraffic(CabStreamedWorld streamedWorld, int seed, int carBudget)
        {
            world = streamedWorld;
            root = new GameObject("Traffic").transform;
            root.SetParent(world.RouteRoot, false);
            random = new System.Random(seed * 31 + 5);
            roadCarBudget = carBudget;
            nextMeetingAt = 600f + (float)random.NextDouble() * 1200f;
        }

        private float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);

        public void Update(float deltaTime, float trainDistance, float previousDistance, bool isNight)
        {
            float dt = Mathf.Clamp(deltaTime, 0f, 0.1f);
            trainSpeed = Mathf.Lerp(trainSpeed, dt > 0f ? (trainDistance - previousDistance) / dt : 0f, 0.2f);
            if (night != isNight)
            {
                night = isNight;
                if (meeting != null && meeting.Headlight != null) meeting.Headlight.enabled = night;
            }
            if (Time.unscaledTime >= nextSpawnCheck)
            {
                nextSpawnCheck = Time.unscaledTime + 0.4f;
                SpawnRoadCars(trainDistance);
                SpawnCrossingCars(trainDistance);
            }
            UpdateMeetingTrain(dt, trainDistance);
            UpdateCrossings(trainDistance);
            UpdateCars(dt, trainDistance);
        }

        /// <summary>Jump (previews): drop everything and let traffic appear around the new place.</summary>
        public void Reset(float trainDistance)
        {
            foreach (Car car in cars) Object.Destroy(car.Body);
            cars.Clear();
            if (meeting != null) Object.Destroy(meeting.Root.gameObject);
            meeting = null;
            nextMeetingAt = trainDistance + Range(300f, 1500f);
            nextSpawnCheck = 0f;
        }

        /// <summary>Spawn an oncoming train right now (previews).</summary>
        public void ForceMeetingTrain(float trainDistance, float ahead)
        {
            if (meeting != null) Object.Destroy(meeting.Root.gameObject);
            meeting = null;
            SpawnMeetingTrain(trainDistance + ahead);
        }

        /// <summary>Preview: cars queued on both sides of a crossing and some traffic on the roads nearby.</summary>
        public void PreviewTraffic(float trainDistance)
        {
            foreach (WorldChunk chunk in world.Chunks)
            {
                if (!chunk.Plan.Crossing) continue;
                for (int direction = -1; direction <= 1; direction += 2)
                {
                    float stopLine = direction > 0 ? PlacementMask.CorridorLeft - CrossingStopGap : PlacementMask.CorridorRight + CrossingStopGap;
                    for (int k = 0; k < 3; k++)
                        SpawnCar(0, direction, stopLine - direction * (3f + k * 7.5f), chunk.Plan.Index);
                }
            }
            for (int i = 0; i < 12; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                float d = trainDistance + 20f + i * 25f;
                if (WorldRoads.TryLateral(world.Planner, world.Terrain, d, side, out float x) && Mathf.Abs(x) < 150f)
                    SpawnCar(side, i % 3 == 0 ? -1 : 1, d, 0);
            }
            foreach (Car car in cars) car.Speed = car.Side == 0 ? 0f : car.Cruise;
        }

        // ---- Road cars -------------------------------------------------------------------------

        private void SpawnRoadCars(float trainDistance)
        {
            int roadCars = 0;
            foreach (Car car in cars) if (car.Side != 0) roadCars++;
            if (roadCars >= roadCarBudget) return;
            float reach = world.ViewDistance - 60f;
            for (int attempt = 0; attempt < 3 && roadCars < roadCarBudget; attempt++)
            {
                int side = random.NextDouble() < 0.5 ? -1 : 1;
                int direction = random.NextDouble() < 0.5 ? -1 : 1;
                // Appear far ahead or behind, where the haze hides it, never right next to the cab.
                float d = random.NextDouble() < 0.7 ? trainDistance + Range(reach * 0.6f, reach) : Mathf.Max(0f, trainDistance - Range(100f, 140f));
                if (!WorldRoads.TryLateral(world.Planner, world.Terrain, d, side, out float x) || Mathf.Abs(x) > 150f) continue;
                WorldChunkKind kind = world.Planner.At(d).Kind;
                float density = kind == WorldChunkKind.City ? 1f : kind == WorldChunkKind.Town ? 0.8f : kind == WorldChunkKind.Village ? 0.4f : 0.3f;
                if (random.NextDouble() > density) continue;
                if (!LaneClear(side, direction, d, 20f)) continue;
                SpawnCar(side, direction, d, 0);
                roadCars++;
            }
        }

        private void SpawnCar(int side, int direction, float position, int crossing)
        {
            double pick = random.NextDouble();
            VehicleType type = pick < 0.28 ? VehicleType.Sedan : pick < 0.45 ? VehicleType.Hatchback : pick < 0.55 ? VehicleType.Wagon
                : pick < 0.68 ? VehicleType.Suv : pick < 0.78 ? VehicleType.OldSedan : pick < 0.87 ? VehicleType.Van : pick < 0.95 ? VehicleType.Truck : VehicleType.Bus;
            float cruise = type == VehicleType.Truck || type == VehicleType.Bus ? Range(12f, 17f) : Range(14f, 22f);
            if (side == 0) cruise *= 0.7f;
            Car car = new Car
            {
                Body = VehicleFactory.Create(root, type, random.Next(0, VehicleFactory.Paints.Length)),
                Type = type,
                Side = side,
                Direction = direction,
                Position = position,
                Cruise = cruise,
                Speed = cruise,
                Length = VehicleFactory.Size(type).z,
                CrossingIndex = crossing
            };
            cars.Add(car);
        }

        private bool LaneClear(int side, int direction, float position, float gap)
        {
            foreach (Car other in cars)
                if (other.Side == side && other.Direction == direction && Mathf.Abs(other.Position - position) < gap) return false;
            return true;
        }

        private void UpdateCars(float dt, float trainDistance)
        {
            float behind = trainDistance - 170f, ahead = trainDistance + world.ViewDistance;
            for (int i = cars.Count - 1; i >= 0; i--)
            {
                Car car = cars[i];
                float target = car.Cruise;
                // Keep a gap to the car in front in the same lane.
                float leadGap = float.MaxValue;
                foreach (Car other in cars)
                {
                    if (other == car || other.Side != car.Side || other.Direction != car.Direction || other.CrossingIndex != car.CrossingIndex) continue;
                    float gap = (other.Position - car.Position) * car.Direction - (other.Length + car.Length) * 0.5f;
                    if (gap > -0.5f && gap < leadGap) leadGap = gap;
                }
                if (leadGap < 30f) target = Mathf.Min(target, Mathf.Max(0f, (leadGap - 2.5f) * 0.9f));
                if (car.Side == 0) target = Mathf.Min(target, CrossingLimit(car, trainDistance));
                float accel = target > car.Speed ? 2.2f : 6f;
                car.Speed = Mathf.MoveTowards(car.Speed, target, accel * dt);
                car.Position += car.Direction * car.Speed * dt;

                bool keep = car.Side == 0 ? PlaceOnCrossing(car) : PlaceOnRoad(car);
                bool outOfRange = car.Side != 0 ? car.Position < behind || car.Position > ahead
                    : Mathf.Abs(car.Position) > 320f || car.CrossingIndex * WorldPlanner.ChunkLength < behind - 60f;
                if (!keep || outOfRange)
                {
                    Object.Destroy(car.Body);
                    cars.RemoveAt(i);
                }
            }
        }

        private bool PlaceOnRoad(Car car)
        {
            if (!WorldRoads.TryLateral(world.Planner, world.Terrain, car.Position, car.Side, out float x) || Mathf.Abs(x) > WorldRoads.AwayReach) return false;
            // Right-hand traffic: facing along the route the right lane is +x.
            float lane = x + car.Direction * WorldRoads.LaneOffset;
            float y = WorldRoads.Height(world.Terrain, car.Position, x);
            Vector3 here = world.LocalPoint(car.Position, lane, y);
            float step = car.Direction * 2f;
            WorldRoads.TryLateral(world.Planner, world.Terrain, car.Position + step, car.Side, out float xNext);
            Vector3 next = world.LocalPoint(car.Position + step, xNext + car.Direction * WorldRoads.LaneOffset, WorldRoads.Height(world.Terrain, car.Position + step, xNext));
            car.Body.transform.localPosition = here;
            Vector3 direction = next - here;
            if (direction.sqrMagnitude > 0.001f) car.Body.transform.localRotation = Quaternion.LookRotation(direction, Vector3.up);
            return true;
        }

        // ---- Level crossings -------------------------------------------------------------------

        private static float CrossingCentre(WorldChunkPlan plan) => plan.Start + WorldPlanner.ChunkLength * 0.5f + (plan.IsStation ? 45f : 0f);

        /// <summary>Barriers close ~15 s before a train arrives and open once its tail is clear.</summary>
        public bool CrossingClosed(WorldChunkPlan plan, float trainDistance)
        {
            float centre = CrossingCentre(plan);
            float lead = Mathf.Clamp(trainSpeed * 15f, 90f, 400f);
            float tail = CabTrainConsist.LocomotiveLength + CabTrainConsist.CoachCount * (CabTrainConsist.CoachLength + CabTrainConsist.CouplingGap) + 12f;
            bool ours = centre - trainDistance < lead && trainDistance - centre < tail;
            bool theirs = meeting != null && meeting.Front - centre < 400f && centre - (meeting.Front + meeting.Length) < 12f && meeting.Front + meeting.Length > centre - 12f;
            return ours || theirs;
        }

        private void SpawnCrossingCars(float trainDistance)
        {
            foreach (WorldChunk chunk in world.Chunks)
            {
                if (!chunk.Plan.Crossing || !chunk.Complete) continue;
                float centre = CrossingCentre(chunk.Plan);
                if (centre < trainDistance - 60f) continue;
                int count = 0;
                foreach (Car car in cars) if (car.CrossingIndex == chunk.Plan.Index && car.Side == 0) count++;
                if (count >= 6) continue;
                int direction = random.NextDouble() < 0.5 ? -1 : 1;
                float start = direction > 0 ? -300f : 300f;
                bool clear = true;
                foreach (Car car in cars)
                    if (car.Side == 0 && car.CrossingIndex == chunk.Plan.Index && car.Direction == direction && Mathf.Abs(car.Position - start) < 25f) clear = false;
                if (clear && random.NextDouble() < 0.5) SpawnCar(0, direction, start, chunk.Plan.Index);
            }
        }

        /// <summary>Speed allowed to a crossing car: stop at the line while the barrier is down.</summary>
        private float CrossingLimit(Car car, float trainDistance)
        {
            WorldChunkPlan plan = world.Planner.Get(car.CrossingIndex);
            if (!CrossingClosed(plan, trainDistance)) return car.Cruise;
            float stopLine = car.Direction > 0 ? PlacementMask.CorridorLeft - CrossingStopGap : PlacementMask.CorridorRight + CrossingStopGap;
            float toLine = (stopLine - car.Position) * car.Direction - car.Length * 0.5f;
            // Already past the line: clear the crossing quickly.
            if (toLine < -1f) return car.Cruise;
            return Mathf.Max(0f, toLine * 0.7f);
        }

        private bool PlaceOnCrossing(Car car)
        {
            WorldChunkPlan plan = world.Planner.Get(car.CrossingIndex);
            float centre = CrossingCentre(plan);
            // Right-hand traffic across the line: heading +x the right lane is at smaller d.
            float d = centre - car.Direction * WorldRoads.LaneOffset;
            float y = CrossingHeight(centre, car.Position);
            Vector3 here = world.LocalPoint(d, car.Position, y);
            Vector3 next = world.LocalPoint(d, car.Position + car.Direction * 2f, CrossingHeight(centre, car.Position + car.Direction * 2f));
            car.Body.transform.localPosition = here;
            car.Body.transform.localRotation = Quaternion.LookRotation(next - here, Vector3.up);
            return true;
        }

        /// <summary>Same profile as the crossing road built by the chunk (level with the rails over the tracks).</summary>
        public float CrossingHeight(float centre, float x)
        {
            float ax = Mathf.Abs(x - WorldTerrain.CorridorCentre);
            float ground = Mathf.Max(world.Terrain.Height(centre, x, false), world.Terrain.Height(centre - 3.2f, x, false), world.Terrain.Height(centre + 3.2f, x, false)) + 0.05f;
            return Mathf.Lerp(0.27f, ground, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(WorldTerrain.CorridorHalfWidth, WorldTerrain.CorridorHalfWidth + 14f, ax)));
        }

        private void UpdateCrossings(float trainDistance)
        {
            foreach (WorldChunk chunk in world.Chunks)
            {
                if (chunk.CrossingArms.Count == 0) continue;
                bool closed = CrossingClosed(chunk.Plan, trainDistance);
                chunk.CrossingAngle = Mathf.MoveTowards(chunk.CrossingAngle, closed ? 0f : 1f, Time.deltaTime / 4f);
                float raise = Mathf.SmoothStep(0f, 1f, chunk.CrossingAngle) * 84f;
                for (int i = 0; i < chunk.CrossingArms.Count; i++)
                    chunk.CrossingArms[i].localRotation = chunk.CrossingArmRest[i] * Quaternion.Euler(i % 2 == 0 ? -raise : raise, 0f, 0f);
                // Red lamps blink in turn while a train is near.
                bool blink = closed && Mathf.Repeat(Time.time * 1.6f, 1f) < 0.5f;
                for (int i = 0; i < chunk.CrossingLamps.Count; i++)
                {
                    bool on = closed && (i % 2 == 0 ? blink : !blink);
                    chunk.CrossingLamps[i].sharedMaterial = on ? WorldMaterials.Glow("CrossingRed", new Color(1f, 0.05f, 0.03f), 1.5f) : WorldMaterials.Plain("CrossingLampOff", new Color(0.25f, 0.05f, 0.04f), 0.6f);
                }
                for (int i = 0; i < chunk.CrossingLights.Count; i++)
                    chunk.CrossingLights[i].enabled = closed && (i % 2 == 0 ? blink : !blink);
            }
        }

        // ---- Trains on the other track ---------------------------------------------------------

        private void UpdateMeetingTrain(float dt, float trainDistance)
        {
            if (meeting == null)
            {
                if (trainDistance >= nextMeetingAt) SpawnMeetingTrain(trainDistance + world.ViewDistance - 40f);
                return;
            }
            meeting.Front -= meeting.Speed * dt;
            for (int i = 0; i < meeting.Cars.Count; i++)
            {
                // The train runs toward smaller distances: its cars trail behind at larger d.
                float centre = meeting.Front + meeting.Offsets[i];
                float half = meeting.Lengths[i] * 0.42f;
                Vector3 front = world.LocalPoint(centre - half, WorldChunkBuilder.SecondTrackOffset, 0.02f);
                Vector3 back = world.LocalPoint(centre + half, WorldChunkBuilder.SecondTrackOffset, 0.02f);
                meeting.Cars[i].localPosition = (front + back) * 0.5f;
                meeting.Cars[i].localRotation = Quaternion.LookRotation(front - back, Vector3.up);
            }
            if (meeting.Front + meeting.Length < trainDistance - 200f)
            {
                Object.Destroy(meeting.Root.gameObject);
                meeting = null;
                nextMeetingAt = trainDistance + Range(1800f, 5200f);
            }
        }

        private void SpawnMeetingTrain(float front)
        {
            double pick = random.NextDouble();
            TrainKind kind = pick < 0.45 ? TrainKind.Freight : pick < 0.8 ? TrainKind.Suburban : TrainKind.Passenger;
            List<RailCarKind> consist = TrainFactory.Compose(kind, random);
            MeetingTrain train = new MeetingTrain
            {
                Root = new GameObject("MeetingTrain_" + kind).transform,
                Front = front,
                Speed = kind == TrainKind.Freight ? Range(14f, 20f) : Range(20f, 28f)
            };
            train.Root.SetParent(root, false);
            int livery = random.Next(0, 6);
            float offset = 0f;
            for (int i = 0; i < consist.Count; i++)
            {
                RailCarKind car = consist[i];
                float length = TrainFactory.Length(car);
                bool lastHead = kind == TrainKind.Suburban && i == consist.Count - 1;
                GameObject body = TrainFactory.Create(train.Root, car, car == RailCarKind.ContainerFlat ? random.Next(0, 12) : livery, lastHead);
                train.Cars.Add(body.transform);
                train.Offsets.Add(offset + length * 0.5f);
                train.Lengths.Add(length);
                offset += length + 1.1f;
            }
            train.Length = offset;
            // The leading cab's headlight lights the track toward us at night.
            GameObject lampObject = new GameObject("MeetingTrainHeadlight", typeof(Light));
            lampObject.transform.SetParent(train.Cars[0], false);
            lampObject.transform.localPosition = new Vector3(0f, 4f, TrainFactory.Length(consist[0]) * 0.5f + 0.3f);
            lampObject.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            Light lamp = lampObject.GetComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.color = new Color(1f, 0.95f, 0.85f);
            lamp.intensity = 900f;
            lamp.range = 150f;
            lamp.spotAngle = 30f;
            lamp.innerSpotAngle = 10f;
            lamp.shadows = LightShadows.None;
            lamp.enabled = night;
            train.Headlight = lamp;
            meeting = train;
            UpdateMeetingTrain(0f, -1e9f);
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root.gameObject);
            cars.Clear();
            meeting = null;
        }
    }
}
