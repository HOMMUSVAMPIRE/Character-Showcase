"""
The showcase's flora: trees, bushes, ferns, vines and ivy as textured leaf cards with full LOD chains, for Unity.
Generate Flora (FloraGenerator, Assets/Showcase/Editor/Pipeline/Flora) runs it in Blender and makes the prefabs.

Started from another generator's flora_gen_cards.py (ReferenceAssets/Assets/FloraAssets, 2026-11-24:
ReferenceAssets/OpenTasks.md, category P) and made ours: it writes where it's told, prints its progress as the
project's generators do ("[flora] ..." lines, "[flora] exported" once it's done), and renders no previews and saves no
.blend library unless asked.

Run (Generate Flora does this):
  Blender -b --factory-startup -P Tools/Flora/generate_flora.py -- <output folder> [options]

Options:
  --seed N         base random seed (default 1): a change makes a fresh batch
  --only a,b       only build these categories: trees,bushes,vines,ivy
  --tex N          leaf-card texture size (default 1024)
  --render         render the preview pictures (into <output>/../Previews), off by default
  --seeds k=a,b;.. each kind's variants' seeds (see SEEDS): a variant per seed, Name_01 the first seed's
  --kinds a,b      only build these kinds (oak, pine, birch, dead, bush, flowering, fern, vine, ivywall, ivypillar)

What it makes, in the output folder:
  Textures/  leaf-card atlases (rendered from real 3D leaf geometry), tiling bark, and one billboard texture per asset:
             BaseColor (with alpha) and Normal.
  Trees/ Bushes/ Vines/ Ivy/   FBX files, each holding Name_LOD0 .. Name_LODn. Vertex colour R is how much a part sways
             in the wind, G how much it flutters, B a phase per card (the flora's shaders read them).
  Materials are named M_Card_<kind> (leaf cards), M_Bark_<kind> (bark), M_Flat_BushCore (a bush's dense middle) and
  M_Imp_<asset> (the last LOD's billboard).
"""
import bpy, math, random, sys, os, argparse, shutil
import numpy as np
from mathutils import Vector, Quaternion, noise

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("out", help="the output folder (BlenderRunner passes it first)")
ap.add_argument("--seed", type=int, default=1)
ap.add_argument("--only", default="trees,bushes,vines,ivy")
ap.add_argument("--tex", type=int, default=1024)
ap.add_argument("--render", action="store_true")
ap.add_argument("--seeds", default="", help='each kind\'s variants\' seeds: "oak=0,1,2;birch=4,9" (see SEEDS)')
ap.add_argument("--kinds", default="", help="only build these kinds: oak,birch (default: every kind)")
ARGS = ap.parse_args(argv)
ARGS.no_render = not ARGS.render

# Each kind's variants, by their seeds: a variant per seed, in slots (Tree_Oak_01 is the first seed's), so a slot given a
# new seed is made again in place (Generate Flora's Flora Seeds asset passes them). These defaults are the first ones made.
# A flowering bush's seeds go to its colours in order (pink, white, yellow, purple); ivy walls' to their three shapes.
SEEDS = {"oak": [0, 1, 2], "pine": [0, 1, 2], "birch": [0, 1], "dead": [0, 1], "bush": [0, 1, 2], "flowering": [0, 1, 2, 3],
         "fern": [0, 1], "vine": [0, 1, 2], "ivywall": [0, 1, 2], "ivypillar": [0]}
for item in filter(None, (i.strip() for i in ARGS.seeds.split(";"))):
    kind, _, values = item.partition("=")
    SEEDS[kind.strip()] = [int(v) for v in values.split(",") if v.strip()]
KINDS = set(k.strip() for k in ARGS.kinds.split(",") if k.strip()) or set(SEEDS)
CATS = set(ARGS.only.split(","))
OUT = os.path.abspath(ARGS.out)
TEXDIR = os.path.join(OUT, "Textures")
TMP = os.path.join(OUT, "_tmp")
UP = Vector((0, 0, 1))
TAU = math.tau


def rvec(rng, s=1.0):
    return Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))) * s


def lerp(a, b, t):
    return a + (b - a) * t


def any_perp(d):
    p = d.cross(UP)
    if p.length < 1e-4:
        p = d.cross(Vector((1, 0, 0)))
    return p.normalized()


def orth(n, axis, rng=None):
    n = n - axis * n.dot(axis)
    if n.length < 1e-4:
        n = any_perp(axis)
    return n.normalized()


# =============================================================== mesh builder
def phase_of(p):
    """Stable pseudo-random 0..1 from a position (per-branch / per-card wind phase)."""
    return abs(math.sin(p.x * 12.9898 + p.y * 78.233 + p.z * 37.719) * 43758.5453) % 1.0


class MB:
    def __init__(self):
        self.v, self.f, self.m, self.s, self.uv, self.mats = [], [], [], [], [], []
        self.vc = []           # per-vertex (flutter weight, phase); sway weight (R) is filled in later
        self.cur = (0.0, 0.0)

    def vert(self, co):
        self.v.append((co[0], co[1], co[2]))
        self.vc.append(self.cur)
        return len(self.v) - 1

    def face(self, idx, material, smooth=False, uvs=None):
        if material not in self.mats:
            self.mats.append(material)
        self.f.append(idx)
        self.m.append(self.mats.index(material))
        self.s.append(smooth)
        self.uv.append(uvs or [(0.0, 0.0)] * len(idx))

    def face_ds(self, cos, material, uvs=None):
        uvs = uvs or [(0.0, 0.0)] * len(cos)
        self.face([self.vert(c) for c in cos], material, False, uvs)
        self.face([self.vert(c) for c in reversed(cos)], material, False, list(reversed(uvs)))

    def build(self, name):
        me = bpy.data.meshes.new(name)
        me.from_pydata(self.v, [], self.f)
        for m in self.mats:
            me.materials.append(m)
        me.polygons.foreach_set("material_index", self.m)
        me.polygons.foreach_set("use_smooth", self.s)
        uvl = me.uv_layers.new(name="UVMap")
        flat = [c for face in self.uv for uv in face for c in uv]
        uvl.data.foreach_set("uv", flat)
        # Wind data for the Flora Shader Graphs: R = sway weight, G = flutter weight, B = phase
        col = me.color_attributes.new("Col", "FLOAT_COLOR", "POINT")
        col.data.foreach_set("color", [c for g, ph in self.vc for c in (0.0, g, ph, 1.0)])
        me.update()
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        return ob


# =============================================================== primitives
def tube(mb, pts, radii, sides, material, cap=True):
    n = len(pts)
    if n < 2:
        return
    mb.cur = (0.0, phase_of(pts[0]))
    C = TAU * max(radii[0], 1e-3)
    rep = max(1, round(C / 0.5))
    tile = C / rep
    t0 = (pts[1] - pts[0]).normalized()
    nrm = any_perp(t0)
    prev_t = t0
    rings, vs, s = [], [], 0.0
    for i in range(n):
        if i:
            s += (pts[i] - pts[i - 1]).length
        t = t0 if i == 0 else ((pts[i] - pts[i - 1]).normalized() if i == n - 1 else (pts[i + 1] - pts[i - 1]).normalized())
        ax = prev_t.cross(t)
        if ax.length > 1e-6:
            nrm = Quaternion(ax.normalized(), prev_t.angle(t)) @ nrm
        prev_t = t
        bi = t.cross(nrm).normalized()
        nrm = bi.cross(t).normalized()
        rings.append([mb.vert(pts[i] + (nrm * math.cos(TAU * k / sides) + bi * math.sin(TAU * k / sides)) * radii[i])
                      for k in range(sides + 1)])
        vs.append(s / tile)
    for i in range(n - 1):
        for k in range(sides):
            u0, u1 = k / sides * rep, (k + 1) / sides * rep
            mb.face([rings[i][k], rings[i][k + 1], rings[i + 1][k + 1], rings[i + 1][k]], material, True,
                    [(u0, vs[i]), (u1, vs[i]), (u1, vs[i + 1]), (u0, vs[i + 1])])
    if cap:
        tip = mb.vert(pts[-1] + prev_t * radii[-1])
        for k in range(sides):
            u0, u1 = k / sides * rep, (k + 1) / sides * rep
            mb.face([rings[-1][k], rings[-1][k + 1], tip], material, True,
                    [(u0, vs[-1]), (u1, vs[-1]), ((u0 + u1) / 2, vs[-1] + 0.1)])


def card(mb, origin, u_ax, v_ax, w, h, material, uv=(0, 0, 1, 1), segs=1, bend=0.0, double=True, flutter=1.0):
    """Quad (optionally bent along v) spanning origin + u*w, + v*h. Front normal = u x v."""
    mb.cur = (flutter, phase_of(origin))
    n = u_ax.cross(v_ax).normalized()
    rows = []
    for j in range(segs + 1):
        t = j / segs
        left = origin + v_ax * h * t + n * bend * h * t * t
        rows.append((left, left + u_ax * w, lerp(uv[1], uv[3], t)))
    for j in range(segs):
        (l0, r0, v0), (l1, r1, v1) = rows[j], rows[j + 1]
        cos = [l0, r0, r1, l1]
        uvs = [(uv[0], v0), (uv[2], v0), (uv[2], v1), (uv[0], v1)]
        if double:
            mb.face_ds(cos, material, uvs)
        else:
            mb.face([mb.vert(c) for c in cos], material, False, uvs)


_ICO = {}


