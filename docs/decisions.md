# Design Decisions
Choices about how the project is built, why they were made, and what was decided against. Read this before undoing one: each had a reason. Add an entry when a decision like these is made.

## Movement is a list of components, not one class (November 2026)
**Decided:** a movement mode (`AbilityMovementMode`) is a list of `MovementComponent`s, each part of the movement with its own settings, which can be turned off, removed, added and reordered on the asset. Every part is a component, walking and falling included: `StandardMovementMode` keeps only what they share.

**Why:** `StandardMovementMode` had grown into one class holding every ability, where each new ability touched the same files. As components, an ability is one file, a designer can turn it off or take it out without code, and another kind of movement reuses the same components in a different list.

**Against:** inheritance (a mode subclass per combination of abilities), which multiplies classes and can't be changed on an asset.

## A component reads another's settings, but takes over only from one turned on (November 2026)
**Decided:** a component shaped by another's settings reads them whether that one is turned on or not, or its defaults if it's been removed (`SettingsOf<T>`): a roll goes as low as Crouching's Crouch Height even with crouching off. One handing the character to another finds it only turned on (`Mode.Get<T>`): climbing over a wall's top becomes a ledge climb only with Ledges on.

**Why:** turning an ability off should remove that ability, not quietly change the others.

## Crouching and sliding are one component (November 2026)
**Decided:** sliding is part of Crouching, with a Can Slide toggle, rather than a component of its own.

**Why:** a slide starts from the crouch press and ends crouched, and the two share their settings: apart, each would need the other.

## Gravity, air steering and landing friction are one component (November 2026)
**Decided:** Fall & Air Handling holds gravity, the fall speed limit, air steering, and the ground's friction on the momentum the character lands with.

**Why:** they're all about how the character moves through the air and how a fall ends, and they're tuned together.

## Unity's own serialization is the source of truth (November 2026)
**Decided:** the movement asset, saved by Unity, is where the tuning lives. Other forms (tuning presets now; spreadsheets later) are read from and written to it, by setting name.

**Why:** Unity's inspector, Undo, prefab and asset references and version control all work on it as it is; a second source of truth would drift from it.

**Against:**
- *XML*, which adds nothing over JSON or CSV here.
- *A live link to Google Sheets*, which is beyond this project's scope: spreadsheets will be CSV files, imported and exported. Anyone who wants a live link can build one on the import and export.

## Tuning presets set values, never the components (November 2026)
**Decided:** a tuning preset holds some or all of a mode's settings and toggles, and applying it never adds, removes or reorders the mode's components. Presets are made and edited in the editor only.

**Why:** reordering through presets would make what a preset does depend on what the mode looked like when it was saved, and that gets messy fast. Anyone who needs it can build it on top.

## No tuning is set in stone (November 2026)
**Decided:** nothing about the character's tuning is fixed, the number of jumps included. The movement envelope (what the levels are checked against) is written again whenever the tuning is saved in the editor, with a log of what changed. `MovementEnvelopeTests` stays, to catch an envelope left out of date by a change made outside the editor. Tests read their expectations from the settings, and may set a tuning on their own copy of the movement to test a case (no air jumps, say).

**Why:** the project is for trying out movement. Levels and tests that assume a tuning get in the way of changing it.

**Against:** making a failing envelope check only a warning, which would let the levels drift from the character unseen.

## The Proving Ground was removed (November 2026)
**Decided:** the Proving Ground, a level graded to one tuning of the character, was deleted. The levelspec tool's grading tests now use a course generated from the envelope, so they follow any tuning.

**Why:** with tuning able to change per level (and soon per segment), a level built for one tuning would need rebuilding with every change. The Level Playground and the Meridian Complex remain.

## Planned: per-character tuning at runtime, reset on leaving (November 2026)
**Decided, partly built:** each character will run its own copy of its movement mode, with tuning presets applied on top of it. The first part is built: a spawner's Tuning Preset gives each character it spawns its own copy with the preset applied (the Tutorial Ring's one jump); tuning zones aren't yet. Tuning zones apply a preset while the character is inside and undo it when it leaves (zones inside zones stack, and respawning clears them), and a level can set one for all of itself.

**Why:** single levels, or parts of them, can then be played with different movement than the character usually has, without changing the asset.

