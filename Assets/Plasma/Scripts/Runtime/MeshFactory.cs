using System.Collections.Generic;
using UnityEngine;

namespace Plasma
{
    /// <summary>
    /// Procedural low-poly meshes (no imported art needed). Vertex colour = base colour,
    /// vertex alpha = tint mask for the Plasma/Lit shader (1 = takes the instance colour).
    /// For Plasma/Fx meshes the vertex colour (incl. alpha) multiplies the instance colour.
    /// </summary>
    public static class MeshFactory
    {
        public class Builder
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<int> T = new List<int>();

            public void Box(Vector3 center, Vector3 size, Color col) => Box(center, size, col, Quaternion.identity);
            public void Box(Vector3 center, Vector3 size, Color col, Quaternion rot)
            {
                Vector3 h = size * 0.5f;
                Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                foreach (var d in dirs)
                {
                    Vector3 u = Mathf.Abs(d.y) > 0.5f ? Vector3.right : Vector3.up;
                    Vector3 w = Vector3.Cross(d, u);
                    int b = V.Count;
                    Vector3 fc = Vector3.Scale(d, h);
                    Vector3 uu = Vector3.Scale(u, h), ww = Vector3.Scale(w, h);
                    V.Add(center + rot * (fc - uu - ww)); V.Add(center + rot * (fc + uu - ww)); V.Add(center + rot * (fc + uu + ww)); V.Add(center + rot * (fc - uu + ww));
                    for (int k = 0; k < 4; k++) { N.Add(rot * d); C.Add(col); }
                    T.Add(b); T.Add(b + 1); T.Add(b + 2); T.Add(b); T.Add(b + 2); T.Add(b + 3);
                }
            }

            /// <summary>Box with rounded edges (radius r); bulge pushes the ±z faces out like a pillow.</summary>
            public void RoundedBox(Vector3 center, Vector3 size, float r, Color col, int seg = 4, float bulge = 0f, Quaternion? rotation = null, int mid = 1)
            {
                var rot = rotation ?? Quaternion.identity;
                Vector3 h = size * 0.5f;
                Vector3 inner = new Vector3(Mathf.Max(0, h.x - r), Mathf.Max(0, h.y - r), Mathf.Max(0, h.z - r));
                Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                int n = seg * 2 + mid;
                foreach (var d in dirs)
                {
                    Vector3 u = Mathf.Abs(d.y) > 0.5f ? Vector3.right : Vector3.up;
                    Vector3 w = Vector3.Cross(d, u);
                    float hu = Vector3.Dot(Abs(u), h), hw = Vector3.Dot(Abs(w), h), iu = Vector3.Dot(Abs(u), inner), iw = Vector3.Dot(Abs(w), inner);
                    int b = V.Count;
                    for (int i = 0; i <= n; i++)
                        for (int j = 0; j <= n; j++)
                        {
                            Vector3 p = Vector3.Scale(d, h) + u * Coord(i, seg, mid, hu, iu) + w * Coord(j, seg, mid, hw, iw);
                            Vector3 q = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                            Vector3 nn = p - q; nn = nn.sqrMagnitude < 1e-8f ? d : nn.normalized;
                            Vector3 vtx = q + nn * r;
                            if (bulge != 0 && Mathf.Abs(nn.z) > 0.01f)
                            {
                                float fx = Mathf.Clamp01(1 - (vtx.x / h.x) * (vtx.x / h.x)), fy = Mathf.Clamp01(1 - (vtx.y / h.y) * (vtx.y / h.y));
                                vtx.z += Mathf.Sign(nn.z) * bulge * fx * fy * Mathf.Abs(nn.z);
                            }
                            V.Add(center + rot * vtx); N.Add(rot * nn); C.Add(col);
                        }
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < n; j++)
                        {
                            int i0 = b + i * (n + 1) + j, i1 = i0 + 1, i2 = i0 + n + 1, i3 = i2 + 1;
                            T.Add(i0); T.Add(i1); T.Add(i3); T.Add(i0); T.Add(i3); T.Add(i2);
                        }
                }
            }

