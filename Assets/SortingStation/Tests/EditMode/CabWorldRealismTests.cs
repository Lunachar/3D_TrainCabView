using NUnit.Framework;
using UnityEngine;

namespace SortingStation.Tests
{
    public sealed class CabWorldRealismTests
    {
        [TestCase(CabTreeSpecies.Broadleaf)]
        [TestCase(CabTreeSpecies.Birch)]
        [TestCase(CabTreeSpecies.Spruce)]
        [TestCase(CabTreeSpecies.Bush)]
        public void EverySpeciesHasBarkAndFoliage(CabTreeSpecies species)
        {
            Mesh mesh = CabTreeFactory.GetMesh(species, 1, false);
            Assert.That(mesh.subMeshCount, Is.EqualTo(2));
            Assert.That(mesh.GetTriangles(0).Length, Is.GreaterThan(0), "bark");
            Assert.That(mesh.GetTriangles(1).Length, Is.GreaterThan(0), "foliage");
            Assert.That(mesh.triangles.Length / 3, Is.LessThan(1500), "keep trees cheap for the tablet");
            Assert.That(mesh.bounds.min.y, Is.GreaterThan(-1.5f), "trees stand on the ground");
        }

        [Test]
        public void DeciduousTreesAreBareInWinterButSprucesKeepNeedles()
        {
            Assert.That(CabTreeFactory.IsLeafless(CabTreeSpecies.Birch, SeasonType.Winter), Is.True);
            Assert.That(CabTreeFactory.IsLeafless(CabTreeSpecies.Broadleaf, SeasonType.Winter), Is.True);
            Assert.That(CabTreeFactory.IsLeafless(CabTreeSpecies.Spruce, SeasonType.Winter), Is.False);
            Assert.That(CabTreeFactory.IsLeafless(CabTreeSpecies.Birch, SeasonType.Summer), Is.False);
            Mesh bare = CabTreeFactory.GetMesh(CabTreeSpecies.Broadleaf, 2, true);
            Assert.That(bare.GetTriangles(1).Length, Is.EqualTo(0));
        }

