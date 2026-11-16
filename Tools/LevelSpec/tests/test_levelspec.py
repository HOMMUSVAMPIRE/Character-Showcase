"""Tests of the levelspec package: run with  python3 -m unittest discover -s Tools/LevelSpec/tests  (CI does).

They check the exported levels (Assets/Levels) are valid; every format converts to every other and back to the same
spec; XML and CSV written by hand read as meant (groups, text values, defaults); mistakes are reported where they are;
groups' placement carries to what's in them; the movement checks grade gaps and climbs by the moves that cross and
reach them (made-up moves, and the character's own from envelope.json, whatever it's tuned to);
and schematics are drawn.
"""

import json
import math
import os
import sys
import tempfile
import unittest
import xml.dom.minidom

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))

import levelspec  # noqa: E402
from levelspec import SpecError, checks, formats, geometry  # noqa: E402

PROJECT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
LEVELS = os.path.join(PROJECT, "Assets", "Levels")
EXAMPLES = os.path.join(os.path.dirname(HERE), "examples")
EXPORTED = sorted(f for f in os.listdir(LEVELS) if f.endswith(".level.json")) if os.path.isdir(LEVELS) else []


def spec_with(*pieces, name="Test", scene="Assets/Scenes/Test/Test.unity"):
    return levelspec.normalize({"name": name, "scene": scene, "parts": [{"name": "Part", "pieces": list(pieces)}]})


class ExportedLevels(unittest.TestCase):
    def test_there_are_exported_levels(self):
        self.assertTrue(EXPORTED, "Export the level specs from Unity (Tools > Character Showcase > Level > Specs).")

    def test_each_is_valid(self):
        for name in EXPORTED:
            with self.subTest(name):
                levelspec.validate(levelspec.read(os.path.join(LEVELS, name)))

    def test_each_converts_to_every_format_and_back_unchanged(self):
        for name in EXPORTED:
            path = os.path.join(LEVELS, name)
            original = levelspec.read(path)
            with open(path, encoding="utf-8") as f:
                text = f.read()
            for fmt in ("xml", "csv"):
                with self.subTest(name, format=fmt):
                    back = levelspec.loads(levelspec.dumps(original, fmt), fmt)
                    self.assertEqual(original, back)
                    # Written as Unity writes it, too: the same text.
                    self.assertEqual(text, levelspec.dumps(back, "json"))


class Authoring(unittest.TestCase):
    def test_xml_groups_text_values_and_defaults(self):
        spec = levelspec.loads("""<level name="L" scene="Assets/Scenes/L/L.unity">
          <part name="P">
            <Group name="Zone">
              <Platform name="Deck" x0="0" x1="10" z0="0" z1="4" y0="0" top="4"/>
              <Prism name="Pad" shape="0 0; 4, 0; 4 4" y0="0" y1="1"/>
              <Tree at="1,2" floor="0" random="7"/>
            </Group>
            <Bollards shape="0,0; 4,0; 4,4" floor="0" gaps="2, 0, 1.5; 4, 2, 1"/>
            <Cells openTop="-1">
              <cells><cell name="Hall" x0="0" z0="0" x1="20" z1="10" floor="0" ceiling="6"/></cells>
              <portals>-4, 0, 0, 1</portals>
            </Cells>
          </part></level>""", "xml")
        levelspec.validate(spec)
        p = spec["parts"][0]["pieces"]
        self.assertEqual(p[0], {"piece": "Group", "parent": "", "name": "Zone"})
        self.assertEqual(p[1]["parent"], "Zone", "A piece in a group defaults to it as its parent.")
        self.assertNotIn("coping", p[1], "Settings left out stay out (their defaults are Unity's).")
        self.assertEqual(p[2]["shape"], [[0, 0], [4, 0], [4, 4]])
        self.assertEqual(p[3]["random"], {"seed": 7})
        self.assertEqual(p[4]["parent"], "", "Outside a group, the parent is the root.")
        self.assertEqual(p[4]["gaps"], [[[2, 0], 1.5], [[4, 2], 1]])
        self.assertEqual(p[5]["cells"], [{"name": "Hall", "x0": 0, "z0": 0, "x1": 20, "z1": 10, "floor": 0, "ceiling": 6}])
        self.assertEqual(p[5]["portals"], [[-4, 0, 0, 1]])

    def test_nested_groups_have_paths(self):
        spec = levelspec.loads("""<level name="L" scene="Assets/Scenes/L/L.unity"><part name="P">
            <Group name="A"><Group name="B"><Marker name="M" at="0,0" height="0"/></Group></Group>
            <Group name="A"><Marker name="N" at="0,0" height="0"/></Group></part></level>""", "xml")
        p = spec["parts"][0]["pieces"]
        self.assertEqual(p[2]["parent"], "A/B")
        self.assertEqual(p[4]["parent"], "A#2", "The second group named A is A#2.")

    def test_csv_with_a_level_row_and_carried_parts(self):
        spec = levelspec.loads(
            "part,piece,parent,name,x0,x1,z0,z1,y0,y1,scene\n"
            ",Level,,My Level,,,,,,,Assets/Scenes/My/My.unity\n"
            "One,Block,,A,0,1,0,1,0,1,\n"
            ",Block,,B,2,3,0,1,0,1,\n"
            "Two,Block,,C,0,1,0,1,0,1,\n", "csv")
        levelspec.validate(spec)
        self.assertEqual(spec["name"], "My Level")
        self.assertEqual([p["name"] for p in spec["parts"]], ["One", "Two"])
        self.assertEqual([x["name"] for x in spec["parts"][0]["pieces"]], ["A", "B"], "An empty part carries on the one above.")

    def test_the_examples_are_valid(self):
        for name in (n for n in os.listdir(EXAMPLES) if n.rsplit(".", 1)[-1] in levelspec.FORMATS):
            with self.subTest(name):
                levelspec.validate(levelspec.read(os.path.join(EXAMPLES, name)))


