"""The command line: python3 Tools/LevelSpec/levelspec <command> ... (run with --help for each command)."""

import argparse
import os
import platform
import re
import subprocess
import sys

from . import formats
from .checks import check, load_envelope, ENVELOPE_PATH
from .schema import SpecError, schema
from .schematic import write_schematic

PROJECT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))


def _show(path):
    """A path to print: relative to here if it's under here, else as it is."""
    full = os.path.abspath(path)
    return os.path.relpath(full) if full.startswith(os.getcwd() + os.sep) else full


def _read(args):
    spec = formats.read(args.input, getattr(args, "format", None), name=getattr(args, "name", None), scene=getattr(args, "scene", None))
    errors = schema().validate(spec)
    if errors:
        raise SpecError(errors)
    return spec


def _print_findings(findings, quiet_info=False):
    for f in findings:
        if quiet_info and f.severity == "info":
            continue
        print(f"  {f.severity:7}  {f.kind:9}  {f.text}")


def cmd_validate(args):
    ok = True
    for path in args.inputs:
        try:
            spec = formats.read(path, args.format)
            errors = schema().validate(spec)
        except SpecError as e:
            errors = e.errors
        if errors:
            ok = False
            print(f"{path}: {len(errors)} problem{'s' if len(errors) != 1 else ''}")
            for e in errors:
                print(f"  {e}")
        else:
            n = sum(len(p.get("pieces", [])) for p in spec["parts"])
            print(f"{path}: a valid level spec ('{spec['name']}', {len(spec['parts'])} parts, {n} pieces).")
    return 0 if ok else 1


def cmd_check(args):
    spec = _read(args)
    findings = check(spec, load_envelope(args.envelope))
    warnings = [f for f in findings if f.severity == "warning"]
    print(f"{spec['name']}: {len(findings)} findings, {len(warnings)} warning{'s' if len(warnings) != 1 else ''} "
          f"(against {_show(args.envelope)}).")
    _print_findings(findings, args.warnings_only)
    return 1 if warnings and args.strict else 0


def cmd_convert(args):
    spec = _read(args)
    formats.write(spec, args.output, args.to)
    print(f"Wrote '{args.output}'.")
    return 0


def cmd_schematic(args):
    spec = _read(args)
    output = args.output or os.path.splitext(args.input)[0] + ".schematic.html"
    findings = write_schematic(spec, output, envelope=load_envelope(args.envelope))
    print(f"Wrote the schematic of '{spec['name']}' to '{output}' ({len(findings)} movement checks).")
    return 0


def default_spec_path(spec):
    """Where a spec is kept in the project: Assets/Levels/[scene file name].level.json (as Unity exports them)."""
    scene = os.path.splitext(os.path.basename(spec["scene"]))[0]
    return os.path.join(PROJECT, "Assets", "Levels", f"{scene}.level.json")


def cmd_build(args):
    spec = _read(args)
    output = args.output or default_spec_path(spec)
    formats.write(spec, output, "json")
    print(f"Wrote the level spec '{_show(output)}'.")
    findings = check(spec, load_envelope(args.envelope))
    schematic = args.schematic or os.path.splitext(os.path.splitext(output)[0])[0] + ".schematic.html"
    if args.schematic != "":
        if os.path.abspath(schematic).startswith(os.path.join(PROJECT, "Assets") + os.sep) and not args.schematic:
            # Kept out of Assets, where Unity would import it.
            schematic = os.path.join(PROJECT, "Schematics", os.path.basename(schematic))
        write_schematic(spec, schematic, findings=findings)
        print(f"Wrote its schematic '{_show(schematic)}'.")
    warnings = [f for f in findings if f.severity == "warning"]
    print(f"Movement checks: {len(findings)}, {len(warnings)} warning{'s' if len(warnings) != 1 else ''}.")
    _print_findings(findings, quiet_info=True)
    if args.unity:
        return run_unity(output, spec, args)
    print(f"Build it in Unity with Tools > Character Showcase > Level > Specs > Build Level from Spec…, or run this again with --unity.")
    return 0


def unity_editor(path=None):
    """The Unity editor to run: path, $UNITY_PATH, or the project's version installed by Unity Hub."""
    if path:
        return path
    if os.environ.get("UNITY_PATH"):
        return os.environ["UNITY_PATH"]
    with open(os.path.join(PROJECT, "ProjectSettings", "ProjectVersion.txt"), encoding="utf-8") as f:
        version = re.search(r"m_EditorVersion:\s*(\S+)", f.read()).group(1)
    system = platform.system()
    if system == "Darwin":
        candidate = f"/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity"
    elif system == "Windows":
        candidate = rf"C:\Program Files\Unity\Hub\Editor\{version}\Editor\Unity.exe"
    else:
        candidate = os.path.expanduser(f"~/Unity/Hub/Editor/{version}/Editor/Unity")
    if not os.path.exists(candidate):
        raise SpecError(f"Couldn't find Unity {version} at '{candidate}': give its path with --unity-path or $UNITY_PATH.")
    return candidate


def _run(editor, method, extra, log):
    cmd = [editor, "-batchmode", "-quit", "-projectPath", PROJECT, "-executeMethod", method] + extra + ["-logFile", log]
    print(f"Running Unity: {method} (log: {_show(log)}) ...")
    code = subprocess.call(cmd)
    with open(log, encoding="utf-8", errors="replace") as f:
        for line in f:
            if line.startswith("[") and ("build]" in line or "[Level specs]" in line or "[SceneGameSetup]" in line):
                print("  " + line.rstrip())
            elif "Exception" in line or "error CS" in line:
                print("  " + line.rstrip())
    return code


