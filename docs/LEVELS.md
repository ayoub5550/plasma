# Levels & difficulty curve

Source of truth: `Assets/Plasma/Scripts/Sim/LevelGenerator.cs` (formulas + public tunables) and
`Assets/Plasma/Scripts/Sim/Upgrades.cs` (player power). **Change numbers there, re-run the sweep,
then update this file.** Never hand-edit levels: level N is generated deterministically (seed = N·7919+17).

## 1. Design rules

1. **Infinite levels.** Level N exists for every N ≥ 1 and is always identical (players can compare).
2. **Saw-tooth curve.** Difficulty rises every level; every **5th level is a boss level** (spike:
   big boss ×5 HP, horde ×0.85) and the level after it is a **relief level** (horde ×0.8).
3. **Onboarding is free.** Levels 1–10 must be won first try by a casual player (bot skill 0.3).
4. **Upgrades are the long-term lever.** From ~30 the casual player needs retries/upgrades;
   enemy power grows slightly faster than gate income, so coins (and later rewarded boosts) matter.
5. **No hard walls.** A casual player must never need more than ~15 attempts on one level
   (current v0.1 violates this at level ~80 → ROADMAP P1).
6. Visible enemies are capped at 2400 (performance); beyond that count stays and HP scales.

## 2. Formulas (t = level − 1)

| Quantity | Formula | Tunable(s) |
|---|---|---|
| Enemies | `90 + 30·t + 0.9·t^1.5` (×0.85 boss, ×0.8 relief), cap 2400 then HP×(n/2400) | `HordeBase, HordePerLevel, HordeCurve, HordeCurveExp` |
| Enemy HP (grunt) | `1 + 0.40·t + 0.011·t²` (brute = ×4) | `EnemyHpLin, EnemyHpQuad` |
| Brute share | 0 % until L4, then `4 % + 0.8 %·(L−4)`, max 30 % | in code |
| Horde speed | `min(1.25 + 0.022·t, 2.5)` u/s | in code |
| Gate budget (sum of values) | `22 + 6.5·t` | `GateBudgetBase, GateBudgetPerLevel` |
| Gate count | `7 + min(L/4, 6)` + `min(3, 1 + L/10)` "+0" blockers | in code |
| Gate values | geometric ramp ×1.3, jitter ±15 %, snapped to 1,2,3,4,5,6,8,10,12,15,20,25,30,40,50,60,75,99 | `GateRamp` |
| Gate HP | `(2 + 2.2·v^1.1)·(1 + 0.10·t)` | `GateHp*` |
| Boss HP | `EnemyHp(L)·(60 + 10·L)` (×5 on boss levels) | `BossHp*, BigBossMult` |
| Boss bite | `(8 + 0.5·L)` soldiers/s (×2 big boss) | in code |
| Win reward | `(25 + 8·L)` (×2 boss level) `+ min(squad,1000)/5` | `BalanceSweep.WinReward` |
| Loss reward | `BaseReward · 0.5 · progress` | `BalanceSweep.LossReward` |

## 3. Generated levels (v0.1.0)

| Level | kind | enemies | enemy HP | brutes | speed | gates | gate values | boss HP | reward |
|---|---|---|---|---|---|---|---|---|---|
| 1 | normal | 90 | 1.0 | 0% | 1.25 | 8 | 1 0 2 2 3 3 5 6 | 70 | 33 |
| 2 | normal | 121 | 1.4 | 0% | 1.27 | 8 | 2 0 2 3 4 5 5 8 | 113 | 41 |
| 3 | normal | 153 | 1.8 | 0% | 1.29 | 8 | 0 2 3 3 5 6 8 8 | 166 | 49 |
| 4 | normal | 185 | 2.3 | 4% | 1.32 | 9 | 2 0 2 3 4 5 6 8 10 | 230 | 57 |
| 5 | boss | 185 | 2.8 | 5% | 1.34 | 9 | 2 0 3 3 4 6 8 10 12 | 1527 | 130 |
| 6 | relief | 200 | 3.3 | 6% | 1.36 | 9 | 0 2 3 4 5 8 10 10 12 | 393 | 73 |
| 10 | boss | 327 | 5.5 | 9% | 1.45 | 11 | 3 0 3 0 4 6 6 10 10 15 20 | 4393 | 210 |
| 15 | boss | 474 | 8.8 | 13% | 1.56 | 12 | 3 0 0 4 5 5 8 10 15 15 25 30 | 9194 | 290 |
| 20 | boss | 624 | 12.6 | 17% | 1.67 | 15 | 2 0 0 3 3 0 4 5 8 10 12 15 20 25 30 | 16342 | 370 |
| 25 | boss | 778 | 16.9 | 21% | 1.78 | 16 | 0 2 2 3 0 0 4 6 6 8 12 15 15 20 30 40 | 26251 | 450 |
| 30 | boss | 935 | 21.9 | 25% | 1.89 | 16 | 0 0 2 0 3 3 5 6 8 10 12 20 25 30 30 40 | 39332 | 530 |
| 40 | boss | 1257 | 33.3 | 30% | 2.11 | 16 | 2 0 3 0 0 4 6 8 10 15 20 25 25 40 60 60 | 76661 | 690 |
| 50 | boss | 1588 | 47.0 | 30% | 2.33 | 16 | 4 0 0 0 5 6 8 10 12 15 25 30 40 40 60 75 | 131631 | 850 |
| 60 | boss | 1928 | 62.9 | 30% | 2.50 | 16 | 5 0 0 0 6 8 10 12 15 20 25 40 50 60 75 99 | 207540 | 1010 |
| 75 | boss | 2400 | 92.7 | 30% | 2.50 | 16 | 5 0 6 0 8 0 10 15 15 25 40 40 50 75 99 99 | 367886 | 1250 |
| 100 | boss | 2400 | 207.4 | 30% | 2.50 | 16 | 6 0 0 0 8 10 15 20 20 30 50 50 75 99 99 99 | 786578 | 1650 |

(Multiples of 5 are boss levels. Regenerate with `tools/simharness/run.sh levels`.)

## 4. Career sweep results (bot plays 1..100, retries, buys upgrades like a player)

`tools/simharness/run.sh 100 <skill>` — full tables in `docs/balance/career_skill_*.md`.

| Bot skill | Meaning | Result v0.1.0 |
|---|---|---|
| 1.0 | instant reactions, perfect dashes | 100/100 levels first try |
| 0.6 | good player | 100 levels, 100 attempts |
| 0.45 | average player | 100 levels, 111 attempts (retries from L53) |
| 0.3 | casual | first retry at L31, grind from L55, **stuck at L80** (20 attempts) |

Interpretation: the curve is friendly for the first ~30 levels (good for retention), then
becomes upgrade-driven. Bots have perfect information, so real humans will be weaker than the
same "skill" number — **calibrate with real playtest data** (analytics: attempts per level,
session length) before the store launch (ROADMAP P1).

## 5. Endless mode

Wave N uses `LevelGenerator.Create(N)`; the squad is **not** reset between waves and new gates
are appended to the conveyor. Coins: half of each cleared wave's `BaseReward` + a quarter of the
final wave's. Best wave is saved.

## 6. How to change difficulty safely

1. Edit tunables in `LevelGenerator.cs` / `Upgrades.cs`.
2. `tools/simharness/run.sh 100 0.3` and `0.45` and `1` (2 s each).
3. Check the rules in §1 (first retry for 0.3 not before L25, never > 15 attempts, 0.6 rarely retries).
4. Save the tables to `docs/balance/`, update §2–§4 here, commit with the numbers in the message.
