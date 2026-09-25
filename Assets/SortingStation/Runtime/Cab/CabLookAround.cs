using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SortingStation
{
    /// <summary>
    /// Lets the driver glance around the cab: drag on free screen space (not on a control) to
    /// turn the head, release to let the view spring back to the track ahead. Drags that start
    /// on UI elements are left to those elements.
    /// </summary>
    public sealed class CabLookAround : MonoBehaviour
    {
        public const float MaxYaw = 35f;
        public const float MaxPitch = 15f;
        /// <summary>
        /// The cab keeps a constant horizontal field of view, so the whole desk fits on every
        /// tablet: narrower screens (4:3) see more vertically instead of losing the side levers.
        /// </summary>
        public const float HorizontalFieldOfView = 84f;

        public static float VerticalFieldOfView(float aspect) =>
            Camera.HorizontalToVerticalFieldOfView(HorizontalFieldOfView, Mathf.Max(0.5f, aspect));
        private const float DegreesPerScreenHeight = 70f;
        private const float ReturnSeconds = 0.55f;

        private Quaternion restRotation;
        private float yaw;
        private float pitch;
        private float yawVelocity;
        private float pitchVelocity;
        private bool dragging;
        private Vector2 lastPosition;

        public float Yaw => yaw;
        public float Pitch => pitch;
        public bool IsDragging => dragging;

        private Camera driverCamera;

        private void Awake()
        {
            restRotation = transform.localRotation;
            driverCamera = GetComponent<Camera>();
            if (driverCamera != null) driverCamera.fieldOfView = VerticalFieldOfView(driverCamera.aspect);
        }

        private void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer != null)
            {
                bool pressed = pointer.press.isPressed;
                Vector2 position = pointer.position.ReadValue();
                if (pressed && !dragging && pointer.press.wasPressedThisFrame && !IsOverUi())
                {
                    dragging = true;
                    lastPosition = position;
                }
                else if (!pressed)
                {
                    dragging = false;
                }
                if (dragging)
                {
                    Vector2 delta = position - lastPosition;
                    lastPosition = position;
                    float scale = DegreesPerScreenHeight / Mathf.Max(1f, Screen.height);
                    ApplyDrag(delta * scale);
                }
            }

            if (!dragging)
            {
                yaw = Mathf.SmoothDamp(yaw, 0f, ref yawVelocity, ReturnSeconds, Mathf.Infinity, Time.unscaledDeltaTime);
                pitch = Mathf.SmoothDamp(pitch, 0f, ref pitchVelocity, ReturnSeconds, Mathf.Infinity, Time.unscaledDeltaTime);
            }
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * restRotation * Quaternion.Euler(-pitch, 0f, 0f);
            if (driverCamera != null) driverCamera.fieldOfView = VerticalFieldOfView(driverCamera.aspect);
        }

        /// <summary>Turns the head by a drag measured in degrees (x: right, y: up).</summary>
        public void ApplyDrag(Vector2 degrees)
        {
            // Dragging the scene to the left looks to the right, like moving a photo under a finger.
            yaw = Mathf.Clamp(yaw - degrees.x, -MaxYaw, MaxYaw);
            pitch = Mathf.Clamp(pitch - degrees.y, -MaxPitch, MaxPitch);
        }

        private static bool IsOverUi()
        {
            EventSystem events = EventSystem.current;
            if (events == null) return false;
            if (events.IsPointerOverGameObject()) return true;
            Touchscreen touch = Touchscreen.current;
            return touch != null && touch.primaryTouch.press.isPressed &&
                   events.IsPointerOverGameObject(touch.primaryTouch.touchId.ReadValue());
        }
    }
}
