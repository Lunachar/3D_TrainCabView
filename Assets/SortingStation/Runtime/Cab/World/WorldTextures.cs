using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// Building surfaces drawn procedurally at start (bricks, corrugated sheet, plaster, wood
    /// siding, roof tiles): tileable greyscale-detail albedo textures that the materials tint,
    /// with matching normal maps, so no downloaded texture sets are needed for the buildings.
    /// </summary>
    public static class WorldTextures
    {
        public enum Surface
        {
            Bricks,
            Corrugated,
            Plaster,
            Siding,
            RoofTiles
        }

        private const int Size = 256;
        private static readonly Dictionary<Surface, Texture2D> Albedo = new Dictionary<Surface, Texture2D>();
        private static readonly Dictionary<Surface, Texture2D> Normals = new Dictionary<Surface, Texture2D>();

        /// <summary>Metres covered by one texture tile.</summary>
        public static float TileMetres(Surface surface)
        {
            switch (surface)
            {
                case Surface.Bricks: return 1.2f;
                case Surface.Corrugated: return 2f;
                case Surface.Siding: return 1.6f;
                case Surface.RoofTiles: return 1.6f;
                default: return 3f;
            }
        }

        public static Texture2D GetAlbedo(Surface surface)
        {
            if (!Albedo.TryGetValue(surface, out Texture2D texture) || texture == null) Build(surface);
            return Albedo[surface];
        }

        public static Texture2D GetNormal(Surface surface)
        {
            if (!Normals.TryGetValue(surface, out Texture2D texture) || texture == null) Build(surface);
            return Normals[surface];
        }

        private static void Build(Surface surface)
        {
            float[] height = new float[Size * Size];
            Color[] color = new Color[Size * Size];
            System.Random random = new System.Random(4000 + (int)surface);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float u = x / (float)Size, v = y / (float)Size;
                    float grain = Tile(u, v, 1.5f, 1.3f) * 0.6f + Tile(u, v, 6f, 7.7f) * 0.4f;
                    float h;
                    float shade;
                    switch (surface)
                    {
                        case Surface.Bricks:
                        {
                            // 8 courses of bricks per tile, every other course offset by half a brick.
                            float row = v * 8f;
                            int course = Mathf.FloorToInt(row);
                            float col = u * 4f + (course % 2 == 0 ? 0f : 0.5f);
                            int brick = Mathf.FloorToInt(col);
                            float fx = col - brick, fy = row - course;
                            bool mortar = fx < 0.04f || fx > 0.96f || fy < 0.09f || fy > 0.91f;
                            float brickTone = 0.82f + 0.18f * Hash01(brick * 31 + course * 7);
                            h = mortar ? 0f : 0.8f + grain * 0.2f;
                            shade = mortar ? 0.78f : brickTone * (0.86f + grain * 0.14f);
                            color[y * Size + x] = mortar ? new Color(0.78f, 0.76f, 0.72f) : new Color(0.62f, 0.30f, 0.22f) * shade * 1.35f;
                            break;
                        }
                        case Surface.Corrugated:
                        {
                            float wave = Mathf.Sin(u * Mathf.PI * 2f * 12f);
                            h = wave * 0.5f + 0.5f;
                            shade = 0.85f + wave * 0.1f + grain * 0.1f;
                            float rust = Mathf.Clamp01(Tile(u, v, 1.2f, 4.4f) * 1.8f - 1.1f);
                            color[y * Size + x] = Color.Lerp(Color.white * shade, new Color(0.55f, 0.36f, 0.24f), rust * 0.35f);
                            break;
                        }
                        case Surface.Siding:
                        {
                            // Horizontal boards with a shadowed lap at the bottom of each.
                            float board = v * 8f;
                            float f = board - Mathf.Floor(board);
                            h = f;
                            shade = (f < 0.12f ? 0.62f : 0.9f + 0.1f * f) * (0.9f + grain * 0.1f);
                            float wood = Tile(u, v, 2.5f, 9.1f);
                            color[y * Size + x] = Color.white * shade * (0.92f + wood * 0.08f);
                            break;
                        }
                        case Surface.RoofTiles:
                        {
                            float row = v * 6f;
                            int course = Mathf.FloorToInt(row);
                            float col = u * 6f + (course % 2 == 0 ? 0f : 0.5f);
                            float fx = col - Mathf.Floor(col), fy = row - course;
                            float round = Mathf.Sin(fx * Mathf.PI);
                            h = round * (0.4f + 0.6f * fy);
                            shade = (0.62f + 0.38f * round) * (fy < 0.1f ? 0.7f : 1f) * (0.9f + grain * 0.1f);
                            color[y * Size + x] = Color.white * shade;
                            break;
                        }
                        default:
                        {
                            h = grain;
                            shade = 0.88f + grain * 0.12f + (Tile(u, v, 0.8f, 2.2f) - 0.5f) * 0.08f;
                            color[y * Size + x] = Color.white * shade;
                            break;
                        }
                    }
                    height[y * Size + x] = h;
                    color[y * Size + x].a = 1f;
                }

            Texture2D albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = "World_" + surface, wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            albedo.SetPixels(color);
            albedo.Apply(true, false);
            Albedo[surface] = albedo;

            // Normal map from the height field (tangent space, Unity's packed format).
            Color[] normal = new Color[Size * Size];
            float strength = surface == Surface.Plaster ? 1.2f : 3f;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float dx = height[y * Size + (x + 1) % Size] - height[y * Size + (x + Size - 1) % Size];
                    float dy = height[((y + 1) % Size) * Size + x] - height[((y + Size - 1) % Size) * Size + x];
                    Vector3 n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                    normal[y * Size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            Texture2D normalMap = new Texture2D(Size, Size, TextureFormat.RGBA32, true, true) { name = "World_" + surface + "_Normal", wrapMode = TextureWrapMode.Repeat };
            normalMap.SetPixels(normal);
            normalMap.Apply(true, false);
            Normals[surface] = normalMap;
        }

        /// <summary>Seamless noise over the unit square: Perlin noise sampled around a torus of radius r.</summary>
        private static float Tile(float u, float v, float r, float seed)
        {
            float a = u * Mathf.PI * 2f, b = v * Mathf.PI * 2f;
            return Mathf.PerlinNoise(seed + Mathf.Cos(a) * r + Mathf.Sin(b) * r * 0.37f, seed * 1.7f + Mathf.Sin(a) * r + Mathf.Cos(b) * r);
        }

        private static float Hash01(int n)
        {
            unchecked
            {
                uint h = (uint)n * 2654435761u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return (h & 0xFFFF) / 65535f;
            }
        }
    }
}
