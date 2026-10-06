using System;
using System.Collections;
using System.IO;
using Plasma.Sim;
using UnityEngine;

namespace Plasma
{
    /// <summary>
    /// Entry point (the only component in Main.unity). Owns the profile, the battle view, the UI and
    /// the flow Menu -> Battle -> Result, the first-run tutorial hints, audio and settings.
    /// Also implements the headless capture mode used by CI/agents:
    ///   -plasmaCapture DIR [-plasmaLevel N] [-plasmaFrames N] [-plasmaSkill 0..1] [-plasmaMenuFrames N] [-plasmaLang en|ar]
    ///   [-plasmaQuality high|low] [-plasmaDamage MULT (capture: weaker squad so the boss walks up)] [-plasmaSettings]
    /// </summary>
    public class Game : MonoBehaviour
    {
        enum Mode { Menu, Battle, Result }

        PlayerProfile _profile;
        BattleView _view;
        GameUI _ui;
        Sfx _sfx;
        Mode _mode;
        bool _endless, _paused;
        int _runCoins;
        float _resultDelay = -1;
        float _captureDamage = 1f;

        // tutorial state (levels 1-2)
        bool _tutorial;
        int _hintStage;
        float _hintClock;

        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Input.multiTouchEnabled = false;

            _profile = Persistence.Load();
            if (!_profile.LangChosen) _profile.Arabic = Application.systemLanguage == SystemLanguage.Arabic;
            string lang = Arg("-plasmaLang");
            if (lang != null) _profile.Arabic = lang == "ar";
            Loc.Arabic = _profile.Arabic;

            _sfx = new GameObject("Sfx").AddComponent<Sfx>();
            _sfx.transform.SetParent(transform);
            _sfx.Enabled = _profile.SoundOn;
            Haptics.Enabled = _profile.VibrationOn;
            if (FindObjectOfType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();

            var cam = Camera.main;
            if (cam == null) { cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; }
            _view = new GameObject("Battle").AddComponent<BattleView>();
            _view.SetupCamera(cam);
            if (!_profile.QualityChosen) _profile.HighQuality = QualityManager.AutoHigh();
            string q = Arg("-plasmaQuality");
            if (q != null) _profile.HighQuality = q != "low";
            QualityManager.Apply(_profile.HighQuality, cam);
            _view.OnEvent += OnSimEvent;

            _ui = gameObject.AddComponent<GameUI>();
            _ui.Build();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-plasmaNoUI") >= 0) _ui.transform.GetChild(0).gameObject.SetActive(false);
            _ui.OnPlay = () => StartBattle(false);
            _ui.OnEndless = () => StartBattle(true);
            _ui.OnNext = () => StartBattle(false);
            _ui.OnRetry = () => StartBattle(_endless);
            _ui.OnMenu = GoMenu;
            _ui.OnPause = () => SetPaused(true);
            _ui.OnResume = () => SetPaused(false);
            _ui.OnBuy = Buy;
            _ui.OnToggleSound = () => { _profile.SoundOn = !_profile.SoundOn; SaveAndRefresh(); };
            _ui.OnToggleMusic = () => { _profile.MusicOn = !_profile.MusicOn; _sfx.SetMusic(_profile.MusicOn); SaveAndRefresh(); };
            _ui.OnToggleVibration = () => { _profile.VibrationOn = !_profile.VibrationOn; Haptics.Enabled = _profile.VibrationOn; if (_profile.VibrationOn) Haptics.Pulse(40); SaveAndRefresh(); };
            _ui.OnToggleQuality = () => { _profile.HighQuality = !_profile.HighQuality; _profile.QualityChosen = true; QualityManager.Apply(_profile.HighQuality, Camera.main); SaveAndRefresh(); };
            _ui.OnToggleLanguage = () => { _profile.Arabic = !_profile.Arabic; _profile.LangChosen = true; Loc.Arabic = _profile.Arabic; _ui.RefreshTexts(); SaveAndRefresh(); };

            GoMenu();
            _sfx.SetMusic(_profile.MusicOn && Arg("-plasmaCapture") == null);
            if (Arg("-plasmaCapture") != null) StartCoroutine(Capture());
        }

