"""Reading and writing level specs as JSON, XML and CSV.

JSON is the spec itself (what Unity builds from). XML and CSV are for writing levels by hand or by other tools; their
values are text, read by what each setting is (see `parse_value`):

- numbers, whole numbers and booleans (true/false, yes/no, 1/0) as themselves;
- points and areas as their numbers, separated by commas or spaces: "136, 23" or "0 4 0";
- lists with ';' between items: "18, 7.5; 7.5, 18" (a list of numbers can use commas: "0, 4, 8");
- a [point, number] item as its numbers in order: "16, 0, 6";
- a random as its seed: "120384022";
- anything as JSON, if it starts with '[' or '{' (cells, in CSV, are written this way).

XML::

    <level name="Gap Test" scene="Assets/Scenes/GapTest/GapTest.unity">
      <part name="Run">
        <Group name="Run">                          <!-- pieces inside a group default to it as their parent -->
          <Platform name="A" x0="0" x1="12" z0="0" z1="16" y0="0" top="4" coping="Accent"/>
        </Group>
        <State stepRise="0.25"/>
        <Cells openTop="-1">
          <cells><cell name="Hall" x0="0" z0="0" x1="20" z1="10" floor="0" ceiling="6"/></cells>   <!-- lists of objects as elements -->
          <portals>-4, 0, 0, 1</portals>            <!-- or any setting as text inside an element named for it -->
        </Cells>
      </part>
    </level>

CSV: a header row "part, piece, then any settings", one row a piece (an empty cell leaves that setting out; an empty
part carries on the part above), and a row with piece "Level" giving the level's name and scene (in a "scene" column),
or give them when converting.

A parent left out is the group the piece is in (XML) or the level's root.
"""

import csv
import io
import json
import os
import re
import xml.parsers.expat
from xml.sax.saxutils import quoteattr, escape

from .model import normalize, to_json, GROUP_PIECES, child_path, PathCounter
from .schema import SpecError, schema, FORMAT, VERSION

FORMATS = ("json", "xml", "csv")


def format_of(path, fmt=None):
    """The format to read or write path in: fmt if given, else its extension's."""
    fmt = (fmt or os.path.splitext(path)[1].lstrip(".")).lower()
    if fmt not in FORMATS:
        raise SpecError(f"Can't tell what format '{path}' is: give it a .json, .xml or .csv name, or say which.")
    return fmt


def read(path, fmt=None, name=None, scene=None):
    """Reads a level spec from a JSON, XML or CSV file, normalized (see model.normalize) but not yet validated.
    name and scene fill in the level's name and scene where the file doesn't give them."""
    with open(path, encoding="utf-8-sig", newline="") as f:
        text = f.read()
    return loads(text, format_of(path, fmt), name=name, scene=scene, source=os.path.basename(path))


def loads(text, fmt, name=None, scene=None, source="the spec"):
    """Reads a level spec from text in a format ("json", "xml" or "csv")."""
    if fmt == "json":
        try:
            spec = json.loads(text)
        except json.JSONDecodeError as e:
            raise SpecError(f"{source}: bad JSON at line {e.lineno}, column {e.colno}: {e.msg}.")
        if not isinstance(spec, dict):
            raise SpecError(f"{source}: a level spec is a JSON object.")
    elif fmt == "xml":
        spec = _read_xml(text, source)
    elif fmt == "csv":
        spec = _read_csv(text, source)
    else:
        raise SpecError(f"There's no format '{fmt}': {', '.join(FORMATS)}.")
    if name and not spec.get("name"):
        spec["name"] = name
    if scene and not spec.get("scene"):
        spec["scene"] = scene
    return normalize(spec)


def write(spec, path, fmt=None):
    """Writes a level spec to a file in its extension's format (or fmt)."""
    text = dumps(spec, format_of(path, fmt))
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def dumps(spec, fmt):
    """A level spec as text in a format."""
    if fmt == "json":
        return to_json(spec)
    if fmt == "xml":
        return _write_xml(spec)
    if fmt == "csv":
        return _write_csv(spec)
    raise SpecError(f"There's no format '{fmt}': {', '.join(FORMATS)}.")


# ---- Values as text ----

def number_text(v):
    """A number as the shortest text that reads back as it (whole numbers without a point)."""
    if isinstance(v, bool):
        return "true" if v else "false"
    if isinstance(v, float) and v.is_integer() and abs(v) < 1e15:
        return str(int(v))
    return repr(v) if isinstance(v, float) else str(v)


