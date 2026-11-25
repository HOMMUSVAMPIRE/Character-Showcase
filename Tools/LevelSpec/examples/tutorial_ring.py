"""Generates the Tutorial Ring level spec: a ring whose first arc teaches every move, one lesson each, and whose second
arc is an agility course mixing them, harder as it goes (ReferenceAssets/Implementation Guidance/TutorialRingDocs; docs/showcase/levels.md, The
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
# The ring's walls, along both edges of every lane, gaps and climbs included: only walls marked for it can be wall-run
# along or slid down (a MarkedWall: the lessons and stretches meant for those moves have them, in the ring wall's place),
# so a plain wall beside a crossing is no help across it. They're too tall to get onto from any floor near them.
CHIMNEY_WIDTH = 3.5           # How far apart the wall-jump lesson's shaft's walls are.
CORRIDOR_WIDTH = 4.0          # How far apart the walls of the course's wall-run corridor are.
WALL_HEIGHT = 17.0            # How far the walls rise over the decks: out of reach from any climb's top (checked).
GROUND = 0.0                  # Where every wall and block goes down to.
SKIRT = 0.5                   # The walls under the decks' edges, from the ground up to them: how thick.
GATE_POST = 0.3               # A timing gate's posts' width (LevelBuilderSettings' Gate Post): its posts stand in the walls.
GATE_TOP = 4.6               # A timing gate's top over its floor: its posts (LevelBuilderSettings' Gate Height) and its beam on them;
GATE_SIGN = 1.1               # and its sign's height over that, where it has one (Gate Panel's). A wall fills above it to the walls' top.
TIMING_GATE_AT = 6.0          # How far into a course stretch its timing gate stands: where the lane's walls are.

# ---- The look: all to see only (Trim, stripes, labels, banners: no colliders), so none of it changes how it plays ----
COPING_BAND = 0.32            # How far below a deck's top its coping band (the Platform's) reaches: Coping Height and Gap.
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
#: The course's stretches' names: at their starts, and on the ground before their faces.
STRETCH_NAMES = ["RIGGING YARD", "RUN WALL", "THE CUT", "FINALE"]
PILASTER_SPACING = 8.0        # How far apart the walls' pilasters are (LevelBuilderSettings' Pilaster Spacing): lamps go between.
PILASTER_MIN = 12.0           # and the shortest wall that has them (its Pilaster Min Wall Length).
LAMP_HEIGHT = 8.0             # How high over the decks the walls' lamps are, every other bay: out of reach, as the walls' tops are.
PIPE_HEIGHT = 12.0            # How high over the decks the pipes along the course's outer walls run.
# Windows in the ring's inner walls and the shuttle's, to see out over the middle by: glass flush with the walls' lane side,
# from over the deck's band to GLASS_BELOW_TOP under the walls' top. It blocks as the wall it's in does.
GLASS_BELOW_TOP = 2.5         # How far below the walls' top a window stops.
GLASS_MIN = 16.0              # The shortest stretch of plain floor an inner wall gets a window beside,
GLASS_MAX = 24.0              # and the longest a window is.
MULLION_EVERY = 4.0           # How far apart a window's mullions are.
ROOF_THICKNESS = 0.08         # The glass roof over the lanes, the hub and the shuttle's way, at the walls' top: its glass,
ROOF_RIB = 8.0                # and its graphite ribs this far apart (under it, from wall to wall).
GROUND_REACH = 1500.0         # How far the ground reaches past the ring, into the haze.
SKYLINE = 40                  # How many blocks stand far off round the ring, in the haze, so the ground doesn't end at the sky.
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
ROPE_BRACKET = 0.3            # How tall a rope's bracket is over its anchor (LevelBuilderSettings' Rope Bracket): its beam sits on it.
ZIPLINE_RISE = 3.2            # How high over its deck a zipline's cable starts (jumped up to).
POOL_DEPTH = 4.0
ARCH_CLEAR = 1.6              # The underwater arch's clearance over the pool's floor.
ARCH_TOP = 8.0                # How high the arch rises over the decks.

ENV = locks.no_air_jumps(levelspec.load_envelope())
CROSSINGS = []                # (Crossing, the moves taught by then): every lesson's and course crossing, as laid out.
BASE_MOVES = {"walk"}


def reach(move, kind=None):
    return locks.reach(move, ENV, kind)


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
        self.keep_clear = []       # Stretches (z0, z1) the outer wall stays out of (a blocker's way).
        self.open = []             # Stretches (z0, z1) with no ring wall either side: something of its own is the wall (the pool).
        self.marked = []           # Walls marked for wall runs or slides: (z0, z1, sides), where the ring's own stop for them.
        self.decks = []            # Its decks (z0, z1, top, x0, x1): the walls' bands follow the deck beside them.

    def gap(self, z0, z1):
        """A gap from z0 to z1 (the ring's walls run on beside it: unmarked, they're no help across it)."""
        # Both lips striped, so the drop reads from either side.
        self.stripes(z0, -1)
        self.stripes(z1, 1)

    def A(self, piece, **settings):
        return self.s.add(piece, parent=self.g, **settings)

    def deck(self, z0, z1, top=FLOOR, name="Deck", x0=-LANE / 2, x1=LANE / 2, coping="Accent"):
        if z1 - z0 > 1e-6:
            self.A("Platform", name=name, x0=r(x0), x1=r(x1), z0=r(z0), z1=r(z1), y0=r(top - DECK), top=r(top), coping=coping)
            TOPS.append(top - FLOOR)
            self.decks.append((z0, z1, top, x0, x1))
            # A dark fascia under its lip at both ends, below its coping band (clear of it, not in front of it), so a deck
            # reads as a deck, not a block of wall.
            for z, side in ((z0, -1), (z1, 1)):
                # A centimetre in from the deck's sides, so its ends aren't in the plane of the skirts' sides.
                self.A("Trim", name="Deck Fascia", centre=[r((x0 + x1) / 2), r(z + side * 0.02)], size=[r(x1 - x0 - 0.02), 0.04],
                       y0=r(top - DECK + 0.1), y1=r(top - COPING_BAND), yaw=0, surface="Graphite")
            # A wall under every edge, from the ground up to the deck, flush with it: the deck stands on something, and
            # there's no seeing under it from a gap.
            if top - DECK - GROUND > 0.5:
                for a, b in ((x0, x0 + SKIRT), (x1 - SKIRT, x1)):
                    self.A("Block", name="Deck Skirt", x0=r(a), x1=r(b), z0=r(z0), z1=r(z1), y0=GROUND, y1=r(top - DECK), surface="Panel")
                for a, b in ((z0, z0 + SKIRT), (z1 - SKIRT, z1)):
                    self.A("Block", name="Deck Skirt", x0=r(x0 + SKIRT), x1=r(x1 - SKIRT), z0=r(a), z1=r(b), y0=GROUND, y1=r(top - DECK), surface="Panel")

    def block(self, z0, z1, y0, y1, name="Block", x0=-LANE / 2, x1=LANE / 2, surface="Panel"):
        self.A("Block", name=name, x0=r(x0), x1=r(x1), z0=r(z0), z1=r(z1), y0=r(y0), y1=r(y1), surface=surface)

    def trim(self, name, x0, x1, z0, z1, y0, y1, surface):
        """A box to see only (no collider), from x0 to x1 across, z0 to z1 along, y0 to y1 up, in a surface."""
        self.A("Trim", name=name, centre=[r((x0 + x1) / 2), r((z0 + z1) / 2)], size=[r(x1 - x0), r(z1 - z0)], y0=r(y0), y1=r(y1),
               yaw=0, surface=surface)

    def stripes(self, z, inward, floor=FLOOR):
        """Hazard stripes across the lane at z on the floor, reaching inward (+1 or -1 along the way) onto it."""
        self.A("HazardStripes", a=[-LANE / 2, r(z)], b=[LANE / 2, r(z)], inward=[0, inward], height=r(floor))

    def run_wall(self, z0, z1):
        """
        A wall marked for wall runs along the lane's outer edge from z0 to z1, as tall as the ring's and from the ground,
        in the ring wall's place there (which stops for it), dressed as they are.
        """
        edge = LANE / 2 - WALL_THICKNESS / 2
        a, b = [edge, r(z0)], [edge, r(z1)]
        self.A("MarkedWall", name=self.name, a=a, b=b, y0=GROUND, y1=FLOOR + WALL_HEIGHT, thickness=WALL_THICKNESS, move="WallRun")
        dress_wall(self.s.add, self.g, a, b, WALL_THICKNESS, FLOOR + WALL_HEIGHT, band=False)
        self.marked.append((z0, z1, ("outer",)))

    def marked_block(self, name, z0, z1, y0, y1, x0, x1, move):
        """
        A block from x0 to x1 across, z0 to z1 along, marked for a move ("WallRun" or "WallSlide": never both), its
        pattern on its sides (the faces along the way).
        """
        a, b = [r((x0 + x1) / 2), r(z0)], [r((x0 + x1) / 2), r(z1)]
        self.A("MarkedWall", name=name, a=a, b=b, y0=r(y0), y1=r(y1), thickness=r(x1 - x0), move=move)
        if y1 >= FLOOR + WALL_HEIGHT - 1e-6:
            dress_wall(self.s.add, self.g, a, b, x1 - x0, y1, band=False)  # Coped as the walls it stands among are.

    def checkpoint(self, cid, z=1.0, start=False, floor=FLOOR):
        # Unmarked: where a lesson starts, its number and name on the floor say so.
        self.A("Checkpoint", id=cid, feet=[0, floor, r(z)], yaw=0, size=[LANE, 3, 2], start=start or None, marked=False)

    def rope(self, z):
        """
        A rope over the lane at z, hanging from an I-beam across the hall from wall to wall (on the rope's bracket), on a
        plate on each wall.
        """
        anchor = FLOOR + ROPE_LENGTH + 1.5
        self.A("Rope", anchor=[0, r(anchor), r(z)], length=ROPE_LENGTH)
        face_in = LANE / 2 - WALL_THICKNESS
        y = anchor + ROPE_BRACKET
        for name, half, y0, y1 in (("Bottom Flange", 0.2, y, y + 0.08), ("Web", 0.04, y + 0.08, y + 0.5), ("Top Flange", 0.2, y + 0.5, y + 0.58)):
            self.trim(f"I-Beam {name}", -face_in, face_in, z - half, z + half, y0, y1, "Graphite")
        for x0, x1 in ((-face_in, -face_in + 0.1), (face_in - 0.1, face_in)):
            self.trim("I-Beam Plate", x0, x1, z - 0.5, z + 0.5, y - 0.4, y + 1.0, "Accent")

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
    st.trim("Low Wall Top", -LANE / 2, LANE / 2, 6, 6.6, FLOOR + VAULT_WALL, FLOOR + VAULT_WALL + 0.02, "Accent")  # Its top, to vault.
    # Shown, not locked: a jump gets over a wall this low too.
    return 12, {"vault"}


def climb_lesson(st, name, height, approach=8.0, top_length=6.0):
    """An approach deck, a block height high to get up (its face at z = approach), and its top; then a drop back to FLOOR."""
    st.deck(0, approach)
    face = approach
    st.block(face, face + top_length, GROUND, FLOOR + height - DECK, name=f"{name} (+{height:g})")
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
    st.run_wall(4, run_up + g + 4)
    st.gap(run_up, run_up + g)
    cross(Crossing("L09 wall run", "gap", g, {"wall run"}, wall_beside=True), taught | {"wall run"})
    return run_up + g + 8, {"wall run"}


def lesson_L10(st, taught):
    h = r(max(7.0, reach("jump to the ledge", "climb") / locks.OTHER_SHARE + 0.5))
    shaft, depth = CHIMNEY_WIDTH, 3.5
    st.deck(0, 6)
    st.deck(6, 6 + depth, name="Shaft Floor", x0=-shaft / 2, x1=shaft / 2)
    # Its sides marked for wall slides (and so wall jumps): kicking between them is the way up. Its back, the wall
    # between them it's climbed to the top of, is plain: it isn't slid down, so it's no help. The ring's walls stop
    # beside it, its sides reaching the lane's edges.
    st.marked_block("Shaft Side", 6, 6 + depth, GROUND, FLOOR + WALL_HEIGHT, -LANE / 2, -shaft / 2, "WallSlide")
    st.marked_block("Shaft Side", 6, 6 + depth, GROUND, FLOOR + WALL_HEIGHT, shaft / 2, LANE / 2, "WallSlide")
    st.marked.append((6, 6 + depth, ("inner", "outer")))
    st.block(6 + depth, 12 + depth, GROUND, FLOOR + h - DECK, name=f"Shaft Back (+{h:g})")
    st.deck(6 + depth, 12 + depth, FLOOR + h, name="Shaft Top")
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
    st.rope(run_up + g / 2)
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
    # A footing under each post, so they stand on something.
    for at, floor in ((start[2], FLOOR), (end[2], far)):
        st.trim("Zipline Footing", -0.6, 0.6, at - 0.6, at + 0.6, floor, floor + 0.06, "Accent")
    st.block(z + 11 + length, z + 15 + length, GROUND, FLOOR - DECK, name="Step Up Base")
    st.deck(z + 11 + length, z + 15 + length, name="Step Up")
    return z + 15 + length, drop


def lesson_L16(st, taught):
    g = r(max(16.0, reach("sprint jump", "gap") / locks.OTHER_SHARE * 2))
    st.deck(0, 6)  # Its labels clear of the zipline's post (2 m into its start deck).
    end, drop = zipline_gap(st, 6, g)
    cross(Crossing("L16 zipline", "gap", g, {"zipline"}, fixtures={"zipline"}), taught | {"zipline"})
    return end, {"zipline"}


def lesson_L17(st, taught):
    pool = 18.0
    bottom = FLOOR - POOL_DEPTH
    st.zone_low = bottom - 1  # Its zone reaches the pool's floor, so diving under the arch stays in it.
    start = 7.0  # Where the pool starts: past the lesson's number and name at its start, so they're on the deck.
    st.deck(0, start)
    inside = LANE / 2 - WALL_THICKNESS
    st.A("Platform", name="Pool Floor", x0=-inside, x1=inside, z0=start, z1=start + pool, y0=bottom - DECK, top=bottom, surface="Concrete")
    # Its sides as tall as the ring's walls, which they carry on, so nothing can be walked along them past the arch.
    for x0, x1 in ((-LANE / 2, -inside), (inside, LANE / 2)):
        st.block(start, start + pool, GROUND, FLOOR + WALL_HEIGHT, name="Pool Side", x0=x0, x1=x1)
    # Its ends, under the decks either side, so it's walled all round (the ladder out stands against the far one).
    for z0, z1 in ((start - 1, start), (start + pool, start + 1 + pool)):
        st.block(z0, z1, GROUND, FLOOR - DECK, name="Pool End", x0=-LANE / 2, x1=LANE / 2)
    st.A("Water", name="Pool", centre=[0, r(start + pool / 2)], size=[2 * inside, pool], bottom=bottom, surface=r(FLOOR - 0.4))
    arch = start + pool / 2
    # Across the pool's sides too, and too high to get over.
    st.block(arch - 1, arch + 1, bottom + ARCH_CLEAR, FLOOR + ARCH_TOP, name=f"Arch ({ARCH_CLEAR:g} m clear under water)",
             x0=-LANE / 2, x1=LANE / 2)
    st.open.append((start, start + pool))  # Its sides are the walls here.
    st.A("Ladder", at=[0, r(start + pool)], floor=bottom, height=POOL_DEPTH, facing=[0, -1])
    # A pool's look: white walls under the water, lane lines on its floor, and a cyan light under the arch to dive for.
    for x0, x1 in ((-inside, -inside + 0.05), (inside - 0.05, inside)):
        st.trim("Pool Tiles", x0, x1, start, start + pool, bottom, FLOOR - 0.5, "White")
    for x in (-inside / 2, inside / 2):
        st.trim("Lane Line", x - 0.15, x + 0.15, start + 1, start - 1 + pool, bottom, bottom + 0.02, "Graphite")
    st.trim("Arch Light", -inside, inside, arch - 1, arch + 1, bottom + ARCH_CLEAR - 0.12, bottom + ARCH_CLEAR, "Cyan")
    for z0, z1 in ((start - 0.3, start), (start + pool, start + 0.3 + pool)):
        st.trim("Pool Rim", -inside, inside, z0, z1, FLOOR, FLOOR + 0.02, "White")
    st.deck(start + pool, start + 6 + pool)
    cross(Crossing("L17 arch", "passage", 2.0, {"swim"}, ceiling=ARCH_CLEAR, underwater=True, fixtures={"water"}), taught | {"swim"})
    return start + 6 + pool, {"swim"}


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
        st.rope(z + g * i / 3)
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
    st.run_wall(z + 4, z + 8 + g + 4)
    # The bounce pad picked out.
    px, pz, half_pad = LANE / 2 - 2, z + 6, 1.4
    for a, b, inward in (([px - half_pad, pz - half_pad], [px + half_pad, pz - half_pad], [0, 1]),
                         ([px - half_pad, pz + half_pad], [px + half_pad, pz + half_pad], [0, -1]),
                         ([px - half_pad, pz - half_pad], [px - half_pad, pz + half_pad], [1, 0])):
        st.A("HazardStripes", a=[r(a[0]), r(a[1])], b=[r(b[0]), r(b[1])], inward=inward, height=FLOOR)
    st.gap(z + 8, z + 8 + g)
    st.deck(z + 8 + g, z + 16 + g, name="Landing")
    cross(Crossing(f"{st.name}: bounce into a wall run", "gap", g, {"wall run", "bounce"}, wall_beside=True,
                   fixtures={"bounce pad"}, also_allowed={"sprint jump and air dash"}), taught)
    return ledge(st, z + 16 + g, share, taught)


def stretch_2(st, share, taught):
    z = slide_into_jump(st, 0, share, taught)
    # Wall runs along a corridor over a gap, from one side to the other and back (a jump off a wall run is a wall jump),
    # up to the top of the wall that ends it.
    g = r(max(share * reach("wall run", "gap"), reach("sprint jump", "gap") / locks.OTHER_SHARE + 0.1))
    corridor = CORRIDOR_WIDTH
    st.deck(z, z + 14, name="Run-up")
    h = r(max(6.0, reach("jump to the ledge", "climb") / locks.OTHER_SHARE + 0.5))
    top = z + 14 + g              # The wall that ends it, its top climbed to.
    # The corridor is cut through solid wall: from its sides out to the lane's edges, from the ground to the walls' top,
    # wall-run walls all along; the wall that ends it is plain (the way up is off the sides). The ring's walls stop beside it.
    for x0, x1 in ((-LANE / 2, -corridor / 2), (corridor / 2, LANE / 2)):
        st.marked_block("Corridor", z + 10, top + 6, GROUND, FLOOR + WALL_HEIGHT, x0, x1, "WallRun")
    st.marked.append((z + 10, top + 6, ("inner", "outer")))
    st.block(top, top + 6, GROUND, FLOOR + h - DECK, name=f"Corridor End (+{h:g})", x0=-corridor / 2, x1=corridor / 2)
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
    st.deck(z + 8, z + 13, FLOOR + hc, name="Crate Wall Top")
    cross(Crossing(f"{st.name}: crate", "climb", hc, {"crate"}, fixtures={"crate"}), taught)
    hp = r(max(6.0, ledge_reach / locks.OTHER_SHARE + 0.5))
    base = FLOOR + hc
    st.block(z + 13, z + 18, GROUND, base + hp - DECK, name=f"Panel Wall (+{hp:g})")
    st.A("ClimbablePanel", a=[-2.5, r(z + 13)], b=[2.5, r(z + 13)], y0=r(base), y1=r(base + hp), facing=[0, -1])
    st.deck(z + 13, z + 18, base + hp, name="Panel Wall Top")
    st.deck(z + 18, z + 24, name="Below")
    # The finish: a lit line across the floor, a pylon of light up each wall beside it, and FINISH beyond.
    face_in = LANE / 2 - WALL_THICKNESS
    st.trim("Finish Line", -face_in, face_in, z + 19.2, z + 19.6, FLOOR, FLOOR + 0.03, "Light")
    for x0, x1 in ((-face_in, -face_in + 0.12), (face_in - 0.12, face_in)):
        st.trim("Finish Pylon", x0, x1, z + 19, z + 19.8, FLOOR, FLOOR + 7, "Light")
    st.label("FINISH", z + 21.5, size=3.0)
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
    WALL_FLORA.clear()
    TOPS.clear()
    s = Spec("Tutorial Ring", SCENE)
    A = s.add
    inner, outer = RING_APOTHEM - LANE / 2, RING_APOTHEM + LANE / 2
    h = face_half_length()

    # ---- The ground (a hazard) and the hub ----
    s.part("Ground and Hub")
    A("Group", parent="", name="Tutorial Ring")
    root = "Tutorial Ring"
    # Far wider than the ring, so its edge is lost in the haze (the scene's setup thickens it: TutorialRingSceneSetup).
    A("Ground", parent=root, half=r(outer + GROUND_REACH), openings=[])
    # Darker under and round the ring, where a fall lands: it's the hazard.
    A("Trim", parent=root, name="Hazard Ground", centre=[0, 0], size=[r(2 * (outer + 40)), r(2 * (outer + 40))], y0=GROUND, y1=GROUND + 0.03,
      yaw=0, surface="Asphalt")
    A("HazardVolume", parent=root, name="Fall", centre=[0, 1, 0], size=[r(2 * (outer + 60)), 6, r(2 * (outer + 60))])
    hub_centre = world(RING_APOTHEM / math.cos(math.radians(22.5)), HUB_ANGLE)
    A("Prism", parent=root, name="Hub", shape=octagon(HUB_APOTHEM, *hub_centre), y0=FLOOR - DECK, y1=FLOOR, surface="Concrete")
    skirt = octagon(HUB_APOTHEM - SKIRT / 2, *hub_centre)
    wall_chain(A, root, skirt + [skirt[0]], SKIRT, GROUND, FLOOR - DECK, "Hub Skirt")  # Its edges walled from the ground up to it.
    A("Group", parent=root, name="Spawn Points")
    # Facing the way the first lesson's lane runs (its face's -x).
    facing = (45 * RUN_FACES[0] - 90) % 360
    A("SpawnMarker", parent=f"{root}/Spawn Points", name="Hub", at=hub_centre, height=FLOOR + 0.1, yaw=facing)
    A("Checkpoint", parent=root, id="hub", feet=[hub_centre[0], FLOOR, hub_centre[1]], yaw=facing, size=[10, 3, 10], start=True, marked=False)
    # The hub marked as the start and the finish: an inlaid octagon on its floor and its name, read facing the first lane.
    A("GuideLine", parent=root, name="Hub Inlay", points=octagon(HUB_APOTHEM - 3, *hub_centre), height=FLOOR, surface="Accent", closed=True)
    A("GuideLine", parent=root, name="Hub Ring", points=octagon(HUB_APOTHEM - 10, *hub_centre), height=FLOOR, surface="White", closed=True)
    ahead = [math.sin(math.radians(facing)), math.cos(math.radians(facing))]
    # Behind the middle (the first lesson's start, its checkpoint line and number, are just ahead of it), between the rings.
    label_at = [r(hub_centre[0] - ahead[0] * 7), r(hub_centre[1] - ahead[1] * 7)]
    A("FloorLabel", parent=root, text="START / FINISH", at=label_at, height=FLOOR, facing=[r(ahead[0]), r(ahead[1])], size=1.4)
    # The ground marked out under the ring, inside and out, so the drop has a scale; and in front of each face, what it is,
    # in letters as big as the lane is wide, reading from the middle.
    for apothem in (inner - 10, inner - 30, outer + 10, outer + 30):
        A("GuideLine", parent=root, name="Ground Line", points=octagon(apothem), height=GROUND + 0.04, surface="Light", closed=True)
    for i, k in enumerate(RUN_FACES):
        text = f"{LESSONS[i][0][0][:3]} - {LESSONS[i][-1][0][:3]}" if i < 4 else f"{i - 3}  {STRETCH_NAMES[i - 4]}"
        out = [math.sin(math.radians(45 * k)), math.cos(math.radians(45 * k))]
        A("FloorLabel", parent=root, text=text, at=world(inner - 22, 45 * k), height=GROUND + 0.05, facing=[r(out[0]), r(out[1])], size=8.0)

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
        plan = {"face": k, "frame": face, "keep_clear": [], "open": [], "marked": [], "starts": [], "decks": []}
        WALL_PLAN.append(plan)

        def note(st, at):
            """Where the stretch's ring walls stop, along its face's lane (u: metres from its start)."""
            plan["keep_clear"] += [(at + z0, at + z1) for z0, z1 in st.keep_clear]  # The outer side's only.
            plan["open"] += [(at + z0, at + z1) for z0, z1 in st.open]
            plan["marked"] += [(at + z0, at + z1, sides) for z0, z1, sides in st.marked]
            plan["decks"] += [(at + z0, at + z1, top, x0, x1) for z0, z1, top, x0, x1 in st.decks]

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
            title = STRETCH_NAMES[n]
            st.checkpoint(f"C{n + 1}-start")
            role, index = ("start", None) if n == 0 else ("split", n - 1)
            # Its posts in the lane's walls, where they stand.
            post = LANE / 2 - WALL_THICKNESS / 2 - GATE_POST / 2
            st.A("TimingGate", course="Agility", role=role, a=[-post, TIMING_GATE_AT], b=[post, TIMING_GATE_AT], floor=FLOOR,
                 approach=[0, 1], display=True if n == 0 else None, index=index)
            # Walled over from its top (its sign's, where it has one) to the walls' top, so it's a doorway in them.
            over = FLOOR + GATE_TOP + (GATE_SIGN if n == 0 else 0)
            st.block(TIMING_GATE_AT - GATE_POST / 2, TIMING_GATE_AT + GATE_POST / 2, over, FLOOR + WALL_HEIGHT, name="Over the Gate")
            # Its number and name at its start.
            # Before the gate, on clear floor: past it, the first gap's stripes are.
            st.label(str(n + 1), TIMING_GATE_AT - 1.8, size=2.0)
            st.label(title, TIMING_GATE_AT - 3.6, size=0.8)
            plan["starts"].append((h - cursor, str(n + 1)))
            length = STRETCHES[n](st, SHARES[n], taught)
            note(st, h - cursor)
            cursor -= length
        if cursor < -h - 1e-6:
            raise LockError(f"Face {k}'s lessons or stretch are {r(h - cursor)} m long: longer than its {r(2 * h)} m lane.")
        if cursor > -h + 1e-6:
            A("Platform", parent=face, name="Lane", x0=r(-h), x1=r(cursor), z0=r(inner), z1=r(outer), y0=FLOOR - DECK, top=FLOOR, coping="Accent")
            for z0, z1 in ((inner, inner + SKIRT), (outer - SKIRT, outer)):
                A("Block", parent=face, name="Deck Skirt", x0=r(-h), x1=r(cursor), z0=r(z0), z1=r(z1), y0=GROUND, y1=FLOOR - DECK, surface="Panel")
            plan["decks"].append((h - cursor, 2 * h, FLOOR, -LANE / 2, LANE / 2))
        if i == 3:
            build_lessons_end(s, face, h)
    build_walls(s, h)
    build_course_end(s, f"{root}/Face {RUN_FACES[-1]}", h)
    build_hub_walls(s, h)
    build_roof(s, h)
    build_skyline(s, outer)
    return s.spec


def build_roof(s, h):
    """
    A glass roof at the walls' top over everywhere the way goes (the lanes, their corners, the hub and the shuttle's way;
    not the open ground in the ring's middle), from wall to wall, on graphite ribs across it every ROOF_RIB. Glass blocks
    as anything does, but it's too high to reach (as the walls' tops are), so nothing about the play changes.
    """
    s.part("Roof")
    A = s.add
    root = "Tutorial Ring"
    top = FLOOR + WALL_HEIGHT
    y0 = top - ROOF_THICKNESS
    inner, outer = RING_APOTHEM - LANE / 2 + WALL_THICKNESS, RING_APOTHEM + LANE / 2 - WALL_THICKNESS  # Between the walls' faces.
    span = 2 * h
    hub_clear = HUB_APOTHEM - (half(RING_APOTHEM) - h)
    hub_centre = world(RING_APOTHEM / math.cos(math.radians(22.5)), HUB_ANGLE)
    n = len(WALL_PLAN)
    for i, plan in enumerate(WALL_PLAN):
        frame, k = plan["frame"], plan["face"]
        # Over the lane, but not into the hub, which has a roof of its own.
        u0 = hub_clear if i == 0 else 0.0
        u1 = span - hub_clear if i == n - 1 else span
        A("Block", parent=frame, name="Roof", x0=r(h - u1), x1=r(h - u0), z0=r(inner), z1=r(outer), y0=r(y0), y1=r(top), surface="Glass")
        u = u0 + ROOF_RIB / 2
        while u < u1:
            A("Trim", parent=frame, name="Roof Rib", centre=[r(h - u), RING_APOTHEM], size=[0.2, r(outer - inner)], y0=r(top - 0.45), y1=r(y0),
              yaw=0, surface="Graphite")
            u += ROOF_RIB
        # Over the corner at the face's start (from the lane's edge, so it meets the lane's roof), unless it's the hub's.
        shape = corner(RING_APOTHEM - LANE / 2, outer)
        middle = face_to_world([sum(p[0] for p in shape) / len(shape), sum(p[1] for p in shape) / len(shape)], k)
        if math.dist(middle, hub_centre) > hub_reach():
            A("Prism", parent=frame, name="Roof (corner)", shape=shape, y0=r(y0), y1=r(top), surface="Glass")
    A("Prism", parent=root, name="Roof (hub)", shape=octagon(HUB_APOTHEM, *hub_centre), y0=r(y0), y1=r(top), surface="Glass")
    # The shuttle's way: its ends follow the lane's inner edge and the hub's, which come back toward the way's middle off its line.
    cos, slope = math.cos(math.radians(22.5)), math.tan(math.radians(22.5))
    corner_z, edge_z, w = RING_APOTHEM / cos, (RING_APOTHEM - LANE / 2) / cos, DOCK_WIDTH / 2
    near, far = -edge_z, corner_z - hub_reach()
    shape = [[-w, r(near + w * slope)], [0, r(near)], [w, r(near + w * slope)], [w, r(far + w * slope)], [0, r(far)], [-w, r(far + w * slope)]]
    frame = f"{root}/Lessons Shortcut"
    A("Prism", parent=frame, name="Roof (shuttle)", shape=shape, y0=r(y0), y1=r(top), surface="Glass")
    z = near + w * slope + ROOF_RIB / 2
    while z < far:
        A("Trim", parent=frame, name="Roof Rib", centre=[0, r(z)], size=[2 * w, 0.2], y0=r(top - 0.45), y1=r(y0), yaw=0, surface="Graphite")
        z += ROOF_RIB


def build_skyline(s, outer):
    """
    Blocks standing far off round the ring, of varied size (spread by fixed steps, not at random, so the build is always
    the same), in the haze: so the ground doesn't end against the sky. To see only (no colliders), far from anywhere a
    character goes.
    """
    s.part("Surroundings")
    s.add("Group", parent="Tutorial Ring", name="Skyline")
    for j in range(SKYLINE):
        angle = j * 360 / SKYLINE + 4.0 * math.sin(j * 1.7)
        radius = outer + 330 + 140 * ((j * 37) % 10) / 10
        width = 30 + 10 * ((j * 13) % 5)
        height = 25 + 10 * ((j * 29) % 9)
        s.add("Trim", parent="Tutorial Ring/Skyline", name="Skyline Block", centre=world(radius, angle), size=[r(width), r(width * 0.8)],
              y0=GROUND, y1=r(height), yaw=r(angle), surface="Panel")


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
                # A pier where it stops at a way in, so its end isn't a raw slab.
                yaw = math.degrees(math.atan2(b[0] - a[0], b[1] - a[1]))
                d = ((b[0] - a[0]) / length, (b[1] - a[1]) / length)
                if j > 0:
                    pier(A, root, p0, d, yaw)
                if k < n:
                    pier(A, root, p1, (-d[0], -d[1]), yaw)
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


WALL_FLORA = []                    # The inner walls' stretches given flora, in order: each its own seed (salt).
IVY_COVER = 0.5                   # How much of the inner walls' middle side ivy covers, from the ground up to the decks,
VINE_COVER = 0.35                 # and how much has vines hanging from under their coping, stopping above the windows.


def wall_flora(A, frame, x0, x1, salt):
    """
    Ivy and vines on an inner wall's middle side, from x0 to x1 along its face: out of reach (the ring's middle is a
    hazard, and the walls too tall to get onto), so the flora can't be taken for a way up. The ivy from the ground to
    the decks' height, under the windows; the vines from under the coping, ending above them.
    """
    WALL_FLORA.append((frame, x0, x1))
    face = RING_APOTHEM - LANE / 2
    a, b = [r(x0), r(face)], [r(x1), r(face)]
    A("Ivy", parent=frame, a=a, b=b, outward=[0, -1], y0=GROUND, y1=FLOOR, salt=salt, cover=IVY_COVER)
    A("Vines", parent=frame, a=a, b=b, outward=[0, -1], top=r(FLOOR + WALL_HEIGHT - COPING), drop=r(GLASS_BELOW_TOP - COPING - 0.3),
      salt=salt, cover=VINE_COVER)


def build_walls(s, h):
    """
    The ring's walls: along both edges of each lane the whole way round, gaps and climbs included, from the ground to
    WALL_HEIGHT over the decks; but where something else stands in their place (the pool's sides, a wall marked for wall
    runs or slides: a stretch's open and marked spans), out of the keep-clear zones (the outer side's), and clear of the
    hub and of the inner corner the shuttle's dock comes in at. They're unmarked, so they can't be run along or slid
    down: a wall beside a crossing is no help across it. Each face's outer corner gets walls too where both lanes either
    side have them up to it.
    """
    tops = reach("jump to the ledge", "climb") / locks.OTHER_SHARE
    if WALL_HEIGHT < max(TOPS, default=0.0) + tops:
        raise LockError("The walls are low enough to get onto from a climb's top.")
    A = s.add
    span = 2 * h
    # The lanes' walls meet the hub's at its edge: the hub's middle is the ring's corner, past the lane's end (its u = 0,
    # at the inner apothem's corner) by the difference between the corner's half-widths.
    hub_clear = HUB_APOTHEM - (half(RING_APOTHEM) - h)
    dock_clear = (DOCK_WIDTH / 2 + WALL_THICKNESS) / math.cos(math.radians(22.5))  # and the shortcut's at the lessons' end.
    n = len(WALL_PLAN)
    for i, plan in enumerate(WALL_PLAN):
        cuts = list(plan["open"])
        if i == 0:
            cuts.append((-1.0, hub_clear))                               # The hub, at the first face's start
        if i == n - 1:
            cuts.append((span - hub_clear, span + 1))                    # and the last face's end.
        walls = {}
        for side in ("inner", "outer"):
            side_cuts = cuts + [(a, b) for a, b, sides in plan["marked"] if side in sides]
            if side == "outer":
                side_cuts += plan["keep_clear"]
            if side == "inner" and i == 3:
                side_cuts.append((span - dock_clear, span + 1))          # The shuttle's dock, at the lessons' end
            if side == "inner" and i == 4:
                side_cuts.append((-1.0, dock_clear))                     # (the corner between the fourth face and the fifth).
            walls[side] = [w for w in _subtract([(0.0, span)], side_cuts) if w[1] - w[0] > 0.05]
        plan["walls"] = walls
        for side, edge in (("inner", RING_APOTHEM - LANE / 2 + WALL_THICKNESS / 2), ("outer", RING_APOTHEM + LANE / 2 - WALL_THICKNESS / 2)):
            toward_lane = 1 if side == "inner" else -1
            # The breaks that are openings (a blocker's way): elsewhere a wall stops where another stands on (the hub's,
            # the pool's sides, a marked wall), and needs no end.
            openings = plan["keep_clear"] if side == "outer" else []
            for u0, u1 in walls[side]:
                a, b = [r(h - u0), r(edge)], [r(h - u1), r(edge)]
                windows = ring_windows(plan, u0, u1) if side == "inner" else []
                glazed = [[[r(h - (w0 + w1) / 2), r(edge)], r(w1 - w0), r(FLOOR + BAND + 0.12), r(FLOOR + WALL_HEIGHT - GLASS_BELOW_TOP)]
                            for w0, w1 in windows]
                A("Wall", parent=plan["frame"], name=f"Ring Wall ({side})", a=a, b=b, y0=GROUND, y1=FLOOR + WALL_HEIGHT, thickness=WALL_THICKNESS,
                  openings=glazed or None)
                for w0, w1 in windows:
                    face = edge + toward_lane * WALL_THICKNESS / 2
                    window(A, plan["frame"], [h - w0, face], [h - w1, face], -toward_lane)
                dress_wall(A, plan["frame"], a, b, WALL_THICKNESS, FLOOR + WALL_HEIGHT, band=False)
                if side == "inner":
                    wall_flora(A, plan["frame"], h - u0, h - u1, salt=100 * i + len(WALL_FLORA))
                ring_wall_dressing(A, plan, side, edge, toward_lane, u0, u1, h, pipes=i >= 4 and side == "outer", skip=windows)
                # Where it stops for an opening, an end post with an orange strip down its lane side, so the break reads as meant.
                for u in (u0, u1):
                    if any(abs(u - k0) < 0.6 or abs(u - k1) < 0.6 for k0, k1 in openings):
                        end_post(A, plan["frame"], h - u, edge, toward_lane)
            # A beam across each opening short enough, at the walls' top, tying the two ends together (far out of reach).
            for (_, e0), (s1, _) in zip(walls[side], walls[side][1:]):
                if s1 - e0 <= LINTEL_SPAN and any(k0 < s1 and k1 > e0 for k0, k1 in openings):
                    A("Trim", parent=plan["frame"], name="Ring Wall (lintel)", centre=[r(h - (e0 + s1) / 2), r(edge)], size=[r(s1 - e0), WALL_THICKNESS],
                      y0=FLOOR + WALL_HEIGHT - LINTEL, y1=FLOOR + WALL_HEIGHT, yaw=0, surface="Graphite")
        # A banner with the number of each lesson and stretch on a wall at its start, where there's one.
        for u, number in plan["starts"]:
            for side, face_z, outward in (("outer", RING_APOTHEM + LANE / 2 - WALL_THICKNESS, -1), ("inner", RING_APOTHEM - LANE / 2 + WALL_THICKNESS, 1)):
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


def ring_windows(plan, u0, u1):
    """
    The windows in an inner ring wall from u0 to u1: one in the middle of each stretch of it beside plain floor (a deck
    at FLOOR the lane's width) GLASS_MIN long or more, up to GLASS_MAX long and 3 m clear of the stretch's ends.
    """
    plain = _merge([(max(u0, d0), min(u1, d1)) for d0, d1, top, x0, x1 in plan["decks"]
                    if abs(top - FLOOR) < 1e-6 and x0 <= -LANE / 2 + 1e-6 and min(u1, d1) - max(u0, d0) > 0.05])
    out = []
    for p0, p1 in plain:
        if p1 - p0 >= GLASS_MIN:
            length = min(GLASS_MAX, p1 - p0 - 6)
            mid = (p0 + p1) / 2
            out.append((mid - length / 2, mid + length / 2))
    return out


def window(A, parent, a, b, outward):
    """
    A window's glass from a to b (points on a wall's lane-side face, in its frame), flush with that face and set into
    the wall outward of it (outward: the face's normal away from the lane, along the frame's z or x as the wall runs), from
    over the deck's band to GLASS_BELOW_TOP under the walls' top; and graphite mullions on it every MULLION_EVERY.
    """
    y0, y1 = FLOOR + BAND + 0.12, FLOOR + WALL_HEIGHT - GLASS_BELOW_TOP
    along_x = abs(b[0] - a[0]) > abs(b[1] - a[1])
    length = abs(b[0] - a[0]) if along_x else abs(b[1] - a[1])
    lo, hi = (min(a[0], b[0]), max(a[0], b[0])) if along_x else (min(a[1], b[1]), max(a[1], b[1]))
    f = a[1] if along_x else a[0]
    g0, g1 = sorted((f, f + outward * 0.1))
    if along_x:
        A("Block", parent=parent, name="Window", x0=r(lo), x1=r(hi), z0=r(g0), z1=r(g1), y0=r(y0), y1=r(y1), surface="Glass")
    else:
        A("Block", parent=parent, name="Window", x0=r(g0), x1=r(g1), z0=r(lo), z1=r(hi), y0=r(y0), y1=r(y1), surface="Glass")
    count = int(length // MULLION_EVERY)
    for j in range(1, count):
        t = lo + (hi - lo) * j / count
        c = [t, f - outward * 0.03] if along_x else [f - outward * 0.03, t]
        A("Trim", parent=parent, name="Mullion", centre=[r(c[0]), r(c[1])], size=[0.15, 0.06] if along_x else [0.06, 0.15],
          y0=r(y0), y1=r(y1), yaw=0, surface="Graphite")


def ring_wall_dressing(A, plan, side, edge, toward_lane, u0, u1, h, pipes=False, skip=()):
    """
    A ring wall's dressing on its lane side, from u0 to u1 along its face (its line at edge, the lane toward_lane of it):
    the band at its foot at the height of the deck beside it (stepping with it, and none beside a gap), with an orange stripe
    along its top; warm lamps high up, between the pilasters, every other bay; and with pipes, two pipe
    runs high along it on brackets at the pilasters.
    """
    frame = plan["frame"]
    face = edge + toward_lane * WALL_THICKNESS / 2
    beside = [(max(u0, d0), min(u1, d1), top) for d0, d1, top, x0, x1 in plan["decks"]
              if (x0 <= -LANE / 2 + 1e-6 if side == "inner" else x1 >= LANE / 2 - 1e-6) and min(u1, d1) - max(u0, d0) > 0.05]
    for b0, b1, top in beside:
        for offset, y0, y1, surface, thick in ((0.02, top, top + BAND, "Graphite", 0.04), (0.025, top + BAND, top + BAND + 0.12, "Accent", 0.05)):
            A("Trim", parent=frame, name="Wall Band", centre=[r(h - (b0 + b1) / 2), r(face + toward_lane * offset)], size=[r(b1 - b0), thick],
              y0=r(y0), y1=r(y1), yaw=0, surface=surface)
    # The pilasters' places, as the Wall piece spaces them (centred on the wall, PILASTER_SPACING apart).
    length = u1 - u0
    if length < PILASTER_MIN:
        return
    count = int((length - 2) // PILASTER_SPACING) + 1
    first = u0 + (length - (count - 1) * PILASTER_SPACING) / 2
    for i in range(count - 1):
        u = first + (i + 0.5) * PILASTER_SPACING
        if i % 2 == 0 and not any(w0 - 1 <= u <= w1 + 1 for w0, w1 in skip):  # Not on a window.
            A("Trim", parent=frame, name="Wall Lamp", centre=[r(h - u), r(face + toward_lane * 0.05)], size=[1.6, 0.1],
              y0=FLOOR + LAMP_HEIGHT, y1=FLOOR + LAMP_HEIGHT + 0.25, yaw=0, surface="Light")
    if pipes:
        for y in (PIPE_HEIGHT, PIPE_HEIGHT + 0.45):
            A("Trim", parent=frame, name="Pipe", centre=[r(h - (u0 + u1) / 2), r(face + toward_lane * 0.35)], size=[r(length - 0.4), 0.2],
              y0=FLOOR + y, y1=FLOOR + y + 0.2, yaw=0, surface="Steel")
        for i in range(count):
            u = first + i * PILASTER_SPACING
            A("Trim", parent=frame, name="Pipe Bracket", centre=[r(h - u), r(face + toward_lane * 0.25)], size=[0.12, 0.42],
              y0=FLOOR + PIPE_HEIGHT - 0.15, y1=FLOOR + PIPE_HEIGHT + 0.8, yaw=0, surface="Graphite")


def dress_wall(A, parent, a, b, thickness, top, band=True):
    """
    A tall wall's dressing, to see only: a graphite coping along its top, and (with band) on both faces a graphite band at
    its foot at deck height (BAND high) with an orange stripe along its top, so the deck's level reads along every wall. A
    marked wall has no band: its pattern is its look.
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
    for side in (1, -1) if band else ():
        for offset, y0, y1, surface in ((thickness / 2 + 0.02, FLOOR, FLOOR + BAND, "Graphite"), (thickness / 2 + 0.025, FLOOR + BAND, FLOOR + BAND + 0.12, "Accent")):
            c = (mid[0] + px * side * offset, mid[1] + pz * side * offset)
            A("Trim", parent=parent, name="Wall Band", centre=[r(c[0]), r(c[1])], size=[0.04 if surface == "Graphite" else 0.05, r(length)],
              y0=r(y0), y1=r(y1), yaw=yaw, surface=surface)


