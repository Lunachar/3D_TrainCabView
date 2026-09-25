using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    /// <summary>Tunnels: concrete or brick bores of three sizes, and natural caves that may open into a miners' hall.</summary>
    public sealed partial class WorldChunkBuilder
    {
        private struct Bore
        {
            public float HalfWidth;
            public float WallHeight;
            public float Crown;
        }

        private static Bore BoreOf(TunnelSize size)
        {
            switch (size)
            {
                case TunnelSize.Small: return new Bore { HalfWidth = 5.0f, WallHeight = 4.2f, Crown = 2.3f };
                case TunnelSize.Large: return new Bore { HalfWidth = 7.2f, WallHeight = 5.4f, Crown = 3.4f };
                default: return new Bore { HalfWidth = 5.65f, WallHeight = 4.6f, Crown = 2.6f };
            }
        }

        private static Vector2[] BoreProfile(Bore bore, int arch = 10)
        {
            // Counter-clockwise (interior on the left), so the sweep's normals face into the bore.
            List<Vector2> points = new List<Vector2>
            {
                new Vector2(-bore.HalfWidth, -0.04f), new Vector2(bore.HalfWidth, -0.04f), new Vector2(bore.HalfWidth, bore.WallHeight)
            };
            for (int i = 1; i < arch; i++)
            {
                float a = i / (float)arch * Mathf.PI;
                points.Add(new Vector2(Mathf.Cos(a) * bore.HalfWidth, bore.WallHeight + Mathf.Sin(a) * bore.Crown));
            }
            points.Add(new Vector2(-bore.HalfWidth, bore.WallHeight));
            points.Add(new Vector2(-bore.HalfWidth, -0.04f));
            return points.ToArray();
        }

        private Material LiningMaterial => plan.Tunnel == TunnelStyle.Brick ? BrickLining
            : plan.Tunnel == TunnelStyle.Concrete ? WorldMaterials.Get("Concrete034", new Color(0.55f, 0.55f, 0.52f), 0.5f) : WorldMaterials.TunnelLining;

        private static Material brickLining;
        private static Material BrickLining
        {
            get
            {
                if (brickLining != null) return brickLining;
                brickLining = CabShaders.CreateLit("TunnelBrick", new Color(0.62f, 0.5f, 0.45f), 0.15f);
                brickLining.mainTexture = WorldTextures.GetAlbedo(WorldTextures.Surface.Bricks);
                brickLining.mainTextureScale = Vector2.one * (3f / WorldTextures.TileMetres(WorldTextures.Surface.Bricks));
                return brickLining;
            }
        }

        private void BuildTunnel()
        {
            float entrance = plan.Start + WorldPlanner.PortalInset;
            float exit = plan.End - WorldPlanner.PortalInset;
            float d0 = plan.TunnelEntrance ? entrance : plan.Start;
            float d1 = plan.TunnelExit ? exit : plan.End;
            float centre = WorldTerrain.CorridorCentre;
            Bore bore = BoreOf(plan.Bore);
            Block(plan.Start - 2f, plan.End + 2f, centre - 34f, centre + 34f);
            if (plan.Tunnel == TunnelStyle.Cave)
            {
                BuildCave(d0, d1, bore);
            }
            else
            {
                Sweep(meshes.For(LiningMaterial, 3f), d0, d1, 3f, centre, BoreProfile(bore));
                // Lamps along the left wall; brick tunnels get older, warmer and sparser ones.
                bool brick = plan.Tunnel == TunnelStyle.Brick;
                WorldMesh lamp = meshes.For(WorldMaterials.LampGlow, 1f);
                float spacing = brick ? 18f : 12f;
                for (int k = Mathf.CeilToInt(d0 / spacing); k * spacing < d1; k++)
                {
                    float d = k * spacing;
                    Vector3 position = P(d, centre - bore.HalfWidth + 0.25f, bore.WallHeight - 0.4f);
                    lamp.AddBox(position, new Vector3(0.18f, 0.12f, brick ? 0.3f : 0.55f), R(d));
                    chunk.TunnelLights.Add(CreateLight("TunnelInteriorLight", position + R(d) * new Vector3(0.4f, -0.2f, 0f), LightType.Point,
                        brick ? new Color(1f, 0.55f, 0.22f) : new Color(1f, 0.72f, 0.42f), 1.1f, 10f));
                }
                // Refuge niches in the walls every 40 m.
                WorldMesh niche = meshes.For(WorldMaterials.Plain("TunnelNiche", new Color(0.05f, 0.05f, 0.05f), 0.1f), 1f);
                for (int k = Mathf.CeilToInt(d0 / 40f); k * 40f < d1; k++)
                    foreach (int side in new[] { -1, 1 })
                        niche.AddQuad(P(k * 40f - 0.8f, centre + side * (bore.HalfWidth - 0.02f), 0f), P(k * 40f + 0.8f, centre + side * (bore.HalfWidth - 0.02f), 0f),
                            P(k * 40f + 0.8f, centre + side * (bore.HalfWidth - 0.02f), 2.2f), P(k * 40f - 0.8f, centre + side * (bore.HalfWidth - 0.02f), 2.2f),
                            R(k * 40f) * new Vector3(-side, 0f, 0f));
            }
            if (plan.TunnelEntrance) BuildPortal(entrance, -1f, bore);
            if (plan.TunnelExit) BuildPortal(exit, 1f, bore);
        }

        /// <summary>Headwall around the bore opening; the terrain supplies the rock face beside it.</summary>
        private void BuildPortal(float d, float outward, Bore bore)
        {
            float centre = WorldTerrain.CorridorCentre;
            float top = 0f;
            for (float x = -WorldTerrain.PortalHalfWidth; x <= WorldTerrain.PortalHalfWidth; x += 2f)
                top = Mathf.Max(top, terrain.Height(d + outward * 0.5f, centre + x, true));
            top += 1.2f;
            float half = WorldTerrain.PortalHalfWidth + 0.6f;
            Vector3 normal = R(d) * new Vector3(0f, 0f, outward);
            Material face = plan.Tunnel == TunnelStyle.Brick ? WorldMaterials.Brick : WorldMaterials.Rock;
            WorldMesh wall = meshes.For(face, 3f, true);
            Vector3 V(float x, float y) => P(d, centre + x, y) + normal * 0.05f;
            Vector2[] profile = BoreProfile(bore);
            wall.AddQuad(V(-half, -0.2f), V(-bore.HalfWidth, -0.2f), V(-bore.HalfWidth, top), V(-half, top), normal);
            wall.AddQuad(V(bore.HalfWidth, -0.2f), V(half, -0.2f), V(half, top), V(bore.HalfWidth, top), normal);
            for (int i = 2; i < profile.Length - 2; i++)
            {
                Vector2 a = profile[i], b = profile[i + 1];
                wall.AddQuad(V(a.x, a.y), V(b.x, b.y), V(b.x, top), V(a.x, top), normal);
            }
            if (plan.Tunnel == TunnelStyle.Cave) return;
            // A built portal: a heavier ring around the arch, a cornice and a year stone.
            WorldMesh rim = meshes.For(plan.Tunnel == TunnelStyle.Brick ? WorldMaterials.Plain("PortalStone", new Color(0.62f, 0.6f, 0.55f), 0.2f) : WorldMaterials.DarkConcrete, 1f, true);
            for (int i = 2; i < profile.Length - 2; i++)
            {
                Vector2 a = profile[i], b = profile[i + 1];
                Vector2 mid = (a + b) * 0.5f;
                Vector2 outwardRing = (mid - new Vector2(0f, bore.WallHeight)).normalized * 0.35f;
                rim.AddBox(P(d, centre + mid.x + outwardRing.x, mid.y + outwardRing.y) + normal * 0.3f,
                    new Vector3((b - a).magnitude + 0.1f, 0.7f, 0.5f), R(d) * Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg));
            }
            rim.AddBox(P(d, centre, top - 0.3f) + normal * 0.4f, new Vector3(half * 2f + 0.6f, 0.6f, 0.9f), R(d));
            for (int side = -1; side <= 1; side += 2)
                rim.AddBox(P(d, centre + side * (bore.HalfWidth + 0.3f), bore.WallHeight * 0.5f) + normal * 0.35f,
                    new Vector3(0.6f, bore.WallHeight + 0.2f, 0.6f), R(d));
            int year = 1890 + (plan.Seed % 120);
            AddText(year.ToString(), P(d, centre, bore.WallHeight + bore.Crown + 1.6f) + normal * 0.62f,
                R(d) * Quaternion.Euler(0f, outward > 0f ? 180f : 0f, 0f), new Color(0.25f, 0.23f, 0.2f), 0.6f);
        }

        // ---- Caves ----------------------------------------------------------------------------

        /// <summary>A rough natural cave: the profile is pushed in and out by noise and widens into a hall.</summary>
        private void BuildCave(float d0, float d1, Bore bore)
        {
            float centre = WorldTerrain.CorridorCentre;
            WorldMesh rock = meshes.For(WorldMaterials.Plain("CaveRock", new Color(0.30f, 0.28f, 0.26f), 0.25f), 3f);
            Bore hall = new Bore { HalfWidth = 28f, WallHeight = 6f, Crown = 13f };
            const float step = 3f;
            Vector3[] previous = null;
            for (float d = d0; d <= d1 + 0.01f; d += step)
            {
                float h = terrain.HallWeight(d);
                Bore here = new Bore
                {
                    HalfWidth = Mathf.Lerp(bore.HalfWidth, hall.HalfWidth, h),
                    WallHeight = Mathf.Lerp(bore.WallHeight, hall.WallHeight, h),
                    Crown = Mathf.Lerp(bore.Crown, hall.Crown, h)
                };
                Vector2[] profile = BoreProfile(here, 14);
                Vector3[] ring = new Vector3[profile.Length];
                Vector2 middle = new Vector2(0f, here.WallHeight * 0.6f);
                for (int k = 0; k < profile.Length; k++)
                {
                    Vector2 p = profile[k];
                    bool floor = p.y < 0f;
                    // Walls and roof bulge in and out; the floor stays flat for the track bed.
                    float bulge = floor ? 0f : (CaveNoise(d * 0.12f, k * 0.9f) - 0.35f) * 1.8f;
                    Vector2 outward = (p - middle).normalized;
                    Vector2 q = floor ? p : p + outward * bulge;
                    ring[k] = P(d, centre + q.x, q.y);
                }
                if (previous != null)
                    for (int k = 0; k < ring.Length - 1; k++)
                    {
                        Vector3 a = previous[k], b = previous[k + 1], c = ring[k + 1], e = ring[k];
                        Vector3 quadCentre = (a + b + c + e) * 0.25f;
                        Vector3 inward = (P(d - step * 0.5f, centre, here.WallHeight * 0.5f) - quadCentre).normalized;
                        Vector3 normal = Vector3.Cross(b - a, e - a).normalized;
                        if (Vector3.Dot(normal, inward) < 0f) normal = -normal;
                        rock.AddQuad(a, b, c, e, normal);
                    }
                previous = ring;
                DecorateCave(d, step, here, h);
            }
            if (plan.CaveHall) BuildMinersHall();
        }

        private float CaveNoise(float a, float b) => Mathf.PerlinNoise(Mathf.Repeat(a, 3000f) + 3.7f, b + (plan.Seed % 97) * 0.13f);

        private void DecorateCave(float d, float step, Bore bore, float hall)
        {
            float centre = WorldTerrain.CorridorCentre;
            WorldMesh stone = meshes.For(WorldMaterials.Plain("CaveDrip", new Color(0.52f, 0.47f, 0.40f), 0.45f), 1f);
            // Stalactites hang from the roof (never low over the wires), stalagmites rise by the walls.
            int drips = random.Next(0, 4);
            for (int i = 0; i < drips; i++)
            {
                float x = Range(-bore.HalfWidth * 0.9f, bore.HalfWidth * 0.9f);
                float roof = bore.WallHeight + Mathf.Sqrt(Mathf.Max(0f, 1f - (x / bore.HalfWidth) * (x / bore.HalfWidth))) * bore.Crown - 0.3f;
                float length = Range(0.4f, 2.4f + hall * 3f);
                if (Mathf.Abs(x) < 4.5f) length = Mathf.Min(length, roof - 6.6f);
                if (length < 0.3f) continue;
                float dd = d + Range(0f, step);
                stone.AddCone(P(dd, centre + x, roof), P(dd, centre + x, roof - length), Range(0.12f, 0.35f) * (1f + hall), 6);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                if (Chance(0.45f))
                {
                    float x = centre + side * Range(4.6f, bore.HalfWidth - 0.4f);
                    float dd = d + Range(0f, step);
                    stone.AddCone(P(dd, x, -0.05f), P(dd, x, Range(0.4f, 1.8f + hall * 2f)), Range(0.18f, 0.45f), 6);
                }
                // Glowing mushrooms in little clusters, some with a coloured light.
                if (Chance(0.3f))
                {
                    float x = centre + side * Range(4.4f, bore.HalfWidth - 0.3f);
                    float dd = d + Range(0f, step);
                    int colour = random.Next(0, 4);
                    Color glow = colour == 0 ? new Color(0.2f, 0.9f, 1f) : colour == 1 ? new Color(0.8f, 0.3f, 1f) : colour == 2 ? new Color(0.4f, 1f, 0.35f) : new Color(1f, 0.55f, 0.15f);
                    Material cap = WorldMaterials.Glow("Mushroom" + colour, glow, 0.8f);
                    WorldMesh stem = meshes.For(WorldMaterials.Plain("MushroomStem", new Color(0.85f, 0.82f, 0.7f), 0.3f), 1f);
                    int count = random.Next(3, 8);
                    for (int m = 0; m < count; m++)
                    {
                        Vector3 foot = P(dd + Range(-0.8f, 0.8f), x + Range(-0.5f, 0.5f), -0.05f);
                        float height = Range(0.15f, 0.7f);
                        stem.AddCylinder(foot, 0.04f + height * 0.05f, height, 5, Quaternion.identity, false);
                        meshes.For(cap, 1f).AddCone(foot + Vector3.up * (height - 0.02f), foot + Vector3.up * (height + height * 0.35f), height * 0.45f + 0.08f, 8);
                    }
                    if (Chance(0.5f)) chunk.TunnelLights.Add(CreateLight("MushroomGlow", P(dd, x, 0.6f), LightType.Point, glow, 1.2f, 6f));
                }
                // Moss and wet streaks on the lower walls; now and then a crystal cluster.
                if (Chance(0.5f))
                {
                    float dd = d + Range(0f, step);
                    float x = centre + side * (bore.HalfWidth - 0.15f);
                    float y = Range(0.3f, bore.WallHeight - 0.5f);
                    float w = Range(0.8f, 2.4f), h = Range(0.6f, 1.8f);
                    meshes.For(WorldMaterials.Plain("CaveMoss", new Color(0.18f, 0.32f, 0.12f), 0.15f), 1f)
                        .AddQuad(P(dd - w, x, y), P(dd + w, x, y), P(dd + w, x, y + h), P(dd - w, x, y + h), R(dd) * new Vector3(-side, 0f, 0f));
                }
                if (Chance(0.08f + hall * 0.2f))
                {
                    float dd = d + Range(0f, step);
                    float x = centre + side * Range(4.8f, bore.HalfWidth - 0.4f);
                    Material crystal = WorldMaterials.Glow("Crystal" + (side > 0 ? 0 : 1), side > 0 ? new Color(0.35f, 0.7f, 1f) : new Color(0.9f, 0.35f, 1f), 0.9f);
                    for (int c = 0; c < 5; c++)
                        meshes.For(crystal, 1f).AddBox(P(dd, x, 0.4f) + Random3(0.3f), new Vector3(0.12f, Range(0.4f, 1.2f), 0.12f),
                            Quaternion.Euler(Range(-35f, 35f), Range(0f, 180f), Range(-35f, 35f)));
                }
            }
        }

        private Vector3 Random3(float size) => new Vector3(Range(-size, size), Range(-size, size), Range(-size, size));

        /// <summary>The cavern hall: miners' huts, lanterns, a mine-cart line and gnomes; sometimes their halt.</summary>
        private void BuildMinersHall()
        {
            float centre = WorldTerrain.CorridorCentre;
            float middle = plan.Start + WorldPlanner.ChunkLength * 0.5f;
            WorldMesh wood = meshes.For(WorldMaterials.Planks, 1.5f, true);
            // Huts on both sides of the line, well clear of the tracks.
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 4; i++)
                {
                    float d = middle - 30f + i * 18f + Range(-3f, 3f);
                    float x = centre + side * Range(12f, 20f);
                    if (plan.IsStation && side < 0 && Mathf.Abs(d - middle) < 34f) continue;
                    BuildHouse(d, x, Range(4f, 5.5f), Range(4.5f, 6f), 2.4f, WorldMaterials.Planks, WorldMaterials.Planks, -0.05f, 1, false);
                    Vector3 door = P(d, x - side * 2.8f, 1.6f);
                    meshes.For(WorldMaterials.Glow("MinerLantern", new Color(1f, 0.72f, 0.3f), 0.8f), 1f).AddBox(door, new Vector3(0.2f, 0.28f, 0.2f), R(d));
                    AddPool(P(d, x - side * 3.3f, -0.05f), 3.5f);
                    if (i % 2 == 0) chunk.TunnelLights.Add(CreateLight("MinerLanternLight", door, LightType.Point, new Color(1f, 0.7f, 0.35f), 1.4f, 9f));
                }
            // A narrow-gauge mine-cart line along the right side, with a few loaded carts.
            float cartX = centre + 9.5f;
            foreach (float rail in new[] { -0.35f, 0.35f })
                Sweep(meshes.For(WorldMaterials.Rail, 1f), middle - 45f, middle + 45f, 3f, cartX + rail,
                    new[] { new Vector2(-0.03f, 0.02f), new Vector2(-0.03f, 0.14f), new Vector2(0.03f, 0.14f), new Vector2(0.03f, 0.02f) });
            for (float d = middle - 40f; d < middle + 40f; d += 0.7f)
                wood.AddBox(P(d, cartX, 0.02f), new Vector3(1.1f, 0.06f, 0.15f), R(d));
            for (int c = 0; c < 3; c++)
            {
                float d = middle - 20f + c * 3.2f;
                meshes.For(WorldMaterials.Plain("MineCart", new Color(0.35f, 0.25f, 0.18f), 0.3f, 0.5f), 1f, true)
                    .AddBox(P(d, cartX, 0.6f), new Vector3(1f, 0.7f, 1.6f), R(d));
                meshes.For(WorldMaterials.Glow("Ore", new Color(0.4f, 0.8f, 1f), 0.6f), 1f).AddBox(P(d, cartX, 0.98f), new Vector3(0.8f, 0.12f, 1.3f), R(d));
            }
            // Gnomes at work and at rest around the hall.
            Transform folk = new GameObject("Gnomes").transform;
            folk.SetParent(chunk.Root.transform, false);
            for (int i = 0; i < 10; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float d = middle + Range(-40f, 40f);
                float x = centre + side * Range(9f, 22f);
                if (plan.IsStation && side < 0 && Mathf.Abs(d - middle) < 30f) x = centre - Range(16f, 22f);
                GameObject gnome = CreateGnome(folk, "Gnome_" + i, P(d, x, -0.05f));
                gnome.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
            }
        }

        /// <summary>A gnome: a small passenger with a long white beard and a pointed red hat.</summary>
        private GameObject CreateGnome(Transform parent, string name, Vector3 position)
        {
            Color coat = random.NextDouble() < 0.5 ? new Color(0.2f, 0.35f, 0.6f) : new Color(0.35f, 0.5f, 0.2f);
            GameObject gnome = CabWorld3DPrototypeFactory.CreatePassenger(parent, name, position, coat, false, false, false, out _, out _, out _);
            Gnomify(gnome);
            return gnome;
        }

        /// <summary>Turns a person into a gnome: smaller, with a pointed red hat and a white beard.</summary>
        private void Gnomify(GameObject gnome)
        {
            gnome.transform.localScale = Vector3.one * 0.58f;
            Transform head = gnome.transform.Find("Head");
            Vector3 headPosition = head != null ? head.localPosition : new Vector3(0f, 1.73f, 0f);
            WorldMesh hat = new WorldMesh(1f);
            hat.AddCone(headPosition + Vector3.up * 0.12f, headPosition + new Vector3(0f, 0.95f, -0.12f), 0.26f, 10);
            AttachMesh(gnome.transform, "GnomeHat", hat, WorldMaterials.Plain("GnomeHat", new Color(0.82f, 0.08f, 0.06f), 0.3f));
            WorldMesh beard = new WorldMesh(1f);
            beard.AddCone(headPosition + new Vector3(0f, -0.05f, 0.14f), headPosition + new Vector3(0f, -0.62f, 0.22f), 0.2f, 8);
            AttachMesh(gnome.transform, "GnomeBeard", beard, WorldMaterials.Plain("GnomeBeard", new Color(0.95f, 0.95f, 0.92f), 0.2f));
            TrackMaterials(gnome);
        }

        private void AttachMesh(Transform parent, string name, WorldMesh mesh, Material material)
        {
            GameObject part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            Mesh built = mesh.ToMesh(name);
            part.GetComponent<MeshFilter>().sharedMesh = built;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            chunk.Meshes.Add(built);
        }
    }
}
