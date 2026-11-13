"""Generates the Meridian Basin level spec: a variant of the Meridian Complex sunk into a terraced octagonal pit.

An example of writing a level spec with code, where a layout repeats (eight faces on each of five terrace rings) or is
worked out from the movement's limits. Run it from the project folder:

    python3 Tools/LevelSpec/examples/meridian_basin.py            # writes Assets/Levels/MeridianBasin.level.json
    python3 Tools/LevelSpec/levelspec check Assets/Levels/MeridianBasin.level.json

The layout (metres, +z north, the ground at 0), after the terraced basin schematic in ReferenceAssets:

- Six levels, 9 m apart, stepping down to the middle: L0 Central Park (-45, the floor, 52 m across) and five
  terrace rings round it, each 12 to 16 m deep: L1 Kinetic Gardens (-36), L2 Research Terraces (-27), L3 Calibration
  Annex (-18), L4 Fabrication and Suspension (-9) and L5 Operations Ring (0, the rim, 184 m across).
- The terraces are solid, so their interiors are carved out of them: a room on level k has its door in the 9 m wall
  (the riser) between terraces k and k+1, and runs back under terrace k+1 and often k+2. Three kinds:
  - halls, two storeys: a front room under terrace k+1 opening into a 17.5 m hall under terrace k+2, with a gallery
    (reached by a stair or a ledge climb) and a balcony door from it out onto terrace k+1, so a hall is a way up too;
  - deep rooms, one storey but 23 to 29 m deep, under both terraces, some divided by partitions;
  - small rooms under terrace k+1 only, with a stair or a ledge climb up a stairwell through the deck above (a hatch).
  Their front walls have windows (sills 1.2 m up, so they can be jumped through) as well as doors.
- A riser is 9 m: too high to climb. Each has one stair, and otherwise the ways up are platforming: a block to jump
  to the ledge from (4.5 m, then 4.5 m), two 3 m steps (double jumps), lifts, the halls and the hatches. Some
  terraces are cut by gaps graded to the jumps, so getting round a level takes platforming too.
- The Ascension Spire stands in the park: a spiral of ledges 3 m apart (double jumps) round its core, from the park to
  its summit 15 m above the rim, with a bridge from it to each terrace (one broken by a gap, one crossed on a shuttle).
- The only tunnel is a shortcut: the Service Lift, from the park through the terrace walls to a lift up to L3.

Every piece's footprint keeps its edges at multiples of 45° (the level's box colliders fill only those): each ring is
eight rectangles, one per face, and eight corner pieces between them, not eight trapezoids. Anything on a face is
built in that face's frame (a Turned group): x along the face, z out from the middle. A guardrail is placed in its
rails group's frame, so a rotated walkway's rails go in a group inside its own Turned group.
"""

import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
sys.path.insert(0, os.path.dirname(HERE))

import levelspec  # noqa: E402

T = math.tan(math.radians(22.5))
FACES = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"]  # Face k faces 45k degrees clockwise from north.
STEP_RISE, STEP_RUN = 0.3, 0.4

LEVEL = [-45.0, -36.0, -27.0, -18.0, -9.0, 0.0]  # Each level's floor.
EDGE = [None, 26.0, 38.0, 50.0, 62.0, 76.0, 92.0]  # Terrace k runs from EDGE[k] to EDGE[k+1] (apothems); the park to EDGE[1].
BOTTOM = -45.5
NAMES = ["Central Park", "Kinetic Gardens", "Research Terraces", "Calibration Annex", "Fabrication and Suspension", "Operations Ring"]
SPIRE_IN, SPIRE_OUT = 8.0, 14.0  # The spire's core, and its ledges' outer edge (apothems).
SPIRAL = [4, 5, 6, 7, 0, 1, 2, 3]  # The spire's ledges climb clockwise from the south: S, SW, W, NW, N, NE, E, SE.
DOOR, WINDOW, SILL, LINTEL = 4.0, 3.0, 1.2, 3.6

# The climbs on each riser (from level k up to k+1), face by face, standing on terrace k against it, with an offset
# along the face if they'd be in a door's way; "gap" cuts terrace k+1 on that face (its width). Faces with an
# interior's door in the riser and nothing else are "none".
RISERS = [
    {"N": ("none",), "NE": ("none",), "E": ("stair",), "SE": ("none",), "S": ("gap", 6.0), "SW": ("lift",), "W": ("none",), "NW": ("steps", -4)},
    {"N": ("ledge", -6), "NE": ("none",), "E": ("stair",), "SE": ("gap", 8.0), "S": ("none",), "SW": ("none",), "W": ("steps",), "NW": ("gap", 5.0)},
    {"N": ("none",), "NE": ("steps", -4), "E": ("gap", 6.0), "SE": ("none",), "S": ("none",), "SW": ("stair",), "W": ("none",), "NW": ("none",)},
    {"N": ("none",), "NE": ("ledge",), "E": ("none",), "SE": ("stair",), "S": ("gap", 9.0), "SW": ("none",), "W": ("steps",), "NW": ("lift", 7)},
    {"N": ("stair",), "NE": ("steps",), "E": ("none",), "SE": ("ledge",), "S": ("lift", 8), "SW": ("none",), "W": ("ledge",), "NW": ("steps",)},
]
# The interiors: face, level (its floor), kind, name, its door's and windows' offsets along the face, and for a hall
# or a small room how its climb goes ("stair" or "climb", a block to jump to the ledge from).
INTERIORS = [
    {"face": "N", "level": 0, "kind": "hall", "name": "Park Pavilion", "door": 0, "windows": [-6, 6], "climb": "stair"},
    {"face": "NE", "level": 0, "kind": "small", "name": "Seed Vault", "door": 0, "windows": [], "climb": "stair"},
    {"face": "SE", "level": 0, "kind": "deep", "name": "Habitat Greenhouse", "door": 0, "windows": [-6, 6]},
    {"face": "NW", "level": 0, "kind": "deep", "name": "Visitor Centre", "door": 6, "windows": [0]},
    {"face": "NE", "level": 1, "kind": "hall", "name": "Growth Lab", "door": -8, "windows": [0, 6], "climb": "climb"},
    {"face": "SW", "level": 1, "kind": "hall", "name": "Greenhouse", "door": 0, "windows": [-9, 9], "climb": "stair"},
    {"face": "N", "level": 2, "kind": "hall", "name": "Archives", "door": 0, "windows": [-10, 10], "climb": "climb"},
    {"face": "SE", "level": 2, "kind": "deep", "name": "Research Labs", "door": 10, "windows": [-12, 16]},
    {"face": "S", "level": 2, "kind": "deep", "name": "Commons", "door": 0, "windows": [-15, -8, 8, 15]},
    {"face": "E", "level": 3, "kind": "hall", "name": "Fabrication Hall", "door": -12, "windows": [-20, 6], "climb": "stair"},
    {"face": "W", "level": 3, "kind": "hall", "name": "Wall-Run Hall", "door": 12, "windows": [20], "climb": "climb"},
    {"face": "NE", "level": 3, "kind": "deep", "name": "Step and Slope Room", "door": -12, "windows": [-20, 8]},
    {"face": "NW", "level": 3, "kind": "deep", "name": "Observation Lab", "door": -12, "windows": [-20, -4, 16]},
    {"face": "SW", "level": 3, "kind": "small", "name": "Ledge Room", "door": 0, "windows": [-10, 10], "climb": "climb"},
    {"face": "SW", "level": 4, "kind": "small", "name": "Stores", "door": -10, "windows": [], "climb": "climb"},
]
# The spire's bridges: level, face (the face whose ledge is at that level), and how it's crossed.
BRIDGES = [(1, "W", "whole"), (2, "NE", "whole"), (3, "S", "gap"), (4, "NW", "shuttle"), (5, "E", "whole")]
# The Service Lift: a tunnel under the west terraces from the park to a shaft up to L3.
TUNNEL = {"x0": -2.0, "x1": 2.0, "z0": EDGE[1] + 0.5, "z1": EDGE[3] + 0.5, "floor": LEVEL[0], "ceiling": LEVEL[0] + 4}
SHAFT = {"x0": -3.0, "x1": 3.0, "z0": EDGE[3] + 0.5, "z1": EDGE[3] + 6.5, "floor": LEVEL[0], "top": LEVEL[3]}


