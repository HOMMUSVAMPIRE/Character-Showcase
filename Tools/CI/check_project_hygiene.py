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
 10-17. The ability conventions (docs/adding-an-ability.md), on every MovementComponent subclass in
     Assets/Scripts/MovementModes/Components, each failure naming the fix and the guide's section:
     10. Every setting (public or [SerializeField], not static, const, readonly or [NonSerialized]) has a [Tooltip].
     11. Every setting is of a kind a tuning preset can copy: a primitive, an enum or an AnimationCurve (as
         MovementTuningPresetTests requires, and MovementSettings.Copy can).
     12. A component that overrides TakeOver overrides Traits and StateEvents.
     13. Every CharacterState in its StateEvents is set somewhere in the movement code (`state |= CharacterState.X`),
         and every state of its own it sets is in its StateEvents (unless it's an ability of a moment, declaring
         OneOffEvents); and every event it raises by hand (`events |=`, JumpOff, JumpOffWall) is in its OneOffEvents.
     14. A PlayMode fixture names it (rig.Component<X>(), AbilityFixture<X>, MomentFixture<X> or typeof(X)), and an
         ability's fixture derives from a test kit: AbilityFixture<X>, or MomentFixture<X> for one of a moment.
     15. docs/movement.md's component list, and abilities.md or traversal.md, name it by its inspector name (as
         MovementComponent.DisplayNameOf gives it).
     16. It's in BasicMovementMode.DefaultComponents.
     17. No HasFlag on a CharacterState or MovementEvents anywhere in Assets/Scripts: it boxes the 64-bit enum, an
         allocation every step.
     Where a component legitimately differs, it's in a named allow-list below (with why), and an entry that's no longer
     needed fails too, so it comes off.
 18. Nothing that ships (AGENTS.md, The package line) names the model pipeline: Blender, its runner, the mannequin's and
     the fox's generators or their menu items, the Blender scripts, or starting a program. Code, comments and messages,
     as a buyer reads all three; PIPELINE_NAMES_ALLOWED lists what may (notes on FBX files exported from Blender).
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


# ---- The ability conventions (docs/adding-an-ability.md), checked on every MovementComponent ----

COMPONENTS = ASSETS / "Scripts" / "MovementModes" / "Components"
ABILITY_GUIDE = ROOT / "docs" / "adding-an-ability.md"
# The guide's sections the failures link to, by its headings (check_ability_guide_sections fails if one is renamed).
THE_COMPONENT = "1. The component"
ITS_STATE_FLAG = "3. Its state flag and events"
IN_THE_MODE = "4. Putting it in the mode"
TESTS_AND_DOCS = "7. Tests and docs"
GUIDE_SECTIONS = (THE_COMPONENT, ITS_STATE_FLAG, IN_THE_MODE, TESTS_AND_DOCS)


def anchor(heading):
    """A Markdown heading's anchor, as GitHub makes it: lower case, punctuation dropped, spaces to hyphens."""
    return re.sub(r"[^\w\- ]", "", heading.strip().lower()).replace(" ", "-")


def see(section):
    """Where a failure's fix is described: a section of docs/adding-an-ability.md."""
    return f"see {ABILITY_GUIDE.relative_to(ROOT)}#{anchor(section)} ({section})"


# The allow-lists: where a component legitimately differs from a convention, with why. Each entry is checked still to be
# needed, so it comes off the list once the component keeps the convention.

