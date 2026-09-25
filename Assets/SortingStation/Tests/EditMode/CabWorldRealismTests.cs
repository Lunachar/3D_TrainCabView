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

        private static Transform FindByPrefix(Transform parent, string prefix)
        {
            foreach (Transform child in parent)
                if (child.name.StartsWith(prefix)) return child;
            return null;
        }
    }
}
