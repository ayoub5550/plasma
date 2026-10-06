# Character models → baked flipbooks

**Since v0.3.1 all three characters are our own, made in Blender by script** (`blender/`), exported to `src/plasma_{boss,soldier,enemy}.glb`:

```bash
B=/work/blender/blender-4.2.3-linux-x64/blender      # https://download.blender.org/release/Blender4.2/blender-4.2.3-linux-x64.tar.xz (no root, CPU only)
$B -b --python tools/models/blender/boss.py -- --out tools/models/src/plasma_boss.glb --blend tools/models/blender/boss.blend --preview /tmp/boss.png
$B -b --python tools/models/blender/soldier.py -- --out tools/models/src/plasma_soldier.glb --blend tools/models/blender/soldier.blend
$B -b --python tools/models/blender/enemy.py -- --out tools/models/src/plasma_enemy.glb --blend tools/models/blender/enemy.blend
uv run --with numpy --with pillow python tools/models/bake_glb.py        # then rebuild the game
```

`--preview` renders Cycles CPU stills (front, side walk frames, top) in ~5 s — always look at them before baking.
Kit conventions (`blender/plasmakit.py`): Z up, character faces **-Y**, feet at z 0; flat material colours become vertex colours;
`K.ell/ball/capsule` take *visual* half-sizes (metaball compensation built in); every part lists the bones it may be weighted to
(smooth inverse-distance weights, no bone-heat); props (sword, rifle) are bone-parented, not skinned, so they do not count toward height;
`K.cut_faces` opens garments (the boss jacket V); `K.mark()/scale_parts()` = cartoon head scale. Keep enemy < 600 tris, soldier < 850.
Tint channels are set in `models.json` by exact source colours (soldier helmet `0.25,0.45,0.95`; enemy `tint: all` minus legs/eyes).

The old CC0 sources (Kenney / Quaternius) stay in `src/` as fallbacks/reference only.

`bake_glb.py` turns glTF characters into the compact format the game instances (`Assets/Plasma/Resources/Models/*.bytes`):
one static mesh per animation frame (skinning is done offline in numpy), texture atlas sampled into vertex colours,
vertex alpha = tint mask (1 = takes the team colour from `Palette`). No texture fetches, no skinned meshes at runtime.

```bash
uv run --with numpy --with pillow python tools/models/bake_glb.py            # bake everything in models.json
uv run --with numpy --with pillow python tools/models/bake_glb.py boss       # one model
uv run --with numpy --with pillow python tools/models/bake_glb.py palette tools/models/src/quaternius_matt.glb   # list colours for recolour rules
```

`models.json` fields: `src` (glb), `clip` (animation name, Quaternius names may be prefixed `CharacterArmature|`), `frames`,
`out`, `tint` (`none` = keep colours, `all` = greyscale + fully tinted), `recolor` (`near` RGB, `tol`, `to`, `tint`),
`mesh_scale` (per-mesh scale, e.g. the boss knife → blade), `skip_meshes`.
Height is normalised to 1 (weapons excluded), feet at y = 0, facing +z, Unity handedness. Scales live in `BattleView` (`*ModelH`).

Sources (all CC0, see `docs/ART_AUDIO.md` licence log): `src/kenney_mini_male_{a,c}.glb` + `src/Textures/colormap.png`
(Kenney Mini Characters), `src/quaternius_matt.glb`, `src/quaternius_shaun.glb` (spare boss candidate) from poly.pizza.
Budget (v0.3.1 own models): soldier 814 tris (≤ 36 drawn), enemy 588 tris (only rows nearer than `LodZ`/14, the rest use `MeshFactory.EnemyLod`), boss ~14.7 k tris / 8 k verts (one instance).
