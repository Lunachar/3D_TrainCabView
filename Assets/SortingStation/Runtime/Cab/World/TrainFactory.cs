using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    public enum RailCarKind
    {
        ElectricLoco,
        EmuHead,
        EmuMiddle,
        Coach,
        BoxCar,
        Tank,
        Hopper,
        Gondola,
        ContainerFlat
    }

    public enum TrainKind
    {
        Suburban,
        Freight,
        Passenger
    }

    /// <summary>
    /// Real-size rail vehicles for the trains met on the other track: a suburban electric
    /// multiple unit, freight trains of mixed wagons and long-distance passenger trains.
    /// Meshes are cached per kind and livery; each car has one mesh with a submesh per material.
    /// </summary>
    public static class TrainFactory
    {
        public const float Width = 3.1f;
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();

        public static float Length(RailCarKind kind)
        {
            switch (kind)
            {
                case RailCarKind.ElectricLoco: return 16.5f;
                case RailCarKind.EmuHead: return 19.6f;
                case RailCarKind.EmuMiddle: return 19.6f;
                case RailCarKind.Coach: return 24.5f;
                case RailCarKind.Tank: return 12.1f;
                case RailCarKind.Hopper: return 14.7f;
                case RailCarKind.ContainerFlat: return 13.6f;
                default: return 13.9f;
            }
        }

        public static List<RailCarKind> Compose(TrainKind kind, System.Random random)
        {
            List<RailCarKind> cars = new List<RailCarKind>();
            switch (kind)
            {
                case TrainKind.Suburban:
                {
                    int middle = 2 + random.Next(0, 7);
                    cars.Add(RailCarKind.EmuHead);
                    for (int i = 0; i < middle; i++) cars.Add(RailCarKind.EmuMiddle);
                    cars.Add(RailCarKind.EmuHead);
                    break;
                }
                case TrainKind.Passenger:
                {
                    cars.Add(RailCarKind.ElectricLoco);
                    int coaches = 6 + random.Next(0, 8);
                    for (int i = 0; i < coaches; i++) cars.Add(RailCarKind.Coach);
                    break;
                }
                default:
                {
                    cars.Add(RailCarKind.ElectricLoco);
                    if (random.NextDouble() < 0.4) cars.Add(RailCarKind.ElectricLoco);
                    int wagons = 15 + random.Next(0, 26);
                    RailCarKind[] kinds = { RailCarKind.BoxCar, RailCarKind.Tank, RailCarKind.Hopper, RailCarKind.Gondola, RailCarKind.ContainerFlat };
                    // Wagons come in blocks of the same kind, as real freight trains are marshalled.
                    while (cars.Count < wagons)
                    {
                        RailCarKind block = kinds[random.Next(0, kinds.Length)];
                        int run = 2 + random.Next(0, 7);
                        for (int i = 0; i < run; i++) cars.Add(block);
                    }
                    break;
                }
            }
            return cars;
        }

        public static GameObject Create(Transform parent, RailCarKind kind, int livery, bool reversed)
        {
            GameObject car = new GameObject("RailCar_" + kind, typeof(MeshFilter), typeof(MeshRenderer));
            car.transform.SetParent(parent, false);
            Transform body = car.transform;
            car.GetComponent<MeshFilter>().sharedMesh = GetMesh(kind, reversed, livery);
            car.GetComponent<MeshRenderer>().sharedMaterials = Materials();
            return car;
        }

        /// <summary>Paint, accent, glass and underframe come from the palette; lamps and lit windows glow.</summary>
        public static Material[] Materials() => new[]
        {
            WorldPalette.Material,
            WorldMaterials.Headlamp,
            WorldMaterials.Glow("RailWindows", new Color(1f, 0.9f, 0.7f), 0f)
        };

        private static void Livery(RailCarKind kind, int livery, out Color main, out Color accent)
        {
            switch (kind)
            {
                case RailCarKind.ElectricLoco: main = livery % 3 == 0 ? new Color(0.72f, 0.12f, 0.10f) : livery % 3 == 1 ? new Color(0.18f, 0.40f, 0.26f) : new Color(0.20f, 0.30f, 0.55f); break;
                case RailCarKind.EmuHead:
                case RailCarKind.EmuMiddle: main = livery % 2 == 0 ? new Color(0.20f, 0.45f, 0.30f) : new Color(0.82f, 0.82f, 0.80f); break;
                case RailCarKind.Coach: main = livery % 2 == 0 ? new Color(0.20f, 0.32f, 0.22f) : new Color(0.55f, 0.12f, 0.12f); break;
                case RailCarKind.Tank: main = livery % 2 == 0 ? new Color(0.10f, 0.10f, 0.10f) : new Color(0.72f, 0.72f, 0.70f); break;
                case RailCarKind.Hopper: main = new Color(0.55f, 0.50f, 0.40f); break;
                case RailCarKind.Gondola: main = new Color(0.35f, 0.25f, 0.18f); break;
                case RailCarKind.ContainerFlat: main = new Color(0.25f, 0.25f, 0.26f); break;
                default: main = new Color(0.48f, 0.22f, 0.14f); break;
            }
            accent = kind == RailCarKind.ContainerFlat ? VehicleFactory.Paints[Mathf.Abs(livery * 3 + 1) % VehicleFactory.Paints.Length]
                : kind == RailCarKind.EmuHead || kind == RailCarKind.EmuMiddle ? new Color(0.85f, 0.2f, 0.12f) : new Color(0.88f, 0.86f, 0.80f);
        }

        public static Mesh GetMesh(RailCarKind kind, bool reversed, int livery = 0)
        {
            Livery(kind, livery, out Color main, out Color accent);
            string key = kind + (reversed ? "_r" : "") + ColorUtility.ToHtmlStringRGB(main) + ColorUtility.ToHtmlStringRGB(accent);
            if (Meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            Mesh mesh = Build(kind, reversed, main, accent);
            Meshes[key] = mesh;
            return mesh;
        }

        private static Mesh Build(RailCarKind kind, bool reversed, Color mainColour, Color accentColour)
        {
            float length = Length(kind);
            float half = length * 0.5f;
            const float floor = 1.25f;
            CabMeshBuilder main = new CabMeshBuilder(1f), accent = new CabMeshBuilder(1f), glass = new CabMeshBuilder(1f),
                under = new CabMeshBuilder(1f), lamp = new CabMeshBuilder(1f), lit = new CabMeshBuilder(1f);
            float front = reversed ? -1f : 1f;

            switch (kind)
            {
                case RailCarKind.ElectricLoco:
                case RailCarKind.EmuHead:
                case RailCarKind.EmuMiddle:
                case RailCarKind.Coach:
                {
                    float height = kind == RailCarKind.ElectricLoco ? 3.0f : 2.95f;
                    main.AddChamferBox(new Vector3(0f, floor + height * 0.5f, 0f), new Vector3(Width, height, length), 0.14f, Quaternion.identity);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float x = s * (Width * 0.5f + 0.01f);
                        accent.AddBox(new Vector3(x, floor + 0.45f, 0f), new Vector3(0.02f, 0.25f, length - 0.6f), Quaternion.identity);
                        if (kind == RailCarKind.ElectricLoco)
                        {
                            for (int g = 0; g < 5; g++) glass.AddBox(new Vector3(x, floor + 2.1f, -5f + g * 2f), new Vector3(0.02f, 0.5f, 1.4f), Quaternion.identity);
                        }
                        else
                        {
                            int windows = Mathf.FloorToInt((length - 5f) / 1.9f);
                            for (int w = 0; w < windows; w++)
                            {
                                float z = -length * 0.5f + 2.8f + w * 1.9f;
                                // Windows glow at night; the glass stays dark by day.
                                glass.AddBox(new Vector3(x, floor + 1.9f, z), new Vector3(0.02f, 0.85f, 1.4f), Quaternion.identity);
                                lit.AddBox(new Vector3(x * 1.001f, floor + 1.9f, z), new Vector3(0.018f, 0.8f, 1.35f), Quaternion.identity);
                            }
                            foreach (float z in new[] { -length * 0.5f + 1.2f, length * 0.5f - 1.2f })
                                accent.AddBox(new Vector3(x, floor + 1.2f, z), new Vector3(0.025f, 2.2f, 1.2f), Quaternion.identity);
                        }
                    }
                    main.AddChamferBox(new Vector3(0f, floor + height + 0.15f, 0f), new Vector3(Width - 0.5f, 0.3f, length - 0.8f), 0.1f, Quaternion.identity);
                    bool cab = kind == RailCarKind.ElectricLoco || kind == RailCarKind.EmuHead;
                    if (cab)
                    {
                        float z = front * (half + 0.01f);
                        glass.AddBox(new Vector3(0f, floor + 2.25f, z), new Vector3(Width - 0.5f, 0.9f, 0.04f), Quaternion.identity);
                        accent.AddBox(new Vector3(0f, floor + 1.1f, z), new Vector3(Width - 0.3f, 0.5f, 0.04f), Quaternion.identity);
                        lamp.AddBox(new Vector3(0f, floor + height - 0.05f, z + front * 0.02f), new Vector3(0.45f, 0.3f, 0.05f), Quaternion.identity);
                        for (int s = -1; s <= 1; s += 2) lamp.AddBox(new Vector3(s * 1.0f, floor + 0.55f, z + front * 0.02f), new Vector3(0.25f, 0.25f, 0.05f), Quaternion.identity);
                    }
                    if (kind == RailCarKind.ElectricLoco || kind == RailCarKind.EmuMiddle)
                        AddPantograph(under, floor + height + 0.3f, kind == RailCarKind.ElectricLoco ? length * 0.3f : 0f);
                    break;
                }
                case RailCarKind.Tank:
                    main.AddCylinder(new Vector3(0f, floor + 1.55f, 0f), 1.45f, length - 1.2f, 16, Quaternion.Euler(90f, 0f, 0f));
                    main.AddCylinder(new Vector3(0f, floor + 3.05f, 0f), 0.4f, 0.4f, 10, Quaternion.identity);
                    under.AddBox(new Vector3(0f, floor + 0.05f, 0f), new Vector3(2.6f, 0.2f, length - 0.5f), Quaternion.identity);
                    break;
                case RailCarKind.Hopper:
                    main.AddExtrudedProfile(new List<Vector2>
                    {
                        new Vector2(floor + 0.2f, -half + 2.4f), new Vector2(floor + 0.2f, half - 2.4f), new Vector2(floor + 3.4f, half - 0.4f), new Vector2(floor + 3.4f, -half + 0.4f)
                    }, -Width * 0.5f, Width * 0.5f, Vector3.zero);
                    for (int s = -1; s <= 1; s += 2)
                        for (float z = -half + 2f; z < half - 1.5f; z += 1.3f)
                            accent.AddBox(new Vector3(s * (Width * 0.5f + 0.03f), floor + 2.2f, z), new Vector3(0.06f, 2.3f, 0.12f), Quaternion.identity);
                    break;
                case RailCarKind.Gondola:
                {
                    main.AddBox(new Vector3(0f, floor + 1.1f, 0f), new Vector3(Width, 2.1f, length), Quaternion.identity);
                    // A load of coal just below the rim.
                    accent.AddBox(new Vector3(0f, floor + 2.05f, 0f), new Vector3(Width - 0.2f, 0.12f, length - 0.2f), Quaternion.identity);
                    for (int s = -1; s <= 1; s += 2)
                        for (float z = -half + 1f; z < half; z += 1.6f)
                            under.AddBox(new Vector3(s * (Width * 0.5f + 0.03f), floor + 1.1f, z), new Vector3(0.06f, 2.1f, 0.14f), Quaternion.identity);
                    break;
                }
                case RailCarKind.ContainerFlat:
                    under.AddBox(new Vector3(0f, floor + 0.1f, 0f), new Vector3(2.8f, 0.3f, length), Quaternion.identity);
                    accent.AddBox(new Vector3(0f, floor + 1.55f, -half * 0.5f), new Vector3(2.44f, 2.6f, 6.0f), Quaternion.identity);
                    main.AddBox(new Vector3(0f, floor + 1.55f, half * 0.5f), new Vector3(2.44f, 2.6f, 6.0f), Quaternion.identity);
                    break;
                default:
                    main.AddChamferBox(new Vector3(0f, floor + 1.7f, 0f), new Vector3(Width, 3.2f, length), 0.06f, Quaternion.identity);
                    for (int s = -1; s <= 1; s += 2)
                        under.AddBox(new Vector3(s * (Width * 0.5f + 0.02f), floor + 1.5f, 0f), new Vector3(0.04f, 2.6f, 2.8f), Quaternion.identity);
                    break;
            }

            // Underframe, bogies and buffers.
            under.AddBox(new Vector3(0f, floor - 0.12f, 0f), new Vector3(2.7f, 0.3f, length - 0.6f), Quaternion.identity);
            foreach (float z in new[] { half - 2.4f, -half + 2.4f })
            {
                under.AddBox(new Vector3(0f, 0.7f, z), new Vector3(2.3f, 0.45f, 2.7f), Quaternion.identity);
                for (int a = -1; a <= 1; a += 2)
                    for (int s = -1; s <= 1; s += 2)
                        under.AddCylinder(new Vector3(s * 0.76f, 0.46f, z + a * 0.95f), 0.46f, 0.14f, 12, Quaternion.Euler(0f, 0f, 90f));
            }
            foreach (float z in new[] { half, -half })
                for (int s = -1; s <= 1; s += 2)
                    under.AddCylinder(new Vector3(s * 0.88f, 1.05f, z), 0.18f, 0.5f, 8, Quaternion.Euler(90f, 0f, 0f));

            CombineInstance[] painted =
            {
                Part(main, WorldPalette.Uv(mainColour, 0.45f)), Part(accent, WorldPalette.Uv(accentColour, 0.45f)),
                Part(glass, WorldPalette.Uv(new Color(0.05f, 0.07f, 0.09f), 0.9f)), Part(under, WorldPalette.Uv(new Color(0.12f, 0.11f, 0.10f), 0.3f))
            };
            Mesh bodyMesh = new Mesh { name = "RailCarBody" };
            bodyMesh.CombineMeshes(painted, true, false);
            CombineInstance[] parts = { new CombineInstance { mesh = bodyMesh, transform = Matrix4x4.identity }, Part(lamp, Vector2.zero), Part(lit, Vector2.zero) };
            Mesh mesh = new Mesh { name = "RailCar_" + kind };
            mesh.CombineMeshes(parts, false, false);
            foreach (CombineInstance part in painted) DestroyTemporary(part.mesh);
            foreach (CombineInstance part in parts) DestroyTemporary(part.mesh);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddPantograph(CabMeshBuilder builder, float roof, float z)
        {
            builder.AddBox(new Vector3(0f, roof + 0.1f, z), new Vector3(1.6f, 0.12f, 1.8f), Quaternion.identity);
            builder.AddBox(new Vector3(0f, roof + 0.75f, z - 0.4f), new Vector3(0.08f, 0.08f, 1.8f), Quaternion.Euler(-40f, 0f, 0f));
            builder.AddBox(new Vector3(0f, roof + 1.45f, z), new Vector3(1.9f, 0.06f, 0.2f), Quaternion.identity);
        }

        private static CombineInstance Part(CabMeshBuilder builder, Vector2 paletteUv)
        {
            Mesh part = builder.Build("RailCarPart");
            if (part.vertexCount == 0)
            {
                part.vertices = new[] { Vector3.zero, Vector3.zero, Vector3.zero };
                part.normals = new[] { Vector3.up, Vector3.up, Vector3.up };
                part.triangles = new[] { 0, 1, 2 };
            }
            Vector2[] uvs = new Vector2[part.vertexCount];
            for (int i = 0; i < uvs.Length; i++) uvs[i] = paletteUv;
            part.uv = uvs;
            return new CombineInstance { mesh = part, transform = Matrix4x4.identity };
        }

        private static void DestroyTemporary(Mesh mesh)
        {
            if (Application.isPlaying) Object.Destroy(mesh);
            else Object.DestroyImmediate(mesh);
        }
    }
}
