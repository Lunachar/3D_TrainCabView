using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SortingStation.Tests
{
    public sealed class CabHybrid3DTests
    {
        private readonly List<RouteSegmentDefinition> created = new List<RouteSegmentDefinition>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < created.Count; i++) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [Test]
        public void JourneyRuntime_StartsWithApprovedPrototypeOrder()
        {
            CabJourneyRuntime runtime = new CabJourneyRuntime(CreateRoutes(), 714);
            RouteSegmentType[] expected =
            {
                RouteSegmentType.Meadow, RouteSegmentType.Forest, RouteSegmentType.Road,
                RouteSegmentType.Village, RouteSegmentType.MountainTunnel
            };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(runtime.CurrentSegment.Type, Is.EqualTo(expected[i]));
                runtime.Advance(runtime.CurrentSegment.Length);
            }
        }

        [Test]
        public void TwoWorldModesReceiveIdenticalJourneyState()
        {
            RouteSegmentDefinition[] routes = CreateRoutes();
            CabJourneyRuntime legacy = new CabJourneyRuntime(routes, 901);
            CabJourneyRuntime hybrid = new CabJourneyRuntime(routes, 901);
            float[] deltas = { 0.5f, 12f, 160f, 48f, 221f, 7f };
            for (int i = 0; i < deltas.Length; i++)
            {
                legacy.Advance(deltas[i]);
                hybrid.Advance(deltas[i]);
                Assert.That(hybrid.Distance, Is.EqualTo(legacy.Distance).Within(0.0001f));
                Assert.That(hybrid.CurrentSegment.Type, Is.EqualTo(legacy.CurrentSegment.Type));
                Assert.That(hybrid.SegmentProgress, Is.EqualTo(legacy.SegmentProgress).Within(0.0001f));
            }
        }

        [Test]
        public void TrackCurveNeverTiltsVerticallyAndReturnsToCentre()
        {
            const float cycle = 1120f;
            for (int i = 0; i <= 200; i++)
            {
                float distance = cycle * i / 200f;
                Assert.That(Cab3DTrackMath.Point(distance, cycle).y, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(Cab3DTrackMath.Tangent(distance, cycle).y, Is.EqualTo(0f).Within(0.0001f));
            }
            Assert.That(Cab3DTrackMath.Point(0f, cycle).x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(Cab3DTrackMath.Point(cycle - 0.1f, cycle).x, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void OpeningRouteFacesForwardAndAlreadyContainsNearbyScenery()
        {
            const float cycle = 1120f;
            Assert.That(Vector3.Dot(Cab3DTrackMath.Tangent(0f, cycle), Vector3.forward), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(Cab3DTrackMath.Tangent(0.01f, cycle), Vector3.forward), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(Cab3DTrackMath.Tangent(cycle - 0.01f, cycle), Vector3.forward), Is.GreaterThan(0.999f));

            GameObject route = CabWorld3DPrototypeFactory.Create(null, cycle, 1f);
            try
            {
                Assert.That(route.GetComponentsInChildren<Transform>(true),
                    Has.Some.Matches<Transform>(item => item.name == "DepartureRoad"));
                Assert.That(route.GetComponentsInChildren<Transform>(true),
                    Has.Some.Matches<Transform>(item => item.name == "StartTree_0"));
            }
            finally
            {
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void InitialCameraFrustumContainsTrackAndNearbyStartFoliage()
        {
            const float cycle = 1120f;
            GameObject route = CabWorld3DPrototypeFactory.Create(null, cycle, 1f);
            GameObject cameraObject = new GameObject("InitialCabWorldCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            try
            {
                Vector3 point = Cab3DTrackMath.Point(0f, cycle);
                Quaternion heading = Cab3DTrackMath.Heading(0f, cycle);
                route.transform.localRotation = Quaternion.Inverse(heading);
                route.transform.localPosition = route.transform.localRotation * -point;
                camera.transform.SetPositionAndRotation(new Vector3(0f, 3.35f, -8f), Quaternion.Euler(16.5f, 0f, 0f));
                camera.fieldOfView = 61f;
                camera.aspect = 2.5f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 240f;

                Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(camera);
                Renderer[] renderers = route.GetComponentsInChildren<Renderer>(true);
                Transform firstStartTree = route.transform.Find("StartTree_0");
                Transform departureRoad = route.transform.Find("DepartureRoad");
                int visibleTrackParts = 0;
                int visibleStartFoliage = 0;
                int visibleDepartureRoadParts = 0;
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (!renderer.enabled || !GeometryUtility.TestPlanesAABB(frustum, renderer.bounds)) continue;
                    if (renderer.name.Contains("Rail") || renderer.name.Contains("Sleeper")) visibleTrackParts++;
                    if (firstStartTree != null && renderer.transform.IsChildOf(firstStartTree)) visibleStartFoliage++;
                    if (departureRoad != null && renderer.transform.IsChildOf(departureRoad)) visibleDepartureRoadParts++;
                }

                Assert.That(visibleTrackParts, Is.GreaterThan(0), "Track geometry must be in view before the first movement update.");
                Assert.That(visibleStartFoliage, Is.GreaterThan(0), "Nearby trees must be in the opening camera frustum, not appear only after moving.");
                Assert.That(visibleDepartureRoadParts, Is.GreaterThan(0), "The opening roadside scenery should be visible before the train starts moving.");
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void CityApproachRoadSurfaceFollowsTheTrackCurve()
        {
            const float cycle = 1120f;
            const float distance = cycle * 0.79f;
            const float roadLength = 108f;
            const float roadLateralOffset = -20f;
            GameObject route = CabWorld3DPrototypeFactory.Create(null, cycle, 1f);
            try
            {
                Transform road = route.transform.Find("CityApproachRoad");
                Transform surface = road != null ? road.Find("RoadSurface") : null;
                MeshFilter filter = surface != null ? surface.GetComponent<MeshFilter>() : null;

                Assert.That(filter, Is.Not.Null);
                Assert.That(filter.sharedMesh.vertexCount, Is.GreaterThan(50),
                    "A long road on a curved route must be tessellated along the track, not a single straight block.");

                Vector3[] vertices = filter.sharedMesh.vertices;
                Vector3 endLeft = surface.TransformPoint(vertices[vertices.Length - 3]);
                Vector3 endRight = surface.TransformPoint(vertices[vertices.Length - 2]);
                Vector3 actualEnd = (endLeft + endRight) * 0.5f;
                float endDistance = distance + roadLength * 0.5f;
                Vector3 expectedEnd = Cab3DTrackMath.Point(endDistance, cycle) +
                                      Cab3DTrackMath.Heading(endDistance, cycle) *
                                      new Vector3(roadLateralOffset, 0f, 0f);

                Assert.That(Vector2.Distance(new Vector2(actualEnd.x, actualEnd.z),
                    new Vector2(expectedEnd.x, expectedEnd.z)), Is.LessThan(0.05f));
            }
            finally
            {
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void ParallelRoadTrafficIsAnchoredToTheRouteCurve()
        {
            const float cycle = 1120f;
            const float distance = cycle * 0.79f;
            const float roadLength = 108f;
            const float roadLateralOffset = -20f;
            const float laneOffset = 1.55f;
            GameObject route = CabWorld3DPrototypeFactory.Create(null, cycle, 1f);
            try
            {
                Transform vehicle = route.transform.Find("CityApproachRoad_CarA");
                Assert.That(vehicle, Is.Not.Null,
                    "Parallel road traffic should stay in the same route coordinate frame as its curved road.");

                float startDistance = distance - roadLength * 0.42f;
                Vector3 expectedPosition = Cab3DTrackMath.Point(startDistance, cycle) +
                                           Cab3DTrackMath.Heading(startDistance, cycle) *
                                           new Vector3(roadLateralOffset + laneOffset, 0.48f, 0f);
                Assert.That(Vector3.Distance(vehicle.position, expectedPosition), Is.LessThan(0.05f),
                    "Vehicle spawn position must lie on the curved road lane, not the road's tangent plane.");
            }
            finally
            {
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void CrossingTrafficUsesTwoRoadLanesAndFacesTheDirectionItTravels()
        {
            GameObject route = CabWorld3DPrototypeFactory.Create(null, 1120f, 1f);
            try
            {
                Transform crossingFrame = route.transform.Find("LevelCrossingFrame");
                Assert.That(crossingFrame, Is.Not.Null,
                    "Crossing traffic should live in the frame aligned to the crossing road.");
                Transform traffic = crossingFrame.Find("CrossingTraffic");
                Assert.That(traffic, Is.Not.Null,
                    "Crossing traffic needs a local frame aligned to the road across the railway.");

                Transform car = traffic.Find("CrossingCar");
                Transform truck = traffic.Find("CrossingTruck");
                Assert.That(car, Is.Not.Null);
                Assert.That(truck, Is.Not.Null);
                Assert.That(car.GetComponent<Cab3DRoadVehicleAnimator>(), Is.Not.Null);
                Assert.That(truck.GetComponent<Cab3DRoadVehicleAnimator>(), Is.Not.Null);
                Assert.That(car.localPosition.x, Is.EqualTo(-9f).Within(0.05f));
                Assert.That(truck.localPosition.x, Is.EqualTo(9f).Within(0.05f));
                Assert.That(Mathf.Abs(car.localPosition.z), Is.LessThan(1.1f),
                    "The car must stay within one of the two lanes on the 5 m-wide crossing road.");
                Assert.That(Mathf.Abs(truck.localPosition.z), Is.LessThan(1.1f),
                    "The truck must stay within one of the two lanes on the 5 m-wide crossing road.");
                Assert.That(Vector3.Dot(car.localRotation * Vector3.back, Vector3.right), Is.GreaterThan(0.99f),
                    "The car's nose should point along its positive-X travel direction.");
                Assert.That(Vector3.Dot(truck.localRotation * Vector3.back, Vector3.left), Is.GreaterThan(0.99f),
                    "The truck's nose should point along its negative-X travel direction.");
                Assert.That(car.GetComponentsInChildren<Transform>(true), Has.Some.Matches<Transform>(item => item.name == "Wheel"));
                Assert.That(truck.GetComponentsInChildren<Transform>(true), Has.Some.Matches<Transform>(item => item.name == "Wheel"));
            }
            finally
            {
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void CrossingBarriersSpanTheRoadInsteadOfRunningAlongIt()
        {
            GameObject route = CabWorld3DPrototypeFactory.Create(null, 1120f, 1f);
            try
            {
                Transform frame = route.transform.Find("LevelCrossingFrame");
                Assert.That(frame, Is.Not.Null,
                    "Crossing road infrastructure should share one frame aligned to the local road.");
                Transform[] barriers = frame.GetComponentsInChildren<Transform>(true);
                int found = 0;
                for (int i = 0; i < barriers.Length; i++)
                {
                    if (!barriers[i].name.Contains("CrossingBarrier")) continue;
                    found++;
                    Transform arm = barriers[i].Find("BarrierArm");
                    Assert.That(arm, Is.Not.Null);
                    Assert.That(arm.localScale.z, Is.GreaterThan(arm.localScale.x * 10f),
                        "A closed barrier arm must span the road width, not lie parallel to traffic.");
                    Assert.That(barriers[i].GetComponentsInChildren<Transform>(true),
                        Has.Some.Matches<Transform>(item => item.name == "BarrierRedBand"),
                        "The barrier should have alternating red warning bands.");
                }
                Assert.That(found, Is.EqualTo(2));

                Cab3DRouteAuthoring authoring = route.GetComponent<Cab3DRouteAuthoring>();
                authoring.SetCrossingApproach(80f);
                Transform firstBarrier = frame.Find("CrossingBarrier");
                Transform raisedBarrierArm = firstBarrier != null ? firstBarrier.Find("BarrierArm") : null;
                Assert.That(raisedBarrierArm, Is.Not.Null);
                Vector3 raisedTip = firstBarrier.TransformPoint(new Vector3(0f, 0f, 2.6f));
                Assert.That(raisedTip.y, Is.GreaterThan(firstBarrier.position.y + 2f),
                    "When open, the arm should rotate upward around the crossing's transverse pivot.");
            }
            finally
            {
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void CrossingSignalsFaceIncomingRoadTrafficOnBothShoulders()
        {
            GameObject route = CabWorld3DPrototypeFactory.Create(null, 1120f, 1f);
            try
            {
                Transform frame = route.transform.Find("LevelCrossingFrame");
                Assert.That(frame, Is.Not.Null);
                Transform[] children = frame.GetComponentsInChildren<Transform>(true);
                int found = 0;
                for (int i = 0; i < children.Length; i++)
                {
                    Transform signal = children[i];
                    if (signal.name != "CrossingWarning") continue;
                    found++;
                    Assert.That(Mathf.Abs(signal.localPosition.z), Is.GreaterThan(2.5f),
                        "Each signal must stand outside one of the road shoulders.");
                    Vector3 approachDirection = signal.localPosition.x < 0f ? Vector3.left : Vector3.right;
                    Vector3 facing = signal.localRotation * Vector3.back;
                    Assert.That(Vector3.Dot(facing, approachDirection), Is.GreaterThan(0.98f),
                        "Signal face must look toward drivers approaching from its road end.");
                }
                Assert.That(found, Is.EqualTo(4),
                    "Both road approaches need signals on both shoulders so every traffic lane can see them.");
            }
            finally
            {
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void CityBuildingsUseThePhotorealFacadeTexture()
        {
            GameObject route = CabWorld3DPrototypeFactory.Create(null, 1120f, 1f);
            try
            {
                Transform district = route.transform.Find("CityViaductDistrict");
                Transform building = district != null ? district.Find("CityBuilding") : null;
                Renderer renderer = building != null ? building.GetComponent<Renderer>() : null;

                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.sharedMaterial.mainTexture, Is.Not.Null);
                Assert.That(renderer.sharedMaterial.mainTexture.name, Is.EqualTo("CityFacade_v2"),
                    "City buildings should use the upgraded weathered plaster texture rather than the flat placeholder.");
            }
            finally
            {
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void SideRoadsUseTheDetailedAsphaltTexture()
        {
            Texture2D asphalt = Resources.Load<Texture2D>("Cab3D/RoadAsphalt_v2");
            Assert.That(asphalt, Is.Not.Null,
                "The upgraded seamless asphalt texture must be available as a runtime resource.");

            GameObject route = CabWorld3DPrototypeFactory.Create(null, 1120f, 1f);
            try
            {
                Transform road = route.transform.Find("DepartureRoad");
                Transform surface = road != null ? road.Find("RoadSurface") : null;
                Renderer renderer = surface != null ? surface.GetComponent<Renderer>() : null;

                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.sharedMaterial.mainTexture, Is.SameAs(asphalt),
                    "The first visible side road should use the detailed asphalt rather than a flat placeholder.");
            }
            finally
            {
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void TrackBallastKeepsTheStoneTextureVisibleInsteadOfCrushingItsValue()
        {
            GameObject route = CabWorld3DPrototypeFactory.Create(null, 1120f, 1f);
            try
            {
                Transform track = route.transform.Find("GeneratedTrackCurve");
                Transform ballast = track != null ? track.Find("BallastTwoTrack") : null;
                Renderer renderer = ballast != null ? ballast.GetComponent<Renderer>() : null;

                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.sharedMaterial.mainTexture, Is.Not.Null);
                Assert.That(renderer.sharedMaterial.mainTexture.name, Is.EqualTo("RailwayBallast_v1"));
                Assert.That(renderer.sharedMaterial.color.r, Is.GreaterThan(0.5f),
                    "The stone albedo must not be multiplied by a near-black tint that hides rock detail.");
            }
            finally
            {
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void EditableCabSeatUsesTheUpdatedWovenFabricTexture()
        {
            Texture2D upholstery = Resources.Load<Texture2D>("Cab3D/CabSeatUpholstery_v2");
            GameObject prefab = Resources.Load<GameObject>("Cab3D/CabInterior3DPrototype");
            Assert.That(upholstery, Is.Not.Null);
            Assert.That(prefab, Is.Not.Null);

            Transform cushion = prefab.transform.Find("CabSeatingAndFittings_Editable/DriverSeat3D/SeatCushion");
            Renderer renderer = cushion != null ? cushion.GetComponent<Renderer>() : null;
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sharedMaterial.mainTexture, Is.SameAs(upholstery),
                "The runtime prefab must use the updated upholstery texture, not only the procedural fallback.");
        }

        [Test]
        public void TerrainRegionsKeepTheirIdentityWhenSeasonChanges()
        {
            GameObject route = CabWorld3DPrototypeFactory.Create(null, 1120f, 1f);
            try
            {
                Transform forest = route.transform.Find("GroundRegion_Forest");
                Transform field = route.transform.Find("GroundRegion_Field");
                Transform mountain = route.transform.Find("GroundRegion_Mountain");
                Transform town = route.transform.Find("GroundRegion_Town");
                Transform water = route.transform.Find("GroundRegion_Water");

                Assert.That(forest, Is.Not.Null, "The route should have a visible forest-floor section.");
                Assert.That(field, Is.Not.Null, "The route should transition through open agricultural fields.");
                Assert.That(mountain, Is.Not.Null, "The tunnel approach should have its own rocky terrain palette.");
                Assert.That(town, Is.Not.Null, "The station and town section should have distinct ground treatment.");
                Assert.That(water, Is.Not.Null, "The final lakeside region should close the route cycle.");

                MeshFilter waterMesh = water.GetComponent<MeshFilter>();
                Vector3[] waterVertices = waterMesh.sharedMesh.vertices;
                Vector3 endLeft = waterVertices[waterVertices.Length - 3];
                Vector3 endRight = waterVertices[waterVertices.Length - 2];
                Assert.That((endLeft.z + endRight.z) * 0.5f, Is.EqualTo(1120f).Within(0.05f),
                    "The last ground region must meet the route cycle without folding back to the start of the map.");

                Renderer forestRenderer = forest.GetComponent<Renderer>();
                Renderer fieldRenderer = field.GetComponent<Renderer>();
                Color forestTint = forestRenderer.sharedMaterial.color;
                Color fieldTint = fieldRenderer.sharedMaterial.color;
                Assert.That(Mathf.Abs(forestTint.r - fieldTint.r), Is.GreaterThan(0.08f),
                    "Forest and field ground must not read as one continuous flat green plane.");

                route.GetComponent<Cab3DRouteAuthoring>().ApplySeason(SeasonType.Autumn);
                Assert.That(forestRenderer.sharedMaterial.mainTexture.name, Is.EqualTo("MeadowGround_Autumn_v1"));
                Assert.That(fieldRenderer.sharedMaterial.mainTexture.name, Is.EqualTo("MeadowGround_Autumn_v1"));
                Assert.That(Mathf.Abs(forestRenderer.sharedMaterial.color.r - fieldRenderer.sharedMaterial.color.r), Is.GreaterThan(0.08f),
                    "Season changes should preserve the region palette instead of flattening every ground zone to one tint.");
            }
            finally
            {
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void GeneratedRoute_StoresTheExactCycleUsedForItsGeometry()
        {
            const float actualJourneyCycle = 1170f;
            GameObject route = CabWorld3DPrototypeFactory.Create(null, actualJourneyCycle, 1f);
            try
            {
                Cab3DRouteAuthoring authoring = route.GetComponent<Cab3DRouteAuthoring>();

                Assert.That(authoring, Is.Not.Null);
                Assert.That(authoring.CycleLength, Is.EqualTo(actualJourneyCycle).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void NativeSun_IsHiddenFromTheFirstMetreOfATunnel()
        {
            Assert.That(CabWorld3DRenderer.ShouldShowNativeSun(false, 0.6f, true), Is.False);
            Assert.That(CabWorld3DRenderer.ShouldShowNativeSun(false, 0.6f, false), Is.True);
        }

        [Test]
        public void HybridCabPhotoOverlayIsSubtleEnoughToAvoidGhostingThePhysicalDashboard()
        {
            Assert.That(CabRideController.HybridPhotoOverlayOpacity(0.30f), Is.EqualTo(0.12f).Within(0.001f));
            Assert.That(CabRideController.HybridPhotoOverlayOpacity(0.08f), Is.EqualTo(0.08f).Within(0.001f));
        }

        [Test]
        public void EverySeasonUsesItsOwnFoliageAtlas()
        {
            Assert.That(Cab3DRouteAuthoring.FoliageAtlasResourceName(SeasonType.Summer),
                Is.EqualTo("Cab3D/FoliageBillboardAtlas_v1"));
            Assert.That(Cab3DRouteAuthoring.FoliageAtlasResourceName(SeasonType.Spring),
                Is.EqualTo("Cab3D/FoliageBillboardAtlas_Spring_v1"));
            Assert.That(Cab3DRouteAuthoring.FoliageAtlasResourceName(SeasonType.Autumn),
                Is.EqualTo("Cab3D/FoliageBillboardAtlas_Autumn_v1"));
            Assert.That(Cab3DRouteAuthoring.FoliageAtlasResourceName(SeasonType.Winter),
                Is.EqualTo("Cab3D/FoliageBillboardAtlas_Winter_v1"));
        }

        [Test]
        public void EditablePhysicalCabContainsEveryPrimaryControlAndCabinLamps()
        {
            GameObject cab = CabInterior3DPrototypeFactory.Create();
            try
            {
                HashSet<CabControlAction> actions = new HashSet<CabControlAction>();
                Cab3DControlAnchor[] anchors = cab.GetComponentsInChildren<Cab3DControlAnchor>(true);
                for (int i = 0; i < anchors.Length; i++) actions.Add(anchors[i].Action);

                Assert.That(actions, Does.Contain(CabControlAction.Throttle));
                Assert.That(actions, Does.Contain(CabControlAction.Brake));
                Assert.That(actions, Does.Contain(CabControlAction.Horn));
                Assert.That(actions, Does.Contain(CabControlAction.Radio));
                Assert.That(actions, Does.Contain(CabControlAction.CabinLight));
                Assert.That(actions, Does.Contain(CabControlAction.Wipers));
                Assert.That(actions, Does.Contain(CabControlAction.Headlights));
                Assert.That(actions, Does.Contain(CabControlAction.Doors));
                Assert.That(actions, Does.Contain(CabControlAction.WindowHeater));
                Assert.That(anchors, Has.Some.Matches<Cab3DControlAnchor>(anchor => anchor.IsDispatcherAcknowledgement));
                Assert.That(cab.transform.Find("CabCeilingLighting_Editable"), Is.Not.Null);
                Assert.That(cab.transform.Find("CabFootwellAndPedals_Editable"), Is.Not.Null);
                Assert.That(cab.GetComponentsInChildren<Transform>(true), Has.Some.Matches<Transform>(item => item.name == "DeadmanPedal3D"));
                Assert.That(cab.GetComponentsInChildren<Transform>(true), Has.Some.Matches<Transform>(item => item.name == "SanderPedal3D"));

                int lamps = 0;
                foreach (Transform item in cab.GetComponentsInChildren<Transform>(true))
                    if (item.name.StartsWith("CeilingCabinLamp3D")) lamps++;
                Assert.That(lamps, Is.EqualTo(2));

                TextMesh[] readouts = cab.GetComponentsInChildren<TextMesh>(true);
                int validatedReadouts = 0;
                for (int i = 0; i < readouts.Length; i++)
                {
                    TextMesh readout = readouts[i];
                    if (readout == null || !readout.name.EndsWith("_Readout")) continue;
                    validatedReadouts++;
                    Assert.That(readout.characterSize, Is.LessThanOrEqualTo(0.05f));
                    Assert.That(readout.transform.localRotation, Is.EqualTo(Quaternion.identity));
                }
                Assert.That(validatedReadouts, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(cab);
            }
        }

        [Test]
        public void ReadoutScreensCannotBecomeTouchControlsEvenInAnOlderCabPrefab()
        {
            GameObject screen = new GameObject("SpeedScreen3D", typeof(BoxCollider), typeof(Cab3DControlAnchor));
            Cab3DControlAnchor anchor = screen.GetComponent<Cab3DControlAnchor>();
            anchor.Configure(CabControlAction.Radio);
            try
            {
                Assert.That(anchor.IsTouchable, Is.False);
                Assert.That(anchor.GetComponent<Collider>(), Is.Not.Null,
                    "Existing prefab colliders may remain, but the raycast layer must ignore this surface.");
            }
            finally
            {
                Object.DestroyImmediate(screen);
            }
        }

        [Test]
        public void SavedCabPrefabGetsHitAreasForItsPhysicalControls()
        {
            GameObject prefab = Resources.Load<GameObject>("Cab3D/CabInterior3DPrototype");
            Assert.That(prefab, Is.Not.Null);
            GameObject cab = Object.Instantiate(prefab);
            try
            {
                Cab3DControlAnchor[] anchors = cab.GetComponentsInChildren<Cab3DControlAnchor>(true);
                Assert.That(anchors.Length, Is.GreaterThan(0));
                bool foundDispatcherAcknowledge = false;
                for (int i = 0; i < anchors.Length; i++)
                {
                    Cab3DControlAnchor anchor = anchors[i];
                    anchor.EnsureHitArea();
                    if (!anchor.IsTouchable)
                    {
                        Collider obsoleteHitArea = anchor.GetComponent<Collider>();
                        Assert.That(obsoleteHitArea == null || !obsoleteHitArea.enabled, Is.True, anchor.name);
                        continue;
                    }
                    Assert.That(anchor.GetComponent<BoxCollider>(), Is.Not.Null, anchor.name);
                    if (anchor.name == "VigilanceButton3D")
                    {
                        Assert.That(anchor.IsDispatcherAcknowledgement, Is.True);
                        foundDispatcherAcknowledge = true;
                    }
                }
                Assert.That(foundDispatcherAcknowledge, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(cab);
            }
        }

        [Test]
        public void EveryPhysicalControlHasATouchHitAreaAndReadoutScreensAreNotControls()
        {
            GameObject cab = CabInterior3DPrototypeFactory.Create();
            try
            {
                Cab3DControlAnchor[] anchors = cab.GetComponentsInChildren<Cab3DControlAnchor>(true);
                Assert.That(anchors.Length, Is.GreaterThanOrEqualTo(11));
                for (int i = 0; i < anchors.Length; i++)
                {
                    Cab3DControlAnchor anchor = anchors[i];
                    Assert.That(anchor.GetComponent<Collider>(), Is.Not.Null,
                        anchor.name + " must be directly touchable");
                    BoxCollider hitArea = anchor.GetComponent<BoxCollider>();
                    Assert.That(hitArea, Is.Not.Null, anchor.name + " needs a predictable box hit area");
                    Assert.That(hitArea.size.x, Is.GreaterThan(0f));
                    Assert.That(hitArea.size.y, Is.GreaterThan(0f));
                    Assert.That(hitArea.size.z, Is.GreaterThan(0f));
                }

                Assert.That(cab.GetComponentsInChildren<Cab3DControlAnchor>(true),
                    Has.None.Matches<Cab3DControlAnchor>(anchor =>
                        anchor.name == "SpeedScreen3D" || anchor.name == "MessageScreen3D"));
            }
            finally
            {
                Object.DestroyImmediate(cab);
            }
        }

        [Test]
        public void PressingSharedActionAnimatesEveryPhysicalControlForThatAction()
        {
            GameObject rendererObject = new GameObject("CabInteriorInputAnimationTest");
            CabInterior3DRenderer renderer = rendererObject.AddComponent<CabInterior3DRenderer>();
            GameObject cab = new GameObject("EditableCab");
            GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = "WiperButtonProp";
            button.transform.SetParent(cab.transform, false);
            button.transform.localPosition = new Vector3(-1f, 0f, 0f);
            button.AddComponent<Cab3DControlAnchor>().Configure(CabControlAction.Wipers, button.transform);
            GameObject toggle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            toggle.name = "WiperToggleProp";
            toggle.transform.SetParent(cab.transform, false);
            toggle.transform.localPosition = new Vector3(1f, 0f, 0f);
            toggle.AddComponent<Cab3DControlAnchor>().Configure(CabControlAction.Wipers, toggle.transform);

            try
            {
                typeof(CabInterior3DRenderer).GetField("interior", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(renderer, cab.transform);
                typeof(CabInterior3DRenderer).GetMethod("CacheAnchors", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(renderer, null);

                renderer.PressControl(CabControlAction.Wipers);
                typeof(CabInterior3DRenderer).GetMethod("AnimatePhysicalButtonPresses", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(renderer, new object[] { 1f });

                Assert.That(button.transform.localPosition.z, Is.GreaterThan(0f),
                    "The round wiper button must visibly depress even when a separate wiper toggle shares its action.");
                Assert.That(toggle.transform.localPosition.z, Is.GreaterThan(0f),
                    "The separate wiper toggle must also remain animated and touchable.");
            }
            finally
            {
                Object.DestroyImmediate(rendererObject);
                Object.DestroyImmediate(cab);
            }
        }

        [Test]
        public void PhysicalControlPointerHitUsesTheDisplayedCabWindowCoordinates()
        {
            GameObject canvasObject = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRect.sizeDelta = new Vector2(Screen.width, Screen.height);
            canvasRect.anchoredPosition = Vector2.zero;

            GameObject imageObject = new GameObject("CabWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imageObject.transform.SetParent(canvasObject.transform, false);
            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.sizeDelta = new Vector2(Screen.width * 0.8f, Screen.height * 0.8f);
            imageRect.anchoredPosition = Vector2.zero;

            GameObject cameraObject = new GameObject("CabRayCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.pixelRect = new Rect(0f, 0f, Screen.width, Screen.height);
            camera.cullingMask = 1 << 30;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;

            GameObject physicalButton = GameObject.CreatePrimitive(PrimitiveType.Cube);
            physicalButton.layer = 30;
            physicalButton.AddComponent<Cab3DControlAnchor>().Configure(CabControlAction.Wipers);
            physicalButton.transform.position = Vector3.zero;
            GameObject decorativeOccluder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            decorativeOccluder.layer = 30;
            decorativeOccluder.name = "DecorativeConsolePanelCollider";
            decorativeOccluder.transform.position = new Vector3(0f, 0f, -2f);

            try
            {
                Vector3 screenCenter = RectTransformUtility.WorldToScreenPoint(null, imageRect.TransformPoint(imageRect.rect.center));
                Assert.That(CabInterior3DRenderer.TryHitControl(camera, imageRect, screenCenter, out CabControlAction action), Is.True);
                Assert.That(action, Is.EqualTo(CabControlAction.Wipers));
                Assert.That(CabInterior3DRenderer.TryHitControl(camera, imageRect, new Vector2(-100f, -100f), out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(decorativeOccluder);
                Object.DestroyImmediate(physicalButton);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(canvasObject);
            }
        }

        [Test]
        public void PhysicalCabControlBeginsDepressingOnPointerDownBeforeClick()
        {
            GameObject rendererObject = new GameObject("PressFeedbackRenderer");
            CabInterior3DRenderer renderer = rendererObject.AddComponent<CabInterior3DRenderer>();
            GameObject canvasObject = new GameObject("PressFeedbackCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform canvas = canvasObject.GetComponent<RectTransform>();
            canvas.sizeDelta = new Vector2(Screen.width, Screen.height);
            GameObject imageObject = new GameObject("CabWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imageObject.transform.SetParent(canvasObject.transform, false);
            RectTransform outputRect = imageObject.GetComponent<RectTransform>();
            outputRect.anchorMin = outputRect.anchorMax = new Vector2(0.5f, 0.5f);
            outputRect.sizeDelta = new Vector2(Screen.width * 0.8f, Screen.height * 0.8f);
            outputRect.anchoredPosition = Vector2.zero;

            GameObject cameraObject = new GameObject("CabRayCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.pixelRect = new Rect(0f, 0f, Screen.width, Screen.height);
            camera.cullingMask = 1 << 30;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            GameObject physicalButton = GameObject.CreatePrimitive(PrimitiveType.Cube);
            physicalButton.layer = 30;
            Cab3DControlAnchor anchor = physicalButton.AddComponent<Cab3DControlAnchor>();
            anchor.Configure(CabControlAction.Wipers, physicalButton.transform);
            physicalButton.transform.position = Vector3.zero;
            typeof(CabInterior3DRenderer).GetField("interiorCamera", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(renderer, camera);
            typeof(CabInterior3DRenderer).GetField("outputRect", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(renderer, outputRect);
            ((Dictionary<CabControlAction, List<Transform>>)typeof(CabInterior3DRenderer)
                .GetField("anchors", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(renderer))
                .Add(CabControlAction.Wipers, new List<Transform> { physicalButton.transform });
            ((Dictionary<Transform, Vector3>)typeof(CabInterior3DRenderer)
                .GetField("restPositions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(renderer))
                .Add(physicalButton.transform, physicalButton.transform.localPosition);

            try
            {
                Transform movingPart = anchor.AnimatedPart;
                float restingZ = movingPart.localPosition.z;
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null,
                    outputRect.TransformPoint(outputRect.rect.center));
                Assert.That(renderer.BeginControlPressFromPointer(screenPoint), Is.True);
                typeof(CabInterior3DRenderer).GetMethod("AnimatePhysicalButtonPresses", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(renderer, new object[] { 1f });

                Assert.That(movingPart.localPosition.z, Is.GreaterThan(restingZ),
                    "A physical control should start moving into its pressed position on pointer-down, not wait for pointer-up/click.");
            }
            finally
            {
                Object.DestroyImmediate(rendererObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(canvasObject);
                Object.DestroyImmediate(physicalButton);
            }
        }

        [Test]
        public void EditablePhysicalCabUsesBlankConsoleSurfaceBehindSeparateControls()
        {
            GameObject cab = CabInterior3DPrototypeFactory.Create();
            try
            {
                Transform console = cab.transform.Find("PhotorealConsolePanel");
                Assert.That(console, Is.Not.Null);
                Texture texture = console.GetComponent<Renderer>().sharedMaterial.mainTexture;
                Assert.That(texture, Is.Not.Null);
                Assert.That(texture.name, Is.EqualTo("CabConsolePanel_v2"));
            }
            finally
            {
                Object.DestroyImmediate(cab);
            }
        }

        private RouteSegmentDefinition[] CreateRoutes()
        {
            RouteSegmentType[] types = (RouteSegmentType[])System.Enum.GetValues(typeof(RouteSegmentType));
            RouteSegmentDefinition[] routes = new RouteSegmentDefinition[types.Length];
            for (int i = 0; i < types.Length; i++)
            {
                RouteSegmentDefinition route = ScriptableObject.CreateInstance<RouteSegmentDefinition>();
                route.Configure(types[i].ToString(), types[i], 150f, 1f, 0, 1f, Color.white,
                    types[i] == RouteSegmentType.Road, false);
                created.Add(route);
                routes[i] = route;
            }
            return routes;
        }
    }
}
