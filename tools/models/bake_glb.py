#!/usr/bin/env python3
"""Bake CC0 glTF characters into Plasma's compact vertex-animation format (.bytes).

Why: the horde draws hundreds of units with GPU instancing; skinned meshes can't be instanced
cheaply, so each animation is baked into N static pose meshes ("flipbook"). The texture atlas is
sampled into vertex colours so the existing `Plasma/Lit` shader (vertex colour + tint mask in
vertex alpha) renders them with no texture fetch.

Usage:  uv run --with numpy --with pillow python tools/models/bake_glb.py   (reads models.json)

Output format (little endian), loaded by Runtime/ModelLibrary.cs:
  "PLM1" | int frames | int verts | int indices | float clip_seconds | ushort[indices] | rgba8[verts] |
  frames x ( float3[verts] positions | sbyte3[verts] normals )
Coordinates are converted to Unity (x mirrored, winding flipped), feet at y=0, facing +z,
height normalised to 1.0 (scale in the game).
"""
import io, json, math, os, struct, sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))

CT = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
NC = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


class Gltf:
    def __init__(self, path):
        data = open(path, "rb").read()
        assert data[:4] == b"glTF"
        off, self.bin = 12, b""
        while off < len(data):
            ln, typ = struct.unpack("<II", data[off:off + 8])
            chunk = data[off + 8: off + 8 + ln]
            if typ == 0x4E4F534A: self.j = json.loads(chunk)
            else: self.bin = chunk
            off += 8 + ln
        self.dir = os.path.dirname(path)
        j = self.j
        self.parent = {}
        for i, n in enumerate(j["nodes"]):
            for c in n.get("children", []): self.parent[c] = i

    def view(self, bv_index):
        bv = self.j["bufferViews"][bv_index]
        o = bv.get("byteOffset", 0)
        return self.bin[o: o + bv["byteLength"]], bv.get("byteStride")

    def acc(self, i):
        a = self.j["accessors"][i]
        raw, stride = self.view(a["bufferView"])
        dt = np.dtype(CT[a["componentType"]]); nc = NC[a["type"]]; cnt = a["count"]
        off = a.get("byteOffset", 0)
        if stride and stride != dt.itemsize * nc:
            out = np.empty((cnt, nc), dt)
            for k in range(cnt):
                out[k] = np.frombuffer(raw, dt, nc, off + k * stride)
        else:
            out = np.frombuffer(raw, dt, cnt * nc, off).reshape(cnt, nc)
        out = out.astype(np.float64) if dt != np.float32 or True else out
        if a.get("normalized"):
            out = out / float(np.iinfo(dt).max)
        return out

    def image(self, mat_index):
        m = self.j["materials"][mat_index]
        pbr = m.get("pbrMetallicRoughness", {})
        factor = np.array(pbr.get("baseColorFactor", [1, 1, 1, 1]))
        if "baseColorTexture" not in pbr: return None, factor
        tex = self.j["textures"][pbr["baseColorTexture"]["index"]]
        img = self.j["images"][tex["source"]]
        if "bufferView" in img: im = Image.open(io.BytesIO(self.view(img["bufferView"])[0]))
        else: im = Image.open(os.path.join(self.dir, img["uri"]))
        return np.asarray(im.convert("RGBA"), np.float64) / 255.0, factor


