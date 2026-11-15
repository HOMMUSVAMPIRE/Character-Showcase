# AGENTS.md

Instructions for AI agents (and people) changing this repository. Read this before editing anything. README.md and
docs/ describe what the project does; this file describes how to change it safely.

## The project at a glance

A Unity character controller showcase: movement and its abilities, a standalone camera rig, a generated humanoid
mannequin with animations and IK, a pipeline for other rigged humanoid models, and generated graybox test levels.

**Unity version:** `ProjectSettings/ProjectVersion.txt` is authoritative. Never change it, and never edit
`Packages/manifest.json` or `Packages/packages-lock.json` unless the task explicitly asks for a package change.

| Folder | What's there |
| --- | --- |
| `Assets/Scripts/Character` | `ACCharacterController`, `ACInputManager`, `CharacterSpawner`, `MovementController` and the types it works on (`MovementData`, `Inputs`, `CharacterState`, `MovementEvents`, `MovementSizeScaling`) |
| `Assets/Scripts/MovementModes` | `MovementMode` and its capability interfaces, `BasicMovementMode` (one partial file per ability), `MovementAbilityStates` |
| `Assets/Scripts/Animation` | `AnimationController`, `CharacterAnimationState`, the animation translators |
| `Assets/Scripts/IK` | `CharacterIK`, the IK components on every model, and `CharacterUtilities` (IK goal helpers) |
| `Assets/Scripts/Models` | `CharacterModelHost` and the model sources |
| `Assets/Scripts/Level` | The level mechanics' runtime components |
| `Assets/Scripts/CameraSystem` | The camera rig (its own assembly) |
| `Assets/Editor` | `Mannequin/` (MannequinGenerator), `Models/` (CharacterModelImporter), `Animation/` (clip importers), `Scene/` (Set Up Scene, level fixtures), `ClimbablePaint/` |
| `Assets/Editor/Level` | The level builders (one per area), `Graybox/` (the shared pieces and the build), `Specs/` (level specs and the Level Editor) |
| `Assets/Tests` | `EditMode/` and `PlayMode/` tests |
| `Tools` | `CI/` (hygiene check), `LevelSpec/` (levelspec tool), `Mannequin/` (Blender scripts) |

| Assembly | Folder | Namespace | May reference |
| --- | --- | --- | --- |
| `CharacterShowcase` | `Assets/Scripts` (except `CameraSystem/`) | `Character`, `Level` | Unity.InputSystem |
| `CameraSystem` | `Assets/Scripts/CameraSystem` | `CameraSystem` | Unity.InputSystem **only** |
| `CharacterShowcase.Editor` | `Assets/Editor` (except `Level/`) | `CharacterShowcase.EditorTools` | CharacterShowcase, CameraSystem |
| `CharacterShowcase.Level.Editor` | `Assets/Editor/Level` | `CharacterShowcase.EditorTools.Level` | Unity.RenderPipelines.Core.Runtime, Unity.RenderPipelines.Universal.Runtime, Unity.TextMeshPro **only** |
| `CharacterShowcase.Tests.EditMode` | `Assets/Tests/EditMode` | `CharacterShowcase.Tests.EditMode` | CharacterShowcase, CharacterShowcase.Editor |
| `CharacterShowcase.Tests.PlayMode` | `Assets/Tests/PlayMode` | `CharacterShowcase.Tests` | CharacterShowcase, CameraSystem |

## How to verify your work

1. **Fast check, no Unity needed (before every commit):** `python3 Tools/CI/check_project_hygiene.py`. It catches
   missing or orphaned `.meta` files, duplicate GUIDs, the camera assembly depending on character code, and fields
   the tests read by name that no longer exist.
2. **EditMode and PlayMode tests** need the Unity Editor. With Unity installed locally, close the editor and run:
   ```
   <Unity> -batchmode -projectPath . -runTests -testPlatform EditMode -testResults EditModeResults.xml
   <Unity> -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults PlayModeResults.xml
   ```
   Without Unity, push a branch or open a pull request: the `Tests` GitHub Actions workflow runs the hygiene check and
   both suites (a new push cancels the run in progress, so batch pushes; Markdown-only and `docs/` changes don't
   trigger it).
