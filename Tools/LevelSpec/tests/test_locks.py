"""Tests of levelspec.locks, the lock check: run with  python3 -m unittest discover -s Tools/LevelSpec/tests  (CI does).

They use the envelope's figures as they were measured (a fixture here, so retuning doesn't change what's tested): the
air jumps taken out, a lesson's lock passes with the right move and fails with another that reaches, and geometry
(ceilings, walls, a blocked dash line, timing, fixtures) rules moves out.
"""

import math
import os
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))

from levelspec import locks  # noqa: E402
from levelspec.locks import Crossing, LockError, check_crossing, no_air_jumps, reach  # noqa: E402

ENVELOPE = {
    "stepHeight": 0.35, "crouchHeight": 0.97, "standHeight": 1.76, "minJumpHeight": 0.5, "maxJumpHeight": 2,
    "jumps": [
        {"distance": 3.3, "move": "jump"},
        {"distance": 6.6, "move": "sprint jump"},
        {"distance": 11.97, "move": "sprint jump and air dash"},
        {"distance": 16.45, "move": "dash, jump and air dash"},
        {"distance": 13, "move": "double jump"},
        {"distance": 18.37, "move": "double jump and air dash"},
    ],
    "climbs": [
        {"height": 0.35, "move": "step up"},
        {"height": 2.37, "move": "jump up"},
        {"height": 4.29, "move": "jump to the ledge"},
        {"height": 4.37, "move": "double jump up"},
        {"height": 6.28, "move": "double jump to the ledge"},
    ],
    "slideLength": 13.41, "rollLength": 5.05, "wallRunLength": 10.99, "wallRunDrop": 3.8,
}
ENV = no_air_jumps(ENVELOPE)
BASICS = {"walk", "jump", "step up", "jump up", "sprint jump", "crouch", "slide", "roll"}
BEFORE_DASH = BASICS | {"jump to the ledge", "wall run", "wall jump", "climb", "ladder", "crate", "bounce", "rope", "zipline", "swim"}
EVERYTHING = BEFORE_DASH | {"sprint jump and air dash", "dash"}


class Envelope(unittest.TestCase):
    def test_no_air_jumps_removes_both_double_jumps(self):
        moves = [m["move"] for m in ENV["jumps"] + ENV["climbs"]]
        self.assertFalse(any("double jump" in m for m in moves))
        self.assertEqual(len(ENV["jumps"]), 4)
        self.assertEqual(len(ENV["climbs"]), 3)
        self.assertEqual(len(ENVELOPE["jumps"]), 6, "The envelope itself is left as it was.")

    def test_missing_move_raises(self):
        env = dict(ENV, wallRunLength=float("nan"))
        with self.assertRaises(LockError):
            reach("wall run", env)
        with self.assertRaises(LockError):
            reach("sprint jump", dict(ENV, jumps=[]))

    def test_every_move_is_known(self):
        for move in ("jump", "wall jump", "rope", "crouch", "sprint jump and air dash"):
            self.assertIn(move, locks.MOVES)


class Lessons(unittest.TestCase):
    def test_ledge_lesson_passes_without_air_jumps(self):
        c = Crossing("ledge", "climb", 4.0, {"jump to the ledge"})
        self.assertEqual(check_crossing(c, BASICS | {"jump to the ledge"}, ENV), [])

    def test_ledge_lesson_fails_with_a_double_jump(self):
        env = dict(ENVELOPE)
        env["climbs"] = ENVELOPE["climbs"]
        c = Crossing("ledge", "climb", 4.0, {"jump to the ledge"})
        locks.MEASURED["climb"]["double jump up"] = ("climbs", "double jump up")
        try:
            self.assertTrue(check_crossing(c, BASICS | {"jump to the ledge", "double jump up"}, env), "A double jump reaches the ledge.")
        finally:
            del locks.MEASURED["climb"]["double jump up"]

    def test_wall_run_lesson_gap_9m_with_wall_passes_before_dash(self):
        c = Crossing("wall run", "gap", 9.0, {"wall run"}, wall_beside=True)
        self.assertEqual(check_crossing(c, BASICS | {"jump to the ledge", "wall run"}, ENV), [])

    def test_dash_lesson_gap_beside_a_wall_fails(self):
        c = Crossing("dash", "gap", 10.0, {"sprint jump and air dash"}, wall_beside=True)
        failures = check_crossing(c, EVERYTHING, ENV)
        self.assertTrue(any("wall run" in f for f in failures), "A wall run reaches 10.99 m beside a wall.")

    def test_dash_lesson_gap_10m_no_wall_passes(self):
        c = Crossing("dash", "gap", 10.0, {"sprint jump and air dash"})
        self.assertEqual(check_crossing(c, EVERYTHING, ENV), [])

    def test_course_gap_needs_wall_run_but_dash_reaches_fails_unless_dash_blocked(self):
        c = Crossing("course", "gap", 9.0, {"wall run"}, wall_beside=True)
        self.assertTrue(any("air dash" in f for f in check_crossing(c, EVERYTHING, ENV)))
        c.dash_blocked = True
        self.assertEqual(check_crossing(c, EVERYTHING, ENV), [])

    def test_intended_beyond_95_percent_fails(self):
        c = Crossing("sprint", "gap", 6.5, {"sprint jump"})
        self.assertTrue(any("too close" in f for f in check_crossing(c, BASICS, ENV)))
        c.need = 6.2
        self.assertEqual(check_crossing(c, {"walk", "jump", "sprint jump"}, ENV), [])

    def test_another_move_within_90_percent_fails(self):
        c = Crossing("sprint", "gap", 3.5, {"sprint jump"})
        self.assertTrue(any(f.startswith("sprint: jump reaches") for f in check_crossing(c, {"jump", "sprint jump"}, ENV)))


