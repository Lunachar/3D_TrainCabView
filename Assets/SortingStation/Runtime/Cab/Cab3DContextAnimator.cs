using UnityEngine;

namespace SortingStation
{
    /// <summary>Small reusable gesture for route animals and workers. It animates only the head or
    /// arm that should respond, so the whole object no longer tilts like a cardboard cut-out.</summary>
    public sealed class Cab3DContextAnimator : MonoBehaviour
    {
        [SerializeField] private Transform gesturePart;
        private Quaternion restRotation;
        private CabInteractionReaction reaction;
        private float duration;
        private float remaining;

        public void Configure(Transform target)
        {
            gesturePart = target;
            restRotation = gesturePart != null ? gesturePart.localRotation : Quaternion.identity;
        }

        public void Trigger(CabInteractionReaction value, float seconds)
        {
            if (gesturePart == null) return;
            reaction = value;
            duration = Mathf.Max(0.2f, seconds);
            remaining = duration;
            restRotation = gesturePart.localRotation;
        }

        private void Update()
        {
            if (remaining <= 0f || gesturePart == null) return;
            remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
            float time = duration - remaining;
            float strength = Mathf.Clamp01(remaining / duration);
            float angle = reaction == CabInteractionReaction.Wave
                ? Mathf.Sin(time * 9f) * 42f * strength
                : Mathf.Sin(time * 5.5f) * 18f * strength;
            gesturePart.localRotation = restRotation * (reaction == CabInteractionReaction.Wave
                ? Quaternion.Euler(0f, 0f, angle)
                : Quaternion.Euler(angle, 0f, 0f));
            if (remaining <= 0f) gesturePart.localRotation = restRotation;
        }
    }
}
