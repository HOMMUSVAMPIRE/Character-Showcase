#!/usr/bin/env python3
"""
Fast checks that catch the mistakes agents most often make in a Unity project, without needing Unity.
Run from the repository root:  python3 Tools/CI/check_project_hygiene.py
Exits non-zero and prints every problem found.

Checks:
  1. Every file and folder under Assets/ has a .meta, and every .meta has its asset (no orphans).
  2. No two .meta files share a GUID (copy-pasted .meta files break references silently).
  3. The CameraSystem assembly stays independent of the character code.
  4. Every serialized field the tests read or set by name (rig.MovementSetting<T>("name"),
     rig.SetMovementSetting("name", value)) still exists.
"""
import json
import os
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "Assets"
problems = []


def check_meta_files():
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        here = Path(dirpath)
        # Unity ignores hidden entries and names ending in "~"; they get no .meta.
        dirnames[:] = [d for d in dirnames if not d.startswith(".") and not d.endswith("~")]
        entries = [d for d in dirnames] + [f for f in filenames if not f.startswith(".")]
        names = set(entries)
        for name in entries:
            if name.endswith(".meta"):
                if name[: -len(".meta")] not in names:
                    problems.append(f"Orphan .meta (its asset is gone): {(here / name).relative_to(ROOT)}")
            elif f"{name}.meta" not in names:
                problems.append(f"Missing .meta: {(here / name).relative_to(ROOT)}")


def check_guids():
    seen = {}
    for meta in ASSETS.rglob("*.meta"):
        match = re.search(r"^guid:\s*([0-9a-f]{32})\s*$", meta.read_text(errors="replace"), re.M)
        if not match:
            problems.append(f"No GUID in {meta.relative_to(ROOT)}")
            continue
        guid = match.group(1)
        if guid in seen:
            problems.append(f"Duplicate GUID {guid}: {seen[guid]} and {meta.relative_to(ROOT)}")
        else:
            seen[guid] = meta.relative_to(ROOT)


def check_camera_independence():
    camera_dir = ASSETS / "Scripts" / "CameraSystem"
    asmdef = camera_dir / "CameraSystem.asmdef"
    if not asmdef.exists():
        problems.append("Assets/Scripts/CameraSystem/CameraSystem.asmdef is missing")
        return
    refs = json.loads(asmdef.read_text()).get("references", [])
    for ref in refs:
        if "CharacterShowcase" in ref or ref.startswith("GUID:"):
            problems.append(f"CameraSystem.asmdef must not reference the character code (found '{ref}')")
    for source in camera_dir.rglob("*.cs"):
        text = source.read_text(errors="replace")
        if re.search(r"^\s*using\s+(static\s+)?(Character|Level|CharacterShowcase)\b", text, re.M):
            problems.append(f"{source.relative_to(ROOT)} uses a character namespace; CameraSystem must stay independent")


def movement_setting_sources():
    # BasicMovementMode is a partial class: its fields are spread over BasicMovementMode.cs and
    # BasicMovementMode.<Ability>.cs. Its components (MovementModes/Components) hold the rest, and the base
    # classes (MovementComponent, ComposedMovementMode) a few more.
    modes = ASSETS / "Scripts" / "MovementModes"
    return (sorted(modes.glob("BasicMovementMode*.cs")) + sorted((modes / "Components").glob("*.cs"))
            + [modes / "MovementComponent.cs", modes / "ComposedMovementMode.cs"])


def check_reflected_settings():
    sources = movement_setting_sources()
    source = "\n".join(p.read_text(errors="replace") for p in sources)
    for test in (ASSETS / "Tests").rglob("*.cs"):
        for name in set(re.findall(r'MovementSetting(?:<\w+>)?\("(\w+)"', test.read_text(errors="replace"))):
            # A field declaration: "<type> name;" or "<type> name = ..."
            if not re.search(rf"\b\w[\w<>\[\],]*\s+{name}\s*(=|;)", source):
                problems.append(
                    f"{test.relative_to(ROOT)} reads or sets movement setting '{name}' by reflection, but no such "
                    f"field is declared in BasicMovementMode (or its BasicMovementMode.*.cs parts) or its components"
                )


def check_unique_movement_settings():
    # The tests (CharacterTestRig.MovementSetting) and MovementModeMigration find a setting by its name alone, across
    # the mode and its components, so no two may share one.
    declared = {}
    for path in movement_setting_sources():
        if path.name == "MovementComponent.cs":
            continue  # Its fields (enabled) are every component's.
        for name in re.findall(r"\[SerializeField\][^;]*?\b(\w+)\s*(?:=[^;]*)?;", path.read_text(errors="replace")):
            if name in declared and declared[name] != path:
                problems.append(
                    f"Movement setting '{name}' is declared in both {declared[name].relative_to(ROOT)} and "
                    f"{path.relative_to(ROOT)}: setting names must be unique across a mode and its components"
                )
            declared.setdefault(name, path)


def main():
    if not ASSETS.is_dir():
        print(f"No Assets folder at {ASSETS}; run this from the repository.", file=sys.stderr)
        return 2
    check_meta_files()
    check_guids()
    check_camera_independence()
    check_reflected_settings()
    check_unique_movement_settings()
    if problems:
        print(f"{len(problems)} problem(s) found:")
        for problem in problems:
            print(f"  - {problem}")
        return 1
    print("Project hygiene checks passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
