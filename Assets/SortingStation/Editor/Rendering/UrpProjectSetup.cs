using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SortingStation.EditorTools
{
    /// <summary>
    /// Moves the project from the Built-in pipeline to URP: creates the two profile assets,
    /// assigns them in Graphics and Quality settings and converts
    /// Standard materials to URP Lit. Safe to run again; existing assets are reused.
    /// </summary>
    public static class UrpProjectSetup
    {
        private const string RenderingFolder = "Assets/SortingStation/Rendering";
        private const string PerformancePath = RenderingFolder + "/URP-Performance.asset";
        private const string BalancedPath = RenderingFolder + "/URP-Balanced.asset";
        private const string SettingsPath = "Assets/SortingStation/Resources/Configuration/CabWorld3DSettings.asset";
        private const string MaterialRoot = "Assets/SortingStation";
        private const string LitShaderName = "Universal Render Pipeline/Lit";

        [MenuItem("Sorting Station/Rendering/Set up URP")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(RenderingFolder))
                AssetDatabase.CreateFolder("Assets/SortingStation", "Rendering");

            CabWorld3DSettings settings = AssetDatabase.LoadAssetAtPath<CabWorld3DSettings>(SettingsPath);
            if (settings == null) throw new InvalidOperationException("Missing " + SettingsPath);

            UniversalRenderPipelineAsset performance = GetOrCreatePipeline(PerformancePath);
            UniversalRenderPipelineAsset balanced = GetOrCreatePipeline(BalancedPath);
            ConfigurePipeline(performance, renderScale: 0.7f, msaa: 1, shadows: false,
                shadowDistance: settings.ShadowDistance(CabWorldQuality.Performance), additionalLightsPerObject: 4);
            ConfigurePipeline(balanced, renderScale: 0.8f, msaa: 2, shadows: true,
                shadowDistance: settings.ShadowDistance(CabWorldQuality.Balanced), additionalLightsPerObject: 6);

            GraphicsSettings.defaultRenderPipeline = balanced;
            AssignQualityLevels(performance, balanced);
            // Colour space stays Gamma for now: in Linear, uGUI blends alpha in linear space and every
            // semi-transparent panel and cab overlay turns noticeably lighter. Linear comes together
            // with the rebuilt cab HUD, which no longer depends on layered translucent images.
            PlayerSettings.colorSpace = ColorSpace.Gamma;

            settings.ConfigureRendering(performance, balanced,
                RequireShader(LitShaderName),
                RequireShader("Unlit/Texture"),
                RequireShader("Unlit/Transparent"),
                RequireShader("Sprites/Default"),
                RequireShader("Legacy Shaders/Particles/Additive"),
                RequireShader("SortingStation/UI/RainWiper"),
                RequireShader("SortingStation/UI/MountainFeather"),
                RequireShader("Skybox/Procedural"),
                RequireShader("SortingStation/WorldText"),
                RequireShader("SortingStation/TerrainBlend"),
                RequireShader("SortingStation/ImpostorBake"));
            EditorUtility.SetDirty(settings);

            string conversion = ConvertStandardMaterials();
            AssetDatabase.SaveAssets();
            Debug.Log("URP setup finished.\n" + conversion);
        }

        private static UniversalRenderPipelineAsset GetOrCreatePipeline(string path)
        {
            UniversalRenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset != null) return asset;

            // The renderer is created the same way as URP's own "Create > Rendering > URP Asset"
            // menu, which also fills in the renderer's shader and post-processing resources.
            MethodInfo createRenderer = typeof(UniversalRenderPipelineAsset).GetMethod("CreateRendererAsset",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (createRenderer == null) throw new InvalidOperationException("URP CreateRendererAsset was not found.");
            ParameterInfo[] parameters = createRenderer.GetParameters();
            object[] arguments = new object[parameters.Length];
            arguments[0] = path;
            arguments[1] = Enum.ToObject(parameters[1].ParameterType, 1); // RendererType.UniversalRenderer
            for (int i = 2; i < parameters.Length; i++) arguments[i] = parameters[i].DefaultValue;
            ScriptableRendererData renderer = (ScriptableRendererData)createRenderer.Invoke(null, arguments);

            asset = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void ConfigurePipeline(UniversalRenderPipelineAsset asset, float renderScale, int msaa,
            bool shadows, float shadowDistance, int additionalLightsPerObject)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SetFloat(serialized, "m_RenderScale", renderScale);
            SetInt(serialized, "m_MSAA", msaa);
            SetBool(serialized, "m_SupportsHDR", false);
            SetBool(serialized, "m_UseSRPBatcher", true);
            SetBool(serialized, "m_RequireDepthTexture", false);
            SetBool(serialized, "m_RequireOpaqueTexture", false);
            SetInt(serialized, "m_MainLightRenderingMode", 1); // per pixel
            SetBool(serialized, "m_MainLightShadowsSupported", shadows);
            SetInt(serialized, "m_MainLightShadowmapResolution", 1024);
            SetInt(serialized, "m_AdditionalLightsRenderingMode", 1); // per pixel
            SetInt(serialized, "m_AdditionalLightsPerObjectLimit", additionalLightsPerObject);
            SetBool(serialized, "m_AdditionalLightShadowsSupported", false);
            SetFloat(serialized, "m_ShadowDistance", shadowDistance);
            SetInt(serialized, "m_ShadowCascadeCount", 1);
            SetBool(serialized, "m_SoftShadowsSupported", false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);

            // Forward+: lights are picked per screen tile instead of per object, so the long
            // track and terrain meshes are not limited to a handful of the route's many lamps
            // and the headlights always reach them.
            SerializedProperty renderers = serialized.FindProperty("m_RendererDataList");
            for (int i = 0; renderers != null && i < renderers.arraySize; i++)
            {
                UnityEngine.Object data = renderers.GetArrayElementAtIndex(i).objectReferenceValue;
                if (data == null) continue;
                SerializedObject rendererData = new SerializedObject(data);
                SetInt(rendererData, "m_RenderingMode", 2);
                rendererData.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
            }
        }

        private static void AssignQualityLevels(RenderPipelineAsset performance, RenderPipelineAsset balanced)
        {
            // The three lower levels (Android defaults to Medium) use the tablet profile; the
            // upper ones use the balanced profile. A ride still switches to the profile the
            // adult picked in the settings.
            int original = QualitySettings.GetQualityLevel();
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = i <= 2 ? performance : balanced;
            }
            QualitySettings.SetQualityLevel(original, false);
        }

        private static string ConvertStandardMaterials()
        {
            Shader lit = RequireShader(LitShaderName);
            int converted = 0;
            List<string> problems = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null) continue;
                string source = material.shader.name;
                if (source != "Standard" && source != "Legacy Shaders/Diffuse") continue;

                // Read the Built-in values before the shader changes; afterwards they are gone.
                Texture mainTexture = material.GetTexture("_MainTex");
                Vector2 scale = material.GetTextureScale("_MainTex");
                Vector2 offset = material.GetTextureOffset("_MainTex");
                Color color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                Texture normal = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;
                Texture emissionMap = material.HasProperty("_EmissionMap") ? material.GetTexture("_EmissionMap") : null;
                Color emission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
                bool emissive = material.IsKeywordEnabled("_EMISSION");
                float metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
                float smoothness = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0.5f;

                material.shader = lit;
                material.SetTexture("_BaseMap", mainTexture);
                material.SetTextureScale("_BaseMap", scale);
                material.SetTextureOffset("_BaseMap", offset);
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Metallic", metallic);
                material.SetFloat("_Smoothness", smoothness);
                if (normal != null)
                {
                    material.SetTexture("_BumpMap", normal);
                    material.EnableKeyword("_NORMALMAP");
                }
                if (emissive)
                {
                    material.SetTexture("_EmissionMap", emissionMap);
                    material.SetColor("_EmissionColor", emission);
                    material.EnableKeyword("_EMISSION");
                }
                EditorUtility.SetDirty(material);

                if (material.shader.name != LitShaderName) problems.Add(path + " landed on " + material.shader.name);
                if (mainTexture != null && material.GetTexture("_BaseMap") == null) problems.Add(path + " lost its texture");
                converted++;
            }

            StringBuilder report = new StringBuilder();
            report.Append("Converted materials: ").Append(converted);
            foreach (string problem in problems) report.Append('\n').Append("PROBLEM: ").Append(problem);
            if (problems.Count > 0) Debug.LogError(report.ToString());
            return report.ToString();
        }

        private static Shader RequireShader(string name)
        {
            Shader shader = Shader.Find(name);
            if (shader == null) throw new InvalidOperationException("Shader not found: " + name);
            return shader;
        }

        private static SerializedProperty Require(SerializedObject serialized, string name)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null) throw new InvalidOperationException("URP asset has no field " + name);
            return property;
        }

        private static void SetFloat(SerializedObject serialized, string name, float value) => Require(serialized, name).floatValue = value;
        private static void SetInt(SerializedObject serialized, string name, int value) => Require(serialized, name).intValue = value;
        private static void SetBool(SerializedObject serialized, string name, bool value) => Require(serialized, name).boolValue = value;
    }
}
