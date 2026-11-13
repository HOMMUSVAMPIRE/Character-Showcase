"""The level spec schema (level-spec.schema.json, written by Unity's LevelSpecTools from the Graybox pieces), and checking
specs against it.

The schema is JSON Schema (draft 2020-12), but only the parts of it the generated schema uses are checked here
(types, const, enum, $ref, oneOf, properties, required, additionalProperties, items, prefixItems, min/maxItems,
pattern, minLength, maximum), so nothing beyond Python's standard library is needed. A piece's oneOf is resolved by its
"piece" name, so a mistake is reported against the piece it's in, not as "matches none of 53 schemas".
"""

import json
import math
import os
import re

SCHEMA_PATH = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "level-spec.schema.json")
FORMAT = "character-showcase-level"
VERSION = 1


class SpecError(Exception):
    """A spec that can't be read or doesn't follow the schema. `errors` lists every problem found, each saying where."""

    def __init__(self, errors):
        self.errors = list(errors) if not isinstance(errors, str) else [errors]
        super().__init__("\n".join(self.errors))


class Schema:
    """The schema, with what the readers need to know about each piece's settings."""

    def __init__(self, path=SCHEMA_PATH):
        with open(path, encoding="utf-8") as f:
            self.root = json.load(f)
        self.defs = self.root["$defs"]
        #: piece name -> its object schema ("State" included)
        self.pieces = {key[len("piece."):]: value for key, value in self.defs.items() if key.startswith("piece.")}

    def resolve(self, schema):
        """A schema with its $ref followed (repeatedly)."""
        while "$ref" in schema:
            schema = self.defs[schema["$ref"].split("/")[-1]]
        return schema

    def kind(self, schema):
        """What a setting's schema is, by name: 'path', 'point2', 'point3', 'area', 'random', 'cell', an enum's def name,
        'number', 'integer', 'boolean', 'string', 'list' or 'tuple'."""
        if "$ref" in schema:
            name = schema["$ref"].split("/")[-1]
            return "enum" if "enum" in self.defs[name] else name
        t = schema.get("type")
        if isinstance(t, list):
            t = next(x for x in t if x != "null")
        if t == "array":
            return "tuple" if "prefixItems" in schema else "list"
        return t

    def settings(self, piece):
        """A piece's settings, in order: name -> schema. Empty for an unknown piece."""
        s = self.pieces.get(piece)
        return {k: v for k, v in s["properties"].items() if k != "piece"} if s else {}

    def required(self, piece):
        s = self.pieces.get(piece)
        return [k for k in s.get("required", []) if k != "piece"] if s else []

    # ---- Checking ----

    def validate(self, spec):
        """Every way spec breaks the schema, each a message saying where ('part "P2", piece 4 (Block): ...'). Empty: none."""
        errors = []
        if not isinstance(spec, dict):
            return ["A level spec is a JSON object."]
        top = self.root
        for key in spec:
            if key not in top["properties"]:
                errors.append(f"The spec has no setting '{key}' (it has {', '.join(top['properties'])}).")
        for key in top["required"]:
            if key not in spec:
                errors.append(f"The spec needs a '{key}'.")
        for key in ("format", "version", "name", "scene"):
            if key in spec:
                errors += [f"'{key}': {e}" for e in self._check(spec[key], top["properties"][key])]
        parts = spec.get("parts")
        if parts is None:
            return errors
        if not isinstance(parts, list):
            return errors + ["'parts' should be a list."]
        for p, part in enumerate(parts, 1):
            if not isinstance(part, dict) or not isinstance(part.get("name"), str):
                errors.append(f"Part {p} needs a 'name'.")
                continue
            for key in part:
                if key not in ("name", "pieces"):
                    errors.append(f"Part '{part['name']}' has no setting '{key}'.")
            pieces = part.get("pieces", [])
            if not isinstance(pieces, list):
                errors.append(f"Part '{part['name']}': 'pieces' should be a list.")
                continue
            for i, piece in enumerate(pieces, 1):
                errors += [f"Part '{part['name']}', piece {i}{_label(piece)}: {e}" for e in self.validate_piece(piece)]
        return errors

    def validate_piece(self, piece):
        """Every way one piece entry breaks the schema."""
        if not isinstance(piece, dict):
            return ["not an object."]
        name = piece.get("piece")
        if not isinstance(name, str):
            return ["it needs a 'piece': which piece it is."]
        if name not in self.pieces:
            return [f"there's no piece '{name}'{_suggest(name, self.pieces)}."]
        schema = self.pieces[name]
        errors = []
        settings = schema["properties"]
        for key in piece:
            if key not in settings:
                errors.append(f"'{name}' has no setting '{key}'{_suggest(key, settings)} "
                              f"(it has {', '.join(k for k in settings if k != 'piece')}).")
        for key in schema.get("required", []):
            if key not in piece:
                errors.append(f"'{name}' needs a '{key}' ({self.describe(settings[key])}).")
        for key, value in piece.items():
            if key in settings and key != "piece":
                errors += [f"'{key}': {e}" for e in self._check(value, settings[key])]
        return errors

    def _check(self, value, schema):
        original, schema = schema, self.resolve(schema)
        if "const" in schema:
            return [] if value == schema["const"] else [f"should be {json.dumps(schema['const'])}."]
        if "enum" in schema:
            return [] if value in schema["enum"] else [f"{json.dumps(value)} isn't one of {', '.join(map(str, schema['enum']))}."]
        if "oneOf" in schema:
            name = value.get("piece") if isinstance(value, dict) else None
            return self.validate_piece(value) if name is not None or isinstance(value, dict) else ["should be a piece."]
        t = schema.get("type")
        types = t if isinstance(t, list) else [t] if t else []
        if types and not any(_is(value, x) for x in types):
            return [f"{_short(value)} isn't {self.describe(original)}."]
        errors = []
        if isinstance(value, str):
            if "minLength" in schema and len(value) < schema["minLength"]:
                errors.append("shouldn't be empty.")
            if "pattern" in schema and not re.search(schema["pattern"], value):
                errors.append(f"'{value}' doesn't match {schema['pattern']}.")
        if _is(value, "number") and "maximum" in schema and value > schema["maximum"]:
            errors.append(f"should be at most {schema['maximum']}.")
        if isinstance(value, list):
            n = len(value)
            if ("minItems" in schema and n < schema["minItems"]) or ("maxItems" in schema and n > schema["maxItems"]):
                return [f"{_short(value)} isn't {self.describe(original)}."]
            if "prefixItems" in schema:
                for i, (item, s) in enumerate(zip(value, schema["prefixItems"]), 1):
                    errors += [f"item {i}: {e}" for e in self._check(item, s)]
            elif "items" in schema:
                for i, item in enumerate(value, 1):
                    errors += [f"item {i}: {e}" for e in self._check(item, schema["items"])]
        if isinstance(value, dict) and "properties" in schema:
            props = schema["properties"]
            for key in value:
                if key not in props and schema.get("additionalProperties") is False:
                    errors.append(f"has no '{key}' (it has {', '.join(props)}).")
            for key in schema.get("required", []):
                if key not in value:
                    errors.append(f"needs '{key}'.")
            for key, item in value.items():
                if key in props:
                    errors += [f"'{key}': {e}" for e in self._check(item, props[key])]
        return errors

    def describe(self, schema):
        """What a value of this schema looks like, for messages."""
        kind = self.kind(schema)
        s = self.resolve(schema)
        if kind == "enum":
            return "one of " + ", ".join(s["enum"])
        names = {"point2": "a point [x, z]", "point3": "a point [x, y, z]", "area": "an area [xMin, zMin, xMax, zMax]",
                 "path": "a parent's path", "random": "a random {seed}", "cell": "a cell {name, x0, z0, x1, z1, floor, ceiling, open}",
                 "number": "a number", "integer": "a whole number", "boolean": "true or false", "string": "a string",
                 "object": "an object"}
        if kind in names:
            return names[kind]
        if kind == "tuple":
            return "[" + ", ".join(self.describe(i) for i in s["prefixItems"]) + "]"
        if kind == "list":
            return "a list of " + re.sub(r"^an? ", "", self.describe(s["items"]))
        return str(kind)


