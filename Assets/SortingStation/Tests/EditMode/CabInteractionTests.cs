using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SortingStation.Tests
{
    public sealed class CabInteractionTests
    {
        [Test]
        public void Sequence_IsDeterministicAndDoesNotRepeatLastThree()
        {
            CabInteractionDefinition[] definitions =
            {
                Definition("cow"), Definition("sheep"), Definition("birds"), Definition("workers"), Definition("crossing")
            };
            CabInteractionSequence first = new CabInteractionSequence(definitions, 42, 3);
            CabInteractionSequence second = new CabInteractionSequence(definitions, 42, 3);
            List<string> recent = new List<string>();

            for (int i = 0; i < 20; i++)
            {
                string a = first.Next(_ => true).id;
                string b = second.Next(_ => true).id;
                Assert.AreEqual(a, b);
                CollectionAssert.DoesNotContain(recent, a);
                recent.Add(a);
                if (recent.Count > 3) recent.RemoveAt(0);
            }
        }

        [Test]
        public void Sequence_IntervalStaysInsideConfiguredRange()
        {
            CabInteractionSequence sequence = new CabInteractionSequence(new[] { Definition("cow") }, 7, 3);
            for (int i = 0; i < 30; i++)
                Assert.That(sequence.NextInterval(45f, 90f), Is.InRange(45f, 90f));
        }

        [Test]
        public void Sequence_ReturnsNoOpportunityInsteadOfRepeatingRecentEvent()
        {
            CabInteractionSequence sequence = new CabInteractionSequence(new[] { Definition("only") }, 7, 3);
            Assert.IsNotNull(sequence.Next(_ => true));
            Assert.IsNull(sequence.Next(_ => true));
        }

        [Test]
        public void DispatcherInteractionRequiresCallAndResponseClips()
        {
            CabInteractionDefinition item = Definition("dispatcher");
            item.requiresDispatcherPair = true;
            Assert.IsFalse(item.HasRequiredResources(null));

            CabInteractionAudioBank bank = new CabInteractionAudioBank
            {
                interactionId = "dispatcher",
                dispatcherCalls = new AudioClip[1],
                dispatcherResponses = new AudioClip[1]
            };
            Assert.IsFalse(item.HasRequiredResources(bank));
        }

        [Test]
        public void StationModel_AutomaticallyOpensAndClosesDoorsThenReleasesTraction()
        {
            CabStationStopModel station = new CabStationStopModel(8f, 7f, 1.5f);
            station.BeginApproach();
            station.Step(20f, 0f, false);
            Assert.AreEqual(CabStationPhase.WaitingForDoors, station.Phase);
            station.Step(8.1f, 0f, false);
            Assert.AreEqual(CabStationPhase.DoorsOpen, station.Phase);
            station.Step(7.1f, 0f, false);
            Assert.AreEqual(CabStationPhase.Releasing, station.Phase);
            station.Step(1.6f, 0f, false);
            Assert.AreEqual(CabStationPhase.Complete, station.Phase);
            Assert.AreEqual(1f, station.TractionMultiplier, 0.001f);
        }

        [Test]
        public void StationCatalog_DefaultApproachBeginsCloseEnoughToSeeThePlatform()
        {
            CabInteractionCatalog catalog = ScriptableObject.CreateInstance<CabInteractionCatalog>();
            try
            {
                Assert.That(catalog.StationApproachDistance, Is.EqualTo(45f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void StationCatalog_ResourceUsesSerializedBrakingSettingsForAnApproachableStop()
        {
            CabInteractionCatalog catalog = Resources.Load<CabInteractionCatalog>("Configuration/CabInteractionCatalog");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.StationApproachDistance, Is.EqualTo(45f).Within(0.001f));
            Assert.That(catalog.StationCrawlStartDistance, Is.EqualTo(4f).Within(0.001f));
            Assert.That(catalog.StationCrawlSpeed01, Is.EqualTo(0.09f).Within(0.001f));
            Assert.That(catalog.StationStopTolerance, Is.EqualTo(1.6f).Within(0.001f));
        }

        [Test]
        public void StationApproach_ReachesDoorReadyNearMarkerWithoutExtendedCrawl()
        {
            CabInteractionCatalog catalog = Resources.Load<CabInteractionCatalog>("Configuration/CabInteractionCatalog");
            CabRideDefinition definition = ScriptableObject.CreateInstance<CabRideDefinition>();
            definition.ConfigureDefaults();
            CabMotionModel motion = new CabMotionModel(definition);
            motion.SetThrottle(1f);
            const float dt = 1f / 60f;
            for (int i = 0; i < 600; i++) motion.Step(dt, 0f);

            CabStationStopModel station = new CabStationStopModel(catalog.StationDoorWaitSeconds,
                catalog.StationOpenSeconds, catalog.StationTractionReleaseSeconds,
                catalog.StationCrawlStartDistance, catalog.StationCrawlSpeed01, catalog.StationStopTolerance);
            station.BeginApproach();
            float targetDistance = catalog.StationApproachDistance;
            float travelled = 0f;
            float slowCrawlSeconds = 0f;
            for (int i = 0; i < 1200 && station.Phase == CabStationPhase.Approaching; i++)
            {
                float remaining = targetDistance - travelled;
                station.Step(dt, motion.Speed01, false, remaining);
                if (remaining <= catalog.StationCrawlStartDistance && motion.Speed01 < 0.14f)
                    slowCrawlSeconds += dt;
                motion.Step(dt, station.BrakeStrength, station.TractionMultiplier);
                travelled += motion.Speed01 * definition.WorldUnitsPerSecond * dt;
            }

            Assert.That(station.Phase, Is.EqualTo(CabStationPhase.WaitingForDoors));
            Assert.That(Mathf.Abs(targetDistance - travelled), Is.LessThanOrEqualTo(catalog.StationStopTolerance + 0.2f));
            Assert.That(slowCrawlSeconds, Is.LessThan(4f), "The final approach should not hold a crawl speed for many seconds.");
            Object.DestroyImmediate(definition);
        }

        [TestCase(600)]
        [TestCase(1800)]
        [TestCase(3600)]
        public void StationApproach_CompletesThePhysicalStopInsideThePlatformTolerance(int accelerationFrames)
        {
            CabInteractionCatalog catalog = Resources.Load<CabInteractionCatalog>("Configuration/CabInteractionCatalog");
            CabRideDefinition definition = ScriptableObject.CreateInstance<CabRideDefinition>();
            definition.ConfigureDefaults();
            CabMotionModel motion = new CabMotionModel(definition);
            motion.SetThrottle(1f);
            const float dt = 1f / 60f;
            for (int i = 0; i < accelerationFrames; i++) motion.Step(dt, 0f);
            float approachSpeed = motion.Speed01;

            CabStationStopModel station = new CabStationStopModel(catalog.StationDoorWaitSeconds,
                catalog.StationOpenSeconds, catalog.StationTractionReleaseSeconds,
                catalog.StationCrawlStartDistance, catalog.StationCrawlSpeed01, catalog.StationStopTolerance);
            station.BeginApproach();
            float targetDistance = catalog.StationApproachDistance;
            float travelled = 0f;
            bool doorsAvailableWhileMoving = false;
            const int maximumFrames = 1800;
            for (int i = 0; i < maximumFrames && station.Phase == CabStationPhase.Approaching; i++)
            {
                float remaining = targetDistance - travelled;
                station.Step(dt, motion.Speed01, false, remaining);
                doorsAvailableWhileMoving |= station.Phase == CabStationPhase.WaitingForDoors && motion.Speed01 > 0.005f;
                motion.Step(dt, station.BrakeStrength, station.TractionMultiplier);
                travelled += motion.Speed01 * definition.WorldUnitsPerSecond * dt;
            }

            TestContext.WriteLine($"approachSpeed={approachSpeed:F3} rest={travelled:F3} target={targetDistance:F3} speed={motion.Speed01:F4} phase={station.Phase}");
            Assert.That(doorsAvailableWhileMoving, Is.False,
                "The door-ready state must not be announced until residual braking movement is finished.");
            Assert.That(motion.Speed01, Is.LessThanOrEqualTo(0.005f), "The train must physically stop before doors become available.");
            Assert.That(station.Phase, Is.EqualTo(CabStationPhase.WaitingForDoors));
            Assert.That(Mathf.Abs(targetDistance - travelled), Is.LessThanOrEqualTo(catalog.StationStopTolerance + 0.2f),
                "The final resting position, not just the braking trigger, must be inside the platform door zone.");
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void StationModel_ReachesDoorReadyStateAtThePreciseStopMarker()
        {
            CabStationStopModel station = new CabStationStopModel(8f, 7f, 1.5f, tolerance: 1.8f);
            station.BeginApproach();

            station.Step(0.05f, 0.42f, false, 1.2f);

            Assert.That(station.Phase, Is.EqualTo(CabStationPhase.Approaching));
            Assert.That(station.BrakeStrength, Is.EqualTo(1f));
            station.Step(0.05f, 0f, false, 0f);
            Assert.That(station.Phase, Is.EqualTo(CabStationPhase.WaitingForDoors));
        }

        [Test]
        public void StationModel_VigilanceCancelsStationControl()
        {
            CabStationStopModel station = new CabStationStopModel(8f, 7f, 1.5f);
            station.BeginApproach();
            station.Step(0.1f, 0.6f, true);
            Assert.AreEqual(CabStationPhase.Cancelled, station.Phase);
            Assert.AreEqual(1f, station.TractionMultiplier, 0.001f);
        }

        [Test]
        public void MotionModel_ExternalTractionMultiplierStopsAccelerationSmoothly()
        {
            CabRideDefinition definition = ScriptableObject.CreateInstance<CabRideDefinition>();
            definition.ConfigureDefaults();
            CabMotionModel motion = new CabMotionModel(definition);
            motion.SetThrottle(1f);
            for (int i = 0; i < 60; i++) motion.Step(1f / 60f, 0f, 1f);
            float movingSpeed = motion.Speed01;

            motion.Step(1f / 60f, 0f, 0f);
            Assert.LessOrEqual(motion.Speed01, movingSpeed + 0.001f);
            Assert.Greater(motion.Speed01, 0f);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void PreferencesUpgradeEnablesGentleInteractionsForExistingProfiles()
        {
            UserPreferences preferences = new UserPreferences { preferencesVersion = 2 };
            preferences.Upgrade();
            Assert.IsTrue(preferences.gentleInteractionsEnabled);
            Assert.IsTrue(preferences.gentleHintsEnabled);
            Assert.GreaterOrEqual(preferences.preferencesVersion, 3);
        }

        private static CabInteractionDefinition Definition(string id)
        {
            return new CabInteractionDefinition { id = id, enabled = true, weight = 1f };
        }
    }
}
