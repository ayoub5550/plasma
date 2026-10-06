# Plasma — Game Design Document

> Working title **Plasma** (subtitle *Squad vs Horde*, Arabic: بلازما — الفرقة ضد الحشد). Portrait, one-finger, Android first.
> Status: v0.2.0 — mechanic and look rebuilt to match the reference video (2026-10-06). Owner: Ayoub Teke.

## 1. Pitch

You command a squad of blue soldiers at the bottom of a deck. A red horde marches down the right
bridge with a sword-wielding boss inside it. On the left, a **conveyor of "+N" tiles** slides toward
you — stand next to its end to collect them and grow. At the far edge of the deck an **upgrade gate**
inflates: shoot it down and **every tile on the belt is upgraded** (+0 → +1 → +5 → … → +99).
Your squad fires automatically; you only slide it left and right.

The core tension in one sentence: **every second spent upgrading or collecting is a second the horde marches unopposed.**

## 2. Reference

The owner supplied an ad video: `docs/reference/reference_ad.mp4` (analysis in `docs/reference/README.md`).
**Only the mechanic and layout are reproduced.** All art, characters, names, sounds and UI are original
and procedural. Never copy characters, logos or art from the reference.

| Element | Reference | Plasma v0.2 |
|---|---|---|
| Camera | high, steep, portrait; deck at the bottom, two bridges to the horizon over a dark void | same (`BattleView.SetupCamera`) |
| Left | conveyor of standing "+N" tiles sliding toward the player | same; tiles collected at the belt end (`BattleSim.StepConveyor`) |
| Upgrade gate | puffy cushion in a dark slot; inflates, breaks, badge flies to the belt | same: inflate (ease-out-back), wobble on hit, deflate + pieces + ring badge |
| Belt upgrade | all tiles change colour/value in a wave from the near end | same (`BattleView.TileValue`, wave 45 u/s) |
| Tiers | grey +0, blue +1, green +5, yellow +99 | grey 0 · blue 1–2 · green 3–9 · cyan 10–24 · purple 25–60 · yellow 61–99 |
| Collect | "+N" pops, soldiers jump into the squad | gold "+N" pop, up to 6 soldiers arc from the belt into the formation |
| Horde | dense red carpet filling the right bridge | 14 columns, up to 2800 instanced enemies (LOD beyond z 21) |
| Shooting | straight orange-yellow tracer streams | up to 9 parallel streams, sweeping between columns |
| Kills | white smoke clouds at the front | white smoke puffs (budgeted ~45/s) |
| Boss | big brute with a giant sword inside the horde, HP bar + number, white flash on hit | same, original design (orange spiky hair, red jacket, black blade) |

## 3. Core loop

```
 Menu (level N, upgrades, settings) ──► Battle (≈30–60 s) ──► Result (+coins) ──► upgrades ──► next level
                                            ▲                                          │
                                            └───────────── retry on defeat ◄───────────┘
```

### 3.1 Controls
* Relative horizontal drag anywhere (`BattleView.HandleInput`). No tapping. Back = pause.
* Squad slides at max 16 u/s, x ∈ [−3.05, 4.25].

### 3.2 The three spots
| Spot | Squad x | What happens |
|---|---|---|
| **Belt end** (far left) | left-most soldier within `ConvMaxX + 0.55` | every arriving tile (≈2.8/s) adds `round(value × TileBonus)` soldiers; bullets partly hit the gate |
| **Dock** (left-centre) | ≈ −1.75 | bullets hit the upgrade gate |
| **Horde lane** (right) | 0 … 4.25 | bullets kill the horde / boss |

### 3.3 Squad & shooting
* Squad = an integer `Soldiers` (can reach thousands). Up to **50 soldiers are drawn**; the width of the
  formation is capped accordingly, so aiming matters at every size. The count is shown above the squad.
* A volley every `0.2 s / FireRate`: total damage `Soldiers × 0.25 × Firepower`, split into up to 9
  parallel streams across the squad front. Streams shift by ⅓ spacing each volley so no column survives between them.
