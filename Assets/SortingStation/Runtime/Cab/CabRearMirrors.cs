using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SortingStation
{
    /// <summary>
    /// Two small rear-view mirrors outside the windscreen corners. Each has its own low-resolution
    /// camera looking back along the side of the train; the mirrors refresh in turns, a few times
    /// a second on the move and more often at a station, to keep the tablet's frame time low.
    /// </summary>
    public sealed class CabRearMirrors : MonoBehaviour
    {
        private const int Width = 256;
        private const int Height = 192;
        private readonly Camera[] cameras = new Camera[2];
        private readonly RenderTexture[] textures = new RenderTexture[2];
        private float nextRender;
        private int next;

        public bool FastRefresh { get; set; }

        /// <summary>Builds both mirrors on the driver's rig (cab-local positions, metres from the eye).</summary>
        public void Build(Transform driverRig, int cockpitLayer)
        {
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Transform mount = new GameObject(i == 0 ? "RearMirror_Left" : "RearMirror_Right").transform;
                mount.SetParent(driverRig, false);
                mount.localPosition = new Vector3(side * 0.98f, -0.02f, 1.52f);
                // Turned so its face points back at the driver's eye.
                mount.localRotation = Quaternion.LookRotation(mount.localPosition.normalized, Vector3.up);

                textures[i] = new RenderTexture(Width, Height, 16, RenderTextureFormat.ARGB32) { name = mount.name + "_RT" };
                textures[i].Create();

                GameObject housing = new GameObject("Housing", typeof(MeshFilter), typeof(MeshRenderer));
                housing.transform.SetParent(mount, false);
                CabMeshBuilder shell = new CabMeshBuilder(0.2f);
                shell.AddChamferBox(new Vector3(0f, 0f, 0.025f), new Vector3(0.25f, 0.19f, 0.05f), 0.015f, Quaternion.identity);
                shell.AddCylinder(new Vector3(-side * 0.16f, 0f, 0.03f), 0.012f, 0.16f, 8, Quaternion.Euler(0f, 0f, 90f));
                housing.GetComponent<MeshFilter>().sharedMesh = shell.Build(mount.name + "_Housing");
                housing.GetComponent<MeshRenderer>().sharedMaterial = CabPbrMaterials.Plain("MirrorHousing", new Color(0.06f, 0.065f, 0.07f), 0.5f);

                // The glass shows the camera image flipped left-right, as a real mirror does.
                GameObject glass = new GameObject("MirrorGlass", typeof(MeshFilter), typeof(MeshRenderer));
                glass.transform.SetParent(mount, false);
                glass.transform.localPosition = new Vector3(0f, 0f, -0.002f);
                Mesh quad = new Mesh { name = mount.name + "_Glass" };
                const float w = 0.105f;
                const float h = 0.08f;
                quad.vertices = new[] { new Vector3(-w, -h, 0f), new Vector3(w, -h, 0f), new Vector3(w, h, 0f), new Vector3(-w, h, 0f) };
                quad.uv = new[] { new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
                quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                glass.GetComponent<MeshFilter>().sharedMesh = quad;
                glass.GetComponent<MeshRenderer>().sharedMaterial = new Material(CabShaders.UnlitTexture)
                {
                    name = mount.name + "_Image",
                    mainTexture = textures[i]
                };
                housing.layer = cockpitLayer;
                glass.layer = cockpitLayer;

                // The camera sits just outside the cab side and looks back along the train.
                GameObject cameraObject = new GameObject("MirrorCamera");
                cameraObject.transform.SetParent(driverRig, false);
                cameraObject.transform.localPosition = new Vector3(side * 1.75f, 0.05f, 1.2f);
                cameraObject.transform.localRotation = Quaternion.Euler(4f, 180f - side * 6f, 0f);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.targetTexture = textures[i];
                camera.fieldOfView = 34f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 140f;
                camera.cullingMask = ~(1 << cockpitLayer);
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.enabled = false;
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                data.renderShadows = false;
                data.renderPostProcessing = false;
                cameras[i] = camera;
            }
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextRender) return;
            Camera camera = cameras[next];
            if (camera != null) camera.Render();
            next = 1 - next;
            nextRender = Time.unscaledTime + (FastRefresh ? 0.05f : 0.1f);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] == null) continue;
                textures[i].Release();
                Destroy(textures[i]);
            }
        }
    }
}
