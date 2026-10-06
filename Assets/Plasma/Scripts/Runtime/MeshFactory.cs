using System.Collections.Generic;
using UnityEngine;

namespace Plasma
{
    /// <summary>
    /// Procedural low-poly meshes (no imported art needed). Vertex colour = base colour,
    /// vertex alpha = tint mask for the Plasma/Lit shader (1 = takes the instance colour).
    /// </summary>
    public static class MeshFactory
    {
        class Builder
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<int> T = new List<int>();

            public void Box(Vector3 center, Vector3 size, Color col)
            {
                Vector3 h = size * 0.5f;
                Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                foreach (var d in dirs)
                {
                    Vector3 u = Mathf.Abs(d.y) > 0.5f ? Vector3.right : Vector3.up;
                    Vector3 w = Vector3.Cross(d, u);
                    int b = V.Count;
                    Vector3 fc = center + Vector3.Scale(d, h);
                    Vector3 uu = Vector3.Scale(u, h), ww = Vector3.Scale(w, h);
                    V.Add(fc - uu - ww); V.Add(fc + uu - ww); V.Add(fc + uu + ww); V.Add(fc - uu + ww);
                    for (int k = 0; k < 4; k++) { N.Add(d); C.Add(col); }
                    T.Add(b); T.Add(b + 1); T.Add(b + 2); T.Add(b); T.Add(b + 2); T.Add(b + 3);
                }
            }

            public void Sphere(Vector3 center, Vector3 radius, Color col, int lon = 10, int lat = 7, float minLat = 0f)
            {
                int b = V.Count;
                for (int y = 0; y <= lat; y++)
                {
                    float fy = Mathf.Lerp(minLat, 1f, (float)y / lat);
                    float phi = Mathf.PI * (1 - fy);              // from bottom (pi) to top (0)
                    for (int x = 0; x <= lon; x++)
                    {
                        float th = 2 * Mathf.PI * x / lon;
                        var n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                        V.Add(center + Vector3.Scale(n, radius)); N.Add(n); C.Add(col);
                    }
                }
                for (int y = 0; y < lat; y++)
                    for (int x = 0; x < lon; x++)
                    {
                        int i0 = b + y * (lon + 1) + x, i1 = i0 + 1, i2 = i0 + lon + 1, i3 = i2 + 1;
                        T.Add(i0); T.Add(i2); T.Add(i1); T.Add(i1); T.Add(i2); T.Add(i3);
                    }
            }

            public void Cylinder(Vector3 center, float radius, float height, Color col, int sides = 10)
            {
                int b = V.Count;
                for (int i = 0; i <= sides; i++)
                {
                    float a = 2 * Mathf.PI * i / sides;
                    var n = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    V.Add(center + n * radius + Vector3.down * height * 0.5f); N.Add(n); C.Add(col);
                    V.Add(center + n * radius + Vector3.up * height * 0.5f); N.Add(n); C.Add(col);
                }
                for (int i = 0; i < sides; i++) { int k = b + i * 2; T.Add(k); T.Add(k + 1); T.Add(k + 2); T.Add(k + 2); T.Add(k + 1); T.Add(k + 3); }
                // top cap
                int top = V.Count; V.Add(center + Vector3.up * height * 0.5f); N.Add(Vector3.up); C.Add(col);
                for (int i = 0; i <= sides; i++) { float a = 2 * Mathf.PI * i / sides; V.Add(center + new Vector3(Mathf.Cos(a) * radius, height * 0.5f, Mathf.Sin(a) * radius)); N.Add(Vector3.up); C.Add(col); }
                for (int i = 0; i < sides; i++) { T.Add(top); T.Add(top + i + 2); T.Add(top + i + 1); }
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(V); m.SetNormals(N); m.SetColors(C); m.SetTriangles(T, 0);
                m.RecalculateBounds();
                m.UploadMeshData(false);
                return m;
            }
        }

        static readonly Color Skin = new Color(0.98f, 0.78f, 0.62f, 0f);
        static readonly Color Dark = new Color(0.16f, 0.17f, 0.22f, 0f);
        static readonly Color TeamWhite = new Color(1, 1, 1, 1);      // tinted by instance colour
        static readonly Color Belt = new Color(0.45f, 0.32f, 0.2f, 0f);

        static Mesh _soldier, _enemy, _cube, _sphere, _bullet, _boss, _weapon;

        /// <summary>Little helmeted soldier (~0.55 units tall), facing +z, with a rifle.</summary>
        public static Mesh Soldier => _soldier ??= BuildCharacter("Soldier", true);
        /// <summary>Enemy grunt: same body, bigger round "hood", no rifle.</summary>
        public static Mesh Enemy => _enemy ??= BuildCharacter("Enemy", false);