def ico(sub):
    if sub not in _ICO:
        import bmesh
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=1.0)
        _ICO[sub] = ([v.co.copy() for v in bm.verts], [[v.index for v in f.verts] for f in bm.faces])
        bm.free()
    return _ICO[sub]


def blob(mb, center, radius, seed, material, sub=2, squash=(1, 1, 0.85)):
    verts, faces = ico(sub)
    mb.cur = (0.15, phase_of(center))
    base = len(mb.v)
    for v in verts:
        d = v.normalized()
        p = Vector((d.x * squash[0], d.y * squash[1], d.z * squash[2])) * radius * (1 + noise.noise(d * 1.7 + seed) * 0.25)
        mb.vert(center + p)
    for f in faces:
        mb.face([base + i for i in f], material)


SHAPES = {
    "oval": [(0, 0), (0.28, 0.2), (0.32, 0.55), (0.18, 0.85), (0, 1), (-0.18, 0.85), (-0.32, 0.55), (-0.28, 0.2)],
    "birch": [(0, 0), (0.3, 0.15), (0.38, 0.4), (0.28, 0.65), (0.12, 0.85), (0, 1), (-0.12, 0.85), (-0.28, 0.65), (-0.38, 0.4), (-0.3, 0.15)],
    "oak": [(0, 0), (0.15, 0.1), (0.3, 0.18), (0.2, 0.3), (0.34, 0.42), (0.22, 0.52), (0.32, 0.65), (0.18, 0.78),
            (0.2, 0.9), (0, 1), (-0.2, 0.9), (-0.18, 0.78), (-0.32, 0.65), (-0.22, 0.52), (-0.34, 0.42), (-0.2, 0.3),
            (-0.3, 0.18), (-0.15, 0.1)],
    "ivy": [(0, 0), (0.42, 0.08), (0.55, 0.42), (0.28, 0.5), (0, 1), (-0.28, 0.5), (-0.55, 0.42), (-0.42, 0.08)],
    "needle": [(0, 0), (0.05, 0.3), (0.03, 0.8), (0, 1), (-0.03, 0.8), (-0.05, 0.3)],
    "pinna": [(0, 0)] + [(x * 0.6, y) for x, y in [(0.06, 0.05), (0.1, 0.15), (0.06, 0.2), (0.1, 0.3), (0.06, 0.36), (0.09, 0.46),
                                              (0.05, 0.52), (0.08, 0.62), (0.04, 0.68), (0.05, 0.78), (0.025, 0.86)]]
             + [(0, 1)] + [(-x * 0.6, y) for x, y in reversed([(0.06, 0.05), (0.1, 0.15), (0.06, 0.2), (0.1, 0.3), (0.06, 0.36),
                                                        (0.09, 0.46), (0.05, 0.52), (0.08, 0.62), (0.04, 0.68), (0.05, 0.78), (0.025, 0.86)])],
    "petal": [(0, 0), (0.32, 0.3), (0.3, 0.75), (0, 1), (-0.3, 0.75), (-0.32, 0.3)],
    "disc": [(math.cos(a) * 0.5, 0.5 + math.sin(a) * 0.5) for a in [TAU * i / 8 - math.pi / 2 for i in range(8)]],
}


def leaf(mb, base, axis, normal, size, material, shape="oval", fold=0.12):
    axis = axis.normalized()
    normal = orth(normal, axis)
    side = axis.cross(normal)
    pts = SHAPES[shape]
    outline = [base + (side * x + axis * y) * size for x, y in pts]
    cy = sum(y for _, y in pts) / len(pts)
    center = base + axis * cy * size + normal * fold * size
    for i in range(len(outline)):
        mb.face([mb.vert(center), mb.vert(outline[i]), mb.vert(outline[(i + 1) % len(outline)])], material)


def grow_line(rng, start, d, length, segs, wobble, tropism, flat=False):
    pts = [start.copy()]
    p = start.copy()
    d = d.normalized()
    for _ in range(segs):
        w = rvec(rng, wobble)
        if flat:
            w.z = 0
        d = (d + w + tropism).normalized()
        p = p + d * (length / segs)
        pts.append(p.copy())
    return pts


# =============================================================== materials
LIN = {  # linear colours for leaf geometry used when baking card textures
    "Leaf_Green": (0.10, 0.30, 0.05), "Leaf_GreenLight": (0.22, 0.42, 0.07), "Leaf_GreenDark": (0.05, 0.19, 0.04),
    "Leaf_Olive": (0.20, 0.30, 0.04), "Leaf_Birch": (0.30, 0.45, 0.07), "Leaf_BirchLight": (0.45, 0.55, 0.10),
    "Needle": (0.03, 0.14, 0.05), "NeedleLight": (0.07, 0.22, 0.07), "NeedleTip": (0.18, 0.32, 0.08),
    "Grass": (0.18, 0.40, 0.06), "GrassLight": (0.32, 0.52, 0.10), "GrassDry": (0.45, 0.42, 0.14),
    "Fern": (0.10, 0.32, 0.06), "FernLight": (0.18, 0.42, 0.08),
    "Ivy": (0.04, 0.17, 0.03), "IvyLight": (0.10, 0.30, 0.05), "IvyDark": (0.02, 0.10, 0.02),
    "Stem": (0.12, 0.08, 0.04), "StemGreen": (0.10, 0.14, 0.04),
    "Pink": (0.90, 0.30, 0.45), "White": (0.90, 0.90, 0.85), "Yellow": (0.95, 0.70, 0.10),
    "Purple": (0.45, 0.20, 0.80), "Center": (0.95, 0.55, 0.05),
}
MAT_INFO = {}  # material name -> dict(base=Image|None, normal=Image|None, cutout=bool, color=(r,g,b))


def src_mat(key):
    name = "SRC_" + key
    if name not in bpy.data.materials:
        m = bpy.data.materials.new(name)
        m.diffuse_color = (*LIN[key], 1)
        MAT_INFO[name] = dict(base=None, normal=None, cutout=False, color=LIN[key])
    return bpy.data.materials[name]