class Mistakes(unittest.TestCase):
    def errors(self, *pieces):
        return levelspec.schema().validate(spec_with(*pieces))

    def test_unknown_piece_with_a_suggestion(self):
        e = self.errors({"piece": "Platfrom", "parent": ""})
        self.assertIn("no piece 'Platfrom' (did you mean 'Platform'?)", e[0])
        self.assertIn("Part 'Part', piece 1", e[0])

    def test_unknown_and_missing_settings(self):
        e = self.errors({"piece": "Block", "parent": "", "name": "B", "x0": 0, "x1": 1, "z0": 0, "z1": 1, "y0": 0, "top": 1})
        self.assertTrue(any("no setting 'top'" in x for x in e))
        self.assertTrue(any("needs a 'y1'" in x for x in e))

    def test_wrong_shapes(self):
        e = self.errors({"piece": "Ramp", "parent": "", "name": "R", "start": [0, 0, 0], "direction": [0, 1], "width": "wide",
                         "length": 8, "y0": 0, "y1": 2},
                        {"piece": "Block", "parent": "", "name": "B", "x0": 0, "x1": 1, "z0": 0, "z1": 1, "y0": 0, "y1": 1, "surface": "Marble"})
        self.assertTrue(any("'start'" in x and "a point [x, z]" in x for x in e), e)
        self.assertTrue(any("'width'" in x and "a number" in x for x in e), e)
        self.assertTrue(any("'surface'" in x and "isn't one of" in x for x in e), e)

    def test_bad_text_values_say_where(self):
        with self.assertRaises(SpecError) as c:
            levelspec.loads('<level name="L" scene="Assets/S/S.unity"><part name="P">\n<Block name="B" x0="one" x1="1" z0="0" z1="1" y0="0" y1="1"/></part></level>', "xml", source="l.xml")
        self.assertIn("l.xml line 2 <Block> 'x0'", str(c.exception))
        with self.assertRaises(SpecError) as c:
            levelspec.loads("part,piece,name,at\nP,Marker,M,1 2 3\n", "csv", source="l.csv")
        self.assertIn("l.csv row 2", str(c.exception))

    def test_bad_json_says_where(self):
        with self.assertRaises(SpecError) as c:
            levelspec.loads('{\n  "name": "x",\n  "parts" []\n}', "json")
        self.assertIn("line 3", str(c.exception))


class Geometry(unittest.TestCase):
    def test_placed_and_turned_groups_carry_to_their_contents(self):
        spec = spec_with(
            {"piece": "Placed", "parent": "", "name": "P", "position": [100, 2, 50]},
            {"piece": "Turned", "parent": "P", "name": "T", "yaw": 90},
            {"piece": "Block", "parent": "P/T", "name": "B", "x0": 0, "x1": 10, "z0": 0, "z1": 2, "y0": 0, "y1": 1})
        block = geometry.layout(spec)[0]
        x0, z0, x1, z1 = block.bounds
        # Turned 90° (Unity: +z goes to +x, +x to -z), then placed at (100, 50) and 2 up.
        self.assertAlmostEqual(x0, 100)
        self.assertAlmostEqual(x1, 102)
        self.assertAlmostEqual(z0, 40)
        self.assertAlmostEqual(z1, 50)
        self.assertAlmostEqual(block.y1, 3)

    def test_flights_follow_state(self):
        flight = {"piece": "Flight", "parent": "", "name": "S", "basePoint": [0, 0], "direction": [0, 1], "width": 2, "y0": 0, "y1": 3}
        default, changed = geometry.layout(spec_with(flight, {"piece": "State", "stepRise": 0.25, "stepRun": 0.5}, flight))
        state = geometry.default_state()
        self.assertAlmostEqual(default.bounds[3], geometry.flight_length(0, 3, state))
        self.assertAlmostEqual(changed.bounds[3], 12 * 0.5)