# Components that take the character over (TakeOver) without declaring Traits: AbilityTraits.None, the default, is what
# they do with it. Declaring it (`public override AbilityTraits Traits => AbilityTraits.None;`) would say so outright.
TAKEOVER_WITH_DEFAULT_TRAITS = {}  # Every takeover declares its Traits now (task 79): a new one may not leave them out.
# The states the movement as a whole sets, not one ability: any component may set them without listing them.
SHARED_STATES = {"Idle", "Moving", "Grounded", "Running", "Dead"}
# Components no PlayMode fixture names (rig.Component<X>(), AbilityFixture<X> or typeof(X)) yet, though one tests them by
# their settings' names. Each comes off as its fixture moves to the kit (AbilityFixture<X> names it).
NOT_NAMED_BY_A_FIXTURE = {}  # Every component's fixture names it now (task 79): a new one's must.
# Components documented in movement.md's own sections, as the character's basic movement, rather than in abilities.md or
# traversal.md: movement.md must mention them outside its component list too.
DOCUMENTED_IN_MOVEMENT_MD = {
    "WalkingAndRunning": "walking and sprinting are movement.md's Walking and Sprinting",
    "FallAndAirHandling": "gravity, air steering and friction are described with walking and jumping in movement.md",
    "Jumping": "movement.md's Jumping",
    "Dashing": "movement.md's Dash",
}
# Components that aren't abilities a player starts, so they needn't be on the ability test kit (AbilityFixture<X>), nor
# have a setting whose 0 turns them off. A fixture may still use the kit for the checks that apply (WadeTests does).
NOT_AN_ABILITY = {
    "WalkingAndRunning": "walking is the movement when no ability has the character: nothing starts or ends",
    "FallAndAirHandling": "gravity, the fall speed limit, air steering and friction act every step: nothing starts or ends",
    "Wading": "it's where the character stands (water too shallow to swim in), not something it does: it never takes the "
              "character over, and how deep wading starts is no on-off switch",
}
# Components whose PlayMode fixture doesn't derive from the ability test kit (AbilityFixture<X>) yet. Every new ability
# starts on the kit (the check fails without it); each of these comes off the list as its fixture moves to the kit (the
# check fails if one has and is still listed). See docs/adding-an-ability.md, The ability test kit.
NOT_YET_ON_THE_KIT = set()  # Every ability's fixture is on a kit now (tasks 79 and 80): a new one's must be.

# The kinds of setting a tuning preset can copy (MovementSettings.Copy; MovementTuningPresetTests fails on any other):
# a primitive (C#'s IsPrimitive: the numbers, bool and char), an enum, or an AnimationCurve (copied key by key).
PRIMITIVE_TYPES = {"bool", "byte", "sbyte", "short", "ushort", "int", "uint", "long", "ulong", "char", "float", "double",
                   "Boolean", "Byte", "SByte", "Int16", "UInt16", "Int32", "UInt32", "Int64", "UInt64", "Char", "Single",
                   "Double"}
# Unity's enums a setting may be of (the project's own are found in Assets/Scripts): add one here when a component uses it.
UNITY_ENUMS = set()


def blank_code(text):
    """
    The text with its comments and the insides of its string and character literals blanked out (spaces, newlines kept),
    so braces, brackets and semicolons in them don't count, and every index still matches the original.
    """
    out = list(text)
    i, n = 0, len(text)

    def blank(start, end):
        for k in range(start, end):
            if out[k] != "\n":
                out[k] = " "

    while i < n:
        c = text[i]
        if text.startswith("//", i):
            end = text.find("\n", i)
            end = n if end < 0 else end
            blank(i, end)
            i = end
        elif text.startswith("/*", i):
            end = text.find("*/", i + 2)
            end = n if end < 0 else end + 2
            blank(i, end)
            i = end
        elif c == '"' or (c in "@$" and text[i + 1: i + 2] in ('"', "@", "$")):
            j = i
            while text[j] in "@$":
                j += 1
            verbatim = "@" in text[i:j]
            k = j + 1
            while k < n:
                if verbatim and text.startswith('""', k):
                    k += 2
                elif not verbatim and text[k] == "\\":
                    k += 2
                elif text[k] == '"':
                    break
                else:
                    k += 1
            blank(j + 1, k)
            i = k + 1
        elif c == "'":
            k = i + 1
            while k < n and text[k] != "'":
                k += 2 if text[k] == "\\" else 1
            blank(i + 1, k)
            i = k + 1
        else:
            i += 1
    return "".join(out)