def flight_length(y0, y1):
    return math.ceil(abs(y1 - y0) / STEP_RISE - 1e-4) * STEP_RUN


def octagon(apothem, cx=0.0, cz=0.0):
    r = apothem / math.cos(math.radians(22.5))
    return [[round(cx + r * math.cos(math.radians(22.5 + 45 * i)), 4), round(cz + r * math.sin(math.radians(22.5 + 45 * i)), 4)] for i in range(8)]


def r(v):
    return round(v, 4)


def half(apothem):
    """Half the width of an octagon's face at that apothem."""
    return apothem * T


def corner(a, b):
    """
    The piece of a ring (apothems a to b) between a face's rectangle and the next face's clockwise, in the first face's
    frame: bounded by the face's rectangle (x = aT), its outer flat (z = b), the next face's outer flat (at 45°) and the
    next face's rectangle (at 45°), so every edge is at a multiple of 45°.
    """
    d = b - a
    s = d / math.sqrt(2)
    return _ccw([[r(half(a)), r(a)], [r(half(a)), r(b)], [r(half(b)), r(b)], [r(half(a) + s), r(a + s)]])


def _ccw(points):
    """Points in counterclockwise order (x right, z up), as the pieces' outlines are."""
    area = sum(points[i][0] * points[(i + 1) % len(points)][1] - points[(i + 1) % len(points)][0] * points[i][1] for i in range(len(points)))
    return points if area > 0 else list(reversed(points))


def subtract(box, voids):
    """
    A box (x0, x1, z0, z1, y0, y1) less the voids in it, as the fewest boxes a greedy merge finds: cut on every void's
    faces, keep the cells outside every void, and grow each box up, then north, then east as far as the kept cells go.
    """
    eps = 1e-6
    clipped = []
    for v in voids:
        c = (max(box[0], v[0]), min(box[1], v[1]), max(box[2], v[2]), min(box[3], v[3]), max(box[4], v[4]), min(box[5], v[5]))
        if c[1] - c[0] > eps and c[3] - c[2] > eps and c[5] - c[4] > eps:
            clipped.append(c)
    if not clipped:
        return [box]
    xs = sorted({box[0], box[1], *(c for v in clipped for c in v[0:2])})
    zs = sorted({box[2], box[3], *(c for v in clipped for c in v[2:4])})
    ys = sorted({box[4], box[5], *(c for v in clipped for c in v[4:6])})

    def solid(i, j, k):
        cx, cz, cy = (xs[i] + xs[i + 1]) / 2, (zs[j] + zs[j + 1]) / 2, (ys[k] + ys[k + 1]) / 2
        return not any(v[0] < cx < v[1] and v[2] < cz < v[3] and v[4] < cy < v[5] for v in clipped)

    nx, nz, ny = len(xs) - 1, len(zs) - 1, len(ys) - 1
    keep = {(i, j, k) for i in range(nx) for j in range(nz) for k in range(ny) if solid(i, j, k)}
    out = []
    for i in range(nx):
        for j in range(nz):
            for k in range(ny):
                if (i, j, k) not in keep:
                    continue
                k1 = k
                while (i, j, k1 + 1) in keep:
                    k1 += 1
                j1 = j
                while all((i, j1 + 1, kk) in keep for kk in range(k, k1 + 1)):
                    j1 += 1
                i1 = i
                while all((i1 + 1, jj, kk) in keep for jj in range(j, j1 + 1) for kk in range(k, k1 + 1)):
                    i1 += 1
                for ii in range(i, i1 + 1):
                    for jj in range(j, j1 + 1):
                        for kk in range(k, k1 + 1):
                            keep.discard((ii, jj, kk))
                out.append((xs[i], xs[i1 + 1], zs[j], zs[j1 + 1], ys[k], ys[k1 + 1]))
    return out


class Spec:
    def __init__(self, name, scene):
        self.spec = {"format": "character-showcase-level", "version": 1, "name": name, "scene": scene, "parts": []}
        self.pieces = None

    def part(self, name):
        self.pieces = []
        self.spec["parts"].append({"name": name, "pieces": self.pieces})

    def add(self, piece, **settings):
        entry = {"piece": piece}
        entry.update({k: v for k, v in settings.items() if v is not None})
        self.pieces.append(entry)
        return entry


def room_width(k):
    """Half the width of a room under terrace k+1: a metre of wall left either side of it in the terrace's rectangle."""
    return r(half(EDGE[k + 1]) - 1.5)


def hatch(k):
    """A small room of level k's stairwell (x0, x1, z0, z1): along its back wall, at its east end, 12 m (one flight of 9 m) long."""
    w = room_width(k)
    back = EDGE[k + 2] - 0.5
    return (r(w - flight_length(0, 9)), w, r(back - 5), back)


