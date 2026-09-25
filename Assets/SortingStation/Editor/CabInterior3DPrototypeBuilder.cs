#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SortingStation
{
    /// <summary>Creates an isolated, movable 3D cab layout without touching the live cab scene.</summary>
    public static class CabInterior3DPrototypeBuilder
    {
        public const string PrefabPath = "Assets/SortingStation/Resources/Cab3D/CabInterior3DPrototype.prefab";
        public const string ScenePath = "Assets/SortingStation/Scenes/CabInterior3DPrototype.unity";
        private const string MaterialFolder = "Assets/SortingStation/Art/Cab3D/Generated/InteriorMaterials";

        [MenuItem("Sorting Station/Cab 3D/Create or rebuild 3D cabin prototype")]
        public static void CreateOrRebuild()
        {
            EnsureFolder("Assets/SortingStation/Art/Cab3D/Generated", "InteriorMaterials");
            GameObject cab = CabInterior3DPrototypeFactory.Create();
            PersistMaterials(cab);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(cab, PrefabPath);
            Object.DestroyImmediate(cab);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "EditableCabInterior_AllControlsCanBeMoved";
            GameObject cameraObject = new GameObject("Preview Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 0.25f, -13f);
            cameraObject.transform.rotation = Quaternion.Euler(1f, 0f, 0f);
            camera.fieldOfView = 55f;
            camera.tag = "MainCamera";
            camera.backgroundColor = new Color(0.012f, 0.020f, 0.034f);
            camera.clearFlags = CameraClearFlags.SolidColor;

            GameObject lightObject = new GameObject("Cab Preview Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.79f, 0.56f);
            light.intensity = 1.55f;
            lightObject.transform.rotation = Quaternion.Euler(34f, -18f, 0f);
            light.shadows = LightShadows.Hard;

            CreatePreviewLight("Cab Preview Fill Light", new Vector3(0f, 0.7f, -8.4f),
                new Color(0.55f, 0.74f, 1f), 1.25f, 20f);
            CreatePreviewLight("Cab Preview Warm Panel Light", new Vector3(-4.6f, 2.9f, -1.6f),
                new Color(1f, 0.53f, 0.18f), 1.05f, 9.5f);
            CreatePreviewLight("Cab Preview Instrument Light", new Vector3(3.8f, 1.8f, -0.8f),
                new Color(0.18f, 0.72f, 1f), 0.42f, 7.5f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.29f, 0.37f);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = prefab;
            Debug.Log("Editable 3D cab created: " + ScenePath);
        }

        [MenuItem("Sorting Station/Cab 3D/Open 3D cabin prototype")]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath)) CreateOrRebuild();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Sorting Station/Cab 3D/Apply opened layout to the game")]
        public static void ApplyOpenedLayout()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject root = null;
            foreach (GameObject candidate in scene.GetRootGameObjects())
            {
                if (candidate.name.StartsWith("EditableCabInterior") || candidate.name.StartsWith("CabInterior3D"))
                {
                    root = candidate;
                    break;
                }
            }
            if (root == null)
            {
                Debug.LogWarning("Open the 3D cabin prototype scene before applying its layout.");
                return;
            }

            if (PrefabUtility.IsPartOfPrefabInstance(root))
                PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction);
            else
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = root;
            Debug.Log("3D cabin layout applied to the game: " + PrefabPath);
        }

        [MenuItem("Sorting Station/Cab 3D/Render cabin preview for validation")]
        public static void RenderPreviewForValidation()
        {
            if (!File.Exists(ScenePath)) CreateOrRebuild();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera camera = Object.FindObjectOfType<Camera>();
            if (camera == null)
            {
                Debug.LogError("3D cabin preview camera was not found.");
                return;
            }

            const int width = 1600;
            const int height = 900;
            RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply(false, false);
                string outputPath = Path.GetFullPath("Temp/CabInterior3DPreview.png");
                File.WriteAllBytes(outputPath, image.EncodeToPNG());
                Debug.Log("3D cabin preview rendered: " + outputPath);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(renderTexture);
            }
        }

        private static void CreatePreviewLight(string name, Vector3 position, Color color, float intensity, float range)
        {
            GameObject source = new GameObject(name);
            source.transform.position = position;
            Light light = source.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        private static void PersistMaterials(GameObject cab)
        {
            Dictionary<Material, Material> stored = new Dictionary<Material, Material>();
            foreach (Renderer renderer in cab.GetComponentsInChildren<Renderer>(true))
            {
                Material source = renderer.sharedMaterial;
                if (source == null) continue;
                if (!stored.TryGetValue(source, out Material asset))
                {
                    // Rebuilding the preview must not keep appending " 1", " 2" material copies
                    // into the project.  A fixed asset name preserves references on the editable
                    // prefab while CopySerialized refreshes generated material properties.
                    string path = MaterialFolder + "/" + SafeName(source.name) + ".mat";
                    asset = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (asset == null)
                    {
                        asset = new Material(source) { name = SafeName(source.name) };
                        AssetDatabase.CreateAsset(asset, path);
                    }
                    else
                    {
                        EditorUtility.CopySerialized(source, asset);
                        asset.name = SafeName(source.name);
                        EditorUtility.SetDirty(asset);
                    }
                    stored[source] = asset;
                }
                renderer.sharedMaterial = asset;
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }

        private static string SafeName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(value) ? "CabMaterial" : value;
        }
    }
}
#endif
