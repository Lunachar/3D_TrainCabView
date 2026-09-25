using UnityEngine;
using UnityEngine.Rendering;

namespace SortingStation
{
    [CreateAssetMenu(menuName = "Sorting Station/Cab 3D World Settings", fileName = "CabWorld3DSettings")]
    public sealed class CabWorld3DSettings : ScriptableObject
    {
        [Header("Editable route")]
        [SerializeField] private GameObject prototypeRoutePrefab;

        [Header("Rendering (URP)")]
        [Tooltip("URP asset used by the \"3D: быстрее\" profile.")]
        [SerializeField] private RenderPipelineAsset performancePipeline;
        [Tooltip("URP asset used by the \"3D: качество\" profile.")]
        [SerializeField] private RenderPipelineAsset balancedPipeline;
        // Shaders for materials created from code. Referencing them here keeps them in player
        // builds; a shader that is only looked up by name can be stripped from Android builds.
        [SerializeField] private Shader litShader;
        [SerializeField] private Shader unlitTextureShader;
        [SerializeField] private Shader unlitTransparentShader;
        [SerializeField] private Shader spriteShader;
        [SerializeField] private Shader additiveShader;
        [SerializeField] private Shader rainWiperShader;
        [SerializeField] private Shader mountainFeatherShader;
        [SerializeField] private Shader skyboxShader;
        [SerializeField] private Shader worldTextShader;
        [SerializeField] private Shader terrainShader;
        [SerializeField] private Shader impostorBakeShader;
        [SerializeField] private Shader glintShader;

        [Header("Render texture")]
        [SerializeField] [Min(640)] private int performanceWidth = 960;
        [SerializeField] [Min(256)] private int performanceHeight = 384;
        [SerializeField] [Min(960)] private int balancedWidth = 1280;
        [SerializeField] [Min(384)] private int balancedHeight = 512;
        [SerializeField] [Range(45f, 75f)] private float cameraFieldOfView = 61f;
        [SerializeField] [Range(120f, 400f)] private float farClip = 240f;

        [Header("Redmi Pad 2 quality")]
        [SerializeField] [Range(12f, 80f)] private float performanceShadowDistance = 24f;
        [SerializeField] [Range(20f, 100f)] private float balancedShadowDistance = 46f;
        [SerializeField] [Range(0.25f, 1f)] private float performanceSceneryDensity = 0.58f;
        [SerializeField] [Range(0.5f, 1.5f)] private float balancedSceneryDensity = 1f;

        [Header("World lighting")]
        [SerializeField] private Color clearSky = new Color(0.50f, 0.75f, 0.91f, 1f);
        [SerializeField] private Color sunlight = new Color(1f, 0.93f, 0.78f, 1f);
        [SerializeField] [Range(0.2f, 1.5f)] private float sunlightIntensity = 0.86f;

        [Header("3D cockpit transition")]
        // Keep only a faint edge reference from the source photo; higher values make the old
        // photographed console visibly ghost through the physical 3D controls.
        [SerializeField] [Range(0f, 0.12f)] private float cockpitPhotoOverlayOpacity = 0.12f;
        [SerializeField] [Min(480)] private int cockpitPerformanceWidth = 720;
        [SerializeField] [Min(320)] private int cockpitPerformanceHeight = 480;
        [SerializeField] [Min(640)] private int cockpitBalancedWidth = 960;
        [SerializeField] [Min(426)] private int cockpitBalancedHeight = 640;
        [SerializeField] [Range(40f, 65f)] private float cockpitCameraFieldOfView = 50f;
        [SerializeField] [Range(0.4f, 1.8f)] private float cockpitCabinLightIntensity = 1.05f;
        [SerializeField] [Range(0.08f, 0.8f)] private float cockpitAccessibleOverlayOpacity = 0.24f;

