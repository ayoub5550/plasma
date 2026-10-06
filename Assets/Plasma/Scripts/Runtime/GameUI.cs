using System;
using Plasma.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Plasma
{
    /// <summary>All screens (menu, HUD, pause, result) built in code. Game.cs drives it.</summary>
    public class GameUI : MonoBehaviour
    {
        public Action OnPlay, OnEndless, OnNext, OnRetry, OnMenu, OnPause, OnResume, OnToggleSound;
        public Action<UpgradeType> OnBuy;

        RectTransform _safe, _menu, _hud, _pause, _result;
        Text _coins, _levelTitle, _bestEndless, _hudLevel, _hudCoins, _resultTitle, _resultStats, _resultCoins, _banner, _hint, _soundLabel;
        Image _progressFill;
        RectTransform _bannerRt;
        UiKit.ChunkyButton _nextBtn, _retryBtn;
        readonly UiKit.ChunkyButton[] _upgBtn = new UiKit.ChunkyButton[Upgrades.Count];
        readonly Text[] _upgLevel = new Text[Upgrades.Count];
        float _bannerT = -1, _hintT = -1;
        Rect _lastSafe;

        static readonly Color Green = new Color(0.22f, 0.78f, 0.25f), Blue = new Color(0.16f, 0.5f, 1f), Orange = new Color(1f, 0.55f, 0.1f), Grey = new Color(0.45f, 0.48f, 0.55f), Purple = new Color(0.55f, 0.3f, 0.95f);

        public void Build()
        {
            var canvasGo = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0.5f;
            if (FindObjectOfType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(transform, false);

            _safe = UiKit.Rect("Safe", canvasGo.transform, Vector2.zero, Vector2.one);
            BuildMenu(); BuildHud(); BuildPause(); BuildResult();
            ApplySafeArea();
            ShowOnly(_menu);
        }

        void Update()
        {
            ApplySafeArea();
            if (_bannerT >= 0)
            {
                _bannerT += Time.unscaledDeltaTime;
                float s = _bannerT < 0.2f ? Mathf.Lerp(2f, 1f, _bannerT / 0.2f) : 1f;
                _bannerRt.localScale = Vector3.one * s;
                var c = _banner.color; c.a = _bannerT < 1.4f ? 1 : Mathf.Clamp01(1 - (_bannerT - 1.4f) / 0.4f); _banner.color = c;
                if (_bannerT > 1.8f) { _bannerT = -1; _banner.gameObject.SetActive(false); }
            }
            if (_hintT >= 0)
            {
                _hintT += Time.unscaledDeltaTime;
                var c = _hint.color; c.a = (_hintT < 3.5f ? 1 : Mathf.Clamp01(1 - (_hintT - 3.5f))) * (0.75f + 0.25f * Mathf.Sin(_hintT * 6)); _hint.color = c;
                if (_hintT > 4.5f) { _hintT = -1; _hint.gameObject.SetActive(false); }
            }
        }

        void ApplySafeArea()
        {
            var sa = Screen.safeArea;
            if (sa == _lastSafe || Screen.width == 0) return;
            _lastSafe = sa;
            _safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            _safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            _safe.offsetMin = _safe.offsetMax = Vector2.zero;
        }

        void ShowOnly(RectTransform rt)
        {
            foreach (var p in new[] { _menu, _hud, _pause, _result }) p.gameObject.SetActive(p == rt);
        }

        // ------------------------------------------------------------------ menu
        void BuildMenu()
        {
            _menu = UiKit.Rect("Menu", _safe, Vector2.zero, Vector2.one);
            // soft gradient at top and bottom so text reads over the live battle
            UiKit.Panel(UiKit.Rect("TopShade", _menu, new Vector2(0, 0.7f), Vector2.one), new Color(0.02f, 0.04f, 0.1f, 0.55f), false).raycastTarget = false;
            UiKit.Panel(UiKit.Rect("BottomShade", _menu, Vector2.zero, new Vector2(1, 0.48f)), new Color(0.02f, 0.04f, 0.1f, 0.72f), false).raycastTarget = false;

            var title = UiKit.Box("Title", _menu, new Vector2(0.5f, 1), new Vector2(0, -330), new Vector2(1000, 220));
            var t = UiKit.Label(title, "PLASMA", 190, Color.white); t.GetComponent<Outline>().effectDistance = new Vector2(8, -8);
            var sub = UiKit.Box("Sub", _menu, new Vector2(0.5f, 1), new Vector2(0, -480), new Vector2(1000, 80));
            UiKit.Label(sub, "SQUAD  vs  HORDE", 54, new Color(1f, 0.85f, 0.3f));

            var coinPill = UiKit.Box("Coins", _menu, new Vector2(1, 1), new Vector2(-190, -80), new Vector2(330, 100));
            UiKit.Panel(coinPill, new Color(0, 0, 0, 0.45f));
            _coins = UiKit.Label(coinPill, "0", 56, Palette.Gold);

            var sound = UiKit.Button(_menu, "Sound", new Vector2(0, 1), new Vector2(110, -80), new Vector2(150, 100), "SND", 40, Grey, () => OnToggleSound?.Invoke());
            _soundLabel = sound.Text;

            var lvl = UiKit.Box("Level", _menu, new Vector2(0.5f, 0), new Vector2(0, 880), new Vector2(900, 100));
            _levelTitle = UiKit.Label(lvl, "LEVEL 1", 84, Color.white);
            UiKit.Button(_menu, "Play", new Vector2(0.5f, 0), new Vector2(0, 730), new Vector2(620, 170), "PLAY", 96, Green, () => OnPlay?.Invoke());
            var endless = UiKit.Button(_menu, "Endless", new Vector2(0.5f, 0), new Vector2(0, 560), new Vector2(620, 120), "ENDLESS", 60, Purple, () => OnEndless?.Invoke());
            var best = UiKit.Box("Best", _menu, new Vector2(0.5f, 0), new Vector2(0, 470), new Vector2(620, 60));
            _bestEndless = UiKit.Label(best, "BEST WAVE 0", 38, new Color(1, 1, 1, 0.8f));

            // upgrade cards
            float w = 240, gap = 16, total = Upgrades.Count * w + (Upgrades.Count - 1) * gap;
            for (int i = 0; i < Upgrades.Count; i++)
            {
                int idx = i;
                float x = -total / 2 + w / 2 + i * (w + gap);
                var b = UiKit.Button(_menu, "Upg" + i, new Vector2(0.5f, 0), new Vector2(x, 230), new Vector2(w, 260), "", 40, Blue, () => OnBuy?.Invoke((UpgradeType)idx));
                b.Text.alignment = TextAnchor.LowerCenter;
                b.Text.rectTransform.offsetMin = new Vector2(0, 26);
                var nameRt = UiKit.Rect("Name", b.Face.transform, new Vector2(0, 0.62f), new Vector2(1, 1));
                UiKit.Label(nameRt, Upgrades.NamesEn[i].ToUpper().Replace(" ", "\n"), 34, Color.white);
                var lvRt = UiKit.Rect("Lv", b.Face.transform, new Vector2(0, 0.38f), new Vector2(1, 0.62f));
                _upgLevel[i] = UiKit.Label(lvRt, "LV 0", 40, new Color(1, 1, 1, 0.9f));
                _upgBtn[i] = b;
            }
        }

        public void ShowMenu(PlayerProfile p)
        {
            ShowOnly(_menu);
            RefreshMenu(p);
        }

        public void RefreshMenu(PlayerProfile p)
        {
            _coins.text = "$ " + p.Coins;
            _levelTitle.text = "LEVEL " + p.Level;
            _bestEndless.text = "BEST WAVE " + p.BestEndlessWave;
            _soundLabel.text = p.SoundOn ? "SND" : "MUTE";
            for (int i = 0; i < Upgrades.Count; i++)
            {
                int lv = p.Upg[i];
                int cost = Upgrades.Cost((UpgradeType)i, lv);
                bool max = lv >= Balance.UpgradeMaxLevel;
                _upgLevel[i].text = "LV " + lv;
                _upgBtn[i].Text.text = max ? "MAX" : "$ " + cost;
                bool can = !max && p.Coins >= cost;
                _upgBtn[i].Interactable = can;
                _upgBtn[i].SetColor(can ? Blue : Grey);
            }
        }

        // ------------------------------------------------------------------ HUD
        void BuildHud()
        {
            _hud = UiKit.Rect("Hud", _safe, Vector2.zero, Vector2.one);
            var top = UiKit.Box("Top", _hud, new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(760, 150));
            var lvl = UiKit.Rect("Level", top, new Vector2(0, 0.5f), new Vector2(1, 1));
            _hudLevel = UiKit.Label(lvl, "LEVEL 1", 58, Color.white);
            var bar = UiKit.Rect("Bar", top, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.38f));
            UiKit.Panel(bar, new Color(0, 0, 0, 0.5f));
            var fill = UiKit.Rect("Fill", bar, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));
            _progressFill = UiKit.Panel(fill, new Color(1f, 0.8f, 0.15f));
            _progressFill.type = Image.Type.Filled; _progressFill.fillMethod = Image.FillMethod.Horizontal; _progressFill.sprite = null;

            var coinPill = UiKit.Box("Coins", _hud, new Vector2(1, 1), new Vector2(-150, -250), new Vector2(250, 80));
            UiKit.Panel(coinPill, new Color(0, 0, 0, 0.4f));
            _hudCoins = UiKit.Label(coinPill, "$ 0", 44, Palette.Gold);

            UiKit.Button(_hud, "Pause", new Vector2(0, 1), new Vector2(90, -110), new Vector2(110, 110), "II", 52, Grey, () => OnPause?.Invoke());

            _bannerRt = UiKit.Box("Banner", _hud, new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(1000, 220));
            _banner = UiKit.Label(_bannerRt, "BOSS!", 150, new Color(1f, 0.3f, 0.25f));
            _banner.GetComponent<Outline>().effectDistance = new Vector2(7, -7);
            _banner.gameObject.SetActive(false);

            var hint = UiKit.Box("Hint", _hud, new Vector2(0.5f, 0), new Vector2(0, 360), new Vector2(1000, 200));
            _hint = UiKit.Label(hint, "<  DRAG TO MOVE  >\nshoot gates to grow your squad!", 54, Color.white);
            _hint.gameObject.SetActive(false);
        }

        public void ShowHud(string title, bool hint)
        {
            ShowOnly(_hud);
            _hudLevel.text = title;
            _progressFill.fillAmount = 0;
            if (hint) { _hint.gameObject.SetActive(true); _hintT = 0; }
        }

        public void UpdateHud(float progress, int coins, string title)
        {
            _progressFill.fillAmount = Mathf.Lerp(_progressFill.fillAmount, Mathf.Clamp01(progress), 0.2f);
            _hudCoins.text = "$ " + coins;
            if (title != null) _hudLevel.text = title;
        }

        public void Banner(string text, Color c)
        {
            _banner.text = text; _banner.color = c; _banner.gameObject.SetActive(true); _bannerT = 0;
        }

        // ------------------------------------------------------------------ pause
        void BuildPause()
        {
            _pause = UiKit.Rect("Pause", _safe, Vector2.zero, Vector2.one);
            UiKit.Panel(UiKit.Rect("Dim", _pause, Vector2.zero, Vector2.one, new Vector2(-200, -200), new Vector2(200, 200)), new Color(0, 0, 0, 0.6f), false);
            var t = UiKit.Box("T", _pause, new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(900, 160));
            UiKit.Label(t, "PAUSED", 120, Color.white);
            UiKit.Button(_pause, "Resume", new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(560, 150), "RESUME", 72, Green, () => OnResume?.Invoke());
            UiKit.Button(_pause, "Menu", new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(560, 120), "MENU", 60, Grey, () => OnMenu?.Invoke());
        }

        public void ShowPause(bool on) { _pause.gameObject.SetActive(on); if (!on) _hud.gameObject.SetActive(true); }

        // ------------------------------------------------------------------ result
        void BuildResult()
        {
            _result = UiKit.Rect("Result", _safe, Vector2.zero, Vector2.one);
            UiKit.Panel(UiKit.Rect("Dim", _result, Vector2.zero, Vector2.one, new Vector2(-200, -200), new Vector2(200, 200)), new Color(0.02f, 0.03f, 0.08f, 0.7f), false);
            var card = UiKit.Box("Card", _result, new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(900, 900));
            UiKit.Panel(card, new Color(0.1f, 0.13f, 0.22f, 0.95f));
            var t = UiKit.Rect("Title", card, new Vector2(0, 0.78f), new Vector2(1, 1));
            _resultTitle = UiKit.Label(t, "VICTORY!", 130, new Color(1f, 0.85f, 0.25f));
            var st = UiKit.Rect("Stats", card, new Vector2(0, 0.45f), new Vector2(1, 0.78f));
            _resultStats = UiKit.Label(st, "", 54, Color.white);
            var co = UiKit.Rect("Coins", card, new Vector2(0, 0.3f), new Vector2(1, 0.45f));
            _resultCoins = UiKit.Label(co, "+0", 80, Palette.Gold);
            _nextBtn = UiKit.Button(card, "Next", new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(560, 150), "NEXT", 80, Green, () => OnNext?.Invoke());
            _retryBtn = UiKit.Button(card, "Retry", new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(560, 150), "RETRY", 80, Orange, () => OnRetry?.Invoke());
            UiKit.Button(_result, "Menu", new Vector2(0.5f, 0.5f), new Vector2(0, -470), new Vector2(400, 110), "MENU", 56, Grey, () => OnMenu?.Invoke());
        }

        public void ShowResult(bool won, string title, string stats, int coins)
        {
            ShowOnly(_result);
            _resultTitle.text = title;
            _resultTitle.color = won ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 0.35f, 0.3f);
            _resultStats.text = stats;
            _resultCoins.text = "+ $" + coins;
            _nextBtn.gameObject.SetActive(won);
            _retryBtn.gameObject.SetActive(!won);
        }
    }
}
