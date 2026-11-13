"""Level specs outside Unity: read levels written as JSON, XML or CSV, check them against the spec schema and against
what the character can do, convert between the formats, draw schematics, and have Unity build them.

The spec format is Unity's (LevelSpec, LevelSpecTools): a level's parts, each the Graybox pieces it's built from, in
order. Its schema, level-spec.schema.json, is written by Unity from the pieces themselves; this package reads it, so a
piece added in Unity is known here once the schema's rewritten.

    import levelspec
    spec = levelspec.read("my_level.csv")            # or .json / .xml
    levelspec.validate(spec)                          # raises SpecError listing every problem
    for finding in levelspec.check(spec): print(finding)
    levelspec.write(spec, "Assets/Levels/MyLevel.level.json")
    levelspec.write_schematic(spec, "MyLevel.html")

Or from the command line: python3 Tools/LevelSpec/levelspec --help
"""

from .schema import SpecError, schema
from .formats import read, write, loads, dumps, FORMATS
from .model import normalize, to_json, pieces
from .checks import check, load_envelope, Finding
from .geometry import layout


def validate(spec):
    """Raises SpecError listing every way spec breaks the schema; returns spec if it doesn't."""
    errors = schema().validate(spec)
    if errors:
        raise SpecError(errors)
    return spec


def write_schematic(spec, path, findings=None, envelope=None):
    """Writes a schematic of spec (an HTML page, or an SVG if path ends .svg)."""
    from .schematic import write_schematic as w
    return w(spec, path, findings=findings, envelope=envelope)


__all__ = ["SpecError", "schema", "read", "write", "loads", "dumps", "FORMATS", "normalize", "to_json", "pieces",
           "check", "load_envelope", "Finding", "layout", "validate", "write_schematic"]