def _numbers(text, where):
    parts = [p for p in re.split(r"[,\s]+", text.strip().strip("()")) if p]
    try:
        return [_number(p) for p in parts]
    except ValueError:
        raise SpecError(f"{where}: '{text}' should be numbers, separated by commas or spaces.")


def _number(text):
    v = float(text)
    return int(v) if v.is_integer() and "." not in text and "e" not in text.lower() else v


def _flat_count(s, kind):
    return {"point2": 2, "point3": 3, "area": 4}.get(kind, 1)


def parse_value(text, setting, where):
    """A setting's value from text, read by what the setting is (its schema)."""
    sc = schema()
    t = text.strip()
    if t.startswith("[") or t.startswith("{"):
        try:
            return json.loads(t)
        except json.JSONDecodeError as e:
            raise SpecError(f"{where}: bad JSON '{t}': {e.msg}.")
    kind = sc.kind(setting)
    if kind in ("path", "string", "enum"):
        return text if kind == "path" else t
    if kind == "number":
        try:
            return _number(t)
        except ValueError:
            raise SpecError(f"{where}: '{t}' isn't a number.")
    if kind == "integer":
        try:
            return int(float(t))
        except ValueError:
            raise SpecError(f"{where}: '{t}' isn't a whole number.")
    if kind == "boolean":
        low = t.lower()
        if low in ("true", "yes", "1"):
            return True
        if low in ("false", "no", "0"):
            return False
        raise SpecError(f"{where}: '{t}' isn't true or false.")
    if kind in ("point2", "point3", "area"):
        numbers = _numbers(t, where)
        if len(numbers) != _flat_count(None, kind):
            raise SpecError(f"{where}: '{t}' should be {sc.describe(setting)}.")
        return numbers
    if kind == "random":
        try:
            return {"seed": int(t)}
        except ValueError:
            raise SpecError(f"{where}: a random is its seed, a whole number ('{t}').")
    resolved = sc.resolve(setting)
    if kind == "tuple":
        numbers = _numbers(t, where)
        out, i = [], 0
        for item in resolved["prefixItems"]:
            n = _flat_count(None, sc.kind(item))
            out.append(numbers[i:i + n] if n > 1 else (numbers[i] if i < len(numbers) else None))
            i += n
        if i != len(numbers):
            raise SpecError(f"{where}: '{t}' should be {i} numbers ({sc.describe(setting)}).")
        return out
    if kind == "list":
        item = resolved["items"]
        if not t:
            return []
        if sc.kind(item) in ("number", "integer") and ";" not in t:
            return _numbers(t, where)
        return [parse_value(p, item, where) for p in t.split(";") if p.strip()]
    raise SpecError(f"{where}: give it as JSON ({sc.describe(setting)}).")


def value_text(value, setting):
    """A setting's value as text (the inverse of parse_value); None if it's only expressible as JSON."""
    sc = schema()
    kind = sc.kind(setting)
    if value is None:
        return ""
    if kind in ("point2", "point3", "area"):
        return ", ".join(number_text(v) for v in value)
    if kind == "random" and isinstance(value, dict) and set(value) == {"seed"}:
        return str(value["seed"])
    if kind == "tuple":
        flat = []
        for v in value:
            flat += v if isinstance(v, list) else [v]
        return ", ".join(number_text(v) for v in flat)
    if kind == "list":
        item = sc.resolve(setting)["items"]
        if not value:
            return "[]"  # An empty cell or attribute leaves a setting out; an empty list is said.
        if sc.kind(item) in ("number", "integer"):
            return ", ".join(number_text(v) for v in value)
        texts = [value_text(v, item) for v in value]
        return None if any(x is None for x in texts) else "; ".join(texts)
    if isinstance(value, (dict, list)):
        return None
    if isinstance(value, (int, float)):
        return number_text(value)
    return str(value)


# ---- XML ----

class _Element:
    __slots__ = ("tag", "attrs", "children", "text", "line")

    def __init__(self, tag, attrs, line):
        self.tag, self.attrs, self.children, self.text, self.line = tag, attrs, [], "", line


