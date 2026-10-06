# Roadmap

Goal: ship **Plasma** on Google Play as a polished, top-quality hyper-casual shooter, then grow it
with live updates. Each milestone ends with a tagged release (`vX.Y.Z`), an APK on GitHub Releases,
a capture video in `docs/media/`, and an updated `AGENTS.md` status.

Legend: ✅ done · 🟡 partly · ⬜ todo · 👤 needs the owner (accounts, money, device, decisions)

## M0 — Foundation ✅ (v0.1.0, 2026-10-06)
- ✅ Reproducible Unity 2022.3 + Android toolchain in a rootless, GPU-less sandbox (`tools/sandbox/`)
- ✅ Engine-free deterministic simulation + bot + Mono harness (balance in seconds)
- ✅ Infinite level generator (boss every 5, relief after), endless mode, upgrades, save
- ✅ Instanced renderer, procedural meshes, code-built UI, synthesised SFX, haptics, icon
- ✅ Android APK (debug-signed) + rendered gameplay capture from the real player

## M1 — "Exactly like the video" ✅ (v0.2.0, 2026-10-06)
- ✅ Mechanic rebuilt from a frame-by-frame analysis of the reference: dock gate upgrades the conveyor tiles, collect at the belt end, boss inside the horde, horde rush (no dead time)
- ✅ Look rebuilt: camera, palette, chibi soldiers, red carpet horde (2800, LOD), pillow gate, belt tiles, brute boss with blade, flame tracers, smoke, blob shadows, fog
- ✅ Juice: inflate/deflate, ring badge, belt upgrade wave, soldiers jump in, gold pops, slow-mo boss kill
- ✅ Arabic + English UI (Lalezar font + own Arabic shaper), settings (sound, music, vibration, language), contextual tutorial (L1–2), synthesised music loop
- ✅ Balance re-tuned with the new bot (100 levels, all skills)

## M1.2 — Real art & lighting ✅ (v0.3.0, 2026-10-06)
- ✅ CC0 3D characters (Kenney soldier/enemy, Quaternius boss) baked to instanced flipbooks with walk/idle animation
- ✅ Real-time shadows + HDR bloom (High), blob shadows (Low); Settings → Graphics; auto pick + fps watchdog

## M1.5 — "Feels great on a phone" (v0.4) — next
- ⬜👤 Device test of v0.3.0 on 2–3 phones (High and Low) (low/mid/high). Record FPS, heat, touch feel. Fill `docs/TESTING.md §4` checklist.
- ⬜ Difficulty: calibrate with real players — the bot wins everything first try (see `docs/LEVELS.md §4`)
- ⬜ Performance: frame budget overlay (debug), verify 60 fps with 2800 enemies on a mid phone, quality toggle ✅ v0.3 (extend Low: enemy cap / LOD distance if needed)
- ⬜ Juice: coin fly-to-counter, walk cycle (squash) for horde & boss, screen-edge red flash on damage, tile "clack" on the belt
- ⬜ Music: longer loops (menu, battle); volume sliders

## M2 — Content & depth (v0.5)
- ⬜ Enemy variety: runners (fast, low HP), shield bearers (block 1 column), bombers (explode on death → kill neighbours), flyers (skip the line)
- ⬜ Gate variety: **×2 multiplier gates** (rare, purple), **negative gates** (−10, red: avoid), **charge gates** (value grows while you shoot them — the grey "+0 → +1" behaviour in the reference), **weapon gates** (switch to shotgun/laser for 10 s)
- ⬜ Boss variety: 5 boss archetypes with one gimmick each (summoner, charger, shield phase, splitter, giant)
- ⬜ Biomes every 20 levels (colour palette + props: desert, snow, neon city, volcano, space)
- ⬜ Balance P1: remove casual wall at ~L80; target table in `docs/LEVELS.md §1`

## M3 — Retention & meta (v0.4)
- ⬜ Daily reward calendar, offline earnings, achievements
- ⬜ Squad skins (helmets/colours) bought with coins or gems; boss trophy collection
- ⬜ Endless leaderboard (Google Play Games) 👤
- ⬜ Cloud save (Play Games) 👤

## M4 — Monetisation & store (v1.0 launch) 👤
- ⬜👤 Accounts: Google Play Console ($25), AdMob or Unity Ads, privacy-policy URL
- ⬜ Rewarded ads: ×2 coins, +20 start soldiers, revive once per level; interstitial every 3 levels after L10 (never during play)
- ⬜ IAP: remove ads, starter pack, coin packs
- ⬜ Analytics (Firebase or Unity Analytics): level start/win/lose/attempts, session length, ad events → re-tune `docs/LEVELS.md`
- ⬜👤 Upload keystore + Play App Signing, signed AAB (`PlasmaBuild.BuildAndroidStore`)
- ⬜ Store listing AR + EN (`docs/STORE.md`), screenshots (`capture.sh`), 30 s trailer, feature graphic 1024×500
- ⬜ Closed testing (20 testers × 14 days is required for new personal developer accounts) → production

## M5 — Live ops (post-launch)
- ⬜ Weekly events (double coins weekend, boss rush), seasonal skins
- ⬜ A/B test difficulty curve and ad frequency using analytics
- ⬜ iOS port (same codebase) 👤 needs a Mac + Apple developer account

## Definition of done for every release
1. `tools/simharness/run.sh 100 0.3` respects the rules in `docs/LEVELS.md §1` (or the deviation is documented).
2. `tools/sandbox/unity.sh Setup` → 0 compile errors/warnings from `Assets/Plasma`.
3. APK built, `aapt` badging checked (id, version, SDKs, ABIs), SHA256 published.
4. Capture video reviewed frame-by-frame for visual regressions; saved to `docs/media/`.
5. `AGENTS.md` status + change log updated; release notes in AR + EN.
