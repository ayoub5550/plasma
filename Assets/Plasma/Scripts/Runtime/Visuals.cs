using UnityEngine;

namespace Plasma
{
    /// <summary>Shared materials, font and small helpers for building scene objects in code.</summary>
    public static class Visuals
    {
        static Material _lit, _fxAlpha, _fxAdd, _text;
        static Font _font;

        public static Material Lit => _lit ??= new Material(Shader.Find("Plasma/Lit")) { enableInstancing = true, name = "PlasmaLit" };
        public static Material FxAlpha => _fxAlpha ??= MakeFx(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, "FxAlpha");
        public static Material FxAdd => _fxAdd ??= MakeFx(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.One, "FxAdd");
        public static Font Font => _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        public static Material TextMat
        {
            get
            {
                if (_text != null) return _text;
                _text = new Material(Shader.Find("Plasma/Text")) { name = "PlasmaText" };
                _text.mainTexture = Font.material.mainTexture;
                return _text;
            }
        }

        static Material MakeFx(UnityEngine.Rendering.BlendMode src, UnityEngine.Rendering.BlendMode dst, string name)
        {
            var m = new Material(Shader.Find("Plasma/Fx")) { enableInstancing = true, name = name };
            m.SetFloat("_SrcBlend", (float)src); m.SetFloat("_DstBlend", (float)dst);
            return m;
        }

        static readonly MaterialPropertyBlock Mpb = new MaterialPropertyBlock();

        /// <summary>Creates a static coloured object from a procedural mesh.</summary>
        public static GameObject Solid(string name, Mesh mesh, Vector3 pos, Vector3 scale, Color color, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Lit;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            SetColor(r, color);
            return go;
        }

        public static void SetColor(Renderer r, Color c, float flash = 0)
        {
            r.GetPropertyBlock(Mpb);
            Mpb.SetColor("_Color", c);
            Mpb.SetFloat("_Flash", flash);
            r.SetPropertyBlock(Mpb);
        }

        /// <summary>World-space label (TextMesh) that renders on top of geometry it sits in front of.</summary>
        public static TextMesh Label(string name, Transform parent, Vector3 pos, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = Font; tm.fontSize = 96; tm.characterSize = size * 0.1f; tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center; tm.color = color; tm.fontStyle = FontStyle.Bold;
            go.GetComponent<MeshRenderer>().sharedMaterial = TextMat;
            return tm;
        }
    }
}
