using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SortingStation.Tests
{
    public sealed class RenderingSetupTests
    {
        private const string SettingsPath = "Assets/SortingStation/Resources/Configuration/CabWorld3DSettings.asset";

        [Test]
        public void ProjectRendersWithUrpInGraphicsAndEveryQualityLevel()
        {
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.InstanceOf<UniversalRenderPipelineAsset>());
            for (int i = 0; i < QualitySettings.names.Length; i++)
                Assert.That(QualitySettings.GetRenderPipelineAssetAt(i), Is.InstanceOf<UniversalRenderPipelineAsset>(),
                    "Quality level " + QualitySettings.names[i]);
        }

        [Test]
        public void EveryCabQualityProfileHasItsOwnUrpAsset()
        {
            CabWorld3DSettings settings = AssetDatabase.LoadAssetAtPath<CabWorld3DSettings>(SettingsPath);
            RenderPipelineAsset performance = settings.Pipeline(CabWorldQuality.Performance);
            RenderPipelineAsset balanced = settings.Pipeline(CabWorldQuality.Balanced);
            Assert.That(performance, Is.InstanceOf<UniversalRenderPipelineAsset>());
            Assert.That(balanced, Is.InstanceOf<UniversalRenderPipelineAsset>());
            Assert.That(performance, Is.Not.SameAs(balanced));
            Assert.That(((UniversalRenderPipelineAsset)performance).supportsMainLightShadows, Is.False,
                "The tablet profile renders without real-time shadows.");
        }

        [Test]
        public void ShadersCreatedFromCodeAreReferencedSoBuildsKeepThem()
        {
            CabWorld3DSettings settings = AssetDatabase.LoadAssetAtPath<CabWorld3DSettings>(SettingsPath);
            Assert.That(settings.LitShader, Is.Not.Null);
            Assert.That(settings.LitShader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(settings.UnlitTextureShader, Is.Not.Null);
            Assert.That(settings.UnlitTransparentShader, Is.Not.Null);
            Assert.That(settings.SpriteShader, Is.Not.Null);
            Assert.That(settings.AdditiveShader, Is.Not.Null);
            Assert.That(settings.RainWiperShader, Is.Not.Null);
            Assert.That(settings.MountainFeatherShader, Is.Not.Null);
        }

        [Test]
        public void ProjectMaterialsNoLongerUseTheBuiltInStandardShader()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/SortingStation" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(material.shader.name, Is.Not.EqualTo("Standard"), path);
            }
        }

        [Test]
        public void CodeCreatedLitMaterialKeepsColourAndSmoothness()
        {
            Material material = CabShaders.CreateLit("Test", new Color(0.2f, 0.4f, 0.6f), 0.33f);
            try
            {
                Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(material.color, Is.EqualTo(new Color(0.2f, 0.4f, 0.6f)));
                Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(0.33f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void SunAndMoonFollowOneClock()
        {
            Assert.That(EnvironmentClock.SunArc(0.47f), Is.GreaterThan(0.99f), "Noon");
            Assert.That(EnvironmentClock.SunArc(0.95f), Is.EqualTo(0f), "No sun at night");
            Assert.That(EnvironmentClock.SunArc(0.02f), Is.EqualTo(0f), "No sun before sunrise");
            Assert.That(EnvironmentClock.MoonTravel(0.86f), Is.EqualTo(0f).Within(0.001f), "Moon rises at sunset");
            Assert.That(EnvironmentClock.MoonTravel(0.08f), Is.EqualTo(1f).Within(0.001f), "Moon sets at sunrise");
            Assert.That(EnvironmentClock.Daylight(0.95f), Is.EqualTo(0f));
            Assert.That(EnvironmentClock.Daylight(0.47f), Is.EqualTo(1f));
        }

        [Test]
        public void Only3DWorldDrawsItsOwnSky()
        {
            GameObject flat = new GameObject("Flat", typeof(RectTransform));
            GameObject hybrid = new GameObject("Hybrid", typeof(RectTransform));
            try
            {
                Assert.That(flat.AddComponent<CabWorldRenderer>().DrawsOwnSky, Is.False);
                Assert.That(hybrid.AddComponent<CabWorld3DRenderer>().DrawsOwnSky, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(flat);
                Object.DestroyImmediate(hybrid);
            }
        }
    }
}
