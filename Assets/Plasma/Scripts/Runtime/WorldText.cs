using System.Collections.Generic;
using UnityEngine;

namespace Plasma
{
    /// <summary>
    /// Batched world-space text with a thick outline (the chunky "+5" look of the reference).
    /// Every label added during a frame is built into ONE dynamic mesh and drawn with one call,
    /// so 100 conveyor tiles cost a single draw call. Glyphs come from the dynamic Lalezar font
    /// (Latin + Arabic presentation forms).
    /// </summary>
    public class WorldText
    {
        const int FontSize = 64;
        struct Item { public string S; public Vector3 P; public Quaternion R; public float Size; public Color C, O; public float Outline; }

        readonly List<Item> _items = new List<Item>(256);
        readonly List<Vector3> _v = new List<Vector3>(4096);
        readonly List<Vector2> _uv = new List<Vector2>(4096);
        readonly List<Color> _c = new List<Color>(4096);
        readonly List<int> _t = new List<int>(6144);
        readonly Mesh _mesh;
        readonly Material _mat;
        static readonly Vector2[] Dirs = { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1), new Vector2(0.7f, 0.7f), new Vector2(-0.7f, 0.7f), new Vector2(0.7f, -0.7f), new Vector2(-0.7f, -0.7f) };

        public WorldText(bool onTop)
        {
            _mesh = new Mesh { name = "WorldText" };
            _mesh.MarkDynamic();
            _mat = new Material(Shader.Find("Plasma/Text")) { name = onTop ? "TextOnTop" : "TextWorld" };
            _mat.SetFloat("_ZTest", onTop ? (float)UnityEngine.Rendering.CompareFunction.Always : (float)UnityEngine.Rendering.CompareFunction.LessEqual);
            _mat.renderQueue = onTop ? 3200 : 3100;
        }

        /// <summary>Queues a centred label. size = cap height in world units; outline = relative thickness.</summary>
        public void Add(string s, Vector3 pos, Quaternion rot, float size, Color color, Color outlineColor, float outline = 0.09f)
        {
            if (string.IsNullOrEmpty(s) || color.a <= 0.01f) return;
            _items.Add(new Item { S = s, P = pos, R = rot, Size = size, C = color, O = outlineColor, Outline = outline });
        }

        public void Flush()
        {
            var font = Visuals.Font;
            foreach (var it in _items) font.RequestCharactersInTexture(it.S, FontSize, FontStyle.Normal);
            _v.Clear(); _uv.Clear(); _c.Clear(); _t.Clear();
            foreach (var it in _items)
            {
                if (it.Outline > 0 && it.O.a > 0)
                {
                    float w = it.Size * it.Outline;
                    var oc = it.O; oc.a *= it.C.a;
                    foreach (var d in Dirs) Build(font, it, oc, new Vector3(d.x * w, d.y * w, 0.002f));
                    Build(font, it, oc, new Vector3(w * 0.4f, -w * 1.6f, 0.002f)); // drop shadow
                }
                Build(font, it, it.C, Vector3.zero);
            }
            _items.Clear();
            _mesh.Clear();
            if (_v.Count == 0) return;
            _mesh.SetVertices(_v); _mesh.SetUVs(0, _uv); _mesh.SetColors(_c); _mesh.SetTriangles(_t, 0, false);
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2000);
            _mat.mainTexture = font.material.mainTexture;
            Graphics.DrawMesh(_mesh, Matrix4x4.identity, _mat, 0);
        }

        void Build(Font font, Item it, Color col, Vector3 offset)
        {
            float k = it.Size / (FontSize * 0.72f);  // FontSize px ~ 1/0.72 cap heights
            float width = 0;
            foreach (char ch in it.S) if (font.GetCharacterInfo(ch, out var ci, FontSize)) width += ci.advance;
            float x = -width * 0.5f;
            float yMid = FontSize * 0.36f;
            foreach (char ch in it.S)
            {
                if (!font.GetCharacterInfo(ch, out var ci, FontSize)) continue;
                int b = _v.Count;
                Vector3 org = it.P; Quaternion rot = it.R;
                Vector3 P(float px, float py) => org + rot * (new Vector3(px * k, (py - yMid) * k, 0) + offset);
                _v.Add(P(x + ci.minX, ci.minY)); _uv.Add(ci.uvBottomLeft);
                _v.Add(P(x + ci.minX, ci.maxY)); _uv.Add(ci.uvTopLeft);
                _v.Add(P(x + ci.maxX, ci.maxY)); _uv.Add(ci.uvTopRight);
                _v.Add(P(x + ci.maxX, ci.minY)); _uv.Add(ci.uvBottomRight);
                for (int i = 0; i < 4; i++) _c.Add(col);
                _t.Add(b); _t.Add(b + 1); _t.Add(b + 2); _t.Add(b); _t.Add(b + 2); _t.Add(b + 3);
                x += ci.advance;
            }
        }
    }
}