def matching_brace(code, open_at):
    """The index of the brace closing the one at open_at (in blanked code)."""
    depth = 0
    for i in range(open_at, len(code)):
        if code[i] == "{":
            depth += 1
        elif code[i] == "}":
            depth -= 1
            if depth == 0:
                return i
    return len(code) - 1


def members(code, start, end):
    """
    The spans of a class body's members (code[start:end], blanked, the braces excluded) that end in a semicolon: fields,
    and expression-bodied or abstract members. Members with bodies of their own (methods, properties, nested types) are
    left out. A brace after an initializer's = (an array or object initializer) is part of the field.
    """
    spans, seg, i = [], start, start
    while i < end:
        c = code[i]
        if c == "{":
            close = matching_brace(code, i)
            assign = re.search(r"(?<![=!<>])=(?![=>])", code[seg:i])
            if assign and "=>" not in code[seg:i] and "(" not in code[seg:seg + assign.start()]:
                i = close + 1  # An initializer: the field goes on to its semicolon.
                continue
            i = close + 1
            seg = i  # A member with a body: not a field.
            continue
        if c == ";":
            spans.append((seg, i))
            seg = i + 1
        i += 1
    return spans


FIELD = re.compile(r"^\s*((?:(?:public|private|protected|internal|static|readonly|const|volatile|new)\s+)*)"
                   r"([\w.]+(?:\s*<[^=;()]*?>)?(?:\s*\[\s*\])*\??)\s+(\w+)\s*(?:=[\s\S]*)?$")


def fields_of(text, code, start, end):
    """A class body's fields: (name, type, modifiers, attribute text, line), the attributes read from the original text."""
    found = []
    for seg_start, seg_end in members(code, start, end):
        i = seg_start
        attributes = []
        while True:
            while i < seg_end and code[i].isspace():
                i += 1
            if i >= seg_end or code[i] != "[":
                break
            depth, j = 0, i
            while j < seg_end:
                depth += {"[": 1, "]": -1}.get(code[j], 0)
                if depth == 0:
                    break
                j += 1
            attributes.append(text[i: j + 1])
            i = j + 1
        rest = code[i:seg_end]
        if "=>" in rest or "(" in rest.split("=", 1)[0]:
            continue  # An expression-bodied or abstract member.
        match = FIELD.match(rest)
        if not match:
            continue
        line = text.count("\n", 0, i) + 1
        found.append((match.group(3), re.sub(r"\s+", "", match.group(2)), match.group(1).split(), " ".join(attributes), line))
    return found


def movement_components():
    """
    Every MovementComponent subclass in Components/: name -> its file, text, blanked code, the span of its body, and its
    attributes (the text before its class declaration, back to the previous member).
    """
    found = {}
    for path in sorted(COMPONENTS.glob("*.cs")):
        text = path.read_text(encoding="utf-8")
        code = blank_code(text)
        for match in re.finditer(r"\bclass\s+(\w+)\s*:\s*MovementComponent\b", code):
            open_at = code.find("{", match.end())
            close = matching_brace(code, open_at)
            before = max(code.rfind(";", 0, match.start()), code.rfind("{", 0, match.start()),
                         code.rfind("}", 0, match.start()))
            found[match.group(1)] = (path, text, code, open_at + 1, close, text[before + 1: match.start()])
    return found


def inspector_name(cls, attributes):
    """
    A component's name in the inspector, as MovementComponent.DisplayNameOf gives it: its [MovementComponentName], else
    its class name with a space before each capital that follows a letter that isn't one.
    """
    named = re.search(r'\bMovementComponentName\s*\(\s*"([^"]*)"\s*\)', attributes)
    if named:
        return named.group(1)
    return "".join(" " + ch if i > 0 and ch.isupper() and not cls[i - 1].isupper() else ch for i, ch in enumerate(cls))


def project_enums():
    """The names of the enums declared in Assets/Scripts."""
    names = set()
    for source in (ASSETS / "Scripts").rglob("*.cs"):
        names.update(re.findall(r"\benum\s+(\w+)", blank_code(source.read_text(errors="replace"))))
    return names


def where(path, line=None):
    return f"{path.relative_to(ROOT)}:{line}" if line else str(path.relative_to(ROOT))