def balcony(it):
    """A hall's balcony door (centre, width) in the riser above it: beside the front room, inside the hall."""
    k = it["level"]
    wf, wb = room_width(k), room_width(k + 1)
    width = min(DOOR, r(wb - wf - 0.7))
    return r((wf + 0.5 + wb) / 2), width


def cells_of(it):
    """An interior's cells (in its face's frame), and its open cells' top."""
    k, kind, name = it["level"], it["kind"], it["name"]
    y, up = LEVEL[k], LEVEL[k + 1]
    wf, front = room_width(k), EDGE[k + 1] + 0.5
    if kind == "hall":
        wb, z = room_width(k + 1), EDGE[k + 2] + 0.5
        return [
            {"name": name, "x0": -wf, "z0": front, "x1": wf, "z1": z, "floor": y, "ceiling": up - 0.5},
            {"name": f"{name} (Hall)", "x0": -wb, "z0": z, "x1": wb, "z1": EDGE[k + 3] - 0.5, "floor": y, "ceiling": LEVEL[k + 2] - 0.5},
        ], up
    if kind == "deep":
        return [{"name": name, "x0": -wf, "z0": front, "x1": wf, "z1": EDGE[k + 3] - 0.5, "floor": y, "ceiling": up - 0.5}], up
    if it.get("climb"):
        hx0, hx1, hz0, hz1 = hatch(k)
        return [
            {"name": name, "x0": -wf, "z0": front, "x1": wf, "z1": hz0, "floor": y, "ceiling": up - 0.5},
            {"name": f"{name} (Back)", "x0": -wf, "z0": hz0, "x1": hx0, "z1": hz1, "floor": y, "ceiling": up - 0.5},
            {"name": f"{name} Stairwell", "x0": hx0, "z0": hz0, "x1": hx1, "z1": hz1, "floor": y, "ceiling": up, "open": True},
        ], up
    return [{"name": name, "x0": -wf, "z0": front, "x1": wf, "z1": EDGE[k + 2] - 0.5, "floor": y, "ceiling": up - 0.5}], up


def envelope(cell, open_top):
    """What a cell takes out of the terraces: its area and walls, from under its floor slab to its top."""
    top = open_top if cell.get("open") else cell["ceiling"] + 0.5
    return (cell["x0"] - 0.5, cell["x1"] + 0.5, cell["z0"] - 0.5, cell["z1"] + 0.5, cell["floor"] - 0.5, top)


def face_voids(face):
    """Everything cut from the terraces on a face, each with what it's for: interiors, gaps, the Service Lift."""
    voids = []
    for it in INTERIORS:
        if it["face"] == face:
            cells, open_top = cells_of(it)
            voids += [(envelope(c, open_top), it["name"]) for c in cells]
    for k in range(5):
        role = RISERS[k][face]
        if role[0] == "gap":
            a, b = EDGE[k + 1], EDGE[k + 2]
            voids.append(((-role[1] / 2, role[1] / 2, a, b, LEVEL[k], LEVEL[k + 1]), f"gap on L{k + 1}"))
    if face == "W":
        t, s = TUNNEL, SHAFT
        voids.append(((t["x0"] - 0.5, t["x1"] + 0.5, t["z0"] - 0.5, t["z1"], t["floor"] - 0.5, t["ceiling"] + 0.5), "Service Lift"))
        voids.append(((s["x0"] - 0.5, s["x1"] + 0.5, s["z0"] - 0.5, s["z1"] + 0.5, s["floor"] - 0.5, s["top"]), "Service Lift"))
    return voids


def check_layout():
    """Interiors, gaps and the tunnel mustn't overlap each other (touching is fine), and each must fit its terraces' rectangles."""
    for face in FACES:
        voids = face_voids(face)
        for i, (a, na) in enumerate(voids):
            for b, nb in voids[i + 1:]:
                if na == nb:
                    continue
                if all(min(a[2 * d + 1], b[2 * d + 1]) - max(a[2 * d], b[2 * d]) > 1e-6 for d in range(3)):
                    raise ValueError(f"{face}: {na} overlaps {nb}")
        for v, name in voids:
            for level in range(1, 6):
                a, b = EDGE[level], EDGE[level + 1]
                if min(v[3], b) - max(v[2], a) > 1e-6 and max(abs(v[0]), abs(v[1])) > half(a) + 1e-6:
                    raise ValueError(f"{face}: {name} is wider than terrace L{level}'s rectangle")