        [Test]
        public void VegetationPassReplacesPrimitiveTreesAndFoliageSprites()
        {
            GameObject route = new GameObject("Route");
            try
            {
                GameObject tree = new GameObject("Tree_4");
                tree.transform.SetParent(route.transform, false);
                GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crown.transform.SetParent(tree.transform, false);
                GameObject billboard = GameObject.CreatePrimitive(PrimitiveType.Quad);
                billboard.name = "FoliageBillboard";
                billboard.transform.SetParent(route.transform, false);
                billboard.transform.localPosition = new Vector3(6f, 3.6f, 20f);

                int placed = CabVegetation.Populate(route.transform, SeasonType.Summer);

                Assert.That(placed, Is.EqualTo(2));
                Assert.That(crown.activeSelf, Is.False);
                Assert.That(billboard.activeSelf, Is.False);
                Assert.That(tree.transform.Find(CabVegetation.TreeObjectName), Is.Not.Null);
                Transform plant = route.transform.Find("Plant3D_2") ?? FindByPrefix(route.transform, "Plant3D_");
                Assert.That(plant, Is.Not.Null);
                Assert.That(plant.localPosition, Is.EqualTo(new Vector3(6f, 0f, 20f)));
            }
            finally
            {
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void PbrPassGivesBallastAGravelTextureAndKeepsItsTiling()
        {
            GameObject route = new GameObject("Route");
            try
            {
                GameObject bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bed.transform.SetParent(route.transform, false);
                Material ballast = CabShaders.CreateLit("Ballast", Color.gray);
                ballast.mainTextureScale = new Vector2(3f, 40f);
                bed.GetComponent<Renderer>().sharedMaterial = ballast;

                Assert.That(CabWorldPbrUpgrade.Apply(route.transform, SeasonType.Summer), Is.EqualTo(1));

                Material upgraded = bed.GetComponent<Renderer>().sharedMaterial;
                Assert.That(upgraded, Is.Not.SameAs(ballast));
                Assert.That(upgraded.mainTexture, Is.Not.Null);
                Assert.That(upgraded.mainTexture.name, Does.StartWith("Gravel022"));
                Assert.That(upgraded.mainTextureScale, Is.EqualTo(new Vector2(3f, 40f)));
            }
            finally
            {
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void HillsStayOutOfTheTrackCorridorAndRiseInTheMountains()
        {
            const float cycle = 1170f;
            for (float d = 0f; d < cycle; d += 37f)
            {
                Assert.That(CabWorldExtras.HillHeight(0f, d, cycle), Is.EqualTo(0f));
                Assert.That(CabWorldExtras.HillHeight(-30f, d, cycle), Is.EqualTo(0f));
                Assert.That(CabWorldExtras.HillHeight(35f, d, cycle), Is.EqualTo(0f));
                Assert.That(CabWorldExtras.HillHeight(200f, d, cycle), Is.GreaterThan(0f));
            }
            float meadow = CabWorldExtras.HillHeight(150f, cycle * 0.05f, cycle);
            float mountains = CabWorldExtras.HillHeight(150f, cycle * 0.62f, cycle);
            Assert.That(mountains, Is.GreaterThan(meadow + 15f));
        }

        [Test]
        public void DeskScreensShowDrivingAndJourneyInformation()
        {
            string speed = CabRideController.SpeedScreenText(42.4f, 0.5f, 0.25f, 380.2f);
            Assert.That(speed, Does.Contain("42"));
            Assert.That(speed, Does.Contain("Тяга 50%"));
            Assert.That(speed, Does.Contain("Тормоз 25%"));
            Assert.That(speed, Does.Contain("До станции 380 м"));
            Assert.That(CabRideController.SpeedScreenText(0f, 0f, 0f, -1f), Does.Not.Contain("До станции"));

            string info = CabRideController.InfoScreenText("СИСТЕМА ГОТОВА", "Удельная", "Деревня", 0.25f, WeatherType.Snow);
            Assert.That(info, Does.Contain("СИСТЕМА ГОТОВА"));
            Assert.That(info, Does.Contain("След.: Удельная"));
            Assert.That(info, Does.Contain("Деревня"));
            Assert.That(info, Does.Contain("10:00"));
            Assert.That(info, Does.Contain("снег"));
        }

        [Test]
        public void CabCaptionsAndSignsUseTheDepthTestedTextShader()
        {
            GameObject rig = new GameObject("Rig");
            try
            {
                CabCockpitFactory.Create(rig.transform);
                Assert.That(CabWorldText.Apply(rig.transform), Is.GreaterThan(5));
                foreach (TextMesh text in rig.GetComponentsInChildren<TextMesh>(true))
                    Assert.That(text.GetComponent<MeshRenderer>().sharedMaterial.shader.name, Is.EqualTo("SortingStation/WorldText"));
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
        }

        [Test]
        public void StationArrivalIsAnnouncedByName()
        {
            Assert.That(CabRideController.StationArrivalAnnouncement("Удельная"),
                Is.EqualTo("Будьте осторожны, на станцию Удельная прибывает поезд."));
        }

        [Test]
        public void FirstPlatformEndsBeforeTheTunnelAndTheCabStopsOnThePlatform()
        {
            const float cycle = 1170f;
            float platformEnd = cycle * CabRouteLayout.FirstStation01 + CabRouteLayout.PlatformLength * 0.5f;
            Assert.That(platformEnd, Is.LessThan(cycle * CabRouteLayout.TunnelStart01 - 15f));
            // The whole train (locomotive + coaches) fits along the platform behind the stop point.
            float trainLength = CabTrainConsist.LocomotiveLength + CabTrainConsist.CoachCount *
                (CabTrainConsist.CoachLength + CabTrainConsist.CouplingGap);
            Assert.That(trainLength, Is.GreaterThan(CabRouteLayout.PlatformLength * 0.5f));
        }

        [Test]
        public void TrainCarsFollowTheTrackBehindTheCabAcrossTheLoopSeam()
        {
            const float cycle = 1170f;
            GameObject route = new GameObject("Route");
            try
            {
                CabTrainConsist consist = new CabTrainConsist(route.transform);
                foreach (float distance in new[] { 5f, 480f, 700f })
                {
                    consist.UpdatePose(distance, cycle);
                    float previousZ = float.MaxValue;
                    foreach (Transform car in consist.Cars)
                    {
                        float z = car.localPosition.z;
                        Assert.That(z, Is.LessThan(previousZ), "cars run one behind another at " + distance);
                        Assert.That(z, Is.GreaterThan(distance - 100f).And.LessThan(distance + 5f), "no car jumps across the loop");
                        float trackX = Cab3DTrackMath.Point(Mathf.Repeat(z, cycle), cycle).x;
                        Assert.That(Mathf.Abs(car.localPosition.x - trackX), Is.LessThan(0.6f), "car stays on the track");
                        previousZ = z;
                    }
                }
                Assert.That(consist.Cars[0].gameObject.layer, Is.EqualTo(CabTrainConsist.ExteriorLayer));
            }
            finally
            {
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void LoopContinuationCopiesTheStartOneLoopAhead()
        {
            const float cycle = 1170f;
            GameObject route = new GameObject("Route");
            try
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = "StartMarker";
                marker.transform.SetParent(route.transform, false);
                marker.transform.localPosition = new Vector3(3f, 0f, 20f);
                GameObject far = GameObject.CreatePrimitive(PrimitiveType.Cube);
                far.name = "MiddleMarker";
                far.transform.SetParent(route.transform, false);
                far.transform.localPosition = new Vector3(0f, 0f, 600f);

                CabWorldExtras.BuildLapContinuation(route.transform, cycle, 270f, 90f);

                Transform next = route.transform.Find(CabWorldExtras.LapContinuationName + "/NextLap/StartMarker");
                Assert.That(next, Is.Not.Null);
                Vector3 copied = route.transform.InverseTransformPoint(next.position);
                Assert.That(Vector3.Distance(copied, new Vector3(3f, 0f, 20f + cycle)), Is.LessThan(0.01f));
                Assert.That(route.transform.Find(CabWorldExtras.LapContinuationName + "/NextLap/MiddleMarker"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void RearMirrorsRenderThroughTheirOwnSmallCameras()
        {
            GameObject rig = new GameObject("Rig");
            try
            {
                CabRearMirrors mirrors = rig.AddComponent<CabRearMirrors>();
                mirrors.Build(rig.transform, CabCockpitFactory.ControlLayer);
                Camera[] cameras = rig.GetComponentsInChildren<Camera>(true);
                Assert.That(cameras.Length, Is.EqualTo(2));
                foreach (Camera camera in cameras)
                {
                    Assert.That(camera.enabled, Is.False, "rendered on a budget, not every frame");
                    Assert.That(camera.targetTexture, Is.Not.Null);
                    Assert.That(camera.targetTexture.width * camera.targetTexture.height, Is.LessThanOrEqualTo(256 * 256));
                    Assert.That((camera.cullingMask & (1 << CabCockpitFactory.ControlLayer)) == 0, Is.True);
                    Assert.That(Vector3.Dot(camera.transform.forward, rig.transform.forward), Is.LessThan(-0.9f), "looks back");
                }
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
        }

        [Test]
        public void HeadlightsLightTheTrackFarAhead()
        {
            GameObject root = new GameObject("Root");
            try
            {
                CabHeadlights lights = new CabHeadlights(root.transform, 3f);
                lights.SetOn(true);
                Assert.That(lights.Far.enabled && lights.Flood.enabled, Is.True);
                Assert.That(lights.Far.renderMode, Is.EqualTo(LightRenderMode.ForcePixel));
                foreach (float ahead in new[] { 15f, 30f, 45f })
                {
                    Vector3 ground = new Vector3(0f, 0f, 3f + ahead);
                    Vector3 toGround = ground - lights.Far.transform.position;
                    float offAxis = Vector3.Angle(lights.Far.transform.forward, toGround);
                    Assert.That(offAxis, Is.LessThan(lights.Far.spotAngle * 0.5f), "inside the beam at " + ahead);
                    float nDotL = Vector3.Dot(Vector3.up, -toGround.normalized);
                    float lit = lights.Far.intensity / toGround.sqrMagnitude * nDotL;
                    Assert.That(lit, Is.GreaterThan(ahead > 40f ? 0.04f : 0.08f), "ground visibly lit at " + ahead + " m");
                }
                lights.SetOn(false);
                Assert.That(lights.Far.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Transform FindByPrefix(Transform parent, string prefix)
        {
            foreach (Transform child in parent)
                if (child.name.StartsWith(prefix)) return child;
            return null;
        }
    }
}
