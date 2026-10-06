using Plasma.Sim;
using UnityEngine;

namespace Plasma
{
    /// <summary>One gate block on the conveyor: coloured block, "+N" label and (front gate only) an HP bar.</summary>
    public class GateView : MonoBehaviour
    {
        MeshRenderer _block;
        TextMesh _label, _shadow;
        Transform _barFill, _barBg;
        float _flash, _shake;
        public float Collapse = -1f;   // >=0 while playing the collapse animation (seconds)
        Color _color;

        public static GateView Create(Transform parent)
        {
            var go = new GameObject("Gate");
            go.transform.SetParent(parent, false);
            var gv = go.AddComponent<GateView>();
            var block = Visuals.Solid("Block", MeshFactory.GateBlock, Vector3.zero, new Vector3(2.4f, 1.15f, 1.3f), Color.white, go.transform);
            gv._block = block.GetComponent<MeshRenderer>();
            gv._shadow = Visuals.Label("Shadow", go.transform, new Vector3(0.04f, 0.6f, -0.47f), 1.25f, new Color(0, 0, 0, 0.35f));
            gv._label = Visuals.Label("Value", go.transform, new Vector3(0, 0.64f, -0.5f), 1.25f, Color.white);
            gv._label.transform.localRotation = gv._shadow.transform.localRotation = Quaternion.Euler(25, 0, 0);
            gv._barBg = Visuals.Solid("HpBg", MeshFactory.Cube, new Vector3(0, 1.32f, -0.1f), new Vector3(2f, 0.12f, 0.12f), new Color(0.1f, 0.1f, 0.12f), go.transform).transform;
            gv._barFill = Visuals.Solid("HpFill", MeshFactory.Cube, new Vector3(0, 1.32f, -0.17f), new Vector3(2f, 0.1f, 0.06f), new Color(1f, 1f, 1f), go.transform).transform;
            return gv;
        }

        public void Show(GateSpec g, bool front, float hpFrac)
        {
            gameObject.SetActive(true);
            _color = Palette.Gate(g.Tier);
            string txt = "+" + g.Value;
            _label.text = txt; _shadow.text = txt;
            _barBg.gameObject.SetActive(front);
            _barFill.gameObject.SetActive(front);
            if (front)
            {
                float f = Mathf.Clamp01(hpFrac);
                _barFill.localScale = new Vector3(2f * f, 0.1f, 0.06f);
                _barFill.localPosition = new Vector3(-1f + f, 1.32f, -0.17f);
            }
        }

        public void Hit() { _flash = 0.6f; _shake = 0.12f; }

        void Update()
        {
            _flash = Mathf.Max(0, _flash - Time.deltaTime * 5);
            _shake = Mathf.Max(0, _shake - Time.deltaTime);
            Visuals.SetColor(_block, _color, _flash * 0.5f);
            var s = _block.transform;
            if (Collapse >= 0)
            {
                Collapse += Time.deltaTime;
                float k = Mathf.Clamp01(Collapse / 0.3f);
                s.localScale = new Vector3(2.4f * (1 + 0.25f * k), 1.15f * (1 - 0.9f * k), 1.3f * (1 + 0.4f * k));
                _label.gameObject.SetActive(k < 0.5f); _shadow.gameObject.SetActive(k < 0.5f);
                if (Collapse > 0.6f) { Collapse = -1; gameObject.SetActive(false); }
            }
            else
            {
                float wob = _shake > 0 ? Mathf.Sin(Time.time * 90) * _shake * 0.6f : 0;
                s.localScale = new Vector3(2.4f + wob, 1.15f - wob * 0.5f, 1.3f);
                _label.gameObject.SetActive(true); _shadow.gameObject.SetActive(true);
            }
        }
    }
}