def build():
    check_layout()
    s = Spec("Meridian Basin", "Assets/Scenes/MeridianBasin/MeridianBasin.unity")
    A = s.add

    def turned(parent, name, k):
        A("Turned", parent=parent, name=name, yaw=45 * k)
        return f"{parent}/{name}" if parent else name

    # ---- L0 Central Park: the basin's floor ----
    s.part("L0 Central Park")
    g0 = "L0 Central Park"
    A("Group", parent="", name=g0)
    A("Prism", parent=g0, name="Park Floor (-45)", shape=octagon(EDGE[1]), y0=BOTTOM, y1=LEVEL[0], surface="Ground")
    A("GuideLine", parent=g0, name="Park Walk (Orange)", points=octagon(18), height=LEVEL[0], surface="Accent", closed=True)
    A("GuideLine", parent=g0, name="Park Walk (White)", points=octagon(18.6), height=LEVEL[0], surface="White", closed=True)
    A("Group", parent=g0, name="Spawn Points")
    for name, at, yaw in (("Park South", [0, -21], 0), ("Park East", [21, 0], 270), ("Park West", [-21, 4], 90)):
        A("SpawnMarker", parent=f"{g0}/Spawn Points", name=name, at=at, height=LEVEL[0] + 0.1, yaw=yaw)
    A("AreaSign", parent=g0, at=[-7, -20], bottom=LEVEL[0] + 1.2, facing=[0, -1], number="L0", name=NAMES[0], freeStanding=True)

    # ---- The terraces, L1 to L5, less the interiors in them, with the climbs up to each ----
    for level in range(1, 6):
        k = level - 1
        part = f"L{level} {NAMES[level]}"
        s.part(part)
        A("Group", parent="", name=part)
        a, b, top = EDGE[level], EDGE[level + 1], LEVEL[level]
        for f, face in enumerate(FACES):
            fg = turned(part, f"Face {face}", f)
            voids = [v for v, _ in face_voids(face)]
            for i, (x0, x1, z0, z1, y0, y1) in enumerate(subtract((-r(half(a)), r(half(a)), a, b, BOTTOM, top), voids)):
                A("Block", parent=fg, name=f"Terrace {face} ({top:+g}) {i + 1}", x0=r(x0), x1=r(x1), z0=r(z0), z1=r(z1), y0=y0, y1=y1)
            A("Prism", parent=fg, name=f"Terrace Corner {face}-{FACES[(f + 1) % 8]} ({top:+g})", shape=corner(a, b), y0=BOTTOM, y1=top)
            role = RISERS[k][face]
            if role[0] == "gap":
                g = role[1]
                for x0, x1 in ((-r(half(a)), -g / 2), (g / 2, r(half(a)))):
                    A("HazardStripes", parent=fg, a=[x0, a], b=[x1, a], inward=[0, 1], height=top)
                for sx in (-1, 1):
                    A("HazardStripes", parent=fg, a=[sx * g / 2, a], b=[sx * g / 2, b], inward=[sx, 0], height=top)
                A("FloorLabel", parent=fg, text=f"GAP {g:g} m", at=[-g / 2 - 2.5, (a + b) / 2], height=top, facing=[0, -1])
            else:
                A("HazardStripes", parent=fg, a=[-r(half(a)), a], b=[r(half(a)), a], inward=[0, 1], height=top)
            _riser(A, fg, k, role)
        A("AreaSign", parent=part, at=[-8, a + 3], bottom=top + 1.2, facing=[0, -1], number=f"L{level}", name=NAMES[level], freeStanding=True)
        A("Banner", parent=part, wallPoint=[9, a], outward=[0, -1], top=top - 0.4, length=6, number=f"L{level}")

    # ---- The interiors carved out of the terraces ----
    s.part("Interiors")
    A("Group", parent="", name="Interiors")
    for it in INTERIORS:
        ig = turned("Interiors", f"L{it['level']} {it['name']} ({it['face']})", FACES.index(it["face"]))
        _interior(A, ig, it)

    # ---- Level dressing: lawns and trees on the gardens, containers in the yard, the operations buildings ----
    s.part("Dressing")
    A("Group", parent="", name="Dressing")
    d = "Dressing"
    A("Lawns", parent=d, area=[-EDGE[1], -EDGE[1], EDGE[1], EDGE[1]], floors=[LEVEL[0]], salt=401)
    A("ScatterTrees", parent=d, area=[-EDGE[1], -EDGE[1], EDGE[1], EDGE[1]], floor=LEVEL[0], salt=402, spread=1.6)
    A("Lawns", parent=d, area=[-EDGE[2], -EDGE[2], EDGE[2], EDGE[2]], floors=[LEVEL[1]], salt=411)
    A("ScatterTrees", parent=d, area=[-EDGE[2], -EDGE[2], EDGE[2], EDGE[2]], floor=LEVEL[1], salt=412, spread=1.3)
    A("Lawns", parent=d, area=[-EDGE[3], -EDGE[3], EDGE[3], EDGE[3]], floors=[LEVEL[2]], salt=421, spacing=1.5, density=0.6)
    yard = turned(d, "Suspension Yard (W)", 6)
    for i, (x0, z0, stacked) in enumerate(((-24, 63.0, True), (-24, 66.4, False), (-8, 63.0, False), (6, 63.0, True), (6, 66.4, False))):
        A("Block", parent=yard, name=f"Container {i + 1}", x0=x0, x1=x0 + 12, z0=z0, z1=z0 + 2.6, y0=LEVEL[4], y1=LEVEL[4] + 2.6, surface="Steel")
        if stacked:
            A("Block", parent=yard, name=f"Container {i + 1} (Upper)", x0=x0 + 1, x1=x0 + 13, z0=z0, z1=z0 + 2.6, y0=LEVEL[4] + 2.6, y1=LEVEL[4] + 5.2, surface="Accent")
    yard2 = turned(d, "Suspension Yard (SW)", 5)
    for i, (x0, z0) in enumerate(((-20, 64.0), (-2, 64.0))):
        A("Block", parent=yard2, name=f"Container {i + 6}", x0=x0, x1=x0 + 12, z0=z0, z1=z0 + 2.6, y0=LEVEL[4], y1=LEVEL[4] + 2.6, surface="Steel")
    # The operations buildings on the rim's diagonal faces: offices with a door to the basin and a roof to climb to.
    for f in (1, 3, 5, 7):
        og = turned(d, f"Operations {FACES[f]}", f)
        cells = [{"name": f"Operations Office {FACES[f]}", "x0": -10, "z0": 81, "x1": 10, "z1": 88, "floor": 0.3, "ceiling": 5}]
        A("Cells", parent=og, cells=cells, portals=[[-2, 80.5, 2, 81]], openTop=-1)
        A("Block", parent=og, name="Door Header", x0=-2, x1=2, z0=80.5, z1=81, y0=4.3, y1=5.5, surface="Panel")
        A("CellLighting", lighting=og, cells=cells)
        A("Platform", parent=og, name="Roof Step (+3)", x0=10.5, x1=13.5, z0=81, z1=84, y0=0, top=3, coping="Accent")
        A("FloorLabel", parent=og, text="ROOF: DOUBLE JUMP", at=[12, 79], height=0, facing=[0, -1])
    for x, z in ((96, 96), (-96, 96), (-96, -96), (96, -96)):
        A("Floodlight", parent=d, at=[x, z], floor=0, facing=[-x / 96, -z / 96])

    # ---- The Ascension Spire: a spiral of ledges round a core, bridged to every terrace ----
    s.part("Ascension Spire")
    sp = "Ascension Spire"
    A("Group", parent="", name=sp)
    summit = 15.0
    A("Prism", parent=sp, name=f"Core (Summit, {summit:+g})", shape=octagon(SPIRE_IN), y0=BOTTOM, y1=summit, surface="Panel")
    A("Column", parent=sp, name="Mast", centre=[0, 0], size=[2, 2], y0=summit, y1=summit + 24, accent=True)
    A("Group", parent=sp, name="Ledges")
    for f, face in enumerate(FACES):
        turned(f"{sp}/Ledges", f"Face {face}", f)
    n, h = 1, LEVEL[0] + 3
    while h < summit:
        f = SPIRAL[(n - 1) % 8]
        lg = f"{sp}/Ledges/Face {FACES[f]}"
        A("Block", parent=lg, name=f"Ledge {n} ({h:+g})", x0=-r(half(SPIRE_IN)), x1=r(half(SPIRE_IN)), z0=SPIRE_IN, z1=SPIRE_OUT, y0=h - 1, y1=h)
        A("Prism", parent=lg, name=f"Ledge {n} Corner ({h:+g})", shape=corner(SPIRE_IN, SPIRE_OUT), y0=h - 1, y1=h)
        A("FloorLabel", parent=lg, text=f"{h:+g}", at=[0, (SPIRE_IN + SPIRE_OUT) / 2], height=h, facing=[0, -1])
        n, h = n + 1, h + 3
    A("Group", parent=sp, name="Bridges")
    for level, face, kind in BRIDGES:
        f = FACES.index(face)
        bg = turned(f"{sp}/Bridges", f"Bridge to L{level} ({face})", f)
        # The rails go in the bridge's own (turned) frame, as the walkway's points are.
        A("Group", parent=bg, name="Guardrails")
        rails = f"{bg}/Guardrails"
        y, far = LEVEL[level], EDGE[level]
        length = far - SPIRE_OUT
        if kind == "whole":
            A("Walkway", parent=bg, rails=rails, start=[0, SPIRE_OUT], direction=[0, 1], length=length, width=3, height=y, suspended=True)
        elif kind == "gap":
            # Broken in the middle: a 6 m sprint jump.
            span = (length - 6) / 2
            A("Walkway", parent=bg, rails=rails, start=[0, SPIRE_OUT], direction=[0, 1], length=span, width=3, height=y, suspended=True)
            A("Walkway", parent=bg, rails=rails, start=[0, far - span], direction=[0, 1], length=span, width=3, height=y, suspended=True)
            A("FloorLabel", parent=bg, text="GAP 6 m", at=[0, SPIRE_OUT + span - 1.5], height=y, facing=[0, 1])
        else:
            # A shuttle between two short spans: 4 m long, 0.5 m from each end of its run.
            span = (length - 16) / 2
            A("Walkway", parent=bg, rails=rails, start=[0, SPIRE_OUT], direction=[0, 1], length=span, width=3, height=y, suspended=True)
            A("Walkway", parent=bg, rails=rails, start=[0, far - span], direction=[0, 1], length=span, width=3, height=y, suspended=True)
            A("MovingPlatform", parent=bg, label="Spire Shuttle", top=[0, y, SPIRE_OUT + span + 2.5], size=[3, 0.5, 4], travel=[0, 0, 11],
              period=6, pause=1)
    A("TimingGate", parent=sp, course="Spire Climb", role="start", a=[-4, -17], b=[4, -17], floor=LEVEL[0], approach=[0, 1])
    A("TimingGate", parent=sp, course="Spire Climb", role="finish", a=[-5, -4], b=[5, -4], floor=summit, approach=[0, 1], display=True)
    A("Banner", parent=sp, wallPoint=[0, -SPIRE_IN], outward=[0, -1], top=LEVEL[0] + 14, length=8, number="01")

    # ---- The Basin Ascent: timed from the park to the rim, by any route ----
    s.part("Basin Ascent")
    A("Group", parent="", name="Basin Ascent")
    A("TimingGate", parent="Basin Ascent", course="Basin Ascent", role="start", a=[-4, 17], b=[4, 17], floor=LEVEL[0], approach=[0, 1])
    A("TimingGate", parent="Basin Ascent", course="Basin Ascent", role="finish", a=[-4, 84], b=[4, 84], floor=0, approach=[0, 1], display=True)
    A("SpawnMarker", parent="Basin Ascent", name="Rim South", at=[0, -86], height=0.1, yaw=0)

    # ---- The Service Lift: the only tunnel, a shortcut from the park to L3 ----
    s.part("Service Lift")
    A("Group", parent="", name="Service Lift")
    lg = turned("Service Lift", "West", 6)
    t, sh = TUNNEL, SHAFT
    cells = [
        {"name": "Service Tunnel", "x0": t["x0"], "z0": t["z0"], "x1": t["x1"], "z1": t["z1"], "floor": t["floor"], "ceiling": t["ceiling"]},
        {"name": "Lift Shaft", "x0": sh["x0"], "z0": sh["z0"], "x1": sh["x1"], "z1": sh["z1"], "floor": sh["floor"], "ceiling": sh["top"], "open": True},
    ]
    A("Cells", parent=lg, cells=cells, portals=[[t["x0"], t["z0"] - 0.5, t["x1"], t["z0"]]], openTop=sh["top"])
    A("Block", parent=lg, name="Tunnel Threshold", x0=t["x0"], x1=t["x1"], z0=t["z0"] - 0.5, z1=t["z0"], y0=t["floor"] - 0.5, y1=t["floor"])
    A("CellLighting", lighting=lg, cells=cells[:1])
    A("MovingPlatform", parent=lg, label="Service Lift", top=[0, sh["floor"] + 0.05, (sh["z0"] + sh["z1"]) / 2], size=[4, 0.5, 4],
      travel=[0, sh["top"] - sh["floor"] - 0.05, 0], period=12, pause=2)
    A("FloorLabel", parent=lg, text="SERVICE LIFT TO L3", at=[0, EDGE[1] - 2], height=LEVEL[0], facing=[0, -1])
    for sx in (-1, 1):
        A("HazardStripes", parent=lg, a=[sx * (sh["x1"] + 0.5), sh["z0"] - 0.5], b=[sx * (sh["x1"] + 0.5), sh["z1"] + 0.5], inward=[sx, 0], height=sh["top"])

    # ---- The ground: open over the basin ----
    s.part("the ground")
    rim = EDGE[6]
    A("Ground", parent="", half=rim + 12, openings=[[-rim, -rim, rim, rim]])
    for sx, sz, name in ((1, 1, "NE"), (-1, 1, "NW"), (-1, -1, "SW"), (1, -1, "SE")):
        A("Prism", parent="", name=f"Rim Corner {name}", shape=_ccw([[sx * rim, sz * r(half(rim))], [sx * rim, sz * rim], [sx * r(half(rim)), sz * rim]]),
          y0=-1, y1=0, surface="Ground")
    return s.spec


