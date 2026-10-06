# Art & audio direction

## Style
Bright, toy-like, readable at a glance on a small screen (reference: hyper-casual crowd shooters).
* **Team colours:** squad blue `#2973FF`, horde red `#F2211F`, brutes dark red, boss red jacket.
* **Gate tiers** (value → colour): 0 grey · 1–2 blue · 3–5 green · 6–12 cyan · 15–30 yellow · 40–60 orange · 75–99 purple (`Palette.Gate`, `LevelGenerator.TierOf`).
* **World:** light-grey bridge deck over near-black navy water; conveyor darker; rails mid-grey.
* **Lighting:** half-lambert + soft rim in `Plasma/Lit` (no real-time shadows yet — blob shadows planned).
* **FX:** tracers = bright core + additive orange glow; kills = white smoke puffs; gate break = coloured burst.
* **UI:** chunky rounded buttons with darker base (pressed = face moves down), bold outlined text, gold for money.

## Current assets (all original, generated in code)
| Asset | Source |
|---|---|
| Soldier, enemy, boss, gate, cube, sphere meshes | `MeshFactory.cs` (procedural, vertex colours; alpha = tint mask) |
| Shaders | `Assets/Plasma/Resources/Shaders/*` (written for this project) |
| UI sprite (rounded rect) | `UiKit.Round` (generated texture) |
| Font | Unity built-in `LegacyRuntime.ttf` (Latin only — no Arabic glyphs) |
| Launcher icon | `tools/branding/make_icon.py` → `Assets/Plasma/Branding/` |
| Sound effects | `Sfx.cs` (synthesised at startup: shot, pop, gate hit/break, gain, hurt, boss hit/die, win, lose, click, coin) |

## Upgrade plan
1. **Characters:** keep procedural but add idle/walk squash, or import CC0 low-poly packs (e.g. Kenney, Quaternius — both CC0). Keep tris < 300 per unit (thousands are instanced).
2. **Arabic font:** a font with Arabic glyphs under OFL (e.g. Noto Sans Arabic / Cairo / Tajawal) + RTL shaping.
3. **Music:** CC0 loops (menu + battle, 90–120 s, seamless) or commissioned; add a music volume setting.
4. **SFX:** replace synthesised sounds with recorded CC0 ones only if they clearly sound better; keep `Sfx.Id` API.

## Licence log (append every imported asset)
| Date | Asset | Author / source URL | Licence | Where used |
|---|---|---|---|---|
| — | (none yet: everything is original) | — | — | — |

**Never** use art, characters, logos or sounds from the reference ad or other commercial games.