def check_ability_guide_sections():
    # The failures below link sections of the guide by their headings: renamed, the links would lead nowhere.
    headings = set(re.findall(r"^#+\s+(.+?)\s*$", ABILITY_GUIDE.read_text(encoding="utf-8"), re.M)) if ABILITY_GUIDE.exists() else set()
    for section in GUIDE_SECTIONS:
        if section not in headings:
            problems.append(f"{ABILITY_GUIDE.relative_to(ROOT)} has no section '{section}', which this script's ability "
                            f"checks link to: update GUIDE_SECTIONS in {Path(__file__).relative_to(ROOT)}")


def check_component_settings(components):
    # Every setting (a field Unity saves, as MovementSettings.Of finds them: public or [SerializeField], not static,
    # const, readonly or [NonSerialized]) has a tooltip for the designer, and is of a kind a tuning preset can copy.
    enums = project_enums() | UNITY_ENUMS
    for cls, (path, text, code, start, end, _) in components.items():
        for name, type_name, modifiers, attributes, line in fields_of(text, code, start, end):
            if {"static", "const", "readonly"} & set(modifiers) or re.search(r"\bNonSerialized\b", attributes):
                continue
            if "public" not in modifiers and not re.search(r"\bSerializeField\b", attributes):
                continue
            if not re.search(r"\bTooltip\s*\(", attributes):
                problems.append(f"{where(path, line)}: {cls}.{name} is a setting without a [Tooltip]: add "
                                f"[Tooltip(\"...\")] above it, saying for a designer what it does (its unit, and what 0 "
                                f"does if 0 turns the ability off): {see(THE_COMPONENT)}")
            bare = re.sub(r"^(?:System|UnityEngine)\.", "", type_name)
            if bare not in PRIMITIVE_TYPES and bare not in enums and bare != "AnimationCurve":
                problems.append(f"{where(path, line)}: {cls}.{name} is a {type_name}, which a tuning preset can't copy "
                                f"(MovementSettings.Copy would share it between a preset and a mode): make it a number, "
                                f"a bool, an enum or an AnimationCurve, or teach MovementSettings.Copy to copy one (and "
                                f"PRIMITIVE_TYPES here, and MovementTuningPresetTests): {see(THE_COMPONENT)}")


def overrides(code, start, end, returns, member):
    return re.search(rf"\boverride\s+{returns}\s+{member}\b", code[start:end]) is not None


def check_component_takeovers(components):
    # A component that takes the character over says what it does with it (Traits, which other abilities ask about) and
    # what states it starts and ends (StateEvents, from which the mode raises its events).
    for cls, (path, _, code, start, end, _) in components.items():
        if not overrides(code, start, end, r"bool", "TakeOver"):
            continue
        if not overrides(code, start, end, r"AbilityTraits", "Traits") and cls not in TAKEOVER_WITH_DEFAULT_TRAITS:
            problems.append(f"{where(path)}: {cls} overrides TakeOver but not Traits: add `public override AbilityTraits "
                            f"Traits => ...;` (HoldsUp if the character isn't falling while it has it, HoldsOn if it "
                            f"holds on to something, None if neither): {see(THE_COMPONENT)}")
        # One of a moment (a bounce: OneOffEvents) has no state to start and end.
        if not overrides(code, start, end, r"StateEvent\s*\[\s*\]", "StateEvents") and not overrides(code, start, end, r"MovementEvents", "OneOffEvents"):
            problems.append(f"{where(path)}: {cls} overrides TakeOver but not StateEvents: add `public override "
                            f"StateEvent[] StateEvents => stateEvents;` and a static array of StateEvent(its "
                            f"CharacterState, its started event, its ended event): {see(ITS_STATE_FLAG)}")
    for listed, why in (("TAKEOVER_WITH_DEFAULT_TRAITS", TAKEOVER_WITH_DEFAULT_TRAITS),):
        member, name = r"AbilityTraits", "Traits"
        for cls in why:
            if cls not in components:
                problems.append(f"{Path(__file__).relative_to(ROOT)}: {listed} lists {cls}, which isn't a movement component: take it off the list")
            else:
                path, _, code, start, end, _ = components[cls]
                if overrides(code, start, end, member, name):
                    problems.append(f"{where(path)}: {cls} declares {name} now: take it off {listed} in "
                                    f"{Path(__file__).relative_to(ROOT)}")