3. **While iterating, run only the fixtures your change can affect** with `-testFilter` (a regex over test names,
   e.g. `"LedgeTests|AbilityAnimationTests"`), plus those of what depends on it: movement reaches the movement,
   ability and scale fixtures; animation, IK and models reach `AnimationTests`, `AbilityAnimationTests`, `ModelTests`,
   `ModelVariantTests` and the EditMode model checks; the camera reaches `CameraRigTests`. Before committing, run both
   full suites and the hygiene check, and only commit on green.
4. The tests don't cover the levels' looks: after changing a builder, rebuild the level and look at it.
5. If you couldn't run the tests, say so plainly. Don't describe untested changes as working.

## Definition of done

- The hygiene check passes, the tests the change can affect pass, and, before a commit, both full suites pass (or
  you've said you couldn't run them).
- New behaviour has PlayMode tests, built on `CharacterTestRig` (character) or the camera tests' plain-Transform
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
- **Movement modes are ScriptableObjects implementing capability interfaces** (in `MovementMode.cs`), with tunables
  as serialized fields on the asset. Modes are shared assets, so per-character state lives on the `MovementController`
  (`MovementAbilityStates.cs`). See the recipe for adding an ability.
- **Physics vs. render timing.** Movement runs in `FixedUpdate` (driven by `ACCharacterController`); visual
  smoothing and animation run in `Update`. Don't move work between them.