def _riser(A, fg, k, role):
    """The climb up riser k on one face (in the face's frame): standing on terrace k, against the edge at EDGE[k+1]."""
    y, up, edge = LEVEL[k], LEVEL[k + 1], EDGE[k + 1]
    kind = role[0]
    xo = role[1] if kind in ("stair", "ledge", "steps", "lift") and len(role) > 1 else 0
    if kind == "stair":
        A("Flight", parent=fg, name=f"Stair (L{k} to L{k + 1})", basePoint=[xo - 6, edge - 2], direction=[1, 0], width=4, y0=y, y1=up, floor=y)
    elif kind == "ledge":
        A("Platform", parent=fg, name=f"Ledge Block ({y + 4.5:+g})", x0=xo - 2, x1=xo + 2, z0=edge - 4, z1=edge, y0=y, top=y + 4.5, coping="Accent")
        A("FloorLabel", parent=fg, text="4.5 m: JUMP TO THE LEDGE", at=[xo, edge - 6], height=y, facing=[0, -1])
    elif kind == "steps":
        A("Platform", parent=fg, name=f"Step Block ({y + 3:+g})", x0=xo - 6, x1=xo - 2, z0=edge - 4, z1=edge, y0=y, top=y + 3, coping="Accent")
        A("Platform", parent=fg, name=f"Step Block ({y + 6:+g})", x0=xo - 2, x1=xo + 2, z0=edge - 4, z1=edge, y0=y, top=y + 6, coping="Accent")
        A("FloorLabel", parent=fg, text="3 m STEPS: DOUBLE JUMP", at=[xo - 2, edge - 6], height=y, facing=[0, -1])
    elif kind == "lift":
        A("MovingPlatform", parent=fg, label=f"Lift (L{k} to L{k + 1})", top=[xo, y + 0.05, edge - 2.25], size=[4, 0.5, 4], travel=[0, up - y - 0.05, 0],
          period=7, pause=1.5)