        void SaveAndRefresh() { Persistence.Save(_profile); _ui.RefreshMenu(_profile); }

        // ------------------------------------------------------------------ flow
        void GoMenu()
        {
            SetPaused(false);
            _mode = Mode.Menu;
            _resultDelay = -1;
            StartAttract();
            _ui.ShowMenu(_profile);
        }

        /// <summary>Background "attract mode": a bot plays the player's current level behind the menu.</summary>
        void StartAttract()
        {
            var spec = LevelGenerator.Create(Mathf.Max(3, _profile.Level));
            _view.Begin(new BattleSim(spec, _profile.Modifiers()));
            _view.Bot = new BotPolicy(0.9f, UnityEngine.Random.Range(1, 9999));
            _sfx.Enabled = false; // attract mode is silent
        }

        void StartBattle(bool endless)
        {
            SetPaused(false);
            _endless = endless;
            _mode = Mode.Battle;
            _runCoins = 0;
            _resultDelay = -1;
            var spec = LevelGenerator.Create(endless ? 1 : _profile.Level);
            _view.Bot = null;
            _sfx.Enabled = _profile.SoundOn;
            _ui.ShowHud(Title());
            var mods = _profile.Modifiers();
            mods.DamageMult *= _captureDamage;   // capture only (-plasmaDamage): lets the boss walk up for visual checks
            _view.Begin(new BattleSim(spec, mods, endless));
            _tutorial = !endless && _profile.Level <= 2;
            _hintStage = 0; _hintClock = 0;
            if (spec.BigBoss && !endless) _ui.Banner(Loc.T("boss_level"), new Color(1f, 0.35f, 0.3f));
        }

        string Title() => _endless ? Loc.T("wave", _view.Sim != null ? _view.Sim.Wave : 1) : Loc.T("level", _profile.Level);

        void SetPaused(bool p)
        {
            _paused = p;
            _view.Paused = p;
            _ui.ShowPause(p && _mode == Mode.Battle);
        }

        void Buy(UpgradeType u)
        {
            if (_profile.TryBuy(u))
            {
                Persistence.Save(_profile);
                bool was = _sfx.Enabled; _sfx.Enabled = _profile.SoundOn; _sfx.Play(Sfx.Id.Coin); _sfx.Enabled = was;
                StartAttract();
            }
            _ui.RefreshMenu(_profile);
        }

