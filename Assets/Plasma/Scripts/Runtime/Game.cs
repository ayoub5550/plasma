using System;
using System.Collections;
using System.IO;
using Plasma.Sim;
using UnityEngine;

namespace Plasma
{
    /// <summary>
    /// Entry point (the only component in Main.unity). Owns the profile, the battle view, the UI and
    /// the flow Menu -> Battle -> Result. Also implements the headless capture mode used by CI/agents:
    ///   -plasmaCapture DIR [-plasmaLevel N] [-plasmaFrames N] [-plasmaSkill 0..1] [-plasmaMenuFrames N]
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

        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Input.multiTouchEnabled = false;

            _profile = Persistence.Load();
            _sfx = new GameObject("Sfx").AddComponent<Sfx>();
            _sfx.transform.SetParent(transform);
            _sfx.Enabled = _profile.SoundOn;
            Haptics.Enabled = _profile.VibrationOn;
            if (FindObjectOfType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();

            var cam = Camera.main;
            if (cam == null) { cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; }
            _view = new GameObject("Battle").AddComponent<BattleView>();
            _view.SetupCamera(cam);
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
            _ui.OnToggleSound = () => { _profile.SoundOn = !_profile.SoundOn; _sfx.Enabled = _profile.SoundOn; Persistence.Save(_profile); _ui.RefreshMenu(_profile); };

            GoMenu();
            if (Arg("-plasmaCapture") != null) StartCoroutine(Capture());
        }

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
            var mods = _profile.Modifiers();
            _view.Begin(new BattleSim(spec, mods));
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
            _view.Begin(new BattleSim(spec, _profile.Modifiers(), endless));
            _view.Bot = null;
            _sfx.Enabled = _profile.SoundOn;
            _ui.ShowHud(Title(), !endless && _profile.Level <= 2);
            if (spec.BigBoss && !endless) _ui.Banner("BOSS LEVEL", new Color(1f, 0.35f, 0.3f));
        }

        string Title() => _endless ? "WAVE " + (_view.Sim != null ? _view.Sim.Wave : 1) : "LEVEL " + _profile.Level;

        void SetPaused(bool p)
        {
            _paused = p;
            _view.Paused = p;
            _ui.ShowPause(p && _mode == Mode.Battle);
        }

        void Buy(UpgradeType u)
        {
            if (_profile.TryBuy(u)) { Persistence.Save(_profile); _sfx.Enabled = true; _sfx.Play(Sfx.Id.Coin); _sfx.Enabled = _mode != Mode.Menu && _profile.SoundOn; StartAttract(); }
            _ui.RefreshMenu(_profile);
        }

        void Update()
        {
            if (_mode == Mode.Battle && _view.Sim != null)
            {
                _ui.UpdateHud(_endless ? (_view.Sim.Wave - 1 + _view.Sim.Progress) / Mathf.Max(1, _view.Sim.Wave) : _view.Sim.Progress, _profile.Coins + _runCoins, Title());
                if (_resultDelay >= 0)
                {
                    _resultDelay -= Time.deltaTime;
                    if (_resultDelay < 0) FinishBattle();
                }
            }
            if (_mode == Mode.Menu && _view.Sim != null && _view.Sim.State != SimState.Running) StartAttract();
            if (Input.GetKeyDown(KeyCode.Escape)) { if (_mode == Mode.Battle) SetPaused(!_paused); else if (_mode == Mode.Result) GoMenu(); }
        }

        void OnApplicationPause(bool pause) { if (pause && _mode == Mode.Battle && _resultDelay < 0) SetPaused(true); }

        // ------------------------------------------------------------------ events -> feel
        void OnSimEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.Volley: _sfx.Play(Sfx.Id.Shot, 0.35f, UnityEngine.Random.Range(0.95f, 1.08f), 0.07f); break;
                case SimEventType.EnemyKilled: _sfx.Play(Sfx.Id.Pop, 0.35f, UnityEngine.Random.Range(0.9f, 1.25f), 0.045f); break;
                case SimEventType.GateHit: _sfx.Play(Sfx.Id.GateHit, 0.3f, 1f, 0.06f); break;
                case SimEventType.GateBroken: _sfx.Play(Sfx.Id.GateBreak, 0.7f); if (_mode == Mode.Battle) Haptics.Pulse(25); break;
                case SimEventType.SoldiersGained: _sfx.Play(Sfx.Id.Gain, 0.5f, 1f, 0.05f); break;
                case SimEventType.SoldiersLost: _sfx.Play(Sfx.Id.Hurt, 0.4f, UnityEngine.Random.Range(0.9f, 1.1f), 0.08f); if (_mode == Mode.Battle) Haptics.Pulse(15); break;
                case SimEventType.BossSpawned: if (_mode == Mode.Battle) _ui.Banner("BOSS!", new Color(1f, 0.3f, 0.25f)); break;
                case SimEventType.BossHit: _sfx.Play(Sfx.Id.BossHit, 0.35f, UnityEngine.Random.Range(0.9f, 1.1f), 0.07f); break;
                case SimEventType.BossKilled: _sfx.Play(Sfx.Id.BossDie, 0.8f); if (_mode == Mode.Battle) Haptics.Pulse(60); break;
                case SimEventType.BossBite: if (_mode == Mode.Battle) Haptics.Pulse(30); break;
                case SimEventType.WaveStarted:
                    if (_mode == Mode.Battle && _endless && e.Value > 1)
                    {
                        _runCoins += LevelGenerator.Create(e.Value - 1).BaseReward / 2;
                        _ui.Banner("WAVE " + e.Value, new Color(0.6f, 0.8f, 1f));
                    }
                    break;
                case SimEventType.Won:
                case SimEventType.Lost:
                    if (_mode == Mode.Battle) { _resultDelay = 1.3f; _sfx.Play(e.Type == SimEventType.Won ? Sfx.Id.Win : Sfx.Id.Lose, 0.8f); }
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
                stats = $"WAVE {wave}{(best ? "  NEW BEST!" : "")}\nKILLS {sim.Kills}\nMAX SQUAD {sim.MaxSoldiers}";
                _ui.ShowResult(false, "GAME OVER", stats, coins);
            }
            else
            {
                coins = won ? BalanceSweep.WinReward(sim.Spec, sim.Soldiers) : BalanceSweep.LossReward(sim.Spec, sim.Progress);
                stats = won ? $"SQUAD LEFT {sim.Soldiers}\nKILLS {sim.Kills}\nMAX SQUAD {sim.MaxSoldiers}"
                            : $"PROGRESS {Mathf.RoundToInt(sim.Progress * 100)}%\nKILLS {sim.Kills}\nTIP: buy upgrades!";
                if (won) _profile.Level++;
                _ui.ShowResult(won, won ? "VICTORY!" : "DEFEAT", stats, coins);
            }
            _profile.Coins += coins;
            Persistence.Save(_profile);
            _mode = Mode.Result;
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
            Directory.CreateDirectory(dir);
            Time.captureFramerate = 30;
            Debug.Log($"[Plasma] capture -> {dir} level {level} frames {frames}");
            _profile = new PlayerProfile { Level = level, Coins = 1234 };
            for (int u = 0; u < Upgrades.Count; u++) _profile.Upg[u] = Mathf.Max(0, level / 4);
            GoMenu();
            int n = 0;
            for (int i = 0; i < menuFrames; i++) { yield return new WaitForEndOfFrame(); Shot(Path.Combine(dir, $"f{n++:00000}.png")); }
            StartBattle(false);
            _view.Bot = new BotPolicy(skill, 7);
            for (int i = 0; i < frames; i++)
            {
                yield return new WaitForEndOfFrame();
                Shot(Path.Combine(dir, $"f{n++:00000}.png"));
                if (_mode == Mode.Result && i > 0) { for (int k = 0; k < 30; k++) { yield return new WaitForEndOfFrame(); Shot(Path.Combine(dir, $"f{n++:00000}.png")); } break; }
            }
            Debug.Log($"[Plasma] capture done: {n} frames, state {_view.Sim.State}, soldiers {_view.Sim.Soldiers}");
            Application.Quit(0);
        }
    }
}
