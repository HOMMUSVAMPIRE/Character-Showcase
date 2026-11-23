# Running the Tests

How to run the tests ([Automated Tests](testing.md) is how they're written, and what each fixture checks), how long each takes, and how to profile and record them.

*See also:* [Troubleshooting: the tests](troubleshooting.md#the-tests).

## Running them

Run them from Window > General > Test Runner (the EditMode and PlayMode tabs), or from the command line with Unity closed. The easy way is the test runner script, which needs only Python 3:

```
python3 Tools/run_tests.py --changed                       # what your change can affect
python3 Tools/run_tests.py --area Abilities Traversal      # one or more areas
python3 Tools/run_tests.py --filter "LedgeTests|VaultTests"  # tests whose names match
python3 Tools/run_tests.py --all                           # both suites in full
```

- **Finding Unity.** It runs the version in ProjectSettings/ProjectVersion.txt, from the Unity Hub's install folders (its default one, and the one chosen in its preferences), or from `--unity <path>` or the `UNITY_PATH` environment variable. If it finds none, it says what to install or set.
- **The editor closed.** Unity can't open a project the editor already has open, so the script refuses while it does (it checks whether anything holds the editor's lock on the project, Temp/UnityLockfile, not just whether the file is there: a crash leaves it behind).
- **What it runs.** `--changed` lists your changes from git (staged, unstaged and new files; add `--base main` for everything your branch changed) and maps them to areas as AGENTS.md's "How to verify your work" says, printing which file led to which area. A change to the tests' shared code, the packages, the project settings or anything under Assets/ it can't place runs everything; a change only to the docs or the Python tools runs nothing, and says so. Both platforms run by default (`--platform EditMode` or `PlayMode` for one), each only if it has fixtures in the chosen areas. `--exclude Performance` leaves a category out, and `--dry-run` prints the Unity commands without running them.
- **What it prints.** For each platform, how many tests passed, failed, were inconclusive and were skipped, then each that didn't pass, with its message and the first line of its stack trace, and where the full results and Unity's log are (Logs/TestRuns/, a folder per run). If Unity stopped before running them (a compile error), it prints the errors from the log. It exits 0 when nothing failed: an inconclusive test is reported but fails the run only with `--strict`.

Its own tests (Tools/Tests, run with `python3 -m unittest discover -s Tools/Tests`, as CI's hygiene job does) use a fake Unity and canned results, so they need no Unity.

The commands it runs, by hand:

```
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults EditModeResults.xml
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults PlayModeResults.xml
```

CI (.github/workflows/tests.yml) runs both.

**One area at a time.** Every fixture is in an area, as the docs are (`TestArea`: Movement, Abilities, Traversal, Animation, Models, Camera, Spawning, Levels), so while working on one, run just its tests: the Test Runner window's Category filter, or `-testCategory "Abilities"` on the command line (several: `-testCategory "Abilities;Traversal"`). The Movement category also holds the Performance fixtures (the stress test and AllocationTests), so while iterating on the movement, leave them out: `-testCategory "Movement;!Performance"`. Run both suites in full before committing.

**The assemblies tested.** The character scripts are in the MeridianMotion.Characters assembly (Assets/MeridianMotion/Runtime, but for the camera rig, in its own MeridianMotion.Cameras assembly, the physics query buffers, in MeridianMotion.PhysicsQueries, and the level elements, in MeridianMotion.Levels: Assets/MeridianMotion/Runtime/Cameras, Assets/MeridianMotion/Runtime/PhysicsQueries and Assets/MeridianMotion/Runtime/LevelElements). Editor tools are in MeridianMotion.EditorTools (Assets/MeridianMotion/Editor), and the level elements' editor tools in MeridianMotion.Levels.EditorTools (Assets/MeridianMotion/Editor/LevelElements), so the tests can reference them. The level builders are in their own assembly, MeridianMotion.Showcase.Levels (Assets/Showcase/Editor/Levels), which doesn't reference the character or camera code (only the level elements), and the model pipeline (MeridianMotion.Showcase.Pipeline, Assets/Showcase/Editor/Pipeline) and the level checks (MeridianMotion.Showcase.LevelChecks, Assets/Showcase/Editor/LevelChecks) are in theirs. The main EditMode test assembly references none of those three; MeridianMotion.Showcase.Tests.Levels references the builders and the level checks.

## How long each test takes
Every test logs a line when it finishes: how long it ran, and the three profiler markers it spent the most time in, for finding the longest tests and what makes them long (`TestTimingAttribute`, in Assets/MeridianMotion/Tests/Shared; every fixture carries it, directly or through `CharacterRigFixture`, and the hygiene check makes sure a new one does). For example:

```
[Test timing] WallClimbTests.TakingHoldFromTheGround_…: 0.399 s; most time in Physics.Simulate 0.007 s, Movement.Step 0.004 s, Movement.Mode 0.003 s
```

The markers are the project's (see [Performance](#performance), below) and the engine's physics step (Physics.Simulate). A marker's time includes the markers inside it, so Movement.Step, which holds the rest of a step, often leads. To list the slowest, search the log (the Console, or a batch run's -logFile) for "[Test timing]".

## Performance
**AllocationTests** (in the Performance category) runs the character and the fox over a course (walking, jumping, sprinting, climbing a wall) once, then again measured, and fails if the second run allocates on the managed heap: in the movement step, in feeding the animation (`MMotionAnimator.UpdateAnimator`), in the Animator's update with the IK it runs, or in the fox's paw placement (`LegPlacementIK.Solve`). It counts allocations from the profiler's GC.Alloc samples (`AllocationCounter`; `GC.GetAllocatedBytesForCurrentThread` always gives 0 under Unity's Mono), and the rig counts its character's steps' (`rig.MeasuresStepAllocations`).

