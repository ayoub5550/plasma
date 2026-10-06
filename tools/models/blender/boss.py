"""Plasma boss - "the Brute Captain": muscular, red open jacket over a white shirt, blond spiky hair + big
beard, oversized cleaver-greatsword resting on his right shoulder. Original design (own work, CC0 in this
repo), modelled entirely by script so any agent can tweak proportions/colours and re-export.

Run (headless):
  /work/blender/blender-4.2.3-linux-x64/blender -b --python tools/models/blender/boss.py -- \
      --out tools/models/src/plasma_boss.glb [--preview /tmp/boss.png] [--blend tools/models/blender/boss.blend]
Then bake:  uv run --with numpy --with pillow python tools/models/bake_glb.py boss
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector
import plasmakit as K

# ---------------------------------------------------------------- palette
JACKET = (0.80, 0.11, 0.09)
JACKET_DK = (0.52, 0.06, 0.05)
SHIRT = (0.95, 0.94, 0.92)
SKIN = (0.96, 0.70, 0.52)
HAIR = (1.00, 0.76, 0.22)
BEARD = (0.96, 0.64, 0.16)
BROW = (0.70, 0.40, 0.10)
PANTS = (0.17, 0.18, 0.24)
BOOT = (0.26, 0.15, 0.09)
BELT = (0.20, 0.12, 0.07)
GOLD = (1.00, 0.78, 0.25)
EYE_W = (0.98, 0.98, 0.98)
EYE_D = (0.06, 0.05, 0.05)
STEEL = (0.17, 0.18, 0.22)
EDGE = (0.78, 0.80, 0.84)
GRIP = (0.30, 0.16, 0.08)

# ---------------------------------------------------------------- skeleton (Z up, faces -Y)
SHO_L, ELB_L, WRI_L, HND_L = (0.45, 0.0, 1.47), (0.58, 0.03, 1.13), (0.62, -0.03, 0.88), (0.63, -0.05, 0.76)
SHO_R, ELB_R, WRI_R, HND_R = (-0.45, 0.0, 1.47), (-0.70, -0.06, 1.22), (-0.60, -0.17, 1.52), (-0.57, -0.19, 1.63)
HIP_L, KNE_L, ANK_L, TOE_L = (0.17, 0.0, 0.86), (0.19, -0.03, 0.48), (0.20, 0.02, 0.13), (0.20, -0.16, 0.04)
mir = lambda p: (-p[0], p[1], p[2])
HIP_R, KNE_R, ANK_R, TOE_R = mir(HIP_L), mir(KNE_L), mir(ANK_L), mir(TOE_L)

BONES = {
    "hips": ((0, 0, 0.86), (0, 0, 1.0), None),
    "spine": ((0, 0, 1.0), (0, 0, 1.25), "hips"),
    "chest": ((0, 0, 1.25), (0, 0, 1.55), "spine"),
    "neck": ((0, 0.01, 1.55), (0, 0, 1.67), "chest"),
    "head": ((0, 0, 1.67), (0, 0, 1.97), "neck"),
    "upperarm.L": (SHO_L, ELB_L, "chest"), "forearm.L": (ELB_L, WRI_L, "upperarm.L"), "hand.L": (WRI_L, HND_L, "forearm.L"),
    "upperarm.R": (SHO_R, ELB_R, "chest"), "forearm.R": (ELB_R, WRI_R, "upperarm.R"), "hand.R": (WRI_R, HND_R, "forearm.R"),
    "thigh.L": (HIP_L, KNE_L, "hips"), "shin.L": (KNE_L, ANK_L, "thigh.L"), "foot.L": (ANK_L, TOE_L, "shin.L"),
    "thigh.R": (HIP_R, KNE_R, "hips"), "shin.R": (KNE_R, ANK_R, "thigh.R"), "foot.R": (ANK_R, TOE_R, "shin.R"),
}
TORSO = ["hips", "spine", "chest"]
HEAD_SCALE = 1.22
HEAD = ["head"]


def lerp(a, b, t): return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def build_body():
    # --- shirt body underneath, jacket on top with a V opening cut out of its front
    chest = [K.ell((0, 0.02, 1.33), 0.40, 0.25, 0.26), K.ball((0.17, -0.10, 1.37), 0.21), K.ball((-0.17, -0.10, 1.37), 0.21),
             K.ell((0, -0.03, 1.10), 0.33, 0.24, 0.22), K.ell((0, 0.0, 0.99), 0.29, 0.21, 0.12)]
    K.metaball_part("ShirtBody", SHIRT, [dict(e, radius=e["radius"] * 0.95) for e in chest], TORSO, resolution=0.05)
    jacket = K.metaball_part("Jacket", JACKET, [dict(e) for e in chest] + [
        K.capsule((-0.28, 0.04, 1.53), (0.28, 0.04, 1.53), 0.13),
        K.ball(SHO_L, 0.19), K.ball(SHO_R, 0.19),
        K.capsule(SHO_L, lerp(SHO_L, ELB_L, 0.95), 0.14),
        K.capsule(SHO_R, lerp(SHO_R, ELB_R, 0.95), 0.14),
    ], TORSO + ["upperarm.L", "upperarm.R"], resolution=0.042)
    K.cut_faces(jacket, lambda c: c.y < -0.12 and abs(c.x) < 0.07 + 0.22 * max(0.0, min(1.0, (c.z - 0.98) / 0.55)) and c.z > 0.97)
    # rolled cuffs at the elbows
    for s, (a, b) in (("L", (SHO_L, ELB_L)), ("R", (SHO_R, ELB_R))):
        d = (Vector(b) - Vector(a)).normalized()
        K.cone("Cuff" + s, JACKET_DK, Vector(b) - d * 0.07, Vector(b) + d * 0.03, 0.175, ["upperarm." + s, "forearm." + s], r2=0.165, seg=16)
    K.ellipsoid("Collar", SHIRT, (0, -0.06, 1.55), (0.15, 0.12, 0.05), ["chest", "neck"], seg=14, rings=6)
    # belt + buckle
    K.torus("Belt", BELT, (0, -0.01, 0.93), 0.27, 0.045, ["hips"], scale=(1.05, 0.78, 1), seg=22, mseg=6)
    K.box("Buckle", GOLD, (0, -0.30, 0.93), (0.10, 0.04, 0.08), ["hips"], bevel=0.008)
    # --- pants + boots
    K.metaball_part("Pants", PANTS, [
        K.ell((0, 0.0, 0.88), 0.27, 0.19, 0.12),
        K.capsule(HIP_L, KNE_L, 0.13), K.capsule(HIP_R, KNE_R, 0.13),
        K.capsule(KNE_L, lerp(KNE_L, ANK_L, 0.85), 0.105), K.capsule(KNE_R, lerp(KNE_R, ANK_R, 0.85), 0.105),
    ], ["hips", "thigh.L", "shin.L", "thigh.R", "shin.R"], resolution=0.05)
    for s, ank in (("L", ANK_L), ("R", ANK_R)):
        K.ellipsoid("Boot" + s, BOOT, (ank[0], -0.05, 0.075), (0.11, 0.18, 0.085), ["foot." + s, "shin." + s], seg=14, rings=8)
        K.cone("BootTop" + s, BOOT, (ank[0], 0.0, 0.08), (ank[0], 0.005, 0.26), 0.10, ["shin." + s, "foot." + s], r2=0.105, seg=14)
    # --- forearms + fists (skin)
    for s, (e, w, h) in (("L", (ELB_L, WRI_L, HND_L)), ("R", (ELB_R, WRI_R, HND_R))):
        K.metaball_part("Arm" + s, SKIN, [K.capsule(e, lerp(e, w, 0.55), 0.105), K.capsule(lerp(e, w, 0.55), w, 0.085),
                                          K.ell(h, 0.10, 0.09, 0.10)],
                        ["upperarm." + s, "forearm." + s, "hand." + s], resolution=0.04)
        d = (Vector(h) - Vector(w)).normalized()
        K.cone("Wrist" + s, BELT, Vector(w) - d * 0.05, Vector(w) + d * 0.02, 0.085, ["forearm." + s, "hand." + s], r2=0.085, seg=12)
    # --- head (built at natural size, then scaled up for the cartoon look)
    head0 = K.mark()
    K.metaball_part("Head", SKIN, [
        K.capsule((0, 0.03, 1.50), (0, 0.0, 1.70), 0.12),
        K.ell((0, 0.0, 1.80), 0.15, 0.155, 0.17),
        K.ell((0, -0.04, 1.71), 0.14, 0.13, 0.10),
        K.ell((0, -0.155, 1.765), 0.035, 0.04, 0.045, stiff=3),
        K.ell((0.15, 0.01, 1.78), 0.03, 0.045, 0.06), K.ell((-0.15, 0.01, 1.78), 0.03, 0.045, 0.06),
    ], ["neck", "head"], resolution=0.03)
    for sx in (1, -1):
        K.ellipsoid("EyeW", EYE_W, (sx * 0.062, -0.135, 1.815), (0.034, 0.02, 0.027), HEAD, seg=10, rings=6)
        K.ellipsoid("EyeD", EYE_D, (sx * 0.058, -0.152, 1.812), (0.014, 0.008, 0.017), HEAD, seg=8, rings=5)
        K.box("Brow", BROW, (sx * 0.066, -0.15, 1.858), (0.075, 0.03, 0.022), HEAD, rot=(0, sx * -16, sx * 8), bevel=0.006)
        K.ellipsoid("Stache", BEARD, (sx * 0.045, -0.165, 1.715), (0.055, 0.022, 0.022), HEAD, rot=(0, sx * 18, sx * 10), seg=10, rings=6)
    # beard: jaw mass + chin wedge
    K.metaball_part("Beard", BEARD, [
        K.ell((0, -0.05, 1.665), 0.155, 0.12, 0.095),
        K.capsule((0, -0.10, 1.64), (0, -0.12, 1.50), 0.075),
        K.ell((0.11, -0.02, 1.72), 0.04, 0.07, 0.07), K.ell((-0.11, -0.02, 1.72), 0.04, 0.07, 0.07),
    ], ["neck", "head"], resolution=0.03)
    # hair: cap + swept-back spikes
    K.ellipsoid("HairCap", HAIR, (0, 0.035, 1.87), (0.158, 0.165, 0.11), HEAD, seg=16, rings=8)
    spikes = [(-0.10, -0.08, 1.90, -0.20, 0.06, 2.07), (-0.05, -0.10, 1.93, -0.10, 0.02, 2.13),
              (0.0, -0.11, 1.94, 0.0, 0.0, 2.15), (0.05, -0.10, 1.93, 0.10, 0.02, 2.13),
              (0.10, -0.08, 1.90, 0.20, 0.06, 2.07), (-0.08, 0.02, 1.95, -0.14, 0.18, 2.08),
              (0.08, 0.02, 1.95, 0.14, 0.18, 2.08), (0.0, 0.04, 1.96, 0.0, 0.22, 2.10),
              (-0.12, 0.07, 1.90, -0.20, 0.22, 1.96), (0.12, 0.07, 1.90, 0.20, 0.22, 1.96),
              (0.0, 0.10, 1.88, 0.0, 0.26, 1.90)]
    for i, (x, y, z, tx, ty, tz) in enumerate(spikes):
        K.cone("Spike%d" % i, HAIR, (x, y, z), (tx, ty, tz), 0.065, HEAD, seg=7)
    K.scale_parts(head0, (0, -0.02, 1.62), HEAD_SCALE)


def build_sword():
    hand = Vector(HND_R) + Vector((0.0, -0.02, -0.02))
    d = Vector((0.30, 0.55, 0.78)).normalized()            # up, back, across the shoulder
    face = Vector((0.0, -1.0, 0.35)).normalized()           # wide face looks at the camera
    w = d.cross(face).normalized()
    parts = []
    parts.append(K.cone("Grip", GRIP, hand - d * 0.13, hand + d * 0.12, 0.033, ["hand.R"], r2=0.033, seg=10))
    parts.append(K.ellipsoid("Pommel", GOLD, tuple(hand - d * 0.16), (0.05, 0.05, 0.05), ["hand.R"], seg=10, rings=6))
    parts.append(K.prism("Guard", GOLD, [(-0.19, -0.03), (0.19, -0.03), (0.21, 0.03), (-0.21, 0.03)], 0.08,
                         hand + d * 0.15, w, d, ["hand.R"]))
    o = hand + d * 0.18
    blade = [(-0.13, 0.0), (0.13, 0.0), (0.16, 1.10), (0.02, 1.36), (-0.13, 1.18)]
    rim = [(-0.155, 0.0), (0.155, 0.0), (0.185, 1.11), (0.02, 1.40), (-0.155, 1.20)]
    parts.append(K.prism("Blade", STEEL, blade, 0.07, o, w, d, ["hand.R"]))
    parts.append(K.prism("Edge", EDGE, rim, 0.03, o, w, d, ["hand.R"]))
    parts.append(K.prism("Fuller", (0.12, 0.13, 0.16), [(-0.03, 0.12), (0.03, 0.12), (0.03, 0.95), (-0.03, 0.95)], 0.08, o, w, d, ["hand.R"]))
    return parts


def walk_keys():
    A = 24   # thigh swing (deg)
    def leg(s, ang, knee, foot=0): return {"thigh." + s: {"rot": (ang, 0, 0)}, "shin." + s: {"rot": (knee, 0, 0)}, "foot." + s: {"rot": (foot, 0, 0)}}
    keys = {}
    for f, ph in ((0, 0), (6, 1), (12, 2), (18, 3), (24, 0)):
        k = {}
        if ph == 0:   k.update(leg("L", A, -8)); k.update(leg("R", -A, -22, 10))
        elif ph == 1: k.update(leg("L", 0, -6)); k.update(leg("R", 6, -48))
        elif ph == 2: k.update(leg("L", -A, -22, 10)); k.update(leg("R", A, -8))
        else:         k.update(leg("L", 6, -48)); k.update(leg("R", 0, -6))
        sgn = 1 if ph in (0, 1) else -1
        bob = -0.035 if ph in (0, 2) else 0.025
        k["hips"] = {"loc": (0, bob, 0), "rot": (0, 4 * sgn, 2 * sgn)}
        k["spine"] = {"rot": (4, -3 * sgn, 0)}
        k["chest"] = {"rot": (2, -4 * sgn, -2 * sgn)}
        k["neck"] = {"rot": (0, 3 * sgn, 0)}
        k["head"] = {"rot": (-3 if ph in (0, 2) else 2, 0, 0)}
        swing = -A * 0.8 if ph == 0 else (A * 0.8 if ph == 2 else 0)
        k["upperarm.L"] = {"rot": (swing, 0, 0)}
        k["forearm.L"] = {"rot": (-14 + (swing * 0.3), 0, 0)}
        k["upperarm.R"] = {"rot": (2 * sgn, 0, 0)}
        keys[f] = k
    return keys


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = dict(zip(argv[::2], argv[1::2]))
    K.reset()
    arm = K.armature(BONES)
    sword = build_sword()
    K.attach(sword, arm, "hand.R", "Sword")
    build_body()
    K.finish_body(arm, name="Boss")
    K.key_action(arm, "Walk", walk_keys())
    if "--blend" in args: bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(args["--blend"]))
    if "--out" in args: K.export_glb(os.path.abspath(args["--out"]))
    if "--preview" in args:
        p = args["--preview"]
        K.render_preview(p, frames=(0, 6), cam_loc=(1.8, -5.6, 3.4), target=(0, 0, 1.15))
        K.render_preview(p.replace(".png", "_side.png"), frames=(0, 6, 12), cam_loc=(5.8, -0.2, 1.4), target=(0, 0, 1.0))
        K.render_preview(p.replace(".png", "_top.png"), frames=(0,), cam_loc=(0.0, -6.0, 7.5), target=(0, 0, 1.0), lens=60)


main()
