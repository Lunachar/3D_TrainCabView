using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// Shaders for materials the cab builds from code. They come from CabWorld3DSettings, a
    /// Resources asset, so player builds always contain them; the name lookup is only a fallback
    /// for the editor and for tests that run before the settings asset exists.
    /// </summary>
    public static class CabShaders
    {
        private const string SettingsResource = "Configuration/CabWorld3DSettings";
        private static CabWorld3DSettings settings;
        private static bool settingsLoaded;

        public static Shader Lit => Resolve(Settings != null ? Settings.LitShader : null, "Universal Render Pipeline/Lit", "Standard");
        public static Shader UnlitTexture => Resolve(Settings != null ? Settings.UnlitTextureShader : null, "Unlit/Texture");
        public static Shader UnlitTransparent => Resolve(Settings != null ? Settings.UnlitTransparentShader : null, "Unlit/Transparent");
        public static Shader Sprite => Resolve(Settings != null ? Settings.SpriteShader : null, "Sprites/Default");
        public static Shader Additive => Resolve(Settings != null ? Settings.AdditiveShader : null, "Legacy Shaders/Particles/Additive");
        public static Shader RainWiper => Resolve(Settings != null ? Settings.RainWiperShader : null, "SortingStation/UI/RainWiper");
        public static Shader MountainFeather => Resolve(Settings != null ? Settings.MountainFeatherShader : null, "SortingStation/UI/MountainFeather");
        public static Shader Skybox => Resolve(Settings != null ? Settings.SkyboxShader : null, "SortingStation/Sky");
        public static Shader ImpostorBake => Resolve(Settings != null ? Settings.ImpostorBakeShader : null, "SortingStation/ImpostorBake");
        public static Shader Glint => Resolve(Settings != null ? Settings.GlintShader : null, "SortingStation/Glint");
        public static Shader Terrain => Resolve(Settings != null ? Settings.TerrainShader : null, "SortingStation/TerrainBlend");
        public static Shader WorldText => Resolve(Settings != null ? Settings.WorldTextShader : null, "SortingStation/WorldText");

        public static Material CreateLit(string name, Color color, float smoothness = 0.12f, float metallic = 0f)
        {
            Material material = new Material(Lit) { name = name, color = color };
            SetSmoothness(material, smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            return material;
        }

        /// <summary>URP Lit calls smoothness "_Smoothness"; the Built-in Standard shader calls it "_Glossiness".</summary>
        public static void SetSmoothness(Material material, float smoothness)
        {
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        }

        private static CabWorld3DSettings Settings
        {
            get
            {
                if (!settingsLoaded)
                {
                    settings = Resources.Load<CabWorld3DSettings>(SettingsResource);
                    settingsLoaded = true;
                }
                return settings;
            }
        }

        private static Shader Resolve(Shader configured, string name, string fallbackName = null)
        {
            if (configured != null) return configured;
            Shader shader = Shader.Find(name);
            if (shader == null && fallbackName != null) shader = Shader.Find(fallbackName);
            return shader;
        }
    }
}
