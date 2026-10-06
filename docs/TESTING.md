# Testing

Four layers, cheapest first.

| Layer | Tool | Time | Catches |
|---|---|---|---|
| 1. Simulation / balance | `tools/simharness/run.sh` (Mono, no Unity) | 2 s | difficulty walls, economy, sim bugs (hangs, NaN) |
| 2. Compile + project setup | `tools/sandbox/unity.sh Setup` | ~40 s | C# errors, asmdef issues, settings drift |
| 3. Rendered gameplay | `BuildLinux` + `tools/sandbox/capture.sh` | ~2 min | invisible objects, UI layout, effects, flow (menu→battle→result) |
| 4. Device | APK on a phone | manual | performance, touch feel, audio, haptics, safe areas |

## 1. Simulation

```bash
tools/simharness/run.sh 100 0.3                  # career sweep: casual bot
tools/simharness/run.sh 100 1                    # expert bot
tools/simharness/run.sh trace 44 1 12 12 11 11   # one level, per-second state (+ upgrade levels F R S T)
```
The harness compiles `Assets/Plasma/Scripts/Sim/*.cs` + `tools/simharness/Program.cs` with Unity's bundled Mono
(`UNITY_MONO` overrides the path). Output is Markdown — save sweeps to `docs/balance/`.
`tools/simharness/run.sh levels` prints the generated-level table used in `docs/LEVELS.md §3`.

Bot skill model (`BotPolicy`): reaction time `0.06 + 0.5·(1−skill)` s, safety-margin and aim noise. It picks
between the three spots (horde / dock / belt): breaks the gate when it fits before the horde arrives, collects
while safe, takes short belt "dashes" under pressure when the tiles outweigh the expected bites, and grabs a
burst of jackpot tiles. It has perfect information, so real players are weaker than the same number suggests.

## 2. Compile

`tools/sandbox/unity.sh Setup` → must print `[Plasma] Setup done` and `exit=0`. Log: `Logs/unity_Setup.log`
(`grep "error CS"`). If the log shows "Invalid ILPostProcessor configuration" with zero CS errors, stale
`Unity.ILPP.Runner` processes are alive: kill them, delete `/tmp/ilpp.sock-*` and `Temp/UnityLockfile`.

## 3. Rendered capture (no GPU)

```bash
tools/sandbox/unity.sh BuildLinux Linux64
tools/sandbox/capture.sh /tmp/cap 5 1800 0.9   # level 5, up to 1800 frames, bot skill 0.9
```
The player runs under Xvfb + Mesa llvmpipe at 540×1170 with `Time.captureFramerate = 30`, so frames are
deterministic regardless of render speed. Capture mode (`Game.Capture`, args `-plasmaCapture DIR
-plasmaLevel N -plasmaFrames N -plasmaSkill S -plasmaMenuFrames N -plasmaLang en|ar`; env `PLASMA_LANG`, `MENU_FRAMES` in capture.sh) records the menu, then a full bot-played
level including the result screen, then quits. Output: `frames/f00000.png…`, `gameplay.mp4`, `player.log`.

Review: build a contact sheet and look at it —
`ffmpeg -i gameplay.mp4 -vf "select='not(mod(n\,60))',scale=216:-1,tile=7x2" -frames:v 1 sheet.png`.

Debug switches: `-plasmaNoInst` (skip instanced draws), `-plasmaNoUI` (hide canvas).

## 4. Device test checklist (fill in per release)

| # | Check | Pass criteria |
|---|---|---|
| 1 | Install & launch | icon correct, splash, menu < 3 s |
| 2 | FPS L1 / L30 / L60 / Endless wave 40 | ≥ 55 fps mid phone, ≥ 30 fps low phone, no hitches on gate break |
| 3 | Drag feel | squad follows finger 1:1, no jitter, can reach both lane edges comfortably |
| 4 | Readability | gate + tile numbers readable at a glance; squad count readable; Arabic text joined correctly |
| 4b | Mechanic clarity | a new player understands (with the hints) gate → belt upgrade → collect at the belt end |
| 5 | Notch / safe area | HUD not under the notch or nav bar |
| 6 | Audio | no clipping, shots not annoying after 2 min, music loop seamless, sound/music toggles persist |
| 7 | Haptics | short ticks only, can be disabled |
| 8 | Pause / background | home button pauses, resume works, no lost progress |
| 9 | Save | kill app → level, coins, upgrades persist |
| 10 | Battery/heat | 10 min play: device not hot, battery drop noted |

Logs from a device: `adb logcat -s Unity`.

## 5. Known limitations of sandbox testing

* No audio output (FMOD runs on emulated output) — sounds are exercised but not heard.
* llvmpipe ≠ phone GPU: capture proves correctness, not performance.
* Touch input is not exercised by the bot (it sets `TargetX` directly).
* Capture mode fixes `Time.deltaTime` (captureFramerate) but not `unscaledDeltaTime` — use `Time.deltaTime` for UI animations or they run at wall-clock speed in captures.
