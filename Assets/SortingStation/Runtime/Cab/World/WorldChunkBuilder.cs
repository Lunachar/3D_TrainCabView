using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SortingStation
{
    /// <summary>
    /// Builds one chunk of the endless world: terrain, the two-track line with its overhead
    /// wires, the buildings of the chunk's kind of scenery (see WorldStructures.cs) and then
    /// the plants, which only go where the chunk's <see cref="PlacementMask"/> is free. The
    /// build is a coroutine so a new chunk is spread over a few frames.
    /// </summary>
    public sealed partial class WorldChunkBuilder
    {
        public const float SecondTrackOffset = 4.8f;
        /// <summary>Trees closer to the track than this get full meshes (while the chunk is near).</summary>
        public const float NearTreeReach = 26f;
        private const float MaxTreeReach = 460f;

        private readonly TrackPath path;
        private readonly WorldTerrain terrain;
        private readonly WorldPlanner planner;
        private SeasonType season = SeasonType.Summer;
        private float density = 1f;
        private Material grassMaterial;

        // Per-build context.
        private WorldChunkPlan plan;
        private WorldChunk chunk;
        private double originX;
        private double originZ;
        private Vector3 chunkOrigin;
        private PlacementMask mask;
        private WorldMeshSet meshes;
        private System.Random random;

        public WorldChunkBuilder(TrackPath trackPath, WorldTerrain worldTerrain)
        {
            path = trackPath;
            terrain = worldTerrain;
            planner = trackPath.Planner;
        }

        public void Configure(SeasonType currentSeason, float sceneryDensity)
        {
            season = currentSeason;
            density = Mathf.Clamp(sceneryDensity, 0.3f, 1.3f);
            grassMaterial = season == SeasonType.Winter ? null : CabWorldExtras.GrassMaterial(season);
        }

        /// <summary>Builds a chunk (the coroutine yields between stages).</summary>
        public IEnumerator Build(WorldChunkPlan chunkPlan, double floatingOriginX, double floatingOriginZ, Transform parent, WorldChunk target)
        {
            plan = chunkPlan;
            chunk = target;
            chunk.Plan = plan;
            originX = floatingOriginX;
            originZ = floatingOriginZ;
            chunkOrigin = path.Local(plan.Start, 0f, 0f, originX, originZ);
            chunkOrigin.y = 0f;
            random = new System.Random(plan.Seed);
            mask = new PlacementMask(plan.Start, plan.End);
            structureZones.Clear();
            meshes = new WorldMeshSet();

            GameObject root = new GameObject("Chunk_" + plan.Index + "_" + plan.Kind);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = chunkOrigin;
            chunk.Root = root;

            Mesh ground = terrain.BuildChunkMesh(path, plan, originX, originZ, chunkOrigin);
            GameObject terrainObject = new GameObject("Terrain", typeof(MeshFilter), typeof(MeshRenderer));
            terrainObject.transform.SetParent(root.transform, false);
            terrainObject.GetComponent<MeshFilter>().sharedMesh = ground;
            MeshRenderer terrainRenderer = terrainObject.GetComponent<MeshRenderer>();
            terrainRenderer.sharedMaterial = WorldMaterials.Terrain(season);
            terrainRenderer.shadowCastingMode = ShadowCastingMode.Off;
            chunk.Meshes.Add(ground);
            yield return null;

            BuildTrack();
            BuildCatenary();
            yield return null;

            BuildStructures();
            Emit(root.transform, "Built");
            yield return null;

            BuildVegetation(root.transform);
            chunk.Complete = true;
        }

        private void Emit(Transform parent, string prefix)
        {
            foreach (Renderer renderer in meshes.Emit(parent, prefix))
                chunk.Meshes.Add(renderer.GetComponent<MeshFilter>().sharedMesh);
        }

        // ---- Coordinates -------------------------------------------------------------------

        /// <summary>Chunk-local position of a point beside the track.</summary>
        private Vector3 P(float d, float x, float y) => path.Local(d, x, y, originX, originZ) - chunkOrigin;
        private Quaternion R(float d) => path.Rotation(d);
        private float Ground(float d, float x) => terrain.Height(d, x, false);
        private float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
        private bool Chance(float probability) => random.NextDouble() < probability;

        // ---- Track --------------------------------------------------------------------------

        private static readonly Vector2[] BallastProfile = { new Vector2(-2.25f, -0.14f), new Vector2(-1.55f, 0.07f), new Vector2(1.55f, 0.07f), new Vector2(2.25f, -0.14f) };
        private static readonly Vector2[] RailProfile =
        {
            new Vector2(-0.035f, 0.07f), new Vector2(-0.035f, 0.243f), new Vector2(-0.0675f, 0.243f), new Vector2(-0.0675f, 0.293f)
        };
        private static readonly Vector2[] RailProfileRight =
        {
            new Vector2(0.0675f, 0.293f), new Vector2(0.0675f, 0.243f), new Vector2(0.035f, 0.243f), new Vector2(0.035f, 0.07f)
        };
        private static readonly Vector2[] RailHeadProfile = { new Vector2(-0.0675f, 0.293f), new Vector2(0.0675f, 0.293f) };

        private void BuildTrack()
        {
            for (int t = 0; t < 2; t++)
            {
                float offset = t * SecondTrackOffset;
                Sweep(meshes.For(WorldMaterials.Ballast, 2f), plan.Start, plan.End, 2f, offset, BallastProfile);
                for (int side = -1; side <= 1; side += 2)
                {
                    float rail = offset + side * 0.76f;
                    Sweep(meshes.For(WorldMaterials.Rail, 1f), plan.Start, plan.End, 2f, rail, RailProfile);
                    Sweep(meshes.For(WorldMaterials.Rail, 1f), plan.Start, plan.End, 2f, rail, RailProfileRight);
                    Sweep(meshes.For(WorldMaterials.RailHead, 1f), plan.Start, plan.End, 2f, rail, RailHeadProfile);
                }
                // Sleepers keep their global spacing, so they continue evenly across chunk borders.
                const float pitch = 0.62f;
                int first = Mathf.CeilToInt(plan.Start / pitch);
                int last = Mathf.FloorToInt((plan.End - 0.001f) / pitch);
                for (int k = first; k <= last; k++)
                {
                    float d = k * pitch;
                    bool wood = WorldPlanner.Hash(k, t) % 23 == 0;
                    meshes.For(wood ? WorldMaterials.WoodSleeper : WorldMaterials.ConcreteSleeper, 1f)
                        .AddBox(P(d, offset, 0.04f), new Vector3(2.62f, 0.16f, 0.245f), R(d));
                }
            }
        }

        /// <summary>Extrudes a profile (lateral, height) along the route; profile ordered with the solid side on the right.</summary>
        private void Sweep(WorldMesh mesh, float d0, float d1, float step, float lateral, Vector2[] profile)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt((d1 - d0) / step));
            for (int i = 0; i < steps; i++)
            {
                float a = d0 + (d1 - d0) * i / steps;
                float b = d0 + (d1 - d0) * (i + 1) / steps;
                Quaternion ra = R(a), rb = R(b);
                float along = 0f;
                for (int k = 0; k < profile.Length - 1; k++)
                {
                    Vector2 p0 = profile[k], p1 = profile[k + 1];
                    Vector2 e = p1 - p0;
                    Vector3 n = (R((a + b) * 0.5f) * new Vector3(-e.y, e.x, 0f)).normalized;
                    float length = e.magnitude;
                    Vector3 v0 = P(a, lateral + p0.x, p0.y), v1 = P(a, lateral + p1.x, p1.y);
                    Vector3 v2 = P(b, lateral + p1.x, p1.y), v3 = P(b, lateral + p0.x, p0.y);
                    float va = a - plan.Start, vb = b - plan.Start;
                    mesh.AddQuad(v0, v1, v2, v3, n, new Vector2(along, va), new Vector2(along + length, va),
                        new Vector2(along + length, vb), new Vector2(along, vb));
                    along += length;
                }
            }
        }

        // ---- Overhead line ------------------------------------------------------------------

        private void BuildCatenary()
        {
            WorldMesh steel = meshes.For(WorldMaterials.Steel, 1f, true);
            WorldMesh concrete = meshes.For(WorldMaterials.Concrete, 1f, true);
            WorldMesh wire = meshes.For(WorldMaterials.Wire, 1f);
            bool station = plan.IsStation;
            float stationCentre = plan.Start + WorldPlanner.PlatformCentre;
            const float spacing = 60f;
            int first = Mathf.CeilToInt((plan.Start - 20f) / spacing);
            int last = Mathf.FloorToInt((plan.End - 20.001f) / spacing);
            for (int k = first; k <= last; k++)
            {
                float d = 20f + k * spacing;
                bool tunnel = planner.TryGetTunnel(d, out _, out _) || planner.TryGetTunnel(d - 8f, out _, out _) || planner.TryGetTunnel(d + 8f, out _, out _);
                if (tunnel) continue;
                bool onPlatform = station && Mathf.Abs(d - stationCentre) < 30f;
                // Inside a terminal's train shed the wires hang from the arches.
                if (station && plan.Station == StationStyle.Terminal && Mathf.Abs(d - stationCentre) < 54f) continue;
                Quaternion r = R(d);
                float leftX = onPlatform ? -7.9f : -3.3f;
                float rightX = SecondTrackOffset + 3.3f;
                foreach (float x in new[] { leftX, rightX })
                {
                    float ground = Mathf.Min(0f, Ground(d, x));
                    float height = 9.2f - ground;
                    concrete.AddBox(P(d, x, ground + height * 0.5f), new Vector3(0.34f, height, 0.34f), r);
                    float towardTrack = x < 0f ? 1f : -1f;
                    float armEnd = x < 0f ? 0.4f : SecondTrackOffset - 0.4f;
                    float armLength = Mathf.Abs(armEnd - x);
                    float armMid = (armEnd + x) * 0.5f;
                    steel.AddBox(P(d, armMid, 7.4f), new Vector3(armLength, 0.09f, 0.09f), r);
                    steel.AddBox(P(d, armMid, 6.6f), new Vector3(armLength, 0.06f, 0.06f), r);
                    steel.AddBox(P(d, armEnd, 6.8f), new Vector3(0.05f, 1.3f, 0.05f), r);
                    steel.AddBox(P(d, x + towardTrack * 0.3f, 7.0f), new Vector3(0.1f, 1.0f, 0.1f), r);
                }
            }
            // Contact and messenger wires run through the whole chunk, tunnels included.
            foreach (float x in new[] { 0f, SecondTrackOffset })
            {
                bool inTunnel = plan.Kind == WorldChunkKind.Tunnel;
                SweepWire(wire, x, inTunnel ? 5.9f : 6.1f, 0.022f);
                if (!inTunnel) SweepWire(wire, x, 7.35f, 0.018f);
            }
        }

        private void SweepWire(WorldMesh mesh, float x, float y, float radius)
        {
            Vector2[] profile =
            {
                new Vector2(-radius, y - radius), new Vector2(-radius, y + radius),
                new Vector2(radius, y + radius), new Vector2(radius, y - radius), new Vector2(-radius, y - radius)
            };
            Sweep(mesh, plan.Start, plan.End, 6f, x, profile);
        }

        // ---- Plants --------------------------------------------------------------------------

        private void BuildVegetation(Transform root)
        {
            WorldMeshSet near = new WorldMeshSet();
            List<Vector3> cardVertices = new List<Vector3>(), nearCardVertices = new List<Vector3>();
            List<Vector3> cardNormals = new List<Vector3>(), nearCardNormals = new List<Vector3>();
            List<Vector2> cardUvs = new List<Vector2>(), nearCardUvs = new List<Vector2>();
            List<int> cardTriangles = new List<int>(), nearCardTriangles = new List<int>();

            // Candidate spots on a jittered grid in route coordinates; the grid widens away from the track.
            float x = 10f;
            while (x < MaxTreeReach)
            {
                float stepX = 4.5f + x * 0.022f;
                float stepD = stepX;
                for (int side = -1; side <= 1; side += 2)
                {
                    for (float d = plan.Start + Range(0f, stepD); d < plan.End; d += stepD)
                    {
                        float px = WorldTerrain.CorridorCentre + side * (x + Range(-stepX * 0.45f, stepX * 0.45f));
                        float pd = Mathf.Clamp(d + Range(-stepD * 0.4f, stepD * 0.4f), plan.Start, plan.End - 0.01f);
                        if (!TreeWanted(pd, px, out CabTreeSpecies species, out float scale)) continue;
                        float radius = species == CabTreeSpecies.Bush ? 1f : 2.2f;
                        if (!mask.IsFree(pd, px, radius)) continue;
                        if (terrain.RiverDepth(pd, px) > 0.8f) continue;
                        bool overBore = terrain.InsideTunnel(pd);
                        float h = terrain.Height(pd, px, overBore);
                        Vector3 rootPosition = P(pd, px, h - 0.05f);
                        int variant = random.Next(0, CabTreeFactory.VariantCount);
                        float yaw = Range(0f, 360f);
                        if (Mathf.Abs(px - WorldTerrain.CorridorCentre) < NearTreeReach)
                        {
                            Mesh treeMesh = CabTreeFactory.GetMesh(species, variant, CabTreeFactory.IsLeafless(species, season));
                            Material[] materials = CabTreeFactory.GetMaterials(species, season);
                            Matrix4x4 matrix = Matrix4x4.TRS(rootPosition, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
                            near.For(materials[0], 1f, true).Append(treeMesh, 0, matrix);
                            near.For(materials[1], 1f, true).Append(treeMesh, 1, matrix);
                            TreeImpostors.AddCard(nearCardVertices, nearCardNormals, nearCardUvs, nearCardTriangles, rootPosition, species, variant, scale, yaw);
                        }
                        else
                        {
                            TreeImpostors.AddCard(cardVertices, cardNormals, cardUvs, cardTriangles, rootPosition, species, variant, scale, yaw);
                        }
                    }
                }
                x += stepX;
            }

            chunk.NearDetail = new GameObject("NearTrees");
            chunk.NearDetail.transform.SetParent(root, false);
            foreach (Renderer renderer in near.Emit(chunk.NearDetail.transform, "Tree"))
                chunk.Meshes.Add(renderer.GetComponent<MeshFilter>().sharedMesh);
            chunk.NearCards = EmitCards(root, "NearTreeCards", nearCardVertices, nearCardNormals, nearCardUvs, nearCardTriangles);
            EmitCards(root, "TreeCards", cardVertices, cardNormals, cardUvs, cardTriangles);
            BuildGrass(root);
            if (chunk.NearCards != null) chunk.NearCards.SetActive(false);
        }

        private GameObject EmitCards(Transform root, string name, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            if (vertices.Count == 0 || TreeImpostors.CardMaterial == null) return null;
            Mesh mesh = new Mesh { name = name + "_" + plan.Index };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            GameObject cards = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            cards.transform.SetParent(root, false);
            cards.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = cards.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = TreeImpostors.CardMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            chunk.Meshes.Add(mesh);
            return cards;
        }

        /// <summary>Whether a plant grows here, and which: forests are dense, meadows clumpy, fields only have shelter belts.</summary>
        private bool TreeWanted(float d, float x, out CabTreeSpecies species, out float scale)
        {
            species = CabTreeSpecies.Broadleaf;
            scale = Range(0.8f, 1.25f);
            float ax = Mathf.Abs(x - WorldTerrain.CorridorCentre);
            float clump = Mathf.PerlinNoise(Mathf.Repeat(d * 0.018f, 4000f) + 3.3f, Mathf.Repeat(x * 0.018f, 4000f) + 7.7f);
            float p = 0f;
            p += terrain.Weight(d, WorldChunkKind.Forest) * (ax < 14f ? 0.25f : 0.9f);
            p += terrain.Weight(d, WorldChunkKind.Meadow) * (clump > 0.62f ? 0.55f : 0.02f);
            p += terrain.Weight(d, WorldChunkKind.Water) * (clump > 0.55f ? 0.4f : 0.04f);
            p += terrain.Weight(d, WorldChunkKind.Village) * (ax > 18f && ax < 90f ? 0.12f : 0.03f);
            p += terrain.Weight(d, WorldChunkKind.Town) * 0.03f;
            p += terrain.Weight(d, WorldChunkKind.City) * 0.004f;
            p += terrain.Weight(d, WorldChunkKind.Industrial) * 0.015f;
            p += terrain.Weight(d, WorldChunkKind.Foothills) * (ax < 20f ? 0.08f : 0.4f);
            p += terrain.Weight(d, WorldChunkKind.Tunnel) * 0.3f;
            // Fields: shelter belts of trees along the parcel edges.
            float field = terrain.Weight(d, WorldChunkKind.Field);
            if (field > 0f)
            {
                float belt = Mathf.Repeat(ax - 70f, 160f);
                p += field * (belt < 9f ? 0.9f : 0.01f);
            }
            if (!Chance(p * density)) return false;

            float spruceShare = 0.25f + terrain.Weight(d, WorldChunkKind.Foothills) * 0.5f + terrain.Weight(d, WorldChunkKind.Tunnel) * 0.6f +
                                terrain.Weight(d, WorldChunkKind.Forest) * (clump > 0.5f ? 0.35f : 0f);
            double pick = random.NextDouble();
            bool open = terrain.Weight(d, WorldChunkKind.Forest) < 0.5f;
            if (open && pick < 0.3) species = CabTreeSpecies.Bush;
            else if (pick < spruceShare + (open ? 0.3 : 0)) species = CabTreeSpecies.Spruce;
            else if (random.NextDouble() < 0.45) species = CabTreeSpecies.Birch;
            if (species == CabTreeSpecies.Bush) scale = Range(0.7f, 1.3f);
            if (terrain.Weight(d, WorldChunkKind.Village) > 0.5f && species == CabTreeSpecies.Broadleaf) scale *= 0.75f;
            return true;
        }

        private void BuildGrass(Transform root)
        {
            if (grassMaterial == null) return;
            WorldMesh mesh = new WorldMesh(1f);
            int count = Mathf.RoundToInt(110f * density);
            for (int i = 0; i < count; i++)
            {
                float d = Range(plan.Start, plan.End);
                float side = Chance(0.5f) ? -1f : 1f;
                float x = WorldTerrain.CorridorCentre + side * Range(WorldTerrain.CorridorHalfWidth - 0.6f, WorldTerrain.CorridorHalfWidth + 9f);
                // The corridor itself is blocked for trees, but grass belongs on the verges; only
                // platforms, roads, buildings and portals keep it away.
                if (!StructuresFree(d, x)) continue;
                if (terrain.InsideTunnel(d) || terrain.RiverDepth(d, x) > 0.3f) continue;
                // No grass on the rock walls of a cutting or on steep banks.
                if (Mathf.Abs(Ground(d, x + 0.8f) - Ground(d, x - 0.8f)) > 0.6f) continue;
                Vector3 rootPosition = P(d, x, Ground(d, x) - 0.02f);
                float width = Range(0.9f, 1.6f), height = Range(0.35f, 0.75f), yaw = Range(0f, 180f);
                for (int card = 0; card < 2; card++)
                {
                    Vector3 right = Quaternion.Euler(0f, yaw + card * 90f, 0f) * Vector3.right * (width * 0.5f);
                    Vector3 up = Vector3.up * height;
                    int start = mesh.Vertices.Count;
                    mesh.Vertices.Add(rootPosition - right); mesh.Vertices.Add(rootPosition + right);
                    mesh.Vertices.Add(rootPosition + right + up); mesh.Vertices.Add(rootPosition - right + up);
                    for (int k = 0; k < 4; k++) mesh.Normals.Add(Vector3.up);
                    mesh.Uvs.Add(new Vector2(0f, 0f)); mesh.Uvs.Add(new Vector2(1f, 0f));
                    mesh.Uvs.Add(new Vector2(1f, 1f)); mesh.Uvs.Add(new Vector2(0f, 1f));
                    mesh.Triangles.Add(start); mesh.Triangles.Add(start + 2); mesh.Triangles.Add(start + 1);
                    mesh.Triangles.Add(start); mesh.Triangles.Add(start + 3); mesh.Triangles.Add(start + 2);
                }
            }
            if (mesh.IsEmpty) return;
            Mesh built = mesh.ToMesh("VergeGrass_" + plan.Index);
            GameObject grass = new GameObject("VergeGrass", typeof(MeshFilter), typeof(MeshRenderer));
            grass.transform.SetParent(root, false);
            grass.GetComponent<MeshFilter>().sharedMesh = built;
            MeshRenderer renderer = grass.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = grassMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            chunk.Meshes.Add(built);
        }

        private readonly List<Rect> structureZones = new List<Rect>();

        private void Block(float d0, float d1, float x0, float x1)
        {
            mask.AddRect(d0, d1, x0, x1);
            structureZones.Add(Rect.MinMaxRect(Mathf.Min(d0, d1), Mathf.Min(x0, x1), Mathf.Max(d0, d1), Mathf.Max(x0, x1)));
        }

        private bool StructuresFree(float d, float x)
        {
            for (int i = 0; i < structureZones.Count; i++)
                if (structureZones[i].Contains(new Vector2(d, x))) return false;
            return true;
        }
    }
}
