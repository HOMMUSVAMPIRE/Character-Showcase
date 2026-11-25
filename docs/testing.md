# Automated Tests

The tests are written to hold for any team's setup, not just this project's: they read what they need from the character (its body, step height, slope limit, walking and dashing speeds, jump count) and from whatever model, animations and camera settings it has, and size their test worlds from those. Running them (the Test Runner, the runner script, CI), how long each takes, and profiling them are in [Running the Tests](running-tests.md).

*See also:* [Troubleshooting: the tests](troubleshooting.md#the-tests), [Why no tuning is set in stone](decisions.md#no-tuning-is-set-in-stone-november-2026).

Play mode tests run on the package's Character prefab unless a CharacterTestSettings asset (Create > Meridian Motion > Tests > Character Test Settings) names another: put one anywhere in the project to run the whole suite on your own character. A test of something the character doesn't have (an air jump on a character with one jump, foot placement on a model that isn't a humanoid) is skipped, with the reason, rather than failed. They've been run on a character tuned differently (a smaller capsule, a lower step height and slope limit, a much slower walk, a slower dash and a single jump).

Tests of the package's own content (the mannequin and other prepared models, the motion-capture clips, the shared Animator Controller) are in the SampleContent category, and skipped where that content isn't there. A team using its own models and animations can leave them out: in the Test Runner, or with `-testCategory "!SampleContent"` on the command line.

## The test rig
Every character test runs on the test rig, `CharacterTestRig` (Assets/MeridianMotion/Tests/PlayMode). It spawns the character with real physics and drives it with scripted inputs, through its MMotionCharacter (`StepMovement`, as the game steps it).

- **`CharacterRigFixture`** is what a character fixture derives from: each test gets a new rig (`rig`), disposed after it. It also gives the fixture `[TestTiming]` (see [How long each test takes](running-tests.md#how-long-each-test-takes)). `AnimationTestsBase` derives from it for the tests that need the model's animation.
- **`AbilityFixture<T>`** derives from it for a fixture of tests on one ability (T, its movement component): the fixture gets the checks every ability should pass as its own tests, named `Kit_…` (it starts from its inputs, raising its start event; turned off by its toggle and by its 0 setting it doesn't start, and does what the fixture says it should instead (`WhatHappensInstead`); turned off while under way it lets go at once; respawned while under way (`MMotionMotor.Respawn`) it lets go, its end event raised once, and the character stands still where it was put; its events come in pairs; saved just after it starts, and well into it, and put back it steps on exactly as it did; at half and twice its size it still starts). It supplies how to set the ability up (`SetUpToStart`, spawning with the kit's `SpawnCharacter`), its inputs (`InputsFor`) and the setting whose 0 turns it off (`OffSetting`). See [Adding a Movement Ability](adding-an-ability.md#7-tests-and-docs). `DashTests` and `ZiplineTests` are examples, and every ability's fixture derives from it but those of a moment's.
- **`MomentFixture<T>`** is the kit for an ability that happens in a moment (a jump, a bounce: its component's `OneOffEvents`), which supplies its event too (`MomentEvent`). Its `Kit_…` checks: it happens from its inputs; turned off by its toggle and by its 0 setting it doesn't; it doesn't happen again straight away from the same inputs; saved before it, or just after it, and put back it steps on exactly as it did; at half and twice its size it still happens. `JumpHeightTests` and `BouncePadTests` derive from it. What the two kits share is `AbilityKitBase<T>`.
- **Settings.** Each test's character moves with its own copy of its movement mode asset, so a test can change settings without changing the asset, and the asset can be retuned freely: the tests read the values they expect from the copy. `rig.SetMovementSetting("fieldName", value)` changes one, for that test only, and `rig.MovementSetting<T>("fieldName")` reads one (a field of the mode or of any of its components, by name). A setting the mode doesn't have ignores the test (it tests a behaviour that mode may not have); one asked for as the wrong type fails it, so a test can't quietly stop testing anything. The sizes, speeds and tolerances a test tries are in the test itself.
- **Looking.** The rig creates no camera: the movement never reads one, and the scripted inputs carry their own look (`rig.Look`), which their move input is relative to. A test that needs a camera (the camera rig, the underwater view) makes its own.
- **Running.** `rig.RunUntil` runs the character until something's true (or a timeout passes), so a test waits as long as the movement takes, however it's tuned (`rig.Settle()` until it's still, for a set-up; `rig.RunUntilStill(inputs)` until it goes no further). Without the rig, `Waits.WaitUntil(condition)` and `Waits.WaitUntilSettled(value, tolerance)` wait frame by frame (the camera's smoothing, a model loading); `rig.RunWithSource` runs it driven by an input source (an AI's, say), as its MMotionCharacter steps it in the game.
- **A gamepad.** A test pressing a gamepad's buttons uses a `VirtualGamepad` (Assets/MeridianMotion/Tests/PlayMode): the machine's own devices are off until it's disposed, so in the editor the mouse over the Game view or a controller plugged in can't come between the test and its presses, and its `Press` leaves each press for the frame's own input update. Don't call `InputSystem.Update()` after queueing a gamepad press: the UI reads its keys in the frame's Update (`WasPressedThisFrame`), and in the editor an update of the test's own, between frames, isn't the frame's, so the press is missed there though it's seen in batch mode.
- **A second character.** `ReplaceRig()` starts again with a new rig, made as the test's first was, for a test comparing two characters one after the other.
- **The level's clock.** While it's in use the rig drives the level's clock (`LevelClock.TickSource`), so moving platforms and the other mechanics go by its steps.

Add helpers to the rig rather than repeating set-up in tests. What keeps the tests on it quick is in [Writing a fast test](#writing-a-fast-test).

## Play mode tests
Play mode tests in Assets/MeridianMotion/Tests/PlayMode spawn the character with real physics and drive it with scripted inputs, on the test rig (see [The test rig](#the-test-rig)). Each fixture below is one file, named for it (but SampleContentAnimationTests, in AnimationTests.cs), in a folder for its area (Movement, InputAndDeterminism, Abilities, Traversal, Animations, Models, Levels, Cameras, Spawning), and in a namespace of the same name, so the Test Runner's tree groups them by area under each assembly. (Animations and Cameras are plural so the namespaces don't hide Unity's `Animation` and `Camera` types from the tests.) What the fixtures share (the rig, `CharacterRigFixture`, `AnimationTestsBase`, `StepTrace`) is at the root of each, in `MeridianMotion.Tests`, which every area's namespace sees without a `using`. A new fixture goes in its area's folder and namespace, and has the area's `[Category(TestArea.…)]` too, for filtering by area. (InputAndDeterminism isn't a `TestArea`: its fixtures are in the Movement category.)

The settings that turn an ability off at 0 are each tested doing so, in the ability's fixture: the slide's longest time, the wall run's and wall slide's longest times, the ledge climb's duration, the max jump height, the rope's and zipline's grab reach, the bounce pads' height multiplier, and the swim depth.

| Fixture | Folder | Category | What it checks |
| --- | --- | --- | --- |
| GoldenPathTests | Movement | Movement, Abilities, Traversal | the movement, step by step, against recorded paths |
| MovementTests | Movement | Movement | settling, falling, steps, coyote time, the jump buffer |
| SprintTests | Movement | Movement | sprinting and walking again |
| MovementComponentTests | Movement | Movement | components turned off or removed |
| CharacterTestRigTests | (the PlayMode root) | Movement | the test rig itself |
| MovementCapabilitiesTests | Movement | Movement | the measured capabilities matching what the character does |
| JumpHeightTests | Movement | Movement | tapped, held and released jump heights (and the moment kit's checks) |
| GroundMomentumTests | Movement | Movement | momentum on landing and hopping |
| DashTests | Movement | Movement | the dash (and the ability kit's checks) |
| MovingGroundTests | Movement | Movement | riding moving and teleporting ground |
| RespawnTests | Movement | Movement | respawning: momentum, jumps and air dashes, a crouch, held ropes and crates (beyond the kit's) |
| BodyShapeTests | Movement | Movement | bodies of other shapes, lying down |
| FoxTests | Movement | Movement | the fox's movement |
| GroundContactTests | Movement | Movement | keeping to the ground, with step traces |
| CollisionTests | Movement | Movement | ceilings, walls, drops, slopes and pushing walls |
| MovementEventTests | Movement | Movement | the basic moves' events |
| EdgeCaseTuningTests | Movement | Movement | settings at their extremes |
| ScaleTests | Movement | Movement | moving the same at any size |
| AllocationTests | Movement | Movement, Animation, Performance | no allocation per step |
| MovementPerformanceTests | Movement | Movement, Performance | a crowd's step time |
| InputSourceTests | InputAndDeterminism | Movement | input sources: none, scripted, a waypoint AI |
| DeterminismTests | InputAndDeterminism | Movement | moving the same, bit for bit |
| InputRecordingTests | InputAndDeterminism | Movement | a recorded run replayed exactly |
| ReplayTests | InputAndDeterminism | Movement | replays: recorded, saved, played back exactly, kept on course by keyframes |
| ReplayGameLoopTests | InputAndDeterminism | Movement | a replay in the game's own loop, exact with keyframes or without |
| CrouchTests | Abilities | Abilities | crouching |
| SlideTests | Abilities | Abilities | sliding (and the ability kit's checks) |
| RollTests | Abilities | Abilities | rolling (and the ability kit's checks) |
| WallRunTests | Abilities | Abilities | wall running (and the ability kit's checks) |
| WallSlideTests | Abilities | Abilities | wall sliding and wall jumps (and the ability kit's checks) |
| LedgeTests | Abilities | Abilities | hanging from and climbing ledges (and the ability kit's checks) |
| VaultTests | Abilities | Abilities | vaulting (and the ability kit's checks) |
| LadderTests | Traversal | Traversal | ladders |
| WallClimbTests | Traversal | Traversal | climbing walls, painted ones too (and the ability kit's checks) |
| PushPullTests | Traversal | Traversal | pushing and pulling (and the ability kit's checks) |
| RopeSwingTests | Traversal | Traversal | swinging on ropes (and the ability kit's checks) |
| ZiplineTests | Traversal | Traversal | riding ziplines (and the ability kit's checks) |
| SwimTests | Traversal | Traversal | swimming at the surface, getting out (and the ability kit's checks) |
| DiveTests | Traversal | Traversal | diving and swimming underwater |
| WaterEffectsTests | Traversal | Traversal | splashes and ripples |
| WadeTests | Traversal | Traversal | wading (and the ability kit's checks) |
| BouncePadTests | Traversal | Traversal | bounce pads (and the moment kit's checks) |
| AnimationTests | Animations | Animation | soles on the floor, planted feet, IK on stretched models |
| SampleContentAnimationTests | Animations | Animation, SampleContent | every prepared model's feet and lean |
| AnimationEventTests | Animations | Animation | movement events passed on by the MMotionAnimator |
| AnimatorParameterTranslatorTests | Animations | Animation | the translator: its parameters, triggers and playback |
| FootLockTests | Animations | Animation | planted feet staying put |
| VisualRotationTests | Animations | Animation | the model turning and leaning |
| AbilityAnimationTests | Animations | Animation, SampleContent | each ability's Animator state and IK |
| TraversalAnimationTests | Animations | Animation, SampleContent | climbing, ladders, ropes, ziplines, stairs and pushing on the model |
| WaterAnimationTests | Animations | Animation, SampleContent | swimming and diving on the model |
| HeadInWaterTests | Animations | Animation | the head-in-water hook |
| FoxPawPlacementTests | Animations | Animation | the fox's paw placement |
| FoxTerrainClearanceTests | Animations | Animation | the fox's body out of stairs and ramps |
| FoxAbilityTests | Animations | Animation | the fox's Animator states, swimming and wall running |
| ModelTests | Models | Models | model hosting: loading, swapping, fallbacks |
| ModelFitTests | Models | Models | the character fitted to its model, and measuring on load |
| ModelVariantTests | Models | Models | models made facing another way or off their origin |
| HitboxTests | Models | Models | hitboxes |
| TimingGateTests | Levels | Levels | a timed run between gates |
| CourseTimerTests | Levels | Levels | lap gates, the racer and splits |
| CheckpointTests | Levels | Levels | checkpoints, hazards and crates that reset |
| ShortcutGateTests | Levels | Levels | shortcut gates and their triggers |
| LessonTrackerTests, PlaytestSessionTests, TutorialReplayTests, TutorialRingSceneTests, TutorialRingGamepadTests | Levels | Levels | the Tutorial Ring (the showcase's: Assets/Showcase/Tests/TutorialRing) |
| PromptTests | UI | UI | the Tutorial Ring's lesson prompts (the showcase's) |
| UnderwaterViewTests | Levels | Levels | the camera's underwater view |
| CameraRigTests | Cameras | Camera | the camera rig's views and modes |
| CameraCollisionTests | Cameras | Camera | the camera's collision and shoulder swap |
| CameraFollowTests | Cameras | Camera | following the target, at any size, and the landing dip |
| CameraEffectsTests | Cameras | Camera | shake and the speed FOV |
| UIUtilityTests | UI | UI | the UI's helpers and its clock |
| UIManagerTests | UI | UI | opening, reusing and closing windows |
| WindowLifecycleTests | UI | UI | a window's events, close animation and click blocker |
| BackKeyTests | UI | UI | the Back key, its fallback, and what the game is told |
| MessageBoxTests | UI | UI | the dialogs and the text prompt |
| PauseMenuTests | UI | UI | the pause menu |
| ShowcaseHUDTests | Assets/Showcase/Tests/HUD | UI | the showcase's HUD: lap times, the controls hint, NPC name tags |
| DataListTests | UI | UI | data lists: rows, search, selection, categories |
| TabTests | UI | UI | tabs, and the windows they track |
| ContextMenuTests | UI | UI | the context menu |
| NotificationTests | UI | UI | notifications |
| RulesTests | UI | UI | rule-gated controls |
| WorldLabelTests | UI | UI | labels anchored to the world |
| UIContentTests | UI | UI | the UI's own prefabs |
| ShapeTests | UI | UI | the shapes and the theme |
| IconTests | UI | UI | the icons drawn in code, and a theme's sprites for them |
| ThemedWidgetTests | UI | UI | themed buttons and text |
| ControlsListTests | UI | UI | the controls list: bindings by scheme, rebinding, saving |
| GamepadNavigationTests | UI | UI | navigating windows with a gamepad: the first control selected, moving, pressing |
| ControlOptionTests | UI | UI | the controls page's options: the Sprint option's button, held or toggled |
| HUDWidgetTests | UI | UI | the HUD widgets |
| ReplayMenuTests | UI | UI | the pause menu's replays, and a replay's end |
| SpawnerTests | Spawning | Spawning | the character spawner |
| EveryCharacterTests | Spawning | Spawning | every character prefab spawns, stands and walks |

### Movement (the Movement category; see [Movement](movement.md))

#### Golden paths
**GoldenPathTests** holds the character's movement to exactly what it was: a scripted run for each part of it (GoldenPaths/*.csv: walking, sprinting and turning; jumps and an air jump; stairs and a ramp; crouching through a tunnel, a slide and a roll; dashes; a ledge hang and climb; a vault; a wall run and wall jump; a wall climb over the top; a ladder onto a platform; a bounce pad; a zipline ride and jump; a rope swing; swimming, diving and getting out; wading and vaulting in the water; a moving platform; and a small and a large character), each checked step by step against the path recorded for it in Assets/MeridianMotion/Tests/PlayMode/GoldenPaths: where the character is, its facing, velocity, state, events and body height. Any difference fails, naming the run, the first step that differs and what differs, with this run's path written to Logs/GoldenPaths to compare. Each run is checked as the character is (with no collider); those that meet things most (`ContactHeavy`: stairs and the ramp, the vault, the ledge, the moving platform) are checked again with a collider of its body's added, which the movement must ignore (`FollowsItsRecordedPath_WithACollider`; see [Collision](movement.md#collision)). The golden paths are in the Movement, Abilities and Traversal areas, so running any of them runs them. Each run must also visit the states and raise the events it's there for, so a run that no longer exercises what it should fails too.

They're for changes meant to leave the movement as it is: a refactor passes only if every path is unchanged. They don't get in the way of tuning: the runs use their own tuning, recorded with the paths (GoldenPaths/Tuning.json: every movement component's settings, the MMotionMotor's own and its body's size), not the movement asset's, so retuning the asset leaves them passing.

When a change to the movement code is meant to change the movement, record the paths again, all at once (so they share one tuning), with Unity closed:

```
MERIDIAN_MOTION_RECORD_GOLDEN_PATHS=1 <Unity> -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter GoldenPathTests -testResults GoldenPaths.xml
```

Then look at what changed in the recorded paths (git diff) before committing them: it should be only what the change meant to change. A run with no recorded path yet is inconclusive rather than failed. The paths are recorded on one machine; physics on another platform (CI's) may differ in the last digits, which the tolerances allow for (`GoldenPathTests`' PositionTolerance, VelocityTolerance, YawTolerance and HeightTolerance).

#### The other movement fixtures
- **RespawnTests**: beyond the kit's check of every ability: a respawn giving the air dashes and jumps back, standing a crouched character up, not carried back by the moving ground it stood on, leaving a rope and a crate it held free and where they were, and working right after the character's spawned.
- **MovementTests**:
  - the move input relative to the input's own look, not to the scene's camera;
  - settling on the ground without hovering, sinking or drifting;
  - falling within gravity's limits, including walking off a ledge after standing still;
  - stopping at walls;
  - climbing any step up to the step height, and not taller ones (walking or sprinting, straight at them or at an angle);
  - not counting as on the ground while falling (or jumping) until its feet are near the floor, then resting on it;
  - coyote time easing gravity in after leaving the ground, and jumps not being refunded by it;
  - keeping speed when walking off edges;
  - walking and sprinting down a long flight of steps nearly the step height (shallower than the character is wide) from different starting points and headings, without leaving them, speeding up or slowing at the steps' edges;
  - and the jump input buffer. (Walking up stairs: GroundContactTests and the golden paths.)
- **SprintTests**: sprinting at the run speed and walking again once Sprint is let go; Sprint alone not moving the character.
- **MovementComponentTests**: a movement mode's components (`AbilityMovementMode`): one turned off, or removed, left out of the movement as if it weren't there, and one turned off while it has the character (a zipline ride) letting go of it; with Walking & Running off not walking but still jumping, and with Fall & Air Handling off not falling.
- **CharacterTestRigTests**: the test rig itself, which every character test relies on: a test's change to a component's settings going to the rig's own copy of the movement mode, not the asset; and a setting asked for as the wrong type failing the test, where one the mode doesn't have is ignored (so a test can't quietly stop testing anything).
- **MovementCapabilitiesTests**: the character's capabilities matching what it does (a jump and a sprint jump carrying it as far as they say, its step, stand and jump heights), following its settings (a higher jump crossing further and reaching a ledge that much higher) and its size (twice the size jumping and sliding twice as far), leaving out what's turned off, adding double jumps with air jumps, and a measurement shared by the same tuning (asked again, or tuned back as it was) but made again for any change, even a tiny change of size.
- **JumpHeightTests**: a tap rising the min jump height, Jump held rising the max, holding longer rising higher between them, letting go past the min height stopping the rise at once, and the heights following the settings; and the moment kit's checks (`MomentFixture`; turned off, Jump leaving the character on the ground, with no jumps at all without the component), from a press of Jump on a floor.
- **GroundMomentumTests**: landing from a jump not adding walking on top of the air speed; hopping again and again (walking or sprinting) never building up speed; landing faster than walking carrying the momentum and easing down to walking, never below it; jumping while walking or sprinting keeping its speed in the air, without air steering stacking on it.
- **DashTests**: a dash lasting exactly its duration, distance and direction, ignoring gravity, momentum afterwards, cooldown and input buffer, one dash per press, air dash limits, and jump and dash cancelling each other; and the ability kit's checks (`AbilityFixture`), from a dash pressed once on a floor.
- **MovingGroundTests**: a platform enabled late being where an early one is at the same tick (both going by the level's clock; the test steps on until they're under way, as where they are depends on how far into the suite it runs); riding sliding, rising and spinning platforms (one parented under a static object, as a level's are, too), not being dragged along by ground that teleports further in a step than the character is carried, staying where a short teleport puts it while standing, and keeping a platform's momentum when jumping off.
- **BodyShapeTests**:
  - bodies of the other primitive shapes in place of the capsule: a box, a sphere, and a capsule centred ahead of the feet each walking up to a wall and stopping with their front against it, and their proportions (how far each reaches ahead, behind and to the sides, and how tall a sphere stands);
  - a box crouching (lowered from the top, its bottom at the feet) and standing again;
  - the character and the fox moving exactly the same with a collider as without one; the Character prefab having a Body Definition and no collider of its own;
  - lying down laying a box or capsule along the facing, resting on the feet, turning with the character, and no thicker than a Lying Thickness;
  - and a body of another kind replacing the body, once there's room.
- **FoxTests**: the fox (Fox.prefab: see [Generic rigs](generic-rigs.md)). Skipped if the fox hasn't been generated.
  - standing on its paws in a rounded body of several parts (no box), held above its legs, hovering over steps, with its eye point at its eyes;
  - trotting at its walk speed and galloping at its run speed; jumping once, to its jump height;
  - crouched, passing under a bar too low to stand under;
  - turning only as far as a corridor narrower than it's long lets it;
  - walking up and down a flight of stairs;
  - and, jumping onto a wall at an angle (shallow to steep, on either side, steering along it; or steeply, steering on at it), running along it.
- **GroundContactTests**: how steadily the character and the fox keep to the ground, each as it is (hovering over steps) and with Hover Over Steps off.
  - The runs: standing, walking, sprinting, turning and crouch-walking on level ground, along a wall, at a step's edge, over a hill of ramps and up and down stairs.
  - In each: never leaving the ground, overlapping the world or being pushed out of it, never moving up or down (nor its feet leaving the ground) on level ground, and never bobbing on stairs or ramps.
  - Hovering, the character's and the fox's feet also stay on the ground at the step's edge, over the hill and on the stairs (but for as far as a body reaching out over a slope further than its lift clears rests on it: the fox's back end on the steps behind, going down), and both get to the end of the hill and the flights.
  - Each run is recorded step by step (a `StepTrace`: where the character is, how the ground check found it, what collision did that step, which `MovementStep.resolution` records, and how far its feet are above the ground under them) and written to Logs/StepTraces/[run]_[character].csv, with a summary of its measurements first, to compare a run before and after a change to collision.
- **CollisionTests**: falling away from a ceiling the character jumps into, losing momentum into a wall, sliding off the edge of a drop taller than a step, sliding off slopes too steep for it, stopping where it lands on a walkable ramp (not sliding down it, then or standing idle), standing and walking on the highest of a pile of more overlapping boxes than the movement's query buffers start with (a buffer that fills grows and queries again: see `MeridianMotion.PhysicsQueries.QueryBuffer`), and being pushed by moving walls.
- **MovementEventTests**: the events the controller and the basic moves raise, each once at the right moment: Jumped and LeftGround on a jump, then Landed on touching down; AirJumped; LeftGround walking off a ledge, without a jump; DashStarted, then DashEnded when it finishes; DashEnded and Jumped together jumping out of a dash; and HitCeiling and HitWall once each on contact. (Each ability's own events are tested in its fixture; that each ability's state has its events, declared once, is MovementCompositionTests'.)
- **EdgeCaseTuningTests**: movement at the edges of its settings, each set on the test's own copy of the movement mode:
  - no walking or running speed (standing still without breaking), and a run speed no faster than walking (Sprint walks);
  - no jumps and many (exactly that many);
  - no gravity (a jump drifting up without breaking), and extreme gravity with no fall speed limit (landing without going through the floor);
  - walking and dashing at extreme speeds into a very thin wall (stopping at it);
  - a dash with no length (no dash), no speed (holding still, then ending), an empty speed curve, or no cooldown (still one dash per press); no air dashes (ground dashes only);
  - no jump input buffer (a press held from before landing doesn't jump, a press on the ground does) and no coyote time (a jump just after walking off is an air jump).
- **ScaleTests**:
  - moving like a scaled copy of itself at any size, from tiny to huge, over a course laid out for the character (ramps up and down, a step, a jump, a dash, a low ceiling and a step too tall for it): every physics step puts it in the same place for its size as at full size, on the ground at the same moments, with the same jump airtime and height for its size;
  - scaling only the model parent and resizing collision and mass to match; size changes taking their time; waiting for room to grow under a low ceiling;
  - non-uniform scales still moving and colliding; natural speed scaling; the model's measurements and placeholder following; the inspector field;
  - and refusing negative, zero and non-finite scales, raising tiny ones to the minimum, and reporting a scaled root transform.
- **AllocationTests** and **MovementPerformanceTests** (also in the Performance category): no allocation in a step, and a crowd's step time. See [Performance](running-tests.md#performance).

### InputAndDeterminism (the Movement category; see [Input](movement.md#input) and [Determinism and state](movement.md#determinism-and-state))
- **InputSourceTests**:
  - without an input source, the owner deciding (an AI's and the local player's, with no device, standing idle; a remote one not stepped), and nothing throwing;
  - a scripted source playing its timed steps (walking the way its look says, jumping once) and finishing;
  - a waypoint AI sprinting a course to its end, jumping a gap and onto a block taller than a step (both sized from the character's capabilities), and its run, recorded frame by frame and played back by a scripted source (`ScriptedInputSource.Play`), moving a new character exactly the same.
- **DeterminismTests**: the movement deterministic on one build, bit for bit:
  - a character three billion steps into its clock moving exactly as one at 0;
  - the same scene built in another order (coplanar floors, a corner of two walls, two ropes) running the same;
  - a character saved partway through riding a moving platform, or changing size, and put back, the level's clock with it, stepping on exactly as it did (each ability's own save and put back is its kit's: `Kit_SavedPartwayAndPutBack`);
  - the same state written to bytes and read into a new character in the level built again, stepping on exactly as the first;
  - and a run on a moving platform the same at another frame rate.
- **InputRecordingTests**: a run's input frames, recorded as it's stepped, saved to a file and read back, replayed on a new character in the same world, moving it exactly as the first, step for step, bit for bit.
- **ReplayGameLoopTests**: a run that sprints, slides and jumps, in the game's own loop (the character stepping itself in FixedUpdate, with its model and Unity's physics), replayed on the same build exactly, step for step (its pose, state, and each step's previous state and events), with a keyframe every half second or none.
- **ReplayTests**: a run recorded from its first step (frames, and a keyframe every Keyframe Seconds), written and read back to the same bytes; played back, the character put back where the run began and moving exactly as it did, then saying it's finished; a run knocked off course put back at the next keyframe; stopping a replay giving the character back and recording from there; the newest replay of a scene found; the level's clock held at a replay's tick until its step, then offset, and put back (see [Replays](replays.md)).

### Abilities (the Abilities category; see [Movement Abilities](abilities.md))
- **CrouchTests**: lowering the body to the crouch height (in proportion at a larger size too) and moving at the crouch speed; standing again on a second press; staying crouched under a low ceiling and standing by itself once out from under it; hold to crouch; one crouch per held press; jumping from a crouch (and not under a low ceiling); its events.
- **SlideTests**: crouching while sprinting slides, starting at least at the slide start speed, only slowing, low, ending crouched within its longest time; crouching while walking just crouches; sliding under a bar too low to stand under; jumping out keeping its speed; stopping at a wall; a second press standing up; sliding turned off (Can Slide off, or no longest time) crouching instead; and sliding turned off during a slide ending it crouched; and the ability kit's checks (`AbilityFixture`, on sliding, its 0 setting the slide's longest time), from crouching while sprinting on a floor.
- **RollTests**:
  - rolling in the input direction for its duration, low, fast, facing it and standing afterwards; straight ahead without input and not steerable;
  - one roll per press, a buffered press waiting out the cooldown; not in the air; ending crouched under a low ceiling;
  - going farther in proportion at a larger size; handing straight over to walking without a burst of speed;
  - on landing from a high fall it rolls on impact the way it's moving, but not standing still, from a lower fall, from an ordinary jump, after a dash or an air jump just before landing (which start the fall again), or with a jump pressed for the landing (which jumps);
  - and the ability kit's checks (`AbilityFixture`), from a roll pressed once on a floor.
- **WallRunTests**:
  - running along a wall in mid-air, falling much slower than falling freely, staying against it and facing along it;
  - ending after its longest time, past the end of the wall, and not starting again on the same wall;
  - wall jumps away and up, giving back jumps, and chaining into a run on a facing wall;
  - no run without steering mostly along the wall, heading into the wall, or close to the ground;
  - steering away for a moment not dropping off it, but for the release delay letting go;
  - giving back air dashes and jumps, so a character that air dashed into one can dash again after dropping off it;
  - ending once the feet drop below the wall's bottom edge, and not starting beside a wall that doesn't reach down to them;
  - only along a wall marked for wall runs (not an unmarked one, one marked for wall slides only, or one whose mark is turned off), or any wall with Wall Run On Any Wall on;
  - and the ability kit's checks (`AbilityFixture`), from a launch along the wall.
- **WallSlideTests**:
  - sliding slowly down a wall fallen into head-on, against it and facing it; a fast fall braked to the slide speed;
  - jumping off upward and away, gaining height and giving back jumps, but not off the same wall twice in a row; only so many jumps in a row off one wall before landing;
  - hops alone not climbing a wall, but a narrow chimney climbed by kicking between its walls;
  - air steering coming back only gradually after a jump; air dashes given back by a kick off a wall, but not off the same wall again;
  - not while rising fast, close to the ground, or glancing along the wall, but on hitting a wall moving into it whatever the steering;
  - letting go by steering away (for the release delay, not a moment) or with Crouch; ending on landing and after its longest time;
  - only down a wall marked for wall slides (not an unmarked one, one marked for wall runs only, or one whose mark is turned off), or any wall with Wall Slide On Any Wall on;
  - and the ability kit's checks (`AbilityFixture`), from falling in front of the wall steering into it.
- **LedgeTests**: grabbing a ledge falling in front of it and hanging still below it, which gives back air dashes and jumps; climbing up onto it, or crouched with only room to crouch; letting go with Crouch or steering away; not grabbing ledges out of reach, or while rising fast; hanging from a moving ledge; climbing from a ledge onto stairs rising from it; letting go when the ledge's object is deactivated; jumping up to a ledge, grabbing and climbing it with its events raised once each; and the ability kit's checks (`AbilityFixture`, on hanging, its 0 setting the climb duration), from falling in front of a ledge steering into it.
- **VaultTests**:
  - heading into the top of a low wall in mid-air vaults over it (giving back air dashes and jumps), rising over the rise time and leaving at its share of the speed, without sliding down it or hanging from it;
  - a low platform stepped up onto, keeping the speed, rather than vaulted; a higher block vaulted onto; at the vault's slowest speed with no speed of its own;
  - walking or sprinting into a low wall on the ground vaults it, but not with vaulting from the ground off, glancing along it, or a railing at the edge of a drop;
  - stairs sprinted up or jumped onto climbed, not vaulted; not without room over the top;
  - with vaulting off, a low wall not slid down, its top grabbed once in reach;
  - and the ability kit's checks (`AbilityFixture`, its 0 setting the rise duration), from falling in front of a low wall steering into it.

### Traversal (the Traversal category; see [Climbing and Traversal](traversal.md))
- **LadderTests**: walking into one climbing straight up its middle facing it; sideways input doing nothing; not taken hold of from behind; one standing on its own climbed to its top and no further, and one up to a platform climbed off onto it; jumping off kicking away.
- **WallClimbTests**:
  - taking hold of a climbable wall giving back air dashes and jumps;
  - from the ground, stepping up onto it first, holding still for the enter time, then climbing, and stepping off the bottom holding still for the exit time, but neither taking hold in mid-air nor letting go higher up;
  - walking into a climbable wall taking hold of it and climbing up it at the climbing speed, against it and facing it, with its events; an unclimbable wall not being climbed;
  - taking hold heading in at a glancing angle, and on falling into one rather than sliding down it; holding still without input; climbing sideways at the climbing speed;
  - stopping at the sides and top of a climbable panel on a plain wall; going round an outside corner onto the next face and into an inside corner onto the other wall;
  - climbing over a walkable top onto it, as soon as the hands reach it;
  - jumping off upward and away; letting go with Crouch and taking hold again only after the regrab delay; stepping off at the bottom by pressing down; moving along with a climbable surface that moves;
  - painted walls: climbing a painted patch only within the paint, and not taking hold of a wall's unpainted part; a box painted on one face not climbed round its corner until the face round it is painted too (paint being for the face painted, and erased as painted: ClimbablePaintTests);
  - and the ability kit's checks (`AbilityFixture`, its 0 setting the climbing speed), from walking into a climbable wall.
- **PushPullTests**: pushing and pulling at the push speed with the character keeping hold, slower for heavier objects, push-only objects staying put, stopping at walls without going into the object, letting go, objects falling off edges, objects just being obstacles without Interact, and not jumping while holding one; and the ability kit's checks (`AbilityFixture`; it has no 0 setting, so that check is skipped), from holding grab into a box.
- **RopeSwingTests**: reaching one in mid-air catching hold and swinging on it about its anchor, the hands staying the grip distance from it, carried on past it and back; no input never swinging higher, pumping with the stick swinging higher, and never past the largest swing angle; a wall stopping the swing; jumping off keeping the swing's speed and jumping up, the rope swinging on by itself; Crouch letting go, the same rope not caught again before the regrab delay; and the ability kit's checks (`AbilityFixture`, its 0 setting the grab reach), from a leap at a rope.
- **ZiplineTests**: catching a cable in mid-air and riding it downhill to its end, letting go there at the ride's speed, with its events; hanging the hang height straight below it, facing the way it rides; speeding up downhill to no more than the zipline's top speed; a level cable carrying the rider at the slowest ride speed; jumping off up on top of the ride's speed; Crouch letting go; and the ability kit's checks (`AbilityFixture`, its 0 setting the grab reach), from falling onto a downhill cable.
- **SwimTests**:
  - falling into deep water plunging below the swim depth (not to the floor) and floating back up to it without bobbing past, with its events and no landing;
  - swimming the way the input points at the swim speed, faster sprinting, facing that way at the swim depth, and slowing to a stop without input;
  - Jump jumping out, the feet rising the swim jump height above the surface, leaving the air jumps, and falling back in swimming again; jumping out onto an edge a little above the water;
  - swimming into a ladder up a low side or a tall one climbing out onto it;
  - swimming into a low side vaulting onto it (or climbing, with a swim vault height of 0), a low divider vaulted over back into the water, a taller side within reach climbed out onto, but one out of reach keeping the swimmer in;
  - walking down a slope into deep water, or plunging to the bottom, floating up to the swim depth; swimming into shallows up a slope and walking out;
  - Swimming turned off sinking to the bottom;
  - and the ability kit's checks (`AbilityFixture`, on swimming, its 0 setting the swim depth), from a drop into a deep pool.
- **DiveTests**:
  - Crouch at the surface diving under it, the body lying flat along the way it swims, resting on its feet, the model drawn lowered to it;
  - underwater swimming the way its input looks (the frame's look, which the player's camera gives), looking down and looking up, at the swim speed; holding Crouch sinking and Jump rising whichever way the camera looks;
  - drifting back up to the surface without input, floating at the swim depth with the body standing again, or holding its depth with no buoyancy;
  - swimming along the floor under an arch too low to stand under (as the Level Playground's is); turning in a channel narrower than the lying body is long only as far as it has room;
  - a press of Jump underwater rising to the surface without jumping out; and a dive speed of 0 turning diving off;
  - swimming into a ladder underwater (from along the floor too) taking hold of it and climbing it without letting go;
  - and swimming underwater up a beach surfacing once the feet are shallower than the swimming depth, finding its feet in the shallows.
- **WaterEffectsTests**: falling in splashing once on the surface, as strong as it fell fast, playing the splash prefab; jumping out splashing; swimming along rippling every ripple spacing it goes, and treading water every idle ripple interval; nothing on dry land; a prefab with its own effect (IWaterEffect) played with where and how strong; no more of an effect playing than the most at once, the oldest reused; and without prefabs the events still raised.
- **WadeTests**: walking through shallow water slowed by how deep it is (at depths part way from the wade start depth to the swim depth), sprinting slowed too, ankle-deep water not slowing it, walking in and out raising the wading events, walking or sprinting into a low wall vaulting it, water deeper than Wade Max Depth not slowing it; and the ability kit's checks (`AbilityFixture`, its 0 setting Wade Max Depth), Wading turned off either way not slowing the walk, from walking in water halfway from the wade start depth to the swim depth.
- **BouncePadTests**: coming down on one launching the feet its launch height above it, once; Jump held bouncing higher by the jump boost; running across one carrying the run on; a tilted pad launching along its up; the bounce being the jump, leaving the air jumps; and the moment kit's checks (`MomentFixture`), from falling onto a pad.

### Animations (the Animation category; see [Character Model and Animations](animation.md), and for the fox, [Generic rigs](generic-rigs.md))
- **AnimationTests**:
  - the character's model standing with its soles on the floor (within a tight tolerance, scaled) at small, full and large sizes and stretched on one axis, with foot placement on, off or fading; an unmeasured source measuring its model on load;
  - its planted feet sliding no more at other sizes, or stretched, than at its own size;
  - IK on stretched models: moving a hand or foot goal moving the hand or foot as far, and a knee hint on its own moving the foot no more than unstretched;
  - sample content (SampleContentAnimationTests, in the same file): every prepared model's planted feet staying in place walking at the walk loop's speed, standing on the floor with its arms hanging down (the character's own model, when it's one of them, checked in AnimationTests' stand-on-the-ground case instead), and the torso leaning as the mannequin's does when standing, walking and running (checks that its avatar's reference pose was set up).
- **AnimationEventTests**: the movement's events passed on by the MMotionAnimator, with the animation state.
- **AnimatorParameterTranslatorTests**: an Animator Parameter Translator: its parameters set from the animation state, its event triggers fired only by their events, and parameters the controller lacks skipped; a state played faster than its fastest loop by however much faster the character moves (never slower than its own speed, and climbing by whichever way is further past its loop); and its bool parameters: a bool showing a character's state (State: …) set while the state is, one showing the animation state's own bool set from it, an unknown source or a parameter the controller lacks skipped (the first with a warning), a float showing an ability value set from it and left as it is until it's set (as Wall Side is out of a wall run), and a new translator's defaults naming the mannequin's parameters and sources that exist.
- **FootLockTests**: turning on the spot, planted feet staying where they were put down (none of the slide they have without it); a model swapped in while standing carrying on the old one's foot placement (faded in as far, the pelvis as low) rather than fading back in; and standing on a rising and falling platform, frame by frame as drawn, the feet staying on it.
- **VisualRotationTests**: the model turning after the character: the character turning at once and the model easing round after it without overshooting; turning slower at a run than standing (no faster than its moving limit); leaning into curves by the configured share of the balancing lean (either way); walking a curve leaning about half as much as sprinting it; walking a sharp corner leaning no more than walking speed allows; leaning forward speeding up and back stopping, and upright otherwise; facing a roll at once without leaning; turning with a spinning platform without lagging.
- **AbilityAnimationTests** (sample content):
  - crouching, sliding, rolling, wall-running, sliding down a wall, hanging, climbing and vaulting each playing their Animator state;
  - a roll following the roll and blending into walking before it ends, a roll from a crouch blending into the crouch without standing up, and the roll playing on landing from a high fall;
  - the wall run for the side the wall is on (feet toward the wall, leaning away);
  - sliding down a wall with the hands flat on it at head height, spread as wide as the library's climbs hold them, the body against it;
  - a hanging model's wrists on the ledge's top just back from its edge, either side of the character and as far apart as the library's climbs grip, fingers over the edge, its body against the wall; staying in the climb when crouched;
  - the planted hand on the top mid-vault;
  - a crouched model fitting its lowered body.
- **TraversalAnimationTests** (sample content):
  - pushing playing its Animator state;
  - a step up onto a low platform easing the model up after the character, never sinking a foot into the top; landing on a staircase on the run (walking or sprinting up it) landing, not staying in the air;
  - the Ladder state holding still with the hands on different rungs and the hands and feet each on a rung;
  - the Wall Climb state with a hand on the wall and one reaching above the head climbing up, and Climb Enter and Climb Exit playing stepping on and off from the ground;
  - the rope swing with the hands on the rope, and the zipline with both hands on the cable.
- **WaterAnimationTests** (sample content):
  - the Swim state treading water with the head just out of it, and swimming along;
  - getting out of the water onto a low side playing the climb, not the vault, without passing through the air state or the body dropping back;
  - diving playing Swim Underwater, the swimmer drawn inside its lying body, and back at the surface Swim.
- **HeadInWaterTests**: the head-in-water hook (MMotionAnimator's HeadSubmerged and HeadSurfaced) on the character's own model: nothing raised swimming at the surface (the stroke's head bobbing at the water line); diving raising HeadSubmerged once, the head the margin under the surface, and rising back raising HeadSurfaced once, the head the margin above it; falling in from high raising the pair in order; and nothing on dry land.
- **FoxPawPlacementTests**: the fox's paw placement (LegPlacementIK). Its animation is stepped with the physics, and its paws placed after each step. Skipped if the fox hasn't been generated.
  - standing on a ramp, up or down it, its paws on the ramp and its body pitched with it;
  - standing over a step's edge, its hind paws reaching down to the floor;
  - walking up a ramp, each paw's sole coming down onto it every stride and no paw's joint in it;
  - turning a little on the spot, its planted paws staying where they were put down while planted (compared with paw locking off);
  - turning on the spot, its body bending into the turn, its Animator told it's turning, and each paw stepping round;
  - riding a platform up and down, frame by frame as drawn (interpolated), its paws and body steady on it;
  - walking along the flat, each paw locking as it's put down and letting go as it lifts, stride after stride, staying put while locked, and its sole coming down onto the ground every stride.
- **FoxTerrainClearanceTests**: walking up and down a flight of stairs as steep as the levels', and up ramps steeper than the levels', the fox's body, head and tail stay out of them (its legs all but out: measured on its mesh, into the boxes they're made of) and its body pitches with them. The slowest of the fox's checks (the whole posed mesh, every step), so a fixture of its own; it shares FoxPawPlacementTests' set-up (`FoxPawTestsBase`). Skipped if the fox hasn't been generated.
- **FoxAbilityTests**: the fox playing its Animator's Locomotion, Crouch and Airborne states; swimming in deep water, at its swim depth with its head out of the water and its back at the waterline, paddling (Swim), at its swim speed, and not diving; and wall-running along a wall on either side (Wall Run), its body rolled to the wall, each paw's mesh on the wall's face and nothing of it through the wall. Skipped if the fox hasn't been generated.

### Models (the Models category; see [Replacing the model](animation.md#replacing-the-model) and [Character models](animation.md#character-models))
- **ModelTests**:
  - a character left without its model (never loaded, as on a server) moving exactly as one with it, step for step, bit for bit, its body the one its model gives from the first step;
  - model hosting: loading at runtime and being set up, swapping models (including one without an Animator), new models staying hidden until swapped in, the placeholder, falling back when a model fails, cancelling loads when the character is destroyed or given another model, sources releasing their models, a model swapped in mid-run or mid-transition carrying on where the old one was.
- **ModelFitTests**:
  - fitting a model to a height (in proportion to the character's size, the eyes following);
  - fitting the body to the model (Body Fit): Height, as tall as the model in proportion as authored, or Model's Body, its source's own Body Definition (sized with the model, replacing the body with one of its kind) or, for a source without one, as wide as the model's shoulders; a fallback model's body being the fallback's, not the failed source's; and waiting for room to grow (as the model slot does it: ScaleTests' resize waiting under a low ceiling is the other way to grow);
  - measuring on load once per model (the editor preview is checked in PreparedModelTests).
- **ModelVariantTests**: models made facing another way, or off their origin, facing forward and standing on the spot; a model with an unevenly scaled armature staying in proportion when stretched.
- **HitboxTests**: the character's and the fox's models bringing their hitboxes (the characters themselves having no collider), on the CharacterHitbox layer, moving with the character's Rigidbody and left out of the movement's layers; the hitboxes covering the model as it's animated, without reaching far beyond it; detecting, a ball dropped on the character reported and passing through; colliding, bouncing off, the character still walking freely; and a timing gate's trigger seeing the fox walk through it (the character's own hitboxes trigger the gates in TimingGateTests).

### Levels (the Levels category; see [Test Levels](showcase/levels.md))
- **TimingGateTests**: sprinting from a start gate to a finish gate timed at the course's length over the run speed (with its speed shown); a split gate between them recording its split.
- **CourseTimerTests** (no character): a lap gate starting a lap on the first pass and ending it on the next, but not straight away; each finished lap passed on with its time and whether it's the best yet; only the racer counting; splits recorded in order (out of order, again, or with no run under way, ignored), passed on with the run, and the best splits the best run's.
- **CheckpointTests**: the checkpoint reached last being the one respawned at (going back through an earlier one makes it the one again); a hazard respawning the character there, once, facing the checkpoint's way, and at the start checkpoint before any's reached; a crate falling into a hazard put back where it started; a respawn at a crate's checkpoint putting it back, and one elsewhere leaving it; the checkpoint reached saved, put back and written with the character's state; and a character with no model respawned all the same.
- **ShortcutGateTests**: a closed gate blocking both ways; its trigger, on its far side, opening it, and then the way through both ways; staying open through a respawn; gates opened on the same tick moving alike; Open All; and a gate's opening saved and put back with the level (WorldStateSnapshot, StateSerializer.WriteWorld).
- **The Tutorial Ring's** (the showcase's own, in Assets/Showcase/Tests/TutorialRing, an assembly on the character's test rig):
  - **LessonTrackerTests**: a zone making its lesson current; its event doing it, once a session (an event that counts only in a state needing it); zones as a stack; a lesson with no event done on leaving; a follow-up shown until leaving; the character walking into a zone and jumping doing its lesson; and the catalog having the design's eighteen lessons in order.
  - **PlaytestSessionTests**: a lesson logged with when it was entered and how long it took; respawns counted toward the lesson last in; a course run logged with its time, splits and respawns; the results written beside the replay; and ending twice writing once.
  - **TutorialReplayTests**: the development keys off outside the editor and development builds; a run through a shortcut gate and a respawn, recorded and replayed (ReplayRecorder), ending where it ended, with the same respawns and the gate opened at the same tick.
  - **TutorialRingSceneTests**: the built scene, played in the game's own loop: the player spawning at the hub with one jump (its own tuned copy of the movement); its capabilities the envelope's without the air jumps, what the level was sized for; every lesson zone in the catalog; the roll lesson stopping crouch-walking and letting a roll through; the lessons shortcut opening from the lessons' end and not from the hub; and falling off the ring respawning at the last checkpoint.
  - **TutorialRingGamepadTests**: the built scene on a virtual gamepad (a `VirtualGamepad`, and the player's input switched to it and kept there): Start opening the pause menu with a control selected, the d-pad moving between its controls, B closing it but never opening it, and Start opening and closing it.
- **IvyFieldTests** (the showcase's own, in Assets/Showcase/Tests/Grass, on a wall face made in code): leaves placed on a face turned toward the camera and none on its back; none beyond the style's Draw Distance, the counts saying so at once; a camera's leaf lists growing to fit what it's asked for and shrinking once they've been roomy a while; and no pixel drawn that isn't a number.
- **UnderwaterViewTests**: a camera going under a water's surface changing the scene's fog to the water's and tinting and dimming the sun, raising CameraSubmerged, and coming back up putting the scene's fog and sun back, raising CameraSurfaced; a camera just above the surface (within the margin) counting as under; outside the water's footprint not; and turning the view off underwater putting the scene's fog back.

### Cameras (the Camera category; see [Camera System](camera.md))
- **CameraRigTests**, **CameraCollisionTests**, **CameraFollowTests** and **CameraEffectsTests** (sharing `CameraRigTestsBase`): the camera rig, which follows a plain Transform, independent of the character code, with expectations taken from its own settings:
  - CameraRigTests: third and first person placement, and mode blending and events; pitch limits and zoom limits; first person target hiding, including renderers added to the target later; first person eyes following an eye anchor; CameraZone overrides;
  - CameraCollisionTests: collision with walls (only as far in as needed, ignoring walls clear of it and the target's own colliders), at several sizes of target; moving to the other shoulder, at full distance, beside a wall partly blocking its own, returning once the wall is gone, and staying put beside a wall on the other side; never pulling in as a target comes up against a wall and runs along it;
  - CameraFollowTests: smooth follow and teleport snapping; framing and clip planes scaled with the target, across a wide range of sizes; the landing dip, as deep as the landing settings make it, in proportion to the target's size, and recovering (landings are judged by the fastest fall since the target last rose, so a frame holding several physics steps, as in the editor, doesn't halve the impact); following a new target with `Follow`: snapping to it and looking the way it faces, and taking its eye anchor and size reference in place of the old target's.
  - CameraEffectsTests: shake and ShakeAll; speed FOV judging speed relative to the target's size, and settling once it stops;

### UI (the UI category)
The UI's fixtures share `UITestsBase`: a UI Manager made in code, with its canvases and an EventSystem and no character (the UI knows nothing of the game), windows opened from templates made in code, and the UI's clock (`UIClock`) driven by the test.
- **UIUtilityTests**: the helpers safe on null and destroyed objects; a world position put on a canvas where the camera sees it; the clock going by its time source.
- **UIManagerTests**: a window opening on its canvas (or the one asked for); opened again, the open one brought to the front (or a copy, when it allows several); closed, destroyed, and made again by the next open; finding it by its prefab; closing all but the one excepted and those exempt; the window catalog's windows opened by their component; and the canvases scaled together.
- **WindowLifecycleTests**: its events in order, once each; a close animation played (in unscaled time, so with the game paused too) before it's destroyed, its animation event ending it, and a close that never plays through ending all the same; its click blocker behind it on its canvas, stopping clicks and closing with it; hiding and showing again keeping its state, stacking and blocker.
- **BackKeyTests**: Back closing the windows from the top down (passing over one still closing), then running the fallback; Cancel Action Overrides run instead; windows that don't take Back, and hidden ones, passed over; a text field with the keyboard keeping it; and the game told when the windows need the pointer or stop its input; and on a virtual gamepad, Back (B) closing a window but never running the fallback, which the Menu key (Start) does.
- **MessageBoxTests**: each dialog opening with its text and returning at once, its buttons running their callbacks (a second press counting once) and closing it with its blocker; Enter pressing OK; Back declining a yes/no dialog and dismissing an OK one; the prompt passing on what's typed; and a dialog opened without a manager saying so.
- **PauseMenuTests**: Back with no window opening the pause menu and pausing the game, and again resuming it (the time scale put back as it was); the game told it needs the pointer and its input stops while it's open; its controls page listing the bindings read from the input actions (but the hidden ones), Back going from it back to the main page; and Quit asking first, Back on the question keeping the game going.
- **DataListTests**: on the blueprint's own example (an item list from a fake source): a row per record in order, from the item prefabs in turn; search by every word; rows reused, never destroyed; the selection kept across refreshes (else the first row's) and passed to the panel; the empty state; only the IDs or records given; a refresh while one's under way ignored; a header before each category.
- **TabTests**: the initial tab selected; a click selecting a tab and deselecting the rest; deselecting only with Allow Deselection; a tab tracking a window opening it, another's selection closing it, and its closing deselecting the tab (or, clicked again, opening it again).
- **ContextMenuTests**: an entry per item shown (hidden ones left out), placed first, middle, last or solo in its group, featured ones on their own prefab; an entry running its item and closing the menu; the pointer leaving, or never coming, closing it; opening flipped at the screen's edges.
- **NotificationTests**: one shown at its place in its style (through the UIManager too), gone when its time's up on the UI's clock (with the game paused too); the cap of a style closing the oldest; the passive ones cleared and the rest left; Read More showing all the message and keeping it.
- **RulesTests**: a gated control right at once and following the rules as they change, with no window opened again, listeners told; the feature and the permission both needed; Reset; an interactability gate greying out (not hiding) its control.
- **WorldLabelTests**: a label following its object across the canvas, hidden behind the camera, closed with its object; the safe-area variant kept on the screen.
- **ShowcaseHUDTests** (the showcase's own, in Assets/Showcase/Tests/HUD): a course timer's finished lap shown as a notification and a new best as a banner across the middle (a timer listened to twice showing it once); a split shown with how far it is from the best run; the controls hint shown once; an NPC's name tag over it on the in-world canvas, closed when it goes.
- **ShapeTests**: each shape's mesh inside its rect (a glow reaching the theme's glow size); a chamfered box cutting only the corners it's told, by its chamfer or the theme's; a ring's arc following its fraction and direction; a segmented bar filling whole segments; colours taken from the theme's roles, tinted by the shape's colour; the UI Manager's theme, else the fallback, else the built-in one; a change to the theme redrawing its shapes.
- **IconTests**: every icon of the library drawing inside its rect at any size; the names unique; a theme's sprite standing in for an icon, fitted and tinted, until it's cleared; an unknown name still drawing.
- **ThemedWidgetTests**: a themed button's kind setting its shape's and label's roles, its state tints the theme's, hatched only while it can't be used; themed text taking the theme's font and colour, and following a change.
- **RebuildInPlaceTests** (EditMode): remaking a prefab in place keeping its GUID, its root's file ID and those of its root's components of types it keeps, taking the new contents (a shape replacing an Image), and pointing what named the made root at the prefab's.
- **ControlsListTests**: a row per action (but the hidden ones) with its binding in the scheme, a composite showing each part's first key, a button's rebindable and a composite's not; using a gamepad listing the gamepad's bindings, and the keyboard's again after; a button rebound by pressing a key (on a virtual keyboard), Back held off meanwhile, the rebinding saved; a gamepad's rebinding not taking the held button that started it, but the next one pressed; a rebinding cancelled, set from code and reset; the saved rebinding put back; the tracker naming the scheme for the device used.
- **GamepadNavigationTests**: nothing selected for the pointer, but the frontmost window's first control once a gamepad's used (on a virtual gamepad, through the Input System's UI module), the d-pad then moving between controls and A pressing them; a window opened while the gamepad's in use selected at once, its First Selected if it has one; a HUD never selected.
- **ControlOptionTests**: a control option's button hidden while no option of its name is registered, showing the option's name and state once one is, and pressing it flipping the option; the Sprint option (`SprintModeOption`) setting the input reader's Toggle Sprint, keeping the choice, and putting it back as it starts.
- **HUDWidgetTests**: a radial progress's hold filling on the UI's clock and completing once; an ability tile's states setting its look, and the bar reusing its tiles; the interact prompt's label, hold and binding, following the device in use; a world marker's edge point, and a marker behind the camera held at the edge with its arrow pointing the way; the minimap placing a world point and holding a far one at its rim; the compass following a heading; the reticle's lock-on; the dialogue panel writing out its lines, Submit going through them and closing it, Back skipping the rest.
- **PromptTests** (the Tutorial Ring's, in Assets/Showcase/Tests/TutorialRing): an action in braces written as its binding in the keyboard's scheme and the gamepad's (read from the actions), an unknown one left as its name; switching to a gamepad while a prompt is up writing it again for the gamepad; a lesson's follow-up, then nothing; and hidden, nothing shown while the lessons are still followed.
- **ReplayMenuTests**: the pause menu's replays on the package's UI Manager and the rig's character: registering as the replay controls, saving once there's a run; Load Replay playing the newest saved from its start, and at its end the choices (continuing, recording afresh; replaying again); the run saved before quitting when nothing's been saved, once, and in builds only by default.
- **UIContentTests**: the package's UI Manager prefab having its canvases, its theme (with its fonts), its choice dialog offering its options and running the one pressed, its pause menu's Replays tab showing only with replay controls and driving them, an EventSystem reading the UI action map, and every dialog and blocker; each dialog opening with its blocker and running its callbacks from its own buttons; Back opening the package's pause menu, listing the project's controls (Jump rebindable on its controls list), its Controls a tab; and the package's context menu and notifications working from their prefabs.

### Spawning (the Spawning category; see [Spawning the Character](spawning.md))
- **EveryCharacterTests**: every character prefab in the project (one with an MMotionCharacter: the Character prefab, the fox, and any made with New Character) spawns, stands on the floor and walks at least half its walk speed, so a character is tested from the moment it's in the project.
- **SpawnerTests**: the character spawner:
  - spawning at a spawn point and facing its way, at a chosen point or one chosen at random from all of them, on Start or only when asked; treating an all-zero rotation as none;
  - telling listeners through both events; handing only the local player's character to the camera (an AI's spawned beside it isn't), each told who it's for;
  - an NPC spawned from a route being an AI's character walking it (a waypoint source as its input), not followed by the camera, the spawner's PlayerCharacter staying the player's, and passed on with its route's name by On Npc Spawned;
  - giving the spawned character the chosen model and movement mode (and keeping the prefab's without them); with a tuning preset, its own tuned copy of the mode (one each, the shared mode left as it was, destroyed with the character); giving the camera its eye point and size reference;
  - and refusing with an error when there's no prefab, no spawn points or no such point.

## Edit mode tests
Edit mode tests in Assets/MeridianMotion/Tests/EditMode check Prepare Humanoid Models, the motion-capture clips and the shared controller's locomotion, the movement mode's composition and data, tuning presets, the physics query buffers, input frames, the level elements' editor tools (setting up a level's mechanics, climbable paint) and Set Up Scene, without entering Play mode, so a change to preparing (or a model added without preparing it) fails fast. As in Play mode, each check's tolerances are in the test, and each fixture is a file in a folder for its area (Movement, InputAndDeterminism, Animations, Models, LevelElements, Spawning, Levels), in a namespace of the same name.

They're in two assemblies. The main one, `MeridianMotion.Tests.EditMode`, tests the package (the character's code, its editor tools, and the level elements and theirs), and references no level builder code. The level builders' tests (and the movement envelope's, which the level checks write) are in their own, `MeridianMotion.Showcase.Tests.Levels` (Assets/Showcase/Tests/Levels), with the builders, the Level Editor and the level checks: none of those is part of the package, so neither is this assembly.

| Fixture | Folder | Category | What it checks |
| --- | --- | --- | --- |
| MovementCompositionTests | Movement | Movement | movement modes made of components, as assets |
| MovementStepTests | Movement | Movement | `MovementStep.Reset` |
| NewAbilityTests | Movement | Movement | what New Ability makes |
| TutorialTests | Movement | Movement | the first-ability tutorial still compiling |
| MovementTuningPresetTests | Movement | Movement | tuning presets |
| QueryBufferTests | Movement | Movement | the physics query buffers growing |
| InputFrameTests | InputAndDeterminism | Movement | input frames and their recordings |
| InputSuppressionTests | InputAndDeterminism | Movement | the player's input suppressed while a window is open |
| SprintToggleTests | InputAndDeterminism | Movement | Sprint toggled rather than held |
| UISceneSetupTests | UI | UI, SampleContent | Set Up Scene adding the UI and connecting it |
| RebuildInPlaceTests | UI | UI | remaking a prefab in place, its GUID and root kept |
| AnimationSetTests | Animations | Animation | animation sets and fallback clip slots |
| LocomotionBlendTests | Animations | Animation, SampleContent | the shared controller's Locomotion blend tree |
| MotionCaptureClipTests | Animations | Animation, SampleContent | the motion-capture clips |
| CharacterDoctorTests | Models | Models | the Character Doctor's checks |
| GenericRigImporterTests | Models | Models | Prepare Generic Rig Models |
| PreparedModelTests | Models | Models (some tests SampleContent) | Prepare Humanoid Models |
| LevelFixtureSetupTests | LevelElements | Levels | setting up a level's mechanics from its markers |
| ClimbablePaintTests | LevelElements | Levels | the climbable paint tools and shader |
| NewCharacterTests | Spawning | Spawning | New Character |
| CharacterSceneSetupTests | Spawning | Spawning, SampleContent | Set Up Scene |
| LevelEditorTests | Levels (own assembly) | Levels | the Level Editor |
| LevelSpecTests | Levels (own assembly) | Levels | level specs building their levels |
| GrayboxUV2Tests | Levels (own assembly) | Levels | the level pieces' UV2 |
| MovementEnvelopeTests | Levels (own assembly) | Movement | envelope.json up to date |

### Movement (the Movement category; see [Movement](movement.md))
- **MovementCompositionTests**: movement modes made of components, as assets: a new StandardMovementMode starting with its default components, one of each, in order; a component's inspector name being its own (`[MovementComponentName]`) or its class name spaced out; the movement mode asset having no missing or doubled components; each ability's state declared once, by the component that sets it, with its events (on the asset and on a new mode's defaults); every movement event declared by what raises it, as a state's or a moment's (`OneOffEvents`) and never both; a mode's JSON holding its components' settings (which the capabilities rely on to know when to measure again); and components added, removed and found.
- **MovementStepTests**: `MovementStep.Reset`, which lets a character reuse its movement data step after step without allocating, putting every field (backing fields too) back as a new one's, so no step's value carries into the next.
- **NewAbilityTests**: what New Ability (`AbilityScaffold`) makes, worked out against the project's own files without writing any. For each kind of answer, the files it makes are as recorded (NewAbilityExpected: record them again with `MERIDIAN_MOTION_RECORD_SCAFFOLDS=1` after changing a template, and check the diff). Its state and events take the next unused bits at the enums' New Ability lines; it goes just after the component chosen, in the mode's order and the composition test's; its docs are added; answers it can't make are refused, saying why; and a template's blocks are kept or left out by the answers.
- **TutorialTests**: [Your First Ability](tutorial-first-ability.md) still works as written. New Ability is run (writing nothing into the project) with the answers in the page's table, each code block is applied where the hidden marker before it says (the file, and the members it replaces), and the result is compiled in a throwaway assembly in Temp/TutorialCheck: the character's code with the ability, its flag and events, then the PlayMode tests with its fixture. So a change to New Ability's templates, to the code the tutorial uses, or to the page itself that would break the tutorial fails here, naming the block or the compile error. Only compiling is checked: the tutorial's ability isn't one of the project's, so what it does isn't run.
- **MovementTuningPresetTests**: a preset saved from a mode gives the same tuning back, curves and on/off switches included; one of a few settings changes only those; a preset never changes a mode's components or their order, and reports what it couldn't set; and a component's settings are the fields Unity saves, of kinds a preset can copy.
- **QueryBufferTests**: a ray or overlap that fills its buffer growing it and finding every hit (the nearest too), reporting each time it grew; a buffer at its largest (`QueryBuffer.MaxSize`) not growing, and reporting that once.

### InputAndDeterminism (the Movement category; see [Input](movement.md#input))
- **UISceneSetupTests** (UI, sample content): Set Up Scene adding the UI Manager (an instance of its prefab) to the setup, connected to the camera rig's Set Cursor Requested and the player input's Set Suppressed once however often it's set up, the camera's unlock key handed to the UI's Back key, and the scene's own UI Manager and EventSystem kept.
- **InputSuppressionTests**: the player's input suppressed (nothing held or pressed), a button held through it left out until let go, and one pressed after it counting at once.
- **SprintToggleTests**: Sprint read as the button is while the toggle's off; on, a press starting it (held after the button's let go) and the next ending it (that press not a press); stopping after moving ending it, but one started standing still waiting to set off; and turning the toggle off ending a sprint under way.
- **InputFrameTests**:
  - the input frame staying unmanaged and its size;
  - presses, releases and hold durations worked out from successive frames, in steps;
  - a tap between two physics steps seen in the next step's frame (and only it), a render frame that runs two steps pressing once, and two characters reading one device in a step seeing the same frame;
  - a recording keeping the latest frames, and reading back from its file exactly (and refusing what isn't one).

### Animations (the Animation category; see [Character Model and Animations](animation.md))
- **AnimationSetTests**: an animation set's controller being its layout's with its clips in the slots it fills and the layout's in the rest (the mannequin's, with an idle of the set's own, differing from the shared controller in that clip alone), made beside the set the first time it's built; and a clip slot with no clip of its own playing its fallback slot's.
- **AbilityValuesTests**: the values abilities publish for their look (`AbilityValues`): kept by their names' hashes, the same in every process; set and read back, a name never set not found; a table holding its capacity and saying so past it; and setting and reading allocating nothing.
- **LocomotionBlendTests** (sample content): the shared Animator Controller's Locomotion blend tree lines up every loop to land its left foot at the same moment, and each loop shown at its own strides' speed (the walks, the jog and the sprint) keeps its feet in step with the ground there.
- **MotionCaptureClipTests** (sample content): every motion-capture clip is a Humanoid animation with a valid avatar, mapped by the CMU bone names and starting after its T-pose frame; played on the mannequin, every clip that moves about on its feet stays on the spot and faces forward; each check lists every clip that fails it.

### Models (the Models category; see [Character models](animation.md#character-models))
- **GenericRigImporterTests**: Prepare Generic Rig Models (`GenericRigModelImporter`): guessing a setup from the fox's bone names finds the bones its own setup names; a generic-rig character with a differently named rig (the fox's bones and clips renamed in the Rigify style) is guessed with nothing missingBones and prepared, with paw placement on its four legs, hitboxes, eyes on its head, its animation set's controller and a body fitted from its mesh, and its prefab (`NewCharacter.SaveVariant`) has that body, held over steps; and the generic-rig body fit's shapes (stacked or side by side, a step above the feet, the head a sphere).
- **CharacterDoctorTests**: every character prefab in the project failing none of the Character Doctor's checks, and one with no model source failing that check, saying what to do.
- **PreparedModelTests**:
  - every model file has a source; every prepared model having a hitbox on each of its body's bones, on the CharacterHitbox layer, which the character's movement and foot placement leave out;
  - straightening nodes above the skeleton keeping everything under them where it was, unevenly scaled ones included, and leaving what can't be straightened; facing corrections rounding to the nearest quarter turn;
  - sample content: ground offsets counting meshes carried by bones, not just skinned ones; every prepared model's source is ready to use and plays the shared Animator Controller; its avatar reference pose is a T-pose (spine up, arms out, legs down) with nothing rotated above its skeleton; its stored Ground Offset, Animated Ground Offset and Facing Correction match a fresh measurement; it has a body of its own, as tall as it is, standing on its feet and lying no thicker than it's wide; what it was prepared against changes when its controller changes; and previews and listed heights are sized like the loaded model, previews standing where it loads.

### LevelElements (the Levels category; see [Adding Level Content](showcase/adding-level-content.md))
The level elements' editor tools (Assets/MeridianMotion/Editor/LevelElements), which are part of the package, tested without the level builders.

- **LevelFixtureSetupTests** (`LevelFixtureSetup`, which every level build and Set Up Scene run):
  - settings read from a fixture's name;
  - moving platforms and crates given what they need from them;
  - a "Ladder | …" fixture made into a Ladder that keeps the level's mesh, but not a "Ladder (future)" marker;
  - ropes, ziplines and bounce pads made from their names (a zipline keeping the level's cable, a pad its spring) but not the placeholders named for them;
  - a climbable panel made climbable at its speed, but not a "Climbable Wall (future)" marker;
  - wall-run, wall-slide and wall-run-and-slide walls marked for what their names say, and nothing else named like them;
  - water made swimmable at its size, keeping the level's surface, but not a "Water (future)" marker;
  - and a timer for each course, in the scene's Course Timers group, its gates' roles and distance, and its display wired to it; set up again (as Set Up Scene does a built level), each course's timer found again rather than made twice, and its display not wired twice.
- **ClimbablePaintTests**: the concrete shader's climbable mask and climbable look settings; every climbable material sharing the black unpainted mask (Assets/MeridianMotion/Content/Materials/ClimbableMask_None.png); a painted face, and only it, white in its renderer's mask, given to the renderer and taken off with masks turned off; paint being for the face painted (not a thin wall's back), and erased as painted; and no mask for a material that shows no climbable areas. (That the level's pieces have a UV2 to paint in is GrayboxUV2Tests', with the builders.)

The level builders take their marker names and the unpainted mask's path from the level elements (`LevelFixtureSetup`'s constants, spawn markers' too, and `ClimbablePaintAssets.NoMaskPath`), so each has one source, and there's nothing for a test to check they agree on.

### Spawning (the Spawning category; see [Spawning the Character](spawning.md))
- **NewCharacterTests**: New Character making a variant of Character.prefab wearing the chosen model in its own folder, with its own copy of the default movement or the default itself shared; with an animation set, a model source of its own playing the set's controller, the shared one left as it was; and answers it can't make refused, saying why.
- **CharacterSceneSetupTests** (sample content): setting up a scene:
  - replacing a placed character with a spawn point where it stood (or keeping it, when asked), spawning at the scene's spawn markers;
  - set up again, keeping its spawn points and its spawner's NPC routes and replacing its old setup;
  - wiring the Camera Rig prefab (or the scene's own camera rig) to the spawner, adding the Input Manager prefab once, both as prefab instances, turning other cameras off rather than deleting them;
  - and giving the camera an underwater view only in a level with water.

### Levels (the level builders' own assembly, not part of the package; see [Test Levels](showcase/levels.md) and [Level Specs](showcase/level-specs.md))
- **LevelEditorTests** (Levels): adding a piece where it's placed (in the picked piece's group, standing on it), deleting it as the Delete key does (the scene left as it was) and undoing that, duplicating a piece, and a piece whose objects are deleted from the scene taken out of the spec; moving a piece moving its positions and not its directions or sizes; a new piece of every kind being valid and building; the preview knowing which piece made each object; rebuilding one edited piece (a platform, a spawn marker, a turntable) leaving the level as building the edited spec afresh would; and an edit undone putting the spec and the preview back.
- **LevelSpecTests** (Levels): every level built again from its own spec (written as JSON and read back) building the same level, object for object; the Tutorial Ring's spec building, with its lesson zones, start checkpoint, shortcut gates and triggers, and hazard; a hand-written spec building what it lists, with State entries and groups of the same name; mistakes (unknown pieces and settings, missing settings, wrong shapes, bad JSON, a missing parent) reported with where they are; numbers reading back as the same float; and the schema covering every piece.
- **IvyGrowthTests** (Levels): the ivy's grower on faces described in code: growing from the foot up and fading out at its front; never on or across a wall it mayn't grow on (a climbable or marked one), nor where there's no wall; the same seed growing the same mask (and the same vines), another seed another; and its stems saved and read back where they were. (FloraPlacementTests holds the Ivy and Vines pieces to the same rules on walls they're built on.)
- **GrayboxUV2Tests** (Levels): the level's boxes and prisms having a UV2 within 0 to 1 with no two triangles sharing a texel, for climbable paint to paint in.
- **MovementEnvelopeTests** (Movement): Tools/LevelSpec/envelope.json is what the default character can do now (the editor writes it again when the tuning is saved; this catches a change made outside it). Its writer is the level checks' (Assets/Showcase/Editor/LevelChecks: see [Level Specs](showcase/level-specs.md)).

## Writing a fast test
- **Movement tests don't wait for Unity's physics steps.** The rig steps the physics itself (`Physics.Simulate`), so a test takes as long as its movement code does. The movement times everything by its own clock, so the steps are the same either way.
- **A fixture whose tests need Unity's frames** (animation, IK, the model's or camera's per-frame updates) says so (`MovementOnly => false`, overriding `CharacterRigFixture`'s). But once such a test takes over the model's Animator (`TakeOverAnimator`, stepping it by hand with the physics) the rig steps the physics itself again, unless the fixture opts out (`StepsPhysicsWithTheAnimation => false`, for tests that read what's updated each frame, such as the model's easing) or the test does (`rig.StepsPhysicsItself = false`).
- **Either way the rig waits for Unity's steps whenever the world has something only they move** (a moving platform, a pushable object, a rope). Stepping itself, it also does what Unity's steps would for the character (fitting its body to its model: `CharacterModelSlot.TryFitBodyNow`).
- **Frame-based tests run sped up, within a limit.** Those tests run up to `CharacterTestRig.TimeScale` times real speed (the fastest the suite passes at reliably), but never let one frame cover more than `CharacterTestRig.MaxFrameTime` of game time (`FrameTimeLimit`), and the camera tests likewise: what's done once a frame (animation, IK, foot placement, the camera easing) is checked against the steps, and sped up as much, a frame of the editor's (far longer than one in batch mode) would cover most of a second, enough to finish a camera blend before it could be seen, or leave the feet off the floor. So the editor runs these tests slower than batch mode does, and they pass in both.
- **Remove things with `Object.DestroyImmediate`** in a test that steps the physics itself, since `Destroy` waits for the end of a frame that doesn't come between its steps.
- **Every `yield return` costs a frame**, however little it waits for, and in the editor a frame is long: wait for something with `rig.RunUntil` (which runs its steps back to back) rather than a loop of `rig.Run(1, …)`, and start again with a second character with `ReplaceRig()` (which makes it as the first was), not a new rig.
