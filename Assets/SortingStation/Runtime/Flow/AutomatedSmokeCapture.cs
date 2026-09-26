using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SortingStation
{
    public sealed class AutomatedSmokeCapture : MonoBehaviour
    {
        private string outputDirectory;

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            Debug.Log("Smoke capture started.");
            outputDirectory = ReadArgument("-smokeOutput");
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                outputDirectory = Path.Combine(Application.persistentDataPath, "SmokeScreens");
            }
            Directory.CreateDirectory(outputDirectory);
            Debug.Log("Smoke output: " + outputDirectory);
            Screen.SetResolution(1600, 1000, FullScreenMode.Windowed);

            if (Array.Exists(Environment.GetCommandLineArgs(), arg => string.Equals(arg, "-rideTimeline", StringComparison.OrdinalIgnoreCase)))
            {
                yield return CaptureRideTimeline();
                yield break;
            }
            if (Array.Exists(Environment.GetCommandLineArgs(), arg => string.Equals(arg, "-routePreview", StringComparison.OrdinalIgnoreCase)))
            {
                yield return CaptureRoutePreview();
                yield break;
            }
            if (Array.Exists(Environment.GetCommandLineArgs(), arg => string.Equals(arg, "-departurePreview", StringComparison.OrdinalIgnoreCase)))
            {
                yield return CaptureDeparturePreview();
                yield break;
            }
            if (Array.Exists(Environment.GetCommandLineArgs(), arg => string.Equals(arg, "-trackPreview", StringComparison.OrdinalIgnoreCase)))
            {
                yield return CaptureTrackPreview();
                yield break;
            }
            if (Array.Exists(Environment.GetCommandLineArgs(), arg => string.Equals(arg, "-switchMotionPreview", StringComparison.OrdinalIgnoreCase)))
            {
                yield return CaptureSwitchMotionPreview();
                yield break;
            }
            if (Array.Exists(Environment.GetCommandLineArgs(), arg => string.Equals(arg, "-weatherPreview", StringComparison.OrdinalIgnoreCase)))
            {
                yield return CaptureWeatherPreview();
                yield break;
            }
            if (Array.Exists(Environment.GetCommandLineArgs(), arg => string.Equals(arg, "-radioUiPreview", StringComparison.OrdinalIgnoreCase)))
            {
                yield return CaptureRadioUiPreview();
                yield break;
            }
            if (Array.Exists(Environment.GetCommandLineArgs(), arg => string.Equals(arg, "-autumnLeavesPreview", StringComparison.OrdinalIgnoreCase)))
            {
                yield return CaptureAutumnLeavesPreview();
                yield break;
            }

            yield return WaitForScene(SceneNames.MainMenu);
            yield return Capture("01-main-menu.png");
            Debug.Log("Smoke captured main menu.");

            AppServices.Instance.Session.Select(GameMode.Colors, 4);
            SceneManager.LoadScene(SceneNames.SortingYard);
            yield return WaitForScene(SceneNames.SortingYard);
            yield return Capture("02-sorting-yard.png");
            Debug.Log("Smoke captured sorting yard.");

            AppServices.Instance.Session.Select(GameMode.CabRide, 2);
            SceneManager.LoadScene(SceneNames.CabRide);
            yield return WaitForScene(SceneNames.CabRide);
            CabRideController cab = FindObjectOfType<CabRideController>();
            if (cab != null) cab.ConfigureSmokeDemo(false);
            yield return new WaitForSecondsRealtime(2.2f);
            yield return Capture("03-cab-ride-1600x1000.png", 1600, 1000);
            yield return Capture("04-cab-ride-1920x1080.png", 1920, 1080);
            yield return Capture("05-cab-ride-1280x800.png", 1280, 800);
            yield return Capture("06-cab-ride-1024x768.png", 1024, 768);
            if (cab != null) cab.ConfigureSmokeDemo(true);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Capture("07-cab-tunnel-1600x1000.png", 1600, 1000);
            Debug.Log("Smoke captured cab ride.");

            Debug.Log("SMOKE_CAPTURE_COMPLETE=" + outputDirectory);
            yield return new WaitForSecondsRealtime(0.3f);
            Application.Quit();
        }

        private IEnumerator CaptureRideTimeline()
        {
            yield return WaitForScene(SceneNames.MainMenu);
            AppServices.Instance.Session.Select(GameMode.CabRide, 2);
            SceneManager.LoadScene(SceneNames.CabRide);
            yield return WaitForScene(SceneNames.CabRide);
            CabRideController cab = FindObjectOfType<CabRideController>();
            if (cab != null) cab.ConfigureSmokeDemo(false);
            yield return new WaitForSecondsRealtime(2.2f);
            yield return Capture("01-ride-00m.png");
            yield return new WaitForSecondsRealtime(60f);
            yield return Capture("02-ride-01m.png");
            yield return new WaitForSecondsRealtime(60f);
            yield return Capture("03-ride-02m.png");
            Debug.Log("RIDE_TIMELINE_COMPLETE=" + outputDirectory);
            yield return new WaitForSecondsRealtime(0.3f);
            Application.Quit();
        }

        private IEnumerator CaptureDeparturePreview()
        {
            yield return WaitForScene(SceneNames.MainMenu);
            AppServices.Instance.Session.Select(GameMode.CabRide, 2);
            SceneManager.LoadScene(SceneNames.CabRide);
            yield return WaitForScene(SceneNames.CabRide);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Capture("departure-ready.png", 1600, 1000);
            Debug.Log("DEPARTURE_PREVIEW_COMPLETE=" + outputDirectory);
            yield return new WaitForSecondsRealtime(0.2f);
            Application.Quit();
        }

        /// <summary>Fixed points of the 3D route: station sign and stop, inside the tunnel, end of the loop.</summary>
        private IEnumerator CaptureRoutePreview()
        {
            yield return WaitForScene(SceneNames.MainMenu);
            AppServices.Instance.Session.Select(GameMode.CabRide, 2);
            SceneManager.LoadScene(SceneNames.CabRide);
            yield return WaitForScene(SceneNames.CabRide);
            yield return new WaitForSecondsRealtime(1.5f);
            CabRideController cab = FindObjectOfType<CabRideController>();
            CabWorld3DRenderer view = FindObjectOfType<CabWorld3DRenderer>();
            WorldPlanner planner = view != null && view.Streamed != null ? view.Streamed.Planner : null;
            if (planner == null)
            {
                Debug.LogError("ROUTE_PREVIEW: no streamed world");
                Application.Quit();
                yield break;
            }
            float Find(Func<WorldChunkPlan, bool> match, float offset)
            {
                for (int i = 2; i < 800; i++)
                {
                    WorldChunkPlan plan = planner.Get(i);
                    if (match(plan)) return plan.Start + offset;
                }
                return 60f;
            }
            TreeImpostors.SaveAtlas(Path.Combine(outputDirectory, "00-tree-atlas.png"));
            float stop = planner.NextStopAfter(0f, out _);
            float tunnel = Find(p => p.TunnelEntrance, WorldPlanner.PortalInset);
            const float noon = 0.5f;
            const float midnight = 0.95f;
            float crossing = Find(p => p.Crossing && !p.IsStation, 15f);
            float city = Find(p => p.Kind == WorldChunkKind.City && !p.DenseLowRise, 20f);
            float dense = Find(p => p.Kind == WorldChunkKind.City && p.DenseLowRise, 20f);
            float terminal = Find(p => p.IsStation && p.Station == StationStyle.Terminal, WorldPlanner.PlatformCentre + WorldPlanner.StopOffsetPastPlatformCentre - 80f);
            float halt = Find(p => p.IsStation && p.Station == StationStyle.Halt, WorldPlanner.PlatformCentre + WorldPlanner.StopOffsetPastPlatformCentre - 60f);
            float townStation = Find(p => p.IsStation && p.Station == StationStyle.Town, WorldPlanner.PlatformCentre + WorldPlanner.StopOffsetPastPlatformCentre - 60f);
            (string file, float distance, bool lights, float time)[] extra =
            {
                ("15-crossing-queue.png", crossing, false, noon),
                ("16-meeting-train.png", Find(p => p.Kind == WorldChunkKind.Meadow || p.Kind == WorldChunkKind.Field, 10f), false, noon),
                ("17-city-night.png", city, true, midnight),
                ("18-dense-city.png", dense, false, noon),
                ("19-dense-city-night.png", dense, true, midnight),
                ("20-terminal.png", terminal, false, noon),
                ("21-terminal-night.png", terminal, true, midnight),
                ("22-halt-night.png", halt, true, midnight),
                ("23-town-station.png", townStation, false, noon),
                ("24-cave.png", Find(p => p.Kind == WorldChunkKind.Tunnel && p.Tunnel == TunnelStyle.Cave && !p.CaveHall && !p.TunnelEntrance, 40f), true, noon),
                ("25-gnome-hall.png", Find(p => p.CaveHall, 5f), true, noon),
                ("26-brick-portal.png", Find(p => p.TunnelEntrance && p.Tunnel == TunnelStyle.Brick, WorldPlanner.PortalInset - 55f), false, noon),
                ("27-station-event.png", Find(p => p.IsStation && p.Event != StationEvent.None, WorldPlanner.PlatformCentre - 20f), false, noon),
                ("28-station-event-night.png", Find(p => p.IsStation && p.Event != StationEvent.None, WorldPlanner.PlatformCentre - 20f), true, midnight),
                ("29-concrete-small.png", Find(p => p.Kind == WorldChunkKind.Tunnel && p.Tunnel == TunnelStyle.Concrete && p.Bore == TunnelSize.Small, 60f), true, noon)
            };
 
            float meadowView = Find(p => p.Kind == WorldChunkKind.Meadow || p.Kind == WorldChunkKind.Field, 40f);
            (string file, float time)[] skies = { ("30-sunrise.png", 0.1f), ("31-morning.png", 0.16f), ("32-noon.png", 0.5f), ("33-sunset.png", 0.78f), ("34-dusk.png", 0.82f), ("35-night-sky.png", 0.97f) };
            foreach ((string file, float time) in skies)
            {
                if (cab != null) cab.ConfigureDistancePreview(meadowView, time > 0.86f, time, true);
                yield return new WaitForSecondsRealtime(1f);
                yield return Capture(file, 1600, 1000);
            }
            (string file, float time, WeatherType weather, bool wipersOn)[] weathers =
            {
                ("36-rain-day.png", 0.45f, WeatherType.Rain, false), ("37-rain-wipers.png", 0.45f, WeatherType.Rain, true),
                ("38-rain-night.png", 0.95f, WeatherType.Rain, true), ("39-snow-day.png", 0.45f, WeatherType.Snow, false),
                ("40-snow-night.png", 0.95f, WeatherType.Snow, true), ("41-fog.png", 0.45f, WeatherType.Fog, false)
            };
            foreach ((string file, float time, WeatherType weather, bool wipersOn) in weathers)
            {
                if (cab != null) cab.ConfigureDistancePreview(meadowView, time > 0.86f, time, false, (int)weather, wipersOn);
                yield return new WaitForSecondsRealtime(6f);
                yield return Capture(file, 1600, 1000);
            }
            if (cab != null) cab.ConfigureDistancePreview(meadowView, false, 0.5f, true, -1, false);

            // People: waiting on the platform, looked at from the cab, then boarding and alighting.
            if (cab != null && cab.LookAround != null)
            {
                cab.ConfigureDistancePreview(stop - 32f, false, 0.45f, true, -1, false);
                yield return new WaitForSecondsRealtime(1.2f);
                view.Streamed.SetStationPhase(CabStationPhase.Approaching);
                cab.LookAround.SetPreviewLook(-62f, -22f);
                yield return new WaitForSecondsRealtime(2.5f);
                yield return Capture("42-platform-people.png", 1600, 1000);
                view.Streamed.SetStationPhase(CabStationPhase.DoorsOpen);
                cab.ConfigureDistancePreview(stop, false, 0.45f, true, -1, false);
                yield return new WaitForSecondsRealtime(0.5f);
                view.Streamed.SetPassengerReport(new CabPassengerStopReport(null, 8, 5, 20, 76));
                cab.LookAround.SetPreviewLook(-12f, -4f);
                yield return new WaitForSecondsRealtime(4f);
                yield return Capture("43-boarding.png", 1600, 1000);
                yield return new WaitForSecondsRealtime(5f);
                yield return Capture("44-boarding-later.png", 1600, 1000);
                view.Streamed.SetStationPhase(CabStationPhase.Complete);
                cab.LookAround.SetPreviewLook(85f, -10f);
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Capture("45-cab-right.png", 1600, 1000);
                cab.LookAround.SetPreviewLook(-89f, -18f);
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Capture("46-cab-left.png", 1600, 1000);
                cab.ConfigureDistancePreview(stop, true, 0.95f, false, -1, false);
                cab.LookAround.SetPreviewLook(-40f, -22f);
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Capture("47-attendant-night.png", 1600, 1000);
                cab.LookAround.SetPreviewLook(null);
            }
            Debug.Log("SMOKE_EVENT " + planner.At(Find(p => p.IsStation && p.Event != StationEvent.None, 60f)).Event);
            foreach ((string file, float distance, bool lights, float time) in extra)
            {
                if (cab != null) cab.ConfigureDistancePreview(distance, lights, time);
                yield return null;
                view.Streamed.Traffic.PreviewTraffic(distance);
                if (file.StartsWith("16")) view.Streamed.Traffic.ForceMeetingTrain(distance, 170f);
                yield return new WaitForSecondsRealtime(file.StartsWith("15") ? 2.5f : 0.9f);
                yield return Capture(file, 1600, 1000);
                Debug.Log("SMOKE_SHOT " + file + " d=" + distance.ToString("0") + " kind=" + planner.At(distance).Kind +
                          " cars=" + view.Streamed.Traffic.CarCount + " lights=" + view.Streamed.EnabledLightCount);
            }
            (string file, float distance, bool lights, float time)[] shots =
            {
                ("01-start.png", 25f, false, noon),
                ("02-station-approach.png", stop - 75f, false, noon),
                ("03-station-stop.png", stop, false, noon),
                ("04-into-forest.png", Find(p => p.Kind == WorldChunkKind.Forest, -20f), false, noon),
                ("05-tunnel-approach.png", tunnel - 70f, false, noon),
                ("06-tunnel-inside.png", tunnel + 70f, true, noon),
                ("07-river-bridge.png", Find(p => p.Kind == WorldChunkKind.Water, 15f), false, noon),
                ("08-town.png", Find(p => p.Kind == WorldChunkKind.Town, 15f), false, noon),
                ("09-town-night.png", Find(p => p.Kind == WorldChunkKind.Town, 15f), true, midnight),
                ("10-station-night.png", stop - 60f, true, midnight),
                ("11-field.png", Find(p => p.Kind == WorldChunkKind.Field, 30f), false, noon),
                ("12-night-lights-off.png", Find(p => p.Kind == WorldChunkKind.Field, 30f), false, midnight),
                ("13-night-lights-on.png", Find(p => p.Kind == WorldChunkKind.Field, 30f), true, midnight)
            };
            foreach ((string file, float distance, bool lights, float time) in shots)
            {
                if (cab != null) cab.ConfigureDistancePreview(distance, lights, time);
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Capture(file, 1600, 1000);
                Debug.Log("SMOKE_SHOT " + file + " d=" + distance.ToString("0") + " kind=" + planner.At(distance).Kind +
                          " chunks=" + view.Streamed.LoadedChunkCount + " lights=" + view.Streamed.EnabledLightCount);
            }
            Debug.Log("HEADLIGHT_RATIO=" + LowerCentreBrightnessRatio(
                Path.Combine(outputDirectory, "13-night-lights-on.png"), Path.Combine(outputDirectory, "12-night-lights-off.png")));

            // Drive: 25 m/s for 30 s with chunks streamed in the background; frame times logged.
            {
                if (cab != null) cab.ConfigureDistancePreview(1000f, false, 0.5f, true, -1, false);
                yield return new WaitForSecondsRealtime(1f);
                float worst = 0f, total = 0f;
                int frames = 0, slow = 0;
                float until = Time.realtimeSinceStartup + 30f;
                while (Time.realtimeSinceStartup < until)
                {
                    float dt = Time.unscaledDeltaTime;
                    view.Streamed.Advance(25f * dt);
                    if (frames > 5)
                    {
                        worst = Mathf.Max(worst, dt);
                        total += dt;
                        if (dt > 1f / 30f) slow++;
                    }
                    frames++;
                    yield return null;
                }
                Debug.Log("DRIVE frames=" + frames + " avgFps=" + ((frames - 6) / Mathf.Max(0.01f, total)).ToString("0") + " worstMs=" +
                          (worst * 1000f).ToString("0") + " slowFrames=" + slow + " chunks=" + view.Streamed.LoadedChunkCount);
            }

            // Long run: 30 km in 60 m steps, building every chunk on the way.
            float soakStart = Time.realtimeSinceStartup;
            long memoryBefore = GC.GetTotalMemory(true);
            int maxChunks = 0;
            for (float d = 0f; d < 30000f; d += 60f)
            {
                view.SetPreviewDistance(d);
                maxChunks = Mathf.Max(maxChunks, view.Streamed.LoadedChunkCount);
                if (Mathf.Repeat(d, 3000f) < 1f) yield return null;
            }
            yield return null;
            Resources.UnloadUnusedAssets();
            long memoryAfter = GC.GetTotalMemory(true);
            float seconds = Time.realtimeSinceStartup - soakStart;
            Debug.Log("SOAK km=30 seconds=" + seconds.ToString("0.0") + " msPerChunk=" + (seconds * 1000f / 250f).ToString("0.0") +
                      " maxChunks=" + maxChunks + " managedMB=" + (memoryBefore / 1048576f).ToString("0.0") + "->" + (memoryAfter / 1048576f).ToString("0.0"));
            if (cab != null) cab.ConfigureDistancePreview(30000f, false, noon);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Capture("14-after-30km.png", 1600, 1000);
            Debug.Log("ROUTE_PREVIEW_COMPLETE=" + outputDirectory);
            yield return new WaitForSecondsRealtime(0.2f);
            Application.Quit();
        }

        /// <summary>How much brighter the track area ahead is in the first image than in the second.</summary>
        private static float LowerCentreBrightnessRatio(string litPath, string darkPath)
        {
            float Mean(string path)
            {
                Texture2D image = new Texture2D(2, 2);
                image.LoadImage(File.ReadAllBytes(path));
                float sum = 0f;
                int count = 0;
                // Texture rows count from the bottom: this is the strip of ground just above the desk.
                for (int y = (int)(image.height * 0.515f); y < image.height * 0.57f; y += 2)
                    for (int x = (int)(image.width * 0.40f); x < image.width * 0.60f; x += 2)
                    {
                        Color c = image.GetPixel(x, y);
                        sum += c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                        count++;
                    }
                Destroy(image);
                return count > 0 ? sum / count : 0f;
            }
            return Mean(litPath) / Mathf.Max(0.001f, Mean(darkPath));
        }

        private IEnumerator CaptureTrackPreview()
        {
            yield return WaitForScene(SceneNames.MainMenu);
            AppServices.Instance.Session.Select(GameMode.CabRide, 2);
            SceneManager.LoadScene(SceneNames.CabRide);
            yield return WaitForScene(SceneNames.CabRide);
            CabRideController cab = FindObjectOfType<CabRideController>();
            if (cab != null) cab.ConfigureTrackPreview(RouteSegmentType.Forest, 0.48f);
            yield return new WaitForSecondsRealtime(1.8f);
            yield return Capture("01-straight-track.png", 1600, 1000);
            if (cab != null) cab.ConfigureTrackPreview(RouteSegmentType.Village, 0.55f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Capture("02-track-switch.png", 1600, 1000);
            if (cab != null) cab.ConfigureTrackPreview(RouteSegmentType.Road, 0.55f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Capture("03-level-crossing.png", 1600, 1000);
            Debug.Log("TRACK_PREVIEW_COMPLETE=" + outputDirectory);
            yield return new WaitForSecondsRealtime(0.2f);
            Application.Quit();
        }

        private IEnumerator CaptureSwitchMotionPreview()
        {
            yield return WaitForScene(SceneNames.MainMenu);
            AppServices.Instance.Session.Select(GameMode.CabRide, 2);
            SceneManager.LoadScene(SceneNames.CabRide);
            yield return WaitForScene(SceneNames.CabRide);
            CabRideController cab = FindObjectOfType<CabRideController>();
            if (cab != null) cab.ConfigureTrackPreview(RouteSegmentType.Village, 0.18f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Capture("01-switch-horizon.png", 1600, 1000);
            if (cab != null) cab.ConfigureTrackPreview(RouteSegmentType.Village, 0.50f);
            yield return new WaitForSecondsRealtime(0.25f);
            yield return Capture("02-switch-middle.png", 1600, 1000);
            if (cab != null) cab.ConfigureTrackPreview(RouteSegmentType.Village, 0.80f);
            yield return new WaitForSecondsRealtime(0.25f);
            yield return Capture("03-switch-near.png", 1600, 1000);
            Debug.Log("SWITCH_MOTION_PREVIEW_COMPLETE=" + outputDirectory);
            yield return new WaitForSecondsRealtime(0.2f);
            Application.Quit();
        }

        private IEnumerator CaptureWeatherPreview()
        {
            yield return WaitForScene(SceneNames.MainMenu);
            AppServices.Instance.Session.Select(GameMode.CabRide, 2);
            SceneManager.LoadScene(SceneNames.CabRide);
            yield return WaitForScene(SceneNames.CabRide);
            CabRideController cab = FindObjectOfType<CabRideController>();
            if (cab != null) cab.ConfigureWeatherPreview(false);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Capture("01-rain-wipers-off.png", 1600, 1000);
            if (cab != null) cab.ConfigureWeatherPreview(true);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Capture("02-rain-wipers-on.png", 1600, 1000);
            Debug.Log("WEATHER_PREVIEW_COMPLETE=" + outputDirectory);
            yield return new WaitForSecondsRealtime(0.2f);
            Application.Quit();
        }

        private IEnumerator CaptureRadioUiPreview()
        {
            yield return WaitForScene(SceneNames.MainMenu);
            AppServices.Instance.Session.Select(GameMode.CabRide, 2);
            SceneManager.LoadScene(SceneNames.CabRide);
            yield return WaitForScene(SceneNames.CabRide);
            CabRideController cab = FindObjectOfType<CabRideController>();
            if (cab != null) cab.ConfigureRadioUiPreview();
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Capture("radio-and-throttle-ui.png", 1600, 1000);
            Debug.Log("RADIO_UI_PREVIEW_COMPLETE=" + outputDirectory);
            yield return new WaitForSecondsRealtime(0.2f);
            Application.Quit();
        }

        private IEnumerator CaptureAutumnLeavesPreview()
        {
            yield return WaitForScene(SceneNames.MainMenu);
            AppServices.Instance.Session.Select(GameMode.CabRide, 2);
            SceneManager.LoadScene(SceneNames.CabRide);
            yield return WaitForScene(SceneNames.CabRide);
            CabRideController cab = FindObjectOfType<CabRideController>();
            if (cab != null) cab.ConfigureAutumnLeafPreview(false);
            yield return new WaitForSecondsRealtime(1.4f);
            yield return Capture("autumn-leaves-window.png", 1600, 1000);
            Debug.Log("AUTUMN_LEAVES_PREVIEW_COMPLETE=" + outputDirectory);
            yield return new WaitForSecondsRealtime(0.2f);
            Application.Quit();
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            yield return new WaitForSecondsRealtime(1.2f);
        }

        private IEnumerator Capture(string fileName, int width = 1600, int height = 1000)
        {
            string path = Path.Combine(outputDirectory, fileName);
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("SmokeCamera", typeof(Camera));
                camera = cameraObject.GetComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            if (camera.GetComponent<CabLookAround>() != null)
            {
                // Immersive cab: the driver's camera and the overlay HUD already make the final
                // image, so capture exactly what is on screen.
                yield return null;
                yield return new WaitForEndOfFrame();
                Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
                // The screen's alpha channel is meaningless for the player; drop it from the file.
                Color32[] pixels = frame.GetPixels32();
                for (int i = 0; i < pixels.Length; i++) pixels[i].a = 255;
                frame.SetPixels32(pixels);
                File.WriteAllBytes(path, frame.EncodeToPNG());
                Destroy(frame);
                yield return null;
                yield break;
            }

            Canvas[] canvases = FindObjectsOfType<Canvas>();
            RenderMode[] originalModes = canvases.Select(canvas => canvas.renderMode).ToArray();
            Camera[] originalCameras = canvases.Select(canvas => canvas.worldCamera).ToArray();
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                canvases[i].worldCamera = camera;
                canvases[i].planeDistance = 1f;
            }

            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            RenderTexture texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            camera.targetTexture = texture;
            RenderTexture.active = texture;
            camera.Render();

            Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
            image.Apply(false, false);
            File.WriteAllBytes(path, image.EncodeToPNG());

            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            texture.Release();
            Destroy(texture);
            Destroy(image);
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = originalModes[i];
                canvases[i].worldCamera = originalCameras[i];
            }
            yield return null;
        }

        private static string ReadArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }
            return string.Empty;
        }
    }
}
