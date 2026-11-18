# Adding Level Content
The steps for adding to the generated levels: a new mechanic, a new piece, or a change to a level's builder. How the levels and their pieces work is in [Test Levels](levels.md), and the format they're recorded in is in [Level Specs](level-specs.md).

The rule behind all of it: **the level builders know nothing about the character or the camera.** They make geometry, lighting and named markers, and their assembly references no project code. What makes a level playable is connected from the character's side by Set Up Scene (`SceneGameSetup`, `LevelFixtureSetup`). See [Architecture](architecture.md#what-may-know-about-what).

## A new level mechanic (a fixture)
Something in the level the character uses or that moves by itself: a moving platform, a rope, a zipline, a bounce pad.

1. **Its runtime component** in Assets/Scripts/Level (namespace `Level`), knowing nothing about the character. An ability that uses it finds it (as `Level.Rope.All` lists the ropes), and moving parts are kinematic Rigidbodies moved in `FixedUpdate`.
2. **A piece that builds it** in `Graybox.Mechanics.cs`, naming its geometry "Name | key=value | ...": its settings, read back when the scene is set up.
3. **A set-up step in `LevelFixtureSetup`** that finds its geometry by that name and adds the runtime component. The prefixes must match (a constant of the same name on `Graybox` and `LevelFixtureSetup`, as neither assembly can reference the other: `LevelFixtureSetupTests` fails if they differ, or if Set Up Scene looks for a prefix the builders don't name), and match only "Name |": "(future)" markers and placeholders are named for mechanics too, and shouldn't be made to work.
4. **A `LevelFixtureSetupTests` case**, showing its settings are read from its name, and that a "(future)" marker isn't set up.
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
8. **Run Set Up Scene on it**, since rebuilding removes its game setup (see [Spawning the Character](spawning.md)).
9. **For the Meridian Complex, update the area's notes** (the `*_Blockout_Notes.md` in Assets/Scenes/MeridianComplex) with the builder.
10. **Look at it.** The tests don't cover the levels' looks. Check the levelspec tool's movement findings, which grade every gap and climb against what the character can do (see [Level Specs](level-specs.md)).
