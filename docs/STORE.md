# Store & monetisation plan (Google Play)

## Identity
* Title: **Plasma: Squad vs Horde** (AR: **بلازما: الفرقة ضد الحشد**) — check name availability before launch 👤
* Package id: `com.ayoubteke.plasma` (permanent once published)
* Category: Action / Casual. Content rating: cartoon violence, no blood → expected PEGI 7 / Everyone 10+.

## Listing copy (draft)
**EN short:** Grow your squad, smash the horde, beat the boss — endless levels!
**EN long:** Slide your squad, they shoot by themselves. Blast the upgrade gate to turn every tile on the
conveyor into +1, +5 … +99, collect them to grow from 1 soldier to thousands, then turn your firepower on the
endless red horde and the giant boss marching inside it. Upgrade firepower, fire rate, starting squad and tile
bonus. Infinite levels + Endless mode. Arabic & English. Plays offline.

**AR قصير:** كبّر فرقتك، احطم الحشد، واهزم الزعيم — مستويات لا نهائية!
**AR طويل:** حرّك فرقتك وهي تطلق النار وحدها. فجّر بوابة الترقية لتتحول كل بطاقات الشريط إلى +1 و+5 حتى +99، واجمعها لتكبر من جندي واحد إلى الآلاف، ثم وجّه نيرانك نحو الحشد الأحمر والزعيم العملاق الذي يسير بداخله. طوّر قوة النار وسرعة الإطلاق وجنود البداية ومكافأة البطاقات. مستويات لا نهائية ووضع بلا نهاية. بالعربية والإنجليزية. تعمل بدون إنترنت.

## Required assets
| Asset | Spec | Status |
|---|---|---|
| App icon | 512×512 PNG | ✅ `docs/store/icon_store_512.png` (v0.1, replace with final art) |
| Feature graphic | 1024×500 | ⬜ |
| Phone screenshots | 2–8, 9:16 or 9:19.5, ≥ 1080 px tall | 🟡 capture pipeline exists; render at 1080×2340 for the store |
| Trailer | 30 s YouTube | ⬜ (`capture.sh` → edit) |
| Privacy policy URL | required (ads/analytics) | ⬜👤 |
| Data safety form | ads ID if ads are added | ⬜👤 |

## Monetisation (planned, M4)
* **Rewarded video** (player-initiated only): double level coins · start with +20 soldiers · revive once.
* **Interstitial:** max one every 3 levels after level 10, only on the result screen, never mid-battle; none for "remove ads" buyers.
* **IAP:** Remove ads (~$2.99), Starter pack, coin packs.
* Fairness rule: the game must stay beatable without paying (verified with the casual bot sweep).

## Release process
1. `PLASMA_VERSION=x.y.z PLASMA_VERSION_CODE=n` + keystore env → `tools/sandbox/unity.sh BuildAndroidStore Android` → `Builds/Plasma.aab`.
2. Upload to Internal testing → Closed testing (new personal accounts: 20 testers for 14 days) → Production.
3. Enable **Play App Signing** at first upload (protects against losing the upload key).
