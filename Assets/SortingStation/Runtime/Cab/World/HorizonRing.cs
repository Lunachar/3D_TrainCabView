using UnityEngine;
using UnityEngine.Rendering;

namespace SortingStation
{
    /// <summary>
    /// Far horizon: a ring of distant ridges and forest edges just inside the camera's far
    /// clip, tinted with the fog colour so it sits in the haze. Where the streamed terrain
    /// ends, the land does not stop: it fades into these silhouettes.
    /// </summary>
    public sealed class HorizonRing
    {
        private const int Segments = 96;
        private readonly Transform root;
        private readonly Material material;

        public Transform Root => root;

        public HorizonRing(Transform parent, float radius, int seed)
        {
            root = new GameObject("HorizonRing", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            root.SetParent(parent, false);
            float bottom = -40f, top = radius * 0.13f;
            Vector3[] vertices = new Vector3[(Segments + 1) * 2];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[Segments * 6];
            for (int i = 0; i <= Segments; i++)
            {
                float a = i / (float)Segments * Mathf.PI * 2f;
                Vector3 direction = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                vertices[i * 2] = direction * radius + Vector3.up * bottom;
                vertices[i * 2 + 1] = direction * radius + Vector3.up * top;
                uv[i * 2] = new Vector2(i / (float)Segments, 0f);
                uv[i * 2 + 1] = new Vector2(i / (float)Segments, 1f);
                if (i == Segments) break;
                int k = i * 6;
                triangles[k] = i * 2; triangles[k + 1] = i * 2 + 1; triangles[k + 2] = i * 2 + 3;
                triangles[k + 3] = i * 2; triangles[k + 4] = i * 2 + 3; triangles[k + 5] = i * 2 + 2;
            }
            Mesh mesh = new Mesh { name = "HorizonRing" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 2.2f);
            root.GetComponent<MeshFilter>().sharedMesh = mesh;

            material = new Material(CabShaders.Sprite) { name = "HorizonRing" };
            material.mainTexture = BuildTexture(seed, bottom, top);
            material.renderQueue = (int)RenderQueue.Transparent - 50;
            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Follows the world's orientation and takes the haze colour of the moment.</summary>
        public void Update(Quaternion worldRotation, Color horizon, float tunnelBlend)
        {
            root.localRotation = worldRotation;
            Color tint = horizon;
            tint.a = 1f - tunnelBlend;
            material.color = tint;
            root.gameObject.SetActive(tunnelBlend < 0.98f);
        }

        /// <summary>Two layers: pale far ridges and a darker, nearer forest edge. RGB is the shade, alpha the shape.</summary>
        private static Texture2D BuildTexture(int seed, float bottom, float top)
        {
            const int width = 1024, height = 128;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = "HorizonSilhouettes",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear
            };
            Color[] pixels = new Color[width * height];
            float offset = (seed % 1000) * 0.37f;
            float zeroRow = -bottom / (top - bottom) * height;
            for (int x = 0; x < width; x++)
            {
                // Sample the noise on a circle so the ring has no seam.
                float a = x / (float)width * Mathf.PI * 2f;
                float cx = Mathf.Cos(a), cy = Mathf.Sin(a);
                float ridge = Mathf.PerlinNoise(offset + cx * 1.6f + 3f, offset + cy * 1.6f + 5f) * 0.7f +
                              Mathf.PerlinNoise(offset + cx * 5f + 9f, offset + cy * 5f + 1f) * 0.3f;
                float ridgeHeight = zeroRow + Mathf.Pow(ridge, 1.8f) * (height - zeroRow) * 0.95f;
                float forest = Mathf.PerlinNoise(offset + cx * 3f + 20f, offset + cy * 3f + 30f);
                float jag = Mathf.PerlinNoise(x * 0.9f, 7.3f) * 0.6f + Mathf.PerlinNoise(x * 0.23f, 2.1f) * 0.4f;
                float forestHeight = zeroRow + (0.05f + forest * 0.12f + jag * 0.05f) * (height - zeroRow);
                for (int y = 0; y < height; y++)
                {
                    Color c = new Color(1f, 1f, 1f, 0f);
                    if (y < ridgeHeight) c = new Color(0.9f, 0.92f, 0.96f, Mathf.Clamp01((ridgeHeight - y) * 0.6f));
                    if (y < forestHeight) c = new Color(0.62f, 0.66f, 0.64f, Mathf.Clamp01((forestHeight - y) * 0.8f) * 0.5f + 0.5f);
                    if (y < zeroRow) c = new Color(0.62f, 0.66f, 0.64f, 1f);
                    pixels[y * width + x] = c;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            return texture;
        }
    }
}
