using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// Editor-visible bridge between a physical cockpit prop and the existing accessible control.
    /// The gameplay UI keeps owning touch/keyboard input during the gradual 3D migration.
    /// </summary>
    public sealed class Cab3DControlAnchor : MonoBehaviour
    {
        [SerializeField] private CabControlAction action;
        [SerializeField] private Transform animatedPart;
        [SerializeField] private bool dispatcherAcknowledgement;

        public CabControlAction Action => action;
        public Transform AnimatedPart => animatedPart != null ? animatedPart : transform;
        public bool IsTouchable => name != "SpeedScreen3D" && name != "MessageScreen3D";
        public bool IsDispatcherAcknowledgement => dispatcherAcknowledgement || name == "VigilanceButton3D";

        public void Configure(CabControlAction value, Transform movingPart = null)
        {
            action = value;
            animatedPart = movingPart;
            EnsureHitArea();
        }

        public void ConfigureDispatcherAcknowledgement(Transform movingPart = null)
        {
            Configure(CabControlAction.DispatcherRadio, movingPart);
            dispatcherAcknowledgement = true;
        }

        public void EnsureHitArea()
        {
            if (!IsTouchable)
            {
                Collider obsoleteHitArea = GetComponent<Collider>();
                if (obsoleteHitArea != null) obsoleteHitArea.enabled = false;
                return;
            }
            BoxCollider hitArea = GetComponent<BoxCollider>();
            if (hitArea != null && hitArea.enabled) return;
            Collider[] previousHitAreas = GetComponents<Collider>();
            for (int i = 0; i < previousHitAreas.Length; i++)
                if (previousHitAreas[i] != null) previousHitAreas[i].enabled = false;
            hitArea = gameObject.AddComponent<BoxCollider>();
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                hitArea.size = Vector3.one * 0.45f;
                return;
            }

            bool initialized = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;
                Bounds source = renderer.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 local = new Vector3(
                        (corner & 1) == 0 ? source.min.x : source.max.x,
                        (corner & 2) == 0 ? source.min.y : source.max.y,
                        (corner & 4) == 0 ? source.min.z : source.max.z);
                    Vector3 point = transform.InverseTransformPoint(renderer.transform.TransformPoint(local));
                    if (!initialized)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        initialized = true;
                    }
                    else bounds.Encapsulate(point);
                }
            }

            if (!initialized)
            {
                hitArea.size = Vector3.one * 0.45f;
                return;
            }

            hitArea.center = bounds.center;
            hitArea.size = Vector3.Max(bounds.size + new Vector3(0.16f, 0.16f, 0.24f), new Vector3(0.22f, 0.22f, 0.28f));
        }
    }
}
