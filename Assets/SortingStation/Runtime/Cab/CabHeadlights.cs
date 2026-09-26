using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// The locomotive's headlights in the immersive world: a narrow long-range beam aimed down
    /// the track, a wide low flood for the first metres ahead and a faint additive beam cone that
    /// shows up in the dark, in the tunnel and in fog. Lights fall off with the square of the
    /// distance and hit the ground at a grazing angle, so the intensities are high on purpose.
    /// </summary>
    public sealed class CabHeadlights
    {
        public const float LampHeight = 4.1f;
        public const float BeamAimDistance = 30f;
        public const float FarIntensity = 2400f;
        public const float FloodIntensity = 38f;
        private const float BeamLength = 70f;

        private readonly Light far;
        private readonly Light flood;
        private readonly Renderer beam;
        private readonly Material beamMaterial;
        private readonly Renderer pool;
        private readonly Material poolMaterial;
        private bool on;

        public Light Far => far;
        public Light Flood => flood;
        public bool On => on;

        public CabHeadlights(Transform parent, float noseAhead)
        {
            Transform root = new GameObject("TrainHeadlights3D").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(0f, LampHeight, noseAhead);

            far = CreateSpot(root, "HeadlightFar", Mathf.Atan2(LampHeight, BeamAimDistance) * Mathf.Rad2Deg,
                FarIntensity, 190f, 34f, 12f);
            flood = CreateSpot(root, "HeadlightFlood", Mathf.Atan2(LampHeight, 12f) * Mathf.Rad2Deg,
                FloodIntensity, 45f, 78f, 40f);

            GameObject cone = new GameObject("HeadlightBeam", typeof(MeshFilter), typeof(MeshRenderer));
            cone.transform.SetParent(far.transform, false);
            cone.GetComponent<MeshFilter>().sharedMesh = BuildBeamMesh(BeamLength, 13f);
            beamMaterial = new Material(CabShaders.Additive) { name = "HeadlightBeam" };
            if (beamMaterial.HasProperty("_TintColor")) beamMaterial.SetColor("_TintColor", new Color(0.5f, 0.46f, 0.38f, 0.5f));
            beamMaterial.color = new Color(0.5f, 0.46f, 0.38f, 0.5f);
            beam = cone.GetComponent<MeshRenderer>();
            beam.sharedMaterial = beamMaterial;
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam.receiveShadows = false;

            // Ground at a grazing angle catches little of a real light, so a soft additive pool
            // on the track shows where the beam lands in the dark.
            GameObject poolObject = new GameObject("HeadlightPool", typeof(MeshFilter), typeof(MeshRenderer));
            poolObject.transform.SetParent(parent, false);
            poolObject.transform.localPosition = new Vector3(0f, 0.4f, noseAhead);
            poolObject.GetComponent<MeshFilter>().sharedMesh = BuildPoolMesh();
            poolMaterial = new Material(CabShaders.Additive) { name = "HeadlightPool" };
            pool = poolObject.GetComponent<MeshRenderer>();
            pool.sharedMaterial = poolMaterial;
            pool.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pool.receiveShadows = false;
            SetOn(false);
        }

        /// <summary>Tells the rail and wire shaders where the beam is (scene space).</summary>
        public void PublishToShaders()
        {
            Shader.SetGlobalVector("_CabHeadlightPos", new Vector4(far.transform.position.x, far.transform.position.y, far.transform.position.z, on ? 1f : 0f));
            Shader.SetGlobalVector("_CabHeadlightDir", far.transform.forward);
        }

        public void SetOn(bool enabled)
        {
            on = enabled;
            far.enabled = enabled;
            flood.enabled = enabled;
            beam.enabled = enabled;
            pool.enabled = enabled;
        }

        /// <summary>The beam cone is only visible where there is little other light to hide it.</summary>
        public void UpdateHaze(float night01, float tunnelBlend, bool badWeather)
        {
            float haze = Mathf.Clamp01(Mathf.Max(night01 * 0.75f, tunnelBlend) + (badWeather ? 0.35f : 0f));
            Color tint = new Color(0.5f, 0.46f, 0.38f, 0.5f) * Mathf.Lerp(0f, 0.14f, haze);
            if (beamMaterial.HasProperty("_TintColor")) beamMaterial.SetColor("_TintColor", tint);
            beamMaterial.color = tint;
            beam.enabled = on && haze > 0.05f;
            float dark = Mathf.Clamp01(Mathf.Max(night01, tunnelBlend));
            Color poolTint = new Color(0.55f, 0.50f, 0.40f, 0.5f) * Mathf.Lerp(0f, 1.3f, dark);
            if (poolMaterial.HasProperty("_TintColor")) poolMaterial.SetColor("_TintColor", poolTint);
            poolMaterial.color = poolTint;
            pool.enabled = on && dark > 0.05f;
        }

        /// <summary>Flat strip along +Z that widens with distance; alpha peaks around 20 m.</summary>
        private static Mesh BuildPoolMesh()
        {
            const int rows = 12;
            const int columns = 7;
            const float near = 5f;
            const float farEnd = 75f;
            Vector3[] vertices = new Vector3[rows * columns];
            Color[] colors = new Color[vertices.Length];
            for (int r = 0; r < rows; r++)
            {
                float t = r / (float)(rows - 1);
                float z = Mathf.Lerp(near, farEnd, t);
                float halfWidth = Mathf.Lerp(2.2f, 11f, t);
                float along = Mathf.SmoothStep(0f, 1f, t * 4f) * Mathf.Pow(1f - t, 1.4f);
                for (int c = 0; c < columns; c++)
                {
                    float u = c / (float)(columns - 1) * 2f - 1f;
                    vertices[r * columns + c] = new Vector3(u * halfWidth, 0f, z);
                    float across = 1f - u * u;
                    colors[r * columns + c] = new Color(1f, 1f, 1f, along * across * across);
                }
            }
            int[] triangles = new int[(rows - 1) * (columns - 1) * 6];
            int k = 0;
            for (int r = 0; r < rows - 1; r++)
                for (int c = 0; c < columns - 1; c++)
                {
                    int a = r * columns + c;
                    triangles[k++] = a; triangles[k++] = a + columns; triangles[k++] = a + 1;
                    triangles[k++] = a + 1; triangles[k++] = a + columns; triangles[k++] = a + columns + 1;
                }
            Mesh mesh = new Mesh { name = "HeadlightPool" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Light CreateSpot(Transform root, string name, float pitch, float intensity, float range, float angle, float inner)
        {
            GameObject lampObject = new GameObject(name);
            lampObject.transform.SetParent(root, false);
            lampObject.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            Light light = lampObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.94f, 0.82f);
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = angle;
            light.innerSpotAngle = inner;
            light.shadows = LightShadows.None;
            // Always per-pixel: the per-object light limit must never drop the headlights.
            light.renderMode = LightRenderMode.ForcePixel;
            return light;
        }

        /// <summary>Open cone along +Z; vertex alpha fades out along the length.</summary>
        private static Mesh BuildBeamMesh(float length, float halfAngle)
        {
            const int sides = 16;
            const int rings = 6;
            Vector3[] vertices = new Vector3[sides * rings];
            Color[] colors = new Color[vertices.Length];
            int[] triangles = new int[sides * (rings - 1) * 12];
            float tan = Mathf.Tan(halfAngle * Mathf.Deg2Rad);
            for (int r = 0; r < rings; r++)
            {
                float t = r / (float)(rings - 1);
                float z = 0.3f + t * length;
                float radius = 0.35f + z * tan;
                float alpha = Mathf.Pow(1f - t, 1.6f) * Mathf.SmoothStep(0f, 1f, t * 6f);
                for (int s = 0; s < sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    vertices[r * sides + s] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, z);
                    colors[r * sides + s] = new Color(1f, 1f, 1f, alpha);
                }
            }
            int k = 0;
            for (int r = 0; r < rings - 1; r++)
                for (int s = 0; s < sides; s++)
                {
                    int a = r * sides + s;
                    int b = r * sides + (s + 1) % sides;
                    int c = a + sides;
                    int d = b + sides;
                    // Both faces: the driver sees the cone from inside.
                    triangles[k++] = a; triangles[k++] = c; triangles[k++] = b;
                    triangles[k++] = b; triangles[k++] = c; triangles[k++] = d;
                    triangles[k++] = a; triangles[k++] = b; triangles[k++] = c;
                    triangles[k++] = b; triangles[k++] = d; triangles[k++] = c;
                }
            Mesh mesh = new Mesh { name = "HeadlightBeam" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
