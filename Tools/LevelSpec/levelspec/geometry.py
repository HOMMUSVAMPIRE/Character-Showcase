"""Where a spec's pieces are: each piece's outline on the ground plane, in the level's (world) coordinates, with its
heights, for drawing schematics and checking the level against the movement.

This mirrors what each Graybox piece builds closely enough to draw and measure it (footprints, decks, stairs' lengths
from the step size, groups placed and turned), not exactly: trims, lights and the like are left out. A new piece with
no outline here is drawn as a marker at its first point, if it has one.
"""

import math
import os
import re

from .model import GROUP_PIECES, PathCounter, child_path

SETTINGS_ASSET = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))),
                              "Assets", "Editor", "Level", "GrayboxSettings.asset")

#: What each layer of a schematic holds.
LAYERS = {
    "ground": "Ground", "solid": "Solids", "deck": "Decks and bridges", "stairs": "Stairs and ramps", "cells": "Rooms",
    "rails": "Guardrails", "structure": "Structure", "mechanics": "Mechanics", "foliage": "Lawns and trees",
    "markings": "Markings", "markers": "Markers and signs",
}


def default_state():
    """The dimensions each part starts from: the generation settings' (GrayboxSettings.asset), as Graybox.Begin sets them."""
    state = {"stepRise": 0.3, "stepRun": 0.4, "bridgeLength": 20.0, "deck": 1.0, "railHeight": 1.1, "railThickness": 0.15}
    keys = {"stepRise": "stepRise", "stepRun": "stepRun", "bridgeLength": "bridgeLength", "deckThickness": "deck",
            "railHeight": "railHeight", "railThickness": "railThickness"}
    try:
        with open(SETTINGS_ASSET, encoding="utf-8") as f:
            for line in f:
                m = re.match(r"\s*(\w+):\s*([-\d.eE]+)\s*$", line)
                if m and m.group(1) in keys:
                    state[keys[m.group(1)]] = float(m.group(2))
    except OSError:
        pass
    return state


class Frame:
    """A group's placement: where its origin is in the world, and how far it's turned (degrees, about the vertical)."""

    def __init__(self, x=0.0, y=0.0, z=0.0, yaw=0.0):
        self.x, self.y, self.z, self.yaw = x, y, z, yaw

    def point(self, p):
        """A point in the group's x and z, in the world's."""
        a = math.radians(self.yaw)
        c, s = math.cos(a), math.sin(a)
        return (self.x + p[0] * c + p[1] * s, self.z - p[0] * s + p[1] * c)

    def vector(self, v):
        a = math.radians(self.yaw)
        c, s = math.cos(a), math.sin(a)
        return (v[0] * c + v[1] * s, -v[0] * s + v[1] * c)

    def child(self, position, yaw):
        px, pz = self.point((position[0], position[2]))
        return Frame(px, self.y + position[1], pz, self.yaw + yaw)


class Shape:
    """One thing to draw: a filled outline ('poly', with holes), a line, a point or a label, from piece number index."""

    def __init__(self, index, part, piece, layer, kind, points, y0=0.0, y1=0.0, closed=False, holes=(), label=None, arrow=None):
        self.index, self.part, self.piece, self.layer, self.kind = index, part, piece, layer, kind
        self.points, self.y0, self.y1, self.closed, self.holes = points, y0, y1, closed, list(holes)
        self.label = label
        self.arrow = arrow  # A (from, to) pair, for travel and facing.

    @property
    def bounds(self):
        xs = [p[0] for p in self.points] or [0]
        zs = [p[1] for p in self.points] or [0]
        return min(xs), min(zs), max(xs), max(zs)

    def axis_rect(self):
        """(x0, z0, x1, z1) if this is an axis-aligned rectangle, else None."""
        if self.kind != "poly" or len(self.points) != 4 or self.holes:
            return None
        x0, z0, x1, z1 = self.bounds
        for x, z in self.points:
            if not (_near(x, x0) or _near(x, x1)) or not (_near(z, z0) or _near(z, z1)):
                return None
        return x0, z0, x1, z1


def _near(a, b, eps=1e-3):
    return abs(a - b) < eps


def _rect(x0, x1, z0, z1):
    return [(x0, z0), (x1, z0), (x1, z1), (x0, z1)]


def _oriented(centre, size, yaw):
    a = math.radians(yaw)
    c, s = math.cos(a), math.sin(a)
    hx, hz = size[0] / 2, size[1] / 2
    return [(centre[0] + x * c + z * s, centre[1] - x * s + z * c) for x, z in ((-hx, -hz), (hx, -hz), (hx, hz), (-hx, hz))]


