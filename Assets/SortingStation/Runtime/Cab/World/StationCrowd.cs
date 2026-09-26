using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// People on a platform, in the platform's own frame (x across, z along the track). When the
    /// doors open, arriving passengers step out one at a time and head for the exit; waiting
    /// ones make for the nearest door, each after their own pause and at their own pace, queue
    /// there and board one by one. Everyone turns to look at the train pulling in.
    /// </summary>
    public sealed class StationCrowd : MonoBehaviour
    {
        private sealed class Queue
        {
            public Vector3 Door;
            public readonly List<PersonAnimator> People = new List<PersonAnimator>();
            public float NextBoard;
        }

        private readonly List<PersonAnimator> waiting = new List<PersonAnimator>();
        private readonly List<PersonAnimator> seatedPeople = new List<PersonAnimator>();
        private readonly List<Queue> queues = new List<Queue>();
        private readonly List<(PersonAnimator person, float at)> pending = new List<(PersonAnimator, float)>();
        private readonly List<(Vector3 door, float at)> arrivals = new List<(Vector3, float)>();
        private readonly List<PersonAnimator> leaving = new List<PersonAnimator>();
        private System.Random random;
        private bool doorsOpen;
        private Vector3 exit;
        private float platformEdge;
        private bool gnomes;

        public int WaitingCount => waiting.Count;
        public int QueuedCount { get { int n = 0; foreach (Queue q in queues) n += q.People.Count; return n; } }
        public System.Func<System.Random, PersonAnimator> SpawnArrival { get; set; }

        public void Configure(int seed, Vector3 exitPoint, float edgeX, bool gnomeStation)
        {
            random = new System.Random(seed);
            exit = exitPoint;
            platformEdge = edgeX;
            gnomes = gnomeStation;
        }

        public void Add(PersonAnimator person, bool seated)
        {
            if (seated) seatedPeople.Add(person);
            else waiting.Add(person);
        }

        private float R(float a, float b) => a + (float)random.NextDouble() * (b - a);

        /// <summary>Everyone on the platform glances at the train (world point), or stops looking.</summary>
        public void LookAtTrain(Vector3? worldPoint)
        {
            foreach (PersonAnimator person in waiting) if (person != null) person.LookAt = worldPoint;
            foreach (PersonAnimator person in seatedPeople) if (person != null) person.LookAt = worldPoint;
        }

        /// <summary>Doors open: <paramref name="boarding"/> people get on, <paramref name="alighting"/> get off, at these doors (frame space).</summary>
        public void Exchange(int boarding, int alighting, IList<Vector3> doors)
        {
            doorsOpen = true;
            queues.Clear();
            foreach (Vector3 door in doors) queues.Add(new Queue { Door = new Vector3(door.x, 0f, door.z), NextBoard = Time.time + R(1.5f, 2.5f) });
            if (queues.Count == 0) return;
            // Arrivals first: one after another from the doors.
            float at = Time.time + 0.6f;
            for (int i = 0; i < alighting; i++)
            {
                arrivals.Add((queues[random.Next(0, queues.Count)].Door, at));
                at += R(0.7f, 1.5f);
            }
            // Then the waiting passengers, each after their own pause.
            List<PersonAnimator> candidates = new List<PersonAnimator>(waiting);
            for (int i = 0; i < boarding && candidates.Count > 0; i++)
            {
                int pick = random.Next(0, candidates.Count);
                PersonAnimator person = candidates[pick];
                candidates.RemoveAt(pick);
                pending.Add((person, Time.time + R(0.3f, 4f)));
            }
        }

        public void CloseDoors()
        {
            doorsOpen = false;
            pending.Clear();
            arrivals.Clear();
            // Whoever did not make it wanders back to where they stood.
            foreach (Queue queue in queues)
                foreach (PersonAnimator person in queue.People)
                    if (person != null && person.gameObject.activeSelf) person.WalkTo(person.transform.localPosition + new Vector3(-1.2f, 0f, 0f), 0.9f);
            queues.Clear();
        }

        private void Update()
        {
            if (!doorsOpen) return;
            for (int i = arrivals.Count - 1; i >= 0; i--)
            {
                if (Time.time < arrivals[i].at) continue;
                Vector3 door = arrivals[i].door;
                arrivals.RemoveAt(i);
                PersonAnimator person = SpawnArrival != null ? SpawnArrival(random) : null;
                if (person == null) continue;
                person.transform.localPosition = door + new Vector3(0.2f, 0f, 0f);
                person.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                person.AllowStrolling = false;
                leaving.Add(person);
                Vector3 step = door + new Vector3(-1.6f, 0f, R(-0.6f, 0.6f));
                person.WalkTo(step, R(1.0f, 1.4f), () => person.WalkTo(exit + new Vector3(R(-1f, 1f), 0f, R(-1f, 1f)), R(1.1f, 1.5f),
                    () => person.gameObject.SetActive(false)));
            }
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < pending[i].at) continue;
                PersonAnimator person = pending[i].person;
                pending.RemoveAt(i);
                if (person == null || !person.gameObject.activeInHierarchy) continue;
                Queue queue = Nearest(person.transform.localPosition);
                queue.People.Add(person);
                person.AllowStrolling = false;
                person.LookAt = null;
                person.WalkTo(QueueSpot(queue, queue.People.Count - 1), R(1.0f, 1.5f) * (gnomes ? 0.8f : 1f));
            }
            foreach (Queue queue in queues)
            {
                // Keep the line closed up, and let the one at the front in when the door is free.
                for (int k = 0; k < queue.People.Count; k++)
                {
                    PersonAnimator person = queue.People[k];
                    if (person == null || person.IsWalking) continue;
                    Vector3 spot = QueueSpot(queue, k);
                    if ((person.transform.localPosition - spot).sqrMagnitude > 0.04f) person.WalkTo(spot, 1f);
                }
                if (queue.People.Count == 0 || Time.time < queue.NextBoard) continue;
                PersonAnimator first = queue.People[0];
                if (first == null || first.IsWalking || (first.transform.localPosition - QueueSpot(queue, 0)).sqrMagnitude > 0.09f) continue;
                queue.People.RemoveAt(0);
                waiting.Remove(first);
                queue.NextBoard = Time.time + R(0.8f, 1.4f);
                first.WalkTo(queue.Door + new Vector3(0.6f, 0f, 0f), 0.9f, () => first.gameObject.SetActive(false));
            }
        }

        private Queue Nearest(Vector3 position)
        {
            Queue best = queues[0];
            float bestDistance = float.MaxValue;
            foreach (Queue queue in queues)
            {
                // Short lines are more attractive than slightly nearer long ones.
                float d = Mathf.Abs(queue.Door.z - position.z) + queue.People.Count * 1.5f;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = queue;
                }
            }
            return best;
        }

        /// <summary>Queue spots run back from the door into the platform, slightly staggered.</summary>
        private Vector3 QueueSpot(Queue queue, int index)
        {
            float stagger = index % 2 == 0 ? 0.15f : -0.15f;
            return new Vector3(platformEdge - 0.55f - index * 0.62f, 0f, queue.Door.z + stagger * Mathf.Min(1, index));
        }
    }
}
