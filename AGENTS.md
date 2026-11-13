# AGENTS.md

Instructions for AI agents (and people) changing this repository. Read this before editing anything. README.md
describes what the project does; this file describes how to change it safely.

## The project at a glance

- A Unity character controller showcase: movement, collision, moving ground, dash, a standalone camera rig, a
  generated humanoid mannequin with animations and IK, and
  a pipeline for using other rigged humanoid models (`Assets/Models/CharacterModels`).
- A generated graybox test level, the Meridian Complex (`Assets/Scenes/MeridianComplex`), built by editor tools in
  `Assets/Editor/Level` (see README, "Test Level").
- **Unity version: 6000.6.7f1.** `ProjectSettings/ProjectVersion.txt` is authoritative. Never change it, and never
  edit `Packages/manifest.json` or `Packages/packages-lock.json` unless the task explicitly asks for a package change.
- About 6,000 lines of runtime C# (plus about 4,200 lines of tests and 4,800 of editor tools), in two runtime
  assemblies, two editor assemblies and two test assemblies:

| Assembly | Folder | Namespace | May reference |
| --- | --- | --- | --- |
| `CharacterShowcase` | `Assets/Scripts` (except `CameraSystem/`) | `Character`, `Level` | Unity.InputSystem |
| `CameraSystem` | `Assets/Scripts/CameraSystem` | `CameraSystem` | Unity.InputSystem **only** |
| `CharacterShowcase.Editor` | `Assets/Editor` (except `Level/`) | `CharacterShowcase.EditorTools` | CharacterShowcase, CameraSystem |
| `CharacterShowcase.Level.Editor` | `Assets/Editor/Level` | `CharacterShowcase.EditorTools.Level` | Unity.RenderPipelines.Core.Runtime, Unity.RenderPipelines.Universal.Runtime, Unity.TextMeshPro **only** |
| `CharacterShowcase.Tests.EditMode` | `Assets/Tests/EditMode` | `CharacterShowcase.Tests.EditMode` | CharacterShowcase, CharacterShowcase.Editor |
| `CharacterShowcase.Tests.PlayMode` | `Assets/Tests/PlayMode` | `CharacterShowcase.Tests` | CharacterShowcase, CameraSystem |

## How to verify your work

1. **Fast check, no Unity needed (run before every commit):**
   ```
   python3 Tools/CI/check_project_hygiene.py
   ```
   It catches missing or orphaned `.meta` files, duplicate GUIDs, the camera assembly depending on character code,
   and renamed fields the tests read by name.
2. **Edit mode and Play mode tests** need the Unity Editor. If you don't have it, push your branch or open a pull
   request: the `Tests` GitHub Actions workflow runs the hygiene check and both suites, and reports failures as a
   check with per-test annotations. The test results XML is uploaded as an artifact.
   A run takes several minutes, and a new push to the same branch cancels the run in progress, so batch code changes
   into fewer pushes. Changes that only touch Markdown files or `docs/` don't trigger a run.
   With Unity installed locally, close the editor and run:
   ```
   <path to Unity 6000.6.7f1> -batchmode -projectPath . -runTests -testPlatform EditMode -testResults EditModeResults.xml
   <path to Unity 6000.6.7f1> -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults PlayModeResults.xml
   ```
   The tests don't cover the level. After changing the level builders, rebuild the level (see "Useful facts") and
   check it in the editor.
3. **Run only the tests your change can affect while you work; run the full suites only when preparing a commit.**
   A full PlayMode run takes several minutes and most of its tests can't be affected by most changes. While iterating,
   run the fixtures that cover what you touched with `-testFilter` (a regex over test names, e.g.
   `-testFilter "LedgeTests|AbilityAnimationTests"`), plus those of anything that depends on it: movement changes
   reach the movement, ability and scale fixtures; animation, IK and model changes reach `AnimationTests`,
   `AbilityAnimationTests`, `ModelTests`, `ModelVariantTests` and the EditMode model checks; camera changes reach
   `CameraRigTests`. Before committing (or when asked for a commit), run both full suites and the hygiene check, and
   only commit on green.
4. If you could not run the tests, say so plainly in your summary. Do not describe untested changes as working.

## Definition of done

