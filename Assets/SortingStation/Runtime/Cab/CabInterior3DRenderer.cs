using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SortingStation
{
    /// <summary>
    /// Renders the first, editable 3D part of the cab into a transparent UI layer.  The legacy
    /// accessible touch targets stay available above it, while physical props receive direct taps
    /// through camera-mapped hit areas.
    /// </summary>
    public sealed class CabInterior3DRenderer : MonoBehaviour
    {
        private const int CockpitLayer = 30;
        private CabWorld3DSettings cockpitSettings;
        private readonly Dictionary<CabControlAction, List<Transform>> anchors = new Dictionary<CabControlAction, List<Transform>>();
        private readonly Dictionary<Transform, Quaternion> restRotations = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, Vector3> restPositions = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<CabControlAction, float> pressedUntil = new Dictionary<CabControlAction, float>();
        private RenderTexture renderTexture;
        private Camera interiorCamera;
        private RectTransform outputRect;
        private System.Action<CabControlAction> physicalControlHandler;
        private System.Action dispatcherAcknowledgementHandler;
        private Light keyLight;
        private Light cabinGlowLight;
        private Light instrumentGlowLight;
        private Transform vigilanceButton;
        private Vector3 vigilanceButtonRestPosition;
        private Renderer vigilanceButtonRenderer;
        private Material vigilanceButtonMaterial;
        private Transform keychainCharm;
        private Quaternion keychainCharmRestRotation;
        private Transform interior;
        private Vector3 interiorRestPosition;
        private Quaternion interiorRestRotation;
        private TextMesh speedReadout;
        private TextMesh stateReadout;
        private Material speedScreenMaterial;
        private Material stateScreenMaterial;
        private readonly List<Transform> gaugeNeedles = new List<Transform>();
        private readonly List<Transform> wiperArms = new List<Transform>();
        private CabWindscreenWeather windscreenWeather;

        /// <summary>Rain or snow on the windscreen; sheltered (in a tunnel) stops new drops landing.</summary>
        public void DryWindscreen()
        {
            if (windscreenWeather == null && interior != null) windscreenWeather = interior.GetComponentInChildren<CabWindscreenWeather>(true);
            if (windscreenWeather != null) windscreenWeather.Dry();
        }

        public void SetWeather(WeatherType weather, float intensity, bool sheltered)
        {
            if (windscreenWeather == null && interior != null) windscreenWeather = interior.GetComponentInChildren<CabWindscreenWeather>(true);
            if (windscreenWeather == null) return;
            windscreenWeather.SetWeather(weather, intensity);
            windscreenWeather.SetSheltered(sheltered);
        }
        private readonly List<Renderer> ceilingLampRenderers = new List<Renderer>();
        private readonly List<Material> ceilingLampMaterials = new List<Material>();
        private readonly Dictionary<CabControlAction, Material> indicatorMaterials = new Dictionary<CabControlAction, Material>();
        private readonly Dictionary<Transform, Quaternion> gaugeRestRotations = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, Quaternion> wiperRestRotations = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<int, BoxCollider> screenRectCache = new Dictionary<int, BoxCollider>();
        // The hybrid prototype is modelled in large units; the immersive cab is in metres, so
        // button travel and cabin sway are scaled down there. Sway also follows the Motion setting.
        private float pressTravelScale = 1f;
        private float swayScale = 1f;
        private bool immersive;
        private bool screenTextsProvided;
        private string speedScreenText = string.Empty;
        private string infoScreenText = string.Empty;

        /// <summary>Full texts for the two desk screens (immersive cab); replaces the short defaults.</summary>
        public void SetScreenTexts(string speedScreen, string infoScreen)
        {
            screenTextsProvided = true;
            speedScreenText = speedScreen ?? string.Empty;
            infoScreenText = infoScreen ?? string.Empty;
        }

        /// <summary>
        /// Immersive mode: the realistic cab is built in metres around the driver's eye and seen
        /// directly by the main camera, which also renders the world. No render texture is used.
        /// </summary>
        public void InitializeImmersive(Transform driverRig, Camera driverCamera, MotionLevel motion,
            System.Action<CabControlAction> onPhysicalControl, System.Action onDispatcherAcknowledgement)
        {
            if (driverRig == null || driverCamera == null || interiorCamera != null) return;
            cockpitSettings = Resources.Load<CabWorld3DSettings>("Configuration/CabWorld3DSettings");
            interiorCamera = driverCamera;
            physicalControlHandler = onPhysicalControl;
            dispatcherAcknowledgementHandler = onDispatcherAcknowledgement;
            immersive = true;
            pressTravelScale = 0.16f;
            swayScale = motion == MotionLevel.Off ? 0f : motion == MotionLevel.Reduced ? 0.04f : 0.12f;

            interior = CabCockpitFactory.Create(driverRig).transform;
            CabWorldText.Apply(interior);
            interiorRestPosition = interior.localPosition;
            interiorRestRotation = interior.localRotation;
            CacheAnchors();

            cabinGlowLight = CreateCabLight("CabInteriorWarmGlow", new Vector3(0f, 0.52f, 0.1f),
                new Color(1f, 0.72f, 0.42f), 0f, 3.2f);
            // Soft daylight bouncing in through the windscreen; keeps the desk readable without
            // a hot spot. It doubles as the instrument glow at night (see SetState).
            instrumentGlowLight = CreateCabLight("CabInteriorDaylightFill", new Vector3(0f, 0.35f, 0.25f),
                new Color(0.92f, 0.95f, 1f), 0.75f, 2.6f);
        }

        /// <summary>Hit test for the immersive cab, straight from a screen position.</summary>
        public static bool TryHitControlOnScreen(Camera rayCamera, Vector2 screenPoint,
            out CabControlAction action, out bool isDispatcherAcknowledgement)
        {
            action = default;
            isDispatcherAcknowledgement = false;
            if (rayCamera == null) return false;
            return TryHitNearestControl(rayCamera.ScreenPointToRay(screenPoint), rayCamera.farClipPlane,
                out action, out isDispatcherAcknowledgement);
        }

        public void Initialize(RectTransform parent, CabWorldQuality quality, System.Action<CabControlAction> onPhysicalControl,
            System.Action onDispatcherAcknowledgement)
        {
            if (parent == null || interiorCamera != null) return;
            cockpitSettings = Resources.Load<CabWorld3DSettings>("Configuration/CabWorld3DSettings");

            GameObject imageObject = new GameObject("CabInterior3DOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imageObject.transform.SetParent(parent, false);
            RawImage output = imageObject.GetComponent<RawImage>();
            output.raycastTarget = true;
            outputRect = output.rectTransform;
            physicalControlHandler = onPhysicalControl;
            dispatcherAcknowledgementHandler = onDispatcherAcknowledgement;
            EventTrigger pointerSurface = imageObject.AddComponent<EventTrigger>();
            EventTrigger.Entry pointerClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            pointerClick.callback.AddListener(data =>
            {
                PointerEventData pointer = data as PointerEventData;
                if (pointer != null && TryHitControl(interiorCamera, outputRect, pointer.position,
                        out CabControlAction action, out bool isDispatcherAcknowledgement))
                    ActivatePhysicalControl(action, isDispatcherAcknowledgement);
            });
            pointerSurface.triggers.Add(pointerClick);
            EventTrigger.Entry pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            pointerDown.callback.AddListener(data =>
            {
                PointerEventData pointer = data as PointerEventData;
                if (pointer != null) BeginControlPressFromPointer(pointer.position);
            });
            pointerSurface.triggers.Add(pointerDown);
            output.color = new Color(1f, 1f, 1f, 0.97f);
            RectTransform rect = output.rectTransform;
            // Keep a full, correctly proportioned render target.  The physical dashboard itself
            // is framed lower down below; cropping the RawImage would stretch it and would make
            // the still-photographic windscreen visibly mismatch.
            rect.anchorMin = new Vector2(0.075f, 0.030f);
            rect.anchorMax = new Vector2(0.925f, 0.885f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            // A 3:2 target matches the broad cab view and avoids stretching the physical
            // windshield pillars. It stays independent from the more expensive 3D world target.
            int width = cockpitSettings != null ? cockpitSettings.CockpitRenderWidth(quality) : quality == CabWorldQuality.Performance ? 720 : 960;
            int height = cockpitSettings != null ? cockpitSettings.CockpitRenderHeight(quality) : quality == CabWorldQuality.Performance ? 480 : 640;
            renderTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
            {
                name = "CabInterior3D_" + width + "x" + height,
                useMipMap = false,
                autoGenerateMips = false
            };
            renderTexture.Create();
            output.texture = renderTexture;

            GameObject cameraObject = new GameObject("CabInterior3DCamera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, -0.15f, -13.5f);
            interiorCamera = cameraObject.AddComponent<Camera>();
            interiorCamera.targetTexture = renderTexture;
            interiorCamera.clearFlags = CameraClearFlags.SolidColor;
            interiorCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            interiorCamera.cullingMask = 1 << CockpitLayer;
            interiorCamera.fieldOfView = cockpitSettings != null ? cockpitSettings.CockpitCameraFieldOfView : 50f;
            interiorCamera.nearClipPlane = 0.08f;
            interiorCamera.farClipPlane = 32f;
            interiorCamera.allowHDR = false;
            interiorCamera.allowMSAA = quality != CabWorldQuality.Performance;

            // The factory is the safe first-run fallback. Once the artist opens the dedicated
            // prototype scene and saves its prefab, the ride uses that exact hand-edited layout.
            // This keeps runtime assembly free of Editor APIs while making visual placement real.
            GameObject editablePrefab = Resources.Load<GameObject>("Cab3D/CabInterior3DPrototype");
            GameObject interiorObject = editablePrefab != null
                ? Object.Instantiate(editablePrefab, transform)
                : CabInterior3DPrototypeFactory.Create(transform);
            interiorObject.name = editablePrefab != null ? "CabInterior3D_EditableInstance" : "CabInterior3D_RuntimeFallback";
            interior = interiorObject.transform;
            ApplyHybridFraming();
            interiorRestPosition = interior.localPosition;
            interiorRestRotation = interior.localRotation;
            SetLayerRecursively(interior.gameObject, CockpitLayer);
            CacheAnchors();

            GameObject lightObject = new GameObject("CabInterior3DKeyLight");
            lightObject.transform.SetParent(cameraObject.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(20f, -24f, 0f);
            keyLight = lightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(0.79f, 0.88f, 1f);
            // Keep the physical panel readable in daylight even before the player turns on
            // cabin lighting. The warm cabin lamp remains a separate tunnel/night effect.
            keyLight.intensity = 1.12f;
            keyLight.shadows = LightShadows.None;

            cabinGlowLight = CreateCabLight("CabInterior3DWarmGlow", new Vector3(-4.6f, 2.9f, -1.6f),
                new Color(1f, 0.53f, 0.18f), 0f, 9.5f);
            instrumentGlowLight = CreateCabLight("CabInterior3DInstrumentGlow", new Vector3(3.8f, 1.8f, -0.8f),
                new Color(0.18f, 0.72f, 1f), 0.18f, 7.5f);

            interiorCamera.Render();
        }

        /// <summary>
        /// This is deliberately a reversible staging step: the 3D controls and lower dashboard
        /// are visible, while the existing correctly-aligned photograph continues to provide the
        /// windscreen frame.  Enabling the shell remains an Inspector option for the final pass.
        /// </summary>
        private void ApplyHybridFraming()
        {
            if (interior == null) return;

            float scale = cockpitSettings != null ? cockpitSettings.CockpitHybridScale : 0.68f;
            float verticalOffset = cockpitSettings != null ? cockpitSettings.CockpitHybridVerticalOffset : -2f;
            interior.localScale *= scale;
            interior.localPosition += Vector3.up * verticalOffset;

            bool showShell = cockpitSettings != null && cockpitSettings.ShowCockpitWindowShellInHybrid;
            if (!showShell)
            {
                Transform shell = FindDeepChild(interior, "CabShellAndWindow_Editable");
                if (shell != null) shell.gameObject.SetActive(false);

                // The original photo already contains properly aligned, large wipers and their
                // rain-cleaning effect.  The prototype wipers are technical placement props, so
                // rendering both made an implausible second row across the windscreen.
                Transform prototypeWipers = FindDeepChild(interior, "WipersOnGlass_Editable");
                if (prototypeWipers != null) prototypeWipers.gameObject.SetActive(false);
            }
        }

        public void SetState(float traction01, float brake01, float speedKph, bool cabinLightOn, bool headlightsOn,
            bool wipersOn, bool radioOn, bool doorsOpen, bool windowHeaterOn, string statusText, float deltaTime)
        {
            if (interiorCamera == null) return;
            AnimateLever(CabControlAction.Throttle, Mathf.Lerp(-42f, 36f, Mathf.Clamp01(traction01)), deltaTime);
            AnimateLever(CabControlAction.Brake, Mathf.Lerp(20f, -48f, Mathf.Clamp01(brake01)), deltaTime);
            AnimateToggle(CabControlAction.CabinLight, cabinLightOn, deltaTime);
            AnimateToggle(CabControlAction.Headlights, headlightsOn, deltaTime);
            AnimateToggle(CabControlAction.Wipers, wipersOn, deltaTime);
            AnimateToggle(CabControlAction.Radio, radioOn, deltaTime);
            AnimateToggle(CabControlAction.Doors, doorsOpen, deltaTime);
            AnimateToggle(CabControlAction.WindowHeater, windowHeaterOn, deltaTime);
            AnimateWipers(wipersOn, deltaTime);
            if (windscreenWeather == null && interior != null) windscreenWeather = interior.GetComponentInChildren<CabWindscreenWeather>(true);
            windscreenWeather?.SetWipers(wipersOn);
            AnimateVigilanceButton(statusText, deltaTime);
            if (keyLight != null)
                keyLight.intensity = Mathf.MoveTowards(keyLight.intensity, cabinLightOn ? 1.34f : 1.12f, deltaTime * 2.8f);
            if (cabinGlowLight != null)
                cabinGlowLight.intensity = Mathf.MoveTowards(cabinGlowLight.intensity,
                    cabinLightOn ? cockpitSettings != null ? cockpitSettings.CockpitCabinLightIntensity : 1.05f : 0f, deltaTime * 3.4f);
            if (instrumentGlowLight != null && !immersive)
                instrumentGlowLight.intensity = Mathf.MoveTowards(instrumentGlowLight.intensity,
                    headlightsOn || cabinLightOn ? 0.52f : 0.18f, deltaTime * 3.4f);
            SetCeilingLampGlow(cabinLightOn);
            AnimateCabinSway(traction01, brake01, speedKph, deltaTime);
            if (screenTextsProvided)
            {
                if (speedReadout != null) speedReadout.text = speedScreenText;
                if (stateReadout != null) stateReadout.text = infoScreenText;
            }
            else if (speedReadout != null) speedReadout.text = Mathf.RoundToInt(speedKph).ToString("000") + " km/h";
            if (stateReadout != null && !screenTextsProvided)
                stateReadout.text = string.IsNullOrWhiteSpace(statusText)
                    ? (headlightsOn ? "ФАРЫ ГОТОВЫ" : cabinLightOn ? "КАБИНА ГОТОВА" : "СИСТЕМА ГОТОВА")
                    : statusText;
            AnimateGauges(speedKph, traction01, brake01, deltaTime);
            SetIndicator(CabControlAction.CabinLight, cabinLightOn, new Color(1f, 0.42f, 0.08f));
            SetIndicator(CabControlAction.Headlights, headlightsOn, new Color(0.22f, 0.62f, 1f));
            SetIndicator(CabControlAction.Wipers, wipersOn, new Color(0.34f, 0.82f, 1f));
            SetIndicator(CabControlAction.Radio, radioOn, new Color(0.18f, 0.95f, 0.52f));
            SetIndicator(CabControlAction.Doors, doorsOpen, new Color(0.30f, 1f, 0.45f));
            SetIndicator(CabControlAction.WindowHeater, windowHeaterOn, new Color(1f, 0.48f, 0.12f));
            SetScreenGlow(cabinLightOn, headlightsOn, radioOn, deltaTime);
            AnimatePhysicalButtonPresses(deltaTime);
        }

        /// <summary>
        /// Gives the matching physical prop immediate visual feedback for pointer and keyboard input.
        /// </summary>
        public void PressControl(CabControlAction action)
        {
            if (action == CabControlAction.Throttle || action == CabControlAction.Brake) return;
            pressedUntil[action] = Time.unscaledTime + 0.16f;
        }

        public static bool TryHitControl(Camera rayCamera, RectTransform viewport, Vector2 screenPoint, out CabControlAction action)
        {
            return TryHitControl(rayCamera, viewport, screenPoint, out action, out _);
        }

        public static bool TryHitControl(Camera rayCamera, RectTransform viewport, Vector2 screenPoint,
            out CabControlAction action, out bool isDispatcherAcknowledgement)
        {
            action = default;
            isDispatcherAcknowledgement = false;
            Camera eventCamera = GetEventCamera(viewport);
            if (rayCamera == null || viewport == null ||
                !RectTransformUtility.RectangleContainsScreenPoint(viewport, screenPoint, eventCamera) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screenPoint, eventCamera, out Vector2 local))
                return false;

            Rect rect = viewport.rect;
            if (rect.width <= 0f || rect.height <= 0f) return false;
            float x = Mathf.Clamp01((local.x - rect.xMin) / rect.width);
            float y = Mathf.Clamp01((local.y - rect.yMin) / rect.height);
            return TryHitNearestControl(rayCamera.ViewportPointToRay(new Vector3(x, y, 0f)), rayCamera.farClipPlane,
                out action, out isDispatcherAcknowledgement);
        }

        private static bool TryHitNearestControl(Ray ray, float distance, out CabControlAction action,
            out bool isDispatcherAcknowledgement)
        {
            action = default;
            isDispatcherAcknowledgement = false;
            Physics.SyncTransforms();
            RaycastHit[] hits = Physics.RaycastAll(ray, distance, 1 << CockpitLayer,
                QueryTriggerInteraction.Ignore);
            Cab3DControlAnchor nearestAnchor = null;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                Cab3DControlAnchor candidate = hit.collider != null
                    ? hit.collider.GetComponentInParent<Cab3DControlAnchor>() : null;
                // The rendered console is assembled from meshes with colliders of its own.
                // Ignore those decorative surfaces and select the nearest actual control instead
                // of letting an unrelated panel silently swallow the player's tap.
                if (candidate == null || !candidate.IsTouchable || hit.distance >= nearestDistance) continue;
                nearestAnchor = candidate;
                nearestDistance = hit.distance;
            }
            if (nearestAnchor == null) return false;
            action = nearestAnchor.Action;
            isDispatcherAcknowledgement = nearestAnchor.IsDispatcherAcknowledgement;
            return true;
        }

        public bool TryHitControl(Vector2 screenPoint, out CabControlAction action)
        {
            return TryHitControl(screenPoint, out action, out _);
        }

        public bool TryHitControl(Vector2 screenPoint, out CabControlAction action, out bool isDispatcherAcknowledgement)
        {
            if (outputRect == null)
                return TryHitControlOnScreen(interiorCamera, screenPoint, out action, out isDispatcherAcknowledgement);
            return TryHitControl(interiorCamera, outputRect, screenPoint, out action, out isDispatcherAcknowledgement);
        }

        /// <summary>
        /// Screen rectangle covered by a control's touch area (immersive mode). The accessible
        /// button is laid over it so taps and the keyboard focus outline line up with the 3D prop.
        /// </summary>
        public bool TryGetControlScreenRect(CabControlAction action, bool dispatcherAcknowledgement, out Rect screenRect)
        {
            screenRect = default;
            if (interiorCamera == null || interior == null) return false;
            int key = (int)action * 2 + (dispatcherAcknowledgement ? 1 : 0);
            if (!screenRectCache.TryGetValue(key, out BoxCollider hitArea))
            {
                hitArea = FindHitArea(action, dispatcherAcknowledgement);
                screenRectCache[key] = hitArea;
            }
            if (hitArea == null) return false;
            Vector3 center = hitArea.center;
            Vector3 extents = hitArea.size * 0.5f;
            Transform owner = hitArea.transform;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 local = center + new Vector3(
                    (corner & 1) == 0 ? -extents.x : extents.x,
                    (corner & 2) == 0 ? -extents.y : extents.y,
                    (corner & 4) == 0 ? -extents.z : extents.z);
                Vector3 screen = interiorCamera.WorldToScreenPoint(owner.TransformPoint(local));
                if (screen.z <= 0f) return false;
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }
            screenRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        private BoxCollider FindHitArea(CabControlAction action, bool dispatcherAcknowledgement)
        {
            Cab3DControlAnchor[] found = interior.GetComponentsInChildren<Cab3DControlAnchor>(true);
            for (int i = 0; i < found.Length; i++)
            {
                Cab3DControlAnchor anchor = found[i];
                if (anchor == null || !anchor.IsTouchable || anchor.Action != action ||
                    anchor.IsDispatcherAcknowledgement != dispatcherAcknowledgement) continue;
                BoxCollider hitArea = anchor.GetComponent<BoxCollider>();
                if (hitArea != null && hitArea.enabled) return hitArea;
            }
            return null;
        }

        /// <summary>Starts the physical prop's press animation as soon as its displayed surface is touched.</summary>
        public bool BeginControlPressFromPointer(Vector2 screenPoint)
        {
            if (!TryHitControl(screenPoint, out CabControlAction action, out bool isDispatcherAcknowledgement)) return false;
            PressControl(isDispatcherAcknowledgement ? CabControlAction.DispatcherRadio : action);
            return true;
        }

        private static Camera GetEventCamera(RectTransform viewport)
        {
            Canvas canvas = viewport != null ? viewport.GetComponentInParent<Canvas>() : null;
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        }

        private void ActivatePhysicalControl(CabControlAction action, bool isDispatcherAcknowledgement)
        {
            AppServices.Instance?.Audio.Play(SoundCue.Tap);
            if (isDispatcherAcknowledgement) dispatcherAcknowledgementHandler?.Invoke();
            else physicalControlHandler?.Invoke(action);
        }

        public void SetKeychainAngle(float degrees)
        {
            if (keychainCharm == null) return;
            keychainCharm.localRotation = Quaternion.Euler(0f, 0f, degrees) * keychainCharmRestRotation;
        }

        private void CacheAnchors()
        {
            if (interior == null) return;
            Cab3DControlAnchor[] found = interior.GetComponentsInChildren<Cab3DControlAnchor>(true);
            for (int i = 0; i < found.Length; i++)
            {
                Cab3DControlAnchor anchor = found[i];
                if (anchor == null) continue;
                if (!anchor.IsTouchable) continue;
                anchor.EnsureHitArea();
                Transform target = anchor.AnimatedPart;
                if (!anchors.TryGetValue(anchor.Action, out List<Transform> actionTargets))
                {
                    actionTargets = new List<Transform>();
                    anchors.Add(anchor.Action, actionTargets);
                }
                if (target != null && !actionTargets.Contains(target)) actionTargets.Add(target);
                if (target != null && !restRotations.ContainsKey(target)) restRotations.Add(target, target.localRotation);
                if (target != null && !restPositions.ContainsKey(target)) restPositions.Add(target, target.localPosition);
            }
            TextMesh[] labels = interior.GetComponentsInChildren<TextMesh>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null) continue;
                if (labels[i].name.StartsWith("SpeedScreen3D")) speedReadout = labels[i];
                else if (labels[i].name.StartsWith("MessageScreen3D")) stateReadout = labels[i];
            }
            Transform[] transforms = interior.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform item = transforms[i];
                if (item == null) continue;
                if (item.name == "GaugeNeedle")
                {
                    gaugeNeedles.Add(item);
                    gaugeRestRotations[item] = item.localRotation;
                }
                else if (item.name.StartsWith("WiperArm3D"))
                {
                    wiperArms.Add(item);
                    wiperRestRotations[item] = item.localRotation;
                }
                else if (item.name.StartsWith("CeilingCabinLamp3D"))
                {
                    Renderer lamp = item.GetComponent<Renderer>();
                    if (lamp != null)
                    {
                        EnableEmission(lamp);
                        ceilingLampRenderers.Add(lamp);
                        Material lampMaterial = lamp.material;
                        if (lampMaterial != null)
                        {
                            lampMaterial.EnableKeyword("_EMISSION");
                            ceilingLampMaterials.Add(lampMaterial);
                        }
                    }
                }
                else if (item.name == "SpeedScreen3D")
                {
                    Renderer renderer = item.GetComponent<Renderer>();
                    EnableEmission(renderer);
                    speedScreenMaterial = renderer != null ? renderer.material : null;
                }
                else if (item.name == "MessageScreen3D")
                {
                    Renderer renderer = item.GetComponent<Renderer>();
                    EnableEmission(renderer);
                    stateScreenMaterial = renderer != null ? renderer.material : null;
                }
            }
            CacheIndicator(CabControlAction.CabinLight, "CabinLight3D");
            CacheIndicator(CabControlAction.Headlights, "Headlights3D");
            CacheIndicator(CabControlAction.Wipers, "Wipers3D");
            CacheIndicator(CabControlAction.Radio, "Radio3D");
            CacheIndicator(CabControlAction.Doors, "Doors3D");
            CacheIndicator(CabControlAction.WindowHeater, "WindowHeater3D");
            vigilanceButton = FindDeepChild(interior, "VigilanceButton3D");
            if (vigilanceButton != null)
            {
                vigilanceButtonRestPosition = vigilanceButton.localPosition;
                vigilanceButtonRenderer = vigilanceButton.GetComponent<Renderer>();
                // This one material is intentionally instanced once.  GetPropertyBlock was
                // throwing a native "dest" exception every frame for this generated mesh in
                // some Unity 2022 editor sessions, flooding the Console and obscuring real
                // diagnostics.  A persistent material avoids the unsupported native path.
                if (vigilanceButtonRenderer != null)
                {
                    vigilanceButtonMaterial = vigilanceButtonRenderer.material;
                    if (vigilanceButtonMaterial != null) vigilanceButtonMaterial.EnableKeyword("_EMISSION");
                }
            }
            keychainCharm = FindDeepChild(interior, "KeychainCharm3D");
            if (keychainCharm != null) keychainCharmRestRotation = keychainCharm.localRotation;
        }

        private Light CreateCabLight(string name, Vector3 localPosition, Color color, float intensity, float range)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(interior, false);
            lightObject.transform.localPosition = localPosition;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        private void AnimateCabinSway(float traction01, float brake01, float speedKph, float deltaTime)
        {
            if (interior == null) return;
            float speed01 = Mathf.Clamp01(speedKph / 120f);
            float railVibration = Mathf.Sin(Time.unscaledTime * (4.2f + speed01 * 8.5f)) * speed01;
            float accelerationLean = traction01 * 0.26f - brake01 * 0.40f;
            Vector3 targetPosition = interiorRestPosition +
                new Vector3(railVibration * 0.035f, Mathf.Cos(Time.unscaledTime * 2.7f) * speed01 * 0.022f, 0f) * swayScale;
            Quaternion targetRotation = interiorRestRotation *
                Quaternion.Euler(railVibration * 0.65f * swayScale, 0f, accelerationLean * swayScale);
            interior.localPosition = Vector3.Lerp(interior.localPosition, targetPosition, Mathf.Clamp01(deltaTime * 5.5f));
            interior.localRotation = Quaternion.Slerp(interior.localRotation, targetRotation, Mathf.Clamp01(deltaTime * 4.2f));
        }

        private void SetCeilingLampGlow(bool cabinLightOn)
        {
            Color emission = cabinLightOn
                ? new Color(1f, 0.43f, 0.12f) * (cockpitSettings != null ? cockpitSettings.CockpitCabinLightIntensity : 1.05f)
                : new Color(0.018f, 0.024f, 0.035f);
            // Use the persistent instances created for the two prototype lamps.  In this Unity
            // version Renderer.GetPropertyBlock occasionally fails for inactive generated props,
            // producing a Console error every frame after the hybrid shell is hidden.
            for (int i = 0; i < ceilingLampMaterials.Count; i++)
            {
                Material lamp = ceilingLampMaterials[i];
                if (lamp == null) continue;
                lamp.SetColor("_EmissionColor", emission);
            }
        }

        private void CacheIndicator(CabControlAction action, string name)
        {
            if (interior == null) return;
            Transform item = FindDeepChild(interior, name);
            if (item == null) return;
            Renderer renderer = item.GetComponent<Renderer>();
            if (renderer != null)
            {
                EnableEmission(renderer);
                Material material = renderer.material;
                if (material != null)
                {
                    material.EnableKeyword("_EMISSION");
                    indicatorMaterials[action] = material;
                }
            }
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent == null) return null;
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeepChild(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private void AnimateLever(CabControlAction action, float degrees, float deltaTime)
        {
            if (!anchors.TryGetValue(action, out List<Transform> targets)) return;
            for (int i = 0; i < targets.Count; i++)
            {
                Transform target = targets[i];
                if (target == null || !restRotations.TryGetValue(target, out Quaternion rest)) continue;
                Quaternion desired = rest * Quaternion.Euler(degrees, 0f, 0f);
                target.localRotation = Quaternion.Slerp(target.localRotation, desired, Mathf.Clamp01(deltaTime * 10f));
            }
        }

        private void AnimateToggle(CabControlAction action, bool enabled, float deltaTime)
        {
            if (!anchors.TryGetValue(action, out List<Transform> targets)) return;
            for (int i = 0; i < targets.Count; i++)
            {
                Transform target = targets[i];
                if (target == null || !restRotations.TryGetValue(target, out Quaternion rest)) continue;
                Quaternion desired = rest * Quaternion.Euler(enabled ? -22f : 8f, 0f, 0f);
                target.localRotation = Quaternion.Slerp(target.localRotation, desired, Mathf.Clamp01(deltaTime * 9f));
            }
        }

        private void AnimatePhysicalButtonPresses(float deltaTime)
        {
            foreach (KeyValuePair<CabControlAction, List<Transform>> entry in anchors)
            {
                bool held = pressedUntil.TryGetValue(entry.Key, out float until) && Time.unscaledTime < until;
                // The camera looks toward +Z, so a positive local-Z offset seats a button into
                // the dashboard. Switches receive a smaller travel than the round controls.
                float travel = (entry.Key == CabControlAction.Doors || entry.Key == CabControlAction.Wipers ? 0.028f : 0.075f) * pressTravelScale;
                for (int i = 0; i < entry.Value.Count; i++)
                {
                    Transform target = entry.Value[i];
                    if (target == null || !restPositions.TryGetValue(target, out Vector3 rest)) continue;
                    Vector3 desired = rest + (held ? Vector3.forward * travel : Vector3.zero);
                    target.localPosition = Vector3.Lerp(target.localPosition, desired,
                        Mathf.Clamp01(deltaTime * (held ? 30f : 15f)));
                }
            }
        }

        private void AnimateGauges(float speedKph, float traction01, float brake01, float deltaTime)
        {
            for (int i = 0; i < gaugeNeedles.Count; i++)
            {
                Transform needle = gaugeNeedles[i];
                if (needle == null || !gaugeRestRotations.TryGetValue(needle, out Quaternion rest)) continue;
                float targetDegrees = i % 3 == 0 ? Mathf.Lerp(-45f, 46f, Mathf.Clamp01(speedKph / 120f))
                    : i % 3 == 1 ? Mathf.Lerp(-35f, 40f, traction01)
                    : Mathf.Lerp(-30f, 42f, brake01);
                needle.localRotation = Quaternion.Slerp(needle.localRotation, rest * Quaternion.Euler(0f, 0f, targetDegrees),
                    Mathf.Clamp01(deltaTime * 7f));
            }
        }

        private void AnimateWipers(bool enabled, float deltaTime)
        {
            for (int i = 0; i < wiperArms.Count; i++)
            {
                Transform arm = wiperArms[i];
                if (arm == null || !wiperRestRotations.TryGetValue(arm, out Quaternion rest)) continue;
                float direction = arm.name.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0 ? 1f : -1f;
                // The immersive cab parks its wipers flat, so they sweep up from there and back.
                float sweep = !enabled ? 0f
                    : immersive ? (0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * 2.75f)) * 78f * direction
                    : Mathf.Sin(Time.unscaledTime * 2.75f) * 34f * direction;
                arm.localRotation = Quaternion.Slerp(arm.localRotation, rest * Quaternion.Euler(0f, 0f, sweep),
                    Mathf.Clamp01(deltaTime * (enabled ? 16f : 7f)));
            }
        }

        private void AnimateVigilanceButton(string statusText, float deltaTime)
        {
            if (vigilanceButton == null) return;
            bool requiresConfirmation = !string.IsNullOrWhiteSpace(statusText) &&
                (statusText.IndexOf("ПРОВЕРКА", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                 statusText.IndexOf("НАЖМИ", System.StringComparison.OrdinalIgnoreCase) >= 0);
            float flash = requiresConfirmation ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6.4f) : 0f;
            Vector3 targetPosition = vigilanceButtonRestPosition +
                Vector3.forward * (requiresConfirmation ? 0.012f + flash * 0.026f : 0.075f) * pressTravelScale;
            vigilanceButton.localPosition = Vector3.Lerp(vigilanceButton.localPosition, targetPosition, Mathf.Clamp01(deltaTime * 12f));
            if (vigilanceButtonMaterial == null) return;
            vigilanceButtonMaterial.SetColor("_EmissionColor", requiresConfirmation
                ? Color.Lerp(new Color(0.48f, 0.015f, 0.01f), new Color(1f, 0.10f, 0.035f), flash) * 1.4f
                : new Color(0.16f, 0.006f, 0.004f));
        }

        private void SetIndicator(CabControlAction action, bool enabled, Color emission)
        {
            if (!indicatorMaterials.TryGetValue(action, out Material material) || material == null) return;
            material.SetColor("_EmissionColor", enabled ? emission * 1.35f : Color.black);
            material.color = enabled ? Color.Lerp(Color.white, emission, 0.72f) : new Color(0.14f, 0.16f, 0.18f);
        }

        private void SetScreenGlow(bool cabinLightOn, bool headlightsOn, bool radioOn, float deltaTime)
        {
            float power = cabinLightOn || headlightsOn ? 1f : 0.62f;
            Color speedGlow = new Color(0.03f, 0.52f, 0.13f) * (0.24f * power);
            Color statusGlow = (radioOn ? new Color(0.03f, 0.38f, 0.16f) : new Color(0.03f, 0.42f, 0.10f)) * (0.20f * power);
            SetScreenMaterial(speedScreenMaterial, speedGlow);
            SetScreenMaterial(stateScreenMaterial, statusGlow);
        }

        private static void SetScreenMaterial(Material material, Color emission)
        {
            if (material == null) return;
            material.color = new Color(0.008f, 0.075f, 0.025f);
            material.SetColor("_EmissionColor", emission);
        }

        private static void EnableEmission(Renderer renderer)
        {
            if (renderer == null || renderer.sharedMaterial == null) return;
            // Only the few cockpit indicators are material instances; this makes their glow
            // visible in the Built-in pipeline without changing shared art assets.
            renderer.material.EnableKeyword("_EMISSION");
        }

        private static void SetLayerRecursively(GameObject value, int layer)
        {
            if (value == null) return;
            value.layer = layer;
            Transform parent = value.transform;
            for (int i = 0; i < parent.childCount; i++) SetLayerRecursively(parent.GetChild(i).gameObject, layer);
        }

        private void OnDestroy()
        {
            if (renderTexture == null) return;
            if (renderTexture.IsCreated()) renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}
