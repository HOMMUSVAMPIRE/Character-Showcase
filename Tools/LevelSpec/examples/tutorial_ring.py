"""Generates the Tutorial Ring level spec: a ring whose first arc teaches every move, one lesson each, and whose second
arc is an agility course mixing them, harder as it goes (ReferenceAssets/TutorialRingDocs; docs/showcase/levels.md, The
Tutorial Ring). Every lesson's lock and course crossing is sized from what the character can do (envelope.json, without
its air jumps: the tutorial's character has one jump) and checked by the lock check (levelspec.locks): if a crossing can
be passed without its intended move, or its move doesn't reach it with room to spare, nothing is written and it exits 1.
Run it from the project folder:

    python3 Tools/LevelSpec/examples/tutorial_ring.py          # writes Assets/Showcase/Levels/TutorialRing.level.json
    python3 Tools/LevelSpec/levelspec check Assets/Showcase/Levels/TutorialRing.level.json

The layout (metres, +z north; the ring's decks FLOOR up, over ground that's a hazard):
- An octagonal ring of lanes, LANE wide at RING_APOTHEM, run counterclockwise (seen from above): face k faces 45k
  degrees clockwise from north, and a lane is run along its frame's -x.
- The hub (the spawn point and the start checkpoint) at the corner between the south and south-east faces.
- The lessons on the first four faces (south-east, east, north-east, north), in the design's order; the agility
  course on the other four (north-west, west, south-west, south), back to the hub.
- The lessons shortcut, a shuttle platform straight across the middle from the lessons' end (the north/north-west
  corner) to the hub, waiting SHUTTLE_PAUSE at each end, its hub dock closed by a gate until the player reaches its
  trigger at the lessons' end; the course's end gate, at
  the hub, closed until the player reaches it from the course. Both stay open for the session, both ways.
- Each lesson and course stretch is built in a frame of its own (a Placed group): z along the way, x out from the
  ring's middle, its floor FLOOR. Each lesson has a checkpoint at its start and a lesson zone over it.
"""

import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
sys.path.insert(0, os.path.dirname(HERE))

import levelspec  # noqa: E402
from levelspec import locks  # noqa: E402
from levelspec.locks import Crossing, LockError  # noqa: E402
from levelspec.rings import ccw, corner, half, octagon, r  # noqa: E402

SCENE = "Assets/Showcase/Scenes/TutorialRing/TutorialRing.unity"
OUT = os.path.join(PROJECT, "Assets", "Showcase", "Levels", "TutorialRing.level.json")

# ---- The layout ----
RING_APOTHEM = 160.0          # The lanes' middle line's apothem.
LANE = 12.0                   # A lane's width.
FLOOR = 10.0                  # The decks' height.
DECK = 1.0                    # A deck's thickness.
MASS = 6.0                    # How far a ceiling's mass goes up (so nothing over a low passage can be stood on).
HUB_APOTHEM = 14.0            # The hub's size.
HUB_ANGLE = 157.5             # Where the hub is: the corner between the south (180) and south-east (135) faces.
SHUTTLE_SIZE = 8.0            # The lessons shortcut's shuttle: a platform this big each way,
SHUTTLE_TRIP = 10.0           # crossing the middle in this many seconds (eased in and out),
SHUTTLE_PAUSE = 5.0           # and waiting this long at each end.
DOCK_WIDTH = 8.0              # The docks it stops at, at the lessons' end and the hub.
# The ring's walls, along both edges of the lanes' plain stretches. A wall beside the way is a second jump (a wall run
# along it and a jump off its end, or a jump off it), and its top could be stood on, so they stop short of every crossing
# (gaps, climbs, the pool, steps: wherever the lane isn't plain floor): before a gap, far enough that a jump off a wall's
# end can't carry the character across it (gap_clear); before a climb, CLIMB_CLEAR; after either, a metre; and they're
# too tall to get onto from any floor near them.
WALL_CLEAR = 12.0             # From anything the script hasn't said how close the walls may come to.
CLIMB_CLEAR = 7.0             # Before a climb: a jump off a wall's end there can't reach its top.
WALL_JUMP_EXTRA = 2.0         # How much further than a sprint jump a jump off a wall run's end carries (its height).
WALL_HEIGHT = 17.0            # How far the walls rise over the decks: out of reach from any climb's top (checked).
GROUND = 0.0                  # Where every wall and block goes down to.
SKIRT = 0.5                   # The walls under the decks' edges, from the ground up to them: how thick.
GATE_POST = 0.3               # A timing gate's posts' width (LevelBuilderSettings' Gate Post): its posts stand in the walls.
TIMING_GATE_AT = 6.0          # How far into a course stretch its timing gate stands: where the lane's walls are.

# ---- The look: all to see only (Trim, stripes, labels, banners: no colliders), so none of it changes how it plays ----
BAND = 1.2                    # The graphite band along the foot of the tall walls, at deck height (and its orange stripe above).
COPING = 0.35                 # The graphite coping along the tall walls' tops.
LINTEL = 1.2                  # The graphite beam over a short break in a tall wall, at its top, tying the two ends together.
LINTEL_SPAN = 30.0            # The longest break that gets one.
#: The lessons' names on the floor at their starts (under their numbers).
LESSON_TITLES = {
    "L01-move": "MOVE", "L02-jump": "JUMP", "L03-sprint": "SPRINT JUMP", "L04-crouch": "CROUCH", "L05-slide": "SLIDE",
    "L06-roll": "ROLL", "L07-vault": "VAULT", "L08-ledge": "LEDGE CLIMB", "L09-wallrun": "WALL RUN", "L10-walljump": "WALL JUMP",
    "L11-climb": "CLIMB", "L12-ladder": "LADDER", "L13-crate": "PUSH AND PULL", "L14-bounce": "BOUNCE PAD",
    "L15-rope": "ROPE SWING", "L16-zipline": "ZIPLINE", "L17-swim": "SWIM AND DIVE", "L18-dash": "DASH",
}
#: The course's stretches: each a name and a colour of its own (its timing gate's frame and its own accents).
STRETCH_THEMES = [("RIGGING YARD", "Light"), ("RUN WALL", "Cyan"), ("THE CUT", "Accent"), ("FINALE", "White")]
WALL_THICKNESS = 1.0          # Every wall along an edge stands just inside it, its outer face on the edge, so the tall
                              # walls, the walls under the decks, the pool's and the wall runs' all line up.
GATE_HEIGHT = 6.0             # A shortcut gate's height: over the highest jump, so it can't be jumped.
GATE_OVERHANG = 3.0           # How far a gate reaches past its way's edge each side, so it can't be jumped round.
#: The faces in the order they're run (counterclockwise from the hub): south-east, east, north-east, north (the lessons),
#: north-west, west, south-west, south (the course).
RUN_FACES = [3, 2, 1, 0, 7, 6, 5, 4]
#: The course's stretches' difficulty: each crossing a measured move is meant for is this share of the move's reach.
SHARES = [0.65, 0.75, 0.85, 0.95]

# ---- Sizes not measured by the envelope (the character's tuning or the mechanics'), verified in play ----
CEILING = 1.3                 # A low passage's clearance: under standing, over crouching (both checked against the envelope).
BLOCKER_WINDOW = 3.0          # The roll lesson's blocked stretch (shorter than a roll, rollLength).
BLOCKER_PERIOD = 1.4          # Its blocker's trip in and out of the passage (s), pausing at each end:
BLOCKER_PAUSE = 0.5           # open about 0.7 s at a time: time for a roll, not a crouch-walk.
ROLL_APPROACH = 16.0          # The low approach before the blockers: longer than a slide, so a slide in is spent first.
VAULT_WALL = 0.9              # The vault lesson's wall: over the step height, under a ledge grab.
CRATE = 1.5                   # The crates' size.
ROPE_LENGTH = 6.0
ZIPLINE_RISE = 3.2            # How high over its deck a zipline's cable starts (jumped up to).
POOL_DEPTH = 4.0
ARCH_CLEAR = 1.6              # The underwater arch's clearance over the pool's floor.
ARCH_TOP = 8.0                # How high the arch rises over the decks.

ENV = locks.no_air_jumps(levelspec.load_envelope())
CROSSINGS = []                # (Crossing, the moves taught by then): every lesson's and course crossing, as laid out.
BASE_MOVES = {"walk"}


def reach(move, kind=None):
    return locks.reach(move, ENV, kind)


def gap_clear(gap):
    """How far before a gap the ring's walls stop: a jump off a wall run's end there can't carry the character across."""
    return max(2.0, r((reach("sprint jump", "gap") + WALL_JUMP_EXTRA) / locks.OTHER_SHARE - gap))


def between(lo, hi, t=0.5):
    """A value t of the way from lo to hi (a lock's size, between what must fall short and what must reach)."""
    if lo > hi:
        raise LockError(f"Nothing fits between {lo:.2f} m and {hi:.2f} m: the character's moves are too close for this lock.")
    return r(lo + (hi - lo) * t)


def gap_between(short, long_, t=0.5):
    """A gap (or height) the move long_ crosses with room to spare and short falls short of."""
    return between(short / locks.OTHER_SHARE, locks.INTENDED_SHARE * long_, t)


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


