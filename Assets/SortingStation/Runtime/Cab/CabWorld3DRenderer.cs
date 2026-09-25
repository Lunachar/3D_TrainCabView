using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SortingStation
{
    public sealed class CabWorld3DRenderer : MonoBehaviour, ICabWorldView
    {
        private CabRideDefinition ride;
        private CabSceneryCatalog catalog;
        private UserPreferences preferences;
        private CabWorld3DSettings settings;
        private CabJourneyRuntime journey;
        private RectTransform viewport;
        private RectTransform skyLayer;
        private RectTransform horizonLayer;
        private RenderTexture renderTexture;
        private RawImage worldOutput;
        private Camera worldCamera;
        // Immersive mode: the world lives outside the UI canvas and the camera draws straight to
        // the screen from inside the cab. The rig carries the cab; the head (camera) can look around.
        private bool immersive;
        private Transform sceneRoot;
        private Transform driverRig;
        private CabSkyAndFog atmosphereSky;
        private CabTrainConsist consist;
        private CabRearMirrors mirrors;
        public const float DriverEyeHeight = 3.25f;
        public const float DriverHeadPitch = 5f;
        private Light sun;
        private Light headlight;
        private Transform nativeSkyRoot;
        private Transform nativeSunDisc;
        private Transform nativeMoonDisc;
        private Renderer nativeSunHalo;
        private Renderer nativeSunGlare;
        private Renderer[] nativeClouds;
        private Renderer[] nativeMountains;
        private bool nativeMountainsUseTexture;
        private Renderer[] nativeStars;
        // Set every frame by the journey's EnvironmentClock; advances on its own only when the
        // renderer runs without a journey (previews and tests).
        private float dayTime01 = 0.30f;
        private bool dayTimeProvided;
        private const float StandaloneDaySeconds = 720f;
        private WeatherType nativeWeather = WeatherType.Clear;
        private Transform routeRoot;
        private Cab3DRouteAuthoring authoring;
        private readonly List<Cab3DInteractiveObject> interactives = new List<Cab3DInteractiveObject>();
        private float crossingDistance = 385f;
        private Color atmosphereTint = Color.clear;
        // The ride switches to the URP asset of the chosen profile and restores the previous one.
        private UnityEngine.Rendering.RenderPipelineAsset previousPipeline;
        private bool pipelineOverridden;

        public event Action<RouteSegmentDefinition> SegmentChanged;
        public event Action<CabAmbientSoundRequest> AmbientSoundRequested;
        public float TunnelBlend { get; private set; }
        public float Distance => journey != null ? journey.Distance : 0f;
        public float CycleLength => journey != null ? journey.CycleLength : 0f;
        public float SegmentProgress => journey != null ? journey.SegmentProgress : 0f;
        public RouteSegmentDefinition CurrentSegment => journey != null ? journey.CurrentSegment : null;
        public string CurrentSegmentName => CurrentSegment != null ? CurrentSegment.DisplayName : "3D-маршрут";
        public RectTransform Viewport => viewport;
        public Camera WorldCamera => worldCamera;
        public Transform DriverRig => driverRig;
        private Transform SceneRoot => sceneRoot != null ? sceneRoot : transform;
        public RectTransform SkyEffectsLayer => skyLayer != null ? skyLayer : viewport;
        public RectTransform HorizonEffectsLayer => horizonLayer != null ? horizonLayer : viewport;
        public bool DrawsOwnSky => true;

        public static bool ShouldShowNativeSun(bool badWeather, float sunArc, bool isInsideTunnel)
        {
            return !badWeather && !isInsideTunnel && sunArc > 0.075f;
        }

        /// <summary>
        /// Immersive 3D: no render texture and no window mask. The world is rendered full screen
        /// by the driver's camera; the UI canvas (an overlay) stays on top for the HUD.
        /// </summary>
        public void InitializeImmersive(RectTransform stage, CabRideDefinition definition, CabSceneryCatalog sceneryCatalog,
            UserPreferences userPreferences)
        {
            ride = definition;
            catalog = sceneryCatalog;
            preferences = userPreferences ?? new UserPreferences();
            settings = Resources.Load<CabWorld3DSettings>("Configuration/CabWorld3DSettings");
            immersive = true;
            journey = new CabJourneyRuntime(catalog != null ? catalog.RouteSegments : null, ride != null ? ride.RouteSeed : 1);
            journey.SegmentChanged += OnJourneySegmentChanged;

            // The journey's 2D effects still need UI layers; in immersive mode they span the stage.
            viewport = UiFactory.Panel("World3DViewport", stage, Color.clear);
            UiFactory.Stretch(viewport);
            viewport.GetComponent<Image>().raycastTarget = false;
            skyLayer = CreateLayer("SkyEffects", viewport);
            horizonLayer = CreateLayer("HorizonEffects", viewport);
            // The journey's flat 2D scenery, sun glare and weather sheets were drawn for the photo
            // window; over a full-screen 3D cab they would cover the dashboard. They stay hidden
            // until weather and route events are rebuilt in 3D.
            viewport.gameObject.SetActive(false);

            sceneRoot = new GameObject("CabWorld3DScene").transform;
            BuildCameraAndLight();
            BuildRoute();
            ApplyRoutePose();
            // The procedural sky draws the sun itself; the old sphere and its glow shells go.
            if (nativeSunDisc != null) Destroy(nativeSunDisc.gameObject);
            if (nativeSunHalo != null) Destroy(nativeSunHalo.gameObject);
            if (nativeSunGlare != null) Destroy(nativeSunGlare.gameObject);
            nativeSunDisc = null;
            nativeSunHalo = null;
            nativeSunGlare = null;
            // Real hills now reach the horizon; the painted backdrop hills would float in front.
            if (nativeMountains != null)
                for (int i = 0; i < nativeMountains.Length; i++)
                    if (nativeMountains[i] != null) nativeMountains[i].gameObject.SetActive(false);
            atmosphereSky = new CabSkyAndFog(worldCamera, sun, worldCamera.farClipPlane);
            // The locomotive body around the cab is only for the mirrors; from inside it would
            // just hide the view.
            worldCamera.cullingMask &= ~(1 << CabTrainConsist.ExteriorLayer);
            mirrors = sceneRoot.gameObject.AddComponent<CabRearMirrors>();
            mirrors.Build(driverRig, CabCockpitFactory.ControlLayer);
        }

        public void Initialize(RectTransform stage, CabRideDefinition definition, CabSceneryCatalog sceneryCatalog,
            UserPreferences userPreferences)
        {
            ride = definition;
            catalog = sceneryCatalog;
            preferences = userPreferences ?? new UserPreferences();
            settings = Resources.Load<CabWorld3DSettings>("Configuration/CabWorld3DSettings");
            journey = new CabJourneyRuntime(catalog != null ? catalog.RouteSegments : null, ride != null ? ride.RouteSeed : 1);
            journey.SegmentChanged += OnJourneySegmentChanged;

            viewport = UiFactory.Panel("World3DViewport", stage, Color.black);
            UiFactory.SetRect(viewport, new Vector2(0.112f, 0.465f), new Vector2(0.888f, 0.865f), Vector2.zero, Vector2.zero);
            viewport.GetComponent<Image>().raycastTarget = false;
            viewport.gameObject.AddComponent<RectMask2D>();
            GameObject outputObject = new GameObject("World3DRenderTexture", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            outputObject.transform.SetParent(viewport, false);
            worldOutput = outputObject.GetComponent<RawImage>();
            UiFactory.Stretch(worldOutput.rectTransform);
            worldOutput.raycastTarget = false;
            worldOutput.color = Color.white;
            worldOutput.enabled = false;
            skyLayer = CreateLayer("SkyEffects", viewport);
            horizonLayer = CreateLayer("HorizonEffects", viewport);

            int width = settings != null ? settings.RenderWidth(preferences.cabWorldQuality) : 1280;
            int height = settings != null ? settings.RenderHeight(preferences.cabWorldQuality) : 512;
            renderTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
            {
                name = "CabWorld3D_" + width + "x" + height,
                antiAliasing = preferences.cabWorldQuality == CabWorldQuality.Performance ? 1 : 2,
                useMipMap = false,
                autoGenerateMips = false
            };
            renderTexture.Create();
            worldOutput.texture = renderTexture;

            BuildCameraAndLight();
            BuildRoute();
            ApplyRoutePose();
            // Force the complete first view before revealing the texture. This prevents a frame of
            // empty terrain while the old editable prefab is being replaced with the current route.
            worldCamera?.Render();
            if (worldOutput != null) worldOutput.enabled = true;
        }

        public void Advance(float speed01, float acceleration01, float unscaledDeltaTime)
        {
            if (journey == null || ride == null) return;
            float delta = CabWorldRenderer.CalculateDistanceDelta(speed01, ride.WorldUnitsPerSecond, unscaledDeltaTime);
            journey.Advance(delta);
            ApplyRoutePose();
            TunnelBlend = CurrentSegment != null && CurrentSegment.Type == RouteSegmentType.MountainTunnel
                ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - Mathf.Abs(SegmentProgress - 0.55f) / 0.45f)) : 0f;
            float ahead = Mathf.Repeat(crossingDistance - Distance, journey.CycleLength);
            authoring?.SetCrossingApproach(ahead);
            if (worldCamera != null)
            {
                worldCamera.backgroundColor = Color.Lerp(settings != null ? settings.ClearSky : new Color(0.5f, 0.75f, 0.9f),
                    new Color(0.055f, 0.06f, 0.065f), TunnelBlend);
                worldCamera.farClipPlane = settings != null ? settings.FarClip : 240f;
            }
            if (sun != null) sun.intensity = Mathf.Lerp(settings != null ? settings.SunlightIntensity : 0.85f, 0.12f, TunnelBlend);
            UpdateNativeSky(unscaledDeltaTime);
            authoring?.SetTunnelLighting(TunnelBlend > 0.02f);
        }

        public void ApplySeason(SeasonThemeDefinition season)
        {
            if (season == null) return;
            authoring?.ApplySeason(season.season);
            if (!immersive) return;
            if (routeRoot.Find(CabWorldExtras.HillsName) == null) CabWorldExtras.BuildHills(routeRoot, journey.CycleLength);
            CabWorldPbrUpgrade.Apply(routeRoot, season.season);
            CabVegetation.Populate(routeRoot, season.season);
            CabWorldExtras.BuildVergeGrass(routeRoot, journey.CycleLength,
                settings != null ? settings.SceneryDensity(preferences.cabWorldQuality) : 1f, season.season);
            // Last: copy the finished first and last stretches past the ends of the loop.
            CabWorldExtras.BuildLapContinuation(routeRoot, journey.CycleLength,
                worldCamera != null ? worldCamera.farClipPlane + 30f : 270f, 90f);
            // The train is added after the loop copies so it is not copied with the scenery.
            if (consist == null) consist = new CabTrainConsist(routeRoot);
            consist.UpdatePose(Distance, journey.CycleLength);
        }

        public void SetDayTime(float time01)
        {
            dayTime01 = Mathf.Repeat(time01, 1f);
            dayTimeProvided = true;
        }

        public void SetAtmosphere(Color sky, Color tint)
        {
            atmosphereTint = tint;
            if (worldCamera != null) worldCamera.backgroundColor = Color.Lerp(sky, tint, tint.a * 0.45f);
        }

        public void SetHeadlights(bool enabled)
        {
            if (headlight != null) headlight.enabled = enabled;
            authoring?.SetHeadlights(enabled);
        }
        public void SetWeather(WeatherType weather) => nativeWeather = weather;
        public void SetStationPhase(CabStationPhase phase)
        {
            authoring?.SetStationPhase(phase);
            // Mirrors refresh faster while passengers get on and off.
            if (mirrors != null)
                mirrors.FastRefresh = phase == CabStationPhase.Approaching || phase == CabStationPhase.WaitingForDoors ||
                                      phase == CabStationPhase.DoorsOpen || phase == CabStationPhase.Releasing;
        }
        public void SetUpcomingStation(CabStationDefinition station) => authoring?.SetStationName(station != null ? station.DisplayName : "Станция");
        public void SetPassengerReport(CabPassengerStopReport report) => authoring?.SetPassengerReport(report);
        public void SetPassengerWeather(WeatherType weather) => authoring?.SetPassengerWeather(weather);

        public bool HasVisibleScenery(string idFragment)
        {
            if (string.IsNullOrWhiteSpace(idFragment)) return true;
            for (int i = 0; i < interactives.Count; i++)
                if (interactives[i] != null && interactives[i].isActiveAndEnabled &&
                    interactives[i].InteractionId.IndexOf(idFragment, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        public bool TryReactToScenery(string idFragment, CabInteractionReaction reaction, float durationSeconds = 2.5f)
        {
            for (int i = 0; i < interactives.Count; i++)
            {
                Cab3DInteractiveObject item = interactives[i];
                if (item == null || !item.isActiveAndEnabled) continue;
                if (!string.IsNullOrWhiteSpace(idFragment) &&
                    item.InteractionId.IndexOf(idFragment, StringComparison.OrdinalIgnoreCase) < 0) continue;
                item.React(reaction, durationSeconds);
                return true;
            }
            return string.IsNullOrWhiteSpace(idFragment);
        }

        public void SetPreviewSegment(RouteSegmentType type, float progress)
        {
            if (journey != null && journey.SetSegment(type, progress, true)) ApplyRoutePose();
        }

        public void SetPreviewDistance(float distance)
        {
            journey?.SetDistance(distance);
            ApplyRoutePose();
        }

        private void BuildCameraAndLight()
        {
            UnityEngine.Rendering.RenderPipelineAsset profilePipeline = settings != null ? settings.Pipeline(preferences.cabWorldQuality) : null;
            if (profilePipeline != null && !pipelineOverridden)
            {
                previousPipeline = QualitySettings.renderPipeline;
                QualitySettings.renderPipeline = profilePipeline;
                pipelineOverridden = true;
            }
            GameObject cameraObject = new GameObject("CabWorld3DCamera");
            if (immersive)
            {
                driverRig = new GameObject("DriverRig").transform;
                driverRig.SetParent(SceneRoot, false);
                // The driver's eye sits exactly over the train's route position: anywhere behind
                // it the curved track would run beside the cab (and through tunnel walls).
                driverRig.localPosition = new Vector3(0f, DriverEyeHeight, 0f);
                cameraObject.name = "DriverHeadCamera";
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(driverRig, false);
            }
            else
            {
                cameraObject.transform.SetParent(transform, false);
            }
            worldCamera = cameraObject.AddComponent<Camera>();
            worldCamera.transform.localPosition = immersive ? Vector3.zero : new Vector3(0f, 3.35f, -8f);
            worldCamera.transform.localRotation = Quaternion.Euler(immersive ? DriverHeadPitch : 16.5f, 0f, 0f);
            worldCamera.fieldOfView = settings != null ? settings.CameraFieldOfView : 61f;
            worldCamera.nearClipPlane = immersive ? 0.04f : 0.1f;
            worldCamera.farClipPlane = settings != null ? settings.FarClip : 240f;
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.backgroundColor = settings != null ? settings.ClearSky : new Color(0.5f, 0.75f, 0.9f);
            worldCamera.targetTexture = immersive ? null : renderTexture;
            if (immersive)
            {
                worldCamera.fieldOfView = 58f;
                worldCamera.depth = -1f;
            }
            worldCamera.allowHDR = false;
            worldCamera.allowMSAA = preferences.cabWorldQuality != CabWorldQuality.Performance;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.46f, 0.50f, 0.53f);
            RenderSettings.ambientIntensity = 1f;

            GameObject lightObject = new GameObject("CabWorld3DSun");
            lightObject.transform.SetParent(SceneRoot, false);
            lightObject.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
            sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = settings != null ? settings.Sunlight : new Color(1f, 0.93f, 0.78f);
            sun.intensity = settings != null ? settings.SunlightIntensity : 0.86f;
            sun.shadows = preferences.cabWorldQuality == CabWorldQuality.Performance ? LightShadows.None : LightShadows.Hard;

            GameObject headlightObject = new GameObject("TrainHeadlight3D");
            headlightObject.transform.SetParent(SceneRoot, false);
            headlightObject.transform.localPosition = new Vector3(0f, 2.15f, -2.8f);
            headlightObject.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);
            headlight = headlightObject.AddComponent<Light>();
            headlight.type = LightType.Spot;
            headlight.color = new Color(1f, 0.86f, 0.60f);
            headlight.intensity = 1.25f;
            // The lamp is intentionally local: it should pool on the first part of the track,
            // rather than creating a flat yellow cone all the way to the horizon.
            headlight.range = 34f;
            headlight.spotAngle = 28f;
            headlight.shadows = LightShadows.None;
            headlight.enabled = false;
            BuildNativeSky(immersive ? driverRig : cameraObject.transform);
        }

        private void BuildNativeSky(Transform cameraTransform)
        {
            nativeSkyRoot = new GameObject("NativeSkyAndDistantLandscape").transform;
            nativeSkyRoot.SetParent(cameraTransform, false);
            nativeSkyRoot.localPosition = Vector3.zero;

            Texture2D distantHills = Resources.Load<Texture2D>("Cab3D/DistantHills_v1");
            Shader distantLayerShader = CabShaders.UnlitTransparent;
            if (distantHills != null && distantLayerShader != null)
            {
                distantHills.wrapMode = TextureWrapMode.Clamp;
                GameObject ridge = GameObject.CreatePrimitive(PrimitiveType.Quad);
                ridge.name = "DistantForestedRidge";
                ridge.transform.SetParent(nativeSkyRoot, false);
                ridge.transform.localPosition = new Vector3(0f, -8f, 184f);
                ridge.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                ridge.transform.localScale = new Vector3(210f, 70f, 1f);
                Material ridgeMaterial = new Material(distantLayerShader)
                {
                    name = "DistantForestedRidgeMaterial",
                    mainTexture = distantHills,
                    color = new Color(0.82f, 0.89f, 0.98f, 0.92f)
                };
                Renderer ridgeRenderer = ridge.GetComponent<Renderer>();
                ridgeRenderer.sharedMaterial = ridgeMaterial;
                ridgeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ridgeRenderer.receiveShadows = false;
                Destroy(ridge.GetComponent<Collider>());
                nativeMountains = new[] { ridgeRenderer };
                nativeMountainsUseTexture = true;
            }
            else
            {
                Material mountainMaterial = new Material(CabShaders.Lit) { color = new Color(0.23f, 0.31f, 0.36f) };
                nativeMountains = new Renderer[7];
                for (int i = 0; i < nativeMountains.Length; i++)
                {
                    GameObject ridge = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    ridge.name = "DistantMountainFallback_" + i;
                    ridge.transform.SetParent(nativeSkyRoot, false);
                    ridge.transform.localPosition = new Vector3(-70f + i * 23f, -8f + (i % 3) * 2.5f, 175f + (i % 2) * 11f);
                    ridge.transform.localScale = new Vector3(31f, 25f + (i % 3) * 7f, 22f);
                    nativeMountains[i] = ridge.GetComponent<Renderer>();
                    nativeMountains[i].sharedMaterial = mountainMaterial;
                    nativeMountains[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    nativeMountains[i].receiveShadows = false;
                    Destroy(ridge.GetComponent<Collider>());
                }
            }

            Texture2D cloudAtlas = Resources.Load<Texture2D>("Cab3D/CloudSpriteAtlas_v1");
            if (cloudAtlas != null) cloudAtlas.wrapMode = TextureWrapMode.Clamp;
            nativeClouds = new Renderer[8];
            for (int i = 0; i < nativeClouds.Length; i++)
            {
                bool useCloudAtlas = cloudAtlas != null && CabShaders.UnlitTransparent != null;
                GameObject cloud = GameObject.CreatePrimitive(useCloudAtlas ? PrimitiveType.Quad : PrimitiveType.Sphere);
                cloud.name = "DistantCloud_" + i;
                cloud.transform.SetParent(nativeSkyRoot, false);
                cloud.transform.localPosition = new Vector3(-70f + i * 20f, 18f + (i % 3) * 6f, 142f + (i % 2) * 8f);
                cloud.transform.localScale = useCloudAtlas
                    ? new Vector3(30f + (i % 3) * 7f, 10f + (i % 2) * 2.2f, 1f)
                    : new Vector3(26f, 3.4f + (i % 2), 8f);
                nativeClouds[i] = cloud.GetComponent<Renderer>();
                if (useCloudAtlas)
                {
                    // Quads face toward the cab camera and crop one of the atlas's six cloud
                    // silhouettes. The transparent margins keep neighbouring cells from bleeding.
                    cloud.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    Material cloudMaterial = new Material(CabShaders.UnlitTransparent)
                    {
                        name = "DistantCloudMaterial_" + i,
                        mainTexture = cloudAtlas,
                        color = new Color(0.88f, 0.93f, 0.98f, 0.76f)
                    };
                    cloudMaterial.mainTextureScale = new Vector2(0.31f, 0.45f);
                    int column = i % 3;
                    int row = i / 3;
                    cloudMaterial.mainTextureOffset = new Vector2(column / 3f + 0.012f, row == 0 ? 0.525f : 0.025f);
                    nativeClouds[i].sharedMaterial = cloudMaterial;
                }
                else
                {
                    nativeClouds[i].sharedMaterial = new Material(CabShaders.Lit)
                    {
                        name = "DistantCloudMaterialFallback_" + i,
                        color = new Color(0.82f, 0.86f, 0.88f)
                    };
                }
                nativeClouds[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                nativeClouds[i].receiveShadows = false;
                Destroy(cloud.GetComponent<Collider>());
            }

            Material sunMaterial = new Material(CabShaders.Lit) { color = new Color(1f, 0.68f, 0.24f) };
            GameObject nativeSun = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nativeSun.name = "MovingSun";
            nativeSun.transform.SetParent(nativeSkyRoot, false);
            nativeSun.transform.localScale = Vector3.one * 3.2f;
            nativeSunDisc = nativeSun.transform;
            nativeSun.GetComponent<Renderer>().sharedMaterial = sunMaterial;
            nativeSun.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            nativeSun.GetComponent<Renderer>().receiveShadows = false;
            Destroy(nativeSun.GetComponent<Collider>());

            GameObject nativeMoon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nativeMoon.name = "MoonAtDusk";
            nativeMoon.transform.SetParent(nativeSkyRoot, false);
            nativeMoon.transform.localScale = Vector3.one * 2.25f;
            nativeMoonDisc = nativeMoon.transform;
            Renderer moonRenderer = nativeMoon.GetComponent<Renderer>();
            moonRenderer.sharedMaterial = new Material(CabShaders.Lit) { color = new Color(0.62f, 0.72f, 0.88f) };
            moonRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            moonRenderer.receiveShadows = false;
            Destroy(nativeMoon.GetComponent<Collider>());

            Shader starShader = CabShaders.Additive;
            if (starShader != null)
            {
                Material starMaterial = new Material(starShader) { name = "DistantStars", color = new Color(0.72f, 0.84f, 1f, 0.58f) };
                nativeStars = new Renderer[18];
                for (int i = 0; i < nativeStars.Length; i++)
                {
                    GameObject star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    star.name = "DistantStar_" + i;
                    star.transform.SetParent(nativeSkyRoot, false);
                    star.transform.localPosition = new Vector3(-76f + Mathf.Repeat(i * 17.9f, 152f), 10f + (i % 5) * 6.2f, 157f + (i % 3) * 3f);
                    float size = 0.16f + (i % 3) * 0.055f;
                    star.transform.localScale = Vector3.one * size;
                    nativeStars[i] = star.GetComponent<Renderer>();
                    nativeStars[i].sharedMaterial = starMaterial;
                    nativeStars[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    nativeStars[i].receiveShadows = false;
                    Destroy(star.GetComponent<Collider>());
                }
            }

            // Two inexpensive additive shells give the sun a soft, child-friendly glare when it
            // appears from behind the horizon. Unlike a screen-space flare they remain naturally
            // behind route geometry and cost almost nothing on the tablet profile.
            Shader glowShader = CabShaders.Additive;
            if (glowShader != null)
            {
                Material glowMaterial = new Material(glowShader)
                {
                    name = "SunHorizonGlow",
                    color = new Color(1f, 0.42f, 0.08f, 0.12f)
                };
                nativeSunHalo = CreateSkyGlow("SunHalo", nativeSkyRoot, glowMaterial, 7.0f);
                nativeSunGlare = CreateSkyGlow("SunGlare", nativeSkyRoot, glowMaterial, 11.5f);
            }
        }

        private static Renderer CreateSkyGlow(string name, Transform parent, Material material, float size)
        {
            GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = name;
            glow.transform.SetParent(parent, false);
            glow.transform.localScale = Vector3.one * size;
            Renderer renderer = glow.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(material) { name = name + "Material" };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Destroy(glow.GetComponent<Collider>());
            return renderer;
        }

        private void UpdateNativeSky(float deltaTime)
        {
            if (nativeSkyRoot == null) return;
            if (!dayTimeProvided)
                dayTime01 = Mathf.Repeat(dayTime01 + Mathf.Max(0f, deltaTime) / StandaloneDaySeconds, 1f);
            float sunTravel = EnvironmentClock.SunTravel(dayTime01);
            float arc = EnvironmentClock.SunArc(dayTime01);
            bool badWeather = nativeWeather == WeatherType.Rain || nativeWeather == WeatherType.Snow || nativeWeather == WeatherType.Fog;
            bool isInsideTunnel = CurrentSegment != null && CurrentSegment.Type == RouteSegmentType.MountainTunnel;
            float nightAmount = 1f - EnvironmentClock.Daylight(dayTime01);
            if (worldCamera != null)
            {
                Color daytime = settings != null ? settings.ClearSky : new Color(0.50f, 0.75f, 0.91f);
                Color sky = Color.Lerp(new Color(0.018f, 0.035f, 0.085f), daytime, Mathf.SmoothStep(0.06f, 0.42f, arc));
                if (badWeather) sky = Color.Lerp(sky, new Color(0.20f, 0.26f, 0.31f), 0.52f);
                worldCamera.backgroundColor = Color.Lerp(sky, new Color(0.055f, 0.06f, 0.065f), TunnelBlend);
            }
            if (nativeSunDisc != null)
            {
                nativeSunDisc.localPosition = new Vector3(Mathf.Lerp(-68f, 68f, sunTravel), -4f + arc * 39f, 165f);
                bool sunVisible = ShouldShowNativeSun(badWeather, arc, isInsideTunnel);
                nativeSunDisc.gameObject.SetActive(sunVisible);
                float horizonGlow = Mathf.Clamp01(1f - arc * 2.6f);
                UpdateSunGlow(nativeSunHalo, nativeSunDisc.localPosition, sunVisible ? horizonGlow : 0f, badWeather, isInsideTunnel);
                UpdateSunGlow(nativeSunGlare, nativeSunDisc.localPosition + new Vector3(0f, 0f, 0.35f), sunVisible ? horizonGlow * horizonGlow : 0f, badWeather, isInsideTunnel);
            }
            if (nativeMoonDisc != null)
            {
                nativeMoonDisc.localPosition = new Vector3(Mathf.Lerp(62f, -62f, EnvironmentClock.MoonTravel(dayTime01)), 9f + nightAmount * 20f, 164f);
                nativeMoonDisc.gameObject.SetActive(!badWeather && !isInsideTunnel && nightAmount > 0.035f);
            }
            if (nativeStars != null)
            {
                bool starsVisible = !badWeather && !isInsideTunnel && nightAmount > 0.08f;
                for (int i = 0; i < nativeStars.Length; i++)
                    if (nativeStars[i] != null) nativeStars[i].gameObject.SetActive(starsVisible);
            }
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(18f + arc * 50f, Mathf.Lerp(-68f, 62f, sunTravel), 0f);
                sun.intensity = Mathf.Lerp(0.38f, settings != null ? settings.SunlightIntensity : 0.86f, arc) * (badWeather ? 0.44f : 1f) * Mathf.Lerp(1f, 0.2f, TunnelBlend);
            }
            if (nativeClouds != null)
                for (int i = 0; i < nativeClouds.Length; i++)
                {
                    Renderer cloud = nativeClouds[i];
                    if (cloud == null) continue;
                    Vector3 p = cloud.transform.localPosition;
                    p.x = -78f + Mathf.Repeat(i * 21.5f + Time.unscaledTime * (badWeather ? 0.8f : 0.28f), 170f);
                    cloud.transform.localPosition = p;
                    cloud.gameObject.SetActive(badWeather || i % 3 != 0);
                    cloud.sharedMaterial.color = badWeather
                        ? new Color(0.50f, 0.57f, 0.64f, 0.90f)
                        : new Color(0.88f, 0.93f, 0.98f, 0.76f);
                }
            if (nativeMountains != null)
                for (int i = 0; i < nativeMountains.Length; i++)
                    if (nativeMountains[i] != null)
                    {
                        if (nativeMountainsUseTexture)
                        {
                            Color daylightTint = Color.Lerp(new Color(0.43f, 0.51f, 0.64f, 0.70f),
                                new Color(0.86f, 0.91f, 0.98f, 0.94f), arc);
                            Color overcastTint = new Color(0.65f, 0.70f, 0.75f, 0.82f);
                            nativeMountains[i].sharedMaterial.color = badWeather
                                ? Color.Lerp(daylightTint, overcastTint, 0.58f)
                                : daylightTint;
                        }
                        else
                        {
                            nativeMountains[i].sharedMaterial.color = Color.Lerp(new Color(0.11f, 0.16f, 0.20f),
                                new Color(0.30f, 0.40f, 0.46f), arc * (badWeather ? 0.55f : 1f));
                        }
                    }
            // Route lamps are intentionally separate from the driver's headlight.  They wake up
            // at dusk, during poor weather and inside the tunnel, while remaining free of shadows
            // for Redmi Pad 2's performance profile.
            authoring?.SetScenicNightLighting(arc < 0.42f || badWeather || TunnelBlend > 0.18f);
            atmosphereSky?.Update(arc, nightAmount, badWeather, TunnelBlend);
        }

        private void UpdateSunGlow(Renderer glow, Vector3 position, float glowAmount, bool badWeather, bool isInsideTunnel)
        {
            if (glow == null) return;
            bool visible = !badWeather && !isInsideTunnel && glowAmount > 0.015f;
            glow.gameObject.SetActive(visible);
            if (!visible) return;
            glow.transform.localPosition = position;
            Material material = glow.sharedMaterial;
            if (material != null)
                material.color = Color.Lerp(new Color(1f, 0.78f, 0.30f, 0.035f), new Color(1f, 0.20f, 0.035f, 0.30f), glowAmount);
        }

        private void BuildRoute()
        {
            GameObject prefab = settings != null ? settings.PrototypeRoutePrefab : null;
            Cab3DRouteAuthoring prefabAuthoring = prefab != null ? prefab.GetComponent<Cab3DRouteAuthoring>() : null;
            bool useFallback = prefab == null || prefabAuthoring == null || !prefabAuthoring.HasPassengerStations ||
                               prefabAuthoring.BuiltRouteVersion < CabWorld3DPrototypeFactory.CurrentRouteVersion ||
                               !MatchesJourneyCycle(prefabAuthoring);
            GameObject route = useFallback ? CabWorld3DPrototypeFactory.Create(SceneRoot,
                journey.CycleLength, settings != null ? settings.SceneryDensity(preferences.cabWorldQuality) : 1f)
                : Instantiate(prefab, SceneRoot);
            Cab3DRouteAuthoring candidate = route.GetComponent<Cab3DRouteAuthoring>();
            // A project may be opened between code changes and the editable prefab can still be an
            // earlier version. Keep the ride functional until the editor rebuild command updates it.
            if (!useFallback && (candidate == null || !candidate.HasPassengerStations ||
                candidate.BuiltRouteVersion < CabWorld3DPrototypeFactory.CurrentRouteVersion ||
                !MatchesJourneyCycle(candidate)))
            {
                Destroy(route);
                route = CabWorld3DPrototypeFactory.Create(SceneRoot, journey.CycleLength,
                    settings != null ? settings.SceneryDensity(preferences.cabWorldQuality) : 1f);
            }
            route.name = prefab != null ? "Editable3DRoute" : "RuntimeFallback3DRoute";
            routeRoot = route.transform;
            authoring = route.GetComponent<Cab3DRouteAuthoring>();
            if (authoring == null) authoring = route.AddComponent<Cab3DRouteAuthoring>();
            interactives.AddRange(route.GetComponentsInChildren<Cab3DInteractiveObject>(true));
        }

        private void ApplyRoutePose()
        {
            if (routeRoot == null || journey == null) return;
            float cycle = journey.CycleLength;
            Vector3 point = Cab3DTrackMath.Point(Distance, cycle);
            Quaternion heading = Cab3DTrackMath.Heading(Distance, cycle);
            routeRoot.localRotation = Quaternion.Inverse(heading);
            routeRoot.localPosition = routeRoot.localRotation * -point;
            consist?.UpdatePose(Distance, cycle);
        }

        private bool MatchesJourneyCycle(Cab3DRouteAuthoring routeAuthoring)
        {
            return routeAuthoring != null && journey != null &&
                   Mathf.Abs(routeAuthoring.CycleLength - journey.CycleLength) <= 0.01f;
        }

        private void OnJourneySegmentChanged(RouteSegmentDefinition segment)
        {
            SegmentChanged?.Invoke(segment);
            CabAmbientSound cue = segment != null ? AmbientFor(segment.Type) : CabAmbientSound.Meadow;
            AmbientSoundRequested?.Invoke(new CabAmbientSoundRequest(cue,
                segment != null && ride != null ? CabWorldRenderer.SegmentAmbientDuration(segment, ride) : 0f));
        }

        private static CabAmbientSound AmbientFor(RouteSegmentType type)
        {
            return type switch
            {
                RouteSegmentType.Forest => CabAmbientSound.Forest,
                RouteSegmentType.Road => CabAmbientSound.RoadTraffic,
                RouteSegmentType.Village => CabAmbientSound.Village,
                RouteSegmentType.Town => CabAmbientSound.City,
                RouteSegmentType.Water => CabAmbientSound.River,
                RouteSegmentType.MountainTunnel => CabAmbientSound.TunnelInterior,
                _ => CabAmbientSound.Meadow
            };
        }

        private static RectTransform CreateLayer(string name, Transform parent)
        {
            GameObject layer = new GameObject(name, typeof(RectTransform));
            layer.transform.SetParent(parent, false);
            RectTransform rect = layer.GetComponent<RectTransform>();
            UiFactory.Stretch(rect);
            return rect;
        }

        private void OnDestroy()
        {
            if (journey != null) journey.SegmentChanged -= OnJourneySegmentChanged;
            if (sceneRoot != null) Destroy(sceneRoot.gameObject);
            atmosphereSky?.Dispose();
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
            if (pipelineOverridden)
            {
                QualitySettings.renderPipeline = previousPipeline;
                pipelineOverridden = false;
            }
        }
    }
}
