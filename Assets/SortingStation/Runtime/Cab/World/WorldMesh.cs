using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SortingStation
{
    /// <summary>
    /// Mesh under construction for one material of one chunk. Everything a chunk builds with
    /// the same material goes into one of these, so a chunk costs only a few draw calls.
    /// </summary>
    public sealed class WorldMesh
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        public readonly List<Color> Colors = new List<Color>();
        public readonly List<int> Triangles = new List<int>();
        private readonly float uvScale;

        public WorldMesh(float metresPerTile = 1f)
        {
            uvScale = 1f / Mathf.Max(0.01f, metresPerTile);
        }

        public bool IsEmpty => Triangles.Count == 0;

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD)
        {
            int start = Vertices.Count;
            Vertices.Add(a); Vertices.Add(b); Vertices.Add(c); Vertices.Add(d);
            Normals.Add(normal); Normals.Add(normal); Normals.Add(normal); Normals.Add(normal);
            Uvs.Add(uvA * uvScale); Uvs.Add(uvB * uvScale); Uvs.Add(uvC * uvScale); Uvs.Add(uvD * uvScale);
            // Wind so the face points along the normal.
            Vector3 face = Vector3.Cross(b - a, c - a);
            // Unity's front face is the one Cross(b - a, c - a) points at.
            if (Vector3.Dot(face, normal) >= 0f)
            {
                Triangles.Add(start); Triangles.Add(start + 1); Triangles.Add(start + 2);
                Triangles.Add(start); Triangles.Add(start + 2); Triangles.Add(start + 3);
            }
            else
            {
                Triangles.Add(start); Triangles.Add(start + 2); Triangles.Add(start + 1);
                Triangles.Add(start); Triangles.Add(start + 3); Triangles.Add(start + 2);
            }
        }

        /// <summary>Quad with UVs projected in metres on its own plane.</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            Vector3 u = (b - a);
            float width = u.magnitude;
            u = width > 0.0001f ? u / width : Vector3.right;
            Vector3 v = Vector3.Cross(normal, u).normalized;
            Vector2 P(Vector3 p) => new Vector2(Vector3.Dot(p - a, u), Vector3.Dot(p - a, v));
            AddQuad(a, b, c, d, normal, P(a), P(b), P(c), P(d));
        }

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            int start = Vertices.Count;
            Vector3 u = (b - a).normalized;
            Vector3 v = Vector3.Cross(normal, u).normalized;
            Vertices.Add(a); Vertices.Add(b); Vertices.Add(c);
            Normals.Add(normal); Normals.Add(normal); Normals.Add(normal);
            Uvs.Add(Vector2.zero);
            Uvs.Add(new Vector2(Vector3.Dot(b - a, u), Vector3.Dot(b - a, v)) * uvScale);
            Uvs.Add(new Vector2(Vector3.Dot(c - a, u), Vector3.Dot(c - a, v)) * uvScale);
            Vector3 face = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(face, normal) >= 0f) { Triangles.Add(start); Triangles.Add(start + 1); Triangles.Add(start + 2); }
            else { Triangles.Add(start); Triangles.Add(start + 2); Triangles.Add(start + 1); }
        }

        /// <summary>Box with flat faces and metre-projected UVs (optionally without the bottom).</summary>
        public void AddBox(Vector3 centre, Vector3 size, Quaternion rotation, bool bottom = false)
        {
            Vector3 h = size * 0.5f;
            Vector3 V(float x, float y, float z) => centre + rotation * new Vector3(x * h.x, y * h.y, z * h.z);
            Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up, forward = rotation * Vector3.forward;
            AddQuad(V(1, -1, -1), V(1, -1, 1), V(1, 1, 1), V(1, 1, -1), right);
            AddQuad(V(-1, -1, 1), V(-1, -1, -1), V(-1, 1, -1), V(-1, 1, 1), -right);
            AddQuad(V(-1, 1, -1), V(1, 1, -1), V(1, 1, 1), V(-1, 1, 1), up);
            if (bottom) AddQuad(V(-1, -1, 1), V(1, -1, 1), V(1, -1, -1), V(-1, -1, -1), -up);
            AddQuad(V(1, -1, 1), V(-1, -1, 1), V(-1, 1, 1), V(1, 1, 1), forward);
            AddQuad(V(-1, -1, -1), V(1, -1, -1), V(1, 1, -1), V(-1, 1, -1), -forward);
        }

        /// <summary>Cylinder along local Y (smooth sides, top cap).</summary>
        public void AddCylinder(Vector3 bottomCentre, float radius, float height, int segments, Quaternion rotation, bool cap = true)
        {
            int start = Vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 radial = rotation * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 up = rotation * Vector3.up;
                float u = i / (float)segments * Mathf.PI * 2f * radius;
                Vertices.Add(bottomCentre + radial * radius);
                Vertices.Add(bottomCentre + radial * radius + up * height);
                Normals.Add(radial); Normals.Add(radial);
                Uvs.Add(new Vector2(u, 0f) * uvScale); Uvs.Add(new Vector2(u, height) * uvScale);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = start + i * 2;
                Triangles.Add(a); Triangles.Add(a + 1); Triangles.Add(a + 3);
                Triangles.Add(a); Triangles.Add(a + 3); Triangles.Add(a + 2);
            }
            if (!cap) return;
            Vector3 top = bottomCentre + rotation * Vector3.up * height;
            int centre = Vertices.Count;
            Vertices.Add(top); Normals.Add(rotation * Vector3.up); Uvs.Add(Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vertices.Add(top + rotation * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
                Normals.Add(rotation * Vector3.up);
                Uvs.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * uvScale);
            }
            for (int i = 0; i < segments; i++)
            {
                Triangles.Add(centre); Triangles.Add(centre + 2 + i); Triangles.Add(centre + 1 + i);
            }
        }

        /// <summary>Cone from a base circle to a tip (stalactites, stalagmites, roofs of tents and towers).</summary>
        public void AddCone(Vector3 baseCentre, Vector3 tip, float radius, int segments)
        {
            Vector3 axis = (tip - baseCentre).normalized;
            Quaternion basis = Quaternion.FromToRotation(Vector3.up, axis);
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2f, a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                Vector3 p0 = baseCentre + basis * new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                Vector3 p1 = baseCentre + basis * new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                Vector3 outward = ((p0 + p1) * 0.5f - baseCentre).normalized + axis * 0.3f;
                AddTriangle(p0, p1, tip, outward.normalized);
            }
        }

        public void Append(Mesh mesh, int subMesh, Matrix4x4 transform)
        {
            int start = Vertices.Count;
            Vector3[] v = mesh.vertices;
            Vector3[] n = mesh.normals;
            Vector2[] uv = mesh.uv;
            for (int i = 0; i < v.Length; i++)
            {
                Vertices.Add(transform.MultiplyPoint3x4(v[i]));
                Normals.Add(n.Length > i ? transform.MultiplyVector(n[i]).normalized : Vector3.up);
                Uvs.Add(uv.Length > i ? uv[i] : Vector2.zero);
            }
            int[] t = mesh.GetTriangles(subMesh);
            for (int i = 0; i < t.Length; i++) Triangles.Add(start + t[i]);
        }

        public Mesh ToMesh(string name)
        {
            Mesh mesh = new Mesh { name = name };
            if (Vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            mesh.SetUVs(0, Uvs);
            if (Colors.Count == Vertices.Count) mesh.SetColors(Colors);
            mesh.SetTriangles(Triangles, 0, true);
            mesh.RecalculateTangents();
            return mesh;
        }
    }

    /// <summary>All meshes of one chunk, grouped by material.</summary>
    public sealed class WorldMeshSet
    {
        private readonly Dictionary<Material, WorldMesh> meshes = new Dictionary<Material, WorldMesh>();
        private readonly Dictionary<Material, bool> shadows = new Dictionary<Material, bool>();

        public WorldMesh For(Material material, float metresPerTile = 1f, bool castShadows = false)
        {
            if (!meshes.TryGetValue(material, out WorldMesh mesh))
            {
                mesh = new WorldMesh(metresPerTile);
                meshes[material] = mesh;
                shadows[material] = castShadows;
            }
            return mesh;
        }

        /// <summary>Creates one child object per material and returns the renderers.</summary>
        public List<Renderer> Emit(Transform parent, string prefix)
        {
            List<Renderer> renderers = new List<Renderer>();
            foreach (KeyValuePair<Material, WorldMesh> pair in meshes)
            {
                if (pair.Value.IsEmpty) continue;
                GameObject part = new GameObject(prefix + "_" + pair.Key.name, typeof(MeshFilter), typeof(MeshRenderer));
                part.transform.SetParent(parent, false);
                part.GetComponent<MeshFilter>().sharedMesh = pair.Value.ToMesh(part.name);
                MeshRenderer renderer = part.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = pair.Key;
                renderer.shadowCastingMode = shadows[pair.Key] ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderers.Add(renderer);
            }
            meshes.Clear();
            return renderers;
        }
    }
}