PIER = 1.4                    # How long a pier capping a wall's end is, along the wall,
PIER_PROUD = 0.03             # and how far past the end it stands.


def pier(A, parent, end, back, yaw):
    """
    A graphite pier capping a wall's end (end, in the parent's frame), from the ground to just over the walls' top: as
    thick as the wall, and PIER long back along it (back: the unit direction from the end into the wall), so it's flush
    with the wall's faces and nothing of it stands in a way or on a floor.
    """
    # Standing a little past the wall's end, so its end face isn't in the same plane as the wall's (they'd flicker).
    at = [end[0] + back[0] * (PIER / 2 - PIER_PROUD), end[1] + back[1] * (PIER / 2 - PIER_PROUD)]
    A("Trim", parent=parent, name="Pier", centre=[r(at[0]), r(at[1])], size=[WALL_THICKNESS + 0.06, PIER], y0=GROUND,
      y1=FLOOR + WALL_HEIGHT + 0.5, yaw=r(yaw), surface="Graphite")


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
        # Skirted all round from the ground, as the lanes' decks are.
        wall_chain(A, frame, [[-w + SKIRT / 2, r(z0 + SKIRT / 2)], [w - SKIRT / 2, r(z0 + SKIRT / 2)], [w - SKIRT / 2, r(z1 - SKIRT / 2)],
                              [-w + SKIRT / 2, r(z1 - SKIRT / 2)], [-w + SKIRT / 2, r(z0 + SKIRT / 2)]], SKIRT, GROUND, FLOOR - DECK, "Dock Skirt")
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
        # The lane's inner edge, beside the corner, comes back toward the middle as it runs off to either side of the
        # way (so -inner, plus): there each wall's outer face meets the end of the ring's inner wall, which stops for the
        # dock (dock_clear, in build_walls) just where it does.
        a, b = [r(x), r(-inner + abs(x) * slope)], [r(x), r(corner - hub_reach() + abs(x) * slope)]
        # Glass in its first and last thirds (clear of the piers at its ends), to see out over the middle; its middle opaque.
        length = b[1] - a[1]
        windows = [(a[1] + 1.5, a[1] + length / 3), (b[1] - length / 3, b[1] - 1.5)]
        openings = [[[r(x), r((w0 + w1) / 2)], r(w1 - w0), r(FLOOR + BAND + 0.12), r(FLOOR + WALL_HEIGHT - GLASS_BELOW_TOP)] for w0, w1 in windows]
        A("Wall", parent=frame, name="Shortcut Wall", a=a, b=b, y0=GROUND, y1=FLOOR + WALL_HEIGHT, thickness=WALL_THICKNESS, openings=openings)
        out = 1 if x > 0 else -1
        for w0, w1 in windows:
            face = x - out * WALL_THICKNESS / 2
            window(A, frame, [face, w0], [face, w1], out)
        dress_wall(A, frame, a, b, WALL_THICKNESS, FLOOR + WALL_HEIGHT)
        # A pier at each end, where it meets the ring's walls and the hub's at an angle, over the join.
        pier(A, frame, a, (0, 1), 0.0)
        pier(A, frame, b, (0, -1), 0.0)
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
    A("Block", parent=face, name="Over the Finish Gate", x0=r(gate_x + 7 - GATE_POST / 2), x1=r(gate_x + 7 + GATE_POST / 2),
      z0=r(RING_APOTHEM - LANE / 2), z1=r(RING_APOTHEM + LANE / 2), y0=FLOOR + GATE_TOP, y1=FLOOR + WALL_HEIGHT, surface="Panel")
    # Over the gate, wall up to the walls' top: it's a doorway in them.
    A("Block", parent=face, name="Over the Course Gate", x0=r(gate_x - WALL_THICKNESS / 2), x1=r(gate_x + WALL_THICKNESS / 2),
      z0=r(RING_APOTHEM - width / 2), z1=r(RING_APOTHEM + width / 2), y0=FLOOR + GATE_HEIGHT, y1=FLOOR + WALL_HEIGHT, surface="Panel")
    # Framed on the course's side (jambs and a head, proud of the wall over it), with HUB on the floor before it.
    front = gate_x + WALL_THICKNESS / 2 + 0.15
    for z0, z1 in ((RING_APOTHEM - width / 2, RING_APOTHEM - width / 2 + 0.5), (RING_APOTHEM + width / 2 - 0.5, RING_APOTHEM + width / 2)):
        A("Trim", parent=face, name="Hub Gate Frame", centre=[r(front), r((z0 + z1) / 2)], size=[0.3, r(z1 - z0)], y0=FLOOR,
          y1=FLOOR + GATE_HEIGHT + 0.5, yaw=0, surface="Graphite")
    A("Trim", parent=face, name="Hub Gate Frame", centre=[r(front), RING_APOTHEM], size=[0.3, r(width)], y0=FLOOR + GATE_HEIGHT,
      y1=FLOOR + GATE_HEIGHT + 0.5, yaw=0, surface="Graphite")
    A("FloorLabel", parent=face, text="HUB", at=[r(gate_x + 3.5), RING_APOTHEM], height=FLOOR, facing=[-1, 0], size=2.0)


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
