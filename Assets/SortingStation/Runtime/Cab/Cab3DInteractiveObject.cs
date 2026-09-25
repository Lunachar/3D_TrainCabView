using UnityEngine;

namespace SortingStation
{
    public sealed class Cab3DInteractiveObject : MonoBehaviour
    {
        [SerializeField] private string interactionId = "worker";
        [SerializeField] private CabInteractionReaction defaultReaction = CabInteractionReaction.Wave;
        private float reactionRemaining;
        private float duration;
        private Quaternion initialRotation;
        private Light[] reactionLights;
        private float[] lightIntensities;
        private Cab3DBirdFlockAnimator birdFlock;
        private Cab3DContextAnimator contextAnimator;

        public string InteractionId => interactionId;

        private void Awake()
        {
            initialRotation = transform.localRotation;
            reactionLights = GetComponentsInChildren<Light>(true);
            lightIntensities = new float[reactionLights.Length];
            for (int i = 0; i < reactionLights.Length; i++)
                lightIntensities[i] = reactionLights[i] != null ? reactionLights[i].intensity : 0f;
            birdFlock = GetComponent<Cab3DBirdFlockAnimator>();
            contextAnimator = GetComponent<Cab3DContextAnimator>();
        }

        public void Configure(string id, CabInteractionReaction reaction)
        {
            interactionId = id;
            defaultReaction = reaction;
            initialRotation = transform.localRotation;
        }

        public void React(CabInteractionReaction reaction, float seconds)
        {
            defaultReaction = reaction;
            duration = Mathf.Max(0.2f, seconds);
            reactionRemaining = duration;
            if (reaction == CabInteractionReaction.FlyAway) birdFlock?.Startle(duration);
            if (contextAnimator == null) contextAnimator = GetComponent<Cab3DContextAnimator>();
            contextAnimator?.Trigger(reaction, duration);
        }

        private void Update()
        {
            if (reactionRemaining <= 0f) return;
            reactionRemaining = Mathf.Max(0f, reactionRemaining - Time.unscaledDeltaTime);
            if (defaultReaction == CabInteractionReaction.ReplyLight || defaultReaction == CabInteractionReaction.RevealLights)
            {
                PulseLights();
            }
            else if (defaultReaction != CabInteractionReaction.FlyAway && contextAnimator == null)
            {
                float amount = Mathf.Sin((duration - reactionRemaining) * 8f) * Mathf.Clamp01(reactionRemaining / duration);
                transform.localRotation = initialRotation * Quaternion.Euler(0f, 0f, amount * 18f);
            }
            if (reactionRemaining <= 0f)
            {
                transform.localRotation = initialRotation;
                RestoreLights();
            }
        }

        private void PulseLights()
        {
            if (reactionLights == null) return;
            float pulse = 1.1f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9f)) * 1.8f;
            for (int i = 0; i < reactionLights.Length; i++)
            {
                Light light = reactionLights[i];
                if (light == null) continue;
                light.enabled = true;
                light.intensity = Mathf.Max(lightIntensities[i], pulse);
            }
        }

        private void RestoreLights()
        {
            if (reactionLights == null) return;
            for (int i = 0; i < reactionLights.Length; i++)
            {
                Light light = reactionLights[i];
                if (light == null) continue;
                light.intensity = lightIntensities[i];
            }
        }
    }
}
