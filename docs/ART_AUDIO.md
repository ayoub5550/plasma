# Art & audio direction

## Style
Bright, toy-like, readable at a glance on a small screen (reference: hyper-casual crowd shooters).
Matched to the reference ad (`docs/reference/`) in layout, palette and readability — with original designs.
* **Team colours:** squad blue caps (tinted `#246BFF`) on navy uniforms, horde red `#ED1A17`, brutes dark red, boss red jacket (tinted) + blond hair/beard + jeans + oversized dark blade.
* **Gate/tile tiers** (value → colour): 0 grey · 1–2 blue · 3–9 green · 10–24 cyan · 25–60 purple · 61–99 yellow (`Palette.Gate`, `LevelGenerator.TierOf`).
* **World:** light grey-lavender deck `#A1A3B3`, dark navy void `#0A1324`, darker belt, mid-grey rounded rails, dark dock pit.
* **Lighting:** half-lambert + rim + soft specular (glossy toy look) + distance fog to the void colour in `Plasma/Lit`. **High quality:** real-time directional shadows from behind-left (squad, boss, tiles, gate → deck/belt; tinted by `_ShadowTint`), HDR camera + `PlasmaBloom` (tracers/sparks glow). **Low quality:** blob shadows, no post. See `QualityManager.cs`.
* **FX:** tracers = alpha-blended flame (white-yellow head → orange tail) + faint additive glow; kills = white smoke; gate break = coloured pieces + ring badge.
* **Text:** Lalezar (chunky display font, Latin + Arabic). World labels have a thick dark outline + drop shadow (`WorldText`).
* **UI:** chunky rounded buttons with darker base (pressed = face moves down), outlined text, gold for money.

## Current assets (all original, generated in code)
| Asset | Source |
|---|---|
| **Soldier, enemy, boss (v0.3)** | CC0 models (see licence log) baked by `tools/models/bake_glb.py` (config `tools/models/models.json`) into flipbook meshes `Assets/Plasma/Resources/Models/*.bytes`, loaded by `ModelLibrary.cs`. Atlas → vertex colours, recolour rules + tint mask per model. Start `-plasmaNoModels` to see the procedural fallback. |
| Enemy far LOD, fallback characters, pillow gate, belt tile, flame, shadow disc, ring, pieces | `MeshFactory.cs` (procedural, vertex colours; alpha = tint mask; rounded-box generator) |
| Shaders | `Assets/Plasma/Resources/Shaders/*` (written for this project) |
| UI sprite (rounded rect) | `UiKit.Round` (generated texture) |
| Font | **Lalezar** (OFL 1.1) `Assets/Plasma/Resources/Fonts/Lalezar.ttf` — Latin + Arabic Presentation Forms-B; shaping by `ArabicText.cs` |
| Launcher icon | `tools/branding/make_icon.py` → `Assets/Plasma/Branding/` |
| Sound effects | `Sfx.cs` (synthesised at startup: shot, pop, gate hit/break, inflate, belt upgrade, tile, hurt, boss hit/die, win, lose, click, coin) |
| Music | `Sfx.MakeMusic` — 8 s 120 BPM loop (Am–F–C–G: bass, arpeggio, kick, snare, hats), synthesised |

## Upgrade plan
1. ~~Characters~~ — done in v0.3 (Kenney Mini Characters + Quaternius). Next: a lighter enemy (< 300 tris) so High can draw models further than z 21; a boss closer to the ad (muscular, shoulder blade) — buy or commission if no CC0 fits; hit/death flipbooks.
2. ~~Arabic font~~ — done in v0.2 (Lalezar + `ArabicText`).
3. **Music:** longer CC0/commissioned loops (menu + battle, 90–120 s) to replace the 8 s synth loop; volume slider.
4. **SFX:** replace synthesised sounds with recorded CC0 ones only if they clearly sound better; keep `Sfx.Id` API.

## Licence log (append every imported asset)
| Date | Asset | Author / source URL | Licence | Where used |
|---|---|---|---|---|
| 2026-10-06 | Mini Characters 1.0 (`character-male-c` → soldier, `character-male-a` → enemy) | Kenney, https://kenney.nl/assets/mini-characters (`tools/models/src/Kenney-License.txt`) | CC0 1.0 | squad + horde |
| 2026-10-06 | "Characters Matt" (+ knife scaled ×2.6 as the blade) | Quaternius (Ultimate Animated Character Pack), https://poly.pizza/m/66kQ4dBBC7 | CC0 1.0 | boss |
| 2026-10-06 | Lalezar font | Borna Izadpanah, https://github.com/BornaIz/Lalezar (via google/fonts) | SIL OFL 1.1 (`Lalezar-OFL.txt` next to the font) | all UI + world text |

**Never** use art, characters, logos or sounds from the reference ad or other commercial games.
