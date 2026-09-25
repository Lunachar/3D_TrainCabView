using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SortingStation
{
    /// <summary>
    /// Landscape added around the prototype route in the immersive mode: rolling hills that rise
    /// from the edge of the flat track corridor to the horizon (higher in the mountain section),
    /// and grass tufts along the verges. Both follow the track curve via <see cref="Cab3DTrackMath"/>.
    /// </summary>
    public static class CabWorldExtras
    {
        public const string HillsName = "GroundRegion_Hills";
        public const string GrassName = "VergeGrass";
        private const float CorridorHalfWidth = 38f;
        private const float HorizonHalfWidth = 230f;
        private const float MountainStart01 = 0.54f;
        private const float MountainEnd01 = 0.71f;

        /// <summary>Height of the landscape at a lateral offset from the track and a route distance.</summary>
        public static float HillHeight(float lateral, float distance, float cycle)
        {
            float away = Mathf.Abs(lateral);
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CorridorHalfWidth, CorridorHalfWidth + 55f, away));
            if (rise <= 0f) return 0f;
            float along = cycle > 0f ? Mathf.Repeat(distance / cycle, 1f) : 0f;
            float mountain = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(MountainStart01 - 0.05f, MountainStart01 + 0.03f, along)) *
                             (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(MountainEnd01 - 0.03f, MountainEnd01 + 0.05f, along)));
            float side = lateral < 0f ? 17.3f : 71.9f;
            float broad = Mathf.PerlinNoise(distance * 0.006f + side, away * 0.012f);
            float detail = Mathf.PerlinNoise(distance * 0.021f + side * 2f, away * 0.035f + 3.1f);
            float height = broad * 16f + detail * 5f + Mathf.InverseLerp(CorridorHalfWidth, HorizonHalfWidth, away) * 10f;
            height += mountain * (22f + broad * 38f);
            return rise * height;
        }

        public static GameObject BuildHills(Transform routeRoot, float cycle)
        {
            const float step = 10f;
            float[] laterals = { 0f, 12f, 22f, 34f, 48f, 64f, 84f, 110f, 145f, 185f, 230f };
            int columns = Mathf.Max(2, Mathf.CeilToInt(cycle / step) + 1);
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();
            for (int side = -1; side <= 1; side += 2)
            {
                int start = vertices.Count;
                for (int c = 0; c < columns; c++)
                {
                    float d = Mathf.Min(cycle, c * step);
                    Vector3 point = Cab3DTrackMath.Point(d, cycle);
                    Quaternion heading = Cab3DTrackMath.Heading(d, cycle);
                    for (int r = 0; r < laterals.Length; r++)
                    {
                        float lateral = side * (CorridorHalfWidth + laterals[r]);
                        float height = HillHeight(lateral, d, cycle) - 0.02f;
                        vertices.Add(point + heading * new Vector3(lateral, height, 0f));
                        // Same tiling as the ground regions (4.5 m across, 9 m along; the PBR pass doubles along).
                        uvs.Add(new Vector2(laterals[r] / 4.5f, d / 9f));
                    }
                }
                int rows = laterals.Length;
                for (int c = 0; c < columns - 1; c++)
                    for (int r = 0; r < rows - 1; r++)
                    {
                        int a = start + c * rows + r;
                        int b = a + rows;
                        // Rows run outward (+X on the right, -X on the left), columns along +Z;
                        // wind each side so the normals face up.
                        if (side > 0)
                        {
                            triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                            triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
                        }
                        else
                        {
                            triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                            triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
                        }
                    }
            }
            Mesh mesh = new Mesh { name = HillsName + "Mesh", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            GameObject hills = new GameObject(HillsName, typeof(MeshFilter), typeof(MeshRenderer));
            hills.transform.SetParent(routeRoot, false);
            hills.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = hills.GetComponent<MeshRenderer>();
            // Named like a ground region so the PBR pass dresses it for the season.
            renderer.sharedMaterial = CabShaders.CreateLit("GroundRegionMaterial_Hills", Color.white, 0.1f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return hills;
        }

        /// <summary>
        /// Grass tufts along both verges, merged into one mesh per 60 m so the whole route costs
        /// only a handful of draw calls. Cards are upright and two-sided.
        /// </summary>
        public static int BuildVergeGrass(Transform routeRoot, float cycle, float density, SeasonType season)
        {
            Transform existing = routeRoot.Find(GrassName);
            if (existing != null) Object.Destroy(existing.gameObject);
            if (season == SeasonType.Winter) return 0;
            Transform group = new GameObject(GrassName).transform;
            group.SetParent(routeRoot, false);
            Material material = GrassMaterial(season);
            System.Random random = new System.Random(4211);
            const float chunk = 60f;
            int tufts = 0;
            for (float chunkStart = 0f; chunkStart < cycle; chunkStart += chunk)
            {
                List<Vector3> vertices = new List<Vector3>();
                List<Vector3> normals = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                List<int> triangles = new List<int>();
                int count = Mathf.RoundToInt(70f * Mathf.Clamp(density, 0.3f, 1.2f));
                for (int i = 0; i < count; i++)
                {
                    float d = chunkStart + (float)random.NextDouble() * chunk;
                    if (d > cycle) continue;
                    float side = random.NextDouble() < 0.5 ? -1f : 1f;
                    float lateral = side * (3.4f + (float)random.NextDouble() * 9f);
                    Vector3 root = Cab3DTrackMath.Point(d, cycle) + Cab3DTrackMath.Heading(d, cycle) * new Vector3(lateral, 0f, 0f);
                    float width = 0.9f + (float)random.NextDouble() * 0.7f;
                    float height = 0.35f + (float)random.NextDouble() * 0.35f;
                    float yaw = (float)random.NextDouble() * 180f;
                    for (int card = 0; card < 2; card++)
                    {
                        Quaternion rotation = Quaternion.Euler(0f, yaw + card * 90f, 0f);
                        Vector3 right = rotation * Vector3.right * (width * 0.5f);
                        int start = vertices.Count;
                        vertices.Add(root - right);
                        vertices.Add(root + right);
                        vertices.Add(root + right + Vector3.up * height);
                        vertices.Add(root - right + Vector3.up * height);
                        for (int k = 0; k < 4; k++) normals.Add(Vector3.up);
                        uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
                        uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));
                        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                    }
                    tufts++;
                }
                if (vertices.Count == 0) continue;
                Mesh mesh = new Mesh { name = GrassName + "_" + Mathf.RoundToInt(chunkStart) };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                GameObject part = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                part.transform.SetParent(group, false);
                part.GetComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = part.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            return tufts;
        }

        private static Material GrassMaterial(SeasonType season)
        {
            Color tint = season == SeasonType.Autumn ? new Color(1.2f, 0.95f, 0.55f)
                : season == SeasonType.Spring ? new Color(0.9f, 1.1f, 0.8f) : new Color(0.85f, 1f, 0.78f);
            Material material = CabShaders.CreateLit("VergeGrass_" + season, tint, 0.1f);
            Texture2D texture = Resources.Load<Texture2D>("Pbr/Foliage_Grass");
            if (texture != null) material.mainTexture = texture;
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.45f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.renderQueue = (int)RenderQueue.AlphaTest;
            return material;
        }
    }
}