def _parse_xml(text, source):
    """Parses XML keeping each element's line (ElementTree doesn't)."""
    parser = xml.parsers.expat.ParserCreate()
    stack, root = [], []

    def start(tag, attrs):
        e = _Element(tag, attrs, parser.CurrentLineNumber)
        (stack[-1].children if stack else root).append(e)
        stack.append(e)

    def end(tag):
        stack.pop()

    def data(d):
        if stack:
            stack[-1].text += d

    parser.StartElementHandler, parser.EndElementHandler, parser.CharacterDataHandler = start, end, data
    try:
        parser.Parse(text, True)
    except xml.parsers.expat.ExpatError as e:
        raise SpecError(f"{source}: bad XML at line {e.lineno}: {xml.parsers.expat.ErrorString(e.code)}.")
    return root[0]


def _read_xml(text, source):
    sc = schema()
    root = _parse_xml(text, source)
    if root.tag != "level":
        raise SpecError(f"{source}: the root element should be <level>, not <{root.tag}>.")
    spec = {"format": FORMAT, "version": VERSION}
    for key in ("name", "scene"):
        if key in root.attrs:
            spec[key] = root.attrs[key]
    if "version" in root.attrs:
        spec["version"] = int(root.attrs["version"])
    spec["parts"] = []
    counter = PathCounter()
    errors = []

    def piece_from(e, context, out):
        where = f"{source} line {e.line} <{e.tag}>"
        if e.tag not in sc.pieces:
            errors.append(f"{where}: there's no piece '{e.tag}'.")
            return
        settings = sc.settings(e.tag)
        piece = {"piece": e.tag}
        for key, text in e.attrs.items():
            if key not in settings:
                piece[key] = text  # Reported by validation, with the piece's settings.
                continue
            if text == "" and sc.kind(settings[key]) != "path":
                continue
            try:
                piece[key] = parse_value(text, settings[key], f"{where} '{key}'")
            except SpecError as err:
                errors.extend(err.errors)
        nested = []
        for child in e.children:
            if child.tag in settings and child.tag not in sc.pieces:
                s = settings[child.tag]
                if child.children:
                    item = sc.resolve(s).get("items", s)
                    values = []
                    for c in child.children:
                        obj = {}
                        props = sc.resolve(item).get("properties", {})
                        for k, v in c.attrs.items():
                            try:
                                obj[k] = parse_value(v, props[k], f"{source} line {c.line} '{k}'") if k in props else v
                            except SpecError as err:
                                errors.extend(err.errors)
                        values.append(obj)
                    piece[child.tag] = values if sc.kind(s) == "list" else values[0]
                else:
                    try:
                        piece[child.tag] = parse_value(child.text, s, f"{source} line {child.line} <{child.tag}>")
                    except SpecError as err:
                        errors.extend(err.errors)
            else:
                nested.append(child)
        # The parent (and any other parent settings) default to the group this piece is in.
        for key, s in settings.items():
            if sc.kind(s) == "path" and key not in piece and key in sc.required(e.tag):
                piece[key] = context
        out.append(piece)
        inner = context
        if e.tag in GROUP_PIECES and "name" in piece:
            inner = child_path(counter, piece.get("parent", context), piece["name"])
        elif "name" in piece and isinstance(piece.get("parent"), str):
            counter.add(piece["parent"], piece["name"])
        if nested and e.tag not in GROUP_PIECES:
            errors.append(f"{where}: only groups (Group, Placed, Turned) can hold pieces; <{nested[0].tag}> isn't a setting of '{e.tag}'.")
        for child in nested:
            piece_from(child, inner, out)

    for part_el in root.children:
        if part_el.tag != "part":
            errors.append(f"{source} line {part_el.line}: <level> holds <part> elements, not <{part_el.tag}>.")
            continue
        part = {"name": part_el.attrs.get("name", ""), "pieces": []}
        if not part["name"]:
            errors.append(f"{source} line {part_el.line}: a <part> needs a name.")
        for e in part_el.children:
            piece_from(e, "", part["pieces"])
        spec["parts"].append(part)
    if errors:
        raise SpecError(errors)
    return spec