def _interior(A, ig, it):
    """
    An interior (in its face's frame): its cells, the door and windows in its front wall (each filled under and over
    the opening, the walls being the interior's own and the terrace cut away round them), its lights, and its climb:
    a hall's gallery and balcony door, or a small room's stairwell through the deck.
    """
    k, kind, name = it["level"], it["kind"], it["name"]
    y, up = LEVEL[k], LEVEL[k + 1]
    front = EDGE[k + 1] + 0.5
    cells, open_top = cells_of(it)
    portals = [[it["door"] - DOOR / 2, front - 0.5, it["door"] + DOOR / 2, front]]
    portals += [[x - WINDOW / 2, front - 0.5, x + WINDOW / 2, front] for x in it["windows"]]
    if kind == "hall":
        bx, bw = balcony(it)
        z = EDGE[k + 2] + 0.5
        portals.append([r(bx - bw / 2), z - 0.5, r(bx + bw / 2), z])
    A("Cells", parent=ig, cells=cells, portals=portals, openTop=open_top)
    d0, d1 = it["door"] - DOOR / 2, it["door"] + DOOR / 2
    A("Block", parent=ig, name="Door Threshold", x0=d0, x1=d1, z0=front - 0.5, z1=front, y0=y - 0.5, y1=y)
    A("Block", parent=ig, name="Door Header", x0=d0, x1=d1, z0=front - 0.5, z1=front, y0=y + 4, y1=up, surface="Panel")
    for i, x in enumerate(it["windows"]):
        w0, w1 = x - WINDOW / 2, x + WINDOW / 2
        A("Block", parent=ig, name=f"Window {i + 1} Sill", x0=w0, x1=w1, z0=front - 0.5, z1=front, y0=y - 0.5, y1=y + SILL, surface="Panel")
        A("Block", parent=ig, name=f"Window {i + 1} Header", x0=w0, x1=w1, z0=front - 0.5, z1=front, y0=y + LINTEL, y1=up, surface="Panel")
    A("CellLighting", lighting=ig, cells=[c for c in cells if not c.get("open")])
    A("FloorLabel", parent=ig, text=name.upper(), at=[it["door"], front - 2.5], height=y, facing=[0, -1])
    wf = room_width(k)
    if kind == "hall":
        # The gallery: along the hall's front wall beside the front room, level with the terrace above, the balcony
        # door in the wall over it; reached by a stair along the same wall or a block to jump to its ledge from.
        wb, z, up2 = room_width(k + 1), EDGE[k + 2] + 0.5, LEVEL[k + 2]
        bx, bw = balcony(it)
        A("Block", parent=ig, name="Balcony Threshold", x0=r(bx - bw / 2), x1=r(bx + bw / 2), z0=z - 0.5, z1=z, y0=y - 0.5, y1=up)
        A("Block", parent=ig, name="Balcony Header", x0=r(bx - bw / 2), x1=r(bx + bw / 2), z0=z - 0.5, z1=z, y0=up + 4, y1=up2, surface="Panel")
        A("Platform", parent=ig, name=f"Gallery ({up:+g})", x0=r(wf + 0.5), x1=wb, z0=z, z1=z + 4.5, y0=y, top=up, coping="Accent")
        A("FloorLabel", parent=ig, text=f"OUT TO L{k + 1}", at=[r(bx), z + 2.5], height=up, facing=[0, -1])
        if it["climb"] == "stair":
            A("Flight", parent=ig, name="Gallery Stair", basePoint=[r(wf + 0.5 - flight_length(y, up)), z + 2.25], direction=[1, 0], width=4,
              y0=y, y1=up, floor=y)
        else:
            A("Platform", parent=ig, name=f"Gallery Climb Block ({y + 4.5:+g})", x0=r(wf - 3.5), x1=r(wf + 0.5), z0=z, z1=z + 4.5, y0=y,
              top=y + 4.5, coping="Accent")
            A("FloorLabel", parent=ig, text="UP: JUMP TO THE LEDGE", at=[r(wf - 1.5), z + 6], height=y, facing=[0, -1])
    elif kind == "small" and it.get("climb"):
        hx0, hx1, hz0, hz1 = hatch(k)
        if it["climb"] == "stair":
            A("Flight", parent=ig, name="Stairwell Stair", basePoint=[hx0, (hz0 + hz1) / 2], direction=[1, 0], width=4, y0=y, y1=up, floor=y)
        else:
            A("Platform", parent=ig, name=f"Stairwell Climb Block ({y + 4.5:+g})", x0=hx1 - 4, x1=hx1, z0=hz0, z1=hz1, y0=y, top=y + 4.5, coping="Accent")
            A("FloorLabel", parent=ig, text="UP: JUMP TO THE LEDGE", at=[hx1 - 8, hz0 - 1.5], height=y, facing=[0, -1])
        A("HazardStripes", parent=ig, a=[hx0, hz0], b=[hx1, hz0], inward=[0, -1], height=up)
    FURNISH[name](A, ig, k, y, up, wf, front)


# ---- What's in each interior (in its face's frame: y its floor, up its front room's deck, wf its front room's half width) ----

def _planters(A, ig, k, y, xs, z0, tree, seed):
    for i, x0 in enumerate(xs):
        A("Planter", parent=ig, name="Planter", x0=x0, x1=x0 + 4, z0=z0, z1=z0 + 4, floor=y, tree=tree, random={"seed": seed + i})