def _is(value, t):
    if t == "null":
        return value is None
    if t == "boolean":
        return isinstance(value, bool)
    if t == "number":
        return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)
    if t == "integer":
        return _is(value, "number") and float(value).is_integer()
    if t == "string":
        return isinstance(value, str)
    if t == "array":
        return isinstance(value, list)
    if t == "object":
        return isinstance(value, dict)
    return True


def _short(value):
    text = json.dumps(value, ensure_ascii=False)
    return text if len(text) <= 60 else text[:57] + "..."


def _label(piece):
    return f" ({piece['piece']})" if isinstance(piece, dict) and isinstance(piece.get("piece"), str) else ""


def _suggest(name, options):
    """' (did you mean X?)' for the closest option, if one's close."""
    import difflib
    close = difflib.get_close_matches(name, [o for o in options if o != "piece"], n=1, cutoff=0.75)
    return f" (did you mean '{close[0]}'?)" if close else ""


_schema = None


def schema():
    """The schema next to this package (loaded once)."""
    global _schema
    if _schema is None:
        if not os.path.exists(SCHEMA_PATH):
            raise SpecError(f"There's no schema at '{SCHEMA_PATH}': write it from Unity (Tools > Character Showcase > Level > "
                            "Specs > Write Spec Schema).")
        _schema = Schema()
    return _schema
