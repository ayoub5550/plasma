using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Plasma
{
    /// <summary>Tiny code-only uGUI toolkit (rounded panels, chunky buttons, outlined text).</summary>
    public static class UiKit
    {
        static Sprite _round;
        public static Font Font => Visuals.Font;

        /// <summary>64x64 rounded-rectangle sprite, 9-sliced.</summary>
        public static Sprite Round
        {
            get
            {
                if (_round != null) return _round;
                const int S = 64, R = 22;
                var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float cx = Mathf.Clamp(x + 0.5f, R, S - R), cy = Mathf.Clamp(y + 0.5f, R, S - R);
                        float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                        byte a = (byte)(Mathf.Clamp01(R - d + 0.5f) * 255);
                        px[y * S + x] = new Color32(255, 255, 255, a);
                    }
                tex.SetPixels32(px); tex.Apply();
                _round = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(R, R, R, R));
                return _round;
            }
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin = default, Vector2 offMax = default)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offMin; rt.offsetMax = offMax;
            return rt;
        }

        /// <summary>Rect anchored at a point with a fixed size.</summary>
        public static RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = Rect(name, parent, anchor, anchor);
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            return rt;
        }

        public static Image Panel(RectTransform rt, Color c, bool rounded = true)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            if (rounded) { img.sprite = Round; img.type = Image.Type.Sliced; }
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, bool outline = true)
        {
            var rt = Rect("Text", parent, Vector2.zero, Vector2.one);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = color; t.alignment = anchor;
            t.fontStyle = FontStyle.Normal; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (outline)
            {
                var o = rt.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.55f); o.effectDistance = new Vector2(3, -3);
            }
            return t;
        }

        public class ChunkyButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
        {
            public Action OnClick;
            public bool Interactable = true;
            public Image Face, Base;
            public Text Text;
            RectTransform _face;
            Vector2 _up;
            void Awake() { }
            public void Init(Image face, Image bas, Text text) { Face = face; Base = bas; Text = text; _face = (RectTransform)face.transform; _up = _face.anchoredPosition; }
            public void OnPointerDown(PointerEventData e) { if (Interactable) _face.anchoredPosition = _up + new Vector2(0, -10); }
            public void OnPointerUp(PointerEventData e) { _face.anchoredPosition = _up; }
            public void OnPointerClick(PointerEventData e)
            {
                if (!Interactable) return;
                Sfx.I?.Play(Sfx.Id.Click);
                OnClick?.Invoke();
            }
            public void SetColor(Color c)
            {
                Face.color = c;
                Base.color = new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f, c.a);
            }
        }

        /// <summary>A chunky 3D-looking button (darker base + raised face).</summary>
        public static ChunkyButton Button(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, string label, int fontSize, Color color, Action onClick)
        {
            var root = Box(name, parent, anchor, pos, size);
            var hit = root.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0);
            var bas = Rect("Base", root, Vector2.zero, Vector2.one, new Vector2(0, -12), new Vector2(0, -12));
            var baseImg = Panel(bas, Color.black);
            var face = Rect("Face", root, Vector2.zero, Vector2.one);
            var faceImg = Panel(face, color);
            var gloss = Rect("Gloss", face, new Vector2(0, 0.55f), new Vector2(1, 1), new Vector2(10, 0), new Vector2(-10, -8));
            Panel(gloss, new Color(1, 1, 1, 0.18f)).raycastTarget = false;
            var txt = Label(face, label, fontSize, Color.white);
            var b = root.gameObject.AddComponent<ChunkyButton>();
            b.Init(faceImg, baseImg, txt);
            b.SetColor(color);
            b.OnClick = onClick;
            faceImg.raycastTarget = false; baseImg.raycastTarget = false;
            return b;
        }
    }
}
