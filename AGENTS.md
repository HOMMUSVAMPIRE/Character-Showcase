# AGENTS.md

Instructions for AI agents (and people) changing this repository. Read this before editing anything. README.md and
docs/ describe what the project does; this file describes how to change it safely. docs/architecture.md is the map
of how it fits together, docs/decisions.md why, docs/glossary.md its terms, and docs/troubleshooting.md the usual
pitfalls.

## Open tasks

`ReferenceAssets/OpenTasks.md` lists the improvements chosen for this repo, in lettered categories (A, B, ...), each
with its creation date and a summary whose first sentence is its status: **COMPLETE.**, **IN-PROGRESS.** or **NOT
STARTED.** A category that isn't complete lists its tasks (C2, E10: letter and number), ordered by impact, each with
where, what's wrong, the fix and its effort. Check it before starting work, and when asked what's next. When a task is
done, strike it through and say what was done (or what's left of it); it stays until its whole category is complete.
Then the category's tasks and notes move to `ReferenceAssets/OpenTasksArchive.md`, and the category keeps only its header
and summary. IDs are fixed, never renumbered. (Tasks were numbered 1-85 before 2026-11-19: the archive maps them to
the letters, for the code comments that cite the old numbers.) Those two are the only files in `ReferenceAssets/` you
may edit. They're kept out of git, so they exist only on the machine that has them. If they're missing, say so; don't
recreate them from memory.

## The project at a glance

A Unity character controller showcase: movement and its abilities, a standalone camera rig, a generated humanoid
mannequin with animations and IK, a pipeline for other rigged humanoid models, and generated graybox test levels.

**Unity version:** `ProjectSettings/ProjectVersion.txt` is authoritative. Never change it, and never edit
`Packages/manifest.json` or `Packages/packages-lock.json` unless the task explicitly asks for a package change.

The project is two folders: **`Assets/MeridianMotion`**, the package (com.meridianmotion.charactercontroller: what ships, with its
`package.json`), and **`Assets/Showcase`**, what doesn't (the levels, the model pipeline, the fox and the sample models).
`Assets/ClimbablePaint` is the climbable paint tool's output (the showcase's masks), `Assets/Settings` the project's URP
settings, and `Assets/TextMesh Pro` Unity's; none of them ship.

