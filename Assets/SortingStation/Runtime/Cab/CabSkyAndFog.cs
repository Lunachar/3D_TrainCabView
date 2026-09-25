using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// Sky, haze and ambient light for the immersive world. The sky shader (SortingStation/Sky)
    /// gets its colours from the time of day: blue days, warm sunrises and sunsets with a glowing
    /// band along the horizon, deep blue nights with stars and, on some nights, the moon. Haze
    /// takes the horizon colour so the far land melts into the sky.
    /// </summary>
    public sealed class CabSkyAndFog
    {
        private static readonly Color DayZenith = new Color(0.18f, 0.38f, 0.74f);
        private static readonly Color DayHorizon = new Color(0.68f, 0.79f, 0.90f);
        private static readonly Color DawnZenith = new Color(0.22f, 0.25f, 0.48f);
        private static readonly Color DawnHorizon = new Color(0.98f, 0.62f, 0.40f);
        private static readonly Color SunsetGlow = new Color(1f, 0.42f, 0.16f);
        private static readonly Color NightZenith = new Color(0.008f, 0.014f, 0.040f);
        private static readonly Color NightHorizon = new Color(0.030f, 0.042f, 0.075f);
        private static readonly Color StormZenith = new Color(0.36f, 0.39f, 0.43f);
        private static readonly Color StormHorizon = new Color(0.52f, 0.55f, 0.58f);
        private static readonly Color TunnelDark = new Color(0.035f, 0.035f, 0.040f);
        private static readonly Color DayAmbient = new Color(0.50f, 0.54f, 0.58f);
        private static readonly Color NightAmbient = new Color(0.030f, 0.036f, 0.060f);

        private readonly Material skybox;
        private readonly float farClip;
        private float environmentKey = -1f;
        private Vector3 sunDirection = Vector3.up;
        private Vector3 moonDirection = Vector3.up;
        private Quaternion worldToScene = Quaternion.identity;
        private float moonAmount;
        private float moonPhase;

        public Material Skybox => skybox;
        public Color HorizonColor { get; private set; } = DayHorizon;
        /// <summary>0 in the country, 1 in a big city: street light glows in the night haze.</summary>
        public float Urban { get; set; }
        /// <summary>0..1 how low and golden the sun is (for the lens glare).</summary>
        public float SunsetAmount { get; private set; }

        public CabSkyAndFog(Camera camera, Light sun, float farClipDistance)
        {
            farClip = farClipDistance;
            skybox = new Material(CabShaders.Skybox) { name = "CabSky" };
            skybox.SetTexture("_CloudTex", CloudNoise());
            RenderSettings.skybox = skybox;
            RenderSettings.sun = sun;
            if (camera != null) camera.clearFlags = CameraClearFlags.Skybox;
            RenderSettings.fog = true;
            // Exponential haze: nearby things stay crisp, the far land fades into the horizon colour.
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            Update(1f, 0f, false, 0f);
        }

        /// <summary>Directions (scene frame, pointing at the body) and the world's turn under the camera.</summary>
        public void SetCelestial(Vector3 sunToward, Vector3 moonToward, Quaternion worldRotation, float moon01, float phase)
        {
            sunDirection = sunToward.normalized;
            moonDirection = moonToward.normalized;
            worldToScene = worldRotation;
            moonAmount = moon01;
            moonPhase = phase;
        }

        /// <param name="sunArc">0 on the horizon and at night, 1 at noon.</param>
        /// <param name="night01">0 in daylight, 1 in full night.</param>
        public void Update(float sunArc, float night01, bool badWeather, float tunnelBlend)
        {
            float day = Mathf.Clamp01(1f - night01);
            // Golden hours: strongest with the sun low but up, fading into twilight.
            float sunset = Mathf.SmoothStep(0f, 1f, day / 0.35f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.42f, sunArc))) * (badWeather ? 0.25f : 1f);
            SunsetAmount = sunset;

            Color zenith = Color.Lerp(DayZenith, DawnZenith, sunset);
            Color horizon = Color.Lerp(DayHorizon, DawnHorizon, sunset);
            Color nightHorizon = Color.Lerp(NightHorizon, new Color(0.17f, 0.12f, 0.09f), Urban);
            // The sky brightens faster than the land at dawn and holds its colour into dusk.
            float skyDay = Mathf.Sqrt(day);
            zenith = Color.Lerp(NightZenith, zenith, skyDay);
            horizon = Color.Lerp(nightHorizon, horizon, skyDay);
            if (badWeather)
            {
                zenith = Color.Lerp(zenith, StormZenith * Mathf.Lerp(0.12f, 1f, day), 0.75f);
                horizon = Color.Lerp(horizon, StormHorizon * Mathf.Lerp(0.15f, 1f, day), 0.7f);
            }
            HorizonColor = horizon;

            Vector3 sunScene = sunDirection;
            skybox.SetVector("_SunDirection", sunScene);
            skybox.SetVector("_MoonDirection", moonDirection);
            skybox.SetMatrix("_SkyRotation", Matrix4x4.Rotate(Quaternion.Inverse(worldToScene)));
            skybox.SetColor("_ZenithColor", zenith);
            skybox.SetColor("_HorizonColor", horizon);
            skybox.SetColor("_SunsetColor", Color.Lerp(SunsetGlow, new Color(1f, 0.6f, 0.35f), 0.3f));
            skybox.SetColor("_SunColor", Color.Lerp(new Color(1f, 0.95f, 0.85f), new Color(1f, 0.55f, 0.25f), sunset));
            skybox.SetFloat("_SunsetAmount", sunset);
            skybox.SetFloat("_SunVisible", badWeather ? 0.15f : Mathf.Clamp01(day * 1.5f) * (sunScene.y > -0.05f ? 1f : 0f));
            skybox.SetFloat("_CloudCover", badWeather ? 0.95f : 0.56f);
            skybox.SetColor("_CloudLight", Color.Lerp(new Color(0.05f, 0.06f, 0.09f), badWeather ? new Color(0.62f, 0.64f, 0.67f) : new Color(0.97f, 0.97f, 0.97f), day));
            skybox.SetColor("_CloudShadow", Color.Lerp(new Color(0.02f, 0.025f, 0.04f), badWeather ? new Color(0.36f, 0.38f, 0.41f) : new Color(0.62f, 0.67f, 0.75f), day));
            skybox.SetFloat("_StarAmount", badWeather ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.95f, night01)) * (1f - Urban * 0.6f));
            skybox.SetFloat("_MoonAmount", badWeather ? 0f : moonAmount * Mathf.InverseLerp(0.3f, 0.8f, night01));
            skybox.SetFloat("_MoonPhase", moonPhase);
            skybox.SetFloat("_SkyTime", Time.time);
            skybox.SetFloat("_Brightness", (badWeather ? 0.9f : 1f) * (1f - tunnelBlend * 0.97f));

            // Bad-weather haze stays a little darker than the sky, so snow on the ground and the
            // haze do not melt into one white sheet.
            RenderSettings.fogColor = Color.Lerp(badWeather ? horizon * 0.88f : horizon, TunnelDark, tunnelBlend);
            // About 90 % haze at the far clip on a clear day, much thicker in rain, snow or fog.
            float clearDensity = 1.5f / Mathf.Max(100f, farClip);
            RenderSettings.fogDensity = Mathf.Lerp(badWeather ? clearDensity * 2.8f : clearDensity, 0.02f, tunnelBlend);
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = farClip * 0.94f;

            Color nightAmbient = Color.Lerp(NightAmbient, new Color(0.12f, 0.11f, 0.11f), Urban);
            Color ambient = Color.Lerp(nightAmbient, Color.Lerp(DayAmbient, new Color(0.62f, 0.50f, 0.44f), sunset * 0.6f), day) * (badWeather ? 0.82f : 1f);
            Color finalAmbient = Color.Lerp(ambient, TunnelDark * 1.2f, tunnelBlend);
            RenderSettings.ambientLight = finalAmbient;
            // URP lights with the ambient probe and reflects the default reflection cubemap; both
            // are captured once at load (in daylight), so refresh them as the light changes or
            // night-time ground would still shine with the day sky.
            UnityEngine.Rendering.SphericalHarmonicsL2 probe = default;
            probe.AddAmbientLight(finalAmbient);
            RenderSettings.ambientProbe = probe;
            RenderSettings.reflectionIntensity = Mathf.Lerp(0.08f, 1f, day) * (badWeather ? 0.7f : 1f) * (1f - tunnelBlend * 0.92f);
            float key = Mathf.Round(day * 10f) + (badWeather ? 20f : 0f) + Mathf.Round(tunnelBlend * 2f) * 40f;
            if (!Mathf.Approximately(key, environmentKey) && Application.isPlaying)
            {
                environmentKey = key;
                DynamicGI.UpdateEnvironment();
            }
        }

        /// <summary>Seamless cloud noise (Perlin sampled on a torus).</summary>
        private static Texture2D CloudNoise()
        {
            const int size = 256;
            Texture2D texture = new Texture2D(size, size, TextureFormat.R8, true) { name = "SkyCloudNoise", wrapMode = TextureWrapMode.Repeat };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = x / (float)size * Mathf.PI * 2f, b = y / (float)size * Mathf.PI * 2f;
                    float n = 0f, amplitude = 0.55f, radius = 1.2f;
                    for (int octave = 0; octave < 4; octave++)
                    {
                        n += amplitude * Mathf.PerlinNoise(10f + octave * 17f + Mathf.Cos(a) * radius + Mathf.Sin(b) * radius * 0.37f,
                            30f + octave * 13f + Mathf.Sin(a) * radius + Mathf.Cos(b) * radius);
                        amplitude *= 0.5f;
                        radius *= 2f;
                    }
                    pixels[y * size + x] = new Color(n, n, n, 1f);
                }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            return texture;
        }

        public void Dispose()
        {
            if (RenderSettings.skybox == skybox) RenderSettings.skybox = null;
            RenderSettings.fog = false;
            if (skybox != null) Object.Destroy(skybox);
        }
    }
}
