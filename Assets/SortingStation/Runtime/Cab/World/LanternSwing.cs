using UnityEngine;

namespace SortingStation
{
    /// <summary>The station attendant's lantern sways gently, as a hand-held lamp does.</summary>
    public sealed class LanternSwing : MonoBehaviour
    {
        private Transform lantern;
        private float phase;

        public void Configure(Transform lanternTransform)
        {
            lantern = lanternTransform;
            phase = Random.value * 10f;
        }

        private void Update()
        {
            if (lantern == null) return;
            float t = Time.time + phase;
            lantern.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.3f) * 9f, 0f, Mathf.Sin(t * 0.9f) * 6f);
        }
    }
}
