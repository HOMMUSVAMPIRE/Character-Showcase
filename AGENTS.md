# AGENTS.md

Instructions for AI agents (and people) changing this repository. Read this before editing anything. README.md and
docs/ describe what the project does; this file describes how to change it safely. docs/architecture.md is the map
of how it fits together, docs/decisions.md why, docs/glossary.md its terms, and docs/troubleshooting.md the usual
pitfalls.

## Open tasks

`ReferenceAssets/OpenTasks.md` lists the improvements chosen for this repo, ordered by impact, each with where, what's
wrong, the fix and its effort. Check it before starting work, and when asked what's next. When a task is done, move it
to the list's Done section, struck through (or note what's left of it); task numbers are fixed IDs, never renumbered. The list is the one file in `ReferenceAssets/` you may edit. It's
kept out of git, so it exists only on the machine that has it. If it's missing, say so; don't recreate it from memory.

## The project at a glance

A Unity character controller showcase: movement and its abilities, a standalone camera rig, a generated humanoid
mannequin with animations and IK, a pipeline for other rigged humanoid models, and generated graybox test levels.

**Unity version:** `ProjectSettings/ProjectVersion.txt` is authoritative. Never change it, and never edit
`Packages/manifest.json` or `Packages/packages-lock.json` unless the task explicitly asks for a package change.

