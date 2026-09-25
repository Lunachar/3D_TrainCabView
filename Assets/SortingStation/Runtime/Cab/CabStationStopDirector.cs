using System;
using UnityEngine;

namespace SortingStation
{
    public sealed class CabStationStopDirector : MonoBehaviour
    {
        // Platforms are 52 m long in the 3D route. The cab stops 3 m short of the platform's far
        // end, like a real train, so the whole consist behind it stands along the platform.
        private const float StopOffsetPastPlatformCentre = 23f;
        private CabInteractionCatalog catalog;
        private CabStationStopModel model;
        private System.Random random;
        private float movingTime;
        private float nextStationAt;
        private float activeStationTargetDistance = -1f;
        private float lastTriggeredStationTargetDistance = -1f;
        private CabStationPhase lastPhase;
        private bool terminalReported;
        private RouteSegmentType lastSegment;
        private bool stationTriggeredInSegment;
        private int stationSequence = 1;

        public event Action<CabStationPhase> PhaseChanged;
        public event Action<CabStationDefinition> StationApproaching;
        public float BrakeStrength => model != null ? model.BrakeStrength : 0f;
        public float TractionMultiplier => model != null ? model.TractionMultiplier : 1f;
        public bool IsActive => model != null && model.IsActive;
        public bool DoorsAreOpen => model != null && model.DoorsAreOpen;
        public CabStationPhase Phase => model != null ? model.Phase : CabStationPhase.Idle;
        public CabStationDefinition CurrentStation { get; private set; }
        /// <summary>Station the train is heading for (the current one while stopping).</summary>
        public CabStationDefinition NextStation => model != null && model.IsActive && CurrentStation != null
            ? CurrentStation : CabStationNetwork.AtSequence(stationSequence);
        /// <summary>Metres to the next stopping point, or a negative value when unknown.</summary>
        public float DistanceToNextStop { get; private set; } = -1f;

        public void Initialize(CabInteractionCatalog interactionCatalog, int seed)
        {
            catalog = interactionCatalog;
            model = new CabStationStopModel(catalog != null ? catalog.StationDoorWaitSeconds : 8f,
                catalog != null ? catalog.StationOpenSeconds : 7f,
                catalog != null ? catalog.StationTractionReleaseSeconds : 1.5f,
                catalog != null ? catalog.StationCrawlStartDistance : 22f,
                catalog != null ? catalog.StationCrawlSpeed01 : 0.052f,
                catalog != null ? catalog.StationStopTolerance : 1.8f);
            random = new System.Random(seed ^ 0x53544154);
            nextStationAt = NextStationInterval();
            lastPhase = model.Phase;
        }

        public void Step(float speed01, float deltaTime, RouteSegmentType segment, bool tunnel, bool priorityStop,
            float segmentProgress = -1f, float routeDistance = 0f, float routeCycleLength = 0f)
        {
            if (model == null) return;
            float dt = Mathf.Clamp(deltaTime, 0f, 0.1f);
            if (speed01 > 0.02f && !priorityStop) movingTime += dt;
            bool suitable = segment == RouteSegmentType.Village || segment == RouteSegmentType.Town;
            if (segment != lastSegment)
            {
                lastSegment = segment;
                stationTriggeredInSegment = false;
            }
            float upcomingTarget = FindUpcomingStationTarget(routeDistance, routeCycleLength);
            float distanceAhead = activeStationTargetDistance >= 0f
                ? Mathf.Max(0f, activeStationTargetDistance - routeDistance)
                : Mathf.Max(0f, upcomingTarget - routeDistance);
            DistanceToNextStop = routeCycleLength >= 100f && (activeStationTargetDistance >= 0f || upcomingTarget >= 0f) ? distanceAhead : -1f;
            bool geometryTrigger = routeCycleLength >= 100f && upcomingTarget >= 0f &&
                                   upcomingTarget - routeDistance <= (catalog != null ? catalog.StationApproachDistance : 92f) &&
                                   Mathf.Abs(upcomingTarget - lastTriggeredStationTargetDistance) > 0.1f;
            bool positionTrigger = routeCycleLength < 100f && segmentProgress >= 0f && segmentProgress >= 0.46f && segmentProgress <= 0.78f &&
                                   !stationTriggeredInSegment;
            bool legacyTimeTrigger = segmentProgress < 0f && movingTime >= nextStationAt;
            if (!model.IsActive && model.Phase != CabStationPhase.Complete && model.Phase != CabStationPhase.Cancelled &&
                ((geometryTrigger && !tunnel) || ((positionTrigger || legacyTimeTrigger) && suitable && !tunnel)) && !priorityStop)
            {
                CurrentStation = CabStationNetwork.AtSequence(stationSequence++);
                model.BeginApproach();
                stationTriggeredInSegment = true;
                activeStationTargetDistance = geometryTrigger ? upcomingTarget : routeDistance;
                lastTriggeredStationTargetDistance = activeStationTargetDistance;
                StationApproaching?.Invoke(CurrentStation);
            }
            model.Step(dt, speed01, priorityStop, distanceAhead);
            if (model.Phase == CabStationPhase.Complete || model.Phase == CabStationPhase.Cancelled)
            {
                if (terminalReported)
                {
                    model.Reset();
                    terminalReported = false;
                }
                else
                {
                    movingTime = 0f;
                    nextStationAt = NextStationInterval();
                    activeStationTargetDistance = -1f;
                    terminalReported = true;
                }
            }
            if (lastPhase == model.Phase) return;
            lastPhase = model.Phase;
            PhaseChanged?.Invoke(lastPhase);
        }

        public void PressDoors() => model?.PressDoors();

        private float NextStationInterval()
        {
            float min = catalog != null ? catalog.MinimumStationIntervalSeconds : 360f;
            float max = catalog != null ? catalog.MaximumStationIntervalSeconds : 600f;
            return min + (float)(random != null ? random.NextDouble() : 0.5d) * (max - min);
        }

        private static float FindUpcomingStationTarget(float distance, float cycleLength)
        {
            if (cycleLength < 100f) return -1f;
            float cycleStart = Mathf.Floor(distance / cycleLength) * cycleLength;
            float first = cycleStart + cycleLength * CabRouteLayout.FirstStation01 + StopOffsetPastPlatformCentre;
            if (first >= distance - 0.01f) return first;
            float second = cycleStart + cycleLength * CabRouteLayout.SecondStation01 + StopOffsetPastPlatformCentre;
            return second >= distance - 0.01f ? second : cycleStart + cycleLength * (1f + CabRouteLayout.FirstStation01) + StopOffsetPastPlatformCentre;
        }
    }
}
