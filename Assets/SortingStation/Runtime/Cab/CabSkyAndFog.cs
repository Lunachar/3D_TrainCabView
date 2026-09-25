using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// Sky, fog and ambient light for the immersive world. A procedural skybox follows the sun
    /// light; linear fog in the horizon colour softens the distance (and hides the end of the
    /// route loop); ambient light follows the time of day, bad weather and the tunnel.
    /// </summary>
    public sealed class CabSkyAndFog
    {
        private static readonly Color DayHorizon = new Color(0.70f, 0.79f, 0.87f);
        private static readonly Color SunsetHorizon = new Color(0.86f, 0.62f, 0.44f);
        private static readonly Color NightHorizon = new Color(0.030f, 0.040f, 0.070f);
        private static readonly Color StormHorizon = new Color(0.50f, 0.54f, 0.58f);
        private static readonly Color TunnelDark = new Color(0.035f, 0.035f, 0.040f);
        private static readonly Color DayAmbient = new Color(0.50f, 0.54f, 0.58f);
        private static readonly Color NightAmbient = new Color(0.030f, 0.036f, 0.060f);

        private readonly Material skybox;
        private readonly float farClip;
        private float environmentKey = -1f;

        public Material Skybox => skybox;
        public Color HorizonColor { get; private set; } = DayHorizon;

        public CabSkyAndFog(Camera camera, Light sun, float farClipDistance)
        {
            farClip = farClipDistance;
            skybox = new Material(CabShaders.Skybox) { name = "CabProceduralSky" };
            skybox.SetFloat("_SunDisk", 2f);
            skybox.SetFloat("_SunSize", 0.045f);
            skybox.SetFloat("_SunSizeConvergence", 6f);
            skybox.SetColor("_SkyTint", new Color(0.52f, 0.54f, 0.56f));
            RenderSettings.skybox = skybox;
            RenderSettings.sun = sun;
            if (camera != null) camera.clearFlags = CameraClearFlags.Skybox;
            RenderSettings.fog = true;
            // Exponential haze: nearby things stay crisp, the far land fades into the horizon colour.
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            Update(1f, 0f, false, 0f);
        }

        /// <param name="sunArc">0 on the horizon and at night, 1 at noon.</param>
        /// <param name="night01">0 in daylight, 1 in full night.</param>
        public void Update(float sunArc, float night01, bool badWeather, float tunnelBlend)
        {
            float day = Mathf.Clamp01(1f - night01);
            // Low sun warms the horizon; full night fades it to deep blue.
            Color horizon = Color.Lerp(SunsetHorizon, DayHorizon, Mathf.SmoothStep(0f, 1f, sunArc * 2.2f));
            horizon = Color.Lerp(NightHorizon, horizon, day);
            if (badWeather) horizon = Color.Lerp(horizon, StormHorizon * Mathf.Lerp(0.18f, 1f, day), 0.6f);
            HorizonColor = horizon;

            float exposure = Mathf.Lerp(0.06f, 1.25f, day) * (badWeather ? 0.62f : 1f) * (1f - tunnelBlend);
            skybox.SetFloat("_Exposure", exposure);
            // Overcast: a thin, grey atmosphere (a thick one turns the gamma-space sky orange).
            skybox.SetFloat("_AtmosphereThickness", badWeather ? 0.55f : Mathf.Lerp(1.7f, 1.0f, sunArc));
            skybox.SetColor("_SkyTint", badWeather ? new Color(0.42f, 0.44f, 0.46f) : new Color(0.52f, 0.54f, 0.56f));
            skybox.SetFloat("_SunDisk", badWeather ? 0f : 2f);
            skybox.SetColor("_GroundColor", horizon);

            // Bad-weather fog stays a little darker than the sky, so snow on the ground and the
            // fog do not melt into one white sheet.
            RenderSettings.fogColor = Color.Lerp(badWeather ? horizon * 0.88f : horizon, TunnelDark, tunnelBlend);
            RenderSettings.fogStartDistance = badWeather ? 35f : 60f;
            RenderSettings.fogEndDistance = badWeather ? farClip * 0.8f : farClip * 0.94f;
            // About 90 % haze at the far clip on a clear day, much thicker in rain, snow or fog.
            float clearDensity = 1.5f / Mathf.Max(100f, farClip);
            RenderSettings.fogDensity = Mathf.Lerp(badWeather ? clearDensity * 2.8f : clearDensity, 0.02f, tunnelBlend);

            Color ambient = Color.Lerp(NightAmbient, DayAmbient, day) * (badWeather ? 0.82f : 1f);
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

        public void Dispose()
        {
            if (RenderSettings.skybox == skybox) RenderSettings.skybox = null;
            RenderSettings.fog = false;
            if (skybox != null) Object.Destroy(skybox);
        }
    }
}