| Folder | What's there |
| --- | --- |
| `Assets/Scripts/Character` | `ACCharacterController`, `ACInputManager`, `CharacterSpawner`, `MovementController` and the types it works on (`MovementData`, `Inputs`, `CharacterState`, `MovementEvents`, `MovementSizeScaling`) |
| `Assets/Scripts/MovementModes` | `MovementMode`, `ComposedMovementMode` and `MovementComponent`, `BasicMovementMode` (posture and the presses its components share; its capability measurements), `ButtonPress`, `MovementHelpers` (shared rules: jumps, wall jumps), `FreeSteering` (3D steering off the ground: underwater, and later flying), `MovementSettings` and `MovementTuningPreset` (saved tunings, applied by setting name); `Components/` (every part of the movement, as a component) |
| `Assets/Scripts/Animation` | `AnimationController`, `CharacterAnimationState`, the animation translators |
| `Assets/Scripts/IK` | `CharacterIK`, the IK components on every model, and `CharacterUtilities` (IK goal helpers) |
| `Assets/Scripts/Models` | `CharacterModelHost`, the model sources, and `CharacterHitboxes` (the hitboxes on a model's bones) |
| `Assets/Scripts/Effects` | `WaterEffects` (splashes and ripples, played from the movement's events) and `IWaterEffect` |
| `Assets/Scripts/Level` | The level mechanics' runtime components |
| `Assets/Scripts/CameraSystem` | The camera rig (its own assembly) |
| `Assets/Editor` | `Mannequin/` (MannequinGenerator), `Creatures/` (FoxGenerator), `Models/` (CharacterModelImporter), `Animation/` (clip importers), `Scene/` (Set Up Scene, level fixtures), `ClimbablePaint/`, `Effects/` (the default water effects), `Movement/` (the movement mode's inspector, tuning presets, the movement envelope) |
| `Assets/Editor/Level` | The level builders (one per area), `Graybox/` (the shared pieces and the build), `Specs/` (level specs and the Level Editor) |
| `Assets/Tests` | `EditMode/` and `PlayMode/` tests |
| `Tools` | `CI/` (hygiene check), `LevelSpec/` (levelspec tool), `Mannequin/` and `Fox/` (Blender scripts), `Animation/` (Blender scripts for clip libraries: root motion, rest pose) |

| Assembly | Folder | Namespace | May reference |
| --- | --- | --- | --- |
| `CharacterShowcase` | `Assets/Scripts` (except `CameraSystem/`) | `Character`, `Level` | Unity.InputSystem |
| `CameraSystem` | `Assets/Scripts/CameraSystem` | `CameraSystem` | Unity.InputSystem **only** |
| `CharacterShowcase.Editor` | `Assets/Editor` (except `Level/`) | `CharacterShowcase.EditorTools` | CharacterShowcase, CameraSystem |
| `CharacterShowcase.Level.Editor` | `Assets/Editor/Level` | `CharacterShowcase.EditorTools.Level` | Unity.RenderPipelines.Core.Runtime, Unity.RenderPipelines.Universal.Runtime, Unity.TextMeshPro **only** |
| `CharacterShowcase.Tests.EditMode` | `Assets/Tests/EditMode` | `CharacterShowcase.Tests.EditMode` | CharacterShowcase, CharacterShowcase.Editor, CharacterShowcase.Level.Editor, CharacterShowcase.Tests.Shared |
| `CharacterShowcase.Tests.PlayMode` | `Assets/Tests/PlayMode` | `CharacterShowcase.Tests` | CharacterShowcase, CameraSystem, CharacterShowcase.Tests.Shared |
| `CharacterShowcase.Tests.Shared` | `Assets/Tests/Shared` | `CharacterShowcase.Tests` | CharacterShowcase (what both test assemblies use: `TestTimingAttribute`, `TestArea`) |

The test assemblies also reference Unity's test runner. The hygiene check holds every assembly to this table (its
`ALLOWED_REFERENCES`): a new assembly, or a new reference, goes in both.

## How to verify your work

1. **Fast check, no Unity needed (before every commit):** `python3 Tools/CI/check_project_hygiene.py`. It catches
   missing or orphaned `.meta` files, duplicate GUIDs, an assembly referencing what the table above doesn't allow it,
   the camera or the level builders using character code, fields the tests read by name that no longer exist (or are
   read as another type), two movement settings sharing a name, a test fixture without `[TestTiming]` or a test area
   (`[Category(TestArea.…)]`), on it or on a class it derives from, and a movement asset naming a component class that
   no longer exists (renamed without `[MovedFrom]`).
2. **EditMode and PlayMode tests** need the Unity Editor. With Unity installed locally, close the editor and run:
   ```
   <Unity> -batchmode -projectPath . -runTests -testPlatform EditMode -testResults EditModeResults.xml
   <Unity> -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults PlayModeResults.xml
   ```
   Without Unity, push a branch or open a pull request: the `Tests` GitHub Actions workflow runs the hygiene check and
   both suites (a new push cancels the run in progress, so batch pushes; Markdown-only and `docs/` changes don't
   trigger it).
3. **A refactor of the movement must leave `GoldenPathTests` passing:** it holds the character's movement, step by step,
   to recorded paths (docs/testing.md, Golden paths). Record them again (`CHARACTER_SHOWCASE_RECORD_GOLDEN_PATHS=1`)
   only when a change is meant to change the movement, and check the recorded paths' diff.
4. **A change to collision or the ground** (the body, the probes, `MovementController.Collision`/`.Ground`/`.Hover`)
   must keep `GroundContactTests` passing: compare its step traces before and after (Logs/StepTraces, docs/testing.md).
5. **While iterating, run only the fixtures your change can affect** with `-testFilter` (a regex over test names,
   e.g. `"LedgeTests|AbilityAnimationTests"`, or a whole area with `-testCategory`: every fixture is in a `TestArea`, as the docs are), plus those of what depends on it: movement reaches the movement,
   ability and scale fixtures; animation, IK and models reach `AnimationTests`, `AbilityAnimationTests`, `ModelTests`,
   `ModelVariantTests` and the EditMode model checks; the camera reaches `CameraRigTests`. Before committing, run both
   full suites and the hygiene check, and only commit on green.
6. The tests don't cover the levels' looks: after changing a builder, rebuild the level and look at it.
7. If you couldn't run the tests, say so plainly. Don't describe untested changes as working.

## Definition of done

- The hygiene check passes, the tests the change can affect pass, and, before a commit, both full suites pass (or
  you've said you couldn't run them).
- New behaviour has PlayMode tests, built on `CharacterTestRig` (character; fixtures derive from `CharacterRigFixture`, and every fixture has `[TestTiming]`, itself or through its base: the hygiene check makes sure) or the camera tests' plain-Transform
  setup (camera). Tests read expected values from the settings (`rig.MovementSetting<T>("fieldName")`) rather than
  hard-coding tuning, so retuning doesn't break them.
- The docs (README.md and docs/) are updated when behaviour, controls, settings or workflows change: they're how the
  project is presented. **The docs never quote a value that lives somewhere else** (a setting, a constant, a measured
  speed, a level dimension): they name where it lives (the setting's inspector name and the asset or component it's
  on), so retuning can't make them wrong. A value only code knows gets a name: a `const` for an implementation
  detail, a serialized field with a `[Tooltip]` for anything a designer would tune.
- No unrelated changes: no reformatting of untouched code, no drive-by renames, no package or version bumps.

## Architectural rules (do not break these)

- **The camera knows nothing about the character.** Nothing in `Assets/Scripts/CameraSystem` may reference the
  `Character`, `Level` or `CharacterShowcase` namespaces or assemblies; it follows a plain Transform. A camera feature
  that needs character information exposes a generic hook (an interface, an event, a settable value) connected from
  character code.
- **Models come from a `CharacterModelSource`.** `CharacterModelHost` asks its source for a model (`CreateModelAsync`)
  and gives it back (`ReleaseModel`); it never instantiates or destroys one itself. New ways of getting a model are new
  subclasses.
- **The level knows nothing about the character or the camera.** The builders in `Assets/Editor/Level` make geometry,
  lighting and markers only, and their assembly references no project code. Game setup is connected from the
  character side (`SceneGameSetup`, `LevelFixtureSetup`).
- **Movement and animation are separate.** Movement produces `MovementData` and raises `MovementEvents`; animation
  reads `CharacterAnimationState` and receives events through `AnimationController`. Movement code never touches
  Animators, models or IK. Model-specific behaviour belongs in a `CharacterAnimationTranslator`.
- **Movement modes are ScriptableObjects made of components.** A `ComposedMovementMode` holds a list of
  `MovementComponent`s (`[SerializeReference]`), each an ability with its own settings, run in list order (higher
  takes over first); a component asks its `Mode` for shared rules (gravity, air acceleration, the jump and crouch
  presses, `AbilityInProgress` or `AbilityInProgressExcept<T>` for one that may take over from a T,
  `AbilityInProgressWith(AbilityTraits)` for one that cares what the ability under way does with the character (holds it
  up, holds on: each ability declares its `Traits`), `InterruptOthers`) rather than reaching into other abilities or
  listing them. One shaped by another's
  settings reads them with `SettingsOf<T>()` (the mode's, on or off, else T's defaults); one handing the character to
  another finds it with `Mode.Get<T>()` (turned-on only). Shared state rules (wall jumps) are in `MovementHelpers`.
  Every part of the movement is a component (walking is `WalkingAndRunning`; gravity, the fall speed limit, air
  steering and ground friction are `FallAndAirHandling`); `BasicMovementMode` keeps only the body's posture and the
  presses and speeds its components share. A step runs: `AdvanceTimers`, `BeforeAbilities` (gravity), `TakeOver`;
  then, if nothing took the character, `BeforeLocomotion` (crouching), `Locomotion` (ground friction before walking,
  which reads the momentum it leaves; air steering) and `AfterWalking` (dashing, then jumping); then posture (held as
  the first component's `Posture` asks: low, or lying) and `FinishStep` (the fall speed limit). Within each point, list order
  decides. A component whose class name can't say its inspector name gets `[MovementComponentName]`. Modes are shared
  assets, so per-character state lives on the `MovementController`, declared beside its component (the state class and
  its property, on a part of the partial `MovementController`, in the component's file). Adding an
  ability: see docs/adding-an-ability.md.
- **Physics vs. render timing.** Movement runs in `FixedUpdate` (driven by `ACCharacterController`); visual
  smoothing and animation run in `Update`. Don't move work between them.
- **The character is resized only through `ACCharacterController.SetScale`,** which scales the model parent, never the
  root. `MovementController.ApplyScale` resizes the body (collision reads the body's size, not transform scale);
  movement multiplies speeds by `SpeedScale` and distances by `HeightScale`; the animation state carries `scale`. New
  size-dependent values scale the same way.
- **Read the physics pose, not the transform.** The character's and platforms' Rigidbodies are interpolated, so a
  Transform holds a lagging render pose. Movement reads `Rigidbody.position` / `rotation` (see `GetGroundPose`).
  Moving platforms are kinematic Rigidbodies that move first each physics step (`MovingPlatform.MoveOrder`, before
  the characters' FixedUpdate), their pose set at once so the characters ride and query them where they'll be; they're
  drawn interpolated by themselves. Anything that steps a character in FixedUpdate must run after them.
- **Collision is resolved by `MovementController` with sweeps of the character's body,** not the physics solver.
  Blocked momentum is removed; `MovementData.collisions` / `previousCollisions` record blocked sides. Every query that
  depends on the body's shape (sweeping it, whether it fits, pushing it out of overlaps, its size) goes through
  `MovementController.Body`, a `CharacterBody` (`PrimitiveBody`: shaped as a capsule, box or sphere, not a collider):
  don't reach for a collider. The character has none: what meets it meets its model's hitboxes (`CharacterHitboxes`, on
  the CharacterHitbox layer, triggers unless set to collide), which no movement or IK query may meet (keep the layer
  out of their masks). A body can be of several parts (a Body Definition's Extra Parts: the fox's back and head);
  its proportions are the union of its parts, so measure the character by them, never by one part. Abilities measure the character by the body's proportions (`StandingHeight`, `FrontExtent`,
  `BackExtent`, `SideExtent`), the one they mean, not by a collider dimension.
- **The body hovers over steps** (Hover Over Steps, on by default): its queries are of the body with its bottom a
  step's height above the feet, which probes keep on the ground (`MovementController.Hover.cs`). So a body swept down
  onto something rests a step above where the feet go: an ability finding where the character will stand puts the feet
  there with `FeetOnGround`, and a sweep down to find a floor reaches `BodyLift` further.
- **Time-since-grounded values update every step, including in the air.** Coyote time, coyote gravity and air
  steering depend on it.
- **Long comments in the code explain past bugs.** Treat them as constraints: keep the behaviour a comment protects,
  or replace it with a test that does.

## Files to treat with care

**Hotspots.** A movement feature lands in its own component file (its state, declared there on a part of
`MovementController`, and its state's events, its own `StateEvents`), and in `BasicMovementMode.DefaultComponents`. If several agents work at once, only one
should own each at a time.

**Serialized field names are part of the interface.** Renaming a serialized field (`[SerializeField]`, or public on a
MonoBehaviour/ScriptableObject) silently drops its saved value everywhere; if a rename is really needed, add
`[UnityEngine.Serialization.FormerlySerializedAs("oldName")]`, which can't follow a setting into another component:
moving one drops its saved value, so set it again on the asset (and in tuning presets, which name settings by
component and field). Tests also read the movement mode's and its components'
fields by name through reflection, so a rename compiles but fails at runtime: the hygiene check finds every name the
tests use and fails if one disappears. A movement component's class name is saved in the asset too
(`[SerializeReference]`): renaming or moving one needs `[UnityEngine.Scripting.APIUpdating.MovedFrom]`, or the asset
shows it as a missing component. Unity silently drops enum fields wider than 32 bits, which is why a saved set of
`MovementEvents` (64-bit) is a `long` (`AnimatorParameterTranslator.EventTrigger`, drawn by `EventTriggerDrawer`).

**Unity asset files.**
- Every file and folder under `Assets/` needs a `.meta` with a unique GUID. Adding a file outside the editor, copy a
  neighbouring `.meta` of the same type with a fresh GUID (`python3 -c "import uuid; print(uuid.uuid4().hex)"`);
  never copy a GUID. Move or delete a file's `.meta` with it (`git mv` both, which keeps references working).
- Avoid hand-editing `.prefab`, `.unity` and `.asset` YAML. Prefer, in order: code that needs no asset change
  (defaults in field initializers, `GetComponent` fallbacks); an editor script run with `-executeMethod`; a minimal
  edit of a value you understand (a tuning number in `Assets/ScriptableObjects/BasicMovementMode.asset` is fine). If
  a task needs inspector wiring you can't do safely, describe the exact change for a person to make.
- The level scenes are generated (see Generated assets) and stored with Git LFS, as are the models and their textures: a
  scene's diff isn't readable, so rebuild a level and look at it rather than reviewing its YAML.

**Generated assets: regenerate, don't edit.** Edit the generator and regenerate. All have `-executeMethod` entry
points (see docs/animation.md, docs/levels.md, docs/spawning.md). If you can't run Blender or Unity, make the source
change and say regeneration is still needed.

| Generated | Generator |
| --- | --- |
| `Mannequin.fbx` (mesh, rig, clips), `Assets/Animation/Mannequin.controller`, the model setup on `Assets/Prefabs/Character.prefab` | `MannequinGenerator` (Generate Mannequin), running `Tools/Mannequin/*.py` in Blender |
| `Fox.fbx` (mesh, rig, clips), and in `Assets/Models/Creatures/Fox` its `Fox.controller`, `Fox Model.prefab`, translator and `Fox.asset`; and `Assets/Prefabs/Fox.prefab` | `FoxGenerator` (Generate Fox), running `Tools/Fox/*.py` in Blender. It makes `Fox Body.asset` and `Assets/ScriptableObjects/Fox Movement.asset` only where they're missing: those are hand-tuned |
| Every model's `[name] Model.prefab`, `[name].asset` and avatar/T-pose import settings in `Assets/Models/CharacterModels` | `CharacterModelImporter` (Prepare Character Models) |
| Every model prefab's hitboxes (`Hitbox (bone)` objects on its bones, and its `CharacterHitboxes`' list of them) | `HitboxFitter` (Fit Hitboxes), which Prepare Character Models and Generate Fox also run on each model they save, keeping its settings |
| A scene's Game Setup object (spawner, input manager, camera), and its fixtures made to work | `SceneGameSetup` (Set Up Scene), which runs `LevelFixtureSetup` |
| The level scenes (`MeridianComplex`, `LevelPlayground`), their `Environment/` assets and baked data | Their builders in `Assets/Editor/Level` |
| `Tools/LevelSpec/level-spec.schema.json`, and `Assets/Levels/[scene].level.json` for levels with builders | `LevelSpecTools` (Export Level Specs). Specs can also hold Level Editor or levelspec-tool edits: don't re-export over those without asking |
| Climbable paint masks (`Assets/ClimbablePaint`) | A `PaintedClimbableSurface`'s Repaint Masks |
| `Tools/LevelSpec/envelope.json` (the movement checks' limits) | `MovementEnvelopeWriter` (Write Movement Envelope, and on saving the tuning in the editor), from the default character's capabilities |
| A model source's measured height, and its body (`[name] Body.asset`, unless its Refit On Prepare is off) | `CharacterModelImporter` (Prepare Character Models) |

The tools keep the IK settings on model prefabs and edits to `Assets/Animation/Mannequin Animator Translator.asset`.
New Animator parameters or states belong in `MannequinGenerator`; new clips in the Blender scripts. The levels'
materials (`Assets/Materials/Level/Level_[Surface].mat`), `NoiseConcrete.shadergraph`, `Water.shader` and
`WaterEffect.shader` are hand-edited: nothing regenerates them. So are the default water effects (`Assets/Prefabs/Effects`,
`Assets/Materials/Effects`), which Create Water Effects (`WaterEffectAssets`) makes only where they're missing.

## Recipes

**A new movement ability:** follow [docs/adding-an-ability.md](docs/adding-an-ability.md), the full checklist (the
component and its hooks, its state, flag and events, its place in the mode, animation, level fixtures, tests and docs).

**A new level mechanic, a new level piece, or a change to a builder:** follow
[docs/adding-level-content.md](docs/adding-level-content.md).

**Retuning the movement:** follow [docs/tuning.md](docs/tuning.md). In short: change the asset, not the code defaults;
saving it in the editor writes the movement envelope again (`MovementEnvelopeAutoWriter`), but a change made outside
the editor needs Write Movement Envelope run, or `MovementEnvelopeTests` fails. No tuning is fixed: tests that need one
set it on the rig. The walk and run speeds suit the locomotion clips (docs/animation.md): retune them together. The
swimming clips are floated at Swimming's Swim Depth, for the height of the body the Character prefab has with its model (its model's, when its host's Collider Fit takes it): after changing either, run Generate Mannequin.

## Things that aren't obvious

- **Input** flows `InputSystem_Actions.inputactions` → `ACInputManager` (a singleton) → `Inputs` →
  `MovementController.ProcessMovementInputs`. New buttons are read with `ACInputManager`'s `ReadButton`, the way Dash
  is: its action looked up once in `FindActions`, its `InputButton` updated in place (the `Inputs` and its buttons are
  the same objects every frame, so nothing should keep one expecting it not to change). One-off presses go through a `ButtonPress`, since a press can last several physics steps or fall between them.
  `Attack` is defined but unread. Camera input is separate (`CameraInputReader`).
- **Couplings that block NPCs:** `ACCharacterController` reads `ACInputManager.instance`. Inputs are relative to
  `MovementController.InputCamera`, falling back to `Camera.main`.
- **Movement times things by `MovementController.StepTime`,** its own clock, never `Time.fixedTime`, so a copy stepped
  faster than real time (`MovementSimulation`, which measures `Capabilities`) behaves the same. Don't read global time
  in movement code.
- **Abilities query the world through the `MovementController`** (`SweepBody`, `Raycast`, `OverlapSphere`,
  `ClosestPointOn`, `CanFitAt`, ...), which skip the character's own colliders and its `IgnoredCollisionRoot`.
  `SetPosture` holds the body standing, low or lying (a `BodyPosture`), keeping the feet in place.
- **Ropes, ziplines and water have no colliders:** abilities find them through `Level.Rope.All` / `Level.Zipline.All`
  / `Level.Water.All` (the character stands on what's built under water). A
  rope swings itself when let go and is drawn interpolated between physics steps, so the hands IK'd onto it stay on it.
  Bounce pad and jump launch speeds are solved for the stepped physics (`LaunchSpeedFor`), so heights are exact.
- **Climbable surfaces** are `Level.ClimbableSurface` (a collider or its parent) or painted cells of a
  `PaintedClimbableSurface`, which also paints a mask into the mesh's UV2 that the NoiseConcrete shader blends a
  climbable look by. Ladders are climbed by the same ability, up and down their middle, animated by `LadderIK`.
- **Spawn points** are spawn markers ("Spawn Point (…)") that `SceneGameSetup` turns into the spawner's spawn
  points; `SceneGameSetup.SpawnMarkerPrefix` must match `Graybox.SpawnMarkerPrefix`.
- **Test rig:** `CharacterTestRig` spawns the real Character prefab with `ACCharacterController` disabled, steps the
  `MovementController` itself with scripted inputs at a sped-up time scale (same physics step), and moves it with its
  own copy of the movement mode: change settings with `rig.SetMovementSetting`, never on the asset. Add helpers there
  rather than repeating setup in tests.

## Code style

Match the existing code:
- XML doc comments (`/// <summary>`) on classes and non-trivial methods.
- `[Tooltip]` on every serialized field, written for a designer; fields grouped with `[Header]`.
- Comments explain *why*, especially non-obvious physics or timing decisions, in full sentences.
- No allocation in per-step code: reuse buffers (see `castHits`, `overlaps`).
- PascalCase types and methods, camelCase fields, the namespaces in the table above.
- Big classes are partial, a file per area of responsibility (`MovementController.<Area>.cs`, `Graybox.<Kind>.cs`,
  `MannequinGenerator.<Part>.cs`): add to the file that fits, or start a new one, rather than growing one file.

## Git

- Work on a branch, based on `main`; don't commit to `main` directly. Keep commits focused, with messages that say what
  changed and why.
- Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Builds/` or test results (the `.gitignore` covers
  most of these).
- `ReferenceAssets/` (at the root, ignored by git) holds reference material for you: read from it, but don't commit,
  move or edit what's in it (except `OpenTasks.md`: see Open tasks).
