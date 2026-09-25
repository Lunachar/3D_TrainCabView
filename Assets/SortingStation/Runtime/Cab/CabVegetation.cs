using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// Replaces the prototype route's primitive trees and flat foliage sprites with procedural
    /// 3D trees and bushes (immersive mode). The originals are only switched off: the route's
    /// season and scenery logic still holds references to them.
    /// </summary>
    public static class CabVegetation
    {
        public const string TreeObjectName = "Tree3D";

        /// <returns>How many trees and bushes are now shown in 3D.</returns>
        public static int Populate(Transform routeRoot, SeasonType season)
        {
            if (routeRoot == null) return 0;
            int placed = 0;
            Transform[] all = routeRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform item = all[i];
                if (item == null || item == routeRoot) continue;
                string name = item.name;
                if (name.StartsWith("Tree_") || name.StartsWith("StartTree_"))
                {
                    int index = ParseIndex(name);
                    CabTreeSpecies species = index % 3 == 0 ? CabTreeSpecies.Birch
                        : index % 4 == 1 ? CabTreeSpecies.Spruce : CabTreeSpecies.Broadleaf;
                    HideChildren(item);
                    Place(item, Vector3.zero, species, index, season);
                    placed++;
                }
                else if (name.StartsWith("FoliageBillboard") && item.parent == routeRoot)
                {
                    Renderer renderer = item.GetComponent<Renderer>();
                    string material = renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.name : string.Empty;
                    bool bush = material.Contains("Bush") || name.Contains("Shrub");
                    CabTreeSpecies species = bush ? CabTreeSpecies.Bush
                        : material.Contains("Birch") ? CabTreeSpecies.Birch : CabTreeSpecies.Broadleaf;
                    // Billboards stand on their centre; the new plant stands on the ground below it.
                    Vector3 ground = item.localPosition;
                    ground.y = 0f;
                    Transform holder = new GameObject("Plant3D_" + i).transform;
                    holder.SetParent(routeRoot, false);
                    holder.localPosition = ground;
                    item.gameObject.SetActive(false);
                    Place(holder, Vector3.zero, species, i, season);
                    placed++;
                }
            }
            return placed;
        }

        private static void Place(Transform parent, Vector3 localPosition, CabTreeSpecies species, int seed, SeasonType season)
        {
            Transform existing = parent.Find(TreeObjectName);
            GameObject tree = existing != null ? existing.gameObject
                : new GameObject(TreeObjectName, typeof(MeshFilter), typeof(MeshRenderer));
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = localPosition;
            // Deterministic variety from the index: variant, size and heading.
            uint hash = (uint)(seed * 2654435761u);
            tree.transform.localRotation = Quaternion.Euler(0f, hash % 360u, 0f);
            float scale = 0.85f + (hash >> 9) % 35u / 100f;
            tree.transform.localScale = Vector3.one * scale;
            bool leafless = CabTreeFactory.IsLeafless(species, season);
            tree.GetComponent<MeshFilter>().sharedMesh = CabTreeFactory.GetMesh(species, (int)(hash >> 3), leafless);
            MeshRenderer renderer = tree.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = CabTreeFactory.GetMaterials(species, season);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        private static void HideChildren(Transform tree)
        {
            for (int i = 0; i < tree.childCount; i++)
            {
                Transform child = tree.GetChild(i);
                if (child.name != TreeObjectName) child.gameObject.SetActive(false);
            }
        }

        private static int ParseIndex(string name)
        {
            int underscore = name.LastIndexOf('_');
            return underscore >= 0 && int.TryParse(name.Substring(underscore + 1), out int value) ? value : name.GetHashCode();
        }
    }
}