# Setting a state flag: `state |= ... CharacterState.X ...` or `state = ... CharacterState.X ...` (not a comparison).
STATE_SET = r"\bstate\s*(?:\|=|=(?![=>]))([^;]*)"


def check_component_states(components):
    # Each state in a component's StateEvents is set somewhere (else its events are never raised): in its own file, or
    # in the movement code (Crouching's is set by BasicMovementMode, which holds the posture). And each state a component
    # sets that's its own (not one the movement as a whole sets: SHARED_STATES) is in its StateEvents, so the mode raises
    # its events, unless its events are of a moment (OneOffEvents: Jumping's state is a jump's rise, its events the jump).
    movement_code = sorted((ASSETS / "Scripts" / "MovementModes").rglob("*.cs")) + \
        sorted((ASSETS / "Scripts" / "Character").glob("MovementController*.cs"))
    set_anywhere = set()
    for source in movement_code:
        for rhs in re.findall(STATE_SET, blank_code(source.read_text(encoding="utf-8"))):
            set_anywhere.update(re.findall(r"\bCharacterState\.(\w+)", rhs))
    for cls, (path, _, code, start, end, _) in components.items():
        body = code[start:end]
        listed = set(re.findall(r"\bnew\s*(?:StateEvent)?\s*\(\s*CharacterState\.(\w+)", body))
        for state in sorted(listed - set_anywhere):
            problems.append(f"{where(path)}: {cls}'s StateEvents names CharacterState.{state}, which nothing sets, so its "
                            f"events are never raised: set it on the steps it's under way "
                            f"(`movementData.state |= CharacterState.{state};`, in {cls}'s TakeOver or the hook it acts at): "
                            f"{see(ITS_STATE_FLAG)}")
        own = set()
        for rhs in re.findall(STATE_SET, body):
            own.update(re.findall(r"\bCharacterState\.(\w+)", rhs))
        missing = own - listed - SHARED_STATES
        moments = overrides(code, start, end, r"MovementEvents", "OneOffEvents")
        if missing and not moments:
            for state in sorted(missing):
                problems.append(f"{where(path)}: {cls} sets CharacterState.{state} but its StateEvents doesn't list it, "
                                f"so the mode raises no events as it starts and ends: add StateEvent(CharacterState.{state}, "
                                f"its started event, its ended event) to {cls}'s StateEvents: {see(ITS_STATE_FLAG)}")
        # The events it raises by hand are its moments, and it declares them (OneOffEvents): `events |= MovementEvents.X`,
        # a jump off something (MovementHelpers.JumpOff(..., MovementEvents.X)), or off a wall (JumpOffWall: WallJumped).
        raised = set()
        for rhs in re.findall(r"\bevents\s*\|=([^;]*)", body):
            raised.update(re.findall(r"\bMovementEvents\.(\w+)", rhs))
        for call in re.findall(r"\bJumpOff\s*\(([^;]*)", body):
            raised.update(re.findall(r"\bMovementEvents\.(\w+)", call))
        if re.search(r"\bJumpOffWall\s*\(", body):
            raised.add("WallJumped")
        declared = re.search(r"\boverride\s+MovementEvents\s+OneOffEvents\s*=>([^;]*);", body)
        declared_events = set(re.findall(r"\bMovementEvents\.(\w+)", declared.group(1))) if declared else set()
        for event in sorted(raised - declared_events):
            problems.append(f"{where(path)}: {cls} raises MovementEvents.{event} itself but doesn't declare it: add it to "
                            f"`public override MovementEvents OneOffEvents => ...;` (or, for a state's start or end, raise it "
                            f"through StateEvents instead): {see(ITS_STATE_FLAG)}")


