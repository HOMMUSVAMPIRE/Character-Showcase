# Your First Ability: Ground Pound

This page builds one small ability from start to finish: **Ground Pound**. In the air, press Crouch: the character hangs still for a moment, then slams straight down until it lands. On the way you'll use every part an ability has: its settings, its logic, a look of its own, and a test. It's written for someone who hasn't added an ability before; the Unity words it uses are explained in [Unity Basics](unity-basics.md), and [Adding a Movement Ability](adding-an-ability.md) is the full reference for each part.

You don't need to understand the whole movement system to follow it. New Ability writes everything that isn't the ability's own logic, and this page tells you which parts to replace and with what.

Before you start, open the project as the README says ([How to open this Project](../README.md#how-to-open-this-project)), and open a code editor on it (double-click any script in the Project window).

## 1. Make it with New Ability

Open **Tools > Character Showcase > New Ability…** and answer:

| Question | Answer | Why |
| --- | --- | --- |
| Name | `GroundPound` | Its class name. Everything else is named after it: its inspector name is Ground Pound, its settings start `groundPound`. |
| What It Does | Takes Over | While it lasts it has the character (nothing else moves it), as a zipline does. |
| Started By | Crouch | The button that starts it. |
| At A Level Element | off | It doesn't need anything in the level. |
| Kind | Abilities | Where its docs and tests go. |
| Place In The Order | Rolling | Abilities earlier in the list take over first: it goes just after Rolling. |
| Needs IK | off | It doesn't place hands or feet on anything. |

The window lists every file it will make or change. Press **Make It**, and wait for Unity to finish compiling. It made:
- `Assets/Scripts/MovementModes/Components/GroundPound.cs`: the ability. As made, it already works: a press of Crouch makes the character hover where it is for a moment.
- `Assets/Tests/PlayMode/Abilities/GroundPoundTests.cs`: its tests, which pass as made.
- Its state flag (`CharacterState.GroundPound`) and events (`GroundPoundStarted`, `GroundPoundEnded`), its place in the movement's order, a section for it in docs/abilities.md, and its name in docs/movement.md.

Once compiled, it's added to every movement mode: turned on for the default character, off for the fox.

**Try it now.** Open `Assets/Scenes/LevelPlayground/LevelPlayground.unity`, press Play, and press Crouch: the character hovers for a moment. Everything from here on changes what it does.

## 2. Its settings

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

Some rules every setting follows (the hygiene check, step 6, checks them):
- a `[Tooltip]` saying what it does, for a designer;
- a name no other setting in the movement has (tests and tuning presets find settings by name);
- "at its authored size": the code multiplies by the character's scale (`SpeedScale`, `DurationScale`), so it works at any size;
- one setting whose 0 turns the ability off: here, the speed.

## 3. What it does

**What it does with the character.** Find the line `public override AbilityTraits Traits => AbilityTraits.HoldsUp;` and its comment. Traits tell other abilities what this one does with the character: `HoldsUp` means the character isn't falling while it has it. A ground pound falls, so it has none. Every ability that takes over says what its traits are, even when they're none (the hygiene check makes sure), so replace those two lines with:

<!-- tutorial: GroundPound.cs, replace "Traits =>" .. "Traits =>" -->
```csharp
/// <summary>None: once it slams, the character is falling (a landing roll measures the fall from where it started).</summary>
public override AbilityTraits Traits => AbilityTraits.None;
```

**Its state.** At the bottom of the file is `GroundPoundState`: what each character is doing with it (the ability's settings are shared by every character, so what changes as it runs lives on the character). As made, it's under way while `timeLeft` counts down. A ground pound is under way from the press until it lands, so it needs one more field. Replace the `GroundPoundState` class with:

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
    public readonly ButtonPress press = new();

    /// <summary>Whether Ground Pound is under way.</summary>
    public bool IsActive => slamming;
}
```

**Stopping it.** `Interrupt` ends it at once, when another ability takes the character or it's turned off in the middle. It has to clear the new field too. Replace `Interrupt` with:

<!-- tutorial: GroundPound.cs, replace "void Interrupt(" .. "void Interrupt(" -->
```csharp
/// <summary>Ends it at once: another ability took the character, or it was turned off while under way.</summary>
public override void Interrupt(MovementData movementData, MovementController controller)
{
    controller.groundPound.timeLeft = 0f;
    controller.groundPound.slamming = false;
}
```

**Its logic.** `TakeOver` runs every physics step. It returns true while the ability has the character, and false to leave the character to the rest of the movement. As made, it starts on a press and holds the character still until its time is up. Replace everything from `TakeOver` to the end of `CanStart` (the three methods `TakeOver`, `PublishProgress` and `CanStart`) with:

<!-- tutorial: GroundPound.cs, replace "bool TakeOver(" .. "bool CanStart(" -->
```csharp
/// <summary>
/// Carries on while it lasts, or starts if it can: the character hangs where it is for its hang time, then slams
/// straight down until it lands.
/// </summary>
public override bool TakeOver(MovementData movementData, Inputs inputs, MovementController controller)
{
    GroundPoundState state = controller.groundPound;
    if (!state.IsActive)
    {
        if (!CanStart(movementData, inputs, controller)) { return false; }
        state.timeLeft = groundPoundHangTime * controller.DurationScale;
        state.slamming = true;
        Mode.InterruptOthers(movementData, controller, this);
    }
    else if (state.timeLeft <= 0f && movementData.initialGrounded)
    {
        // It's landed: it lets go, and the rest of the movement has the character this step.
        state.slamming = false;
        return false;
    }
    // Hanging, it holds still; then it slams straight down at its speed.
    movementData.velocityOverride = state.timeLeft > 0f ? Vector3.zero : Vector3.down * (groundPoundSpeed * controller.SpeedScale);
    movementData.state |= CharacterState.GroundPound;
    PublishProgress(controller);
    return true;
}

private void PublishProgress(MovementController controller)
{
    float hang = groundPoundHangTime * controller.DurationScale;
    controller.AbilityValues.Set(ProgressValue, hang > 0f ? 1f - controller.groundPound.timeLeft / hang : 1f);
}

/// <summary>Whether Ground Pound can start this step: turned on by its speed, in the air, on a press of Crouch.</summary>
private bool CanStart(MovementData movementData, Inputs inputs, MovementController controller)
{
    if (groundPoundSpeed <= 0f || movementData.initialGrounded) { return false; }
    return controller.groundPound.press.Consume(inputs.crouch, groundPoundInputBuffer);
}
```

What each part does:
- **Starting.** `CanStart` says no if the speed is 0 (turned off) or the character is on the ground (`initialGrounded`: where it was at the start of this step); otherwise it starts on a press of Crouch (`Consume` uses up the press, so one press starts it once). Starting sets the hang's time and tells the other abilities to let go (`InterruptOthers`).
- **Hanging, then slamming.** `velocityOverride` sets the character's velocity for this step outright: nothing at all while it hangs, then straight down at its speed. The movement still sweeps the character's body, so it stops at the floor rather than going through it.
- **Landing.** Once it's slamming and on the ground, it lets go: it returns false, and walking has the character.
- **Its state flag.** `movementData.state |= CharacterState.GroundPound` says it's under way this step. The animation and the tests read it, and its events (`GroundPoundStarted`, `GroundPoundEnded`) are raised when it starts and stops being set.
- **Its progress.** `PublishProgress` publishes how far through the hang it is, which an Animator parameter can show (step 5).
- **Timing.** The hang counts down in `AdvanceTimers` (as made), one physics step at a time: abilities never read the clock (see [Determinism](movement.md#determinism-and-state)).

## 4. Press Play

Save the file and let Unity compile. Open LevelPlayground, press Play, jump, and press Crouch in the air: the character hangs for a moment, then slams down. To tune it, select `Assets/ScriptableObjects/BasicMovementMode.asset` and find Ground Pound in its Components list: change its Ground Pound Hang Time and Ground Pound Speed there (in Play mode too, to feel the difference; changes made in Play mode stay).

Nothing happens? Check Ground Pound is ticked in that list, and look in the Console for a compile error: it names the file and the line.

## 5. A look of its own

As it is, the character plays its falling animation while it slams. To give the slam a pose of its own (the tucked crouch, say), the animation reads its state flag; no animation code is needed. The steps, each explained in [Animator layouts](animation.md#animator-layouts):

1. **A parameter for it.** Select `Assets/Animation/Mannequin Layout.asset`. Under Parameters, add a Bool named `GroundPounding`.
2. **A state.** Under States, add one named `Ground Pound`, with one motion: the clip slot `Crouch Idle` (any clip slot works; add a slot of its own for a clip made for it).
3. **Transitions.** Under Transitions, add one from `Any State` to `Ground Pound` on the condition `GroundPounding` true, and one from `Ground Pound` to `Locomotion` on `GroundPounding` false. The crouch's transition in from Any State excludes every other ability's bool: add `GroundPounding` false to its conditions too, or the two keep swapping.
4. **Drive it.** Select `Assets/Animation/Mannequin Animator Translator.asset`. Under Bool Parameters, add one: its Parameter `GroundPounding`, its Shows `State: GroundPound` (the list offers every state flag).
5. **Build.** Tools > Character Showcase > Build Animator Controllers, then press Play.

The hang's progress (Ground Pound Progress) can drive a float parameter the same way (Float Parameters, Shows `Ability: Ground Pound Progress`), to wind up a pose through the hang.

## 6. Its tests

`GroundPoundTests.cs` (in `Assets/Tests/PlayMode/Abilities`) is built on the ability test kit (`AbilityFixture<GroundPound>`), which gives it the checks every ability needs: it starts; with its 0 setting it doesn't; it lets go when interrupted; its events are raised; it can be saved and put back; it works at another size. The kit needs three things from you: how to set the ability up, the inputs that start it, and the setting whose 0 turns it off. As made, they start it standing on the floor, but a ground pound starts in the air. Replace them (`SetUpToStart`, `InputsFor` and `OffSetting`) with:

<!-- tutorial: GroundPoundTests.cs, replace "IEnumerator SetUpToStart()" .. "string OffSetting" -->
```csharp
protected override IEnumerator SetUpToStart()
{
    // In the air over a floor to land on: with no time to settle, it stays where it's put until the check runs it.
    rig.Floor();
    yield return SpawnCharacter(new Vector3(0f, 3f * Scale, 0f), settleSeconds: 0f);
}

protected override Inputs InputsFor(int step) => step == 0 ? CharacterTestRig.Crouch() : CharacterTestRig.Idle();

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
    Assert.That(rig.InState(CharacterState.GroundPound), Is.True, "A press of Crouch in the air starts it.");
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
    Assert.That(rig.InState(CharacterState.GroundPound), Is.False, "Landed, it lets go.");
}
```

The tests read every number from the settings (`rig.MovementSetting<float>("groundPoundSpeed")`), never one of their own, so retuning Ground Pound doesn't break them.

**Run them.** Close Unity (the tests run it themselves), then, in a terminal at the project's folder:

```
python3 Tools/run_tests.py --filter GroundPoundTests
```

It prints what passed and what failed (with the reason). `python3 Tools/run_tests.py --changed` runs every test your changes can affect. Without a terminal, Unity's Test Runner window (Window > General > Test Runner, its PlayMode tab) runs them too. See [Automated Tests](testing.md).

Then run the hygiene check (no Unity needed): `python3 Tools/CI/check_project_hygiene.py`. It checks every ability follows the conventions (a tooltip on each setting, its tests on the kit, its docs), and each failure says how to fix it.

## 7. Its docs

New Ability added a section for it to docs/abilities.md, to fill in: what it does, how to start it, and its settings by their inspector names (Ground Pound Hang Time, Ground Pound Speed: name settings, never quote their values, so retuning can't make the docs wrong).

## If it needs IK

A ground pound doesn't place hands or feet on anything. An ability that does (hands on a ledge, feet on a rung) has an IK component of its own: New Ability's **Needs IK** makes one to start from, which every humanoid model gets. What to copy from for each kind of placement is in [IK for your own model](animation.md#ik-for-your-own-model).

## Where to go next

- [Adding a Movement Ability](adding-an-ability.md): the full reference for each part of an ability.
- [Movement Abilities](abilities.md) and [Climbing and Traversal](traversal.md): the abilities there are, to read and copy from (`Dashing` and `Rolling` are short).
- [Tuning the Character](tuning.md): tuning abilities, and saving tunings as presets.

<!-- This page is kept true by TutorialTests (Assets/Tests/EditMode/Movement): it runs New Ability with the answers above, applies each code block where its marker says, and compiles the result. Change a block, and the test says whether it still compiles. -->
