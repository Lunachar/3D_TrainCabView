using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// Lens glare from a low sun: a soft bloom over the sun and a few coloured ghosts along the
    /// line through the centre of the view, strongest at sunrise and sunset.
    /// </summary>
    public sealed class SunGlare : MonoBehaviour
    {
        private static readonly float[] GhostSteps = { 0f, 0.45f, 0.75f, 1.25f, 1.7f };
        private static readonly float[] GhostSizes = { 0.55f, 0.05f, 0.09f, 0.035f, 0.12f };
        private static readonly Color[] GhostTints =
        {
            new Color(1f, 0.78f, 0.5f), new Color(0.5f, 0.8f, 1f), new Color(1f, 0.6f, 0.3f), new Color(0.6f, 1f, 0.6f), new Color(0.7f, 0.5f, 1f)
        };
        private const float Distance = 0.6f;
        private Camera view;
        private Transform[] quads;
        private Material[] materials;
        private Vector3 sunToward = Vector3.up;
        private float sunset;
        private bool allowed;

        public void Configure(Vector3 toward, float sunsetAmount, bool visible)
        {
            sunToward = toward;
            sunset = sunsetAmount;
            allowed = visible;
        }

        private void Start()
        {
            view = GetComponent<Camera>();
            Texture2D soft = SoftDisc();
            quads = new Transform[GhostSteps.Length];
            materials = new Material[GhostSteps.Length];
            for (int i = 0; i < quads.Length; i++)
            {
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
                quad.name = "SunGlare_" + i;
                quad.transform.SetParent(transform, false);
                materials[i] = new Material(CabShaders.Additive) { mainTexture = soft };
                quad.GetComponent<Renderer>().sharedMaterial = materials[i];
                quad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                quads[i] = quad.transform;
            }
        }

        private void LateUpdate()
        {
            if (view == null || quads == null) return;
            Vector3 viewport = view.WorldToViewportPoint(view.transform.position + sunToward * 1000f);
            bool inView = viewport.z > 0f && viewport.x > -0.15f && viewport.x < 1.15f && viewport.y > -0.15f && viewport.y < 1.15f;
            // A high sun is behind the cab roof; the glare belongs to a low sun in the windscreen.
            float low = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.55f, sunToward.y));
            float edge = Mathf.Clamp01(Mathf.Min(Mathf.Min(viewport.x, 1f - viewport.x), Mathf.Min(viewport.y, 1f - viewport.y)) * 6f + 0.6f);
            float strength = allowed && inView && sunToward.y > -0.02f ? (0.25f + 0.75f * sunset) * low * edge : 0f;
            Vector2 sun = new Vector2(viewport.x, viewport.y);
            for (int i = 0; i < quads.Length; i++)
            {
                bool on = strength > 0.01f;
                quads[i].gameObject.SetActive(on);
                if (!on) continue;
                Vector2 point = Vector2.LerpUnclamped(sun, new Vector2(0.5f, 0.5f), GhostSteps[i]);
                quads[i].position = view.ViewportToWorldPoint(new Vector3(point.x, point.y, Distance));
                quads[i].rotation = view.transform.rotation;
                quads[i].localScale = Vector3.one * GhostSizes[i] * (i == 0 ? 1f + sunset : 1f);
                Color tint = GhostTints[i] * (i == 0 ? 0.55f : 0.22f) * strength;
                tint.a = 0.5f;
                materials[i].SetColor("_TintColor", tint);
                materials[i].color = tint;
            }
        }

        private static Texture2D SoftDisc()
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "GlareDisc" };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float f = Mathf.Clamp01(1f - r);
                    f = f * f + Mathf.Clamp01(1f - Mathf.Abs(r - 0.8f) * 12f) * 0.25f;
                    pixels[y * size + x] = new Color(f, f, f, f);
                }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            return texture;
        }
    }

    /// <summary>Now and then on a clear night a shooting star streaks across the sky.</summary>
    public sealed class ShootingStars : MonoBehaviour
    {
        private Transform streak;
        private Material material;
        private float nextAt;
        private float startedAt = -10f;
        private Vector3 from;
        private Vector3 direction;

        public bool Active { get; set; }

        private void Start()
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.name = "ShootingStar";
            quad.transform.SetParent(transform, false);
            material = new Material(CabShaders.Additive) { mainTexture = StreakTexture() };
            quad.GetComponent<Renderer>().sharedMaterial = material;
            streak = quad.transform;
            quad.SetActive(false);
            nextAt = Time.time + Random.Range(8f, 20f);
        }

        private void Update()
        {
            Camera view = Camera.main;
            if (streak == null || view == null) return;
            if (Active && Time.time >= nextAt)
            {
                nextAt = Time.time + Random.Range(18f, 55f);
                startedAt = Time.time;
                float yaw = Random.Range(-70f, 70f) + view.transform.eulerAngles.y, pitch = Random.Range(18f, 50f);
                from = Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward * 700f;
                direction = (Quaternion.Euler(0f, yaw, 0f) * new Vector3(Random.value < 0.5f ? -1f : 1f, -0.45f, 0f)).normalized;
            }
            float t = (Time.time - startedAt) / 0.9f;
            bool visible = t >= 0f && t <= 1f;
            streak.gameObject.SetActive(visible);
            if (!visible) return;
            Vector3 position = view.transform.position + from + direction * (t * 160f);
            streak.position = position;
            Vector3 toCamera = (view.transform.position - position).normalized;
            streak.rotation = Quaternion.LookRotation(-toCamera, Vector3.Cross(toCamera, direction));
            streak.localScale = new Vector3(3f, 70f, 1f);
            float fade = Mathf.Sin(t * Mathf.PI);
            Color tint = new Color(0.8f, 0.9f, 1f, 0.5f) * fade;
            material.SetColor("_TintColor", tint);
            material.color = tint;
        }

        private static Texture2D StreakTexture()
        {
            const int w = 16, h = 128;
            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "ShootingStarStreak" };
            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float across = 1f - Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                    float along = (y + 0.5f) / h;
                    float f = across * across * along * along;
                    pixels[y * w + x] = new Color(f, f, f, f);
                }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            return texture;
        }
    }
}