- The hygiene check passes, the tests the change can affect pass, and, before a commit, the full EditMode and
  PlayMode suites pass (or you have stated that you could not run them).
- New behaviour has PlayMode tests in `Assets/Tests/PlayMode`, built on `CharacterTestRig` (character) or the
  camera tests' plain-Transform setup (camera). Tests read expected values from the movement mode's settings
  (`rig.MovementSetting<T>("fieldName")`) rather than hard-coding tuning numbers, so retuning doesn't break them.
- README.md is updated when behaviour, controls, tuning options or workflows change. The README is part of the
  product here; it is how the project is presented.
- No unrelated changes: no reformatting of untouched code, no drive-by renames, no package or version bumps.

## Architectural rules (do not break these)

- **The camera knows nothing about the character.** Nothing in `Assets/Scripts/CameraSystem` may reference the
  `Character`, `Level` or `CharacterShowcase` namespaces or assemblies. The camera follows a plain Transform. If a
  camera feature needs character information, expose a generic hook on the camera side (an interface, an event, a
  settable value) and connect it from character code.
- **Models come from a `CharacterModelSource`.** The character's `CharacterModelHost` asks its source for a model with
  `CreateModelAsync` and gives it back with `ReleaseModel`; it never instantiates or destroys a model itself, so sources can load models
  however they like (directly, Addressables, asset bundles). New ways of getting a model are new subclasses.
- **The level knows nothing about the character or the camera.** The level builders in `Assets/Editor/Level` make
  level geometry, lighting and markers only, and their assembly references no project code. Keep game setup (the
  character, cameras, spawn points with behaviour) out of them; connect it from the character side.
- **Movement and animation are separate.** Movement code produces `MovementData` and raises `MovementEvents`.
  Animation reads `CharacterAnimationState` and receives events through `AnimationController`. Movement code must not
  touch Animators, models or IK. Model-specific behaviour belongs in a `CharacterAnimationTranslator`.
- **Movement modes are ScriptableObjects implementing capability interfaces** (`WalkRunMode`, `JumpMode`,
  `DashMode`, `CrouchMode`, `SlideMode`, `RollMode`, `WallRunningMode`, `WallSlideMode`, `LedgeMode`, `VaultMode`, `WallClimbMode`, `PushPullMode` in
  `MovementMode.cs`). New movement abilities follow the same pattern: an interface for the capability, implemented by
  `BasicMovementMode`, with tunables as serialized fields on the asset. `BasicMovementMode` is a partial class: walking,
  jumping and dashing are in `BasicMovementMode.cs`, and each other ability in `BasicMovementMode.<Ability>.cs` with
  its fields under its own header. A new ability gets a file of its own, a state class on the `MovementController`
  (`MovementAbilityStates.cs`; modes are shared assets), a `CharacterState` flag (its start and end events then come
  from `MovementController.StateEvents`), and a place in the chain in `ProcessMovementData`, which tries the abilities
  most committed first; each returns whether it has the character this step.
- **Physics vs. render timing.** Movement runs in `FixedUpdate` (driven by `ACCharacterController`); visual
  smoothing and animation run in `Update`. Don't move work between them.
