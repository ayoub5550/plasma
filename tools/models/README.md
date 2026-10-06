# Character models (CC0) → baked flipbooks

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
Budget: soldier 793 tris (≤ 36 drawn), enemy 723 tris (only rows nearer than `LodZ`/14, the rest use `MeshFactory.EnemyLod`), boss 7 k tris (one).
