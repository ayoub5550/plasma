# Reference material

| File | What |
|---|---|
| `reference_ad.mp4` | The ad video the owner supplied as the target (720x1280, 60 fps, 54 s). Third-party promotional material - **reference only**, never ship any of it. |
| `compare_v0.4.0_numbers.png` | v0.3.1 vs v0.4.0 number fixes: stacked pops → one counter, tile label = soldiers received, belt pays from the first second + collect pad. |
| `compare_v0.3.1.png` | Side by side: the ad vs Plasma v0.3.1 (own Blender boss/soldier/enemy) + in-game close-ups of boss and squad. Gap: boss stands far, under the HUD bar. |
| `compare_v0.3.0.png` | Side by side: the ad vs Plasma v0.3.0 High (3D models, shadows, bloom) vs Low. |
| `compare_v0.2.1.png` | Side by side: the ad (left) vs Plasma v0.2.1 (right) at the same moment (jackpot gate). |
| `reference_contact_sheet.png` | One frame every 2 s of the ad (generated with ffmpeg) for quick viewing. |

## What the ad shows (frame-by-frame analysis, 2026-10-06)

1. **Deck** (light grey) at the bottom with the blue squad; dark navy void around it; two long bridges run to the horizon.
2. **Conveyor (left):** a belt of standing "+N" tiles slides toward the player. Standing next to its end collects each
   arriving tile (+N soldiers, yellow "+N" pops, soldiers jump into the squad).
3. **Dock gate:** a puffy "+N" gate inflates in a dark slot at the far edge of the deck. Shooting it down bursts it,
   a ring badge flies to the belt and **every tile on the belt is upgraded** (wave from the near end outward):
   grey +0 -> blue +1 -> green +5 -> yellow +99.
4. **Horde (right):** a dense red carpet marching down the right bridge; shots erode its front with white smoke.
5. **Boss:** a big brute with a giant sword walks inside the horde, HP bar + number above him, flashes white when hit.
6. **Shooting:** continuous straight orange-yellow tracer streams from the squad.

Plasma reproduces this **mechanic and layout**; all characters, art, audio and names are original (procedural).