def playmode_fixtures():
    """The PlayMode test files with tests in them (the rig and its helpers apart): path -> text."""
    found = {}
    for path in sorted((ASSETS / "Tests" / "PlayMode").rglob("*.cs")):
        text = path.read_text(encoding="utf-8")
        if re.search(r"^[ \t]*\[(Test|UnityTest|TestCase)\b", text, re.M):
            found[path] = blank_code(text)
    return found


def check_component_tests(components):
    # A PlayMode fixture names each component (so a rename reaches its tests, and every component has some), and each
    # ability's fixture is on the ability test kit (AbilityFixture<X>), whose checks every ability should pass.
    fixtures = playmode_fixtures()
    this = Path(__file__).relative_to(ROOT)
    for cls, (path, *_) in components.items():
        qualified = rf"(?:[\w.]+\.)?{cls}"  # Character.Ziplines, or Ziplines
        named = rf"\bComponent\s*<\s*{qualified}\s*>\s*\(|\b(?:Ability|Moment)Fixture\s*<\s*{qualified}\s*>|\btypeof\s*\(\s*{qualified}\s*\)"
        is_named = any(re.search(named, code) for code in fixtures.values())
        if not is_named and cls not in NOT_NAMED_BY_A_FIXTURE:
            problems.append(f"{where(path)}: no PlayMode test fixture names {cls}: give it a fixture deriving "
                            f"AbilityFixture<{cls}> (or reach it with rig.Component<{cls}>()) in Assets/Tests/PlayMode/"
                            f"<Area>: {see(TESTS_AND_DOCS)}")
        elif is_named and cls in NOT_NAMED_BY_A_FIXTURE:
            problems.append(f"{where(path)}: a PlayMode fixture names {cls} now: take it off NOT_NAMED_BY_A_FIXTURE in {this}")
        on_kit = [p for p, code in fixtures.items() if re.search(rf"\bclass\s+\w+\s*:\s*(?:Ability|Moment)Fixture\s*<\s*{qualified}\s*>", code)]
        if cls in NOT_AN_ABILITY:
            continue
        if not on_kit and cls not in NOT_YET_ON_THE_KIT:
            problems.append(f"{where(path)}: {cls} has no PlayMode fixture on the ability test kit: derive its fixture "
                            f"from AbilityFixture<{cls}> (SetUpToStart, InputsFor, OffSetting), or MomentFixture<{cls}> for an ability of a moment, as DashTests and "
                            f"ZiplineTests do: {see(TESTS_AND_DOCS)}")
        elif on_kit and cls in NOT_YET_ON_THE_KIT:
            problems.append(f"{where(on_kit[0])}: {cls}'s fixture is on the ability test kit now: take it off "
                            f"NOT_YET_ON_THE_KIT in {this}")
    for listed, entries in (("NOT_NAMED_BY_A_FIXTURE", NOT_NAMED_BY_A_FIXTURE), ("NOT_AN_ABILITY", NOT_AN_ABILITY),
                            ("NOT_YET_ON_THE_KIT", NOT_YET_ON_THE_KIT)):
        for cls in sorted(set(entries) - set(components)):
            problems.append(f"{this}: {listed} lists {cls}, which isn't a movement component: take it off the list")


def markdown_section(text, heading):
    """A Markdown section's text, from its heading to the next heading of the same level or higher."""
    match = re.search(rf"^(#+)\s+{re.escape(heading)}\s*$", text, re.M)
    if not match:
        return None
    level = len(match.group(1))
    after = re.search(rf"^#{{1,{level}}}\s", text[match.end():], re.M)
    return text[match.end(): match.end() + after.start()] if after else text[match.end():]


def mentions(text, name):
    return re.search(rf"(?<!\w){re.escape(name)}(?!\w)", text) is not None


