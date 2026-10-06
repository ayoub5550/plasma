# Levels & difficulty curve

Source of truth: `Assets/Plasma/Scripts/Sim/LevelGenerator.cs` (formulas + public tunables),
`Balance.cs` (layout, fire rate, conveyor speed) and `Upgrades.cs` (player power). **Change numbers
there, re-run the sweep, then update this file.** Never hand-edit levels: level N is generated
deterministically (seed = N·7919+17).

## 1. Design rules

1. **Infinite levels.** Level N exists for every N ≥ 1 and is always identical.
2. **Saw-tooth curve.** Every **5th level is a boss level** (big boss, bigger jackpot gate) and the
   level after it is a **relief level** (horde ×0.8, jackpot ×0.85).
3. **The core decision** (from the reference video): every second spent at the dock (breaking the
   upgrade gate) or at the belt (collecting tiles) is a second the horde marches unopposed.
4. **Onboarding is free.** Levels 1–10 must be won first try by a casual player (bot skill 0.3).
5. **No hard walls.** A casual player never needs more than ~15 attempts on one level.
6. Visible enemies are capped at 2800 (performance); beyond that the count stays and HP grows by √(n/2800).

## 2. Formulas (t = level − 1)

| Quantity | Formula | Tunable(s) |
|---|---|---|
| Enemies | `1400 + 120·t` (×0.8 relief), rounded to 14 columns, cap 2800 then HP×√(n/2800) | `HordeBase, HordePerLevel` |
| Enemy HP (grunt) | `1 + 0.14·t + 0.0008·t²` (brute = ×4) | `EnemyHpLin, EnemyHpQuad` |
| Brute share | 0 % until L4, then `3 % + 0.6 %·(L−4)`, max 25 % | in code |
| Horde speed | `min(0.6 + 0.012·t, 1.15)` u/s; **rushes** up to ×3.5 while nothing is within z 11 (no dead time) | `BattleSim.MarchSpeed` |
| Jackpot (last gate value) | `Nice(min(99, (18 + 4·t)·(1.6 boss)·(0.85 relief)))` | `TopBase, TopPerLevel, TopExp` |
| Gate ladder | `3 + min(3, L/8)` gates, geometric 1 → jackpot, snapped to 1,2,3,5,8,10,15,20,25,30,40,50,75,99 | in code |
| Gate HP | `(4 + 9·(v − 1))·(1 + 0.06·t)` | `GateHp*` |
| Conveyor | one tile every 1.15 u at 3.2 u/s ≈ **2.8 tiles/s**; tile = `round(value × TileBonus)` soldiers | `Balance.Conv*` |
| Squad damage | 1.25 dps per soldier × Firepower × Fire rate; up to 9 parallel tracer streams | `Balance.FireInterval, BaseDamage` |
| Boss HP | `max(30, 0.12 · horde HP)` (×3 on boss levels); walks inside the horde at 80 % depth (90 % boss level) | `BossHpMult, BigBossMult` |
| Boss bite | `(6 + 0.4·L)` soldiers/s while touching the squad (×2 big boss) | in code |
| Win reward | `(25 + 8·L)` (×2 boss level) `+ min(squad,1000)/5` | `BalanceSweep.WinReward` |
| Loss reward | `BaseReward · 0.5 · progress` | `BalanceSweep.LossReward` |

## 3. Generated levels (v0.2.0)

