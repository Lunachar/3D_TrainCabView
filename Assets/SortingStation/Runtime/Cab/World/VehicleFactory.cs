using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    public enum VehicleType
    {
        Sedan,
        Hatchback,
        Wagon,
        Suv,
        Van,
        Truck,
        Bus,
        OldSedan
    }

    /// <summary>
    /// Road vehicles built from side silhouettes: the body profile is extruded across the car's
    /// width, the glasshouse sits a little narrower on top, then wheels, lamps and bumpers.
    /// One mesh with a submesh per material, cached per type and paint.
    /// </summary>
    public static class VehicleFactory
    {
        public static readonly Color[] Paints =
        {
            new Color(0.82f, 0.82f, 0.80f), new Color(0.08f, 0.08f, 0.09f), new Color(0.45f, 0.46f, 0.48f), new Color(0.62f, 0.08f, 0.07f),
            new Color(0.10f, 0.20f, 0.45f), new Color(0.72f, 0.72f, 0.74f), new Color(0.15f, 0.35f, 0.22f), new Color(0.85f, 0.62f, 0.15f),
            new Color(0.55f, 0.52f, 0.46f), new Color(0.30f, 0.12f, 0.08f), new Color(0.90f, 0.90f, 0.88f), new Color(0.25f, 0.45f, 0.62f)
        };

        private static readonly Dictionary<int, Mesh> Meshes = new Dictionary<int, Mesh>();

        public static Vector3 Size(VehicleType type)
        {
            switch (type)
            {
                case VehicleType.Hatchback: return new Vector3(1.75f, 1.5f, 4.0f);
                case VehicleType.Wagon: return new Vector3(1.8f, 1.5f, 4.7f);
                case VehicleType.Suv: return new Vector3(1.9f, 1.78f, 4.7f);
                case VehicleType.Van: return new Vector3(2.0f, 2.3f, 5.2f);
                case VehicleType.Truck: return new Vector3(2.5f, 3.4f, 8.6f);
                case VehicleType.Bus: return new Vector3(2.55f, 3.1f, 11.5f);
                case VehicleType.OldSedan: return new Vector3(1.62f, 1.44f, 4.1f);
                default: return new Vector3(1.8f, 1.45f, 4.6f);
            }
        }

        public static GameObject Create(Transform parent, VehicleType type, int paint)
        {
            GameObject vehicle = new GameObject("Vehicle_" + type, typeof(MeshFilter), typeof(MeshRenderer));
            vehicle.transform.SetParent(parent, false);
            vehicle.GetComponent<MeshFilter>().sharedMesh = GetMesh(type, paint);
            MeshRenderer renderer = vehicle.GetComponent<MeshRenderer>();
            Color body = type == VehicleType.Bus ? (paint % 2 == 0 ? new Color(0.85f, 0.75f, 0.2f) : new Color(0.2f, 0.45f, 0.75f)) : Paints[Mathf.Abs(paint) % Paints.Length];
            renderer.sharedMaterials = new[]
            {
                WorldMaterials.Plain("CarPaint", body, 0.72f, 0.35f),
                WorldMaterials.Plain("CarGlass", new Color(0.06f, 0.08f, 0.10f), 0.92f, 0.2f),
                WorldMaterials.Plain("CarTrim", new Color(0.05f, 0.05f, 0.05f), 0.25f),
                WorldMaterials.Plain("CarChrome", new Color(0.7f, 0.7f, 0.72f), 0.8f, 0.9f),
                WorldMaterials.Headlamp,
                WorldMaterials.TailLamp
            };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return vehicle;
        }

        public static Mesh GetMesh(VehicleType type, int paint)
        {
            int key = (int)type;
            if (Meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            Mesh mesh = Build(type);
            Meshes[key] = mesh;
            return mesh;
        }

        public static int TriangleCount(VehicleType type) => GetMesh(type, 0).triangles.Length / 3;

        private static Mesh Build(VehicleType type)
        {
            Vector3 size = Size(type);
            float w = size.x, h = size.y, l = size.z;
            float halfL = l * 0.5f;
            CabMeshBuilder paint = new CabMeshBuilder(1f), glass = new CabMeshBuilder(1f), trim = new CabMeshBuilder(1f),
                chrome = new CabMeshBuilder(1f), head = new CabMeshBuilder(1f), tail = new CabMeshBuilder(1f);
            float wheelRadius = type == VehicleType.Truck || type == VehicleType.Bus ? 0.5f : type == VehicleType.Suv || type == VehicleType.Van ? 0.38f : 0.32f;
            float floor = wheelRadius * 0.9f;

            if (type == VehicleType.Truck)
            {
                // Cab at the front, a box body behind it.
                paint.AddChamferBox(new Vector3(0f, floor + 1.25f, halfL - 1.1f), new Vector3(w, 2.1f, 2.2f), 0.12f, Quaternion.identity);
                glass.AddBox(new Vector3(0f, floor + 1.75f, halfL + 0.005f), new Vector3(w - 0.3f, 0.8f, 0.02f), Quaternion.identity);
                for (int s = -1; s <= 1; s += 2) glass.AddBox(new Vector3(s * (w * 0.5f + 0.005f), floor + 1.7f, halfL - 0.9f), new Vector3(0.02f, 0.7f, 1f), Quaternion.identity);
                trim.AddBox(new Vector3(0f, floor + 1.7f, -0.9f), new Vector3(w + 0.05f, h - floor - 0.2f, l - 2.5f), Quaternion.identity);
                chrome.AddBox(new Vector3(0f, floor + 0.3f, -0.4f), new Vector3(0.9f, 0.25f, l - 2f), Quaternion.identity);
                AddLamps(head, tail, w, floor + 0.55f, halfL, -halfL + 0.05f);
            }
            else if (type == VehicleType.Bus)
            {
                paint.AddChamferBox(new Vector3(0f, floor + (h - floor) * 0.5f, 0f), new Vector3(w, h - floor, l), 0.15f, Quaternion.identity);
                for (int s = -1; s <= 1; s += 2)
                    glass.AddBox(new Vector3(s * (w * 0.5f + 0.005f), floor + 1.75f, 0.3f), new Vector3(0.02f, 1.1f, l - 1.6f), Quaternion.identity);
                glass.AddBox(new Vector3(0f, floor + 1.5f, halfL + 0.005f), new Vector3(w - 0.25f, 1.6f, 0.02f), Quaternion.identity);
                trim.AddBox(new Vector3(0f, h + 0.08f, -1f), new Vector3(1.6f, 0.2f, 3f), Quaternion.identity);
                AddLamps(head, tail, w, floor + 0.45f, halfL, -halfL + 0.05f);
            }
            else
            {
                // Proportions of the side silhouette: bonnet, windscreen, roof, rear.
                float bonnet = type == VehicleType.Van ? 0.12f : type == VehicleType.Hatchback ? 0.24f : 0.28f;
                float boot = type == VehicleType.Sedan || type == VehicleType.OldSedan ? 0.2f : 0.03f;
                float belt = type == VehicleType.Van ? h * 0.5f : h * 0.62f;
                float screen = type == VehicleType.Van ? 0.35f : type == VehicleType.OldSedan ? 0.45f : 0.7f;
                float rearScreen = type == VehicleType.Sedan ? 0.55f : type == VehicleType.OldSedan ? 0.4f : 0.18f;
                float front = halfL, rear = -halfL;
                List<Vector2> body = new List<Vector2>
                {
                    new Vector2(floor, rear + 0.1f), new Vector2(floor, front - 0.1f), new Vector2(floor + 0.25f, front),
                    new Vector2(belt - 0.05f, front - 0.08f), new Vector2(belt, front - l * bonnet),
                    new Vector2(belt, rear + l * boot), new Vector2(belt - 0.04f, rear + 0.05f), new Vector2(floor + 0.25f, rear)
                };
                paint.AddExtrudedProfile(body, -w * 0.5f, w * 0.5f, Vector3.zero);
                float roofFront = front - l * bonnet - screen, roofRear = rear + l * boot + rearScreen;
                List<Vector2> house = new List<Vector2>
                {
                    new Vector2(belt, rear + l * boot), new Vector2(belt, front - l * bonnet), new Vector2(h - 0.04f, roofFront), new Vector2(h - 0.04f, roofRear)
                };
                glass.AddExtrudedProfile(house, -w * 0.5f + 0.1f, w * 0.5f - 0.1f, Vector3.zero);
                paint.AddBox(new Vector3(0f, h - 0.02f, (roofFront + roofRear) * 0.5f), new Vector3(w - 0.16f, 0.05f, roofFront - roofRear + 0.05f), Quaternion.identity);
                // Pillars between the windows.
                for (int s = -1; s <= 1; s += 2)
                    paint.AddBox(new Vector3(s * (w * 0.5f - 0.08f), (belt + h) * 0.5f, (roofFront + roofRear) * 0.5f), new Vector3(0.03f, h - belt, 0.09f), Quaternion.identity);
                trim.AddBox(new Vector3(0f, floor + 0.12f, front - 0.02f), new Vector3(w - 0.05f, 0.24f, 0.12f), Quaternion.identity);
                trim.AddBox(new Vector3(0f, floor + 0.12f, rear + 0.02f), new Vector3(w - 0.05f, 0.24f, 0.12f), Quaternion.identity);
                chrome.AddBox(new Vector3(0f, floor + 0.42f, front + 0.02f), new Vector3(w * 0.45f, 0.14f, 0.03f), Quaternion.identity);
                AddLamps(head, tail, w, floor + 0.45f, front + 0.01f, rear - 0.01f);
            }

            // Wheels with hub caps.
            float axle = type == VehicleType.Bus ? l * 0.32f : type == VehicleType.Truck ? l * 0.36f : l * 0.32f;
            foreach (float z in new[] { axle, -axle })
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 hub = new Vector3(s * (w * 0.5f - 0.12f), wheelRadius, z);
                    trim.AddCylinder(hub, wheelRadius, 0.24f, 12, Quaternion.Euler(0f, 0f, 90f));
                    chrome.AddCylinder(hub + new Vector3(s * 0.125f, 0f, 0f), wheelRadius * 0.55f, 0.02f, 10, Quaternion.Euler(0f, 0f, 90f));
                }

            CombineInstance[] parts =
            {
                Part(paint, "Paint"), Part(glass, "Glass"), Part(trim, "Trim"), Part(chrome, "Chrome"), Part(head, "Head"), Part(tail, "Tail")
            };
            Mesh mesh = new Mesh { name = "Vehicle_" + type };
            mesh.CombineMeshes(parts, false, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddLamps(CabMeshBuilder head, CabMeshBuilder tail, float w, float y, float front, float rear)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                head.AddBox(new Vector3(s * (w * 0.5f - 0.28f), y + 0.12f, front), new Vector3(0.34f, 0.14f, 0.04f), Quaternion.identity);
                tail.AddBox(new Vector3(s * (w * 0.5f - 0.2f), y + 0.12f, rear), new Vector3(0.28f, 0.14f, 0.04f), Quaternion.identity);
            }
        }

        private static CombineInstance Part(CabMeshBuilder builder, string name)
        {
            Mesh part = builder.Build(name);
            // An empty part still needs a (degenerate) triangle so the submesh indices line up.
            if (part.vertexCount == 0)
            {
                part.vertices = new[] { Vector3.zero, Vector3.zero, Vector3.zero };
                part.triangles = new[] { 0, 1, 2 };
            }
            return new CombineInstance { mesh = part, transform = Matrix4x4.identity };
        }
    }
}
