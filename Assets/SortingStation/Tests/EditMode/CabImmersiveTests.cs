using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SortingStation.Tests
{
    public sealed class CabImmersiveTests
    {
        private static readonly CabControlAction[] PhysicalActions =
        {
            CabControlAction.Horn, CabControlAction.Bell, CabControlAction.Headlights, CabControlAction.CabinLight,
            CabControlAction.Wipers, CabControlAction.Radio, CabControlAction.Doors, CabControlAction.WindowHeater,
            CabControlAction.Throttle, CabControlAction.Brake, CabControlAction.DispatcherRadio
        };

        private GameObject rig;
        private Camera camera;

        [SetUp]
        public void SetUp()
        {
            rig = new GameObject("TestDriverRig");
            GameObject head = new GameObject("TestHead", typeof(Camera));
            head.transform.SetParent(rig.transform, false);
            head.transform.localRotation = Quaternion.Euler(CabWorld3DRenderer.DriverHeadPitch, 0f, 0f);
            camera = head.GetComponent<Camera>();
            camera.nearClipPlane = 0.04f;
            CabCockpitFactory.Create(rig.transform);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(rig);
        }

        [Test]
        public void EveryCabActionHasATouchable3DControlWithAHitArea()
        {
            Cab3DControlAnchor[] anchors = rig.GetComponentsInChildren<Cab3DControlAnchor>(true);
            foreach (CabControlAction action in PhysicalActions)
            {
                Cab3DControlAnchor anchor = System.Array.Find(anchors, a => a.Action == action && a.IsTouchable && !a.IsDispatcherAcknowledgement);
                Assert.That(anchor, Is.Not.Null, action.ToString());
                BoxCollider hitArea = anchor.GetComponent<BoxCollider>();
                Assert.That(hitArea != null && hitArea.enabled, Is.True, action + " hit area");
                Assert.That(anchor.gameObject.layer, Is.EqualTo(CabCockpitFactory.ControlLayer));
            }
            Cab3DControlAnchor vigilance = System.Array.Find(anchors, a => a.IsDispatcherAcknowledgement);
            Assert.That(vigilance, Is.Not.Null);
            Assert.That(vigilance.name, Is.EqualTo("VigilanceButton3D"));
        }

        [Test]
        public void CockpitProvidesThePartsTheRendererAnimates()
        {
            Transform[] parts = rig.GetComponentsInChildren<Transform>(true);
            string[] required = { "SpeedScreen3D", "MessageScreen3D", "VigilanceButton3D", "KeychainCharm3D",
                "WiperArm3D_Left", "WiperArm3D_Right", "Headlights3D", "CabinLight3D", "Wipers3D", "Radio3D",
                "Doors3D", "WindowHeater3D" };
            foreach (string name in required)
                Assert.That(System.Array.Exists(parts, t => t.name == name), Is.True, name);
            Assert.That(System.Array.FindAll(parts, t => t.name == "GaugeNeedle").Length, Is.EqualTo(3));
            Assert.That(System.Array.FindAll(parts, t => t.name.StartsWith("CeilingCabinLamp3D")).Length, Is.EqualTo(2));
            TextMesh[] readouts = rig.GetComponentsInChildren<TextMesh>(true);
            Assert.That(System.Array.Exists(readouts, t => t.name == "SpeedScreen3D_Readout"), Is.True);
            Assert.That(System.Array.Exists(readouts, t => t.name == "MessageScreen3D_Readout"), Is.True);
        }

        [TestCase(16f / 9f)]
        [TestCase(16f / 10f)]
        [TestCase(4f / 3f)]
        public void EveryControlIsFullyOnScreenAtRestAndControlsDoNotOverlap(float aspect)
        {
            camera.aspect = aspect;
            camera.fieldOfView = CabLookAround.VerticalFieldOfView(aspect);
            List<(string name, Rect rect)> rects = new List<(string, Rect)>();
            foreach (Cab3DControlAnchor anchor in rig.GetComponentsInChildren<Cab3DControlAnchor>(true))
            {
                if (!anchor.IsTouchable) continue;
                Rect rect = ViewportRect(anchor.GetComponent<BoxCollider>());
                Assert.That(rect.xMin >= 0f && rect.xMax <= 1f && rect.yMin >= 0f && rect.yMax <= 1f, Is.True,
                    anchor.name + " is cut off at aspect " + aspect.ToString("0.00") + ": " + rect);
                rects.Add((anchor.name, rect));
            }
            for (int i = 0; i < rects.Count; i++)
                for (int j = i + 1; j < rects.Count; j++)
                {
                    Rect a = rects[i].rect;
                    Rect b = rects[j].rect;
                    float overlapX = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
                    float overlapY = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                    float overlapArea = Mathf.Max(0f, overlapX) * Mathf.Max(0f, overlapY);
                    float smaller = Mathf.Min(a.width * a.height, b.width * b.height);
                    Assert.That(overlapArea / smaller, Is.LessThan(0.1f), rects[i].name + " overlaps " + rects[j].name);
                }
        }

        [Test]
        public void TapOnAControlOnScreenFindsIt()
        {
            camera.aspect = 16f / 10f;
            camera.fieldOfView = CabLookAround.VerticalFieldOfView(camera.aspect);
            // Screen-space hit tests use the camera's pixel rect; derive a point from the viewport.
            Cab3DControlAnchor horn = System.Array.Find(rig.GetComponentsInChildren<Cab3DControlAnchor>(true),
                a => a.Action == CabControlAction.Horn);
            Vector3 viewport = camera.WorldToViewportPoint(horn.GetComponent<BoxCollider>().bounds.center);
            Vector2 screen = new Vector2(viewport.x * camera.pixelWidth, viewport.y * camera.pixelHeight);
            Assert.That(CabInterior3DRenderer.TryHitControlOnScreen(camera, screen, out CabControlAction action, out bool dispatcher), Is.True);
            Assert.That(action, Is.EqualTo(CabControlAction.Horn));
            Assert.That(dispatcher, Is.False);
        }

        [Test]
        public void WorldModeSettingCyclesThroughAllModes()
        {
            Assert.That(SettingsView.NextWorldMode(CabWorldMode.Immersive3D), Is.EqualTo(CabWorldMode.Hybrid3D));
            Assert.That(SettingsView.NextWorldMode(CabWorldMode.Hybrid3D), Is.EqualTo(CabWorldMode.Legacy2D));
            Assert.That(SettingsView.NextWorldMode(CabWorldMode.Legacy2D), Is.EqualTo(CabWorldMode.Immersive3D));
            Assert.That(new UserPreferences().cabWorldMode, Is.EqualTo(CabWorldMode.Immersive3D));
        }

        [Test]
        public void LookAroundStaysWithinTheCab()
        {
            CabLookAround look = camera.gameObject.AddComponent<CabLookAround>();
            look.ApplyDrag(new Vector2(-500f, -500f));
            Assert.That(look.Yaw, Is.EqualTo(CabLookAround.MaxYaw));
            Assert.That(look.Pitch, Is.EqualTo(CabLookAround.MaxPitch));
            look.ApplyDrag(new Vector2(1000f, 1000f));
            Assert.That(look.Yaw, Is.EqualTo(-CabLookAround.MaxYaw));
            Assert.That(look.Pitch, Is.EqualTo(-CabLookAround.MaxPitch));
        }

        private Rect ViewportRect(BoxCollider hitArea)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            Vector3 extents = hitArea.size * 0.5f;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 local = hitArea.center + new Vector3(
                    (corner & 1) == 0 ? -extents.x : extents.x,
                    (corner & 2) == 0 ? -extents.y : extents.y,
                    (corner & 4) == 0 ? -extents.z : extents.z);
                Vector3 viewport = camera.WorldToViewportPoint(hitArea.transform.TransformPoint(local));
                min = Vector2.Min(min, viewport);
                max = Vector2.Max(max, viewport);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
