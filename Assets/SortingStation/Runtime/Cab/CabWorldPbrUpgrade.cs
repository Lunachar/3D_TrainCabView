using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// Gives the generated route real surfaces in the immersive mode: after the route is built
    /// and its season applied, flat-coloured ground, ballast, sleepers, rails, trunks, roads,
    /// concrete and rock get the CC0 PBR materials from Resources/Pbr. Each source material keeps
    /// its UV tiling, so the textures land at the scale the route meshes were made for.
    /// </summary>
    public static class CabWorldPbrUpgrade
    {
        private struct Rule
        {
            public string TextureId;
            public Color Tint;
            public float Smoothness;
            public Vector2 TilingScale;
        }

        /// <returns>How many renderers received a PBR material.</returns>
        public static int Apply(Transform root, SeasonType season)
        {
            if (root == null) return 0;
            Dictionary<Material, Material> upgraded = new Dictionary<Material, Material>();
            int count = 0;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null) continue;
                    if (!upgraded.TryGetValue(source, out Material replacement))
                    {
                        replacement = TryFindRule(source.name, season, out Rule rule) ? Create(source, rule) : null;
                        upgraded[source] = replacement;
                    }
                    if (replacement == null) continue;
                    materials[i] = replacement;
                    changed = true;
                }
                if (!changed) continue;
                renderer.sharedMaterials = materials;
                count++;
            }
            return count;
        }

        private static Material Create(Material source, Rule rule)
        {
            Material material = new Material(CabPbrMaterials.Get(rule.TextureId, rule.Tint, rule.Smoothness))
            {
                name = StripSuffix(source.name) + "_PBR"
            };
            material.mainTextureScale = Vector2.Scale(source.mainTextureScale, rule.TilingScale);
            material.mainTextureOffset = source.mainTextureOffset;
            return material;
        }

        private static bool TryFindRule(string materialName, SeasonType season, out Rule rule)
        {
            string name = StripSuffix(materialName);
            rule = default;
            if (name.StartsWith("GroundRegionMaterial_") || name.StartsWith("SoftGrass") || name.EndsWith("_RoadsideGrass"))
            {
                bool field = name.EndsWith("_Field");
                rule = GroundRule(season, field);
                return true;
            }
            if (name.StartsWith("Ballast")) return Set(out rule, "Gravel022", new Color(0.80f, 0.77f, 0.72f), 0.8f);
            if (name.EndsWith("_GravelShoulder")) return Set(out rule, "Gravel040", new Color(0.86f, 0.84f, 0.80f), 0.8f);
            if (name.StartsWith("SleeperWood") || name.StartsWith("SleeperWeathered"))
                return Set(out rule, "Planks021", new Color(0.46f, 0.34f, 0.25f), 0.6f);
            if (name.StartsWith("SleeperConcrete")) return Set(out rule, "Concrete034", new Color(0.78f, 0.77f, 0.74f), 0.7f);
            if (name.StartsWith("RailSteel")) return Set(out rule, "Metal046A", new Color(0.46f, 0.38f, 0.32f), 0.6f);
            if (name.StartsWith("RailHead")) return Set(out rule, "Metal032", new Color(0.82f, 0.82f, 0.80f), 1f);
            if (name.StartsWith("Trunk")) return Set(out rule, "Bark012", new Color(0.82f, 0.78f, 0.74f), 0.7f);
            if (name.EndsWith("_Asphalt") || name.StartsWith("OverpassAsphalt") || name.StartsWith("ViaductAsphalt"))
                return Set(out rule, "Asphalt012", new Color(0.85f, 0.85f, 0.85f), 0.8f);
            if (name == "Station" || name.StartsWith("OverpassConcrete") || name.StartsWith("ViaductConcrete") ||
                name.StartsWith("Platform") && !name.Contains("Safety"))
                return Set(out rule, "Concrete034", new Color(0.80f, 0.80f, 0.78f), 0.7f);
            if (name.StartsWith("TunnelLining")) return Set(out rule, "Rock035", new Color(0.72f, 0.72f, 0.72f), 0.7f);
            if (name.StartsWith("Rock")) return Set(out rule, "Rock030", new Color(0.86f, 0.84f, 0.80f), 0.7f);
            return false;
        }

        private static Rule GroundRule(SeasonType season, bool field)
        {
            // Ground region meshes tile the texture every 4.5 m across and 9 m along the track;
            // doubling the along-track tiling keeps grass blades square.
            Vector2 square = new Vector2(1f, 2f);
            switch (season)
            {
                case SeasonType.Winter:
                    return new Rule { TextureId = "Snow004", Tint = new Color(0.93f, 0.96f, 1f), Smoothness = 0.8f, TilingScale = square };
                case SeasonType.Autumn:
                    return new Rule { TextureId = field ? "Ground037" : "Ground037", Tint = new Color(0.92f, 0.80f, 0.58f), Smoothness = 0.5f, TilingScale = square };
                case SeasonType.Spring:
                    return new Rule { TextureId = field ? "Ground037" : "Grass004", Tint = new Color(0.86f, 1f, 0.80f), Smoothness = 0.5f, TilingScale = square };
                default:
                    return new Rule { TextureId = field ? "Ground037" : "Grass004", Tint = new Color(0.92f, 1f, 0.84f), Smoothness = 0.5f, TilingScale = square };
            }
        }

        private static bool Set(out Rule rule, string textureId, Color tint, float smoothness)
        {
            rule = new Rule { TextureId = textureId, Tint = tint, Smoothness = smoothness, TilingScale = Vector2.one };
            return true;
        }

        private static string StripSuffix(string name)
        {
            const string instance = " (Instance)";
            if (name.EndsWith(instance)) name = name.Substring(0, name.Length - instance.Length);
            if (name.EndsWith("_PBR")) name = name.Substring(0, name.Length - 4);
            return name;
        }
    }
}
