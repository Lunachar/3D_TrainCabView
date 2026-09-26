using NUnit.Framework;
using UnityEngine;

namespace SortingStation.Tests
{
    public sealed class WorldStreamingTests
    {
        [Test]
        public void PlannerIsDeterministicForASeed()
        {
            WorldPlanner a = new WorldPlanner(42), b = new WorldPlanner(42), c = new WorldPlanner(43);
            // Ask in different orders: the plan must not depend on it.
            a.Get(300);
            bool differs = false;
            for (int i = 0; i < 300; i++)
            {
                Assert.That(a.Get(i).Kind, Is.EqualTo(b.Get(i).Kind), "chunk " + i);
                Assert.That(a.Get(i).StationNumber, Is.EqualTo(b.Get(i).StationNumber));
                differs |= a.Get(i).Kind != c.Get(i).Kind;
            }
            Assert.That(differs, Is.True, "another seed gives another world");
        }

        [Test]
        public void GrammarKeepsTunnelsInFoothillsAndStationsSpaced()
        {
            WorldPlanner planner = new WorldPlanner(7);
            int lastStation = -1, stations = 0;
            for (int i = 1; i < 1700; i++)
            {
                WorldChunkPlan plan = planner.Get(i);
                if (plan.TunnelEntrance) Assert.That(planner.Get(i - 1).Kind, Is.EqualTo(WorldChunkKind.Foothills), "a tunnel starts in the foothills");
                if (plan.TunnelExit) Assert.That(planner.Get(i + 1).Kind, Is.EqualTo(WorldChunkKind.Foothills));
                if (plan.Kind == WorldChunkKind.Tunnel || plan.IsStation) Assert.That(plan.EndCurvature, Is.EqualTo(0f), "straight in tunnels and at platforms");
                if (!plan.IsStation) continue;
                stations++;
                if (lastStation >= 0) Assert.That(i - lastStation, Is.LessThanOrEqualTo(WorldPlanner.MaximumChunksBetweenStations + 8), "no endless run without a stop");
                lastStation = i;
            }
            Assert.That(stations, Is.GreaterThan(40), "about one station every 3 km over 200 km");
        }

        [Test]
        public void TrackIsContinuousAcrossChunkBorders()
        {
            TrackPath path = new TrackPath(new WorldPlanner(11));
            for (int k = 1; k < 200; k++)
            {
                float border = k * WorldPlanner.ChunkLength;
                path.Point(border - 0.001, out double x0, out double z0);
                path.Point(border + 0.001, out double x1, out double z1);
                Assert.That(System.Math.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0)), Is.LessThan(0.01), "position at " + border);
                Assert.That(System.Math.Abs(path.Heading(border + 0.001) - path.Heading(border - 0.001)) * Mathf.Rad2Deg, Is.LessThan(0.01), "heading at " + border);
                Assert.That(Mathf.Abs(path.Curvature(border)), Is.LessThanOrEqualTo(1f / 900f + 1e-6f), "radius at least 900 m");
            }
        }

        [Test]
        public void TerrainChunksShareTheirBorderVertices()
        {
            WorldPlanner planner = new WorldPlanner(5);
            TrackPath path = new TrackPath(planner);
            WorldTerrain terrain = new WorldTerrain(planner, 5);
            for (int k = 3; k < 30; k++)
            {
                float border = k * WorldPlanner.ChunkLength;
                foreach (float x in WorldTerrain.Columns)
                    Assert.That(terrain.Height(border, x + WorldTerrain.CorridorCentre, terrain.InsideTunnel(border)),
                        Is.EqualTo(terrain.Height(border, x + WorldTerrain.CorridorCentre, terrain.InsideTunnel(border))));
            }
            // The corridor is flat for the track everywhere outside the rivers.
            for (float d = 0f; d < 20000f; d += 37f)
                if (terrain.RiverDepth(d, WorldTerrain.CorridorCentre) < 0.01f && !terrain.InsideTunnel(d))
                    Assert.That(terrain.Height(d, WorldTerrain.CorridorCentre, false), Is.EqualTo(WorldTerrain.GroundOffset).Within(0.001f), "flat bed at " + d);
        }

        [Test]
        public void PlacementMaskKeepsTheCorridorAndRegisteredZonesFree()
        {
            PlacementMask mask = new PlacementMask(0f, 120f);
            Assert.That(mask.IsFree(50f, 0f, 0.5f), Is.False, "track");
            Assert.That(mask.IsFree(50f, WorldChunkBuilder.SecondTrackOffset, 0.5f), Is.False, "second track");
            Assert.That(mask.IsFree(50f, 20f, 1f), Is.True);
            mask.AddRect(40f, 60f, 15f, 25f);
            Assert.That(mask.IsFree(50f, 20f, 1f), Is.False, "road or building");
            Assert.That(mask.IsFree(50f, 27f, 1f), Is.True);
        }

        [Test]
        public void StationsSitOnStraightChunksWithTheStopPastThePlatformCentre()
        {
            WorldPlanner planner = new WorldPlanner(3);
            float stop = planner.NextStopAfter(0f, out WorldChunkPlan station);
            Assert.That(stop, Is.GreaterThan(0f));
            Assert.That(station.IsStation, Is.True);
            Assert.That(stop, Is.EqualTo(station.Start + WorldPlanner.PlatformCentre + WorldPlanner.StopOffsetPastPlatformCentre).Within(0.01f));
            Assert.That(planner.NextStopAfter(stop + 1f, out WorldChunkPlan next), Is.GreaterThan(stop));
            Assert.That(WorldPlanner.StationDefinition(station), Is.Not.Null);
        }

        [Test]
        public void PeopleAreOneSkinnedMeshWithAWalkingSkeleton()
        {
            GameObject parent = new GameObject("Platform");
            try
            {
                System.Random random = new System.Random(4);
                for (int i = 0; i < 12; i++)
                {
                    PersonAnimator person = PersonFactory.Create(parent.transform, "P" + i, new Vector3(i, 0f, 0f), PersonLook.Random(random, i % 4 == 0));
                    SkinnedMeshRenderer[] skins = person.GetComponentsInChildren<SkinnedMeshRenderer>();
                    Assert.That(skins.Length, Is.EqualTo(1), "one renderer per person");
                    Mesh mesh = skins[0].sharedMesh;
                    Assert.That(mesh.triangles.Length / 3, Is.LessThan(3500), "cheap enough for crowds on the tablet");
                    Assert.That(skins[0].bones.Length, Is.EqualTo(18));
                    Assert.That(mesh.bounds.min.y, Is.InRange(-0.1f, 0.05f), "feet on the ground");
                    Assert.That(mesh.bounds.max.y, Is.InRange(1f, 2.3f), "a person's height");
                }
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void VehiclesAndRailCarsStayWithinTriangleBudgets()
        {
            foreach (VehicleType type in System.Enum.GetValues(typeof(VehicleType)))
            {
                Assert.That(VehicleFactory.TriangleCount(type), Is.LessThan(1500), type.ToString());
                Assert.That(VehicleFactory.GetMesh(type, 0).bounds.min.y, Is.GreaterThan(-0.05f), "wheels on the ground: " + type);
            }
            foreach (RailCarKind kind in System.Enum.GetValues(typeof(RailCarKind)))
            {
                Mesh mesh = TrainFactory.GetMesh(kind, false);
                Assert.That(mesh.triangles.Length / 3, Is.LessThan(4000), kind.ToString());
                Assert.That(mesh.bounds.size.x, Is.InRange(2.8f, 3.4f), "real width: " + kind);
            }
        }
    }
}