class Stretch:
    """A lesson's or a course stretch's frame (a Placed group): z along the way from 0, x out from the ring's middle."""

    def __init__(self, s, parent, name):
        self.s, self.g, self.name, self.z = s, f"{parent}/{name}", name, 0.0
        self.zone_low = FLOOR - 3  # How low its lesson zone reaches (a lesson that goes lower sets it).
        self.floors = []           # Its plain floor (full-width decks at FLOOR): (z0, z1), where the ring's walls may go.
        self.keep_clear = []       # Stretches (z0, z1) the outer wall stays out of though the floor's plain (a blocker's way).
        self.features = []         # Its crossings (z0, z1, clear before, clear after): how close the walls may come.
        self.walled = []           # Stretches (z0, z1) the ring's walls run straight through (a wall run's gap: it's for it).

    def feature(self, z0, z1, before, after=0.0):
        """A crossing from z0 to z1, which the ring's walls stop before before it and start after after it."""
        self.features.append((z0, z1, before, after))

    def gap(self, z0, z1, before=None, after=0.0, walled=False):
        """
        A gap from z0 to z1, the walls kept back from it as gap_clear says (or before); walled, they run on along both
        sides of it (a gap a wall run is meant for: a wall either side of it changes nothing).
        """
        if walled:
            self.walled.append((z0, z1))
        else:
            self.feature(z0, z1, gap_clear(z1 - z0) if before is None else before, after)
        # Both lips striped, so the drop reads from either side.
        self.stripes(z0, -1)
        self.stripes(z1, 1)

    def A(self, piece, **settings):
        return self.s.add(piece, parent=self.g, **settings)

    def deck(self, z0, z1, top=FLOOR, name="Deck", x0=-LANE / 2, x1=LANE / 2, coping="Accent"):
        if z1 - z0 > 1e-6:
            self.A("Platform", name=name, x0=r(x0), x1=r(x1), z0=r(z0), z1=r(z1), y0=r(top - DECK), top=r(top), coping=coping)
            TOPS.append(top - FLOOR)
            # A wall under each side edge, from the ground up to the deck, flush with it: the deck stands on something.
            if top - DECK - GROUND > 0.5:
                for a, b in ((x0, x0 + SKIRT), (x1 - SKIRT, x1)):
                    self.A("Block", name="Deck Skirt", x0=r(a), x1=r(b), z0=r(z0), z1=r(z1), y0=GROUND, y1=r(top - DECK), surface="Panel")
            if abs(top - FLOOR) < 1e-6 and x0 <= -LANE / 2 + 1e-6 and x1 >= LANE / 2 - 1e-6:
                self.floors.append((z0, z1))

    def block(self, z0, z1, y0, y1, name="Block", x0=-LANE / 2, x1=LANE / 2, surface="Panel"):
        self.A("Block", name=name, x0=r(x0), x1=r(x1), z0=r(z0), z1=r(z1), y0=r(y0), y1=r(y1), surface=surface)

    def trim(self, name, x0, x1, z0, z1, y0, y1, surface):
        """A box to see only (no collider), from x0 to x1 across, z0 to z1 along, y0 to y1 up, in a surface."""
        self.A("Trim", name=name, centre=[r((x0 + x1) / 2), r((z0 + z1) / 2)], size=[r(x1 - x0), r(z1 - z0)], y0=r(y0), y1=r(y1),
               yaw=0, surface=surface)

    def stripes(self, z, inward, floor=FLOOR):
        """Hazard stripes across the lane at z on the floor, reaching inward (+1 or -1 along the way) onto it."""
        self.A("HazardStripes", a=[-LANE / 2, r(z)], b=[LANE / 2, r(z)], inward=[0, inward], height=r(floor))

    def face_bars(self, z, x0, x1, y0, y1, every, surface="Accent"):
        """Horizontal bars on a face across the lane at z (facing back along the way), every so high from y0 to y1."""
        y = y0
        while y + 0.15 <= y1:
            self.trim("Bar", x0, x1, z - 0.05, z, y, y + 0.15, surface)
            y += every

    def tall_wall(self, name, a, b):
        """A tall wall from a to b (to the ground, as tall as the ring's), dressed as they are."""
        self.A("Wall", name=name, a=a, b=b, y0=GROUND, y1=FLOOR + WALL_HEIGHT, thickness=WALL_THICKNESS)
        dress_wall(self.s.add, self.g, a, b, WALL_THICKNESS, FLOOR + WALL_HEIGHT)

    def checkpoint(self, cid, z=1.0, start=False, floor=FLOOR):
        self.A("Checkpoint", id=cid, feet=[0, floor, r(z)], yaw=0, size=[LANE, 3, 2], start=start or None)

    def zone(self, lesson, length, low=None, high=FLOOR + 12):
        low = self.zone_low if low is None else low
        self.A("LessonZone", lesson=lesson, centre=[0, r((low + high) / 2), r(length / 2)], size=[LANE + 2, r(high - low), r(length)])

    def label(self, text, z, floor=FLOOR, size=None):
        """Lettering on the floor at z, reading the right way up for someone coming along the way (+z)."""
        self.A("FloorLabel", text=text, at=[0, r(z)], height=floor, facing=[0, 1], size=size)


def cross(c, taught):
    CROSSINGS.append((c, set(taught)))


# ---- The lessons: each lays out its stretch, adds its crossings, returns its length and the moves it teaches ----

def lesson_L01(st, taught):
    st.deck(0, 16)
    return 16, {"walk"}


def lesson_L02(st, taught):
    h = gap_between(reach("step up", "climb"), reach("jump up", "climb"))
    st.deck(0, 5)
    st.deck(5, 11, FLOOR + h, name=f"Step (+{h:g})")
    st.block(5, 11, GROUND, FLOOR + h - DECK, name="Step Base")
    st.walled.append((5, 11))  # Walled past: a step this low is a jump up whatever's beside it.
    st.deck(11, 14)
    taught = taught | {"jump", "step up", "jump up"}
    cross(Crossing("L02 step", "climb", h, {"jump up"}), taught)
    return 14, {"jump", "step up", "jump up"}


def lesson_L03(st, taught):
    g = gap_between(reach("jump", "gap"), reach("sprint jump", "gap"))
    st.deck(0, 16, name="Run-up")
    st.deck(16 + g, 24 + g, name="Landing")
    st.gap(16, 16 + g)
    st.label(f"{g:.1f} m", 14)
    cross(Crossing("L03 gap", "gap", g, {"sprint jump"}), taught | {"sprint jump"})
    return 24 + g, {"sprint jump"}


def lesson_L04(st, taught):
    length = 8.0
    st.deck(0, 14)
    st.block(3, 3 + length, FLOOR + CEILING, FLOOR + CEILING + MASS, name=f"Tunnel ({CEILING:g} m clear)")
    st.stripes(3, -1)
    cross(Crossing("L04 tunnel", "passage", length, {"crouch"}, ceiling=CEILING), taught | {"crouch"})
    return 14, {"crouch"}


def lesson_L05(st, taught):
    run_up, inside = 16.0, 8.0
    end = run_up + inside + 6
    st.deck(0, end, name="Run-up and Slot")
    st.block(run_up, run_up + inside, FLOOR + CEILING, FLOOR + CEILING + MASS, name=f"Slot ({CEILING:g} m clear)")
    st.stripes(run_up, -1)
    # Shown, not locked: a crouch-walk gets through a low slot too, and nothing but a gap at its end (which made the floor
    # look broken) could tell the two apart.
    return end, {"slide"}


def lesson_L06(st, taught):
    start, window = 2.0, BLOCKER_WINDOW
    blocked = start + ROLL_APPROACH
    end = blocked + window + 4
    st.deck(0, end)
    st.block(start, end - 1, FLOOR + CEILING, FLOOR + CEILING + MASS, name=f"Low Passage ({CEILING:g} m clear)")
    st.stripes(start, -1)
    st.stripes(blocked, -1)  # Where the blocker sweeps across.
    # The blocker slides in from beside the lane across the passage and out again, open about half its trip. It carries a
    # hazard: it moves through a character rather than pushing it, so being caught by it respawns.
    st.A("MovingPlatform", label="Roll Blocker", top=[LANE + 0.5, r(FLOOR + CEILING - 0.05), r(blocked + window / 2)],
         size=[LANE, r(CEILING - 0.1), window], travel=[-(LANE + 0.5), 0, 0], period=BLOCKER_PERIOD, pause=BLOCKER_PAUSE, smooth=False,
         hazard=True)
    # The blocker comes in through the outer wall: an opening there just its height (wall over it, from the passage's
    # ceiling up), and none in the inner wall.
    st.keep_clear.append((blocked - 1, blocked + window + 1))
    st.block(blocked - 1, blocked + window + 1, FLOOR + CEILING, FLOOR + WALL_HEIGHT, name="Over the Blocker's Way",
             x0=LANE / 2 - WALL_THICKNESS, x1=LANE / 2)
    # The dash isn't taught yet; on a revisit it gets through too (it's quicker than a roll, so no timing stops it), which
    # the design accepts.
    cross(Crossing("L06 blockers", "passage", window, {"roll"}, ceiling=CEILING, timed=True, run_up=False), taught | {"roll"})
    return end, {"roll"}


