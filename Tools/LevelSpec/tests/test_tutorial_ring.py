"""Tests of the Tutorial Ring's script (examples/tutorial_ring.py): run with  python3 -m unittest discover -s Tools/LevelSpec/tests.

They check the Basin's spec is unchanged by its helpers' move to levelspec.rings; the ring's spec is valid; it has one
start checkpoint and one spawn point; each shortcut gate has its trigger on its far side; a hazard covers the ring; every
lesson in the catalog has one zone and a checkpoint, in the design's order counterclockwise, the dash last; every
crossing locks on its moves; and the course has four stretches of rising difficulty, a start, three splits and a
finish, every combination the design asks for, and no lesson zones.
"""

import json
import math
import os
import re
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
TOOL = os.path.dirname(HERE)
sys.path.insert(0, TOOL)
sys.path.insert(0, os.path.join(TOOL, "examples"))

import levelspec  # noqa: E402
from levelspec import geometry, locks  # noqa: E402

import meridian_basin  # noqa: E402
import tutorial_ring  # noqa: E402

PROJECT = os.path.dirname(os.path.dirname(TOOL))
CATALOG = os.path.join(PROJECT, "Assets", "Showcase", "TutorialRing", "Tutorial Lessons.asset")
BASIN = os.path.join(PROJECT, "Assets", "Showcase", "Levels", "MeridianBasin.level.json")


def _inside(point, polygon):
    """Whether a point (x, z) is inside a polygon of (x, z) points (even-odd)."""
    x, z = point
    inside = False
    for i in range(len(polygon)):
        (ax, az), (bx, bz) = polygon[i], polygon[(i + 1) % len(polygon)]
        if (az > z) != (bz > z) and x < ax + (z - az) * (bx - ax) / (bz - az):
            inside = not inside
    return inside


def pieces(spec, kind=None):
    for part in spec["parts"]:
        for piece in part["pieces"]:
            if kind is None or piece["piece"] == kind:
                yield part["name"], piece


