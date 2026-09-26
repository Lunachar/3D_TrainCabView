using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    /// <summary>One built 120 m piece of the world and the live parts the ride talks to.</summary>
    public sealed class WorldChunk
    {
        public WorldChunkPlan Plan;
        public GameObject Root;
        /// <summary>Full tree meshes near the track; swapped for <see cref="NearCards"/> when the chunk is far away.</summary>
        public GameObject NearDetail;
        public GameObject NearCards;
        public Transform PlatformFrame;
        public bool Complete;
        public readonly List<Light> Lamps = new List<Light>();
        public readonly List<Light> TunnelLights = new List<Light>();
        /// <summary>Things that only appear after dark (the station attendant with the lantern).</summary>
        public readonly List<GameObject> NightOnly = new List<GameObject>();
        /// <summary>Things that are only there in daylight (the attendant with the flag).</summary>
        public readonly List<GameObject> DayOnly = new List<GameObject>();
        public readonly List<Transform> CrossingArms = new List<Transform>();
        public readonly List<Quaternion> CrossingArmRest = new List<Quaternion>();
        public readonly List<Renderer> CrossingLamps = new List<Renderer>();
        public readonly List<Light> CrossingLights = new List<Light>();
        /// <summary>1 = barriers up, 0 = down.</summary>
        public float CrossingAngle = 1f;
        public readonly List<TextMesh> Signs = new List<TextMesh>();
        public readonly List<Transform> SignBoards = new List<Transform>();
        public StationCrowd Crowd;
        public readonly List<PersonAnimator> Attendants = new List<PersonAnimator>();
        public readonly List<Mesh> Meshes = new List<Mesh>();
        public readonly List<Material> Materials = new List<Material>();
        private bool? nearShown;

        public void SetNear(bool near)
        {
            if (nearShown == near) return;
            nearShown = near;
            if (NearDetail != null) NearDetail.SetActive(near);
            if (NearCards != null) NearCards.SetActive(!near);
        }

        public void Destroy()
        {
            if (Root != null) DestroyObject(Root);
            for (int i = 0; i < Meshes.Count; i++) if (Meshes[i] != null) DestroyObject(Meshes[i]);
            for (int i = 0; i < Materials.Count; i++) if (Materials[i] != null) DestroyObject(Materials[i]);
            Meshes.Clear();
            Materials.Clear();
            Root = null;
        }

        private static void DestroyObject(Object target)
        {
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
