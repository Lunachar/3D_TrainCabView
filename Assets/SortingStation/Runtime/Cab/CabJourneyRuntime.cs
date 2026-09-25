using System;
using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    public sealed class CabJourneyRuntime
    {
        private static readonly RouteSegmentType[] PrototypeOrder =
        {
            RouteSegmentType.Meadow,
            RouteSegmentType.Forest,
            RouteSegmentType.Road,
            RouteSegmentType.Village,
            RouteSegmentType.MountainTunnel,
            RouteSegmentType.Town,
            RouteSegmentType.Water
        };

        private readonly RouteSegmentDefinition[] orderedSegments;
        private int segmentIndex;

        public event Action<RouteSegmentDefinition> SegmentChanged;
        public float Distance { get; private set; }
        public float SegmentDistance { get; private set; }
        public RouteSegmentDefinition CurrentSegment { get; private set; }
        public float SegmentProgress => CurrentSegment == null ? 0f :
            Mathf.Clamp01(SegmentDistance / Mathf.Max(0.01f, CurrentSegment.Length));
        public float CycleLength { get; }

        public CabJourneyRuntime(RouteSegmentDefinition[] availableSegments, int seed)
        {
            orderedSegments = BuildPrototypeOrder(availableSegments, seed);
            CurrentSegment = orderedSegments.Length > 0 ? orderedSegments[0] : null;
            float length = 0f;
            for (int i = 0; i < orderedSegments.Length; i++)
                if (orderedSegments[i] != null) length += orderedSegments[i].Length;
            CycleLength = Mathf.Max(1f, length);
        }

        public void Advance(float distanceDelta)
        {
            float delta = Mathf.Max(0f, distanceDelta);
            Distance += delta;
            SegmentDistance += delta;
            if (orderedSegments.Length == 0 || CurrentSegment == null) return;
            int safety = orderedSegments.Length + 2;
            while (SegmentDistance >= CurrentSegment.Length && safety-- > 0)
            {
                SegmentDistance -= CurrentSegment.Length;
                segmentIndex = (segmentIndex + 1) % orderedSegments.Length;
                CurrentSegment = orderedSegments[segmentIndex];
                SegmentChanged?.Invoke(CurrentSegment);
            }
        }

        public bool SetSegment(RouteSegmentType type, float progress, bool notify)
        {
            for (int i = 0; i < orderedSegments.Length; i++)
            {
                RouteSegmentDefinition segment = orderedSegments[i];
                if (segment == null || segment.Type != type) continue;
                segmentIndex = i;
                CurrentSegment = segment;
                SegmentDistance = segment.Length * Mathf.Clamp01(progress);
                if (notify) SegmentChanged?.Invoke(segment);
                return true;
            }
            return false;
        }

        public void SetDistance(float distance)
        {
            Distance = Mathf.Max(0f, distance);
        }

        public static RouteSegmentDefinition[] BuildPrototypeOrder(RouteSegmentDefinition[] availableSegments, int seed)
        {
            RouteSegmentDefinition[] source = availableSegments ?? Array.Empty<RouteSegmentDefinition>();
            List<RouteSegmentDefinition> ordered = new List<RouteSegmentDefinition>(source.Length);
            for (int typeIndex = 0; typeIndex < PrototypeOrder.Length; typeIndex++)
            {
                RouteSegmentType type = PrototypeOrder[typeIndex];
                for (int i = 0; i < source.Length; i++)
                {
                    RouteSegmentDefinition candidate = source[i];
                    if (candidate != null && candidate.Type == type && !ordered.Contains(candidate))
                    {
                        ordered.Add(candidate);
                        break;
                    }
                }
            }
            for (int i = 0; i < source.Length; i++)
                if (source[i] != null && !ordered.Contains(source[i])) ordered.Add(source[i]);
            return ordered.ToArray();
        }
    }
}