def lesson_L07(st, taught):
    st.deck(0, 12)
    st.block(6, 6.6, FLOOR, FLOOR + VAULT_WALL, name=f"Low Wall ({VAULT_WALL:g} m)", surface="Graphite")
    # Shown, not locked: a jump gets over a wall this low too.
    return 12, {"vault"}


def climb_lesson(st, name, height, approach=8.0, top_length=6.0):
    """An approach deck, a block height high to get up (its face at z = approach), and its top; then a drop back to FLOOR."""
    st.deck(0, approach)
    face = approach
    st.block(face, face + top_length, GROUND, FLOOR + height - DECK, name=f"{name} (+{height:g})")
    st.feature(face, face + top_length, CLIMB_CLEAR)
    st.deck(face, face + top_length, FLOOR + height, name=f"{name} Top")
    st.stripes(face, 1, FLOOR + height)  # The lip to reach for.
    st.deck(face + top_length, face + top_length + 6, name="Below")
    return face, face + top_length + 6


def lesson_L08(st, taught):
    h = gap_between(reach("jump up", "climb"), reach("jump to the ledge", "climb"))
    _, end = climb_lesson(st, "Ledge Wall", h)
    cross(Crossing("L08 ledge", "climb", h, {"jump to the ledge"}), taught | {"jump to the ledge"})
    return end, {"jump to the ledge"}


def lesson_L09(st, taught):
    g = gap_between(reach("sprint jump", "gap"), reach("wall run", "gap"))
    run_up = 16.0
    st.deck(0, run_up, name="Run-up")
    st.deck(run_up + g, run_up + g + 8, name="Landing")
    edge = LANE / 2 - WALL_THICKNESS / 2
    st.tall_wall("Wall-Run Wall", [edge, 4], [edge, r(run_up + g + 4)])
    st.gap(run_up, run_up + g, walled=True)  # A wall run is what it's for.
    cross(Crossing("L09 wall run", "gap", g, {"wall run"}, wall_beside=True), taught | {"wall run"})
    return run_up + g + 8, {"wall run"}


def lesson_L10(st, taught):
    h = r(max(7.0, reach("jump to the ledge", "climb") / locks.OTHER_SHARE + 0.5))
    shaft, depth = 3.5, 3.5
    st.deck(0, 6)
    st.deck(6, 6 + depth, name="Shaft Floor", x0=-shaft / 2, x1=shaft / 2)
    st.block(6, 6 + depth, GROUND, FLOOR + h + 4, name="Shaft Side", x0=-LANE / 2, x1=-shaft / 2)
    st.block(6, 6 + depth, GROUND, FLOOR + h + 4, name="Shaft Side", x0=shaft / 2, x1=LANE / 2)
    st.block(6 + depth, 12 + depth, GROUND, FLOOR + h - DECK, name=f"Shaft Back (+{h:g})")
    st.feature(6, 12 + depth, CLIMB_CLEAR)
    st.deck(6 + depth, 12 + depth, FLOOR + h, name="Shaft Top")
    # Rungs of orange up the shaft's back, to the lip: the way out is up.
    st.face_bars(6 + depth, -shaft / 2 + 0.2, shaft / 2 - 0.2, FLOOR + 1, FLOOR + h - 0.6, 1.2)
    st.deck(12 + depth, 18 + depth, name="Below")
    cross(Crossing("L10 shaft", "climb", h, {"wall jump"}, fixtures={"walls"}), taught | {"wall jump"})
    return 18 + depth, {"wall jump"}


def lesson_L11(st, taught):
    h = r(max(6.5, reach("jump to the ledge", "climb") / locks.OTHER_SHARE + 0.5))
    face, end = climb_lesson(st, "Panel Wall", h)
    st.A("ClimbablePanel", a=[-2.5, r(face)], b=[2.5, r(face)], y0=FLOOR, y1=r(FLOOR + h), facing=[0, -1])
    cross(Crossing("L11 panel", "climb", h, {"climb"}, fixtures={"panel"}), taught | {"climb"})
    return end, {"climb"}


def lesson_L12(st, taught):
    h = r(max(6.5, reach("jump to the ledge", "climb") / locks.OTHER_SHARE + 0.5))
    face, end = climb_lesson(st, "Ladder Wall", h)
    st.A("Ladder", at=[0, r(face)], floor=FLOOR, height=h, facing=[0, -1])
    cross(Crossing("L12 ladder", "climb", h, {"ladder"}, fixtures={"ladder"}), taught | {"ladder"})
    return end, {"ladder"}


def crate_climb(st, cid, approach=10.0, alcove=True):
    """A climb a crate (pulled out of an alcove first, if alcove) pushed against the wall makes reachable: the wall's
    height is out of a jump to the ledge's reach from the floor and within it from the crate's top."""
    ledge = reach("jump to the ledge", "climb")
    h = gap_between(ledge, ledge + CRATE)
    face, end = climb_lesson(st, "Crate Wall", h, approach=approach)
    at = 2.0 + CRATE / 2
    if alcove:
        # Its back against a low wall, so it's pulled out before it can be pushed.
        st.block(1.2, 2.0 - 0.05, FLOOR, FLOOR + 1.2, name="Alcove Back", x0=-1.2, x1=1.2, surface="Graphite")
    st.A("PushableCrate", at=[0, r(at)], floor=FLOOR, size=CRATE, reset=cid)
    return h, end


def lesson_L13(st, taught):
    h, end = crate_climb(st, "L13-crate")
    cross(Crossing("L13 crate", "climb", h, {"crate"}, fixtures={"crate"}), taught | {"crate"})
    return end, {"crate"}


def lesson_L14(st, taught):
    h = r(max(7.0, reach("jump to the ledge", "climb") / locks.OTHER_SHARE + 0.5))
    face, end = climb_lesson(st, "Pad Wall", h)
    st.A("BouncePad", at=[0, r(face - 2.5)], floor=FLOOR, size=2, height=r(h + 2))
    cross(Crossing("L14 bounce", "climb", h, {"bounce"}, fixtures={"bounce pad"}), taught | {"bounce"})
    return end, {"bounce"}


def lesson_L15(st, taught):
    g = gap_between(reach("sprint jump", "gap"), reach("sprint jump", "gap") * 1.6, 0.4)
    run_up = 10.0
    st.deck(0, run_up, name="Run-up")
    st.deck(run_up + g, run_up + g + 6, name="Landing")
    st.gap(run_up, run_up + g)
    st.A("Rope", anchor=[0, r(FLOOR + ROPE_LENGTH + 1.5), r(run_up + g / 2)], length=ROPE_LENGTH)
    cross(Crossing("L15 rope", "gap", g, {"rope"}, fixtures={"rope"}), taught | {"rope"})
    return run_up + g + 6, {"rope"}


def zipline_gap(st, z, length, drop=2.0):
    """A gap length long from z, crossed on a zipline down to a deck drop lower; then a step back up to FLOOR (not a lock:
    any way up will do). Returns its end and the drop."""
    st.deck(z, z + 5, name="Zipline Start")
    far = FLOOR - drop
    st.deck(z + 5 + length, z + 5 + length + 6, far, name=f"Zipline End ({-drop:+g})")
    start = [0, r(FLOOR + ZIPLINE_RISE), r(z + 2)]
    end = [0, r(far + ZIPLINE_RISE - 0.4), r(z + 5 + length + 4)]
    st.A("Zipline", start=start, end=end, startFloor=FLOOR, endFloor=r(far))
    st.block(z + 11 + length, z + 15 + length, GROUND, FLOOR - DECK, name="Step Up Base")
    st.feature(z + 5, z + 15 + length, gap_clear(length))
    st.deck(z + 11 + length, z + 15 + length, name="Step Up")
    return z + 15 + length, drop


def lesson_L16(st, taught):
    g = r(max(16.0, reach("sprint jump", "gap") / locks.OTHER_SHARE * 2))
    end, drop = zipline_gap(st, 0, g)
    cross(Crossing("L16 zipline", "gap", g, {"zipline"}, fixtures={"zipline"}), taught | {"zipline"})
    return end, {"zipline"}