        static Mesh BuildCharacter(string name, bool rifle)
        {
            var b = new Builder();
            b.Box(new Vector3(-0.06f, 0.08f, 0), new Vector3(0.08f, 0.16f, 0.09f), Dark);    // legs
            b.Box(new Vector3(0.06f, 0.08f, 0), new Vector3(0.08f, 0.16f, 0.09f), Dark);
            b.Cylinder(new Vector3(0, 0.25f, 0), 0.12f, 0.2f, rifle ? new Color(0.93f, 0.86f, 0.62f, 0f) : TeamWhite, 8); // torso
            b.Cylinder(new Vector3(0, 0.17f, 0), 0.125f, 0.035f, Belt, 8);
            b.Sphere(new Vector3(0, 0.43f, 0), new Vector3(0.12f, 0.12f, 0.12f), Skin, 8, 6);  // head
            if (rifle)
            {
                b.Sphere(new Vector3(0, 0.47f, 0), new Vector3(0.14f, 0.11f, 0.14f), TeamWhite, 10, 5, 0.45f); // helmet
                b.Box(new Vector3(0.1f, 0.27f, 0.14f), new Vector3(0.04f, 0.05f, 0.26f), Dark); // rifle
            }
            else
            {
                b.Sphere(new Vector3(0, 0.47f, -0.01f), new Vector3(0.16f, 0.14f, 0.16f), TeamWhite, 10, 6, 0.35f); // hood
                b.Box(new Vector3(-0.15f, 0.27f, 0.05f), new Vector3(0.05f, 0.14f, 0.05f), Skin);  // arms
                b.Box(new Vector3(0.15f, 0.27f, 0.05f), new Vector3(0.05f, 0.14f, 0.05f), Skin);
            }
            return b.Build(name);
        }

        /// <summary>Brute boss: chunky body + a huge blade (tinted), ~1 unit tall before scaling.</summary>
        public static Mesh Boss => _boss ??= BuildBoss();
        static Mesh BuildBoss()
        {
            var b = new Builder();
            b.Box(new Vector3(-0.13f, 0.14f, 0), new Vector3(0.16f, 0.28f, 0.16f), Dark);
            b.Box(new Vector3(0.13f, 0.14f, 0), new Vector3(0.16f, 0.28f, 0.16f), Dark);
            b.Box(new Vector3(0, 0.48f, 0), new Vector3(0.5f, 0.42f, 0.3f), TeamWhite);              // jacket
            b.Box(new Vector3(0, 0.46f, 0.151f), new Vector3(0.16f, 0.36f, 0.01f), new Color(0.95f, 0.95f, 0.95f, 0f)); // shirt
            b.Box(new Vector3(-0.32f, 0.47f, 0.02f), new Vector3(0.14f, 0.4f, 0.14f), Skin);        // arms
            b.Box(new Vector3(0.32f, 0.5f, 0.02f), new Vector3(0.14f, 0.36f, 0.14f), Skin);
            b.Sphere(new Vector3(0, 0.83f, 0), new Vector3(0.16f, 0.17f, 0.16f), Skin, 10, 7);    // head
            b.Sphere(new Vector3(0, 0.95f, -0.02f), new Vector3(0.15f, 0.12f, 0.15f), new Color(1f, 0.82f, 0.2f, 0f), 8, 5, 0.4f); // hair
            b.Box(new Vector3(0.36f, 0.95f, -0.05f), new Vector3(0.08f, 0.9f, 0.05f), new Color(0.75f, 0.77f, 0.8f, 0f)); // blade
            b.Box(new Vector3(0.36f, 0.48f, -0.05f), new Vector3(0.16f, 0.05f, 0.08f), Dark);          // guard
            return b.Build("Boss");
        }

        public static Mesh Cube => _cube ??= BuildCube();
        static Mesh BuildCube() { var b = new Builder(); b.Box(Vector3.zero, Vector3.one, new Color(1, 1, 1, 1)); return b.Build("Cube"); }

        public static Mesh Sphere => _sphere ??= BuildSphere();
        static Mesh BuildSphere() { var b = new Builder(); b.Sphere(Vector3.zero, Vector3.one * 0.5f, new Color(1, 1, 1, 1), 10, 7); return b.Build("Sphere"); }

        /// <summary>Elongated tracer (unit length along +z).</summary>
        public static Mesh Bullet => _bullet ??= BuildBullet();
        static Mesh BuildBullet() { var b = new Builder(); b.Sphere(Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f), Color.white, 6, 4); return b.Build("Bullet"); }

        /// <summary>A rounded gate block: unit box with bevel-ish top (tinted).</summary>
        public static Mesh GateBlock => _weapon ??= BuildGate();
        static Mesh BuildGate()
        {
            var b = new Builder();
            b.Box(new Vector3(0, 0.5f, 0), new Vector3(1f, 0.86f, 0.35f), TeamWhite);
            b.Cylinder(new Vector3(0, 0.93f, 0), 0.0f, 0.0f, TeamWhite, 3);
            b.Box(new Vector3(0, 0.95f, 0), new Vector3(0.92f, 0.1f, 0.3f), TeamWhite);
            b.Box(new Vector3(0, 0.04f, 0), new Vector3(1.04f, 0.08f, 0.4f), new Color(0.35f, 0.37f, 0.42f, 0f));
            return b.Build("Gate");
        }
    }
}
