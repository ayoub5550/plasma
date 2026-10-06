using UnityEngine;

namespace Plasma
{
    /// <summary>
    /// Mobile bloom on the main camera (High quality only, see QualityManager). Half-resolution dual box
    /// filter: prefilter/threshold -> N downsamples -> additive upsamples -> combine. Needs an HDR camera
    /// so that only HDR-boosted effects (Fx _Glow, specular) exceed the threshold.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PlasmaBloom : MonoBehaviour
    {
        public float Threshold = 1.05f;
        [Range(0, 1)] public float SoftKnee = 0.5f;
        public float Intensity = 0.7f;
        [Range(1, 6)] public int Iterations = 4;

        Material _mat;
        readonly RenderTexture[] _rt = new RenderTexture[8];
        static readonly int FilterId = Shader.PropertyToID("_Filter"), IntensityId = Shader.PropertyToID("_Intensity"), SourceId = Shader.PropertyToID("_SourceTex");

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (_mat == null)
            {
                var sh = Shader.Find("Hidden/Plasma/Bloom");
                if (sh == null || !sh.isSupported) { Graphics.Blit(src, dst); enabled = false; return; }
                _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            }
            float knee = Threshold * SoftKnee;
            _mat.SetVector(FilterId, new Vector4(Threshold, Threshold - knee, 2f * knee, 0.25f / (knee + 0.00001f)));
            _mat.SetFloat(IntensityId, Intensity);

            int w = src.width / 2, h = src.height / 2;
            var fmt = src.format;
            var cur = _rt[0] = RenderTexture.GetTemporary(w, h, 0, fmt);
            Graphics.Blit(src, cur, _mat, 0);
            int i = 1;
            for (; i < Iterations; i++)
            {
                w /= 2; h /= 2;
                if (h < 2 || w < 2) break;
                _rt[i] = RenderTexture.GetTemporary(w, h, 0, fmt);
                Graphics.Blit(cur, _rt[i], _mat, 1);
                cur = _rt[i];
            }
            for (i -= 2; i >= 0; i--)
            {
                Graphics.Blit(cur, _rt[i], _mat, 2);
                RenderTexture.ReleaseTemporary(cur);
                cur = _rt[i];
            }
            _mat.SetTexture(SourceId, src);
            Graphics.Blit(cur, dst, _mat, 3);
            RenderTexture.ReleaseTemporary(cur);
        }
    }
}