def lesson_L17(st, taught):
    pool = 18.0
    bottom = FLOOR - POOL_DEPTH
    st.zone_low = bottom - 1  # Its zone reaches the pool's floor, so diving under the arch stays in it.
    st.deck(0, 4)
    inside = LANE / 2 - WALL_THICKNESS
    st.A("Platform", name="Pool Floor", x0=-inside, x1=inside, z0=4, z1=4 + pool, y0=bottom - DECK, top=bottom, surface="Concrete")
    # Its sides as tall as the ring's walls, which they carry on, so nothing can be walked along them past the arch.
    for x0, x1 in ((-LANE / 2, -inside), (inside, LANE / 2)):
        st.block(4, 4 + pool, GROUND, FLOOR + WALL_HEIGHT, name="Pool Side", x0=x0, x1=x1)
    # Its ends, under the decks either side, so it's walled all round (the ladder out stands against the far one).
    for z0, z1 in ((3, 4), (4 + pool, 5 + pool)):
        st.block(z0, z1, GROUND, FLOOR - DECK, name="Pool End", x0=-LANE / 2, x1=LANE / 2)
    st.A("Water", name="Pool", centre=[0, r(4 + pool / 2)], size=[2 * inside, pool], bottom=bottom, surface=r(FLOOR - 0.4))
    arch = 4 + pool / 2
    # Across the pool's sides too, and too high to get over from a wall run along them.
    st.block(arch - 1, arch + 1, bottom + ARCH_CLEAR, FLOOR + ARCH_TOP, name=f"Arch ({ARCH_CLEAR:g} m clear under water)",
             x0=-LANE / 2, x1=LANE / 2)
    st.feature(4, 4 + pool, 0.0, 0.0)
    st.A("Ladder", at=[0, r(4 + pool)], floor=bottom, height=POOL_DEPTH, facing=[0, -1])
    # A pool's look: white walls under the water, lane lines on its floor, and a cyan light under the arch to dive for.
    for x0, x1 in ((-inside, -inside + 0.05), (inside - 0.05, inside)):
        st.trim("Pool Tiles", x0, x1, 4, 4 + pool, bottom, FLOOR - 0.5, "White")
    for x in (-inside / 2, inside / 2):
        st.trim("Lane Line", x - 0.15, x + 0.15, 5, 3 + pool, bottom, bottom + 0.02, "Graphite")
    st.trim("Arch Light", -inside, inside, arch - 1, arch + 1, bottom + ARCH_CLEAR - 0.12, bottom + ARCH_CLEAR, "Cyan")
    st.deck(4 + pool, 10 + pool)
    cross(Crossing("L17 arch", "passage", 2.0, {"swim"}, ceiling=ARCH_CLEAR, underwater=True, fixtures={"water"}), taught | {"swim"})
    return 10 + pool, {"swim"}


def lesson_L18(st, taught):
    g = gap_between(reach("sprint jump", "gap"), reach("sprint jump and air dash", "gap"))
    run_up = 16.0
    st.deck(0, run_up, name="Run-up")
    st.deck(run_up + g, run_up + g + 8, name="Landing")
    st.gap(run_up, run_up + g)
    st.label(f"{g:.1f} m", run_up - 2)
    cross(Crossing("L18 gap", "gap", g, {"sprint jump and air dash"}), taught | {"sprint jump and air dash", "dash"})
    return run_up + g + 8, {"sprint jump and air dash", "dash"}


#: The lessons, in the design's order, grouped onto the lessons' four faces.
LESSONS = [
    # Basics and the first low move.
    [("L01-move", lesson_L01), ("L02-jump", lesson_L02), ("L03-sprint", lesson_L03), ("L04-crouch", lesson_L04)],
    # Low moves, then walls and ledges.
    [("L05-slide", lesson_L05), ("L06-roll", lesson_L06), ("L07-vault", lesson_L07), ("L08-ledge", lesson_L08), ("L09-wallrun", lesson_L09)],
    # Walls, climbing, and props.
    [("L10-walljump", lesson_L10), ("L11-climb", lesson_L11), ("L12-ladder", lesson_L12), ("L13-crate", lesson_L13), ("L14-bounce", lesson_L14)],
    # Lines, water, and the dash last.
    [("L15-rope", lesson_L15), ("L16-zipline", lesson_L16), ("L17-swim", lesson_L17), ("L18-dash", lesson_L18)],
]


# ---- The course: four stretches, each a share of the moves' reach harder than the last ----

def slide_into_jump(st, z, share, taught):
    """A low beam to slide under, then a gap a sprint jump (share of its reach) crosses (a dash can always too)."""
    g = r(max(share * reach("sprint jump", "gap"), reach("jump", "gap") / locks.OTHER_SHARE + 0.05))
    st.deck(z, z + 14, name="Run-up")
    st.A("LowBeam", name="Slide Beam", a=[-LANE / 2, r(z + 9)], b=[LANE / 2, r(z + 9)], floor=FLOOR, clear=CEILING)
    st.stripes(z + 9 - 0.3, -1)  # Slide here.
    st.deck(z + 14 + g, z + 20 + g, name="Landing")
    st.gap(z + 14, z + 14 + g)
    cross(Crossing(f"{st.name}: slide into a jump", "gap", g, {"sprint jump"}, also_allowed={"sprint jump and air dash"}), taught)
    return z + 20 + g


def ledge(st, z, share, taught, name="Ledge"):
    h = r(max(share * reach("jump to the ledge", "climb"), reach("jump up", "climb") / locks.OTHER_SHARE + 0.05))
    st.block(z, z + 5, GROUND, FLOOR + h - DECK, name=f"{name} (+{h:g})")
    st.feature(z, z + 5, CLIMB_CLEAR)
    st.deck(z, z + 5, FLOOR + h, name=f"{name} Top")
    st.stripes(z, 1, FLOOR + h)  # The lip to reach for.
    st.deck(z + 5, z + 10, name="Below")
    cross(Crossing(f"{st.name}: {name.lower()}", "climb", h, {"jump to the ledge"}), taught)
    return z + 10


def stretch_0(st, share, taught):
    z = slide_into_jump(st, 0, share, taught)
    # Rope to zipline: a gap too wide for anything but its ropes, then one crossed on a zipline.
    g = r(reach("sprint jump and air dash", "gap") / locks.OTHER_SHARE + 1)
    st.deck(z + g, z + g + 4, name="Rope Landing")
    st.gap(z, z + g)
    for i in (1, 2):
        anchor = FLOOR + ROPE_LENGTH + 1.5
        st.A("Rope", anchor=[0, r(anchor), r(z + g * i / 3)], length=ROPE_LENGTH)
        # A gantry it hangs from: a beam across the gap on two posts outside the lane, from the ground.
        za = z + g * i / 3
        st.trim("Rope Gantry", -LANE / 2 - 2, LANE / 2 + 2, za - 0.25, za + 0.25, anchor + 0.15, anchor + 0.75, "Graphite")
        for x in (-LANE / 2 - 1.75, LANE / 2 + 1.75):
            st.trim("Gantry Post", x - 0.25, x + 0.25, za - 0.25, za + 0.25, GROUND, anchor + 0.75, "Steel")
    cross(Crossing(f"{st.name}: ropes", "gap", g, {"rope"}, fixtures={"rope"}), taught)
    z, drop = zipline_gap(st, z + g + 4, g)
    cross(Crossing(f"{st.name}: zipline", "gap", g, {"zipline"}, fixtures={"zipline"}), taught)
    return ledge(st, z, share, taught)


def stretch_1(st, share, taught):
    z = slide_into_jump(st, 0, share, taught)
    # A bounce pad into a wall run.
    g = r(max(share * reach("wall run", "gap"), reach("sprint jump", "gap") / locks.OTHER_SHARE + 0.1))
    st.deck(z, z + 8, name="Pad Deck")
    st.A("BouncePad", at=[r(LANE / 2 - 2), r(z + 6)], floor=FLOOR, size=2, height=3)
    edge = LANE / 2 - WALL_THICKNESS / 2
    st.tall_wall("Wall-Run Wall", [edge, r(z + 4)], [edge, r(z + 8 + g + 4)])
    # Its run line: a cyan band along the wall at the height a wall run holds, from the pad to past the gap.
    face = LANE / 2 - WALL_THICKNESS
    st.trim("Run Line", face - 0.05, face, z + 4, z + 8 + g + 4, FLOOR + 1.5, FLOOR + 1.8, "Cyan")
    # The bounce pad picked out.
    px, pz, half_pad = LANE / 2 - 2, z + 6, 1.4
    for a, b, inward in (([px - half_pad, pz - half_pad], [px + half_pad, pz - half_pad], [0, 1]),
                         ([px - half_pad, pz + half_pad], [px + half_pad, pz + half_pad], [0, -1]),
                         ([px - half_pad, pz - half_pad], [px - half_pad, pz + half_pad], [1, 0])):
        st.A("HazardStripes", a=[r(a[0]), r(a[1])], b=[r(b[0]), r(b[1])], inward=inward, height=FLOOR)
    st.gap(z + 8, z + 8 + g, walled=True)  # A wall run is what it's for.
    st.deck(z + 8 + g, z + 16 + g, name="Landing")
    cross(Crossing(f"{st.name}: bounce into a wall run", "gap", g, {"wall run", "bounce"}, wall_beside=True,
                   fixtures={"bounce pad"}, also_allowed={"sprint jump and air dash"}), taught)
    return ledge(st, z + 16 + g, share, taught)


