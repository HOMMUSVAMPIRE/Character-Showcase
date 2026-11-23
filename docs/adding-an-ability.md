# Adding a Movement Ability
The steps for adding a new part to the character's movement: a new `MovementComponent` in the StandardMovementMode's list (see [Movement Components](movement.md#movement-components)). `RopeSwinging` and `Ziplines` (Assets/MeridianMotion/Runtime/Characters/MovementModes/Components) are good examples to copy.

The inspector (its toggle, moving it up and down, removing it, Add Component), tuning presets and letting go when it's turned off mid-use come with being a component: they all work from the class and its fields, with nothing to add.

## Your first ability

New to this? The short path, which [Your First Ability](tutorial-first-ability.md) walks through with a real ability (Ground Pound), step by step:

1. **Make it:** Tools > Meridian Motion > New Ability…, answer its questions, press Make It. It makes a small ability that already works, with its tests (see [The quick way](#the-quick-way-new-ability)).
2. **Write what it does:** in its file (`<output folder>/Abilities/<Name>/<Name>.cs` for one of your project's own, the default: see [An ability of your project's own](#an-ability-of-your-projects-own)), change its settings and replace what its `TakeOver` does (or `AfterLocomotion`, for one on top of walking).
3. **Press Play** and try it; tune it on the movement mode asset's Components list.
4. **Run its tests:** close Unity, then `python3 Tools/run_tests.py --changed` (see [Automated Tests](testing.md)), and the hygiene check, `python3 Tools/CI/check_project_hygiene.py`.

The rest of this page is the full reference: what each part is for, and what to do by hand.

## The quick way: New Ability
**Tools > Meridian Motion > New Ability…** makes the start of an ability, with everything below that isn't its logic already done, and shows each file it will make or change before it does. Its questions:

- **Where:** Project (the default) makes it in your project's own folder and assembly, changing nothing of the package's: see [An ability of your project's own](#an-ability-of-your-projects-own). Package makes it in Meridian Motion itself, as its own abilities are (for developing the package; not offered when the package is installed read-only). The rest of this section is about Package.
- **Name:** its class name (Gliding, say). Its inspector name is this spaced out; its state, events and settings are named after it.
- **What It Does:** Takes Over (it has the character while under way, as a zipline does) or On Top Of Walking (it adds to the character's own movement, as a dash does).
- **Started By:** a button the input already has (Jump, Dash, Crouch, Roll or Interact), or None. A button of its own is a step of its own: see AGENTS.md, Input.
- **At A Level Element:** it starts near a level element of its own, made with it.
- **Kind:** Abilities or Traversal, which picks its docs page and its tests' folder.
- **Place In The Order:** the default component it goes just after (see [Movement Components](movement.md#movement-components)).
- **Give It To** (Project only): which of your project's own movement assets get it once it's compiled.

In the package, Make It writes:

- **The component** (section 1): a small ability that works as it is. Taking over, it holds the character where it is for its Duration; on top of walking, it moves the character on the way it faces at its Speed for its Duration. It has its settings, its hooks, its state and that state's place on the MMotionMotor (section 2), its `StateEvents` and `Traits`.
- **Its flag and events** (section 3), each the next unused bit, added at the New Ability line in `CharacterState.cs` and `MovementEvents.cs`. Keep those lines: New Ability refuses without them.
- **Its place in the order** (section 4): `StandardMovementMode.DefaultComponents` and the composition test's copy. Once Unity has compiled it, it's added to every movement mode asset, turned on for the default character's and off for the others' (`MovementModeSync`).
- **Its tests** (section 7), on the ability test kit, which pass as it's made.
- **Its docs**: a section in abilities.md or traversal.md to write, and its name in movement.md's component list.
- **With At A Level Element:** a level element in Assets/MeridianMotion/Runtime/LevelElements (`<Name>Point`) that it starts within reach of (section 6).

Then make it yours: replace what its `TakeOver` or `AfterLocomotion` does, change its settings, write its docs section and add tests of what it does. Run its tests as you go: `python3 Tools/run_tests.py --changed`. A look of its own (section 5) is still to add. Unity's command line can make one too: `-executeMethod MeridianMotion.EditorTools.NewAbilityWindow.MakeFromCommandLine -target Package -abilityName Gliding -kind TakesOver -button Interact -after Rolling -area Abilities` (and `-levelElement`), then `MovementModeSync.AddToAllModesFromCommandLine` once it's compiled.

The sections below are the full reference: what each part is for, and what to do by hand.

## An ability of your project's own
With Where: Project, New Ability writes nothing into the package (`Assets/MeridianMotion`, which an upgrade replaces). Everything goes in `Abilities/` in the output folder (Project Settings > Meridian Motion):

- **Its component and state**, in `Abilities/<Name>/<Name>.cs`, namespace `Abilities`. The `Abilities` assembly (`Abilities.asmdef`, made the first time) references `MeridianMotion.Characters` and `MeridianMotion.Levels`. A partial class can't span assemblies, so the ability can't add a part to `MMotionMotor` (section 2). It keeps its state with `motor.StateOf<NameState>()`, one object per character and type, made the first time it's asked for. That state is saved, put back and written to bytes with the rest, as a package ability's is.
- **Its flags**, in `Abilities/<Name>/<Name>.Flags.cs`: its state and its two events, on bits of the project's own range (`Flags256.UserFirstBit` on).
  - The class is marked `[MovementFlags]`, so the names are `<Name>.State`, `<Name>.Started` and `<Name>.Ended`, in the inspectors and in `ToString`. Use them as the package's are: `step.state |= GroundPound.State`, `case GroundPound.StartedBit:`.
  - The bits come from the project's ledger, `Abilities/Character Flags.json`, which records every bit New Ability gives out and never gives one twice: a bit is saved data (recordings, a translator's triggers). Keep the ledger in version control. Never edit a bit or take one out.
- **Its level element and IK**, if asked for, beside it (`<Name>Point.cs`, `<Name>IK.cs`). Prepare Humanoid Models adds an IK component marked `[HumanoidIK]` to the models wherever it's declared.
- **Its tests**, in `Abilities/Tests/<Name>Tests.cs`, on the ability test kit (section 7), in the `Abilities.Tests` assembly (PlayMode, made the first time). They need the package's tests in the project; when they aren't there, New Ability leaves the tests out. Run them from the Test Runner (PlayMode). The character the kit moves has the package's own movement, which doesn't have your ability, so the kit puts it in the rig's copy, last in the order.
- **Its place:** once Unity has compiled it, it's added, turned on, to the movement assets chosen under Give It To, just after the component chosen under Place In The Order. These are your project's own (New Character's copies, say), never the package's: the package's `DefaultComponents` and `StandardMovementMode.asset` stay as they are. To give it to another later, use the movement asset inspector's Add Component.
- **No docs page, and no convention checks:** the hygiene check is this repository's, not part of the package. Section 1's rules still make a good ability.

From the command line: `-executeMethod MeridianMotion.EditorTools.NewAbilityWindow.MakeFromCommandLine -abilityName Gliding -kind TakesOver -button Interact -after Rolling` (`-target Project` is the default), then, once it's compiled, `-executeMethod MeridianMotion.EditorTools.NewAbilityWindow.AddToProjectModesFromCommandLine -abilityName Gliding -after Rolling`. Add `-modes` to choose the movement assets: `all` (the default), `none`, or paths separated by `;`.

Sections 2 to 4 and 7 below describe the package's own abilities. A project's ability differs only as above.

## 1. The component
A `[Serializable]` subclass of `MovementComponent` in `Assets/MeridianMotion/Runtime/Characters/MovementModes/Components/<Ability>.cs`.

**Settings.** Each has a `[Tooltip]` written for a designer, grouped with `[Header]` where that helps. Field names must be unique across the mode and all its components (the hygiene check enforces it): tests and tuning presets find settings by name. Settings are numbers, toggles, enums or curves; `MovementTuningPresetTests` fails on any other kind, which presets couldn't copy. A value of 0 that turns the ability off (a duration, a reach) is the convention.

**When it acts.** Override the hooks for the points of a physics step it acts at (see `MovementComponent` and `StandardMovementMode.Run`); the mode calls a component only at the hooks it overrides (`MovementComponent.Overridden`), so the rest cost nothing:

- `TakeOver`: carries on, or starts if its inputs and surroundings call for it, and returns whether it has the character. The usual one for an ability: the first component in the list that takes over has the character for the step.
- `IsActive` and `Interrupt`: whether it's under way, and ending it cleanly, when another ability takes the character or it's turned off while it has it.
- `Traits`: what it does with the character while it's under way (`AbilityTraits`): `Supported` if the character isn't falling while it has it (a wall, a rope, water: a landing roll measures falls from where it lets go), `Attached` if the character holds on to something (a bounce pad doesn't launch it). Other abilities ask about traits, not about it, so set them for a new ability.
- `AdvanceTimers`: its cooldowns, every step, whatever has the character.
- For what isn't a takeover: `BeforeAbilities` (forces, like gravity), `BeforeLocomotion` (changing how the character walks, like crouching), `Locomotion` (how it moves by itself), `AfterLocomotion` (acting on top of walking, like dashing and jumping) and `FinishStep` (limits, like the fall speed).
- `Posture`: how it holds the character's body this step (a `BodyPosture`: low at the crouch height, or lying), or null; the first component in list order to hold it has its way.
- `AfterMovementResolved`: for moving something along with the character, by however far it actually moved.
- `MeasureCapabilities`: if how far it carries the character matters to levels. Add a value for it to `MovementCapabilities` (the character's [Capabilities](movement.md#capabilities)). The showcase's level checks are a separate, optional export of them (`MovementEnvelopeWriter`, in Assets/Showcase/Editor/LevelChecks): add it there too if the levelspec tool's checks should use it.

**Working with the rest of the movement.**

- Ask the component's `Mode` for the shared rules: `GravityAcceleration`, `AirAcceleration`, `MoveSpeed`, `HasFreshJumpPress`, `ConsumeCrouchPress`, `AbilityInProgress` (or `AbilityInProgressExcept<T>`, for an ability that may take over from a T; `AbilityInProgressWith(traits)`, for one that cares what the ability under way does with the character, not which it is) and `InterruptOthers` (when it takes the character).
- Read another component's settings with `SettingsOf<T>()` (they apply whether it's turned on or not, or its defaults if it's been removed). Hand the character to another component with `Mode.Get<T>()` (only found turned on). Never reach into another component otherwise.
- The rules several abilities share are in `MovementRules`: using up and giving back jumps (`TryUseJump`, `GiveBackJumps`, `GiveBackAirMoves`), catching hold of a line (`AttachToLine`), jumping off what the character holds (`JumpOff`, and `JumpOffWall` for a wall, which also starts air steering coming back), and counting down a duration step by step (`CountDown`, `HasElapsed`: a cooldown, how long an ability lasts; for when something happened, keep its tick instead, below). An ability that moves the character free of the ground, in three dimensions, steers with `FreeSteering` (the way the input looks, the player's camera for the player: see [Input](movement.md#input); Jump and Crouch to rise and sink, facing across, pitching the body along its heading through `MovementStep.targetPitch`), as underwater swimming does.
- A button that starts the ability once per press, within an input buffer, uses a `PressBuffer` in its state (`Track` it every step, `Consume` it to start), as Jump, Dash, Crouch and Roll do.
- Query the world through the `MMotionMotor` (`SweepBody`, `Raycast`, `OverlapSphere`, `ClosestPointOn`, `CanFitAt`, ...), which skip the character's own colliders, and read the physics pose, not the Transform. Hovering over steps (as the characters do), the body is swept with its bottom a step above the feet (`BodyLift`): a sweep down to find a floor reaches that much further, and where the body comes to rest on something, `FeetOnGround` puts the feet on the ground under it (see Ledges' `TryDropOntoLedge`). Moving the character somewhere at once goes through `MMotionMotor.Teleport`. Measure the character by its body's proportions (`controller.Body.StandingHeight`, `FrontExtent`, `BackExtent`, `SideExtent`): heights as shares of its standing height, distances to what it faces by the extent on that side.
- Time when something happened in ticks: keep `controller.StepIndex` when it happens (`MMotionMotor.NeverTick` for never) and ask `controller.SecondsSince(tick)`, never global time, so the capability measurements (which step a copy of the character faster than real time) behave the same, and timing stays exact however long a character runs.
- Where the ability chooses between equally good candidates (two hits as near, two ropes), break the tie with `StableOrder`, so it chooses the same on any machine and however the scene was built.
- A level component the ability depends on that has state of its own (as a rope's swing and a pushable object's position are) is part of what the character's next steps depend on: it needs a place in the level that `MeridianMotion.Levels.StableId` names alike on every machine (and `StableId.Resolve` finds again), and to be among the moving parts saved and written (`WorldStateSnapshot.MovingParts`, which `StateSerializer.WriteWorld` writes). See [Determinism and state](movement.md#determinism-and-state).
- Multiply speeds by `controller.SpeedScale`, distances by `HeightScale`, accelerations by `AccelerationScale` and durations by `DurationScale`, so it works at any size.
- No allocation in per-step code: reuse buffers.

**Its name.** The inspector shows the class name spaced out. Give it `[MovementComponentName("...")]` if it needs a name a class name can't have (an ampersand, say).

## 2. Its state
Movement modes are shared assets, so what each character is doing lives on its `MMotionMotor`. Put a state class at the bottom of the component's file, and below it, still in the component's file, a part of `MMotionMotor` (a partial class) with a property for it: `public partial class MMotionMotor { public FooState foo { get; } = new FooState(); }`. Every ability's state is declared this way, beside its component, so `MMotionMotor.cs` itself isn't touched.

The state is saved and put back with the character's (`MMotionMotor.SaveState`, `MovementStateSnapshot`) and written to bytes (`StateSerializer`) by itself, as long as it's plain values and plain objects: a reference to a level object is written as its `MeridianMotion.Levels.StableId`, and one to an asset or a setting isn't written (the reader keeps its own).

## 3. Its state flag and events
- A `CharacterState` flag for it, and `MovementEvents` for it starting and ending. Both are sets of 256 flags (`Flags256`): the package's in bits 0 to 127 (`Flags256.PackageBits`), the rest a project's own. Each flag is two lines in `CharacterState.cs` or `MovementEvents.cs`: its bit number, the next unused one (`public const int FooBit = 22;`), and its value (`public static readonly CharacterState Foo = Bit(FooBit);`).
- Test a flag with `HasAny` (`step.state.HasAny(CharacterState.Foo)`), set it with `|=`, clear it with `&= ~`. To act on one event of several, switch on its bit: `switch (movementEvent.SingleBit) { case MovementEvents.FooStartedBit: … }` (the bit numbers are constants, as a `case` needs).
- The hygiene check fails if two flags of either share a bit (two abilities written at once, say), a bit number has no value or a value isn't its own bit number's, or a package flag's bit is past the package's.
- The component's `StateEvents`: a static array of `StateEventPair(flag, started, ended)`, from which the mode raises the events as the flag comes and goes (`AbilityMovementMode.DetectStateEvents`). A component with several states (Crouching's crouch and slide) lists each.
- An event of a moment, with no state that starts and ends (a jump, a bounce, a jump off a wall), goes in the component's `OneOffEvents` (flags of `MovementEvents`), and the component raises it itself (`step.events |= …`, or through `MovementRules.JumpOff`). Several components may raise the same one (Jumped is raised jumping from the ground, and off a rope). The hygiene check fails on a component raising an event it doesn't declare, and `MovementCompositionTests` on an event declared as both kinds, or by nothing.
- A state's end is raised on the first step without its state, as its start is on the first with it, so an ability's end event comes the step after its last.

## 4. Putting it in the mode
- Add it to `StandardMovementMode.DefaultComponents` at its place in the order: most committed first among the takeovers, and in step order among the others that act at the same point.
- Update the expected list in `MovementCompositionTests.ANewStandardMovementMode_StartsWithItsComponents_InOrder`: the order is a deliberate choice, so the test holds it.
- Add it to every movement mode asset. Select `Assets/MeridianMotion/Content/Movement/StandardMovementMode.asset`: its inspector warns that it lacks the new component, and **Add Missing Components, Turned On** adds it at its place in the list (just after the nearest component above it in `DefaultComponents` that the mode has), with Undo. Do the same on the fox's mode, `Assets/Showcase/Movement/Fox Movement.asset` (a StandardMovementMode of its own, hand-tuned), with **Add Them Turned Off** unless the fox's body and animations suit it (as several of its components are off). Outside the editor, `-executeMethod MeridianMotion.EditorTools.MovementModeSync.AddToAllModesFromCommandLine` does both: turned on for the Character prefab's mode, off for every other. A mode left without it can't be given it by a tuning preset, so `MovementCompositionTests.EveryMovementModeAsset_HasEveryDefaultComponent` fails until every mode has it.
- In this project, saving the character's mode also writes the level checks' movement envelope again (`MovementEnvelopeAutoWriter`), and the Console says what changed (see [Tuning the Character](tuning.md#what-changes-when-you-save)).

## 5. Animation, if it has a look of its own
1. **A bool parameter showing its state needs no code.** The animation state carries the character's states (`CharacterAnimationState.state`), so in the Animator Parameter Translator's inspector (`Assets/MeridianMotion/Content/Animation/Mannequin Animator Translator.asset`, and the fox's in Assets/Showcase/Models/Generic/Fox if it shows it), add an entry to Bool Parameters: Shows > Character State > your state, and the Animator parameter's name. The default name goes in a `Default…Parameter` constant on `AnimatorParameterTranslator`, which the generators use too.
2. **A float it shows needs no animation code either:** the component publishes it as an ability value during its step (`controller.AbilityValues.Set(id, value)`, its id an `AbilityValueId` made once from its name) and names it in its `AbilityValueNames`, and the translator's Float Parameters gets an entry: Shows > Ability Value > its name. New Ability's abilities publish their progress this way. Only a value worked out for the look, from more than the movement has (as Vault Progress is paced by the MMotionAnimator), is code: a field in `CharacterAnimationState`, filled by `MMotionAnimator.BuildState`, and a row in `AnimatorParameterTranslator.AnimationFloats` (or `AnimationBools`, for a bool).
3. **Its state in the Animator Controller:** in the mannequin's layout (`Assets/MeridianMotion/Content/Animation/Mannequin Layout.asset`), its parameter, a clip slot for its clip, a state with its motion, and its transitions in and out; add its bool to the crouch's exclusions too. Then build it (Tools > Meridian Motion > Prepare > Build Animator Controllers). See [Animator layouts](animation.md#animator-layouts).
4. **A clip:** a library's (the Quaternius libraries have many unused ones: see [Quaternius animations](animation.md#quaternius-animations)), or one of the mannequin's own, keyed in `Tools/Mannequin/mannequin_animations.py` (the model pipeline, then Generate Mannequin).
5. **An IK component, if it places limbs:** a subclass of `AbilityIK` marked `[HumanoidIK]`, which Prepare Humanoid Models then adds to every humanoid model (New Ability's Needs IK makes one). See [IK for your own model](animation.md#ik-for-your-own-model).
6. **The fox** has its own layout (`Assets/Showcase/Models/Generic/Fox/Fox Layout.asset`) and translator: it shows the new state only if its layout gets the state too (with clips of the fox's), and plays whatever its other parameters show until then. See [Generic rigs](generic-rigs.md).

## 6. Level side, if it uses something in the level
For an ability used on something in the level (as ropes and ziplines are), in two parts.

**The level element** (ships with the character):
1. A runtime component for it in `Assets/MeridianMotion/Runtime/LevelElements` (the level elements' assembly, namespace `MeridianMotion.Levels`), knowing nothing about the character. The rules it keeps (the level's clock, moving before the characters, `StableId`) are in [Adding Level Content](adding-level-content.md#a-new-level-mechanic-a-fixture).
2. If levels should get it from a named marker, a set-up step in `LevelFixtureSetup` (Assets/MeridianMotion/Editor/LevelElements) for its name's prefix, with a `LevelFixtureSetupTests` case.

**The showcase levels** (the builders: optional, and not part of the character):
1. A graybox piece in `Graybox.Mechanics.cs`, naming its geometry "Name | key=value | ..." by `LevelFixtureSetup`'s prefix (the build runs the fixture setup, so it works once built).
2. An example in the Level Playground.
3. An outline in `Tools/LevelSpec/levelspec/geometry.py`.

The level builders know nothing about the character: see [Test Levels](levels.md) and [Level Specs](level-specs.md).

## 7. Tests and docs
- **PlayMode tests on `CharacterTestRig`**, in a fixture deriving from `AbilityFixture<T>` (T your component: see The ability test kit, below), itself a `CharacterRigFixture` (which makes and disposes the rig for each test, and gives it `[TestTiming]`). The fixture goes in its area's folder and namespace (`Assets/MeridianMotion/Tests/PlayMode/Abilities` or `Traversal`, namespace `MeridianMotion.Tests.Abilities` or `.Traversal`: see [Automated Tests](testing.md#play-mode-tests)) and needs its area's `[Category(TestArea.…)]` of its own, which the base doesn't give: the hygiene check fails without it. Tests of the movement alone step the physics themselves, which keeps them fast; a fixture whose tests need Unity's frames (animation, IK) overrides `MovementOnly` to false (see [Automated Tests](testing.md#how-long-each-test-takes)).
  - Reach its settings by name with `rig.MovementSetting<T>("name")` and `rig.SetMovementSetting`, and the component with `rig.Component<T>()`.
  - Read expected values from the settings rather than hard-coding tuning, so retuning doesn't break the tests.
  - A test that needs a particular tuning (no air jumps, say) sets it on the rig: that changes only the rig's own copy of the movement mode.
  - Wait for what the movement does rather than a fixed time chosen for one tuning: `rig.RunUntil(condition, inputs)`, `rig.RunUntilLanded()` after a jump or fall, `rig.RunUntilBackOnTheGround(inputs)` for a whole jump. Use these rather than a loop of `rig.Run(1, …)`: each yield costs a frame, which in the editor is long.
  - The rig records the character's movement events from spawn (`rig.EventsRaised`, `rig.Count(event)`, `rig.StepOf(event)`; `rig.ClearEvents()` to record from a point), and has the common readings (`rig.Velocity`, `rig.Gravity`, `rig.HorizontalSpeed`, `rig.MeanSpeed`, `rig.StandOnFloor()`...): add to it rather than to one fixture.
- **The ability test kit.** Deriving `AbilityFixture<T>` gives the fixture the checks every ability should pass, as its own tests (named `Kit_…`):
  - it starts from its inputs, raising its start event;
  - turned off by its toggle, and by its 0 setting, it doesn't start;
  - turned off while it's under way, it lets go at once, raising its end event;
  - its states' start and end events come in pairs;
  - saved partway through and put back, the character steps on exactly as it did;
  - at half and twice its size, it still starts.

  The fixture supplies three things: `SetUpToStart` (the world it needs, and the character spawned with the kit's `SpawnCharacter`, so it's at the size a check asks for), `InputsFor(step)` (the inputs that start it and keep it going) and `OffSetting` (the name of the setting whose 0 turns it off). `AbilityState`, `SecondsToStart`, `StepsBeforeSaving` and `StepsCompared` can be overridden. `DashTests` and `ZiplineTests` are examples. Then add the tests that are about the ability itself.
- **The moment kit.** An ability that happens in a moment rather than being under way for a while (a jump, a bounce: `OneOffEvents`, no state) derives from `MomentFixture<T>` instead, and supplies its event too (`MomentEvent`). Its `Kit_…` checks:
  - it happens from its inputs (and its component declares the event);
  - turned off by its toggle, and by its 0 setting, it doesn't happen;
  - once it's happened, the same inputs don't make it happen again straight away (`RepeatSeconds`);
  - saved before it and put back, the character steps on exactly as it did, the moment included;
  - at half and twice its size, it still happens.

  `JumpHeightTests` and `BouncePadTests` are examples. What the two kits share is `AbilityKitBase<T>`.
- **Docs.**
  - Add a section for it in [abilities.md](abilities.md) or [traversal.md](traversal.md), and add it to the component list in [movement.md](movement.md#movement-components).
  - Name each setting by its inspector name and the component it's on, never by its value.

Then run the hygiene check, the tests the change can affect, and both full suites before committing (see AGENTS.md, How to verify your work).
