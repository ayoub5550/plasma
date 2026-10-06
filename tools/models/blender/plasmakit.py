"""plasmakit - tiny helper library for building Plasma's stylised characters *by script* in Blender 4.2.

Every character script (boss.py, soldier.py, ...) does:
    K.reset()
    arm = K.armature({bone: (head, tail, parent)})
    K.metaball_part(name, color, elements, bones)   # organic blended shapes (torso, limbs)
    K.ellipsoid / K.cone / K.box / K.cylinder(...)  # small hard parts (eyes, spikes, belt...)
    body = K.finish_body(arm)                         # join, smooth weights, armature modifier
    K.key_action(arm, "Walk", fps, {frame: {bone: pose}})
    K.export_glb(path)  /  K.render_preview(path, ...)

Conventions: Blender units, Z up, character faces -Y (Blender "front"), feet at z=0. The glTF exporter
turns -Y into +Z, which is what tools/models/bake_glb.py expects. Colours are flat material base colours
(baked into vertex colours by bake_glb.py). Weights are computed from distance to the allowed bones of
each part (smooth falloff), so no bone-heat solver is needed (deterministic, works headless).
"""
import math
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler, Quaternion

PARTS = []        # (object, allowed bone names)
MATS = {}
BONES = {}        # name -> (head Vector, tail Vector, parent)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    PARTS.clear(); MATS.clear(); BONES.clear()


def mat(color, rough=0.6):
    key = tuple(round(c, 3) for c in color)
    if key in MATS: return MATS[key]
    m = bpy.data.materials.new("c_%02x%02x%02x" % tuple(int(c * 255) for c in color[:3]))
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*color[:3], 1)
    b.inputs["Roughness"].default_value = rough
    MATS[key] = m
    return m


def _link(obj):
    bpy.context.scene.collection.objects.link(obj)
    return obj


def _add_part(obj, color, bones):
    obj.data.materials.append(mat(color))
    for p in obj.data.polygons: p.use_smooth = True
    PARTS.append((obj, list(bones)))
    return obj


def _from_bmesh(name, bm):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return _link(bpy.data.objects.new(name, me))


def _orient(bm, loc, rot, scale):
    m = Matrix.Translation(Vector(loc)) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4() @ Matrix.Diagonal((*scale, 1))
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)


def ellipsoid(name, color, loc, size, bones, rot=(0, 0, 0), seg=16, rings=10):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0)
    _orient(bm, loc, rot, size)
    return _add_part(_from_bmesh(name, bm), color, bones)


def cone(name, color, base, tip, r1, bones, r2=0.0, seg=8):
    """Cone/frustum from point `base` (radius r1) to `tip` (radius r2)."""
    base, tip = Vector(base), Vector(tip)
    d = tip - base
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r1, radius2=max(r2, 0.0001), depth=d.length)
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    m = Matrix.Translation((base + tip) / 2) @ q.to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    if r2 <= 0.0001: bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
    return _add_part(_from_bmesh(name, bm), color, bones)


def box(name, color, loc, size, bones, rot=(0, 0, 0), bevel=0.0, smooth=False):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=2, affect='EDGES', profile=0.5)
    _orient(bm, loc, rot, size)
    o = _add_part(_from_bmesh(name, bm), color, bones)
    if not smooth:
        for p in o.data.polygons: p.use_smooth = False
    return o


def torus(name, color, loc, major, minor, bones, scale=(1, 1, 1), rot=(0, 0, 0), seg=20, mseg=6):
    bm = bmesh.new()
    verts = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        ring = []
        for j in range(mseg):
            b = 2 * math.pi * j / mseg
            r = major + minor * math.cos(b)
            ring.append(bm.verts.new((r * math.cos(a), r * math.sin(a), minor * math.sin(b))))
        verts.append(ring)
    for i in range(seg):
        for j in range(mseg):
            a, b = verts[i][j], verts[(i + 1) % seg][j]
            c, d = verts[(i + 1) % seg][(j + 1) % mseg], verts[i][(j + 1) % mseg]
            bm.faces.new((a, b, c, d))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    _orient(bm, loc, rot, scale)
    return _add_part(_from_bmesh(name, bm), color, bones)


