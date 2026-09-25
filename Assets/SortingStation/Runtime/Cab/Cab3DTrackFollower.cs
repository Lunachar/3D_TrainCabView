using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// Keeps a decorative train aligned with the same horizontal curve as the player route.
    /// It is deliberately transform-only: there is no physics workload on the tablet.
    /// </summary>
    public sealed class Cab3DTrackFollower : MonoBehaviour
    {
        [SerializeField, Min(100f)] private float cycleLength = 1120f;
        [SerializeField] private float distance;
        [SerializeField] private float lateralOffset = 4.65f;
        [SerializeField, Min(0.1f)] private float metresPerSecond = 16f;

        public void Configure(float cycle, float startDistance, float trackOffset, float speed)
        {
            cycleLength = Mathf.Max(100f, cycle);
            distance = startDistance;
            lateralOffset = trackOffset;
            metresPerSecond = Mathf.Max(0.1f, speed);
            ApplyPose();
        }

        private void Update()
        {
            // The train runs opposite to the player's direction, so it naturally approaches,
            // passes the cab and re-enters beyond the far end of the loop.
            distance = Mathf.Repeat(distance - metresPerSecond * Time.deltaTime, cycleLength);
            ApplyPose();
        }

        private void ApplyPose()
        {
            Quaternion routeHeading = Cab3DTrackMath.Heading(distance, cycleLength);
            transform.localPosition = Cab3DTrackMath.Point(distance, cycleLength) +
                                      routeHeading * Vector3.right * lateralOffset + Vector3.up * 0.35f;
            transform.localRotation = routeHeading * Quaternion.Euler(0f, 180f, 0f);
        }
    }
}
