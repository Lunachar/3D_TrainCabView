using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// The train behind the driver in the immersive mode: the locomotive body around the cab and
    /// a few passenger coaches, following the track curve every frame. The locomotive body sits on
    /// a layer the driver's camera does not draw (it would only hide the cab) — the rear-view
    /// mirrors see it; the coaches are ordinary scenery.
    /// </summary>
    public sealed class CabTrainConsist
    {
        public const int ExteriorLayer = 29;
        public const float LocomotiveLength = 16f;
        public const float CoachLength = 20f;
        public const float CouplingGap = 1.2f;
        // The cab front is this far ahead of the driver's eye.
        public const float NoseAhead = 2.6f;
        public const int CoachCount = 3;

        private readonly List<Transform> cars = new List<Transform>();
        private readonly List<float> carCentreOffsets = new List<float>();

        public IReadOnlyList<Transform> Cars => cars;

        public CabTrainConsist(Transform routeRoot)
        {
            Material body = CabPbrMaterials.Get("Plastic010", new Color(0.72f, 0.12f, 0.10f), 0.6f);
            Material stripe = CabPbrMaterials.Get("Plastic010", new Color(0.92f, 0.90f, 0.86f), 0.6f);
            Material glass = CabPbrMaterials.Plain("ConsistWindowGlass", new Color(0.05f, 0.08f, 0.10f), 0.9f, 0.2f);
            Material metal = CabPbrMaterials.Get("Metal046A", new Color(0.30f, 0.30f, 0.32f), 0.7f);
            Material roof = CabPbrMaterials.Get("Metal032", new Color(0.62f, 0.64f, 0.66f), 0.7f);
            Material door = CabPbrMaterials.Get("Plastic010", new Color(0.84f, 0.80f, 0.20f), 0.6f);

            float offset = NoseAhead - LocomotiveLength * 0.5f;
            AddCar(routeRoot, BuildCar("ConsistLocomotive", LocomotiveLength, true, body, stripe, glass, metal, roof, door), offset, ExteriorLayer);
            offset -= LocomotiveLength * 0.5f;
            for (int i = 0; i < CoachCount; i++)
            {
                offset -= CouplingGap + CoachLength * 0.5f;
                AddCar(routeRoot, BuildCar("ConsistCoach_" + (i + 1), CoachLength, false, body, stripe, glass, metal, roof, door), offset, 0);
                offset -= CoachLength * 0.5f;
            }
        }

        private void AddCar(Transform routeRoot, GameObject car, float centreOffset, int layer)
        {
            car.transform.SetParent(routeRoot, false);
            SetLayer(car, layer);
            cars.Add(car.transform);
            carCentreOffsets.Add(centreOffset);
        }

        /// <summary>Places every car on the track relative to the train's route distance.</summary>
        public void UpdatePose(float routeDistance, float cycle)
        {
            float cycleLength = Mathf.Max(100f, cycle);
            float here = Mathf.Repeat(routeDistance, cycleLength);
            for (int i = 0; i < cars.Count; i++)
            {
                float d = routeDistance + carCentreOffsets[i];
                // Sample the car's front and back on the track so it follows curves like bogies.
                float half = (i == 0 ? LocomotiveLength : CoachLength) * 0.42f;
                Vector3 front = Unwrapped(d + half, here, cycleLength);
                Vector3 back = Unwrapped(d - half, here, cycleLength);
                Transform car = cars[i];
                car.localPosition = (front + back) * 0.5f;
                Vector3 direction = front - back;
                car.localRotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction, Vector3.up) : Quaternion.identity;
            }
        }

        /// <summary>Places every car along an open (non-looping) route given by <paramref name="point"/>.</summary>
        public void UpdatePose(float routeDistance, System.Func<float, Vector3> point)
        {
            for (int i = 0; i < cars.Count; i++)
            {
                float d = routeDistance + carCentreOffsets[i];
                float half = (i == 0 ? LocomotiveLength : CoachLength) * 0.42f;
                Vector3 front = point(d + half);
                // Before the route start the path simply continues straight back.
                Vector3 back = point(d - half);
                Transform car = cars[i];
                car.localPosition = (front + back) * 0.5f;
                Vector3 direction = front - back;
                car.localRotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction, Vector3.up) : Quaternion.identity;
            }
        }

        /// <summary>Track point kept on the same side of the loop seam as the train itself.</summary>
        private static Vector3 Unwrapped(float distance, float here, float cycle)
        {
            Vector3 point = Cab3DTrackMath.Point(distance, cycle);
            if (point.z - here > cycle * 0.5f) point.z -= cycle;
            else if (here - point.z > cycle * 0.5f) point.z += cycle;
            return point;
        }

        /// <summary>World-space position of a coach door on the platform (left) side.</summary>
        public Vector3 DoorPosition(int coachIndex, int doorIndex)
        {
            Transform coach = cars[Mathf.Clamp(coachIndex + 1, 1, cars.Count - 1)];
            float along = doorIndex == 0 ? CoachLength * 0.32f : -CoachLength * 0.32f;
            return coach.TransformPoint(new Vector3(-1.55f, 0.5f, along));
        }

        private static GameObject BuildCar(string name, float length, bool locomotive, Material body, Material stripe,
            Material glass, Material metal, Material roof, Material door)
        {
            GameObject car = new GameObject(name);
            const float width = 3.05f;
            const float floor = 1.25f;
            const float height = 3.1f;
            CabMeshBuilder shell = new CabMeshBuilder(1f);
            shell.AddChamferBox(new Vector3(0f, floor + height * 0.5f, 0f), new Vector3(width, height, length), 0.12f, Quaternion.identity);
            Part(car, "Body", shell, body);

            CabMeshBuilder band = new CabMeshBuilder(1f);
            for (int side = -1; side <= 1; side += 2)
                band.AddBox(new Vector3(side * (width * 0.5f + 0.005f), floor + 0.55f, 0f), new Vector3(0.01f, 0.22f, length - 0.4f), Quaternion.identity);
            Part(car, "Stripe", band, stripe);

            CabMeshBuilder windows = new CabMeshBuilder(1f);
            CabMeshBuilder doors = new CabMeshBuilder(1f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (width * 0.5f + 0.012f);
                if (locomotive)
                {
                    windows.AddBox(new Vector3(x, floor + 2.1f, length * 0.5f - 1.6f), new Vector3(0.01f, 0.8f, 1.1f), Quaternion.identity);
                    doors.AddBox(new Vector3(x, floor + 1.3f, length * 0.5f - 3.2f), new Vector3(0.012f, 2.2f, 0.9f), Quaternion.identity);
                    for (int g = 0; g < 4; g++)
                        windows.AddBox(new Vector3(x, floor + 2.2f, -1.5f - g * 2.2f), new Vector3(0.01f, 0.5f, 1.6f), Quaternion.identity);
                }
                else
                {
                    for (int w = 0; w < 7; w++)
                        windows.AddBox(new Vector3(x, floor + 1.9f, -6.3f + w * 2.1f), new Vector3(0.01f, 0.95f, 1.5f), Quaternion.identity);
                    for (int d = -1; d <= 1; d += 2)
                        doors.AddBox(new Vector3(x, floor + 1.25f, d * length * 0.32f), new Vector3(0.014f, 2.3f, 1.3f), Quaternion.identity);
                }
            }
            Part(car, "Windows", windows, glass);
            Part(car, "Doors", doors, door);

            CabMeshBuilder top = new CabMeshBuilder(1f);
            top.AddChamferBox(new Vector3(0f, floor + height + 0.12f, 0f), new Vector3(width - 0.4f, 0.24f, length - 0.6f), 0.08f, Quaternion.identity);
            Part(car, "Roof", top, roof);

            CabMeshBuilder under = new CabMeshBuilder(1f);
            for (int b = -1; b <= 1; b += 2)
            {
                float z = b * length * 0.34f;
                under.AddBox(new Vector3(0f, 0.72f, z), new Vector3(2.3f, 0.5f, 2.6f), Quaternion.identity);
                for (int axle = -1; axle <= 1; axle += 2)
                    for (int side = -1; side <= 1; side += 2)
                        under.AddCylinder(new Vector3(side * 0.76f, 0.46f, z + axle * 0.9f), 0.46f, 0.14f, 14, Quaternion.Euler(0f, 0f, 90f));
            }
            under.AddBox(new Vector3(0f, 1.05f, 0f), new Vector3(2.6f, 0.3f, length - 1f), Quaternion.identity);
            Part(car, "Underframe", under, metal);
            return car;
        }

        private static void Part(GameObject car, string name, CabMeshBuilder builder, Material material)
        {
            GameObject part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(car.transform, false);
            part.GetComponent<MeshFilter>().sharedMesh = builder.Build(car.name + "_" + name);
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void SetLayer(GameObject value, int layer)
        {
            value.layer = layer;
            foreach (Transform child in value.transform) SetLayer(child.gameObject, layer);
        }
    }
}
