"""Octagonal rings, for level scripts (examples/meridian_basin.py, examples/tutorial_ring.py): an octagon's corners, a
face's half-width, and the corner piece between two faces of a ring, every edge at a multiple of 45 degrees (the levels'
box colliders fill only those). A ring's face k faces 45k degrees clockwise from north; in its own frame (a Turned
group of yaw 45k) x runs along the face, clockwise toward face k+1, and z out from the middle.
"""

import math

#: tan(22.5 degrees): a face's half-width over its apothem.
T = math.tan(math.radians(22.5))


def r(v):
    """A coordinate rounded as the specs keep them (to 0.1 mm)."""
    return round(v, 4)


def octagon(apothem, cx=0.0, cz=0.0):
    """An octagon's corners (counterclockwise from east-north-east), faces at 45 degree steps, centred at (cx, cz)."""
    radius = apothem / math.cos(math.radians(22.5))
    return [[r(cx + radius * math.cos(math.radians(22.5 + 45 * i))), r(cz + radius * math.sin(math.radians(22.5 + 45 * i)))] for i in range(8)]


def half(apothem):
    """Half the width of an octagon's face at that apothem."""
    return apothem * T


def corner(a, b):
    """
    The piece of a ring (apothems a to b) between a face's rectangle and the next face's clockwise, in the first face's
    frame: bounded by the face's rectangle (x = aT), its outer flat (z = b), the next face's outer flat (at 45 degrees) and
    the next face's rectangle (at 45 degrees), so every edge is at a multiple of 45 degrees.
    """
    d = b - a
    s = d / math.sqrt(2)
    return ccw([[r(half(a)), r(a)], [r(half(a)), r(b)], [r(half(b)), r(b)], [r(half(a) + s), r(a + s)]])


def ccw(points):
    """Points in counterclockwise order (x right, z up), as the pieces' outlines are."""
    area = sum(points[i][0] * points[(i + 1) % len(points)][1] - points[(i + 1) % len(points)][0] * points[i][1] for i in range(len(points)))
    return points if area > 0 else list(reversed(points))
