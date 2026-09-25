using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// Switches every 3D TextMesh under a root to the depth-tested SortingStation/WorldText shader
    /// (one material per font texture), so captions and station signs are hidden behind geometry.
    /// </summary>
    public static class CabWorldText
    {
        private static readonly Dictionary<Texture, Material> Materials = new Dictionary<Texture, Material>();

        public static int Apply(Transform root)
        {
            if (root == null || CabShaders.WorldText == null) return 0;
            int count = 0;
            TextMesh[] texts = root.GetComponentsInChildren<TextMesh>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                MeshRenderer renderer = texts[i].GetComponent<MeshRenderer>();
                Material source = renderer != null ? renderer.sharedMaterial : null;
                Texture fontTexture = source != null ? source.mainTexture : null;
                if (fontTexture == null && texts[i].font != null) fontTexture = texts[i].font.material.mainTexture;
                if (fontTexture == null) continue;
                if (!Materials.TryGetValue(fontTexture, out Material material) || material == null)
                {
                    material = new Material(CabShaders.WorldText) { name = "WorldText_" + fontTexture.name, mainTexture = fontTexture };
                    Materials[fontTexture] = material;
                }
                renderer.sharedMaterial = material;
                count++;
            }
            return count;
        }
    }
}