def export_mat(key, color=(0.5, 0.5, 0.5)):
    """Final material M_<key>. Uses T_<key>_BaseColor/Normal.png if present."""
    name = "M_" + key
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.85
    bpath = os.path.join(TEXDIR, f"T_{key}_BaseColor.png")
    npath = os.path.join(TEXDIR, f"T_{key}_Normal.png")
    cutout = key.startswith("Card_") or key.startswith("Imp_")
    info = dict(base=None, normal=None, cutout=cutout, color=color)
    if os.path.exists(bpath):
        img = bpy.data.images.load(bpath, check_existing=True)
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if cutout:
            nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
        info["base"] = img
    else:
        bsdf.inputs["Base Color"].default_value = (*color, 1)
    if os.path.exists(npath):
        img = bpy.data.images.load(npath, check_existing=True)
        img.colorspace_settings.name = "Non-Color"
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nm.uv_map = "UVMap"
        nt.links.new(tex.outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
        info["normal"] = img
    m.diffuse_color = (*color, 1)
    m.blend_method = "CLIP" if cutout else "OPAQUE"
    m.shadow_method = "CLIP" if cutout else "OPAQUE"
    m.use_backface_culling = True
    MAT_INFO[name] = info
    return m


# ============================================================ render passes
PASS = {}
NORMAL_DOTS = []  # per normal-pass material: dot nodes for camera right / up / back


def pass_mat(src, kind):
    key = (src.name, kind)
    if key in PASS:
        return PASS[key]
    info = MAT_INFO.get(src.name, dict(base=None, normal=None, cutout=False, color=tuple(src.diffuse_color[:3])))
    m = bpy.data.materials.new(f"PASS_{kind}_{src.name}")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    N, L = nt.nodes, nt.links
    out = N.new("ShaderNodeOutputMaterial")
    em = N.new("ShaderNodeEmission")
    alpha = None
    tex = None
    if info["base"] is not None:
        tex = N.new("ShaderNodeTexImage")
        tex.image = info["base"]
        if info["cutout"]:
            alpha = tex.outputs["Alpha"]
    if kind == "albedo":
        if tex:
            col = tex.outputs["Color"]
        else:
            rgb = N.new("ShaderNodeRGB")
            rgb.outputs[0].default_value = (*info["color"], 1)
            nz = N.new("ShaderNodeTexNoise")
            nz.inputs["Scale"].default_value = 6.0
            mr = N.new("ShaderNodeMapRange")
            mr.inputs["To Min"].default_value = 0.8
            mr.inputs["To Max"].default_value = 1.2
            L.new(nz.outputs["Fac"], mr.inputs["Value"])
            mul = N.new("ShaderNodeVectorMath")
            mul.operation = "MULTIPLY"
            L.new(rgb.outputs[0], mul.inputs[0])
            L.new(mr.outputs["Result"], mul.inputs[1])
            col = mul.outputs[0]
        ao = N.new("ShaderNodeAmbientOcclusion")
        mr2 = N.new("ShaderNodeMapRange")
        mr2.inputs["To Min"].default_value = 0.55
        mr2.inputs["To Max"].default_value = 1.0
        L.new(ao.outputs["AO"], mr2.inputs["Value"])
        mul2 = N.new("ShaderNodeVectorMath")
        mul2.operation = "MULTIPLY"
        L.new(col, mul2.inputs[0])
        L.new(mr2.outputs["Result"], mul2.inputs[1])
        L.new(mul2.outputs[0], em.inputs["Color"])
    else:
        geo = N.new("ShaderNodeNewGeometry")
        nrm = geo.outputs["Normal"]
        if info["normal"] is not None:
            ntex = N.new("ShaderNodeTexImage")
            ntex.image = info["normal"]
            nm = N.new("ShaderNodeNormalMap")
            nm.uv_map = "UVMap"
            L.new(ntex.outputs["Color"], nm.inputs["Color"])
            nrm = nm.outputs["Normal"]
        comb = N.new("ShaderNodeCombineXYZ")
        dots = []
        for i in range(3):
            dn = N.new("ShaderNodeVectorMath")
            dn.operation = "DOT_PRODUCT"
            L.new(nrm, dn.inputs[0])
            L.new(dn.outputs["Value"], comb.inputs[i])
            dots.append(dn)
        NORMAL_DOTS.append(dots)
        ma = N.new("ShaderNodeVectorMath")
        ma.operation = "MULTIPLY_ADD"
        ma.inputs[1].default_value = (0.5, 0.5, 0.5)
        ma.inputs[2].default_value = (0.5, 0.5, 0.5)
        L.new(comb.outputs[0], ma.inputs[0])
        L.new(ma.outputs[0], em.inputs["Color"])
    if alpha is not None:
        tr = N.new("ShaderNodeBsdfTransparent")
        mix = N.new("ShaderNodeMixShader")
        L.new(alpha, mix.inputs[0])
        L.new(tr.outputs[0], mix.inputs[1])
        L.new(em.outputs[0], mix.inputs[2])
        L.new(mix.outputs[0], out.inputs["Surface"])
    else:
        L.new(em.outputs[0], out.inputs["Surface"])
    m.blend_method = "CLIP"
    m.alpha_threshold = 0.5
    m.use_backface_culling = True
    PASS[key] = m
    return m


CAM = None


def camera():
    global CAM
    if CAM is None:
        CAM = bpy.data.objects.new("BakeCam", bpy.data.cameras.new("BakeCam"))
        bpy.context.scene.collection.objects.link(CAM)
        CAM.data.clip_start = 0.01
        CAM.data.clip_end = 500
    return CAM


def set_view(raw):
    vs = bpy.context.scene.view_settings
    vs.look = "None"
    vs.exposure = 0
    vs.gamma = 1
    if raw:
        for vt in ("Raw", "Standard"):
            try:
                vs.view_transform = vt
                break
            except TypeError:
                pass
    else:
        vs.view_transform = "Standard"


def load_pixels(path):
    img = bpy.data.images.load(path, check_existing=False)
    img.colorspace_settings.name = "Non-Color"
    w, h = img.size
    arr = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(arr)
    bpy.data.images.remove(img)
    return arr.reshape(h, w, 4)


def save_png(arr, path):
    h, w = arr.shape[:2]
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=True)
    img.colorspace_settings.name = "Non-Color"
    img.pixels.foreach_set(np.clip(arr, 0, 1).astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def dilate(arr, iters=40):
    """Bleed colour into transparent pixels so mipmaps don't get dark halos."""
    rgb = arr[..., :3].copy()
    m = arr[..., 3] > 0.02
    for _ in range(iters):
        if m.all():
            break
        mf = m.astype(np.float32)
        acc = np.zeros_like(rgb)
        cnt = np.zeros(m.shape, np.float32)
        prgb = np.pad(rgb * mf[..., None], ((1, 1), (1, 1), (0, 0)))
        pm = np.pad(mf, 1)
        H, W = mf.shape
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1)):
            acc += prgb[1 + dy:1 + dy + H, 1 + dx:1 + dx + W]
            cnt += pm[1 + dy:1 + dy + H, 1 + dx:1 + dx + W]
        new = (~m) & (cnt > 0)
        rgb[new] = acc[new] / cnt[new][:, None]
        m |= new
    if m.any():
        rgb[~m] = rgb[m].mean(0)
    out = arr.copy()
    out[..., :3] = rgb
    return out


def render_passes(objs, cam_loc, cam_rot, ortho, rx, ry, ao_dist):
    """Render albedo(+alpha) and camera-space normals of objs. Returns two HxWx4 arrays."""
    sc = bpy.context.scene
    cam = camera()
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = ortho
    cam.location = cam_loc
    cam.rotation_euler = cam_rot
    sc.camera = cam
    sc.render.resolution_x, sc.render.resolution_y = rx, ry
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.render.image_settings.color_depth = "8"
    sc.eevee.gtao_distance = ao_dist
    bpy.context.view_layer.update()
    mw = cam.matrix_world.to_3x3()
    axes = [mw.col[0].normalized(), mw.col[1].normalized(), mw.col[2].normalized()]  # right, up, towards viewer
    keep = set(o.name for o in objs)
    hidden = {}
    for o in sc.objects:
        hidden[o.name] = o.hide_render
        o.hide_render = o.name not in keep and o is not cam
    saved = {o.name: list(o.data.materials) for o in objs}
    res = []
    for kind in ("albedo", "normal"):
        for o in objs:
            for mm in saved[o.name]:
                pass_mat(mm, "normal")
        for dots in NORMAL_DOTS:
            for dn, ax in zip(dots, axes):
                dn.inputs[1].default_value = ax
        for o in objs:
            for i, mm in enumerate(saved[o.name]):
                o.data.materials[i] = pass_mat(mm, kind)
        set_view(kind == "normal")
        os.makedirs(TMP, exist_ok=True)
        sc.render.filepath = os.path.join(TMP, f"pass_{kind}.png")
        bpy.ops.render.render(write_still=True)
        res.append(load_pixels(sc.render.filepath))
    for o in objs:
        for i, mm in enumerate(saved[o.name]):
            o.data.materials[i] = mm
    for o in sc.objects:
        o.hide_render = hidden.get(o.name, False)
    set_view(False)
    return res


def save_tex(key, col, nrm, cutout=True):
    col = dilate(col) if cutout else col
    nrm = dilate(nrm)
    nrm[..., 3] = 1.0
    if not cutout:
        col[..., 3] = 1.0
    save_png(col, os.path.join(TEXDIR, f"T_{key}_BaseColor.png"))
    save_png(nrm, os.path.join(TEXDIR, f"T_{key}_Normal.png"))


def remove_objs(objs):
    for o in objs:
        me = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if me and me.users == 0:
            bpy.data.meshes.remove(me)


# ====================================================== bark (numpy, tiling)
def pnoise(h, w, cy, cx, rng):
    g = rng.random((cy, cx)).astype(np.float32)
    ys = np.arange(h) * cy / h
    xs = np.arange(w) * cx / w
    y0 = np.floor(ys).astype(int)
    x0 = np.floor(xs).astype(int)
    ty = ys - y0
    tx = xs - x0
    ty = (ty * ty * (3 - 2 * ty))[:, None]
    tx = (tx * tx * (3 - 2 * tx))[None, :]
    y1 = (y0 + 1) % cy
    x1 = (x0 + 1) % cx
    a, b = g[y0][:, x0], g[y0][:, x1]
    c, d = g[y1][:, x0], g[y1][:, x1]
    return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty


def fbm(h, w, cy, cx, rng, octaves=4):
    s = np.zeros((h, w), np.float32)
    amp = tot = 0.0
    amp = 1.0
    for o in range(octaves):
        s += amp * pnoise(h, w, cy * 2 ** o, cx * 2 ** o, rng)
        tot += amp
        amp *= 0.5
    return s / tot


