using UnityEngine;
using UnityEngine.Rendering;

namespace Plasma
{
    /// <summary>Collects matrices + colours for one mesh/material and draws them with GPU instancing (1023 per call).</summary>
    public class InstancedBatch
    {
        const int Max = 1023;
        readonly Mesh _mesh;
        readonly Material _mat;
        readonly Matrix4x4[] _m = new Matrix4x4[Max];
        readonly Vector4[] _c = new Vector4[Max];
        readonly float[] _f = new float[Max];
        readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
        /// <summary>Cast real-time shadows (only effective when the light has shadows: High quality).</summary>
        public bool CastShadows;
        readonly bool _useFlash;
        int _n;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int FlashId = Shader.PropertyToID("_Flash");

        public InstancedBatch(Mesh mesh, Material mat, bool useFlash = false, bool castShadows = false)
        {
            _mesh = mesh; _mat = mat; _useFlash = useFlash; _mat.enableInstancing = true; CastShadows = castShadows;
        }

        public int Count => _n;

        public void Add(in Matrix4x4 m, Color c, float flash = 0f)
        {
            if (_n == Max) Flush();
            _m[_n] = m; _c[_n] = c; _f[_n] = flash; _n++;
        }

        public static bool DebugDisable = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-plasmaNoInst") >= 0;

        public void Flush()
        {
            if (_n == 0) return;
            if (DebugDisable) { _n = 0; return; }
            _mpb.Clear();
            _mpb.SetVectorArray(ColorId, _c);
            if (_useFlash) _mpb.SetFloatArray(FlashId, _f);
            Graphics.DrawMeshInstanced(_mesh, 0, _mat, _m, _n, _mpb, CastShadows && QualityManager.High ? ShadowCastingMode.On : ShadowCastingMode.Off, true);
            _n = 0;
        }
    }
}
