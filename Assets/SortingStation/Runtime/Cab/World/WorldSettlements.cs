using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    /// <summary>Roads, villages, towns, big cities and stations.</summary>
    public sealed partial class WorldChunkBuilder
    {
        private static readonly string[] ShopNames =
        {
            "ПРОДУКТЫ", "АПТЕКА", "КАФЕ", "ОТЕЛЬ", "24 ЧАСА", "ЦВЕТЫ", "ПЕКАРНЯ", "КИНО", "СУШИ", "БАНК", "КНИГИ", "ОПТИКА",
            "ПИЦЦА", "ЧАЙ", "РАМЕН", "ТЕАТР", "ГОСТИНИЦА", "ШАУРМА", "МАГАЗИН", "САЛОН"
        };

        // ---- Shared pieces ----------------------------------------------------------------------

        /// <summary>Soft pool of lamplight on the ground (an additive decal, lit only at night).</summary>
        private void AddPool(Vector3 centre, float radius)
        {
            WorldMesh pool = meshes.For(WorldMaterials.LightPool, 1f);
            Vector3 a = centre + new Vector3(-radius, 0.05f, -radius), b = centre + new Vector3(radius, 0.05f, -radius);
            Vector3 c = centre + new Vector3(radius, 0.05f, radius), e = centre + new Vector3(-radius, 0.05f, radius);
            pool.AddQuad(a, b, c, e, Vector3.up, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            for (int i = 0; i < 4; i++) pool.Colors.Add(Color.white);
        }

        private void BuildStreetLamp(float d, float x, float baseY, float armSide, float height = 5f)
        {
            Quaternion r = R(d);
            WorldMesh steel = meshes.For(WorldMaterials.Steel, 1f, true);
            steel.AddCylinder(P(d, x, baseY), 0.07f, height, 6, r, false);
            steel.AddBox(P(d, x + armSide * 0.45f, baseY + height), new Vector3(0.9f, 0.06f, 0.08f), r);
            Vector3 head = P(d, x + armSide * 0.9f, baseY + height - 0.1f);
            meshes.For(WorldMaterials.LampGlow, 1f).AddBox(head, new Vector3(0.45f, 0.1f, 0.25f), r);
            AddPool(P(d, x + armSide * 0.9f, baseY), 5.5f);
            chunk.Lamps.Add(CreateLight("ScenicNightLight", head + Vector3.down * 0.3f, LightType.Point, new Color(1f, 0.80f, 0.52f), 1.5f, 13f));
        }

        /// <summary>3D lettering (drawn unlit, so it glows at night like neon or a lit sign).</summary>
        private TextMesh AddText(string text, Vector3 position, Quaternion rotation, Color colour, float letterHeight)
        {
            GameObject label = new GameObject("Sign_" + text, typeof(TextMesh));
            label.transform.SetParent(chunk.Root.transform, false);
            label.transform.localPosition = position;
            label.transform.localRotation = rotation;
            TextMesh mesh = label.GetComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 48;
            mesh.characterSize = letterHeight * 0.53f;
            mesh.color = colour;
            return mesh;
        }

        /// <summary>A sign facing the track side of a building, turned a little toward the oncoming train.</summary>
        private Quaternion FacingTrack(float d, float x) => R(d) * Quaternion.Euler(0f, x < WorldTerrain.CorridorCentre ? -55f : 55f, 0f);

        private void AddNeonSign(float d, float x, float y, bool vertical)
        {
            // Signs come in several fashions: neon letters, light boxes, LED screens, round logos, vertical neon.
            int style = vertical ? 4 : random.Next(0, 4);
            int colour = random.Next(0, WorldMaterials.NeonColours.Length);
            Color neon = WorldMaterials.NeonColours[colour];
            string name = ShopNames[random.Next(0, ShopNames.Length)];
            Quaternion facing = FacingTrack(d, x);
            float towardTrack = x < WorldTerrain.CorridorCentre ? 1f : -1f;
            Vector3 position = P(d, x + towardTrack * 0.35f, y);
            Vector3 front = facing * Vector3.back;
            Material back = WorldMaterials.Plain("SignBack", new Color(0.05f, 0.05f, 0.06f), 0.5f);
            switch (style)
            {
                case 0:
                {
                    // Neon letters on a dark board, framed by a tube.
                    float width = name.Length * 0.75f + 1f;
                    meshes.For(back, 1f).AddBox(position, new Vector3(width, 1.2f, 0.2f), facing);
                    WorldMesh tube = meshes.For(WorldMaterials.Neon(colour), 1f);
                    tube.AddBox(position + Vector3.down * 0.62f + front * 0.1f, new Vector3(width, 0.06f, 0.06f), facing);
                    tube.AddBox(position + Vector3.up * 0.62f + front * 0.1f, new Vector3(width, 0.06f, 0.06f), facing);
                    AddText(name, position + front * 0.14f, facing, neon, 0.8f);
                    break;
                }
                case 1:
                {
                    // A glowing white light box with dark letters.
                    float width = name.Length * 0.7f + 1.2f;
                    meshes.For(WorldMaterials.Glow("LightBox", new Color(0.95f, 0.95f, 0.9f), 0.6f), 1f).AddBox(position, new Vector3(width, 1.3f, 0.35f), facing);
                    meshes.For(WorldMaterials.Neon(colour), 1f).AddBox(position + Vector3.down * 0.7f, new Vector3(width + 0.1f, 0.12f, 0.4f), facing);
                    AddText(name, position + front * 0.2f, facing, new Color(0.08f, 0.08f, 0.1f), 0.8f);
                    break;
                }
                case 2:
                {
                    // An LED screen cycling through colours, with white lettering.
                    Material screen = CabPbrMaterials.Emissive("LedScreen", new Color(0.05f, 0.05f, 0.05f), neon);
                    chunk.Materials.Add(screen);
                    GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    RemoveCollider(panel);
                    panel.name = "LedScreen";
                    panel.transform.SetParent(chunk.Root.transform, false);
                    panel.transform.localPosition = position + Vector3.up * 0.8f;
                    panel.transform.localRotation = facing;
                    panel.transform.localScale = new Vector3(4.2f, 2.4f, 0.25f);
                    panel.GetComponent<Renderer>().sharedMaterial = screen;
                    panel.AddComponent<ColorCycle>().Configure(screen, (float)random.NextDouble());
                    meshes.For(back, 1f).AddBox(position + Vector3.up * 0.8f - front * 0.05f, new Vector3(4.5f, 2.7f, 0.2f), facing);
                    AddText(name, position + Vector3.up * 0.8f + front * 0.16f, facing, Color.white, 0.7f);
                    break;
                }
                default:
                {
                    if (style == 3)
                    {
                        // A round logo with the name beside it.
                        meshes.For(WorldMaterials.Neon(colour), 1f).AddCylinder(position - facing * Vector3.right * 1.6f, 0.75f, 0.2f, 18, facing * Quaternion.Euler(90f, 0f, 0f));
                        AddText(name.Substring(0, 1), position - facing * Vector3.right * 1.6f + front * 0.25f, facing, new Color(0.05f, 0.05f, 0.08f), 0.9f);
                        AddText(name, position + facing * Vector3.right * (name.Length * 0.3f) + front * 0.05f, facing, neon, 0.7f);
                        break;
                    }
                    string column = string.Join("\n", name.ToCharArray());
                    float height = name.Length * 0.9f + 0.6f;
                    meshes.For(back, 1f).AddBox(position, new Vector3(1.1f, height, 0.2f), facing);
                    meshes.For(WorldMaterials.Neon(colour), 1f).AddBox(position - front * 0.02f, new Vector3(1.25f, height + 0.15f, 0.12f), facing);
                    AddText(column, position + front * 0.14f, facing, neon, 0.62f);
                    break;
                }
            }
        }

        /// <summary>A lit shop window with goods on show, and a small crowd of onlookers in front of it.</summary>
        private void BuildShowWindow(float centre, float wallX, float ground, float side)
        {
            float towardTrack = -side;
            Quaternion r = R(centre);
            Vector3 normal = r * new Vector3(towardTrack, 0f, 0f);
            float width = Range(6f, 10f);
            Vector3 glassCentre = P(centre, wallX + towardTrack * 0.06f, ground + 2.2f);
            Vector3 along = r * Vector3.forward * (width * 0.5f), up = Vector3.up * 2f;
            meshes.For(WorldMaterials.Glow("ShowWindow", new Color(1f, 0.95f, 0.85f), 0.55f), 1f)
                .AddQuad(glassCentre - along - up, glassCentre + along - up, glassCentre + along + up, glassCentre - along + up, normal);
            meshes.For(WorldMaterials.Plain("WindowFrameDark", new Color(0.12f, 0.12f, 0.13f), 0.6f, 0.6f), 1f)
                .AddBox(glassCentre + up + normal * 0.1f, new Vector3(0.25f, 0.25f, width + 0.3f), r);
            // Goods and mannequins behind the glass: coloured shapes on stands.
            for (int i = 0; i < 4; i++)
            {
                float s = Range(-width * 0.4f, width * 0.4f);
                Vector3 item = P(centre + s, wallX + towardTrack * 0.25f, ground + Range(0.8f, 2.4f));
                meshes.For(WorldMaterials.Neon(random.Next(0, WorldMaterials.NeonColours.Length)), 1f)
                    .AddBox(item, new Vector3(0.1f, Range(0.4f, 1.1f), Range(0.3f, 0.8f)), r);
            }
            AddPool(P(centre, wallX + towardTrack * 2.5f, ground), 4.5f);
            // Onlookers facing the window.
            Transform crowd = new GameObject("Onlookers").transform;
            crowd.SetParent(chunk.Root.transform, false);
            int people = random.Next(4, 10);
            for (int i = 0; i < people; i++)
            {
                float s = Range(-width * 0.55f, width * 0.55f);
                float away = Range(1.2f, 3.6f);
                Vector3 spot = P(centre + s, wallX + towardTrack * away, ground);
                Quaternion facing = r * Quaternion.Euler(0f, (towardTrack > 0f ? -90f : 90f) + Range(-35f, 35f), 0f);
                PersonAnimator person = SpawnPerson(crowd, "Onlooker_" + i, spot, facing, random, i % 5 == 0);
                person.AllowStrolling = false;
            }
            Block(centre - width, centre + width, wallX + towardTrack * 4f - 1f, wallX + towardTrack * 4f + 1f);
        }

        // ---- Roads -----------------------------------------------------------------------------

        private void BuildRoads()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                bool anyRoad = false;
                const float step = 6f;
                for (float d = plan.Start; d < plan.End - 0.01f; d += step)
                {
                    float e = Mathf.Min(plan.End, d + step);
                    if (!WorldRoads.TryLateral(planner, terrain, d, side, out float xa) || !WorldRoads.TryLateral(planner, terrain, e, side, out float xb)) continue;
                    if (Mathf.Abs(xa) > WorldRoads.AwayReach || Mathf.Abs(xb) > WorldRoads.AwayReach) continue;
                    anyRoad = true;
                    float ya = WorldRoads.Height(terrain, d, xa), yb = WorldRoads.Height(terrain, e, xb);
                    const float w = WorldRoads.HalfWidth;
                    meshes.For(WorldMaterials.Asphalt, 4f).AddQuad(P(d, xa - w, ya), P(d, xa + w, ya), P(e, xb + w, yb), P(e, xb - w, yb), Vector3.up,
                        new Vector2(0f, d - plan.Start), new Vector2(2f * w, d - plan.Start), new Vector2(2f * w, e - plan.Start), new Vector2(0f, e - plan.Start));
                    WorldMesh gravel = meshes.For(WorldMaterials.Gravel, 3f);
                    foreach (float edge in new[] { -1f, 1f })
                    {
                        float inner = edge * w, outer = edge * (w + 1.2f);
                        gravel.AddQuad(P(d, xa + inner, ya - 0.01f), P(d, xa + outer, ya - 0.12f), P(e, xb + outer, yb - 0.12f), P(e, xb + inner, yb - 0.01f), Vector3.up);
                        // Edge lines.
                        meshes.For(WorldMaterials.Plain("RoadPaint", new Color(0.88f, 0.88f, 0.84f), 0.3f), 1f)
                            .AddQuad(P(d, xa + edge * (w - 0.35f), ya + 0.012f), P(d, xa + edge * (w - 0.23f), ya + 0.012f),
                                P(e, xb + edge * (w - 0.23f), yb + 0.012f), P(e, xb + edge * (w - 0.35f), yb + 0.012f), Vector3.up);
                    }
                    // Dashed centre line: 3 m paint, 3 m gap, continuous across chunks.
                    if (Mathf.Repeat(d, 12f) < 5.9f)
                        meshes.For(WorldMaterials.Plain("RoadPaint", new Color(0.88f, 0.88f, 0.84f), 0.3f), 1f)
                            .AddQuad(P(d, xa - 0.07f, ya + 0.012f), P(d, xa + 0.07f, ya + 0.012f),
                                P(d + 3f, xb + 0.07f, yb + 0.012f), P(d + 3f, xa - 0.07f, ya + 0.012f), Vector3.up);
                    Block(d - 0.5f, e + 0.5f, Mathf.Min(xa, xb) - w - 2f, Mathf.Max(xa, xb) + w + 2f);
                }
                if (!anyRoad) continue;
                // Street lighting through settlements (the lamps stand on the track side of the road).
                WorldChunkKind litKind = plan.KindOnSide(side);
                bool lit = litKind == WorldChunkKind.Village || litKind == WorldChunkKind.Town || litKind == WorldChunkKind.City || litKind == WorldChunkKind.Industrial;
                if (!lit) continue;
                WorldChunkKind sideKind = plan.KindOnSide(side);
                float spacing = sideKind == WorldChunkKind.City ? 15f : sideKind == WorldChunkKind.Town ? 20f : sideKind == WorldChunkKind.Village ? 35f : 28f;
                for (float d = Mathf.Ceil(plan.Start / spacing) * spacing; d < plan.End; d += spacing)
                {
                    if (!WorldRoads.TryLateral(planner, terrain, d, side, out float x) || Mathf.Abs(x) > 80f) continue;
                    float lampX = x - side * (WorldRoads.HalfWidth + 1.4f);
                    if (!StructuresFreeExceptRoads(d, lampX)) continue;
                    BuildStreetLamp(d, lampX, WorldRoads.Height(terrain, d, x) - 0.06f, side, plan.Kind == WorldChunkKind.City ? 7f : 5.5f);
                }
            }
        }

        private bool StructuresFreeExceptRoads(float d, float x) => Mathf.Abs(x - WorldTerrain.CorridorCentre) > 9f;

        /// <summary>Centre of the road on a side at d, if it runs alongside here (for settlement layout).</summary>
        private bool RoadAt(float d, int side, out float x) => WorldRoads.TryLateral(planner, terrain, d, side, out x) && Mathf.Abs(x) < 120f;

        // ---- Trackside -------------------------------------------------------------------------

        private void BuildTrackside()
        {
            // Kilometre posts and the odd relay cabinet.
            int km = Mathf.CeilToInt(plan.Start / 1000f);
            if (km * 1000f < plan.End && !planner.TryGetTunnel(km * 1000f, out _, out _))
            {
                float d = km * 1000f;
                float x = PlacementMask.CorridorLeft - 0.9f;
                meshes.For(WorldMaterials.Plain("KmPost", new Color(0.92f, 0.92f, 0.9f), 0.3f), 1f).AddBox(P(d, x, Ground(d, x) + 0.6f), new Vector3(0.16f, 1.2f, 0.16f), R(d));
                AddText(km.ToString(), P(d, x + 0.1f, Ground(d, x) + 1.0f), R(d) * Quaternion.Euler(0f, -90f, 0f), new Color(0.1f, 0.1f, 0.1f), 0.18f);
            }
            if (Chance(0.3f) && plan.Kind != WorldChunkKind.Tunnel && plan.Kind != WorldChunkKind.Water)
            {
                float d = Range(plan.Start + 10f, plan.End - 10f);
                float x = PlacementMask.CorridorRight + 1.4f;
                if (StructuresFree(d, x))
                {
                    meshes.For(WorldMaterials.Plain("RelayCabinet", new Color(0.55f, 0.58f, 0.52f), 0.4f), 1f, true)
                        .AddBox(P(d, x, Ground(d, x) + 0.8f), new Vector3(1.1f, 1.6f, 0.8f), R(d));
                    Block(d - 1f, d + 1f, x - 1f, x + 1f);
                }
            }
        }

        // ---- Villages --------------------------------------------------------------------------

        private void BuildVillage()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                if (!SideAllowed(side)) continue;
                bool road = RoadAt(plan.Start + 60f, side, out float roadX);
                if (!road) roadX = WorldTerrain.CorridorCentre + side * 70f;
                for (int row = -1; row <= 1; row += 2)
                {
                    float d = plan.Start + Range(4f, 12f);
                    while (d < plan.End - 8f)
                    {
                        float length = Range(6f, 9f), depth = Range(6f, 8f);
                        if (!RoadAt(d, side, out float here)) here = roadX;
                        float x = here + row * (WorldRoads.HalfWidth + Range(8f, 12f));
                        bool trackSide = side * (x - here) < 0f;
                        bool roomy = Mathf.Abs(x - WorldTerrain.CorridorCentre) > 18f;
                        if (roomy && mask.IsFree(d, x, Mathf.Max(length, depth) * 0.5f + 1f))
                        {
                            int variant = random.Next(0, 12);
                            Material walls = variant % 3 == 0 ? WorldMaterials.Plaster(variant) : WorldMaterials.Siding(variant);
                            BuildHouse(d, x, depth, length, Range(2.8f, 3.4f), walls, WorldMaterials.Roof(variant), MinGround(d, x, depth, length), 2, true);
                            float fenceX = here + row * (WorldRoads.HalfWidth + 2.2f);
                            if (!trackSide || Mathf.Abs(fenceX - WorldTerrain.CorridorCentre) > 14f) BuildFence(d, fenceX, length + Range(6f, 10f));
                            if (Chance(0.5f)) BuildShed(d + length * 0.5f + 3f, x + row * 3f);
                        }
                        d += length + Range(9f, 16f);
                    }
                }
            }
            if (Chance(0.3f)) BuildChurch();
        }

        /// <summary>A small church with a drum and an onion dome, on a rise behind the village.</summary>
        private void BuildChurch()
        {
            float side = sideFilter != 0 ? sideFilter : Chance(0.5f) ? -1f : 1f;
            float d = plan.Start + Range(30f, 90f);
            float x = WorldTerrain.CorridorCentre + side * Range(60f, 110f);
            if (!mask.IsFree(d, x, 9f)) return;
            float ground = MinGround(d, x, 10f, 16f);
            BuildHouse(d, x, 10f, 16f, 6f, WorldMaterials.Plaster(4), WorldMaterials.Roof(3), ground, 3, false);
            Material white = WorldMaterials.Plaster(4);
            Material dome = WorldMaterials.Plain("ChurchDome", new Color(0.35f, 0.62f, 0.45f), 0.6f, 0.4f);
            Quaternion r = R(d);
            float top = ground + 6f + 3.6f;
            meshes.For(white, 2f, true).AddCylinder(P(d, x, top - 1f), 2.2f, 4.5f, 12, r);
            // The onion dome is a stack of short cylinders swelling and narrowing to a point.
            float[] radii = { 2.4f, 2.8f, 2.9f, 2.6f, 2.0f, 1.2f, 0.5f, 0.12f };
            for (int i = 0; i < radii.Length; i++)
                meshes.For(dome, 1f, true).AddCylinder(P(d, x, top + 3.5f + i * 0.55f), radii[i], 0.56f, 14, r);
            meshes.For(WorldMaterials.Glow("Cross", new Color(1f, 0.85f, 0.4f), 0.5f), 1f).AddBox(P(d, x, top + 8.6f), new Vector3(0.08f, 1.6f, 0.08f), r);
            meshes.For(WorldMaterials.Glow("Cross", new Color(1f, 0.85f, 0.4f), 0.5f), 1f).AddBox(P(d, x, top + 8.9f), new Vector3(0.08f, 0.08f, 0.8f), r);
        }

        // ---- Towns -----------------------------------------------------------------------------

        private void BuildTown()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                if (!SideAllowed(side)) continue;
                float front = RoadAt(plan.Start + 60f, side, out float roadX) ? Mathf.Abs(roadX - WorldTerrain.CorridorCentre) + WorldRoads.HalfWidth + 5f : Range(26f, 34f);
                float d = plan.Start + Range(3f, 10f);
                while (d < plan.End - 14f)
                {
                    float length = Mathf.Min(Range(24f, 46f), plan.End - 4f - d);
                    if (length < 16f) break;
                    float depth = Range(11f, 13f);
                    float x = WorldTerrain.CorridorCentre + side * (front + depth * 0.5f);
                    float centre = d + length * 0.5f;
                    if (mask.IsFree(centre, x, length * 0.5f))
                    {
                        int floors = Chance(0.35f) ? 9 : 5;
                        float ground = MinGround(centre, x, depth, length);
                        BuildBlock(centre, x, depth, length, floors * 3f, random.Next(0, 4), ground);
                        BuildShopFront(centre, x - side * depth * 0.5f, length, ground, side);
                        float back = x + side * Range(22f, 34f);
                        if (Chance(0.7f) && mask.IsFree(centre, back, length * 0.5f))
                            BuildBlock(centre, back, depth, length * Range(0.7f, 1f), (Chance(0.5f) ? 9 : 5) * 3f, random.Next(0, 4), MinGround(centre, back, depth, length));
                    }
                    d += length + Range(10f, 18f);
                }
            }
        }

        /// <summary>Lit shop windows along the ground floor and a sign above them, facing the track.</summary>
        private void BuildShopFront(float centre, float wallX, float length, float ground, float side)
        {
            float towardTrack = -side;
            Quaternion r = R(centre);
            Vector3 normal = r * new Vector3(towardTrack, 0f, 0f);
            WorldMesh windows = meshes.For(WorldMaterials.ShopWindow, 1f);
            for (float s = -length * 0.5f + 1.5f; s < length * 0.5f - 3f; s += 5f)
            {
                Vector3 c = P(centre + s + 1.7f, wallX + towardTrack * 0.05f, ground + 1.6f);
                Vector3 along = r * Vector3.forward * 1.6f, up = Vector3.up * 1.1f;
                windows.AddQuad(c - along - up, c + along - up, c + along + up, c - along + up, normal);
            }
            if (Chance(0.8f)) AddNeonSign(centre + Range(-length * 0.3f, length * 0.3f), wallX, ground + 3.6f, false);
            // Now and then a big show window draws a crowd.
            if (Chance(plan.Kind == WorldChunkKind.City ? 0.35f : 0.15f)) BuildShowWindow(centre + Range(-length * 0.2f, length * 0.2f), wallX, ground, side);
        }

        // ---- Big city --------------------------------------------------------------------------

        private void BuildCity()
        {
            if (plan.DenseLowRise) BuildDenseCity();
            else BuildMetropolis();
            // A fence and utility poles along the line through the city.
            WorldMesh fence = meshes.For(WorldMaterials.Plain("CityFence", new Color(0.32f, 0.35f, 0.34f), 0.4f, 0.5f), 1f);
            for (int side = -1; side <= 1; side += 2)
            {
                if (!SideAllowed(side)) continue;
                float fx = side < 0 ? PlacementMask.CorridorLeft - 1.6f : PlacementMask.CorridorRight + 1.6f;
                for (float d = plan.Start; d < plan.End - 0.01f; d += 4f)
                {
                    if (!StructuresFree(d + 2f, fx)) continue;
                    fence.AddBox(P(d + 2f, fx, Ground(d + 2f, fx) + 0.9f), new Vector3(0.05f, 1.8f, 3.96f), R(d + 2f));
                }
            }
        }

        /// <summary>Packed two- to four-storey houses right up to the line, signs and vending machines glowing at night.</summary>
        private void BuildDenseCity()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                if (!SideAllowed(side)) continue;
                float edge = side < 0 ? PlacementMask.CorridorLeft - 3.5f : PlacementMask.CorridorRight + 3.5f;
                for (int row = 0; row < 3; row++)
                {
                    float d = plan.Start + Range(0f, 4f);
                    float rowFront = edge + side * row * 13f;
                    while (d < plan.End - 5f)
                    {
                        float length = Range(6f, 11f), depth = Range(8f, 11f);
                        float centre = d + length * 0.5f;
                        float x = rowFront + side * depth * 0.5f;
                        if (RoadAt(centre, side, out float roadX) && side * (x + side * depth * 0.5f - (roadX - side * WorldRoads.HalfWidth)) > -1f) break;
                        if (mask.IsFree(centre, x, Mathf.Min(length, depth) * 0.45f))
                        {
                            float ground = MinGround(centre, x, depth, length);
                            int floors = 2 + random.Next(0, 3);
                            BuildBlock(centre, x, depth, length, floors * 3f, random.Next(0, 4), ground);
                            if (row == 0)
                            {
                                float wallX = x - side * depth * 0.5f;
                                if (Chance(0.35f)) AddNeonSign(centre, wallX, ground + floors * 3f * 0.55f, true);
                                // Air conditioners and balconies on the wall facing the track.
                                WorldMesh units = meshes.For(WorldMaterials.Plain("AcUnit", new Color(0.82f, 0.82f, 0.8f), 0.4f), 1f);
                                for (int f = 1; f < floors; f++)
                                    if (Chance(0.6f)) units.AddBox(P(centre + Range(-length * 0.3f, length * 0.3f), wallX - side * 0.35f, ground + f * 3f + 0.5f),
                                        new Vector3(0.6f, 0.6f, 0.9f), R(centre));
                            }
                        }
                        d += length + Range(0.4f, 2.2f);
                    }
                }
                // Glowing vending machines by the line.
                for (int i = 0; i < 3; i++)
                {
                    float d = Range(plan.Start + 5f, plan.End - 5f);
                    float x = edge - side * 1.4f;
                    if (!StructuresFree(d, x)) continue;
                    meshes.For(WorldMaterials.Glow("Vending" + (i % 3), WorldMaterials.NeonColours[(i + side + 3) % WorldMaterials.NeonColours.Length], 0.6f), 1f)
                        .AddBox(P(d, x, Ground(d, x) + 0.9f), new Vector3(0.8f, 1.8f, 0.9f), R(d));
                    AddPool(P(d, x - side * 0.8f, Ground(d, x)), 2.2f);
                }
                // Wooden utility poles with sagging cables along the fence.
                WorldMesh wood = meshes.For(WorldMaterials.Planks, 2f, true);
                WorldMesh wire = meshes.For(WorldMaterials.Wire, 1f);
                float poleX = edge - side * 1f;
                Vector3 previous = Vector3.zero;
                for (float d = Mathf.Ceil(plan.Start / 30f) * 30f; d < plan.End; d += 30f)
                {
                    if (!StructuresFree(d, poleX)) { previous = Vector3.zero; continue; }
                    float g = Ground(d, poleX);
                    wood.AddCylinder(P(d, poleX, g), 0.13f, 8.5f, 6, R(d), true);
                    wood.AddBox(P(d, poleX, g + 7.9f), new Vector3(1.6f, 0.12f, 0.12f), R(d));
                    Vector3 top = P(d, poleX, g + 7.9f);
                    if (previous != Vector3.zero)
                        for (int k = -1; k <= 1; k += 2)
                        {
                            Vector3 offset = R(d) * Vector3.right * (k * 0.7f);
                            Vector3 mid = (previous + top) * 0.5f + offset + Vector3.down * 0.8f;
                            AddBeam(wire, previous + offset, mid, 0.03f);
                            AddBeam(wire, mid, top + offset, 0.03f);
                        }
                    previous = top;
                }
            }
        }

        /// <summary>Tall blocks along the streets with shops, neon signs and roof billboards.</summary>
        private void BuildMetropolis()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                if (!SideAllowed(side)) continue;
                float front = RoadAt(plan.Start + 60f, side, out float roadX) ? Mathf.Abs(roadX - WorldTerrain.CorridorCentre) + WorldRoads.HalfWidth + 4f : 16f;
                for (int row = 0; row < 3; row++)
                {
                    float d = plan.Start + Range(0f, 6f);
                    float rowFront = front + row * Range(26f, 34f);
                    while (d < plan.End - 10f)
                    {
                        float length = Mathf.Min(Range(18f, 40f), plan.End - 2f - d);
                        if (length < 12f) break;
                        float depth = Range(12f, 18f);
                        float centre = d + length * 0.5f;
                        float x = WorldTerrain.CorridorCentre + side * (rowFront + depth * 0.5f);
                        if (mask.IsFree(centre, x, Mathf.Min(length, depth) * 0.45f))
                        {
                            float ground = MinGround(centre, x, depth, length);
                            int floors = row == 0 ? 6 + random.Next(0, 10) : 9 + random.Next(0, 14);
                            BuildBlock(centre, x, depth, length, floors * 3f, random.Next(0, 4), ground);
                            if (row == 0) BuildShopFront(centre, x - side * depth * 0.5f, length, ground, side);
                            if (Chance(0.3f))
                            {
                                // Rooftop billboard.
                                float top = ground + floors * 3f;
                                Quaternion facing = FacingTrack(centre, x);
                                int colour = random.Next(0, WorldMaterials.NeonColours.Length);
                                meshes.For(WorldMaterials.Steel, 1f).AddBox(P(centre, x, top + 1.5f), new Vector3(0.3f, 3f, 0.3f), facing);
                                meshes.For(WorldMaterials.Neon(colour), 1f).AddBox(P(centre, x, top + 4.2f), new Vector3(9f, 2.6f, 0.3f), facing);
                                AddText(ShopNames[random.Next(0, ShopNames.Length)], P(centre, x, top + 4.2f) + facing * Vector3.back * 0.2f, facing,
                                    new Color(0.05f, 0.05f, 0.08f), 1.4f);
                            }
                        }
                        d += length + Range(6f, 14f);
                    }
                }
            }
        }

        // ---- Stations --------------------------------------------------------------------------

        public static string StationName(WorldChunkPlan station)
        {
            CabStationDefinition definition = WorldPlanner.StationDefinition(station);
            return definition != null ? definition.DisplayName : "Станция";
        }

        private void BuildStation()
        {
            float centre = plan.Start + WorldPlanner.PlatformCentre;
            bool terminal = plan.Station == StationStyle.Terminal;
            float half = terminal ? 45f : WorldPlanner.PlatformLength * 0.5f;
            Quaternion r = R(centre);
            float platformX = PlatformEdge - PlatformWidth * 0.5f;
            Block(centre - half - 8f, centre + half + 8f, terminal ? -45f : -34f, PlatformEdge + 0.2f);

            WorldMesh concrete = meshes.For(WorldMaterials.Concrete, 2f, true);
            BuildPlatform(centre, platformX, half, concrete);
            if (terminal)
            {
                // An island platform beyond the second track.
                float islandX = SecondTrackOffset + 1.95f + PlatformWidth * 0.5f;
                Block(centre - half - 4f, centre + half + 4f, islandX - PlatformWidth * 0.5f, islandX + PlatformWidth * 0.5f + 6f);
                BuildPlatform(centre, islandX, half, concrete);
            }

            switch (plan.Station)
            {
                case StationStyle.Halt: BuildHaltShelter(centre, platformX); break;
                case StationStyle.Town: BuildTownStation(centre, platformX); break;
                case StationStyle.Terminal: BuildTerminal(centre, platformX, half); break;
                case StationStyle.Gnome: BuildHaltShelter(centre, platformX); break;
                default: BuildVillageStation(centre, platformX); break;
            }

            // Platform lamps: many at a terminal, a few at a halt; each with its pool of light.
            float spacing = plan.Station == StationStyle.Halt ? 17f : terminal ? 9f : 9f;
            if (!terminal)
                for (float d = centre - half + 3f; d <= centre + half - 2f; d += spacing)
                    BuildStreetLamp(d, platformX - 1.9f, PlatformHeight, 1f, 4.5f);
            // The station square behind the platform is lit as well.
            if (plan.Station != StationStyle.Gnome)
                for (int i = -1; i <= 1; i++)
                {
                    float lampX = platformX - PlatformWidth * 0.5f - (terminal ? 28f : 18f);
                    float lampD = centre + i * 14f;
                    if (mask.IsFree(lampD, lampX, 0.5f)) BuildStreetLamp(lampD, lampX, Ground(lampD, lampX), 1f, 5f);
                }
            BuildNameBoards(centre, platformX, terminal);
            BuildPassengers(centre, terminal);
            BuildAttendant(centre);
            BuildStationEvent(centre);
        }

        private void BuildPlatform(float centre, float x, float half, WorldMesh concrete)
        {
            Quaternion r = R(centre);
            float edge = x > 0f ? x - PlatformWidth * 0.5f : x + PlatformWidth * 0.5f;
            float inward = x > 0f ? 1f : -1f;
            concrete.AddBox(P(centre, x, PlatformHeight * 0.5f - 0.3f), new Vector3(PlatformWidth, PlatformHeight + 0.6f, half * 2f), r);
            meshes.For(WorldMaterials.SafetyLine, 1f).AddBox(P(centre, edge + inward * 0.35f, PlatformHeight + 0.004f), new Vector3(0.12f, 0.02f, half * 2f - 0.4f), r);
            meshes.For(WorldMaterials.DarkConcrete, 1f).AddBox(P(centre, edge + inward * 0.06f, PlatformHeight - 0.1f), new Vector3(0.14f, 0.22f, half * 2f), r);
            foreach (float end in new[] { -1f, 1f })
            {
                float d = centre + end * (half + 3f);
                Vector3 normal = R(d) * new Vector3(0f, 1f, -end * 0.35f).normalized;
                concrete.AddQuad(P(centre + end * half, x - 2.2f, PlatformHeight), P(centre + end * half, x + 2.2f, PlatformHeight),
                    P(centre + end * (half + 6f), x + 2.2f, Ground(d, x)), P(centre + end * (half + 6f), x - 2.2f, Ground(d, x)), normal);
            }
        }

        private void BuildHaltShelter(float centre, float platformX)
        {
            Quaternion r = R(centre);
            WorldMesh steel = meshes.For(WorldMaterials.Steel, 1f, true);
            foreach (float s in new[] { -3f, 3f })
                steel.AddBox(P(centre + s, platformX - 1.8f, PlatformHeight + 1.25f), new Vector3(0.12f, 2.5f, 0.12f), r);
            meshes.For(WorldMaterials.ShedMetal(3), 2f, true).AddBox(P(centre, platformX - 1.2f, PlatformHeight + 2.6f), new Vector3(2.6f, 0.12f, 7f), r * Quaternion.Euler(0f, 0f, -8f), true);
            meshes.For(WorldMaterials.Plain("ShelterGlass", new Color(0.3f, 0.4f, 0.45f), 0.8f), 1f).AddBox(P(centre, platformX - 2.3f, PlatformHeight + 1.3f), new Vector3(0.05f, 2.2f, 6f), r);
            meshes.For(WorldMaterials.Planks, 1f).AddBox(P(centre, platformX - 1.9f, PlatformHeight + 0.45f), new Vector3(0.45f, 0.08f, 3f), r);
        }

        private void BuildVillageStation(float centre, float platformX)
        {
            Quaternion r = R(centre);
            WorldMesh steel = meshes.For(WorldMaterials.Steel, 1f, true);
            for (float d = centre - 14f; d <= centre + 14.1f; d += 7f)
                steel.AddBox(P(d, platformX - 1f, PlatformHeight + 1.6f), new Vector3(0.16f, 3.2f, 0.16f), r);
            meshes.For(WorldMaterials.ShedMetal(2), 2f, true).AddBox(P(centre, platformX - 0.6f, PlatformHeight + 3.3f), new Vector3(4.6f, 0.18f, 32f), r * Quaternion.Euler(0f, 0f, -6f), true);
            float buildingX = platformX - PlatformWidth * 0.5f - 7.5f;
            float ground = Mathf.Min(PlatformHeight - 0.2f, Ground(centre, buildingX));
            int variant = random.Next(0, 6);
            BuildHouse(centre + 4f, buildingX, 9f, 20f, 4.4f, variant % 2 == 0 ? WorldMaterials.Brick : WorldMaterials.Plaster(variant), WorldMaterials.Roof(variant), ground, 4, true);
            BuildBenches(centre, platformX);
        }

        private void BuildTownStation(float centre, float platformX)
        {
            Quaternion r = R(centre);
            WorldMesh steel = meshes.For(WorldMaterials.Steel, 1f, true);
            for (float d = centre - 22f; d <= centre + 22.1f; d += 5.5f)
            {
                steel.AddBox(P(d, platformX, PlatformHeight + 1.7f), new Vector3(0.18f, 3.4f, 0.18f), r);
                steel.AddBox(P(d, platformX, PlatformHeight + 3.4f), new Vector3(PlatformWidth - 0.4f, 0.14f, 0.14f), r);
            }
            meshes.For(WorldMaterials.ShedMetal(0), 2f, true).AddBox(P(centre, platformX, PlatformHeight + 3.55f), new Vector3(PlatformWidth + 0.6f, 0.16f, 46f), r, true);
            // Two-storey brick station house with a clock.
            float buildingX = platformX - PlatformWidth * 0.5f - 9f;
            float ground = Mathf.Min(PlatformHeight - 0.2f, Ground(centre, buildingX));
            BuildHouse(centre, buildingX, 12f, 30f, 7.5f, WorldMaterials.Brick, WorldMaterials.Roof(3), ground, 6, true);
            AddClock(P(centre, buildingX + 6.1f, ground + 6.2f), r * Quaternion.Euler(0f, -90f, 0f));
            // Footbridge over both tracks.
            float bridgeD = centre - 30f;
            float leftX = platformX - 1f, rightX = SecondTrackOffset + 5.5f;
            float deck = 8.7f;
            WorldMesh bridge = meshes.For(WorldMaterials.Plain("FootbridgeSteel", new Color(0.36f, 0.42f, 0.48f), 0.4f, 0.5f), 1f, true);
            bridge.AddBox(P(bridgeD, (leftX + rightX) * 0.5f, deck), new Vector3(rightX - leftX + 2f, 0.3f, 2.6f), R(bridgeD));
            foreach (float s in new[] { -1f, 1f })
                bridge.AddBox(P(bridgeD + s * 1.25f, (leftX + rightX) * 0.5f, deck + 0.6f), new Vector3(rightX - leftX + 2f, 1.1f, 0.08f), R(bridgeD));
            foreach (float x in new[] { leftX, rightX })
            {
                float g = x < 0f ? PlatformHeight : Ground(bridgeD, x);
                bridge.AddBox(P(bridgeD, x, (g + deck) * 0.5f), new Vector3(2.4f, deck - g, 2.8f), R(bridgeD));
            }
            Block(bridgeD - 3f, bridgeD + 3f, rightX - 2f, rightX + 2f);
            BuildBenches(centre, platformX);
        }

        /// <summary>City terminal: an arched train shed over both tracks and platforms, and a grand station hall.</summary>
        private void BuildTerminal(float centre, float platformX, float half)
        {
            float left = platformX - PlatformWidth * 0.5f - 1.5f;
            float right = SecondTrackOffset + 1.95f + PlatformWidth + 1.5f;
            float mid = (left + right) * 0.5f, span = (right - left) * 0.5f;
            const float springing = 6.5f, rise = 9f;
            WorldMesh ribs = meshes.For(WorldMaterials.Plain("ShedIron", new Color(0.24f, 0.30f, 0.32f), 0.5f, 0.6f), 1f, true);
            WorldMesh roof = meshes.For(WorldMaterials.ShedMetal(2), 2f, true);
            WorldMesh glass = meshes.For(WorldMaterials.Plain("ShedGlass", new Color(0.55f, 0.66f, 0.72f), 0.9f, 0.1f), 2f);
            const int segments = 10;
            float first = centre - half, last = centre + half;
            for (float d = first; d <= last + 0.01f; d += 9f)
            {
                Vector3 previous = P(d, left, springing);
                foreach (float x in new[] { left, right })
                    ribs.AddBox(P(d, x, springing * 0.5f), new Vector3(0.45f, springing, 0.45f), R(d));
                for (int i = 1; i <= segments; i++)
                {
                    float a = i / (float)segments * Mathf.PI;
                    Vector3 point = P(d, mid - Mathf.Cos(a) * span, springing + Mathf.Sin(a) * rise);
                    AddBeam(ribs, previous, point, 0.35f);
                    previous = point;
                }
                if (d + 9f > last + 0.01f) continue;
                // Roof between this rib and the next: metal lower slopes, glass in the middle.
                for (int i = 0; i < segments; i++)
                {
                    float a0 = i / (float)segments * Mathf.PI, a1 = (i + 1) / (float)segments * Mathf.PI;
                    float x0 = mid - Mathf.Cos(a0) * span, x1 = mid - Mathf.Cos(a1) * span;
                    float y0 = springing + Mathf.Sin(a0) * rise + 0.2f, y1 = springing + Mathf.Sin(a1) * rise + 0.2f;
                    Vector3 n = R(d) * new Vector3(-(y1 - y0), x1 - x0, 0f).normalized;
                    (i >= 3 && i <= 6 ? glass : roof).AddQuad(P(d, x0, y0), P(d, x1, y1), P(d + 9f, x1, y1), P(d + 9f, x0, y0), n);
                }
                // Hanging lamps over both platforms.
                foreach (float x in new[] { platformX, SecondTrackOffset + 1.95f + PlatformWidth * 0.5f })
                {
                    Vector3 lamp = P(d + 4.5f, x, springing - 0.3f);
                    meshes.For(WorldMaterials.LampGlow, 1f).AddBox(lamp, new Vector3(0.5f, 0.25f, 0.5f), R(d));
                    ribs.AddBox(lamp + Vector3.up * 1.2f, new Vector3(0.04f, 2.2f, 0.04f), R(d));
                    AddPool(P(d + 4.5f, x, PlatformHeight), 5f);
                    chunk.Lamps.Add(CreateLight("ScenicNightLight", lamp + Vector3.down * 0.4f, LightType.Point, new Color(1f, 0.86f, 0.62f), 1.6f, 12f));
                }
            }
            Block(first - 2f, last + 2f, left - 2f, right + 2f);
            // The station hall behind the left platform, with its name across the top.
            float hallX = left - 13f;
            float ground = Mathf.Min(PlatformHeight - 0.2f, Ground(centre, hallX));
            BuildBlock(centre, hallX, 22f, 70f, 18f, 3, ground);
            Quaternion facing = R(centre) * Quaternion.Euler(0f, -90f, 0f);
            AddText(StationName(plan).ToUpperInvariant(), P(centre, hallX + 11.3f, ground + 15f), facing, new Color(1f, 0.92f, 0.6f), 2.2f);
            AddClock(P(centre + 26f, hallX + 11.2f, ground + 12f), facing);
            BuildBenches(centre, platformX);
        }

        private void AddClock(Vector3 position, Quaternion facing)
        {
            meshes.For(WorldMaterials.Glow("ClockFace", new Color(0.95f, 0.93f, 0.85f), 0.5f), 1f).AddCylinder(position, 1f, 0.15f, 20, facing * Quaternion.Euler(90f, 0f, 0f));
            WorldMesh hands = meshes.For(WorldMaterials.Plain("ClockHands", new Color(0.05f, 0.05f, 0.05f), 0.3f), 1f);
            Vector3 front = facing * Vector3.back * 0.2f;
            hands.AddBox(position + front + facing * new Vector3(0f, 0.3f, 0f), new Vector3(0.08f, 0.6f, 0.03f), facing);
            hands.AddBox(position + front + facing * new Vector3(0.22f, -0.1f, 0f), new Vector3(0.45f, 0.07f, 0.03f), facing * Quaternion.Euler(0f, 0f, -25f));
        }

        private void BuildBenches(float centre, float platformX)
        {
            Quaternion r = R(centre);
            WorldMesh wood = meshes.For(WorldMaterials.Planks, 1f);
            WorldMesh steel = meshes.For(WorldMaterials.Steel, 1f, true);
            for (int i = -1; i <= 1; i++)
            {
                float d = centre + i * 8f + 4f;
                wood.AddBox(P(d, platformX - 1.4f, PlatformHeight + 0.45f), new Vector3(0.5f, 0.08f, 1.8f), r);
                wood.AddBox(P(d, platformX - 1.65f, PlatformHeight + 0.75f), new Vector3(0.06f, 0.5f, 1.8f), r);
                steel.AddBox(P(d, platformX - 1.4f, PlatformHeight + 0.2f), new Vector3(0.4f, 0.4f, 0.08f), r);
            }
        }

        /// <summary>
        /// Name boards stand along the back of the platform, parallel to the track and facing it,
        /// as at real stations: the driver reads them sideways while pulling in, and they never
        /// hang across the view.
        /// </summary>
        private void BuildNameBoards(float centre, float platformX, bool terminal)
        {
            string name = StationName(plan);
            WorldMesh steel = meshes.For(WorldMaterials.Steel, 1f, true);
            float backX = platformX - PlatformWidth * 0.5f + 0.35f;
            float[] places = terminal ? new[] { centre - 32f, centre - 6f, centre + 20f } : new[] { centre - 17f, centre + 9f };
            foreach (float d in places)
            {
                Quaternion facing = R(d) * Quaternion.Euler(0f, -90f, 0f);
                Vector3 position = P(d, backX, PlatformHeight + 2.6f);
                foreach (float s2 in new[] { -1.6f, 1.6f })
                    steel.AddBox(P(d + s2, backX - 0.05f, PlatformHeight + 1.35f), new Vector3(0.09f, 2.7f, 0.09f), R(d));
                GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                board.name = "StationSignBoard";
                RemoveCollider(board);
                board.transform.SetParent(chunk.Root.transform, false);
                board.transform.localPosition = position;
                board.transform.localRotation = facing;
                board.GetComponent<Renderer>().sharedMaterial = WorldMaterials.SignBoard;
                TextMesh front = CabWorld3DPrototypeFactory.CreateStationSignText(chunk.Root.transform, "StationName_" + name,
                    position + facing * Vector3.back * 0.06f, facing, name);
                front.transform.localRotation = facing;
                board.AddComponent<SignBoardFitter>().Configure(front);
                chunk.Signs.Add(front);
                chunk.SignBoards.Add(board.transform);
                // A strip light over the board so the name reads at night.
                meshes.For(WorldMaterials.LampGlow, 1f).AddBox(position + Vector3.up * 0.72f + facing * Vector3.back * 0.25f, new Vector3(2.4f, 0.06f, 0.1f), facing);
            }
        }

        /// <summary>A person of the new kind, placed in a parent's frame; their mesh is freed with the chunk.</summary>
        private PersonAnimator SpawnPerson(Transform parent, string name, Vector3 local, Quaternion facing, System.Random rng,
            bool child = false, Color? top = null, bool gnome = false)
        {
            PersonLook look = PersonLook.Random(rng, child);
            if (top.HasValue) look.Top = top.Value;
            if (gnome)
            {
                look.Height = 1.0f + (float)rng.NextDouble() * 0.15f;
                look.Build = 0.9f;
                look.Hair = 5;
                look.Carry = 0;
            }
            PersonAnimator person = PersonFactory.Create(parent, name, local, look);
            person.transform.localRotation = facing;
            person.SetHome(local, facing);
            SkinnedMeshRenderer skin = person.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin != null && chunk != null) chunk.Meshes.Add(skin.sharedMesh);
            if (gnome) Gnomify(person);
            return person;
        }

        private void BuildPassengers(float centre, bool terminal)
        {
            Quaternion r = R(centre);
            Transform frame = new GameObject("PlatformFrame").transform;
            frame.SetParent(chunk.Root.transform, false);
            frame.localPosition = P(centre, 0f, PlatformHeight);
            frame.localRotation = r;
            chunk.PlatformFrame = frame;
            bool gnomes = plan.Station == StationStyle.Gnome;
            float half = terminal ? 45f : WorldPlanner.PlatformLength * 0.5f;
            StationCrowd crowd = frame.gameObject.AddComponent<StationCrowd>();
            crowd.Configure(plan.Seed, new Vector3(PlatformEdge - 2.6f, 0f, -half - 3f), PlatformEdge, gnomes);
            WorldChunk owner = chunk;
            crowd.SpawnArrival = rng =>
            {
                PersonAnimator person = SpawnPerson(frame, "Arriving", Vector3.zero, Quaternion.identity, rng, rng.NextDouble() < 0.12, null, gnomes);
                owner.Meshes.Add(person.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh);
                return person;
            };
            chunk.Crowd = crowd;
            int count = plan.Station == StationStyle.Halt ? 3 + random.Next(0, 4)
                : plan.Station == StationStyle.Village ? 6 + random.Next(0, 7)
                : plan.Station == StationStyle.Town ? 10 + random.Next(0, 7)
                : gnomes ? 5 + random.Next(0, 6) : 16 + random.Next(0, 9);
            System.Random rng2 = new System.Random(plan.Seed * 13 + 1);
            for (int i = 0; i < count; i++)
            {
                bool seated = i % 5 == 3;
                // Small groups stand together; some sit on the benches.
                float z = seated ? (random.Next(-1, 2) * 8f + 4f) + Range(-0.5f, 0.5f) : Range(-half + 4f, half - 6f);
                Vector3 platform = seated ? new Vector3(PlatformEdge - PlatformWidth * 0.5f - 1.4f + 0.1f, 0f, z)
                    : new Vector3(Range(PlatformEdge - 3.6f, PlatformEdge - 0.9f), 0f, z);
                Quaternion facing = seated ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.Euler(0f, Range(20f, 160f), 0f);
                PersonAnimator person = SpawnPerson(frame, "Passenger_" + plan.Index + "_" + i, platform, facing, rng2, i % 7 == 6, null, gnomes);
                person.StrollMin = new Vector2(PlatformEdge - 3.8f, platform.z - 5f);
                person.StrollMax = new Vector2(PlatformEdge - 0.8f, platform.z + 5f);
                if (seated) person.Sit(true);
                crowd.Add(person, seated);
            }
            if (!terminal) return;
            // People also wait on the island platform; they do not board this train.
            for (int i = 0; i < 8; i++)
            {
                Vector3 spot = new Vector3(SecondTrackOffset + 1.95f + Range(1.2f, 3.8f), 0f, Range(-30f, 30f));
                PersonAnimator person = SpawnPerson(frame, "Waiting_" + i, spot, Quaternion.Euler(0f, Range(0f, 360f), 0f), rng2);
                person.StrollMin = new Vector2(SecondTrackOffset + 2.8f, spot.z - 6f);
                person.StrollMax = new Vector2(SecondTrackOffset + 6.2f, spot.z + 6f);
            }
        }

        /// <summary>
        /// The station attendant stands at the platform end where the locomotive stops. By day
        /// with a yellow flag (and a wave for the driver), at night with a lantern whose beam
        /// reaches down the platform. Between trains they stretch, stroll a few steps and look around.
        /// </summary>
        private void BuildAttendant(float centre)
        {
            Transform frame = chunk.PlatformFrame;
            if (frame == null) return;
            bool gnome = plan.Station == StationStyle.Gnome;
            Vector3 spot = new Vector3(PlatformEdge - 1.1f, 0f, WorldPlanner.StopOffsetPastPlatformCentre + 3.5f);
            Color vest = new Color(0.95f, 0.42f, 0.05f);
            System.Random rng = new System.Random(plan.Seed * 17 + 3);
            Quaternion facing = Quaternion.Euler(0f, 180f, 0f);

            PersonAnimator night = SpawnPerson(frame, "StationAttendant", spot, facing, rng, false, vest, gnome);
            Attendant(night, spot);
            Transform hand = night.BoneOf(PersonFactory.Bone.HandL);
            Transform lantern = new GameObject("Lantern").transform;
            lantern.SetParent(hand, false);
            lantern.localPosition = new Vector3(0f, -0.14f, 0.03f);
            GameObject glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glass.name = "LanternGlass";
            RemoveCollider(glass);
            glass.transform.SetParent(lantern, false);
            glass.transform.localScale = new Vector3(0.14f, 0.2f, 0.14f);
            glass.GetComponent<Renderer>().sharedMaterial = WorldMaterials.Glow("Lantern", new Color(1f, 0.78f, 0.35f), 0.4f);
            GameObject lightObject = new GameObject("LanternLight", typeof(Light));
            lightObject.transform.SetParent(lantern, false);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.74f, 0.4f);
            light.intensity = 1.4f;
            light.range = 7f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            chunk.Lamps.Add(light);
            // The lantern throws a beam down the platform toward the arriving train.
            GameObject beamObject = new GameObject("LanternBeam", typeof(Light), typeof(MeshFilter), typeof(MeshRenderer));
            beamObject.transform.SetParent(lantern, false);
            beamObject.transform.rotation = frame.rotation * Quaternion.Euler(9f, 180f, 0f);
            beamObject.AddComponent<KeepWorldRotation>().Configure(frame, Quaternion.Euler(9f, 180f, 0f));
            Light beam = beamObject.GetComponent<Light>();
            beam.type = LightType.Spot;
            beam.color = new Color(1f, 0.8f, 0.5f);
            beam.intensity = 14f;
            beam.range = 32f;
            beam.spotAngle = 32f;
            beam.innerSpotAngle = 10f;
            beam.shadows = LightShadows.None;
            beam.enabled = false;
            chunk.Lamps.Add(beam);
            Mesh cone = BeamCone(22f, 12f);
            chunk.Meshes.Add(cone);
            beamObject.GetComponent<MeshFilter>().sharedMesh = cone;
            beamObject.GetComponent<MeshRenderer>().sharedMaterial = LanternBeamMaterial;
            beamObject.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            night.gameObject.SetActive(false);
            chunk.NightOnly.Add(night.gameObject);

            PersonAnimator day = SpawnPerson(frame, "StationAttendantDay", spot, facing, rng, false, vest, gnome);
            Attendant(day, spot);
            Transform flagHand = day.BoneOf(PersonFactory.Bone.HandR);
            GameObject stick = GameObject.CreatePrimitive(PrimitiveType.Cube);
            RemoveCollider(stick);
            stick.name = "FlagStick";
            stick.transform.SetParent(flagHand, false);
            stick.transform.localPosition = new Vector3(0f, -0.2f, 0.02f);
            stick.transform.localScale = new Vector3(0.025f, 0.5f, 0.025f);
            stick.GetComponent<Renderer>().sharedMaterial = WorldMaterials.Plain("FlagStick", new Color(0.3f, 0.22f, 0.12f), 0.3f);
            GameObject flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            RemoveCollider(flag);
            flag.name = "Flag";
            flag.transform.SetParent(flagHand, false);
            flag.transform.localPosition = new Vector3(0f, -0.38f, 0.18f);
            flag.transform.localScale = new Vector3(0.015f, 0.22f, 0.32f);
            flag.GetComponent<Renderer>().sharedMaterial = WorldMaterials.Plain("SignalFlag", new Color(0.98f, 0.82f, 0.05f), 0.3f);
            chunk.DayOnly.Add(day.gameObject);
            chunk.Attendants.Add(day);
            chunk.Attendants.Add(night);
        }

        private void Attendant(PersonAnimator person, Vector3 spot)
        {
            // A short beat up and down the platform end.
            person.StrollMin = new Vector2(PlatformEdge - 2.4f, spot.z - 3f);
            person.StrollMax = new Vector2(PlatformEdge - 0.9f, spot.z + 1.5f);
            person.gameObject.AddComponent<Cab3DInteractiveObject>().Configure("station passenger worker", CabInteractionReaction.Wave);
        }

        private static Material lanternBeamMaterial;
        private static Material LanternBeamMaterial
        {
            get
            {
                if (lanternBeamMaterial != null) return lanternBeamMaterial;
                lanternBeamMaterial = new Material(CabShaders.Additive) { name = "LanternBeam" };
                Color tint = new Color(0.45f, 0.36f, 0.22f, 0.5f) * 0.25f;
                if (lanternBeamMaterial.HasProperty("_TintColor")) lanternBeamMaterial.SetColor("_TintColor", tint);
                lanternBeamMaterial.color = tint;
                return lanternBeamMaterial;
            }
        }

        /// <summary>Open cone along +Z with alpha fading toward the far end (a visible beam of light).</summary>
        private static Mesh BeamCone(float length, float halfAngle)
        {
            const int sides = 12, rings = 5;
            Vector3[] vertices = new Vector3[sides * rings];
            Color[] colours = new Color[vertices.Length];
            List<int> triangles = new List<int>();
            float tan = Mathf.Tan(halfAngle * Mathf.Deg2Rad);
            for (int r = 0; r < rings; r++)
            {
                float t = r / (float)(rings - 1);
                float z = 0.1f + t * length;
                for (int k = 0; k < sides; k++)
                {
                    float a = k / (float)sides * Mathf.PI * 2f;
                    vertices[r * sides + k] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (0.08f + z * tan) + Vector3.forward * z;
                    colours[r * sides + k] = new Color(1f, 1f, 1f, Mathf.Pow(1f - t, 1.5f));
                }
            }
            for (int r = 0; r < rings - 1; r++)
                for (int k = 0; k < sides; k++)
                {
                    int a = r * sides + k, b = r * sides + (k + 1) % sides, c = a + sides, e = b + sides;
                    triangles.AddRange(new[] { a, c, b, b, c, e, a, b, c, b, e, c });
                }
            Mesh mesh = new Mesh { name = "LanternBeam" };
            mesh.vertices = vertices;
            mesh.colors = colours;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private void TrackMaterials(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                if (renderer.sharedMaterial != null && !renderer.sharedMaterial.name.StartsWith("WorldGlow") && !chunk.Materials.Contains(renderer.sharedMaterial))
                    chunk.Materials.Add(renderer.sharedMaterial);
        }
    }
}
