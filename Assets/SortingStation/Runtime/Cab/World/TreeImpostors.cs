using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SortingStation
{
    /// <summary>
    /// Distant trees as cheap crossed cards. At start (and when the season changes) every tree
    /// species and variant from <see cref="CabTreeFactory"/> is photographed from the side into
    /// one atlas; the cards use that picture, so a forest hundreds of metres away still looks
    /// like the same trees that stand by the track, for a fraction of the triangles.
    /// </summary>
    public static class TreeImpostors
    {
        public const int BakeLayer = 27;
        private const int Cells = 4;
        private const int CellSize = 256;
        private static readonly CabTreeSpecies[] Species = { CabTreeSpecies.Broadleaf, CabTreeSpecies.Birch, CabTreeSpecies.Spruce, CabTreeSpecies.Bush };
        private static readonly Dictionary<int, Vector2> Sizes = new Dictionary<int, Vector2>();
        private static RenderTexture atlas;
        private static Material cardMaterial;
        private static Texture2D atlasCopy;
        private static SeasonType bakedSeason = (SeasonType)(-1);

        public static Material CardMaterial => cardMaterial;

        /// <summary>Photographs every tree for the season; cheap to call again for the same season.</summary>
        public static void Bake(SeasonType season)
        {
            if (atlas != null && bakedSeason == season && cardMaterial != null) return;
            bakedSeason = season;
            if (atlas == null)
            {
                atlas = new RenderTexture(Cells * CellSize, Cells * CellSize, 16, RenderTextureFormat.ARGB32)
                {
                    name = "TreeImpostorAtlas",
                    useMipMap = true,
                    autoGenerateMips = false,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Trilinear
                };
                atlas.Create();
            }

            // Drawn straight into the atlas with a command buffer: a URP camera would drop the
            // transparent background, and the albedo must not carry the scene's lighting.
            Shader bake = CabShaders.ImpostorBake;
            CommandBuffer commands = new CommandBuffer { name = "TreeImpostorBake" };
            commands.SetRenderTarget(atlas);
            commands.ClearRenderTarget(true, true, new Color(0f, 0f, 0f, 0f));
            List<Material> temporary = new List<Material>();
            for (int s = 0; s < Species.Length; s++)
                for (int v = 0; v < CabTreeFactory.VariantCount; v++)
                {
                    CabTreeSpecies species = Species[s];
                    Mesh mesh = CabTreeFactory.GetMesh(species, v, CabTreeFactory.IsLeafless(species, season));
                    Material[] source = CabTreeFactory.GetMaterials(species, season);
                    Bounds bounds = mesh.bounds;
                    float half = Mathf.Max(bounds.extents.y, Mathf.Max(bounds.extents.x, bounds.extents.z)) * 1.02f;
                    Sizes[s * 16 + v] = new Vector2(half * 2f, half * 2f);
                    commands.SetViewport(new Rect(s * CellSize, v * CellSize, CellSize, CellSize));
                    // Side view: the tree's foot at the bottom of its cell, centred.
                    Matrix4x4 view = Matrix4x4.TRS(new Vector3(0f, half, -40f), Quaternion.identity, new Vector3(1f, 1f, -1f)).inverse;
                    Matrix4x4 projection = Matrix4x4.Ortho(-half, half, -half, half, 0.1f, 100f);
                    commands.SetViewProjectionMatrices(view, projection);
                    Matrix4x4 model = Matrix4x4.Translate(new Vector3(-bounds.center.x, 0f, -bounds.center.z));
                    for (int sub = 0; sub < mesh.subMeshCount && sub < source.Length; sub++)
                    {
                        if (mesh.GetTriangles(sub).Length == 0) continue;
                        Material material = new Material(bake) { mainTexture = source[sub].mainTexture, color = source[sub].color };
                        material.mainTextureScale = source[sub].mainTextureScale;
                        material.SetFloat("_Cutoff", sub == 0 ? 0f : 0.42f);
                        temporary.Add(material);
                        commands.DrawMesh(mesh, model, material, sub, 0);
                    }
                }
            Graphics.ExecuteCommandBuffer(commands);
            commands.Release();
            atlas.GenerateMips();
            foreach (Material material in temporary) Object.DestroyImmediate(material);
            // A render texture loses its contents when the screen mode changes or the app is
            // paused on Android, which turned the distant trees into black boards: keep a copy.
            if (atlasCopy == null)
                atlasCopy = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, true)
                    { name = "TreeImpostorAtlasCopy", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = atlas;
            atlasCopy.ReadPixels(new Rect(0, 0, atlas.width, atlas.height), 0, 0, false);
            atlasCopy.Apply(true, false);
            RenderTexture.active = previous;

            if (cardMaterial == null)
            {
                cardMaterial = CabShaders.CreateLit("TreeImpostorCards", Color.white, 0.05f);
                cardMaterial.SetFloat("_AlphaClip", 1f);
                cardMaterial.SetFloat("_Cutoff", 0.35f);
                cardMaterial.EnableKeyword("_ALPHATEST_ON");
                cardMaterial.SetFloat("_Cull", (float)CullMode.Off);
                cardMaterial.renderQueue = (int)RenderQueue.AlphaTest;
            }
            cardMaterial.mainTexture = atlasCopy;
        }

        /// <summary>Writes the atlas to a PNG (smoke captures use it to check the bake).</summary>
        public static void SaveAtlas(string file)
        {
            if (atlas == null) return;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = atlas;
            Texture2D copy = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, atlas.width, atlas.height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            System.IO.File.WriteAllBytes(file, copy.EncodeToPNG());
            Object.Destroy(copy);
        }

        /// <summary>Adds two crossed cards for one tree to a card mesh under construction.</summary>
        public static void AddCard(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
            Vector3 root, CabTreeSpecies species, int variant, float scale, float yaw)
        {
            int s = System.Array.IndexOf(Species, species);
            variant = Mathf.Abs(variant) % CabTreeFactory.VariantCount;
            Vector2 size = Sizes.TryGetValue(s * 16 + variant, out Vector2 known) ? known : new Vector2(8f, 8f);
            float w = size.x * scale * 0.5f;
            float h = size.y * scale;
            Rect cell = new Rect(s / (float)Cells, variant / (float)Cells, 1f / Cells, 1f / Cells);
            for (int card = 0; card < 2; card++)
            {
                Quaternion rotation = Quaternion.Euler(0f, yaw + card * 90f, 0f);
                Vector3 right = rotation * Vector3.right * w;
                Vector3 facing = rotation * Vector3.back;
                // Normals lean up and out so the cards shade like a rounded crown, not a flat board.
                Vector3 lower = (facing * 0.45f + Vector3.up * 0.55f).normalized;
                Vector3 upper = (facing * 0.3f + Vector3.up).normalized;
                int start = vertices.Count;
                vertices.Add(root - right);
                vertices.Add(root + right);
                vertices.Add(root + right + Vector3.up * h);
                vertices.Add(root - right + Vector3.up * h);
                normals.Add(lower); normals.Add(lower); normals.Add(upper); normals.Add(upper);
                uvs.Add(new Vector2(cell.xMin, cell.yMin));
                uvs.Add(new Vector2(cell.xMax, cell.yMin));
                uvs.Add(new Vector2(cell.xMax, cell.yMax));
                uvs.Add(new Vector2(cell.xMin, cell.yMax));
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
            }
        }
    }
}
