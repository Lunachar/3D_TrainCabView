using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    public static class CabWorld3DPrototypeFactory
    {
        public const int CurrentRouteVersion = 39;
        private const float OppositeTrackOffset = 4.80f;
        private sealed class MeshBuilder
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> triangles = new List<int>();

            public void AddBox(Vector3 center, Quaternion rotation, Vector3 size)
            {
                int first = vertices.Count;
                Vector3 half = size * 0.5f;
                Vector3[] corners =
                {
                    new Vector3(-half.x, -half.y, -half.z), new Vector3(half.x, -half.y, -half.z),
                    new Vector3(half.x, half.y, -half.z), new Vector3(-half.x, half.y, -half.z),
                    new Vector3(-half.x, -half.y, half.z), new Vector3(half.x, -half.y, half.z),
                    new Vector3(half.x, half.y, half.z), new Vector3(-half.x, half.y, half.z)
                };
                for (int i = 0; i < corners.Length; i++)
                {
                    vertices.Add(center + rotation * corners[i]);
                    normals.Add(rotation * corners[i].normalized);
                    uv.Add(new Vector2((i & 1) == 0 ? 0f : 1f, (i & 2) == 0 ? 0f : 1f));
                }
                int[] indices =
                {
                    0,2,1, 0,3,2, 4,5,6, 4,6,7, 0,1,5, 0,5,4,
                    3,7,6, 3,6,2, 0,4,7, 0,7,3, 1,2,6, 1,6,5
                };
                for (int i = 0; i < indices.Length; i++) triangles.Add(first + indices[i]);
            }

            public void AddSurfaceQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvSize, bool flipWinding)
            {
                AddSurfaceQuad(a, b, c, d, Vector2.zero, new Vector2(0f, uvSize.y), uvSize,
                    new Vector2(uvSize.x, 0f), flipWinding);
            }

            public void AddSurfaceQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD, bool flipWinding)
            {
                int first = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                vertices.Add(d);
                for (int i = 0; i < 4; i++) normals.Add(Vector3.up);
                uv.Add(uvA);
                uv.Add(uvB);
                uv.Add(uvC);
                uv.Add(uvD);
                if (flipWinding)
                {
                    triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
                    triangles.Add(first); triangles.Add(first + 3); triangles.Add(first + 2);
                }
                else
                {
                    triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                    triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
                }
            }

            public Mesh Create(string name)
            {
                Mesh mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(triangles, 0, true);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        public static GameObject Create(Transform parent, float cycleLength, float density)
        {
            GameObject root = new GameObject("CabWorld3DPrototype");
            root.transform.SetParent(parent, false);
            Cab3DRouteAuthoring authoring = root.AddComponent<Cab3DRouteAuthoring>();
            authoring.MarkBuiltRouteVersion(CurrentRouteVersion);
            authoring.SetCycleLength(cycleLength);
            Material grass = CreateTiledMaterial("SoftGrass", new Color(0.22f, 0.43f, 0.14f),
                "Cab3D/MeadowGround_v1", new Vector2(10f, 130f), 0.04f);
            Material grassLight = Material("GrassHighlights", new Color(0.34f, 0.58f, 0.20f));
            Material earth = Material("EarthPatches", new Color(0.31f, 0.24f, 0.13f));
            // The source ballast texture contains its own dark stone occlusion. A low base tint
            // multiplies that detail almost to black, so keep a warm neutral tint that preserves
            // the aggregate contrast when viewed from the cab.
            Material ballast = Material("Ballast", new Color(0.68f, 0.64f, 0.56f));
            Texture2D ballastTexture = Resources.Load<Texture2D>("Cab3D/RailwayBallast_v1");
            if (ballastTexture != null)
            {
                ballast.mainTexture = ballastTexture;
                ballast.mainTextureScale = new Vector2(0.42f, 0.42f);
            }
            Material steel = Material("RailSteel", new Color(0.19f, 0.22f, 0.23f));
            // A bright, broken rail head read as white "zebra" stripes from the cab camera.
            // Keep a restrained blue-grey highlight instead; the continuous dark web below
            // still gives the rail a believable metal profile.
            Material railHead = Material("RailHead", new Color(0.31f, 0.34f, 0.35f));
            CabShaders.SetSmoothness(railHead, 0.28f);
            Material wood = Material("SleeperWood", new Color(0.24f, 0.13f, 0.065f));
            Material concreteSleeper = Material("SleeperConcrete", new Color(0.40f, 0.39f, 0.35f));
            Material leaves = Material("Leaves", new Color(0.18f, 0.44f, 0.14f));
            Material trunk = Material("Trunk", new Color(0.26f, 0.15f, 0.08f));
            Material concrete = Material("Station", new Color(0.55f, 0.57f, 0.56f));

            // The camera begins a few metres before the formal start of the route.  Extend the terrain
            // behind zero so the player never sees a hard map edge while the train first starts.
            Renderer ground = Primitive(root.transform, PrimitiveType.Cube, "Ground", new Vector3(0f, -0.42f, cycleLength * 0.5f - 58f),
                new Vector3(74f, 0.8f, cycleLength + 116f), grass).GetComponent<Renderer>();
            BuildLandscapeRegions(root.transform, cycleLength);
            BuildGroundDetail(root.transform, cycleLength, grassLight, earth, density);
            Transform track = new GameObject("GeneratedTrackCurve").transform;
            track.SetParent(root.transform, false);
            MeshBuilder bedMesh = new MeshBuilder();
            MeshBuilder railMesh = new MeshBuilder();
            MeshBuilder railHeadMesh = new MeshBuilder();
            MeshBuilder woodSleeperMesh = new MeshBuilder();
            MeshBuilder weatheredSleeperMesh = new MeshBuilder();
            MeshBuilder concreteSleeperMesh = new MeshBuilder();
            MeshBuilder railFastenerMesh = new MeshBuilder();
            MeshBuilder ballastShoulderMesh = new MeshBuilder();
            // A 0.62 m sleeper pitch and 1.52 m rail gauge make the rails read as a real
            // broad-gauge railway instead of widely spaced planks from the cab camera.
            const float trackStep = 0.62f;
            const float ballastWidth = 3.45f;
            const float railGaugeHalfWidth = 0.76f;
            int sleeperIndex = 0;
            for (float d = -64f; d < 0f; d += trackStep)
            {
                Vector3 p = new Vector3(0f, 0f, d);
                AddTrackSegment(bedMesh, ballastShoulderMesh, railMesh, railHeadMesh, woodSleeperMesh, weatheredSleeperMesh, concreteSleeperMesh, railFastenerMesh, p,
                    Quaternion.identity, 0f, sleeperIndex++, ballastWidth, railGaugeHalfWidth, trackStep);
                AddTrackSegment(bedMesh, ballastShoulderMesh, railMesh, railHeadMesh, woodSleeperMesh, weatheredSleeperMesh, concreteSleeperMesh, railFastenerMesh, p,
                    Quaternion.identity, OppositeTrackOffset, sleeperIndex++, ballastWidth, railGaugeHalfWidth, trackStep);
            }
            int stepCount = Mathf.CeilToInt(cycleLength / trackStep);
            for (int i = 0; i <= stepCount; i++)
            {
                float d = Mathf.Min(cycleLength, i * trackStep);
                Vector3 p = Cab3DTrackMath.Point(d, cycleLength);
                Quaternion q = Cab3DTrackMath.Heading(d, cycleLength);
                AddTrackSegment(bedMesh, ballastShoulderMesh, railMesh, railHeadMesh, woodSleeperMesh, weatheredSleeperMesh, concreteSleeperMesh, railFastenerMesh, p,
                    q, 0f, sleeperIndex++, ballastWidth, railGaugeHalfWidth, trackStep);
                AddTrackSegment(bedMesh, ballastShoulderMesh, railMesh, railHeadMesh, woodSleeperMesh, weatheredSleeperMesh, concreteSleeperMesh, railFastenerMesh, p,
                    q, OppositeTrackOffset, sleeperIndex++, ballastWidth, railGaugeHalfWidth, trackStep);
            }
            CreateMeshObject(track, "BallastShouldersTwoTrack", ballastShoulderMesh.Create("BallastShouldersTwoTrackMesh"), earth);
            CreateMeshObject(track, "BallastTwoTrack", bedMesh.Create("BallastTwoTrackMesh"), ballast);
            CreateMeshObject(track, "RailWebsTwoTrack", railMesh.Create("RailWebsTwoTrackMesh"), steel);
            CreateMeshObject(track, "RailHeadsTwoTrack", railHeadMesh.Create("RailHeadsTwoTrackMesh"), railHead);
            CreateMeshObject(track, "WoodSleepers", woodSleeperMesh.Create("WoodSleepersMesh"), wood);
            CreateMeshObject(track, "WeatheredWoodSleepers", weatheredSleeperMesh.Create("WeatheredWoodSleepersMesh"), Material("SleeperWeathered", new Color(0.31f, 0.20f, 0.10f)));
            CreateMeshObject(track, "ConcreteSleepers", concreteSleeperMesh.Create("ConcreteSleepersMesh"), concreteSleeper);
            CreateMeshObject(track, "RailFasteners", railFastenerMesh.Create("RailFastenersMesh"), steel);

            int treeCount = Mathf.RoundToInt(42f * Mathf.Clamp(density, 0.3f, 1.2f));
            for (int i = 0; i < treeCount; i++)
            {
                float d = 55f + Mathf.Repeat(i * 17.3f, cycleLength - 90f);
                float side = i % 2 == 0 ? -1f : 1f;
                float lateral = side * (7f + (i % 5) * 2.4f);
                Vector3 p = Cab3DTrackMath.Point(d, cycleLength) + Cab3DTrackMath.Heading(d, cycleLength) * new Vector3(lateral, 0f, 0f);
                GameObject tree = new GameObject("Tree_" + i);
                tree.transform.SetParent(root.transform, false);
                tree.transform.localPosition = p;
                Renderer trunkRenderer = Primitive(tree.transform, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 1.5f, 0f), new Vector3(0.45f, 1.5f, 0.45f), trunk).GetComponent<Renderer>();
                Renderer crown = Primitive(tree.transform, i % 3 == 0 ? PrimitiveType.Capsule : PrimitiveType.Sphere, "Crown", new Vector3(0f, 4.1f, 0f), new Vector3(3.2f, 3.6f, 3.2f), leaves).GetComponent<Renderer>();
                trunkRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                crown.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            BuildStartScenery(root.transform, leaves, trunk);
            BuildBillboardFoliage(root.transform, cycleLength, density);

            BuildCrossing(root.transform, cycleLength, concrete);
            BuildTracksideInfrastructure(root.transform, cycleLength, steel, concrete);
            BuildCatenary(root.transform, cycleLength, steel, concrete);
            BuildParallelRoads(root.transform, cycleLength, concrete, steel);
            BuildCityViaduct(root.transform, cycleLength, concrete, steel);
            BuildRoadOverpass(root.transform, cycleLength, concrete, steel);
            BuildStation(root.transform, cycleLength, CabRouteLayout.FirstStation01, "Станция Удельная", concrete, 0);
            BuildStation(root.transform, cycleLength, CabRouteLayout.SecondStation01, "Станция Приозерская", concrete, 1);
            BuildLivingScenery(root.transform, cycleLength, concrete, steel, wood);
            BuildTunnel(root.transform, cycleLength, concrete);
            BuildDistantScenery(root.transform, cycleLength, concrete, leaves);
            BuildPhotorealBackdrops(root.transform, cycleLength);
            return root;
        }

        private static void AddTrackSegment(MeshBuilder bedMesh, MeshBuilder ballastShoulders, MeshBuilder railMesh, MeshBuilder railHeadMesh,
            MeshBuilder woodSleepers, MeshBuilder weatheredSleepers, MeshBuilder concreteSleepers, MeshBuilder fasteners, Vector3 pathPoint, Quaternion heading,
            float lateralOffset, int sleeperIndex, float ballastWidth, float railGaugeHalfWidth, float segmentLength)
        {
            Vector3 right = heading * Vector3.right;
            Vector3 center = pathPoint + right * lateralOffset;
            // The wider, slightly lower shoulder makes the ballast settle into the ground.
            ballastShoulders.AddBox(center + Vector3.down * 0.085f, heading, new Vector3(ballastWidth + 0.28f, 0.10f, segmentLength + 0.10f));
            bedMesh.AddBox(center + Vector3.down * 0.025f, heading, new Vector3(ballastWidth, 0.18f, segmentLength + 0.04f));
            // The sleepers are intentionally short individual pieces, but rails must not be.
            // At 0.62 m each separate reflective rail cube produced the white/black "zebra"
            // visible from the cab. One eight-sleeper run has only an imperceptible join every
            // five metres, and still follows the route curve closely enough for this scale.
            const int railRunSleeperCount = 8;
            bool startsRailRun = sleeperIndex % (railRunSleeperCount * 2) < 2;
            if (startsRailRun)
            {
                float railRunLength = Mathf.Max(0.1f, segmentLength * railRunSleeperCount - 0.024f);
                Vector3 railRunCenter = center + heading * Vector3.forward * ((railRunLength - segmentLength) * 0.5f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 railCenter = railRunCenter + right * (side * railGaugeHalfWidth);
                    railMesh.AddBox(railCenter + Vector3.up * 0.155f, heading, new Vector3(0.070f, 0.19f, railRunLength));
                    railHeadMesh.AddBox(railCenter + Vector3.up * 0.268f, heading, new Vector3(0.135f, 0.050f, railRunLength));
                }
            }
            MeshBuilder sleepers = sleeperIndex % 17 == 0
                ? concreteSleepers
                : sleeperIndex % 6 == 0 ? weatheredSleepers : woodSleepers;
            sleepers.AddBox(center + Vector3.up * 0.040f, heading, new Vector3(2.62f, 0.16f, 0.245f));

            // Small clips, placed every other sleeper, are enough to break the toy-track silhouette
            // without making Android draw thousands of separate objects.
            if (sleeperIndex % 2 == 0)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 railCenter = center + right * (side * railGaugeHalfWidth);
                    fasteners.AddBox(railCenter + Vector3.up * 0.135f, heading, new Vector3(0.22f, 0.05f, 0.065f));
                }
            }
        }

        private static GameObject CreateMeshObject(Transform parent, string name, Mesh mesh, Material material)
        {
            GameObject go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static void BuildLandscapeRegions(Transform root, float cycle)
        {
            // Broad material regions follow the track curve so the view changes naturally as
            // the journey moves from open meadows into forest, farm country, a mountain bore,
            // town and lakeside terrain. The plain Ground remains beneath them as a safety bed.
            BuildLandscapeRegion(root, cycle, "Meadow", 0.00f, 0.11f, new Color(0.87f, 0.96f, 0.72f));
            BuildLandscapeRegion(root, cycle, "Forest", 0.11f, 0.27f, new Color(0.59f, 0.76f, 0.54f));
            BuildLandscapeRegion(root, cycle, "Field", 0.27f, 0.43f, new Color(0.88f, 0.82f, 0.57f));
            BuildLandscapeRegion(root, cycle, "Village", 0.43f, 0.54f, new Color(0.81f, 0.82f, 0.67f));
            BuildLandscapeRegion(root, cycle, "Mountain", 0.54f, 0.71f, new Color(0.65f, 0.69f, 0.62f));
            BuildLandscapeRegion(root, cycle, "Town", 0.71f, 0.86f, new Color(0.81f, 0.78f, 0.64f));
            BuildLandscapeRegion(root, cycle, "Water", 0.86f, 1.00f, new Color(0.67f, 0.87f, 0.78f));
        }

        private static void BuildLandscapeRegion(Transform root, float cycle, string regionName,
            float start01, float end01, Color tint)
        {
            const float sectionLength = 14f;
            const float halfWidth = 40f;
            float startDistance = cycle * start01;
            float length = cycle * (end01 - start01);
            int sectionCount = Mathf.Max(1, Mathf.CeilToInt(length / sectionLength));
            MeshBuilder mesh = new MeshBuilder();
            for (int i = 0; i < sectionCount; i++)
            {
                float d0 = startDistance + length * i / sectionCount;
                float d1 = startDistance + length * (i + 1) / sectionCount;
                Vector3 leftStart = LandscapePoint(d0, cycle) +
                                    Cab3DTrackMath.Heading(d0, cycle) * new Vector3(-halfWidth, -0.012f, 0f);
                Vector3 leftEnd = LandscapePoint(d1, cycle) +
                                  Cab3DTrackMath.Heading(d1, cycle) * new Vector3(-halfWidth, -0.012f, 0f);
                Vector3 rightEnd = LandscapePoint(d1, cycle) +
                                   Cab3DTrackMath.Heading(d1, cycle) * new Vector3(halfWidth, -0.012f, 0f);
                Vector3 rightStart = LandscapePoint(d0, cycle) +
                                     Cab3DTrackMath.Heading(d0, cycle) * new Vector3(halfWidth, -0.012f, 0f);
                Vector2 uvLeftStart = new Vector2(0f, d0 / 9f);
                Vector2 uvLeftEnd = new Vector2(0f, d1 / 9f);
                Vector2 uvRightEnd = new Vector2(halfWidth / 4.5f, d1 / 9f);
                Vector2 uvRightStart = new Vector2(halfWidth / 4.5f, d0 / 9f);
                mesh.AddSurfaceQuad(leftStart, leftEnd, rightEnd, rightStart,
                    uvLeftStart, uvLeftEnd, uvRightEnd, uvRightStart, false);
            }

            Material material = CreateTiledMaterial("GroundRegionMaterial_" + regionName, tint,
                "Cab3D/MeadowGround_v1", Vector2.one, 0.04f);
            GameObject region = CreateMeshObject(root, "GroundRegion_" + regionName,
                mesh.Create("GroundRegion_" + regionName + "Mesh"), material);
            Renderer renderer = region.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        private static Vector3 LandscapePoint(float distance, float cycle)
        {
            if (distance < cycle - 0.001f) return Cab3DTrackMath.Point(distance, cycle);
            Vector3 end = Cab3DTrackMath.Point(cycle - 0.001f, cycle);
            end.z = cycle;
            return end;
        }

        private static void BuildGroundDetail(Transform root, float cycle, Material grassLight, Material earth, float density)
        {
            int patchCount = Mathf.RoundToInt(70f * Mathf.Clamp(density, 0.4f, 1.2f));
            for (int i = 0; i < patchCount; i++)
            {
                float d = 18f + Mathf.Repeat(i * 31.7f, cycle - 36f);
                Quaternion heading = Cab3DTrackMath.Heading(d, cycle);
                float side = i % 2 == 0 ? -1f : 1f;
                float x = side * (4.5f + (i % 7) * 3.2f);
                Vector3 p = Cab3DTrackMath.Point(d, cycle) + heading * new Vector3(x, -0.005f, (i % 5 - 2) * 1.3f);
                GameObject patch = Primitive(root, PrimitiveType.Quad, i % 4 == 0 ? "EarthPatch" : "GrassPatch", p,
                    new Vector3(1.8f + (i % 4) * 0.85f, 1.4f + (i % 3) * 0.65f, 1f), i % 4 == 0 ? earth : grassLight);
                patch.transform.rotation = Quaternion.Euler(90f, heading.eulerAngles.y + (i % 3 - 1) * 18f, 0f);
                patch.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private static void BuildStartScenery(Transform root, Material leaves, Material trunk)
        {
            // The camera starts at the beginning of the route, so the first 40 m must already
            // look inhabited.  Previously the real trees began at 55 m, leaving a bare field
            // exactly when the child first takes control.
            Texture2D foliageAtlas = Resources.Load<Texture2D>("Cab3D/FoliageBillboardAtlas_v1");
            Material birchBillboard = foliageAtlas != null
                ? BillboardMaterial("StartBirchBillboard", foliageAtlas, new Vector2(0f, 0.5f)) : null;
            Material mapleBillboard = foliageAtlas != null
                ? BillboardMaterial("StartMapleBillboard", foliageAtlas, new Vector2(0.5f, 0.5f)) : null;
            Material shrubBillboard = foliageAtlas != null
                ? BillboardMaterial("StartShrubBillboard", foliageAtlas, new Vector2(0.5f, 0f)) : null;
            for (int i = 0; i < 8; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float distance = 14f + i * 4.8f;
                GameObject tree = new GameObject("StartTree_" + i);
                tree.transform.SetParent(root, false);
                tree.transform.localPosition = new Vector3(side * (8.8f + (i % 3) * 2.7f), 0f, distance);
                Primitive(tree.transform, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 2.1f, 0f),
                    new Vector3(0.46f, 2.1f, 0.46f), trunk);
                if (birchBillboard != null && mapleBillboard != null)
                {
                    Material canopy = i % 3 == 0 ? birchBillboard : mapleBillboard;
                    GameObject front = Primitive(tree.transform, PrimitiveType.Quad, "FoliageBillboard_StartTreeA",
                        new Vector3(0f, 5.1f, 0f), new Vector3(5.5f, 9.2f, 1f), canopy);
                    front.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    GameObject cross = Primitive(tree.transform, PrimitiveType.Quad, "FoliageBillboard_StartTreeB",
                        new Vector3(0f, 5.1f, 0f), new Vector3(5.5f, 9.2f, 1f), canopy);
                    cross.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    front.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    cross.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                else
                {
                    GameObject crown = Primitive(tree.transform, i % 3 == 0 ? PrimitiveType.Capsule : PrimitiveType.Sphere,
                        "Crown", new Vector3(0f, 5.1f, 0f), new Vector3(3.6f, 4.2f, 3.6f), leaves);
                    crown.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }
            for (int i = 0; i < 10; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Vector3 p = new Vector3(side * (6.5f + i % 3 * 2.1f), 0.55f, 8f + i * 7.5f);
                GameObject bush = shrubBillboard != null
                    ? Primitive(root, PrimitiveType.Quad, "FoliageBillboard_StartShrub", p,
                        new Vector3(2.0f + i % 2 * 0.4f, 1.8f, 1f), shrubBillboard)
                    : Primitive(root, PrimitiveType.Sphere, "StartBush", p,
                        new Vector3(1.3f + i % 2 * 0.55f, 1.1f, 1.15f), leaves);
                bush.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                bush.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (i % 3 == 0)
                {
                    GameObject post = Primitive(root, PrimitiveType.Cylinder, "RailwayPost", p + new Vector3(side * 1.6f, 0.9f, 0.3f),
                        new Vector3(0.10f, 0.9f, 0.10f), trunk);
                    post.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }
        }

        private static void BuildBillboardFoliage(Transform root, float cycle, float density)
        {
            Texture2D atlas = Resources.Load<Texture2D>("Cab3D/FoliageBillboardAtlas_v1");
            if (atlas == null) return;
            Material[] variants =
            {
                BillboardMaterial("BirchBillboard", atlas, new Vector2(0f, 0.5f)),
                BillboardMaterial("MapleBillboard", atlas, new Vector2(0.5f, 0.5f)),
                BillboardMaterial("BerryBushBillboard", atlas, new Vector2(0f, 0f)),
                BillboardMaterial("FlowerBushBillboard", atlas, new Vector2(0.5f, 0f))
            };
            int count = Mathf.RoundToInt(68f * Mathf.Clamp(density, 0.35f, 1.1f));
            for (int i = 0; i < count; i++)
            {
                // Start close to the cab, so the first rendered frame already contains the
                // same layered vegetation as the rest of the route.
                float d = 9f + Mathf.Repeat(i * 19.7f, cycle - 18f);
                Quaternion heading = Cab3DTrackMath.Heading(d, cycle);
                float side = i % 2 == 0 ? -1f : 1f;
                int variant = i % variants.Length;
                float lateral = side * (5.8f + (i % 6) * 2.7f);
                Vector3 p = Cab3DTrackMath.Point(d, cycle) + heading * new Vector3(lateral, variant < 2 ? 3.6f : 1.45f, 0f);
                float height = variant < 2 ? 8.0f + (i % 3) * 1.3f : 3.0f + (i % 2) * 0.7f;
                float width = variant < 2 ? height * 0.70f : height * 1.18f;
                GameObject billboard = Primitive(root, PrimitiveType.Quad, "FoliageBillboard", p,
                    new Vector3(width, height, 1f), variants[variant]);
                billboard.transform.rotation = heading * Quaternion.Euler(0f, 180f, 0f);
                Renderer renderer = billboard.GetComponent<Renderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static Material BillboardMaterial(string name, Texture2D atlas, Vector2 offset)
        {
            Shader shader = CabShaders.Sprite;
            Material material = new Material(shader) { name = name, mainTexture = atlas };
            material.mainTextureScale = new Vector2(0.5f, 0.5f);
            material.mainTextureOffset = offset;
            return material;
        }

        private static void BuildStationSign(Transform root, Vector3 position, Quaternion heading, string stationName)
        {
            GameObject pole = Primitive(root, PrimitiveType.Cylinder, "StationSignPole", position + Vector3.down * 1.05f,
                new Vector3(0.13f, 1.1f, 0.13f), Material("SignPole", new Color(0.18f, 0.22f, 0.24f)));
            pole.transform.rotation = heading;
            // A TextMesh draws from both sides, so the two copies (one per direction) sit on either
            // face of a board; without it the back copy showed through mirrored over the name.
            GameObject board = Primitive(root, PrimitiveType.Cube, "StationSignBoard", position,
                new Vector3(4f, 1.1f, 0.08f), Material("StationSignBoard", new Color(0.05f, 0.22f, 0.48f)));
            board.transform.rotation = heading;
            Vector3 forward = heading * Vector3.forward;
            TextMesh front = CreateStationSignText(root, "StationName_" + stationName, position - forward * 0.06f, heading, stationName);
            CreateStationSignText(root, "StationName_Back_" + stationName, position + forward * 0.06f,
                heading * Quaternion.Euler(0f, 180f, 0f), stationName);
            FitSignBoard(board.transform, front);
        }

        /// <summary>Sizes a station sign board to its name with a margin around the letters.</summary>
        public static void FitSignBoard(Transform board, TextMesh text)
        {
            MeshRenderer renderer = text != null ? text.GetComponent<MeshRenderer>() : null;
            if (board == null || renderer == null) return;
            Vector3 size = Vector3.Scale(renderer.localBounds.size, text.transform.lossyScale);
            board.localScale = new Vector3(Mathf.Max(1.5f, size.x + 0.7f), Mathf.Max(0.9f, size.y + 0.35f), 0.08f);
        }

        internal static TextMesh CreateStationSignText(Transform root, string name, Vector3 position, Quaternion rotation, string stationName)
        {
            GameObject label = new GameObject(name, typeof(TextMesh));
            label.transform.SetParent(root, false);
            label.transform.localPosition = position;
            label.transform.rotation = rotation;
            TextMesh text = label.GetComponent<TextMesh>();
            text.text = stationName;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.32f;
            text.fontSize = 42;
            text.color = new Color(0.96f, 0.94f, 0.78f);
            return text;
        }

        private static void BuildDistantScenery(Transform root, float cycle, Material building, Material leaves)
        {
            Material roof = Material("VillageRoof", new Color(0.48f, 0.16f, 0.10f));
            Material villageWalls = CreateTiledMaterial("VillageHouseFacade", new Color(0.92f, 0.88f, 0.80f),
                "Cab3D/CityFacade_v1", new Vector2(1.5f, 2f), 0.12f);
            for (int i = 0; i < 16; i++)
            {
                float d = 90f + i * 59f;
                Quaternion q = Cab3DTrackMath.Heading(d, cycle);
                float side = i % 2 == 0 ? -1f : 1f;
                Vector3 p = Cab3DTrackMath.Point(d, cycle) + q * new Vector3(side * (13f + i % 4 * 2.2f), 1.1f, (i % 5 - 2) * 2.5f);
                GameObject house = Primitive(root, PrimitiveType.Cube, "VillageHouse", p, new Vector3(3.4f, 2.2f, 4.2f), villageWalls);
                house.transform.rotation = q;
                GameObject roofObject = Primitive(root, PrimitiveType.Cube, "VillageRoof", p + Vector3.up * 1.45f, new Vector3(3.9f, 0.35f, 4.7f), roof);
                roofObject.transform.rotation = q * Quaternion.Euler(0f, 0f, 14f);
                if (i % 3 == 0)
                {
                    GameObject bush = Primitive(root, PrimitiveType.Sphere, "Bush", p + q * new Vector3(side * 2.8f, 0.55f, 1.6f), new Vector3(1.8f, 1.1f, 1.5f), leaves);
                    bush.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }
        }

        private static void BuildPhotorealBackdrops(Transform root, float cycle)
        {
            // These are original generated vistas, mounted far from the track so they enrich the
            // horizon without pretending to be a close 3D object.  They can be replaced in the
            // editable prototype with hand-made scenery later.
            CreatePhotoBackdrop(root, cycle, cycle * CabRouteLayout.FirstStation01, -35f, 12f, "Environment/Station_Lenoblast_v1", "LenoblastStationVista");
            CreatePhotoBackdrop(root, cycle, cycle * 0.82f, -42f, 15f, "Environment/Station_MuseumFantasy_v1", "MuseumStationVista");
            CreatePhotoBackdrop(root, cycle, cycle * 0.69f, 42f, 12f, "Environment/Tunnel_TwoTrack_v1", "TunnelExitVista");
            CreatePhotoBackdrop(root, cycle, cycle * 0.36f, 42f, 12f, "Environment/Crossing_RoadTraffic_v1", "CrossingRoadVista");
            CreatePhotoBackdrop(root, cycle, cycle * 0.79f, 48f, 17f, "Environment/City_Viaduct_v1", "CityViaductVista");
            CreatePhotoBackdrop(root, cycle, cycle * 0.055f, -39f, 12f, "Environment/Road_ViaductTraffic_v1", "RoadTrafficVista");
            CreatePhotoBackdrop(root, cycle, cycle * 0.49f, -42f, 15f, "Environment/Station_NorthernRain_v1", "NorthernRainStationVista");
            CreatePhotoBackdrop(root, cycle, cycle * 0.565f, -40f, 14f, "Environment/Station_LenoblastRainy_v2", "LenoblastRainyStationVista");
        }

        private static void CreatePhotoBackdrop(Transform root, float cycle, float distance, float lateral, float height, string resourcePath, string name)
        {
            Texture2D texture = Resources.Load<Texture2D>("Cab3D/" + resourcePath);
            if (texture == null) return;
            Quaternion heading = Cab3DTrackMath.Heading(distance, cycle);
            Vector3 lateralOffset = heading * new Vector3(lateral, 0f, 0f);
            Vector3 point = Cab3DTrackMath.Point(distance, cycle) + lateralOffset + Vector3.up * height;
            Shader shader = CabShaders.UnlitTexture;
            Material material = new Material(shader) { name = name + "_Material", mainTexture = texture };
            GameObject view = Primitive(root, PrimitiveType.Quad, name, point, new Vector3(34f, 19.125f, 1f), material);
            view.transform.rotation = Quaternion.LookRotation(-lateralOffset.normalized, Vector3.up);
            Renderer renderer = view.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void BuildCrossing(Transform root, float cycle, Material material)
        {
            float d = cycle * 0.36f;
            Vector3 p = Cab3DTrackMath.Point(d, cycle);
            Quaternion q = Cab3DTrackMath.Heading(d, cycle);
            Material asphalt = CreateAsphaltMaterial("CrossingRoad_Asphalt", new Vector2(7f, 1.5f));
            GameObject crossingFrame = new GameObject("LevelCrossingFrame");
            crossingFrame.transform.SetParent(root, false);
            crossingFrame.transform.localPosition = p;
            crossingFrame.transform.localRotation = q;
            Primitive(crossingFrame.transform, PrimitiveType.Cube, "CrossingRoad", Vector3.up * 0.04f,
                new Vector3(28f, 0.12f, 5f), asphalt);
            Material barrierWhite = Material("BarrierWhite", new Color(0.92f, 0.90f, 0.82f));
            Material barrierRed = Material("BarrierRed", new Color(0.82f, 0.055f, 0.035f));
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject barrier = new GameObject("CrossingBarrier");
                barrier.transform.SetParent(crossingFrame.transform, false);
                barrier.transform.localPosition = new Vector3(side * 4.2f, 1.02f, 0f);
                Primitive(barrier.transform, PrimitiveType.Cube, "BarrierArm", Vector3.zero,
                    new Vector3(0.14f, 0.16f, 5.2f), barrierWhite);
                for (int band = 0; band < 5; band++)
                    Primitive(barrier.transform, PrimitiveType.Cube, "BarrierRedBand",
                        new Vector3(0f, 0.006f, -2.08f + band * 1.04f), new Vector3(0.145f, 0.166f, 0.52f), barrierRed);
            }
            GameObject traffic = new GameObject("CrossingTraffic");
            traffic.transform.SetParent(crossingFrame.transform, false);
            GameObject car = CreateRoadVehicle(traffic.transform, "CrossingCar", 0f, -9f, 9f, 5.5f, false,
                Material("CarBlue", new Color(0.08f, 0.34f, 0.72f)), Material("CarTrim", new Color(0.20f, 0.22f, 0.23f)), false);
            car.transform.localPosition = new Vector3(-9f, 0.48f, -0.88f);
            car.transform.localRotation = Quaternion.Euler(0f, 270f, 0f);
            car.GetComponent<Cab3DRoadVehicleAnimator>().ConfigureAcross(-9f, 9f, 5.5f, false);
            GameObject truck = CreateRoadVehicle(traffic.transform, "CrossingTruck", 0f, -9f, 9f, 4.2f, true,
                Material("CrossingTruckWhite", new Color(0.76f, 0.76f, 0.70f)), Material("CrossingTruckTrim", new Color(0.20f, 0.22f, 0.23f)), true);
            truck.transform.localPosition = new Vector3(9f, 0.48f, 0.88f);
            truck.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            truck.GetComponent<Cab3DRoadVehicleAnimator>().ConfigureAcross(-9f, 9f, 4.2f, true);
            for (int approachSide = -1; approachSide <= 1; approachSide += 2)
            {
                Quaternion faceIncomingTraffic = Quaternion.Euler(0f, approachSide < 0 ? 90f : -90f, 0f);
                for (int shoulderSide = -1; shoulderSide <= 1; shoulderSide += 2)
                {
                    Vector3 signalPosition = new Vector3(approachSide * 4.1f, 0f, shoulderSide * 3.1f);
                    CreateCrossingWarning(crossingFrame.transform, signalPosition, faceIncomingTraffic, approachSide);
                }
            }
        }

        private static void CreateCrossingWarning(Transform root, Vector3 position, Quaternion heading, int side)
        {
            Material mast = Material("CrossingMast", new Color(0.15f, 0.16f, 0.16f));
            Material housing = Material("CrossingSignalHousing", new Color(0.055f, 0.06f, 0.062f));
            Material red = Material("CrossingLampRed", new Color(0.80f, 0.035f, 0.02f));
            GameObject warning = new GameObject("CrossingWarning");
            warning.transform.SetParent(root, false);
            warning.transform.localPosition = position;
            warning.transform.localRotation = heading;
            Primitive(warning.transform, PrimitiveType.Cylinder, "Mast", new Vector3(0f, 1.35f, 0f), new Vector3(0.11f, 1.35f, 0.11f), mast);
            Primitive(warning.transform, PrimitiveType.Cube, "BlackWarningPlate", new Vector3(0f, 2.65f, -0.08f), new Vector3(1.34f, 0.70f, 0.26f), housing);
            for (int lampIndex = -1; lampIndex <= 1; lampIndex += 2)
                Primitive(warning.transform, PrimitiveType.Sphere, "WarningLens", new Vector3(lampIndex * 0.37f, 2.65f, -0.23f), Vector3.one * 0.26f, red);
            GameObject cross = Primitive(warning.transform, PrimitiveType.Cube, "CrossbuckArmA", new Vector3(0f, 3.45f, -0.10f), new Vector3(1.35f, 0.10f, 0.07f),
                Material("CrossbuckWhite", new Color(0.95f, 0.92f, 0.83f)));
            cross.transform.localRotation = Quaternion.Euler(0f, 0f, 35f * side);
            GameObject crossB = Primitive(warning.transform, PrimitiveType.Cube, "CrossbuckArmB", new Vector3(0f, 3.45f, -0.11f), new Vector3(1.35f, 0.10f, 0.07f),
                Material("CrossbuckWhiteB", new Color(0.95f, 0.92f, 0.83f)));
            crossB.transform.localRotation = Quaternion.Euler(0f, 0f, -35f * side);
            GameObject lightEmitter = new GameObject("CrossingWarningLight");
            lightEmitter.transform.SetParent(warning.transform, false);
            lightEmitter.transform.localPosition = new Vector3(0f, 2.65f, -0.28f);
            Light warningLight = lightEmitter.AddComponent<Light>();
            warningLight.type = LightType.Point;
            warningLight.color = new Color(1f, 0.06f, 0.02f);
            warningLight.range = 7f;
            warningLight.intensity = 2.1f;
            warningLight.shadows = LightShadows.None;
            warningLight.enabled = false;
        }

        private static void BuildTracksideInfrastructure(Transform root, float cycle, Material steel, Material concrete)
        {
            // Physical speed signs and signals replace the old floating icon-like route markers.
            float[] signalDistances = { cycle * 0.11f, cycle * 0.34f, cycle * 0.61f, cycle * 0.76f, cycle * 0.93f };
            for (int i = 0; i < signalDistances.Length; i++)
            {
                float d = signalDistances[i];
                Quaternion q = Cab3DTrackMath.Heading(d, cycle);
                Vector3 p = Cab3DTrackMath.Point(d, cycle) + q * new Vector3(-4.0f, 0f, 0f);
                CreateRailSignal(root, p, q, i * 0.91f);

                if (i % 2 != 0) CreateSpeedSign(root, p + q * new Vector3(-1.7f, 0f, 3.0f), q, i == 1 ? "40" : "60", steel, concrete);
            }
        }

        private static void BuildCatenary(Transform root, float cycle, Material steel, Material concrete)
        {
            // A sparse sequence of true 3D portal masts gives both tracks a clear railway scale
            // from the cab without the draw cost of a wire mesh along every sleeper.
            const float mastInterval = 70f;
            const float span = mastInterval;
            const float mastHeight = 8.0f;
            const float outerLeft = -3.25f;
            const float outerRight = OppositeTrackOffset + 3.25f;
            const float centre = (outerLeft + outerRight) * 0.5f;
            Material insulator = Material("CatenaryInsulator", new Color(0.57f, 0.69f, 0.73f));
            for (float d = 36f; d < cycle; d += mastInterval)
            {
                // The double-track tunnel has its own lamps and lining; portal masts must stop
                // before the mountain instead of visually passing through the roof.
                if (d > cycle * 0.54f && d < cycle * 0.715f) continue;
                Vector3 point = Cab3DTrackMath.Point(d, cycle);
                Quaternion heading = Cab3DTrackMath.Heading(d, cycle);
                GameObject portal = new GameObject("CatenaryPortal");
                portal.transform.SetParent(root, false);
                portal.transform.localPosition = point;
                portal.transform.localRotation = heading;
                Primitive(portal.transform, PrimitiveType.Cylinder, "CatenaryMastLeft", new Vector3(outerLeft, mastHeight * 0.5f, 0f),
                    new Vector3(0.15f, mastHeight * 0.5f, 0.15f), concrete);
                Primitive(portal.transform, PrimitiveType.Cylinder, "CatenaryMastRight", new Vector3(outerRight, mastHeight * 0.5f, 0f),
                    new Vector3(0.15f, mastHeight * 0.5f, 0.15f), concrete);
                Primitive(portal.transform, PrimitiveType.Cube, "CatenaryCrossBeam", new Vector3(centre, mastHeight - 0.15f, 0f),
                    new Vector3(outerRight - outerLeft + 0.25f, 0.16f, 0.20f), steel);
                for (int track = 0; track < 2; track++)
                {
                    float offset = track == 0 ? 0f : OppositeTrackOffset;
                    Primitive(portal.transform, PrimitiveType.Cylinder, "CatenaryInsulator", new Vector3(offset, mastHeight - 0.48f, 0f),
                        new Vector3(0.10f, 0.28f, 0.10f), insulator);
                    float wireDistance = d + span * 0.5f;
                    if (wireDistance >= cycle) continue;
                    Vector3 wirePoint = Cab3DTrackMath.Point(wireDistance, cycle);
                    Quaternion wireHeading = Cab3DTrackMath.Heading(wireDistance, cycle);
                    GameObject wire = Primitive(root, PrimitiveType.Cube, track == 0 ? "ContactWireOwn" : "ContactWireOpposite",
                        wirePoint + wireHeading * new Vector3(offset, mastHeight - 0.70f, 0f), new Vector3(0.045f, 0.045f, span), steel);
                    wire.transform.rotation = wireHeading;
                    wire.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }

        private static void BuildParallelRoads(Transform root, float cycle, Material concrete, Material steel)
        {
            // Not every segment has a road: these discrete stretches keep the route varied while
            // allowing cars and trucks to appear alongside the player for a readable few seconds.
            // This first road is visible soon after departure, so the route does not open empty.
            // Bring the first road into the opening view.  The previous centre point was 61 m
            // away and too far to one side, leaving the first scene as an empty field.
            BuildParallelRoad(root, cycle, cycle * 0.024f, 76f, -10.5f, concrete, steel, "DepartureRoad", 3);
            BuildParallelRoad(root, cycle, cycle * 0.13f, 76f, -17f, concrete, steel, "ForestRoad", 0);
            BuildParallelRoad(root, cycle, cycle * 0.36f, 92f, 18f, concrete, steel, "CrossingRoad", 1);
            BuildParallelRoad(root, cycle, cycle * 0.79f, 108f, -20f, concrete, steel, "CityApproachRoad", 2);
        }

        private static void BuildParallelRoad(Transform root, float cycle, float distance, float length, float lateral, Material concrete, Material steel, string name, int seed)
        {
            Quaternion heading = Cab3DTrackMath.Heading(distance, cycle);
            GameObject roadRoot = new GameObject(name);
            roadRoot.transform.SetParent(root, false);
            roadRoot.transform.localPosition = RoadFramePosition(distance, cycle, lateral);
            roadRoot.transform.localRotation = heading;
            Material asphalt = CreateAsphaltMaterial(name + "_Asphalt", Vector2.one);
            Material shoulder = CreateTiledMaterial(name + "_GravelShoulder", Color.white,
                "Cab3D/RailwayBallast_v1", new Vector2(0.8f, length / 1.8f), 0.025f);
            Material stripe = Material(name + "_RoadStripe", new Color(0.88f, 0.88f, 0.82f));
            Material reflector = Material(name + "_RoadReflector", new Color(0.86f, 0.78f, 0.50f));
            bool elevated = name == "CityApproachRoad";
            CreateCurvedRoadStrip(roadRoot.transform, "RoadSurface", cycle, distance, lateral,
                distance - length * 0.5f, length, 0f, 3.3f, 0.095f, 2.5f, asphalt);
            for (int side = -1; side <= 1; side += 2)
            {
                CreateCurvedRoadStrip(roadRoot.transform, "GravelShoulder", cycle, distance, lateral,
                    distance - length * 0.5f, length, side * 3.82f, 0.575f, 0.094f, 0.8f, shoulder);
                CreateCurvedRoadStrip(roadRoot.transform, "RoadEdgeLine", cycle, distance, lateral,
                    distance - length * 0.48f, length * 0.96f, side * 2.92f, 0.065f, 0.118f, 1f, stripe);
            }
            if (!elevated)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    CreateCurvedRoadStrip(roadRoot.transform, "RoadsideGrassVerge", cycle, distance, lateral,
                        distance - length * 0.5f, length, side * 5.62f, 1.28f, 0.035f, 0.64f,
                        CreateTiledMaterial(name + "_RoadsideGrass", Color.white,
                            "Cab3D/MeadowGround_v1", Vector2.one, 0.04f));
                }
            }
            for (int side = -1; side <= 1; side += 2)
            {
                if (elevated)
                {
                    for (float z = -length * 0.5f; z < length * 0.5f; z += 6f)
                    {
                        CreateCurvedRoadBox(roadRoot.transform, "GuardRail", cycle, distance, lateral,
                            distance + z + 3f, side * 4.45f, 0.58f, new Vector3(0.10f, 0.16f, 6.1f), steel);
                        CreateCurvedRoadBox(roadRoot.transform, "GuardRailPost", cycle, distance, lateral,
                            distance + z, side * 4.45f, 0.30f, new Vector3(0.10f, 0.48f, 0.10f), steel);
                    }
                }
                else
                {
                    for (float z = -length * 0.43f; z < length * 0.43f; z += 14f)
                    {
                        CreateCurvedRoadBox(roadRoot.transform, "RoadsideReflectorPost", cycle, distance, lateral,
                            distance + z, side * 4.55f, 0.43f, new Vector3(0.08f, 0.86f, 0.08f), steel);
                        CreateCurvedRoadBox(roadRoot.transform, "RoadsideReflector", cycle, distance, lateral,
                            distance + z, side * 4.55f, 0.65f, new Vector3(0.09f, 0.12f, 0.045f), reflector);
                    }
                }
            }
            for (float z = -length * 0.47f; z < length * 0.47f; z += 6.5f)
                CreateCurvedRoadStrip(roadRoot.transform, "CentreDash", cycle, distance, lateral,
                    distance + z, 3.0f, 0f, 0.06f, 0.118f, 1f, stripe);
            CreateRoadVehicle(root, name + "_CarA", lateral + 1.55f, distance - length * 0.42f,
                distance + length * 0.42f, 8f + seed, false,
                Material("RoadCarBlue", new Color(0.04f, 0.27f, 0.62f)), steel, false, cycle);
            CreateRoadVehicle(root, name + "_Truck", lateral - 1.55f, distance - length * 0.38f,
                distance + length * 0.40f, 5.1f + seed, true,
                Material("RoadTruckWhite", new Color(0.70f, 0.72f, 0.70f)), steel, true, cycle);
        }

        private static GameObject CreateCurvedRoadStrip(Transform parent, string name, float cycle,
            float frameDistance, float frameLateral, float pathStartDistance, float pathLength,
            float stripLateral, float halfWidth, float worldY, float textureWidthRepeat, Material material)
        {
            const float sectionLength = 6f;
            int sectionCount = Mathf.Max(2, Mathf.CeilToInt(pathLength / sectionLength));
            Quaternion inverseFrameRotation = Quaternion.Inverse(Cab3DTrackMath.Heading(frameDistance, cycle));
            Vector3 framePosition = RoadFramePosition(frameDistance, cycle, frameLateral);
            MeshBuilder mesh = new MeshBuilder();

            for (int i = 0; i < sectionCount; i++)
            {
                float d0 = pathStartDistance + pathLength * i / sectionCount;
                float d1 = pathStartDistance + pathLength * (i + 1) / sectionCount;
                Vector3 leftStart = CurvedRoadVertex(d0, cycle, framePosition, inverseFrameRotation,
                    frameLateral + stripLateral - halfWidth, worldY);
                Vector3 leftEnd = CurvedRoadVertex(d1, cycle, framePosition, inverseFrameRotation,
                    frameLateral + stripLateral - halfWidth, worldY);
                Vector3 rightEnd = CurvedRoadVertex(d1, cycle, framePosition, inverseFrameRotation,
                    frameLateral + stripLateral + halfWidth, worldY);
                Vector3 rightStart = CurvedRoadVertex(d0, cycle, framePosition, inverseFrameRotation,
                    frameLateral + stripLateral + halfWidth, worldY);
                Vector2 uvStart = new Vector2(0f, (d0 - pathStartDistance) / 2.6f);
                Vector2 uvEnd = new Vector2(0f, (d1 - pathStartDistance) / 2.6f);
                Vector2 uvRightStart = new Vector2(textureWidthRepeat, uvStart.y);
                Vector2 uvRightEnd = new Vector2(textureWidthRepeat, uvEnd.y);
                mesh.AddSurfaceQuad(leftStart, leftEnd, rightEnd, rightStart,
                    uvStart, uvEnd, uvRightEnd, uvRightStart, false);
            }

            GameObject surface = CreateMeshObject(parent, name, mesh.Create(name + "Mesh"), material);
            Renderer renderer = surface.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return surface;
        }

        private static Vector3 CurvedRoadVertex(float distance, float cycle, Vector3 framePosition,
            Quaternion inverseFrameRotation, float lateral, float worldY)
        {
            Vector3 point = Cab3DTrackMath.Point(distance, cycle) +
                            Cab3DTrackMath.Heading(distance, cycle) * new Vector3(lateral, 0f, 0f) +
                            Vector3.up * worldY;
            return inverseFrameRotation * (point - framePosition);
        }

        private static Vector3 RoadFramePosition(float distance, float cycle, float lateral)
        {
            return Cab3DTrackMath.Point(distance, cycle) +
                   Cab3DTrackMath.Heading(distance, cycle) * new Vector3(lateral, 0.035f, 0f);
        }

        private static GameObject CreateCurvedRoadBox(Transform parent, string name, float cycle,
            float frameDistance, float frameLateral, float distance, float lateral, float vertical,
            Vector3 size, Material material)
        {
            Quaternion frameRotation = Cab3DTrackMath.Heading(frameDistance, cycle);
            Vector3 framePosition = RoadFramePosition(frameDistance, cycle, frameLateral);
            Quaternion inverseFrameRotation = Quaternion.Inverse(frameRotation);
            Vector3 routePosition = Cab3DTrackMath.Point(distance, cycle) +
                                    Cab3DTrackMath.Heading(distance, cycle) *
                                    new Vector3(frameLateral + lateral, vertical, 0f);
            GameObject box = Primitive(parent, PrimitiveType.Cube, name,
                inverseFrameRotation * (routePosition - framePosition), size, material);
            box.transform.localRotation = inverseFrameRotation * Cab3DTrackMath.Heading(distance, cycle);
            return box;
        }

        private static GameObject CreateRoadVehicle(Transform road, string name, float laneX, float from, float to, float speed, bool reverse, Material paint, Material steel, bool truck, float trackCycle = 0f)
        {
            GameObject vehicle = new GameObject(name);
            vehicle.transform.SetParent(road, false);
            vehicle.transform.localPosition = trackCycle > 0f
                ? Cab3DTrackMath.Point(from, trackCycle) + Cab3DTrackMath.Heading(from, trackCycle) * new Vector3(laneX, 0.48f, 0f)
                : new Vector3(laneX, 0.48f, from);
            // Vehicle geometry is authored facing local -Z. The non-reversed traffic stream
            // moves +Z, so rotate that model; reverse traffic moves toward its authored nose.
            if (!reverse) vehicle.transform.localRotation = (trackCycle > 0f ? Cab3DTrackMath.Heading(from, trackCycle) : Quaternion.identity) * Quaternion.Euler(0f, 180f, 0f);
            else if (trackCycle > 0f) vehicle.transform.localRotation = Cab3DTrackMath.Heading(from, trackCycle);
            float length = truck ? 5.8f : 3.2f;
            Primitive(vehicle.transform, PrimitiveType.Cube, "Chassis", new Vector3(0f, 0f, 0f), new Vector3(1.55f, 0.42f, length), paint);
            Material glass = Material(name + "_Glass", new Color(0.045f, 0.16f, 0.20f));
            Material lamp = Material(name + "_Lamp", new Color(1f, 0.79f, 0.33f));
            if (truck)
            {
                Primitive(vehicle.transform, PrimitiveType.Cube, "CargoBox", new Vector3(0f, 0.95f, 0.72f), new Vector3(1.5f, 1.55f, 4.15f), paint);
                Primitive(vehicle.transform, PrimitiveType.Cube, "Cab", new Vector3(0f, 0.75f, -2.0f), new Vector3(1.48f, 1.15f, 1.25f), steel);
                Primitive(vehicle.transform, PrimitiveType.Cube, "Windshield", new Vector3(0f, 1.00f, -2.65f), new Vector3(1.18f, 0.48f, 0.035f), glass);
                CreateRoadDriver(vehicle.transform, new Vector3(0f, 0.72f, -2.14f), name.GetHashCode());
            }
            else
            {
                Primitive(vehicle.transform, PrimitiveType.Cube, "Cabin", new Vector3(0f, 0.50f, -0.22f), new Vector3(1.35f, 0.68f, 1.72f), steel);
                Primitive(vehicle.transform, PrimitiveType.Cube, "Windshield", new Vector3(0f, 0.68f, -1.10f), new Vector3(1.08f, 0.34f, 0.035f), glass);
                CreateRoadDriver(vehicle.transform, new Vector3(-0.22f, 0.50f, -0.28f), name.GetHashCode());
            }
            Primitive(vehicle.transform, PrimitiveType.Sphere, "HeadlampLeft", new Vector3(-0.46f, 0.18f, -length * 0.51f), Vector3.one * 0.10f, lamp);
            Primitive(vehicle.transform, PrimitiveType.Sphere, "HeadlampRight", new Vector3(0.46f, 0.18f, -length * 0.51f), Vector3.one * 0.10f, lamp);
            Material wheelMaterial = Material(name + "_RoadWheel", new Color(0.025f, 0.028f, 0.03f));
            Transform[] wheels = new Transform[truck ? 6 : 4];
            int wheelIndex = 0;
            int axles = truck ? 3 : 2;
            for (int axle = 0; axle < axles; axle++)
            {
                float z = Mathf.Lerp(-length * 0.33f, length * 0.33f, axles == 1 ? 0.5f : axle / (float)(axles - 1));
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject wheel = Primitive(vehicle.transform, PrimitiveType.Cylinder, "Wheel", new Vector3(side * 0.86f, -0.18f, z),
                        new Vector3(0.33f, 0.13f, 0.33f), wheelMaterial);
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    wheels[wheelIndex++] = wheel.transform;
                }
            }
            Cab3DRoadVehicleAnimator animator = vehicle.AddComponent<Cab3DRoadVehicleAnimator>();
            if (trackCycle > 0f)
                animator.ConfigureAlongTrack(from, to, trackCycle, laneX, 0.48f, speed, reverse, wheels);
            else
                animator.Configure(from, to, speed, reverse, wheels);
            return vehicle;
        }

        private static void BuildRoadOverpass(Transform root, float cycle, Material concrete, Material steel)
        {
            float distance = cycle * 0.755f;
            Vector3 point = Cab3DTrackMath.Point(distance, cycle);
            Quaternion heading = Cab3DTrackMath.Heading(distance, cycle);
            GameObject overpass = new GameObject("RoadOverpass");
            overpass.transform.SetParent(root, false);
            overpass.transform.localPosition = point + Vector3.up * 6.3f;
            overpass.transform.localRotation = heading;
            Material deck = Material("OverpassConcrete", new Color(0.38f, 0.40f, 0.40f));
            Material asphalt = CreateAsphaltMaterial("OverpassAsphalt", new Vector2(8f, 2.8f));
            Primitive(overpass.transform, PrimitiveType.Cube, "OverpassDeck", Vector3.zero, new Vector3(22f, 0.70f, 7.5f), deck);
            Primitive(overpass.transform, PrimitiveType.Cube, "OverpassRoad", new Vector3(0f, 0.40f, 0f), new Vector3(21f, 0.10f, 6.4f), asphalt);
            for (int side = -1; side <= 1; side += 2)
            {
                Primitive(overpass.transform, PrimitiveType.Cube, "OverpassGuard", new Vector3(0f, 0.78f, side * 3.04f), new Vector3(21.8f, 0.18f, 0.08f), steel);
                Primitive(overpass.transform, PrimitiveType.Cube, "OverpassPillar", new Vector3(side * 8.5f, -3.25f, 0f), new Vector3(1.45f, 6.5f, 1.45f), deck);
            }
            GameObject car = CreateRoadVehicle(overpass.transform, "OverpassCar", 0f, -9f, 9f, 8.2f, false,
                Material("OverpassCarBlue", new Color(0.04f, 0.28f, 0.60f)), steel, false);
            car.transform.localPosition = new Vector3(0f, 0.75f, -1.45f);
            // CarA travels +X and its authored nose is -Z; yaw 270° turns the nose toward +X.
            car.transform.localRotation = Quaternion.Euler(0f, 270f, 0f);
            car.GetComponent<Cab3DRoadVehicleAnimator>().ConfigureAcross(-9f, 9f, 8.2f, false);
            GameObject truck = CreateRoadVehicle(overpass.transform, "OverpassTruck", 0f, -8.6f, 8.6f, 5.4f, true,
                Material("OverpassTruckWhite", new Color(0.72f, 0.72f, 0.69f)), steel, true);
            truck.transform.localPosition = new Vector3(0f, 0.75f, 1.45f);
            // The truck travels -X; yaw 90° points its authored -Z nose along that lane.
            truck.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            truck.GetComponent<Cab3DRoadVehicleAnimator>().ConfigureAcross(-8.6f, 8.6f, 5.4f, true);
        }

        private static void CreateRoadDriver(Transform vehicle, Vector3 position, int seed)
        {
            Material coat = Material("DriverCoat", seed % 2 == 0 ? new Color(0.12f, 0.25f, 0.40f) : new Color(0.36f, 0.13f, 0.09f));
            Material skin = Material("DriverSkin", seed % 3 == 0 ? new Color(0.44f, 0.27f, 0.16f) : new Color(0.74f, 0.50f, 0.34f));
            GameObject driver = new GameObject("RoadDriver");
            driver.transform.SetParent(vehicle, false);
            driver.transform.localPosition = position;
            Primitive(driver.transform, PrimitiveType.Capsule, "DriverTorso", new Vector3(0f, 0.13f, 0f), new Vector3(0.30f, 0.40f, 0.25f), coat);
            Primitive(driver.transform, PrimitiveType.Sphere, "DriverHead", new Vector3(0f, 0.42f, -0.015f), Vector3.one * 0.20f, skin);
        }

        private static void BuildCityViaduct(Transform root, float cycle, Material concrete, Material steel)
        {
            float distance = cycle * 0.79f;
            Vector3 point = Cab3DTrackMath.Point(distance, cycle);
            Quaternion heading = Cab3DTrackMath.Heading(distance, cycle);
            GameObject district = new GameObject("CityViaductDistrict");
            district.transform.SetParent(root, false);
            district.transform.localPosition = point + heading * new Vector3(27f, 4.2f, 0f);
            district.transform.localRotation = heading;
            Material deck = Material("ViaductConcrete", new Color(0.36f, 0.38f, 0.38f));
            Material road = CreateAsphaltMaterial("ViaductAsphalt", new Vector2(2.4f, 112f / 2.6f));
            Material window = Material("CityWindow", new Color(0.16f, 0.38f, 0.50f));
            Primitive(district.transform, PrimitiveType.Cube, "ViaductDeck", Vector3.zero, new Vector3(7.2f, 0.72f, 112f), deck);
            Primitive(district.transform, PrimitiveType.Cube, "ViaductRoadSurface", new Vector3(0f, 0.42f, 0f), new Vector3(6.4f, 0.10f, 112f), road);
            for (int pillar = 0; pillar < 8; pillar++)
            {
                float z = -48f + pillar * 13.7f;
                Primitive(district.transform, PrimitiveType.Cube, "ViaductPillar", new Vector3(0f, -3.7f, z), new Vector3(1.35f, 7.2f, 1.35f), deck);
            }
            // A deliberately small number of real lights gives the city a night identity without
            // turning a tablet route into a field of expensive dynamic shadows.
            for (int lampIndex = 0; lampIndex < 6; lampIndex++)
            {
                float z = -42f + lampIndex * 16.5f;
                CreateScenicLamp(district.transform, new Vector3(-3.25f, 2.1f, z));
                CreateScenicLamp(district.transform, new Vector3(3.25f, 2.1f, z));
            }
            CreateRoadVehicle(district.transform, "ViaductCarA", 1.55f, -48f, 48f, 9f, false,
                Material("ViaductCarBlue", new Color(0.04f, 0.28f, 0.64f)), steel, false);
            CreateRoadVehicle(district.transform, "ViaductTruck", -1.55f, -42f, 44f, 5.2f, true,
                Material("ViaductTruckWhite", new Color(0.72f, 0.72f, 0.69f)), steel, true);

            // A small city silhouette gives the viaduct a destination without expensive imported
            // building meshes. Window strips make the block forms read as inhabited at cab range.
            for (int buildingIndex = 0; buildingIndex < 11; buildingIndex++)
            {
                float side = buildingIndex % 2 == 0 ? -1f : 1f;
                float z = -42f + buildingIndex * 8.4f;
                float height = 6f + (buildingIndex % 4) * 2.4f;
                Color facadeTint = Color.Lerp(new Color(0.90f, 0.78f, 0.63f), new Color(0.74f, 0.82f, 0.86f), buildingIndex / 10f);
                Material facade = CreateTiledMaterial("CityFacade_" + buildingIndex, facadeTint,
                    "Cab3D/CityFacade_v2", new Vector2(2f, 3f), 0.16f);
                GameObject building = Primitive(district.transform, PrimitiveType.Cube, "CityBuilding", new Vector3(side * (10f + buildingIndex % 3 * 2.4f), height * 0.5f - 4.1f, z),
                    new Vector3(6.5f, height, 6.2f), facade);
                for (int row = 0; row < 3; row++)
                    Primitive(building.transform, PrimitiveType.Cube, "WindowStrip", new Vector3(-side * 3.28f, -height * 0.22f + row * 1.55f, 0f), new Vector3(0.035f, 0.45f, 4.7f), window);
            }
        }

        private static void CreateRailSignal(Transform root, Vector3 position, Quaternion heading, float phase)
        {
            Material poleMaterial = Material("SignalPole", new Color(0.10f, 0.12f, 0.13f));
            Material housingMaterial = Material("SignalHousing", new Color(0.055f, 0.065f, 0.072f));
            GameObject signal = new GameObject("RailwaySignal");
            signal.transform.SetParent(root, false);
            signal.transform.localPosition = position;
            signal.transform.localRotation = heading;
            Primitive(signal.transform, PrimitiveType.Cylinder, "SignalBase", new Vector3(0f, 0.13f, 0f), new Vector3(0.52f, 0.13f, 0.52f), poleMaterial);
            Primitive(signal.transform, PrimitiveType.Cylinder, "Mast", new Vector3(0f, 2.2f, 0f), new Vector3(0.11f, 2.2f, 0.11f), poleMaterial);
            Primitive(signal.transform, PrimitiveType.Cube, "SignalHead", new Vector3(0f, 4.35f, -0.06f), new Vector3(0.62f, 1.62f, 0.34f), housingMaterial);
            Renderer red = Primitive(signal.transform, PrimitiveType.Sphere, "RedLamp", new Vector3(0f, 4.84f, -0.25f), Vector3.one * 0.20f,
                Material("SignalRed", new Color(0.85f, 0.04f, 0.02f))).GetComponent<Renderer>();
            Renderer yellow = Primitive(signal.transform, PrimitiveType.Sphere, "YellowLamp", new Vector3(0f, 4.35f, -0.25f), Vector3.one * 0.20f,
                Material("SignalYellow", new Color(0.25f, 0.16f, 0.02f))).GetComponent<Renderer>();
            Renderer green = Primitive(signal.transform, PrimitiveType.Sphere, "GreenLamp", new Vector3(0f, 3.86f, -0.25f), Vector3.one * 0.20f,
                Material("SignalGreen", new Color(0.02f, 0.18f, 0.045f))).GetComponent<Renderer>();
            float[] hoodHeights = { 4.84f, 4.35f, 3.86f };
            for (int i = 0; i < hoodHeights.Length; i++)
            {
                // Three small pieces form an open hood: the lamp stays visible from the cab,
                // while its silhouette no longer reads as a bare coloured sphere.
                Primitive(signal.transform, PrimitiveType.Cube, "SignalLensHoodTop", new Vector3(0f, hoodHeights[i] + 0.16f, -0.31f),
                    new Vector3(0.48f, 0.06f, 0.25f), housingMaterial);
                Primitive(signal.transform, PrimitiveType.Cube, "SignalLensHoodLeft", new Vector3(-0.18f, hoodHeights[i], -0.31f),
                    new Vector3(0.06f, 0.34f, 0.25f), housingMaterial);
                Primitive(signal.transform, PrimitiveType.Cube, "SignalLensHoodRight", new Vector3(0.18f, hoodHeights[i], -0.31f),
                    new Vector3(0.06f, 0.34f, 0.25f), housingMaterial);
            }
            Primitive(signal.transform, PrimitiveType.Cube, "SignalIdentificationPlate", new Vector3(0f, 3.15f, -0.25f), new Vector3(0.42f, 0.22f, 0.035f),
                Material("SignalPlate", new Color(0.86f, 0.86f, 0.74f)));
            GameObject glowObject = new GameObject("SignalGlowLight", typeof(Light));
            glowObject.transform.SetParent(signal.transform, false);
            glowObject.transform.localPosition = new Vector3(0f, 4.35f, -0.35f);
            Light glow = glowObject.GetComponent<Light>();
            glow.type = LightType.Point;
            glow.range = 3.2f;
            glow.shadows = LightShadows.None;
            signal.AddComponent<Cab3DSignalAnimator>().Configure(red, yellow, green, glow, phase,
                Mathf.Abs(Mathf.RoundToInt(phase * 10f)) % 3);
        }

        private static void CreateSpeedSign(Transform root, Vector3 position, Quaternion heading, string speed, Material steel, Material concrete)
        {
            GameObject sign = new GameObject("SpeedSign_" + speed);
            sign.transform.SetParent(root, false);
            sign.transform.localPosition = position;
            sign.transform.localRotation = heading;
            Primitive(sign.transform, PrimitiveType.Cylinder, "Post", new Vector3(0f, 1.2f, 0f), new Vector3(0.08f, 1.2f, 0.08f), steel);
            // Unity cylinders face along local Y by default. Rotate the sign disks into the
            // track plane: the earlier horizontal disks were the reason they looked like odd
            // floating bars rather than actual speed signs from the cab.
            GameObject border = Primitive(sign.transform, PrimitiveType.Cylinder, "RedBorder", new Vector3(0f, 2.35f, -0.075f),
                new Vector3(0.90f, 0.055f, 0.90f), Material("SignRedBorder", new Color(0.76f, 0.035f, 0.025f)));
            border.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject face = Primitive(sign.transform, PrimitiveType.Cylinder, "Face", new Vector3(0f, 2.35f, -0.125f),
                new Vector3(0.72f, 0.055f, 0.72f), Material("SignWhite", new Color(0.94f, 0.92f, 0.84f)));
            face.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject textObject = new GameObject("SpeedText", typeof(TextMesh));
            textObject.transform.SetParent(sign.transform, false);
            textObject.transform.localPosition = new Vector3(0f, 2.35f, -0.195f);
            textObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            TextMesh text = textObject.GetComponent<TextMesh>();
            text.text = speed;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.18f;
            text.fontSize = 62;
            text.color = new Color(0.09f, 0.08f, 0.07f);
        }

        private static void BuildStation(Transform root, float cycle, float routeT, string stationName, Material material, int stationIndex)
        {
            float d = cycle * routeT;
            Vector3 p = Cab3DTrackMath.Point(d, cycle);
            Quaternion q = Cab3DTrackMath.Heading(d, cycle);
            // The platform belongs outside the player's track.  Keeping it out of the two-track gap
            // prevents it from visually swallowing the second line at a station.
            GameObject platform = Primitive(root, PrimitiveType.Cube, "StationPlatform", p + q * new Vector3(-4.0f, 0.25f, 0f),
                new Vector3(4.2f, 0.5f, 52f), material);
            platform.transform.rotation = q;
            Material stationFacade = CreateTiledMaterial("StationWarm", Color.white,
                "Cab3D/StationBrick_v1", new Vector2(6f, 4f), 0.10f);
            GameObject building = Primitive(root, PrimitiveType.Cube, "StationBuilding", p + q * new Vector3(-8.2f, 2.4f, 3f),
                new Vector3(7f, 4.8f, 18f), stationFacade);
            building.transform.rotation = q;
            Material facadeWindow = Material("StationWindow", new Color(0.08f, 0.24f, 0.31f));
            Material doorMaterial = Material("StationDoor", new Color(0.20f, 0.15f, 0.10f));
            Material roofTrim = Material("StationRoof", new Color(0.16f, 0.22f, 0.25f));
            // The façade is built from simple pieces but has a door rhythm and window lighting,
            // rather than reading as one unbroken block at station range.
            Primitive(root, PrimitiveType.Cube, "StationRoofline", p + q * new Vector3(-8.2f, 4.95f, 3f),
                new Vector3(7.45f, 0.30f, 18.45f), roofTrim).transform.rotation = q;
            for (int row = 0; row < 2; row++)
            {
                for (int window = 0; window < 5; window++)
                {
                    float z = -3.6f + window * 3.2f;
                    GameObject facade = Primitive(root, PrimitiveType.Cube, "StationFacadeWindow", p + q * new Vector3(-4.67f, 1.55f + row * 1.55f, z),
                        new Vector3(0.045f, 0.82f, 1.38f), facadeWindow);
                    facade.transform.rotation = q;
                }
            }
            GameObject stationDoor = Primitive(root, PrimitiveType.Cube, "StationEntrance", p + q * new Vector3(-4.66f, 1.18f, 4.85f),
                new Vector3(0.055f, 2.05f, 1.46f), doorMaterial);
            stationDoor.transform.rotation = q;
            Material canopy = Material("StationCanopy", new Color(0.15f, 0.22f, 0.25f));
            Material safety = Material("PlatformSafetyLine", new Color(0.96f, 0.79f, 0.18f));
            GameObject safetyLine = Primitive(root, PrimitiveType.Cube, "PlatformSafetyLine", p + q * new Vector3(-1.78f, 0.53f, 0f),
                new Vector3(0.14f, 0.04f, 51.5f), safety);
            safetyLine.transform.rotation = q;
            GameObject roof = Primitive(root, PrimitiveType.Cube, "StationCanopy", p + q * new Vector3(-5.3f, 5.3f, -2f),
                new Vector3(6.4f, 0.22f, 26f), canopy);
            roof.transform.rotation = q;
            for (int support = 0; support < 4; support++)
            {
                float z = -11f + support * 7.3f;
                GameObject column = Primitive(root, PrimitiveType.Cylinder, "CanopyColumn", p + q * new Vector3(-5.3f, 2.8f, z),
                    new Vector3(0.13f, 2.5f, 0.13f), canopy);
                column.transform.rotation = q;
                GameObject lamp = Primitive(root, PrimitiveType.Sphere, "PlatformLamp", p + q * new Vector3(-4.2f, 4.75f, z),
                    Vector3.one * 0.22f, Material("LampGlow", new Color(1f, 0.82f, 0.36f)));
                lamp.transform.rotation = q;
                CreateScenicLamp(lamp.transform, Vector3.zero, 5.8f, 0.78f);
            }
            for (int benchIndex = 0; benchIndex < 3; benchIndex++)
            {
                GameObject bench = Primitive(root, PrimitiveType.Cube, "StationBench", p + q * new Vector3(-4.1f, 0.9f, -13f + benchIndex * 11f),
                    new Vector3(1.6f, 0.32f, 0.52f), Material("BenchWood", new Color(0.38f, 0.20f, 0.08f)));
                bench.transform.rotation = q * Quaternion.Euler(0f, 90f, 0f);
            }
            BuildStationSign(root, p + q * new Vector3(-5.2f, 2.8f, -7f), q, stationName);
            for (int i = 0; i < 12; i++)
            {
                // Pair nearby positions into small families. Children use a smaller body scale and
                // seated passengers line up with the benches rather than forming a uniform row.
                float familyZ = -16f + (i / 2) * 5.7f;
                float familyOffset = i % 2 == 0 ? -0.35f : 0.45f;
                bool seated = i == 3 || i == 8;
                Vector3 platformPosition = p + q * new Vector3(-3.1f - (i % 3) * 0.7f, 0.28f, seated ? -13f + (i == 8 ? 11f : 0f) : familyZ + familyOffset);
                Vector3 coachDoorPosition = p + q * new Vector3(-1.5f, 0.28f, familyZ + familyOffset);
                GameObject passenger = CreatePassenger(root, "Passenger_" + stationIndex + "_" + i, platformPosition,
                    Color.Lerp(new Color(0.12f, 0.45f, 0.72f), new Color(0.95f, 0.54f, 0.18f), i / 11f),
                    i % 6 == 0, seated, i % 3 == 1, out GameObject umbrella, out Renderer coat, out Transform[] limbs);
                passenger.AddComponent<Cab3DPassengerAgent>().Configure(platformPosition, coachDoorPosition, umbrella, coat, limbs);
                Cab3DInteractiveObject interactive = passenger.AddComponent<Cab3DInteractiveObject>();
                interactive.Configure("station passenger worker", CabInteractionReaction.Wave);
                passenger.SetActive(false);
            }
        }

        internal static GameObject CreatePassenger(Transform root, string name, Vector3 position, Color coatColor,
            bool child, bool seated, bool withUmbrella, out GameObject umbrella, out Renderer coatRenderer, out Transform[] limbs)
        {
            GameObject actor = new GameObject(name);
            actor.transform.SetParent(root, false);
            actor.transform.localPosition = position;
            actor.transform.localScale = Vector3.one * (child ? 0.64f : seated ? 0.92f : 1f);
            int appearance = name.Length > 0 ? name[name.Length - 1] % 4 : 0;
            Material coat = Material(name + "_Coat", coatColor);
            Color skinTone = appearance == 0 ? new Color(0.88f, 0.65f, 0.48f) :
                appearance == 1 ? new Color(0.69f, 0.43f, 0.28f) :
                appearance == 2 ? new Color(0.48f, 0.29f, 0.18f) : new Color(0.95f, 0.77f, 0.60f);
            Color hairTone = appearance == 0 ? new Color(0.12f, 0.07f, 0.035f) :
                appearance == 1 ? new Color(0.38f, 0.16f, 0.06f) :
                appearance == 2 ? new Color(0.035f, 0.028f, 0.022f) : new Color(0.66f, 0.51f, 0.24f);
            Color trouserTone = appearance == 0 ? new Color(0.065f, 0.10f, 0.16f) :
                appearance == 1 ? new Color(0.17f, 0.20f, 0.23f) :
                appearance == 2 ? new Color(0.13f, 0.18f, 0.30f) : new Color(0.22f, 0.16f, 0.10f);
            Material skin = Material(name + "_Skin", skinTone);
            Material hair = Material(name + "_Hair", hairTone);
            Material trousers = Material(name + "_Trousers", trouserTone);
            GameObject body = Primitive(actor.transform, PrimitiveType.Capsule, "Body", new Vector3(0f, seated ? 0.52f : 0.82f, seated ? 0.18f : 0f),
                new Vector3(0.42f, seated ? 0.60f : 0.82f, 0.42f), coat);
            if (seated) body.transform.localRotation = Quaternion.Euler(0f, 0f, 82f);
            Primitive(actor.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, seated ? 1.15f : 1.73f, seated ? 0.18f : 0f), new Vector3(0.42f, 0.42f, 0.42f), skin);
            Primitive(actor.transform, PrimitiveType.Sphere, "Hair", new Vector3(0f, seated ? 1.35f : 1.93f, seated ? 0.14f : -0.04f), new Vector3(0.44f, 0.19f, 0.42f), hair);
            limbs = new Transform[4];
            float torsoY = seated ? 0.74f : 1.10f;
            float legY = seated ? 0.25f : 0.42f;
            for (int side = -1; side <= 1; side += 2)
            {
                int index = side < 0 ? 0 : 1;
                GameObject arm = Primitive(actor.transform, PrimitiveType.Capsule, "Arm", new Vector3(side * 0.36f, torsoY, 0f),
                    new Vector3(0.12f, 0.48f, 0.12f), coat);
                arm.transform.localRotation = Quaternion.Euler(0f, 0f, side * -11f);
                limbs[index] = arm.transform;
                GameObject leg = Primitive(actor.transform, PrimitiveType.Capsule, "Leg", new Vector3(side * 0.16f, legY, seated ? 0.34f : 0f),
                    new Vector3(0.15f, seated ? 0.40f : 0.54f, 0.15f), trousers);
                if (seated) leg.transform.localRotation = Quaternion.Euler(86f, 0f, 0f);
                limbs[index + 2] = leg.transform;
            }
            if (appearance == 1 || child)
            {
                Material backpack = Material(name + "_Backpack", child ? new Color(0.92f, 0.32f, 0.10f) : new Color(0.12f, 0.22f, 0.36f));
                Primitive(actor.transform, PrimitiveType.Cube, "Backpack", new Vector3(0f, seated ? 0.78f : 1.12f, 0.25f),
                    new Vector3(0.42f, 0.58f, 0.20f), backpack);
            }
            umbrella = null;
            if (withUmbrella)
            {
                umbrella = new GameObject("Umbrella");
                umbrella.transform.SetParent(actor.transform, false);
                int umbrellaVariant = name.Length > 0 ? name[name.Length - 1] % 4 : 0;
                Color umbrellaColor = umbrellaVariant == 0 ? new Color(0.08f, 0.20f, 0.48f) :
                    umbrellaVariant == 1 ? new Color(0.62f, 0.12f, 0.10f) :
                    umbrellaVariant == 2 ? new Color(0.12f, 0.42f, 0.26f) : new Color(0.52f, 0.30f, 0.10f);
                float handHeight = seated ? 0.64f : 1.02f;
                umbrella.transform.localPosition = new Vector3(0.34f, handHeight, 0.04f);
                // The root is at the passenger's hand. This gives a real vertical stem, keeps the
                // canopy above the hair, and avoids the old effect where a flat oval crossed a face.
                Material canopy = Material(name + "_Umbrella", umbrellaColor);
                Material ribs = Material(name + "_UmbrellaRibs", new Color(0.07f, 0.08f, 0.09f));
                Primitive(umbrella.transform, PrimitiveType.Cylinder, "UmbrellaHandle", new Vector3(0f, 0.61f, 0f), new Vector3(0.026f, 0.61f, 0.026f), hair);
                Primitive(umbrella.transform, PrimitiveType.Sphere, "UmbrellaCanopy", new Vector3(0f, 1.34f, 0f), new Vector3(0.86f, 0.16f, 0.86f), canopy);
                Primitive(umbrella.transform, PrimitiveType.Cylinder, "UmbrellaCanopyRim", new Vector3(0f, 1.24f, 0f), new Vector3(0.74f, 0.022f, 0.74f), ribs);
                GameObject ribA = Primitive(umbrella.transform, PrimitiveType.Cube, "UmbrellaRibA", new Vector3(0f, 1.27f, 0f), new Vector3(0.055f, 0.06f, 1.42f), ribs);
                ribA.transform.localRotation = Quaternion.Euler(0f, 34f, 0f);
                GameObject ribB = Primitive(umbrella.transform, PrimitiveType.Cube, "UmbrellaRibB", new Vector3(0f, 1.27f, 0f), new Vector3(1.42f, 0.06f, 0.055f), ribs);
                ribB.transform.localRotation = Quaternion.Euler(0f, 34f, 0f);
                Primitive(umbrella.transform, PrimitiveType.Sphere, "UmbrellaTip", new Vector3(0f, 1.54f, 0f), Vector3.one * 0.09f, ribs);
                umbrella.SetActive(false);
            }
            coatRenderer = body.GetComponent<Renderer>();
            return actor;
        }

        private static void BuildTunnel(Transform root, float cycle, Material material)
        {
            // CabJourneyRuntime's fixed prototype order reaches the mountain segment after
            // meadow, forest, road and village.  The old single portal at 69% was near the
            // *exit*, which made the train appear to skirt the mountain.  Build the complete
            // two-track bore from the actual segment entry to its exit instead.
            float startDistance = cycle * CabRouteLayout.TunnelStart01;
            float endDistance = cycle * CabRouteLayout.TunnelEnd01;
            float d = startDistance;
            Vector3 p = Cab3DTrackMath.Point(d, cycle);
            Quaternion q = Cab3DTrackMath.Heading(d, cycle);
            Material rock = Material("Rock", new Color(0.27f, 0.29f, 0.27f));
            Material lining = Material("TunnelLining", new Color(0.20f, 0.22f, 0.22f));
            Material lamp = Material("TunnelLamp", new Color(1f, 0.64f, 0.22f));
            // Both lines share one broad tunnel bore.  Center its portal midway between the tracks,
            // otherwise the parallel line appears to run around the outside of the mountain.
            float boreCenter = OppositeTrackOffset * 0.5f;
            // The track curves inside the tunnel, so the rock mass is built in short pieces that
            // follow it like the lining does. One long straight block used to cut into the
            // track toward the exit, and the cab drove through the rock.
            for (float rockDistance = startDistance; rockDistance < endDistance; rockDistance += 5.8f)
            {
                Vector3 point = Cab3DTrackMath.Point(rockDistance + 2.9f, cycle);
                Quaternion heading = Cab3DTrackMath.Heading(rockDistance + 2.9f, cycle);
                GameObject rockLeft = Primitive(root, PrimitiveType.Cube, "TunnelMountainLeft", point + heading * new Vector3(boreCenter - 12.4f, 9f, 0f),
                    new Vector3(13f, 19f, 6.2f), rock);
                GameObject rockRight = Primitive(root, PrimitiveType.Cube, "TunnelMountainRight", point + heading * new Vector3(boreCenter + 12.4f, 9f, 0f),
                    new Vector3(13f, 19f, 6.2f), rock);
                GameObject rockTop = Primitive(root, PrimitiveType.Cube, "TunnelMountainTop", point + heading * new Vector3(boreCenter, 12.6f, 0f),
                    new Vector3(12.2f, 12.6f, 6.2f), rock);
                rockLeft.transform.rotation = rockRight.transform.rotation = rockTop.transform.rotation = heading;
            }
            BuildTunnelFacade(root, startDistance, cycle, boreCenter, rock, -2.5f);
            BuildTunnelFacade(root, endDistance, cycle, boreCenter, rock, 2.5f);

            for (float tunnelDistance = startDistance; tunnelDistance <= endDistance; tunnelDistance += 5.8f)
            {
                Vector3 point = Cab3DTrackMath.Point(tunnelDistance, cycle);
                Quaternion heading = Cab3DTrackMath.Heading(tunnelDistance, cycle);
                // The walls sit outside both tracks. The roof is high enough for a double line,
                // leaving the rails and their ballast visually on the ground rather than in black.
                GameObject leftWall = Primitive(root, PrimitiveType.Cube, "TunnelLiningLeft", point + heading * new Vector3(boreCenter - 5.65f, 3.0f, 0f),
                    new Vector3(0.34f, 6.0f, 5.95f), lining);
                GameObject rightWall = Primitive(root, PrimitiveType.Cube, "TunnelLiningRight", point + heading * new Vector3(boreCenter + 5.65f, 3.0f, 0f),
                    new Vector3(0.34f, 6.0f, 5.95f), lining);
                GameObject roof = Primitive(root, PrimitiveType.Cube, "TunnelLiningRoof", point + heading * new Vector3(boreCenter, 6.0f, 0f),
                    new Vector3(11.6f, 0.35f, 5.95f), lining);
                leftWall.transform.rotation = rightWall.transform.rotation = roof.transform.rotation = heading;
                if (Mathf.RoundToInt((tunnelDistance - startDistance) / 5.8f) % 2 == 0)
                {
                    GameObject bulb = Primitive(root, PrimitiveType.Sphere, "TunnelLamp", point + heading * new Vector3(boreCenter, 5.60f, 0f),
                        Vector3.one * 0.22f, lamp);
                    bulb.transform.rotation = heading;
                    bulb.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    GameObject lightObject = new GameObject("TunnelInteriorLight", typeof(Light));
                    lightObject.transform.SetParent(root, false);
                    lightObject.transform.position = bulb.transform.position;
                    Light interiorLight = lightObject.GetComponent<Light>();
                    interiorLight.type = LightType.Point;
                    interiorLight.color = new Color(1f, 0.61f, 0.26f);
                    interiorLight.range = 8.2f;
                    interiorLight.intensity = 0.92f;
                    interiorLight.shadows = LightShadows.None;
                    interiorLight.enabled = false;
                }
            }
            CreateTunnelPortal(root, startDistance, cycle, boreCenter, lining, "Entrance");
            CreateTunnelPortal(root, endDistance, cycle, boreCenter, lining, "Exit");
        }

        /// <summary>Rock face around a portal opening, leaving the bore itself clear.</summary>
        private static void BuildTunnelFacade(Transform root, float distance, float cycle, float boreCenter, Material rock, float outward)
        {
            Vector3 p = Cab3DTrackMath.Point(distance, cycle);
            Quaternion q = Cab3DTrackMath.Heading(distance, cycle);
            GameObject left = Primitive(root, PrimitiveType.Cube, "TunnelFacadeLeft", p + q * new Vector3(boreCenter - 19f, 9f, outward),
                new Vector3(26f, 18f, 5f), rock);
            GameObject right = Primitive(root, PrimitiveType.Cube, "TunnelFacadeRight", p + q * new Vector3(boreCenter + 19f, 9f, outward),
                new Vector3(26f, 18f, 5f), rock);
            GameObject top = Primitive(root, PrimitiveType.Cube, "TunnelFacadeTop", p + q * new Vector3(boreCenter, 13.2f, outward),
                new Vector3(13f, 12.4f, 5f), rock);
            left.transform.rotation = right.transform.rotation = top.transform.rotation = q;
        }

        private static void CreateTunnelPortal(Transform root, float distance, float cycle, float boreCenter, Material lining, string name)
        {
            Vector3 p = Cab3DTrackMath.Point(distance, cycle);
            Quaternion q = Cab3DTrackMath.Heading(distance, cycle);
            GameObject header = Primitive(root, PrimitiveType.Cube, "TunnelPortal_" + name + "_Header", p + q * new Vector3(boreCenter, 6.15f, 0f),
                new Vector3(12.4f, 0.65f, 0.62f), lining);
            GameObject left = Primitive(root, PrimitiveType.Cube, "TunnelPortal_" + name + "_Left", p + q * new Vector3(boreCenter - 5.9f, 3.1f, 0f),
                new Vector3(0.65f, 6.2f, 0.62f), lining);
            GameObject right = Primitive(root, PrimitiveType.Cube, "TunnelPortal_" + name + "_Right", p + q * new Vector3(boreCenter + 5.9f, 3.1f, 0f),
                new Vector3(0.65f, 6.2f, 0.62f), lining);
            header.transform.rotation = left.transform.rotation = right.transform.rotation = q;
        }

        private static void BuildLivingScenery(Transform root, float cycle, Material concrete, Material steel, Material wood)
        {
            float d = cycle * 0.18f;
            Vector3 p = Cab3DTrackMath.Point(d, cycle) + new Vector3(-14f, 0f, 0f);
            GameObject mill = Primitive(root, PrimitiveType.Cylinder, "WindmillTower", p + Vector3.up * 3.2f,
                new Vector3(1.8f, 3.2f, 1.8f), concrete);
            GameObject rotor = new GameObject("WindmillRotor");
            rotor.transform.SetParent(mill.transform, false);
            rotor.transform.localPosition = new Vector3(0f, 0.65f, -0.62f);
            for (int i = 0; i < 4; i++)
            {
                Quaternion bladeRotation = Quaternion.Euler(0f, 0f, i * 90f);
                GameObject blade = Primitive(rotor.transform, PrimitiveType.Cube, "Blade", bladeRotation * new Vector3(0f, 1.5f, 0f),
                    new Vector3(0.28f, 3f, 0.18f), wood);
                blade.transform.localRotation = bladeRotation;
            }
            rotor.AddComponent<Cab3DAnimatedObject>().Configure(Cab3DAnimatedObject.MotionKind.Rotate, Vector3.forward, 28f);

            BuildPastureLife(root, cycle, concrete);

            // A small wind farm makes the horizon feel alive without expensive animated shaders.
            for (int turbineIndex = 0; turbineIndex < 3; turbineIndex++)
            {
                float turbineD = cycle * (0.22f + turbineIndex * 0.045f);
                Quaternion turbineQ = Cab3DTrackMath.Heading(turbineD, cycle);
                Vector3 turbineP = Cab3DTrackMath.Point(turbineD, cycle) + turbineQ * new Vector3(17f + turbineIndex * 4f, 0f, 0f);
                GameObject tower = Primitive(root, PrimitiveType.Cylinder, "WindTurbineTower", turbineP + Vector3.up * 7f,
                    new Vector3(0.34f, 7f, 0.34f), concrete);
                GameObject turbine = new GameObject("WindTurbineRotor");
                turbine.transform.SetParent(tower.transform, false);
                turbine.transform.localPosition = new Vector3(0f, 1f, -0.2f);
                for (int bladeIndex = 0; bladeIndex < 3; bladeIndex++)
                {
                    Quaternion bladeRotation = Quaternion.Euler(0f, 0f, bladeIndex * 120f);
                    GameObject blade = Primitive(turbine.transform, PrimitiveType.Cube, "TurbineBlade", bladeRotation * new Vector3(0f, 1.45f, 0f),
                        new Vector3(0.14f, 2.8f, 0.12f), steel);
                    blade.transform.localRotation = bladeRotation;
                }
                turbine.AddComponent<Cab3DAnimatedObject>().Configure(Cab3DAnimatedObject.MotionKind.Rotate, Vector3.forward, 14f + turbineIndex * 3f);
            }

            float tractorD = cycle * 0.12f;
            Quaternion tractorQ = Cab3DTrackMath.Heading(tractorD, cycle);
            Vector3 tractorP = Cab3DTrackMath.Point(tractorD, cycle) + tractorQ * new Vector3(-15f, 0.65f, -8f);
            GameObject tractor = Primitive(root, PrimitiveType.Cube, "FieldTractor", tractorP, new Vector3(2.2f, 1.3f, 3f),
                Material("TractorYellow", new Color(0.86f, 0.56f, 0.05f)));
            tractor.transform.rotation = tractorQ;
            tractor.AddComponent<Cab3DAnimatedObject>().Configure(Cab3DAnimatedObject.MotionKind.Drive, Vector3.right, 0.025f, 6f);

            float trainD = cycle * 0.19f;
            GameObject train = BuildMeetingTrain(root, Vector3.zero, Quaternion.identity);
            train.AddComponent<Cab3DTrackFollower>().Configure(cycle, trainD, OppositeTrackOffset, 16f);
            Cab3DInteractiveObject interactive = train.AddComponent<Cab3DInteractiveObject>();
            interactive.Configure("meeting train", CabInteractionReaction.ReplyLight);

            GameObject birds = new GameObject("BirdFlock");
            birds.transform.SetParent(root, false);
            birds.transform.localPosition = Cab3DTrackMath.Point(cycle * 0.27f, cycle) + new Vector3(-18f, 12f, 0f);
            Transform[] birdBodies = new Transform[8];
            Transform[] leftWings = new Transform[8];
            Transform[] rightWings = new Transform[8];
            Material birdMaterial = Material("BirdFeathers", new Color(0.055f, 0.07f, 0.08f));
            Material wingEdge = Material("BirdWingEdge", new Color(0.10f, 0.12f, 0.13f));
            Material beakMaterial = Material("BirdBeak", new Color(0.74f, 0.40f, 0.08f));
            Mesh wingMesh = CreateBirdWingMesh();
            Mesh tailMesh = CreateBirdTailMesh();
            for (int i = 0; i < birdBodies.Length; i++)
            {
                GameObject bird = new GameObject("Bird_" + i);
                bird.transform.SetParent(birds.transform, false);
                bird.transform.localPosition = new Vector3(i * 1.22f, Mathf.Abs(3.5f - i) * 0.38f, (i % 3 - 1) * 0.48f);
                float birdScale = 0.88f + (i % 3) * 0.08f;
                GameObject body = Primitive(bird.transform, PrimitiveType.Capsule, "Body", Vector3.zero, new Vector3(0.20f, 0.28f, 0.52f) * birdScale, birdMaterial);
                body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Primitive(bird.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.02f, 0.29f) * birdScale, Vector3.one * 0.17f * birdScale, birdMaterial);
                Primitive(bird.transform, PrimitiveType.Cylinder, "Beak", new Vector3(0f, 0.02f, 0.44f) * birdScale, new Vector3(0.06f, 0.14f, 0.06f) * birdScale, beakMaterial)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Material eyeMaterial = Material("BirdEye_" + i, new Color(0.005f, 0.006f, 0.008f));
                Primitive(bird.transform, PrimitiveType.Sphere, "EyeLeft", new Vector3(-0.105f, 0.075f, 0.37f) * birdScale,
                    Vector3.one * 0.035f * birdScale, eyeMaterial);
                Primitive(bird.transform, PrimitiveType.Sphere, "EyeRight", new Vector3(0.105f, 0.075f, 0.37f) * birdScale,
                    Vector3.one * 0.035f * birdScale, eyeMaterial);
                GameObject tail = new GameObject("TailFeathers", typeof(MeshFilter), typeof(MeshRenderer));
                tail.transform.SetParent(bird.transform, false);
                tail.transform.localPosition = new Vector3(0f, -0.01f, -0.30f) * birdScale;
                tail.transform.localScale = Vector3.one * birdScale;
                tail.GetComponent<MeshFilter>().sharedMesh = tailMesh;
                tail.GetComponent<MeshRenderer>().sharedMaterial = wingEdge;
                GameObject leftWing = CreateBirdWing(bird.transform, "LeftWing", wingMesh, wingEdge, true);
                GameObject rightWing = CreateBirdWing(bird.transform, "RightWing", wingMesh, wingEdge, false);
                birdBodies[i] = bird.transform;
                leftWings[i] = leftWing.transform;
                rightWings[i] = rightWing.transform;
            }
            birds.AddComponent<Cab3DBirdFlockAnimator>().Configure(birdBodies, leftWings, rightWings, 0.75f, 20f, cycle * 0.13f);
            Cab3DInteractiveObject birdInteractive = birds.AddComponent<Cab3DInteractiveObject>();
            birdInteractive.Configure("bird flock", CabInteractionReaction.FlyAway);

            GameObject balloon = Primitive(root, PrimitiveType.Sphere, "HotAirBalloon", Cab3DTrackMath.Point(cycle * 0.46f, cycle) + new Vector3(-20f, 16f, 0f),
                new Vector3(2.2f, 3.1f, 2.2f), Material("Balloon", new Color(0.94f, 0.24f, 0.12f)));
            balloon.AddComponent<Cab3DAnimatedObject>().Configure(Cab3DAnimatedObject.MotionKind.Bob, Vector3.up, 0.18f, 0.7f);
        }

        private static void BuildPastureLife(Transform root, float cycle, Material concrete)
        {
            CreatePastureAnimal(root, "Cow_0", Cab3DTrackMath.Point(cycle * 0.095f, cycle) + new Vector3(-14f, 0f, 3f),
                Quaternion.Euler(0f, 22f, 0f), new Color(0.78f, 0.70f, 0.56f), CabInteractionReaction.Nod, false);
            CreatePastureAnimal(root, "Cow_1", Cab3DTrackMath.Point(cycle * 0.108f, cycle) + new Vector3(-16f, 0f, -2f),
                Quaternion.Euler(0f, -18f, 0f), new Color(0.34f, 0.21f, 0.12f), CabInteractionReaction.Nod, false);
            CreatePastureAnimal(root, "Sheep_0", Cab3DTrackMath.Point(cycle * 0.285f, cycle) + new Vector3(15f, 0f, 2f),
                Quaternion.Euler(0f, -30f, 0f), new Color(0.84f, 0.82f, 0.72f), CabInteractionReaction.LookUp, true);
            CreatePastureAnimal(root, "Sheep_1", Cab3DTrackMath.Point(cycle * 0.296f, cycle) + new Vector3(18f, 0f, -1.5f),
                Quaternion.Euler(0f, 15f, 0f), new Color(0.71f, 0.68f, 0.58f), CabInteractionReaction.LookUp, true);
            CreatePastureAnimal(root, "Horse_0", Cab3DTrackMath.Point(cycle * 0.455f, cycle) + new Vector3(-17f, 0f, 4f),
                Quaternion.Euler(0f, 42f, 0f), new Color(0.30f, 0.14f, 0.07f), CabInteractionReaction.Nod, false, true);

            for (int i = 0; i < 2; i++)
            {
                float distance = cycle * (0.325f + i * 0.018f);
                GameObject worker = new GameObject("TrackWorker_" + i);
                worker.transform.SetParent(root, false);
                worker.transform.localPosition = Cab3DTrackMath.Point(distance, cycle) + new Vector3(-5.7f - i * 0.75f, 0.22f, 0f);
                worker.transform.localRotation = Quaternion.Euler(0f, 68f + i * 20f, 0f);
                Material coat = Material("WorkerHiVis_" + i, new Color(0.94f, 0.54f + i * 0.12f, 0.06f));
                Material skin = Material("WorkerSkin_" + i, i == 0 ? new Color(0.74f, 0.48f, 0.30f) : new Color(0.50f, 0.30f, 0.18f));
                Primitive(worker.transform, PrimitiveType.Capsule, "WorkerBody", new Vector3(0f, 1.0f, 0f), new Vector3(0.46f, 0.90f, 0.46f), coat);
                GameObject head = Primitive(worker.transform, PrimitiveType.Sphere, "WorkerHead", new Vector3(0f, 1.93f, 0f), Vector3.one * 0.34f, skin);
                Primitive(worker.transform, PrimitiveType.Sphere, "WorkerHelmet", new Vector3(0f, 2.12f, 0f), new Vector3(0.40f, 0.16f, 0.40f), Material("WorkerHelmet_" + i, new Color(0.96f, 0.84f, 0.15f)));
                GameObject arm = Primitive(worker.transform, PrimitiveType.Capsule, "WorkerWaveArm", new Vector3(0.38f, 1.18f, 0f), new Vector3(0.12f, 0.50f, 0.12f), coat);
                arm.transform.localRotation = Quaternion.Euler(0f, 0f, -16f);
                worker.AddComponent<Cab3DContextAnimator>().Configure(arm.transform);
                worker.AddComponent<Cab3DInteractiveObject>().Configure("track worker", CabInteractionReaction.Wave);
            }
        }

        private static void CreatePastureAnimal(Transform root, string name, Vector3 position, Quaternion rotation, Color coatColor,
            CabInteractionReaction reaction, bool sheep, bool horse = false)
        {
            GameObject animal = new GameObject(name);
            animal.transform.SetParent(root, false);
            animal.transform.localPosition = position;
            animal.transform.localRotation = rotation;
            float bodyLength = horse ? 2.35f : sheep ? 1.20f : 1.82f;
            float bodyHeight = horse ? 1.08f : sheep ? 0.72f : 0.90f;
            Material coat = Material(name + "_Coat", coatColor);
            Primitive(animal.transform, sheep ? PrimitiveType.Sphere : PrimitiveType.Capsule, "AnimalBody", new Vector3(0f, bodyHeight, 0f),
                new Vector3(sheep ? 1.18f : 0.86f, bodyHeight, bodyLength), coat);
            GameObject head = Primitive(animal.transform, PrimitiveType.Sphere, "AnimalHead", new Vector3(0f, bodyHeight + (horse ? 0.86f : 0.46f), bodyLength * 0.40f),
                sheep ? new Vector3(0.48f, 0.42f, 0.50f) : new Vector3(0.52f, 0.58f, 0.60f), coat);
            Material legMaterial = Material(name + "_Leg", new Color(0.10f, 0.075f, 0.045f));
            for (int side = -1; side <= 1; side += 2)
                for (int front = -1; front <= 1; front += 2)
                    Primitive(animal.transform, PrimitiveType.Cylinder, "AnimalLeg", new Vector3(side * (sheep ? 0.38f : 0.46f), bodyHeight * 0.45f, front * bodyLength * 0.28f),
                        new Vector3(0.11f, bodyHeight * 0.48f, 0.11f), legMaterial);
            if (horse)
                Primitive(animal.transform, PrimitiveType.Capsule, "HorseMane", new Vector3(0f, bodyHeight + 0.76f, bodyLength * 0.25f), new Vector3(0.18f, 0.55f, 0.18f),
                    Material(name + "_Mane", new Color(0.055f, 0.035f, 0.02f)));
            animal.AddComponent<Cab3DContextAnimator>().Configure(head.transform);
            animal.AddComponent<Cab3DInteractiveObject>().Configure(name.StartsWith("Cow") ? "cow" : name.StartsWith("Sheep") ? "sheep" : "horse", reaction);
        }

        private static GameObject CreateBirdWing(Transform parent, string name, Mesh wingMesh, Material material, bool left)
        {
            GameObject wing = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            wing.transform.SetParent(parent, false);
            wing.transform.localPosition = new Vector3(left ? -0.06f : 0.06f, 0f, 0.02f);
            wing.transform.localScale = new Vector3(left ? -1f : 1f, 1f, 1f);
            wing.GetComponent<MeshFilter>().sharedMesh = wingMesh;
            wing.GetComponent<MeshRenderer>().sharedMaterial = material;
            wing.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return wing;
        }

        private static Mesh CreateBirdWingMesh()
        {
            // A thin swept wing has a readable silhouette from the cab, unlike the old rectangles.
            Mesh mesh = new Mesh { name = "BirdWingLowPoly" };
            mesh.vertices = new[]
            {
                Vector3.zero, new Vector3(0.78f, 0.06f, 0.10f), new Vector3(0.20f, 0.01f, -0.47f),
                new Vector3(0.56f, 0.18f, -0.05f), new Vector3(0.78f, -0.02f, 0.10f), new Vector3(0.20f, -0.02f, -0.47f)
            };
            mesh.triangles = new[] { 0, 1, 3, 0, 3, 2, 0, 4, 5, 0, 5, 3, 1, 4, 3, 2, 3, 5 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateBirdTailMesh()
        {
            Mesh mesh = new Mesh { name = "BirdTailFeathers" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0.02f, 0.10f), new Vector3(-0.26f, 0f, -0.40f), new Vector3(0.26f, 0f, -0.40f),
                new Vector3(0f, -0.035f, 0.10f), new Vector3(-0.26f, -0.04f, -0.40f), new Vector3(0.26f, -0.04f, -0.40f)
            };
            mesh.triangles = new[] { 0, 1, 2, 3, 5, 4, 0, 3, 4, 0, 4, 1, 0, 2, 5, 0, 5, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GameObject BuildMeetingTrain(Transform root, Vector3 position, Quaternion heading)
        {
            GameObject train = new GameObject("MeetingTrain");
            train.transform.SetParent(root, false);
            train.transform.localPosition = position;
            train.transform.localRotation = heading;
            Material blue = Material("MeetingTrainBlue", new Color(0.05f, 0.28f, 0.55f));
            Material cream = Material("MeetingTrainStripe", new Color(0.88f, 0.84f, 0.65f));
            Material glass = Material("MeetingTrainWindows", new Color(0.08f, 0.30f, 0.36f));
            Material wheel = Material("MeetingTrainWheels", new Color(0.06f, 0.07f, 0.075f));
            CreateTrainCar(train.transform, "Locomotive", 0f, 4.8f, blue, cream, glass, wheel, true);
            CreateTrainCar(train.transform, "CoachA", 5.7f, 5.2f, blue, cream, glass, wheel, false);
            CreateTrainCar(train.transform, "CoachB", 11.6f, 5.2f, blue, cream, glass, wheel, false);
            return train;
        }

        private static void CreateTrainCar(Transform parent, string name, float z, float length, Material body,
            Material stripe, Material glass, Material wheel, bool locomotive)
        {
            GameObject car = new GameObject(name);
            car.transform.SetParent(parent, false);
            car.transform.localPosition = new Vector3(0f, 0f, z);
            Primitive(car.transform, PrimitiveType.Cube, "Body", new Vector3(0f, 1.22f, 0f), new Vector3(2.35f, 2.0f, length), body);
            Primitive(car.transform, PrimitiveType.Cube, "Stripe", new Vector3(0f, 1.3f, -0.01f), new Vector3(2.42f, 0.23f, length + 0.04f), stripe);
            Primitive(car.transform, PrimitiveType.Cube, "Roof", new Vector3(0f, 2.34f, 0f), new Vector3(2.5f, 0.18f, length + 0.16f), stripe);
            int windows = locomotive ? 2 : 4;
            for (int side = -1; side <= 1; side += 2)
                for (int window = 0; window < windows; window++)
                {
                    float windowZ = Mathf.Lerp(-length * 0.30f, length * 0.30f, windows == 1 ? 0.5f : window / (float)(windows - 1));
                    Primitive(car.transform, PrimitiveType.Cube, "Window", new Vector3(side * 1.19f, 1.62f, windowZ),
                        new Vector3(0.035f, 0.62f, locomotive ? 0.74f : 0.58f), glass);
                }
            for (int bogie = -1; bogie <= 1; bogie += 2)
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject wheelObject = Primitive(car.transform, PrimitiveType.Cylinder, "Wheel", new Vector3(side * 1.02f, 0.43f, bogie * length * 0.30f),
                        new Vector3(0.42f, 0.16f, 0.42f), wheel);
                    wheelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
            if (locomotive)
            {
                Material lampMaterial = Material("MeetingTrainHeadlamp", new Color(1f, 0.84f, 0.48f));
                Primitive(car.transform, PrimitiveType.Sphere, "MeetingTrainLamp", new Vector3(0f, 1.48f, -length * 0.51f),
                    Vector3.one * 0.19f, lampMaterial);
                GameObject headlight = new GameObject("MeetingTrainHeadlight", typeof(Light));
                headlight.transform.SetParent(car.transform, false);
                headlight.transform.localPosition = new Vector3(0f, 1.48f, -length * 0.53f);
                Light light = headlight.GetComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.78f, 0.45f);
                light.range = 9f;
                light.intensity = 1.15f;
                light.shadows = LightShadows.None;
                light.enabled = false;
            }
        }

        private static void CreateScenicLamp(Transform parent, Vector3 localPosition, float range = 6.5f, float intensity = 0.64f)
        {
            GameObject lamp = new GameObject("ScenicNightLight", typeof(Light));
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = localPosition;
            Light light = lamp.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.42f);
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.enabled = false;
        }

        private static GameObject Primitive(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Component collider = go.GetComponent("Collider");
            if (collider != null)
            {
                if (Application.isPlaying) Object.Destroy(collider);
                else Object.DestroyImmediate(collider);
            }
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return go;
        }

        private static Material Material(string name, Color color)
        {
            return CabShaders.CreateLit(name, color);
        }

        private static Material CreateAsphaltMaterial(string name, Vector2 tileScale)
        {
            return CreateTiledMaterial(name, Color.white, "Cab3D/RoadAsphalt_v2", tileScale, 0.06f);
        }

        private static Material CreateTiledMaterial(string name, Color color, string resourcePath, Vector2 tileScale, float glossiness)
        {
            Material material = Material(name, color);
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture != null)
            {
                texture.wrapMode = TextureWrapMode.Repeat;
                texture.filterMode = FilterMode.Trilinear;
                texture.anisoLevel = 4;
                material.mainTexture = texture;
                material.mainTextureScale = tileScale;
            }
            CabShaders.SetSmoothness(material, glossiness);
            return material;
        }
    }
}