        [Header("Hybrid framing (keeps the route visible)")]
        [Tooltip("Scale of the physical 3D dashboard while the photographic cab is still used as the window surround.")]
        [SerializeField] [Range(0.35f, 1f)] private float cockpitHybridScale = 0.68f;
        [Tooltip("Moves the physical 3D dashboard downward in the hybrid view. Negative values leave more of the windscreen unobstructed.")]
        [SerializeField] [Range(-5f, 0f)] private float cockpitHybridVerticalOffset = -2f;
        [Tooltip("The prototype's full 3D windscreen shell is kept off during the staged transition: the proven photographic frame remains the window mask.")]
        [SerializeField] private bool showCockpitWindowShellInHybrid;

        public GameObject PrototypeRoutePrefab => prototypeRoutePrefab;
        public RenderPipelineAsset Pipeline(CabWorldQuality quality) => quality == CabWorldQuality.Performance ? performancePipeline : balancedPipeline;
        public Shader LitShader => litShader;
        public Shader UnlitTextureShader => unlitTextureShader;
        public Shader UnlitTransparentShader => unlitTransparentShader;
        public Shader SpriteShader => spriteShader;
        public Shader AdditiveShader => additiveShader;
        public Shader RainWiperShader => rainWiperShader;
        public Shader MountainFeatherShader => mountainFeatherShader;
        public Shader SkyboxShader => skyboxShader;
        public Shader WorldTextShader => worldTextShader;
        public Shader TerrainShader => terrainShader;
        public Shader ImpostorBakeShader => impostorBakeShader;
        public Shader GlintShader => glintShader;
        public int RenderWidth(CabWorldQuality quality) => quality == CabWorldQuality.Performance ? performanceWidth : balancedWidth;
        public int RenderHeight(CabWorldQuality quality) => quality == CabWorldQuality.Performance ? performanceHeight : balancedHeight;
        public float CameraFieldOfView => cameraFieldOfView;
        public float FarClip => farClip;
        public float ShadowDistance(CabWorldQuality quality) => quality == CabWorldQuality.Performance ? performanceShadowDistance : balancedShadowDistance;
        public float SceneryDensity(CabWorldQuality quality) => quality == CabWorldQuality.Performance ? performanceSceneryDensity : balancedSceneryDensity;
        public Color ClearSky => clearSky;
        public Color Sunlight => sunlight;
        public float SunlightIntensity => sunlightIntensity;
        public float CockpitPhotoOverlayOpacity => cockpitPhotoOverlayOpacity;
        public int CockpitRenderWidth(CabWorldQuality quality) => quality == CabWorldQuality.Performance ? cockpitPerformanceWidth : cockpitBalancedWidth;
        public int CockpitRenderHeight(CabWorldQuality quality) => quality == CabWorldQuality.Performance ? cockpitPerformanceHeight : cockpitBalancedHeight;
        public float CockpitCameraFieldOfView => cockpitCameraFieldOfView;
        public float CockpitCabinLightIntensity => cockpitCabinLightIntensity;
        public float CockpitAccessibleOverlayOpacity => cockpitAccessibleOverlayOpacity;
        public float CockpitHybridScale => cockpitHybridScale;
        public float CockpitHybridVerticalOffset => cockpitHybridVerticalOffset;
        public bool ShowCockpitWindowShellInHybrid => showCockpitWindowShellInHybrid;

#if UNITY_EDITOR
        public void ConfigurePrototype(GameObject prefab) => prototypeRoutePrefab = prefab;

        public void ConfigureRendering(RenderPipelineAsset performance, RenderPipelineAsset balanced,
            Shader lit, Shader unlitTexture, Shader unlitTransparent, Shader sprite, Shader additive,
            Shader rainWiper, Shader mountainFeather, Shader skybox, Shader worldText, Shader terrain, Shader impostorBake, Shader glint)
        {
            performancePipeline = performance;
            balancedPipeline = balanced;
            litShader = lit;
            unlitTextureShader = unlitTexture;
            unlitTransparentShader = unlitTransparent;
            spriteShader = sprite;
            additiveShader = additive;
            rainWiperShader = rainWiper;
            mountainFeatherShader = mountainFeather;
            skyboxShader = skybox;
            worldTextShader = worldText;
            terrainShader = terrain;
            impostorBakeShader = impostorBake;
            glintShader = glint;
        }
#endif
    }
}
