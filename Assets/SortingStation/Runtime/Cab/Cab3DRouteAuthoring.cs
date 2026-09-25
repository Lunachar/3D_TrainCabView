using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    public sealed class Cab3DRouteAuthoring : MonoBehaviour
    {
        // Written only by the prototype factory.  It lets a new runtime safely fall back to the
        // current procedural route when a locally cached editable prefab predates its geometry.
        [SerializeField] private int builtRouteVersion;
        [SerializeField] [Min(100f)] private float cycleLength = 1120f;
        [SerializeField] private Transform crossingRoot;
        [SerializeField] private Transform leftBarrier;
        [SerializeField] private Transform rightBarrier;
        [SerializeField] private Light[] crossingLights;
        [SerializeField] private Transform stationRoot;
        [SerializeField] private Transform[] passengers;
        [SerializeField] private TextMesh[] stationSigns;
        [SerializeField] private Light headlight;
        [SerializeField] private Light[] scenicNightLights;
        [SerializeField] private Light[] tunnelInteriorLights;
        [SerializeField] private Renderer[] groundRenderers;
        [SerializeField] private Renderer[] foliageRenderers;

        public float CycleLength => Mathf.Max(100f, cycleLength);
        public int BuiltRouteVersion => builtRouteVersion;

        public void MarkBuiltRouteVersion(int value)
        {
            builtRouteVersion = Mathf.Max(0, value);
        }

        // Geometry, camera centring, station targets and tunnel lighting have to use the same
        // physical cycle.  A generated route must never silently retain the inspector default.
        public void SetCycleLength(float value)
        {
            cycleLength = Mathf.Max(100f, value);
        }
        public bool HasPassengerStations
        {
            get
            {
                ResolveGeneratedReferences();
                return stationSigns != null && stationSigns.Length >= 2 && passengers != null && passengers.Length >= 12;
            }
        }

        public void SetCrossingApproach(float distanceAhead)
        {
            ResolveGeneratedReferences();
            float t = 1f - Mathf.InverseLerp(65f, 12f, distanceAhead);
            float angle = Mathf.Lerp(0f, 78f, Mathf.SmoothStep(0f, 1f, t));
            if (leftBarrier != null) leftBarrier.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            if (rightBarrier != null) rightBarrier.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            bool flash = t > 0.1f && Mathf.Repeat(Time.unscaledTime * 2.3f, 1f) < 0.5f;
            if (crossingLights != null)
                for (int i = 0; i < crossingLights.Length; i++) if (crossingLights[i] != null) crossingLights[i].enabled = flash;
        }

        public void SetStationPhase(CabStationPhase phase)
        {
            ResolveGeneratedReferences();
            bool visible = phase == CabStationPhase.Approaching || phase == CabStationPhase.WaitingForDoors || phase == CabStationPhase.DoorsOpen;
            bool active = phase == CabStationPhase.WaitingForDoors || phase == CabStationPhase.DoorsOpen;
            if (passengers == null) return;
            for (int i = 0; i < passengers.Length; i++)
            {
                if (passengers[i] == null) continue;
                Cab3DPassengerAgent agent = passengers[i].GetComponent<Cab3DPassengerAgent>();
                if (agent != null)
                {
                    agent.SetPlatformVisible(visible);
                    agent.SetDoorsOpen(phase == CabStationPhase.DoorsOpen);
                }
                float wave = active ? Mathf.Sin(Time.unscaledTime * 4f + i) * 14f : 0f;
                passengers[i].localRotation = Quaternion.Euler(0f, 0f, wave);
            }
        }

        public void SetPassengerReport(CabPassengerStopReport report)
        {
            ResolveGeneratedReferences();
            if (report == null) return;
            SetStationName(report.Station != null ? report.Station.DisplayName : "Станция");

            if (passengers == null) return;
            int departing = Mathf.Min(passengers.Length, report.Boarded);
            int arriving = Mathf.Min(Mathf.Max(0, passengers.Length - departing), report.Alighted);
            for (int i = 0; i < passengers.Length; i++)
            {
                if (passengers[i] == null) continue;
                Cab3DPassengerAgent agent = passengers[i].GetComponent<Cab3DPassengerAgent>();
                if (agent == null) continue;
                agent.SetFlow(i < departing, i >= departing && i < departing + arriving);
            }
        }

        public void SetStationName(string stationName)
        {
            ResolveGeneratedReferences();
            if (stationSigns == null) return;
            for (int i = 0; i < stationSigns.Length; i++)
                if (stationSigns[i] != null) stationSigns[i].text = stationName;
        }

        public void SetHeadlights(bool enabled)
        {
            ResolveGeneratedReferences();
            if (headlight != null) headlight.enabled = enabled;
        }

        public void SetScenicNightLighting(bool enabled)
        {
            ResolveGeneratedReferences();
            if (scenicNightLights == null) return;
            for (int i = 0; i < scenicNightLights.Length; i++)
                if (scenicNightLights[i] != null) scenicNightLights[i].enabled = enabled;
        }

        public void SetTunnelLighting(bool enabled)
        {
            ResolveGeneratedReferences();
            if (tunnelInteriorLights == null) return;
            for (int i = 0; i < tunnelInteriorLights.Length; i++)
                if (tunnelInteriorLights[i] != null) tunnelInteriorLights[i].enabled = enabled;
        }

        public void ApplySeason(SeasonType season)
        {
            ResolveGeneratedReferences();
            Color ground = season == SeasonType.Winter ? new Color(0.82f, 0.88f, 0.88f) :
                season == SeasonType.Autumn ? new Color(0.43f, 0.38f, 0.18f) :
                season == SeasonType.Spring ? new Color(0.30f, 0.58f, 0.25f) : new Color(0.25f, 0.50f, 0.19f);
            Texture2D groundTexture = Resources.Load<Texture2D>(GroundTextureResourceName(season));
            Color leaves = season == SeasonType.Winter ? new Color(0.32f, 0.38f, 0.35f) :
                season == SeasonType.Autumn ? new Color(0.86f, 0.34f, 0.08f) : new Color(0.20f, 0.48f, 0.18f);
            if (groundRenderers != null)
            {
                for (int i = 0; i < groundRenderers.Length; i++)
                {
                    Renderer renderer = groundRenderers[i];
                    if (renderer == null || renderer.sharedMaterial == null) continue;
                    Material material = renderer.sharedMaterial;
                    bool terrainRegion = renderer.name.StartsWith("GroundRegion_");
                    bool texturedGround = renderer.name == "Ground" || terrainRegion || renderer.name.Contains("RoadsideGrassVerge");
                    if (texturedGround && groundTexture != null)
                    {
                        groundTexture.wrapMode = TextureWrapMode.Repeat;
                        groundTexture.filterMode = FilterMode.Trilinear;
                        groundTexture.anisoLevel = 4;
                        material.mainTexture = groundTexture;
                        material.color = terrainRegion ? GroundRegionTint(renderer.name, season) : Color.white;
                    }
                    else if (renderer.name.Contains("EarthPatch"))
                    {
                        material.color = season == SeasonType.Winter ? new Color(0.40f, 0.43f, 0.41f) :
                            season == SeasonType.Autumn ? new Color(0.35f, 0.26f, 0.15f) : new Color(0.31f, 0.24f, 0.13f);
                    }
                    else
                    {
                        material.color = ground;
                    }
                }
            }
            Tint(foliageRenderers, leaves);
            ApplyFoliageAtlas(season);
            if (passengers != null)
                for (int i = 0; i < passengers.Length; i++)
                    passengers[i]?.GetComponent<Cab3DPassengerAgent>()?.ApplySeason(season);
        }

        public void SetPassengerWeather(WeatherType weather)
        {
            ResolveGeneratedReferences();
            bool umbrellas = weather == WeatherType.Rain;
            if (passengers == null) return;
            for (int i = 0; i < passengers.Length; i++)
                passengers[i]?.GetComponent<Cab3DPassengerAgent>()?.SetWetWeather(umbrellas);
        }

        private static void Tint(Renderer[] renderers, Color color)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null && renderers[i].sharedMaterial != null) renderers[i].sharedMaterial.color = color;
        }

        private void ApplyFoliageAtlas(SeasonType season)
        {
            string resourceName = FoliageAtlasResourceName(season);
            Texture2D atlas = Resources.Load<Texture2D>(resourceName);
            if (atlas == null || foliageRenderers == null) return;
            for (int i = 0; i < foliageRenderers.Length; i++)
            {
                Renderer renderer = foliageRenderers[i];
                if (renderer == null || !renderer.name.Contains("FoliageBillboard") || renderer.sharedMaterial == null) continue;
                renderer.sharedMaterial.mainTexture = atlas;
                // The generated atlas carries the season's natural palette; keep its full colour
                // instead of tinting a photo cutout as though it were a flat sprite.
                renderer.sharedMaterial.color = Color.white;
            }
        }

        public static string FoliageAtlasResourceName(SeasonType season)
        {
            return season == SeasonType.Spring ? "Cab3D/FoliageBillboardAtlas_Spring_v1" :
                season == SeasonType.Autumn ? "Cab3D/FoliageBillboardAtlas_Autumn_v1" :
                season == SeasonType.Winter ? "Cab3D/FoliageBillboardAtlas_Winter_v1" : "Cab3D/FoliageBillboardAtlas_v1";
        }

        public static string GroundTextureResourceName(SeasonType season)
        {
            return season == SeasonType.Spring ? "Cab3D/MeadowGround_Spring_v1" :
                season == SeasonType.Autumn ? "Cab3D/MeadowGround_Autumn_v1" :
                season == SeasonType.Winter ? "Cab3D/MeadowGround_Winter_v1" : "Cab3D/MeadowGround_v1";
        }

        private static Color GroundRegionTint(string name, SeasonType season)
        {
            Color summer = Color.white;
            if (name.Contains("Forest")) summer = new Color(0.59f, 0.76f, 0.54f);
            else if (name.Contains("Field")) summer = new Color(0.88f, 0.82f, 0.57f);
            else if (name.Contains("Village")) summer = new Color(0.81f, 0.82f, 0.67f);
            else if (name.Contains("Mountain")) summer = new Color(0.65f, 0.69f, 0.62f);
            else if (name.Contains("Town")) summer = new Color(0.81f, 0.78f, 0.64f);
            else if (name.Contains("Water")) summer = new Color(0.67f, 0.87f, 0.78f);
            else if (name.Contains("Meadow")) summer = new Color(0.87f, 0.96f, 0.72f);

            if (season == SeasonType.Autumn)
                return Color.Lerp(summer, new Color(1f, 0.73f, 0.43f), 0.28f);
            if (season == SeasonType.Winter)
                return Color.Lerp(summer, new Color(0.91f, 0.97f, 1f), 0.38f);
            if (season == SeasonType.Spring)
                return Color.Lerp(summer, new Color(0.74f, 1f, 0.71f), 0.16f);
            return summer;
        }

        private void ResolveGeneratedReferences()
        {
            if (leftBarrier == null || rightBarrier == null)
            {
                Transform[] all = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].name == "CrossingBarrier")
                    {
                        if (leftBarrier == null) leftBarrier = all[i];
                        else if (rightBarrier == null && all[i] != leftBarrier) rightBarrier = all[i];
                    }
                }
            }
            if (crossingLights == null || crossingLights.Length == 0)
            {
                List<Light> foundLights = new List<Light>();
                Light[] allLights = GetComponentsInChildren<Light>(true);
                for (int i = 0; i < allLights.Length; i++)
                    if (allLights[i] != null && allLights[i].name.StartsWith("CrossingWarningLight")) foundLights.Add(allLights[i]);
                crossingLights = foundLights.ToArray();
            }
            if (scenicNightLights == null || scenicNightLights.Length == 0)
            {
                List<Light> foundLights = new List<Light>();
                Light[] allLights = GetComponentsInChildren<Light>(true);
                for (int i = 0; i < allLights.Length; i++)
                {
                    Light light = allLights[i];
                    if (light != null && (light.name == "ScenicNightLight" || light.name == "MeetingTrainHeadlight"))
                        foundLights.Add(light);
                }
                scenicNightLights = foundLights.ToArray();
            }
            if (tunnelInteriorLights == null || tunnelInteriorLights.Length == 0)
            {
                List<Light> foundLights = new List<Light>();
                Light[] allLights = GetComponentsInChildren<Light>(true);
                for (int i = 0; i < allLights.Length; i++)
                    if (allLights[i] != null && allLights[i].name == "TunnelInteriorLight") foundLights.Add(allLights[i]);
                tunnelInteriorLights = foundLights.ToArray();
            }
            if (passengers == null || passengers.Length == 0)
            {
                List<Transform> found = new List<Transform>();
                Transform[] all = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++) if (all[i].name.StartsWith("Passenger_")) found.Add(all[i]);
                passengers = found.ToArray();
            }
            if (stationSigns == null || stationSigns.Length == 0)
            {
                TextMesh[] allText = GetComponentsInChildren<TextMesh>(true);
                List<TextMesh> signs = new List<TextMesh>();
                for (int i = 0; i < allText.Length; i++)
                    if (allText[i] != null && allText[i].gameObject.name.StartsWith("StationName_")) signs.Add(allText[i]);
                stationSigns = signs.ToArray();
            }
            if (groundRenderers == null || groundRenderers.Length == 0 || foliageRenderers == null || foliageRenderers.Length == 0)
            {
                List<Renderer> grounds = new List<Renderer>();
                List<Renderer> foliage = new List<Renderer>();
                Renderer[] all = GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].name == "Ground" || all[i].name.StartsWith("GroundRegion_") || all[i].name.Contains("GrassPatch") || all[i].name.Contains("EarthPatch") ||
                        all[i].name.Contains("RoadsideGrassVerge"))
                        grounds.Add(all[i]);
                    else if (all[i].name == "Crown" || all[i].name == "Bush" || all[i].name.Contains("FoliageBillboard"))
                        foliage.Add(all[i]);
                }
                groundRenderers = grounds.ToArray();
                foliageRenderers = foliage.ToArray();
            }
        }
    }
}
