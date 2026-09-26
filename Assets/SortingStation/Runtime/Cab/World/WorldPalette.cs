using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// One small texture of flat colours (smoothness in alpha) and one Lit material over it.
    /// People and road vehicles colour their parts by UV into this palette instead of a
    /// material per part, so each of them is a single draw call and all of them batch.
    /// </summary>
    public static class WorldPalette
    {
        private const int Size = 64;
        private static Texture2D texture;
        private static Material material;
        private static readonly Dictionary<int, int> Cells = new Dictionary<int, int>();
        private static bool warned;

        public static Material Material
        {
            get
            {
                Ensure();
                return material;
            }
        }

        /// <summary>The UV of the palette cell with this colour and smoothness (added on first use).</summary>
        public static Vector2 Uv(Color colour, float smoothness)
        {
            Ensure();
            // Five bits per channel and four for smoothness: close colours share a cell.
            int r = Mathf.Clamp(Mathf.RoundToInt(colour.r * 31f), 0, 31);
            int g = Mathf.Clamp(Mathf.RoundToInt(colour.g * 31f), 0, 31);
            int b = Mathf.Clamp(Mathf.RoundToInt(colour.b * 31f), 0, 31);
            int s = Mathf.Clamp(Mathf.RoundToInt(smoothness * 15f), 0, 15);
            int key = (r << 14) | (g << 9) | (b << 4) | s;
            if (!Cells.TryGetValue(key, out int cell))
            {
                if (Cells.Count >= Size * Size)
                {
                    if (!warned) Debug.LogWarning("World palette is full; reusing the first cell.");
                    warned = true;
                    return CellUv(0);
                }
                cell = Cells.Count;
                Cells[key] = cell;
                texture.SetPixel(cell % Size, cell / Size, new Color(r / 31f, g / 31f, b / 31f, s / 15f));
                texture.Apply(false);
            }
            return CellUv(cell);
        }

        private static Vector2 CellUv(int cell) => new Vector2((cell % Size + 0.5f) / Size, (cell / Size + 0.5f) / Size);

        private static void Ensure()
        {
            if (material != null && texture != null) return;
            Cells.Clear();
            texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "WorldPalette",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            material = CabShaders.CreateLit("WorldPalette", Color.white, 1f, 0.15f);
            material.mainTexture = texture;
            // Smoothness comes from the palette's alpha.
            material.SetFloat("_SmoothnessTextureChannel", 1f);
            material.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        }
    }
}
