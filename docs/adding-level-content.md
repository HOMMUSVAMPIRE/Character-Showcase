# Adding Level Content
The steps for adding to the generated levels: a new mechanic, a new piece, or a change to a level's builder. How the levels and their pieces work is in [Test Levels](levels.md), and the format they're recorded in is in [Level Specs](level-specs.md).

The rule behind all of it: **the level builders know nothing about the character or the camera.** They make geometry, lighting and named markers, and their assembly references no project code but the level elements (`CharacterShowcase.LevelElements`, Assets/Scripts/Level, and its editor assembly, `CharacterShowcase.LevelElements.Editor`, Assets/Scripts/Level/Editor). Each build turns its markers into the mechanics they stand for (`LevelFixtureSetup`, in the level elements' editor assembly, run as one of the build's steps, before the scene is saved), so a built level's mechanics work as it comes out of the build. What makes it playable (the character, its camera, and who's racing the courses' timers) is connected from the character's side by Set Up Scene (`SceneGameSetup`), which runs `LevelFixtureSetup` again (it leaves what's set up as it is) for a scene built some other way. See [Architecture](architecture.md#what-may-know-about-what).

## A new level mechanic (a fixture)
Something in the level the character uses or that moves by itself: a moving platform, a rope, a zipline, a bounce pad.

1. **Its runtime component**, a level element: in Assets/Scripts/Level (the `CharacterShowcase.LevelElements` assembly, namespace `Level`), knowing nothing about the character. An ability that uses it finds it (as `Level.Rope.All` lists the ropes), and moving parts are kinematic Rigidbodies moved in `FixedUpdate`. So that the movement stays deterministic (see [Determinism and state](movement.md#determinism-and-state)):
   - **Go by the level's clock**, `Level.LevelClock` (its `Tick`, and `SecondsAt` for a time), never Unity's `Time`, so where it is is a function of the step, the same however late it was enabled, on another machine, or for a character replayed against a past step.
   - **If a character rides it, move it before the characters** each physics step, as `MovingPlatform` does (`MovingPlatform.MoveOrder`), its pose set at once, so they ride and query it where it'll be.
   - **If it holds state a character's next steps depend on** (as a rope's swing or a pushed crate's place does), make it findable by `Level.StableId` (an object in the level, named and placed the same on every machine) and add it to `WorldStateSnapshot` (its `MovingParts`, and what `Save` and `Restore` keep) and to `StateSerializer.WriteWorld` and `ReadWorld`, so it's saved and put back with the character. A mechanic that only goes by the clock, as platforms do, needs none.
   - **Break ties with `StableOrder`** wherever an ability chooses between two of it equally near (the order Unity lists objects in differs between builds), never by instance ID.
   - **Add a `DeterminismTests` case** for a character using it, saved and put back mid-use.
2. **A set-up step in `LevelFixtureSetup`** (Assets/Scripts/Level/Editor/LevelFixtureSetup.cs, namespace `Level.EditorTools`): a constant for how its marker's name starts (beside the others, such as `RopePrefix`), a `SetUpX` that adds the runtime component from the name's settings (adding only what isn't there, so setting a level up again leaves it as it is), and a line in `SetUp` that finds its markers by that name. Match only "Name |" (`IsFixture`): "(future)" markers and placeholders are named for mechanics too, and shouldn't be made to work.
3. **A piece that builds it** in `Graybox.Mechanics.cs`, naming its geometry "Name | key=value | ...": its settings, read back when the level is set up. Its name's start is `LevelFixtureSetup`'s constant (`Graybox`'s prefix constants are `LevelFixtureSetup`'s), so there's one name and nothing to keep in step.
4. **A `LevelFixtureSetupTests` case** (Assets/Tests/EditMode/LevelElements), showing its settings are read from its name, and that a "(future)" marker isn't set up; and **PlayMode tests** of the mechanic itself (see [Automated Tests](testing.md)).
5. **An example in the Level Playground** (`LevelPlaygroundBuilder`).
6. **An outline in Tools/LevelSpec/levelspec/geometry.py**, so the levelspec tool's schematics and checks know its shape.

If the character needs a new ability to use it, see [Adding a Movement Ability](adding-an-ability.md).

## A new level piece
A new kind of thing levels are built from (a kind of stair, a railing, a prop).

1. **Mark it `[Piece]`** and start it with `using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Piece), arg1, arg2, ...);`, its arguments in parameter order, so the level spec records it. Its parameters must be types `LevelSpec.Encode` and `Decode` handle.
2. **Its dimensions go in `GrayboxSettings`** (a field with a `[Tooltip]`), not constants, so they're tuned in Assets/Editor/Level/GrayboxSettings.asset.
3. **Give its mesh a UV2.** `GrayboxMesh.AddTo` does, unless `uv2: false`: lighting bakes and the climbable paint use it.
4. **If it looks at what's around it** (a railing following a deck's edge, say), it does so in `Graybox.Defer`, once everything else is placed.
5. **Collision and occlusion:**
   - Visual detail never changes collision: trims have no collider, and structure gets one box around it.
   - Only solid pieces are occluders. Use `MakeStatic(obj, occluder: false)` for anything seen through.
6. **An example in the Level Playground**, and **an outline in geometry.py**.
7. **Export the specs** (Tools > Character Showcase > Level > Specs > Export Level Specs), which also writes the schema with the new piece in it; `LevelEditorTests` checks a new piece of every kind is valid and builds.

## Changing a level's builder
1. **Build everything through pieces.** Never move, rename or delete what a piece made afterwards. To place a group, use `Graybox.Placed` or `Turned`.
2. **No randomness except the seeded kind**, so a level builds the same every time. Use no `System.Random` but the seeded ones.
3. **No `/` or `#` in the names of groups used as parents**: a spec's parents are paths, and `#` numbers groups of the same name.
4. **Keep to what the character can do.** Stairs stay under the character's step height, and ramps under the envelope's ramp gradient (GrayboxSettings' Max Ramp Gradient). The Calibration Annex's deliberate tests are the exception. Prism footprints' edges run at multiples of 45°, so `FootprintBoxes` fills them exactly.
5. **Try it in the Level Playground first**, which builds in seconds.
6. **Rebuild the level** (Tools > Character Showcase > Level > Build <Level>).
7. **Re-export its spec:** `LevelSpecTools.ExportFromCommandLine -level "<Level>"`. A spec changed since its export (in the Level Editor, say) is only replaced with `-force`, so check first that the only differences are yours.
8. **Run Set Up Scene on it**, since rebuilding removes its game setup (the spawner, the camera and the input manager; the level's mechanics come with the build: see [Spawning the Character](spawning.md)).
9. **For the Meridian Complex, update the area's notes** (the `*_Blockout_Notes.md` in Assets/Scenes/MeridianComplex) with the builder.
10. **Look at it.** The tests don't cover the levels' looks. Check the levelspec tool's movement findings, which grade every gap and climb against what the character can do (see [Level Specs](level-specs.md)).