def run_unity(spec_path, spec, args):
    """Builds the spec's scene in Unity (closed: batch mode), then sets it up for play unless asked not to."""
    editor = unity_editor(args.unity_path)
    logs = os.path.join(PROJECT, "Logs")
    os.makedirs(logs, exist_ok=True)
    rel = os.path.relpath(spec_path, PROJECT)
    code = _run(editor, "CharacterShowcase.EditorTools.Level.LevelSpecTools.BuildFromCommandLine", ["-spec", rel],
                os.path.join(logs, "levelspec-build.log"))
    if code != 0:
        print(f"Unity exited with {code}: see the log. (Is the project open in Unity? Close it first.)")
        return code
    if not args.no_set_up:
        code = _run(editor, "CharacterShowcase.EditorTools.SceneGameSetup.SetUpFromCommandLine", ["-scene", spec["scene"]],
                    os.path.join(logs, "levelspec-setup.log"))
    return code


def cmd_pieces(args):
    sc = schema()
    names = [args.piece] if args.piece else sorted(sc.pieces, key=str.lower)
    for name in names:
        if name not in sc.pieces:
            print(f"There's no piece '{name}'.")
            return 1
        required = sc.required(name)
        settings = sc.settings(name)
        if not args.piece:
            print(f"{name}({', '.join(k if k in required else k + '?' for k in settings)})")
            continue
        print(name)
        for key, s in settings.items():
            default = sc.resolve(s).get("default", s.get("default"))
            note = "required" if key in required else f"default {default}" if "default" in s else "optional"
            print(f"  {key:16} {sc.describe(s):42} {note}")
    if not args.piece:
        print("\n? = optional. Details: pieces <name>.")
    return 0


def main(argv=None):
    p = argparse.ArgumentParser(prog="levelspec", description="Read, check, convert and draw level specs, and have Unity build them.")
    sub = p.add_subparsers(dest="command", required=True)

    def reader(sp, many=False):
        if many:
            sp.add_argument("inputs", nargs="+", help="spec files (.json, .xml or .csv)")
        else:
            sp.add_argument("input", help="a spec file (.json, .xml or .csv)")
        sp.add_argument("--format", choices=formats.FORMATS, help="the input's format, if its extension doesn't say")
        if not many:
            sp.add_argument("--name", help="the level's name, if the file doesn't give it")
            sp.add_argument("--scene", help="the scene to build it into (Assets/....unity), if the file doesn't give it")
            sp.add_argument("--envelope", default=ENVELOPE_PATH, help="what the character can do, for the movement checks (envelope.json)")

    sp = sub.add_parser("validate", help="check specs against the schema")
    reader(sp, many=True)
    sp.set_defaults(run=cmd_validate)

    sp = sub.add_parser("check", help="check a level against what the character can do")
    reader(sp)
    sp.add_argument("--warnings-only", action="store_true", help="list only warnings")
    sp.add_argument("--strict", action="store_true", help="exit with 1 if there are warnings")
    sp.set_defaults(run=cmd_check)

    sp = sub.add_parser("convert", help="convert a spec to another format")
    reader(sp)
    sp.add_argument("output", help="the file to write (.json, .xml or .csv)")
    sp.add_argument("--to", choices=formats.FORMATS, help="the output's format, if its extension doesn't say")
    sp.set_defaults(run=cmd_convert)

    sp = sub.add_parser("schematic", help="draw a spec's schematic (HTML, or SVG for a .svg output)")
    reader(sp)
    sp.add_argument("output", nargs="?", help="the file to write (default: beside the input, .schematic.html)")
    sp.set_defaults(run=cmd_schematic)

    sp = sub.add_parser("build", help="write a spec for Unity (Assets/Levels) and its schematic, and optionally build it")
    reader(sp)
    sp.add_argument("-o", "--output", help="the spec to write (default: Assets/Levels/[scene].level.json)")
    sp.add_argument("--schematic", help="the schematic to write (default: Schematics/[scene].schematic.html; '' for none)")
    sp.add_argument("--unity", action="store_true", help="then build the scene in Unity (which must be closed) and set it up for play")
    sp.add_argument("--no-set-up", action="store_true", help="with --unity: don't run Set Up Scene after building")
    sp.add_argument("--unity-path", help="the Unity editor to run (default: $UNITY_PATH, or the project's version from Unity Hub)")
    sp.set_defaults(run=cmd_build)

    sp = sub.add_parser("pieces", help="list the pieces a spec can use, or one piece's settings")
    sp.add_argument("piece", nargs="?")
    sp.set_defaults(run=cmd_pieces)

    args = p.parse_args(argv)
    try:
        return args.run(args)
    except SpecError as e:
        print(f"{len(e.errors)} problem{'s' if len(e.errors) != 1 else ''}:" if len(e.errors) > 1 else "Problem:", file=sys.stderr)
        for err in e.errors:
            print(f"  {err}", file=sys.stderr)
        return 1
    except OSError as e:
        print(f"{e.filename or ''}: {e.strerror}", file=sys.stderr)
        return 1