def _park_pavilion(A, ig, k, y, up, wf, front):
    # A shaded hall off the park: benches in front, trees growing up the 17.5 m hall behind.
    z = EDGE[k + 2] + 0.5
    for x in (-6.0, 2.0):
        A("Block", parent=ig, name="Bench", x0=x, x1=x + 4, z0=front + 4, z1=front + 4.8, y0=y, y1=y + 0.5, surface="Graphite")
    for x in (-5.0, 5.0):
        A("Column", parent=ig, name="Column", centre=[x, z - 4], size=[1, 1], y0=y, y1=up - 0.5)
    _planters(A, ig, k, y, (-12, -5, 2), z + 6, True, 6100)


def _seed_vault(A, ig, k, y, up, wf, front):
    for i in range(3):
        x0 = -wf + 1.5 + 4 * i
        A("Block", parent=ig, name=f"Seed Racks {i + 1}", x0=x0, x1=x0 + 1.2, z0=front + 1, z1=front + 4.5, y0=y, y1=y + 3, surface="Graphite")


def _habitat_greenhouse(A, ig, k, y, up, wf, front):
    # Two rows of trees down a 23 m house, a path between them.
    for i, z0 in enumerate((front + 4, front + 11, front + 18)):
        _planters(A, ig, k, y, (-wf + 1, wf - 5), z0, True, 6200 + 10 * i)


def _visitor_centre(A, ig, k, y, up, wf, front):
    for x in (-5.0, 5.0):
        for z in (front + 7, front + 15):
            A("Column", parent=ig, name="Column", centre=[x, z], size=[1, 1], y0=y, y1=up - 0.5)
    for i, z in enumerate((front + 9, front + 13)):
        A("Block", parent=ig, name=f"Display {i + 1}", x0=-2, x1=2, z0=z, z1=z + 2, y0=y, y1=y + 1.1, surface="Steel")
    A("Column", parent=ig, name="Spire Model", centre=[0, front + 19], size=[1.5, 1.5], y0=y, y1=y + 5, accent=True)


def _growth_lab(A, ig, k, y, up, wf, front):
    z = EDGE[k + 2] + 0.5
    _planters(A, ig, k, y, (-11, -3), front + 4, False, 6300)
    _planters(A, ig, k, y, (-17, -10, -3, 4), z + 6, True, 6310)


def _greenhouse(A, ig, k, y, up, wf, front):
    z = EDGE[k + 2] + 0.5
    _planters(A, ig, k, y, (-11, 7), front + 4, False, 6400)
    _planters(A, ig, k, y, (-17, -10, -3, 4, 11), z + 6, True, 6410)


def _archives(A, ig, k, y, up, wf, front):
    # Reading tables in front; in the hall, stacks of shelving of different heights, a climb of their own.
    z = EDGE[k + 2] + 0.5
    for x in (-14.0, -4.0, 6.0):
        A("Block", parent=ig, name="Reading Table", x0=x, x1=x + 5, z0=front + 4, z1=front + 6, y0=y, y1=y + 0.9, surface="Steel")
    for i, (x0, h) in enumerate(((-22, 3), (-16, 6), (-10, 4.5), (-4, 7.5), (2, 4.5), (8, 3))):
        A("Platform", parent=ig, name=f"Stack {i + 1} ({h:g} m)", x0=x0, x1=x0 + 2, z0=z + 6, z1=z + 12, y0=y, top=y + h, surface="Graphite", coping="Accent")


def _research_labs(A, ig, k, y, up, wf, front):
    # A corridor along the front, three labs off it behind a glazed partition, divided by solid walls.
    zc = front + 9.5
    A("Wall", parent=ig, name="Lab Partition", a=[-wf, zc], b=[wf, zc], y0=y, y1=up - 0.5, thickness=0.3,
      openings=[[[-12.8, zc], 2, y, y + 2.6], [[0, zc], 2, y, y + 2.6], [[12.8, zc], 2, y, y + 2.6],
                [[-7.4, zc], 3, y + 1, y + 2.4], [[5.4, zc], 3, y + 1, y + 2.4], [[18.2, zc], 1.6, y + 1, y + 2.4]])
    back = EDGE[k + 3] - 0.5
    for x in (-6.4, 6.4):
        A("Wall", parent=ig, name="Lab Wall", a=[x, zc + 0.15], b=[x, back], y0=y, y1=up - 0.5, thickness=0.3)
    for x in (-16.0, -3.0, 10.0):
        A("Block", parent=ig, name="Lab Bench", x0=x, x1=x + 5, z0=zc + 6, z1=zc + 7.5, y0=y, y1=y + 1, surface="Steel")


def _commons(A, ig, k, y, up, wf, front):
    # A colonnaded hall with benches, and tiered seating at the back.
    for x in (-8.0, 8.0):
        for z in (front + 8, front + 16):
            A("Column", parent=ig, name="Column", centre=[x, z], size=[1, 1], y0=y, y1=up - 0.5)
    for x in (-14.0, -3.0, 9.0):
        A("Block", parent=ig, name="Bench", x0=x, x1=x + 5, z0=front + 12, z1=front + 12.8, y0=y, y1=y + 0.5, surface="Graphite")
    back = EDGE[k + 3] - 0.5
    A("Platform", parent=ig, name="Tiered Seating (+1.2)", x0=-12, x1=12, z0=back - 4.5, z1=back, y0=y, top=y + 1.2, coping="Accent")
    A("Flight", parent=ig, name="Seating Steps", basePoint=[0, back - 4.5 - flight_length(y, y + 1.2)], direction=[0, 1], width=6, y0=y, y1=y + 1.2)


def _fabrication_hall(A, ig, k, y, up, wf, front):
    # Crates and a conveyor in front; in the 17.5 m hall, two cranes over machines and a press to climb.
    z, back = EDGE[k + 2] + 0.5, EDGE[k + 3] - 0.5
    for x, dz in ((-18, 4), (-2, 6), (14, 3)):
        A("PushableCrate", parent=ig, at=[x, front + dz], floor=y, size=2)
    A("LowBeam", parent=ig, name="Conveyor (Slide Under)", a=[6, front + 2], b=[6, front + 10], floor=y, clear=1.2)
    for x in (-20.0, 0.0):
        A("OverheadCrane", parent=ig, a=[x, z + 1], b=[x, back - 1], railTop=LEVEL[k + 2] - 2.5, hookBottom=y + 7)
    for i, x0 in enumerate((-27, -13)):
        A("Block", parent=ig, name=f"Machine {i + 1}", x0=x0, x1=x0 + 6, z0=z + 7, z1=z + 12, y0=y, y1=y + 3, surface="Graphite")
    A("Platform", parent=ig, name="Press (+4.5)", x0=2, x1=8, z0=z + 8, z1=z + 13, y0=y, top=y + 4.5, surface="Steel", coping="Accent")


