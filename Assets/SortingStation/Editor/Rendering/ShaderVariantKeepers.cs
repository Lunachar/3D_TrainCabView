using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SortingStation.EditorTools
{
    /// <summary>
    /// The immersive world creates its materials at runtime, so no asset in the project used
    /// alpha-clipped foliage, emission or normal-mapped Lit variants, and the build stripped
    /// them: grass and tree cards drew as solid squares. These small materials in Resources
    /// keep every keyword combination the runtime uses in the player.
    /// </summary>
    public static class ShaderVariantKeepers
    {
        public const string Folder = "Assets/SortingStation/Resources/Rendering/VariantKeepers";

        [MenuItem("Sorting Station/Rendering/Create shader variant keepers")]
        public static void Create()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new System.InvalidOperationException("URP Lit shader not found.");
            Directory.CreateDirectory(Folder);
            Make(lit, "LitAlphaClip", material =>
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.4f);
                material.SetFloat("_Cull", (float)CullMode.Off);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            });
            Make(lit, "LitEmission", material => material.EnableKeyword("_EMISSION"));
            Make(lit, "LitNormal", material => material.EnableKeyword("_NORMALMAP"));
            // WorldPalette: smoothness from the albedo alpha.
            Make(lit, "LitAlbedoSmoothness", material =>
            {
                material.SetFloat("_SmoothnessTextureChannel", 1f);
                material.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            });
            Make(lit, "LitPbr", material =>
            {
                material.EnableKeyword("_NORMALMAP");
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.EnableKeyword("_OCCLUSIONMAP");
            });
            Make(lit, "LitTransparent", material =>
            {
                material.SetFloat("_Surface", 1f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            });
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Shader variant keepers created in " + Folder);
        }

        private static void Make(Shader shader, string name, System.Action<Material> setup)
        {
            string path = Folder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created) material = new Material(shader) { name = name };
            material.shader = shader;
            setup(material);
            if (created) AssetDatabase.CreateAsset(material, path);
            EditorUtility.SetDirty(material);
        }
    }
}
