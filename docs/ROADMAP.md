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

## M1 — "Feels great on a phone" (v0.2) — next
- ⬜👤 Device test of v0.1.0 on 2–3 phones (low/mid/high). Record FPS, heat, touch feel. Fill `docs/TESTING.md §4` checklist.
- ⬜ Performance: frame budget overlay (debug), LOD for far enemies (merge 4 → 1 instance beyond z 25), cap tracers, verify 60 fps with 2400 enemies on a mid phone
- ⬜ Juice: coin fly-to-counter, new soldiers jump in from the gate, slow-mo + flash on boss kill, blob shadows, tracer trails, gate "cloth" collapse mesh, screen-edge red flash on damage
- ⬜ Music: 2 loops (menu, battle) — CC0 or commissioned; volume sliders
- ⬜ Tutorial polish: animated hand hint on L1, "shoot the gate!" arrow until first gate broken
- ⬜ Settings panel: sound, music, vibration, language

## M2 — Content & depth (v0.3)
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