| Folder | What's there |
| --- | --- |
| `Assets/MeridianMotion/Runtime/Characters/Character` | `MMotionCharacter`, `CharacterSpawner`, and what the movement and animation share: `CharacterState`, `MovementEvents`, `AbilityValues` |
| `Assets/MeridianMotion/Runtime/Characters/Motor` | `MMotionMotor` (partial, a file per area) and the types it works on: `MovementStep`, `StepResolution`, `MovementSizeScaling`, `MovementCapabilities`, `MovementSimulation` |
| `Assets/MeridianMotion/Runtime/Characters/Body` | The character's body: `CharacterBody`, `PrimitiveBody`, `BodyShape`, `BodyDefinition`, `BodyPosture` |
| `Assets/MeridianMotion/Runtime/Characters/Input` | `CharacterInputFrame`, `InputState`, `StepInputs`, `InputLatch`, `PlayerInputReader`, `ControlledBy`, recording (`InputRecording`, `CharacterInputRecorder`), `InputSources/` |
| `Assets/MeridianMotion/Runtime/Characters/Determinism` | Saving and restoring the movement: `ObjectGraphSnapshot`, `WorldStateSnapshot`, `StateSerializer`, `StableOrder` |
| `Assets/MeridianMotion/Runtime/Characters/MovementModes` | `MovementMode`, `AbilityMovementMode` and `MovementComponent`, `StandardMovementMode` (posture and the presses its components share; its capability measurements), `PressBuffer`, `MovementRules` (shared rules: jumps, wall jumps), `FreeSteering` (3D steering off the ground: underwater, and later flying), `MovementSettings` and `MovementTuningPreset` (saved tunings, applied by setting name); `Components/` (every part of the movement, as a component) |
| `Assets/MeridianMotion/Runtime/Characters/Animation` | `MMotionAnimator`, `CharacterAnimationState`, the animation translators |
| `Assets/MeridianMotion/Runtime/Characters/IK` | `HumanoidIK`, the IK components on every model, and `HumanoidIKGoals` (IK goal helpers) |
| `Assets/MeridianMotion/Runtime/Characters/Models` | `CharacterModelSlot`, the model sources, `CharacterHitboxes` (the hitboxes on a model's bones), `BodyFitting` and `GenericRigSetup` (what a generic-rig character is made of, for Prepare Generic Rig Models) |
| `Assets/MeridianMotion/Runtime/Characters/Effects` | `WaterEffects` (splashes and ripples, played from the movement's events) and `IWaterEffect` |
| `Assets/MeridianMotion/Runtime/LevelElements` | The level elements, their own assembly: water (and `UnderwaterView`, the camera's fog under it), climbable and painted climbable surfaces, ladders, ropes, ziplines, bounce pads, moving platforms, pushables, the course timer and timing gates, the level clock and stable IDs |
| `Assets/MeridianMotion/Runtime/Cameras` | The camera rig (its own assembly) |
| `Assets/MeridianMotion/Runtime/PhysicsQueries` | The growing query buffers (its own assembly) |
| `Assets/MeridianMotion/Editor` | The editor tools: `Models/` (HumanoidModelImporter, GenericRigModelImporter, HitboxFitter, HitboxLayerSetup, the Character Doctor), `Animation/` (clip importers), `Scene/` (Set Up Scene, New Character), `Effects/` (the default water effects), `Movement/` (the movement mode's inspector, tuning presets, New Ability), `Settings/` (`MeridianMotionAssets`, finding the package's assets by GUID; `MeridianMotionSettings`, Project Settings > Meridian Motion); `LevelElements/` (the level elements' editor assembly: the climbable paint tool, and `LevelFixtureSetup`, which turns a level's named fixtures into level elements and holds the names) |
| `Assets/MeridianMotion/Content` | The assets that ship: `Prefabs/` (Character, Camera Rig, Input Manager, the water effects), `Movement/` (StandardMovementMode, Character Body), `Animation/` (the mannequin's controller, layouts and translator, the generic rig layout, and the Quaternius clip libraries it plays), `Models/Humanoid/Mannequin`, `Materials/` (the water shaders and effects, the unpainted climbable mask), `Input/` (the input actions) |
| `Assets/MeridianMotion/Tests` | `EditMode/` and `PlayMode/` tests, each fixture in a folder (and namespace) for its area: `Movement/`, `InputAndDeterminism/`, `Abilities/`, `Traversal/`, `Animations/`, `Models/`, `Cameras/` (PlayMode only), `Spawning/` (EditMode has only the areas it has fixtures in; its `LevelElements/` holds the level elements' fixtures); `Shared/`; what fixtures share (the rig, `CharacterRigFixture`, `AnimationTestsBase`) at each mode's root |
| `Assets/Showcase/Editor` | `Levels/` (the level builders, one per area; `Graybox/`, the shared pieces and the build; `Specs/`, level specs and the Level Editor), `Pipeline/` (`Mannequin/`: MannequinGenerator; `GenericRigs/`: FoxGenerator; `Blender/`: `BlenderRunner`, finding Blender and running the generators' scripts) and `LevelChecks/` (the movement envelope: `MovementEnvelopeWriter`, `MovementEnvelopeAutoWriter`), each an assembly of its own |
| `Assets/Showcase/Tests/Levels` | The level builders' and level checks' EditMode fixtures (an assembly of their own) |
| `Assets/Showcase` (the rest) | `Levels/` (the level specs and `LevelBuilderSettings.asset`), `Scenes/`, `Materials/` (the levels' and NoiseConcrete), `Models/` (`Generic/Fox`, the sample humanoids in `Humanoid/`, `Level/`), `Prefabs/Fox.prefab`, `Movement/Fox Movement.asset`, `Animation/CMU` (motion capture, which can't ship: its license forbids selling it as animation). It's the project's output folder (Project Settings > Meridian Motion), so what the tools make goes here: prepared models, and `Characters/` (made with New Character, each in a folder of its own: its prefab, a variant of Character.prefab, its movement copy, and its own model source with an animation set; not there until one is made) |
| `Tools` | `run_tests.py` (runs the Unity tests from the command line) and `Tests/` (its tests), `CI/` (hygiene check), `LevelSpec/` (levelspec tool), `Mannequin/` and `Fox/` (Blender scripts), `Blender/` (what they share: `blender_common.py`, the FBX export and posing helpers), `Animation/` (Blender scripts for clip libraries: root motion, rest pose, and taking Quaternius's male mannequin out of its library) |

| Assembly | Folder | Namespace | May reference |
| --- | --- | --- | --- |
| `MeridianMotion.PhysicsQueries` | `Assets/MeridianMotion/Runtime/PhysicsQueries` | `MeridianMotion.PhysicsQueries` | nothing (the growing query buffers, `QueryBuffer`: shared by the character and the camera, so it knows neither) |
| `MeridianMotion.Levels` | `Assets/MeridianMotion/Runtime/LevelElements` | `MeridianMotion.Levels` | MeridianMotion.PhysicsQueries **only** (the level elements: used by the character and the level builders, so they know neither) |
| `MeridianMotion.Levels.EditorTools` | `Assets/MeridianMotion/Editor/LevelElements` | `MeridianMotion.Levels.EditorTools` | MeridianMotion.Levels **only** (the climbable paint tool, `LevelFixtureSetup`) |
| `MeridianMotion.Characters` | `Assets/MeridianMotion/Runtime/Characters` | `MeridianMotion.Characters` | Unity.InputSystem, MeridianMotion.PhysicsQueries, MeridianMotion.Levels |
| `MeridianMotion.Cameras` | `Assets/MeridianMotion/Runtime/Cameras` | `MeridianMotion.Cameras` | Unity.InputSystem, MeridianMotion.PhysicsQueries **only** |
| `MeridianMotion.EditorTools` | `Assets/MeridianMotion/Editor` (except `LevelElements/`) | `MeridianMotion.EditorTools` | MeridianMotion.Characters, MeridianMotion.Cameras, MeridianMotion.Levels, MeridianMotion.Levels.EditorTools |
| `MeridianMotion.Showcase.Pipeline` | `Assets/Showcase/Editor/Pipeline` | `MeridianMotion.Showcase.Pipeline` | MeridianMotion.Characters, MeridianMotion.EditorTools (the generators and `BlenderRunner`; its internals through `InternalsVisibleTo`, in `Assets/MeridianMotion/Editor/AssemblyInfo.cs`) |
| `MeridianMotion.Showcase.LevelChecks` | `Assets/Showcase/Editor/LevelChecks` | `MeridianMotion.Showcase.LevelChecks` | MeridianMotion.Characters, MeridianMotion.EditorTools (the movement envelope; its internals as the pipeline does) |
| `MeridianMotion.Showcase.Levels` | `Assets/Showcase/Editor/Levels` | `MeridianMotion.Showcase.Levels` | Unity.RenderPipelines.Core.Runtime, Unity.RenderPipelines.Universal.Runtime, Unity.TextMeshPro, MeridianMotion.Levels, MeridianMotion.Levels.EditorTools **only** |
| `MeridianMotion.Tests.EditMode` | `Assets/MeridianMotion/Tests/EditMode` | `MeridianMotion.Tests.EditMode`, a fixture in `.EditMode.<Area>` (its folder) | MeridianMotion.Characters, MeridianMotion.EditorTools, MeridianMotion.Tests.Shared, MeridianMotion.PhysicsQueries, MeridianMotion.Levels, MeridianMotion.Levels.EditorTools |
| `MeridianMotion.Showcase.Tests.Levels` | `Assets/Showcase/Tests/Levels` | `MeridianMotion.Showcase.Tests.Levels` | MeridianMotion.Characters, MeridianMotion.EditorTools, MeridianMotion.Tests.Shared, MeridianMotion.Showcase.Levels, MeridianMotion.Showcase.LevelChecks, MeridianMotion.Levels, MeridianMotion.Levels.EditorTools (`LevelEditorTests`, `LevelSpecTests`, `MovementEnvelopeTests`, `GrayboxUV2Tests`) |
| `MeridianMotion.Tests.PlayMode` | `Assets/MeridianMotion/Tests/PlayMode` | `MeridianMotion.Tests` (shared), a fixture in `.<Area>` (its folder) | MeridianMotion.Characters, MeridianMotion.Cameras, MeridianMotion.Tests.Shared, MeridianMotion.PhysicsQueries, MeridianMotion.Levels |
| `MeridianMotion.Tests.Shared` | `Assets/MeridianMotion/Tests/Shared` | `MeridianMotion.Tests` | MeridianMotion.Characters (what both test assemblies use: `TestTimingAttribute`, `TestArea`) |

**Names.** The package is Meridian Motion: every namespace and assembly starts `MeridianMotion.` (the showcase's own, which doesn't
ship, `MeridianMotion.Showcase.`); every component has `[AddComponentMenu("Meridian Motion/<Area>/...")]` and every asset type a
Create menu under "Meridian Motion/<Area>/"; the tools are under Tools > Meridian Motion (the showcase's under its Showcase
submenu). No namespace may be named as a Unity type it would hide inside `MeridianMotion` (`.Cameras`, not `.Camera`;
`.PhysicsQueries`, not `.Physics`; `.EditorTools`, not `.Editor`). Folders are named for their assemblies
(`Runtime/Cameras` holds `MeridianMotion.Cameras`, `Runtime/LevelElements` `MeridianMotion.Levels`). A component's class name must be one other packages are unlikely to use, since Add
Component's search shows bare class names (ReferenceAssets/OpenTasks.md, category E).

The test assemblies also reference Unity's test runner. The hygiene check holds every assembly to this table (its
`ALLOWED_REFERENCES`): a new assembly, or a new reference, goes in both, kept in step.

**The package line.** The character is meant to ship as an Asset Store package without the showcase's levels or its
Blender pipeline: what ships is `Assets/MeridianMotion`, and nothing else, so cutting the package is taking that folder. The
assemblies are split along that line:
- *Ships:* `MeridianMotion.Characters`, `MeridianMotion.PhysicsQueries`, `MeridianMotion.Levels` and its `.EditorTools`,
  `MeridianMotion.EditorTools`, and the test assemblies (`Tests.Shared`, `Tests.EditMode`, `Tests.PlayMode`).
  `MeridianMotion.Cameras` is undecided: it knows nothing of the character, so it can ship or be left out (and the PlayMode
  tests that reference it, `Cameras/`, with it).
- *Doesn't ship* (`Assets/Showcase`): the level builders (`MeridianMotion.Showcase.Levels`), the pipeline
  (`MeridianMotion.Showcase.Pipeline`), the level checks (`MeridianMotion.Showcase.LevelChecks`), their tests
  (`MeridianMotion.Showcase.Tests.Levels`), and their content; and `Tools/`.

So nothing that ships may reference what doesn't. Shipped code reaches the pipeline only through a hook the pipeline
registers with (`HumanoidModelPostprocessor.BeforePreparing`, which `MannequinRegeneration` uses); the mannequin's paths
are the character side's (`HumanoidModelImporter.MannequinModelPath`, `.MannequinPrefabPath`), which
`MannequinGenerator` reads; and the tuning presets' inspector finds the Character prefab by Set Up Scene's path
(`CharacterSceneSetup.CharacterPrefabPath`), not the level checks'. Shipped tools find the package's own assets by
their GUIDs (`MeridianMotionAssets`), never a path written in the code, and write what they make into the output folder
(`MeridianMotionSettings`), never into the package (which may be read-only, installed under `Packages/`). The shipped
tests that need the showcase's content (the fox, the levels' materials) skip without it (`CharacterTestRig.FoxPrefab`,
`NoFox`). What makes the animation ready ships: the Animator
layouts and their builder (`AnimatorControllerBuilder`), and the clips' preparation (`AnimationPreparation`, Prepare
Animations), which the generators call after their Blender step. So does setting up a generic-rig character from a model someone
already has: `GenericRigModelImporter` (Prepare Generic Rig Models), `GenericRigSetup` and its bone guessing, paw placement's set-up,
the hitbox bones, the generic-rig body fit (`BodyFitting.GenericRig`), the generic rig layout (`Assets/MeridianMotion/Content/Animation/Generic Rig
Layout.asset`) and making a generic-rig character's prefab (`NewCharacter.SaveVariant`): `FoxGenerator` writes the fox's setup and
calls them. The generators keep only their Blender step (and the data only their model knows: the fox's setup, written
where it's missing), and nothing that ships names Blender or the generators (the hygiene check, check 18).

## How to verify your work

1. **Fast check, no Unity needed (before every commit):** `python3 Tools/CI/check_project_hygiene.py`. It catches
   missing or orphaned `.meta` files, duplicate GUIDs, an assembly referencing what the table above doesn't allow it,
   the camera, the level elements or the level builders using character code, fields the tests read by name that no longer exist (or are
   read as another type), two movement settings sharing a name, a test fixture without `[TestTiming]` or a test area
   (`[Category(TestArea.…)]`), on it or on a class it derives from, a movement asset naming a component class that
   no longer exists (renamed without `[MovedFrom]`), two `CharacterState` or `MovementEvents` members sharing a bit
   (or one that isn't a single bit), `HasFlag` on either (it boxes, every step), a `...FromCommandLine` entry point the docs or tools name that the code doesn't declare, and an `"Assets/..."` path in shipped code (find the asset by its GUID, `MeridianMotionAssets`, or write into the output folder). And it holds every movement component
   to docs/adding-an-ability.md: a `[Tooltip]` on each setting, settings of kinds presets copy (numbers, bools, enums,
   curves), `Traits` and `StateEvents` on a takeover, its states set, a PlayMode fixture naming it (on the ability test
   kit, `AbilityFixture<T>`, unless the script's `NOT_YET_ON_THE_KIT` lists it), its inspector name in movement.md's
   component list and abilities.md or traversal.md, and its place in `DefaultComponents`; each failure names the fix
   and the guide's section, and the script's allow-lists say where a component legitimately differs.
2. **EditMode and PlayMode tests** need the Unity Editor. With Unity installed locally, close the editor and run
   `python3 Tools/run_tests.py --all` (or `--changed`, `--area`, `--filter`: see step 5). It finds the project's Unity
   (from `ProjectVersion.txt`, the Hub's folders, `UNITY_PATH` or `--unity`), refuses while the editor has the project
   open, runs both suites in batch mode and prints what failed, with the results and logs in `Logs/TestRuns/`
   (docs/testing.md, Running them). The raw commands it runs:
   ```
   <Unity> -batchmode -projectPath . -runTests -testPlatform EditMode -testResults EditModeResults.xml
   <Unity> -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults PlayModeResults.xml
   ```
   Without Unity, push a branch or open a pull request: the `Tests` GitHub Actions workflow runs the hygiene check and
   both suites (a new push cancels the run in progress, so batch pushes; Markdown-only and `docs/` changes don't
   trigger it). The Unity suites run only when something they read changed (`Assets/`, `Packages/`, `ProjectSettings/`,
   the movement envelope or level spec schema, or the workflow): a change to the Python tools alone runs the hygiene
   job, which also tests the levelspec tool and the test runner script and syntax-checks every Python script.
3. **A refactor of the movement must leave `GoldenPathTests` passing:** it holds the character's movement, step by step,
   to recorded paths (docs/testing.md, Golden paths). Record them again (`MERIDIAN_MOTION_RECORD_GOLDEN_PATHS=1`)
   only when a change is meant to change the movement, and check the recorded paths' diff.
4. **A change to collision or the ground** (the body, the probes, `MMotionMotor.Collision`/`.Ground`/`.Hover`)
   must keep `GroundContactTests` passing: compare its step traces before and after (Logs/StepTraces, docs/testing.md).
5. **While iterating, run only the fixtures your change can affect** with `-testFilter` (a regex over test names,
   e.g. `"LedgeTests|AbilityAnimationTests"`, or a whole area with `-testCategory`: every fixture is in a `TestArea`, as the docs are), plus those of what depends on it: movement reaches the movement,
   ability and scale fixtures; animation, IK and models reach the `Animation` and `Models` categories; the camera
   reaches `Camera`. `-testCategory` takes the category names (`TestArea`'s: `Animation`, `Camera`, ...), not the
   folders' (`Animations/`, `Cameras/`), and the fixtures in `InputAndDeterminism/` are in the `Movement` category.
   The easy way: `python3 Tools/run_tests.py --changed` maps your git diff to areas by this rule and says which file led
   to which (its `CHANGE_RULES`: keep them in step with this rule); `--area Abilities`, `--filter "LedgeTests|VaultTests"`
   run what you name. Before committing, run both
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

These are the canonical rules; docs/architecture.md summarises them for readers and links here. Keep the two in step.

- **The camera knows nothing about the character.** Nothing in `Assets/MeridianMotion/Runtime/Cameras` may reference the
  `MeridianMotion.Characters` or `MeridianMotion.Levels` namespaces or assemblies; it follows a plain Transform. A camera feature
  that needs character information exposes a generic hook (an interface, an event, a settable value) connected from
  character code.
- **Models come from a `CharacterModelSource`.** `CharacterModelSlot` asks its source for a model (`CreateModelAsync`)
  and gives it back (`ReleaseModel`); it never instantiates or destroys one itself. New ways of getting a model are new
  subclasses.
- **The level knows nothing about the character or the camera.** The level elements (`Assets/MeridianMotion/Runtime/LevelElements`, the
  `MeridianMotion.Levels` assembly) are the level's: the character uses them (its abilities find them, as with
  `MeridianMotion.Levels.SwingRope.All`), never the reverse. The builders in `Assets/Showcase/Editor/Levels` make geometry, lighting and named fixtures
  and markers, and each build attaches the level elements to its fixtures (`LevelFixtureSetup.SetUp`, a step of
  `LevelDefinition`'s build, before saving); their assembly references the level elements and nothing of the character's
  or the camera's. The fixture and marker names are `LevelFixtureSetup`'s constants, the one source both sides use
  (`Graybox`'s are aliases of them). The character side is connected by Set Up Scene (`CharacterSceneSetup`): the spawner, its
  spawn points, the camera, the input manager, and each course timer's racer (the spawner's On Character Spawned to
  `CourseTimer.SetRacer`). It runs `LevelFixtureSetup` too, which leaves what's set up as it is, for a scene built some
  other way.
- **Movement and animation are separate.** Movement produces `MovementStep` and raises `MovementEvents`; animation
  reads `CharacterAnimationState` and receives events through `MMotionAnimator`. Movement code never touches
  Animators, models or IK. Model-specific behaviour belongs in a `AnimationTranslator`.
- **Movement modes are ScriptableObjects made of components.** A `AbilityMovementMode` holds a list of
  `MovementComponent`s (`[SerializeReference]`), each an ability with its own settings, run in list order (higher
  takes over first). Every part of the movement is a component (walking is `WalkingAndRunning`; gravity, the fall
  speed limit, air steering and ground friction are `FallAndAirHandling`); `StandardMovementMode` keeps only the body's
  posture and the presses and speeds its components share. A component whose class name can't say its inspector name
  gets `[MovementComponentName]`. Adding an ability: see docs/adding-an-ability.md.
  - *Components talk through their mode,* never reaching into other abilities or listing them. A component asks its
    `Mode` for shared rules: gravity, air acceleration, the jump and crouch presses, `AbilityInProgress` (or
    `AbilityInProgressExcept<T>` for one that may take over from a T, `AbilityInProgressWith(AbilityTraits)` for one
    that cares what the ability under way does with the character: holds it up, holds on; each ability declares its
    `Traits`), `InterruptOthers`. One shaped by another's settings reads them with `SettingsOf<T>()` (the mode's, on or
    off, else T's defaults); one handing the character to another finds it with `Mode.Get<T>()` (turned-on only).
    Shared state rules (wall jumps) are in `MovementRules`.
  - *A step runs* `AdvanceTimers`, `BeforeAbilities` (gravity), `TakeOver`; then, if nothing took the character,
    `BeforeLocomotion` (crouching), `Locomotion` (ground friction before walking, which reads the momentum it leaves;
    air steering) and `AfterLocomotion` (dashing, then jumping); then posture (held as the first component's `Posture`
    asks: low, or lying) and `FinishStep` (the fall speed limit). Within each point, list order decides.
  - *State lives on the character.* Modes are shared assets, so per-character state lives on the `MMotionMotor`,
    declared beside its component (the state class and its property, on a part of the partial `MMotionMotor`, in
    the component's file).
- **Physics vs. render timing.** Movement runs in `FixedUpdate` (driven by `MMotionCharacter`); visual
  smoothing and animation run in `Update`. Don't move work between them.
- **The character is resized only through `MMotionCharacter.SetScale`,** which scales the model parent, never the
  root. `MMotionMotor.ApplyScale` resizes the body (collision reads the body's size, not transform scale);
  movement multiplies speeds by `SpeedScale` and distances by `HeightScale`; the animation state carries `scale`. New
  size-dependent values scale the same way.
- **Read the physics pose, not the transform.** The character's and platforms' Rigidbodies are interpolated, so a
  Transform holds a lagging render pose. Movement reads `Rigidbody.position` / `rotation` (see `MMotionMotor.GetPhysicsPose`).
  Moving platforms are kinematic Rigidbodies that move first each physics step (`TickedPlatform.MoveOrder`, before
  the characters' FixedUpdate), their pose set at once (a function of `LevelClock.Tick`) so the characters ride and
  query them where they'll be; they're drawn interpolated by themselves. Anything that steps a character in FixedUpdate must run after them.
- **Collision is resolved by `MMotionMotor` with sweeps of the character's body,** not the physics solver.
  Blocked momentum is removed; `MovementStep.collisions` / `previousCollisions` record blocked sides. Every query that
  depends on the body's shape (sweeping it, whether it fits, pushing it out of overlaps, its size) goes through
  `MMotionMotor.Body`, a `CharacterBody` (`PrimitiveBody`: shaped as a capsule, box or sphere, not a collider):
  don't reach for a collider. The character has none: what meets it meets its model's hitboxes (`CharacterHitboxes`, on
  the CharacterHitbox layer, triggers unless set to collide), which no movement or IK query may meet (keep the layer
  out of their masks). A body can be of several parts (a Body Definition's Extra Parts: the fox's back and head);
  its proportions are the union of its parts, so measure the character by them, never by one part. Abilities measure the character by the body's proportions (`StandingHeight`, `FrontExtent`,
  `BackExtent`, `SideExtent`), the one they mean, not by a collider dimension.
- **The body hovers over steps** (Hover Over Steps, on by default): its queries are of the body with its bottom a
  step's height above the feet, which probes keep on the ground (`MMotionMotor.Hover.cs`). So a body swept down
  onto something rests a step above where the feet go: an ability finding where the character will stand puts the feet
  there with `FeetOnGround`, and a sweep down to find a floor reaches `BodyLift` further.
- **Time-since-grounded values update every step, including in the air.** Coyote time, coyote gravity and air
  steering depend on it.
- **Long comments in the code explain past bugs.** Treat them as constraints: keep the behaviour a comment protects,
  or replace it with a test that does.

## Files to treat with care

**Hotspots.** A movement feature lands in its own component file (its state, declared there on a part of
`MMotionMotor`, and its state's events, its own `StateEvents`), and in `StandardMovementMode.DefaultComponents`. If several agents work at once, only one
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
  edit of a value you understand (a tuning number in `Assets/MeridianMotion/Content/Movement/StandardMovementMode.asset` is fine). If
  a task needs inspector wiring you can't do safely, describe the exact change for a person to make.
- The level scenes are generated (see Generated assets) and stored with Git LFS, as are the models and their textures: a
  scene's diff isn't readable, so rebuild a level and look at it rather than reviewing its YAML.

**Generated assets: regenerate, don't edit.** Edit the generator and regenerate. All but the climbable paint masks
(repainted from their surface's inspector only) have `-executeMethod` entry points (see docs/animation.md,
docs/levels.md, docs/level-specs.md, docs/spawning.md). If you can't run Blender or Unity, make the source
change and say regeneration is still needed.

| Generated | Generator |
| --- | --- |
| `Mannequin.fbx` (mesh, rig, clips), the model setup on `Assets/MeridianMotion/Content/Prefabs/Character.prefab` | `MannequinGenerator` (Generate Mannequin), running `Tools/Mannequin/*.py` in Blender, then Prepare Animations |
| Every Animator Controller (`Assets/MeridianMotion/Content/Animation/Mannequin.controller`, `Fox.controller`), and the libraries' clips' floor and swim offsets | `AnimationPreparation` (Prepare Animations), building each from its Animator Layout (`AnimatorControllerBuilder`). The layouts (`Mannequin Layout.asset`, `Fox Layout.asset`) are hand-edited sources |
| `Fox.fbx` (mesh, rig, clips), its clips' import settings, and `Assets/Showcase/Prefabs/Fox.prefab` | `FoxGenerator` (Generate Fox), running `Tools/Fox/*.py` in Blender, then preparing the fox as any generic-rig character (`GenericRigModelImporter`) and saving Fox.prefab (`NewCharacter.SaveVariant`). It makes `Fox Setup.asset`, `Fox Body.asset` and `Assets/Showcase/Movement/Fox Movement.asset` only where they're missing: those are hand-edited |
| A generic-rig character's `[name] Model.prefab`, `[name].asset` and its animation set's controller in `Assets/Showcase/Models/Generic/[name]`; where they're missing, its `[name] Setup.asset` (guessed), `[name] Animation Set.asset` (guessed), translator and `[name] Body.asset` (fitted), which are then hand-edited | `GenericRigModelImporter` (Prepare Generic Rig Models, which runs by itself when a generic-rig character's model, setup or set changes) |
| Every model's `[name] Model.prefab`, `[name].asset` and avatar/T-pose import settings in each `Models/Humanoid` folder (the package's, the mannequin's; the output folder's, the rest) | `HumanoidModelImporter` (Prepare Humanoid Models) |
| Every model prefab's hitboxes (`BoneHitbox (bone)` objects on its bones, and its `CharacterHitboxes`' list of them) | `HitboxFitter` (Fit Hitboxes), which Prepare Humanoid Models and Prepare Generic Rig Models also run on each model they save, keeping its settings |
| A scene's Meridian Motion Setup object (spawner, spawn points, input manager, camera) and its course timers' racer | `CharacterSceneSetup` (Set Up Scene), which also runs `LevelFixtureSetup`, for a scene whose fixtures aren't set up yet |
| The level scenes (`MeridianComplex`, `LevelPlayground`), their `Environment/` assets and baked data, and their level elements (attached to the fixtures, their course timers in a "Course Timers" group) | Their builders in `Assets/Showcase/Editor/Levels`, whose build runs `LevelFixtureSetup` before saving |
| The `MeridianBasin` scene, its `Environment/` assets and baked data, and its level elements (as the builders' are) | Built from its spec, `Assets/Showcase/Levels/MeridianBasin.level.json` (which `Tools/LevelSpec/examples/meridian_basin.py` writes), by `LevelSpecTools` (Build Level from Spec) |
| `Tools/LevelSpec/level-spec.schema.json`, and `Assets/Showcase/Levels/[scene].level.json` for levels with builders | `LevelSpecTools` (Export Level Specs). Specs can also hold Level Editor or levelspec-tool edits: don't re-export over those without asking |
| Climbable paint masks (`Assets/ClimbablePaint`) | A `PaintedClimbableSurface`'s Repaint Masks |
| `Tools/LevelSpec/envelope.json` (the movement checks' limits) | `MovementEnvelopeWriter` (Write Movement Envelope, and on saving the tuning in the editor: `MovementEnvelopeAutoWriter`), in the level checks (`Assets/Showcase/Editor/LevelChecks`), from the default character's capabilities |
| A model source's measured height, and its body (`[name] Body.asset`, unless its Refit On Prepare is off) | `HumanoidModelImporter` (Prepare Humanoid Models) |

The tools keep the IK settings on model prefabs and edits to `Assets/MeridianMotion/Content/Animation/Mannequin Animator Translator.asset`.
New Animator parameters, states and transitions belong in a layout (`Mannequin Layout.asset`, `Fox Layout.asset`: docs/animation.md, Animator layouts), with the reason in its note; new clips of the mannequin's own in the Blender scripts. The levels'
materials (`Assets/Showcase/Materials/Level/Level_[Surface].mat`), `NoiseConcrete.shadergraph`, `SwimmableWater.shader` and
`WaterEffect.shader` are hand-edited: nothing regenerates them. So are the default water effects (`Assets/MeridianMotion/Content/Prefabs/Effects`,
`Assets/MeridianMotion/Content/Materials/Effects`), which Create Water Effects (`WaterEffectAssets`) makes only where they're missing.

## Recipes

**A new movement ability:** start it with New Ability (`NewAbilityWindow`, or `MakeFromCommandLine`), which makes a
working ability with its state, flag, events, place in the order and the modes, kit tests and docs stubs, then follow
[docs/adding-an-ability.md](docs/adding-an-ability.md), the full checklist (the component and its hooks, its state, flag
and events, its place in the mode, animation, level fixtures, tests and docs). Keep the New Ability lines in
`CharacterState.cs` and `MovementEvents.cs`.

**A new character or model:** follow [docs/adding-a-character.md](docs/adding-a-character.md) (a new look for the
humanoid, a character of its own, which New Character makes, or a generic-rig character); check it with the Character Doctor
(`CharacterChecks`, `CharacterDoctorWindow.CheckFromCommandLine`); its failures are in docs/troubleshooting.md's New
characters (a generic-rig character's in its Generic rigs). A generic-rig character is prepared from its `GenericRigSetup` (docs/generic-rigs.md, A generic-rig character of
your own). Every character prefab in the project is spawned and walked by `EveryCharacterTests`.

**A new level mechanic, a new level piece, or a change to a builder:** follow
[docs/adding-level-content.md](docs/adding-level-content.md).

**Retuning the movement:** follow [docs/tuning.md](docs/tuning.md). In short: change the asset, not the code defaults;
saving it in the editor writes the movement envelope again (`MovementEnvelopeAutoWriter`), but a change made outside
the editor needs Write Movement Envelope run, or `MovementEnvelopeTests` fails. No tuning is fixed: tests that need one
set it on the rig. The walk and run speeds suit the locomotion clips (docs/animation.md): retune them together. The
swimming clips are floated at Swimming's Swim Depth, for the height of the body the Character prefab has with its model (its model's, when its host's Body Fit takes it): saving a change to either floats them again by itself in the editor (`AnimationPreparation`); outside it, run Prepare Animations (`AnimationPreparation.PrepareFromCommandLine`).

## Things that aren't obvious

- **Input** flows `InputSystem_Actions.inputactions` → `PlayerInputReader` (a singleton, read each render frame into an
  `InputLatch`) → one `CharacterInputFrame` per physics step (`FrameFor(tick)`) → `MMotionMotor.Step`.
  The frame is unmanaged and says only what's held, the move input and the look. The character works out presses and
  hold durations itself, counted in steps (`InputState`), and its components read them as `StepInputs` (`InputState.Current`),
  so the same frames give the same movement at any frame rate. Don't read time or the device in the movement. One-off
  presses go through a `PressBuffer`. A new button is:
  - a flag in `InputButtons`;
  - a button on `StepInputs` and in `InputState` (its `Buttons` list, and `Advance`);
  - a line in `CharacterInputFrame.Holding`;
  - an action read in `PlayerInputReader` (`FindActions`, then `ReadButton` in `Update`).

  `Attack` is read into the frame but used by nothing. Camera input is separate (`CameraInputReader`). An
  `CharacterInputRecorder` on a character records its frames, which can be saved and replayed (`InputRecording`).
- **The movement never reads a camera.** The move input is relative to the input frame's look
  (`CharacterInputFrame.lookYaw`/`lookPitch`, `MovementStep.look`), which `PlayerInputReader` fills from the main camera
  for the player.
- **What drives a character** is its input source (`ICharacterInputSource`; components derive from
  `CharacterInputSource`): `MMotionCharacter.InputSource`, the one on the character, else its owner's
  (`MMotionCharacter.ControlledBy`, a `ControlledBy`). The local player's reads the device (`PlayerInputSource`). An
  AI's stands idle without one. A remote one without one isn't stepped at all: something else places it. The sources
  are `ScriptedInputSource` (timed steps, or a recording) and `WaypointInputSource` (an AI walking a route, jumping gaps
  and what the capabilities say it can get up). `CharacterSpawner` hands only the local player's character to the
  camera, and spawns NPCs from its routes (`Npcs`, `SpawnNpc`). `MMotionCharacter.StepMovement` is one step, from
  the source: the game's FixedUpdate and the test rig both call it.
- **The body doesn't wait for the model.** With Body Fit on, `CharacterModelSlot` fits the body its source was prepared
  with (`BodyFor`) in `Start`, before the first step, and keeps it when the model arrives, so a character moves the same
  body with its model, before it, or without one (`LoadModelOnStart` off, as a server would run it).
- **Movement times things in ticks, by `MMotionMotor.StepIndex`,** its own clock in whole steps, never
  `Time.fixedTime`: keep the tick something happened at (`NeverTick` for never) and ask `SecondsSince(tick)`. The step's
  length is given (`Step(frame, stepLength)`), not read. So timing is exact however long a character
  runs (`SetClock` starts it anywhere), and a copy stepped faster than real time (`MovementSimulation`, which measures
  `Capabilities`), or restored and replayed, behaves the same. Don't read global time in movement code.
- **The movement is deterministic on one build:** the same state, frames and world give bit-identical steps
  (`DeterminismTests`).
  - Where two candidates are equally good (two hits as near, two ropes), choose by `StableOrder`, never by the order
    results came in or instance IDs.
  - A character's state can be saved and put back between steps (`MMotionMotor.SaveState`, `RestoreState`;
    `Summarize` gives the compact, unmanaged part), the level's ropes and pushables with it (`WorldStateSnapshot`).
  - It can also be written to bytes and read into a character of the same prefab in a level built the same way
    (`StateSerializer`: the level's objects named by `MeridianMotion.Levels.StableId`, the character's own by their paths from it,
    assets kept). A new field on a character's state is carried by both without changes, if it's a plain value or
    object; a reference to an asset or setting is left as the reader's.
  - PhysX's Enhanced Determinism is on (`ProjectSettings/DynamicsManager.asset`).
  - The level's mechanics that move a character go by `MeridianMotion.Levels.LevelClock` (a tick), not their own time since they were
    enabled.
- **Abilities query the world through the `MMotionMotor`** (`SweepBody`, `Raycast`, `OverlapSphere`,
  `ClosestPointOn`, `CanFitAt`, ...), which skip the character's own colliders and its `IgnoredCollisionRoot`.
  `SetPosture` holds the body standing, low or lying (a `BodyPosture`), keeping the feet in place.
- **Ropes, ziplines and water have no colliders:** abilities find them through `MeridianMotion.Levels.SwingRope.All` / `MeridianMotion.Levels.ZiplineCable.All`
  / `MeridianMotion.Levels.SwimmableWater.All` (the character stands on what's built under water). A
  rope swings itself when let go and is drawn interpolated between physics steps, so the hands IK'd onto it stay on it.
  Bounce pad and jump launch speeds are solved for the stepped physics (`LaunchSpeedFor`), so heights are exact.
- **Climbable surfaces** are `Level.ClimbableSurface` (a collider or its parent) or painted cells of a
  `PaintedClimbableSurface`, which also paints a mask into the mesh's UV2 that the NoiseConcrete shader blends a
  climbable look by. Ladders are climbed by the same ability, up and down their middle, animated by `LadderIK`.
- **Spawn points** are spawn markers ("Spawn Point (…)") that `CharacterSceneSetup` turns into the spawner's spawn
  points. Their name is `LevelFixtureSetup.SpawnMarkerPrefix`, which `CharacterSceneSetup.SpawnMarkerPrefix` and
  `Graybox.SpawnMarkerPrefix` both are.
- **Test rig:** `CharacterTestRig` spawns the real Character prefab with `MMotionCharacter` disabled, steps it
  itself through the controller (`StepMovement`) with scripted inputs (its own input source; `rig.RunWithSource` for a
  source of the test's, an AI's say) at a sped-up time scale (same physics step), and moves it with its
  own copy of the movement mode: change settings with `rig.SetMovementSetting`, never on the asset. It owns the level's
  clock while it's in use (`LevelClock.TickSource`) and resets it on `Dispose`; a test without the rig that sets it
  must set it back to null. Add helpers there rather than repeating setup in tests.

## Code style

Match the existing code:
- XML doc comments (`/// <summary>`) on classes and non-trivial methods.
- `[Tooltip]` on every serialized field, written for a designer; fields grouped with `[Header]`.
- Comments explain *why*, especially non-obvious physics or timing decisions, in full sentences.
- No allocation in per-step code: reuse buffers (see `castHits`, `overlaps`). A physics query's buffer is a `RaycastHitBuffer` or
  `ColliderBuffer` (`MeridianMotion.PhysicsQueries`), never a bare array: Unity's results are unordered, so a full array can leave out the
  nearest hit; these grow (to `QueryBuffer.MaxSize`) and query again, reporting it in development builds.
- PascalCase types and methods, camelCase fields, the namespaces in the table above.
- Big classes are partial, a file per area of responsibility (`MMotionMotor.<Area>.cs`, `Graybox.<Kind>.cs`,
  `MannequinGenerator.<Part>.cs`): add to the file that fits, or start a new one, rather than growing one file.

## Git

- Work on a branch, based on `main`; don't commit to `main` directly. Keep commits focused, with messages that say what
  changed and why.
- Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Builds/` or test results (the `.gitignore` covers
  most of these).
- `ReferenceAssets/` (at the root, ignored by git) holds reference material for you: read from it, but don't commit,
  move or edit what's in it (except `OpenTasks.md` and `OpenTasksArchive.md`: see Open tasks).
