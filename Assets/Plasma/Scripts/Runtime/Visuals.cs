using UnityEngine;

namespace Plasma
{
    /// <summary>Shared materials, font and small helpers for building scene objects in code.</summary>
    public static class Visuals
    {
        static Material _lit, _fxAlpha, _fxAdd, _fxShadow;
        static Font _font;

        public static Material Lit => _lit ??= new Material(Shader.Find("Plasma/Lit")) { enableInstancing = true, name = "PlasmaLit" };
        public static Material FxAlpha => _fxAlpha ??= MakeFx(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, "FxAlpha", 1);
        public static Material FxAdd => _fxAdd ??= MakeFx(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.One, "FxAdd", 0.6f);
        static Material _fxSolid;
        /// <summary>Unshaded alpha blend that keeps saturated colours on a bright floor (tracers).</summary>
        public static Material FxSolid => _fxSolid ??= MakeFx(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, "FxSolid", 0);
        /// <summary>Flat, unshaded alpha (blob shadows, badges): drawn before other transparents.</summary>
        public static Material FxFlat
        {
            get
            {
                if (_fxShadow != null) return _fxShadow;
                _fxShadow = MakeFx(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, "FxFlat", 0);
                _fxShadow.renderQueue = 2950;
                _fxShadow.SetFloat("_Cull", 0f);
                return _fxShadow;
            }
        }

        /// <summary>Lalezar (OFL): chunky display font with Latin + Arabic. Falls back to the built-in font.</summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.Load<Font>("Fonts/Lalezar");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        static Material MakeFx(UnityEngine.Rendering.BlendMode src, UnityEngine.Rendering.BlendMode dst, string name, float soft)
        {
            var m = new Material(Shader.Find("Plasma/Fx")) { enableInstancing = true, name = name };
            m.SetFloat("_SrcBlend", (float)src); m.SetFloat("_DstBlend", (float)dst); m.SetFloat("_Soft", soft);
            return m;
        }

        static readonly MaterialPropertyBlock Mpb = new MaterialPropertyBlock();

        /// <summary>Creates a static coloured object from a procedural mesh.</summary>
        public static GameObject Solid(string name, Mesh mesh, Vector3 pos, Vector3 scale, Color color, Transform parent, Quaternion? rot = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            if (rot.HasValue) go.transform.localRotation = rot.Value;
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
    }
}