def stretch_2(st, share, taught):
    z = slide_into_jump(st, 0, share, taught)
    # A wall run along a corridor of two walls into wall jumps up its far end.
    g = r(max(share * reach("wall run", "gap"), reach("sprint jump", "gap") / locks.OTHER_SHARE + 0.1))
    corridor = 4.0
    st.deck(z, z + 14, name="Run-up")
    h = r(max(6.0, reach("jump to the ledge", "climb") / locks.OTHER_SHARE + 0.5))
    top = z + 14 + g
    # The corridor is cut through solid wall: from its sides out to the lane's edges, from the ground to the walls' top.
    for x0, x1 in ((-LANE / 2, -corridor / 2), (corridor / 2, LANE / 2)):
        st.block(z + 10, top + 6, GROUND, FLOOR + WALL_HEIGHT, name="Corridor Wall", x0=x0, x1=x1)
    st.block(top, top + 6, GROUND, FLOOR + h - DECK, name=f"Corridor End (+{h:g})", x0=-corridor / 2, x1=corridor / 2)
    st.walled.append((z + 14, top + 6))  # Walled through: a wall run, then wall jumps, are what it's for.
    # The cut's look: dark faces, a cyan line along each at its foot, and orange rungs up the end to the lip.
    for x0, x1 in ((-corridor / 2, -corridor / 2 + 0.05), (corridor / 2 - 0.05, corridor / 2)):
        st.trim("Cut Face", x0, x1, z + 10, top, FLOOR, FLOOR + WALL_HEIGHT - 2, "Graphite")
        st.trim("Cut Light", x0, x1, z + 10, top, FLOOR + 0.3, FLOOR + 0.5, "Cyan")
    st.face_bars(top, -corridor / 2 + 0.2, corridor / 2 - 0.2, FLOOR + 1, FLOOR + h - 0.6, 1.2)
    st.deck(top, top + 6, FLOOR + h, name="Corridor End Top", x0=-corridor / 2, x1=corridor / 2)
    st.deck(top + 6, top + 12, name="Below")
    cross(Crossing(f"{st.name}: wall run", "gap", g, {"wall run", "wall jump"}, wall_beside=True, fixtures={"walls"},
                   also_allowed={"sprint jump and air dash"}), taught)
    cross(Crossing(f"{st.name}: wall jumps", "climb", h, {"wall jump"}, fixtures={"walls"}), taught)
    return ledge(st, top + 12, share, taught)


def stretch_3(st, share, taught):
    z = slide_into_jump(st, 0, share, taught)
    # A dash to a ledge grab.
    g = r(max(share * reach("sprint jump and air dash", "gap"), reach("sprint jump", "gap") / locks.OTHER_SHARE + 0.1))
    st.deck(z, z + 16, name="Run-up")
    land = z + 16 + g
    st.deck(land, land + 3, name="Dash Landing")
    st.gap(z + 16, land)
    z = ledge(st, land + 3, share, taught, name="Dash Ledge")
    cross(Crossing(f"{st.name}: dash", "gap", g, {"sprint jump and air dash"}), taught)
    # A crate to a ledge to a climb.
    ledge_reach = reach("jump to the ledge", "climb")
    hc = gap_between(ledge_reach, ledge_reach + CRATE)
    st.deck(z, z + 8, name="Crate Deck")
    st.A("PushableCrate", at=[0, r(z + 3)], floor=FLOOR, size=CRATE, reset="C4-start")
    st.block(z + 8, z + 13, GROUND, FLOOR + hc - DECK, name=f"Crate Wall (+{hc:g})")
    st.feature(z + 8, z + 18, CLIMB_CLEAR)
    st.deck(z + 8, z + 13, FLOOR + hc, name="Crate Wall Top")
    cross(Crossing(f"{st.name}: crate", "climb", hc, {"crate"}, fixtures={"crate"}), taught)
    hp = r(max(6.0, ledge_reach / locks.OTHER_SHARE + 0.5))
    base = FLOOR + hc
    st.block(z + 13, z + 18, GROUND, base + hp - DECK, name=f"Panel Wall (+{hp:g})")
    st.A("ClimbablePanel", a=[-2.5, r(z + 13)], b=[2.5, r(z + 13)], y0=r(base), y1=r(base + hp), facing=[0, -1])
    # The panel framed in light, so the last climb reads from the crate.
    for x0, x1 in ((-2.9, -2.65), (2.65, 2.9)):
        st.trim("Panel Light", x0, x1, z + 12.9, z + 13, base, base + hp, "Light")
    st.trim("Panel Light", -2.9, 2.9, z + 12.9, z + 13, base + hp - 0.25, base + hp, "Light")
    st.deck(z + 13, z + 18, base + hp, name="Panel Wall Top")
    st.deck(z + 18, z + 24, name="Below")
    st.label("FINISH", z + 21, size=2.0)
    cross(Crossing(f"{st.name}: panel", "climb", hp, {"climb"}, fixtures={"panel"}), taught)
    return z + 24


STRETCHES = [stretch_0, stretch_1, stretch_2, stretch_3]
#: The course's combinations (the design's), and the stretch each is in, for the tests.
COMBOS = {"slide into a jump": 0, "rope to zipline": 0, "bounce pad into a wall run": 1, "wall run into a wall jump": 2,
          "dash to a ledge grab": 3, "crate to a ledge to a climb": 3}


# ---- The ring ----

def world(radius, angle):
    """A point radius from the middle at angle degrees clockwise from north."""
    a = math.radians(angle)
    return [r(radius * math.sin(a)), r(radius * math.cos(a))]


def hub_reach():
    """How far the hub's corners reach from its middle."""
    return HUB_APOTHEM / math.cos(math.radians(22.5))


def face_half_length():
    return half(RING_APOTHEM - LANE / 2)


def build():
    CROSSINGS.clear()
    WALL_PLAN.clear()
    TOPS.clear()
    s = Spec("Tutorial Ring", SCENE)
    A = s.add
    inner, outer = RING_APOTHEM - LANE / 2, RING_APOTHEM + LANE / 2
    h = face_half_length()

    # ---- The ground (a hazard) and the hub ----
    s.part("Ground and Hub")
    A("Group", parent="", name="Tutorial Ring")
    root = "Tutorial Ring"
    A("Ground", parent=root, half=r(outer + 60), openings=[])
    A("HazardVolume", parent=root, name="Fall", centre=[0, 1, 0], size=[r(2 * (outer + 60)), 6, r(2 * (outer + 60))])
    hub_centre = world(RING_APOTHEM / math.cos(math.radians(22.5)), HUB_ANGLE)
    A("Prism", parent=root, name="Hub", shape=octagon(HUB_APOTHEM, *hub_centre), y0=FLOOR - DECK, y1=FLOOR, surface="Concrete")
    A("Group", parent=root, name="Spawn Points")
    # Facing the way the first lesson's lane runs (its face's -x).
    facing = (45 * RUN_FACES[0] - 90) % 360
    A("SpawnMarker", parent=f"{root}/Spawn Points", name="Hub", at=hub_centre, height=FLOOR + 0.1, yaw=facing)
    A("Checkpoint", parent=root, id="hub", feet=[hub_centre[0], FLOOR, hub_centre[1]], yaw=facing, size=[10, 3, 10], start=True)
    # The hub marked as the start and the finish: an inlaid octagon on its floor and its name, read facing the first lane.
    A("GuideLine", parent=root, name="Hub Inlay", points=octagon(HUB_APOTHEM - 3, *hub_centre), height=FLOOR, surface="Accent", closed=True)
    ahead = [r(math.sin(math.radians(facing))), r(math.cos(math.radians(facing)))]
    A("FloorLabel", parent=root, text="START / FINISH", at=hub_centre, height=FLOOR, facing=ahead, size=1.4)
    # The ground marked out under the ring, inside and out, so the drop has a scale.
    for apothem in (inner - 10, inner - 30, outer + 10, outer + 30):
        A("GuideLine", parent=root, name="Ground Line", points=octagon(apothem), height=GROUND, surface="Light", closed=True)

    # ---- The lanes and corners, face by face, with their lessons and stretches ----
    taught = set(BASE_MOVES)
    for i, k in enumerate(RUN_FACES):
        part = f"{'Lessons' if i < 4 else 'Course'} {i % 4 + 1} ({['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'][k]})"
        s.part(part)
        face = f"{root}/Face {k}"
        A("Turned", parent=root, name=f"Face {k}", yaw=45 * k)
        A("Prism", parent=face, name="Corner", shape=corner(inner, outer), y0=FLOOR - DECK, y1=FLOOR, surface="Concrete")
        # Its outer edges walled from the ground up to it, as the lanes' are, and the inner point between the two lanes'.
        wall_chain(A, face, corner_line(h, outer - SKIRT / 2), SKIRT, GROUND, FLOOR - DECK, "Corner Skirt")
        inner_joint(A, k, h, inner, SKIRT, GROUND, FLOOR - DECK, "Corner Skirt")
        cursor = h
        plan = {"face": k, "frame": face, "floors": [], "keep_clear": [], "registered": [], "starts": []}
        WALL_PLAN.append(plan)

        def note(st, at):
            """The stretch's plain floor and keep-clear zones, along its face's lane (u: metres from its start)."""
            plan["floors"] += [(at + z0, at + z1) for z0, z1 in st.floors]
            plan["keep_clear"] += [(at + z0, at + z1) for z0, z1 in st.keep_clear]  # The outer side's only.
            plan["registered"] += [(at + z0, at + z1, before, after) for z0, z1, before, after in st.features]
            plan["floors"] += [(at + z0, at + z1) for z0, z1 in st.walled]

        if i < 4:
            for lesson, make in LESSONS[i]:
                A("Placed", parent=face, name=lesson, position=[r(cursor), 0, RING_APOTHEM], yaw=-90)
                st = Stretch(s, face, lesson)
                st.checkpoint(lesson)
                # Its number and name on the floor at its start, reading along the way.
                st.label(lesson[:3], 5.0, size=1.6)
                st.label(LESSON_TITLES[lesson], 3.0, size=0.8)
                plan["starts"].append((h - cursor, lesson[1:3]))
                length, moves = make(st, taught)
                st.zone(lesson, length)
                note(st, h - cursor)
                taught |= moves
                cursor -= length
        else:
            n = i - 4
            name = f"Stretch {n + 1}"
            A("Placed", parent=face, name=name, position=[r(cursor), 0, RING_APOTHEM], yaw=-90)
            st = Stretch(s, face, name)
            st.checkpoint(f"C{n + 1}-start")
            role, index = ("start", None) if n == 0 else ("split", n - 1)
            # Its posts in the lane's walls, where they stand.
            post = LANE / 2 - WALL_THICKNESS / 2 - GATE_POST / 2
            st.A("TimingGate", course="Agility", role=role, a=[-post, TIMING_GATE_AT], b=[post, TIMING_GATE_AT], floor=FLOOR,
                 approach=[0, 1], display=True if n == 0 else None, index=index)
            # Its number and name at its start, and its gate framed in its colour: a bar overhead and one up each wall.
            title, colour = STRETCH_THEMES[n]
            st.label(str(n + 1), TIMING_GATE_AT + 5, size=3.0)
            st.label(title, TIMING_GATE_AT + 2.5, size=0.8)
            plan["starts"].append((h - cursor, str(n + 1)))
            face_in = LANE / 2 - WALL_THICKNESS
            st.trim("Gate Frame", -face_in, face_in, TIMING_GATE_AT - 0.3, TIMING_GATE_AT + 0.3, FLOOR + WALL_HEIGHT - 2.4,
                    FLOOR + WALL_HEIGHT - 1.8, colour)
            for x0, x1 in ((-face_in, -face_in + 0.06), (face_in - 0.06, face_in)):
                st.trim("Gate Frame", x0, x1, TIMING_GATE_AT - 0.3, TIMING_GATE_AT + 0.3, FLOOR, FLOOR + WALL_HEIGHT - 1.8, colour)
            length = STRETCHES[n](st, SHARES[n], taught)
            note(st, h - cursor)
            cursor -= length
        if cursor < -h - 1e-6:
            raise LockError(f"Face {k}'s lessons or stretch are {r(h - cursor)} m long: longer than its {r(2 * h)} m lane.")
        if cursor > -h + 1e-6:
            A("Platform", parent=face, name="Lane", x0=r(-h), x1=r(cursor), z0=r(inner), z1=r(outer), y0=FLOOR - DECK, top=FLOOR, coping="Accent")
            for z0, z1 in ((inner, inner + SKIRT), (outer - SKIRT, outer)):
                A("Block", parent=face, name="Deck Skirt", x0=r(-h), x1=r(cursor), z0=r(z0), z1=r(z1), y0=GROUND, y1=FLOOR - DECK, surface="Panel")
            plan["floors"].append((h - cursor, 2 * h))
        if i == 3:
            build_lessons_end(s, face, h)
    build_walls(s, h)
    build_course_end(s, f"{root}/Face {RUN_FACES[-1]}", h)
    build_hub_walls(s, h)
    return s.spec


