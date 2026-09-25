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

        void Advance(float speed01, float acceleration01, float unscaledDeltaTime);
        void ApplySeason(SeasonThemeDefinition season);
        void SetAtmosphere(Color sky, Color tint);
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