def _along(start, direction, length, width):
    d = math.hypot(direction[0], direction[1]) or 1.0
    dx, dz = direction[0] / d, direction[1] / d
    sx, sz = dz * width / 2, -dx * width / 2
    end = (start[0] + dx * length, start[1] + dz * length)
    return [(start[0] + sx, start[1] + sz), (end[0] + sx, end[1] + sz), (end[0] - sx, end[1] - sz), (start[0] - sx, start[1] - sz)]


def flight_length(y0, y1, state):
    """A flight's length along its direction (Graybox.FlightLength)."""
    return math.ceil(abs(y1 - y0) / state["stepRise"] - 1e-4) * state["stepRun"]


def layout(spec):
    """Every piece's shapes, in the order they're built, in world coordinates: a list of Shape."""
    shapes = []
    frames = {"": Frame()}
    counter = PathCounter()
    index = -1

    def frame_of(path):
        while path not in frames and path:
            path = path.rsplit("/", 1)[0] if "/" in path else ""
        return frames.get(path, frames[""])

    for p, part in enumerate(spec.get("parts", [])):
        state = default_state()
        for piece in part.get("pieces", []):
            index += 1
            name = piece.get("piece")
            if name == "State":
                state.update({k: v for k, v in piece.items() if k != "piece"})
                continue
            # The group it's built in: its parent, or for pieces built across groups, the one holding its main part.
            parent = next((piece[k] for k in ("parent", "bridges", "lighting", "rails") if k in piece), "")
            f = frame_of(parent if isinstance(parent, str) else "")
            if name in GROUP_PIECES:
                path = child_path(counter, parent, piece.get("name", ""))
                position = piece.get("position", [0, 0, 0]) if name == "Placed" else [0, 0, 0]
                frames[path] = f.child(position, piece.get("yaw", 0.0))
                continue
            if "name" in piece and isinstance(parent, str):
                counter.add(parent, piece["name"])
            for s in _shapes(piece, f, state):
                shapes.append(Shape(index, p, piece, *s[:2], [f.point(q) for q in s[2]], *s[3:5],
                                    **{k: v for k, v in (s[5] if len(s) > 5 else {}).items()}))
                # Labels, holes and arrows are given in the group's coordinates too.
                last = shapes[-1]
                last.holes = [[f.point(q) for q in h] for h in last.holes]
                if last.arrow:
                    last.arrow = (f.point(last.arrow[0]), f.point(last.arrow[1]))
                last.y0 += f.y
                last.y1 += f.y
    return shapes