def _to_frame(point, yaw, origin=(0.0, 0.0)):
    """A world point (x, z) in a frame turned yaw degrees about origin (the inverse of geometry.Frame.point)."""
    a = math.radians(yaw)
    c, sn = math.cos(a), math.sin(a)
    x, z = point[0] - origin[0], point[1] - origin[1]
    return (x * c - z * sn, x * sn + z * c)


def build_hub_walls(s, h):
    """
    The hub's walls: tall walls round its edges, from the ground, but where its three ways come in: the first lesson's
    lane, the course's lane, and the shuttle's dock. Each edge is walled wherever it isn't over one of them.
    """
    A = s.add
    root = "Tutorial Ring"
    cos = math.cos(math.radians(22.5))
    centre = world(RING_APOTHEM / cos, HUB_ANGLE)
    inner_r, outer_r = RING_APOTHEM - LANE / 2, RING_APOTHEM + LANE / 2
    corner_z = RING_APOTHEM / cos

    def in_a_way(p):
        for k in (RUN_FACES[0], RUN_FACES[-1]):                         # The two lanes at the hub.
            x, z = _to_frame(p, 45 * k)
            if abs(x) <= h and inner_r <= z <= outer_r:
                return True
        x, z = _to_frame(p, HUB_ANGLE)                                   # The shuttle's dock.
        return abs(x) <= DOCK_WIDTH / 2 and z <= corner_z

    corners = octagon(HUB_APOTHEM, *centre)
    step = 0.25
    starts, ends = {}, {}                                                # Walls reaching each corner, by edge.
    for i in range(8):
        a, b = corners[i], corners[(i + 1) % 8]
        length = math.dist(a, b)
        # Out from the hub's middle by half the wall's thickness, so it stands outside its floor.
        mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
        out = ((mid[0] - centre[0]) / HUB_APOTHEM * WALL_THICKNESS / 2, (mid[1] - centre[1]) / HUB_APOTHEM * WALL_THICKNESS / 2)
        n = max(1, int(length / step))
        free = [not in_a_way((a[0] + (b[0] - a[0]) * (j + 0.5) / n, a[1] + (b[1] - a[1]) * (j + 0.5) / n)) for j in range(n)]
        j = 0
        while j < n:
            if not free[j]:
                j += 1
                continue
            k = j
            while k < n and free[k]:
                k += 1
            if (k - j) * length / n >= 1.0:
                t0, t1 = j / n, k / n
                p0 = [r(a[0] + (b[0] - a[0]) * t0 + out[0]), r(a[1] + (b[1] - a[1]) * t0 + out[1])]
                p1 = [r(a[0] + (b[0] - a[0]) * t1 + out[0]), r(a[1] + (b[1] - a[1]) * t1 + out[1])]
                A("Wall", parent=root, name="Hub Wall", a=p0, b=p1, y0=GROUND, y1=FLOOR + WALL_HEIGHT, thickness=WALL_THICKNESS)
                dress_wall(A, root, p0, p1, WALL_THICKNESS, FLOOR + WALL_HEIGHT)
                if j == 0:
                    starts[i] = (p0, p1)
                if k == n:
                    ends[i] = (p0, p1)
            j = k
    # A wedge at each of the hub's corners its walls go round, so they join without a notch.
    for i in range(8):
        before, after = ends.get((i - 1) % 8), starts.get(i)
        if before and after:
            a, p = before
            q, b = after
            wedge = _hull(_ends(a, p, WALL_THICKNESS)[1] + _ends(q, b, WALL_THICKNESS)[0])
            if len(wedge) >= 3:
                A("Prism", parent=root, name="Hub Wall (join)", shape=ccw([[r(x), r(z)] for x, z in wedge]), y0=GROUND,
                  y1=FLOOR + WALL_HEIGHT, surface="Panel")


#: The highest any deck stands over FLOOR, as built (a climb's top): the walls rise beyond reach of it.
TOPS = []


#: For each face of the run, in order: its plain floor and keep-clear zones (u, metres along its lane from its start),
#: and the walls built along it: what build_walls worked out, for the tests.
WALL_PLAN = []


def _merge(intervals):
    out = []
    for a, b in sorted(intervals):
        if out and a <= out[-1][1] + 1e-6:
            out[-1] = (out[-1][0], max(out[-1][1], b))
        else:
            out.append((a, b))
    return out


def _subtract(intervals, cuts):
    out = []
    for a, b in intervals:
        pieces = [(a, b)]
        for c0, c1 in cuts:
            pieces = [p for q in pieces for p in ((q[0], min(q[1], c0)), (max(q[0], c1), q[1])) if p[1] - p[0] > 1e-6]
        out += pieces
    return out