def check_component_docs(components):
    # The docs name every component by its inspector name: movement.md's component list, and its own section in
    # abilities.md or traversal.md (or movement.md, for the character's basic movement).
    docs = ROOT / "docs"
    movement = (docs / "movement.md").read_text(encoding="utf-8")
    # The list is the section's first paragraph ("They're Bounce Pads, Ziplines, ..."): a name elsewhere in the section
    # (the order, the settings they share) isn't the list.
    section = markdown_section(movement, "Movement Components")
    component_list = section.strip().split("\n\n", 1)[0] if section else None
    if component_list is None:
        problems.append("docs/movement.md has no 'Movement Components' section, which the component docs check reads: "
                        f"update check_component_docs in {Path(__file__).relative_to(ROOT)}")
        return
    rest_of_movement = movement.replace(section, "")
    abilities = (docs / "abilities.md").read_text(encoding="utf-8") + (docs / "traversal.md").read_text(encoding="utf-8")
    for cls, (path, _, _, _, _, attributes) in components.items():
        name = inspector_name(cls, attributes)
        if not mentions(component_list, name):
            problems.append(f"{where(path)}: docs/movement.md's component list (#movement-components) doesn't name "
                            f"{name} (the inspector name of {cls}): add it at its place in the list: {see(TESTS_AND_DOCS)}")
        if cls in DOCUMENTED_IN_MOVEMENT_MD:
            if not mentions(rest_of_movement, name):
                problems.append(f"{where(path)}: docs/movement.md names {name} (the inspector name of {cls}) only in its "
                                f"component list: name it in the section describing it: {see(TESTS_AND_DOCS)}")
        elif not mentions(abilities, name):
            problems.append(f"{where(path)}: neither docs/abilities.md nor docs/traversal.md names {name} (the "
                            f"inspector name of {cls}): add a section for it there, naming the component and its settings by their "
                            f"inspector names (never their values), or name it in the section describing it: "
                            f"{see(TESTS_AND_DOCS)}")
    for cls in sorted(set(DOCUMENTED_IN_MOVEMENT_MD) - set(components)):
        problems.append(f"{Path(__file__).relative_to(ROOT)}: DOCUMENTED_IN_MOVEMENT_MD lists {cls}, which isn't a "
                        f"movement component: take it off the list")


def check_default_components(components):
    # A new mode starts with every component (BasicMovementMode.DefaultComponents), and the inspector offers what a
    # mode lacks from that list.
    path = ASSETS / "Scripts" / "MovementModes" / "BasicMovementMode.cs"
    code = blank_code(path.read_text(encoding="utf-8"))
    match = re.search(r"\bDefaultComponents\s*\(\s*\)\s*\{", code)
    if not match:
        problems.append(f"{where(path)}: DefaultComponents() not found (update check_default_components in "
                        f"{Path(__file__).relative_to(ROOT)} if it moved)")
        return
    body = code[match.end(): matching_brace(code, match.end() - 1)]
    for cls, (component_path, *_) in components.items():
        if not re.search(rf"\bnew\s+{cls}\s*\(", body):
            problems.append(f"{where(component_path)}: {cls} isn't in BasicMovementMode.DefaultComponents: add "
                            f"`yield return new {cls}();` at its place in the order ({where(path)}): {see(IN_THE_MODE)}")


BOXED_FLAG_ENUMS = ("CharacterState", "MovementEvents")


def check_no_has_flag():
    # Enum.HasFlag boxes its argument (and, in Unity's Mono, a 64-bit enum's value too): an allocation per call, every
    # step. Test the bit instead: (state & CharacterState.X) != 0. Found by its argument (HasFlag(CharacterState.X)), or
    # by its receiver, a name something of these types is declared as (state.HasFlag(...)).
    sources = {path: blank_code(path.read_text(errors="replace")) for path in sorted((ASSETS / "Scripts").rglob("*.cs"))}
    types = "|".join(BOXED_FLAG_ENUMS)
    names = set()
    for code in sources.values():
        names.update(re.findall(rf"\b(?:{types})\s+(\w+)\s*[;=,)]", code))
    receivers = "|".join(sorted(names)) or r"(?!)"
    pattern = re.compile(rf"\bHasFlag\s*\(\s*(?:\(\s*(?:{types})\s*\)\s*)?\(?\s*(?:{types})\.|\b(?:{receivers})\s*\.\s*HasFlag\s*\(")
    for path, code in sources.items():
        for match in pattern.finditer(code):
            line = code.count("\n", 0, match.start()) + 1
            problems.append(f"{where(path, line)}: HasFlag on a {' or '.join(BOXED_FLAG_ENUMS)} boxes the 64-bit enum, an "
                            f"allocation every call: test the bit instead, `(value & CharacterState.X) != 0`: "
                            f"{see(ITS_STATE_FLAG)}")


