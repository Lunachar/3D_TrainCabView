using UnityEngine;

namespace SortingStation
{
    /// <summary>Moves a lightweight roadside vehicle along a local road strip.  The vehicle is
    /// intentionally transform-only: dozens can run on an Android tablet without physics.</summary>
    public sealed class Cab3DRoadVehicleAnimator : MonoBehaviour
    {
        [SerializeField] private float startZ;
        [SerializeField] private float endZ;
        [SerializeField] private float metresPerSecond = 7f;
        [SerializeField] private bool reverse;
        [SerializeField] private Transform[] wheels;
        [SerializeField] private Vector3 localTravelAxis = Vector3.forward;
        [SerializeField] private bool trackAligned;
        [SerializeField] private float trackCycleLength;
        [SerializeField] private float trackLateralOffset;
        [SerializeField] private float trackHeight = 0.48f;
        private Vector3 basePosition;
        private float z;

        private void Awake()
        {
            // Runtime-created vehicles are configured immediately after AddComponent, while an
            // editable prefab keeps only its serialized route values.  Initialise both cases.
            z = reverse ? endZ : startZ;
            basePosition = transform.localPosition - localTravelAxis * z;
        }

        public void Configure(float from, float to, float speed, bool backwards, Transform[] wheelTransforms)
        {
            startZ = from;
            endZ = to;
            metresPerSecond = Mathf.Max(0.1f, speed);
            reverse = backwards;
            wheels = wheelTransforms;
            localTravelAxis = Vector3.forward;
            Vector3 position = transform.localPosition;
            basePosition = position - localTravelAxis * Vector3.Dot(position, localTravelAxis);
            z = backwards ? endZ : startZ;
            transform.localPosition = basePosition + localTravelAxis * z;
        }

        public void ConfigureAcross(float from, float to, float speed, bool backwards)
        {
            startZ = from;
            endZ = to;
            metresPerSecond = Mathf.Max(0.1f, speed);
            reverse = backwards;
            localTravelAxis = Vector3.right;
            Vector3 position = transform.localPosition;
            basePosition = position - localTravelAxis * Vector3.Dot(position, localTravelAxis);
            z = backwards ? endZ : startZ;
            transform.localPosition = basePosition + localTravelAxis * z;
        }

        public void ConfigureAlongTrack(float fromDistance, float toDistance, float cycleLength,
            float lateralOffset, float height, float speed, bool backwards, Transform[] wheelTransforms)
        {
            startZ = fromDistance;
            endZ = toDistance;
            trackCycleLength = Mathf.Max(1f, cycleLength);
            trackLateralOffset = lateralOffset;
            trackHeight = height;
            metresPerSecond = Mathf.Max(0.1f, speed);
            reverse = backwards;
            wheels = wheelTransforms;
            trackAligned = true;
            z = backwards ? endZ : startZ;
            ApplyTrackPose(z);
        }

        private void Update()
        {
            float direction = reverse ? -1f : 1f;
            z += direction * metresPerSecond * Time.unscaledDeltaTime;
            if (!reverse && z > endZ) z = startZ;
            else if (reverse && z < startZ) z = endZ;
            if (trackAligned)
                ApplyTrackPose(z);
            else
            {
                Vector3 p = basePosition + localTravelAxis * z;
                p.y = basePosition.y + Mathf.Sin(Time.unscaledTime * 7f + startZ) * 0.012f;
                transform.localPosition = p;
            }
            if (wheels == null) return;
            // The wheel mesh is a Y-axis cylinder rotated 90° around Z, so its axle is
            // the wheel's local up axis. A fixed local spin sign follows the authored -Z nose
            // after either vehicle yaw (and also on the perpendicular overpass).
            float rotation = -metresPerSecond * 48f * Time.unscaledDeltaTime;
            for (int i = 0; i < wheels.Length; i++) if (wheels[i] != null) wheels[i].Rotate(Vector3.up, rotation, Space.Self);
        }

        private void ApplyTrackPose(float distance)
        {
            Vector3 lateral = Cab3DTrackMath.Heading(distance, trackCycleLength) *
                              new Vector3(trackLateralOffset, trackHeight, 0f);
            transform.localPosition = Cab3DTrackMath.Point(distance, trackCycleLength) + lateral;
            Quaternion travelHeading = Cab3DTrackMath.Heading(distance, trackCycleLength);
            transform.localRotation = travelHeading * (reverse
                ? Quaternion.identity
                : Quaternion.Euler(0f, 180f, 0f));
        }
    }
}