def build_walls(s, h):
    """
    The ring's walls: along both edges of each lane, wherever its floor is plain and WALL_CLEAR from anything that isn't
    (a gap, a climb, the pool, a step: the lane's crossings), the neighbouring faces' near ends counted too (through
    the corner between, as if it had no length); out of the keep-clear zones; and clear of the hub, and of the inner
    corner the shuttle's dock comes in at. Each face's outer corner gets walls too where both lanes either side have
    them up to it. From the ground to WALL_HEIGHT over the decks.
    """
    if WALL_CLEAR < gap_clear(0.0):
        raise LockError(f"WALL_CLEAR ({WALL_CLEAR} m) is shorter than a jump off a wall's end carries: it could reach a crossing.")
    tops = reach("jump to the ledge", "climb") / locks.OTHER_SHARE
    if CLIMB_CLEAR < reach("sprint jump", "gap") or WALL_HEIGHT < max(TOPS, default=0.0) + tops:
        raise LockError("The walls come too close to a climb, or are low enough to get onto from a climb's top.")
    A = s.add
    span = 2 * h
    # The lanes' walls meet the hub's at its edge: the hub's middle is the ring's corner, past the lane's end (its u = 0,
    # at the inner apothem's corner) by the difference between the corner's half-widths.
    hub_clear = HUB_APOTHEM - (half(RING_APOTHEM) - h)
    dock_clear = (DOCK_WIDTH / 2 + WALL_THICKNESS) / math.cos(math.radians(22.5))  # and the shortcut's at the lessons' end.
    # Each face's crossings: where its lane isn't plain floor, each with the clearance its crossing asked for (the
    # closest of those it holds), or WALL_CLEAR if none did.
    for plan in WALL_PLAN:
        floors = _merge(plan["floors"])
        plan["features"] = []
        for a, b in _subtract([(0.0, span)], floors):
            asked = [(before, after) for f0, f1, before, after in plan["registered"] if f0 < b - 1e-6 and f1 > a + 1e-6]
            before = max((x for x, _ in asked), default=WALL_CLEAR)
            after = max((y for _, y in asked), default=0.0)
            plan["features"].append((a, b, before, after))
    n = len(WALL_PLAN)
    for i, plan in enumerate(WALL_PLAN):
        before, after = WALL_PLAN[i - 1], WALL_PLAN[(i + 1) % n]
        near = list(plan["features"])
        near += [(a - span, b - span, c, d) for a, b, c, d in before["features"]]   # The face run before ends where this starts.
        near += [(a + span, b + span, c, d) for a, b, c, d in after["features"]]    # The one after starts where this ends.
        cuts = [(a - c, b + d) for a, b, c, d in near]
        if i == 0:
            cuts.append((-1.0, hub_clear))                               # The hub, at the first face's start
        if i == n - 1:
            cuts.append((span - hub_clear, span + 1))                    # and the last face's end.
        walls = {}
        for side in ("inner", "outer"):
            side_cuts = list(cuts) + (plan["keep_clear"] if side == "outer" else [])
            if side == "inner" and i == 3:
                side_cuts.append((span - dock_clear, span + 1))          # The shuttle's dock, at the lessons' end
            if side == "inner" and i == 4:
                side_cuts.append((-1.0, dock_clear))                     # (the corner between the fourth face and the fifth).
            walls[side] = [w for w in _subtract(_merge(plan["floors"]), side_cuts) if w[1] - w[0] >= 2.0]
        plan["walls"] = walls
        joined = {hub_clear} if i == 0 else {span - hub_clear} if i == n - 1 else set()
        for side, edge in (("inner", RING_APOTHEM - LANE / 2 + WALL_THICKNESS / 2), ("outer", RING_APOTHEM + LANE / 2 - WALL_THICKNESS / 2)):
            side_joined = joined | ({span - dock_clear} if side == "inner" and i == 3 else {dock_clear} if side == "inner" and i == 4 else set())
            toward_lane = 1 if side == "inner" else -1
            for u0, u1 in walls[side]:
                a, b = [r(h - u0), r(edge)], [r(h - u1), r(edge)]
                A("Wall", parent=plan["frame"], name=f"Ring Wall ({side})", a=a, b=b, y0=GROUND, y1=FLOOR + WALL_HEIGHT, thickness=WALL_THICKNESS)
                dress_wall(A, plan["frame"], a, b, WALL_THICKNESS, FLOOR + WALL_HEIGHT)
                # Where it stops for a crossing (not at a corner, the hub or the shortcut, where another wall meets it), an
                # end post with an orange strip down its lane side, so the break reads as meant.
                for u in (u0, u1):
                    if u <= 1e-6 or u >= span - 1e-6 or any(abs(u - j) < 0.6 for j in side_joined):
                        continue
                    end_post(A, plan["frame"], h - u, edge, toward_lane)
            # A beam across each short break, at the walls' top, tying the two ends together (far out of reach).
            for (_, e0), (s1, _) in zip(walls[side], walls[side][1:]):
                if s1 - e0 <= LINTEL_SPAN:
                    A("Trim", parent=plan["frame"], name="Ring Wall (lintel)", centre=[r(h - (e0 + s1) / 2), r(edge)], size=[r(s1 - e0), WALL_THICKNESS],
                      y0=FLOOR + WALL_HEIGHT - LINTEL, y1=FLOOR + WALL_HEIGHT, yaw=0, surface="Graphite")
        # A banner with the number of each lesson and stretch on a wall at its start, where there's one.
        for u, number in plan["starts"]:
            for side, face_z, outward in (("inner", RING_APOTHEM - LANE / 2 + WALL_THICKNESS, 1), ("outer", RING_APOTHEM + LANE / 2 - WALL_THICKNESS, -1)):
                if any(w0 <= u + 3 and w1 >= u + 9 for w0, w1 in walls[side]):
                    A("Banner", parent=plan["frame"], wallPoint=[r(h - u - 6), r(face_z)], outward=[0, outward], top=FLOOR + 10, length=5.0, number=number)
                    break
    # The corners: the outer walls round each corner's outer flats where the walls on both sides reach it, and a wedge
    # where the inner walls of the two faces meet at its inner point (not the hub's corner, the first face's start).
    inner_a, outer_b = RING_APOTHEM - LANE / 2, RING_APOTHEM + LANE / 2
    for i, plan in enumerate(WALL_PLAN):
        if i == 0:
            continue
        before = WALL_PLAN[i - 1]
        reaches = {side: any(u0 <= 1e-6 for u0, _ in plan["walls"][side]) and any(u1 >= span - 1e-6 for _, u1 in before["walls"][side])
                   for side in ("inner", "outer")}
        if reaches["outer"]:
            wall_chain(A, plan["frame"], corner_line(h, outer_b - WALL_THICKNESS / 2), WALL_THICKNESS, GROUND, FLOOR + WALL_HEIGHT, "Ring Wall (corner)")
        if reaches["inner"]:
            inner_joint(A, plan["face"], h, inner_a, WALL_THICKNESS, GROUND, FLOOR + WALL_HEIGHT, "Ring Wall (corner)")


def corner_line(h, edge):
    """The line along a corner's outer flats at edge (an apothem), in its face's frame: from where the face's lane ends,
    round the bend, to where the next face's lane starts."""
    inner_a = RING_APOTHEM - LANE / 2
    d = edge - inner_a
    return [[r(h), r(edge)], [r(half(edge)), r(edge)], [r(half(inner_a) + d / math.sqrt(2)), r(inner_a + d / math.sqrt(2))]]


def _hull(points):
    """The convex hull of points (x, z), counterclockwise (monotone chain)."""
    pts = sorted(set((round(p[0], 4), round(p[1], 4)) for p in points))
    if len(pts) < 3:
        return [list(p) for p in pts]

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])

    lower, upper = [], []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    for p in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return [list(p) for p in lower[:-1] + upper[:-1]]


def _ends(a, b, thickness):
    """The corners of a wall's end faces, from a to b, thickness thick: (at a), (at b)."""
    dx, dz = b[0] - a[0], b[1] - a[1]
    n = math.hypot(dx, dz) or 1.0
    px, pz = -dz / n * thickness / 2, dx / n * thickness / 2
    return [(a[0] + px, a[1] + pz), (a[0] - px, a[1] - pz)], [(b[0] + px, b[1] + pz), (b[0] - px, b[1] - pz)]


def wall_chain(A, parent, points, thickness, y0, y1, name, piece="Wall"):
    """Walls along a line of points, with a wedge filling each bend between two, so they join without a notch (tall
    ones dressed as the ring's are)."""
    for a, b in zip(points, points[1:]):
        A("Wall", parent=parent, name=name, a=a, b=b, y0=y0, y1=y1, thickness=thickness)
        if y1 > FLOOR:
            dress_wall(A, parent, a, b, thickness, y1)
    for a, p, b in zip(points, points[1:], points[2:]):
        wedge = _hull(_ends(a, p, thickness)[1] + _ends(p, b, thickness)[0] + [tuple(p)])
        if len(wedge) >= 3:
            A("Prism", parent=parent, name=f"{name} (join)", shape=ccw([[r(x), r(z)] for x, z in wedge]), y0=y0, y1=y1, surface="Panel")


def dress_wall(A, parent, a, b, thickness, top):
    """
    A tall wall's dressing, to see only: a graphite coping along its top, and on both faces a graphite band at its foot at
    deck height (BAND high) with an orange stripe along its top, so the deck's level reads along every wall.
    """
    dx, dz = b[0] - a[0], b[1] - a[1]
    length = math.hypot(dx, dz)
    if length < 0.5:
        return
    yaw = r(math.degrees(math.atan2(dx, dz)))
    mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
    px, pz = dz / length, -dx / length
    A("Trim", parent=parent, name="Coping", centre=[r(mid[0]), r(mid[1])], size=[r(thickness + 0.12), r(length)],
      y0=r(top - COPING), y1=r(top + 0.06), yaw=yaw, surface="Graphite")
    for side in (1, -1):
        for offset, y0, y1, surface in ((thickness / 2 + 0.02, FLOOR, FLOOR + BAND, "Graphite"), (thickness / 2 + 0.025, FLOOR + BAND, FLOOR + BAND + 0.12, "Accent")):
            c = (mid[0] + px * side * offset, mid[1] + pz * side * offset)
            A("Trim", parent=parent, name="Wall Band", centre=[r(c[0]), r(c[1])], size=[0.05 if surface == "Accent" else 0.04, r(length)],
              y0=r(y0), y1=r(y1), yaw=yaw, surface=surface)


def end_post(A, parent, x, z, toward_lane):
    """An end post where a ring wall stops (at x along its face, its line at z): graphite round the wall's end, from the
    ground to just over its top, with an orange strip down its lane side."""
    A("Trim", parent=parent, name="Wall End", centre=[r(x), r(z)], size=[0.9, r(WALL_THICKNESS + 0.4)], y0=GROUND,
      y1=FLOOR + WALL_HEIGHT + 0.4, yaw=0, surface="Graphite")
    lane_face = z + toward_lane * (WALL_THICKNESS / 2 + 0.2)
    A("Trim", parent=parent, name="Wall End Strip", centre=[r(x), r(lane_face + toward_lane * 0.03)], size=[0.3, 0.06],
      y0=FLOOR, y1=FLOOR + WALL_HEIGHT, yaw=0, surface="Accent")


