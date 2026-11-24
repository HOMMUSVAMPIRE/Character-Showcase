"""The lock check: whether each crossing of a level (a gap, a climb, a low passage) can only be crossed by the moves it's
meant for, against what the character can do (envelope.json). The Tutorial Ring's script (examples/tutorial_ring.py)
runs it on every lesson and course crossing it lays out, and writes no spec if one fails.

A crossing names its intended moves. It passes when:
- each intended move reaches it, with room to spare: a measured move's reach is at least the need over INTENDED_SHARE
  (the need at most 95% of the reach); a move the envelope doesn't measure (a wall jump, a rope, a ladder...) reaches
  only where the crossing has its fixture (FIXTURE_MOVES);
- and every other move the player has by then (taught) falls short, with room to spare (its reach at most OTHER_SHARE,
  90%, of the need), or is ruled out by the crossing's geometry:
  - a ceiling lower than the character's standing height rules out every jump (and the wall run, which starts with one);
    a ceiling over standing height only shortens a jump, so it isn't counted (the check stays on the safe side);
  - no wall beside it rules out the wall run and the wall jump;
  - something in the dash's straight line (dash_blocked) rules out the dash;
  - no run-up (run_up False) rules out what needs sprinting speed (the sprint jump, the slide);
  - timing (timed: moving blockers) rules out the slow way through (crouch-walking);
  - deep water (underwater) rules out everything but swimming;
  - a fixture move needs its fixture.
A crossing can also name moves it accepts besides its intended ones (also_allowed): a gap a sprint jump is meant for
that a dash can always reach too, since nothing in a gap stops a dash's straight line without stopping a jump's.

Kinds of crossing, and the moves measured for each (the envelope's figures, or its keys):
- "gap": across, at the same height or near it (jump, sprint jump, sprint jump and air dash, wall run);
- "climb": up (step up, jump up, jump to the ledge);
- "passage": a low way through, its ceiling given (roll, slide; crouch gets through any length that fits it; standing,
  "walk", through one at least WALK_SHARE of the standing height).
"""

import math
from dataclasses import dataclass, field

#: An intended move must reach the need with it at most this share of its reach (no frame-perfect crossings).
INTENDED_SHARE = 0.95
#: Every other move must reach at most this share of the need (so it clearly falls short).
OTHER_SHARE = 0.90
#: A low passage fits a crouch if its ceiling is at least the crouch height over this; standing, if it's at least this
#: share of the standing height.
CROUCH_FIT_SHARE = 0.95
WALK_SHARE = 0.95

#: The moves a crossing's geometry can't rule out by measuring them: each counts as reaching only where the crossing has
#: the fixture or geometry it needs.
FIXTURE_MOVES = {
    "wall jump": "walls",      # Facing walls (a shaft, a corridor) to jump between.
    "climb": "panel",          # A climbable panel.
    "ladder": "ladder",
    "crate": "crate",          # A crate to push into place and climb.
    "bounce": "bounce pad",
    "rope": "rope",
    "zipline": "zipline",
    "swim": "water",
    "slide": "slot",           # A low slot whose slide carries the character over the gap at its end.
    "vault": "low wall",
}

#: What the envelope measures, by kind: the move, and where its reach is ("jumps"/"climbs" by its move, or a key).
MEASURED = {
    "gap": {"jump": ("jumps", "jump"), "sprint jump": ("jumps", "sprint jump"),
            "sprint jump and air dash": ("jumps", "sprint jump and air dash"), "wall run": ("key", "wallRunLength")},
    "climb": {"step up": ("climbs", "step up"), "jump up": ("climbs", "jump up"),
              "jump to the ledge": ("climbs", "jump to the ledge")},
    "passage": {"roll": ("key", "rollLength"), "slide": ("key", "slideLength")},
}

#: Every move the check knows.
MOVES = sorted({m for kind in MEASURED.values() for m in kind} | set(FIXTURE_MOVES) | {"crouch", "walk", "dash"})


class LockError(Exception):
    """A move the check needs isn't in the envelope (or is NaN): the level can't be sized for the character."""


@dataclass
class Crossing:
    """One crossing of a level: what kind it is, how far (or high, or long) it is, the moves meant for it, and its
    geometry: a ceiling's clearance over it (m above its floor), whether a wall is beside it, whether a dash's straight
    line is blocked, whether there's a run-up to it, whether it's timed, whether it's under deep water, the fixtures it
    has, and the moves it accepts besides its intended ones."""
    name: str
    kind: str
    need: float
    intended: set
    ceiling: float = None
    wall_beside: bool = False
    dash_blocked: bool = False
    run_up: bool = True
    timed: bool = False
    underwater: bool = False
    fixtures: set = field(default_factory=set)
    also_allowed: set = field(default_factory=set)


