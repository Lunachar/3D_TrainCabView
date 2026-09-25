using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SortingStation
{
    /// <summary>
    /// URP Lit materials built from the CC0 textures in Resources/Pbr (see Art/Pbr/SOURCES.md).
    /// Materials are cached per texture set and tint, so every part that shares a surface also
    /// shares one material and batches with the SRP Batcher.
    /// </summary>
    public static class CabPbrMaterials
    {
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        public static Material Get(string textureId, Color tint, float smoothnessScale = 1f)
        {
            string key = textureId + "|" + ColorUtility.ToHtmlStringRGBA(tint) + "|" + smoothnessScale.ToString("0.00");
            if (Cache.TryGetValue(key, out Material cached) && cached != null) return cached;

            Material material = new Material(CabShaders.Lit) { name = textureId + "_" + ColorUtility.ToHtmlStringRGB(tint) };
            material.color = tint;
            Texture2D color = Resources.Load<Texture2D>("Pbr/" + textureId + "_Color");
            Texture2D normal = Resources.Load<Texture2D>("Pbr/" + textureId + "_Normal");
            Texture2D mask = Resources.Load<Texture2D>("Pbr/" + textureId + "_Mask");
            if (color != null) material.mainTexture = color;
            if (normal != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            if (mask != null && material.HasProperty("_MetallicGlossMap"))
            {
                // R = metalness, G = ambient occlusion, A = smoothness.
                material.SetTexture("_MetallicGlossMap", mask);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.SetTexture("_OcclusionMap", mask);
                material.EnableKeyword("_OCCLUSIONMAP");
            }
            CabShaders.SetSmoothness(material, smoothnessScale);
            Cache[key] = material;
            return material;
        }

        /// <summary>Untextured colour material (screens, lamps, paint accents), also cached.</summary>
        public static Material Plain(string name, Color color, float smoothness = 0.35f, float metallic = 0f)
        {
            string key = "plain|" + name + "|" + ColorUtility.ToHtmlStringRGBA(color) + "|" + smoothness.ToString("0.00") + "|" + metallic.ToString("0.00");
            if (Cache.TryGetValue(key, out Material cached) && cached != null) return cached;
            Material material = CabShaders.CreateLit(name, color, smoothness, metallic);
            Cache[key] = material;
            return material;
        }

        /// <summary>Emissive material instance for lamps and screens that change at runtime.</summary>
        public static Material Emissive(string name, Color baseColor, Color emission)
        {
            Material material = CabShaders.CreateLit(name, baseColor, 0.6f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return material;
        }

        /// <summary>Clear glass: transparent URP Lit with a faint tint and sharp reflections.</summary>
        public static Material Glass(string name, Color tint)
        {
            string key = "glass|" + name + "|" + ColorUtility.ToHtmlStringRGBA(tint);
            if (Cache.TryGetValue(key, out Material cached) && cached != null) return cached;
            Material material = CabShaders.CreateLit(name, tint, 0.96f);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            Cache[key] = material;
            return material;
        }
    }
}
