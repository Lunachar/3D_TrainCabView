using UnityEngine;

namespace SortingStation
{
    public sealed class CabStationStopModel
    {
        private readonly float automaticDoorWait;
        private readonly float doorsOpenDuration;
        private readonly float tractionReleaseDuration;
        private readonly float crawlStartDistance;
        private readonly float crawlSpeed01;
        private readonly float stopTolerance;
        private float phaseTime;

        public CabStationPhase Phase { get; private set; } = CabStationPhase.Idle;
        public float BrakeStrength { get; private set; }
        public float TractionMultiplier { get; private set; } = 1f;
        public bool DoorsAreOpen => Phase == CabStationPhase.DoorsOpen;
        public bool IsActive => Phase == CabStationPhase.Approaching || Phase == CabStationPhase.WaitingForDoors ||
                                Phase == CabStationPhase.DoorsOpen || Phase == CabStationPhase.Releasing;

        public CabStationStopModel(float doorWaitSeconds, float openSeconds, float releaseSeconds,
            float crawlStart = 22f, float crawlSpeed = 0.052f, float tolerance = 1.8f)
        {
            automaticDoorWait = Mathf.Max(0.1f, doorWaitSeconds);
            doorsOpenDuration = Mathf.Max(0.1f, openSeconds);
            tractionReleaseDuration = Mathf.Max(0.1f, releaseSeconds);
            crawlStartDistance = Mathf.Max(2f, crawlStart);
            crawlSpeed01 = Mathf.Clamp(crawlSpeed, 0.01f, 0.20f);
            stopTolerance = Mathf.Max(0.25f, tolerance);
        }

        public void BeginApproach()
        {
            if (IsActive) return;
            SetPhase(CabStationPhase.Approaching);
            TractionMultiplier = 0f;
        }

        public void PressDoors()
        {
            if (Phase == CabStationPhase.WaitingForDoors) SetPhase(CabStationPhase.DoorsOpen);
        }

        public void Reset()
        {
            SetPhase(CabStationPhase.Idle);
            BrakeStrength = 0f;
            TractionMultiplier = 1f;
        }

        public void Step(float deltaTime, float speed01, bool vigilanceHasPriority, float distanceAhead = float.PositiveInfinity)
        {
            if (vigilanceHasPriority && IsActive)
            {
                SetPhase(CabStationPhase.Cancelled);
                BrakeStrength = 0f;
                TractionMultiplier = 1f;
                return;
            }

            float dt = Mathf.Max(0f, deltaTime);
            phaseTime += dt;
            switch (Phase)
            {
                case CabStationPhase.Approaching:
                    // Older callers and tests do not provide a physical platform distance. Keep
                    // their safe stop behaviour while the 3D route uses the precise branch below.
                    if (float.IsPositiveInfinity(distanceAhead))
                    {
                        TractionMultiplier = 0f;
                        BrakeStrength = Mathf.Lerp(0.28f, 0.82f, Mathf.Clamp01(speed01 * 1.4f));
                        if (speed01 <= 0.012f) SetPhase(CabStationPhase.WaitingForDoors);
                        break;
                    }
                    float remaining = Mathf.Max(0f, distanceAhead);
                    if (remaining <= stopTolerance)
                    {
                        TractionMultiplier = 0f;
                        BrakeStrength = 1f;
                        // Keep the stop authoritative, but do not advertise door readiness while
                        // the motion model is still carrying residual speed through the platform.
                        // The full service brake remains applied on each approach step until the
                        // train is effectively stationary.
                        if (speed01 <= 0.005f) SetPhase(CabStationPhase.WaitingForDoors);
                        break;
                    }
                    float approachBlend = Mathf.Clamp01(remaining / crawlStartDistance);
                    float desiredSpeed = Mathf.Lerp(crawlSpeed01, 0.18f, approachBlend);
                    float excess = Mathf.Max(0f, speed01 - desiredSpeed);
                    BrakeStrength = excess <= 0.006f ? 0.025f : Mathf.Lerp(0.20f, 0.84f, Mathf.Clamp01(excess / 0.34f));
                    TractionMultiplier = speed01 < desiredSpeed * 0.72f ? 0.34f : 0f;
                    break;
                case CabStationPhase.WaitingForDoors:
                    TractionMultiplier = 0f;
                    BrakeStrength = 1f;
                    if (phaseTime >= automaticDoorWait) SetPhase(CabStationPhase.DoorsOpen);
                    break;
                case CabStationPhase.DoorsOpen:
                    TractionMultiplier = 0f;
                    BrakeStrength = 1f;
                    if (phaseTime >= doorsOpenDuration) SetPhase(CabStationPhase.Releasing);
                    break;
                case CabStationPhase.Releasing:
                    BrakeStrength = 0f;
                    TractionMultiplier = Mathf.Clamp01(phaseTime / tractionReleaseDuration);
                    if (phaseTime >= tractionReleaseDuration)
                    {
                        TractionMultiplier = 1f;
                        SetPhase(CabStationPhase.Complete);
                    }
                    break;
                case CabStationPhase.Complete:
                case CabStationPhase.Cancelled:
                case CabStationPhase.Idle:
                    BrakeStrength = 0f;
                    TractionMultiplier = 1f;
                    break;
            }
        }

        private void SetPhase(CabStationPhase value)
        {
            Phase = value;
            phaseTime = 0f;
        }
    }
}