## The body is a shape; hitboxes meet the world (November 2026)
**Decided:** the character's movement no longer depends on a collider: its body (`CharacterBody`) is a shape the movement sweeps and overlaps itself, and the Character and Fox prefabs have no collider of their own. What meets the character meets its model's hitboxes (`CharacterHitboxes`): a capsule on each body bone, fitted to the mesh, on the CharacterHitbox layer, which detect (triggers) unless set to collide.

**Why:** one collider round the whole character fit no one: the fox's box covered its head, body and legs, so its corners caught on things far from its paws, and the body's resting contact with the ground was fixed up in shape-specific ways. With the body a shape of its own, it can be shaped for moving (and next held above the ground on probes: see [Collision](movement.md#collision)) while the hitboxes follow the model closely.

**Against:** a trimmed root collider beside the hitboxes, for other characters to meet: the hitboxes are the character's presence, and characters will meet one another through their bodies (a list of them, as ropes and water are found) rather than colliders. Solid hitboxes by default: what comes at the character is only detected for now, as the Response setting allows changing later.

## The body hovers over steps (November 2026)
**Decided:** the character's body is held a step's height above its feet (Hover Over Steps, on by default), and probes under the feet keep them on the ground: steps pass under the body, and the feet step up and down onto them. The body standing on its feet (the setting off) is kept, as it was, for comparison and for bodies that can't be lifted (a sphere).

**Why:** the body's bottom resting on the ground was where the jitter came from (see the ground contact traces, in [Testing](testing.md)): a rounded bottom rolled over step edges and slopes with the feet in the air under it, the root jumping a tread at once and then settling, and a box rested on its corners, its feet far off the ground. Hovering, the mannequin's feet stay on the ground over ramps, stairs and edges.

**Against:** a fractional lift (part of the step height): steps between the lift and the step height would need the body's own step-up, which hovering otherwise retires. And treating the long fox's body differently: it still rides slopes and stairs on the ground ahead of its paws, which a rounder or pitched body is next to fix.

## A long body is made of several parts (November 2026)
**Decided:** a body can have extra parts beside its shape (a Body Definition's Extra Parts), together one body: the fox's is several rounded parts (see Fox Body.asset), held a step above its paws, in place of the box round all of it.

**Why:** a box long enough for the fox reached out with its corners: turning in a corridor, coming at a wall to run along it, and over slopes and stairs, where its flat bottom rested on the ground ahead of its paws. Rounded parts have no corners, and held above its legs, they leave the ground to its paws. A single capsule couldn't do it: round across, as wide as the fox would leave it only as tall as its back, or as tall as its head, far too wide.

**Against:** pitching the body along the ground (as its model is), which would keep a long body off the steps behind it going down a flight too, but turn the character's own queries with every stride; it's still open, if how little it rests on them there matters. And dropping the long-body rules (WallRunning's reach and alongside gap): they're about a body being longer than it's wide, which a rounded one still is, not about corners.

## Input is a frame per step, and the character works out its presses (November 2026)
**Decided:** a character is given one input frame each physics step (`CharacterInputFrame`): unmanaged, saying only the move input, the look and which buttons are held. The character works out presses and hold durations itself, counted in steps (`InputState`), and its components read them as `StepInputs`.

**Why:** the same frames then give the same movement at any frame rate, and a frame can be copied, recorded, compared and sent as it is. A press worked out from the frames can't disagree with them.

**Against:** the input manager's own `StepInputs`, updated in place each render frame, with presses and durations timed by it: what the movement saw depended on the frame rate, and there was nothing to record or send.

## The movement never reads a camera (November 2026)
**Decided:** the move input is relative to the input frame's look (`MovementStep.look`), which the player's input manager fills from the main camera, and an AI from its own aim.

**Why:** a character steps the same on a machine with no camera (a server), and an NPC moves by where it looks, not where the player's camera does.

**Against:** the movement reading `Camera.main` (or a camera set on it), which tied every character to the player's view.

## Movement keeps time in ticks (November 2026)
**Decided:** movement times things by its own clock in whole steps (`MMotionMotor.StepIndex`), keeping the tick something happened at and asking `SecondsSince`, rather than adding a float of seconds each step or reading Unity's time.

**Why:** a float added to step by step drifts, and loses its fractions as it grows: in ticks, timing is exact however long a character runs, and a copy stepped faster than real time, or restored and replayed, times things the same.

## Deterministic on one build, corrected across machines (November 2026)
**Decided:** the same state, input frames and world give bit-identical steps on one build: ties go by where things are (`StableOrder`), the level's moving parts by the level's clock (`MeridianMotion.Levels.LevelClock`), and PhysX's Enhanced Determinism is on. Across machines, a server corrects its clients (from a saved or written state: `SaveState`, `StateSerializer`).

**Why:** a networked game's prediction and rollback need a character restored to a step to step on exactly as it did, and replays and the golden path tests need the same. Floating point isn't reproducible across platforms, so agreement between machines comes from correction, not from the arithmetic.

**Against:** breaking ties by the order Unity gives results in, or by instance IDs, which differ between runs and machines; and mechanics timed by their own time since they were enabled.

## What drives a character is its input source and owner (November 2026)
**Decided:** a character is driven by an input source (`ICharacterInputSource`: the player's device, a script, a recording, an AI walking a route), else by what its owner (`ControlledBy`) gives: the local player's reads the device, an AI's stands idle, a remote one isn't stepped.

**Why:** the player's character, an NPC, a replay and another machine's character are then the same character with the same movement, only fed differently; the controller reading the input manager itself was what kept NPCs out.

## Physics query buffers are their own assembly, and grow (November 2026)
**Decided:** the reused buffers for physics queries (`QueryBuffer`, `RaycastHitBuffer`, `ColliderBuffer`) are in `MeridianMotion.PhysicsQueries`, an assembly that references nothing, and a buffer that a query fills grows and runs the query again.

**Why:** Unity's results come in no order, so a full buffer may have left out the nearest hit. The character and the camera both need the buffers, and neither may know the other, so they live apart from both.

**Against:** bare arrays sized generously, which still fill and then miss hits without a sign; and queries that allocate, every step.

## The body is fitted from the model's source at Start (November 2026)
**Decided:** with Body Fit on, `CharacterModelSlot` fits the body its source was prepared with in `Start`, before the first step, and keeps it when the model arrives (`LoadModelOnStart` off, it never does).

**Why:** the character moves the same body from its first step, with its model, before it loads, or without one (a server), so its movement doesn't depend on when, or whether, a model loaded.

**Against:** fitting the body only once the model has loaded, which moved a character with one body and then another.

## Moving ground moves first, its pose set at once (November 2026)
**Decided:** each physics step a moving platform sets its pose directly (where the level's clock has it), before the characters step (`TickedPlatform.MoveOrder`), and draws itself interpolated.

**Why:** a character then rides the platform and queries it where it will be after the step.

**Against:** moving it with `Rigidbody.MovePosition`, which only takes effect at the physics step: the characters read it a step late and rode it a step behind.

## The level elements are their own assembly, and the builders attach them (November 2026)
**Decided:**
- The level elements (Assets/MeridianMotion/Runtime/LevelElements: water, ropes, ladders, platforms and the rest) are an assembly of their own, `MeridianMotion.Levels`, which references only `MeridianMotion.PhysicsQueries`. The character uses it for traversal, and the level builders use it to equip their levels.
- Its editor assembly holds the climbable paint tool and `LevelFixtureSetup`, which turns named fixtures into level elements and holds their names. Each level build runs it before saving, so a level comes out of its build with its mechanics working. Set Up Scene keeps the character's jobs (the spawner, the camera, the input manager, who's racing the course timers) and runs it again, finding what's set up, for a scene built some other way.
- The mannequin and fox generators with their Blender runner (`MeridianMotion.Showcase.Pipeline`), the movement envelope's writer (`MeridianMotion.Showcase.LevelChecks`) and the level builders' tests (`MeridianMotion.Showcase.Tests.Levels`) are assemblies of their own, as the level builders already were.

**Why:** the character is meant to ship as a package without the showcase's level builders or its Blender tooling. With them in assemblies of their own, and the level elements (which traversal needs) apart from the builders, the line between what ships and what doesn't is the assemblies' (AGENTS.md, The package line), and the hygiene check holds it.

**Against:** the level elements staying in the character's assembly, with only Set Up Scene turning a level's markers into them. The builders, which couldn't reference the character's code, then named the markers by constants of their own that had to match Set Up Scene's: a names contract duplicated across two assemblies, held by a test, and a level that didn't work until Set Up Scene had been run on it.

## Ability states and events are 256-bit flags, the package's and a project's apart (November 2026)
**Decided:** `CharacterState` and `MovementEvents` are structs of 256 flags (`Flags256`: four `ulong`s), not enums. The package's flags are bits 0 to 127; a project's own abilities' are 128 to 255. Each flag is a bit number (`public const int JumpedBit = 0;`) and its value (`public static readonly MovementEvents Jumped = Bit(JumpedBit);`), keeping the bit it had as an enum. Code tests them with `HasAny`, and a switch on one flag is on its bit number (`switch (e.SingleBit)`). Their names (for `ToString`, `TryParse` and the inspectors) are their fields', found by reflection.

**Why:**
- `MovementEvents` had 22 of its 64 bits left, two an ability: about eleven abilities before it ran out, and none for a buyer's own.
- A struct can be any width and stays blittable: cheap to copy, compare and send (32 bytes a set), and kept in `MovementStateSummary`, which stays unmanaged. Unity saves it, where it drops an enum wider than 32 bits (which had made a saved set of events a bare `long`).
- The width lives in one struct, so a project that wants another changes only it.
- With the package's and a project's bits apart, a package upgrade adding flags can't take one a project's ability uses.

**Against:**
- Staying 64-bit enums: `const` members, `switch` on them and `Enum` for names, but too few bits, and the drop on saving.
- `when` guards for switches on flags (`case var e when e == MovementEvents.Jumped:`): readable, but an `if` chain the compiler can't check for duplicates. A switch on the bit number keeps real `case`s.
- Bits handed out as the mode is built: no hand-picked bits, but bits that change with the mode, which recorded golden paths, replays and the network would have to map.

## A project's abilities live in its own assembly: StateOf, [MovementFlags] and a ledger (November 2026)
**Decided:** New Ability makes an ability of a project's own (its default, Where: Project) in the project's folder and its own assembly (`Abilities`, in the output folder), never in the package.
- **Its state:** it keeps its state with `MMotionMotor.StateOf<T>()`: one object per character and state type, made the first time it's asked for, held in an object array on the motor.
- **Its flags:** they're declared in a `[MovementFlags]` class of its own (`GroundPound.State`, `.Started`, `.Ended`, on bits 128 and up), which the name registry finds by reflection.
- **Its bits:** they're given out by the project's ledger (`Character Flags.json`), which records every bit it has given and never gives one twice.
- **Its place:** it's added to the project's own movement assets. The package's `DefaultComponents` and `StandardMovementMode.asset` aren't touched.

The package's own abilities keep their parts of the partial `MMotionMotor` and their flags in `CharacterState.cs` and `MovementEvents.cs` (New Ability's Where: Package).

**Why:**
- A package upgrade replaces the package's files, so an ability that edits them (the flags, `DefaultComponents`, a part of `MMotionMotor`) is lost on the next upgrade. A partial class can't span assemblies anyway.
- `StateOf` finds a state by its type, not its place. So snapshots and `StateSerializer`, which already carry the character's plain objects by type, carry it without changes, whatever order the states were made in.
- The ledger, not the highest bit the code declares, decides the next bit. Otherwise deleting the newest ability would free its bits, and a translator trigger saved for it would fire on the next ability's event.
- Test assemblies aren't looked in for flags, so a test's flags never show in the inspectors or take a project's bits.

**Against:**
- State slots by the component's place in the mode: no lookup, but a different mode (or order) puts a state in another slot, which a serialized state would have to map.
- Slots numbered as state types are first used: the order differs between machines, so a state written on one would be read into the wrong slot on another.
- A ScriptableObject ledger: inspector-friendly, but it's YAML in a binary-ish asset, and a hand-merge of it is harder than of a small JSON file.

## Ability states and events stay global flag enums, 64 bits wide (November 2026, replaced by the one above)
**Decided:** `CharacterState` is 64 bits wide (`long`), as `MovementEvents` already was. Each ability still takes bits of both by hand, and the hygiene check fails if two members share a bit or one isn't a single bit. No field of either is serialized by Unity (which drops enum fields wider than 32 bits): `MovementStep`'s are `[NonSerialized]`, and a saved set of events is a `long`.

**Why:** with 22 of its 32 bits taken, `CharacterState` would have run out about ten abilities on. One flags value per step stays cheap to copy, compare and keep in `MovementStateSummary` (which stays unmanaged), and every reader (animation, effects, AIs, tests) keeps testing bits as before.

**Against:**
- Each component declaring its own states and events, with bits handed out as the mode is built: no limit and no hand-picked bits, but bits that change with the mode's components, which recorded golden paths, replays and anything sent over a network would have to map.
- Revisit that when `MovementEvents` (two bits an ability) nears its 64.

## Animator Controllers are built from layouts; the animation's own floats stay the animation's (November 2026)
**Decided:**
- Each character's Animator Controller is built from an Animator Layout asset (`AnimatorControllerBuilder`), not written by its generator's code: the mannequin's and the fox's controllers moved over with nothing changed (their structures, every parameter the tests step through, and their poses compared before and after). The reasons the generators' code carried as comments are the layouts' notes.
- The translator's parameters are lists in its inspector, each with a source. A source is the animation state's own value (worked out by the MMotionAnimator for how it should look) or what the movement has (a character's state, or a value an ability publishes, `AbilityValues`).

**Why:** a new ability's look then needs no code (a state in the layout, a translator entry), the controllers can be changed and rebuilt without Blender, and the package has one way to build a controller, for a buyer's characters too.

**Against:** moving every Animator float into values the movement publishes, as first planned. The animation's own floats (a vault's progress paced by Vault Min Duration, a climb's held as it blends out) are presentation, worked out each frame: in the movement, they'd break "movement and animation are separate" and "movement runs on physics steps". So they stay the animation state's, and only what the movement itself has is published.

## The UI is its own assembly, knowing nothing of the game (November 2026)
The UI framework (ReferenceAssets/Implementation Guidance/UI_Framework_Blueprint.md's window manager, windows, dialogs and the rest) is `MeridianMotion.UI`, referencing only uGUI, TextMeshPro and the Input System. Like the camera, its shipping is undecided: nothing else that ships references it (its own editor assembly, its prefabs and its tests aside), so the package can be cut with or without it.

**Why:** a buyer's game has its own screens; a window manager tied to the character would be one more thing to work around. Kept apart, it's a framework for any game, and the character doesn't change for it.

**How it meets the game:** through generic hooks the scene connects, as the camera does: the UI Manager's On Cursor Needed Changed and On Game Input Blocked Changed, to the camera's `CursorRequested` and `PlayerInputReader.Suppressed`. The UI runs on its own unscaled clock (`UIClock`), so it keeps going while the game is paused, and tests drive it. A project's own windows are listed in a window catalog asset, not as fields on the manager as the blueprint has them, so a project adds a window without editing the framework; the framework's own dialogs and blockers stay fields, as the blueprint's class diagram shows.

**Against:** UI Toolkit, which the blueprint isn't written for (it's uGUI throughout), and putting the UI in the showcase, which would leave a package user without a menu or a dialog.

## The UI's shapes and icons are meshes drawn in code, not sprites (November 2026)
The UI's look (ReferenceAssets/Image References/Concept Art/MeridianMotion UI Reference.png) is drawn by graphics that build their own meshes: the shapes (`UIShapeGraphic`: chamfered panels, slanted bars, diamonds, rings, stripes, brackets) and the icons (`UIIcon`, from `UIIconLibrary`, on a grid of their own), their edges smoothed by a feathered band of vertex alpha. Every colour is a role of one theme asset (`UITheme`).

**Why:** the reference sheet couldn't be cut up (its transparency is broken), and sprites would have had to be drawn again for each size, colour and state. Meshes are sharp at any size and canvas scale, recolour from the theme at once (so retheming is editing one asset), need no package (Vector Graphics, sprite atlases) and no art pipeline, and can be tested: a mesh can be checked against its rect, a ring against its fraction.

**Against:** sprites from an artist, which look richer and can't be matched by strokes and polygons. So a theme can put a sprite in any icon's place by name (its Icon Overrides): an artist's set can replace the drawn icons one at a time, with no code changed. The display font is a real font (Chakra Petch, SIL OFL), not drawn: text needs kerning and shaping a mesh of strokes wouldn't have.

## Wall runs and wall slides only on walls marked for them (November 2026)
A wall is run along or slid down (and so wall-jumped off) only if it's marked for that move: a `TraversableWall` on it, for Wall Run or Wall Slide, never both. Wall Running's Wall Run On Any Wall and Wall Sliding's Wall Slide On Any Wall put back the old way, every upright wall, for a character or game that wants it; both are off by default.

**Why:** with every wall runnable, the moves were hard to balance. Every wall in every level was a wall run and a wall jump, so a level had to keep its walls away from whatever it didn't want crossed (the Tutorial Ring's walls stopped short of every gap and climb, leaving it full of openings), and making the moves more forgiving made every wall more of a shortcut. Marked walls make the moves a level's choice, as climbing already was (`ClimbableSurface`): a plain wall beside a gap is just a wall, and the moves can be as forgiving as they like where they're meant to be. A wall is for one move, never both, because they're different promises: a wall to run along isn't one to climb by kicking off, and a player reading the wall's pattern should know which it is.

**Against:** a per-level list of walls, or a physics layer. A layer would cost one of the project's few, clash with what a game uses its layers for, and couldn't mark a wall for one move and not the other. A component goes where the wall is, is found the same way as climbing's, and can be turned off.

## The Tutorial Ring's height shading is a full-screen pass (November 2026)
The Tutorial Ring darkens things by their height (lower, darker; the walls' tops untouched), so its long white walls and decks read by depth. It's one full-screen pass after post-processing (a URP Full Screen Pass renderer feature with the Height Shade shader) that works out each pixel's world height from the depth and scales its colour, turned on by a `HeightShade` component in the scene through a global shader value.

**Why:** the levels share their materials (Level_[Surface].mat, NoiseConcrete), so shading them by height in their shaders would have changed every level, or meant a copy of every material for the ring. A pass changes no material, reaches everything (decks, walls, trim, fixtures) the same, and is off wherever no scene turns it on. After post-processing, it scales the colour as it's shown, not HDR values the tonemapping would squeeze back towards white.

**Against:** a vertex-colour or lightmap gradient baked into the ring's meshes, which would cost nothing at runtime but miss what isn't graybox (the fixtures, the character), and need the builders to know about it.

## Replays are input frames with keyframes, and the pause menu reaches them through a bridge (November 2026)
A replay (`Replay`, recorded by `ReplayRecorder`) is the player's character's input frames, one a step, and a keyframe every couple of seconds: the character's whole movement state and the level's moving parts' (`StateSerializer`), and the level's clock. Playback puts the character back at the first keyframe, drives it with the frames, and snaps it to each keyframe as it comes.

**Why:** the frames alone replay exactly on the build that recorded them (the movement is deterministic on one build), and they're small. But a playtest's replay is watched on another build, the developer's editor, where floating point can differ and a run would drift. Keyframes bound the drift to a couple of seconds and put it right, at the cost of a few MB for a long session. Both halves already existed (`InputRecording`, `StateSerializer`, made for networking).

**How it meets the UI:** the UI knows nothing of the character and the character nothing of the UI, so the UI has a generic hook (`IReplayControls`, registered with `ReplayControls`), and `ReplayMenu`, in a bridge assembly referencing both (`MeridianMotion.CharacterUI`, category I's first step, done early), implements it. The pause menu shows its Replays tab only while something's registered.

**Against:** recording the character's pose each step (a "ghost"), which can't be continued from (Continue playing needs the real state, which a keyframe is), and video, which can't be inspected or resumed.

## The level's volumes are checked from the character's step, not by triggers (November 2026)
Checkpoints, hazards, shortcut triggers and lesson zones (`LevelVolume`) are boxes with no collider. After each of a character's steps, its `LevelPresence` asks which hold its feet, from its physics pose, and the volumes act then: a hazard respawns it at once, between steps.

**Why:** the character has no collider of its own (what meets it is its model's hitboxes, and a character without its model, as a server would run it, has none), and a trigger's callbacks come at the physics step, after the characters have stepped, in an order of Unity's. Checked from the step, a respawn happens at a known step, the same in a replay and with or without a model. Where the character is in them (`LevelPresence`, with the checkpoint it last reached) is part of its state, so keyframes and saves carry it; a shortcut gate's opening is one of the level's moving parts, as a pushed crate is.

**Against:** triggers, as the timing gates use (a timing gate only times, so when its callback comes doesn't move anyone).

## The levels' grass is placed on the GPU each frame, not baked (November 2026)
The lawns' grass isn't stored. Each lawn group has a Grass Field holding a mask of where its lawns are; before each camera renders, a compute shader places a blade on every cell of a fine grid over the mask's chunks in view, by a hash of its cell, culled and thinned with distance, into two lists (near and far) that are drawn with two indirect draws.

**Why:** the grass it replaced was baked: thin tufts of blades stored in the scene, about 3 blades a square metre, 25 MB of meshes in MeridianComplex, cut off by distance in chunks. A lawn that reads as grass needs a hundred times the blades, which baked would be gigabytes, the same everywhere however far. Placed each frame, only what's near and in view exists, thinning out with distance as the square of it (about as many blades to a patch of the screen); the scenes got smaller, not bigger; and the grass can be restyled live (a style asset, read every frame) and moved (wind, characters pushing it) without rebaking anything. Which blades go is set by a rank on each cell, cells of every 2nd, 4th and 8th row and column the last, so a far chunk looks at only the cells it could keep and nothing pops as the camera moves.

**Against:** baked blades as GameObjects or meshes drawn through the GPU Resident Drawer or a BatchRendererGroup, which suit many separate objects, not millions of blades placed per view; and alpha-tested grass cards, as cheap but shimmering and overdrawing far off, and less like the levels' low-poly look. Each camera has its own blade lists, so the Scene view placing its blades can't change what the game camera draws; sharing one set across fields (fewer draws, less memory) was weighed and left for later: fields with different styles can't share a draw.

## The grass's shaders are hand-written, not Shader Graph (November 2026)
The blades' shader (Grass.shader), the lawns' (Lawn.shader) and what they share with the compute shader (GrassCommon.hlsl: the hash, the clumps) are HLSL.

**Why:** the blades are drawn with `Graphics.RenderPrimitivesIndexedIndirect`, their vertices made in the shader from the blade the compute shader wrote; Shader Graph reaches indirect, procedural draws only through Custom Function workarounds, and the lawns' tint has to match the blades' to the hash, which shared HLSL does. Two lessons are in their comments: the main light's `distanceAttenuation` is left out (it read 0 for the lawns, given a property block, leaving them unlit by the sun), and a blade's position along itself is clamped before `pow` (a hair below 0, it gave NaN pixels that bloom spread into flashes across the screen).

**Against:** Shader Graph, for being editable without code: the grass's look is a style asset's settings instead, which are.

## Characters push the grass with their legs' hitboxes (November 2026)
A Grass Bender on each character pushes the grass with the hitbox capsules on its model's bones in the lowest part of its height (its lower legs and feet), found by the hitboxes' layer; the pushes go into a map round the camera that springs back, which the blades read.

**Why:** one circle at the feet looked like a shape dragged along the ground. The legs part the grass where they step, as many legs as the character has (the fox's four), let it go when a foot lifts, and lay it the way they went. Going through the hitboxes' layer keeps the grass from knowing the character: Set Up Scene gives each spawned character a bender through the spawner's events, and a character without hitboxes pushes with a circle at its feet.

**Against:** the Animator's foot bones (a humanoid's only), and colliders on the grass (the movement never reads it, and nothing needs to stop at it).

## The flora is leaf cards from a generator of our own, on the grass's wind (November 2026)
The levels' trees, bushes, ferns, ivy and vines are leaf-card models made in Blender by the project's own script (`Tools/Flora/generate_flora.py`, run by Generate Flora), each variant from a seed of its own, with LODs down to a crossed-card billboard; their shaders (Flora Leaf, Flora Bark) are hand-written HLSL, swaying in the grass's wind and recoloured to the grass's palette.

**Why:** the faceted trees and shrubs read as placeholders beside the new grass. Generated, the plants are ours to change (a seed, a kind regenerated) and come in as many variants as wanted, and a level's flora seed reshuffles them without moving anything. Hand-written shaders, as the grass's are, avoid Shader Graph's workarounds on Metal and share the grass's wind and colours, so a lawn and the bush on it move and look as one; the grass's lessons (no main-light `distanceAttenuation`, nothing that can make a pixel that isn't a number) apply to both.

**Against:** the reference set's own Unity side (an import postprocessor keyed on a folder name, Shader Graphs needing workarounds, a wind of its own), and its flat-colour low-poly set, too close to what it replaced.

## The GPU Resident Drawer is left off (November 2026)
The flora's shaders support it (DOTS instancing in every pass), and it was measured on and off in the levels; it's off.

**Why:** it made no measurable difference to a frame, and its GPU occlusion culling made the Meridian Complex slower (there's little to occlude there). What it saves is the CPU's draw submission, which these levels barely ask of it; copies of a plant are already drawn instanced. Turn it on in the PC render pipeline asset if a profile ever shows the flora's draw calls (a build with many cameras or characters, say): the shaders are ready for it.

**Against:** keeping it on for its own sake, and every LOD but the first two not casting shadows (cheaper, but far trees without shadows looked like they floated).

