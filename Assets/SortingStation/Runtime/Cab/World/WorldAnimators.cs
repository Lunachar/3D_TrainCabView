using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    /// <summary>The station attendant raises the flag and waves to the driver every few seconds.</summary>
    public sealed class AttendantWave : MonoBehaviour
    {
        private Transform arm;
        private Quaternion rest;
        private float nextWave;
        private float waveStart = -10f;

        public void Configure(Transform[] limbs)
        {
            arm = limbs != null && limbs.Length > 1 ? limbs[1] : null;
            if (arm != null) rest = arm.localRotation;
            nextWave = Time.time + Random.Range(1f, 5f);
        }

        private void Update()
        {
            if (arm == null) return;
            if (Time.time >= nextWave)
            {
                waveStart = Time.time;
                nextWave = Time.time + Random.Range(5f, 11f);
            }
            float t = (Time.time - waveStart) / 2.6f;
            float lift = t >= 0f && t <= 1f ? Mathf.Sin(t * Mathf.PI) : 0f;
            float swing = Mathf.Sin(Time.time * 9f) * 22f * lift;
            arm.localRotation = rest * Quaternion.Euler(swing, 0f, -150f * Mathf.Clamp01(lift * 1.6f));
        }
    }

    /// <summary>An LED screen slowly cycling through colours.</summary>
    public sealed class ColorCycle : MonoBehaviour
    {
        private Material material;
        private float phase;

        public void Configure(Material target, float startPhase)
        {
            material = target;
            phase = startPhase;
        }

        private void Update()
        {
            if (material == null) return;
            float hue = Mathf.Repeat(phase + Time.time * 0.08f, 1f);
            material.SetColor("_EmissionColor", Color.HSVToRGB(hue, 0.75f, 1f) * 1.6f);
        }
    }

    /// <summary>Spins a part around a local axis (turbine rotors, carousels, crane jibs).</summary>
    public sealed class Spinner : MonoBehaviour
    {
        private Vector3 axis = Vector3.forward;
        private float degreesPerSecond = 30f;

        public void Configure(Vector3 localAxis, float speed)
        {
            axis = localAxis;
            degreesPerSecond = speed;
        }

        private void Update() => transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }

    /// <summary>Alternating flashers: police and ambulance light bars, radio mast beacons.</summary>
    public sealed class Blinker : MonoBehaviour
    {
        private readonly List<Renderer> first = new List<Renderer>();
        private readonly List<Renderer> second = new List<Renderer>();
        private Material onA, onB, off;
        private float period = 0.5f;

        public void Configure(Material colourA, Material colourB, Material dark, float seconds)
        {
            onA = colourA;
            onB = colourB;
            off = dark;
            period = seconds;
        }

        public void Add(Renderer renderer, bool firstGroup) => (firstGroup ? first : second).Add(renderer);

        private void Update()
        {
            bool phase = Mathf.Repeat(Time.time / period, 1f) < 0.5f;
            foreach (Renderer r in first) if (r != null) r.sharedMaterial = phase ? onA : off;
            foreach (Renderer r in second) if (r != null) r.sharedMaterial = phase ? off : onB;
        }
    }

    /// <summary>Night fireworks over a station: bursts of glowing sparks that fly out and fade.</summary>
    public sealed class FireworkShow : MonoBehaviour
    {
        private readonly List<Transform> sparks = new List<Transform>();
        private readonly List<Vector3> velocities = new List<Vector3>();
        private Material[] colours;
        private float age = 10f;
        private float nextBurst;

        private void Start()
        {
            colours = new[]
            {
                WorldMaterials.Glow("Spark0", new Color(1f, 0.3f, 0.3f), 3f), WorldMaterials.Glow("Spark1", new Color(0.3f, 1f, 0.4f), 3f),
                WorldMaterials.Glow("Spark2", new Color(1f, 0.85f, 0.3f), 3f), WorldMaterials.Glow("Spark3", new Color(0.5f, 0.5f, 1f), 3f)
            };
            for (int i = 0; i < 28; i++)
            {
                GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(spark.GetComponent<Collider>());
                spark.transform.SetParent(transform, false);
                spark.transform.localScale = Vector3.one * 0.35f;
                spark.SetActive(false);
                sparks.Add(spark.transform);
                velocities.Add(Vector3.zero);
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (Time.time >= nextBurst)
            {
                nextBurst = Time.time + Random.Range(1.2f, 2.4f);
                age = 0f;
                Vector3 centre = new Vector3(Random.Range(-15f, 15f), Random.Range(28f, 40f), Random.Range(-20f, 20f));
                Material colour = colours[Random.Range(0, colours.Length)];
                for (int i = 0; i < sparks.Count; i++)
                {
                    sparks[i].localPosition = centre;
                    sparks[i].GetComponent<Renderer>().sharedMaterial = colour;
                    sparks[i].gameObject.SetActive(true);
                    velocities[i] = Random.onUnitSphere * Random.Range(6f, 10f);
                }
            }
            float fade = Mathf.Clamp01(1f - age / 1.6f);
            for (int i = 0; i < sparks.Count; i++)
            {
                if (!sparks[i].gameObject.activeSelf) continue;
                velocities[i] += Vector3.down * 4f * Time.deltaTime;
                sparks[i].localPosition += velocities[i] * Time.deltaTime;
                sparks[i].localScale = Vector3.one * 0.35f * fade;
                if (fade <= 0f) sparks[i].gameObject.SetActive(false);
            }
        }
    }
}
