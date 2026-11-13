"""Level specs as Python values: dicts and lists as in their JSON, normalized, and written as Unity writes them."""

import json

from .schema import schema, FORMAT, VERSION

#: Pieces that make a group other pieces can be put in (by its path).
GROUP_PIECES = ("Group", "Placed", "Turned")


class PathCounter:
    """How many objects of each name have been made under each parent, to give a group of a name already used its
    "#n" path (see LevelSpec.PathOf). Only pieces with a "name" are counted: an approximation, as some pieces name what
    they make otherwise, which only matters for groups sharing a name with something else in the same parent."""

    def __init__(self):
        self.counts = {}

    def add(self, parent, name):
        key = (parent, name)
        self.counts[key] = self.counts.get(key, 0) + 1
        return self.counts[key]


def child_path(counter, parent, name):
    """The path of a new child named name under parent, counting it."""
    n = counter.add(parent, name)
    segment = name if n == 1 else f"{name}#{n}"
    return segment if parent == "" else f"{parent}/{segment}"


def _tidy(value):
    """Whole floats as ints, recursively, so specs read (and compare) the same however they were written."""
    if isinstance(value, float) and value.is_integer() and abs(value) < 1e15:
        return int(value)
    if isinstance(value, list):
        return [_tidy(v) for v in value]
    if isinstance(value, dict):
        return {k: _tidy(v) for k, v in value.items()}
    return value


def normalize(spec):
    """A spec put in order: format and version filled in, the top-level keys in the schema's order, each piece's settings
    in its parameters' order (unknown ones last, for validation to report), parents left out set to the root, and whole
    numbers as ints. Doesn't validate."""
    sc = schema()
    out = {"format": spec.get("format", FORMAT), "version": spec.get("version", VERSION)}
    for key in ("name", "scene", "parts"):
        if key in spec:
            out[key] = spec[key]
    for key in spec:
        if key not in out:
            out[key] = spec[key]
    parts = out.get("parts")
    if isinstance(parts, list):
        new_parts = []
        for part in parts:
            if not isinstance(part, dict):
                new_parts.append(part)
                continue
            new_part = dict(part)
            pieces = part.get("pieces")
            if isinstance(pieces, list):
                new_part["pieces"] = [_normalize_piece(p, sc) for p in pieces]
            new_parts.append(new_part)
        out["parts"] = new_parts
    return _tidy(out)


def _normalize_piece(piece, sc):
    if not isinstance(piece, dict) or piece.get("piece") not in sc.pieces:
        return piece
    name = piece["piece"]
    settings = sc.settings(name)
    out = {"piece": name}
    required = sc.required(name)
    for key, s in settings.items():
        if key in piece:
            out[key] = piece[key]
        elif key in required and sc.kind(s) == "path":
            out[key] = ""
    for key in piece:
        if key not in out:
            out[key] = piece[key]
    return out


def to_json(spec):
    """The spec as JSON text, laid out as Unity writes it: one line per piece."""
    def one(value):
        return json.dumps(_tidy(value), ensure_ascii=False, separators=(", ", ": "))

    lines = ["{"]
    for key in ("format", "version", "name", "scene"):
        if key in spec:
            lines.append(f"  {json.dumps(key)}: {one(spec[key])},")
    parts = spec.get("parts", [])
    if not parts:
        lines.append('  "parts": []')
    else:
        lines.append('  "parts": [')
        for p, part in enumerate(parts):
            lines.append("    {")
            pieces = part.get("pieces", [])
            lines.append(f'      "name": {one(part["name"])},')
            if not pieces:
                lines.append('      "pieces": []')
            else:
                lines.append('      "pieces": [')
                lines += [f"        {one(piece)}{',' if i < len(pieces) - 1 else ''}" for i, piece in enumerate(pieces)]
                lines.append("      ]")
            lines.append("    }" + ("," if p < len(parts) - 1 else ""))
        lines.append("  ]")
    lines.append("}")
    return "\n".join(lines) + "\n"


def pieces(spec):
    """Every piece in the spec, in order, with where it is: (part index, piece index, part name, piece)."""
    for p, part in enumerate(spec.get("parts", [])):
        for i, piece in enumerate(part.get("pieces", [])):
            yield p, i, part.get("name", ""), piece