def _write_xml(spec):
    sc = schema()
    out = ['<?xml version="1.0" encoding="utf-8"?>',
           f'<level name={quoteattr(spec.get("name", ""))} scene={quoteattr(spec.get("scene", ""))} version="{spec.get("version", VERSION)}">']
    for part in spec.get("parts", []):
        out.append(f'  <part name={quoteattr(part["name"])}>')
        for piece in part.get("pieces", []):
            settings = sc.settings(piece["piece"])
            attrs, children = [], []
            for key, value in piece.items():
                if key == "piece":
                    continue
                setting = settings.get(key, {})
                text = value_text(value, setting) if setting else json.dumps(value)
                if text is None:
                    # Objects: as elements, one per item, their fields attributes.
                    items = value if isinstance(value, list) else [value]
                    inner = "".join(f"<{'cell' if sc.kind(setting) in ('list', 'cell') else 'item'} " + " ".join(
                        f"{k}={quoteattr(number_text(v) if not isinstance(v, str) else v)}" for k, v in item.items()) + "/>"
                        for item in items)
                    children.append(f"<{key}>{inner}</{key}>")
                else:
                    attrs.append(f"{key}={quoteattr(text)}")
            head = f'    <{piece["piece"]}' + ("" if not attrs else " " + " ".join(attrs))
            out.append(head + ("/>" if not children else ">" + "".join(children) + f'</{piece["piece"]}>'))
        out.append("  </part>")
    out.append("</level>")
    return "\n".join(out) + "\n"


# ---- CSV ----

def _read_csv(text, source):
    sc = schema()
    rows = list(csv.reader(io.StringIO(text)))
    rows = [r for r in rows if any(c.strip() for c in r) and not r[0].lstrip().startswith("#")]
    if not rows:
        raise SpecError(f"{source}: there's nothing in it.")
    header = [h.strip() for h in rows[0]]
    if "piece" not in header:
        raise SpecError(f"{source}: the first row should name the columns, with a 'piece' column (and 'part').")
    spec = {"format": FORMAT, "version": VERSION, "parts": []}
    parts = {}
    part_name = None
    errors = []
    for n, row in enumerate(rows[1:], 2):
        cells = {header[i]: row[i] for i in range(min(len(header), len(row)))}
        piece_name = cells.get("piece", "").strip()
        where = f"{source} row {n}"
        if piece_name == "Level":
            for key in ("name", "scene"):
                if cells.get(key, "").strip():
                    spec[key] = cells[key].strip()
            continue
        if cells.get("part", "").strip():
            part_name = cells["part"].strip()
        if part_name is None:
            errors.append(f"{where}: say which part the piece is in (the 'part' column).")
            continue
        if part_name not in parts:
            parts[part_name] = {"name": part_name, "pieces": []}
            spec["parts"].append(parts[part_name])
        if not piece_name:
            errors.append(f"{where}: say which piece it is (the 'piece' column).")
            continue
        settings = sc.settings(piece_name)
        piece = {"piece": piece_name}
        for key, text in cells.items():
            if key in ("part", "piece") or (key == "scene" and key not in settings):
                continue
            if text.strip() == "" and not (key in settings and sc.kind(settings[key]) == "path" and key in sc.required(piece_name)):
                continue
            if key not in settings:
                piece[key] = text  # Reported by validation.
                continue
            try:
                piece[key] = parse_value(text, settings[key], f"{where} '{key}'")
            except SpecError as err:
                errors.extend(err.errors)
        parts[part_name]["pieces"].append(piece)
    if errors:
        raise SpecError(errors)
    return spec


def _write_csv(spec):
    sc = schema()
    columns = ["part", "piece", "parent", "name"]
    for part in spec.get("parts", []):
        for piece in part.get("pieces", []):
            for key in piece:
                if key != "piece" and key not in columns:
                    columns.append(key)
    if "scene" not in columns:
        columns.append("scene")
    buffer = io.StringIO()
    w = csv.writer(buffer, lineterminator="\n")
    w.writerow(columns)
    w.writerow(["", "Level"] + [spec.get("name", "") if c == "name" else spec.get("scene", "") if c == "scene" else "" for c in columns[2:]])
    for part in spec.get("parts", []):
        for piece in part.get("pieces", []):
            settings = sc.settings(piece["piece"])
            row = [part["name"], piece["piece"]]
            for c in columns[2:]:
                if c not in piece:
                    row.append("")
                    continue
                text = value_text(piece[c], settings[c]) if c in settings else None
                row.append(text if text is not None else json.dumps(piece[c], ensure_ascii=False))
            w.writerow(row)
    return buffer.getvalue()