- **The character is resized only through `ACCharacterController.SetScale`,** which scales the model parent, never
  the character's root transform. Everything size-dependent follows from that: `MovementController.ApplyScale` resizes
  the capsule (collision code reads the capsule's own size, not transform scale), movement multiplies speeds by
  `SpeedScale` and distances by `HeightScale`, and the animation state carries `scale`. New size-dependent distances or
  speeds should scale the same way; new code must not scale the root.
- **Read the physics pose, not the transform.** The character's and platforms' Rigidbodies are interpolated, so a
  Transform holds a smoothed render pose that lags the simulation. Movement code reads `Rigidbody.position` /
  `rotation` (see `GetGroundPose`). Moving platforms are kinematic Rigidbodies moved in `FixedUpdate`.
- **Collision is resolved by `MovementController` with capsule sweeps,** not by the physics solver. Blocked
  momentum is removed, and `MovementData.collisions` / `previousCollisions` record blocked sides.
- **Time-since-grounded values update every step, including in the air.** Coyote time, coyote gravity and air
  steering all depend on this.
- **Long comments in the code explain past bugs.** When a comment says why something is done a particular way,
  treat it as a constraint. If you need to change that code, keep the behaviour the comment protects or replace it
  with a test that does.

## Files to treat with care

### Hotspots

`Assets/Scripts/MovementController.cs` and `Assets/Scripts/MovementModes/BasicMovementMode.cs` are where almost every
movement feature lands (the ability files beside `BasicMovementMode.cs` less so: each is one ability's). Parallel edits to them will conflict. If several agents are working at once, only one should
own each of these files at a time; others should wait or work elsewhere.

### Serialized field names are part of the interface

- Renaming a serialized field (anything with `[SerializeField]` or public on a MonoBehaviour/ScriptableObject)
  silently drops its saved value in every asset, prefab and scene. If a rename is really needed, add
  `[UnityEngine.Serialization.FormerlySerializedAs("oldName")]`.
- The tests read and set these `BasicMovementMode` fields **by name through reflection**, so renaming them compiles
  fine but fails at runtime: `walkSpeed`, `runSpeed`, `gravityStrength`, `maxFallSpeed`, `coyoteTime`, `maxJumps`,
  `jumpInputBuffer`, `dashSpeed`, `dashDuration`, `dashSpeedCurve`, `dashEndSpeedRetention`, `dashCooldown`,
  `dashInputBuffer`, `dashJumpSpeedRetention`, `maxAirDashes`, `holdToCrouch`, `slideMinStartSpeed`,
  `slideMaxDuration`, `rollSpeedCurve`, `rollCooldown`, `rollInputBuffer`, `wallRunMaxDuration`, `wallRunMinHeight`,
  `wallJumpAwaySpeed`, `wallJumpUpSpeed`, `ledgeClimbDuration`, `ledgeHangHeight`, `ledgeGrabMaxHeight`,
  `ledgeMaxGrabRiseSpeed`, `wallSlideBraking`, `wallSlideMaxAngle`, `wallSlideMaxJumps`, `wallSlideJumpUpSpeed`,
  `wallSlideJumpAwaySpeed`, `ledgeGrabMinHeight`, `vaultRiseDuration`, `vaultSpeedRetention`, `vaultMinSpeed`, `vaultFromGround`, `vaultGroundMaxAngle`, `vaultStepUpHeight`, `wallRunFootHeight`, `wallRunReleaseDelay`, `wallSlideReleaseDelay`,
  `wallJumpSteeringRecovery`, `wallSlideMaxStartRiseSpeed`, `wallRunMaxSteerAngle`, `wallClimbSpeed`, `wallClimbStartMaxAngle`,
  `wallClimbGripHeight`, `wallClimbJumpUpSpeed`, `wallClimbJumpAwaySpeed`, `wallClimbRegrabDelay`, `wallClimbEnterDuration`,
  `wallClimbExitDuration`, `landingRollMinFall`. The hygiene check (which reads every `BasicMovementMode*.cs`) catches a missing one.

### Unity asset files (.meta, .prefab, .unity, .asset, .controller)

- **Every file and folder under `Assets/` needs a `.meta` file** with a unique 32-hex-digit GUID. When you add a
  file without the editor, add its `.meta` too: copy the shape of a neighbouring `.meta` of the same type and
  generate a fresh random GUID (e.g. `python3 -c "import uuid; print(uuid.uuid4().hex)"`). Never copy a GUID. When
  you delete or move a file, delete or move its `.meta` with it.
- **Avoid hand-editing `.prefab`, `.unity` and `.asset` YAML.** They reference scripts and assets by GUID and fileID,
  and mistakes fail silently (a missing script, a null reference). Prefer, in order:
  1. Code that doesn't need asset changes (sensible defaults in field initializers, `GetComponent` fallbacks in
     `Awake`, as `MovementController` does for its capsule).
  2. An editor script in `Assets/Editor` that makes the change through Unity's API, which can be run in batch mode
     with `-executeMethod`.
  3. A minimal, careful YAML edit of a value on an existing object, only when you understand the surrounding YAML.
     Changing a tuning number in `Assets/ScriptableObjects/BasicMovementMode.asset` is fine.
  If a task needs inspector wiring you can't do safely, say so and describe the exact change for a person to make.
- `Assets/Scenes/SampleScene.unity` is the playable demo. Don't modify it unless the task is about the scene.
- `Assets/Scenes/MeridianComplex/MeridianComplex.unity` is generated (see below): never edit it by hand. Its Game
  Setup object (character spawner, input manager and camera) is added afterwards by `SceneGameSetup` (the Set Up Scene
  window), from the spawn markers the builders place, since the level builders must not hold game setup.

### Generated assets: regenerate, don't edit

These are produced by tools. Edit the generator instead and regenerate:

| Generated | Generator |
| --- | --- |
| `Assets/Models/CharacterModels/Mannequin/Mannequin.fbx` (mesh, rig, animation clips) | `Tools/Mannequin/generate_mannequin.py` and `mannequin_animations.py`, run in Blender by `MannequinGenerator` (Tools > Character Showcase > Generate Mannequin) |
| `Assets/Animation/Mannequin.controller` | `MannequinGenerator` |
| The model setup on `Assets/Prefabs/Character.prefab` (always the mannequin) | `MannequinGenerator` |
| `[name] Model.prefab`, `[name].asset` and the avatar/T-pose import settings of every model in `Assets/Models/CharacterModels` | `CharacterModelImporter` (Tools > Character Showcase > Prepare Character Models) |
| The Game Setup object in a scene (a `CharacterSpawner` wired to the camera, and copies of SampleScene's InputManager and Main Camera if the scene has none), including the character, model and movement mode the scene spawns, and the replacement of characters placed in it | `SceneGameSetup` (Tools > Character Showcase > Set Up Scene…, or `-executeMethod CharacterShowcase.EditorTools.SceneGameSetup.SetUpFromCommandLine -scene <path>`) |
| `Assets/Scenes/MeridianComplex/MeridianComplex.unity`, its `Environment/` assets (sky, post-processing, lighting settings) and its baked `MeridianComplex/` lighting data | `MeridianComplexBuilder` (Tools > Character Showcase > Level > Build Meridian Complex), which calls one builder per area in `Assets/Editor/Level` |
| `Assets/Scenes/LevelPlayground/LevelPlayground.unity`, its `Environment/` assets and baked `LevelPlayground/` lighting data | `LevelPlaygroundBuilder` (Tools > Character Showcase > Level > Build Level Playground) |
| `Assets/Scenes/ProvingGround/ProvingGround.unity`, its `Environment/` assets and baked `ProvingGround/` lighting data | `ProvingGroundBuilder` (Tools > Character Showcase > Level > Build Proving Ground) |
| `Tools/LevelSpec/level-spec.schema.json` | `LevelSpecTools` (Tools > Character Showcase > Level > Specs > Write Spec Schema, and every export), from the `[Piece]` methods |
| `Assets/Levels/[scene].level.json` (level specs) for levels with builders | `LevelSpecTools` (Export Level Specs), recorded from the builders. Specs are also edited in the Level Editor (`LevelEditorWindow`) or written by the levelspec tool: those edits live in the spec, not a builder, so don't re-export over them without asking |
| A level's moving platforms, pushable crates and timing gates made to work (components, course timers under the Game Setup, displays wired) | `LevelFixtureSetup`, run by `SceneGameSetup` from the fixtures' names |

The tools keep IK settings on model prefabs and edits to `Assets/Animation/Mannequin Animator Translator.asset` and
the model sources. New Animator parameters or states belong in `MannequinGenerator.cs`; new animation clips belong
in the Blender scripts. If you can't run Blender or Unity, make the source changes, and state that regeneration is
still needed. All three tools have `-executeMethod` entry points (see README), so they can run headless.

Climbable paint masks (`Assets/ClimbablePaint/...`) are written by the paint tool from a `PaintedClimbableSurface`'s
cells: repaint them (the component's Repaint Masks button), don't edit them.

The levels' materials (`Assets/Materials/Level/Level_[Surface].mat`, one per `Graybox.Surface`, shared by both level
scenes) are the exception: `GrayboxScene` makes each only if it's missing, so they can be edited by hand. They use
`Assets/Materials/NoiseConcrete.shadergraph`, which is edited in the Shader Graph editor (nothing generates it).

## Useful facts for common tasks

- **Input** flows `InputSystem_Actions.inputactions` → `ACInputManager` (a singleton reading the `Player` action
  map) → `Inputs` / `InputButton` (in `CharacterUtilities.cs`) → `MovementController.ProcessMovementInputs`. The
  action map also defines `Attack`, which nothing reads yet. `Crouch` and `Roll` are read as `Inputs.crouch` and
  `Inputs.roll`, and `Interact` (a plain button, held) as `Inputs.grab`. New buttons are read inside `ACInputManager`
  with its private `ReadButton` helper, the way `Dash` is. One-off presses of ability buttons go through a
  `ButtonPress` (press buffer, one use per press), since a press can last several physics steps or fall between them. Camera input
  is read separately by `CameraSystem/CameraInputReader`.
- **Sprint** is `Inputs.sprint` (read like `Dash`), held to move at `runSpeed` instead of `walkSpeed`:
  `WalkRunMode.MoveSpeed(inputs)` picks the speed, for walking and air steering alike. The speeds suit the
  animations (README, "Walking and Sprinting"): sprinting shows the Quaternius jog at its measured 5.79 m/s, and
  the Locomotion blend tree's speeds are each loop's measured strides plus `MannequinGenerator`'s `BriskWalkUpTo`,
  so retune them together. The in-between gaits (`brisk_walk`, `run`) and the fallback clips are in
  `Tools/Mannequin/mannequin_animations.py`; after changing them, regenerate the mannequin with Blender.
- **Quaternius clips** (`Assets/Animation/Quaternius`, CC0, LFS) are prepared by `QuaterniusAnimationImporter`
  and found with `QuaterniusAnimationImporter.FindClip("Walk_Loop")`. Generate Mannequin stores their root height
  offsets (a positive `heightOffset` lowers a clip) so they share the mannequin's floor: don't hand-edit them.
  Without the libraries the generator falls back to the mannequin's own clips.
- **Known couplings that block NPCs:** `MovementController.cameraTransform` uses `Camera.main`, and
  `ACCharacterController` reads `ACInputManager.instance`. `ACCharacterController.Setup` and `UpdateCaches` are empty
  placeholders.
- **New movement events** go in the `MovementEvents` flags enum. Movement modes report events they cause;
  `MovementController.DetectEvents` detects the ones every mode shares, including each ability's start and end, from
  its `CharacterState` flag (`StateEvents`).
- **Abilities query the world through the `MovementController`** (`SweepCapsule`, `Raycast`, `OverlapSphere`,
  `ClosestPointOn`, `CanFitAt`, `StandingHeight`, `Radius`, `Skin`), which skip the character's own colliders and its `IgnoredCollisionRoot` (an
  object it holds). Crouching lowers the collider with `SetHeightFraction`, which keeps the feet in place and only
  raises it where there's room. `MovementMode.AfterMovementResolved` runs once the step's movement is resolved, for
  moving something along with the character (a held `Level.PushableObject`).
- **Climbable surfaces** are marked with `Level.ClimbableSurface` (on a collider or a parent: all of it), or painted
  with `Level.PaintedClimbableSurface` (cells per object-space face, painted in the Scene view by
  `PaintedClimbableSurfaceEditor`). Climbing asks `ClimbableSurface.IsClimbableAt` about the point of each collider
  nearest the grip; wall climbing (`BasicMovementMode.WallClimb.cs`) climbs those and nothing else. Painting also
  paints a mask per renderer in the mesh's UV2 (`PaintedClimbableSurface.RebuildMasks`, saved by
  `ClimbablePaintAssets` in `Assets/ClimbablePaint`), passed as `_ClimbableMask` to materials that have it: the
  NoiseConcrete shader blends a "Climbable" twin of each look setting by it (the twins were added to the graph's JSON
  by a script, laid out beside the properties they blend; tidy them in the Shader Graph editor freely). Unpainted
  materials share `Assets/Materials/ClimbableMask_None.png`. The level's meshes get a UV2 from `GrayboxUV2` (boxes use
  its Graybox Cube asset); new mesh pieces should give theirs one too (`GrayboxMesh.AddTo` does, unless `uv2: false`).
  Wall climbing hands over to the ledge climb (`TryClimbOverTop`) at a walkable top. The level builders can't reference the component, so climbable level geometry
  would be a fixture (see "Level mechanics are fixtures"); none exists yet. Climbing animates with the Quaternius Pro
  library's Climb_*_Loop clips in MannequinGenerator's Wall Climb blend tree (ClimbRight/ClimbUp), each loop placed at
  its measured speed (`MotionCaptureLocomotion.ClimbSpeed`); WallSlideIK moves the climbing body to the wall. Stepping
  on from the ground and off onto it are timed phases of the climb (`WallClimbState.Phase`), shown by Climb Enter/Exit;
  the ledge climb (from hanging or a wall climb) plays ClimbLedge. Ladders (`Level.Ladder`) are climbed by the same
  ability (its contact carries the ladder: up and down only, on its middle, from its front) and animated procedurally
  by `LadderIK` (on every model prefab, added by `CharacterModelImporter`) over the Animator's Ladder state.
