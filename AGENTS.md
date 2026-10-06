# AGENTS.md — handover guide for developers and AI agents

Read this first. It explains what Plasma is, how the repository is organised, how to set up a
build machine from zero (no GPU, no root), how to build/test/capture, the conventions, and the
prioritised task list. **Keep it up to date when you change anything it describes.**

The owner (Ayoub Teke, GitHub `ayoub5550`) communicates in **Arabic**. Code, comments and these
docs are in English; `README.md` is bilingual.

---

## 1. What this project is

**Plasma — Squad vs Horde**: an infinite-level, portrait, one-finger Android shooter that reproduces the
mechanic of the owner's reference ad (`docs/reference/reference_ad.mp4`, analysis in `docs/reference/README.md`):
shoot the **upgrade gate** in the dock to upgrade every tile on the left **conveyor** (+1 → +5 → … → +99; starts at +1 since v0.4),
stand at the belt end to **collect** tiles (+N soldiers each), and stop the **red horde** with the **boss** walking
inside it. Full design: `docs/GDD.md`. Difficulty: `docs/LEVELS.md`.

Status **v0.4.0 (2026-10-06)** — **numbers fixed + "soldiers for standing in front of the numbers"** (owner request): the belt pays
from the first second (+1), a glowing collect pad shows where to stand, every printed number (tile, gate, badge) is exactly the
soldiers you get, one merged gold "+N" counter instead of stacked pops. APK 0.4.0 (code 6), same signing key as 0.3.1 (updates in place).

v0.3.1 (2026-10-06) — **own characters modelled in Blender by script** (`tools/models/blender/`): the boss
(red open jacket over white shirt, blond spiky hair + beard, cleaver-greatsword on the shoulder), the helmeted squad soldier and the
round-headed horde grunt — all original work, no third-party art left in the game. APK 0.3.1 (code 5).

v0.3.0 (2026-10-06) — real 3D characters + real-time shadows + bloom on top of the v0.2 "exactly like the video" rebuild:
* CC0 3D characters (Kenney Mini Characters soldier/enemy, Quaternius boss) baked into instanced flipbooks (`tools/models/`, `ModelLibrary`).
* Graphics quality **High** (real shadows, HDR + bloom, models out to z 21) / **Low** (blob shadows, no post) — Settings row, automatic
  first-run pick + fps watchdog (`QualityManager`, `Game.FpsWatchdog`).
* Android APK 0.3.0 (code 4). Everything below from v0.2.1 still applies.

v0.2.1 summary:
* New mechanic (dock gate → belt upgrade → collect), boss inside the horde, horde rush when nothing is near.
* New look matched to the reference frame by frame: camera, palette, chibi soldiers, red carpet horde (2800 instanced + LOD),
  pillow gate (inflate/wobble/deflate), belt tiles with upgrade wave, brute boss with blade, flame tracers, smoke, blob shadows, fog.
* Arabic + English UI (Lalezar OFL font + `ArabicText` shaper), settings (sound, music, vibration, language), tutorial hints on L1–2, synthesised music.
* Balance re-tuned with the new bot: 100 levels, all bot skills win (too gentle → calibrate with players, `docs/LEVELS.md §4`).
* Android APK `com.ayoubteke.plasma` 0.2.1 (code 3), min 24 / target 36, ARM64+ARMv7, ~17 MB, **debug-signed**.
* **Not yet done:** physical-device test (performance/touch feel), difficulty calibration with humans, monetisation, store listing. See §8.

## 2. Repository map

