#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SortingStation
{
    [InitializeOnLoad]
    public static class CabWorld3DPrototypeBuilder
    {
        public const string PrefabPath = "Assets/SortingStation/Resources/Cab3D/CabWorld3DPrototype.prefab";
        public const string SettingsPath = "Assets/SortingStation/Resources/Configuration/CabWorld3DSettings.asset";
        public const string ScenePath = "Assets/SortingStation/Scenes/CabWorld3DPrototype.unity";
        private const string MaterialFolder = "Assets/SortingStation/Art/Cab3D/Generated/Materials";
        private const string MeshFolder = "Assets/SortingStation/Art/Cab3D/Generated/Meshes";

        static CabWorld3DPrototypeBuilder()
        {
            EditorApplication.delayCall += TryCreateInitialPrototype;
        }

        [MenuItem("Sorting Station/Cab 3D/Create or rebuild prototype route")]
        public static void CreateOrRebuild()
        {
            EnsureFolders();
            GameObject route = CabWorld3DPrototypeFactory.Create(null, GetJourneyCycleLength(), 1f);
            route.name = "CabWorld3DPrototype_Editable";
            PersistMeshes(route);
            PersistMaterials(route);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(route, PrefabPath);
            Object.DestroyImmediate(route);

            CabWorld3DSettings settings = AssetDatabase.LoadAssetAtPath<CabWorld3DSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<CabWorld3DSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            settings.ConfigurePrototype(prefab);
            EditorUtility.SetDirty(settings);
            CreatePreviewScene(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = prefab;
            Debug.Log("Cab 3D prototype created: " + ScenePath);
        }

        [MenuItem("Sorting Station/Cab 3D/Open prototype scene")]
        public static void OpenPrototypeScene()
        {
            if (!File.Exists(ScenePath)) CreateOrRebuild();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Sorting Station/Cab 3D/Apply opened route layout to the game")]
        public static void ApplyOpenedRouteLayout()
        {
            GameObject route = GameObject.Find("EditableRoute_AllObjectsCanBeMoved");
            if (route == null)
            {
                Debug.LogWarning("Open CabWorld3DPrototype first. The editable route root was not found in this scene.");
                return;
            }

            if (route.GetComponent<Cab3DRouteAuthoring>() == null)
            {
                Debug.LogWarning("The selected route does not contain Cab3DRouteAuthoring and cannot be applied.");
                return;
            }

            if (PrefabUtility.GetPrefabInstanceStatus(route) != PrefabInstanceStatus.Connected)
            {
                Debug.LogWarning("The editable route is no longer connected to its prefab. Reopen the prototype scene before applying it.");
                return;
            }

            PrefabUtility.ApplyPrefabInstance(route, InteractionMode.UserAction);
            EditorSceneManager.MarkSceneDirty(route.scene);
            EditorSceneManager.SaveScene(route.scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log("Cab 3D route layout applied to the game prefab.");
        }

        [MenuItem("Sorting Station/Cab 3D/Apply opened route layout to the game", true)]
        private static bool CanApplyOpenedRouteLayout()
        {
            GameObject route = GameObject.Find("EditableRoute_AllObjectsCanBeMoved");
            return route != null && PrefabUtility.GetPrefabInstanceStatus(route) == PrefabInstanceStatus.Connected;
        }

        [MenuItem("Sorting Station/Cab 3D/Render route preview for validation")]
        public static void RenderRoutePreviewForValidation()
        {
            // Uses the factory directly, so the image always exercises the route code that a
            // stale editable prefab would otherwise hide. It is intentionally non-destructive:
            // the preview scene and its temporary objects are discarded after rendering.
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject route = CabWorld3DPrototypeFactory.Create(null, GetJourneyCycleLength(), 1f);
                SceneManager.MoveGameObjectToScene(route, previewScene);

                GameObject cameraObject = new GameObject("RouteValidationCamera");
                SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
                Camera camera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.position = new Vector3(0f, 3.35f, -8f);
                cameraObject.transform.rotation = Quaternion.Euler(16.5f, 0f, 0f);
                camera.fieldOfView = 61f;
                camera.farClipPlane = 260f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.50f, 0.75f, 0.91f);

                GameObject lightObject = new GameObject("RouteValidationSun");
                SceneManager.MoveGameObjectToScene(lightObject, previewScene);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.93f, 0.78f);
                light.intensity = 0.86f;
                light.shadows = LightShadows.Hard;
                lightObject.transform.rotation = Quaternion.Euler(42f, -28f, 0f);

                const int width = 1600;
                const int height = 900;
                RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
                RenderTexture previous = RenderTexture.active;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                string output = Path.GetFullPath("Temp/CabWorld3DRoutePreview.png");
                File.WriteAllBytes(output, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                target.Release();
                Object.DestroyImmediate(target);
                Debug.Log("Cab 3D route preview written: " + output);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static float GetJourneyCycleLength()
        {
            CabSceneryCatalog catalog = Resources.Load<CabSceneryCatalog>("Configuration/CabSceneryCatalog");
            CabJourneyRuntime journey = new CabJourneyRuntime(catalog != null ? catalog.RouteSegments : null, 1);
            return journey.CycleLength;
        }

        private static void TryCreateInitialPrototype()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (File.Exists(PrefabPath) && File.Exists(ScenePath) && File.Exists(SettingsPath)) return;
            Scene current = SceneManager.GetActiveScene();
            if (current.IsValid() && current.isDirty)
            {
                Debug.Log("Cab 3D prototype is not generated because the current scene has unsaved changes. " +
                          "Use Sorting Station > Cab 3D > Create or rebuild prototype route.");
                return;
            }
            CreateOrRebuild();
        }

        private static void CreatePreviewScene(GameObject prefab)
        {
            string previous = SceneManager.GetActiveScene().path;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject route = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            route.name = "EditableRoute_AllObjectsCanBeMoved";

            GameObject cameraObject = new GameObject("Preview Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 3.35f, -8f);
            cameraObject.transform.rotation = Quaternion.Euler(16.5f, 0f, 0f);
            camera.fieldOfView = 61f;
            camera.farClipPlane = 260f;
            camera.tag = "MainCamera";

            GameObject lightObject = new GameObject("Sun");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.93f, 0.78f);
            light.intensity = 0.86f;
            light.shadows = LightShadows.Hard;
            lightObject.transform.rotation = Quaternion.Euler(42f, -28f, 0f);

            GameObject note = new GameObject("README_MoveObjectsAndApplyToPrefab");
            note.transform.SetAsFirstSibling();
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (!string.IsNullOrWhiteSpace(previous) && File.Exists(previous))
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        }

        private static void PersistMaterials(GameObject route)
        {
            // The factory creates a separate Material for every object. Objects whose materials
            // match share one asset, and a rebuild overwrites the same files instead of adding
            // "Name 1", "Name 2", ... copies next to the previous build's materials.
            Dictionary<string, Material> persisted = new Dictionary<string, Material>();
            Dictionary<string, int> nameUses = new Dictionary<string, int>();
            HashSet<string> writtenPaths = new HashSet<string>();
            Renderer[] renderers = route.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material source = renderers[i].sharedMaterial;
                if (source == null || AssetDatabase.Contains(source)) continue;
                string safeName = Sanitize(source.name);
                string key = safeName + "\n" + MaterialContent(source);
                if (!persisted.TryGetValue(key, out Material asset))
                {
                    nameUses.TryGetValue(safeName, out int uses);
                    nameUses[safeName] = ++uses;
                    string fileName = uses == 1 ? safeName : safeName + " " + uses;
                    string path = MaterialFolder + "/" + fileName + ".mat";
                    asset = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (asset == null)
                    {
                        asset = new Material(source) { name = fileName };
                        AssetDatabase.CreateAsset(asset, path);
                    }
                    else
                    {
                        asset.shader = source.shader;
                        asset.CopyPropertiesFromMaterial(source);
                        EditorUtility.SetDirty(asset);
                    }
                    writtenPaths.Add(path);
                    persisted[key] = asset;
                }
                renderers[i].sharedMaterial = asset;
            }
            DeleteStaleMaterials(writtenPaths);
        }

        private static string MaterialContent(Material material)
        {
            string name = material.name;
            material.name = string.Empty;
            string json = EditorJsonUtility.ToJson(material);
            material.name = name;
            return json;
        }

        private static void DeleteStaleMaterials(HashSet<string> writtenPaths)
        {
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!writtenPaths.Contains(path)) AssetDatabase.DeleteAsset(path);
            }
        }

        private static void PersistMeshes(GameObject route)
        {
            Dictionary<Mesh, Mesh> persisted = new Dictionary<Mesh, Mesh>();
            MeshFilter[] filters = route.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh source = filters[i].sharedMesh;
                if (source == null || AssetDatabase.Contains(source)) continue;
                if (!persisted.TryGetValue(source, out Mesh asset))
                {
                    string path = MeshFolder + "/" + Sanitize(source.name) + ".asset";
                    asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (asset == null)
                    {
                        asset = Object.Instantiate(source);
                        asset.name = source.name;
                        AssetDatabase.CreateAsset(asset, path);
                    }
                    else
                    {
                        EditorUtility.CopySerialized(source, asset);
                        asset.name = source.name;
                        EditorUtility.SetDirty(asset);
                    }
                    persisted[source] = asset;
                }
                filters[i].sharedMesh = asset;
            }
        }

        private static string Sanitize(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(value) ? "Cab3DMaterial" : value;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/SortingStation/Resources", "Cab3D");
            EnsureFolder("Assets/SortingStation/Resources", "Configuration");
            EnsureFolder("Assets/SortingStation/Art", "Cab3D");
            EnsureFolder("Assets/SortingStation/Art/Cab3D", "Generated");
            EnsureFolder("Assets/SortingStation/Art/Cab3D/Generated", "Materials");
            EnsureFolder("Assets/SortingStation/Art/Cab3D/Generated", "Meshes");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