- **Ability animations** are Animator states built by `MannequinGenerator.BuildAbilityStates`, driven by the
  translator's Bool parameters (`Crouching`, `Sliding`, ...) and `GrabSpeed`, `RollProgress`, `ClimbProgress`; the
  `Crouch`, `Hang` and `Wall Slide` clips are the mannequin's own (`mannequin_animations.py`), their hand spread and
  height checked against the Quaternius climbs (the libraries have no hang or wall slide). Hands on a ledge (and the planted hand of a vault) are `LedgeHandIK`, and on a wall slid down
  `WallSlideIK` (both on every model prefab, added by `CharacterModelImporter` like `FootPlacementIK`), placed from
  `CharacterAnimationState.ledgeEdge` / `ledgeNormal` and `wallPoint` / `wallNormal`. `AnimatorParameterTranslator` can fire
  Animator triggers on them.
- **Spawn points in the level** are spawn markers: empty objects named "Spawn Point (…)" made by `Graybox.SpawnMarker`,
  facing their forward. `SceneGameSetup` turns them into the spawner's spawn points (`SpawnMarkerPrefix` must match
  `Graybox.SpawnMarkerPrefix`; the level assembly can't reference it). Rebuilding the level removes its game setup:
  set it up again afterwards.
- **Level mechanics are fixtures** the same way: `Graybox.Mechanics` builds a moving platform, pushable crate,
  ladder or timing gate (and its display) as geometry named "Moving Platform (…) | key=value | …", "Pushable Crate | …",
  "Ladder | height=… | …", "Timing Gate | course=… | role=… | distance=…" and "Timing Display | course=…", and
  `LevelFixtureSetup` (run by `SceneGameSetup`) adds `Level.MovingPlatform`, `Level.PushableObject`, `Level.Ladder`
  (keeping the level's mesh), `Level.TimingGate` and a `Level.CourseTimer` per course from them. Ladders match only
  "Ladder |", since "Ladder (future): …" markers exist; a ladder's rung layout is `Ladder.RungCountFor`'s in both. The prefixes in `Graybox` and `LevelFixtureSetup` must match. A new mechanic is a fixture
  builder in `Graybox.Mechanics`, a set-up step in `LevelFixtureSetup`, an example in the Level Playground and a
  `LevelFixtureSetupTests` case. Displays are wired through their `text` setter by reflection, so the editor
  assembly needn't reference TextMeshPro.
- **Level specs record the builders' piece calls.** Every `Graybox` piece a builder can call is marked `[Piece]`
  and starts with `using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Piece), arg1, arg2, …);`,
  its arguments in parameter order (pieces it calls itself aren't recorded). A new piece needs both, and parameters
  of types `LevelSpec.Encode`/`Decode` handle (add a type there and in `LevelSpecTools.Schema` if needed). Builders
  must make everything through pieces: no moving, renaming or deleting what a piece made afterwards (place a group
  with `Graybox.Placed` or `Turned`, not by setting its position), no `System.Random` other than what `Tree`/`Planter`
  seed from, and no names with `/` or `#` on groups used as parents. Changes to the stair or deck dimensions
  (`Graybox.StepRise` etc.) are recorded as "State" entries; a new such field goes in `LevelSpec.StateFields`.
  After changing a builder, re-export the specs (`LevelSpecTools.ExportFromCommandLine -force`, which fails if a
  spec doesn't rebuild its level exactly; without `-force` it keeps specs changed since they were exported, which
  may hold Level Editor edits: ask before replacing those); `LevelSpecTests` check each level round-trips. Pieces
  that look at what's round them (floors, room for trees and lawns) must do so in deferred placements
  (`Graybox.Defer`): the Level Editor's preview redoes those after every edit, which is what keeps a one-piece
  rebuild the same as a full build (`LevelEditorTests`).
- **The levelspec tool** (`Tools/LevelSpec`, Python 3 standard library only, so CI's hygiene job can run its tests)
  reads the schema Unity writes, so new pieces are known to it once the schema's rewritten. What it knows of each
  piece's meaning is in `levelspec/geometry.py` (each piece's outline, for schematics and the movement checks): give a
  new piece an outline there. The movement limits are `Tools/LevelSpec/envelope.json`, measured with the movement
  code: revise them when the tuning changes. Its tests: `python3 -m unittest discover -s Tools/LevelSpec/tests`.
- **The Proving Ground's layout is graded to the movement's tuning** (gap widths, ledge heights, wall-run lengths,
  low clearances: see the notes in `ProvingGroundBuilder`). If a change to the movement or its tuning moves those
  limits, say so and revise the layout with it.
- **The level** is rebuilt with `-executeMethod CharacterShowcase.EditorTools.Level.MeridianComplexBuilder.BuildFromCommandLine`
  (Unity closed; it also bakes the environment lighting, a few seconds: level geometry isn't Contribute GI, which
  made the baker pack lightmaps for every piece and took about seven minutes). The menu item builds the same steps
  (`GrayboxScene.Steps`) one per editor update, with `LevelBuildWindow` showing progress and the bake run
  by `Lightmapping.BakeAsync`. `GrayboxScene` is the build both level scenes share (new scene, material, a part per
  builder, environment, save, bake); `MeridianComplexBuilder.Scene` lists the complex's parts. Each area has a builder with
  `BuildInto(Transform complex, Material material)`, listed as one of `MeridianComplexBuilder.Scene`'s parts; a new area is a new
  builder listed there, joined at the existing areas' connection markers. Builders share `Graybox`'s pieces
  (flights of stairs, ramps, bridges, guardrails, walls with openings, underground cells and their lighting, shaft
  walls, blocks, the ground, markers): put a piece used in more than one place there rather than in a builder. Each
  piece gives its parts their palette surfaces (`Graybox.Surface`); a piece made of several (a flight, a guardrail) is one
  mesh with a submesh per surface (`GrayboxMesh`). Visual detail never changes collision: trims get no collider
  (`Graybox.Trim`), and a guardrail's collider is one solid box whatever its posts and rails look like. `Graybox` is
  a partial class: the basic pieces are in `Graybox.cs`, architectural detail (wall pilasters and opening frames,
  platform copings, chamfered columns) in `Graybox.Detail.cs`, and structure (I-beam columns, trusses, braced frames,
  brackets) in `Graybox.Structure.cs`. Detail has no colliders (columns keep their footprint's box); structure that can
  be walked into has one box collider round the whole of it. Detail that meets a walkable top stops just short of it
  (copings end 1 cm below the platform's top), so it never shares a plane with a step or ramp arriving there. Lighting
  (`Graybox.Lighting.cs`: underground ceiling panels, strip lights and their lights, door lights, bollards, floodlights,
  accent posts), utilities (`Graybox.Utilities.cs`: pipe runs, utility stations) and markings and wayfinding
  (`Graybox.Markings.cs`: hazard stripes, corner brackets, floor labels, bridge sign gantries, area signs) are in their
  own files too, as are dressing (`Graybox.Dressing.cs`: banners, guide lines, spawn pads, future-mechanic marks, rulers,
  tower bands, the overhead crane) and foliage (`Graybox.Foliage.cs`: trees, shrubs, planters, lawns and grass
  tufts, scattering). Lawns are most of the level's triangles (the tufts): tune `foliage.tuftDensity` first if it needs
  to be lighter. Tufts are their own objects under each lawn chunk, casting no shadows, with a LOD group that stops
  drawing them beyond `foliage.tuftDrawDistance` (allowing for the quality level's LOD bias).
- **Occlusion culling:** `GrayboxScene` bakes it after the lighting (`StaticOcclusionCulling`, settings in
  `culling`), the build window in the background. Only solid pieces are occluders: pass `occluder: false` to
  `Graybox.MakeStatic` for anything that can be seen through or round (railings, trusses, foliage, decals, fixtures),
  or it may hide what shows through it. What has to
  find the floor under it or room round it is put off with `Graybox.Defer` and placed by `RunDeferred`, which
  `GrayboxScene` runs after every part ("Placing the finishing touches"), using physics queries against the built
  level: spawn markers get pads and markers named "… (future): …" get marks that way. Anything random is seeded
  (`foliage.seed`), so builds repeat. Lettering is TextMeshPro (the levels are for demonstration and stay out of the package, so the level
  assembly may use it; TMP's essential resources are in `Assets/TextMesh Pro`). Text objects aren't marked static.
- **Generation settings:** every number a shared piece is made with is in `GrayboxSettings`
  (`Assets/Editor/Level/GrayboxSettings.asset`, made with the field defaults if it's missing), loaded by `Graybox.Begin`
  and read through `Graybox.Settings`. A new piece's dimensions go there (a field with a `[Tooltip]` under its section),
  not in constants; an area's layout stays in its builder. Daylight fixtures cast no light unless
  `lights.fixturesCastLight` is on: keep the level's real-time light count down.
  **Try level changes in the Level Playground first** (`LevelPlaygroundBuilder`, one of each piece, built with
  `-executeMethod CharacterShowcase.EditorTools.Level.LevelPlaygroundBuilder.BuildFromCommandLine` in a couple of
  seconds), then rebuild the complex; a new kind of piece gets one example in the playground too. Units are metres;
  +Z is north. Each area's notes (`*_Blockout_Notes.md` beside the scene) record how its diagram was read; update them
  with the builder.
- **Level stairs and slopes keep to the character's limits:** steps under its 0.35 m step height (0.3 m, or 0.33 m
  in the Kinetic Gardens), ramps no steeper than 1:4. The Calibration Annex's step and slope tests are the deliberate exceptions.
- **Level collision is all box colliders.** `Graybox.Prism` gives its convex solids box colliders that fill the
  footprint exactly (`FootprintBoxes`), which only holds for footprints whose edges all run at multiples of 45°.
  Keep new prism footprints to that, or check the coverage another way. Openings below ground (stairwells, shafts,
  basements) are cut from the ground slab through each builder's `GroundOpenings`.
- **Test rig:** `CharacterTestRig` spawns the real Character prefab, disables `ACCharacterController`, and calls the
  `MovementController` directly once per physics step with scripted inputs (`Idle()`, `Dash(...)`, etc.). It runs
  at 4x time scale with unchanged physics step size. The spawned character moves with the rig's own copy of its
  movement mode (destroyed with the rig): change settings for a test with `rig.SetMovementSetting("fieldName", value)`,
  never on the asset. `EdgeCaseTuningTests` covers extreme settings. Add helpers there rather than duplicating setup in tests.

## Code style

Match the existing code:
- XML doc comments (`/// <summary>`) on classes and non-trivial methods.
- `[Tooltip]` on every serialized field, written for a designer reading the inspector; group fields with `[Header]`.
- Comments explain *why*, especially non-obvious physics or timing decisions, in full sentences.
- Allocation-free per-step code: reuse buffers (see `castHits`, `overlaps`) rather than allocating in `FixedUpdate`.
- PascalCase types and methods, camelCase fields, the existing namespaces per assembly (see the table above).

## Git

- Work on a branch; don't commit to `main` directly. Keep commits focused, with messages that say what changed and
  why.
- Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Builds/` or `TestResults.xml` (the `.gitignore`
  covers most of these).
- `ReferenceAssets/` (at the root, outside `Assets/`, so Unity doesn't import it) holds reference material put
  there for you to use. It's ignored by git: read from it, but don't commit, move or edit what's in it.
- Base work on `main`.