            static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            /// <summary>Grid coordinate: seg steps across each rounded edge, one step across the flat middle.</summary>
            static float Coord(int i, int seg, int mid, float h, float inner)
            {
                if (seg <= 0) return Mathf.Lerp(-h, h, (float)i / mid);
                if (i <= seg) return -inner - (h - inner) * (1f - (float)i / seg);
                if (i < seg + mid) return Mathf.Lerp(-inner, inner, (float)(i - seg) / mid);
                return inner + (h - inner) * ((float)(i - seg - mid) / seg);
            }

            public void Sphere(Vector3 center, Vector3 radius, Color col, int lon = 10, int lat = 7, float minLat = 0f) => Sphere(center, radius, _ => col, lon, lat, minLat);

            public void Sphere(Vector3 center, Vector3 radius, System.Func<Vector3, Color> colorOf, int lon, int lat, float minLat = 0f)
            {
                int b = V.Count;
                for (int y = 0; y <= lat; y++)
                {
                    float fy = Mathf.Lerp(minLat, 1f, (float)y / lat);
                    float phi = Mathf.PI * (1 - fy);
                    for (int x = 0; x <= lon; x++)
                    {
                        float th = 2 * Mathf.PI * x / lon;
                        var n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                        V.Add(center + Vector3.Scale(n, radius)); N.Add(n); C.Add(colorOf(n));
                    }
                }
                for (int y = 0; y < lat; y++)
                    for (int x = 0; x < lon; x++)
                    {
                        int i0 = b + y * (lon + 1) + x, i1 = i0 + 1, i2 = i0 + lon + 1, i3 = i2 + 1;
                        T.Add(i0); T.Add(i2); T.Add(i1); T.Add(i1); T.Add(i2); T.Add(i3);
                    }
            }

            public void Cylinder(Vector3 center, float radius, float height, Color col, int sides = 10, float topRadius = -1)
            {
                if (topRadius < 0) topRadius = radius;
                int b = V.Count;
                for (int i = 0; i <= sides; i++)
                {
                    float a = 2 * Mathf.PI * i / sides;
                    var n = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    V.Add(center + n * radius + Vector3.down * height * 0.5f); N.Add(n); C.Add(col);
                    V.Add(center + n * topRadius + Vector3.up * height * 0.5f); N.Add(n); C.Add(col);
                }
                for (int i = 0; i < sides; i++) { int k = b + i * 2; T.Add(k); T.Add(k + 1); T.Add(k + 2); T.Add(k + 2); T.Add(k + 1); T.Add(k + 3); }
                int top = V.Count; V.Add(center + Vector3.up * height * 0.5f); N.Add(Vector3.up); C.Add(col);
                for (int i = 0; i <= sides; i++) { float a = 2 * Mathf.PI * i / sides; V.Add(center + new Vector3(Mathf.Cos(a) * topRadius, height * 0.5f, Mathf.Sin(a) * topRadius)); N.Add(Vector3.up); C.Add(col); }
                for (int i = 0; i < sides; i++) { T.Add(top); T.Add(top + i + 2); T.Add(top + i + 1); }
            }

            /// <summary>Cone pointing along dir (for hair spikes).</summary>
            public void Cone(Vector3 basePos, Vector3 dir, float radius, Color col, int sides = 6)
            {
                Vector3 tip = basePos + dir;
                Vector3 a = Vector3.Cross(dir.normalized, Vector3.right); if (a.sqrMagnitude < 0.01f) a = Vector3.Cross(dir.normalized, Vector3.forward);
                a.Normalize(); Vector3 bb = Vector3.Cross(dir.normalized, a);
                int b0 = V.Count;
                for (int i = 0; i < sides; i++)
                {
                    float t0 = 2 * Mathf.PI * i / sides, t1 = 2 * Mathf.PI * (i + 1) / sides;
                    Vector3 p0 = basePos + (a * Mathf.Cos(t0) + bb * Mathf.Sin(t0)) * radius, p1 = basePos + (a * Mathf.Cos(t1) + bb * Mathf.Sin(t1)) * radius;
                    Vector3 nn = Vector3.Cross(p1 - tip, p0 - tip).normalized;
                    int k = V.Count; V.Add(p0); V.Add(p1); V.Add(tip); N.Add(nn); N.Add(nn); N.Add(nn); C.Add(col); C.Add(col); C.Add(col);
                    T.Add(k); T.Add(k + 2); T.Add(k + 1);
                }
            }