def metaball_part(name, color, elements, bones, resolution=0.045, decimate=None, threshold=0.6):
    """elements: list of dicts {type: BALL|ELLIPSOID|CAPSULE, co, radius, size:(x,y,z), rot:(deg xyz), stiff}
    Each call is its own metaball family (unique name, no dot) -> converted to a mesh immediately."""
    mb = bpy.data.metaballs.new(name)
    mb.resolution = resolution; mb.render_resolution = resolution; mb.threshold = threshold
    for e in elements:
        el = mb.elements.new(type=e.get("type", "BALL"))
        el.co = e["co"]; el.radius = e.get("radius", 1.0); el.stiffness = e.get("stiff", 2.0)
        sx, sy, sz = e.get("size", (0.1, 0.1, 0.1))
        el.size_x, el.size_y, el.size_z = sx, sy, sz
        if "rot" in e: el.rotation = Euler([math.radians(a) for a in e["rot"]]).to_quaternion()
        if "dir" in e:  # capsule axis = local X; aim it along dir
            el.rotation = Vector((1, 0, 0)).rotation_difference(Vector(e["dir"]).normalized())
    src = _link(bpy.data.objects.new(name, mb))
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(src.evaluated_get(dg))
    bpy.data.objects.remove(src); bpy.data.metaballs.remove(mb)
    obj = _link(bpy.data.objects.new(name + "_m", me))
    if decimate:
        mod = obj.modifiers.new("dec", "DECIMATE"); mod.ratio = decimate
        dg = bpy.context.evaluated_depsgraph_get()
        me2 = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
        obj.modifiers.clear(); old = obj.data; obj.data = me2; bpy.data.meshes.remove(old)
    return _add_part(obj, color, bones)


# Metaball field at threshold 0.6 / stiffness 2: an isolated element's surface sits at 0.57 x its radius.
# The helpers below take *visual* sizes (half-extents) and compensate, so numbers read like ellipsoid sizes.
MB_K = 1.0 / 0.57


def capsule(a, b, r, stiff=2.0):
    """Metaball capsule element between points a and b with radius r."""
    a, b = Vector(a), Vector(b)
    return dict(type="CAPSULE", co=tuple((a + b) / 2), radius=r * MB_K, size=((b - a).length / 2, 0.1, 0.1), dir=tuple(b - a), stiff=stiff)


def ball(c, r, stiff=2.0):
    return dict(type="BALL", co=tuple(c), radius=r * MB_K, stiff=stiff)


def ell(c, sx, sy, sz, rot=(0, 0, 0), stiff=2.0):
    return dict(type="ELLIPSOID", co=tuple(c), radius=MB_K, size=(sx, sy, sz), rot=rot, stiff=stiff)


# ---------------------------------------------------------------- rig
def armature(bones):
    """bones: {name: (head, tail, parent or None)} in world coords (armature at origin)."""
    BONES.update({k: (Vector(v[0]), Vector(v[1]), v[2]) for k, v in bones.items()})
    ad = bpy.data.armatures.new("Rig")
    arm = _link(bpy.data.objects.new("Rig", ad))
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for name, (h, t, par) in BONES.items():
        eb = ad.edit_bones.new(name); eb.head = h; eb.tail = t; eb.roll = 0
    for name, (h, t, par) in BONES.items():
        if par: ad.edit_bones[name].parent = ad.edit_bones[par]; ad.edit_bones[name].use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def _seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (p - (a + ab * t)).length


def finish_body(arm, power=4.0, max_inf=3, name="Body"):
    """Smooth distance weights per part (restricted to its allowed bones), join, add Armature modifier."""
    for obj, allowed in PARTS:
        groups = {b: obj.vertex_groups.new(name=b) for b in allowed}
        for v in obj.data.vertices:
            p = obj.matrix_world @ v.co
            ws = []
            for b in allowed:
                h, t, _ = BONES[b]
                ws.append((1.0 / (max(_seg_dist(p, h, t), 0.01) ** power), b))
            ws.sort(reverse=True); ws = ws[:max_inf]
            s = sum(w for w, _ in ws)
            for w, b in ws:
                if w / s > 0.02: groups[b].add([v.index], w / s, "REPLACE")
    objs = [o for o, _ in PARTS]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    body = bpy.context.view_layer.objects.active
    body.name = name; body.data.name = name
    body.parent = arm
    mod = body.modifiers.new("Armature", "ARMATURE"); mod.object = arm
    PARTS.clear()
    print(f"[plasmakit] {name}: {len(body.data.vertices)} verts, {sum(len(p.vertices) - 2 for p in body.data.polygons)} tris")
    return body


def attach(obj_list, arm, bone, name):
    """Join rigid props (sword...) into one object parented to a bone (not skinned -> excluded from height)."""
    names = {o.name for o in obj_list}
    PARTS[:] = [(o, b) for o, b in PARTS if o.name not in names]
    bpy.ops.object.select_all(action="DESELECT")
    for o in obj_list: o.select_set(True)
    bpy.context.view_layer.objects.active = obj_list[0]
    if len(obj_list) > 1: bpy.ops.object.join()
    prop = bpy.context.view_layer.objects.active; prop.name = name; prop.data.name = name
    mw = prop.matrix_world.copy()
    prop.parent = arm; prop.parent_type = "BONE"; prop.parent_bone = bone
    bpy.context.view_layer.update()
    prop.matrix_world = mw
    return prop