**MovementPerformanceTests** (in the Performance category) is a stress test: a crowd of characters (its `CharacterCount`) stepped together on a course of walls and ledges, sprinting, jumping and dashing at staggered times, so their abilities' checks have walls and ledges to find (it fails if they don't run, jump and dash). It reports how long a step takes per character, on average and at worst, in the test's output and the Console, and fails only if the average is many times over what a step usually costs. Timings vary between machines, so it's for noticing a change, not for exact numbers.

For where the time goes, run it (or play a level) with the Profiler open (Window > Analysis > Profiler, CPU Usage). Each part has a marker:

| Marker | What it times |
| --- | --- |
| Movement.Step | A character's whole physics step |
| Movement.Mode | The movement mode, within a step |
| Movement.<Component> | One movement component's hooks in a step, together (Movement.Ledges, Movement.WallClimbing, ...) |
| Movement.Collision | Resolving a step's movement against the world |
| Movement.MeasureCapabilities | Measuring what the character can do, when it's measured again |
| Animation.Update | Animating a character, each frame |
| Camera.Update | The camera rig's update, each frame |

## Recording the animation tests

For comparing the animation before and after a change to how it's driven or built (the translator, an Animator layout), which a test passing can miss, `AnimationRecorder` records the animation tests when asked, each by an environment variable naming a folder:

- `MERIDIAN_MOTION_RECORD_ANIMATOR`: every Animator parameter's value, and each layer's state, after each step a test steps the Animator by hand, a file per test. Stepped with the physics, they're the same on every run, so two runs' files can be compared. How far into its state each layer is varies (the Animator runs for a moment before a test takes it over), so compare the parameters and the states, not that.
- `MERIDIAN_MOTION_CAPTURE_POSES`: a picture of the model as each animation test ends, from its right, from behind on its left and from in front on its right, a PNG per test, to look at side by side.

`AnimatorControllerDescription.DescribeFromCommandLine` writes a controller's structure as text, to compare two builds of it (see [Animator layouts](animation.md#animator-layouts)).

## The levelspec tool's tests
The levelspec tool's tests run with `python3 -m unittest discover -s Tools/LevelSpec/tests`: the exported specs valid, every format converting to every other and back unchanged, XML and CSV written by hand reading as meant, mistakes reported where they are, groups' placement carried to their contents, gaps and climbs graded by the moves that cross and reach them (made-up moves, and the character's own from envelope.json, whatever it's tuned to), and schematics drawn.