* **Pierce:** a stream's damage carries on to the next enemy in the same column until spent → big squads carve deep notches.

### 3.4 Upgrade gate (dock) and conveyor
* Each level has a **ladder** of 3–6 gates, geometric from +1 to the level's jackpot (L1: +1 → +5 → +20; boss
  levels reach +50/+99 early). Breaking a gate sets the belt value; the next gate inflates (0.75 s, not shootable).
* The belt starts at **+0** (grey tiles worth nothing), exactly like the reference.
* Gate HP = `(4 + 9·(v−1))·(1 + 0.06·t)`.

### 3.5 Horde (right lane)
* 14 columns × N rows marching at the level's speed (0.6 → 1.15 u/s). While nothing is within z 11 the horde
  **rushes** (up to ×3.5) so there is never dead time.
* An enemy reaching the defence line (z 0.7) dies and kills **1 soldier** (brute: 3) — wherever the squad is.
* Brutes (×4 HP, bigger, darker) from level 4 (3 % → 25 %).

### 3.6 Boss
* Walks **inside** the horde (80 % depth; 90 % on boss levels). Rows behind him in his columns queue up behind
  him when he stops. "BOSS!" banner + HP bar when he gets close (z < 19).
* At the squad he stops and eats `6 + 0.4·L` soldiers/s (×2 big boss) until killed. Kill = slow-motion, smoke burst, shake.
* Every 5th level: **big boss** (×3 HP, larger), "BOSS LEVEL" banner, bigger jackpot.

### 3.7 Win / lose
* Win: horde + boss dead. Reward = `BaseReward + min(squad,1000)/5`; `BaseReward = 25 + 8L` (×2 boss levels).
* Lose: squad reaches 0. Consolation = `BaseReward × 0.5 × progress`.

## 4. Meta progression

Four permanent upgrades (cost = base × 1.25^level, max 60):

| Upgrade | Effect per level | Base cost |
|---|---|---|
| Firepower | +15 % damage | 60 |
| Fire rate | +8 % volleys/s | 60 |
| Start squad | +1 starting soldier | 80 |
| Tile bonus | +12 % soldiers per collected tile | 70 |

Planned (ROADMAP): skins, daily reward, rewarded-ad boosts ("start with +20", "double coins"), offline earnings, achievements.

## 5. Modes

* **Levels (main)** — infinite, deterministic (seeded). Progress saved.
* **Endless** — waves chain (wave N = level N spec) without resetting the squad; gates that beat the current
  belt value are appended. Score = best wave.
* **Attract mode** — the menu background is a live battle played by the bot.
* **Tutorial** (levels 1–2): contextual hints — drag to move → shoot the gate → collect the tiles → stop the horde.

## 6. Game feel checklist (implemented ✅ / planned ⬜)

✅ gate inflate / wobble / deflate + pieces · ✅ ring badge flies to the belt · ✅ belt upgrade wave with flash + pop ·
✅ tiles fly into the squad / fall off the belt end · ✅ soldiers jump in on collect · ✅ gold "+N" pops ·
✅ enemy hit flash · ✅ white smoke on kills · ✅ muzzle glow · ✅ blob shadows · ✅ boss white flash, HP bar + number ·
✅ slow-mo + smoke burst on boss kill · ✅ camera shake · ✅ procedural SFX for every event · ✅ synthesised music loop ·
✅ short haptics · ✅ Arabic + English UI · ⬜ coin fly-to-counter · ⬜ character walk animation · ⬜ red screen-edge flash on damage

## 7. Tech constraints (summary — details in AGENTS.md)

* Unity 2022.3.62f3 LTS, built-in RP, GLES3, IL2CPP ARM64+ARMv7, min SDK 24, target 36.
* No imported art except the OFL font Lalezar: meshes procedural (`MeshFactory`), audio synthesised (`Sfx`).
* Everything dynamic is GPU-instanced (`InstancedBatch`); all world labels are 2 batched meshes (`WorldText`).
* Gameplay = pure C# simulation (`Assets/Plasma/Scripts/Sim`, no UnityEngine) → testable headless with Mono in 2 s.
