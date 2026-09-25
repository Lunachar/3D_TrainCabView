using UnityEngine;

namespace SortingStation
{
    /// <summary>Moves each bird on a slightly different curved route and flaps its own wings.
    /// This avoids the old "five spheres sliding in a row" appearance.</summary>
    public sealed class Cab3DBirdFlockAnimator : MonoBehaviour
    {
        [SerializeField] private Transform[] birds;
        [SerializeField] private Transform[] leftWings;
        [SerializeField] private Transform[] rightWings;
        [SerializeField] private Vector3[] origins;
        [SerializeField] private float speed = 0.7f;
        [SerializeField] private float span = 10f;
        [SerializeField] private float phase;
        private float startledUntil;

        public void Configure(Transform[] birdTransforms, Transform[] left, Transform[] right, float speed01, float travel, float seed)
        {
            birds = birdTransforms;
            leftWings = left;
            rightWings = right;
            speed = Mathf.Max(0.05f, speed01);
            span = Mathf.Max(1f, travel);
            phase = seed;
            origins = new Vector3[birds.Length];
            for (int i = 0; i < birds.Length; i++) origins[i] = birds[i] != null ? birds[i].localPosition : Vector3.zero;
        }

        public void Startle(float seconds)
        {
            startledUntil = Mathf.Max(startledUntil, Time.unscaledTime + Mathf.Max(0.5f, seconds));
        }

        private void Update()
        {
            if (birds == null || origins == null) return;
            float time = Time.unscaledTime * speed + phase;
            bool startled = Time.unscaledTime < startledUntil;
            float flightScale = startled ? 2.35f : 1f;
            for (int i = 0; i < birds.Length; i++)
            {
                Transform bird = birds[i];
                if (bird == null) continue;
                float individual = time + i * 0.71f;
                float travel = Mathf.Repeat(individual * 0.16f * flightScale, 1f) * span - span * 0.5f;
                float climb = startled ? Mathf.Clamp01((startledUntil - Time.unscaledTime) * 0.45f) * 2.8f : 0f;
                bird.localPosition = origins[i] + new Vector3(travel, Mathf.Sin(individual * 1.7f * flightScale) * 0.65f + climb,
                    Mathf.Cos(individual * 0.65f) * 0.8f);
                // The model faces local +Z, while the flock crosses local +X. Pitch it along its
                // own climb so birds stop looking like sideways static ornaments.
                float pitch = -Mathf.Cos(individual * 1.7f * flightScale) * 7.5f;
                float bank = Mathf.Sin(individual * 0.65f) * 6f;
                bird.localRotation = Quaternion.Euler(pitch, 90f, bank);
                float flap = Mathf.Sin(individual * 11f * flightScale) * (startled ? 48f : 34f);
                if (leftWings != null && i < leftWings.Length && leftWings[i] != null) leftWings[i].localRotation = Quaternion.Euler(0f, 0f, flap);
                if (rightWings != null && i < rightWings.Length && rightWings[i] != null) rightWings[i].localRotation = Quaternion.Euler(0f, 0f, -flap);
            }
        }
    }
}
