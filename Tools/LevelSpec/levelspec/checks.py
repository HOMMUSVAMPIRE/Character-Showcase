"""Checks of a level against what the character can do (envelope.json): every stair's step and ramp's slope against
the step height and steepest ramp; low beams' clearance against standing and crouching; and, between the level's
standable boxes (axis-aligned blocks, platforms and decks), each gap across at the same height (at ground level, only
over an opening in the ground: a pit) and each climb up to a neighbour, with the move it takes.

Findings are reports, not errors: a gap too far to jump may be meant (a pit with stairs out). Severity "warning" marks
what breaks the level's own rules (steps too high, ramps too steep, beams too low) or is out of reach; "info" the rest.
"""

import json
import math
import os

from .geometry import layout

ENVELOPE_PATH = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "envelope.json")

#: Boxes narrower than this aren't counted as somewhere to stand.
MIN_STANDABLE = 1.0
#: Gaps narrower than this are stepped across; wider than the farthest jump plus this aren't reported.
STEP_ACROSS, REPORT_BEYOND = 0.3, 6.0
#: Faces this close count as touching, for climbs; overlaps shorter than this don't count as an edge.
TOUCHING, MIN_EDGE = 0.3, 1.0


def load_envelope(path=ENVELOPE_PATH):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


class Finding:
    def __init__(self, kind, severity, text, at, line=None, pieces=()):
        self.kind, self.severity, self.text, self.at, self.line, self.pieces = kind, severity, text, at, line, list(pieces)

    def as_dict(self):
        return {"kind": self.kind, "severity": self.severity, "text": self.text, "at": self.at, "line": self.line, "pieces": self.pieces}

    def __repr__(self):
        return f"{self.severity}: {self.text}"


def move_for(distance, moves, key):
    """The first move in moves (sorted) that covers distance, or None."""
    for m in sorted(moves, key=lambda m: m[key]):
        if distance <= m[key] + 1e-6:
            return m["move"]
    return None


def check(spec, envelope=None, shapes=None):
    """Every finding for spec, in order: stairs and ramps, beams, gaps, climbs."""
    env = envelope or load_envelope()
    shapes = shapes if shapes is not None else layout(spec)
    findings = []
    findings += _stairs_and_ramps(spec, shapes, env)
    findings += _beams(shapes, env)
    boxes = []
    for s in shapes:
        r = s.axis_rect()
        # Walls (Panel, by the levels' convention) aren't somewhere to stand.
        if r and s.layer in ("solid", "deck") and s.piece.get("surface") != "Panel" and min(r[2] - r[0], r[3] - r[1]) >= MIN_STANDABLE:
            boxes.append((r, s))
    # Openings in the ground (pits, the bowl): a gap at ground level over one is a real gap.
    openings = [h.axis_rect() or h.bounds for s in shapes if s.piece.get("piece") == "Ground" for h in [_Rect(r) for r in s.holes]]
    findings += _gaps(boxes, env, openings)
    findings += _climbs(boxes, env)
    return findings


def _name(s):
    return s.label or s.piece.get("piece")


def _at(s):
    """A box's name with its height, unless its name says it."""
    return _name(s) if "(+" in _name(s) or "(-" in _name(s) or "(±" in _name(s) else f"{_name(s)} ({s.y1:+g})"


def _stairs_and_ramps(spec, shapes, env):
    out = []
    for s in shapes:
        name = s.piece.get("piece")
        if s.layer != "stairs":
            continue
        x0, z0, x1, z1 = s.bounds
        at = ((x0 + x1) / 2, (z0 + z1) / 2)
        rise = abs(s.y1 - s.y0)
        if name == "Flight":
            steps = int(s.label.rsplit("(", 1)[1].split()[0]) if "steps)" in (s.label or "") else 0
            riser = rise / steps if steps else 0
            if riser > env["stepHeight"] + 1e-4:
                out.append(Finding("stair", "warning", f"{_name(s)}: steps {riser:.2f} m high, over the {env['stepHeight']} m step height.", at, pieces=[s.index]))
        elif name == "Ramp":
            length = s.piece.get("length", 0)
            gradient = rise / length if length else math.inf
            if gradient > env["maxRampGradient"] + 1e-4:
                out.append(Finding("ramp", "warning", f"{_name(s)}: 1:{length / rise:.1f} ({math.degrees(math.atan(gradient)):.0f}°), steeper than "
                                   f"1:{1 / env['maxRampGradient']:.0f}.", at, pieces=[s.index]))
    return out


def _beams(shapes, env):
    out = []
    for s in shapes:
        if s.piece.get("piece") != "LowBeam":
            continue
        clear = s.piece.get("clear")
        at = s.points[0]
        if clear < env["crouchHeight"]:
            out.append(Finding("clearance", "warning", f"{_name(s)}: {clear} m clear, lower than a crouch ({env['crouchHeight']} m).", at, pieces=[s.index]))
        elif clear >= env["standHeight"]:
            out.append(Finding("clearance", "info", f"{_name(s)}: {clear} m clear, enough to walk under standing.", at, pieces=[s.index]))
    return out


