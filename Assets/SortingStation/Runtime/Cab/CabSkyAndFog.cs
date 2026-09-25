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
        private static readonly Color NightAmbient = new Color(0.055f, 0.065f, 0.10f);

        private readonly Material skybox;
        private readonly float farClip;

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
            RenderSettings.fogMode = FogMode.Linear;
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
            skybox.SetFloat("_AtmosphereThickness", badWeather ? 2.4f : Mathf.Lerp(1.7f, 1.0f, sunArc));
            skybox.SetColor("_GroundColor", horizon);

            // Bad-weather fog stays a little darker than the sky, so snow on the ground and the
            // fog do not melt into one white sheet.
            RenderSettings.fogColor = Color.Lerp(badWeather ? horizon * 0.88f : horizon, TunnelDark, tunnelBlend);
            RenderSettings.fogStartDistance = badWeather ? 35f : 60f;
            RenderSettings.fogEndDistance = badWeather ? farClip * 0.8f : farClip * 0.94f;

            Color ambient = Color.Lerp(NightAmbient, DayAmbient, day) * (badWeather ? 0.82f : 1f);
            RenderSettings.ambientLight = Color.Lerp(ambient, TunnelDark * 2.2f, tunnelBlend);
        }

        public void Dispose()
        {
            if (RenderSettings.skybox == skybox) RenderSettings.skybox = null;
            RenderSettings.fog = false;
            if (skybox != null) Object.Destroy(skybox);
        }
    }
}
