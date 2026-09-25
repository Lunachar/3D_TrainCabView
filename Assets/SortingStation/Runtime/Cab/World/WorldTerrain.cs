using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// Ground shape and ground layers of the endless world as continuous functions of the route
    /// distance d and the lateral offset x. Every chunk samples the same functions, and each
    /// kind of scenery fades into the next over ±<see cref="BlendHalfWidth"/> metres around a
    /// chunk border, so the land flows from meadow into forest or up into the mountains
    /// without seams.
    /// </summary>
    public sealed class WorldTerrain
    {
        public const float BlendHalfWidth = 30f;
        /// <summary>Middle of the two-track formation (the second track is 4.8 m to the right).</summary>
        public const float CorridorCentre = 2.4f;
        public const float CorridorHalfWidth = 7.5f;
        public const float GroundOffset = -0.06f;
        public const float WaterLevel = -5.2f;
        public const float BridgeHalfSpan = 20f;
        public const float PortalHalfWidth = 11f;
        public const float UvWrap = 6000f;

        /// <summary>Lateral offsets of the terrain columns from the corridor centre: dense by the track, wide far away.</summary>
        public static readonly float[] Columns = BuildColumns();

        private struct Profile
        {
            public float Rugged;
            public float FarRise;
            public float Mountain;
            public float Soil;
            public float Crop;
            public float Forest;
            public float Paving;

            public static Profile Lerp(Profile a, Profile b, float t)
            {
                return new Profile
                {
                    Rugged = Mathf.Lerp(a.Rugged, b.Rugged, t),
                    FarRise = Mathf.Lerp(a.FarRise, b.FarRise, t),
                    Mountain = Mathf.Lerp(a.Mountain, b.Mountain, t),
                    Soil = Mathf.Lerp(a.Soil, b.Soil, t),
                    Crop = Mathf.Lerp(a.Crop, b.Crop, t),
                    Forest = Mathf.Lerp(a.Forest, b.Forest, t),
                    Paving = Mathf.Lerp(a.Paving, b.Paving, t)
                };
            }
        }

        private readonly WorldPlanner planner;
        private readonly float noiseOffset;

        public WorldTerrain(WorldPlanner worldPlanner, int seed)
        {
            planner = worldPlanner;
            noiseOffset = (seed % 997) * 13.7f;
        }

        public WorldPlanner Planner => planner;

        private static Profile ProfileOf(WorldChunkKind kind)
        {
            switch (kind)
            {
                case WorldChunkKind.Field: return new Profile { Rugged = 3f, FarRise = 14f, Soil = 1f, Crop = 1f };
                case WorldChunkKind.Forest: return new Profile { Rugged = 9f, FarRise = 38f, Forest = 1f };
                case WorldChunkKind.Village: return new Profile { Rugged = 3.5f, FarRise = 20f, Soil = 0.35f, Crop = 0.3f, Paving = 0.12f };
                case WorldChunkKind.Town: return new Profile { Rugged = 1.2f, FarRise = 12f, Paving = 0.55f };
                case WorldChunkKind.Industrial: return new Profile { Rugged = 1.2f, FarRise = 10f, Paving = 0.75f, Soil = 0.2f };
                case WorldChunkKind.Foothills: return new Profile { Rugged = 16f, FarRise = 95f, Mountain = 0.45f, Forest = 0.45f };
                case WorldChunkKind.Tunnel: return new Profile { Rugged = 22f, FarRise = 150f, Mountain = 1f, Forest = 0.3f };
                case WorldChunkKind.City: return new Profile { Rugged = 0.5f, FarRise = 8f, Paving = 0.95f };
                case WorldChunkKind.Water: return new Profile { Rugged = 4f, FarRise = 22f, Crop = 0.1f };
                default: return new Profile { Rugged = 7f, FarRise = 26f, Crop = 0.12f };
            }
        }

        /// <summary>The chunk kinds that meet around d and how far the later one has taken over (0..1).</summary>
        public void Blend(float d, out WorldChunkPlan a, out WorldChunkPlan b, out float t)
        {
            WorldChunkPlan here = planner.At(d);
            float intoChunk = d - here.Start;
            if (intoChunk < BlendHalfWidth && here.Index > 0)
            {
                a = planner.Get(here.Index - 1);
                b = here;
                t = Mathf.SmoothStep(0f, 1f, 0.5f + 0.5f * intoChunk / BlendHalfWidth);
                return;
            }
            float toEnd = here.End - d;
            if (toEnd < BlendHalfWidth)
            {
                a = here;
                b = planner.Get(here.Index + 1);
                t = Mathf.SmoothStep(0f, 1f, 0.5f - 0.5f * toEnd / BlendHalfWidth);
                return;
            }
            a = b = here;
            t = 0f;
        }

        /// <summary>How strongly a kind of scenery is present at d (0..1), averaged over both sides of the line.</summary>
        public float Weight(float d, WorldChunkKind kind) => (Weight(d, CorridorCentre - 50f, kind) + Weight(d, CorridorCentre + 50f, kind)) * 0.5f;

        /// <summary>How strongly a kind of scenery is present at (d, x), including the blend zones and the change of side.</summary>
        public float Weight(float d, float x, WorldChunkKind kind)
        {
            Blend(d, out WorldChunkPlan a, out WorldChunkPlan b, out float t);
            float right = SideBlend(x);
            float W(WorldChunkPlan plan) => (plan.Kind == kind ? 1f - right : 0f) + (plan.RightKind == kind ? right : 0f);
            return W(a) * (1f - t) + W(b) * t;
        }

        /// <summary>0 left of the line, 1 right of it; the two sides meet across the track corridor.</summary>
        public static float SideBlend(float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-8f, 8f, x - CorridorCentre));

        private Profile ProfileAt(float d, float x)
        {
            Blend(d, out WorldChunkPlan a, out WorldChunkPlan b, out float t);
            float right = SideBlend(x);
            Profile P(WorldChunkPlan plan) => Profile.Lerp(ProfileOf(plan.Kind), ProfileOf(plan.RightKind), right);
            return Profile.Lerp(P(a), P(b), t);
        }

        /// <summary>0..1 over the last 90 m before a portal: the ground closes into a rock cutting.</summary>
        public float TunnelApproach(float d)
        {
            float best = 0f;
            for (int offset = -1; offset <= 1; offset++)
            {
                WorldChunkPlan plan = planner.At(d + offset * WorldPlanner.ChunkLength);
                if (plan.TunnelEntrance)
                {
                    float portal = plan.Start + WorldPlanner.PortalInset;
                    float before = portal - d;
                    if (before >= -1f && before < 90f) best = Mathf.Max(best, 1f - Mathf.Clamp01(before / 90f));
                }
                if (plan.TunnelExit)
                {
                    float portal = plan.End - WorldPlanner.PortalInset;
                    float after = d - portal;
                    if (after >= -1f && after < 90f) best = Mathf.Max(best, 1f - Mathf.Clamp01(after / 90f));
                }
            }
            return Mathf.SmoothStep(0f, 1f, best);
        }

        /// <summary>River centre line (route distance) at lateral x for a water chunk.</summary>
        public static float RiverCentre(WorldChunkPlan water, float x)
        {
            return water.Start + WorldPlanner.ChunkLength * 0.5f + 16f * Mathf.Sin(x * 0.011f + (water.Seed % 628) * 0.01f) -
                   16f * Mathf.Sin(WorldTerrain.CorridorCentre * 0.011f + (water.Seed % 628) * 0.01f);
        }

        public float RiverDepth(float d, float x)
        {
            float depth = 0f;
            for (int offset = -1; offset <= 1; offset++)
            {
                WorldChunkPlan plan = planner.At(d + offset * WorldPlanner.ChunkLength);
                if (plan.Kind != WorldChunkKind.Water) continue;
                float fromCentre = Mathf.Abs(d - RiverCentre(plan, x));
                float bankDepth = 7.8f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(13f, 46f, fromCentre)));
                // The embankment carries the track right up to the bridge abutments.
                if (Mathf.Abs(x - CorridorCentre) < CorridorHalfWidth + 3f && fromCentre > BridgeHalfSpan) bankDepth = 0f;
                depth = Mathf.Max(depth, bankDepth);
            }
            return depth;
        }

        /// <param name="insideTunnel">Whether the point is treated as over the bore (portal rows are sampled both ways).</param>
        public float Height(float d, float x, bool insideTunnel)
        {
            Profile p = ProfileAt(d, x);
            float xc = x - CorridorCentre;
            float ax = Mathf.Abs(xc);
            float approach = TunnelApproach(d);
            float ramp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CorridorHalfWidth, CorridorHalfWidth + 30f, ax));
            float cutting = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CorridorHalfWidth + 0.5f, CorridorHalfWidth + 7f, ax));
            ramp = Mathf.Lerp(ramp, cutting, approach);

            float n1 = Noise(d * 0.0065f, x * 0.0065f);
            float n2 = Noise(d * 0.0018f + 41.3f, x * 0.0018f - 17.9f);
            float n3 = Noise(d * 0.03f + 5.1f, x * 0.03f + 9.4f);
            float hills = p.Rugged * (n1 * 1.35f - 0.25f + n3 * 0.2f) +
                          p.FarRise * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(60f, 650f, ax)) * (0.55f + 0.9f * n2);
            float mountainRamp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CorridorHalfWidth, CorridorHalfWidth + Mathf.Lerp(70f, 12f, approach), ax));
            float mountain = p.Mountain * (30f + 70f * n2 + 12f * n1) * mountainRamp;
            float height = ramp * Mathf.Max(-1.5f, hills) + mountain;
            if (insideTunnel)
            {
                // Over a cavern hall the mountain has to be much thicker than over a plain bore.
                float cover = Mathf.Lerp(14f + 5f * n1 + 0.3f * ax, 34f + 6f * n1 + 0.25f * ax, HallWeight(d));
                height = Mathf.Max(height, cover);
            }
            height -= RiverDepth(d, x);
            return height + GroundOffset;
        }

        public bool InsideTunnel(float d) => planner.TryGetTunnel(d, out _, out _);

        /// <summary>0 in an ordinary bore, 1 inside a cavern hall; the cave widens over 30 m at each end.</summary>
        public float HallWeight(float d)
        {
            WorldChunkPlan plan = planner.At(d);
            if (!plan.CaveHall) return 0f;
            float inside = Mathf.Min(d - plan.Start, plan.End - d);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 34f, inside));
        }

        /// <summary>Ground layer weights: R soil, G forest floor, B gravel/paving, A crops.</summary>
        public Color Layers(float d, float x)
        {
            Profile p = ProfileAt(d, x);
            float xc = x - CorridorCentre;
            float ax = Mathf.Abs(xc);
            float gravel = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CorridorHalfWidth - 0.8f, CorridorHalfWidth + 1.5f, ax));
            float outside = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CorridorHalfWidth + 4f, CorridorHalfWidth + 12f, ax));

            // Fields come in parcels: some ploughed, some with ripe crops, some left as meadow.
            int parcelD = Mathf.FloorToInt((d + 100000f) / 85f);
            int parcelX = Mathf.FloorToInt((xc + 100000f) / 60f);
            int parcel = WorldPlanner.Hash(parcelD * 31 + 7, parcelX) % 4;
            float soil = p.Soil * (parcel == 2 ? 0.9f : parcel == 3 ? 0.35f : 0f) * outside;
            float crop = p.Crop * (parcel == 1 || parcel == 3 ? 1f : 0f) * outside;
            float forest = p.Forest * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CorridorHalfWidth + 5f, CorridorHalfWidth + 13f, ax));
            float paving = p.Paving * outside * (Noise(d * 0.02f, x * 0.02f) > 0.45f ? 1f : 0.35f);
            float mud = 0.25f * Noise(d * 0.05f + 3f, x * 0.05f) * (1f - outside);
            return new Color(Mathf.Clamp01(soil + mud), Mathf.Clamp01(forest), Mathf.Clamp01(Mathf.Max(gravel, paving)), Mathf.Clamp01(crop));
        }

        private float Noise(float a, float b)
        {
            // Perlin noise loses precision far from the origin; keep its input small.
            float u = Mathf.Repeat(a + noiseOffset, 4096f);
            float v = Mathf.Repeat(b + noiseOffset * 0.37f, 4096f);
            return Mathf.PerlinNoise(u, v) * 0.65f + Mathf.PerlinNoise(u * 2.03f + 11f, v * 2.03f + 7f) * 0.35f;
        }

        private static float[] BuildColumns()
        {
            float[] half = { 1.2f, 3f, 5f, 6.5f, 7.5f, 8.8f, 10.5f, 12.5f, 15f, 18f, 22f, 27f, 33f, 40f, 49f, 60f, 74f, 91f, 112f, 138f, 170f, 210f, 258f, 316f, 385f, 470f, 570f, 690f };
            List<float> columns = new List<float>();
            for (int i = half.Length - 1; i >= 0; i--) columns.Add(-half[i]);
            for (int i = 0; i < half.Length; i++) columns.Add(half[i]);
            return columns.ToArray();
        }

        /// <summary>
        /// Terrain mesh of one chunk, positioned relative to <paramref name="chunkOrigin"/> (the
        /// chunk start in world coordinates minus the floating origin).
        /// </summary>
        public Mesh BuildChunkMesh(TrackPath path, WorldChunkPlan plan, double originX, double originZ, Vector3 chunkOrigin)
        {
            List<float> rows = new List<float>();
            List<int> rowTunnel = new List<int>();
            const float rowStep = 4f;
            // Portal rows are sampled twice, outside and over the bore, for a clean vertical step.
            bool hasEntrance = plan.TunnelEntrance;
            bool hasExit = plan.TunnelExit;
            float entrance = plan.Start + WorldPlanner.PortalInset;
            float exit = plan.End - WorldPlanner.PortalInset;
            for (float d = plan.Start; d <= plan.End + 0.01f; d += rowStep)
            {
                if (hasEntrance && d > entrance && d - rowStep < entrance)
                {
                    rows.Add(entrance); rowTunnel.Add(0);
                    rows.Add(entrance); rowTunnel.Add(1);
                }
                if (hasExit && d > exit && d - rowStep < exit)
                {
                    rows.Add(exit); rowTunnel.Add(1);
                    rows.Add(exit); rowTunnel.Add(0);
                }
                rows.Add(Mathf.Min(d, plan.End));
                rowTunnel.Add(InsideTunnel(Mathf.Min(d, plan.End)) ? 1 : 0);
            }

            int columns = Columns.Length;
            Vector3[] vertices = new Vector3[rows.Count * columns];
            Vector3[] normals = new Vector3[vertices.Length];
            Color[] colors = new Color[vertices.Length];
            Vector2[] uv = new Vector2[vertices.Length];
            Vector2[] uv1 = new Vector2[vertices.Length];
            path.Point(plan.Start, out double baseX, out double baseZ);
            double uvBaseX = System.Math.Round(baseX / UvWrap) * UvWrap;
            double uvBaseZ = System.Math.Round(baseZ / UvWrap) * UvWrap;
            for (int r = 0; r < rows.Count; r++)
            {
                float d = rows[r];
                bool inside = rowTunnel[r] == 1;
                path.Point(d, out double px, out double pz);
                double heading = path.Heading(d);
                float sin = (float)System.Math.Sin(heading);
                float cos = (float)System.Math.Cos(heading);
                for (int c = 0; c < columns; c++)
                {
                    float x = CorridorCentre + Columns[c];
                    float h = Height(d, x, inside);
                    double wx = px + cos * x;
                    double wz = pz - sin * x;
                    int i = r * columns + c;
                    vertices[i] = new Vector3((float)(wx - originX) - chunkOrigin.x, h, (float)(wz - originZ) - chunkOrigin.z);
                    // Normal from the height function itself, so both chunks at a border agree.
                    float step = Mathf.Max(1f, Mathf.Abs(Columns[c]) * 0.04f);
                    float dx = (Height(d, x + step, inside) - Height(d, x - step, inside)) / (2f * step);
                    float dd = (Height(d + 1f, x, inside) - Height(d - 1f, x, inside)) / 2f;
                    Vector3 local = new Vector3(-dx, 1f, -dd).normalized;
                    normals[i] = new Vector3(local.x * cos + local.z * sin, local.y, -local.x * sin + local.z * cos);
                    colors[i] = Layers(d, x);
                    uv[i] = new Vector2((float)(wx - uvBaseX), (float)(wz - uvBaseZ));
                    uv1[i] = new Vector2(h, 0f);
                }
            }

            List<int> triangles = new List<int>(rows.Count * columns * 6);
            for (int r = 0; r < rows.Count - 1; r++)
            {
                bool portalFace = Mathf.Abs(rows[r + 1] - rows[r]) < 0.001f;
                for (int c = 0; c < columns - 1; c++)
                {
                    // The portal headwall fills the step across the bore itself.
                    if (portalFace && Mathf.Abs(Columns[c] + Columns[c + 1]) * 0.5f < PortalHalfWidth) continue;
                    int a = r * columns + c;
                    int b = a + 1;
                    int e = a + columns;
                    int f = e + 1;
                    triangles.Add(a); triangles.Add(e); triangles.Add(b);
                    triangles.Add(b); triangles.Add(e); triangles.Add(f);
                }
            }

            Mesh mesh = new Mesh { name = "Terrain_" + plan.Index };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.uv = uv;
            mesh.uv2 = uv1;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
