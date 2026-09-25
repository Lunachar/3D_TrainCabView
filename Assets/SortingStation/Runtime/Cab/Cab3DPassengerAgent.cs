using UnityEngine;

namespace SortingStation
{
    /// <summary>Lightweight visual actor for a platform passenger.  No Animator/controller is needed.</summary>
    public sealed class Cab3DPassengerAgent : MonoBehaviour
    {
        [SerializeField] private Vector3 platformPosition;
        [SerializeField] private Vector3 coachDoorPosition = new Vector3(0f, 0f, -1.5f);
        [SerializeField] private float walkDuration = 2.4f;
        [SerializeField] private GameObject umbrella;
        [SerializeField] private Renderer coatRenderer;
        [SerializeField] private Transform[] animatedLimbs;
        private Quaternion[] limbRestRotations;
        private bool doorsOpen;
        private bool board;
        private bool alight;
        private bool wetWeather;
        private float progress;
        private Color baseCoatColor = Color.white;
        private bool hasBaseCoatColor;
        private Quaternion idleRestRotation;
        private float idlePhase;
        private bool idleWave;

        public void Configure(Vector3 platform, Vector3 door, GameObject umbrellaObject = null, Renderer coat = null, Transform[] limbs = null)
        {
            platformPosition = platform;
            coachDoorPosition = door;
            umbrella = umbrellaObject;
            coatRenderer = coat;
            if (coatRenderer != null && coatRenderer.sharedMaterial != null)
            {
                baseCoatColor = coatRenderer.sharedMaterial.color;
                hasBaseCoatColor = true;
            }
            animatedLimbs = limbs;
            if (animatedLimbs != null)
            {
                limbRestRotations = new Quaternion[animatedLimbs.Length];
                for (int i = 0; i < animatedLimbs.Length; i++) limbRestRotations[i] = animatedLimbs[i] != null ? animatedLimbs[i].localRotation : Quaternion.identity;
            }
            transform.localPosition = platformPosition;
            idleRestRotation = transform.localRotation;
            idlePhase = Mathf.Repeat(platformPosition.x * 0.73f + platformPosition.z * 0.19f, Mathf.PI * 2f);
            idleWave = transform.localScale.x < 0.8f || Mathf.Abs(Mathf.RoundToInt(platformPosition.z)) % 5 == 0;
        }

        public void SetPlatformVisible(bool visible)
        {
            if (!visible)
            {
                board = false;
                alight = false;
                gameObject.SetActive(false);
                return;
            }
            if (!gameObject.activeSelf)
            {
                transform.localPosition = platformPosition;
                gameObject.SetActive(true);
            }
            if (umbrella != null) umbrella.SetActive(wetWeather);
        }

        public void SetWetWeather(bool active)
        {
            wetWeather = active;
            if (umbrella != null) umbrella.SetActive(active && gameObject.activeInHierarchy);
        }

        public void ApplySeason(SeasonType season)
        {
            if (coatRenderer == null || coatRenderer.sharedMaterial == null) return;
            if (!hasBaseCoatColor)
            {
                baseCoatColor = coatRenderer.sharedMaterial.color;
                hasBaseCoatColor = true;
            }
            // Preserve the passenger's own coat colour: a season should change the wardrobe's
            // warmth, not turn the whole platform into one uniform-coloured group.
            Color seasonalTone = season == SeasonType.Winter ? new Color(0.20f, 0.31f, 0.52f) :
                season == SeasonType.Autumn ? new Color(0.72f, 0.30f, 0.10f) :
                season == SeasonType.Spring ? new Color(0.22f, 0.66f, 0.46f) : baseCoatColor;
            float influence = season == SeasonType.Summer ? 0f : 0.34f;
            coatRenderer.sharedMaterial.color = Color.Lerp(baseCoatColor, seasonalTone, influence);
        }

        public void SetFlow(bool boards, bool alights)
        {
            board = boards;
            alight = alights;
            progress = 0f;
            // The platform remains alive while only some people board or alight.
            gameObject.SetActive(true);
            transform.localPosition = alights ? coachDoorPosition : platformPosition;
        }

        public void SetDoorsOpen(bool value)
        {
            doorsOpen = value;
            if (!value && gameObject.activeSelf)
            {
                transform.localPosition = platformPosition;
                transform.localRotation = idleRestRotation;
                board = false;
                alight = false;
            }
        }

        private void Update()
        {
            if (!doorsOpen || (!board && !alight))
            {
                AnimateIdle();
                return;
            }
            progress = Mathf.Clamp01(progress + Time.deltaTime / Mathf.Max(0.2f, walkDuration));
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            Vector3 from = alight ? coachDoorPosition : platformPosition;
            Vector3 to = alight ? platformPosition : coachDoorPosition;
            transform.localPosition = Vector3.Lerp(from, to, eased);
            if (board && progress >= 1f)
            {
                // Reached the coach door: the passenger has stepped aboard.
                gameObject.SetActive(false);
                return;
            }
            transform.localRotation = Quaternion.Euler(0f, board ? 180f : 0f,
                Mathf.Sin(Time.time * 9f + transform.GetSiblingIndex()) * 2f);
            AnimateWalk(eased);
        }

        private void AnimateIdle()
        {
            float time = Time.unscaledTime + idlePhase;
            float shift = Mathf.Sin(time * 0.74f) * 1.25f;
            transform.localRotation = Quaternion.Slerp(transform.localRotation,
                idleRestRotation * Quaternion.Euler(0f, shift, Mathf.Sin(time * 0.56f) * 0.45f), Time.unscaledDeltaTime * 2.2f);
            if (animatedLimbs == null || limbRestRotations == null) return;
            for (int i = 0; i < animatedLimbs.Length; i++)
            {
                Transform limb = animatedLimbs[i];
                if (limb == null) continue;
                if (i >= 2)
                {
                    limb.localRotation = Quaternion.Slerp(limb.localRotation, limbRestRotations[i], Time.unscaledDeltaTime * 3.5f);
                    continue;
                }
                float armMotion = idleWave && i == 1
                    ? 24f + Mathf.Sin(time * 3.1f) * 30f
                    : Mathf.Sin(time * 0.9f + i * Mathf.PI) * 3.0f;
                limb.localRotation = Quaternion.Slerp(limb.localRotation,
                    limbRestRotations[i] * Quaternion.Euler(0f, 0f, armMotion), Time.unscaledDeltaTime * 4.5f);
            }
        }

        private void AnimateWalk(float walkProgress)
        {
            if (animatedLimbs == null || limbRestRotations == null) return;
            float swing = Mathf.Sin(walkProgress * Mathf.PI * 8f) * 24f;
            for (int i = 0; i < animatedLimbs.Length; i++)
            {
                Transform limb = animatedLimbs[i];
                if (limb == null) continue;
                float sign = i % 2 == 0 ? 1f : -1f;
                // Arms are the first pair, legs the second.  Opposite pairs sway together.
                float amount = i < 2 ? swing : -swing * 0.72f;
                limb.localRotation = limbRestRotations[i] * Quaternion.Euler(amount * sign, 0f, 0f);
            }
        }
    }
}
