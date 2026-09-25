using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// Roads beside the line. A road is a continuous function of the route distance: it runs
    /// parallel to the track while the plan has a road on that side, swings out into the land
    /// through the neighbouring chunk when it ends, and makes room for stations and dense towns.
    /// Chunks build their piece of it; cars drive on the same function.
    /// </summary>
    public static class WorldRoads
    {
        public const float HalfWidth = 3.4f;
        public const float LaneOffset = 1.7f;
        public const float AwayReach = 340f;

        private static bool Has(WorldChunkPlan plan, int side) => side < 0 ? plan.RoadLeft : plan.RoadRight;

        /// <summary>Lateral position of the road centre on a side (-1 left, +1 right), if there is one near d.</summary>
        public static bool TryLateral(WorldPlanner planner, WorldTerrain terrain, float d, int side, out float x)
        {
            x = 0f;
            WorldChunkPlan here = planner.At(d);
            float away;
            if (Has(here, side))
            {
                away = 0f;
            }
            else
            {
                // Swing out from whichever neighbour still has the road.
                float fromPrevious = here.Index > 0 && Has(planner.Get(here.Index - 1), side) ? (d - here.Start) / WorldPlanner.ChunkLength : 2f;
                float fromNext = Has(planner.Get(here.Index + 1), side) ? (here.End - d) / WorldPlanner.ChunkLength : 2f;
                away = Mathf.Min(fromPrevious, fromNext);
                if (away > 1f) return false;
            }
            float wiggle = 0.5f + 0.5f * Mathf.Sin(Mathf.Repeat(d * 0.0031f + side * 1.7f, Mathf.PI * 2f));
            float room = 12f + 5f * wiggle;
            // Stations and packed city streets keep the road further out.
            room += 26f * StationRoom(planner, d, side) + 32f * terrain.Weight(d, WorldChunkKind.City) * (here.DenseLowRise ? 1f : 0.35f);
            float edge = side < 0 ? PlacementMask.CorridorLeft : PlacementMask.CorridorRight;
            x = edge + side * (room + away * away * AwayReach);
            return true;
        }

        private static float StationRoom(WorldPlanner planner, float d, int side)
        {
            if (side > 0) return 0f;
            float best = 0f;
            for (int offset = -1; offset <= 1; offset++)
            {
                WorldChunkPlan plan = planner.At(d + offset * WorldPlanner.ChunkLength);
                if (!plan.IsStation) continue;
                float centre = plan.Start + WorldPlanner.PlatformCentre;
                best = Mathf.Max(best, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(45f, 110f, Mathf.Abs(d - centre))));
            }
            return best;
        }

        /// <summary>Road surface height at d: the highest ground under the carriageway, just above it.</summary>
        public static float Height(WorldTerrain terrain, float d, float x)
        {
            float h = Mathf.Max(terrain.Height(d, x - HalfWidth, false), terrain.Height(d, x, false), terrain.Height(d, x + HalfWidth, false));
            return h + 0.06f;
        }
    }
}
