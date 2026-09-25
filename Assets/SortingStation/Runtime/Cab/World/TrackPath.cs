using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// Centre line of the endless route. Each chunk eases its curvature linearly from the
    /// previous chunk's value to its own over the first <see cref="EaseLength"/> metres (a
    /// clothoid), so there are no kinks anywhere. Positions are integrated once per chunk and
    /// kept in double precision; <see cref="Local"/> turns them into small floats relative to a
    /// moving origin near the train.
    /// </summary>
    public sealed class TrackPath
    {
        public const float EaseLength = 50f;
        private const float SampleStep = 2f;
        private const int SamplesPerChunk = (int)(WorldPlanner.ChunkLength / SampleStep) + 1;

        private struct ChunkStart
        {
            public double X;
            public double Z;
            public double Heading;
            public float StartCurvature;
            public float EndCurvature;
            public Vector2[] Offsets;
        }

        private readonly WorldPlanner planner;
        private readonly List<ChunkStart> chunks = new List<ChunkStart>();

        public TrackPath(WorldPlanner worldPlanner)
        {
            planner = worldPlanner;
        }

        public WorldPlanner Planner => planner;

        /// <summary>Curvature (1/m) at a route distance.</summary>
        public float Curvature(float distance)
        {
            ChunkStart chunk = Chunk(ChunkIndex(distance));
            float s = distance - ChunkIndex(distance) * WorldPlanner.ChunkLength;
            return chunk.StartCurvature + (chunk.EndCurvature - chunk.StartCurvature) * Mathf.Clamp01(s / EaseLength);
        }

        /// <summary>Heading in radians, measured from +Z toward +X.</summary>
        public double Heading(double distance)
        {
            int index = ChunkIndex((float)distance);
            ChunkStart chunk = Chunk(index);
            return chunk.Heading + TurnWithin(chunk, distance - index * WorldPlanner.ChunkLength);
        }

        /// <summary>Route point in world (double) coordinates.</summary>
        public void Point(double distance, out double x, out double z)
        {
            int index = ChunkIndex((float)distance);
            ChunkStart chunk = Chunk(index);
            double s = distance - index * WorldPlanner.ChunkLength;
            int sample = Mathf.Clamp((int)(s / SampleStep), 0, SamplesPerChunk - 1);
            double s0 = sample * SampleStep;
            Vector2 offset = chunk.Offsets[sample];
            double ds = s - s0;
            double mid = chunk.Heading + TurnWithin(chunk, s0 + ds * 0.5);
            x = chunk.X + offset.x + System.Math.Sin(mid) * ds;
            z = chunk.Z + offset.y + System.Math.Cos(mid) * ds;
        }

        /// <summary>Position of a point beside the track (lateral metres, + to the right) relative to an origin.</summary>
        public Vector3 Local(double distance, float lateral, float height, double originX, double originZ)
        {
            Point(distance, out double x, out double z);
            double heading = Heading(distance);
            x += System.Math.Cos(heading) * lateral;
            z -= System.Math.Sin(heading) * lateral;
            return new Vector3((float)(x - originX), height, (float)(z - originZ));
        }

        public Quaternion Rotation(double distance)
        {
            return Quaternion.Euler(0f, (float)(Heading(distance) * Mathf.Rad2Deg), 0f);
        }

        private static int ChunkIndex(float distance) => Mathf.Max(0, Mathf.FloorToInt(distance / WorldPlanner.ChunkLength));

        private static double TurnWithin(ChunkStart chunk, double s)
        {
            double k0 = chunk.StartCurvature;
            double dk = chunk.EndCurvature - chunk.StartCurvature;
            double eased = s < EaseLength ? s * s / (2.0 * EaseLength) : EaseLength * 0.5 + (s - EaseLength);
            return k0 * s + dk * eased;
        }

        private ChunkStart Chunk(int index)
        {
            while (chunks.Count <= index)
            {
                int i = chunks.Count;
                ChunkStart start;
                if (i == 0)
                {
                    start = new ChunkStart { X = 0d, Z = 0d, Heading = 0d, StartCurvature = 0f };
                }
                else
                {
                    ChunkStart previous = chunks[i - 1];
                    Vector2 end = Integrate(previous, WorldPlanner.ChunkLength, null);
                    start = new ChunkStart
                    {
                        X = previous.X + end.x,
                        Z = previous.Z + end.y,
                        Heading = previous.Heading + TurnWithin(previous, WorldPlanner.ChunkLength),
                        StartCurvature = previous.EndCurvature
                    };
                }
                start.EndCurvature = planner.Get(i).EndCurvature;
                start.Offsets = new Vector2[SamplesPerChunk];
                Integrate(start, WorldPlanner.ChunkLength, start.Offsets);
                chunks.Add(start);
            }
            return chunks[index];
        }

        /// <summary>Offset from the chunk start after s metres; optionally records every sample.</summary>
        private static Vector2 Integrate(ChunkStart chunk, double length, Vector2[] samples)
        {
            const double step = 0.5;
            double x = 0d, z = 0d, s = 0d;
            int recorded = 0;
            if (samples != null) samples[recorded++] = Vector2.zero;
            while (s < length - 1e-6)
            {
                double ds = System.Math.Min(step, length - s);
                double mid = chunk.Heading + TurnWithin(chunk, s + ds * 0.5);
                x += System.Math.Sin(mid) * ds;
                z += System.Math.Cos(mid) * ds;
                s += ds;
                if (samples != null && recorded < samples.Length && s >= recorded * SampleStep - 1e-6)
                    samples[recorded++] = new Vector2((float)x, (float)z);
            }
            return new Vector2((float)x, (float)z);
        }
    }
}
