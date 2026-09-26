using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// Drops and snowflakes on the outside of the windscreen (SortingStation/WindscreenWeather),
    /// cleared where the wiper blades pass.
    /// </summary>
    public sealed class CabWindscreenWeather : MonoBehaviour
    {
        private Material material;
        private float rain;
        private float snow;
        private float targetRain;
        private float targetSnow;
        private bool wipers;

        public void Configure(Material weatherMaterial) => material = weatherMaterial;

        private WeatherType weatherType;
        private float weatherAmount;
        private bool shelter;

        public void SetWeather(WeatherType weather, float intensity)
        {
            weatherType = weather;
            weatherAmount = Mathf.Clamp01(intensity);
            Retarget();
        }

        /// <summary>In a tunnel no new drops land; the ones on the glass stay until wiped or dried.</summary>
        public void SetSheltered(bool sheltered)
        {
            if (shelter == sheltered) return;
            shelter = sheltered;
            Retarget();
        }

        private void Retarget()
        {
            float amount = shelter ? Mathf.Min(rain, weatherAmount) : weatherAmount;
            targetRain = weatherType == WeatherType.Rain ? amount : 0f;
            targetSnow = weatherType == WeatherType.Snow ? (shelter ? Mathf.Min(snow, weatherAmount) : weatherAmount) : 0f;
        }

        public void SetWipers(bool on) => wipers = on;

        /// <summary>Previews: start from dry glass instead of waiting for the last shower to dry.</summary>
        public void Dry()
        {
            rain = targetRain;
            snow = targetSnow;
        }

        private void Update()
        {
            if (material == null) return;
            // Glass wets gradually and dries slowly after the rain stops.
            rain = Mathf.MoveTowards(rain, targetRain, Time.deltaTime * (targetRain > rain ? 0.2f : 0.08f));
            snow = Mathf.MoveTowards(snow, targetSnow, Time.deltaTime * (targetSnow > snow ? 0.12f : 0.04f));
            material.SetFloat("_Rain", rain);
            material.SetFloat("_Snow", snow);
            material.SetFloat("_WipersOn", wipers ? 1f : 0f);
            material.SetFloat("_WiperTime", Time.unscaledTime);
            Color ambient = RenderSettings.ambientLight;
            float level = Mathf.Clamp(ambient.maxColorComponent * 1.6f + 0.1f, 0.15f, 1f);
            material.SetColor("_Light", new Color(level, level, level * 1.03f, 1f));
        }
    }

    /// <summary>
    /// Rain and snow around the cab: particles in the scene frame that fall and rush toward the
    /// cab as fast as the train moves, and vanish before they would pass through the windscreen.
    /// No precipitation in tunnels.
    /// </summary>
    public sealed class CabPrecipitation : MonoBehaviour
    {
        public const int WeatherLayer = 28;
        private ParticleSystem system;
        private ParticleSystemRenderer particleRenderer;
        private Material rainMaterial;
        private Material snowMaterial;
        private ParticleSystem.Particle[] particles = new ParticleSystem.Particle[4000];
        private WeatherType weather = WeatherType.Clear;
        private float intensity;
        private float trainSpeed;
        private float tunnel;

        public int AliveCount => system != null ? system.particleCount : 0;

        private void Awake()
        {
            GameObject host = new GameObject("Precipitation");
            host.transform.SetParent(transform, false);
            host.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            // Its own layer: the rear-view mirror cameras leave the weather out.
            host.layer = WeatherLayer;
            system = host.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = 4000;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = 2.2f;
            main.startSpeed = 0f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 1f, 70f);
            shape.position = new Vector3(0f, 16f, 34f);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            particleRenderer = host.GetComponent<ParticleSystemRenderer>();
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            rainMaterial = new Material(CabShaders.Additive) { name = "RainStreak", mainTexture = Streak() };
            snowMaterial = new Material(CabShaders.Additive) { name = "SnowFlake", mainTexture = Flake() };
            system.Play();
        }

        public void SetWeather(WeatherType type, float amount)
        {
            weather = type;
            intensity = Mathf.Clamp01(amount);
        }

        public void SetMotion(float metresPerSecond, float tunnelBlend)
        {
            trainSpeed = Mathf.Max(0f, metresPerSecond);
            tunnel = tunnelBlend;
        }

        private void LateUpdate()
        {
            if (system == null) return;
            bool raining = weather == WeatherType.Rain, snowing = weather == WeatherType.Snow;
            float amount = (raining || snowing) ? intensity * (1f - tunnel) : 0f;
            ParticleSystem.MainModule main = system.main;
            ParticleSystem.EmissionModule emission = system.emission;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            float fall = raining ? 11f : 1.4f;
            // In the particles' frame the air rushes at the cab as fast as the train goes.
            velocity.x = new ParticleSystem.MinMaxCurve(snowing ? -0.8f : -0.4f, snowing ? 0.8f : 0.4f);
            velocity.y = new ParticleSystem.MinMaxCurve(-fall * 1.1f, -fall * 0.9f);
            velocity.z = new ParticleSystem.MinMaxCurve(-trainSpeed - (snowing ? 0.5f : 0f), -trainSpeed + (snowing ? 0.5f : 0f));
            main.startLifetime = raining ? 1.6f : 9f;
            main.startSize = raining ? new ParticleSystem.MinMaxCurve(0.025f, 0.04f) : new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            emission.rateOverTime = amount * (raining ? 2600f : 900f);
            particleRenderer.renderMode = raining ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            particleRenderer.velocityScale = raining ? 0.045f : 0f;
            particleRenderer.lengthScale = raining ? 1.5f : 1f;
            particleRenderer.sharedMaterial = raining ? rainMaterial : snowMaterial;
            float level = Mathf.Clamp(RenderSettings.ambientLight.maxColorComponent * 1.5f + 0.08f, 0.1f, 0.9f);
            Color tint = raining ? new Color(0.5f, 0.55f, 0.6f, 0.5f) * level : new Color(0.9f, 0.9f, 0.95f, 0.5f) * level;
            if (rainMaterial.HasProperty("_TintColor")) { rainMaterial.SetColor("_TintColor", tint); snowMaterial.SetColor("_TintColor", tint); }

            // Nothing may reach the cab: remove particles about to cross the windscreen.
            int count = system.GetParticles(particles);
            bool changed = false;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = particles[i].position;
                if (p.z < 2.6f && Mathf.Abs(p.x) < 2.2f && p.y > -3.5f)
                {
                    particles[i].remainingLifetime = 0f;
                    changed = true;
                }
            }
            if (changed) system.SetParticles(particles, count);
        }

        private static Texture2D Streak()
        {
            const int w = 8, h = 64;
            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "RainStreak", wrapMode = TextureWrapMode.Clamp };
            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float across = 1f - Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                    float along = Mathf.Sin((y + 0.5f) / h * Mathf.PI);
                    float f = across * across * along;
                    pixels[y * w + x] = new Color(f, f, f, f);
                }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            return texture;
        }

        private static Texture2D Flake()
        {
            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "SnowFlake", wrapMode = TextureWrapMode.Clamp };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float f = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    f = f * f;
                    pixels[y * size + x] = new Color(f, f, f, f);
                }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            return texture;
        }
    }

    /// <summary>
    /// Light from lamps the cab passes (platform, street and tunnel lamps) sweeps across the
    /// desk and instruments: a small light inside the cab follows the nearest lit lamp outside.
    /// </summary>
    public sealed class CabPassingLight : MonoBehaviour
    {
        private Light glow;
        private readonly List<Light> scratch = new List<Light>();

        public CabStreamedWorld World { get; set; }

        private void Awake()
        {
            GameObject lightObject = new GameObject("PassingLampGlow", typeof(Light));
            lightObject.transform.SetParent(transform, false);
            glow = lightObject.GetComponent<Light>();
            glow.type = LightType.Point;
            glow.range = 4.5f;
            glow.shadows = LightShadows.None;
            glow.renderMode = LightRenderMode.ForcePixel;
            glow.enabled = false;
        }

        private void LateUpdate()
        {
            if (World == null || glow == null) return;
            Vector3 eye = transform.position;
            Light nearest = null;
            float best = float.MaxValue;
            foreach (WorldChunk chunk in World.Chunks)
            {
                Consider(chunk.Lamps, eye, ref nearest, ref best);
                Consider(chunk.TunnelLights, eye, ref nearest, ref best);
            }
            if (nearest == null || best > 22f * 22f)
            {
                glow.enabled = false;
                return;
            }
            float distance = Mathf.Sqrt(best);
            Vector3 toward = (nearest.transform.position - eye) / Mathf.Max(0.01f, distance);
            // Place the glow just outside the glass in the lamp's direction, so its light falls
            // through the windscreen and side windows onto the desk and dials.
            glow.transform.position = eye + toward * 1.9f;
            glow.color = nearest.color;
            glow.intensity = Mathf.Clamp01(1f - distance / 22f) * 2.2f * Mathf.Clamp01(nearest.intensity);
            glow.enabled = glow.intensity > 0.02f;
        }

        private static void Consider(List<Light> lights, Vector3 eye, ref Light nearest, ref float best)
        {
            for (int i = 0; i < lights.Count; i++)
            {
                Light light = lights[i];
                if (light == null || !light.enabled || !light.gameObject.activeInHierarchy) continue;
                float d = (light.transform.position - eye).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = light;
                }
            }
        }
    }
}