- **The character is resized only through `ACCharacterController.SetScale`,** which scales the model parent, never the
  root. `MovementController.ApplyScale` resizes the capsule (collision reads the capsule's size, not transform scale);
  movement multiplies speeds by `SpeedScale` and distances by `HeightScale`; the animation state carries `scale`. New
  size-dependent values scale the same way.
- **Read the physics pose, not the transform.** The character's and platforms' Rigidbodies are interpolated, so a
  Transform holds a lagging render pose. Movement reads `Rigidbody.position` / `rotation` (see `GetGroundPose`).
  Moving platforms are kinematic Rigidbodies moved in `FixedUpdate`.
- **Collision is resolved by `MovementController` with capsule sweeps,** not the physics solver. Blocked momentum is
  removed; `MovementData.collisions` / `previousCollisions` record blocked sides.
- **Time-since-grounded values update every step, including in the air.** Coyote time, coyote gravity and air
  steering depend on it.
- **Long comments in the code explain past bugs.** Treat them as constraints: keep the behaviour a comment protects,
  or replace it with a test that does.

## Files to treat with care

**Hotspots.** `Assets/Scripts/Character/MovementController.cs` and `Assets/Scripts/MovementModes/BasicMovementMode.cs`
are where most movement features land. If several agents work at once, only one should own each at a time.

**Serialized field names are part of the interface.** Renaming a serialized field (`[SerializeField]`, or public on a
MonoBehaviour/ScriptableObject) silently drops its saved value everywhere; if a rename is really needed, add
`[UnityEngine.Serialization.FormerlySerializedAs("oldName")]`. Tests also read `BasicMovementMode` fields by name
through reflection, so a rename compiles but fails at runtime: the hygiene check finds every name the tests use and
fails if one disappears. Unity silently drops enum fields wider than 32 bits, which is why a saved set of
`MovementEvents` (64-bit) is a `long` (`AnimatorParameterTranslator.EventTrigger`, drawn by `EventTriggerDrawer`).

**Unity asset files.**
- Every file and folder under `Assets/` needs a `.meta` with a unique GUID. Adding a file outside the editor, copy a
  neighbouring `.meta` of the same type with a fresh GUID (`python3 -c "import uuid; print(uuid.uuid4().hex)"`);
  never copy a GUID. Move or delete a file's `.meta` with it (`git mv` both, which keeps references working).
- Avoid hand-editing `.prefab`, `.unity` and `.asset` YAML. Prefer, in order: code that needs no asset change
  (defaults in field initializers, `GetComponent` fallbacks); an editor script run with `-executeMethod`; a minimal
  edit of a value you understand (a tuning number in `Assets/ScriptableObjects/BasicMovementMode.asset` is fine). If
  a task needs inspector wiring you can't do safely, describe the exact change for a person to make.
- `Assets/Scenes/SampleScene.unity` is the playable demo: don't modify it unless the task is about it.

**Generated assets: regenerate, don't edit.** Edit the generator and regenerate. All have `-executeMethod` entry
points (see docs/animation.md, docs/levels.md, docs/spawning.md). If you can't run Blender or Unity, make the source
change and say regeneration is still needed.

| Generated | Generator |
| --- | --- |
| `Mannequin.fbx` (mesh, rig, clips), `Assets/Animation/Mannequin.controller`, the model setup on `Assets/Prefabs/Character.prefab` | `MannequinGenerator` (Generate Mannequin), running `Tools/Mannequin/*.py` in Blender |
| Every model's `[name] Model.prefab`, `[name].asset` and avatar/T-pose import settings in `Assets/Models/CharacterModels` | `CharacterModelImporter` (Prepare Character Models) |
| A scene's Game Setup object (spawner, input manager, camera), and its fixtures made to work | `SceneGameSetup` (Set Up Scene), which runs `LevelFixtureSetup` |
| The level scenes (`MeridianComplex`, `LevelPlayground`, `ProvingGround`), their `Environment/` assets and baked data | Their builders in `Assets/Editor/Level` |
| `Tools/LevelSpec/level-spec.schema.json`, and `Assets/Levels/[scene].level.json` for levels with builders | `LevelSpecTools` (Export Level Specs). Specs can also hold Level Editor or levelspec-tool edits: don't re-export over those without asking |
| Climbable paint masks (`Assets/ClimbablePaint`) | A `PaintedClimbableSurface`'s Repaint Masks |
| `Tools/LevelSpec/envelope.json` (the movement checks' limits) | `MovementEnvelopeWriter` (Write Movement Envelope), from the default character's capabilities |
| A model source's measured height and shoulder width | `CharacterModelImporter` (Prepare Character Models) |

The tools keep the IK settings on model prefabs and edits to `Assets/Animation/Mannequin Animator Translator.asset`.
New Animator parameters or states belong in `MannequinGenerator`; new clips in the Blender scripts. The levels'
materials (`Assets/Materials/Level/Level_[Surface].mat`) and `NoiseConcrete.shadergraph` are hand-edited: nothing
regenerates them.

## Recipes

**A new movement ability** (rope swinging and ziplines are recent examples to copy):
1. A capability interface in `MovementMode.cs`, added to `BasicMovementMode`'s declaration; its code and settings (under
   their own `[Header]`) in `BasicMovementMode.<Ability>.cs`.
2. A state class in `MovementAbilityStates.cs` and a property for it on `MovementController`.
3. A `CharacterState` flag, `MovementEvents` for its start and end, and an entry in `MovementController.StateEvents`
   (which raises them from the flag).
4. A place in `BasicMovementMode.ProcessMovementData`'s chain (most committed first; each returns whether it has the
   character), in `AbilityInProgress`, and in `AdvanceAbilityTimers` for its cooldowns; `AfterMovementResolved` if it
   moves something with the character.
5. Animation: fields in `CharacterAnimationState` filled by `AnimationController.BuildState`; a parameter on
   `AnimatorParameterTranslator` (default name, field, hash, set); in `MannequinGenerator`, the parameter, the crouch
   state's exclusions and a state in `MannequinGenerator.AbilityStates.cs`; a clip in `mannequin_animations.py` if
   needed; an IK component added in `CharacterModelImporter.SaveModelPrefab` if needed. Then regenerate the mannequin
   and prepare the character models.
6. PlayMode tests, a docs section, and its turned-off-at-0 case.

**A new level mechanic** (a fixture): a piece in `Graybox.Mechanics.cs` naming its geometry "Name | key=value | …"; a
set-up step in `LevelFixtureSetup` (the prefixes in both must match, and match only "Name |", since "(future)" markers
and placeholders are named for mechanics too); an example in the Level Playground; a `LevelFixtureSetupTests` case; an
outline in `Tools/LevelSpec/levelspec/geometry.py`.

