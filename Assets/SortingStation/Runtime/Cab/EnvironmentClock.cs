using UnityEngine;

namespace SortingStation
{
    public sealed class EnvironmentClock
    {
        private readonly CabEnvironmentCatalog catalog;
        public float Time01 { get; private set; }

        public EnvironmentClock(CabEnvironmentCatalog value)
        {
            catalog = value;
            Time01 = value != null ? value.StartTime01 : 0.30f;
        }

        /// <summary>Smoke-capture hook: jump to a time of day and stop the clock there.</summary>
        public void Freeze(float time01)
        {
            Time01 = Mathf.Repeat(time01, 1f);
            frozen = true;
        }

        private bool frozen;

        public void Step(float unscaledDeltaTime)
        {
            if (frozen) return;
            float duration = catalog != null ? catalog.DayCycleSeconds : 720f;
            Time01 = Mathf.Repeat(Time01 + Mathf.Max(0f, unscaledDeltaTime) / duration, 1f);
        }

        public DayPhase Phase
        {
            get
            {
                if (Time01 < 0.18f) return DayPhase.Dawn;
                if (Time01 < 0.62f) return DayPhase.Day;
                if (Time01 < 0.78f) return DayPhase.Sunset;
                return DayPhase.Night;
            }
        }

        public float Night01 => Mathf.Clamp01(1f - Daylight01);
        public float Daylight01 => Daylight(Time01);

        // Shared by the 2D overlays and the 3D sky so both show the same sun and moon.
        private const float Sunrise = 0.08f;
        private const float Sunset = 0.86f;

        public static float Daylight(float time01)
        {
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Sunrise, 0.25f, time01));
            float set = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, Sunset, time01));
            return Mathf.Min(rise, set);
        }

        /// <summary>0 at sunrise, 1 at sunset; clamped outside the day.</summary>
        public static float SunTravel(float time01) => Mathf.Clamp01(Mathf.InverseLerp(Sunrise, Sunset, time01));

        /// <summary>Height of the sun's path: 0 on the horizon and all night, 1 at noon.</summary>
        public static float SunArc(float time01) => Mathf.Max(0f, Mathf.Sin(SunTravel(time01) * Mathf.PI));

        /// <summary>0 at sunset, 1 at the next sunrise.</summary>
        public static float MoonTravel(float time01) =>
            Mathf.Clamp01(Mathf.Repeat(time01 - Sunset, 1f) / (1f - Sunset + Sunrise));

        public Color SkyColor
        {
            get
            {
                if (catalog == null) return Color.cyan;
                if (Time01 < 0.18f) return Color.Lerp(catalog.NightSky, catalog.DawnSky, Mathf.InverseLerp(0f, 0.18f, Time01));
                if (Time01 < 0.32f) return Color.Lerp(catalog.DawnSky, catalog.DaySky, Mathf.InverseLerp(0.18f, 0.32f, Time01));
                if (Time01 < 0.62f) return catalog.DaySky;
                if (Time01 < 0.78f) return Color.Lerp(catalog.DaySky, catalog.SunsetSky, Mathf.InverseLerp(0.62f, 0.78f, Time01));
                return Color.Lerp(catalog.SunsetSky, catalog.NightSky, Mathf.InverseLerp(0.78f, 1f, Time01));
            }
        }
    }
}