def quat_mat(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def trs(t, r, s):
    m = np.eye(4)
    m[:3, :3] = quat_mat(r) * np.array(s)[None, :]
    m[:3, 3] = t
    return m


def slerp(a, b, k):
    d = np.dot(a, b)
    if d < 0: b, d = -b, -d
    if d > 0.9995: q = a + k * (b - a); return q / np.linalg.norm(q)
    th = math.acos(d)
    return (math.sin((1 - k) * th) * a + math.sin(k * th) * b) / math.sin(th)


def sample_pose(g, clip, t):
    """Local TRS for every node at time t of animation `clip` (None = rest pose)."""
    loc = []
    for n in g.j["nodes"]:
        if "matrix" in n:
            loc.append(("M", np.array(n["matrix"]).reshape(4, 4).T))
        else:
            loc.append(["T", np.array(n.get("translation", [0, 0, 0]), float), np.array(n.get("rotation", [0, 0, 0, 1]), float), np.array(n.get("scale", [1, 1, 1]), float)])
    if clip is not None:
        for ch in clip["channels"]:
            s = clip["samplers"][ch["sampler"]]
            times = g.acc(s["input"])[:, 0]
            vals = g.acc(s["output"])
            if s.get("interpolation") == "CUBICSPLINE": vals = vals[1::3]
            tt = min(max(t, times[0]), times[-1])
            k = int(np.searchsorted(times, tt, side="right") - 1); k = max(0, min(k, len(times) - 1))
            k2 = min(k + 1, len(times) - 1)
            f = 0 if k2 == k else (tt - times[k]) / (times[k2] - times[k])
            path = ch["target"]["path"]; node = ch["target"]["node"]
            if loc[node][0] == "M" or path == "weights": continue
            if s.get("interpolation") == "STEP": f = 0
            if path == "rotation": v = slerp(vals[k] / np.linalg.norm(vals[k]), vals[k2] / np.linalg.norm(vals[k2]), f)
            else: v = vals[k] * (1 - f) + vals[k2] * f
            loc[node][{"translation": 1, "rotation": 2, "scale": 3}[path]] = v
    mats = [l[1] if l[0] == "M" else trs(l[1], l[2], l[3]) for l in loc]
    glob = [None] * len(mats)

    def G(i):
        if glob[i] is None:
            glob[i] = mats[i] if i not in g.parent else G(g.parent[i]) @ mats[i]
        return glob[i]
    return [G(i) for i in range(len(mats))]


def color_for(g, prim, uv, n):
    tex, factor = g.image(prim["material"]) if "material" in prim else (None, np.ones(4))
    if tex is None or uv is None: return np.tile(factor, (n, 1))   # flat material colour (Blender-made models)
    h, w = tex.shape[:2]
    u = np.clip((uv[:, 0] % 1.0) * w, 0, w - 1).astype(int)
    v = np.clip((uv[:, 1] % 1.0) * h, 0, h - 1).astype(int)
    return tex[v, u] * factor[None, :]


def bake(cfg):
    g = Gltf(os.path.join(ROOT, cfg["src"]))
    clip = None
    if cfg.get("clip"):
        clip = next(a for a in g.j["animations"] if a.get("name") == cfg["clip"] or a.get("name", "").endswith("|" + cfg["clip"]))
    frames = cfg.get("frames", 1)
    dur = 0
    if clip:
        dur = max(g.acc(s["input"])[-1, 0] for s in clip["samplers"])
    skip = set(cfg.get("skip_meshes", []))
    mesh_scale = cfg.get("mesh_scale", {})

    # gather primitives (static data)
    prims = []
    for ni, n in enumerate(g.j["nodes"]):
        if "mesh" not in n: continue
        mesh = g.j["meshes"][n["mesh"]]
        if mesh.get("name") in skip or n.get("name") in skip: continue
        for p in mesh["primitives"]:
            a = p["attributes"]
            pos = g.acc(a["POSITION"])[:, :3]
            nor = g.acc(a["NORMAL"])[:, :3] if "NORMAL" in a else np.zeros_like(pos)
            uv = g.acc(a["TEXCOORD_0"]) if "TEXCOORD_0" in a else None
            idx = g.acc(p["indices"])[:, 0].astype(np.int64) if "indices" in p else np.arange(len(pos))
            col = color_for(g, p, uv, len(pos))
            sc = mesh_scale.get(mesh.get("name"), 1.0)
            prims.append(dict(node=ni, skin=n.get("skin"), pos=pos * sc, nor=nor, idx=idx, col=col,
                              joints=g.acc(a["JOINTS_0"]).astype(int) if "JOINTS_0" in a else None,
                              weights=g.acc(a["WEIGHTS_0"]) if "WEIGHTS_0" in a else None, name=mesh.get("name")))

    def pose_vertices(t):
        G = sample_pose(g, clip, t)
        P, N = [], []
        for p in prims:
            if p["skin"] is not None and p["joints"] is not None:
                skin = g.j["skins"][p["skin"]]
                ibm = g.acc(skin["inverseBindMatrices"]).reshape(-1, 4, 4).transpose(0, 2, 1)
                jm = np.stack([G[jn] @ ibm[k] for k, jn in enumerate(skin["joints"])])
                M = np.einsum("vk,vkij->vij", p["weights"], jm[p["joints"]])
            else:
                M = np.broadcast_to(G[p["node"]], (len(p["pos"]), 4, 4))
            hp = np.concatenate([p["pos"], np.ones((len(p["pos"]), 1))], 1)
            P.append(np.einsum("vij,vj->vi", M, hp)[:, :3])
            nn = np.einsum("vij,vj->vi", M[:, :3, :3], p["nor"])
            nn /= np.maximum(np.linalg.norm(nn, axis=1, keepdims=True), 1e-9)
            N.append(nn)
        return np.concatenate(P), np.concatenate(N)

    # indices / colours
    I, C, base = [], [], 0
    for p in prims:
        I.append(p["idx"] + base); C.append(p["col"]); base += len(p["pos"])
    I = np.concatenate(I).reshape(-1, 3)[:, [0, 2, 1]].reshape(-1)  # flip winding (x mirror)
    C = np.concatenate(C)
    C = recolor(C, cfg.get("recolor", []), cfg.get("tint", "none"))

    times = [cfg.get("t0", 0.0) + (dur * k / frames if clip else 0) for k in range(frames)]
    poses = [pose_vertices(t) for t in times]
    # normalise from frame 0: feet at 0, centred, height 1
    P0 = poses[0][0]
    body = np.concatenate([np.full(len(p["pos"]), p["skin"] is not None) for p in prims])
    if not body.any(): body[:] = True
    lo, hi = P0[body].min(0), P0[body].max(0)   # weapons don't count toward the height
    height = hi[1] - lo[1]
    cx, cz = (lo[0] + hi[0]) / 2 if cfg.get("center_x", True) else 0, cfg.get("center_z", (lo[2] + hi[2]) / 2)
    s = 1.0 / height
    out = io.BytesIO()
    out.write(b"PLM1"); out.write(struct.pack("<iiif", frames, len(C), len(I), float(dur)))
    assert len(C) < 65535, "too many vertices for 16-bit indices"
    out.write(I.astype("<u2").tobytes())
    out.write((np.clip(C, 0, 1) * 255 + 0.5).astype(np.uint8).tobytes())
    for P, N in poses:
        Q = (P - np.array([cx, lo[1], cz])) * s
        Q[:, 0] *= -1; N = N.copy(); N[:, 0] *= -1
        out.write(Q.astype("<f4").tobytes())
        out.write(np.clip(np.round(N * 127), -127, 127).astype(np.int8).tobytes())
    dst = os.path.join(ROOT, cfg["out"])
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    open(dst, "wb").write(out.getvalue())
    print(f"{cfg['out']}: {frames} frames / {dur:.2f}s, {len(C)} verts, {len(I)//3} tris, {len(out.getvalue())//1024} KB, raw height {height:.3f}")


def hsv(c):
    r, g, b = c[..., 0], c[..., 1], c[..., 2]
    mx, mn = np.max(c[..., :3], -1), np.min(c[..., :3], -1)
    d = mx - mn + 1e-9
    h = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60
    return h, np.where(mx > 0, (mx - mn) / (mx + 1e-9), 0), mx


def recolor(C, rules, tint):
    """rules: [{"near":[r,g,b], "tol":0.12, "to":[r,g,b], "tint":0..1}] matched by RGB distance.
    tint: "none" (alpha 0 = keep colours) | "all" (greyscale base, alpha 1 = fully team-coloured)."""
    C = C.copy()
    orig = C.copy()
    C[:, 3] = 0.0
    if tint == "all":
        lum = C[:, :3] @ np.array([0.3, 0.59, 0.11])
        g = 0.55 + 0.45 * np.clip(lum / max(lum.max(), 1e-6), 0, 1)
        C[:, 0] = C[:, 1] = C[:, 2] = g
        C[:, 3] = 1.0
    for r in rules:
        d = np.linalg.norm(orig[:, :3] - np.array(r["near"]), axis=1)
        m = d < r.get("tol", 0.12)
        if "to" in r: C[m, :3] = r["to"]
        if "tint" in r: C[m, 3] = r["tint"]
    return C


def palette(path):
    """Print the distinct vertex colours of a model (helps writing recolor rules)."""
    g = Gltf(path)
    for n in g.j["nodes"]:
        if "mesh" not in n: continue
        for p in g.j["meshes"][n["mesh"]]["primitives"]:
            uv = g.acc(p["attributes"]["TEXCOORD_0"]) if "TEXCOORD_0" in p["attributes"] else None
            c = (color_for(g, p, uv, g.j["accessors"][p["attributes"]["POSITION"]]["count"])[:, :3] * 255).round().astype(int)
            u, k = np.unique(c, axis=0, return_counts=True)
            print(g.j["meshes"][n["mesh"]].get("name"))
            for col, cnt in sorted(zip(u.tolist(), k.tolist()), key=lambda x: -x[1])[:24]:
                print("   ", [round(x / 255, 3) for x in col], cnt)


if __name__ == "__main__":
    if len(sys.argv) > 2 and sys.argv[1] == "palette":
        palette(sys.argv[2]); sys.exit()
    cfgs = json.load(open(os.path.join(HERE, "models.json")))
    only = set(sys.argv[1:])
    for c in cfgs:
        if not only or c["name"] in only: bake(c)
