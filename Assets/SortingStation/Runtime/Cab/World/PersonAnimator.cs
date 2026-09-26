using System;
using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// Brings a person to life: walking with swinging arms and bending knees, and between walks
    /// small everyday things on each person's own timer: shifting weight, looking around,
    /// checking the phone or the watch, stretching, scratching the head, strolling a few steps
    /// and back, waving at the train, chatting. Nobody moves in step with anybody else.
    /// </summary>
    public sealed class PersonAnimator : MonoBehaviour
    {
        public enum Activity { Stand, ShiftWeight, LookAround, Phone, Watch, Stretch, Scratch, Wave, Talk, Stroll, Sit, Walk }

        private Transform[] bones;
        private Quaternion[] rest;
        private Vector3 hipsRest;
        private PersonLook look;
        private System.Random random;
        private Activity activity = Activity.Stand;
        private float activityEnds;
        private float phase;
        private float walkPhase;
        private Vector3 target;
        private float speed;
        private Action arrived;
        private Vector3 home;
        private Quaternion homeFacing;
        private bool seated;
        private float nextCull;
        private bool culled;

        /// <summary>Box (parent space, x/z) the person may stroll within.</summary>
        public Vector2 StrollMin { get; set; } = new Vector2(-2f, -2f);
        public Vector2 StrollMax { get; set; } = new Vector2(2f, 2f);
        /// <summary>World point to look at (the arriving train), or null.</summary>
        public Vector3? LookAt { get; set; }
        public bool AllowStrolling { get; set; } = true;
        public bool IsWalking => activity == Activity.Walk;
        public Activity Current => activity;
        public float Height => look.Height;

        public void Configure(Transform[] skeleton, PersonLook personLook)
        {
            bones = skeleton;
            look = personLook;
            rest = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) rest[i] = bones[i].localRotation;
            hipsRest = bones[(int)PersonFactory.Bone.Hips].localPosition;
            random = new System.Random(GetInstanceID() * 7 + Environment.TickCount);
            phase = (float)random.NextDouble() * 100f;
            home = transform.localPosition;
            homeFacing = transform.localRotation;
            activityEnds = Time.time + (float)random.NextDouble() * 4f;
        }

        public Transform BoneOf(PersonFactory.Bone bone) => bones[(int)bone];

        public void SetHome(Vector3 localPosition, Quaternion facing)
        {
            home = localPosition;
            homeFacing = facing;
        }

        public void Sit(bool value)
        {
            seated = value;
            activity = value ? Activity.Sit : Activity.Stand;
        }

        /// <summary>Walk to a point in the parent's space, then call back.</summary>
        public void WalkTo(Vector3 localTarget, float metresPerSecond, Action onArrived = null)
        {
            seated = false;
            target = localTarget;
            speed = metresPerSecond;
            arrived = onArrived;
            activity = Activity.Walk;
        }

        public void Wave(float seconds = 3f)
        {
            if (activity == Activity.Walk) return;
            activity = Activity.Wave;
            activityEnds = Time.time + seconds;
        }

        private float R(float a, float b) => a + (float)random.NextDouble() * (b - a);

        private void Update()
        {
            if (bones == null) return;
            // Far away nobody sees the detail: stop animating beyond ~150 m.
            if (Time.time >= nextCull)
            {
                nextCull = Time.time + 1f;
                Camera eye = Camera.main;
                culled = eye != null && (eye.transform.position - transform.position).sqrMagnitude > 150f * 150f;
            }
            if (culled) return;
            float dt = Time.deltaTime;
            float t = Time.time + phase;

            if (activity == Activity.Walk) StepWalk(dt);
            else if (Time.time >= activityEnds) ChooseNext();

            Pose(t, dt);
        }

        private void StepWalk(float dt)
        {
            Vector3 position = transform.localPosition;
            Vector3 to = target - position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance < 0.05f)
            {
                activity = Activity.Stand;
                activityEnds = Time.time + R(1f, 4f);
                Action callback = arrived;
                arrived = null;
                callback?.Invoke();
                return;
            }
            float step = Mathf.Min(distance, speed * dt);
            transform.localPosition = position + to / distance * step;
            Quaternion facing = Quaternion.LookRotation(to / distance, Vector3.up);
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, facing, 360f * dt);
            walkPhase += step / Mathf.Max(0.4f, look.Height * 0.42f) * Mathf.PI;
        }

        private void ChooseNext()
        {
            if (seated)
            {
                activity = Activity.Sit;
                activityEnds = Time.time + R(4f, 10f);
                return;
            }
            float roll = (float)random.NextDouble();
            float away = (transform.localPosition - home).sqrMagnitude;
            if (away > 0.2f && roll < 0.5f)
            {
                // Wander back to where they were waiting.
                WalkTo(home, R(0.8f, 1.2f), () => transform.localRotation = homeFacing);
                return;
            }
            if (AllowStrolling && roll < 0.14f)
            {
                Vector3 spot = new Vector3(R(StrollMin.x, StrollMax.x), home.y, R(StrollMin.y, StrollMax.y));
                WalkTo(spot, R(0.7f, 1.1f));
                return;
            }
            activity = roll < 0.35f ? Activity.Stand : roll < 0.5f ? Activity.ShiftWeight : roll < 0.62f ? Activity.LookAround
                : roll < 0.72f ? Activity.Phone : roll < 0.78f ? Activity.Watch : roll < 0.83f ? Activity.Stretch
                : roll < 0.88f ? Activity.Scratch : roll < 0.95f ? Activity.Talk : Activity.Wave;
            activityEnds = Time.time + (activity == Activity.Stretch || activity == Activity.Scratch || activity == Activity.Watch ? R(2f, 3.5f) : R(3f, 9f));
        }

        private void Pose(float t, float dt)
        {
            float blend = Mathf.Clamp01(dt * 6f);
            Quaternion Q(float x, float y, float z) => Quaternion.Euler(x, y, z);
            // Defaults: a relaxed stance with gentle breathing.
            float breathe = Mathf.Sin(t * 1.7f) * 1.2f;
            Quaternion spine = Q(breathe * 0.5f, 0f, 0f), chest = Q(breathe, 0f, 0f), neck = Q(0f, 0f, 0f), head = Q(0f, 0f, 0f);
            Quaternion upperL = Q(2f, 0f, -6f), upperR = Q(2f, 0f, 6f), foreL = Q(-8f, 0f, 0f), foreR = Q(-8f, 0f, 0f);
            Quaternion thighL = Q(0f, 0f, -2f), thighR = Q(0f, 0f, 2f), shinL = Q(0f, 0f, 0f), shinR = Q(0f, 0f, 0f);
            Vector3 hips = hipsRest;
            float hipsTwist = 0f;

            switch (activity)
            {
                case Activity.Walk:
                {
                    float s = Mathf.Sin(walkPhase), c = Mathf.Cos(walkPhase);
                    float stride = 28f;
                    thighL = Q(-s * stride, 0f, 0f);
                    thighR = Q(s * stride, 0f, 0f);
                    shinL = Q(Mathf.Max(0f, -c) * 38f + 6f, 0f, 0f);
                    shinR = Q(Mathf.Max(0f, c) * 38f + 6f, 0f, 0f);
                    upperL = Q(s * 22f, 0f, -5f);
                    upperR = Q(-s * 22f, 0f, 5f);
                    foreL = Q(-18f - Mathf.Max(0f, s) * 12f, 0f, 0f);
                    foreR = Q(-18f - Mathf.Max(0f, -s) * 12f, 0f, 0f);
                    hips += new Vector3(0f, Mathf.Abs(c) * 0.025f - 0.015f, 0f);
                    hipsTwist = s * 5f;
                    break;
                }
                case Activity.Sit:
                    hips = new Vector3(hipsRest.x, 0.47f, hipsRest.z - 0.05f);
                    thighL = Q(-88f, 0f, -4f);
                    thighR = Q(-86f, 0f, 4f);
                    shinL = Q(86f, 0f, 0f);
                    shinR = Q(84f, 0f, 0f);
                    upperL = Q(-20f, 0f, -8f);
                    upperR = Q(-20f, 0f, 8f);
                    foreL = Q(-40f, 0f, 0f);
                    foreR = Q(-40f, 0f, 0f);
                    head = Q(Mathf.Sin(t * 0.3f) * 6f + 6f, Mathf.Sin(t * 0.21f) * 20f, 0f);
                    break;
                case Activity.ShiftWeight:
                {
                    float w = Mathf.Sin(t * 0.8f);
                    hips += new Vector3(w * 0.03f, 0f, 0f);
                    thighL = Q(0f, 0f, -2f - w * 3f);
                    thighR = Q(0f, 0f, 2f - w * 3f);
                    shinL = Q(Mathf.Max(0f, w) * 10f, 0f, 0f);
                    shinR = Q(Mathf.Max(0f, -w) * 10f, 0f, 0f);
                    break;
                }
                case Activity.LookAround:
                    head = Q(0f, Mathf.Sin(t * 0.6f) * 55f, 0f);
                    neck = Q(0f, Mathf.Sin(t * 0.6f) * 15f, 0f);
                    break;
                case Activity.Phone:
                    upperR = Q(-25f, 0f, 12f);
                    foreR = Q(-95f, 0f, -20f);
                    upperL = Q(-10f, 0f, -10f);
                    foreL = Q(-75f, 0f, 25f);
                    head = Q(28f, 0f, 0f);
                    neck = Q(10f, 0f, 0f);
                    break;
                case Activity.Watch:
                    upperL = Q(-40f, 0f, -20f);
                    foreL = Q(-100f, 0f, 30f);
                    head = Q(25f, -15f, 0f);
                    break;
                case Activity.Stretch:
                {
                    float up = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Sin(Mathf.Clamp01((activityEnds - Time.time) / 3f) * Mathf.PI) * 1.5f));
                    upperL = Q(0f, 0f, Mathf.Lerp(-6f, -165f, up));
                    upperR = Q(0f, 0f, Mathf.Lerp(6f, 165f, up));
                    foreL = Q(0f, 0f, -10f * up);
                    foreR = Q(0f, 0f, 10f * up);
                    chest = Q(-8f * up, 0f, 0f);
                    head = Q(-15f * up, 0f, 0f);
                    break;
                }
                case Activity.Scratch:
                    upperR = Q(-70f, 0f, 60f);
                    foreR = Q(-120f + Mathf.Sin(t * 14f) * 10f, 0f, 0f);
                    head = Q(8f, 10f, -8f);
                    break;
                case Activity.Wave:
                    upperR = Q(-10f, 0f, 150f);
                    foreR = Q(0f, 0f, 20f + Mathf.Sin(t * 9f) * 28f);
                    head = Q(0f, 0f, 0f);
                    break;
                case Activity.Talk:
                    upperR = Q(-30f, 0f, 10f);
                    foreR = Q(-60f + Mathf.Sin(t * 3.1f) * 18f, Mathf.Sin(t * 2.3f) * 20f, 0f);
                    upperL = Q(-15f + Mathf.Sin(t * 2.7f) * 8f, 0f, -8f);
                    head = Q(Mathf.Sin(t * 2.2f) * 5f, Mathf.Sin(t * 0.9f) * 12f, 0f);
                    break;
            }

            // The arriving train draws everyone's eyes.
            if (LookAt.HasValue && activity != Activity.Walk && activity != Activity.Phone)
            {
                Vector3 local = transform.InverseTransformPoint(LookAt.Value);
                float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -75f, 75f);
                head = Q(0f, yaw * 0.7f, 0f);
                neck = Q(0f, yaw * 0.3f, 0f);
            }

            Set(PersonFactory.Bone.Spine, spine * Q(0f, hipsTwist * -0.6f, 0f), blend);
            Set(PersonFactory.Bone.Chest, chest, blend);
            Set(PersonFactory.Bone.Neck, neck, blend);
            Set(PersonFactory.Bone.Head, head, blend);
            Set(PersonFactory.Bone.UpperArmL, upperL, blend);
            Set(PersonFactory.Bone.UpperArmR, upperR, blend);
            Set(PersonFactory.Bone.ForearmL, foreL, blend);
            Set(PersonFactory.Bone.ForearmR, foreR, blend);
            Set(PersonFactory.Bone.ThighL, thighL, blend);
            Set(PersonFactory.Bone.ThighR, thighR, blend);
            Set(PersonFactory.Bone.ShinL, shinL, blend);
            Set(PersonFactory.Bone.ShinR, shinR, blend);
            Set(PersonFactory.Bone.Hips, Q(0f, hipsTwist, 0f), blend);
            Transform hipsBone = bones[(int)PersonFactory.Bone.Hips];
            hipsBone.localPosition = Vector3.Lerp(hipsBone.localPosition, hips, blend);
        }

        private void Set(PersonFactory.Bone bone, Quaternion pose, float blend)
        {
            Transform t = bones[(int)bone];
            t.localRotation = Quaternion.Slerp(t.localRotation, rest[(int)bone] * pose, blend);
        }
    }
}