def face_to_world(point, k):
    """A point (x, z) in face k's frame (a Turned group of yaw 45k), in the world (as geometry.Frame.point)."""
    a = math.radians(45 * k)
    c, sn = math.cos(a), math.sin(a)
    return (point[0] * c + point[1] * sn, -point[0] * sn + point[1] * c)


def inner_joint(A, k, h, inner_a, thickness, y0, y1, name):
    """
    The wedge where the walls along the inner edges of face k's lane and the next face's (clockwise, k + 1) meet, at the
    inner point of the corner between them: they stand inside the lane, so their ends leave a wedge open there.
    """
    mid = inner_a + thickness / 2
    here = [face_to_world((h, mid - thickness / 2), k), face_to_world((h, mid + thickness / 2), k)]
    there = [face_to_world((-h, mid - thickness / 2), k + 1), face_to_world((-h, mid + thickness / 2), k + 1)]
    wedge = _hull(here + there)
    if len(wedge) >= 3:
        A("Prism", parent="Tutorial Ring", name=f"{name} (join)", shape=ccw([[r(x), r(z)] for x, z in wedge]), y0=y0, y1=y1, surface="Panel")


def build_lessons_end(s, face, h):
    """The lessons' end (the north face's end, at the north/north-west corner): the course's start, and the lessons
    shortcut: a shuttle straight across the middle between a dock there and one at the hub, between tall walls, the trigger
    over the lane's corner there (the ring's halfway point) opening the gate on the hub's dock. Built in a frame of its own (a Turned group) whose +z runs from the middle toward
    the hub: the hub's middle is at z = corner, the lessons' end at z = -corner."""
    A = s.add
    root = "Tutorial Ring"
    frame = f"{root}/Lessons Shortcut"
    A("Turned", parent=root, name="Lessons Shortcut", yaw=HUB_ANGLE)
    cos = math.cos(math.radians(22.5))
    corner = RING_APOTHEM / cos
    inner = (RING_APOTHEM - LANE / 2) / cos
    w = DOCK_WIDTH / 2
    # The docks: one into the lane's corner at the lessons' end, one into the hub.
    dock_a = (-corner, -inner + 4)
    dock_b = (corner - hub_reach() - 4, corner - HUB_APOTHEM / 2)
    for name, (z0, z1) in (("Lessons Dock", dock_a), ("Hub Dock", dock_b)):
        A("Platform", parent=frame, name=name, x0=-w, x1=w, z0=r(z0), z1=r(z1), y0=FLOOR - DECK, top=FLOOR, coping="Accent")
    # The shuttle, from the lessons' dock to the hub's and back, its deck level with theirs.
    size = SHUTTLE_SIZE
    za, zb = dock_a[1] + size / 2 + 0.05, dock_b[0] - size / 2 - 0.05
    A("MovingPlatform", parent=frame, label="Shortcut Shuttle", top=[0, FLOOR, r(za)], size=[size, 0.5, size], travel=[0, 0, r(zb - za)],
      period=2 * (SHUTTLE_TRIP + SHUTTLE_PAUSE), pause=SHUTTLE_PAUSE)
    # The trigger over the lane's corner at the lessons' end (the ring's halfway point): reaching it opens the gate across
    # the hub's dock, just outside the hub (clear of its corners).
    A("ShortcutTrigger", parent=frame, gate="lessons", centre=[0, FLOOR + 1.5, r(-corner)], size=[LANE + 8, 4, LANE + 8])
    # Tall walls along both sides of the shuttle's way, from the lane's corner to the hub.
    # Each runs from the lane's inner edge to the hub's edge: both meet the way's line at 22.5 degrees off square, at a
    # point of their octagons, so each wall's end is set where its line crosses them.
    slope = math.tan(math.radians(22.5))
    for x in (-(DOCK_WIDTH / 2 + WALL_THICKNESS / 2), DOCK_WIDTH / 2 + WALL_THICKNESS / 2):
        a, b = [r(x), r(-inner - abs(x) * slope)], [r(x), r(corner - hub_reach() + abs(x) * slope)]
        A("Wall", parent=frame, name="Shortcut Wall", a=a, b=b, y0=GROUND, y1=FLOOR + WALL_HEIGHT, thickness=WALL_THICKNESS)
        dress_wall(A, frame, a, b, WALL_THICKNESS, FLOOR + WALL_HEIGHT)
    # Its mouth at the lessons' end framed: a beam across at the walls' top, with a light under it.
    mouth = -inner + 2
    A("Trim", parent=frame, name="Shortcut Mouth", centre=[0, r(mouth)], size=[DOCK_WIDTH + 2 * WALL_THICKNESS, 1.0],
      y0=FLOOR + WALL_HEIGHT - LINTEL, y1=FLOOR + WALL_HEIGHT, yaw=0, surface="Graphite")
    A("Trim", parent=frame, name="Shortcut Mouth Light", centre=[0, r(mouth)], size=[DOCK_WIDTH, 0.6],
      y0=FLOOR + WALL_HEIGHT - LINTEL - 0.2, y1=FLOOR + WALL_HEIGHT - LINTEL, yaw=0, surface="Cyan")
    # The gate across the dock, into the walls either side.
    gate_z = corner - hub_reach() - 2
    A("ShortcutGate", parent=frame, id="lessons", centre=[0, FLOOR + GATE_HEIGHT / 2, r(gate_z)],
      size=[DOCK_WIDTH + WALL_THICKNESS, GATE_HEIGHT, 0.6], travel=[0, -(GATE_HEIGHT + 0.5), 0], time=1.5)
    # Over the gate, wall up to the walls' top: it's a doorway in them.
    A("Block", parent=frame, name="Over the Lessons Gate", x0=r(-(DOCK_WIDTH + WALL_THICKNESS) / 2), x1=r((DOCK_WIDTH + WALL_THICKNESS) / 2),
      z0=r(gate_z - WALL_THICKNESS / 2), z1=r(gate_z + WALL_THICKNESS / 2), y0=FLOOR + GATE_HEIGHT, y1=FLOOR + WALL_HEIGHT, surface="Panel")


def build_course_end(s, face, h):
    """The course's end (the south face's end, at the hub): its finish, and the gate into the hub, opened from the
    course's side."""
    A = s.add
    # The hub's middle is the lane's corner (x = -half(RING_APOTHEM) in this face's frame): the gate stands on the lane
    # just outside it, clear of its corners, with the trigger and the finish before it on the course's side.
    gate_x = -half(RING_APOTHEM) + hub_reach() + 2
    A("ShortcutTrigger", parent=face, gate="course", centre=[r(gate_x + 4), FLOOR + 1.5, RING_APOTHEM], size=[4, 4, LANE + 2])
    # Into the lane's walls either side where they stand there (as they should: it's plain floor), else past its edges
    # far enough that it can't be jumped round.
    plan = WALL_PLAN[-1]
    u = h - gate_x
    walled = all(any(u0 <= u - 1 and u1 >= u + 1 for u0, u1 in plan["walls"][side]) for side in ("inner", "outer"))
    width = LANE - WALL_THICKNESS if walled else LANE + 2 * GATE_OVERHANG
    A("ShortcutGate", parent=face, id="course", centre=[r(gate_x), FLOOR + GATE_HEIGHT / 2, RING_APOTHEM],
      size=[0.6, GATE_HEIGHT, width], travel=[0, -(GATE_HEIGHT + 0.5), 0], time=1.5)
    post = LANE / 2 - WALL_THICKNESS / 2 - GATE_POST / 2
    A("TimingGate", parent=face, course="Agility", role="finish", a=[r(gate_x + 7), r(RING_APOTHEM - post)],
      b=[r(gate_x + 7), r(RING_APOTHEM + post)], floor=FLOOR, approach=[-1, 0])
    # Over the gate, wall up to the walls' top: it's a doorway in them.
    A("Block", parent=face, name="Over the Course Gate", x0=r(gate_x - WALL_THICKNESS / 2), x1=r(gate_x + WALL_THICKNESS / 2),
      z0=r(RING_APOTHEM - width / 2), z1=r(RING_APOTHEM + width / 2), y0=FLOOR + GATE_HEIGHT, y1=FLOOR + WALL_HEIGHT, surface="Panel")


def check_locks():
    """Every crossing's lock (levelspec.locks): the failures, empty when all pass."""
    return locks.check_all(CROSSINGS, ENV)


def main(argv):
    try:
        spec = levelspec.normalize(build())
        failures = check_locks()
    except LockError as error:
        print(f"Not written: {error}", file=sys.stderr)
        return 1
    if failures:
        print("Not written: these crossings don't lock on their moves:", file=sys.stderr)
        for f in failures:
            print(f"  {f}", file=sys.stderr)
        return 1
    levelspec.validate(spec)
    out = argv[1] if len(argv) > 1 else OUT
    levelspec.write(spec, out)
    n = sum(len(p["pieces"]) for p in spec["parts"])
    print(f"Wrote '{os.path.relpath(out)}': {len(spec['parts'])} parts, {n} pieces, {len(CROSSINGS)} crossings locked.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
