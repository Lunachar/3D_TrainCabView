using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SortingStation
{
    /// <summary>Buildings and engineering structures of each kind of chunk.</summary>
    public sealed partial class WorldChunkBuilder
    {
        public const float PlatformHeight = 1.1f;
        public const float PlatformEdge = -1.95f;
        public const float PlatformWidth = 5.1f;
        private const float BoreHalfWidth = 5.65f;
        private const float BoreWallHeight = 4.6f;
        private const float BoreCrown = 2.6f;

        private void BuildStructures()
        {
            if (plan.Kind == WorldChunkKind.Tunnel) BuildTunnel();
            if (plan.Kind == WorldChunkKind.Water) BuildRiverAndBridge();
            if (plan.IsStation) BuildStation();
            if (plan.Crossing) BuildCrossing();
            switch (plan.Kind)
            {
                case WorldChunkKind.Village: BuildVillage(); break;
                case WorldChunkKind.Town: BuildTown(); break;
                case WorldChunkKind.Industrial: BuildIndustry(); break;
                case WorldChunkKind.Field: BuildFieldDetails(); break;
                case WorldChunkKind.Foothills: BuildBoulders(6); break;
                case WorldChunkKind.Tunnel: BuildBoulders(4); break;
            }
        }

        // ---- Tunnel ---------------------------------------------------------------------------

        private static Vector2[] BoreProfile()
        {
            // Counter-clockwise (interior on the left), so the sweep's normals face into the bore.
            List<Vector2> points = new List<Vector2>
            {
                new Vector2(-BoreHalfWidth, -0.04f), new Vector2(BoreHalfWidth, -0.04f), new Vector2(BoreHalfWidth, BoreWallHeight)
            };
            const int arch = 10;
            for (int i = 1; i < arch; i++)
            {
                float a = i / (float)arch * Mathf.PI;
                points.Add(new Vector2(Mathf.Cos(a) * BoreHalfWidth, BoreWallHeight + Mathf.Sin(a) * BoreCrown));
            }
            points.Add(new Vector2(-BoreHalfWidth, BoreWallHeight));
            points.Add(new Vector2(-BoreHalfWidth, -0.04f));
            return points.ToArray();
        }

        private void BuildTunnel()
        {
            float entrance = plan.Start + WorldPlanner.PortalInset;
            float exit = plan.End - WorldPlanner.PortalInset;
            float d0 = plan.TunnelEntrance ? entrance : plan.Start;
            float d1 = plan.TunnelExit ? exit : plan.End;
            float centre = WorldTerrain.CorridorCentre;
            Block(plan.Start - 2f, plan.End + 2f, centre - 30f, centre + 30f);
            Sweep(meshes.For(WorldMaterials.TunnelLining, 3f), d0, d1, 3f, centre, BoreProfile());

            WorldMesh lamp = meshes.For(WorldMaterials.LampGlow, 1f);
            const float lampSpacing = 12f;
            for (int k = Mathf.CeilToInt(d0 / lampSpacing); k * lampSpacing < d1; k++)
            {
                float d = k * lampSpacing;
                Vector3 position = P(d, centre - BoreHalfWidth + 0.25f, 4.2f);
                lamp.AddBox(position, new Vector3(0.18f, 0.12f, 0.55f), R(d));
                chunk.TunnelLights.Add(CreateLight("TunnelInteriorLight", position + R(d) * new Vector3(0.4f, -0.2f, 0f), LightType.Point,
                    new Color(1f, 0.62f, 0.28f), 1.1f, 10f));
            }
            if (plan.TunnelEntrance) BuildPortal(entrance, -1f);
            if (plan.TunnelExit) BuildPortal(exit, 1f);
        }

        /// <summary>Concrete headwall around the bore opening; the terrain supplies the rock face beside it.</summary>
        private void BuildPortal(float d, float outward)
        {
            float centre = WorldTerrain.CorridorCentre;
            float top = 0f;
            for (float x = -WorldTerrain.PortalHalfWidth; x <= WorldTerrain.PortalHalfWidth; x += 2f)
                top = Mathf.Max(top, terrain.Height(d + outward * 0.5f, centre + x, true));
            top += 1.2f;
            float half = WorldTerrain.PortalHalfWidth + 0.6f;
            Vector3 normal = R(d) * new Vector3(0f, 0f, outward);
            WorldMesh wall = meshes.For(WorldMaterials.Rock, 3f, true);
            Vector3 V(float x, float y) => P(d, centre + x, y) + normal * 0.05f;
            wall.AddQuad(V(-half, -0.2f), V(-BoreHalfWidth, -0.2f), V(-BoreHalfWidth, top), V(-half, top), normal);
            wall.AddQuad(V(BoreHalfWidth, -0.2f), V(half, -0.2f), V(half, top), V(BoreHalfWidth, top), normal);
            Vector2[] profile = BoreProfile();
            float archTop = BoreWallHeight + BoreCrown;
            // The part above the arch: from each arch segment straight up to the headwall top.
            for (int i = 2; i < profile.Length - 2; i++)
            {
                Vector2 a = profile[i], b = profile[i + 1];
                wall.AddQuad(V(a.x, a.y), V(b.x, b.y), V(b.x, top), V(a.x, top), normal);
            }
            // A heavier rim and a cornice make the portal read as built, not cut.
            WorldMesh rim = meshes.For(WorldMaterials.DarkConcrete, 1f, true);
            rim.AddBox(P(d, centre, archTop + 0.35f) + normal * 0.35f, new Vector3(BoreHalfWidth * 2f + 1.2f, 0.7f, 0.6f), R(d));
            rim.AddBox(P(d, centre, top - 0.3f) + normal * 0.4f, new Vector3(half * 2f + 0.6f, 0.6f, 0.9f), R(d));
            for (int side = -1; side <= 1; side += 2)
                rim.AddBox(P(d, centre + side * (BoreHalfWidth + 0.3f), BoreWallHeight * 0.5f) + normal * 0.35f,
                    new Vector3(0.6f, BoreWallHeight + 0.2f, 0.6f), R(d));
        }

        // ---- River and bridge ------------------------------------------------------------------

        private void BuildRiverAndBridge()
        {
            float bridge = WorldTerrain.RiverCentre(plan, WorldTerrain.CorridorCentre);
            Block(bridge - 48f, bridge + 48f, -700f, 700f);
            WorldMesh water = meshes.For(WorldMaterials.Water, 8f);
            const float halfWidth = 15f;
            for (float x = -700f; x < 700f; x += 20f)
            {
                float a = WorldTerrain.RiverCentre(plan, x), b = WorldTerrain.RiverCentre(plan, x + 20f);
                water.AddQuad(P(a - halfWidth, x, WorldTerrain.WaterLevel), P(a + halfWidth, x, WorldTerrain.WaterLevel),
                    P(b + halfWidth, x + 20f, WorldTerrain.WaterLevel), P(b - halfWidth, x + 20f, WorldTerrain.WaterLevel), Vector3.up);
            }

            // A through-truss bridge: deck, two trusses outside the tracks, piers and abutments.
            Quaternion r = R(bridge);
            float span = WorldTerrain.BridgeHalfSpan * 2f;
            WorldMesh steel = meshes.For(WorldMaterials.Plain("BridgeSteel", new Color(0.30f, 0.38f, 0.34f), 0.45f, 0.5f), 1f, true);
            WorldMesh concrete = meshes.For(WorldMaterials.Concrete, 2f, true);
            float left = -3.4f, right = SecondTrackOffset + 3.4f;
            concrete.AddBox(P(bridge, (left + right) * 0.5f, -0.45f), new Vector3(right - left, 0.5f, span + 2f), r, true);
            foreach (float x in new[] { left, right })
            {
                steel.AddBox(P(bridge, x, -0.1f), new Vector3(0.45f, 0.6f, span), r);
                steel.AddBox(P(bridge, x, 6.2f), new Vector3(0.45f, 0.45f, span - 8f), r);
                const int panels = 10;
                for (int i = 0; i <= panels; i++)
                {
                    float d = bridge - span * 0.5f + span * i / panels;
                    float y0 = 0.1f, y1 = i == 0 || i == panels ? 0.1f : 6.2f;
                    if (i > 0 && i < panels) steel.AddBox(P(d, x, (y0 + y1) * 0.5f), new Vector3(0.28f, y1 - y0, 0.28f), r);
                    if (i < panels)
                    {
                        float dn = bridge - span * 0.5f + span * (i + 1) / panels;
                        float ya = i == 0 ? 0.1f : 6.2f, yb = i + 1 == panels ? 0.1f : 0.1f;
                        Vector3 from = P(i % 2 == 0 ? d : dn, x, ya), to = P(i % 2 == 0 ? dn : d, x, i % 2 == 0 ? yb : 6.2f);
                        AddBeam(steel, from, to, 0.22f);
                    }
                }
            }
            // Portal bracing over the tracks at both ends and in the middle.
            foreach (float d in new[] { bridge - span * 0.5f + 4f, bridge, bridge + span * 0.5f - 4f })
                steel.AddBox(P(d, (left + right) * 0.5f, 6.3f), new Vector3(right - left, 0.3f, 0.3f), R(d));
            foreach (float d in new[] { bridge - span * 0.5f - 1f, bridge + span * 0.5f + 1f })
                concrete.AddBox(P(d, (left + right) * 0.5f, -4.8f), new Vector3(right - left + 2f, 9.4f, 2.4f), R(d), true);
            concrete.AddBox(P(bridge, (left + right) * 0.5f, (WorldTerrain.WaterLevel - 1.5f - 0.7f) * 0.5f),
                new Vector3(right - left - 1f, -WorldTerrain.WaterLevel + 1.5f - 0.7f, 2.2f), r, true);
        }

        private static void AddBeam(WorldMesh mesh, Vector3 from, Vector3 to, float thickness)
        {
            Vector3 direction = to - from;
            if (direction.sqrMagnitude < 0.0001f) return;
            mesh.AddBox((from + to) * 0.5f, new Vector3(thickness, thickness, direction.magnitude), Quaternion.LookRotation(direction, Vector3.up));
        }

        // ---- Station --------------------------------------------------------------------------

        private void BuildStation()
        {
            float centre = plan.Start + WorldPlanner.PlatformCentre;
            float half = WorldPlanner.PlatformLength * 0.5f;
            Quaternion r = R(centre);
            Block(centre - half - 8f, centre + half + 8f, -34f, PlatformEdge + 0.2f);

            WorldMesh concrete = meshes.For(WorldMaterials.Concrete, 2f, true);
            float platformX = PlatformEdge - PlatformWidth * 0.5f;
            concrete.AddBox(P(centre, platformX, PlatformHeight * 0.5f - 0.3f), new Vector3(PlatformWidth, PlatformHeight + 0.6f, WorldPlanner.PlatformLength), r);
            meshes.For(WorldMaterials.SafetyLine, 1f).AddBox(P(centre, PlatformEdge - 0.35f, PlatformHeight + 0.004f), new Vector3(0.12f, 0.02f, WorldPlanner.PlatformLength - 0.4f), r);
            meshes.For(WorldMaterials.DarkConcrete, 1f).AddBox(P(centre, PlatformEdge - 0.06f, PlatformHeight - 0.1f), new Vector3(0.14f, 0.22f, WorldPlanner.PlatformLength), r);
            // Ramps down to the ground at both ends.
            foreach (float end in new[] { -1f, 1f })
            {
                float d = centre + end * (half + 3f);
                Vector3 normal = R(d) * new Vector3(0f, 1f, -end * 0.35f).normalized;
                concrete.AddQuad(P(centre + end * half, platformX - 2.2f, PlatformHeight), P(centre + end * half, platformX + 2.2f, PlatformHeight),
                    P(centre + end * (half + 6f), platformX + 2.2f, Ground(d, platformX)), P(centre + end * (half + 6f), platformX - 2.2f, Ground(d, platformX)), normal);
            }

            // Canopy over the middle of the platform.
            WorldMesh steel = meshes.For(WorldMaterials.Steel, 1f, true);
            WorldMesh roof = meshes.For(WorldMaterials.ShedMetal(2), 2f, true);
            for (float d = centre - 14f; d <= centre + 14.1f; d += 7f)
                steel.AddBox(P(d, platformX - 1f, PlatformHeight + 1.6f), new Vector3(0.16f, 3.2f, 0.16f), r);
            roof.AddBox(P(centre, platformX - 0.6f, PlatformHeight + 3.3f), new Vector3(4.6f, 0.18f, 32f), r * Quaternion.Euler(0f, 0f, -6f), true);

            // Station building behind the platform.
            float buildingX = platformX - PlatformWidth * 0.5f - 7.5f;
            float ground = Mathf.Min(PlatformHeight - 0.2f, Ground(centre, buildingX));
            BuildHouse(centre + 4f, buildingX, 9f, 20f, 4.4f, WorldMaterials.Brick, WorldMaterials.Roof(1), ground, 4, true);

            // Lamps along the platform and benches under the canopy.
            for (float d = centre - half + 4f; d <= centre + half - 3f; d += 12f)
                BuildStreetLamp(d, platformX - 1.6f, PlatformHeight, 1f);
            WorldMesh wood = meshes.For(WorldMaterials.Planks, 1f);
            for (int i = -1; i <= 1; i++)
            {
                float d = centre + i * 8f;
                wood.AddBox(P(d, platformX - 1.4f, PlatformHeight + 0.45f), new Vector3(0.5f, 0.08f, 1.8f), r);
                wood.AddBox(P(d, platformX - 1.65f, PlatformHeight + 0.75f), new Vector3(0.06f, 0.5f, 1.8f), r);
                steel.AddBox(P(d, platformX - 1.4f, PlatformHeight + 0.2f), new Vector3(0.4f, 0.4f, 0.08f), r);
            }

            // Name boards facing the arriving train.
            string name = StationName(plan);
            foreach (float d in new[] { centre - 14f, centre + 16f })
            {
                Vector3 position = P(d, platformX - 1.3f, PlatformHeight + 2.4f);
                steel.AddBox(position + Vector3.down * 1.2f, new Vector3(0.1f, 2.4f, 0.1f), r);
                GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                board.name = "StationSignBoard";
                Collider collider = board.GetComponent<Collider>();
                if (Application.isPlaying) Object.Destroy(collider);
                else Object.DestroyImmediate(collider);
                board.transform.SetParent(chunk.Root.transform, false);
                board.transform.localPosition = position;
                board.transform.localRotation = r;
                board.GetComponent<Renderer>().sharedMaterial = WorldMaterials.SignBoard;
                Vector3 forward = r * Vector3.forward;
                TextMesh front = CabWorld3DPrototypeFactory.CreateStationSignText(chunk.Root.transform, "StationName_" + name, position - forward * 0.06f, r, name);
                TextMesh back = CabWorld3DPrototypeFactory.CreateStationSignText(chunk.Root.transform, "StationName_Back_" + name, position + forward * 0.06f,
                    r * Quaternion.Euler(0f, 180f, 0f), name);
                front.transform.localRotation = r;
                back.transform.localRotation = r * Quaternion.Euler(0f, 180f, 0f);
                // The text mesh gets its size only when first drawn; the board fits itself then.
                board.AddComponent<SignBoardFitter>().Configure(front);
                chunk.Signs.Add(front);
                chunk.Signs.Add(back);
                chunk.SignBoards.Add(board.transform);
            }

            // People waiting on the platform, in the platform's own frame.
            Transform frame = new GameObject("PlatformFrame").transform;
            frame.SetParent(chunk.Root.transform, false);
            frame.localPosition = P(centre, 0f, PlatformHeight);
            frame.localRotation = r;
            chunk.PlatformFrame = frame;
            int count = 6 + random.Next(0, 7);
            for (int i = 0; i < count; i++)
            {
                float z = Range(-18f, 16f);
                bool seated = i % 5 == 3;
                Vector3 platform = new Vector3(Range(PlatformEdge - 3.6f, PlatformEdge - 1.1f), 0f, seated ? Mathf.Round(z / 8f) * 8f : z);
                Vector3 door = new Vector3(PlatformEdge + 0.35f, 0f, z);
                Color coat = Color.HSVToRGB((float)random.NextDouble(), Range(0.35f, 0.7f), Range(0.25f, 0.7f));
                GameObject passenger = CabWorld3DPrototypeFactory.CreatePassenger(frame, "Passenger_" + plan.Index + "_" + i, platform, coat,
                    i % 6 == 0, seated, i % 3 == 1, out GameObject umbrella, out Renderer coatRenderer, out Transform[] limbs);
                passenger.transform.localRotation = Quaternion.Euler(0f, Range(40f, 140f), 0f);
                Cab3DPassengerAgent agent = passenger.AddComponent<Cab3DPassengerAgent>();
                agent.Configure(platform, door, umbrella, coatRenderer, limbs);
                passenger.AddComponent<Cab3DInteractiveObject>().Configure("station passenger worker", CabInteractionReaction.Wave);
                passenger.SetActive(false);
                chunk.Passengers.Add(agent);
                foreach (Renderer renderer in passenger.GetComponentsInChildren<Renderer>(true))
                    if (renderer.sharedMaterial != null && !chunk.Materials.Contains(renderer.sharedMaterial)) chunk.Materials.Add(renderer.sharedMaterial);
            }
        }

        public static string StationName(WorldChunkPlan station)
        {
            CabStationDefinition definition = CabStationNetwork.AtSequence(station.StationNumber);
            return definition != null ? definition.DisplayName : "Станция";
        }

        private void BuildStreetLamp(float d, float x, float baseY, float armSide)
        {
            Quaternion r = R(d);
            WorldMesh steel = meshes.For(WorldMaterials.Steel, 1f, true);
            steel.AddCylinder(P(d, x, baseY), 0.07f, 5f, 6, r, false);
            steel.AddBox(P(d, x + armSide * 0.45f, baseY + 5f), new Vector3(0.9f, 0.06f, 0.08f), r);
            Vector3 head = P(d, x + armSide * 0.9f, baseY + 4.9f);
            meshes.For(WorldMaterials.LampGlow, 1f).AddBox(head, new Vector3(0.45f, 0.1f, 0.25f), r);
            chunk.Lamps.Add(CreateLight("ScenicNightLight", head + Vector3.down * 0.3f, LightType.Point, new Color(1f, 0.80f, 0.52f), 1.4f, 12f));
        }

        private Light CreateLight(string name, Vector3 localPosition, LightType type, Color color, float intensity, float range)
        {
            GameObject lightObject = new GameObject(name, typeof(Light));
            lightObject.transform.SetParent(chunk.Root.transform, false);
            lightObject.transform.localPosition = localPosition;
            Light light = lightObject.GetComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
        }

        // ---- Level crossing -------------------------------------------------------------------

        private void BuildCrossing()
        {
            float centre = plan.Start + WorldPlanner.ChunkLength * 0.5f + (plan.IsStation ? 45f : 0f);
            const float halfWidth = 3.2f;
            Block(centre - 9f, centre + 9f, -400f, 400f);
            WorldMesh road = meshes.For(WorldMaterials.Asphalt, 4f);
            float previousX = -400f;
            float Y(float x)
            {
                float ax = Mathf.Abs(x - WorldTerrain.CorridorCentre);
                float ground = Mathf.Max(Ground(centre, x), terrain.Height(centre - halfWidth, x, false), terrain.Height(centre + halfWidth, x, false)) + 0.05f;
                // Level with the rail heads over the tracks, then easing down to the ground.
                return Mathf.Lerp(0.27f, ground, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(WorldTerrain.CorridorHalfWidth, WorldTerrain.CorridorHalfWidth + 14f, ax)));
            }
            for (float x = -396f; x <= 400f; x += x > -30f && x < 36f ? 2f : 8f)
            {
                road.AddQuad(P(centre - halfWidth, previousX, Y(previousX)), P(centre + halfWidth, previousX, Y(previousX)),
                    P(centre + halfWidth, x, Y(x)), P(centre - halfWidth, x, Y(x)), Vector3.up,
                    new Vector2(0f, previousX), new Vector2(halfWidth * 2f, previousX), new Vector2(halfWidth * 2f, x), new Vector2(0f, x));
                previousX = x;
            }
            // Barrier posts and arms (raised), warning signs.
            WorldMesh steel = meshes.For(WorldMaterials.Steel, 1f, true);
            WorldMesh stripes = meshes.For(WorldMaterials.Plain("BarrierPaint", new Color(0.85f, 0.12f, 0.10f), 0.4f), 1f);
            foreach ((float d, float x) in new[] { (centre - halfWidth - 1.5f, -4.4f), (centre + halfWidth + 1.5f, SecondTrackOffset + 4.4f) })
            {
                float ground = Ground(d, x);
                steel.AddBox(P(d, x, ground + 0.6f), new Vector3(0.35f, 1.2f, 0.35f), R(d));
                stripes.AddBox(P(d, x, ground + 3.8f), new Vector3(0.12f, 5.2f, 0.12f), R(d));
                steel.AddBox(P(d, x, ground + 1.6f), new Vector3(0.12f, 3.2f, 0.12f), R(d));
                meshes.For(WorldMaterials.Plain("CrossingSignWhite", new Color(0.92f, 0.92f, 0.9f), 0.3f), 1f)
                    .AddBox(P(d, x, ground + 3.1f), new Vector3(1.1f, 0.18f, 0.03f), R(d) * Quaternion.Euler(0f, 0f, 35f));
            }
        }

        // ---- Settlements ----------------------------------------------------------------------

        /// <summary>A house with a gable roof, aligned to the track; windows on the long sides.</summary>
        private void BuildHouse(float d, float x, float depth, float length, float wallHeight, Material walls, Material roofMaterial,
            float groundY, int windows, bool framed)
        {
            Quaternion r = R(d);
            Block(d - length * 0.5f - 1.5f, d + length * 0.5f + 1.5f, x - depth * 0.5f - 1.5f, x + depth * 0.5f + 1.5f);
            WorldMesh wall = meshes.For(walls, 2f, true);
            wall.AddBox(P(d, x, groundY + wallHeight * 0.5f - 0.4f), new Vector3(depth, wallHeight + 0.8f, length), r);
            float pitch = depth * 0.36f;
            Vector3 ridgeA = P(d - length * 0.5f - 0.3f, x, groundY + wallHeight + pitch);
            Vector3 ridgeB = P(d + length * 0.5f + 0.3f, x, groundY + wallHeight + pitch);
            WorldMesh roof = meshes.For(roofMaterial, 2f, true);
            for (int side = -1; side <= 1; side += 2)
            {
                float eaveX = x + side * (depth * 0.5f + 0.45f);
                Vector3 eaveA = P(d - length * 0.5f - 0.3f, eaveX, groundY + wallHeight - 0.2f);
                Vector3 eaveB = P(d + length * 0.5f + 0.3f, eaveX, groundY + wallHeight - 0.2f);
                Vector3 normal = Vector3.Cross(eaveB - eaveA, ridgeA - eaveA).normalized;
                if (normal.y < 0f) normal = -normal;
                roof.AddQuad(eaveA, eaveB, ridgeB, ridgeA, normal);
            }
            for (int end = -1; end <= 1; end += 2)
            {
                float de = d + end * length * 0.5f;
                wall.AddTriangle(P(de, x - depth * 0.5f, groundY + wallHeight), P(de, x + depth * 0.5f, groundY + wallHeight),
                    P(de, x, groundY + wallHeight + pitch - 0.05f), r * new Vector3(0f, 0f, end));
            }
            // Windows on both long walls.
            WorldMesh glass = meshes.For(WorldMaterials.Facade(3), 1f);
            WorldMesh frame = meshes.For(WorldMaterials.WindowFrame, 1f);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < windows; i++)
                {
                    float wd = d - length * 0.5f + length * (i + 0.5f) / windows;
                    float wx = x + side * (depth * 0.5f + 0.03f);
                    Vector3 centre = P(wd, wx, groundY + wallHeight * 0.52f);
                    Vector3 normal = r * new Vector3(side, 0f, 0f);
                    Vector3 along = r * Vector3.forward * 0.55f;
                    Vector3 up = Vector3.up * 0.7f;
                    // Windows reuse one random window cell of the town facade, so some glow at night.
                    int cx = random.Next(0, 6), cy = random.Next(0, 4);
                    float u0 = (cx + 0.26f) / 6f, u1 = (cx + 0.74f) / 6f, v0 = (cy + 0.34f) / 4f, v1 = (cy + 0.76f) / 4f;
                    glass.AddQuad(centre - along - up, centre + along - up, centre + along + up, centre - along + up, normal,
                        new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1));
                    if (framed) frame.AddBox(centre + normal * 0.03f, new Vector3(0.08f, 1.55f, 1.3f), r * Quaternion.identity);
                }
        }

        private void BuildVillage()
        {
            float streetSide = plan.Seed % 2 == 0 ? -1f : 1f;
            float streetX = WorldTerrain.CorridorCentre + streetSide * Range(24f, 30f);
            // A gravel lane along the village, following the ground.
            WorldMesh lane = meshes.For(WorldMaterials.Gravel, 3f);
            for (float d = plan.Start; d < plan.End - 0.01f; d += 6f)
            {
                float e = Mathf.Min(plan.End, d + 6f);
                lane.AddQuad(P(d, streetX - 2.2f, Ground(d, streetX - 2.2f) + 0.04f), P(d, streetX + 2.2f, Ground(d, streetX + 2.2f) + 0.04f),
                    P(e, streetX + 2.2f, Ground(e, streetX + 2.2f) + 0.04f), P(e, streetX - 2.2f, Ground(e, streetX - 2.2f) + 0.04f), Vector3.up);
            }
            Block(plan.Start, plan.End, streetX - 3f, streetX + 3f);
            float stationGap = plan.IsStation ? 1f : 0f;
            for (int row = -1; row <= 1; row += 2)
            {
                float d = plan.Start + Range(4f, 12f);
                while (d < plan.End - 8f)
                {
                    float length = Range(6f, 9f), depth = Range(6f, 8f);
                    float x = streetX + row * Range(9f, 12f);
                    bool nearTrack = Mathf.Abs(x - WorldTerrain.CorridorCentre) < 16f;
                    bool stationBlock = stationGap > 0f && streetSide < 0f && row * streetSide < 0f;
                    if (!nearTrack && !stationBlock && mask.IsFree(d, x, Mathf.Max(length, depth) * 0.5f + 1f))
                    {
                        int variant = random.Next(0, 12);
                        Material walls = variant % 3 == 0 ? WorldMaterials.Plaster(variant) : WorldMaterials.Siding(variant);
                        BuildHouse(d, x, depth, length, Range(2.8f, 3.4f), walls, WorldMaterials.Roof(variant), MinGround(d, x, depth, length), 2, true);
                        BuildFence(d, streetX + row * 3.4f, length + Range(6f, 10f));
                        if (Chance(0.5f)) BuildShed(d + length * 0.5f + 3f, x + row * 3f);
                    }
                    d += length + Range(9f, 16f);
                }
            }
        }

        private float MinGround(float d, float x, float depth, float length)
        {
            float min = float.MaxValue;
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                    min = Mathf.Min(min, Ground(d + i * length * 0.5f, x + j * depth * 0.5f));
            return min;
        }

        private void BuildFence(float d, float x, float length)
        {
            WorldMesh planks = meshes.For(WorldMaterials.Planks, 1.2f);
            for (float s = -length * 0.5f; s < length * 0.5f; s += 2.5f)
            {
                float pd = d + s + 1.25f;
                float ground = Ground(pd, x);
                planks.AddBox(P(pd, x, ground + 0.6f), new Vector3(0.04f, 1.2f, 2.45f), R(pd));
                planks.AddBox(P(d + s, x, ground + 0.7f), new Vector3(0.1f, 1.4f, 0.1f), R(pd));
            }
        }

        private void BuildShed(float d, float x)
        {
            if (!mask.IsFree(d, x, 2.5f)) return;
            float ground = Ground(d, x);
            Block(d - 2f, d + 2f, x - 2f, x + 2f);
            meshes.For(WorldMaterials.Planks, 1.5f, true).AddBox(P(d, x, ground + 1f), new Vector3(3f, 2.2f, 3.4f), R(d));
            meshes.For(WorldMaterials.ShedMetal(random.Next(0, 5)), 1.5f, true)
                .AddBox(P(d, x, ground + 2.2f), new Vector3(3.5f, 0.1f, 3.9f), R(d) * Quaternion.Euler(0f, 0f, 8f));
        }

        private void BuildTown()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float d = plan.Start + Range(3f, 10f);
                while (d < plan.End - 14f)
                {
                    float length = Mathf.Min(Range(24f, 46f), plan.End - 4f - d);
                    if (length < 16f) break;
                    float depth = Range(11f, 13f);
                    float front = Range(26f, 34f);
                    float x = WorldTerrain.CorridorCentre + side * (front + depth * 0.5f);
                    float centre = d + length * 0.5f;
                    if (mask.IsFree(centre, x, length * 0.5f))
                    {
                        int floors = Chance(0.35f) ? 9 : 5;
                        BuildBlock(centre, x, depth, length, floors * 3f, random.Next(0, 4), MinGround(centre, x, depth, length));
                        // A second row further out.
                        float back = x + side * Range(22f, 34f);
                        if (Chance(0.7f) && mask.IsFree(centre, back, length * 0.5f))
                            BuildBlock(centre, back, depth, length * Range(0.7f, 1f), (Chance(0.5f) ? 9 : 5) * 3f, random.Next(0, 4), MinGround(centre, back, depth, length));
                    }
                    d += length + Range(10f, 18f);
                }
                // Streetlights along the town street between the track and the houses.
                for (float d2 = plan.Start + 10f; d2 < plan.End; d2 += 30f)
                    BuildStreetLamp(d2, WorldTerrain.CorridorCentre + side * 18f, Ground(d2, WorldTerrain.CorridorCentre + side * 18f), -side);
            }
        }

        private void BuildBlock(float d, float x, float depth, float length, float height, int variant, float groundY)
        {
            Quaternion r = R(d);
            Block(d - length * 0.5f - 2f, d + length * 0.5f + 2f, x - depth * 0.5f - 2f, x + depth * 0.5f + 2f);
            Material facade = WorldMaterials.Facade(variant);
            WorldMesh mesh = meshes.For(facade, 1f, true);
            float bottom = groundY - 0.6f;
            float top = groundY + height;
            // Four walls with the window grid in real scale (the ground floor starts at the ground).
            for (int face = 0; face < 4; face++)
            {
                bool longSide = face < 2;
                float sign = face % 2 == 0 ? -1f : 1f;
                float width = longSide ? length : depth;
                Vector3 normal = r * (longSide ? new Vector3(sign, 0f, 0f) : new Vector3(0f, 0f, sign));
                Vector3 centre = longSide ? P(d, x + sign * depth * 0.5f, 0f) : P(d + sign * length * 0.5f, x, 0f);
                Vector3 along = r * (longSide ? Vector3.forward : Vector3.right) * (width * 0.5f);
                float u = width / WorldMaterials.FacadeTileWidth;
                float v0 = (bottom - groundY) / WorldMaterials.FacadeTileHeight, v1 = height / WorldMaterials.FacadeTileHeight;
                mesh.AddQuad(centre - along + Vector3.up * bottom, centre + along + Vector3.up * bottom,
                    centre + along + Vector3.up * top, centre - along + Vector3.up * top, normal,
                    new Vector2(0f, v0), new Vector2(u, v0), new Vector2(u, v1), new Vector2(0f, v1));
            }
            WorldMesh roof = meshes.For(WorldMaterials.DarkConcrete, 3f, true);
            roof.AddBox(P(d, x, top + 0.1f), new Vector3(depth + 0.2f, 0.2f, length + 0.2f), r);
            roof.AddBox(P(d, x, top + 0.5f), new Vector3(depth + 0.3f, 0.6f, 0.25f), r * Quaternion.identity);
            if (height > 20f) roof.AddBox(P(d, x, top + 1.4f), new Vector3(3f, 2.4f, 4f), r);
        }

        private void BuildIndustry()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                if (Chance(0.3f)) continue;
                float length = Range(40f, 70f), depth = Range(20f, 32f);
                float centre = plan.Start + WorldPlanner.ChunkLength * 0.5f + Range(-15f, 15f);
                float x = WorldTerrain.CorridorCentre + side * (Range(28f, 40f) + depth * 0.5f);
                if (!mask.IsFree(centre, x, 5f)) continue;
                float ground = MinGround(centre, x, depth, length);
                int variant = random.Next(0, 5);
                BuildHouse(centre, x, depth, length, Range(8f, 12f), WorldMaterials.ShedMetal(variant), WorldMaterials.ShedMetal(variant + 1), ground, 0, false);
                if (Chance(0.6f))
                {
                    float chimneyX = x + side * (depth * 0.5f + 5f);
                    float chimneyD = centre + Range(-length * 0.3f, length * 0.3f);
                    meshes.For(WorldMaterials.Brick, 2f, true).AddCylinder(P(chimneyD, chimneyX, Ground(chimneyD, chimneyX) - 0.5f), 1.6f, Range(28f, 42f), 12, R(chimneyD));
                    Block(chimneyD - 3f, chimneyD + 3f, chimneyX - 3f, chimneyX + 3f);
                }
                // Concrete panel fence along the plant.
                WorldMesh fence = meshes.For(WorldMaterials.DarkConcrete, 2f);
                float fenceX = WorldTerrain.CorridorCentre + side * 20f;
                for (float d = plan.Start + 2f; d < plan.End - 2f; d += 3f)
                    fence.AddBox(P(d + 1.5f, fenceX, Ground(d + 1.5f, fenceX) + 1.2f), new Vector3(0.15f, 2.4f, 2.95f), R(d + 1.5f));
                Block(plan.Start, plan.End, fenceX - 1f, fenceX + 1f);
            }
        }

        private void BuildFieldDetails()
        {
            if (season == SeasonType.Winter || season == SeasonType.Spring) return;
            WorldMesh hay = meshes.For(WorldMaterials.Hay, 1f, true);
            int bales = random.Next(4, 14);
            for (int i = 0; i < bales; i++)
            {
                float d = Range(plan.Start, plan.End);
                float x = WorldTerrain.CorridorCentre + (Chance(0.5f) ? -1f : 1f) * Range(22f, 120f);
                if (!mask.IsFree(d, x, 1.2f) || terrain.Layers(d, x).a < 0.5f) continue;
                hay.AddCylinder(P(d, x - 0.6f, Ground(d, x) + 0.75f), 0.75f, 1.2f, 10, R(d) * Quaternion.Euler(0f, 0f, -90f));
            }
        }

        private void BuildBoulders(int count)
        {
            WorldMesh rock = meshes.For(WorldMaterials.Rock, 2f, true);
            for (int i = 0; i < count; i++)
            {
                float d = Range(plan.Start, plan.End);
                float x = WorldTerrain.CorridorCentre + (Chance(0.5f) ? -1f : 1f) * Range(12f, 60f);
                if (!mask.IsFree(d, x, 2f)) continue;
                float size = Range(1f, 3.2f);
                rock.AddBox(P(d, x, Ground(d, x) + size * 0.25f), new Vector3(size, size * 0.7f, size * 1.2f),
                    Quaternion.Euler(Range(-20f, 20f), Range(0f, 360f), Range(-20f, 20f)));
                mask.AddRect(d - size, d + size, x - size, x + size);
            }
        }
    }
}
