using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    /// <summary>How a person looks: build, clothes and what they carry.</summary>
    public struct PersonLook
    {
        public float Height;
        public float Build;       // 0 slim .. 1 broad
        public bool Child;
        public bool Skirt;
        public bool LongCoat;
        public int Hair;          // 0 short, 1 long, 2 bun, 3 cap, 4 knitted hat, 5 bald
        public int Carry;         // 0 none, 1 backpack, 2 shoulder bag, 3 suitcase
        public Color Skin;
        public Color HairColour;
        public Color Top;
        public Color Bottom;
        public Color Shoes;
        public Color Accent;

        private static readonly Color[] Skins =
        {
            new Color(0.95f, 0.78f, 0.64f), new Color(0.88f, 0.66f, 0.50f), new Color(0.72f, 0.50f, 0.36f),
            new Color(0.52f, 0.34f, 0.23f), new Color(0.98f, 0.84f, 0.72f)
        };
        private static readonly Color[] Hairs =
        {
            new Color(0.10f, 0.07f, 0.05f), new Color(0.32f, 0.20f, 0.10f), new Color(0.62f, 0.46f, 0.22f),
            new Color(0.82f, 0.70f, 0.45f), new Color(0.55f, 0.55f, 0.55f), new Color(0.45f, 0.14f, 0.08f)
        };
        private static readonly Color[] Coats =
        {
            new Color(0.12f, 0.16f, 0.28f), new Color(0.55f, 0.12f, 0.10f), new Color(0.22f, 0.34f, 0.22f), new Color(0.72f, 0.62f, 0.44f),
            new Color(0.15f, 0.15f, 0.16f), new Color(0.82f, 0.82f, 0.80f), new Color(0.36f, 0.24f, 0.18f), new Color(0.20f, 0.45f, 0.70f),
            new Color(0.85f, 0.55f, 0.15f), new Color(0.55f, 0.30f, 0.55f), new Color(0.40f, 0.42f, 0.45f), new Color(0.70f, 0.20f, 0.35f),
            new Color(0.25f, 0.55f, 0.50f), new Color(0.90f, 0.80f, 0.30f), new Color(0.30f, 0.20f, 0.40f), new Color(0.60f, 0.65f, 0.70f)
        };
        private static readonly Color[] Trousers =
        {
            new Color(0.10f, 0.12f, 0.20f), new Color(0.18f, 0.18f, 0.20f), new Color(0.30f, 0.35f, 0.50f), new Color(0.40f, 0.33f, 0.24f),
            new Color(0.08f, 0.08f, 0.09f), new Color(0.55f, 0.50f, 0.42f)
        };

        public static PersonLook Random(System.Random random, bool child = false)
        {
            float R() => (float)random.NextDouble();
            bool woman = R() < 0.5f;
            PersonLook look = new PersonLook
            {
                Child = child,
                Height = child ? 1.1f + R() * 0.35f : woman ? 1.55f + R() * 0.25f : 1.65f + R() * 0.3f,
                Build = R(),
                Skirt = woman && !child && R() < 0.4f,
                LongCoat = !child && R() < 0.35f,
                Hair = woman ? (R() < 0.45f ? 1 : R() < 0.5f ? 2 : R() < 0.5f ? 4 : 0) : (R() < 0.15f ? 5 : R() < 0.3f ? 3 : R() < 0.25f ? 4 : 0),
                Carry = child ? (R() < 0.5f ? 1 : 0) : R() < 0.25f ? 1 : R() < 0.3f ? 2 : R() < 0.15f ? 3 : 0,
                Skin = Skins[random.Next(0, Skins.Length)],
                HairColour = Hairs[random.Next(0, Hairs.Length)],
                Top = Coats[random.Next(0, Coats.Length)],
                Bottom = Trousers[random.Next(0, Trousers.Length)],
                Shoes = R() < 0.6f ? new Color(0.06f, 0.05f, 0.05f) : Coats[random.Next(0, Coats.Length)] * 0.6f,
                Accent = Coats[random.Next(0, Coats.Length)]
            };
            if (look.Skirt) look.Bottom = Coats[random.Next(0, Coats.Length)];
            return look;
        }
    }

    /// <summary>
    /// People as one skinned mesh each: a simple skeleton (hips, spine, head, arms and legs with
    /// elbows and knees) with rounded, tapered body parts rigidly bound to their bones, so a
    /// person costs one renderer and a few materials, and PersonAnimator poses the bones.
    /// </summary>
    public static class PersonFactory
    {
        public enum Bone
        {
            Root, Hips, Spine, Chest, Neck, Head,
            UpperArmL, ForearmL, HandL, UpperArmR, ForearmR, HandR,
            ThighL, ShinL, FootL, ThighR, ShinR, FootR
        }

        private const int Skin = 0, Top = 1, Bottom = 2, Hair = 3, Shoes = 4, Accent = 5;

        public static PersonAnimator Create(Transform parent, string name, Vector3 localPosition, PersonLook look)
        {
            GameObject person = new GameObject(name);
            person.transform.SetParent(parent, false);
            person.transform.localPosition = localPosition;
            float s = look.Height / 1.75f;
            float w = Mathf.Lerp(0.88f, 1.18f, look.Build) * (look.Child ? 0.92f : 1f);

            // Skeleton in the bind pose (T-less: arms hanging down).
            Transform[] bones = new Transform[System.Enum.GetValues(typeof(Bone)).Length];
            Transform B(Bone bone, Bone parentBone, Vector3 local)
            {
                Transform t = new GameObject(bone.ToString()).transform;
                t.SetParent(bone == Bone.Root ? person.transform : bones[(int)parentBone], false);
                t.localPosition = local;
                bones[(int)bone] = t;
                return t;
            }
            B(Bone.Root, Bone.Root, Vector3.zero);
            B(Bone.Hips, Bone.Root, new Vector3(0f, 0.93f * s, 0f));
            B(Bone.Spine, Bone.Hips, new Vector3(0f, 0.12f * s, 0f));
            B(Bone.Chest, Bone.Spine, new Vector3(0f, 0.24f * s, 0f));
            B(Bone.Neck, Bone.Chest, new Vector3(0f, 0.2f * s, 0f));
            B(Bone.Head, Bone.Neck, new Vector3(0f, 0.08f * s, 0f));
            for (int side = -1; side <= 1; side += 2)
            {
                bool left = side < 0;
                B(left ? Bone.UpperArmL : Bone.UpperArmR, Bone.Chest, new Vector3(side * 0.19f * s * w, 0.15f * s, 0f));
                B(left ? Bone.ForearmL : Bone.ForearmR, left ? Bone.UpperArmL : Bone.UpperArmR, new Vector3(0f, -0.29f * s, 0f));
                B(left ? Bone.HandL : Bone.HandR, left ? Bone.ForearmL : Bone.ForearmR, new Vector3(0f, -0.26f * s, 0f));
                B(left ? Bone.ThighL : Bone.ThighR, Bone.Hips, new Vector3(side * 0.095f * s * w, -0.02f * s, 0f));
                B(left ? Bone.ShinL : Bone.ShinR, left ? Bone.ThighL : Bone.ThighR, new Vector3(0f, -0.44f * s, 0f));
                B(left ? Bone.FootL : Bone.FootR, left ? Bone.ShinL : Bone.ShinR, new Vector3(0f, -0.42f * s, 0f));
            }

            PartBuilder mesh = new PartBuilder(person.transform, bones);
            float coatLength = look.LongCoat ? 0.42f : 0.12f;
            // Torso: hips (trousers or skirt), belly and chest (top), shoulders.
            mesh.Tube(Bone.Hips, look.Skirt ? Top : Bottom, new Vector3(0f, -0.06f * s, 0f), new Vector3(0f, 0.14f * s, 0f), 0.15f * s * w, 0.14f * s * w, 0.62f);
            mesh.Tube(Bone.Spine, Top, new Vector3(0f, -0.02f * s, 0f), new Vector3(0f, 0.25f * s, 0f), 0.14f * s * w, 0.16f * s * w, 0.6f);
            mesh.Tube(Bone.Chest, Top, new Vector3(0f, 0f, 0f), new Vector3(0f, 0.17f * s, 0f), 0.165f * s * w, 0.13f * s * w, 0.62f);
            if (look.LongCoat || look.Skirt)
                mesh.Tube(Bone.Hips, Top, new Vector3(0f, 0.02f * s, 0f), new Vector3(0f, -coatLength * s - (look.Skirt ? 0.12f * s : 0f), 0f),
                    0.155f * s * w, (look.Skirt ? 0.22f : 0.18f) * s * w, 0.7f);
            // Neck and head with hair or a hat.
            mesh.Tube(Bone.Neck, Skin, new Vector3(0f, -0.02f * s, 0f), new Vector3(0f, 0.09f * s, 0f), 0.05f * s, 0.045f * s, 1f);
            mesh.Ball(Bone.Head, Skin, new Vector3(0f, 0.1f * s, 0.005f), new Vector3(0.095f, 0.115f, 0.105f) * s);
            mesh.Ball(Bone.Head, Hair, new Vector3(0f, 0.16f * s, 0.035f), new Vector3(0.012f, 0.012f, 0.012f) * s); // keeps the hair submesh present
            switch (look.Hair)
            {
                case 1: // long
                    mesh.Ball(Bone.Head, Hair, new Vector3(0f, 0.13f * s, -0.012f), new Vector3(0.102f, 0.1f, 0.1f) * s);
                    mesh.Tube(Bone.Head, Hair, new Vector3(0f, 0.1f * s, -0.04f), new Vector3(0f, -0.1f * s, -0.06f), 0.085f * s, 0.07f * s, 0.8f);
                    break;
                case 2: // bun
                    mesh.Ball(Bone.Head, Hair, new Vector3(0f, 0.14f * s, -0.01f), new Vector3(0.1f, 0.095f, 0.1f) * s);
                    mesh.Ball(Bone.Head, Hair, new Vector3(0f, 0.2f * s, -0.07f), new Vector3(0.045f, 0.045f, 0.045f) * s);
                    break;
                case 3: // cap
                    mesh.Ball(Bone.Head, Accent, new Vector3(0f, 0.16f * s, 0f), new Vector3(0.104f, 0.07f, 0.108f) * s);
                    mesh.Box(Bone.Head, Accent, new Vector3(0f, 0.15f * s, 0.11f * s), new Vector3(0.14f, 0.015f, 0.09f) * s);
                    break;
                case 4: // knitted hat
                    mesh.Ball(Bone.Head, Accent, new Vector3(0f, 0.165f * s, -0.005f), new Vector3(0.106f, 0.09f, 0.108f) * s);
                    mesh.Ball(Bone.Head, Accent, new Vector3(0f, 0.25f * s, -0.01f), new Vector3(0.035f, 0.035f, 0.035f) * s);
                    break;
                case 5:
                    break;
                default: // short
                    mesh.Ball(Bone.Head, Hair, new Vector3(0f, 0.145f * s, -0.01f), new Vector3(0.1f, 0.085f, 0.1f) * s);
                    break;
            }
            // Eyes, so faces read at a glance.
            for (int side = -1; side <= 1; side += 2)
                mesh.Ball(Bone.Head, Shoes, new Vector3(side * 0.035f * s, 0.11f * s, 0.095f * s), new Vector3(0.012f, 0.012f, 0.008f) * s);
            // Arms: sleeves and bare hands.
            for (int side = -1; side <= 1; side += 2)
            {
                bool left = side < 0;
                mesh.Tube(left ? Bone.UpperArmL : Bone.UpperArmR, Top, new Vector3(0f, 0.02f, 0f), new Vector3(0f, -0.29f * s, 0f), 0.055f * s * w, 0.045f * s * w, 1f);
                mesh.Tube(left ? Bone.ForearmL : Bone.ForearmR, Top, Vector3.zero, new Vector3(0f, -0.25f * s, 0f), 0.045f * s * w, 0.036f * s * w, 1f);
                mesh.Ball(left ? Bone.HandL : Bone.HandR, Skin, new Vector3(0f, -0.05f * s, 0f), new Vector3(0.035f, 0.07f, 0.02f) * s);
                // Legs: trousers (or bare legs under a skirt) and shoes.
                int legMaterial = look.Skirt ? Skin : Bottom;
                mesh.Tube(left ? Bone.ThighL : Bone.ThighR, legMaterial, new Vector3(0f, 0.04f, 0f), new Vector3(0f, -0.44f * s, 0f), 0.075f * s * w, 0.055f * s * w, 1f);
                mesh.Tube(left ? Bone.ShinL : Bone.ShinR, legMaterial, Vector3.zero, new Vector3(0f, -0.42f * s, 0f), 0.052f * s * w, 0.04f * s * w, 1f);
                mesh.Box(left ? Bone.FootL : Bone.FootR, Shoes, new Vector3(0f, -0.035f * s, 0.05f * s), new Vector3(0.09f, 0.07f, 0.24f) * s);
            }
            // Things they carry.
            if (look.Carry == 1)
                mesh.Box(Bone.Chest, Accent, new Vector3(0f, 0.02f * s, -0.16f * s * w), new Vector3(0.28f, 0.36f, 0.14f) * s);
            else if (look.Carry == 2)
            {
                mesh.Box(Bone.Hips, Accent, new Vector3(0.2f * s * w, 0.08f * s, 0.02f), new Vector3(0.06f, 0.22f, 0.26f) * s);
                mesh.Box(Bone.Chest, Accent, new Vector3(0.05f, 0.08f * s, 0.0f), new Vector3(0.03f, 0.5f, 0.02f) * s);
            }
            // A wheeled suitcase at the right side, part of the same mesh.
            if (look.Carry == 3)
            {
                mesh.Box(Bone.Root, Accent, new Vector3(0.32f * s, 0.32f, -0.25f), new Vector3(0.22f, 0.56f, 0.38f));
                mesh.Box(Bone.Root, Shoes, new Vector3(0.32f * s, 0.66f, -0.25f), new Vector3(0.03f, 0.14f, 0.03f));
            }
            mesh.Emit(name, Colours(look));

            PersonAnimator animator = person.AddComponent<PersonAnimator>();
            animator.Configure(bones, look);
            return animator;
        }

        /// <summary>Palette cells for skin, top, bottom, hair, shoes and accent.</summary>
        private static Vector2[] Colours(PersonLook look)
        {
            return new[]
            {
                WorldPalette.Uv(look.Skin, 0.35f),
                WorldPalette.Uv(look.Top, 0.2f),
                WorldPalette.Uv(look.Bottom, 0.2f),
                WorldPalette.Uv(look.HairColour, 0.3f),
                WorldPalette.Uv(look.Shoes, 0.4f),
                WorldPalette.Uv(look.Accent, 0.3f)
            };
        }

        /// <summary>Collects body parts per material and bone, then builds one skinned mesh.</summary>
        private sealed class PartBuilder
        {
            private readonly Transform root;
            private readonly Transform[] bones;
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<BoneWeight> weights = new List<BoneWeight>();
            private readonly List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>() };

            public PartBuilder(Transform owner, Transform[] skeleton)
            {
                root = owner;
                bones = skeleton;
            }

            private Vector3 ToRoot(Bone bone, Vector3 local) => root.InverseTransformPoint(bones[(int)bone].TransformPoint(local));
            private Vector3 DirToRoot(Bone bone, Vector3 local) => root.InverseTransformDirection(bones[(int)bone].TransformDirection(local));

            private void Vertex(Bone bone, Vector3 position, Vector3 normal)
            {
                vertices.Add(ToRoot(bone, position));
                normals.Add(DirToRoot(bone, normal).normalized);
                weights.Add(new BoneWeight { boneIndex0 = (int)bone, weight0 = 1f });
            }

            /// <summary>A rounded, tapered tube between two points; depthScale flattens it front to back.</summary>
            public void Tube(Bone bone, int material, Vector3 from, Vector3 to, float r0, float r1, float depthScale)
            {
                const int sides = 8, rings = 4;
                Vector3 axis = (to - from).normalized;
                Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.z) > 0.9f ? Vector3.right : Vector3.forward).normalized;
                Vector3 front = Vector3.Cross(side, axis).normalized;
                int start = vertices.Count;
                for (int r = 0; r <= rings; r++)
                {
                    float t = r / (float)rings;
                    // Round ends: the radius tapers into a dome at each end.
                    float end = Mathf.Sin(Mathf.Lerp(0.35f, Mathf.PI - 0.35f, t));
                    float radius = Mathf.Lerp(r0, r1, t) * Mathf.Lerp(0.75f, 1f, end);
                    Vector3 centre = Vector3.Lerp(from, to, t);
                    for (int k = 0; k < sides; k++)
                    {
                        float a = k / (float)sides * Mathf.PI * 2f;
                        Vector3 n = side * Mathf.Cos(a) + front * Mathf.Sin(a) * depthScale;
                        Vertex(bone, centre + n * radius, side * Mathf.Cos(a) + front * Mathf.Sin(a) / Mathf.Max(0.3f, depthScale));
                    }
                }
                for (int r = 0; r < rings; r++)
                    for (int k = 0; k < sides; k++)
                    {
                        int a = start + r * sides + k, b = start + r * sides + (k + 1) % sides;
                        int c = a + sides, d = b + sides;
                        triangles[material].AddRange(new[] { a, c, b, b, c, d });
                    }
                Cap(bone, material, from, -axis, start, sides, false);
                Cap(bone, material, to, axis, start + rings * sides, sides, true);
            }

            private void Cap(Bone bone, int material, Vector3 centre, Vector3 normal, int ring, int sides, bool top)
            {
                int c = vertices.Count;
                Vertex(bone, centre, normal);
                for (int k = 0; k < sides; k++)
                {
                    int a = ring + k, b = ring + (k + 1) % sides;
                    if (top) triangles[material].AddRange(new[] { a, b, c });
                    else triangles[material].AddRange(new[] { a, c, b });
                }
            }

            public void Ball(Bone bone, int material, Vector3 centre, Vector3 radii)
            {
                const int lat = 6, lon = 10;
                int start = vertices.Count;
                for (int i = 0; i <= lat; i++)
                {
                    float v = i / (float)lat * Mathf.PI;
                    for (int j = 0; j < lon; j++)
                    {
                        float u = j / (float)lon * Mathf.PI * 2f;
                        Vector3 n = new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u));
                        Vertex(bone, centre + Vector3.Scale(n, radii), new Vector3(n.x / radii.x, n.y / radii.y, n.z / radii.z));
                    }
                }
                for (int i = 0; i < lat; i++)
                    for (int j = 0; j < lon; j++)
                    {
                        int a = start + i * lon + j, b = start + i * lon + (j + 1) % lon;
                        int c = a + lon, d = b + lon;
                        triangles[material].AddRange(new[] { a, b, c, b, d, c });
                    }
            }

            public void Box(Bone bone, int material, Vector3 centre, Vector3 size)
            {
                Vector3 h = size * 0.5f;
                Vector3[] n = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                foreach (Vector3 normal in n)
                {
                    Vector3 u = normal.x != 0f ? Vector3.forward : Vector3.right;
                    Vector3 v = Vector3.Cross(normal, u);
                    int start = vertices.Count;
                    Vector3 c = centre + Vector3.Scale(normal, h);
                    Vector3 du = Vector3.Scale(u, h), dv = Vector3.Scale(v, h);
                    Vertex(bone, c - du - dv, normal); Vertex(bone, c + du - dv, normal);
                    Vertex(bone, c + du + dv, normal); Vertex(bone, c - du + dv, normal);
                    triangles[material].AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                }
            }

            public void Emit(string name, Vector2[] colours)
            {
                Mesh mesh = new Mesh { name = "Person_" + name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.boneWeights = weights.ToArray();
                // Every part's vertices point at its colour in the shared palette: one submesh, one draw.
                Vector2[] uvs = new Vector2[vertices.Count];
                for (int m = 0; m < triangles.Length; m++)
                    foreach (int index in triangles[m]) uvs[index] = colours[m];
                mesh.uv = uvs;
                // Face every triangle outward (along its vertex normals), whatever order it was added in.
                for (int m = 0; m < triangles.Length; m++)
                {
                    List<int> list = triangles[m];
                    for (int i = 0; i + 2 < list.Count; i += 3)
                    {
                        Vector3 a = vertices[list[i]], b = vertices[list[i + 1]], c = vertices[list[i + 2]];
                        Vector3 face = Vector3.Cross(b - a, c - a);
                        if (Vector3.Dot(face, normals[list[i]] + normals[list[i + 1]] + normals[list[i + 2]]) < 0f)
                        {
                            int swap = list[i + 1];
                            list[i + 1] = list[i + 2];
                            list[i + 2] = swap;
                        }
                    }
                }
                List<int> all = new List<int>();
                foreach (List<int> list in triangles) all.AddRange(list);
                mesh.SetTriangles(all, 0);
                Matrix4x4[] bind = new Matrix4x4[bones.Length];
                for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * root.localToWorldMatrix;
                mesh.bindposes = bind;
                mesh.RecalculateBounds();
                GameObject skin = new GameObject("Body");
                skin.transform.SetParent(root, false);
                SkinnedMeshRenderer renderer = skin.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                renderer.bones = bones;
                renderer.rootBone = bones[(int)Bone.Hips];
                renderer.sharedMaterial = WorldPalette.Material;
                renderer.updateWhenOffscreen = false;
                renderer.localBounds = new Bounds(new Vector3(0f, -0.1f, 0f), new Vector3(1.4f, 2.4f, 1.4f));
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }
}