            /// <summary>Flat disc on the XZ plane, alpha fading from centre to rim (blob shadow).</summary>
            public void Disc(float radius, Color centre, Color rim, int sides = 16)
            {
                int c = V.Count; V.Add(Vector3.zero); N.Add(Vector3.up); C.Add(centre);
                for (int i = 0; i <= sides; i++) { float a = 2 * Mathf.PI * i / sides; V.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius); N.Add(Vector3.up); C.Add(rim); }
                for (int i = 0; i < sides; i++) { T.Add(c); T.Add(c + i + 2); T.Add(c + i + 1); }
            }

            /// <summary>Flat ring on the XY plane (badge outline).</summary>
            public void Ring(float r0, float r1, Color col, int sides = 32)
            {
                int b = V.Count;
                for (int i = 0; i <= sides; i++)
                {
                    float a = 2 * Mathf.PI * i / sides; var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                    V.Add(d * r0); V.Add(d * r1); N.Add(Vector3.back); N.Add(Vector3.back); C.Add(col); C.Add(col);
                }
                for (int i = 0; i < sides; i++) { int k = b + i * 2; T.Add(k); T.Add(k + 2); T.Add(k + 1); T.Add(k + 1); T.Add(k + 2); T.Add(k + 3); }
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                if (V.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(V); m.SetNormals(N); m.SetColors(C); m.SetTriangles(T, 0);
                m.RecalculateBounds();
                m.UploadMeshData(false);
                return m;
            }
        }

        static readonly Color Skin = new Color(1f, 0.8f, 0.64f, 0f);
        static readonly Color Navy = new Color(0.13f, 0.16f, 0.3f, 0f);
        static readonly Color Gun = new Color(0.12f, 0.12f, 0.14f, 0f);
        static readonly Color Tint = new Color(1, 1, 1, 1);
        static readonly Color Tint90 = new Color(0.86f, 0.86f, 0.86f, 1);
        static readonly Color Boot = new Color(0.22f, 0.12f, 0.08f, 0f);
        static readonly Color Belt = new Color(0.95f, 0.75f, 0.3f, 0f);

        static Mesh _soldier, _enemy, _enemyLod, _cube, _sphere, _flame, _boss, _pillow, _tile, _shadow, _ring, _piece, _rail;

        /// <summary>Chibi soldier (~0.5 tall) facing +z: navy uniform, big glossy helmet (tinted), rifle.</summary>
        public static Mesh Soldier => _soldier ??= BuildSoldier();
        static Mesh BuildSoldier()
        {
            var b = new Builder();
            b.Box(new Vector3(-0.055f, 0.06f, 0), new Vector3(0.075f, 0.12f, 0.09f), Navy);
            b.Box(new Vector3(0.055f, 0.06f, 0), new Vector3(0.075f, 0.12f, 0.09f), Navy);
            b.RoundedBox(new Vector3(0, 0.2f, 0), new Vector3(0.24f, 0.2f, 0.17f), 0.06f, Navy, 2);
            b.Box(new Vector3(0, 0.12f, 0), new Vector3(0.25f, 0.03f, 0.18f), Belt);
            b.Box(new Vector3(-0.14f, 0.2f, 0.04f), new Vector3(0.06f, 0.15f, 0.07f), Navy);
            b.Box(new Vector3(0.14f, 0.2f, 0.06f), new Vector3(0.06f, 0.07f, 0.16f), Navy);
            b.Sphere(new Vector3(0, 0.36f, 0.01f), new Vector3(0.105f, 0.1f, 0.1f), Skin, 8, 6);
            b.Sphere(new Vector3(0, 0.385f, -0.01f), new Vector3(0.135f, 0.12f, 0.14f), Tint, 10, 6, 0.42f); // helmet
            b.Cylinder(new Vector3(0, 0.36f, -0.01f), 0.14f, 0.025f, Tint90, 10);                          // helmet rim
            b.Box(new Vector3(0.12f, 0.22f, 0.2f), new Vector3(0.045f, 0.05f, 0.3f), Gun);                 // rifle
            return b.Build("Soldier");
        }

        /// <summary>Horde grunt: round red body + head (tinted), dark boots. ~0.42 tall.</summary>
        public static Mesh Enemy => _enemy ??= BuildEnemy(false);
        /// <summary>Far-away version (fewer triangles).</summary>
        public static Mesh EnemyLod => _enemyLod ??= BuildEnemy(true);
        static Mesh BuildEnemy(bool lod)
        {
            var b = new Builder();
            if (!lod)
            {
                b.Box(new Vector3(-0.06f, 0.04f, 0), new Vector3(0.08f, 0.08f, 0.1f), Boot);
                b.Box(new Vector3(0.06f, 0.04f, 0), new Vector3(0.08f, 0.08f, 0.1f), Boot);
            }
            else b.Box(new Vector3(0, 0.04f, 0), new Vector3(0.2f, 0.08f, 0.1f), Boot);
            int lon = lod ? 6 : 9, lat = lod ? 4 : 6;
            b.Sphere(new Vector3(0, 0.19f, 0), new Vector3(0.16f, 0.13f, 0.15f), Tint90, lon, lat);
            b.Sphere(new Vector3(0, 0.33f, 0.01f), new Vector3(0.12f, 0.11f, 0.12f), Tint, lon, lat);
            return b.Build(lod ? "EnemyLod" : "Enemy");
        }

        /// <summary>Brute boss (~1 unit tall before scaling): red jacket (tinted), white shirt, huge blade on the shoulder.</summary>
        public static Mesh Boss => _boss ??= BuildBoss();
        static Mesh BuildBoss()
        {
            var b = new Builder();
            var jeans = new Color(0.25f, 0.3f, 0.45f, 0f);
            var shirt = new Color(0.97f, 0.97f, 0.97f, 0f);
            var hair = new Color(1f, 0.55f, 0.12f, 0f);
            var blade = new Color(0.16f, 0.16f, 0.19f, 0f);
            var edge = new Color(0.78f, 0.8f, 0.85f, 0f);
            b.RoundedBox(new Vector3(-0.12f, 0.14f, 0), new Vector3(0.17f, 0.3f, 0.18f), 0.05f, jeans, 2);
            b.RoundedBox(new Vector3(0.12f, 0.14f, 0), new Vector3(0.17f, 0.3f, 0.18f), 0.05f, jeans, 2);
            b.Box(new Vector3(-0.12f, 0.02f, 0.03f), new Vector3(0.19f, 0.05f, 0.25f), Boot);
            b.Box(new Vector3(0.12f, 0.02f, 0.03f), new Vector3(0.19f, 0.05f, 0.25f), Boot);
            b.RoundedBox(new Vector3(0, 0.5f, 0), new Vector3(0.56f, 0.46f, 0.34f), 0.12f, Tint, 3);       // jacket
            b.RoundedBox(new Vector3(0, 0.47f, 0.13f), new Vector3(0.2f, 0.38f, 0.1f), 0.04f, shirt, 2);    // shirt
            b.RoundedBox(new Vector3(-0.36f, 0.5f, 0.02f), new Vector3(0.18f, 0.3f, 0.18f), 0.07f, Tint, 2); // sleeves
            b.RoundedBox(new Vector3(0.36f, 0.56f, 0.02f), new Vector3(0.18f, 0.26f, 0.18f), 0.07f, Tint, 2);
            b.Sphere(new Vector3(-0.38f, 0.3f, 0.05f), new Vector3(0.1f, 0.1f, 0.1f), Skin, 8, 6);          // fists
            b.Sphere(new Vector3(0.38f, 0.38f, 0.08f), new Vector3(0.1f, 0.1f, 0.1f), Skin, 8, 6);
            b.Sphere(new Vector3(0, 0.86f, 0.02f), new Vector3(0.15f, 0.16f, 0.15f), Skin, 10, 7);           // head
            b.Box(new Vector3(0, 0.83f, 0.15f), new Vector3(0.12f, 0.04f, 0.04f), new Color(0.4f, 0.2f, 0.12f, 0f)); // brow
            for (int i = 0; i < 7; i++)
            {
                float a = -1.1f + i * 0.37f;
                b.Cone(new Vector3(Mathf.Sin(a) * 0.09f, 0.96f, -0.02f + Mathf.Cos(a) * 0.03f), new Vector3(Mathf.Sin(a) * 0.1f, 0.2f, -0.05f), 0.06f, hair);
            }
            var tilt = Quaternion.Euler(0, 0, -28);
            b.RoundedBox(new Vector3(0.42f, 0.98f, -0.12f), new Vector3(0.18f, 0.95f, 0.05f), 0.03f, blade, 1, 0, tilt); // blade
            b.RoundedBox(new Vector3(0.44f, 0.99f, -0.12f) + tilt * new Vector3(0.1f, 0, 0), new Vector3(0.03f, 0.92f, 0.06f), 0.01f, edge, 1, 0, tilt);
            b.Box(new Vector3(0.24f, 0.6f, -0.12f), new Vector3(0.24f, 0.05f, 0.09f), new Color(0.35f, 0.25f, 0.1f, 0f), tilt); // guard
            return b.Build("Boss");
        }

        /// <summary>Upgrade gate: a puffy pillow (tinted), unit size.</summary>
        public static Mesh Pillow => _pillow ??= Single(b => b.RoundedBox(new Vector3(0, 0.5f, 0), Vector3.one, 0.3f, Tint, 4, 0.12f, null, 6), "Pillow");
        /// <summary>Conveyor tile: a rounded slab (tinted), unit size, standing on y=0.</summary>
        public static Mesh Tile => _tile ??= Single(b => b.RoundedBox(new Vector3(0, 0.5f, 0), Vector3.one, 0.12f, Tint, 2), "Tile");
        public static Mesh Cube => _cube ??= Single(b => b.Box(Vector3.zero, Vector3.one, Tint), "Cube");
        public static Mesh RoundedRail => _rail ??= Single(b => b.RoundedBox(Vector3.zero, Vector3.one, 0.35f, Tint, 2), "Rail");
        public static Mesh Piece => _piece ??= Single(b => b.RoundedBox(Vector3.zero, Vector3.one, 0.25f, Tint, 1), "Piece");
        public static Mesh Sphere => _sphere ??= Single(b => b.Sphere(Vector3.zero, Vector3.one * 0.5f, Tint, 10, 7), "Sphere");
        public static Mesh Shadow => _shadow ??= Single(b => b.Disc(0.5f, new Color(1, 1, 1, 1), new Color(1, 1, 1, 0)), "Shadow");
        public static Mesh Ring => _ring ??= Single(b => b.Ring(0.42f, 0.5f, Tint), "Ring");

        /// <summary>Tracer flame along +z: hot white-yellow head, orange tail fading out.</summary>
        public static Mesh Flame => _flame ??= Single(b => b.Sphere(Vector3.zero, Vector3.one * 0.5f, n =>
        {
            float t = (n.z + 1) * 0.5f;
            var tail = new Color(1f, 0.38f, 0.0f, 0f); var mid = new Color(1f, 0.62f, 0.05f, 0.95f); var head = new Color(1f, 0.97f, 0.65f, 1f);
            return t < 0.55f ? Color.Lerp(tail, mid, t / 0.55f) : Color.Lerp(mid, head, (t - 0.55f) / 0.45f);
        }, 8, 6), "Flame");

        static Mesh Single(System.Action<Builder> f, string name) { var b = new Builder(); f(b); return b.Build(name); }
    }
}