class DashJumpAndAirDash(unittest.TestCase):
    """The longest one-jump gap (dash off a lip, coyote jump, air dash past the peak): expert tech, measured."""

    EXPERT = EVERYTHING | {"dash, jump and air dash"}

    def test_its_reach_is_measured(self):
        self.assertEqual(reach("dash, jump and air dash", ENV), 16.45)

    def test_a_gap_only_it_crosses_passes(self):
        c = Crossing("mastery gap", "gap", 14.5, {"dash, jump and air dash"})
        self.assertEqual(check_crossing(c, self.EXPERT, ENV), [])

    def test_a_gap_a_sprint_jump_and_air_dash_nearly_reaches_fails(self):
        c = Crossing("mastery gap", "gap", 12.5, {"dash, jump and air dash"})
        self.assertTrue(check_crossing(c, self.EXPERT, ENV), "A sprint jump and air dash reaches 11.97 m, over 90% of 12.5.")

    def test_it_needs_a_run_up(self):
        c = Crossing("mastery gap", "gap", 14.5, {"dash, jump and air dash"}, run_up=False)
        self.assertTrue(check_crossing(c, self.EXPERT, ENV), "With no run-up to the lip, it can't be done.")


class Geometry(unittest.TestCase):
    def test_ceiling_rules_out_jumps(self):
        c = Crossing("slot", "gap", 1.5, {"slide"}, ceiling=1.3, fixtures={"slot"})
        self.assertEqual(check_crossing(c, BASICS, ENV), [], "Under the slot's ceiling only the slide crosses.")
        c.ceiling = None
        self.assertTrue(check_crossing(c, BASICS, ENV), "Without the ceiling a jump crosses too.")

    def test_a_crouch_passage_fits_a_crouch_but_not_standing(self):
        c = Crossing("tunnel", "passage", 6.0, {"crouch"}, ceiling=1.3)
        self.assertEqual(check_crossing(c, {"walk", "jump", "sprint jump", "crouch"}, ENV), [])
        c.ceiling = 1.8
        self.assertTrue(any("walk" in f for f in check_crossing(c, {"walk", "crouch"}, ENV)), "Too high: it can be walked.")
        c.ceiling = 0.9
        self.assertTrue(any("ruled out" in f for f in check_crossing(c, {"walk", "crouch"}, ENV)), "Too low: it can't be crouched through.")

    def test_timing_and_no_run_up_leave_only_the_roll(self):
        c = Crossing("roll", "passage", 4.5, {"roll"}, ceiling=1.2, timed=True, run_up=False, dash_blocked=True)
        self.assertEqual(check_crossing(c, EVERYTHING, ENV), [])
        c.timed = False
        self.assertTrue(any("crouch" in f for f in check_crossing(c, EVERYTHING, ENV)))

    def test_fixture_moves_need_their_fixture(self):
        c = Crossing("rope", "gap", 14.0, {"rope"}, fixtures={"rope"})
        self.assertEqual(check_crossing(c, BEFORE_DASH, ENV), [])
        c.fixtures = set()
        self.assertTrue(any("ruled out" in f for f in check_crossing(c, BEFORE_DASH, ENV)))

    def test_an_unrelated_fixture_move_present_fails(self):
        c = Crossing("panel", "climb", 6.0, {"climb"}, fixtures={"panel", "ladder"})
        self.assertTrue(any("ladder gets through" in f for f in check_crossing(c, BEFORE_DASH, ENV)))

    def test_reach_is_infinite_for_crouching(self):
        c = Crossing("tunnel", "passage", 100.0, {"crouch"}, ceiling=1.3)
        self.assertTrue(math.isinf(locks._reach_for("crouch", c, ENV)))


if __name__ == "__main__":
    unittest.main()