| Path | What |
|---|---|
| `Assets/Plasma/Scripts/Sim/` | **Pure C# gameplay** (asmdef `Plasma.Sim`, `noEngineReferences`): `Balance` (layout + constants), `LevelSpec`/`LevelGenerator` (levels, gate ladders), `BattleSim` (the whole battle: conveyor, dock gate, horde, boss, bullets), `BotPolicy` (AI player), `Upgrades`/`PlayerProfile`, `BalanceSweep`. No `UnityEngine` allowed here. |
| `Assets/Plasma/Scripts/Runtime/` | Unity layer (asmdef `Plasma.Runtime`): `Game` (entry point, flow, tutorial, settings, capture mode), `BattleView` (runs sim at fixed 60 Hz, draws everything + effects), `WorldText` (batched outlined world labels), `GameUI` + `UiKit` (code-built uGUI), `Loc` (EN/AR strings) + `ArabicText` (shaping/RTL), `MeshFactory` (procedural meshes), `InstancedBatch`, `Visuals`, `Palette`, `Sfx` (synth SFX + music), `Haptics`, `Persistence`, `ModelLibrary` (loads baked models), `QualityManager` (High/Low: shadow light, HDR, bloom), `PlasmaBloom` (post effect). |
| `Assets/Plasma/Resources/Shaders/` | `Plasma/Lit` (instanced, tint mask, spec, fog, ForwardBase + shadow receive + ShadowCaster), `Plasma/LitHorde` (same look, no shadow passes — the horde), `Plasma/Fx` (instanced transparent/additive, vertex colour, HDR `_Glow`), `Plasma/Text` (WorldText, ZTest switch), `Hidden/Plasma/Bloom`. In `Resources/` so they are never stripped. |
| `Assets/Plasma/Resources/Models/` | Baked CC0 character flipbooks (`soldier/enemy/boss.bytes`), made by `tools/models/bake_glb.py` — never hand-edit; re-bake. |
| `Assets/Plasma/Resources/Fonts/` | `Lalezar.ttf` (OFL, Latin + Arabic presentation forms) + licence. Loaded by `Visuals.Font`. |
| `Assets/Plasma/Editor/PlasmaBuild.cs` | Setup + builds (APK, AAB, Linux capture player) + balance sweep. Menu **Plasma/**. |
| `Assets/Plasma/Branding/` | Launcher icon (generated by `tools/branding/make_icon.py`). |
| `Assets/Scenes/Main.unity` | Only a camera + `Game`. Regenerated by `PlasmaBuild.Setup` — don't hand-edit. |
| `tools/models/blender/` | **Character sources as code**: `plasmakit.py` (helpers: metaball bodies, primitives, distance-weight skinning, actions, glTF export, Cycles preview) + `boss.py`, `soldier.py`, `enemy.py` (+ `.blend` snapshots for humans). Run headless with Blender 4.2 → `tools/models/src/plasma_*.glb`. |
| `tools/models/` | Source models (`src/`), `models.json` (clip, frames, recolour/tint rules), `bake_glb.py` (offline skinning → `.bytes`). See its README. |
| `tools/simharness/` | Mono console harness: compiles `Sim/*.cs` and runs sweeps/traces in ~2 s, no Unity. |
| `tools/sandbox/` | Unity install (`setup_unity.sh`), launchers (`run_unity.sh`, `unity.sh`), gameplay capture (`capture.sh`), helpers. |
| `docs/` | `GDD.md`, `LEVELS.md`, `ROADMAP.md`, `TESTING.md`, `ART_AUDIO.md`, `STORE.md`, `balance/` (sweep tables), `media/` (screenshots + videos), `store/` (512 px icon), `reference/` (the owner's reference ad + analysis — never ship it). |

Generated / git-ignored: `Library/ Temp/ Logs/ Builds/ UserSettings/`. Never commit builds, keystores or passwords.

## 3. Architecture in one picture

```
 touch drag ─► BattleView.HandleInput ─► sim.TargetX
                                     ┌──────────────── BattleSim.Step(1/60) ─────────────────┐
 BotPolicy (attract/capture/sweep) ─►│ squad move · conveyor (collect/miss) · horde march +   │──► Events (SimEvent list)
                                     │ rush · contact bites · boss · volleys · bullet hits     │        │
                                     │ (dock gate → belt upgrade, horde pierce, boss)          │        ▼
                                     └─────────────────────────────────────────────────────────┘  Game.OnSimEvent → Sfx, Haptics,
 BattleView.Draw: InstancedBatch (soldiers, enemies+LOD, tiles, tracers, smoke, shadows) +            UI banners, tutorial, results
                  DrawMesh (pillow gate, boss) + WorldText (tile/gate numbers, squad count, pops)
```
Rules: gameplay logic lives **only** in `Sim/` (deterministic, engine-free). The view never changes
game state; it reads state and reacts to events. This is what makes headless balancing possible.

## 4. Build machine setup (from zero, rootless Linux — validated 2026-10-06)

```bash
tools/sandbox/setup_unity.sh                       # Unity 2022.3.62f3 + Android SDK/NDK/JDK into /work/unity (~6 min)
# one-time licence activation (Unity Personal; credentials from the owner, NEVER commit or log them):
tools/sandbox/run_unity.sh -batchmode -nographics -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" -quit -logFile /tmp/act.log
grep "Serial number assigned" /tmp/act.log         # → "...-UnityPersXXXX"; licence cached in /work/unity/home
```
Sandbox gotchas (all handled by the scripts, documented so you don't rediscover them):
* **gVisor**: native `UnityShaderCompiler` crashes (`PESetupFS`) → it runs under `qemu-x86_64-static` (wrapper installed by setup). FMOD aborts on realtime-priority → `libschedfix.so` preloaded.
* The Android module only ships as a macOS `.pkg`; its payload is platform-neutral. Setup extracts it with Python (`xar_extract.py` + `cpio_odc.py`), no installer scripts are run.
* Unity 2022.3 wants `SDK/cmdline-tools/6.0` exactly.
* A project folder without `ProjectSettings/` is treated as a **new project and Unity overwrites `Packages/manifest.json`** with the default template (Ads, IAP, TMP…). Keep `ProjectSettings/` committed; if it happens, restore the manifest from git and delete `Packages/packages-lock.json`.
* Runtime-created materials ⇒ Unity strips GPU-instancing variants ⇒ **everything instanced renders invisible**. `PlasmaBuild.KeepInstancingVariants()` sets `m_InstancingStripping: 2` (Keep All). Don't remove it.
* Capturing the Linux player under Xvfb needs `-popupwindow`; otherwise Unity waits forever for a window-manager resize and renders one frame.
* `pkill -f Plasma.x86_64` also kills your own shell (pattern matches the command line) → use `pkill -x Plasma.x86_64`.
* Agent sandboxes: don't background Unity/capture with `( … &)` inside a tool call that has a short deadline — the
  whole process group is killed when the call times out. Run builds in the foreground with a long timeout.

## 5. Everyday commands

```bash
tools/simharness/run.sh 100 0.3                  # balance career sweep (2 s, no Unity)
tools/simharness/run.sh trace 44 1 12 12 11 11   # per-second timeline of one level (+ upgrade levels F R S G)
tools/sandbox/unity.sh Setup                     # compile check + regenerate settings/scene (~40 s)
tools/sandbox/unity.sh BuildAndroid Android      # → Builds/Plasma.apk (~3 min incremental)
tools/sandbox/unity.sh BuildLinux Linux64        # → Builds/linux/Plasma.x86_64 (capture player)
tools/sandbox/capture.sh /tmp/cap 5 1800 0.9     # real rendered gameplay: frames + gameplay.mp4 (~1 min)
PLASMA_LANG=ar tools/sandbox/capture.sh /tmp/cap 10 2400 0.9   # same with the Arabic UI
PLASMA_EXTRA="-plasmaQuality low" tools/sandbox/capture.sh /tmp/cap 10 600 0.9   # extra player args: -plasmaQuality high|low, -plasmaDamage 0.5 (weaker squad: boss walks up), -plasmaSettings 1 (capture the settings panel), -plasmaNoModels
uv run --with numpy --with pillow python tools/models/bake_glb.py   # re-bake the CC0 character models after editing tools/models/models.json
# camera tuning without rebuilding: add  -plasmaCam "px,py,pz,lx,ly,lz,halfWidth"  to the player command line (see BattleView.SetupCamera)
PLASMA_VERSION=0.2.0 PLASMA_VERSION_CODE=2 tools/sandbox/unity.sh BuildAndroid Android   # versioned build
```
Store build (`.aab`): set `PLASMA_KEYSTORE`, `PLASMA_KEYSTORE_PASS`, `PLASMA_KEYALIAS[_PASS]`, run `BuildAndroidStore`.
The keystore is owned by the repo owner and must never be committed (`.gitignore` blocks `*.keystore`/`*.jks`).
Bump `PLASMA_VERSION_CODE` on every upload. Verify an APK: `aapt dump badging Builds/Plasma.apk` and `apksigner verify --print-certs` (build-tools 34 in the AndroidPlayer SDK).

**Signing caveat:** test APKs are signed with Gradle's auto-generated debug key (`~/.android/debug.keystore` of the OS user).
`tools/sandbox/unity.sh` copies a persisted key from `$UNITY_ROOT/keys/debug.keystore` before each build (and saves it there the first
time). Since v0.3.1 the cert SHA-256 is `6f11a828cd0fafcf1c5b5244e54dfb42aa66236e47a7e4fe5b5296a74b1055aa`. A different key = testers must
uninstall before updating.

## 6. Testing (details in `docs/TESTING.md`)

Before every push: (1) `tools/simharness/run.sh 100 0.3` — no new walls; (2) `tools/sandbox/unity.sh Setup` — 0 `error CS`;
(3) for visual changes: `BuildLinux` + `capture.sh`, **look at the frames**; (4) for releases: `BuildAndroid` + `aapt` check + device test checklist.

## 7. Conventions

* Gameplay numbers only in `Balance.cs` / `LevelGenerator.cs` / `Upgrades.cs`; document changes in `docs/LEVELS.md` with the sweep result.
* Every UI string goes through `Loc.T(key)` with both English and Arabic; never assign raw Arabic to a Text (it must be shaped by `ArabicText.Fix`). Arabic lines must not rely on auto-wrap (RTL reordering is per line) — use explicit `\n`.
* World-space labels: add them to `WorldText` (one draw call); don't create TextMesh objects.
* `Sim/` stays engine-free and deterministic (no `UnityEngine`, no `DateTime.Now`, seeded `System.Random` only).
* One MonoBehaviour per file, file name = class name (Unity "missing script" rule).
* New visuals: procedural or CC0 assets only (see `docs/ART_AUDIO.md`); record licence + source for every imported asset in `docs/ART_AUDIO.md`.
  Characters go through `tools/models/` (bake to `.bytes`; no FBX/skinned meshes at runtime). Units drawn in the hundreds use `Visuals.LitHorde` (no shadow passes).
* Lit output is clamped to ≤ 1 so only `Plasma/Fx` materials with `_Glow` > 0 bloom (white text must never glow). Anything that should cast a real
  shadow: `InstancedBatch(..., castShadows: true)` or `Graphics.DrawMesh(..., QualityManager.High, true)`; keep blob shadows for Low (`!QualityManager.High`).
* Branches: `feat/…`, `fix/…`; PR into `main`; keep `main` buildable. Tag releases `vX.Y.Z` and attach APK + SHA256 + capture video.
* Never commit: credentials, keystores, `Builds/`, `Library/`.

## 8. What to do next (priority order) — see `docs/ROADMAP.md` for the full plan

1. **Device test the v0.4.0 APK** (owner): FPS in High vs Low (Settings → Graphics), whether the watchdog drops to Low, shadows/bloom on the
   phone's GPU, touch feel (`BattleView.DragSensitivity`), readability, Arabic text, audio. Collect concrete notes.
   Visual gaps still open vs the ad (`docs/reference/compare_v0.3.1.png`): the boss now matches the ad's design but stands at the far
   end of the lane, partly under the HUD progress bar (ad: closer, mid-upper screen) — fix with camera/boss approach (balance) or a lower HUD;
   squad helmets glow a bit too much under bloom; tracers shorter than the ad's flame streams; script-made models are clean/cartoony,
   not hand-sculpted (an artist can open `tools/models/blender/*.blend`, improve, export to the same `src/*.glb` and re-bake).
2. **Difficulty calibration:** the bot wins all 100 levels first try at every skill (`docs/LEVELS.md §4`). Use the
   levers listed there; add analytics hooks (attempts per level, time per level) once accounts exist.
3. **Performance pass** if needed: quality setting (enemy cap 2800 → 1600, LOD distance), fewer tile labels beyond z 40.
4. **Juice:** coin fly-to-counter, walk squash for horde/boss, red screen-edge flash on damage.
5. **Content (M2):** enemy variety (runners, shields), gate variety (×2, charge gates), boss archetypes, biomes.
6. **Monetisation:** rewarded ads (double coins, +squad start, revive), "remove ads" IAP. Needs owner accounts (AdMob/Unity Ads).
7. **Store:** Play Console listing (AR/EN), privacy policy, content rating, signed AAB, closed testing track.

## 9. Change log

* **v0.4.0 (2026-10-06)** — owner: "fix the number problems and make the player get soldiers for standing in front of the numbers".
  Problems found in the v0.3.1 capture (`docs/reference/compare_v0.4.0_numbers.png`) and fixed:
  (1) gain pops stacked into unreadable piles ("+7 +7", "+27 +135") → `BattleView.AddGain`: one gold counter above the squad counter
  that sums a streak (+5 → +10 → …), follows the squad, restarts every 2 s, rises/fades 0.45 s after the last tile;
  (2) tiles/gate printed the base value but the squad received `round(value × TileBonus)` (+5 printed, +7 received) →
  `BattleSim.GainFor(v)`; tiles, gate label and flying badge all print the effective gain (`BattleView.TileLabel`, cached strings);
  (3) every gate break flashed the whole belt back to "+0" (wave `From` defaulted to 0) → `_beltValue` tracks the shown value;
  (4) tile labels sat mid-face and were hidden by the tile in front → upper face (0.7), 4-5 char values shrink to fit;
  (5) squad counter ≥ 10 000 prints 12.3K. Mechanic: `Balance.StartTileValue = 1` (belt starts at +1, gates ≤ belt value skipped,
  L1 ladder +5 → +20) and `CatchReach` 0.55 → 0.75; glowing **collect pad** + chevrons on the deck at the belt end
  (`BattleView.DrawCollectPad`, solid while the squad is on it). Tutorial order: drag → "stand by the numbers = soldiers" →
  "shoot the gate: bigger numbers" → horde (two-line hints, box 190 px). Sweep: skill 0 168 → 110 attempts, 0.3 109 → 101,
  0.6/1.0 100 (`docs/LEVELS.md §4`). APK 0.4.0 code 6, cert `6f11a828…` (same as 0.3.1).

* **v0.3.1 (2026-10-06)** — owner asked to make the art in Blender: Blender 4.2.3 (headless, CPU) + `tools/models/blender/plasmakit.py`;
  original boss (`boss.py`: metaball body, V-cut open jacket over shirt body, 1.22× cartoon head, 17-bone rig, 1 s stomp walk, sword
  bone-parented to the right hand = excluded from height), soldier (`soldier.py`: big round helmet = team tint channel, navy uniform,
  rifle, 4-frame idle/recoil, 814 tris) and enemy (`enemy.py`: round head = tint, dark legs + angry eyes kept, 6-frame waddle, 588 tris).
  `bake_glb.py` accepts untextured flat-colour materials. `BossModelH` 3.3 → 3.9, `BigBossModelH` 4.1 → 4.8. Sim unchanged. APK 0.3.1 code 5.
  ⚠ **New debug signing certificate** (SHA-256 `6f11a828…055aa`): the old debug key lived in the sandbox OS home and was lost on a
  restart, so 0.3.1 cannot update 0.3.0 in place — uninstall the old app first. `tools/sandbox/unity.sh` now persists the key in
  `$UNITY_ROOT/keys/debug.keystore` (outside the repo) so later versions update normally. Store builds need a real upload key (owner).

* **v0.3.0 (2026-10-06)** — owner asked for "all of it": CC0 3D characters (Kenney Mini Characters soldier + enemy, Quaternius "Matt"
  boss with the knife scaled into a blade) baked offline into flipbook meshes (`tools/models/bake_glb.py` → `Resources/Models/*.bytes`,
  `ModelLibrary`), walk/idle animation per instance phase; real-time directional shadows (`Plasma/Lit` ForwardBase + ShadowCaster,
  `Plasma/LitHorde` without shadow passes) and HDR bloom (`PlasmaBloom`, Fx `_Glow`); **Graphics High/Low** setting (EN/AR) with first-run
  auto pick and fps watchdog; capture args `-plasmaQuality`, `-plasmaDamage`, `-plasmaSettings`, `PLASMA_EXTRA`. Sim/balance unchanged
  (sweep 0.3 → 109 attempts / 100 levels). APK 0.3.0 code 4, ~18 MB.
* **v0.2.1 (2026-10-06)** — owner said "not there yet": side-by-side review vs the ad (`docs/reference/compare_v0.2.1.png`) →
  closer/lower camera (`-plasmaCam` override for tuning), dock + horde moved closer, bigger upright belt tiles + labels,
  bigger gate with cloth heap/collapse (`MeshFactory.Cloth`), tight horde carpet (0.27 u columns, jitter, faces), compact
  tall squad (36 drawn, khaki jackets), 6 long flame streams, tile/gate shadows, neutral grey deck. Remaining gaps vs the ad:
  character model quality (needs real 3D art), bloom/real shadows.
* **v0.2.0 (2026-10-06)** — "exactly like the video": new mechanic (dock gate upgrades the conveyor, collect at the
  belt end, boss inside the horde, horde rush), full visual rebuild (camera, palette, meshes, pillow gate, belt tiles,
  flame tracers, smoke, shadows, fog, effects), `WorldText`, Arabic/English UI with own shaper + Lalezar font,
  settings panel, tutorial hints, synthesised music, new bot + re-tuned balance, reference video in `docs/reference/`.
* **v0.1.0 (2026-10-06)** — first playable: sim + generator + bot sweep, instanced renderer, UI, SFX, icon, Android APK, capture pipeline, docs.
