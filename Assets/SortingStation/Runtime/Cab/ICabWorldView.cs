using System;
using UnityEngine;

namespace SortingStation
{
    public interface ICabWorldView
    {
        event Action<RouteSegmentDefinition> SegmentChanged;
        event Action<CabAmbientSoundRequest> AmbientSoundRequested;

        float TunnelBlend { get; }
        float Distance { get; }
        float SegmentProgress { get; }
        RouteSegmentDefinition CurrentSegment { get; }
        string CurrentSegmentName { get; }
        RectTransform Viewport { get; }
        RectTransform SkyEffectsLayer { get; }
        RectTransform HorizonEffectsLayer { get; }
        /// <summary>True when the view renders its own sun, moon and stars, so 2D sky overlays must stay hidden.</summary>
        bool DrawsOwnSky { get; }

        void Advance(float speed01, float acceleration01, float unscaledDeltaTime);
        void ApplySeason(SeasonThemeDefinition season);
        void SetAtmosphere(Color sky, Color tint);
        /// <summary>Time of day from the ride's single EnvironmentClock (0..1).</summary>
        void SetDayTime(float time01);
        void SetHeadlights(bool enabled);
        void SetStationPhase(CabStationPhase phase);
        void SetUpcomingStation(CabStationDefinition station);
        void SetPassengerReport(CabPassengerStopReport report);
        bool HasVisibleScenery(string idFragment);
        bool TryReactToScenery(string idFragment, CabInteractionReaction reaction, float durationSeconds = 2.5f);
        void SetPreviewSegment(RouteSegmentType type, float progress);
        void SetPreviewDistance(float distance);
    }
}
