# Adding a Movement Ability
The steps for adding a new part to the character's movement: a new `MovementComponent` in the BasicMovementMode's list (see [Movement Components](movement.md#movement-components)). `RopeSwinging` and `Ziplines` (Assets/Scripts/MovementModes/Components) are good examples to copy.

The inspector (its toggle, moving it up and down, removing it, Add Component), tuning presets and letting go when it's turned off mid-use come with being a component: they all work from the class and its fields, with nothing to add.

## 1. The component
A `[Serializable]` subclass of `MovementComponent` in `Assets/Scripts/MovementModes/Components/<Ability>.cs`.

**Settings.** Each has a `[Tooltip]` written for a designer, grouped with `[Header]` where that helps. Field names must be unique across the mode and all its components (the hygiene check enforces it): tests and tuning presets find settings by name. Settings are numbers, toggles, enums or curves; `MovementTuningPresetTests` fails on any other kind, which presets couldn't copy. A value of 0 that turns the ability off (a duration, a reach) is the convention.

**When it acts.** Override the hooks for the points of a physics step it acts at (see `MovementComponent` and `BasicMovementMode.ProcessMovementData`); the mode calls a component only at the hooks it overrides (`MovementComponent.Overridden`), so the rest cost nothing:

- `TakeOver`: carries on, or starts if its inputs and surroundings call for it, and returns whether it has the character. The usual one for an ability: the first component in the list that takes over has the character for the step.
- `IsActive` and `Interrupt`: whether it's under way, and ending it cleanly, when another ability takes the character or it's turned off while it has it.
- `Traits`: what it does with the character while it's under way (`AbilityTraits`): `HoldsUp` if the character isn't falling while it has it (a wall, a rope, water: a landing roll measures falls from where it lets go), `HoldsOn` if the character holds on to something (a bounce pad doesn't launch it). Other abilities ask about traits, not about it, so set them for a new ability.
- `AdvanceTimers`: its cooldowns, every step, whatever has the character.
- For what isn't a takeover: `BeforeAbilities` (forces, like gravity), `BeforeLocomotion` (changing how the character walks, like crouching), `Locomotion` (how it moves by itself), `AfterWalking` (acting on top of walking, like dashing and jumping) and `FinishStep` (limits, like the fall speed).
- `Posture`: how it holds the character's body this step (a `BodyPosture`: low at the crouch height, or lying), or null; the first component in list order to hold it has its way.
- `AfterMovementResolved`: for moving something along with the character, by however far it actually moved.
- `MeasureCapabilities`: if how far it carries the character matters to levels. Add a value for it to `MovementCapabilities`, and to `MovementEnvelopeWriter` if the levelspec tool's checks should use it.

**Working with the rest of the movement.**

- Ask the component's `Mode` for the shared rules: `GravityAcceleration`, `AirAcceleration`, `MoveSpeed`, `IsNewJumpPress`, `ConsumeCrouchPress`, `AbilityInProgress` (or `AbilityInProgressExcept<T>`, for an ability that may take over from a T; `AbilityInProgressWith(traits)`, for one that cares what the ability under way does with the character, not which it is) and `InterruptOthers` (when it takes the character).
- Read another component's settings with `SettingsOf<T>()` (they apply whether it's turned on or not, or its defaults if it's been removed). Hand the character to another component with `Mode.Get<T>()` (only found turned on). Never reach into another component otherwise.
- The rules several abilities share are in `MovementHelpers`: using up and giving back jumps (`TryUseJump`, `GiveBackJumps`, `GiveBackAirMoves`), catching hold of a line (`TakeHoldOfALine`), jumping off what the character holds (`JumpOff`, and `JumpOffWall` for a wall, which also starts air steering coming back), and the timers' countdowns (`CountDown`, `HasElapsed`). An ability that moves the character free of the ground, in three dimensions, steers with `FreeSteering` (the way the camera looks, Jump and Crouch to rise and sink, facing across, pitching the body along its heading through `MovementData.targetPitch`), as underwater swimming does.
- A button that starts the ability once per press, within an input buffer, uses a `ButtonPress` in its state (`Track` it every step, `Consume` it to start), as Jump, Dash, Crouch and Roll do.
- Query the world through the `MovementController` (`SweepBody`, `Raycast`, `OverlapSphere`, `ClosestPointOn`, `CanFitAt`, ...), which skip the character's own colliders, and read the physics pose, not the Transform. Hovering over steps (as the characters do), the body is swept with its bottom a step above the feet (`BodyLift`): a sweep down to find a floor reaches that much further, and where the body comes to rest on something, `FeetOnGround` puts the feet on the ground under it (see Ledges' `TryDropOntoLedge`). Moving the character somewhere at once goes through `MovementController.Teleport`. Measure the character by its body's proportions (`controller.Body.StandingHeight`, `FrontExtent`, `BackExtent`, `SideExtent`): heights as shares of its standing height, distances to what it faces by the extent on that side.
- Time things by `controller.StepTime`, never global time, so the capability measurements (which step a copy of the character faster than real time) behave the same.
- Multiply speeds by `controller.SpeedScale`, distances by `HeightScale`, accelerations by `AccelerationScale` and durations by `DurationScale`, so it works at any size.
- No allocation in per-step code: reuse buffers.

**Its name.** The inspector shows the class name spaced out. Give it `[MovementComponentName("...")]` if it needs a name a class name can't have (an ampersand, say).

## 2. Its state
Movement modes are shared assets, so what each character is doing lives on its `MovementController`. Put a state class at the bottom of the component's file, and below it, still in the component's file, a part of `MovementController` (a partial class) with a property for it: `public partial class MovementController { public FooState foo { get; } = new FooState(); }`. Every ability's state is declared this way, beside its component, so `MovementController.cs` itself isn't touched.

## 3. Its state flag and events
- A `CharacterState` flag for it (a 32-bit flags enum: take the next unused bit).
- `MovementEvents` for it starting and ending (a 64-bit flags enum: take the next unused bits).
- The component's `StateEvents`: a static array of `StateEvent(flag, started, ended)`, from which the mode raises the events as the flag comes and goes (`ComposedMovementMode.DetectStateEvents`). A component with several states (Crouching's crouch and slide) lists each.

## 4. Putting it in the mode
- Add it to `BasicMovementMode.DefaultComponents` at its place in the order: most committed first among the takeovers, and in step order among the others that act at the same point.
- Update the expected list in `MovementCompositionTests.ANewBasicMovementMode_StartsWithItsComponents_InOrder`.
- Add it to `Assets/ScriptableObjects/BasicMovementMode.asset` with Add Component in its inspector, and move it to its place. Saving writes the movement envelope again (`MovementEnvelopeAutoWriter`), and the Console says what changed.

## 5. Animation, if it has a look of its own
1. Fields for it in `CharacterAnimationState`, filled by `AnimationController.BuildState`.
2. A parameter on `AnimatorParameterTranslator`: its default name and its name setting (a serialized field, under Bool or Float parameters), and a row pairing that setting with what it shows, which looks it up and sets it each frame: in `BoolParameters` for a bool (the state's flag), in `FloatParameters` for a float (its value from the state, as the authored model would move, and whether it's smoothed). Only a float set some of the time (Wall Side) or worked out from others (the playback parameters) is set by hand, in `Driver.UpdateAnimation`.
3. In `MannequinGenerator`: the parameter, the crouch state's exclusions, and a state in `MannequinGenerator.AbilityStates.cs`.
4. A clip in `Tools/Mannequin/mannequin_animations.py`, if it needs one.
5. An IK component added in `CharacterModelImporter.SaveModelPrefab`, if it needs one: a subclass of `AbilityIK`, which hooks it into CharacterIK and eases it in and out, so it only places the limbs (`Solve`).
6. Then regenerate the mannequin (Generate Mannequin) and prepare the character models (Prepare Character Models). See [Character Model and Animations](animation.md).

## 6. Level side, if it uses something in the level
For an ability used on something in the level (as ropes and ziplines are):

1. A runtime component for it in `Assets/Scripts/Level`.
2. A graybox piece in `Graybox.Mechanics.cs`, naming its geometry "Name | key=value | ...".
3. A set-up step in `LevelFixtureSetup` (its prefix matching the piece's), with a `LevelFixtureSetupTests` case.
4. An example in the Level Playground.
5. An outline in `Tools/LevelSpec/levelspec/geometry.py`.

The level builders know nothing about the character: see [Test Levels](levels.md) and [Level Specs](level-specs.md).

## 7. Tests and docs
- **PlayMode tests on `CharacterTestRig`**, in a fixture deriving from `CharacterRigFixture` (which makes and disposes the rig for each test). Tests of the movement alone step the physics themselves, which keeps them fast; a fixture whose tests need Unity's frames (animation, IK) overrides `MovementOnly` to false (see [Automated Tests](testing.md#how-long-each-test-takes)).
  - Reach its settings by name with `rig.MovementSetting<T>("name")` and `rig.SetMovementSetting`, and the component with `rig.Component<T>()`.
  - Read expected values from the settings rather than hard-coding tuning, so retuning doesn't break the tests.
  - A test that needs a particular tuning (no air jumps, say) sets it on the rig: that changes only the rig's own copy of the movement mode.
  - Wait for what the movement does rather than a fixed time chosen for one tuning: `rig.RunUntil(condition, inputs)`, `rig.RunUntilLanded()` after a jump or fall, `rig.RunUntilBackOnTheGround(inputs)` for a whole jump. Use these rather than a loop of `rig.Run(1, …)`: each yield costs a frame, which in the editor is long.
  - The rig records the character's movement events from spawn (`rig.EventsRaised`, `rig.Count(event)`, `rig.StepOf(event)`; `rig.ClearEvents()` to record from a point), and has the common readings (`rig.Velocity`, `rig.Gravity`, `rig.HorizontalSpeed`, `rig.MeanSpeed`, `rig.StandOnFloor()`...): add to it rather than to one fixture.
- **The turned-off cases.** Cover the component turned off with its toggle, and turned off by its 0 setting.
- **Docs.**
  - Add a section for it in [abilities.md](abilities.md) or [traversal.md](traversal.md), and add it to the component list in [movement.md](movement.md#movement-components).
  - Name each setting by its inspector name and the component it's on, never by its value.

Then run the hygiene check, the tests the change can affect, and both full suites before committing (see AGENTS.md, How to verify your work).
