"""Plasma horde enemy - round-headed red grunt (body is the tint channel: Palette.Enemy / Palette.Brute),
dark stubby legs, angry eyes. Original design (own work), very low-poly for hundreds of instances (<400 tris).

Run: blender -b --python tools/models/blender/enemy.py -- --out tools/models/src/plasma_enemy.glb [--preview p.png]
Bake: uv run --with numpy --with pillow python tools/models/bake_glb.py enemy
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import plasmakit as K

BODY = (0.92, 0.92, 0.92)      # tinted (greyscale x team colour)
BODY_SH = (0.70, 0.70, 0.70)
LEGS = (0.22, 0.12, 0.10)      # kept (tint 0)
EYE = (0.05, 0.03, 0.03)
EYE_W = (1.0, 1.0, 1.0)

BONES = {
    "hips": ((0, 0, 0.20), (0, 0, 0.30), None),
    "body": ((0, 0, 0.30), (0, 0, 0.55), "hips"),
    "head": ((0, 0, 0.55), (0, 0, 0.95), "body"),
    "arm.L": ((0.17, 0, 0.42), (0.27, -0.02, 0.28), "body"),
    "arm.R": ((-0.17, 0, 0.42), (-0.27, -0.02, 0.28), "body"),
    "leg.L": ((0.08, 0, 0.22), (0.085, 0, 0.03), "hips"),
    "leg.R": ((-0.08, 0, 0.22), (-0.085, 0, 0.03), "hips"),
}


def build():
    for s, x in (("L", 0.08), ("R", -0.08)):
        K.cone("Leg" + s, LEGS, (x, 0, 0.26), (x, 0, 0.04), 0.05, ["leg." + s, "hips"], r2=0.045, seg=6)
        K.ellipsoid("Foot" + s, LEGS, (x, -0.02, 0.035), (0.05, 0.07, 0.04), ["leg." + s], seg=6, rings=4)
    K.ellipsoid("Body", BODY_SH, (0, 0, 0.36), (0.17, 0.14, 0.15), ["hips", "body"], seg=10, rings=6)
    for s in ("L", "R"):
        h, t, _ = BONES["arm." + s]
        K.cone("Arm" + s, BODY_SH, h, t, 0.045, ["arm." + s, "body"], r2=0.04, seg=6)
    K.ellipsoid("Head", BODY, (0, 0, 0.66), (0.24, 0.23, 0.22), ["head"], seg=12, rings=8)
    for sx in (1, -1):
        K.ellipsoid("EyeW", EYE_W, (sx * 0.075, -0.19, 0.68), (0.045, 0.03, 0.04), ["head"], seg=6, rings=4)
        K.ellipsoid("Eye", EYE, (sx * 0.07, -0.215, 0.675), (0.022, 0.015, 0.026), ["head"], seg=6, rings=4)
        K.box("Brow", EYE, (sx * 0.075, -0.205, 0.735), (0.07, 0.02, 0.018), ["head"], rot=(0, sx * -22, 0))


def walk_keys():
    keys = {}
    for f, ph in ((0, 0), (4, 1), (8, 2), (12, 3), (16, 0)):
        sw = (30, 0, -30, 0)[ph]
        keys[f] = {"leg.L": {"rot": (sw, 0, 0)}, "leg.R": {"rot": (-sw, 0, 0)},
                   "arm.L": {"rot": (-sw * 0.8, 0, 0)}, "arm.R": {"rot": (sw * 0.8, 0, 0)},
                   "hips": {"loc": (0, (-0.01, 0.02, -0.01, 0.02)[ph], 0), "rot": (0, 0, (5, 0, -5, 0)[ph])},
                   "head": {"rot": (0, (4, 0, -4, 0)[ph], 0)}}
    return keys


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = dict(zip(argv[::2], argv[1::2]))
    K.reset()
    arm = K.armature(BONES)
    build()
    K.finish_body(arm, name="Enemy")
    K.key_action(arm, "Walk", walk_keys())
    if "--blend" in args: bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(args["--blend"]))
    if "--out" in args: K.export_glb(os.path.abspath(args["--out"]))
    if "--preview" in args:
        p = args["--preview"]
        K.render_preview(p, frames=(0, 4), cam_loc=(0.9, -2.6, 1.5), target=(0, 0, 0.45))
        K.render_preview(p.replace(".png", "_top.png"), frames=(0,), cam_loc=(0.0, -2.2, 3.0), target=(0, 0, 0.4))
        K.render_preview(p.replace(".png", "_side.png"), frames=(0, 4, 8), cam_loc=(2.6, -0.1, 0.7), target=(0, 0, 0.45))


main()
