using UnityEngine;

namespace SortingStation
{
    public sealed class Cab3DAnimatedObject : MonoBehaviour
    {
        public enum MotionKind { Rotate, Drive, Bob, Fly }
        [SerializeField] private MotionKind motion = MotionKind.Rotate;
        [SerializeField] private Vector3 axis = Vector3.forward;
        [SerializeField] private float speed = 35f;
        [SerializeField] private float travel = 12f;
        private Vector3 origin;

        public void Configure(MotionKind kind, Vector3 motionAxis, float motionSpeed, float travelDistance = 12f)
        {
            motion = kind;
            axis = motionAxis;
            speed = motionSpeed;
            travel = travelDistance;
            origin = transform.localPosition;
        }

        private void Awake() => origin = transform.localPosition;

        private void Update()
        {
            switch (motion)
            {
                case MotionKind.Rotate:
                    transform.Rotate(axis, speed * Time.deltaTime, Space.Self);
                    break;
                case MotionKind.Drive:
                case MotionKind.Fly:
                    float phase = Mathf.Repeat(Time.time * Mathf.Max(0.01f, speed), 1f);
                    transform.localPosition = origin + axis.normalized * Mathf.Lerp(-travel, travel, phase);
                    break;
                case MotionKind.Bob:
                    transform.localPosition = origin + axis.normalized * (Mathf.Sin(Time.time * speed) * travel);
                    break;
            }
        }
    }
}