def check_ability_conventions():
    components = movement_components()
    if not components:
        problems.append(f"No MovementComponent subclasses found in {COMPONENTS.relative_to(ROOT)}: update "
                        f"check_ability_conventions in {Path(__file__).relative_to(ROOT)} if they moved")
        return
    check_ability_guide_sections()
    check_component_settings(components)
    check_component_takeovers(components)
    check_component_states(components)
    check_component_tests(components)
    check_component_docs(components)
    check_default_components(components)
    check_no_has_flag()


# What the package doesn't have, so nothing that ships may name it: the model pipeline (Blender, the mannequin's and the
# fox's generators and their menu items, the Blender scripts) and starting programs (what running Blender needs).
PIPELINE_NAMES = re.compile(r"\bBlender\b|\bBlenderRunner\b|\bMannequinGenerator\b|\bFoxGenerator\b|Generate Mannequin|Generate Fox"
                            r"|Tools/(?:Mannequin|Fox|Blender|Animation)/|System\.Diagnostics\.Process|\bProcess\.Start\b")
# The folders that don't ship (AGENTS.md, The package line): everything else under these roots does.
UNSHIPPED_FOLDERS = ("Editor/Pipeline/", "Editor/LevelChecks/", "Editor/Level/", "Tests/EditMode/Levels/")
# Mentions that ship on purpose: how FBX files exported from Blender are put right (a buyer's model may be one), which
# require nothing of Blender. Each is (the file, a phrase on its line).
PIPELINE_NAMES_ALLOWED = (
    ("Editor/Models/CharacterModelImporter.cs", "Blender's \"Armature\""),
    ("Editor/Models/CharacterModelImporter.Pose.cs", "Blender's FBX export"),
    ("Tests/EditMode/Models/PreparedModelTests.cs", "Blender's axis conversion"),
)


def check_no_pipeline_in_package():
    # The package ships without the model pipeline (AGENTS.md, The package line), so what ships may not name it: not in
    # code, comments or messages, since a buyer reads all three, and a message sending them to Generate Mannequin would
    # send them to a menu item they haven't got.
    used = set()
    for path in sorted(ASSETS.rglob("*.cs")):
        relative = path.relative_to(ASSETS).as_posix()
        if not relative.startswith(("Scripts/", "Editor/", "Tests/")) or relative.startswith(UNSHIPPED_FOLDERS):
            continue
        for number, line in enumerate(path.read_text(encoding="utf-8").split("\n"), 1):
            match = PIPELINE_NAMES.search(line)
            if not match:
                continue
            allowed = next((a for a in PIPELINE_NAMES_ALLOWED if a[0] == relative and a[1] in line), None)
            if allowed:
                used.add(allowed)
                continue
            problems.append(f"{path.relative_to(ROOT)}:{number}: names the model pipeline ('{match.group(0)}'), which the package "
                            f"doesn't have: name what ships instead (Prepare Animations, the layouts), or say it's the showcase's")
    for allowed in PIPELINE_NAMES_ALLOWED:
        if allowed not in used:
            problems.append(f"Tools/CI/check_project_hygiene.py: PIPELINE_NAMES_ALLOWED's {allowed} matches nothing now: take it off")


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
    check_no_pipeline_in_package()
    check_ability_conventions()
    if problems:
        print(f"{len(problems)} problem(s) found:")
        for problem in problems:
            print(f"  - {problem}")
        return 1
    print("Project hygiene checks passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
