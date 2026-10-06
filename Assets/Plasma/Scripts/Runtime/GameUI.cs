using System;
using System.Collections.Generic;
using Plasma.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Plasma
{
    /// <summary>All screens (menu, settings, HUD, hints, pause, result) built in code. Game.cs drives it. Strings come from Loc.</summary>
    public class GameUI : MonoBehaviour
    {
        public Action OnPlay, OnEndless, OnNext, OnRetry, OnMenu, OnPause, OnResume, OnToggleSound, OnToggleMusic, OnToggleVibration, OnToggleLanguage, OnToggleQuality;
        public Action<UpgradeType> OnBuy;

        RectTransform _safe, _menu, _hud, _pause, _result, _settings;
        Text _coins, _levelTitle, _bestEndless, _hudLevel, _hudCoins, _resultTitle, _resultStats, _resultCoins, _banner, _hint;
        Text _setSound, _setMusic, _setVib, _setLang, _setQuality;
        Image _progressFill;
        RectTransform _bannerRt, _hintRt, _hintArrow;
        CanvasGroup _hintGroup;
        UiKit.ChunkyButton _nextBtn, _retryBtn;
        readonly UiKit.ChunkyButton[] _upgBtn = new UiKit.ChunkyButton[Upgrades.Count];
        readonly Text[] _upgLevel = new Text[Upgrades.Count];
        readonly Text[] _upgName = new Text[Upgrades.Count];
        readonly List<KeyValuePair<Text, string>> _static = new List<KeyValuePair<Text, string>>();
        float _bannerT = -1, _hintT = -1, _hintDur, _coinAnimT = -1;
        int _coinTarget;
        Rect _lastSafe;

        static readonly Color Green = new Color(0.22f, 0.78f, 0.25f), Blue = new Color(0.16f, 0.5f, 1f), Orange = new Color(1f, 0.55f, 0.1f), Grey = new Color(0.42f, 0.45f, 0.53f), Purple = new Color(0.55f, 0.3f, 0.95f), Dark = new Color(0.08f, 0.11f, 0.2f, 0.96f);

        public void Build()
        {
            var canvasGo = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0.5f;
            if (FindObjectOfType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(transform, false);

            _safe = UiKit.Rect("Safe", canvasGo.transform, Vector2.zero, Vector2.one);
            BuildMenu(); BuildHud(); BuildPause(); BuildResult(); BuildSettings();
            ApplySafeArea();
            ShowOnly(_menu);
        }

        Text Static(Text t, string key) { _static.Add(new KeyValuePair<Text, string>(t, key)); t.text = Loc.T(key); return t; }

        /// <summary>Re-applies every fixed label (after a language switch).</summary>
        public void RefreshTexts()
        {
            foreach (var kv in _static) kv.Key.text = Loc.T(kv.Value);
            for (int i = 0; i < Upgrades.Count; i++) _upgName[i].text = Loc.T("upg" + i);
        }

        void Update()
        {
            ApplySafeArea();
            float dt = Time.deltaTime; // (not unscaled: capture mode fixes deltaTime via captureFramerate)
            if (_bannerT >= 0)
            {
                _bannerT += dt;
                float s = _bannerT < 0.2f ? Mathf.Lerp(2f, 1f, _bannerT / 0.2f) : 1f;
                _bannerRt.localScale = Vector3.one * s;
                var c = _banner.color; c.a = _bannerT < 1.4f ? 1 : Mathf.Clamp01(1 - (_bannerT - 1.4f) / 0.4f); _banner.color = c;
                if (_bannerT > 1.8f) { _bannerT = -1; _banner.gameObject.SetActive(false); }
            }
            if (_hintT >= 0)
            {
                _hintT += dt;
                float a = (_hintT < _hintDur ? 1 : Mathf.Clamp01(1 - (_hintT - _hintDur) / 0.4f));
                _hintGroup.alpha = a;
                _hintRt.localScale = Vector3.one * (1 + 0.06f * Mathf.Sin(_hintT * 7));
                if (_hintArrow != null) _hintArrow.anchoredPosition = new Vector2(0, -95 + 14 * Mathf.Sin(_hintT * 8));
                if (_hintT > _hintDur + 0.4f) { _hintT = -1; _hintRt.gameObject.SetActive(false); }
            }
            if (_coinAnimT >= 0)
            {
                _coinAnimT += dt;
                float k = Mathf.Clamp01(_coinAnimT / 0.9f);
                _resultCoins.text = "+ $" + Mathf.RoundToInt(_coinTarget * (1 - Mathf.Pow(1 - k, 3)));
                _resultCoins.rectTransform.localScale = Vector3.one * (k < 1 ? 1 + 0.08f * Mathf.Sin(_coinAnimT * 30) : 1);
                if (k >= 1) _coinAnimT = -1;
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
            foreach (var p in new[] { _menu, _hud, _pause, _result, _settings }) p.gameObject.SetActive(p == rt);
        }

        // ------------------------------------------------------------------ menu
        void BuildMenu()
        {
            _menu = UiKit.Rect("Menu", _safe, Vector2.zero, Vector2.one);
            UiKit.Panel(UiKit.Rect("TopShade", _menu, new Vector2(0, 0.72f), Vector2.one, new Vector2(-50, 0), new Vector2(50, 200)), new Color(0.02f, 0.04f, 0.1f, 0.55f), false).raycastTarget = false;
            UiKit.Panel(UiKit.Rect("BottomShade", _menu, Vector2.zero, new Vector2(1, 0.47f), new Vector2(-50, -200), new Vector2(50, 0)), new Color(0.02f, 0.04f, 0.1f, 0.74f), false).raycastTarget = false;

            var title = UiKit.Box("Title", _menu, new Vector2(0.5f, 1), new Vector2(0, -330), new Vector2(1000, 240));
            var t = Static(UiKit.Label(title, "", 200, Color.white), "title"); t.GetComponent<Outline>().effectDistance = new Vector2(8, -8);
            var sub = UiKit.Box("Sub", _menu, new Vector2(0.5f, 1), new Vector2(0, -490), new Vector2(1000, 90));
            Static(UiKit.Label(sub, "", 60, Palette.Gold), "subtitle");

            var coinPill = UiKit.Box("Coins", _menu, new Vector2(1, 1), new Vector2(-190, -85), new Vector2(330, 105));
            UiKit.Panel(coinPill, new Color(0, 0, 0, 0.5f));
            _coins = UiKit.Label(coinPill, "0", 60, Palette.Gold);

            var gear = UiKit.Button(_menu, "Settings", new Vector2(0, 1), new Vector2(105, -85), new Vector2(130, 110), "", 64, Grey, ShowSettings);
            for (int i = 0; i < 3; i++) UiKit.Panel(UiKit.Box("Bar" + i, gear.Face.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 24 - i * 24), new Vector2(66, 12)), Color.white).raycastTarget = false;

            var lvl = UiKit.Box("Level", _menu, new Vector2(0.5f, 0), new Vector2(0, 880), new Vector2(900, 110));
            _levelTitle = UiKit.Label(lvl, "", 90, Color.white);
            Static(UiKit.Button(_menu, "Play", new Vector2(0.5f, 0), new Vector2(0, 725), new Vector2(620, 175), "", 104, Green, () => OnPlay?.Invoke()).Text, "play");
            Static(UiKit.Button(_menu, "Endless", new Vector2(0.5f, 0), new Vector2(0, 555), new Vector2(620, 120), "", 64, Purple, () => OnEndless?.Invoke()).Text, "endless");
            var best = UiKit.Box("Best", _menu, new Vector2(0.5f, 0), new Vector2(0, 465), new Vector2(620, 60));
            _bestEndless = UiKit.Label(best, "", 42, new Color(1, 1, 1, 0.8f));

            float w = 240, gap = 16, total = Upgrades.Count * w + (Upgrades.Count - 1) * gap;
            for (int i = 0; i < Upgrades.Count; i++)
            {
                int idx = i;
                float x = -total / 2 + w / 2 + i * (w + gap);
                var b = UiKit.Button(_menu, "Upg" + i, new Vector2(0.5f, 0), new Vector2(x, 225), new Vector2(w, 270), "", 46, Blue, () => OnBuy?.Invoke((UpgradeType)idx));
                b.Text.alignment = TextAnchor.LowerCenter;
                b.Text.rectTransform.offsetMin = new Vector2(0, 22);
                var nameRt = UiKit.Rect("Name", b.Face.transform, new Vector2(0, 0.6f), new Vector2(1, 1), new Vector2(6, 0), new Vector2(-6, -6));
                _upgName[i] = UiKit.Label(nameRt, Loc.T("upg" + i), 38, Color.white);
                _upgName[i].horizontalOverflow = HorizontalWrapMode.Wrap; _upgName[i].resizeTextForBestFit = true; _upgName[i].resizeTextMaxSize = 40; _upgName[i].resizeTextMinSize = 20;
                var lvRt = UiKit.Rect("Lv", b.Face.transform, new Vector2(0, 0.36f), new Vector2(1, 0.6f));
                _upgLevel[i] = UiKit.Label(lvRt, "", 40, new Color(1, 1, 1, 0.9f));
                _upgBtn[i] = b;
            }
        }

        public void ShowMenu(PlayerProfile p) { ShowOnly(_menu); RefreshMenu(p); }

        public void RefreshMenu(PlayerProfile p)
        {
            _coins.text = "$ " + p.Coins;
            _levelTitle.text = Loc.T("level", p.Level);
            _bestEndless.text = Loc.T("best_wave", p.BestEndlessWave);
            for (int i = 0; i < Upgrades.Count; i++)
            {
                int lv = p.Upg[i];
                int cost = Upgrades.Cost((UpgradeType)i, lv);
                bool max = lv >= Balance.UpgradeMaxLevel;
                _upgLevel[i].text = Loc.T("lv", lv);
                _upgBtn[i].Text.text = max ? Loc.T("max") : "$ " + cost;
                bool can = !max && p.Coins >= cost;
                _upgBtn[i].Interactable = can;
                _upgBtn[i].SetColor(can ? Blue : Grey);
            }
            RefreshSettings(p);
        }

        // ------------------------------------------------------------------ settings
        void BuildSettings()
        {
            _settings = UiKit.Rect("Settings", _safe, Vector2.zero, Vector2.one);
            UiKit.Panel(UiKit.Rect("Dim", _settings, Vector2.zero, Vector2.one, new Vector2(-200, -200), new Vector2(200, 200)), new Color(0, 0, 0, 0.65f), false);
            var card = UiKit.Box("Card", _settings, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(880, 1100));
            UiKit.Panel(card, Dark);
            Static(UiKit.Label(UiKit.Rect("T", card, new Vector2(0, 0.84f), new Vector2(1, 1)), "", 96, Color.white), "settings");
            _setSound = Row(card, 0.70f, "sound", () => OnToggleSound?.Invoke());
            _setMusic = Row(card, 0.57f, "music", () => OnToggleMusic?.Invoke());
            _setVib = Row(card, 0.44f, "vibration", () => OnToggleVibration?.Invoke());
            _setLang = Row(card, 0.31f, "language", () => OnToggleLanguage?.Invoke());
            _setQuality = Row(card, 0.18f, "graphics", () => OnToggleQuality?.Invoke());
            Static(UiKit.Button(_settings, "Close", new Vector2(0.5f, 0.5f), new Vector2(0, -600), new Vector2(460, 130), "", 64, Green, () => ShowOnly(_menu)).Text, "close");
        }

        Text Row(RectTransform card, float y, string key, Action onClick)
        {
            var lbl = UiKit.Rect(key, card, new Vector2(0.06f, y - 0.06f), new Vector2(0.5f, y + 0.06f));
            Static(UiKit.Label(lbl, "", 56, Color.white), key);
            var b = UiKit.Button(card, key + "Btn", new Vector2(0.73f, y), Vector2.zero, new Vector2(330, 115), "", 52, Blue, onClick);
            return b.Text;
        }

        void RefreshSettings(PlayerProfile p)
        {
            _setSound.text = Loc.T(p.SoundOn ? "on" : "off");
            _setMusic.text = Loc.T(p.MusicOn ? "on" : "off");
            _setVib.text = Loc.T(p.VibrationOn ? "on" : "off");
            _setLang.text = Loc.Arabic ? Loc.T("lang_name") : "ENGLISH";
            _setQuality.text = Loc.T(p.HighQuality ? "high" : "low");
        }

        public void ShowSettings() { ShowOnly(_settings); }
        public bool SettingsOpen => _settings.gameObject.activeSelf;
        public void CloseSettings() { ShowOnly(_menu); }

        // ------------------------------------------------------------------ HUD
        void BuildHud()
        {
            _hud = UiKit.Rect("Hud", _safe, Vector2.zero, Vector2.one);
            var top = UiKit.Box("Top", _hud, new Vector2(0.5f, 1), new Vector2(0, -105), new Vector2(720, 150));
            var lvl = UiKit.Rect("Level", top, new Vector2(0, 0.48f), new Vector2(1, 1));
            _hudLevel = UiKit.Label(lvl, "", 62, Color.white);
            var bar = UiKit.Rect("Bar", top, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.38f));
            UiKit.Panel(bar, new Color(0, 0, 0, 0.55f));
            var fill = UiKit.Rect("Fill", bar, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));
            _progressFill = UiKit.Panel(fill, new Color(1f, 0.8f, 0.15f));
            _progressFill.type = Image.Type.Filled; _progressFill.fillMethod = Image.FillMethod.Horizontal; _progressFill.sprite = null;

            var coinPill = UiKit.Box("Coins", _hud, new Vector2(1, 1), new Vector2(-150, -245), new Vector2(250, 84));
            UiKit.Panel(coinPill, new Color(0, 0, 0, 0.45f));
            _hudCoins = UiKit.Label(coinPill, "$ 0", 48, Palette.Gold);

            UiKit.Button(_hud, "Pause", new Vector2(0, 1), new Vector2(90, -105), new Vector2(115, 115), "II", 56, Grey, () => OnPause?.Invoke());

            _bannerRt = UiKit.Box("Banner", _hud, new Vector2(0.5f, 0.5f), new Vector2(0, 330), new Vector2(1000, 230));
            _banner = UiKit.Label(_bannerRt, "", 160, new Color(1f, 0.3f, 0.25f));
            _banner.GetComponent<Outline>().effectDistance = new Vector2(7, -7);
            _banner.gameObject.SetActive(false);

            _hintRt = UiKit.Box("Hint", _hud, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 150));
            _hintGroup = _hintRt.gameObject.AddComponent<CanvasGroup>(); _hintGroup.blocksRaycasts = false; _hintGroup.interactable = false;
            UiKit.Panel(_hintRt, new Color(0, 0, 0, 0.45f));
            _hint = UiKit.Label(_hintRt, "", 66, Color.white);
            _hintArrow = UiKit.Box("Arrow", _hintRt, new Vector2(0.5f, 0.5f), new Vector2(0, -95), new Vector2(120, 80));
            UiKit.Label(_hintArrow, "V", 80, Palette.Gold);
            _hintRt.gameObject.SetActive(false);
        }

        public void ShowHud(string title)
        {
            ShowOnly(_hud);
            _hudLevel.text = title;
            _progressFill.fillAmount = 0;
            _hintRt.gameObject.SetActive(false); _hintT = -1;
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

        /// <summary>Tutorial hint at a normalised screen position (0..1), optional arrow pointing down at the target.</summary>
        public void Hint(string text, Vector2 screenPos, float seconds, bool arrow)
        {
            _hintRt.gameObject.SetActive(true);
            _hintRt.anchorMin = _hintRt.anchorMax = screenPos;
            _hintRt.anchoredPosition = Vector2.zero;
            _hint.text = text;
            _hintArrow.gameObject.SetActive(arrow);
            _hintT = 0; _hintDur = seconds;
        }

        // ------------------------------------------------------------------ pause
        void BuildPause()
        {
            _pause = UiKit.Rect("Pause", _safe, Vector2.zero, Vector2.one);
            UiKit.Panel(UiKit.Rect("Dim", _pause, Vector2.zero, Vector2.one, new Vector2(-200, -200), new Vector2(200, 200)), new Color(0, 0, 0, 0.6f), false);
            var t = UiKit.Box("T", _pause, new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(900, 170));
            Static(UiKit.Label(t, "", 120, Color.white), "paused");
            Static(UiKit.Button(_pause, "Resume", new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(580, 160), "", 80, Green, () => OnResume?.Invoke()).Text, "resume");
            Static(UiKit.Button(_pause, "Menu", new Vector2(0.5f, 0.5f), new Vector2(0, -160), new Vector2(580, 125), "", 64, Grey, () => OnMenu?.Invoke()).Text, "menu");
        }

        public void ShowPause(bool on) { _pause.gameObject.SetActive(on); if (!on && !_menu.gameObject.activeSelf && !_result.gameObject.activeSelf && !_settings.gameObject.activeSelf) _hud.gameObject.SetActive(true); }

        // ------------------------------------------------------------------ result
        void BuildResult()
        {
            _result = UiKit.Rect("Result", _safe, Vector2.zero, Vector2.one);
            UiKit.Panel(UiKit.Rect("Dim", _result, Vector2.zero, Vector2.one, new Vector2(-200, -200), new Vector2(200, 200)), new Color(0.02f, 0.03f, 0.08f, 0.7f), false);
            var card = UiKit.Box("Card", _result, new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(900, 920));
            UiKit.Panel(card, Dark);
            var t = UiKit.Rect("Title", card, new Vector2(0, 0.78f), new Vector2(1, 1));
            _resultTitle = UiKit.Label(t, "", 140, new Color(1f, 0.85f, 0.25f));
            var st = UiKit.Rect("Stats", card, new Vector2(0, 0.45f), new Vector2(1, 0.78f));
            _resultStats = UiKit.Label(st, "", 58, Color.white);
            _resultStats.lineSpacing = 1.1f;
            var co = UiKit.Rect("Coins", card, new Vector2(0, 0.3f), new Vector2(1, 0.45f));
            _resultCoins = UiKit.Label(co, "+0", 92, Palette.Gold);
            _nextBtn = UiKit.Button(card, "Next", new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(580, 160), "", 88, Green, () => OnNext?.Invoke());
            Static(_nextBtn.Text, "next");
            _retryBtn = UiKit.Button(card, "Retry", new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(580, 160), "", 76, Orange, () => OnRetry?.Invoke());
            Static(_retryBtn.Text, "retry");
            Static(UiKit.Button(_result, "Menu", new Vector2(0.5f, 0.5f), new Vector2(0, -480), new Vector2(420, 120), "", 60, Grey, () => OnMenu?.Invoke()).Text, "menu");
        }

        public void ShowResult(bool won, string title, string stats, int coins)
        {
            ShowOnly(_result);
            _resultTitle.text = title;
            _resultTitle.color = won ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 0.35f, 0.3f);
            _resultStats.text = stats;
            _coinTarget = coins; _coinAnimT = 0; _resultCoins.text = "+ $0";
            _nextBtn.gameObject.SetActive(won);
            _retryBtn.gameObject.SetActive(!won);
        }
    }
}