| Level | kind | enemies | enemy HP | brutes | speed | gates | gate ladder (value/HP) | boss HP | reward |
|---|---|---|---|---|---|---|---|---|---|
| 1 | normal | 1400 | 1.0 | 0% | 0.60 | 3 | +1/4 +5/40 +20/175 | 168 | 33 |
| 2 | normal | 1526 | 1.1 | 0% | 0.61 | 3 | +1/4 +5/42 +20/185 | 209 | 41 |
| 3 | normal | 1638 | 1.3 | 0% | 0.62 | 3 | +1/4 +5/45 +25/246 | 252 | 49 |
| 4 | normal | 1764 | 1.4 | 3% | 0.64 | 3 | +1/5 +5/47 +30/313 | 329 | 57 |
| 5 | boss | 1876 | 1.6 | 4% | 0.65 | 3 | +1/5 +8/83 +50/552 | 1176 | 130 |
| 6 | relief | 1596 | 1.7 | 4% | 0.66 | 3 | +1/5 +5/52 +30/344 | 371 | 73 |
| 10 | boss | 2478 | 2.3 | 7% | 0.71 | 4 | +1/6 +5/62 +20/269 +99/1364 | 2483 | 210 |
| 15 | boss | 2800 | 3.3 | 10% | 0.77 | 4 | +1/7 +5/74 +20/322 +99/1630 | 4241 | 290 |
| 20 | boss | 2800 | 4.5 | 13% | 0.83 | 5 | +1/9 +3/47 +10/182 +30/567 +99/1896 | 6284 | 370 |
| 25 | boss | 2800 | 6.0 | 16% | 0.89 | 6 | +1/10 +3/54 +5/98 +15/317 +40/866 +99/2162 | 8814 | 450 |
| 30 | boss | 2800 | 7.6 | 19% | 0.95 | 6 | +1/11 +2/36 +5/110 +15/356 +40/973 +99/2428 | 11879 | 530 |
| 40 | boss | 2800 | 11.3 | 25% | 1.07 | 6 | +1/13 +2/43 +5/134 +15/434 +40/1186 +99/2959 | 19808 | 690 |
| 50 | boss | 2800 | 15.8 | 25% | 1.15 | 6 | +1/16 +3/87 +8/264 +15/512 +40/1399 +99/3491 | 27820 | 850 |
| 60 | boss | 2800 | 21.0 | 25% | 1.15 | 6 | +1/18 +3/100 +8/304 +15/590 +40/1612 +99/4022 | 36976 | 1010 |
| 75 | boss | 2800 | 30.2 | 25% | 1.15 | 6 | +1/22 +2/71 +5/218 +15/707 +40/1931 +99/4820 | 53204 | 1250 |
| 100 | boss | 2800 | 49.4 | 25% | 1.15 | 6 | +1/28 +2/90 +5/278 +15/902 +40/2464 +99/6149 | 87209 | 1650 |

(Multiples of 5 are boss levels. Regenerate with `tools/simharness/run.sh levels`.)

## 4. Career sweep results (bot plays 1..100, retries, buys upgrades like a player)

`tools/simharness/run.sh 100 <skill>` — full tables in `docs/balance/career_skill_*.md`.

| Bot skill | Meaning | Result v0.2.0 |
|---|---|---|
| 1.0 | fast reactions, dashes under pressure | 100/100 first try |
| 0.6 | good player | 100/100 first try |
| 0.3 | casual | 100/100 first try |
| 0.0 | very slow reactions | 100 levels, 101 attempts |

Typical level length for the bot: 25–45 s.

**Known issue (P1): the curve is too gentle for bots.** The squad economy is exponential (tile
value × collection time), so once the bot reaches the jackpot tier it out-scales the horde. Bots
make perfect macro decisions; humans will not, so the real difficulty must be calibrated from the
first device tests / analytics. Levers, in order: earlier pressure (lower `HordeStartZ`, faster
`HordeSpeed` growth), higher `GateHpGrowth` (jackpot comes later), `EnemyHpLin`, and capping
`TopValue` growth. Re-run the sweep after each change.

## 5. Endless mode

Wave N uses `LevelGenerator.Create(N)`; the squad is **not** reset between waves; only gates whose
value beats the current belt value are appended to the dock queue. Coins: half of each cleared
wave's `BaseReward` + a quarter of the final wave's. Best wave is saved.

## 6. How to change difficulty safely

1. Edit tunables in `LevelGenerator.cs` / `Balance.cs` / `Upgrades.cs`.
2. `tools/simharness/run.sh 100 0.3`, `0.6` and `1` (≈2 s each); `run.sh trace L skill F R S T` for one level second by second.
3. Check the rules in §1.
4. Save the tables to `docs/balance/`, update §2–§4 here, commit with the numbers in the message.
