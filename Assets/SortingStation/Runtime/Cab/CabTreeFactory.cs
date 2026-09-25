using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SortingStation
{
    public enum CabTreeSpecies
    {
        Broadleaf,
        Birch,
        Spruce,
        Bush
    }

    /// <summary>
    /// Procedural trees for the immersive world: a bent, tapering trunk with PBR bark, a few
    /// branches, and a crown of two-sided alpha-cut foliage cards (textures generated for this
    /// project, see Art/Pbr/SOURCES.md). Card normals point away from the crown centre, so the
    /// canopy is lit as one soft volume. Meshes are cached per species and variant and shared by
    /// every tree that uses them; submesh 0 is bark, submesh 1 is foliage.
    /// </summary>
    public static class CabTreeFactory
    {
        public const int VariantCount = 4;
        private static readonly Dictionary<int, Mesh> Meshes = new Dictionary<int, Mesh>();
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        public static Mesh GetMesh(CabTreeSpecies species, int variant, bool leafless)
        {
            variant = Mathf.Abs(variant) % VariantCount;
            int key = ((int)species * 16 + variant) * 2 + (leafless ? 1 : 0);
            if (Meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            Mesh mesh = Build(species, 9173 + (int)species * 131 + variant * 17, leafless);
            Meshes[key] = mesh;
            return mesh;
        }

        public static Material[] GetMaterials(CabTreeSpecies species, SeasonType season)
        {
            return new[] { BarkMaterial(species), FoliageMaterial(species, season) };
        }

        public static bool IsLeafless(CabTreeSpecies species, SeasonType season) =>
            season == SeasonType.Winter && (species == CabTreeSpecies.Broadleaf || species == CabTreeSpecies.Birch);

        private static Material BarkMaterial(CabTreeSpecies species)
        {
            return species == CabTreeSpecies.Birch
                ? CabPbrMaterials.Get("Bark007", new Color(0.92f, 0.92f, 0.90f), 0.6f)
                : CabPbrMaterials.Get("Bark012", species == CabTreeSpecies.Spruce ? new Color(0.55f, 0.46f, 0.40f) : new Color(0.72f, 0.66f, 0.60f), 0.6f);
        }

        private static Material FoliageMaterial(CabTreeSpecies species, SeasonType season)
        {
            string texture = species == CabTreeSpecies.Spruce ? "Foliage_Spruce"
                : species == CabTreeSpecies.Birch ? "Foliage_Birch"
                : species == CabTreeSpecies.Bush ? "Foliage_Bush" : "Foliage_Broadleaf";
            Color tint = FoliageTint(species, season);
            string key = texture + "|" + ColorUtility.ToHtmlStringRGB(tint);
            if (Materials.TryGetValue(key, out Material cached) && cached != null) return cached;
            Material material = CabShaders.CreateLit(texture + "_" + season, tint, 0.15f);
            Texture2D foliage = Resources.Load<Texture2D>("Pbr/" + texture);
            if (foliage != null) material.mainTexture = foliage;
            // Alpha-clipped, rendered from both sides.
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.42f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.renderQueue = (int)RenderQueue.AlphaTest;
            Materials[key] = material;
            return material;
        }

        private static Color FoliageTint(CabTreeSpecies species, SeasonType season)
        {
            if (species == CabTreeSpecies.Spruce)
                return season == SeasonType.Winter ? new Color(0.78f, 0.86f, 0.88f) : new Color(0.92f, 1f, 0.92f);
            switch (season)
            {
                case SeasonType.Autumn:
                    return species == CabTreeSpecies.Birch ? new Color(1.25f, 1.05f, 0.35f) : new Color(1.35f, 0.72f, 0.30f);
                case SeasonType.Spring:
                    return new Color(0.95f, 1.12f, 0.78f);
                case SeasonType.Winter:
                    return new Color(0.70f, 0.66f, 0.56f); // bushes keep a few dry leaves
                default:
                    return new Color(0.80f, 1f, 0.70f);
            }
        }

        private static Mesh Build(CabTreeSpecies species, int seed, bool leafless)
        {
            System.Random random = new System.Random(seed);
            TreeMeshData data = new TreeMeshData();
            switch (species)
            {
                case CabTreeSpecies.Spruce:
                    BuildSpruce(data, random);
                    break;
                case CabTreeSpecies.Bush:
                    BuildBush(data, random, leafless);
                    break;
                default:
                    BuildDeciduous(data, random, species == CabTreeSpecies.Birch, leafless);
                    break;
            }
            return data.ToMesh(species + (leafless ? "_Bare_" : "_") + seed);
        }

        private static void BuildDeciduous(TreeMeshData data, System.Random random, bool birch, bool leafless)
        {
            float height = birch ? Range(random, 9f, 12f) : Range(random, 7f, 9.5f);
            float radius = birch ? Range(random, 0.13f, 0.17f) : Range(random, 0.2f, 0.27f);
            Vector3 lean = new Vector3(Range(random, -0.35f, 0.35f), 0f, Range(random, -0.35f, 0.35f));
            List<Vector3> spine = data.AddTrunk(Vector3.zero, height, radius, radius * 0.25f, lean, 7, 7, 1.2f);

            Vector3 crownCenter = spine[5] + Vector3.up * (birch ? 0.3f : 0.1f);
            Vector3 crownRadii = birch ? new Vector3(1.9f, 3.1f, 1.9f) : new Vector3(2.8f, 2.3f, 2.8f);
            int branches = leafless ? 11 : (birch ? 5 : 7);
            for (int i = 0; i < branches; i++)
            {
                float t = Range(random, birch ? 0.35f : 0.42f, 0.9f);
                Vector3 start = SampleSpine(spine, t);
                float yaw = i / (float)branches * Mathf.PI * 2f + Range(random, -0.4f, 0.4f);
                float rise = Range(random, birch ? 55f : 30f, birch ? 75f : 58f) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(yaw) * Mathf.Cos(rise), Mathf.Sin(rise), Mathf.Sin(yaw) * Mathf.Cos(rise));
                float length = (birch ? 2.0f : 2.8f) * (1.1f - t * 0.6f) * Range(random, 0.8f, 1.2f);
                data.AddLimb(start, direction, length, radius * (leafless ? 0.35f : 0.42f), 4, 3);
                if (leafless)
                {
                    // Bare winter crowns read as a fine net of twigs.
                    Vector3 tip = start + direction * length;
                    for (int k = 0; k < 3; k++)
                    {
                        Vector3 twig = (direction + new Vector3(Range(random, -0.7f, 0.7f), Range(random, 0.1f, 0.8f), Range(random, -0.7f, 0.7f))).normalized;
                        data.AddLimb(Vector3.Lerp(start, tip, Range(random, 0.5f, 0.95f)), twig, length * 0.45f, radius * 0.12f, 3, 2);
                    }
                }
            }
            if (leafless) return;
            int cards = birch ? 40 : 34;
            float cardSize = birch ? 1.9f : 2.7f;
            for (int i = 0; i < cards; i++)
                data.AddCrownCard(random, crownCenter, crownRadii, cardSize * Range(random, 0.8f, 1.15f), new Rect(0f, 0f, 1f, 1f));
        }

        private static void BuildSpruce(TreeMeshData data, System.Random random)
        {
            float height = Range(random, 10f, 14f);
            float radius = Range(random, 0.2f, 0.26f);
            List<Vector3> spine = data.AddTrunk(Vector3.zero, height, radius, 0.03f, Vector3.zero, 6, 8, 1.2f);
            // Whorls of drooping boughs, wide at the bottom and narrowing to the tip.
            for (float y = 1.1f; y < height - 0.4f; y += Range(random, 0.42f, 0.58f))
            {
                float t = y / height;
                float reach = Mathf.Lerp(3.2f, 0.45f, t) * Range(random, 0.85f, 1.1f);
                int boughs = t > 0.8f ? 4 : 6;
                float offset = Range(random, 0f, Mathf.PI * 2f);
                Vector3 center = SampleSpine(spine, t);
                for (int i = 0; i < boughs; i++)
                {
                    float yaw = offset + i / (float)boughs * Mathf.PI * 2f;
                    Vector3 outward = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                    data.AddBough(center, outward, reach, reach * 0.62f, Range(random, 12f, 24f));
                }
            }
        }

        private static void BuildBush(TreeMeshData data, System.Random random, bool leafless)
        {
            Vector3 center = new Vector3(0f, 0.75f, 0f);
            for (int i = 0; i < 6; i++)
            {
                Vector3 direction = new Vector3(Range(random, -0.6f, 0.6f), 1f, Range(random, -0.6f, 0.6f)).normalized;
                data.AddLimb(Vector3.zero, direction, Range(random, 0.9f, 1.4f), 0.035f, 3, 2);
            }
            int cards = leafless ? 5 : 16;
            for (int i = 0; i < cards; i++)
                data.AddCrownCard(random, center, new Vector3(1.1f, 0.7f, 1.1f), Range(random, 1.1f, 1.5f), new Rect(0f, 0f, 1f, 1f));
        }

        private static Vector3 SampleSpine(List<Vector3> spine, float t)
        {
            float f = Mathf.Clamp01(t) * (spine.Count - 1);
            int i = Mathf.Min(spine.Count - 2, Mathf.FloorToInt(f));
            return Vector3.Lerp(spine[i], spine[i + 1], f - i);
        }

        private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        private sealed class TreeMeshData
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<int> bark = new List<int>();
            private readonly List<int> foliage = new List<int>();

            /// <summary>Tapered, gently bent trunk; returns its centre line (rings + 1 points).</summary>
            public List<Vector3> AddTrunk(Vector3 root, float height, float baseRadius, float topRadius, Vector3 lean,
                int sides, int rings, float barkTileMetres)
            {
                List<Vector3> spine = new List<Vector3>();
                for (int r = 0; r <= rings; r++)
                {
                    float t = r / (float)rings;
                    spine.Add(root + Vector3.up * (height * t) + lean * (t * t));
                }
                AddTube(spine, baseRadius, topRadius, sides, barkTileMetres);
                return spine;
            }

            public void AddLimb(Vector3 start, Vector3 direction, float length, float radius, int sides, int segments)
            {
                List<Vector3> spine = new List<Vector3>();
                for (int s = 0; s <= segments; s++)
                {
                    float t = s / (float)segments;
                    // Branches arch slightly downward toward their tips.
                    spine.Add(start + direction * (length * t) + Vector3.down * (length * 0.12f * t * t));
                }
                AddTube(spine, radius, radius * 0.25f, sides, 0.8f);
            }

            private void AddTube(List<Vector3> spine, float startRadius, float endRadius, int sides, float tileMetres)
            {
                int start = vertices.Count;
                float travelled = 0f;
                for (int r = 0; r < spine.Count; r++)
                {
                    if (r > 0) travelled += Vector3.Distance(spine[r], spine[r - 1]);
                    Vector3 forward = r < spine.Count - 1 ? spine[r + 1] - spine[r] : spine[r] - spine[r - 1];
                    Quaternion frame = Quaternion.FromToRotation(Vector3.up, forward.normalized);
                    float radius = Mathf.Lerp(startRadius, endRadius, r / (float)(spine.Count - 1));
                    for (int s = 0; s <= sides; s++)
                    {
                        float angle = s / (float)sides * Mathf.PI * 2f;
                        Vector3 normal = frame * new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                        vertices.Add(spine[r] + normal * radius);
                        normals.Add(normal);
                        uvs.Add(new Vector2(s / (float)sides * Mathf.Max(0.5f, startRadius * 6.28f / tileMetres), travelled / tileMetres));
                    }
                }
                int stride = sides + 1;
                for (int r = 0; r < spine.Count - 1; r++)
                    for (int s = 0; s < sides; s++)
                    {
                        int a = start + r * stride + s;
                        int b = a + stride;
                        bark.Add(a); bark.Add(b); bark.Add(a + 1);
                        bark.Add(a + 1); bark.Add(b); bark.Add(b + 1);
                    }
            }

            /// <summary>A foliage card inside the crown ellipsoid, facing a random direction.</summary>
            public void AddCrownCard(System.Random random, Vector3 center, Vector3 radii, float size, Rect uv)
            {
                Vector3 offset = RandomInsideUnitSphere(random);
                Vector3 position = center + Vector3.Scale(offset, radii);
                Vector3 facing = (RandomInsideUnitSphere(random) + offset * 0.8f).normalized;
                if (facing.sqrMagnitude < 0.01f) facing = Vector3.forward;
                Quaternion rotation = Quaternion.LookRotation(facing) * Quaternion.Euler(0f, 0f, Range(random, 0f, 360f));
                AddCard(position, rotation, new Vector2(size, size), uv, center);
            }

            /// <summary>
            /// A drooping spruce bough running outward from the trunk: two crossed cards (flat and
            /// upright) so it reads as a full branch from the side as well as from above.
            /// </summary>
            public void AddBough(Vector3 trunkPoint, Vector3 outward, float length, float width, float droopDegrees)
            {
                Vector3 across = Vector3.Cross(Vector3.up, outward).normalized;
                Vector3 along = Quaternion.AngleAxis(droopDegrees, across) * outward;
                Vector3 upright = Vector3.Cross(along, across).normalized;
                if (upright.y < 0f) upright = -upright;
                Vector3 center = trunkPoint + along * (length * 0.5f);
                // Only the band of the bough texture that holds the branch is mapped.
                Rect band = new Rect(0f, 0.28f, 1f, 0.44f);
                Vector3 normalOrigin = trunkPoint + Vector3.down * 1.2f;
                AddCardAlong(center, along, across, length, width, band, normalOrigin);
                AddCardAlong(center, along, upright, length, width * 0.7f, band, normalOrigin);
            }

            private void AddCard(Vector3 center, Quaternion rotation, Vector2 size, Rect uv, Vector3 normalOrigin)
            {
                Vector3 right = rotation * Vector3.right * (size.x * 0.5f);
                Vector3 up = rotation * Vector3.up * (size.y * 0.5f);
                AddQuad(center - right - up, center + right - up, center + right + up, center - right + up, uv, normalOrigin);
            }

            private void AddCardAlong(Vector3 center, Vector3 along, Vector3 across, float length, float width, Rect uv, Vector3 normalOrigin)
            {
                Vector3 a = along.normalized * (length * 0.5f);
                Vector3 c = across.normalized * (width * 0.5f);
                AddQuad(center - a - c, center + a - c, center + a + c, center - a + c, uv, normalOrigin);
            }

            private void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Rect uv, Vector3 normalOrigin)
            {
                int start = vertices.Count;
                Vector3[] points = { p0, p1, p2, p3 };
                Vector2[] coords =
                {
                    new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMax, uv.yMin),
                    new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMin, uv.yMax)
                };
                for (int i = 0; i < 4; i++)
                {
                    vertices.Add(points[i]);
                    Vector3 normal = points[i] - normalOrigin;
                    normals.Add(normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up);
                    uvs.Add(coords[i]);
                }
                foliage.Add(start); foliage.Add(start + 1); foliage.Add(start + 2);
                foliage.Add(start); foliage.Add(start + 2); foliage.Add(start + 3);
            }

            private static Vector3 RandomInsideUnitSphere(System.Random random)
            {
                for (int attempt = 0; attempt < 16; attempt++)
                {
                    Vector3 v = new Vector3(Range(random, -1f, 1f), Range(random, -1f, 1f), Range(random, -1f, 1f));
                    if (v.sqrMagnitude <= 1f) return v;
                }
                return Vector3.zero;
            }

            public Mesh ToMesh(string name)
            {
                Mesh mesh = new Mesh { name = "Tree_" + name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(bark, 0);
                mesh.SetTriangles(foliage, 1);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }
        }
    }
}
