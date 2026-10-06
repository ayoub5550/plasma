"""Plasma squad soldier - chibi trooper with a big round blue helmet (the helmet is the team-tint channel),
navy uniform and a stubby rifle. Original design (own work), low-poly for mass GPU instancing (~600 tris).

Run: blender -b --python tools/models/blender/soldier.py -- --out tools/models/src/plasma_soldier.glb [--preview p.png]
Bake: uv run --with numpy --with pillow python tools/models/bake_glb.py soldier
Seen mostly from BEHIND (squad faces +z, away from the camera): the helmet + back must read well.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import plasmakit as K

HELMET = (0.25, 0.45, 0.95)     # exact key: bake recolours this to white + tint 1 (Palette.Squad in game)
HELMET_RIM = (0.16, 0.30, 0.75)
SKIN = (0.98, 0.76, 0.60)
UNIFORM = (0.15, 0.20, 0.46)
UNIFORM_DK = (0.10, 0.13, 0.30)
BELT = (0.85, 0.85, 0.88)
BOOT = (0.08, 0.08, 0.10)
GUN = (0.12, 0.12, 0.14)
GUN_TIP = (1.0, 0.75, 0.2)
EYE = (0.06, 0.05, 0.05)

BONES = {
    "hips": ((0, 0, 0.30), (0, 0, 0.38), None),
    "chest": ((0, 0, 0.38), (0, 0, 0.56), "hips"),
    "head": ((0, 0, 0.56), (0, 0, 0.95), "chest"),
    "arm.L": ((0.15, 0, 0.52), (0.10, -0.16, 0.44), "chest"),
    "arm.R": ((-0.15, 0, 0.52), (-0.08, -0.17, 0.45), "chest"),
    "leg.L": ((0.075, 0, 0.30), (0.08, 0, 0.04), "hips"),
    "leg.R": ((-0.075, 0, 0.30), (-0.08, 0, 0.04), "hips"),
}


def build():
    # legs + boots
    for s, x in (("L", 0.075), ("R", -0.075)):
        K.cone("Leg" + s, UNIFORM_DK, (x, 0, 0.33), (x, 0, 0.06), 0.062, ["leg." + s, "hips"], r2=0.055, seg=7)
        K.ellipsoid("Boot" + s, BOOT, (x, -0.025, 0.045), (0.06, 0.085, 0.05), ["leg." + s], seg=6, rings=4)
    # torso + belt + backpack
    K.ellipsoid("Torso", UNIFORM, (0, 0, 0.44), (0.16, 0.12, 0.15), ["hips", "chest"], seg=9, rings=6)
    K.cone("Belt", BELT, (0, 0, 0.33), (0, 0, 0.37), 0.15, ["hips"], r2=0.155, seg=9)
    K.box("Pack", UNIFORM_DK, (0, 0.12, 0.47), (0.17, 0.07, 0.16), ["chest"])
    # arms reaching to the rifle
    for s in ("L", "R"):
        h, t, _ = BONES["arm." + s]
        K.cone("Arm" + s, UNIFORM, h, t, 0.05, ["arm." + s, "chest"], r2=0.042, seg=6)
        K.ellipsoid("Hand" + s, SKIN, t, (0.035, 0.035, 0.035), ["arm." + s], seg=6, rings=4)
    # head + big round helmet
    K.ellipsoid("Head", SKIN, (0, -0.01, 0.68), (0.145, 0.14, 0.14), ["head"], seg=10, rings=6)
    K.ellipsoid("Helmet", HELMET, (0, 0.02, 0.76), (0.172, 0.172, 0.14), ["head"], seg=12, rings=7)
    K.torus("Rim", HELMET_RIM, (0, 0.015, 0.715), 0.162, 0.02, ["head"], seg=12, mseg=4)
    for sx in (1, -1):
        K.ellipsoid("Eye", EYE, (sx * 0.05, -0.135, 0.655), (0.018, 0.012, 0.024), ["head"], seg=6, rings=4)


def build_gun():
    p = [K.box("Rifle", GUN, (0, -0.20, 0.45), (0.05, 0.30, 0.06), ["chest"]),
         K.box("Stock", GUN, (0, -0.06, 0.43), (0.045, 0.08, 0.09), ["chest"]),
         K.box("Muzzle", GUN_TIP, (0, -0.355, 0.455), (0.035, 0.02, 0.035), ["chest"])]
    return p


def idle_keys():
    # 4 frames / 1.33 s: breathing + small firing recoil (the squad shoots all the time)
    keys = {}
    for f, ph in ((0, 0), (8, 1), (16, 2), (24, 3), (32, 0)):
        rec = 1 if ph == 1 else 0
        keys[f] = {"hips": {"loc": (0, (-0.006, 0.0, 0.008, 0.0)[ph], 0)},
                   "chest": {"rot": (-5 * rec, 0, 0)},
                   "head": {"rot": (3 * rec, 0, 0)},
                   "arm.L": {"rot": (-6 * rec, 0, 0)}, "arm.R": {"rot": (-6 * rec, 0, 0)}}
    return keys


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = dict(zip(argv[::2], argv[1::2]))
    K.reset()
    arm = K.armature(BONES)
    K.attach(build_gun(), arm, "chest", "Rifle")
    build()
    K.finish_body(arm, name="Soldier")
    K.key_action(arm, "Idle", idle_keys())
    if "--blend" in args: bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(args["--blend"]))
    if "--out" in args: K.export_glb(os.path.abspath(args["--out"]))
    if "--preview" in args:
        p = args["--preview"]
        K.render_preview(p, frames=(0, 8), cam_loc=(0.9, -2.6, 1.5), target=(0, 0, 0.45))
        K.render_preview(p.replace(".png", "_top.png"), frames=(0,), cam_loc=(0.0, 2.6, 2.6), target=(0, 0, 0.4))   # from behind/above like the game
        K.render_preview(p.replace(".png", "_side.png"), frames=(0, 8, 16), cam_loc=(2.6, -0.1, 0.7), target=(0, 0, 0.45))


main()