def no_air_jumps(envelope):
    """A copy of the envelope without its air jumps (every move with "double jump" in it): the Tutorial Ring's character."""
    env = dict(envelope)
    for key in ("jumps", "climbs"):
        env[key] = [m for m in envelope.get(key, []) if "double jump" not in m["move"]]
    return env


def _number(value, what):
    if not isinstance(value, (int, float)) or math.isnan(value):
        raise LockError(f"The envelope has no figure for {what}: write it again (Write Movement Envelope).")
    return float(value)


def reach(move, env, kind=None):
    """How far a measured move reaches (m): for kind, or the first kind that measures it. Raises LockError if the
    envelope has no figure for it (absent or NaN)."""
    kinds = [kind] if kind else [k for k in MEASURED if move in MEASURED[k]]
    for k in kinds:
        source = MEASURED.get(k, {}).get(move)
        if source is None:
            continue
        where, name = source
        if where == "key":
            return _number(env.get(name), f"{move} ({name})")
        listed = [m for m in env.get(where, []) if m.get("move") == name]
        key = "distance" if where == "jumps" else "height"
        return _number(listed[0].get(key) if listed else None, f"{move} ({where})")
    raise LockError(f"No kind of crossing measures '{move}'.")


def _ruled_out(move, c, env):
    """Why the crossing's geometry rules the move out (or None)."""
    if c.underwater and move != "swim":
        return "deep water"
    if move in FIXTURE_MOVES and FIXTURE_MOVES[move] not in c.fixtures and not (c.kind == "passage" and move == "slide"):
        return f"no {FIXTURE_MOVES[move]}"
    if c.ceiling is not None and c.kind != "passage" and ("jump" in move or move == "wall run") and c.ceiling < _number(env.get("standHeight"), "the standing height"):
        return "the ceiling"
    if not c.wall_beside and move in ("wall run", "wall jump") and "walls" not in c.fixtures:
        return "no wall"
    if c.dash_blocked and "dash" in move:
        return "the dash's line is blocked"
    if not c.run_up and ("sprint" in move or move == "slide"):
        return "no run-up"
    if c.timed and move == "crouch":
        return "too slow for its timing"
    if c.kind == "passage" and move in ("crouch", "slide", "roll") and c.ceiling is not None:
        if c.ceiling < _number(env.get("crouchHeight"), "the crouch height") / CROUCH_FIT_SHARE:
            return "too low to fit crouched"
    return None


def _reach_for(move, c, env):
    """How far the move gets over the crossing (math.inf for one that gets through any length, 0 for none)."""
    if move in MEASURED.get(c.kind, {}):
        return reach(move, env, c.kind)
    if c.kind == "passage" and move == "crouch":
        return math.inf
    if c.kind == "passage" and move == "walk":
        stand = _number(env.get("standHeight"), "the standing height")
        return math.inf if c.ceiling is None or c.ceiling >= stand * WALK_SHARE else 0.0
    if move in FIXTURE_MOVES:
        return math.inf if FIXTURE_MOVES[move] in c.fixtures else 0.0
    return 0.0


def check_crossing(c, taught, env):
    """The ways the crossing fails its lock (empty when it passes): its intended moves must reach it, and every other
    move taught must fall short or be ruled out (see the module)."""
    failures = []
    if c.kind not in MEASURED:
        return [f"{c.name}: unknown kind '{c.kind}'"]
    for move in sorted(c.intended):
        why = _ruled_out(move, c, env)
        if why:
            failures.append(f"{c.name}: its intended move, {move}, is ruled out ({why})")
            continue
        r = _reach_for(move, c, env)
        if r <= 0:
            failures.append(f"{c.name}: its intended move, {move}, can't cross a {c.kind}")
        elif c.need > INTENDED_SHARE * r:
            failures.append(f"{c.name}: {move} reaches {r:.2f} m, too close to the {c.need:.2f} m needed "
                            f"(at most {INTENDED_SHARE:.0%} of its reach: {INTENDED_SHARE * r:.2f} m)")
    for move in sorted(set(taught) - set(c.intended) - set(c.also_allowed)):
        if _ruled_out(move, c, env):
            continue
        r = _reach_for(move, c, env)
        if r > OTHER_SHARE * c.need:
            got = "gets through" if math.isinf(r) else f"reaches {r:.2f} m"
            failures.append(f"{c.name}: {move} {got}, against {c.need:.2f} m needed: it should fall short "
                            f"(at most {OTHER_SHARE:.0%} of the need) or be ruled out")
    return failures


def check_all(crossings, env):
    """Every failure of every (crossing, taught) pair, in order."""
    failures = []
    for c, taught in crossings:
        failures += check_crossing(c, taught, env)
    return failures
