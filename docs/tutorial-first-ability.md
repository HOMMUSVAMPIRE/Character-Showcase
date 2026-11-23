# Your First Ability: Ground Pound

This page builds one small ability from start to finish: **Ground Pound**. In the air, press Crouch: the character hangs still for a moment, then slams straight down until it lands. On the way you'll use every part an ability has: its settings, its logic, a look of its own, and a test. It's written for someone who hasn't added an ability before; the Unity words it uses are explained in [Unity Basics](unity-basics.md), and [Adding a Movement Ability](adding-an-ability.md) is the full reference for each part.

You don't need to understand the whole movement system to follow it. New Ability writes everything that isn't the ability's own logic, and this page tells you which parts to replace and with what. Everything you make is in your project's own folders, never in the package (`Assets/MeridianMotion`), so upgrading the package leaves it alone.

Before you start, open the project as the README says ([How to open this Project](../README.md#how-to-open-this-project)), and open a code editor on it (double-click any script in the Project window).

The files go in the **output folder** (Project Settings > Meridian Motion: `Assets/Showcase` in this repository, `Assets` unless you choose another in yours). Below, `<output>` is that folder.

## 1. A character of your own

The ability goes into a character's movement, and the package's own (the default character's) is the package's to change, not yours. So start with a character of your own, with a copy of that movement:

1. Open `Assets/Showcase/Scenes/LevelPlayground/LevelPlayground.unity` (or a scene of your own).
2. Open **Tools > Meridian Motion > New Character…** and fill it in:
   - **Name:** `Pounder`.
   - **Prepared Model:** `Mannequin`.
   - **Movement:** Copy The Default.
   - **Set Up The Open Scene:** leave it ticked.
3. Press **Make It**.

It makes `<output>/Characters/Pounder`: a prefab (a variant of the default character), and its own movement, `Pounder Movement.asset`, which starts as the default's tuning. The scene now spawns Pounder. (More in [A new character of your own](adding-a-character.md#a-new-character-of-your-own).)

## 2. Make it with New Ability

Open **Tools > Meridian Motion > New Ability…** and answer:

| Question | Answer | Why |
| --- | --- | --- |
| Where | Project | In your project's own folder and code, not the package's. |
| Name | `GroundPound` | Its class name. Everything else is named after it: its inspector name is Ground Pound, its settings start `groundPound`. |
| What It Does | Takes Over | While it lasts it has the character (nothing else moves it), as a zipline does. |
| Started By | Crouch | The button that starts it. |
| At A Level Element | off | It doesn't need anything in the level. |
| Kind | Abilities | Which test area its tests are in. |
| Place In The Order | Rolling | Abilities earlier in the list take over first: it goes just after Rolling. |
| Needs IK | off | It doesn't place hands or feet on anything. |
| Give It To | `Pounder Movement` only | The movement assets of your project's it goes in, turned on. (In this repository, untick Fox Movement: the fox has no animation for it.) |

The window lists every file it will make. Press **Make It**, and wait for Unity to finish compiling. In `<output>/Abilities` it made:
- `GroundPound/GroundPound.cs`: the ability. As made, it already works: a press of Crouch makes the character hover where it is for a moment.
- `GroundPound/GroundPound.Flags.cs`: its state flag (`GroundPound.State`) and the events of it starting and ending (`GroundPound.Started`, `GroundPound.Ended`). The movement, the animation and the tests know what it's doing by these. Each has a bit of its own, which New Ability gave out from `Character Flags.json`, the project's record of the bits it has given. Leave both files as they are.
- `Tests/GroundPoundTests.cs`: its tests, which pass as made.
- `Abilities.asmdef` and `Tests/Abilities.Tests.asmdef` (the first time): the assemblies your abilities and their tests are compiled into.

Once compiled, it's in Pounder's movement, turned on, just after Rolling.

**Try it now.** In the scene from step 1, press Play, and press Crouch: the character hovers for a moment. Everything from here on changes what it does.

## 3. Its settings

Open `GroundPound.cs`. Near the top are its settings: the fields with a `[Tooltip]`, which show in the movement mode's inspector under Ground Pound. As made it has a Duration and an Input Buffer. A ground pound needs a hang time (how long it hangs before slamming) and a speed (how fast it slams down).

Replace the two settings (from the `groundPoundDuration` line to the `groundPoundInputBuffer` line, with their tooltips) with these three:

<!-- tutorial: GroundPound.cs, replace "float groundPoundDuration" .. "float groundPoundInputBuffer" -->
```csharp
[Tooltip("How long (s) the character hangs in the air before it slams down, at its authored size")]
[Min(0f)] [SerializeField] private float groundPoundHangTime = 0.2f;
[Tooltip("How fast (m/s) it slams straight down, at its authored size. 0 turns Ground Pound off")]
[Min(0f)] [SerializeField] private float groundPoundSpeed = 20f;
[Tooltip("If Crouch is pressed this many seconds before Ground Pound can start, it starts as soon as it can")]
[Min(0f)] [SerializeField] private float groundPoundInputBuffer = 0.1f;
```

Some rules every setting follows:
- a `[Tooltip]` saying what it does, for a designer;
- a name no other setting in the movement has (tests and tuning presets find settings by name);
- "at its authored size": the code multiplies by the character's scale (`SpeedScale`, `DurationScale`), so it works at any size;
- one setting whose 0 turns the ability off: here, the speed.

## 4. What it does

**What it does with the character.** Find the line `public override AbilityTraits Traits => AbilityTraits.Supported;` and its comment. Traits tell other abilities what this one does with the character: `Supported` means the character isn't falling while it has it. A ground pound falls, so it has none. Every ability that takes over should say what its traits are, even when they're none, so replace those two lines with:

<!-- tutorial: GroundPound.cs, replace "Traits =>" .. "Traits =>" -->
```csharp
/// <summary>None: once it slams, the character is falling (a landing roll measures the fall from where it started).</summary>
public override AbilityTraits Traits => AbilityTraits.None;
```

**Its state.** At the bottom of the file is `GroundPoundState`: what each character is doing with it. The ability's settings are shared by every character, so what changes as it runs lives on the character, which keeps one for it: `motor.StateOf<GroundPoundState>()`. As made, it's under way while `timeLeft` counts down. A ground pound is under way from the press until it lands, so it needs one more field. Replace the `GroundPoundState` class with:

<!-- tutorial: GroundPound.cs, replace "public class GroundPoundState" .. "public class GroundPoundState" -->
```csharp
/// <summary>What each character is doing with Ground Pound (modes are shared assets, so it lives on the character).</summary>
public class GroundPoundState
{
    /// <summary>Seconds left hanging in the air before it slams down.</summary>
    public float timeLeft;
    /// <summary>Whether it's under way: from the press until it lands.</summary>
    public bool slamming;
    /// <summary>Crouch's presses, so each press starts it at most once.</summary>
    public readonly PressBuffer press = new();

    /// <summary>Whether Ground Pound is under way.</summary>
    public bool IsActive => slamming;
}
```

**Stopping it.** `Interrupt` ends it at once, when another ability takes the character or it's turned off in the middle. It has to clear the new field too. Replace `Interrupt` with:

<!-- tutorial: GroundPound.cs, replace "void Interrupt(" .. "void Interrupt(" -->
```csharp
/// <summary>Ends it at once: another ability took the character, or it was turned off while under way.</summary>
public override void Interrupt(MovementStep step, MMotionMotor motor)
{
    GroundPoundState state = motor.StateOf<GroundPoundState>();
    state.timeLeft = 0f;
    state.slamming = false;
}
```

**Its logic.** `TakeOver` runs every physics step. It returns true while the ability has the character, and false to leave the character to the rest of the movement. As made, it starts on a press and holds the character still until its time is up. Replace everything from `TakeOver` to the end of `CanStart` (the three methods `TakeOver`, `PublishProgress` and `CanStart`) with:

<!-- tutorial: GroundPound.cs, replace "bool TakeOver(" .. "bool CanStart(" -->
```csharp
/// <summary>
/// Carries on while it lasts, or starts if it can: the character hangs where it is for its hang time, then slams
/// straight down until it lands.
/// </summary>
public override bool TakeOver(MovementStep step, StepInputs inputs, MMotionMotor motor)
{
    GroundPoundState state = motor.StateOf<GroundPoundState>();
    if (!state.IsActive)
    {
        if (!CanStart(step, inputs, motor)) { return false; }
        state.timeLeft = groundPoundHangTime * motor.DurationScale;
        state.slamming = true;
        Mode.InterruptOthers(step, motor, this);
    }
    else if (state.timeLeft <= 0f && step.initialGrounded)
    {
        // It's landed: it lets go, and the rest of the movement has the character this step.
        state.slamming = false;
        return false;
    }
    // Hanging, it holds still; then it slams straight down at its speed.
    step.velocityOverride = state.timeLeft > 0f ? Vector3.zero : Vector3.down * (groundPoundSpeed * motor.SpeedScale);
    step.state |= GroundPound.State;
    PublishProgress(motor);
    return true;
}

private void PublishProgress(MMotionMotor motor)
{
    float hang = groundPoundHangTime * motor.DurationScale;
    motor.AbilityValues.Set(ProgressValue, hang > 0f ? 1f - motor.StateOf<GroundPoundState>().timeLeft / hang : 1f);
}

/// <summary>Whether Ground Pound can start this step: turned on by its speed, in the air, on a press of Crouch.</summary>
private bool CanStart(MovementStep step, StepInputs inputs, MMotionMotor motor)
{
    if (groundPoundSpeed <= 0f || step.initialGrounded) { return false; }
    return motor.StateOf<GroundPoundState>().press.Consume(inputs.crouch, groundPoundInputBuffer);
}
```

What each part does:
- **Starting.** `CanStart` says no if the speed is 0 (turned off) or the character is on the ground (`initialGrounded`: where it was at the start of this step); otherwise it starts on a press of Crouch (`Consume` uses up the press, so one press starts it once). Starting sets the hang's time and tells the other abilities to let go (`InterruptOthers`).
- **Hanging, then slamming.** `velocityOverride` sets the character's velocity for this step outright: nothing at all while it hangs, then straight down at its speed. The movement still sweeps the character's body, so it stops at the floor rather than going through it.
- **Landing.** Once it's slamming and on the ground, it lets go: it returns false, and walking has the character.
- **Its state flag.** `step.state |= GroundPound.State` says it's under way this step. The animation and the tests read it, and its events (`GroundPound.Started`, `GroundPound.Ended`) are raised when it starts and stops being set.
- **Its progress.** `PublishProgress` publishes how far through the hang it is, which an Animator parameter can show (step 6).
- **Timing.** The hang counts down in `AdvanceTimers` (as made), one physics step at a time: abilities never read the clock (see [Determinism](movement.md#determinism-and-state)).

## 5. Press Play

Save the file and let Unity compile. In the scene from step 1, press Play, jump, and press Crouch in the air: the character hangs for a moment, then slams down. To tune it, select `<output>/Characters/Pounder/Pounder Movement.asset` and find Ground Pound in its Components list: change its Ground Pound Hang Time and Ground Pound Speed there (in Play mode too, to feel the difference; changes made in Play mode stay).

Nothing happens? Check Ground Pound is in that list and ticked (if it isn't there, add it with the list's Add Component), and look in the Console for a compile error: it names the file and the line.

## 6. A look of its own

As it is, the character plays its falling animation while it slams. To give the slam a pose of its own (the tucked crouch, say), the animation reads its state flag; no animation code is needed. Pounder plays the package's animation, which is the package's to change, so it gets copies of its own to change. Put each in `<output>/Characters/Pounder`. The parts are explained in [Animator layouts](animation.md#animator-layouts) and [Animation sets](animation.md#animation-sets).

1. **A layout of its own.** Select `Assets/MeridianMotion/Content/Animation/Mannequin Layout.asset`, then Edit > Duplicate. Drag the copy into Pounder's folder and name it `Pounder Layout`.
   - Tick its **Only For Sets**, and clear its **Controller**. The copy still names the package's controller, and building it would replace that controller for every character. Only For Sets builds it only into an animation set's own controller.
   - The copy doesn't follow changes to the package's layout later: copy them over by hand if you want them.
2. **A parameter for it.** In Pounder Layout, under Parameters, add a Bool named `GroundPounding`.
3. **A state.** Under States, add one named `Ground Pound`, with one motion: the clip slot `Crouch Idle` (any clip slot works; add a slot of its own for a clip made for it).
4. **Transitions.**
   - Add one from `Any State` to `Ground Pound`, on the condition `GroundPounding` true.
   - Add one from `Ground Pound` to `Locomotion`, on `GroundPounding` false.
   - The crouch's transition in from Any State excludes every other ability's bool: add `GroundPounding` false to its conditions too, or the two keep swapping.
5. **An animation set, to build it.** In Pounder's folder, Create > Meridian Motion > Animation > Animation Set. Name it `Pounder Animation Set`, set its Layout to Pounder Layout, and press **Build Its Animator Controller**. That makes `Pounder Animation Set Controller` beside it.
6. **A translator of its own, to drive it.**
   - Duplicate `Assets/MeridianMotion/Content/Animation/Mannequin Animator Translator.asset` into Pounder's folder as `Pounder Translator`.
   - Under its Bool Parameters, add one: its Parameter `GroundPounding`, its Shows `State: GroundPound.State` (the list offers every state flag, your project's too).
7. **A model source of its own, playing them.**
   - Duplicate `Assets/MeridianMotion/Content/Models/Humanoid/Mannequin/Mannequin.asset` (the mannequin's model source) into Pounder's folder as `Pounder Model`.
   - Set its Animator Controller to `Pounder Animation Set Controller`, and its Translator to `Pounder Translator`.
   - Open `Pounder.prefab`, and set its CharacterModelSlot's Model Source to `Pounder Model`. Then press Play.

The hang's progress (Ground Pound Progress) can drive a float parameter the same way (the translator's Float Parameters, Shows `Ability: Ground Pound Progress`), to wind up a pose through the hang.

## 7. Its tests

`GroundPoundTests.cs` (in `<output>/Abilities/Tests`) is built on the ability test kit (`AbilityFixture<GroundPound>`), which gives it the checks every ability needs: it starts; with its 0 setting it doesn't; it lets go when interrupted; its events are raised; it can be saved and put back; it works at another size. The kit runs it on the default character, giving it Ground Pound (its movement doesn't have it). It needs three things from you: how to set the ability up, the inputs that start it, and the setting whose 0 turns it off. As made, they start it standing on the floor, but a ground pound starts in the air. Replace them (`SetUpToStart`, `InputsFor` and `OffSetting`) with:

<!-- tutorial: GroundPoundTests.cs, replace "IEnumerator SetUpToStart()" .. "string OffSetting" -->
```csharp
protected override IEnumerator SetUpToStart()
{
    // In the air over a floor to land on: with no time to settle, it stays where it's put until the check runs it.
    rig.Floor();
    yield return SpawnCharacter(new Vector3(0f, 3f * Scale, 0f), settleSeconds: 0f);
}

protected override StepInputs InputsFor(int step) => step == 0 ? CharacterTestRig.Crouch() : CharacterTestRig.Idle();

protected override string OffSetting => "groundPoundSpeed";
```

Then the tests of what Ground Pound itself does. As made, there's one that checks it lasts its Duration, which it no longer has. Replace it with these two:

<!-- tutorial: GroundPoundTests.cs, replace "IEnumerator GroundPound_LastsItsDuration()" .. "IEnumerator GroundPound_LastsItsDuration()" -->
```csharp
[UnityTest]
public IEnumerator GroundPound_HangsThenSlamsStraightDown()
{
    yield return SetUpToStart();
    float hang = rig.MovementSetting<float>("groundPoundHangTime") * rig.Movement.DurationScale;
    float speed = rig.MovementSetting<float>("groundPoundSpeed") * rig.Movement.SpeedScale;
    float startHeight = rig.Position.y;
    yield return rig.Run(CharacterTestRig.StepsFor(hang), InputsFor);
    Assert.That(rig.InState(GroundPound.State), Is.True, "A press of Crouch in the air starts it.");
    Assert.That(rig.Position.y, Is.EqualTo(startHeight).Within(0.01f), "It hangs where it was for its hang time.");
    yield return rig.Run(2, _ => CharacterTestRig.Idle());
    Assert.That(rig.Velocity.y, Is.EqualTo(-speed).Within(0.01f * speed), "Then it slams straight down at its speed.");
}

[UnityTest]
public IEnumerator GroundPound_LetsGoWhenItLands()
{
    yield return SetUpToStart();
    yield return rig.Run(CharacterTestRig.StepsFor(1f), InputsFor);
    Assert.That(rig.Grounded, Is.True, "It lands.");
    Assert.That(rig.InState(GroundPound.State), Is.False, "Landed, it lets go.");
}
```

The tests read every number from the settings (`rig.MovementSetting<float>("groundPoundSpeed")`), never one of their own, so retuning Ground Pound doesn't break them.

**Run them.** Close Unity (the tests run it themselves), then, in a terminal at the project's folder:

```
python3 Tools/run_tests.py --filter GroundPoundTests
```

It prints what passed and what failed (with the reason). Without a terminal, Unity's Test Runner window (Window > General > Test Runner, its PlayMode tab, under Abilities.Tests) runs them too. See [Automated Tests](testing.md).

## 8. Its docs

Where your project keeps its docs, say what it does, how to start it, and its settings by their inspector names (Ground Pound Hang Time, Ground Pound Speed). Name the settings, and never quote their values, so retuning can't make the docs wrong.

## If it needs IK

A ground pound doesn't place hands or feet on anything. An ability that does (hands on a ledge, feet on a rung) has an IK component of its own: New Ability's **Needs IK** makes one to start from, which every humanoid model gets. What to copy from for each kind of placement is in [IK for your own model](animation.md#ik-for-your-own-model).

## Where to go next

- [Adding a Movement Ability](adding-an-ability.md): the full reference for each part of an ability (and [An ability of your project's own](adding-an-ability.md#an-ability-of-your-projects-own), for what's different about yours).
- [Movement Abilities](abilities.md) and [Climbing and Traversal](traversal.md): the abilities there are, to read and copy from (`Dashing` and `Rolling` are short).
- [Tuning the Character](tuning.md): tuning abilities, and saving tunings as presets.

<!-- This page is kept true by TutorialTests (Assets/MeridianMotion/Tests/EditMode/Movement): it runs New Ability with the answers above, applies each code block where its marker says, and compiles the result (the ability as an assembly of its own, against the package's). Change a block, and the test says whether it still compiles. -->
