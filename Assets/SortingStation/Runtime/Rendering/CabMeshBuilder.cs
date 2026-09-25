using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// Small procedural mesh kit for the cab and the world: chamfered boxes, cylinders and
    /// extruded profiles. Faces are flat-shaded like machined or pressed parts, and UVs are
    /// projected in metres so tiling PBR textures keep a real-world scale on every part.
    /// </summary>
    public sealed class CabMeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();
        private readonly float uvScale;

        public CabMeshBuilder(float uvMetresPerTile = 1f)
        {
            uvScale = 1f / Mathf.Max(0.01f, uvMetresPerTile);
        }

        public Mesh Build(string name)
        {
            Mesh mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>Adds a flat convex polygon; the winding is corrected to face away from <paramref name="inside"/>.</summary>
        public void AddPolygon(IList<Vector3> points, Vector3 inside)
        {
            if (points.Count < 3) return;
            Vector3 normal = NewellNormal(points);
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < points.Count; i++) centroid += points[i];
            centroid /= points.Count;
            bool flip = Vector3.Dot(normal, centroid - inside) < 0f;
            if (flip) normal = -normal;
            int start = vertices.Count;
            for (int i = 0; i < points.Count; i++)
            {
                vertices.Add(points[i]);
                normals.Add(normal);
                uvs.Add(ProjectUv(points[i], normal));
            }
            for (int i = 1; i < points.Count - 1; i++)
            {
                if (flip)
                {
                    triangles.Add(start);
                    triangles.Add(start + i + 1);
                    triangles.Add(start + i);
                }
                else
                {
                    triangles.Add(start);
                    triangles.Add(start + i);
                    triangles.Add(start + i + 1);
                }
            }
        }

        /// <summary>Box with 45° chamfered edges and corners, centred on <paramref name="center"/>.</summary>
        public void AddChamferBox(Vector3 center, Vector3 size, float chamfer, Quaternion rotation)
        {
            Vector3 h = size * 0.5f;
            float c = Mathf.Clamp(chamfer, 0f, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.95f);
            if (c <= 0.0001f)
            {
                AddBox(center, size, rotation);
                return;
            }
            Vector3 P(int axis, int sx, int sy, int sz)
            {
                Vector3 p = new Vector3(
                    sx * (axis == 0 ? h.x : h.x - c),
                    sy * (axis == 1 ? h.y : h.y - c),
                    sz * (axis == 2 ? h.z : h.z - c));
                return center + rotation * p;
            }
            // Main faces.
            for (int axis = 0; axis < 3; axis++)
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3[] quad = new Vector3[4];
                    int k = 0;
                    foreach ((int a, int b) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                    {
                        quad[k++] = axis == 0 ? P(0, s, a, b) : axis == 1 ? P(1, a, s, b) : P(2, a, b, s);
                    }
                    AddPolygon(quad, center);
                }
            // Edge chamfers.
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    AddPolygon(new[] { P(0, sx, sy, -1), P(0, sx, sy, 1), P(1, sx, sy, 1), P(1, sx, sy, -1) }, center);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    AddPolygon(new[] { P(0, sx, -1, sz), P(0, sx, 1, sz), P(2, sx, 1, sz), P(2, sx, -1, sz) }, center);
            for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    AddPolygon(new[] { P(1, -1, sy, sz), P(1, 1, sy, sz), P(2, 1, sy, sz), P(2, -1, sy, sz) }, center);
            // Corner triangles.
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        AddPolygon(new[] { P(0, sx, sy, sz), P(1, sx, sy, sz), P(2, sx, sy, sz) }, center);
        }

        public void AddBox(Vector3 center, Vector3 size, Quaternion rotation)
        {
            Vector3 h = size * 0.5f;
            Vector3 V(int x, int y, int z) => center + rotation * new Vector3(x * h.x, y * h.y, z * h.z);
            AddPolygon(new[] { V(1, -1, -1), V(1, 1, -1), V(1, 1, 1), V(1, -1, 1) }, center);
            AddPolygon(new[] { V(-1, -1, -1), V(-1, 1, -1), V(-1, 1, 1), V(-1, -1, 1) }, center);
            AddPolygon(new[] { V(-1, 1, -1), V(1, 1, -1), V(1, 1, 1), V(-1, 1, 1) }, center);
            AddPolygon(new[] { V(-1, -1, -1), V(1, -1, -1), V(1, -1, 1), V(-1, -1, 1) }, center);
            AddPolygon(new[] { V(-1, -1, 1), V(1, -1, 1), V(1, 1, 1), V(-1, 1, 1) }, center);
            AddPolygon(new[] { V(-1, -1, -1), V(1, -1, -1), V(1, 1, -1), V(-1, 1, -1) }, center);
        }

        /// <summary>Cylinder along the rotated local Y axis; smooth sides, flat caps.</summary>
        public void AddCylinder(Vector3 center, float radius, float height, int segments, Quaternion rotation,
            bool capTop = true, bool capBottom = true)
        {
            segments = Mathf.Max(3, segments);
            float half = height * 0.5f;
            int start = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 normal = rotation * radial;
                float u = i / (float)segments * Mathf.PI * 2f * radius * uvScale;
                vertices.Add(center + rotation * (radial * radius + Vector3.down * half));
                normals.Add(normal);
                uvs.Add(new Vector2(u, 0f));
                vertices.Add(center + rotation * (radial * radius + Vector3.up * half));
                normals.Add(normal);
                uvs.Add(new Vector2(u, height * uvScale));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = start + i * 2;
                triangles.Add(a); triangles.Add(a + 1); triangles.Add(a + 3);
                triangles.Add(a); triangles.Add(a + 3); triangles.Add(a + 2);
            }
            if (capTop) AddDisc(center + rotation * (Vector3.up * half), radius, segments, rotation * Vector3.up);
            if (capBottom) AddDisc(center + rotation * (Vector3.down * half), radius, segments, rotation * Vector3.down);
        }

        public void AddDisc(Vector3 center, float radius, int segments, Vector3 normal)
        {
            Quaternion basis = Quaternion.FromToRotation(Vector3.up, normal.normalized);
            Vector3[] ring = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                ring[i] = center + basis * new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }
            AddPolygon(ring, center - normal);
        }

        /// <summary>
        /// Extrudes a simple (possibly concave) profile drawn in the YZ plane along X, from
        /// <paramref name="xMin"/> to <paramref name="xMax"/>. Used for desks, sills and frames.
        /// </summary>
        public void AddExtrudedProfile(IList<Vector2> profileYZ, float xMin, float xMax, Vector3 offset)
        {
            int n = profileYZ.Count;
            if (n < 3) return;
            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = profileYZ[i];
                Vector2 b = profileYZ[(i + 1) % n];
                area += a.x * b.y - b.x * a.y;
            }
            bool counterClockwise = area > 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = profileYZ[i];
                Vector2 b = profileYZ[(i + 1) % n];
                Vector3 a0 = offset + new Vector3(xMin, a.x, a.y);
                Vector3 a1 = offset + new Vector3(xMax, a.x, a.y);
                Vector3 b0 = offset + new Vector3(xMin, b.x, b.y);
                Vector3 b1 = offset + new Vector3(xMax, b.x, b.y);
                // Outward side normal of the 2D edge (y, z) for the profile's winding.
                Vector2 edge = b - a;
                Vector2 outward2D = counterClockwise ? new Vector2(edge.y, -edge.x) : new Vector2(-edge.y, edge.x);
                Vector3 outward = new Vector3(0f, outward2D.x, outward2D.y).normalized;
                AddQuad(a0, b0, b1, a1, outward);
            }
            List<int> cap = Triangulate(profileYZ);
            AddCap(profileYZ, cap, xMin, offset, Vector3.left);
            AddCap(profileYZ, cap, xMax, offset, Vector3.right);
        }

        private void AddCap(IList<Vector2> profile, List<int> indices, float x, Vector3 offset, Vector3 normal)
        {
            int start = vertices.Count;
            for (int i = 0; i < profile.Count; i++)
            {
                Vector3 p = offset + new Vector3(x, profile[i].x, profile[i].y);
                vertices.Add(p);
                normals.Add(normal);
                uvs.Add(ProjectUv(p, normal));
            }
            for (int i = 0; i < indices.Count; i += 3)
            {
                int a = start + indices[i];
                int b = start + indices[i + 1];
                int c = start + indices[i + 2];
                Vector3 faceNormal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                if (Vector3.Dot(faceNormal, normal) < 0f)
                {
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                }
                else
                {
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                }
            }
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            int start = vertices.Count;
            Vector3 normal = outward.normalized;
            Vector3[] points = { a, b, c, d };
            for (int i = 0; i < 4; i++)
            {
                vertices.Add(points[i]);
                normals.Add(normal);
                uvs.Add(ProjectUv(points[i], normal));
            }
            Vector3 faceNormal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(faceNormal, normal) < 0f)
            {
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
            }
            else
            {
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }
        }

        private Vector2 ProjectUv(Vector3 point, Vector3 normal)
        {
            Vector3 n = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            if (n.x >= n.y && n.x >= n.z) return new Vector2(point.z, point.y) * uvScale;
            if (n.y >= n.z) return new Vector2(point.x, point.z) * uvScale;
            return new Vector2(point.x, point.y) * uvScale;
        }

        private static Vector3 NewellNormal(IList<Vector3> points)
        {
            Vector3 normal = Vector3.zero;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 current = points[i];
                Vector3 next = points[(i + 1) % points.Count];
                normal.x += (current.y - next.y) * (current.z + next.z);
                normal.y += (current.z - next.z) * (current.x + next.x);
                normal.z += (current.x - next.x) * (current.y + next.y);
            }
            return normal.sqrMagnitude > 1e-12f ? normal.normalized : Vector3.up;
        }

        /// <summary>Ear-clipping triangulation of a simple polygon.</summary>
        public static List<int> Triangulate(IList<Vector2> polygon)
        {
            List<int> result = new List<int>();
            int n = polygon.Count;
            if (n < 3) return result;
            List<int> remaining = new List<int>(n);
            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % n];
                area += a.x * b.y - b.x * a.y;
            }
            for (int i = 0; i < n; i++) remaining.Add(area > 0f ? i : n - 1 - i);
            int guard = n * n;
            while (remaining.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int prev = remaining[(i + remaining.Count - 1) % remaining.Count];
                    int curr = remaining[i];
                    int next = remaining[(i + 1) % remaining.Count];
                    Vector2 a = polygon[prev];
                    Vector2 b = polygon[curr];
                    Vector2 c = polygon[next];
                    if (Cross(b - a, c - b) <= 0f) continue;
                    bool containsOther = false;
                    for (int j = 0; j < remaining.Count; j++)
                    {
                        int other = remaining[j];
                        if (other == prev || other == curr || other == next) continue;
                        if (PointInTriangle(polygon[other], a, b, c))
                        {
                            containsOther = true;
                            break;
                        }
                    }
                    if (containsOther) continue;
                    result.Add(prev); result.Add(curr); result.Add(next);
                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (remaining.Count == 3)
            {
                result.Add(remaining[0]); result.Add(remaining[1]); result.Add(remaining[2]);
            }
            return result;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a);
            float d2 = Cross(c - b, p - b);
            float d3 = Cross(a - c, p - c);
            bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }
    }
}