        void Update()
        {
            if (_mode == Mode.Battle && _view.Sim != null)
            {
                var sim = _view.Sim;
                _ui.UpdateHud(_endless ? (sim.Wave - 1 + sim.Progress) / Mathf.Max(1, sim.Wave) : sim.Progress, _profile.Coins + _runCoins, Title());
                if (_tutorial && !_paused) Tutorial(sim);
                if (!_paused) FpsWatchdog();
                if (_resultDelay >= 0)
                {
                    _resultDelay -= Time.deltaTime;
                    if (_resultDelay < 0) FinishBattle();
                }
            }
            if (_mode == Mode.Menu && _view.Sim != null && _view.Sim.State != SimState.Running) StartAttract();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_mode == Mode.Battle) SetPaused(!_paused);
                else if (_mode == Mode.Result) GoMenu();
                else if (_ui.SettingsOpen) _ui.CloseSettings();
            }
        }

        // ------------------------------------------------------------------ fps watchdog
        float _fpsClock; int _fpsFrames, _slowWindows;
        /// <summary>Automatic quality only: two consecutive 5 s windows under 38 fps in High -> switch to Low (once).</summary>
        void FpsWatchdog()
        {
            if (_profile.QualityChosen || !QualityManager.High || Arg("-plasmaCapture") != null) return;
            _fpsClock += Time.unscaledDeltaTime; _fpsFrames++;
            if (_fpsClock < 5f) return;
            float fps = _fpsFrames / _fpsClock;
            _fpsClock = 0; _fpsFrames = 0;
            _slowWindows = fps < 38f ? _slowWindows + 1 : 0;
            if (_slowWindows < 2) return;
            Debug.Log($"[Plasma] {fps:0} fps in High quality -> switching to Low");
            _profile.HighQuality = false;
            QualityManager.Apply(false, Camera.main);
            Persistence.Save(_profile);
            _slowWindows = 0;
        }

        /// <summary>Contextual first-run hints: drag, shoot the gate, collect the tiles, stop the horde.</summary>
        void Tutorial(BattleSim sim)
        {
            _hintClock += Time.deltaTime;
            switch (_hintStage)
            {
                case 0: _ui.Hint(Loc.T("hint_drag"), new Vector2(0.5f, 0.24f), 2.2f, false); _hintStage = 1; _hintClock = 0; break;
                case 1:
                    if (_hintClock > 2.0f && sim.GateAvailable && sim.GateIndex == 0)
                    { _ui.Hint(Loc.T("hint_gate"), new Vector2(0.36f, 0.66f), 3f, true); _hintStage = 2; _hintClock = 0; }
                    else if (sim.GateIndex > 0) _hintStage = 2;
                    break;
                case 2:
                    if (sim.ConveyorValue > 0 && _hintClock > 0.5f) { _ui.Hint(Loc.T("hint_collect"), new Vector2(0.3f, 0.36f), 3f, true); _hintStage = 3; _hintClock = 0; }
                    break;
                case 3:
                    if (sim.AliveEnemies > 0 && sim.FrontZ() < 7f && _hintClock > 1f) { _ui.Hint(Loc.T("hint_horde"), new Vector2(0.62f, 0.5f), 2.5f, true); _hintStage = 4; _hintClock = 0; }
                    break;
            }
        }

        void OnApplicationPause(bool pause) { if (pause && _mode == Mode.Battle && _resultDelay < 0) SetPaused(true); }

        // ------------------------------------------------------------------ events -> feel
        void OnSimEvent(SimEvent e)
        {
            bool battle = _mode == Mode.Battle;
            switch (e.Type)
            {
                case SimEventType.Volley: _sfx.Play(Sfx.Id.Shot, 0.3f, UnityEngine.Random.Range(0.95f, 1.08f), 0.07f); break;
                case SimEventType.EnemyKilled: _sfx.Play(Sfx.Id.Pop, 0.3f, UnityEngine.Random.Range(0.9f, 1.25f), 0.045f); break;
                case SimEventType.GateHit: _sfx.Play(Sfx.Id.GateHit, 0.28f, 1f, 0.06f); break;
                case SimEventType.GateSpawned: _sfx.Play(Sfx.Id.Inflate, 0.5f); break;
                case SimEventType.GateBroken: _sfx.Play(Sfx.Id.GateBreak, 0.7f); if (battle) Haptics.Pulse(25); break;
                case SimEventType.ConveyorUpgraded: _sfx.Play(Sfx.Id.Upgrade, 0.6f); break;
                case SimEventType.TileCaught: if (e.Value > 0) { _sfx.Play(Sfx.Id.Tile, 0.45f, 1f + Mathf.Min(0.5f, e.Value * 0.004f), 0.05f); if (battle) Haptics.Pulse(8); } break;
                case SimEventType.SoldiersLost: _sfx.Play(Sfx.Id.Hurt, 0.35f, UnityEngine.Random.Range(0.9f, 1.1f), 0.08f); if (battle) Haptics.Pulse(15); break;
                case SimEventType.BossRevealed:
                    if (battle) { _ui.Banner(Loc.T("boss"), new Color(1f, 0.3f, 0.25f)); _sfx.Play(Sfx.Id.BossHit, 0.8f, 0.6f); }
                    break;
                case SimEventType.BossHit: _sfx.Play(Sfx.Id.BossHit, 0.3f, UnityEngine.Random.Range(0.9f, 1.1f), 0.07f); break;
                case SimEventType.BossKilled: _sfx.Play(Sfx.Id.BossDie, 0.8f); if (battle) Haptics.Pulse(60); break;
                case SimEventType.BossBite: if (battle) Haptics.Pulse(30); break;
                case SimEventType.WaveStarted:
                    if (battle && _endless && e.Value > 1)
                    {
                        _runCoins += LevelGenerator.Create(e.Value - 1).BaseReward / 2;
                        _ui.Banner(Loc.T("wave", e.Value), new Color(0.6f, 0.8f, 1f));
                    }
                    break;
                case SimEventType.Won:
                case SimEventType.Lost:
                    if (battle) { _resultDelay = e.Type == SimEventType.Won ? 1.6f : 1.2f; _sfx.Play(e.Type == SimEventType.Won ? Sfx.Id.Win : Sfx.Id.Lose, 0.8f); }
                    break;
            }
        }

        void FinishBattle()
        {
            var sim = _view.Sim;
            bool won = sim.State == SimState.Won;
            int coins;
            string stats;
            if (_endless)
            {
                int wave = sim.Wave;
                bool best = wave > _profile.BestEndlessWave;
                if (best) _profile.BestEndlessWave = wave;
                coins = _runCoins + LevelGenerator.Create(wave).BaseReward / 4;
                stats = Loc.T("wave", wave) + (best ? "\n" + Loc.T("new_best") : "") + "\n" + Loc.T("kills", sim.Kills) + "\n" + Loc.T("max_squad", sim.MaxSoldiers);
                _ui.ShowResult(false, Loc.T("game_over"), stats, coins);
            }
            else
            {
                coins = won ? BalanceSweep.WinReward(sim.Spec, sim.Soldiers) : BalanceSweep.LossReward(sim.Spec, sim.Progress);
                stats = won ? Loc.T("squad_left", sim.Soldiers) + "\n" + Loc.T("kills", sim.Kills) + "\n" + Loc.T("max_squad", sim.MaxSoldiers)
                            : Loc.T("progress", Mathf.RoundToInt(sim.Progress * 100)) + "\n" + Loc.T("kills", sim.Kills) + "\n" + Loc.T("tip_upgrades");
                if (won) { _profile.Level++; _profile.TutorialDone = true; }
                _ui.ShowResult(won, Loc.T(won ? "victory" : "defeat"), stats, coins);
            }
            _profile.Coins += coins;
            Persistence.Save(_profile);
            _mode = Mode.Result;
            _sfx.Play(Sfx.Id.Coin, 0.6f);
        }

        // ------------------------------------------------------------------ capture mode (agents / CI)
        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        static void Shot(string path)
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
        }

        IEnumerator Capture()
        {
            string dir = Arg("-plasmaCapture");
            int level = int.TryParse(Arg("-plasmaLevel"), out var l) ? l : 3;
            int frames = int.TryParse(Arg("-plasmaFrames"), out var f) ? f : 300;
            int menuFrames = int.TryParse(Arg("-plasmaMenuFrames"), out var mf) ? mf : 45;
            float skill = float.TryParse(Arg("-plasmaSkill"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 0.9f;
            if (float.TryParse(Arg("-plasmaDamage"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dm)) _captureDamage = dm;
            Directory.CreateDirectory(dir);
            Time.captureFramerate = 30;
            Debug.Log($"[Plasma] capture -> {dir} level {level} frames {frames}");
            bool ar = _profile.Arabic;
            _profile = new PlayerProfile { Level = level, Coins = 1234, Arabic = ar, LangChosen = true };
            for (int u = 0; u < Upgrades.Count; u++) _profile.Upg[u] = Mathf.Max(0, (level - 1) / 3);
            GoMenu();
            int n = 0;
            if (Arg("-plasmaSettings") != null) { _ui.RefreshMenu(_profile); _ui.ShowSettings(); }   // capture the settings panel instead of the menu
            for (int i = 0; i < menuFrames; i++) { yield return new WaitForEndOfFrame(); Shot(Path.Combine(dir, $"f{n++:00000}.png")); }
            StartBattle(false);
            _view.Bot = new BotPolicy(skill, 7);
            for (int i = 0; i < frames; i++)
            {
                yield return new WaitForEndOfFrame();
                Shot(Path.Combine(dir, $"f{n++:00000}.png"));
                if (_mode == Mode.Result && i > 0) { for (int k = 0; k < 40; k++) { yield return new WaitForEndOfFrame(); Shot(Path.Combine(dir, $"f{n++:00000}.png")); } break; }
            }
            Debug.Log($"[Plasma] capture done: {n} frames, state {_view.Sim.State}, soldiers {_view.Sim.Soldiers}");
            Application.Quit(0);
        }
    }
}