def key_action(arm, name, keys, fps=24):
    """keys: {frame: {bone: {"rot":(x,y,z) deg local euler, "loc":(x,y,z) local}}}; interpolation smooth, loops if
    the last frame repeats the first."""
    s = bpy.context.scene; s.render.fps = fps
    arm.animation_data_create()
    act = bpy.data.actions.new(name); arm.animation_data.action = act
    for pb in arm.pose.bones: pb.rotation_mode = "XYZ"
    for f in sorted(keys):
        for bname, pose in keys[f].items():
            pb = arm.pose.bones[bname]
            pb.rotation_euler = [math.radians(a) for a in pose.get("rot", (0, 0, 0))]
            pb.location = pose.get("loc", (0, 0, 0))
            pb.keyframe_insert("rotation_euler", frame=f)
            pb.keyframe_insert("location", frame=f)
    s.frame_start, s.frame_end = min(keys), max(keys)
    return act


def export_glb(path):
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=False, export_animations=True,
                              export_animation_mode="ACTIONS", export_skins=True, export_apply=False,
                              export_yup=True, export_texcoords=False, export_normals=True, export_materials="EXPORT",
                              export_cameras=False, export_lights=False)
    print("[plasmakit] exported", path)


def render_preview(path, frames=(0,), cam_loc=(2.2, -5.5, 3.2), target=(0, 0, 1.1), lens=50, res=(560, 700)):
    """Quick Cycles CPU preview(s): writes path with _f<frame>.png per frame."""
    s = bpy.context.scene
    s.render.engine = "CYCLES"; s.cycles.device = "CPU"; s.cycles.samples = 24; s.cycles.use_denoising = False
    s.render.resolution_x, s.render.resolution_y = res
    s.render.film_transparent = False
    s.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("W"); s.world = w; w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.58, 0.66, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.9
    if "PreviewCam" not in bpy.data.objects:
        cam = _link(bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam")))
        sun = _link(bpy.data.objects.new("PreviewSun", bpy.data.lights.new("PreviewSun", "SUN")))
        sun.data.energy = 3.5; sun.rotation_euler = (math.radians(40), math.radians(15), math.radians(30))
        bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=6)
        fl = _from_bmesh("PreviewFloor", bm); fl.data.materials.append(mat((0.72, 0.72, 0.76)))
    cam = bpy.data.objects["PreviewCam"]; cam.data.lens = lens
    cam.location = cam_loc
    cam.rotation_euler = (Vector(target) - Vector(cam_loc)).to_track_quat("-Z", "Y").to_euler()
    s.camera = cam
    out = []
    for f in frames:
        s.frame_set(f)
        s.render.filepath = path.replace(".png", f"_f{f:02d}.png")
        bpy.ops.render.render(write_still=True); out.append(s.render.filepath)
    return out


def remove_preview():
    for n in ("PreviewCam", "PreviewSun", "PreviewFloor"):
        if n in bpy.data.objects: bpy.data.objects.remove(bpy.data.objects[n])


def prism(name, color, outline, thickness, origin, u, v, bones, smooth=False):
    """Extrude a 2D outline [(a,b),...] (a along axis u, b along axis v) by `thickness` along u x v,
    centred on the plane through `origin`. Good for blades, lapels, badges."""
    u, v = Vector(u).normalized(), Vector(v).normalized()
    n = u.cross(v).normalized(); o = Vector(origin)
    bm = bmesh.new()
    front = [bm.verts.new(o + u * a + v * b + n * thickness / 2) for a, b in outline]
    back = [bm.verts.new(o + u * a + v * b - n * thickness / 2) for a, b in outline]
    bm.faces.new(front); bm.faces.new(list(reversed(back)))
    k = len(outline)
    for i in range(k):
        j = (i + 1) % k
        bm.faces.new((front[j], front[i], back[i], back[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    o2 = _add_part(_from_bmesh(name, bm), color, bones)
    if not smooth:
        for p in o2.data.polygons: p.use_smooth = False
    return o2


def mark():
    """Index into the part list; pass to scale_parts() to transform everything created after it."""
    return len(PARTS)


def scale_parts(since, pivot, factor):
    """Uniformly scale (mesh data of) parts created after mark() around pivot - e.g. a bigger cartoon head."""
    m = Matrix.Translation(Vector(pivot)) @ Matrix.Scale(factor, 4) @ Matrix.Translation(-Vector(pivot))
    for obj, _ in PARTS[since:]:
        obj.data.transform(obj.matrix_world.inverted() @ m @ obj.matrix_world)


def cut_faces(obj, keep_out):
    """Delete faces whose centre (world) satisfies keep_out(Vector) -> opens a garment (e.g. an open jacket
    over a shirt body built underneath)."""
    bm = bmesh.new(); bm.from_mesh(obj.data)
    dead = [f for f in bm.faces if keep_out(obj.matrix_world @ f.calc_center_median())]
    bmesh.ops.delete(bm, geom=dead, context="FACES")
    bm.to_mesh(obj.data); bm.free()
    return obj
