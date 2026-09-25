using UnityEngine;

namespace SortingStation
{
    public static class Cab3DTrackMath
    {
        public static Vector3 Point(float distance, float cycleLength)
        {
            float cycle = Mathf.Max(100f, cycleLength);
            float z = Mathf.Repeat(distance, cycle);
            float t = z / cycle;
            float x = 0f;
            if (t >= 0.28f && t < 0.44f) x = 12f * Smooth01(Mathf.InverseLerp(0.28f, 0.44f, t));
            else if (t >= 0.44f && t < 0.64f) x = 12f;
            else if (t >= 0.64f && t < 0.82f) x = 12f * (1f - Smooth01(Mathf.InverseLerp(0.64f, 0.82f, t)));
            return new Vector3(x, 0f, z);
        }

        public static Vector3 Tangent(float distance, float cycleLength)
        {
            float step = 0.35f;
            float cycle = Mathf.Max(100f, cycleLength);
            // Point is cyclic, but its Z coordinate is intentionally stored in the 0..cycle
            // authoring range. At distance zero the old central difference compared Z=0.35 with
            // Z=cycle-0.35 and therefore returned a huge *backward* vector for one frame. Rebase
            // the wrapped forward sample before subtraction, so the train faces forward from the
            // very first rendered frame instead of flipping the entire route on frame two.
            Vector3 before = Point(distance - step, cycle);
            Vector3 after = Point(distance + step, cycle);
            if (after.z < before.z) after.z += cycle;
            Vector3 tangent = after - before;
            tangent.y = 0f;
            return tangent.sqrMagnitude < 0.0001f ? Vector3.forward : tangent.normalized;
        }

        public static Quaternion Heading(float distance, float cycleLength)
        {
            return Quaternion.LookRotation(Tangent(distance, cycleLength), Vector3.up);
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
