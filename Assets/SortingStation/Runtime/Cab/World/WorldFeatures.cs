using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// Extra landmarks that make chunks of the same kind differ: wind farms, power lines,
    /// orchards, sunflowers, dachas, lakes, quarries, cranes, sports grounds, masts, water
    /// towers, campfires, cattle and moose, a steam engine on a plinth.
    /// </summary>
    public sealed partial class WorldChunkBuilder
    {
        private readonly List<(float d, float x, CabTreeSpecies species, float scale)> extraTrees = new List<(float, float, CabTreeSpecies, float)>();

        private void BuildFeatures()
        {
            WorldChunkKind kind = plan.Kind;
            bool open = kind == WorldChunkKind.Meadow || kind == WorldChunkKind.Field;
            bool summer = season == SeasonType.Summer || season == SeasonType.Autumn;
            BuildPowerLine();
            if (open && Chance(0.12f)) WindTurbines();
            if ((kind == WorldChunkKind.Village || kind == WorldChunkKind.Field) && Chance(0.22f)) Orchard();
            if (kind == WorldChunkKind.Field && season == SeasonType.Summer && Chance(0.25f)) Sunflowers();
            if ((kind == WorldChunkKind.Meadow || kind == WorldChunkKind.Forest) && Chance(0.15f)) Dachas();
            if ((kind == WorldChunkKind.Meadow || kind == WorldChunkKind.Forest) && Chance(0.14f)) Lake();
            if (kind == WorldChunkKind.Foothills && Chance(0.35f)) Quarry();
            if ((kind == WorldChunkKind.City || kind == WorldChunkKind.Town) && Chance(0.3f)) TowerCrane();
            if ((kind == WorldChunkKind.Town || kind == WorldChunkKind.Village) && Chance(0.18f)) SportsGround();
            if (open && Chance(0.08f)) RadioMast();
            if ((kind == WorldChunkKind.Village || kind == WorldChunkKind.Industrial) && Chance(0.22f)) WaterTower();
            if (kind == WorldChunkKind.Forest && Chance(0.15f)) Campfire();
            if (open && summer && Chance(0.3f)) Herd(false);
            if (kind == WorldChunkKind.Forest && Chance(0.1f)) Herd(true);
            if (plan.IsStation && plan.Station == StationStyle.Town && Chance(0.4f)) SteamMonument();
        }

        /// <summary>A free spot away from the line: route distance and lateral offset.</summary>
        private bool FindSpot(float minX, float maxX, float radius, out float d, out float x)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                d = Range(plan.Start + radius, plan.End - radius);
                x = WorldTerrain.CorridorCentre + (Chance(0.5f) ? -1f : 1f) * Range(minX, maxX);
                if (mask.IsFree(d, x, radius) && terrain.RiverDepth(d, x) < 0.2f) return true;
            }
            d = x = 0f;
            return false;
        }

        private void BuildPowerLine()
        {
            // Power lines run for several chunks at a time, so their route is a function of distance.
            int block = plan.Index / 6;
            if (WorldPlanner.Hash(block, 77) % 100 > 32) return;
            if (plan.Kind == WorldChunkKind.Tunnel || plan.Kind == WorldChunkKind.City || plan.Kind == WorldChunkKind.Water) return;
            float side = WorldPlanner.Hash(block, 78) % 2 == 0 ? -1f : 1f;
            float X(float d) => WorldTerrain.CorridorCentre + side * (62f + 14f * Mathf.Sin(Mathf.Repeat(d * 0.0021f, Mathf.PI * 2f)));
            WorldMesh steel = meshes.For(WorldMaterials.Plain("Pylon", new Color(0.55f, 0.57f, 0.58f), 0.4f, 0.6f), 1f, true);
            WorldMesh wire = meshes.For(WorldMaterials.Wire, 1f);
            const float spacing = 70f;
            for (float d = Mathf.Floor(plan.Start / spacing) * spacing; d < plan.End; d += spacing)
            {
                float next = d + spacing;
                float x0 = X(d), x1 = X(next);
                float g0 = Ground(d, x0), g1 = Ground(next, x1);
                if (d >= plan.Start)
                {
                    Block(d - 4f, d + 4f, x0 - 4f, x0 + 4f);
                    // A lattice tower: four legs leaning in, two cross-arms.
                    for (int a = -1; a <= 1; a += 2)
                        for (int b = -1; b <= 1; b += 2)
                            AddBeam(steel, P(d + a * 2.2f, x0 + b * 2.2f, g0), P(d + a * 0.5f, x0 + b * 0.5f, g0 + 26f), 0.18f);
                    for (float h = 5f; h < 26f; h += 5f)
                        steel.AddBox(P(d, x0, g0 + h), new Vector3(3.6f - h * 0.1f, 0.12f, 3.6f - h * 0.1f), R(d) * Quaternion.Euler(0f, 90f, 0f));
                    steel.AddBox(P(d, x0, g0 + 22f), new Vector3(0.25f, 0.25f, 12f), R(d) * Quaternion.Euler(0f, 90f, 0f));
                    steel.AddBox(P(d, x0, g0 + 26f), new Vector3(0.25f, 0.25f, 8f), R(d) * Quaternion.Euler(0f, 90f, 0f));
                }
                // Each span belongs to the chunk that holds its first tower.
                if (d < plan.Start) continue;
                // Three sagging conductors to the next tower.
                foreach (float arm in new[] { -5.5f, 0f, 5.5f })
                {
                    float y = arm == 0f ? 26f : 22f;
                    Vector3 a0 = P(d, x0 + arm, g0 + y);
                    Vector3 a1 = P(next, x1 + arm, g1 + y);
                    Vector3 mid = (a0 + a1) * 0.5f + Vector3.down * 3f;
                    AddBeam(wire, a0, mid, 0.04f);
                    AddBeam(wire, mid, a1, 0.04f);
                }
            }
        }

        private void WindTurbines()
        {
            int count = random.Next(2, 5);
            for (int i = 0; i < count; i++)
            {
                if (!FindSpot(120f, 320f, 8f, out float d, out float x)) continue;
                float g = Ground(d, x);
                Block(d - 8f, d + 8f, x - 8f, x + 8f);
                Material white = WorldMaterials.Plain("TurbineWhite", new Color(0.92f, 0.93f, 0.92f), 0.5f);
                meshes.For(white, 2f, true).AddCylinder(P(d, x, g), 1.6f, 60f, 12, Quaternion.identity);
                meshes.For(white, 2f, true).AddBox(P(d, x, g + 61f), new Vector3(2.2f, 2.4f, 5f), Quaternion.identity);
                GameObject rotor = new GameObject("TurbineRotor");
                rotor.transform.SetParent(chunk.Root.transform, false);
                rotor.transform.localPosition = P(d, x, g + 61f) + Vector3.forward * 2.8f;
                WorldMesh blades = new WorldMesh(1f);
                for (int b = 0; b < 3; b++)
                {
                    Quaternion q = Quaternion.Euler(0f, 0f, b * 120f);
                    blades.AddBox(q * new Vector3(0f, 14f, 0f), new Vector3(1.4f, 28f, 0.3f), q);
                }
                AttachMesh(rotor.transform, "Blades", blades, white);
                rotor.AddComponent<Spinner>().Configure(Vector3.forward, Range(35f, 55f));
                // A red beacon on the nacelle, blinking all night.
                GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cube);
                RemoveCollider(beacon);
                beacon.transform.SetParent(chunk.Root.transform, false);
                beacon.transform.localPosition = P(d, x, g + 62.5f);
                beacon.transform.localScale = Vector3.one * 0.6f;
                Blinker blinker = beacon.AddComponent<Blinker>();
                blinker.Configure(WorldMaterials.Glow("Beacon", new Color(1f, 0.05f, 0.03f), 1.5f), null, WorldMaterials.Plain("BeaconOff", new Color(0.3f, 0.05f, 0.04f), 0.5f), 2f);
                blinker.Add(beacon.GetComponent<Renderer>(), true);
            }
        }

        private void Orchard()
        {
            if (!FindSpot(30f, 80f, 20f, out float d, out float x)) return;
            float side = Mathf.Sign(x - WorldTerrain.CorridorCentre);
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < 7; col++)
                {
                    float td = d - 18f + col * 6f, tx = x + side * row * 6f;
                    if (td < plan.Start || td > plan.End || !mask.IsFree(td, tx, 1.5f)) continue;
                    extraTrees.Add((td, tx, CabTreeSpecies.Broadleaf, Range(0.45f, 0.6f)));
                }
            Block(d - 20f, d + 20f, Mathf.Min(x, x + side * 26f) - 3f, Mathf.Max(x, x + side * 26f) + 3f);
        }

        private void Sunflowers()
        {
            if (!FindSpot(16f, 40f, 12f, out float d, out float x)) return;
            WorldMesh stems = meshes.For(WorldMaterials.Plain("SunflowerStem", new Color(0.25f, 0.45f, 0.15f), 0.2f), 1f);
            WorldMesh heads = meshes.For(WorldMaterials.Plain("SunflowerHead", new Color(0.98f, 0.78f, 0.08f), 0.3f), 1f);
            WorldMesh hearts = meshes.For(WorldMaterials.Plain("SunflowerHeart", new Color(0.25f, 0.15f, 0.06f), 0.2f), 1f);
            for (int i = 0; i < 160; i++)
            {
                float fd = d + Range(-12f, 12f), fx = x + Range(-9f, 9f);
                if (!mask.IsFree(fd, fx, 0.3f)) continue;
                float g = Ground(fd, fx), h = Range(1.4f, 2.1f);
                stems.AddCylinder(P(fd, fx, g), 0.03f, h, 4, Quaternion.identity, false);
                // Heads face the morning side (all the same way, as sunflowers do).
                Quaternion face = R(fd) * Quaternion.Euler(-15f, 60f, 0f);
                heads.AddCylinder(P(fd, fx, g + h), 0.28f, 0.05f, 10, face * Quaternion.Euler(90f, 0f, 0f));
                hearts.AddCylinder(P(fd, fx, g + h) + face * Vector3.back * 0.03f, 0.14f, 0.04f, 8, face * Quaternion.Euler(90f, 0f, 0f));
            }
            Block(d - 13f, d + 13f, x - 10f, x + 10f);
        }

        private void Dachas()
        {
            int count = random.Next(3, 6);
            float side = Chance(0.5f) ? -1f : 1f;
            float x = WorldTerrain.CorridorCentre + side * Range(32f, 55f);
            float d = plan.Start + Range(8f, 20f);
            for (int i = 0; i < count && d < plan.End - 8f; i++)
            {
                if (mask.IsFree(d, x, 5f))
                {
                    int paint = random.Next(0, WorldMaterials.HousePaints.Length);
                    BuildHouse(d, x, 4.5f, 5f, 2.6f, WorldMaterials.Siding(paint), WorldMaterials.Roof(paint + 1), MinGround(d, x, 4.5f, 5f), 1, true);
                    // A greenhouse behind it.
                    float gx = x + side * 6f;
                    if (mask.IsFree(d, gx, 2.5f))
                    {
                        float g = Ground(d, gx);
                        meshes.For(WorldMaterials.Plain("Greenhouse", new Color(0.75f, 0.85f, 0.85f), 0.9f), 1f).AddBox(P(d, gx, g + 1f), new Vector3(3f, 2f, 5f), R(d));
                        Block(d - 3f, d + 3f, gx - 2f, gx + 2f);
                    }
                    BuildFence(d, x - side * 4f, 10f);
                }
                d += Range(12f, 18f);
            }
        }

        private void Lake()
        {
            if (!FindSpot(60f, 140f, 30f, out float d, out float x)) return;
            float radius = Range(18f, 30f);
            float level = float.MaxValue;
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                level = Mathf.Min(level, Ground(d + Mathf.Cos(a) * radius, x + Mathf.Sin(a) * radius));
            }
            level += 0.25f;
            WorldMesh water = meshes.For(WorldMaterials.Water, 8f);
            Vector3 centre = P(d, x, level);
            const int sides = 24;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                float r0 = radius * (0.8f + 0.3f * Mathf.PerlinNoise(i * 0.4f, plan.Seed % 50));
                float r1 = radius * (0.8f + 0.3f * Mathf.PerlinNoise((i + 1) % sides * 0.4f, plan.Seed % 50));
                water.AddTriangle(centre, centre + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r0, centre + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r1, Vector3.up);
            }
            // Reeds on the shore, a little pier with a boat.
            WorldMesh reeds = meshes.For(WorldMaterials.Plain("Reeds", new Color(0.45f, 0.5f, 0.25f), 0.2f), 1f);
            for (int i = 0; i < 60; i++)
            {
                float a = Range(0f, Mathf.PI * 2f);
                Vector3 root = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * Range(0.75f, 0.95f);
                reeds.AddBox(root + Vector3.up * 0.7f, new Vector3(0.04f, 1.4f, 0.04f), Quaternion.Euler(Range(-10f, 10f), 0f, Range(-10f, 10f)));
            }
            Vector3 pier = centre + new Vector3(radius * 0.6f, 0.2f, 0f);
            meshes.For(WorldMaterials.Planks, 1f, true).AddBox(pier, new Vector3(radius * 0.5f, 0.15f, 1.6f), Quaternion.identity);
            meshes.For(WorldMaterials.Plain("Boat", new Color(0.55f, 0.28f, 0.15f), 0.3f), 1f, true)
                .AddBox(centre + new Vector3(radius * 0.4f, 0.15f, 2f), new Vector3(3.2f, 0.4f, 1.2f), Quaternion.Euler(0f, 20f, 0f));
            Block(d - radius - 4f, d + radius + 4f, x - radius - 4f, x + radius + 4f);
        }

        private void Quarry()
        {
            if (!FindSpot(50f, 110f, 25f, out float d, out float x)) return;
            float g = Ground(d, x);
            WorldMesh rock = meshes.For(WorldMaterials.Rock, 4f, true);
            // Stepped benches cut into the hillside.
            for (int step = 0; step < 4; step++)
                rock.AddBox(P(d, x, g + step * 3f + 1.5f), new Vector3(40f - step * 8f, 3f, 36f - step * 7f), R(d));
            GameObject truck = VehicleFactory.Create(chunk.Root.transform, VehicleType.Truck, 7);
            truck.transform.localPosition = P(d, x, g + 12f);
            truck.transform.localRotation = R(d) * Quaternion.Euler(0f, 30f, 0f);
            Block(d - 22f, d + 22f, x - 22f, x + 22f);
        }

        private void TowerCrane()
        {
            if (!FindSpot(40f, 90f, 6f, out float d, out float x)) return;
            float g = Ground(d, x);
            Material yellow = WorldMaterials.Plain("CraneYellow", new Color(0.95f, 0.72f, 0.05f), 0.5f);
            meshes.For(yellow, 1f, true).AddBox(P(d, x, g + 22f), new Vector3(1.6f, 44f, 1.6f), R(d));
            GameObject jib = new GameObject("CraneJib");
            jib.transform.SetParent(chunk.Root.transform, false);
            jib.transform.localPosition = P(d, x, g + 44.5f);
            WorldMesh arm = new WorldMesh(1f);
            arm.AddBox(new Vector3(0f, 0f, 12f), new Vector3(1.2f, 1.4f, 34f), Quaternion.identity);
            arm.AddBox(new Vector3(0f, -0.4f, -7f), new Vector3(2.4f, 2f, 4f), Quaternion.identity);
            arm.AddBox(new Vector3(0f, -9f, 20f), new Vector3(0.05f, 18f, 0.05f), Quaternion.identity);
            AttachMesh(jib.transform, "Jib", arm, yellow);
            jib.AddComponent<Spinner>().Configure(Vector3.up, Range(-6f, 6f));
            Block(d - 4f, d + 4f, x - 4f, x + 4f);
        }

        private void SportsGround()
        {
            if (!FindSpot(35f, 90f, 24f, out float d, out float x)) return;
            float g = Ground(d, x) + 0.05f;
            Quaternion r = R(d);
            meshes.For(WorldMaterials.Plain("Pitch", new Color(0.2f, 0.5f, 0.2f), 0.15f), 1f).AddBox(P(d, x, g), new Vector3(30f, 0.08f, 46f), r);
            WorldMesh paint = meshes.For(WorldMaterials.Plain("PitchLines", new Color(0.95f, 0.95f, 0.95f), 0.2f), 1f);
            paint.AddBox(P(d, x, g + 0.05f), new Vector3(28f, 0.02f, 0.12f), r);
            foreach (float end in new[] { -1f, 1f })
            {
                paint.AddBox(P(d + end * 22f, x, g + 0.05f), new Vector3(28f, 0.02f, 0.12f), r);
                paint.AddBox(P(d + end * 22.3f, x, g + 1.2f), new Vector3(7.3f, 0.12f, 0.12f), r);
                foreach (float post in new[] { -3.6f, 3.6f }) paint.AddBox(P(d + end * 22.3f, x + post, g + 0.6f), new Vector3(0.12f, 1.2f, 0.12f), r);
            }
            foreach (float corner in new[] { -1f, 1f })
            {
                meshes.For(WorldMaterials.Steel, 1f, true).AddBox(P(d + corner * 24f, x + 16f, g + 7f), new Vector3(0.3f, 14f, 0.3f), r);
                Vector3 lamp = P(d + corner * 24f, x + 16f, g + 14f);
                meshes.For(WorldMaterials.LampGlow, 1f).AddBox(lamp, new Vector3(2f, 1f, 0.3f), r);
                chunk.Lamps.Add(CreateLight("FloodLight", lamp, LightType.Point, new Color(1f, 0.97f, 0.9f), 2.5f, 30f));
            }
            Block(d - 26f, d + 26f, x - 18f, x + 18f);
        }

        private void RadioMast()
        {
            if (!FindSpot(150f, 300f, 6f, out float d, out float x)) return;
            float g = Ground(d, x);
            WorldMesh red = meshes.For(WorldMaterials.Plain("MastRed", new Color(0.8f, 0.15f, 0.1f), 0.4f), 1f, true);
            WorldMesh white = meshes.For(WorldMaterials.Plain("MastWhite", new Color(0.9f, 0.9f, 0.9f), 0.4f), 1f, true);
            for (int i = 0; i < 8; i++) (i % 2 == 0 ? red : white).AddBox(P(d, x, g + i * 10f + 5f), new Vector3(1.4f - i * 0.1f, 10f, 1.4f - i * 0.1f), Quaternion.identity);
            GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            RemoveCollider(beacon);
            beacon.transform.SetParent(chunk.Root.transform, false);
            beacon.transform.localPosition = P(d, x, g + 81f);
            beacon.transform.localScale = Vector3.one;
            Blinker blinker = beacon.AddComponent<Blinker>();
            blinker.Configure(WorldMaterials.Glow("Beacon", new Color(1f, 0.05f, 0.03f), 1.5f), null, WorldMaterials.Plain("BeaconOff", new Color(0.3f, 0.05f, 0.04f), 0.5f), 1.6f);
            blinker.Add(beacon.GetComponent<Renderer>(), true);
            Block(d - 5f, d + 5f, x - 5f, x + 5f);
        }

        private void WaterTower()
        {
            if (!FindSpot(30f, 90f, 5f, out float d, out float x)) return;
            float g = Ground(d, x);
            meshes.For(WorldMaterials.Brick, 2f, true).AddCylinder(P(d, x, g), 2.4f, 16f, 14, Quaternion.identity);
            meshes.For(WorldMaterials.ShedMetal(2), 2f, true).AddCylinder(P(d, x, g + 16f), 3.6f, 4f, 16, Quaternion.identity);
            meshes.For(WorldMaterials.ShedMetal(1), 2f, true).AddCone(P(d, x, g + 20f), P(d, x, g + 22.5f), 3.9f, 16);
            Block(d - 4f, d + 4f, x - 4f, x + 4f);
        }

        private void Campfire()
        {
            if (!FindSpot(20f, 45f, 6f, out float d, out float x)) return;
            float g = Ground(d, x);
            Vector3 fire = P(d, x, g);
            meshes.For(WorldMaterials.Glow("Fire", new Color(1f, 0.45f, 0.08f), 1.2f), 1f).AddCone(fire, fire + Vector3.up * 1f, 0.5f, 7);
            meshes.For(WorldMaterials.Planks, 1f).AddBox(fire + Vector3.up * 0.1f, new Vector3(1.2f, 0.15f, 0.15f), Quaternion.Euler(0f, 30f, 0f));
            AddPool(fire, 5f);
            chunk.Lamps.Add(CreateLight("CampfireLight", fire + Vector3.up * 0.8f, LightType.Point, new Color(1f, 0.55f, 0.2f), 2.2f, 12f));
            Transform camp = new GameObject("Campers").transform;
            camp.SetParent(chunk.Root.transform, false);
            for (int i = 0; i < 4; i++)
            {
                float a = i / 4f * Mathf.PI * 2f;
                Vector3 spot = fire + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.8f;
                GameObject person = CabWorld3DPrototypeFactory.CreatePassenger(camp, "Camper_" + i, spot, Color.HSVToRGB((float)random.NextDouble(), 0.6f, 0.5f), false, i % 2 == 0, false, out _, out _, out _);
                person.transform.localRotation = Quaternion.LookRotation(new Vector3(-Mathf.Cos(a), 0f, -Mathf.Sin(a)), Vector3.up);
                TrackMaterials(person);
            }
            Material tent = WorldMaterials.Plain("TentGreen", new Color(0.2f, 0.45f, 0.25f), 0.2f);
            Vector3 t = fire + new Vector3(4f, 0f, 2f);
            meshes.For(tent, 1f, true).AddCone(t, t + Vector3.up * 1.6f, 1.4f, 4);
            Block(d - 5f, d + 5f, x - 5f, x + 5f);
        }

        private void Herd(bool moose)
        {
            if (!FindSpot(20f, 70f, 8f, out float d, out float x)) return;
            int count = moose ? random.Next(1, 3) : random.Next(4, 9);
            Material hide = moose ? WorldMaterials.Plain("MooseHide", new Color(0.28f, 0.2f, 0.13f), 0.2f) : WorldMaterials.Plain("CowHide", new Color(0.15f, 0.12f, 0.1f), 0.25f);
            Material patch = WorldMaterials.Plain("CowPatch", new Color(0.92f, 0.9f, 0.86f), 0.25f);
            for (int i = 0; i < count; i++)
            {
                float ad = d + Range(-8f, 8f), ax = x + Range(-6f, 6f);
                if (!mask.IsFree(ad, ax, 1.5f)) continue;
                float g = Ground(ad, ax);
                Quaternion q = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                Vector3 c = P(ad, ax, g);
                float s = moose ? 1.25f : 1f;
                WorldMesh body = meshes.For(hide, 1f, true);
                body.AddBox(c + Vector3.up * 1.15f * s, new Vector3(0.75f, 0.75f, 1.8f) * s, q);
                body.AddBox(c + q * new Vector3(0f, 1.3f, 1.05f) * s, new Vector3(0.36f, 0.4f, 0.6f) * s, q * Quaternion.Euler(moose ? 20f : 30f, 0f, 0f));
                for (int leg = 0; leg < 4; leg++)
                    body.AddBox(c + q * new Vector3((leg % 2 == 0 ? -0.25f : 0.25f), 0.4f, (leg < 2 ? 0.65f : -0.65f)) * s, new Vector3(0.14f, 0.8f, 0.14f) * s, q);
                if (!moose) meshes.For(patch, 1f).AddBox(c + q * new Vector3(0.38f, 1.2f, -0.2f), new Vector3(0.02f, 0.4f, 0.6f), q);
                else meshes.For(WorldMaterials.Plain("Antler", new Color(0.7f, 0.62f, 0.5f), 0.3f), 1f)
                    .AddBox(c + q * new Vector3(0f, 1.95f, 1.2f) * s, new Vector3(1.4f, 0.1f, 0.4f), q);
            }
        }

        private void SteamMonument()
        {
            float centre = plan.Start + WorldPlanner.PlatformCentre;
            float x = PlatformEdge - PlatformWidth - 20f;
            float d = centre - 40f;
            if (!mask.IsFree(d, x, 7f)) return;
            float g = Ground(d, x);
            Quaternion r = R(d);
            meshes.For(WorldMaterials.Concrete, 1f, true).AddBox(P(d, x, g + 0.5f), new Vector3(3.6f, 1f, 13f), r);
            Material black = WorldMaterials.Plain("SteamBlack", new Color(0.06f, 0.06f, 0.07f), 0.5f, 0.3f);
            Material red = WorldMaterials.Plain("SteamRed", new Color(0.65f, 0.08f, 0.06f), 0.4f);
            meshes.For(black, 1f, true).AddCylinder(P(d + 1.5f, x, g + 2.7f), 0.9f, 7f, 14, r * Quaternion.Euler(90f, 0f, 0f));
            meshes.For(black, 1f, true).AddBox(P(d - 3f, x, g + 3.1f), new Vector3(2.8f, 2.6f, 2.6f), r);
            meshes.For(black, 1f, true).AddCylinder(P(d + 4.3f, x, g + 3.5f), 0.3f, 1.4f, 10, r);
            for (int w = 0; w < 4; w++)
                foreach (float side in new[] { -1f, 1f })
                    meshes.For(red, 1f, true).AddCylinder(P(d - 2f + w * 1.8f, x + side * 0.95f, g + 1.8f), 0.75f, 0.15f, 14, r * Quaternion.Euler(0f, 0f, 90f));
            meshes.For(WorldMaterials.Glow("SteamStar", new Color(0.95f, 0.1f, 0.05f), 0.8f), 1f).AddBox(P(d + 5.2f, x, g + 2.7f), new Vector3(0.5f, 0.5f, 0.05f), r);
            Block(d - 8f, d + 8f, x - 3f, x + 3f);
        }
    }
}
