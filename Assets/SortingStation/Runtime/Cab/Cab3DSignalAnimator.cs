using UnityEngine;

namespace SortingStation
{
    /// <summary>Small, cheap animation for a physical trackside signal.  It deliberately uses
    /// materials already on the model, so a signal stays readable on Android without post FX.</summary>
    public sealed class Cab3DSignalAnimator : MonoBehaviour
    {
        [SerializeField] private Renderer redLamp;
        [SerializeField] private Renderer yellowLamp;
        [SerializeField] private Renderer greenLamp;
        [SerializeField] private Light glowLight;
        [SerializeField] private float phase;
        [SerializeField] [Range(0, 2)] private int activeAspect;

        public void Configure(Renderer red, Renderer yellow, Renderer green, Light light, float seed, int aspect)
        {
            redLamp = red;
            yellowLamp = yellow;
            greenLamp = green;
            glowLight = light;
            phase = seed;
            activeAspect = Mathf.Clamp(aspect, 0, 2);
        }

        private void Update()
        {
            float pulse = 0.76f + Mathf.Sin(Time.unscaledTime * 1.65f + phase) * 0.12f;
            float red = activeAspect == 0 ? pulse : 0.05f;
            float yellow = activeAspect == 1 ? pulse : 0.05f;
            float green = activeAspect == 2 ? pulse : 0.05f;
            SetLamp(redLamp, new Color(1f, 0.055f, 0.025f), red);
            SetLamp(yellowLamp, new Color(1f, 0.56f, 0.035f), yellow);
            SetLamp(greenLamp, new Color(0.05f, 0.92f, 0.28f), green);
            if (glowLight != null)
            {
                glowLight.color = activeAspect == 0 ? new Color(1f, 0.08f, 0.03f) :
                    activeAspect == 1 ? new Color(1f, 0.56f, 0.04f) : new Color(0.05f, 1f, 0.28f);
                glowLight.intensity = 0.55f + pulse * 0.65f;
            }
        }

        private static void SetLamp(Renderer renderer, Color color, float intensity)
        {
            if (renderer == null || renderer.sharedMaterial == null) return;
            Color lit = Color.Lerp(new Color(0.025f, 0.025f, 0.025f), color, intensity);
            renderer.sharedMaterial.color = lit;
        }
    }
}