class Ring(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.spec = levelspec.normalize(tutorial_ring.build())
        cls.shapes = geometry.layout(cls.spec)

    def world_of(self, piece_name, **match):
        """The world (x, z) of the first shape of a piece matching the settings given."""
        for s in self.shapes:
            if s.piece["piece"] == piece_name and all(s.piece.get(k) == v for k, v in match.items()):
                x0, z0, x1, z1 = s.bounds
                return ((x0 + x1) / 2, (z0 + z1) / 2)
        self.fail(f"No {piece_name} {match}")

    def test_basin_spec_unchanged_after_moving_helpers(self):
        with open(BASIN, encoding="utf-8") as f:
            committed = levelspec.normalize(json.load(f))
        self.assertEqual(levelspec.normalize(meridian_basin.build()), committed)

    def test_it_has_its_own_sky_and_keeps_its_tuned_post_processing(self):
        self.assertEqual(self.spec["sky"], "Assets/Showcase/Materials/Sky/Sky_29.mat")
        self.assertIs(self.spec["keepPostProcessing"], True)

    def test_ring_spec_is_valid_against_schema(self):
        levelspec.validate(self.spec)

    def test_ring_has_one_start_checkpoint_and_one_spawn(self):
        starts = [p for _, p in pieces(self.spec, "Checkpoint") if p.get("start")]
        self.assertEqual(len(starts), 1)
        self.assertEqual(len(list(pieces(self.spec, "SpawnMarker"))), 1)

    def test_both_shortcut_gates_have_a_trigger_on_their_far_side(self):
        hub = self.world_of("Checkpoint", id="hub")
        for gate in ("lessons", "course"):
            g = self.world_of("ShortcutGate", id=gate)
            t = self.world_of("ShortcutTrigger", gate=gate)
            self.assertGreater(math.dist(t, hub), math.dist(g, hub), f"The {gate} gate's trigger should be on its side away from the hub.")
            self.assertGreater(math.dist(g, hub), tutorial_ring.hub_reach(), f"The {gate} gate should stand outside the hub, or it can be walked round.")

    def test_every_checkpoint_stands_on_a_floor(self):
        floors = [s for s in self.shapes if s.layer in ("solid", "deck") and s.kind == "poly"]
        for s in self.shapes:
            if s.piece["piece"] != "Checkpoint":
                continue
            feet = s.piece["feet"]
            x0, z0, x1, z1 = s.bounds
            at = ((x0 + x1) / 2, (z0 + z1) / 2)
            under = [f for f in floors if abs(f.y1 - s.y0) < 0.05 and _inside(at, f.points)]
            self.assertTrue(under, f"Checkpoint {s.piece['id']} at {at} (y {feet[1]}) should stand on a floor, or a respawn there falls.")

    def test_the_ring_is_walled_all_round_but_for_its_ways_in(self):
        plans = tutorial_ring.WALL_PLAN
        self.assertEqual(len(plans), 8)
        span = 2 * tutorial_ring.face_half_length()
        for i, plan in enumerate(plans):
            for side in ("inner", "outer"):
                marked = [(a, b) for a, b, sides in plan["marked"] if side in sides]
                covered = plan["walls"][side] + marked + plan["open"] + (plan["keep_clear"] if side == "outer" else [])
                for u0, u1 in tutorial_ring._subtract([(0.0, span)], covered):
                    at_hub = (i == 0 and u0 < 1e-6) or (i == len(plans) - 1 and u1 > span - 1e-6)
                    at_dock = side == "inner" and ((i == 3 and u1 > span - 1e-6) or (i == 4 and u0 < 1e-6))
                    self.assertTrue(at_hub or at_dock, f"Face {plan['face']}'s {side} side has no wall from {u0:.1f} to {u1:.1f} m.")

    def test_no_ring_wall_stands_on_a_marked_wall(self):
        for plan in tutorial_ring.WALL_PLAN:
            for side in ("inner", "outer"):
                for m0, m1, sides in plan["marked"]:
                    if side not in sides:
                        continue
                    for w0, w1 in plan["walls"][side]:
                        self.assertTrue(w1 <= m0 + 1e-6 or w0 >= m1 - 1e-6,
                                        f"Face {plan['face']}'s {side} ring wall {w0:.1f}-{w1:.1f} m overlaps a marked wall: it'd be swept into first.")

    def test_only_the_wall_run_and_wall_jump_crossings_have_marked_walls(self):
        for c, _ in tutorial_ring.CROSSINGS:
            if c.wall_beside:
                self.assertIn("wall run", c.intended, f"{c.name}: only a wall run's crossing has a wall marked for it beside it.")
        parts = {part for part, _ in pieces(self.spec, "MarkedWall")}
        self.assertEqual(parts, {"Lessons 2 (E)", "Lessons 3 (NE)", "Course 2 (W)", "Course 3 (SW)"},
                         "Marked walls belong to the wall run and wall jump lessons and the stretches with wall runs.")
        for _, w in pieces(self.spec, "MarkedWall"):
            self.assertIn(w["move"], ("WallRun", "WallSlide"), "A marked wall is for one move, never both.")

    def test_a_hazard_covers_the_ring(self):
        hazards = [p for _, p in pieces(self.spec, "HazardVolume")]
        self.assertTrue(hazards)
        reach = tutorial_ring.RING_APOTHEM / math.cos(math.radians(22.5)) + tutorial_ring.LANE
        self.assertTrue(any(h["size"][0] / 2 >= reach and h["size"][2] / 2 >= reach and h["centre"][1] + h["size"][1] / 2 < tutorial_ring.FLOOR - 4
                            for h in hazards), "One hazard should lie under the whole ring, below its decks.")


class Lessons(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.spec = levelspec.normalize(tutorial_ring.build())
        cls.crossings = list(tutorial_ring.CROSSINGS)
        with open(CATALOG, encoding="utf-8") as f:
            cls.catalog = re.findall(r"^\s*- id: (\S+)", f.read(), re.M)

    def test_the_catalog_has_the_designs_lessons(self):
        self.assertEqual(len(self.catalog), 18)

    def test_every_lesson_in_the_catalog_has_one_zone_and_a_checkpoint(self):
        zones = [p["lesson"] for _, p in pieces(self.spec, "LessonZone")]
        checkpoints = {p["id"] for _, p in pieces(self.spec, "Checkpoint")}
        for lesson in self.catalog:
            self.assertEqual(zones.count(lesson), 1, f"{lesson} should have one zone.")
            self.assertIn(lesson, checkpoints, f"{lesson} should have a checkpoint.")
        self.assertEqual(sorted(zones), sorted(self.catalog), "Every zone should be a lesson of the catalog's.")

    def test_every_lesson_crossing_passes_the_lock_check(self):
        lesson_crossings = [(c, t) for c, t in self.crossings if c.name.startswith("L")]
        self.assertGreaterEqual(len(lesson_crossings), 14)
        self.assertEqual(locks.check_all(lesson_crossings, tutorial_ring.ENV), [])

    def test_lessons_run_in_the_designs_order_counterclockwise(self):
        order = [lesson for face in tutorial_ring.LESSONS for lesson, _ in face]
        self.assertEqual(order, self.catalog)
        # Counterclockwise from the hub: the run's faces step down by 45 degrees (mod 360).
        faces = tutorial_ring.RUN_FACES
        self.assertTrue(all((faces[i] - faces[i + 1]) % 8 == 1 for i in range(len(faces) - 1)))
        zones = [p["lesson"] for _, p in pieces(self.spec, "LessonZone")]
        self.assertEqual(zones, self.catalog, "The zones should come in the catalog's order, as they're run.")

    def test_dash_is_taught_last(self):
        before = set()
        for name, (c, taught) in ((c.name, (c, t)) for c, t in self.crossings if c.name.startswith("L")):
            if not name.startswith("L18"):
                self.assertNotIn("sprint jump and air dash", taught, f"{name}: the dash shouldn't be taught yet.")
        self.assertEqual(tutorial_ring.LESSONS[-1][-1][0], "L18-dash")


class Course(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.spec = levelspec.normalize(tutorial_ring.build())
        cls.crossings = [(c, t) for c, t in tutorial_ring.CROSSINGS if c.name.startswith("Stretch")]

    def test_course_has_four_stretches_with_rising_shares(self):
        self.assertEqual(len(tutorial_ring.STRETCHES), 4)
        self.assertEqual(tutorial_ring.SHARES, sorted(tutorial_ring.SHARES))
        self.assertLessEqual(max(tutorial_ring.SHARES), locks.INTENDED_SHARE)
        for n in range(4):
            self.assertGreaterEqual(sum(1 for c, _ in self.crossings if c.name.startswith(f"Stretch {n + 1}:")), 3,
                                    f"Stretch {n + 1} should have at least three crossings.")

    def test_every_course_crossing_passes_the_lock_check_with_everything_taught(self):
        # The dash, jump and air dash is expert tech, not taught (the mastery routes' design): only the mastery routes'
        # long gaps are checked with it.
        expert = "dash, jump and air dash"
        everything = set().union(*(t for _, t in tutorial_ring.CROSSINGS)) - {expert}
        for c, taught in self.crossings:
            expected = everything | {expert} if expert in c.intended else everything
            self.assertEqual(taught, expected, f"{c.name}: on the course, everything's taught.")
        self.assertEqual(locks.check_all(self.crossings, tutorial_ring.ENV), [])

    def test_course_has_start_finish_and_three_splits(self):
        gates = [p for _, p in pieces(self.spec, "TimingGate") if p["course"] == "Agility"]
        roles = sorted((g["role"], g.get("index", 0)) for g in gates)
        self.assertEqual(roles, [("finish", 0), ("split", 0), ("split", 1), ("split", 2), ("start", 0)])

    def test_every_spec_combo_appears_on_the_course(self):
        names = " ".join(c.name for c, _ in self.crossings)
        self.assertIn("slide into a jump", names)
        self.assertIn("ropes", names)
        self.assertIn("zipline", names)
        self.assertIn("bounce into a wall run", names)
        self.assertIn("wall jumps", names)
        self.assertIn("dash", names)
        self.assertIn("crate", names)
        self.assertIn("panel", names)
        self.assertEqual(set(tutorial_ring.COMBOS), {"slide into a jump", "rope to zipline", "bounce pad into a wall run",
                                                       "wall run into a wall jump", "dash to a ledge grab", "crate to a ledge to a climb"})

    def test_course_has_no_lesson_zones(self):
        for part, piece in pieces(self.spec, "LessonZone"):
            self.assertTrue(part.startswith("Lessons"), f"A lesson zone in {part}")


if __name__ == "__main__":
    unittest.main()


class Mastery(unittest.TestCase):
    """The mastery routes over the agility course's long stretches (docs/superpowers/specs/2026-11-25-tutorial-ring-mastery-routes-design.md):
    an undercroft each, its main route slow, its roof the mastery line, every fall from it onto the main route."""

    @classmethod
    def setUpClass(cls):
        cls.spec = levelspec.normalize(tutorial_ring.build())
        cls.crossings = list(tutorial_ring.CROSSINGS)

    def test_courses_1_to_3_have_one_and_nothing_else_does(self):
        self.assertEqual(set(tutorial_ring.MASTERY), {"Stretch 1", "Stretch 2", "Stretch 3"})

    def test_each_is_entered_by_a_ledge_grab_and_ends_with_the_longest_one_jump_gap(self):
        for stretch in tutorial_ring.MASTERY:
            mine = [c for c, _ in self.crossings if c.name.startswith(f"{stretch}: mastery")]
            self.assertTrue(any(c.kind == "climb" and c.intended == {"jump to the ledge"} for c in mine), f"{stretch}'s entry")
            self.assertTrue(any(c.kind == "gap" and c.intended == {"dash, jump and air dash"} for c in mine), f"{stretch}'s last gap")

    def test_every_fall_lands_on_the_main_route(self):
        for stretch, m in tutorial_ring.MASTERY.items():
            for z0, z1 in m["openings"]:
                self.assertTrue(any(f0 <= z0 + 1e-6 and z1 <= f1 + 1e-6 for f0, f1 in m["main_floor"]),
                                f"{stretch}'s opening {z0}..{z1} isn't over main route floor")
                for b0, b1 in m["blocked"]:
                    self.assertFalse(b0 < z1 and z0 < b1, f"{stretch}'s opening {z0}..{z1} is over {b0}..{b1}, which stands up into it")

    def test_the_roof_is_out_of_the_main_routes_reach(self):
        out_of_reach = tutorial_ring.reach("jump to the ledge", "climb") / locks.OTHER_SHARE
        for stretch, m in tutorial_ring.MASTERY.items():
            self.assertGreater(m["roof"] - tutorial_ring.FLOOR, out_of_reach, f"{stretch}: from its deck")
            self.assertGreater(m["roof"] - tutorial_ring.FLOOR - tutorial_ring.VAULT_WALL, out_of_reach, f"{stretch}: from a hurdle")

    def test_every_pit_has_a_way_out(self):
        for stretch, m in tutorial_ring.MASTERY.items():
            self.assertTrue(m["pits"], f"{stretch} has a pit")
            for pit in m["pits"]:
                self.assertIn(pit["out"], ("ladder", "crate"))

    def test_the_mastery_line_saves_time(self):
        for stretch, m in tutorial_ring.MASTERY.items():
            self.assertGreaterEqual(m["main_time"] - m["mastery_time"], tutorial_ring.MASTERY_MARGIN,
                                    f"{stretch}: main {m['main_time']:.1f} s, mastery {m['mastery_time']:.1f} s")

    def test_no_wall_lamps_beside_an_undercroft(self):
        # Lamps along the ring walls would stand at head height over the catwalks, or inside Course 3's upper catwalk.
        h = tutorial_ring.face_half_length()
        for plan in tutorial_ring.WALL_PLAN:
            spans = plan.get("no_windows", [])
            if not spans:
                continue
            for _, piece in pieces(self.spec, "Trim"):
                if piece.get("name") == "Wall Lamp" and piece.get("parent") == plan["frame"]:
                    u = h - piece["centre"][0]
                    self.assertFalse(any(u0 - 1 <= u <= u1 + 1 for u0, u1 in spans), f"a lamp at u {u:.1f} beside {spans}")

    def test_no_floor_stands_in_the_hazard(self):
        # The ground's hazard volume respawns whoever's feet are in it: every floor must stand clear of its top.
        hazard = [p for _, p in pieces(self.spec, "HazardVolume") if p.get("name") == "Fall"][0]
        top = hazard["centre"][1] + hazard["size"][1] / 2
        for part, p in pieces(self.spec, "Platform"):
            self.assertGreater(p["top"], top + tutorial_ring.HAZARD_CLEARANCE - 1e-6, f"{part}: {p.get('name')} at {p['top']}")

    def test_the_mastery_chimney_is_for_wall_slides_as_the_lessons_shaft_is(self):
        walls = [p for _, p in pieces(self.spec, "MarkedWall") if p.get("name") == "Mastery Chimney"]
        self.assertEqual(len(walls), 2)
        for w in walls:
            self.assertEqual(w["move"], "WallSlide")
        inner_faces = sorted(abs(w["a"][0]) - w["thickness"] / 2 for w in walls)
        self.assertAlmostEqual(inner_faces[0] * 2, tutorial_ring.CHIMNEY_WIDTH, places=3)

    def test_their_faces_have_no_plain_lane_left(self):
        lanes = {part for part, p in pieces(self.spec, "Platform") if p.get("name") == "Lane"}
        for part in ("Course 1 (NW)", "Course 2 (W)", "Course 3 (SW)"):
            self.assertNotIn(part, lanes)
        self.assertIn("Course 4 (S)", lanes, "Course 4's 37 m is under the 40 m the mastery routes are for")