def _separation(a, b):
    """(gap along x, gap along z, overlap along z, overlap along x) between two rects."""
    gx = max(b[0] - a[2], a[0] - b[2])
    gz = max(b[1] - a[3], a[1] - b[3])
    oz = min(a[3], b[3]) - max(a[1], b[1])
    ox = min(a[2], b[2]) - max(a[0], b[0])
    return gx, gz, oz, ox


def _corridor(a, b, axis):
    """The band between two rects across a gap along axis ('x' or 'z'): (x0, z0, x1, z1)."""
    if axis == "x":
        lo, hi = (a, b) if a[2] <= b[0] else (b, a)
        return lo[2], max(a[1], b[1]), hi[0], min(a[3], b[3])
    lo, hi = (a, b) if a[3] <= b[1] else (b, a)
    return max(a[0], b[0]), lo[3], min(a[2], b[2]), hi[1]


def _blocked(corridor, top, boxes, skip):
    """Whether something standable at about this height or above fills part of the corridor."""
    x0, z0, x1, z1 = corridor
    for r, s in boxes:
        if s in skip or s.y1 < top - 0.5:
            continue
        if r[0] < x1 - 1e-3 and r[2] > x0 + 1e-3 and r[1] < z1 - 1e-3 and r[3] > z0 + 1e-3:
            return True
    return False


class _Rect:
    """A hole's outline, as a shape's bounds."""

    def __init__(self, points):
        self.points = points

    def axis_rect(self):
        xs, zs = [p[0] for p in self.points], [p[1] for p in self.points]
        return min(xs), min(zs), max(xs), max(zs)

    @property
    def bounds(self):
        return self.axis_rect()


def _over(point, openings):
    return any(o[0] < point[0] < o[2] and o[1] < point[1] < o[3] for o in openings)


def _gaps(boxes, env, openings=()):
    out = []
    farthest = max(j["distance"] for j in env["jumps"])
    seen = set()
    for i, (a, sa) in enumerate(boxes):
        for b, sb in boxes[i + 1:]:
            if abs(sa.y1 - sb.y1) > 0.1:
                continue
            gx, gz, oz, ox = _separation(a, b)
            for gap, overlap, axis in ((gx, oz, "x"), (gz, ox, "z")):
                if gap <= STEP_ACROSS or overlap < MIN_EDGE or gap > farthest + REPORT_BEYOND:
                    continue
                c = _corridor(a, b, axis)
                # At ground level the ground fills a gap, unless it's open there (a pit).
                if sa.y1 < 0.4 and not _over(((c[0] + c[2]) / 2, (c[1] + c[3]) / 2), openings):
                    continue
                if _blocked(c, sa.y1, boxes, (sa, sb)):
                    continue
                key = (round(gap, 2), round(sa.y1, 2), round((c[0] + c[2]) / 2, 1), round((c[1] + c[3]) / 2, 1))
                if key in seen:
                    continue
                seen.add(key)
                mid = ((c[0] + c[2]) / 2, (c[1] + c[3]) / 2)
                line = [(c[0], mid[1]), (c[2], mid[1])] if axis == "x" else [(mid[0], c[1]), (mid[0], c[3])]
                move = move_for(gap, env["jumps"], "distance")
                text = f"{gap:.1f} m gap at {sa.y1:+g}, {_name(sa)} to {_name(sb)}: " + (move if move else f"too far to jump (over {farthest} m)")
                out.append(Finding("gap", "info" if move else "warning", text, mid, line, [sa.index, sb.index]))
    return out


def _climbs(boxes, env):
    out = []
    highest = max(c["height"] for c in env["climbs"])
    seen = set()
    for i, (a, sa) in enumerate(boxes):
        for b, sb in boxes[i + 1:]:
            rise = abs(sa.y1 - sb.y1)
            if rise <= env["stepHeight"] + 0.01:
                continue
            gx, gz, oz, ox = _separation(a, b)
            if gx <= TOUCHING and gz <= TOUCHING and (oz >= MIN_EDGE or ox >= MIN_EDGE) and (gx >= -1e-3 or gz >= -1e-3):
                low, high = (sa, sb) if sa.y1 < sb.y1 else (sb, sa)
                # The edge between them.
                if gx >= -1e-3 and oz >= MIN_EDGE:
                    x = (a[2] + b[0]) / 2 if a[2] <= b[0] + TOUCHING else (b[2] + a[0]) / 2
                    z0, z1 = max(a[1], b[1]), min(a[3], b[3])
                    line, at = [(x, z0), (x, z1)], (x, (z0 + z1) / 2)
                elif gz >= -1e-3 and ox >= MIN_EDGE:
                    z = (a[3] + b[1]) / 2 if a[3] <= b[1] + TOUCHING else (b[3] + a[1]) / 2
                    x0, x1 = max(a[0], b[0]), min(a[2], b[2])
                    line, at = [(x0, z), (x1, z)], ((x0 + x1) / 2, z)
                else:
                    continue
                key = (round(rise, 2), round(at[0], 1), round(at[1], 1))
                if key in seen:
                    continue
                seen.add(key)
                move = move_for(rise, env["climbs"], "height")
                text = f"{rise:.1f} m up, {_at(low)} to {_at(high)}: " + (move if move else f"out of reach (over {highest} m)")
                out.append(Finding("climb", "info" if move else "warning", text, at, line, [low.index, high.index]))
    return out