def platform(name, x0, x1, z0, z1, top):
    return {"piece": "Platform", "parent": "", "name": name, "x0": x0, "x1": x1, "z0": z0, "z1": z1, "y0": 0, "top": top}


def envelope_with(jumps=None, climbs=None):
    """The character's envelope with made-up moves, so the grading is tested whatever the character is tuned to."""
    env = dict(checks.load_envelope())
    if jumps is not None:
        env["jumps"] = [{"distance": d, "move": m} for m, d in jumps]
    if climbs is not None:
        env["climbs"] = [{"height": h, "move": m} for m, h in climbs]
    return env


def gap_course(*gaps):
    """A pair of 6 m platforms 4 m up per gap, each pair far from the others, gap metres apart along x."""
    pieces = []
    for i, gap in enumerate(gaps):
        z = 100 * i
        pieces += [platform(f"From {i}", 0, 6, z, z + 6, 4), platform(f"To {i}", 6 + gap, 12 + gap, z, z + 6, 4)]
    return spec_with(*pieces)


def gap_grades(findings):
    return {f.text.split(" m ")[0]: f.text.rsplit(": ", 1)[1] for f in findings if f.kind == "gap"}


class MovementChecks(unittest.TestCase):
    def test_a_gap_is_graded_by_the_shortest_move_that_crosses_it(self):
        env = envelope_with(jumps=[("jump", 3), ("sprint jump", 6), ("double jump", 9)])
        findings = levelspec.check(gap_course(2.5, 5, 8, 10), envelope=env)
        self.assertEqual(gap_grades(findings), {"2.5": "jump", "5.0": "sprint jump", "8.0": "double jump", "10.0": "too far to jump (over 9 m)"})
        self.assertEqual([f.severity for f in findings if f.kind == "gap" and f.text.startswith("10.0")], ["warning"])

    def test_a_climb_is_graded_by_the_lowest_move_that_reaches_it(self):
        env = envelope_with(climbs=[("step up", 0.35), ("jump up", 2), ("jump to the ledge", 4.5)])
        spec = spec_with(platform("Base", 0, 6, 0, 6, 1),
                         platform("Low", 6, 12, 0, 6, 2.5), platform("Ledge", -6, 0, 0, 6, 5), platform("Cliff", 0, 6, 6, 12, 7))
        climbs = sorted(f.text.split(" m up")[0] + ": " + f.text.rsplit(": ", 1)[1] for f in levelspec.check(spec, envelope=env) if f.kind == "climb")
        self.assertEqual(climbs, ["1.5: jump up", "4.0: jump to the ledge", "6.0: out of reach (over 4.5 m)"])

    def test_the_characters_own_moves_grade_gaps_up_to_their_reach(self):
        env = checks.load_envelope()
        jumps = sorted(env["jumps"], key=lambda j: j["distance"])
        farthest = jumps[-1]["distance"]
        # Just short of each move's reach, to a tenth of a metre (so the findings' text rounds to the same).
        gaps = [math.floor((j["distance"] - 0.05) * 10) / 10 for j in jumps] + [math.floor(farthest * 10) / 10 + 0.5]
        grades = gap_grades(levelspec.check(gap_course(*gaps), envelope=env))
        for gap in gaps[:-1]:
            self.assertEqual(grades[f"{gap:.1f}"], checks.move_for(gap, env["jumps"], "distance"))
        self.assertTrue(grades[f"{gaps[-1]:.1f}"].startswith("too far to jump"), grades)

    def test_steep_ramps_and_low_beams_are_warned_of(self):
        f = levelspec.check(spec_with(
            {"piece": "Ramp", "parent": "", "name": "Steep", "start": [0, 0], "direction": [0, 1], "width": 4, "length": 4, "y0": 0, "y1": 2},
            {"piece": "LowBeam", "parent": "", "name": "Beam", "a": [0, 10], "b": [4, 10], "floor": 0, "clear": 0.9}))
        self.assertTrue(any(x.kind == "ramp" and x.severity == "warning" for x in f))
        self.assertTrue(any(x.kind == "clearance" and x.severity == "warning" for x in f))


class Schematics(unittest.TestCase):
    def test_html_and_svg(self):
        spec = levelspec.read(os.path.join(EXAMPLES, "gap_course.xml"))
        with tempfile.TemporaryDirectory() as d:
            findings = levelspec.write_schematic(spec, os.path.join(d, "s.html"))
            levelspec.write_schematic(spec, os.path.join(d, "s.svg"))
            with open(os.path.join(d, "s.html"), encoding="utf-8") as f:
                page = f.read()
            self.assertIn("<svg", page)
            self.assertIn("Gap Course", page)
            self.assertEqual(page.count('class="check '), sum(1 for x in findings if x.line or True))
            xml.dom.minidom.parse(os.path.join(d, "s.svg"))  # A well-formed SVG.


if __name__ == "__main__":
    unittest.main()
