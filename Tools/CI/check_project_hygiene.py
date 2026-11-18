#!/usr/bin/env python3
"""
Fast checks that catch the mistakes agents most often make in a Unity project, without needing Unity.
Run from the repository root:  python3 Tools/CI/check_project_hygiene.py
Exits non-zero and prints every problem found.

Checks:
  1. Every file and folder under Assets/ has a .meta, and every .meta has its asset (no orphans).
  2. No two .meta files share a GUID (copy-pasted .meta files break references silently).
  3. Every assembly references only what AGENTS.md's assembly table allows it (ALLOWED_REFERENCES), and no
     assembly is missing from the table.
  4. The camera uses no character or level code, and the level elements and the level builders no character code:
     no `using Character` or the like (the level builders may use the level elements, `Level`).
  5. Every serialized field the tests read or set by name (rig.MovementSetting<T>("name"),
     rig.SetMovementSetting("name", value), an ability fixture's OffSetting => "name") still exists, and is of
     the type the test asks for it as (<T>).
  6. No two movement settings share a name (the tests find a setting by its name alone).
  7. Every test fixture has [TestTiming] and a test area ([Category(TestArea.…)]), itself or through a base class (an
     abstract base, such as AbilityFixture<T>, is checked through the fixtures deriving it).
  8. Every movement component a movement asset names ([SerializeReference]: "type: {class: X, ns: Y, asm: Z}")
     still exists by that name, or names it in a [MovedFrom]: renamed without one, it drops out of the asset.
  9. Every member of the flag enums abilities take bits of (CharacterState, MovementEvents) is a single bit of its own,
     within the enum's width: no two share one.
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


# What each assembly may reference, as AGENTS.md's assembly table says (keep the two in step). The test assemblies also
# reference Unity's test runner, which every test assembly needs.
TEST_RUNNER = {"UnityEngine.TestRunner", "UnityEditor.TestRunner"}
ALLOWED_REFERENCES = {
    "PhysicsQueries": set(),
    "CharacterShowcase.LevelElements": {"PhysicsQueries"},
    "CharacterShowcase.LevelElements.Editor": {"CharacterShowcase.LevelElements"},
    "CharacterShowcase": {"Unity.InputSystem", "PhysicsQueries", "CharacterShowcase.LevelElements"},
    "CameraSystem": {"Unity.InputSystem", "PhysicsQueries"},
    "CharacterShowcase.Editor": {"CharacterShowcase", "CameraSystem", "CharacterShowcase.LevelElements",
                                 "CharacterShowcase.LevelElements.Editor"},
    "CharacterShowcase.Pipeline.Editor": {"CharacterShowcase", "CharacterShowcase.Editor"},
    "CharacterShowcase.LevelChecks.Editor": {"CharacterShowcase", "CharacterShowcase.Editor"},
    "CharacterShowcase.Level.Editor": {"Unity.RenderPipelines.Core.Runtime", "Unity.RenderPipelines.Universal.Runtime",
                                       "Unity.TextMeshPro", "CharacterShowcase.LevelElements",
                                       "CharacterShowcase.LevelElements.Editor"},
    "CharacterShowcase.Tests.EditMode": {"CharacterShowcase", "CharacterShowcase.Editor", "CharacterShowcase.Tests.Shared",
                                         "PhysicsQueries", "CharacterShowcase.LevelElements",
                                         "CharacterShowcase.LevelElements.Editor"} | TEST_RUNNER,
    "CharacterShowcase.Tests.EditMode.Levels": {"CharacterShowcase", "CharacterShowcase.Editor", "CharacterShowcase.Tests.Shared",
                                                "CharacterShowcase.Level.Editor", "CharacterShowcase.LevelChecks.Editor",
                                                "CharacterShowcase.LevelElements",
                                                "CharacterShowcase.LevelElements.Editor"} | TEST_RUNNER,
    "CharacterShowcase.Tests.PlayMode": {"CharacterShowcase", "CameraSystem", "CharacterShowcase.Tests.Shared",
                                         "PhysicsQueries", "CharacterShowcase.LevelElements"} | TEST_RUNNER,
    "CharacterShowcase.Tests.Shared": {"CharacterShowcase"} | TEST_RUNNER,
}


def assemblies():
    """Every assembly definition under Assets/: its name, folder and references."""
    found = []
    for asmdef in sorted(ASSETS.rglob("*.asmdef")):
        data = json.loads(asmdef.read_text())
        found.append((data.get("name", asmdef.stem), asmdef.parent, data.get("references", [])))
    return found


def check_assembly_references():
    for name, folder, refs in assemblies():
        where = (folder / f"{name}.asmdef").relative_to(ROOT) if (folder / f"{name}.asmdef").exists() else folder.relative_to(ROOT)
        if name not in ALLOWED_REFERENCES:
            problems.append(f"{where}: assembly {name} isn't in AGENTS.md's assembly table (nor ALLOWED_REFERENCES in "
                            f"{Path(__file__).relative_to(ROOT)}): add it to both")
            continue
        for ref in refs:
            if ref.startswith("GUID:"):
                problems.append(f"{where}: references an assembly by GUID ({ref}), which this check can't read: "
                                f"untick Use GUIDs in the assembly definition's inspector")
            elif ref not in ALLOWED_REFERENCES[name]:
                problems.append(f"{where}: {name} may not reference {ref} (see AGENTS.md's assembly table)")


# Folders whose code must know nothing of the character's: the namespaces they may not use. The camera uses nothing of the
# character's or the level's; the level elements (Assets/Scripts/Level) and the level builders, which equip levels with
# them, use the level elements (Level) but nothing of the character's or the camera's.
CHARACTER_AND_LEVEL = r"(Character|Level|CameraSystem|CharacterShowcase\.EditorTools)\s*;|(Character|Level)\."
CHARACTER_ONLY = r"(Character|CameraSystem|CharacterShowcase\.EditorTools)\s*;|Character\."
INDEPENDENT_FOLDERS = {
    ASSETS / "Scripts" / "CameraSystem": ("the camera follows a plain Transform", CHARACTER_AND_LEVEL),
    ASSETS / "Scripts" / "Level": ("the level elements are the level's, used by the character", CHARACTER_ONLY),
    ASSETS / "Editor" / "Level": ("the level builders make geometry, lighting and the level elements only", CHARACTER_ONLY),
}


def check_independent_folders():
    for folder, (why, forbidden) in INDEPENDENT_FOLDERS.items():
        for source in folder.rglob("*.cs"):
            text = source.read_text(errors="replace")
            for match in re.finditer(rf"^\s*using\s+(?:static\s+)?(?:\w+\s*=\s*)?(?:{forbidden})", text, re.M):
                used = match.group(0).strip()
                # A folder's own namespace is its to use (CameraSystem's camera code is in CameraSystem).
                if folder.name == "CameraSystem" and re.search(r"\bCameraSystem\b", used):
                    continue
                problems.append(f"{source.relative_to(ROOT)}: '{used}': {why}, so it uses no character code")


def movement_setting_sources():
    # BasicMovementMode is a partial class: its fields are spread over BasicMovementMode.cs and
    # BasicMovementMode.<Ability>.cs. Its components (MovementModes/Components) hold the rest, and the base
    # classes (MovementComponent, ComposedMovementMode) a few more.
    modes = ASSETS / "Scripts" / "MovementModes"
    return (sorted(modes.glob("BasicMovementMode*.cs")) + sorted((modes / "Components").glob("*.cs"))
            + [modes / "MovementComponent.cs", modes / "ComposedMovementMode.cs"])


# C#'s keyword names for types, as a field may be declared with either.
TYPE_ALIASES = {"Single": "float", "Int32": "int", "Boolean": "bool", "Double": "double", "String": "string"}


def check_reflected_settings():
    sources = movement_setting_sources()
    source = "\n".join(p.read_text(errors="replace") for p in sources)
    for test in (ASSETS / "Tests").rglob("*.cs"):
        text = test.read_text(errors="replace")
        # By the rig's MovementSetting/SetMovementSetting, and the setting an ability fixture turns its ability off by
        # (AbilityFixture.OffSetting), which the kit sets by name.
        names = set(re.findall(r'MovementSetting(?:<\w+>)?\("(\w+)"', text)) | set(re.findall(r'OffSetting\s*=>\s*"(\w+)"', text))
        for name in names:
            # A field declaration: "<type> name;" or "<type> name = ..."
            declared = re.search(rf"\b(\w[\w<>\[\],]*)\s+{name}\s*(=|;)", source)
            if not declared:
                problems.append(
                    f"{test.relative_to(ROOT)} reads or sets movement setting '{name}' by reflection, but no such "
                    f"field is declared in BasicMovementMode (or its BasicMovementMode.*.cs parts) or its components"
                )
                continue
            # Asked for as a type (MovementSetting<float>("name")), it must be the field's: as another, the rig fails
            # the test (CharacterTestRig.FindSetting). SetMovementSetting's type is the value's, which this can't see.
            field = re.search(rf"\b(?:public|private|protected|internal)\s+(?:readonly\s+)?([\w<>\[\],.]+)\s+{name}\s*(=|;)", source)
            if not field:
                continue  # Declared some other way: only its name is checked.
            field_type = TYPE_ALIASES.get(field.group(1), field.group(1))
            for asked in set(re.findall(rf'MovementSetting<(\w+)>\("{name}"', text)):
                if asked != "T" and TYPE_ALIASES.get(asked, asked) != field_type:
                    problems.append(f"{test.relative_to(ROOT)} reads movement setting '{name}' as a {asked}, but it's a "
                                    f"{field_type}")


def check_unique_movement_settings():
    # The tests (CharacterTestRig.MovementSetting) find a setting by its name alone, across
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


def check_test_fixtures():
    # Every test fixture logs how long each test ran and where the time went (TestTimingAttribute), and is in an area
    # ([Category(TestArea.X)], so an area can be run on its own): each on the fixture or on a class it derives from
    # (CharacterRigFixture carries the timing).
    # A class's name may have type parameters (AbilityFixture<T>), and so may its base (AbilityFixture<Dashing>): both are
    # known by their names alone. An abstract class isn't run by itself (only the fixtures deriving it are), so it's
    # checked through them.
    classes = {}  # name -> (base, attributes, has tests, file, abstract)
    for path in sorted((ASSETS / "Tests").rglob("*.cs")):
        text = path.read_text(encoding="utf-8")
        for match in re.finditer(r"((?:^[ \t]*\[[^\n]*\][ \t]*\n)*)^[ \t]*(?:public |internal )?(abstract |sealed )?(?:partial )?class (\w+)(?:<[^>]*>)?(?:\s*:\s*(\w+))?", text, re.M):
            body_start = match.end()
            next_class = re.search(r"^[ \t]*(?:public |internal )?(?:abstract |sealed )?(?:partial )?class \w+", text[body_start:], re.M)
            body = text[body_start: body_start + next_class.start()] if next_class else text[body_start:]
            classes[match.group(3)] = (match.group(4), match.group(1),
                                       bool(re.search(r"^[ \t]*\[(Test|UnityTest|TestCase)\b", body, re.M)), path,
                                       match.group(2) == "abstract ")

    def has(name, attribute, seen=()):
        if name not in classes or name in seen:
            return False
        base, attributes, _, _, _ = classes[name]
        return attribute in attributes or has(base, attribute, seen + (name,))

    def has_tests(name, seen=()):
        # Its own tests, or a base class's (an ability fixture's kit tests are AbilityFixture's).
        if name not in classes or name in seen:
            return False
        base, _, own, _, _ = classes[name]
        return own or has_tests(base, seen + (name,))

    for name, (_, _, _, path, abstract) in classes.items():
        if abstract or not has_tests(name):
            continue
        if not has(name, "TestTiming"):
            problems.append(f"{path.relative_to(ROOT)}: test fixture {name} has no [TestTiming] (nor a base class with it)")
        if not has(name, "Category(TestArea."):
            problems.append(f"{path.relative_to(ROOT)}: test fixture {name} is in no area: give it [Category(TestArea.…)] (see TestArea)")


def check_serialized_components():
    # [SerializeReference] saves each object's class by name: "type: {class: X, ns: Y, asm: Z}". Renamed or moved without
    # a [MovedFrom] naming the old one, it shows as a missing component in the asset (its settings lost).
    ours = {name: folder for name, folder, _ in assemblies()}
    for asset in sorted(ASSETS.rglob("*.asset")):
        text = asset.read_text(errors="replace")
        if "type: {class:" not in text:
            continue
        for cls, ns, asm in sorted(set(re.findall(r"type: \{class: (\w+), ns: ([\w.]*), asm: ([\w.]+)\}", text))):
            if asm not in ours:
                continue  # Unity's or a package's
            code = "\n".join(p.read_text(errors="replace") for p in ours[asm].rglob("*.cs"))
            declared = re.search(rf"\bnamespace\s+{re.escape(ns)}\b[\s\S]*?\bclass\s+{cls}\b", code) if ns else \
                re.search(rf"\bclass\s+{cls}\b", code)
            moved = re.search(rf'MovedFrom\([^)]*sourceClassName:\s*"{cls}"', code)
            if not declared and not moved:
                problems.append(f"{asset.relative_to(ROOT)} names the class {ns}.{cls} ({asm}), which no longer exists: "
                                f"give its new name [MovedFrom(... sourceClassName: \"{cls}\")], or the asset loses it")


FLAG_ENUMS = {  # The flag enums each ability takes bits of (see docs/adding-an-ability.md), and where they're declared.
    "CharacterState": ASSETS / "Scripts" / "Character" / "CharacterState.cs",
    "MovementEvents": ASSETS / "Scripts" / "Character" / "MovementEvents.cs",
}


def check_flag_enums():
    # An ability takes the next unused bit of CharacterState for its state and two of MovementEvents for its start and
    # end, by hand. Two abilities written at once could take the same bit, and C# accepts that: both flags would then be
    # one, and each ability would seem to start when the other does. So every member is a single bit (None and Idle
    # apart), no two share one, and each fits the enum's width. A member made of others (A | B) is an alias, and allowed.
    for name, path in FLAG_ENUMS.items():
        where = path.relative_to(ROOT)
        text = path.read_text(encoding="utf-8")
        match = re.search(rf"\benum\s+{name}\s*(?::\s*(\w+))?\s*\{{(.*?)\}}", text, re.S)
        if not match:
            problems.append(f"{where}: enum {name} not found (update FLAG_ENUMS in this script if it moved)")
            continue
        bits = 64 if match.group(1) in ("long", "ulong") else 32
        body = re.sub(r"//[^\n]*|/\*.*?\*/", "", match.group(2), flags=re.S)
        owner = {}  # bit -> member
        for member in (m.strip() for m in body.split(",")):
            if not member:
                continue
            parts = [p.strip() for p in member.split("=", 1)]
            if len(parts) != 2:
                problems.append(f"{where}: {name}.{parts[0]} has no value of its own: give it its bit (the next unused one)")
                continue
            member_name, expression = parts
            if re.fullmatch(r"[\w\s|]+", expression) and re.search(r"[A-Za-z_]", expression.replace("L", "")):
                continue  # An alias of other members.
            shift = re.fullmatch(r"1[uUlL]*\s*<<\s*(\d+)", expression)
            literal = re.fullmatch(r"(0[xX][0-9a-fA-F]+|\d+)[uUlL]*", expression)
            if shift:
                value = 1 << int(shift.group(1))
            elif literal:
                value = int(literal.group(1), 0)
            else:
                problems.append(f"{where}: {name}.{member_name}'s value ({expression}) isn't a number or 1L << n: "
                                f"this check can't read it")
                continue
            if value == 0:
                continue  # None, Idle.
            if value & (value - 1):
                problems.append(f"{where}: {name}.{member_name} ({expression}) isn't a single bit: each member is one bit "
                                f"of its own (a member made of others is written as A | B)")
                continue
            bit = value.bit_length() - 1
            if bit >= bits:
                problems.append(f"{where}: {name}.{member_name} is bit {bit}, past the enum's {bits} bits")
            if bit in owner:
                problems.append(f"{where}: {name}.{member_name} and {name}.{owner[bit]} are both bit {bit}: "
                                f"give one the next unused bit")
            owner.setdefault(bit, member_name)


def main():
    if not ASSETS.is_dir():
        print(f"No Assets folder at {ASSETS}; run this from the repository.", file=sys.stderr)
        return 2
    check_meta_files()
    check_guids()
    check_assembly_references()
    check_independent_folders()
    check_reflected_settings()
    check_unique_movement_settings()
    check_test_fixtures()
    check_serialized_components()
    check_flag_enums()
    if problems:
        print(f"{len(problems)} problem(s) found:")
        for problem in problems:
            print(f"  - {problem}")
        return 1
    print("Project hygiene checks passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