def _shapes(piece, f, state):
    """A piece's shapes in its group's coordinates: (layer, kind, points, y0, y1, extras)."""
    g = piece.get
    name = piece.get("piece")
    label = g("name", g("label", name))
    if name in ("Block",):
        layer = "solid"
        return [(layer, "poly", _rect(g("x0"), g("x1"), g("z0"), g("z1")), g("y0"), g("y1"), {"label": label})]
    if name == "Platform":
        return [("solid", "poly", _rect(g("x0"), g("x1"), g("z0"), g("z1")), g("y0"), g("top"), {"label": label})]
    if name in ("BoxXZ", "Trim"):
        return [("solid" if name == "BoxXZ" else "structure", "poly", _oriented(g("centre"), g("size"), g("yaw", 0)), g("y0"), g("y1"), {"label": label})]
    if name == "Box":
        c, s = g("localCentre"), g("size")
        return [("solid", "poly", _oriented((c[0], c[2]), (s[0], s[2]), g("yaw", 0)), c[1] - s[1] / 2, c[1] + s[1] / 2, {"label": label})]
    if name == "Prism":
        return [("solid", "poly", [tuple(q) for q in g("shape")], g("y0"), g("y1"), {"label": label})]
    if name == "Coping":
        return [("markings", "line", [tuple(q) for q in g("shape")], g("top"), g("top"), {"closed": True, "label": label})]
    if name in ("Column", "IBeamColumn"):
        return [("structure", "poly", _oriented(g("centre"), g("size"), g("yaw", 0)), g("y0"), g("y1"), {"label": label})]
    if name == "Flight":
        y0, y1 = g("y0"), g("y1")
        length = flight_length(y0, y1, state)
        b, d = g("basePoint"), g("direction")
        n = math.hypot(*d) or 1
        top = (b[0] + d[0] / n * length, b[1] + d[1] / n * length)
        return [("stairs", "poly", _along(b, d, length, g("width")), y0, y1,
                 {"label": f"{label} ({math.ceil(abs(y1 - y0) / state['stepRise'] - 1e-4)} steps)", "arrow": (tuple(b), top)})]
    if name == "Ramp":
        s, d, length = g("start"), g("direction"), g("length")
        n = math.hypot(*d) or 1
        end = (s[0] + d[0] / n * length, s[1] + d[1] / n * length)
        low, high = (s, end) if g("y1") >= g("y0") else (end, s)
        return [("stairs", "poly", _along(s, d, length, g("width")), g("y0"), g("y1"), {"label": label, "arrow": (tuple(low), tuple(high))})]
    if name in ("Walkway", "Bridge"):
        length = g("length") if name == "Walkway" else state["bridgeLength"]
        h = g("height")
        return [("deck", "poly", _along(g("start"), g("direction"), length, g("width")), h - state["deck"], h, {"label": label or name})]
    if name == "Wall":
        a, b = g("a"), g("b")
        length = math.hypot(b[0] - a[0], b[1] - a[1])
        return [("solid", "poly", _along(a, (b[0] - a[0], b[1] - a[1]), length, g("thickness")), g("y0"), g("y1"), {"label": label})]
    if name == "Rail":
        h = g("height")
        return [("rails", "line", [tuple(g("a")), tuple(g("b"))], h, h + state["railHeight"], {"label": "Guardrail"})]
    if name == "RailAround":
        h = g("height")
        return [("rails", "line", [tuple(q) for q in g("shape")], h, h + state["railHeight"], {"closed": True, "label": "Guardrail"})]
    if name in ("SlabWithHoles", "Ground"):
        if name == "Ground":
            half = g("half")
            area, holes, y0, y1 = [-half, -half, half, half], g("openings", []), -1.0, 0.0
        else:
            area, holes, y0, y1 = g("area"), g("holes", []), g("y0"), g("y1")
        return [("ground" if name == "Ground" else "solid", "poly", _rect(area[0], area[2], area[1], area[3]), y0, y1,
                 {"holes": [_rect(h[0], h[2], h[1], h[3]) for h in holes], "label": g("name", "Ground")})]
    if name == "Cells":
        out = []
        for c in g("cells", []):
            out.append(("cells", "poly", _rect(c["x0"], c["x1"], c["z0"], c["z1"]), c["floor"], c["ceiling"],
                        {"label": f"{c.get('name', 'Cell')} ({'open' if c.get('open') else 'ceiling ' + _n(c['ceiling'])})"}))
        for r in g("portals", []):
            out.append(("cells", "line", _rect(r[0], r[2], r[1], r[3]), 0, 0, {"closed": True, "label": "Portal"}))
        return out
    if name == "ShaftWalls":
        return [("solid", "line", _rect(g("x0"), g("x1"), g("z0"), g("z1")), g("y0"), g("y1"), {"closed": True, "label": label})]
    if name == "MovingPlatform":
        top, size, travel = g("top"), g("size"), g("travel")
        c = (top[0], top[2])
        out = [("mechanics", "poly", _rect(c[0] - size[0] / 2, c[0] + size[0] / 2, c[1] - size[2] / 2, c[1] + size[2] / 2),
                top[1] - size[1], top[1], {"label": f"Moving Platform ({g('label')})" + _travel_text(travel, g("period"), g("spin"))})]
        if abs(travel[0]) > 1e-3 or abs(travel[2]) > 1e-3:
            out[0][5]["arrow"] = (c, (c[0] + travel[0], c[1] + travel[2]))
            end = (c[0] + travel[0], c[1] + travel[2])
            out.append(("mechanics", "line", _rect(end[0] - size[0] / 2, end[0] + size[0] / 2, end[1] - size[2] / 2, end[1] + size[2] / 2),
                        top[1] + travel[1], top[1] + travel[1], {"closed": True, "label": "…its far end"}))
        return out
    if name == "PushableCrate":
        a, s = g("at"), g("size")
        return [("mechanics", "poly", _rect(a[0] - s / 2, a[0] + s / 2, a[1] - s / 2, a[1] + s / 2), g("floor"), g("floor") + s,
                 {"label": "Pushable Crate" + ("" if g("canPull", True) else " (push only)")})]
    if name == "Ladder":
        a, f, w = g("at"), g("facing"), g("width", 0.6)
        length = math.hypot(f[0], f[1]) or 1.0
        across = (-f[1] / length * w / 2, f[0] / length * w / 2)
        return [("mechanics", "line", [(a[0] - across[0], a[1] - across[1]), (a[0] + across[0], a[1] + across[1])], g("floor"),
                 g("floor") + g("height"), {"label": f"Ladder ({_n(g('height'))} m)"})]
    if name == "TimingGate":
        return [("mechanics", "line", [tuple(g("a")), tuple(g("b"))], g("floor"), g("floor") + 4.2,
                 {"label": f"Timing Gate: {g('course')} ({g('role')})" + (f", {_n(g('distance'))} m" if g("distance") else "")})]
    if name == "LowBeam":
        return [("mechanics", "line", [tuple(g("a")), tuple(g("b"))], g("floor") + g("clear"), g("floor") + g("clear") + 0.5,
                 {"label": f"{label}: {_n(g('clear'))} m clear"})]
    if name == "GuideLine":
        return [("markings", "line", [tuple(q) for q in g("points")], g("height"), g("height"), {"closed": g("closed", False), "label": label})]
    if name in ("HazardStripes", "OverheadCrane", "Truss", "BracedFrame"):
        layer = "markings" if name == "HazardStripes" else "structure"
        y0 = g("height", g("y0", g("railTop", 0)))
        return [(layer, "line", [tuple(g("a")), tuple(g("b"))], y0, g("y1", y0), {"label": g("name", name)})]
    if name == "CornerBrackets":
        return [("markings", "line", _rect(g("x0"), g("x1"), g("z0"), g("z1")), g("height"), g("height"), {"closed": True, "label": "Corner Brackets"})]
    if name == "Ruler":
        s, d, length = g("start"), g("direction"), g("length")
        n = math.hypot(*d) or 1
        return [("markings", "line", [tuple(s), (s[0] + d[0] / n * length, s[1] + d[1] / n * length)], g("height"), g("height"),
                 {"label": f"{label} ({_n(length)} m)"})]
    if name == "FloorLabel":
        return [("markings", "text", [tuple(g("at"))], g("height"), g("height"), {"label": g("text")})]
    if name == "AreaSign":
        return [("markers", "text", [tuple(g("at"))], g("bottom"), g("bottom"), {"label": f"{g('number')} {g('name')}"})]
    if name in ("Tree",):
        return [("foliage", "point", [tuple(g("at"))], g("floor"), g("floor") + 6, {"label": "Tree"})]
    if name == "Planter":
        return [("foliage", "poly", _rect(g("x0"), g("x1"), g("z0"), g("z1")), g("floor"), g("floor") + 0.6, {"label": label})]
    if name in ("Lawns", "ScatterTrees"):
        a = g("area")
        floor = (g("floors") or [0])[0] if name == "Lawns" else g("floor")
        return [("foliage", "poly" if name == "Lawns" else "line", _rect(a[0], a[2], a[1], a[3]), floor, floor,
                 {"label": "Lawns" if name == "Lawns" else "Trees", "closed": True})]
    if name == "Bollards":
        return [("markers", "line", [tuple(q) for q in g("shape")], g("floor"), g("floor") + 1, {"closed": True, "label": "Bollards"})]
    if name == "SpawnMarker":
        a, yaw = g("at"), math.radians(g("yaw"))
        return [("markers", "point", [tuple(a)], g("height"), g("height"),
                 {"label": f"Spawn: {label}", "arrow": (tuple(a), (a[0] + 3 * math.sin(yaw), a[1] + 3 * math.cos(yaw)))})]
    if name == "Marker":
        return [("markers", "point", [tuple(g("at"))], g("height"), g("height"), {"label": label})]
    # Fixtures and the like: a marker at the first point they have.
    for key in ("at", "wallPoint", "centre"):
        if isinstance(g(key), list):
            y = g("floor", g("height", g("top", 0)))
            return [("markers", "point", [tuple(g(key))], y if isinstance(y, (int, float)) else 0, y if isinstance(y, (int, float)) else 0, {"label": name})]
    return []


def _n(v):
    return f"{v:g}" if isinstance(v, (int, float)) else str(v)


def _travel_text(travel, period, spin):
    parts = []
    if any(abs(t) > 1e-3 for t in travel):
        parts.append(f"travels {_n(round(math.sqrt(sum(t * t for t in travel)), 2))} m every {_n(period)} s")
    if spin and any(abs(s) > 1e-3 for s in spin):
        parts.append(f"spins {_n(max(spin, key=abs))}°/s")
    return ": " + ", ".join(parts) if parts else ""
