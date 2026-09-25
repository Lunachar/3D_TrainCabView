using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// Keep-out areas of a chunk in route coordinates (d along the track, x to the right).
    /// Builders register the track corridor, roads, platforms, buildings, tunnels and water
    /// first; every tree, bush, grass tuft or animal is placed only where the mask is free.
    /// </summary>
    public sealed class PlacementMask
    {
        /// <summary>Main track at x = 0, the second track at +4.8: the corridor between both ballast edges.</summary>
        public const float CorridorLeft = -4.6f;
        public const float CorridorRight = 9.4f;

        private readonly struct Zone
        {
            public readonly float D0, D1, X0, X1;

            public Zone(float d0, float d1, float x0, float x1)
            {
                D0 = Mathf.Min(d0, d1);
                D1 = Mathf.Max(d0, d1);
                X0 = Mathf.Min(x0, x1);
                X1 = Mathf.Max(x0, x1);
            }
        }

        private readonly List<Zone> zones = new List<Zone>();

        public int Count => zones.Count;

        public PlacementMask(float chunkStart, float chunkEnd)
        {
            // The track corridor is always blocked, with a little clearance for the verges.
            AddRect(chunkStart - 1f, chunkEnd + 1f, CorridorLeft - 1.2f, CorridorRight + 1.2f);
        }

        public void AddRect(float d0, float d1, float x0, float x1) => zones.Add(new Zone(d0, d1, x0, x1));

        /// <summary>A band across the whole chunk width (tunnel bore, river, level crossing road).</summary>
        public void AddAcross(float d0, float d1, float halfWidth = 2000f) => AddRect(d0, d1, -halfWidth, halfWidth);

        public bool IsFree(float d, float x, float radius)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                Zone zone = zones[i];
                if (d + radius > zone.D0 && d - radius < zone.D1 && x + radius > zone.X0 && x - radius < zone.X1) return false;
            }
            return true;
        }
    }
}