def height_to_normal(hgt, k):
    dx = (np.roll(hgt, -1, 1) - np.roll(hgt, 1, 1)) * k
    dy = (np.roll(hgt, -1, 0) - np.roll(hgt, 1, 0)) * k
    n = np.stack([-dx, -dy, np.ones_like(hgt)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    out = np.ones(hgt.shape + (4,), np.float32)
    out[..., :3] = n * 0.5 + 0.5
    return out


def bark_textures():
    S = 512
    specs = {
        "Bark_Oak": ((0.16, 0.11, 0.07), (0.42, 0.32, 0.22), "furrow", 14),
        "Bark_Dead": ((0.22, 0.21, 0.20), (0.62, 0.60, 0.56), "furrow", 10),
        "Bark_Pine": ((0.20, 0.10, 0.06), (0.55, 0.33, 0.20), "plate", 8),
        "Bark_Birch": ((0.12, 0.10, 0.09), (0.90, 0.88, 0.82), "birch", 6),
        "Bark_Stem": ((0.18, 0.14, 0.08), (0.40, 0.33, 0.18), "furrow", 18),
    }
    for i, (key, (dark, light, style, cx)) in enumerate(specs.items()):
        rng = np.random.default_rng(ARGS.seed * 100 + i)
        fine = fbm(S, S, 32, 32, rng, 3)
        dark, light = np.array(dark), np.array(light)
        if style == "furrow":
            n = fbm(S, S, 3, cx, rng, 5)
            hgt = (1 - np.abs(2 * n - 1)) ** 1.5
            t = np.clip(hgt * 1.3 - 0.1, 0, 1)
        elif style == "plate":
            n = fbm(S, S, 5, cx, rng, 5)
            hgt = np.clip((n - 0.38) * 4, 0, 1)
            t = hgt * 0.8 + fine * 0.2
        else:
            marks = fbm(S, S, 48, cx, rng, 3)
            patch = fbm(S, S, 4, 4, rng, 4)
            mk = np.clip((marks - 0.66) * 8, 0, 1) + np.clip((patch - 0.66) * 6, 0, 1)
            mk = np.clip(mk, 0, 1)
            hgt = 1 - 0.6 * mk
            t = 1 - mk
        col = np.ones((S, S, 4), np.float32)
        col[..., :3] = (dark + (light - dark) * t[..., None]) * (0.85 + 0.3 * fine[..., None])
        nrm = height_to_normal(hgt.astype(np.float32), 6.0)
        save_tex(key, col, nrm, cutout=False)
        print("  bark", key)


# ============================================== leaf-card textures (rendered)
def inside(p, W, H, m=0.015):
    return m < p.x < W - m and m < p.y < H - m


def safe_leaf(mb, base, axis, nrm, size, mat, shape, W, H, fold=0.12):
    for _ in range(8):
        tip = base + axis.normalized() * size
        side = abs(SHAPES[shape][2][0]) * size + 0.01
        if inside(tip, W, H) and inside(base + axis.normalized() * size * 0.5 + Vector((side, 0, 0)), W, H) \
                and inside(base + axis.normalized() * size * 0.5 - Vector((side, 0, 0)), W, H):
            leaf(mb, base, axis, nrm, size, mat, shape, fold)
            return True
        size *= 0.82
    return False


def flower(mb, base, size, petal_mat):
    ref = Vector((1, 0, 0))
    for k in range(5):
        ax = Quaternion(UP, TAU * k / 5) @ ref
        leaf(mb, base + Vector((0, 0, 0.002 * k)), ax + UP * 0.2, UP, size, petal_mat, "petal", 0.05)
    leaf(mb, base + Vector((-size * 0.15, 0, 0.02)), Vector((1, 0, 0)) * 1 + Vector((0, 0.0, 0)), UP, size * 0.3,
         src_mat("Center"), "disc", 0.2)


def twig_cluster(rng, shape, size_rng, leafkeys, n_subs, per_pt, flower_key=None, flower_share=0.0, stem="Stem"):
    """Square card [0,1]^2. Twig enters at bottom centre (u=0.5, v=0)."""
    mb = MB()
    sm = src_mat(stem)
    lms = [src_mat(k) for k in leafkeys]
    main = grow_line(rng, Vector((0.5, 0.0, 0)), Vector((0, 1, 0)), 0.72, 7, 0.12, Vector(), flat=True)
    stems = [main]
    for _ in range(n_subs):
        i = rng.randint(2, 5)
        sgn = rng.choice([-1, 1])
        d = (Quaternion(UP, sgn * math.radians(rng.uniform(30, 55))) @ (main[i + 1] - main[i])).normalized()
        stems.append(grow_line(rng, main[i], d, rng.uniform(0.25, 0.4), 4, 0.15, Vector(), flat=True))
    for si, pts in enumerate(stems):
        r0 = 0.014 if si == 0 else 0.009
        tube(mb, [p - Vector((0, 0, 0.02)) for p in pts], [lerp(r0, r0 * 0.4, i / (len(pts) - 1)) for i in range(len(pts))], 5, sm)
        start = 2 if si == 0 else 1
        for i in range(start, len(pts)):
            d = (pts[i] - pts[i - 1]).normalized()
            for k in range(per_pt):
                sgn = 1 if (i + k) % 2 else -1
                ax = (Quaternion(UP, sgn * math.radians(rng.uniform(30, 70))) @ d) + UP * rng.uniform(0, 0.2)
                base = pts[i] + Vector((0, 0, rng.uniform(0, 0.05) + i * 0.004))
                nrm = UP + rvec(rng, 0.35)
                if flower_key and rng.random() < flower_share:
                    fb = base + ax.normalized() * 0.06
                    if inside(fb, 1, 1, 0.1):
                        flower(mb, fb + Vector((0, 0, 0.08)), rng.uniform(0.06, 0.09), src_mat(flower_key))
                        continue
                safe_leaf(mb, base, ax, nrm, rng.uniform(*size_rng), rng.choice(lms), shape, 1, 1)
        safe_leaf(mb, pts[-1] + Vector((0, 0, 0.06)), pts[-1] - pts[-2], UP + rvec(rng, 0.2),
                  size_rng[1], rng.choice(lms), shape, 1, 1)
    return mb


def pine_spray(rng):
    """2:1 card [0,2]x[0,1]. Branch enters at left centre (u=0, v=0.5) and runs along +u."""
    mb = MB()
    sm = src_mat("Stem")
    nm = [src_mat("Needle"), src_mat("NeedleLight")]
    tipm = src_mat("NeedleTip")
    main = grow_line(rng, Vector((0.0, 0.5, 0)), Vector((1, 0, 0)), 1.85, 10, 0.04, Vector(), flat=True)
    stems = [(main, 0.75)]
    for i in range(1, 10):
        for sgn in (1, -1):
            if rng.random() < 0.95:
                d = (Quaternion(UP, sgn * math.radians(rng.uniform(35, 55))) @ (main[i + 1] - main[i])).normalized()
                L = rng.uniform(0.3, 0.48) * lerp(1, 0.6, i / 10)
                stems.append((grow_line(rng, main[i], d, L, 3, 0.1, Vector(), flat=True), 0.55))
    for pts, nl in stems:
        tube(mb, [p - Vector((0, 0, 0.03)) for p in pts], [0.012] * len(pts), 4, sm)
        total = sum((pts[i] - pts[i - 1]).length for i in range(1, len(pts)))
        steps = int(total / 0.012)
        for s in range(steps):
            t = s / steps
            fi = t * (len(pts) - 1)
            i = int(fi)
            p = lerp(pts[i], pts[i + 1], fi - i)
            d = (pts[i + 1] - pts[i]).normalized()
            for sgn in (1, -1):
                ax = Quaternion(UP, sgn * math.radians(rng.uniform(40, 65))) @ d + UP * rng.uniform(-0.1, 0.3)
                L = rng.uniform(0.11, 0.15) * nl * 1.4 * lerp(1, 0.6, t)
                mat = tipm if t > 0.85 and rng.random() < 0.6 else rng.choice(nm)
                base = p + Vector((0, 0, rng.uniform(-0.01, 0.04)))
                safe_leaf(mb, base, ax, UP + rvec(rng, 0.3), L, mat, "needle", 2, 1, fold=0.05)
    return mb


def ivy_cluster(rng):
    mb = MB()
    sm = src_mat("StemGreen")
    lms = [src_mat("Ivy"), src_mat("IvyLight"), src_mat("IvyDark"), src_mat("Ivy")]
    root = Vector((0.5, 0.02, 0))
    for i in range(rng.randint(14, 18)):
        ang = math.radians(rng.uniform(-70, 70))
        d = Vector((math.sin(ang), math.cos(ang), 0))
        L = rng.uniform(0.2, 0.62)
        pts = grow_line(rng, root, d, L, 3, 0.15, Vector(), flat=True)
        tube(mb, [p - Vector((0, 0, 0.02)) for p in pts], [0.008, 0.006, 0.005, 0.004], 4, sm)
        safe_leaf(mb, pts[-1] + Vector((0, 0, rng.uniform(0, 0.1))), pts[-1] - pts[-2] + rvec(rng, 0.3),
                  UP + rvec(rng, 0.3), rng.uniform(0.22, 0.32), rng.choice(lms), "ivy", 1, 1, fold=0.15)
    return mb


def fern_frond(rng):
    """2:1 card [0,2]x[0,1]. Rachis from u=0 (base) to u=1 (tip) along v=0.5."""
    mb = MB()
    lms = [src_mat("Fern"), src_mat("FernLight")]
    rach = [Vector((x, 0.5 + 0.04 * math.sin(x * 1.5), 0)) for x in np.linspace(0.02, 1.96, 24)]
    tube(mb, [p - Vector((0, 0, 0.02)) for p in rach], [lerp(0.012, 0.004, i / 23) for i in range(24)], 4, src_mat("StemGreen"))
    x = 0.12
    while x < 1.9:
        t = x / 2
        y = 0.5 + 0.04 * math.sin(x * 1.5)
        L = 0.44 * math.sin(math.pi * (0.15 + 0.85 * t)) + 0.04
        for sgn in (1, -1):
            ax = Vector((math.cos(math.radians(62)), sgn * math.sin(math.radians(62)), 0))
            safe_leaf(mb, Vector((x, y, 0.01 * rng.random())), ax, UP + rvec(rng, 0.25), L, rng.choice(lms), "pinna", 2, 1, 0.1)
        x += 0.065
    return mb


def grass_strip(rng):
    """2:1 card [0,2]x[0,1], blades rooted along v=0."""
    mb = MB()
    lms = [src_mat("Grass"), src_mat("Grass"), src_mat("GrassLight"), src_mat("GrassDry")]
    for _ in range(80):
        x = rng.uniform(0.08, 1.92)
        h = rng.uniform(0.45, 0.95)
        lean = rng.uniform(-0.25, 0.25)
        w = rng.uniform(0.025, 0.045)
        m = rng.choice(lms)
        pts, z = [], rng.uniform(0, 0.05)
        for i in range(6):
            t = i / 5
            pts.append(Vector((x + lean * t * t, h * t * (1 - 0.1 * abs(lean)), z)))
        for i in range(5):
            d = (pts[i + 1] - pts[i]).normalized()
            s = Vector((d.y, -d.x, 0))
            wa, wb = w * (1 - i / 5), w * (1 - (i + 1) / 5)
            q = [pts[i] - s * wa / 2, pts[i] + s * wa / 2, pts[i + 1] + s * wb / 2, pts[i + 1] - s * wb / 2]
            if wb < 1e-4:
                q = q[:3]
            mb.face([mb.vert(c) for c in q], m)
    return mb


def vine_strand(rng):
    """1:4 card [0,0.25]x[0,1], tiles vertically. Stem at u=0.5."""
    mb = MB()
    lms = [src_mat("Leaf_Green"), src_mat("Leaf_GreenDark"), src_mat("IvyLight")]
    pts = [Vector((0.125 + 0.012 * math.sin(TAU * y), y, -0.02)) for y in np.linspace(-0.05, 1.05, 30)]
    tube(mb, pts, [0.006] * 30, 4, src_mat("StemGreen"), cap=False)
    n = 14
    for i in range(n):
        y = (i + 0.5) / n
        sgn = 1 if i % 2 else -1
        base = Vector((0.125 + 0.012 * math.sin(TAU * y), y, 0.01 * i))
        ax = Vector((sgn * 0.75, -0.6, 0)) + rvec(rng, 0.15)
        ax.z = 0
        safe_leaf(mb, base, ax, UP + rvec(rng, 0.3), rng.uniform(0.08, 0.1), rng.choice(lms), "oval", 0.25, 1)
    return mb


def bake_card(key, mb, W, H, res_x, res_y):
    ob = mb.build("TEX_" + key)
    col, nrm = render_passes([ob], Vector((W / 2, H / 2, 20)), (0, 0, 0), max(W, H), res_x, res_y, 0.08 * max(W, H))
    save_tex(key, col, nrm, cutout=True)
    remove_objs([ob])
    print("  card", key)


def card_textures():
    T = ARGS.tex
    R = lambda tag: random.Random(f"{ARGS.seed}-tex-{tag}")
    bake_card("Card_Oak", twig_cluster(R("oak"), "oak", (0.16, 0.24), ["Leaf_Green", "Leaf_GreenLight", "Leaf_GreenDark", "Leaf_Olive"], 3, 2), 1, 1, T, T)
    bake_card("Card_Birch", twig_cluster(R("birch"), "birch", (0.1, 0.16), ["Leaf_Birch", "Leaf_BirchLight", "Leaf_Green"], 4, 2), 1, 1, T, T)
    bake_card("Card_Bush", twig_cluster(R("bush"), "oval", (0.1, 0.15), ["Leaf_Green", "Leaf_GreenLight", "Leaf_GreenDark"], 4, 2), 1, 1, T, T)
    for fk in ("Pink", "White", "Yellow", "Purple"):
        bake_card(f"Card_Bush{fk}", twig_cluster(R("fb" + fk), "oval", (0.1, 0.15), ["Leaf_Green", "Leaf_GreenLight", "Leaf_GreenDark"], 4, 2, fk, 0.3), 1, 1, T, T)
    bake_card("Card_Pine", pine_spray(R("pine")), 2, 1, T, T // 2)
    bake_card("Card_Fern", fern_frond(R("fern")), 2, 1, T, T // 2)
    bake_card("Card_Grass", grass_strip(R("grass")), 2, 1, T, T // 2)
    bake_card("Card_Ivy", ivy_cluster(R("ivy")), 1, 1, T // 2, T // 2)
    bake_card("Card_Vine", vine_strand(R("vine")), 0.25, 1, T // 4, T)


# ================================================================ impostors
def bounds(objs):
    bpy.context.view_layer.update()
    xs, ys, zs = [], [], []
    for o in objs:
        for v in o.data.vertices:
            p = o.matrix_world @ v.co
            xs.append(p.x), ys.append(p.y), zs.append(p.z)
    return (min(xs), max(xs)), (min(ys), max(ys)), (min(zs), max(zs))


def impostor(name, src, mode="cross", res=512):
    (x0, x1), (y0, y1), (z0, z1) = bounds([src])
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    key = "Imp_" + name
    mb = MB()
    if mode == "cross":
        S = max(x1 - x0, y1 - y0, z1 - z0) * 1.04
        cz = z0 + S / 2
        a = render_passes([src], Vector((cx, -100, cz)), (math.pi / 2, 0, 0), S, res, res, S * 0.08)
        b = render_passes([src], Vector((100, cy, cz)), (math.pi / 2, 0, math.pi / 2), S, res, res, S * 0.08)
        save_tex(key, np.concatenate([a[0], b[0]], 1), np.concatenate([a[1], b[1]], 1))
        m = export_mat(key)
        card(mb, Vector((cx - S / 2, cy, z0)), Vector((1, 0, 0)), UP, S, S, m, (0, 0, 0.5, 1), flutter=0.0)
        card(mb, Vector((cx, cy - S / 2, z0)), Vector((0, 1, 0)), UP, S, S, m, (0.5, 0, 1, 1), flutter=0.0)
    else:  # single front-facing quad (ivy walls, hanging vines)
        W, H = (x1 - x0) * 1.04, (z1 - z0) * 1.04
        big = 1024
        rx, ry = (big, max(64, int(big * H / W))) if W >= H else (max(64, int(big * W / H)), big)
        cz = (z0 + z1) / 2
        col, nrm = render_passes([src], Vector((cx, -100, cz)), (math.pi / 2, 0, 0), max(W, H), rx, ry, 0.15)
        save_tex(key, col, nrm)
        m = export_mat(key)
        y = y0 - 0.005 if mode == "wall" else cy
        card(mb, Vector((cx - W / 2, y, cz - H / 2)), Vector((1, 0, 0)), UP, W, H, m, flutter=0.0)
    return mb


# ===================================================================== trees
def tree_skeleton(rng, P):
    branches, tips = [], []

    def rec(start, d, length, radius, depth):
        segs = P["segs"][depth]
        pts = grow_line(rng, start, d, length, segs, P["wobble"][depth], Vector((0, 0, P["tropism"][depth])))
        r_end = radius * (0.12 if depth == P["depth"] else 0.3)
        radii = [lerp(radius, r_end, (i / segs) ** 0.8) for i in range(segs + 1)]
        if depth == 0:
            radii[0] *= 1.35
        branches.append((pts, radii, depth))
        if depth == P["depth"]:
            tips.append(pts[-1])
            return
        lo, hi = P["children"][depth]
        nchild = rng.randint(lo, hi)
        az0 = rng.uniform(0, TAU)
        for c in range(nchild):
            f = lerp(P["child_start"][depth], 0.97, (c + rng.uniform(0.2, 0.8)) / nchild)
            fi = f * (len(pts) - 1)
            i = min(int(fi), len(pts) - 2)
            at = lerp(pts[i], pts[i + 1], fi - i)
            r_at = lerp(radii[i], radii[i + 1], fi - i)
            pd = (pts[i + 1] - pts[i]).normalized()
            perp = Quaternion(pd, az0 + c * 2.39996 + rng.uniform(-0.4, 0.4)) @ any_perp(pd)
            ang = math.radians(P["angle"][depth] + rng.uniform(-12, 12))
            cd = (pd * math.cos(ang) + perp * math.sin(ang)).normalized()
            clen = length * P["len_ratio"][depth] * lerp(1.0, P["top_shrink"], f) * rng.uniform(0.8, 1.15)
            rec(at - pd * r_at * 0.3, cd, clen, min(r_at * 0.8, radius * P["rad_ratio"]), depth + 1)
        tips.append(pts[-1])

    rec(Vector((0, 0, -0.1)), (UP + rvec(rng, 0.08)).normalized(),
        P["height"] * rng.uniform(0.9, 1.1), P["radius"] * rng.uniform(0.9, 1.1), 0)
    return branches, tips


OAK = dict(height=3.2, radius=0.26, depth=3, segs=[8, 5, 4, 3], wobble=[0.06, 0.15, 0.2, 0.25],
           tropism=[0, 0.05, 0.06, 0.04], children=[(4, 6), (3, 4), (2, 3)], angle=[55, 45, 40],
           len_ratio=[0.85, 0.6, 0.55], top_shrink=0.6, child_start=[0.45, 0.3, 0.3], rad_ratio=0.55,
           bark="Bark_Oak", leaf="Card_Oak", card=1.15, per_pt=2)
BIRCH = dict(height=6.5, radius=0.15, depth=2, segs=[10, 5, 4], wobble=[0.04, 0.12, 0.18],
             tropism=[0, 0.12, 0.08], children=[(6, 9), (2, 4)], angle=[35, 35], len_ratio=[0.38, 0.55],
             top_shrink=0.5, child_start=[0.4, 0.3], rad_ratio=0.4, bark="Bark_Birch", leaf="Card_Birch",
             card=0.85, per_pt=3)
DEAD = dict(height=3.8, radius=0.24, depth=4, segs=[8, 5, 4, 3, 3], wobble=[0.06, 0.2, 0.25, 0.3, 0.3],
            tropism=[0, 0.03, 0.02, 0.0, -0.02], children=[(3, 5), (2, 3), (2, 3), (1, 2)],
            angle=[50, 45, 40, 40], len_ratio=[0.8, 0.6, 0.6, 0.6], top_shrink=0.6,
            child_start=[0.4, 0.3, 0.3, 0.3], rad_ratio=0.5, bark="Bark_Dead", leaf=None)

LOD_KEEP = [1.0, 0.45, 0.2]
LOD_SCALE = [1.0, 1.3, 1.65]


def sub(pts, step):
    out = pts[::step]
    if out[-1] is not pts[-1]:
        out = out + [pts[-1]]
    return out


def emit_branches(mb, branches, lod, maxdepth, bark):
    sides = [[10, 7, 5, 4, 3], [7, 5, 4, 3, 3], [5, 4, 3, 3, 3]][lod]
    step = [1, 2, 3][lod]
    for pts, radii, depth in branches:
        if depth > maxdepth[lod]:
            continue
        idx = sub(list(range(len(pts))), step)
        tube(mb, [pts[i] for i in idx], [radii[i] for i in idx], sides[min(depth, 4)], bark)


def emit_cards(mb, cards, lod, mat_of, keep=LOD_KEEP, scale=LOD_SCALE):
    for c in cards:
        if c["key"] < keep[lod]:
            s = c["s"] * scale[lod]
            card(mb, c["base"] - c["u"] * s * c.get("w", 1) / 2, c["u"], c["v"], s * c.get("w", 1), s * c.get("h", 1),
                 mat_of(c), bend=c.get("bend", 0.0), segs=2 if c.get("bend") else 1)


def broadleaf_tree(seed, name, P):
    rng = random.Random(seed)
    branches, tips = tree_skeleton(rng, P)
    bark = export_mat(P["bark"])
    cards = []
    if P["leaf"]:
        center = sum(tips, Vector()) / len(tips)
        center.z -= 0.5
        for pts, radii, depth in branches:
            if depth < P["depth"] - 1:
                continue
            for i in range(len(pts) // 3 if depth == P["depth"] - 1 else 1, len(pts)):
                for _ in range(P["per_pt"]):
                    d = (pts[i] - pts[i - 1]).normalized()
                    out = (pts[i] - center).normalized()
                    v = (d * 0.5 + out * 0.6 + UP * 0.3 + rvec(rng, 0.5)).normalized()
                    n = orth(out + UP * 0.4 + rvec(rng, 0.9), v)
                    s = P["card"] * rng.uniform(0.75, 1.2)
                    cards.append(dict(base=pts[i] - v * s * 0.15, u=v.cross(n), v=v, s=s, key=rng.random(), bend=-0.15))
    leafm = export_mat(P["leaf"]) if P["leaf"] else None
    maxdepth = [P["depth"], P["depth"] - 1, max(1, P["depth"] - 2)]
    lods = []
    for lod in range(3):
        mb = MB()
        emit_branches(mb, branches, lod, maxdepth, bark)
        emit_cards(mb, cards, lod, lambda c: leafm)
        lods.append(mb.build(f"{name}_LOD{lod}"))
    lods.append(impostor(name, lods[0]).build(f"{name}_LOD3"))
    return lods


def pine_tree(seed, name, H):
    rng = random.Random(seed)
    H *= rng.uniform(0.85, 1.15)
    trunk = grow_line(rng, Vector((0, 0, -0.1)), UP, H, 10, 0.012, Vector())
    radii = [lerp(0.2 * H / 8, 0.02, i / 10) for i in range(11)]
    radii[0] *= 1.4
    bark = export_mat("Bark_Pine")
    needles = export_mat("Card_Pine")
    limbs, cards = [], []
    z = 1.0
    while z < H * 0.93:
        t = z / H
        n = rng.randint(5, 7)
        az0 = rng.uniform(0, TAU)
        start = Vector((trunk[-1].x * t, trunk[-1].y * t, z))
        for k in range(n):
            az = az0 + TAU * k / n + rng.uniform(-0.3, 0.3)
            e = math.radians(lerp(-5, 20, t) + rng.uniform(-8, 8))
            d = Vector((math.cos(az) * math.cos(e), math.sin(az) * math.cos(e), math.sin(e)))
            L = lerp(H * 0.3, H * 0.07, t) * rng.uniform(0.85, 1.1)
            pts = grow_line(rng, start, d, L, 3, 0.05, Vector((0, 0, -0.06)))
            limbs.append(pts)
            u = (pts[-1] - pts[0]).normalized()
            side = UP.cross(u).normalized()
            roll = rng.uniform(-25, 25)
            key = rng.random()
            for j, extra in enumerate((0, 60, -60)):
                v = Quaternion(u, math.radians(roll + extra)) @ side
                w = L * 1.15
                cards.append(dict(base=pts[0] + u * w / 2, u=u, v=v, s=w, w=1, h=0.55, key=key, j=j))
        z += 0.42 * H / 8 * rng.uniform(0.85, 1.15)
    top = trunk[-1]
    for k in range(3):
        v = Quaternion(UP, TAU * k / 3) @ Vector((1, 0, 0))
        w = H * 0.14
        cards.append(dict(base=top - UP * w * 0.75 + UP * w / 2, u=UP, v=v, s=w, w=1, h=0.5, key=0.0, j=0))
    lods = []
    for lod in range(3):
        mb = MB()
        tube(mb, sub(trunk, [1, 2, 3][lod]), sub(radii, [1, 2, 3][lod]), [8, 6, 4][lod], bark)
        if lod == 0:
            for pts in limbs:
                tube(mb, pts, [0.035, 0.025, 0.018, 0.01], 4, bark)
        for c in cards:
            if lod == 1 and c["j"] == 2:
                continue
            if lod == 2 and (c["j"] == 2 or (c["j"] == 1 and c["key"] > 0.5)):
                continue
            sc = [1.0, 1.05, 1.2][lod]
            w, h = c["s"] * sc, c["s"] * sc * c["h"]
            # card attaches at its left edge (u=0) where the branch meets the trunk
            card(mb, c["base"] - c["u"] * c["s"] / 2 - c["v"] * h / 2, c["u"], c["v"], w, h, needles)
        lods.append(mb.build(f"{name}_LOD{lod}"))
    lods.append(impostor(name, lods[0]).build(f"{name}_LOD3"))
    return lods


def dead_tree(seed, name):
    return broadleaf_tree(seed, name, DEAD)


# ==================================================================== bushes
def fib_dirs(n):
    out = []
    for i in range(n):
        z = 1 - 2 * (i + 0.5) / n
        r = math.sqrt(1 - z * z)
        a = i * 2.39996
        out.append(Vector((math.cos(a) * r, math.sin(a) * r, z)))
    return out


def bush(seed, name, flower_key=None):
    rng = random.Random(seed)
    size = rng.uniform(0.7, 1.1)
    leafm = export_mat("Card_Bush")
    flm = export_mat("Card_Bush" + flower_key) if flower_key else None
    core = export_mat("Flat_BushCore", (0.03, 0.10, 0.02))
    bark = export_mat("Bark_Stem")
    clumps, cards = [], []
    n = rng.randint(4, 6)
    for i in range(n):
        a = TAU * i / n + rng.uniform(-0.3, 0.3)
        d = size * rng.uniform(0.25, 0.5) if i else 0
        r = size * rng.uniform(0.45, 0.6)
        c = Vector((math.cos(a) * d, math.sin(a) * d, r * 0.75 + (0.15 * size if i == 0 else 0)))
        clumps.append((c, r, rvec(rng, 50)))
        for dr in fib_dirs(34):
            if dr.z < -0.7:
                continue
            dr = (dr + rvec(rng, 0.2)).normalized()
            v = (dr + UP * 0.5 + rvec(rng, 0.3)).normalized()
            nn = orth(dr + rvec(rng, 0.6), v)
            s = size * 0.5 * rng.uniform(0.8, 1.2)
            cards.append(dict(base=c + dr * r * 0.6 - v * s * 0.25, u=v.cross(nn), v=v, s=s, key=rng.random(),
                              fl=bool(flm) and rng.random() < 0.4, bend=-0.12))
    stems = [grow_line(rng, Vector((0, 0, -0.05)), (UP + rvec(rng, 0.5)).normalized(), size * 0.5, 3, 0.2, Vector()) for _ in range(4)]
    lods = []
    for lod in range(3):
        mb = MB()
        for c, r, sd in clumps:
            blob(mb, c, r * [0.55, 0.45, 0.4][lod], sd, core, sub=2 if lod == 0 else 1)
        if lod == 0:
            for pts in stems:
                tube(mb, pts, [0.03, 0.022, 0.015, 0.008], 5, bark)
        emit_cards(mb, cards, lod, lambda c: flm if c["fl"] else leafm, keep=[1.0, 0.6, 0.35], scale=[1.0, 1.2, 1.45])
        lods.append(mb.build(f"{name}_LOD{lod}"))
    lods.append(impostor(name, lods[0]).build(f"{name}_LOD3"))
    return lods


def fern(seed, name):
    rng = random.Random(seed)
    m = export_mat("Card_Fern")
    fronds = []
    nfr = rng.randint(7, 10)
    for f in range(nfr):
        az = TAU * f / nfr + rng.uniform(-0.3, 0.3)
        el = math.radians(rng.uniform(45, 70))
        L = rng.uniform(0.6, 0.95)
        d = Vector((math.cos(az) * math.cos(el), math.sin(az) * math.cos(el), math.sin(el)))
        pts, dirs = [Vector((0, 0, 0.02))], []
        for i in range(12):
            d = (d - UP * rng.uniform(0.08, 0.13)).normalized()
            dirs.append(d)
            pts.append(pts[-1] + d * L / 12)
        dirs.append(d)
        fronds.append((pts, dirs, L, math.radians(rng.uniform(-20, 20)), rng.random()))

    def frond_mesh(mb, pts, dirs, L, roll, step):
        idx = sub(list(range(len(pts))), step)
        W = L * 0.5
        mb.cur = (1.0, phase_of(pts[-1]))
        s_acc = 0.0
        rows = []
        for j, i in enumerate(idx):
            if j:
                s_acc += (pts[i] - pts[idx[j - 1]]).length
            side = Quaternion(dirs[i], roll) @ dirs[i].cross(UP).normalized()
            rows.append((pts[i] - side * W / 2, pts[i] + side * W / 2, s_acc / L))
        for j in range(len(rows) - 1):
            (a0, a1, u0), (b0, b1, u1) = rows[j], rows[j + 1]
            mb.face_ds([a0, b0, b1, a1], m, [(u0, 0), (u1, 0), (u1, 1), (u0, 1)])

    lods = []
    for lod, (step, keep) in enumerate([(2, 1.0), (4, 0.7)]):
        mb = MB()
        for pts, dirs, L, roll, key in fronds:
            if key < keep:
                frond_mesh(mb, pts, dirs, L, roll, step)
        lods.append(mb.build(f"{name}_LOD{lod}"))
    lods.append(impostor(name, lods[0]).build(f"{name}_LOD2"))
    return lods


def grass(seed, name):
    rng = random.Random(seed)
    m = export_mat("Card_Grass")
    h = rng.uniform(0.35, 0.5)
    w = h * 2 * rng.uniform(0.6, 0.8)
    a0 = rng.uniform(0, TAU)
    lods = []
    for lod, n in enumerate([4, 2]):
        mb = MB()
        r2 = random.Random(seed)
        for k in range(n):
            a = a0 + math.pi * k / n + r2.uniform(-0.15, 0.15)
            u = Vector((math.cos(a), math.sin(a), 0))
            card(mb, Vector((0, 0, -0.02)) - u * w / 2, u, UP, w, h, m, segs=2, bend=r2.uniform(-0.15, 0.15))
        lods.append(mb.build(f"{name}_LOD{lod}"))
    return lods


# ===================================================================== vines
def hanging_vines(seed, name):
    rng = random.Random(seed)
    m = export_mat("Card_Vine")
    stem = export_mat("Bark_Stem")
    W = rng.uniform(1.0, 2.0)
    runner = [Vector((lerp(-W / 2, W / 2, i / 10), rng.uniform(-0.03, 0), rng.uniform(-0.02, 0.02))) for i in range(11)]
    strands = []

    def strand(start, L):
        N = max(4, int(L / 0.07))
        ph = rng.uniform(0, TAU)
        amp = rng.uniform(0.03, 0.08)
        pts = [start + Vector((math.sin(ph + i / N * 6) * amp * i / N, -0.06 * i / N - 0.12 * (i / N) ** 2, -L * i / N)) for i in range(N + 1)]
        strands.append(pts)
        return pts

    ns = rng.randint(4, 7)
    for s in range(ns):
        x = lerp(-W / 2, W / 2, (s + rng.uniform(0.2, 0.8)) / ns)
        L = rng.uniform(0.8, 2.6)
        pts = strand(Vector((x, -0.01, 0)), L)
        for _ in range(rng.randint(0, 2)):
            strand(pts[rng.randint(len(pts) // 4, len(pts) // 2)], L * rng.uniform(0.25, 0.45))

    RW = 0.2  # ribbon width; texture is 1:4 so one tile = 0.8 m of strand
    def ribbon(mb, pts, side_fn):
        mb.cur = (1.0, phase_of(pts[0]))
        s = 0.0
        for i in range(len(pts) - 1):
            seg = (pts[i + 1] - pts[i]).length
            a, b = side_fn(pts, i), side_fn(pts, i + 1)
            v0, v1 = 1 - s / (RW * 4), 1 - (s + seg) / (RW * 4)
            mb.face_ds([pts[i] - a * RW / 2, pts[i] + a * RW / 2, pts[i + 1] + b * RW / 2, pts[i + 1] - b * RW / 2], m,
                       [(0, v0), (1, v0), (1, v1), (0, v1)])
            s += seg

    def side_x(pts, i):
        return Vector((1, 0, 0))

    def side_y(pts, i):
        return Vector((0, 1, 0))

    lods = []
    for lod in range(2):
        mb = MB()
        tube(mb, runner, [0.014] * 11, [5, 3][lod], stem)
        for pts in strands:
            p = sub(pts, [2, 4][lod])
            ribbon(mb, p, side_x)
            if lod == 0:
                ribbon(mb, p, side_y)
        lods.append(mb.build(f"{name}_LOD{lod}"))
    lods.append(impostor(name, lods[0], mode="front").build(f"{name}_LOD2"))
    return lods


# ======================================================================= ivy
def grow_ivy(rng, W, H, wrap, roots, max_len, step=0.04, branch_p=0.07):
    walkers = [dict(u=lerp(0, W, (i + rng.uniform(0.2, 0.8)) / roots), v=-0.02,
                    a=math.pi / 2 + rng.uniform(-0.4, 0.4), left=max_len * rng.uniform(0.6, 1.0), g=0)
               for i in range(roots)]
    paths = []
    while walkers:
        w = walkers.pop()
        path = [(w["u"], w["v"])]
        u, v, a = w["u"], w["v"], w["a"]
        while w["left"] > 0:
            a += rng.gauss(0, 0.35) + (math.pi / 2 - a) * 0.08
            u += math.cos(a) * step
            v += math.sin(a) * step
            if wrap:
                u %= W
            elif u < 0.02 or u > W - 0.02:
                a = math.pi - a
                u = min(max(u, 0.02), W - 0.02)
            if v > H or v < -0.05:
                break
            path.append((u, v))
            w["left"] -= step
            if rng.random() < branch_p and w["g"] < 4:
                walkers.append(dict(u=u, v=v, a=a + rng.choice([-1, 1]) * rng.uniform(0.6, 1.2),
                                    left=w["left"] * rng.uniform(0.4, 0.8), g=w["g"] + 1))
        if len(path) > 2:
            paths.append((path, w["g"]))
    return paths


def wall_surface(u, v):
    return Vector((u, 0, v)), Vector((0, -1, 0))


def pillar_surface(R):
    def f(u, v):
        n = Vector((math.cos(u / R), math.sin(u / R), 0))
        return n * R + Vector((0, 0, v)), n
    return f


def ivy(seed, name, surf, W, H, wrap, roots, max_len, wall):
    rng = random.Random(seed)
    m = export_mat("Card_Ivy")
    stem = export_mat("Bark_Stem")
    stems, cards = [], []
    for path, gen in grow_ivy(rng, W, H, wrap, roots, max_len):
        r0 = 0.014 * (0.65 ** gen)
        pts, nrms = [], []
        for u, v in path:
            p, n = surf(u - (W / 2 if not wrap else 0), v)
            pts.append(p + n * (r0 + 0.004))
            nrms.append(n)
        stems.append((pts, r0))
        for i in range(1, len(pts), 2):
            n = nrms[i]
            t = (pts[i] - pts[i - 1]).normalized()
            nn = (n + UP * 0.25 + rvec(rng, 0.3)).normalized()
            v = orth(t * 0.6 + UP * 0.4 + n * 0.2 + rvec(rng, 0.5), nn)
            s = rng.uniform(0.17, 0.25) * lerp(1.1, 0.8, i / len(pts))
            cards.append(dict(base=pts[i] + n * 0.01 - v * s * 0.2, u=v.cross(nn), v=v, s=s, key=rng.random()))
    lods = []
    for lod in range(3):
        if lod == 2 and wall:
            break
        mb = MB()
        if lod == 0:
            for pts, r0 in stems:
                sp = sub(pts, 2)
                tube(mb, sp, [lerp(r0, 0.003, i / max(1, len(sp) - 1)) for i in range(len(sp))], 3, stem)
        emit_cards(mb, cards, lod, lambda c: m)
        lods.append(mb.build(f"{name}_LOD{lod}"))
    if wall:
        lods.append(impostor(name, lods[0], mode="wall").build(f"{name}_LOD2"))
    return lods


# ====================================================================== export
def tri_count(ob):
    me = ob.data
    me.calc_loop_triangles()
    return len(me.loop_triangles)


def export_fbx(objs, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True,
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE",
                             use_tspace=True, add_leaf_bones=False, bake_anim=False, path_mode="RELATIVE",
                             colors_type="LINEAR")


ASSETS = {}  # category -> list of (name, [lod objs])
REPORT = []


def wind_weights(category, name, lods):
    """Fill vertex colour R (sway weight) from height. Taller parts sway more; trunk base is fixed."""
    zs = [v.co.z for v in lods[0].data.vertices]
    top, bottom = max(zs), min(zs)
    for o in lods:
        me = o.data
        col = me.color_attributes["Col"]
        data = [0.0] * (len(me.vertices) * 4)
        col.data.foreach_get("color", data)
        for i, v in enumerate(me.vertices):
            z = v.co.z
            if category == "Vines":                       # hangs from the top: free ends swing most
                r = 0.8 * min(1.0, max(0.0, -z / max(1e-3, -bottom))) ** 1.2
            elif category == "Ivy":                       # clings to a wall: barely moves
                r = 0.08
            elif category == "Bushes":
                k = 0.5 if name.startswith("Grass") else 0.35
                r = k * min(1.0, max(0.0, z / max(1e-3, top))) ** 1.2
            else:                                         # trees
                r = min(1.0, max(0.0, z / max(1e-3, top))) ** 1.6
            data[i * 4] = r
        col.data.foreach_set("color", data)
        me.update()


def register(category, name, lods):
    wind_weights(category, name, lods)
    export_fbx(lods, os.path.join(OUT, category, name + ".fbx"))
    tris = [tri_count(o) for o in lods]
    REPORT.append((category, name, tris))
    print(f"[flora] {category}/{name}.fbx: triangles per LOD {tris}", flush=True)
    ASSETS.setdefault(category, []).append((name, lods))
    for o in lods:  # park far away so later bakes/renders start clean
        o.location.x += 1000


def S(tag, i):
    return f"{ARGS.seed}-{tag}-{i}"


def build_all():
    def want(category, kind):
        return category in CATS and kind in KINDS

    print("[flora] seeds " + "; ".join(f"{k}={','.join(map(str, v))}" for k, v in SEEDS.items() if k in KINDS), flush=True)
    if want("trees", "oak"):
        for i, n in enumerate(SEEDS["oak"]):
            register("Trees", f"Tree_Oak_{i+1:02d}", broadleaf_tree(S("oak", n), f"Tree_Oak_{i+1:02d}", OAK))
    if want("trees", "pine"):
        for i, n in enumerate(SEEDS["pine"]):
            register("Trees", f"Tree_Pine_{i+1:02d}", pine_tree(S("pine", n), f"Tree_Pine_{i+1:02d}", 7.5))
    if want("trees", "birch"):
        for i, n in enumerate(SEEDS["birch"]):
            register("Trees", f"Tree_Birch_{i+1:02d}", broadleaf_tree(S("birch", n), f"Tree_Birch_{i+1:02d}", BIRCH))
    if want("trees", "dead"):
        for i, n in enumerate(SEEDS["dead"]):
            register("Trees", f"Tree_Dead_{i+1:02d}", dead_tree(S("dead", n), f"Tree_Dead_{i+1:02d}"))
    if want("bushes", "bush"):
        for i, n in enumerate(SEEDS["bush"]):
            register("Bushes", f"Bush_Round_{i+1:02d}", bush(S("bush", n), f"Bush_Round_{i+1:02d}"))
    if want("bushes", "flowering"):
        for fk, n in zip(["Pink", "White", "Yellow", "Purple"], SEEDS["flowering"]):
            register("Bushes", f"Bush_Flowering_{fk}", bush(S("fbush", n), f"Bush_Flowering_{fk}", fk))
    if want("bushes", "fern"):
        for i, n in enumerate(SEEDS["fern"]):
            register("Bushes", f"Fern_{i+1:02d}", fern(S("fern", n), f"Fern_{i+1:02d}"))
    # No grass clumps: the levels' grass is the lawns' (GrassField), made on the GPU.
    if want("vines", "vine"):
        for i, n in enumerate(SEEDS["vine"]):
            register("Vines", f"Vines_Hanging_{i+1:02d}", hanging_vines(S("vine", n), f"Vines_Hanging_{i+1:02d}"))
    if want("ivy", "ivywall"):
        shapes = [(1.5, 2.0, 4), (3.0, 2.5, 7), (1.0, 3.0, 3)]
        for i, n in enumerate(SEEDS["ivywall"]):
            W, H, roots = shapes[i % len(shapes)]
            name = f"Ivy_Wall_{i+1:02d}"
            register("Ivy", name, ivy(S("ivywall", n), name, wall_surface, W, H, False, roots, H * 1.6, True))
    if want("ivy", "ivypillar"):
        R = 0.3
        for n in SEEDS["ivypillar"][:1]:
            register("Ivy", "Ivy_Pillar_R30cm", ivy(S("ivypillar", n), "Ivy_Pillar_R30cm", pillar_surface(R), TAU * R, 2.8, True, 5, 4.0, False))


# ===================================================================== previews
def render_previews():
    sc = bpy.context.scene
    sc.render.film_transparent = False
    set_view(False)
    sc.view_settings.view_transform = "AgX" if "AgX" in [i.identifier for i in sc.view_settings.bl_rna.properties["view_transform"].enum_items] else "Filmic"
    world = bpy.data.worlds.new("W")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.65, 0.8, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.7
    sc.world = world
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 4
    sun.data.angle = math.radians(4)
    sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
    sc.collection.objects.link(sun)
    cam = camera()
    cam.data.type = "PERSP"
    cam.data.lens = 50
    sc.camera = cam
    sc.render.resolution_x, sc.render.resolution_y = 1800, 800
    sc.eevee.gtao_distance = 1.0
    gm = bpy.data.materials.new("GroundPreview")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.3, 0.26, 0.18, 1)
    wm = bpy.data.materials.new("WallPreview")
    wm.use_nodes = True
    wm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.55, 0.5, 0.45, 1)
    lm = bpy.data.materials.new("LabelPreview")
    lm.use_nodes = True
    lm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.02, 0.02, 0.02, 1)

    def shoot(path, objs, extras_fn=None, wall=False, hang=False):
        for o in sc.objects:
            if o.type == "MESH":
                o.hide_render = o not in objs
        (x0, x1), _, (z0, z1) = bounds(objs)
        W, H = x1 - x0, z1 - z0
        extras = []
        bpy.ops.mesh.primitive_plane_add(size=1, location=((x0 + x1) / 2, 0, z0 if hang else 0))
        g = bpy.context.active_object
        g.scale = (W * 3 + 10, 60, 1)
        g.data.materials.append(gm)
        extras.append(g)
        if wall:
            bpy.ops.mesh.primitive_plane_add(size=1, location=((x0 + x1) / 2, 0.02, 0), rotation=(math.pi / 2, 0, 0))
            wl = bpy.context.active_object
            wl.scale = (W * 3 + 10, 10, 1)
            wl.data.materials.append(wm)
            extras.append(wl)
        if extras_fn:
            extras += extras_fn()
        fov = 2 * math.atan(18 / cam.data.lens)
        dist = max((W / 2) / math.tan(fov / 2), (H * 1.1) / math.tan(fov / 2) * (1800 / 800) / 2) * 1.12
        target = Vector(((x0 + x1) / 2, 0, z0 + H * 0.45))
        cam.location = target + Vector((0, -dist, dist * 0.12))
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        for e in extras:
            bpy.data.objects.remove(e, do_unlink=True)
        print("  preview", os.path.basename(path), flush=True)

    pdir = os.path.join(os.path.dirname(OUT), "Previews", "LeafCards")
    os.makedirs(pdir, exist_ok=True)
    for cat, items in ASSETS.items():
        x = 0.0
        objs = []
        pillar = None
        for name, lods in items:
            o = lods[0]
            o.location = (0, 0, 0)
            (a, b), _, (lo, hi) = bounds([o])
            x += -a + 0.4
            o.location = (x, 0, 0)
            x += b + 0.4
            objs.append(o)
            if "Pillar" in name:
                pillar = o
        hang = cat == "Vines"
        if hang:
            top = -min(bounds([o])[2][0] for o in objs) + 0.1
            for o in objs:
                o.location.z = top
        extra = None
        if pillar:
            pillar.location.y = -1.2

            def extra():
                bpy.ops.mesh.primitive_cylinder_add(radius=0.29, depth=3.2, location=(pillar.location.x, -1.2, 1.6))
                c = bpy.context.active_object
                c.data.materials.append(wm)
                return [c]
        if hang:
            def extra():
                (x0, x1), _, (z0, z1) = bounds(objs)
                bpy.ops.mesh.primitive_cube_add(size=1, location=((x0 + x1) / 2, 0.25, z1 + 0.15))
                c = bpy.context.active_object
                c.scale = (x1 - x0 + 0.4, 0.6, 0.3)
                c.data.materials.append(gm)
                return [c]
        shoot(os.path.join(pdir, f"{cat}.png"), objs, extra, wall=cat == "Ivy")
        for o in objs:
            o.location = (1000, 0, 0)

    # LOD comparison sheets
    for cat, pick in (("Trees", "Tree_Oak_01"), ("Trees", "Tree_Pine_01"), ("Bushes", "Bush_Flowering_Pink"),
                      ("Bushes", "Fern_01"), ("Ivy", "Ivy_Wall_01")):
        hit = [l for n, l in ASSETS.get(cat, []) if n == pick]
        if not hit:
            continue
        lods = hit[0]
        x = 0.0
        labels = []
        for i, o in enumerate(lods):
            o.location = (0, 0, 0)
            (a, b), _, _ = bounds([o])
            x += -a + 0.3
            o.location = (x, 0, 0)
            cu = bpy.data.curves.new(f"lbl{i}", "FONT")
            cu.body = f"LOD{i}  {tri_count(o)} tris"
            cu.align_x = "CENTER"
            cu.size = max(0.12, (b - a) * 0.09)
            t = bpy.data.objects.new(f"lbl{i}", cu)
            t.data.materials.append(lm)
            t.location = (x + (a + b) / 2, -0.6, 0.02)
            t.rotation_euler = (math.radians(75), 0, 0)
            sc.collection.objects.link(t)
            labels.append(t)
            x += b + 0.3

        def lbls():
            return labels
        shoot(os.path.join(pdir, f"LODs_{pick}.png"), lods, lbls, wall=cat == "Ivy")
        for o in lods:
            o.location = (1000, 0, 0)


if __name__ == "__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.eevee.taa_render_samples = 16
    sc.eevee.use_gtao = True
    sc.eevee.use_soft_shadows = True
    os.makedirs(TEXDIR, exist_ok=True)
    print("[flora] baking textures", flush=True)
    bark_textures()
    card_textures()
    print("[flora] building assets", flush=True)
    build_all()
    if not ARGS.no_render:
        render_previews()
    for m in list(bpy.data.materials):
        if m.name.startswith("PASS_") or m.name.startswith("SRC_"):
            bpy.data.materials.remove(m)
    for cat, items in ASSETS.items():
        for _, lods in items:
            for o in lods:
                o.location = (0, 0, 0)
                o.hide_render = False
                o.hide_viewport = not o.name.endswith("_LOD0")
    shutil.rmtree(TMP, ignore_errors=True)
    if ARGS.render:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(OUT), "FloraLibrary.blend"), relative_remap=True)
    print(f"[flora] {len(REPORT)} assets", flush=True)
    print("[flora] exported", flush=True)
