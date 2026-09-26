using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SortingStation
{
    /// <summary>
    /// Shared materials of the endless world. One instance per surface keeps the SRP Batcher
    /// happy; the few that change with the time of day (windows, lamps) are updated in one place.
    /// </summary>
    public static class WorldMaterials
    {
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static readonly List<Material> NightWindows = new List<Material>();
        private static readonly List<Material> Lamps = new List<Material>();
        private static readonly List<(Material material, Color colour, float day)> Glows = new List<(Material, Color, float)>();
        private static Material lightPool;
        private static Texture2D poolTexture;

        public static readonly Color[] NeonColours =
        {
            new Color(1f, 0.18f, 0.55f), new Color(0.15f, 0.85f, 1f), new Color(1f, 0.82f, 0.15f),
            new Color(0.35f, 1f, 0.35f), new Color(1f, 0.35f, 0.12f), new Color(0.7f, 0.35f, 1f), new Color(1f, 1f, 1f)
        };
        private static Material terrain;
        private static Texture2D macro;
        private static float night = -1f;

        public static readonly Color[] HousePaints =
        {
            new Color(0.62f, 0.72f, 0.52f), new Color(0.78f, 0.66f, 0.42f), new Color(0.55f, 0.66f, 0.78f),
            new Color(0.80f, 0.52f, 0.40f), new Color(0.86f, 0.82f, 0.66f), new Color(0.58f, 0.44f, 0.32f)
        };

        public static readonly Color[] RoofPaints =
        {
            new Color(0.30f, 0.45f, 0.34f), new Color(0.55f, 0.20f, 0.16f), new Color(0.36f, 0.38f, 0.42f),
            new Color(0.24f, 0.33f, 0.52f), new Color(0.50f, 0.46f, 0.40f)
        };

        public static Material Get(string textureId, Color tint, float smoothness = 1f) => CabPbrMaterials.Get(textureId, tint, smoothness);
        public static Material Plain(string name, Color color, float smoothness = 0.3f, float metallic = 0f) => CabPbrMaterials.Plain(name, color, smoothness, metallic);

        public static Material Ballast => Get("Gravel022", new Color(0.78f, 0.75f, 0.70f), 0.6f);
        public static Material ConcreteSleeper => Get("Concrete034", new Color(0.72f, 0.71f, 0.67f), 0.6f);
        public static Material WoodSleeper => Get("Planks021", new Color(0.42f, 0.30f, 0.20f), 0.5f);
        public static Material Rail => Plain("WorldRail", new Color(0.24f, 0.22f, 0.20f), 0.35f, 0.6f);
        public static Material RailHead => GlintMaterial("RailHead", new Color(0.50f, 0.51f, 0.52f), 45f);
        public static Material Concrete => Get("Concrete034", new Color(0.80f, 0.79f, 0.75f), 0.6f);
        public static Material DarkConcrete => Get("Concrete034", new Color(0.48f, 0.48f, 0.46f), 0.6f);
        public static Material Steel => Get("Metal046A", new Color(0.46f, 0.48f, 0.50f), 0.7f);
        public static Material Wire => GlintMaterial("Wire", new Color(0.13f, 0.11f, 0.09f), 70f);

        private static readonly List<Material> Glints = new List<Material>();

        /// <summary>Thin metal that flashes in the sun along its length (wires, rail heads).</summary>
        public static Material GlintMaterial(string name, Color colour, float sharpness)
        {
            string key = "glint|" + name;
            if (Cache.TryGetValue(key, out Material cached) && cached != null) return cached;
            Material material = new Material(CabShaders.Glint) { name = "WorldGlint_" + name };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_GlintSharpness", sharpness);
            material.SetFloat("_GlintStrength", sunGlint);
            // Only the rail heads carry the headlight sheen; wires are above the beam.
            material.SetFloat("_GlintStrength2", name == "RailHead" ? 1f : 0.3f);
            Cache[key] = material;
            Glints.Add(material);
            return material;
        }

        private static float sunGlint = 1f;

        /// <summary>How strongly the sun glints on metal now (0 at night, in the tunnel or overcast).</summary>
        public static void SetSunGlint(float strength, Vector3 trackAxisWorld)
        {
            sunGlint = strength;
            foreach (Material material in Glints)
            {
                if (material == null) continue;
                material.SetFloat("_GlintStrength", strength);
                material.SetVector("_GlintAxis", trackAxisWorld);
            }
        }
        public static Material Asphalt => Get("Asphalt012", new Color(0.72f, 0.72f, 0.72f), 0.5f);
        public static Material Gravel => Get("Gravel040", new Color(0.80f, 0.78f, 0.72f), 0.5f);
        public static Material Rock => Get("Rock030", new Color(0.72f, 0.70f, 0.66f), 0.6f);
        public static Material TunnelLining => Get("Rock035", new Color(0.52f, 0.50f, 0.47f), 0.5f);
        public static Material Planks => Get("Planks021", new Color(0.62f, 0.50f, 0.38f), 0.5f);
        public static Material Brick => Built(WorldTextures.Surface.Bricks, new Color(0.95f, 0.92f, 0.9f), 0.15f);
        public static Material Glass => Plain("WorldWindowGlass", new Color(0.10f, 0.13f, 0.15f), 0.9f, 0.1f);
        public static Material WindowFrame => Plain("WorldWindowFrame", new Color(0.88f, 0.88f, 0.84f), 0.4f);
        public static Material SafetyLine => Plain("WorldPlatformEdge", new Color(0.92f, 0.76f, 0.16f), 0.3f);
        public static Material Hay => Plain("WorldHay", new Color(0.78f, 0.66f, 0.36f), 0.1f);
        public static Material Water => Plain("WorldWater", new Color(0.10f, 0.20f, 0.24f), 0.93f, 0.0f);
        public static Material SignBoard => Plain("WorldSignBoard", new Color(0.08f, 0.22f, 0.52f), 0.4f);

        public static Material Siding(int variant) => Built(WorldTextures.Surface.Siding, HousePaints[Mathf.Abs(variant) % HousePaints.Length], 0.2f);
        public static Material Plaster(int variant) => Built(WorldTextures.Surface.Plaster, Color.Lerp(HousePaints[Mathf.Abs(variant) % HousePaints.Length], Color.white, 0.45f), 0.1f);
        public static Material Roof(int variant) => variant % 3 == 0
            ? Built(WorldTextures.Surface.RoofTiles, Color.Lerp(RoofPaints[Mathf.Abs(variant) % RoofPaints.Length], new Color(0.7f, 0.3f, 0.2f), 0.5f), 0.25f)
            : Built(WorldTextures.Surface.Corrugated, RoofPaints[Mathf.Abs(variant) % RoofPaints.Length], 0.45f, 0.3f);
        public static Material ShedMetal(int variant) => Built(WorldTextures.Surface.Corrugated,
            Color.Lerp(RoofPaints[Mathf.Abs(variant) % RoofPaints.Length], new Color(0.8f, 0.8f, 0.78f), 0.5f), 0.45f, 0.3f);

        /// <summary>Material on a procedural building surface; builders lay its UVs at 2 m per unit.</summary>
        private static Material Built(WorldTextures.Surface surface, Color tint, float smoothness, float metallic = 0f)
        {
            string key = "built|" + surface + "|" + ColorUtility.ToHtmlStringRGB(tint);
            if (Cache.TryGetValue(key, out Material cached) && cached != null) return cached;
            Material material = CabShaders.CreateLit("World" + surface + "_" + ColorUtility.ToHtmlStringRGB(tint), tint, smoothness, metallic);
            material.mainTexture = WorldTextures.GetAlbedo(surface);
            material.mainTextureScale = Vector2.one * (2f / WorldTextures.TileMetres(surface));
            if (material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", WorldTextures.GetNormal(surface));
                material.EnableKeyword("_NORMALMAP");
            }
            Cache[key] = material;
            return material;
        }

        /// <summary>Emissive lamp glass that glows only at night.</summary>
        public static Material LampGlow
        {
            get
            {
                if (Cache.TryGetValue("lamp", out Material cached) && cached != null) return cached;
                Material material = CabPbrMaterials.Emissive("WorldLampGlow", new Color(0.95f, 0.92f, 0.82f), Color.black);
                Cache["lamp"] = material;
                Lamps.Add(material);
                ApplyNight();
                return material;
            }
        }

        /// <summary>Self-lit colour (neon tubes, shop windows, car lights): faint by day, bright at night.</summary>
        public static Material Glow(string name, Color colour, float dayLevel = 0.25f)
        {
            string key = "glow|" + name + "|" + ColorUtility.ToHtmlStringRGB(colour);
            if (Cache.TryGetValue(key, out Material cached) && cached != null) return cached;
            Material material = CabPbrMaterials.Emissive("WorldGlow_" + name, colour * 0.6f, colour * dayLevel);
            Cache[key] = material;
            Glows.Add((material, colour, dayLevel));
            if (night >= 0f) material.SetColor("_EmissionColor", colour * Mathf.Lerp(dayLevel, 2.2f, Lit(night)));
            return material;
        }

        public static Material Neon(int index) => Glow("Neon" + index, NeonColours[Mathf.Abs(index) % NeonColours.Length], 0.35f);
        public static Material ShopWindow => Glow("ShopWindow", new Color(1f, 0.86f, 0.62f), 0.12f);
        public static Material Headlamp => Glow("Headlamp", new Color(1f, 0.96f, 0.85f), 0.2f);
        public static Material TailLamp => Glow("TailLamp", new Color(1f, 0.08f, 0.05f), 0.3f);

        /// <summary>
        /// A soft pool of lamplight on the ground: an additive decal that costs nothing like a
        /// real light, so streets and platforms can have dozens of lit lamps at night.
        /// </summary>
        public static Material LightPool
        {
            get
            {
                if (lightPool != null) return lightPool;
                if (poolTexture == null)
                {
                    const int size = 64;
                    poolTexture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "LightPool", wrapMode = TextureWrapMode.Clamp };
                    Color[] pixels = new Color[size * size];
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                            float f = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                            f = f * f * (3f - 2f * f);
                            pixels[y * size + x] = new Color(f, f, f, f);
                        }
                    poolTexture.SetPixels(pixels);
                    poolTexture.Apply(true, false);
                }
                lightPool = new Material(CabShaders.Additive) { name = "WorldLightPool", mainTexture = poolTexture };
                SetTint(lightPool, Color.black);
                ApplyNight();
                return lightPool;
            }
        }

        private static void SetTint(Material material, Color colour)
        {
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", colour);
            material.color = colour;
        }

        private static float Lit(float amount) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.8f, amount));

        /// <summary>Town facade: a painted panel wall with a grid of windows; some light up at night.</summary>
        public static Material Facade(int variant)
        {
            variant = Mathf.Abs(variant) % 4;
            string key = "facade" + variant;
            if (Cache.TryGetValue(key, out Material cached) && cached != null) return cached;
            Color wall = variant == 0 ? new Color(0.80f, 0.78f, 0.72f) : variant == 1 ? new Color(0.78f, 0.66f, 0.52f)
                : variant == 2 ? new Color(0.70f, 0.74f, 0.76f) : new Color(0.86f, 0.80f, 0.64f);
            Texture2D albedo = BuildFacadeTexture(wall, variant, false);
            Texture2D glow = BuildFacadeTexture(wall, variant, true);
            Material material = CabShaders.CreateLit("TownFacade" + variant, Color.white, 0.25f);
            material.mainTexture = albedo;
            material.EnableKeyword("_EMISSION");
            material.SetTexture("_EmissionMap", glow);
            material.SetColor("_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            Cache[key] = material;
            NightWindows.Add(material);
            ApplyNight();
            return material;
        }

        /// <summary>A facade texture tile covers 6 windows × 4 floors: 18 m × 12 m.</summary>
        public const float FacadeTileWidth = 18f;
        public const float FacadeTileHeight = 12f;

        private static Texture2D BuildFacadeTexture(Color wall, int variant, bool glow)
        {
            const int size = 256;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = (glow ? "FacadeGlow" : "Facade") + variant, wrapMode = TextureWrapMode.Repeat };
            System.Random random = new System.Random(811 + variant);
            Color[] pixels = new Color[size * size];
            const int cellW = size / 6;
            const int cellH = size / 4;
            bool[] lit = new bool[24];
            for (int i = 0; i < lit.Length; i++) lit[i] = random.NextDouble() < 0.5;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int cx = x / cellW, cy = y / cellH;
                    int lx = x % cellW, ly = y % cellH;
                    bool window = lx > cellW * 0.22f && lx < cellW * 0.78f && ly > cellH * 0.30f && ly < cellH * 0.80f;
                    bool frame = window && (lx < cellW * 0.26f || lx > cellW * 0.74f || ly < cellH * 0.34f || ly > cellH * 0.76f || Mathf.Abs(lx - cellW * 0.5f) < 1.2f);
                    bool slab = ly < 3;
                    float grain = 0.93f + 0.07f * Mathf.PerlinNoise(x * 0.21f, y * 0.21f);
                    Color c;
                    if (glow)
                    {
                        bool on = window && !frame && lit[(cy * 6 + cx) % lit.Length];
                        Color warm = random.NextDouble() < 0.02 ? new Color(0.7f, 0.8f, 1f) : new Color(1f, 0.78f, 0.45f);
                        c = on ? warm * (0.75f + 0.25f * Mathf.PerlinNoise(cx * 3.1f, cy * 2.7f)) : Color.black;
                    }
                    else if (frame) c = new Color(0.86f, 0.86f, 0.82f);
                    else if (window) c = Color.Lerp(new Color(0.10f, 0.14f, 0.17f), new Color(0.32f, 0.40f, 0.46f), ly / (float)cellH);
                    else if (slab) c = wall * 0.72f;
                    else c = wall * grain;
                    c.a = 1f;
                    pixels[y * size + x] = c;
                }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            return texture;
        }

        public static Material Terrain(SeasonType season)
        {
            if (terrain == null)
            {
                terrain = new Material(CabShaders.Terrain) { name = "WorldTerrain" };
                terrain.SetTexture("_SoilTex", Load("Ground037_Color"));
                terrain.SetTexture("_ForestTex", Load("Ground037_Color"));
                terrain.SetTexture("_GravelTex", Load("Gravel040_Color"));
                terrain.SetTexture("_RockTex", Load("Rock030_Color"));
                terrain.SetTexture("_MacroTex", Macro());
            }
            bool winter = season == SeasonType.Winter;
            terrain.SetTexture("_GrassTex", Load(winter ? "Snow004_Color" : "Grass004_Color"));
            terrain.SetColor("_GrassTint", winter ? new Color(0.95f, 0.97f, 1f) :
                season == SeasonType.Autumn ? new Color(0.95f, 0.82f, 0.55f) :
                season == SeasonType.Spring ? new Color(0.85f, 1.02f, 0.72f) : new Color(0.82f, 0.95f, 0.70f));
            terrain.SetColor("_CropTint", winter ? new Color(0.88f, 0.90f, 0.93f) :
                season == SeasonType.Spring ? new Color(0.46f, 0.62f, 0.30f) : new Color(0.84f, 0.70f, 0.36f));
            terrain.SetColor("_SoilTint", winter ? new Color(0.80f, 0.82f, 0.86f) : new Color(0.62f, 0.50f, 0.40f));
            terrain.SetColor("_ForestTint", winter ? new Color(0.78f, 0.80f, 0.84f) :
                season == SeasonType.Autumn ? new Color(0.72f, 0.46f, 0.26f) : new Color(0.42f, 0.36f, 0.26f));
            terrain.SetColor("_GravelTint", winter ? new Color(0.80f, 0.82f, 0.85f) : new Color(0.76f, 0.73f, 0.68f));
            terrain.SetColor("_RockTint", winter ? new Color(0.78f, 0.80f, 0.84f) : new Color(0.70f, 0.68f, 0.64f));
            return terrain;
        }

        /// <summary>Weather on the ground: wet after rain, white while snow settles.</summary>
        public static void SetGroundWeather(float wetness, float snowCover)
        {
            if (terrain == null) return;
            terrain.SetFloat("_Wetness", wetness);
            terrain.SetFloat("_SnowCover", snowCover);
        }

        /// <summary>Night 0..1: lights windows and lamp glass.</summary>
        public static void SetNight(float amount)
        {
            if (Mathf.Abs(amount - night) < 0.02f) return;
            night = amount;
            ApplyNight();
        }

        /// <summary>Applies the current night level to every glowing material (also ones created later).</summary>
        private static void ApplyNight()
        {
            float amount = Mathf.Max(0f, night);
            float lit = Lit(amount);
            for (int i = 0; i < Glows.Count; i++)
                if (Glows[i].material != null) Glows[i].material.SetColor("_EmissionColor", Glows[i].colour * Mathf.Lerp(Glows[i].day, 2.2f, lit));
            if (lightPool != null) SetTint(lightPool, new Color(0.55f, 0.42f, 0.26f, 0.5f) * lit);
            for (int i = 0; i < NightWindows.Count; i++)
                if (NightWindows[i] != null) NightWindows[i].SetColor("_EmissionColor", Color.white * (lit * 2.6f));
            for (int i = 0; i < Lamps.Count; i++)
                if (Lamps[i] != null) Lamps[i].SetColor("_EmissionColor", new Color(1f, 0.82f, 0.52f) * (0.08f + lit * 1.6f));
        }

        private static Texture2D Load(string name) => Resources.Load<Texture2D>("Pbr/" + name);

        private static Texture2D Macro()
        {
            if (macro != null) return macro;
            const int size = 128;
            macro = new Texture2D(size, size, TextureFormat.R8, true) { name = "TerrainMacro", wrapMode = TextureWrapMode.Repeat };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Tileable noise: sample on a torus.
                    float u = x / (float)size * Mathf.PI * 2f, v = y / (float)size * Mathf.PI * 2f;
                    float n = Mathf.PerlinNoise(10f + Mathf.Cos(u) * 1.6f + Mathf.Sin(v) * 0.3f, 20f + Mathf.Sin(u) * 1.6f + Mathf.Cos(v) * 1.6f) * 0.6f +
                              Mathf.PerlinNoise(30f + Mathf.Cos(u) * 4f, 40f + Mathf.Sin(v) * 4f + Mathf.Sin(u) * 2f) * 0.4f;
                    pixels[y * size + x] = new Color(n, n, n, 1f);
                }
            macro.SetPixels(pixels);
            macro.Apply(true, false);
            return macro;
        }
    }
}