def _wall_run_hall(A, ig, k, y, up, wf, front):
    # In front, low walls to vault; in the hall, two 12 m walls 5 m apart to run along, from a run-up 4 m high.
    z = EDGE[k + 2] + 0.5
    for x in (-14.0, -2.0):
        A("Block", parent=ig, name="Vault Wall", x0=x, x1=x + 6, z0=front + 6, z1=front + 6.5, y0=y, y1=y + 1.2, surface="Panel")
    for zw in (z + 4, z + 10):
        A("Block", parent=ig, name="Wall-Run Wall", x0=-26, x1=4, z0=zw, z1=zw + 1, y0=y, y1=y + 12, surface="Panel")
    A("Platform", parent=ig, name="Run-up (+4)", x0=5, x1=9, z0=z + 5.5, z1=z + 9.5, y0=y, top=y + 4, coping="Accent")
    A("FloorLabel", parent=ig, text="WALL RUN 11 m", at=[-6, z + 7.5], height=y, facing=[1, 0])


def _step_and_slope_room(A, ig, k, y, up, wf, front):
    back = EDGE[k + 3] - 0.5
    for i, (h, label) in enumerate(((0.3, "0.30 m"), (0.35, "0.35 m"), (0.4, "0.40 m"))):
        A("Block", parent=ig, name=f"Step {label}", x0=-22 + 6 * i, x1=-18 + 6 * i, z0=front + 4, z1=front + 8, y0=y, y1=y + h)
        A("FloorLabel", parent=ig, text=label, at=[-20 + 6 * i, front + 2.5], height=y, facing=[0, -1])
    for i, h in enumerate((2, 3, 4.5, 6)):
        x0 = -22 + 5 * i
        A("Platform", parent=ig, name=f"Ledge {h:g} m", x0=x0, x1=x0 + 4, z0=back - 5, z1=back, y0=y, top=y + h, coping="Accent")
        A("FloorLabel", parent=ig, text=f"{h:g} m", at=[x0 + 2, back - 6.5], height=y, facing=[0, -1])
    for i, deg in enumerate((14.04, 30, 40)):
        x = 4 + 7 * i
        length = r(3 / math.tan(math.radians(deg)))
        A("Platform", parent=ig, name=f"Slope Deck {deg:g}°", x0=x - 2.5, x1=x + 2.5, z0=back - 5, z1=back, y0=y, top=y + 3)
        A("Ramp", parent=ig, name=f"Slope {deg:.0f}°", start=[x, r(back - 5 - length)], direction=[0, 1], width=5, length=length, y0=y, y1=y + 3)
        A("FloorLabel", parent=ig, text=f"{deg:.0f}°", at=[x, r(back - 5 - length - 1.5)], height=y, facing=[0, -1])


def _observation_lab(A, ig, k, y, up, wf, front):
    # Desks under the windows; a glazed partition; a raised observation floor at the back.
    zc = front + 14
    for x in (-21.0, -6.0, 13.0):
        A("Block", parent=ig, name="Desk", x0=x, x1=x + 5, z0=front + 3, z1=front + 4.2, y0=y, y1=y + 0.9, surface="Steel")
    A("Wall", parent=ig, name="Partition", a=[-wf, zc], b=[wf, zc], y0=y, y1=up - 0.5, thickness=0.3,
      openings=[[[0, zc], 3, y, y + 3], [[-12, zc], 8, y + 1, y + 3.5], [[12, zc], 8, y + 1, y + 3.5]])
    back = EDGE[k + 3] - 0.5
    A("Platform", parent=ig, name="Observation Floor (+1.5)", x0=-20, x1=20, z0=back - 7, z1=back, y0=y, top=y + 1.5, coping="Accent")
    A("Flight", parent=ig, name="Observation Steps", basePoint=[0, back - 7 - flight_length(y, y + 1.5)], direction=[0, 1], width=6, y0=y, y1=y + 1.5)
    for x in (-12.0, 12.0):
        A("Column", parent=ig, name="Column", centre=[x, zc + 6], size=[1, 1], y0=y, y1=up - 0.5)


def _ledge_room(A, ig, k, y, up, wf, front):
    back = EDGE[k + 2] - 0.5
    for i, h in enumerate((2, 3, 4.5, 6)):
        x0 = -wf + 2 + 6 * i
        A("Platform", parent=ig, name=f"Ledge {h:g} m", x0=x0, x1=x0 + 4, z0=back - 5, z1=back, y0=y, top=y + h, coping="Accent")
        A("FloorLabel", parent=ig, text=f"{h:g} m", at=[x0 + 2, back - 6.5], height=y, facing=[0, -1])


def _stores(A, ig, k, y, up, wf, front):
    for i in range(4):
        x0 = -wf + 2 + 5 * i
        A("Block", parent=ig, name=f"Racking {i + 1}", x0=x0, x1=x0 + 1.2, z0=front + 1, z1=front + 6, y0=y, y1=y + 4, surface="Graphite")
    for x in (2.0, 7.0, 11.0):
        A("PushableCrate", parent=ig, at=[x, front + 4], floor=y, size=2)


FURNISH = {
    "Park Pavilion": _park_pavilion, "Seed Vault": _seed_vault, "Habitat Greenhouse": _habitat_greenhouse,
    "Visitor Centre": _visitor_centre, "Growth Lab": _growth_lab, "Greenhouse": _greenhouse, "Archives": _archives,
    "Research Labs": _research_labs, "Commons": _commons, "Fabrication Hall": _fabrication_hall,
    "Wall-Run Hall": _wall_run_hall, "Step and Slope Room": _step_and_slope_room, "Observation Lab": _observation_lab,
    "Ledge Room": _ledge_room, "Stores": _stores,
}


if __name__ == "__main__":
    spec = levelspec.normalize(build())
    levelspec.validate(spec)
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(PROJECT, "Assets", "Levels", "MeridianBasin.level.json")
    levelspec.write(spec, out)
    n = sum(len(p["pieces"]) for p in spec["parts"])
    print(f"Wrote '{os.path.relpath(out)}': {len(spec['parts'])} parts, {n} pieces.")