**A new level piece:** mark it `[Piece]` and start it with
`using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Piece), arg1, arg2, …);` (arguments in
parameter order), using parameter types `LevelSpec.Encode`/`Decode` handle. Its dimensions go in `GrayboxSettings` (a
field with a `[Tooltip]`), not constants. Give its mesh a UV2 (`GrayboxMesh.AddTo` does unless `uv2: false`). Pieces
that look at what's round them do so in `Graybox.Defer`. Add an example to the Level Playground and an outline in
`geometry.py`. Visual detail never changes collision (trims have no collider; structure gets one box round it), and
only solid pieces are occluders (`MakeStatic(obj, occluder: false)` for anything seen through).

**Changing a builder:** builders make everything through pieces: no moving, renaming or deleting what a piece made
afterwards (place groups with `Graybox.Placed` or `Turned`), no `System.Random` but the seeded ones, no `/` or `#` in
names of groups used as parents. Try it in the Level Playground first (builds in seconds), then rebuild the level,
re-export its spec (`LevelSpecTools.ExportFromCommandLine -level "<Level>"`; `-force` replaces a spec that changed
since its export, so check first that the only differences are yours), and run Set Up Scene on it (rebuilding removes
its game setup). Update the area's `*_Blockout_Notes.md` with the builder. Stairs keep under the character's step
height and ramps to the envelope's ramp gradient, except the Calibration Annex's deliberate tests; prism footprints'
edges run at multiples of 45° (so `FootprintBoxes` fills them exactly).

**Retuning the movement:** change the asset, not the code defaults. Then write the movement envelope again (Write
Movement Envelope: `Tools/LevelSpec/envelope.json`, from the default character's `MovementController.Capabilities`;
`MovementEnvelopeTests` fails until you do), and check the Proving Ground, whose layout is graded to it. A new ability
whose reach matters to levels gets a measurement in `BasicMovementMode.Capabilities.cs` and a `MovementCapabilities` value. The walk and run speeds suit the locomotion clips (docs/animation.md): retune them
together.

## Things that aren't obvious

- **Input** flows `InputSystem_Actions.inputactions` → `ACInputManager` (a singleton) → `Inputs` →
  `MovementController.ProcessMovementInputs`. New buttons are read with `ACInputManager`'s `ReadButton`, the way Dash
  is. One-off presses go through a `ButtonPress`, since a press can last several physics steps or fall between them.
  `Attack` is defined but unread. Camera input is separate (`CameraInputReader`).
- **Couplings that block NPCs:** `ACCharacterController` reads `ACInputManager.instance`. Inputs are relative to
  `MovementController.InputCamera`, falling back to `Camera.main`.
- **Movement times things by `MovementController.StepTime`,** its own clock, never `Time.fixedTime`, so a copy stepped
  faster than real time (`MovementSimulation`, which measures `Capabilities`) behaves the same. Don't read global time
  in movement code.
- **Abilities query the world through the `MovementController`** (`SweepCapsule`, `Raycast`, `OverlapSphere`,
  `ClosestPointOn`, `CanFitAt`, ...), which skip the character's own colliders and its `IgnoredCollisionRoot`.
  `SetHeightFraction` lowers the collider keeping the feet in place.
- **Ropes and ziplines have no colliders:** abilities find them through `Level.Rope.All` / `Level.Zipline.All`. A
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
- Big classes are partial, a file per area of responsibility (`BasicMovementMode.<Ability>.cs`, `Graybox.<Kind>.cs`,
  `MannequinGenerator.<Part>.cs`): add to the file that fits, or start a new one, rather than growing one file.

## Git

- Work on a branch, based on `main`; don't commit to `main` directly. Keep commits focused, with messages that say what
  changed and why.
- Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Builds/` or test results (the `.gitignore` covers
  most of these).
- `ReferenceAssets/` (at the root, ignored by git) holds reference material for you: read from it, but don't commit,
  move or edit what's in it.
