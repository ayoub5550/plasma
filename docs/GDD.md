# Plasma — Game Design Document

> Working title **Plasma** (subtitle *Squad vs Horde*). Portrait, one-finger, Android first.
> Status: v0.1.0 playable prototype (2026-10-06). Owner: Ayoub Teke.

## 1. Pitch

You command a small squad of blue soldiers at the bottom of a bridge. A red horde marches
down the right lane; a conveyor of **number gates** slides down the left lane. Your squad
fires straight ahead automatically. Drag left to shoot gates and **grow** your squad
(+1, +5, +25, +99 ...), drag right to **shoot the horde** before it reaches you. Every level
ends with a **boss**. Levels never end — difficulty keeps climbing — and an **Endless**
mode chains waves until you fall.

The core tension in one sentence: **every second spent growing is a second the horde gets closer.**

## 2. Reference

The mechanic is reproduced from an ad video supplied by the owner (popular "shoot the gate
/ defend against the horde" mini-game). **Only the mechanic is reproduced.** All art,
characters, names, sounds and UI are original. Do **not** copy characters, logos or art
from the reference (store rejection / copyright risk).

What the reference shows (frame-by-frame analysis):

| Element | Reference | Plasma v0.1 |
|---|---|---|
| Camera | high, steep, portrait, bridge over dark water | same (`BattleView.SetupCamera`) |
| Left lane | column of gate blocks on a conveyor, front one sits in a slot | same, 14 visible, slide-in animation |
| Gate tiers | grey +0, blue +1, green +5, yellow +99 | grey 0 · blue 1-2 · green 3-5 · cyan 6-12 · yellow 15-30 · orange 40-60 · purple 75-99 |
| Gate break | block collapses like cloth, squad grows, "+5" pops | squash/collapse anim, coloured puffs, floating "+N", chime |
| Right lane | dense red carpet of enemies advancing | instanced red grunts (12 columns), darker brutes later |
| Shooting | squad fires continuous yellow tracers straight up | volleys every 0.22 s, damage pooled into ≤18 tracers |
| Kills | white smoke puffs | white smoke puffs |
| Boss | big brute with a sword + HP bar & number | original brute boss with blade, HP bar + number |

## 3. Core loop

```
 Menu (level N, upgrades) ──► Battle (≈20–45 s) ──► Result (+coins) ──► buy upgrades ──► next level
                                   ▲                                         │
                                   └────────────── retry on defeat ◄─────────┘
```

### 3.1 Controls
* Horizontal drag anywhere (relative drag, `BattleView.HandleInput`). No tapping needed.
* Squad slides at max 16 u/s, clamped to the bridge.
* Back button = pause.

### 3.2 Squad & shooting
* Squad = an integer `Soldiers`. Formation = sunflower packing (visual cap 140 soldiers).
* Every `0.22 s / FireRateMult` the squad fires a volley. Total damage of a volley =
  `Soldiers × 1 × DamageMult`, split into `min(Soldiers, 18)` tracers that start from
  random soldiers in the formation (so a wider squad covers more columns).
* **Pierce**: a tracer's damage carries on to the next enemy in the same column until spent.
  Big squads therefore carve deep notches into the horde — exactly the reference look.

### 3.3 Gates (left lane)
* The front gate sits in a slot at z = 12.5; it has HP. Break it → `+Value × GateMult` soldiers.
* The next gate slides in (0.35 s, not shootable while sliding).
* Values ramp from small to a jackpot inside each level; a few **+0 blockers** sit early to
  punish greed (they cost time and give nothing, but block better gates).
* Gate HP = `(2 + 2.2 × Value^1.1) × (1 + 0.10 × (L−1))`.

### 3.4 Horde (right lane)
* 12 columns × N rows, all marching at the level's speed (1.25 → 2.5 u/s).
* An enemy reaching the defence line (z = 0.7) dies and kills **1 soldier** (brute: 3).
  This is true even if the squad is away at the gates → leaving the horde is a real cost.
* Brutes (×4 HP, bigger, darker) appear from level 4 (4 % → 30 %).

### 3.5 Boss
* Spawns when the horde is cleared, walks toward the squad (0.9–1.1 u/s), stops at the line
  and then eats soldiers continuously (`BossBiteRate` per second) until killed.
* Every 5th level is a **boss level**: big boss (×5 HP, bigger model, "BOSS LEVEL" banner).

### 3.6 Win / lose
* Win: horde + boss dead. Reward = `BaseReward + min(squad,1000)/5`; `BaseReward = 25 + 8L` (×2 on boss levels).
* Lose: squad reaches 0. Consolation = `BaseReward × 0.5 × progress`.

## 4. Meta progression

Four permanent upgrades (cost = base × 1.25^level, max 60):

| Upgrade | Effect per level | Base cost |
|---|---|---|
| Firepower | +15 % damage | 60 |
| Fire rate | +8 % volleys/s | 60 |
| Start squad | +1 starting soldier | 80 |
| Gate bonus | +12 % soldiers from gates | 70 |

Planned (see ROADMAP): skins (squad colour/helmet), daily reward, rewarded-ad boosts
("start with +20", "double coins"), offline earnings, achievements.

## 5. Modes

* **Levels (main)** — infinite, deterministic: level N is always the same map (seeded). Progress saved.
* **Endless** — waves of increasing difficulty (wave N uses level N's spec) without healing;
  gates keep coming. Score = best wave. Coins per cleared wave.
* **Attract mode** — the menu background is a live battle played by the bot.

## 6. Game feel checklist (implemented ✅ / planned ⬜)

✅ recoil + bob on soldiers · ✅ enemy hit flash · ✅ white smoke puffs on kills · ✅ gate shake/flash on hit ·
✅ gate collapse + coloured burst · ✅ floating "+N" · ✅ squad count label · ✅ camera shake on damage ·
✅ boss HP bar + number · ✅ BOSS banner · ✅ procedural SFX for every event · ✅ short haptic pulses ·
⬜ slow-mo on boss kill · ⬜ coin fly-to-counter animation · ⬜ squad "jump-in" animation for new soldiers ·
⬜ music loop · ⬜ dynamic lighting / shadows (blob shadows) · ⬜ particle trails on tracers

## 7. Tech constraints (summary — details in AGENTS.md)

* Unity 2022.3.62f3 LTS, built-in render pipeline, GLES3, IL2CPP ARM64+ARMv7, min SDK 24, target 36.
* No imported art: all meshes procedural (`MeshFactory`), all sounds synthesised (`Sfx`).
* Everything visible is GPU-instanced (`InstancedBatch`) — thousands of units at 60 fps on mid phones (to be verified on device).
* Gameplay = pure C# simulation (`Assets/Plasma/Scripts/Sim`, no UnityEngine) → testable headless with Mono in 2 s.
