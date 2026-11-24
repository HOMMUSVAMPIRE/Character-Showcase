# Level Specs

Each level can also be kept as data: a level spec (Assets/Showcase/Levels/[scene].level.json), listing the pieces it's built from in order, with their settings. A spec can be exported from a level's builder, edited in the Level Editor, written by hand or by other tools as JSON, XML or CSV, checked against what the character can do, drawn as a schematic, and built into a scene like any level.

**The format.** A spec is JSON, one line per piece:

```
{"format": "character-showcase-level", "version": 1, "name": "Level Playground", "scene": "Assets/Showcase/Scenes/LevelPlayground/LevelPlayground.unity",
 "parts": [{"name": "Movement Tests", "pieces": [
   {"piece": "Group", "parent": "", "name": "Movement Tests"},
   {"piece": "Platform", "parent": "Movement Tests", "name": "Beam Start (+4)", "x0": -38, "x1": -34, "z0": -40, "z1": -36, "y0": 0, "top": 4},
   ...]}]}
```

Each piece is one of the builders' pieces (`Graybox`'s methods marked `[Piece]`), its settings named as its parameters; settings left out take their defaults.

Points are `[x, z]` (or `[x, y, z]`), areas `[xMin, zMin, xMax, zMax]`, surfaces by name, and a parent is a path under the level's root (`""` the root, `"Building/Catwalk (+4)"`, `"G#2"` for the second group named G). `Placed` and `Turned` make groups set at a position or turned, whose contents are laid out in their own coordinates. A `"State"` entry changes the stair step size or the deck and rail dimensions for the pieces after it in its part.

The whole format, every piece and its settings and defaults, is described by a JSON Schema, Tools/LevelSpec/level-spec.schema.json, written by Unity from the pieces themselves (Tools > Meridian Motion > Showcase > Levels > Specs > Write Spec Schema, or `-executeMethod MeridianMotion.Showcase.Levels.LevelSpecTools.WriteSchemaFromCommandLine`, and every export).

**The specs in Assets/Showcase/Levels:**
- MeridianComplex.level.json and LevelPlayground.level.json are exported from those levels' builders.
- MeridianBasin.level.json has no builder: it's written by a script, Tools/LevelSpec/examples/meridian_basin.py, an example of a spec made with code (see [The Meridian Basin](levels.md#the-meridian-basin)), and its scene is built from it.

The levelspec tool's tests check every spec there (valid, and converting to every format and back unchanged), so one saved there has to stay valid; Unity's LevelSpecTests rebuild only the two with builders from their specs.

**In Unity** (Tools > Meridian Motion > Showcase > Levels > Specs):
- **Export Level Specs** records each level's builder as it builds and writes its spec, then builds the level again from the spec and checks it's the same, object for object (the Meridian Complex and Level Playground both are). A spec that's been changed since it was exported (in the Level Editor, say) is only replaced if you say so.
- **Build Level from Spec…** builds a level from a spec as its builder would: the scene the spec names, with its environment and bakes, and its mechanics set up on their markers (`LevelFixtureSetup`, as every build does), ready for Set Up Scene to add the character and camera.
- From the command line: `-executeMethod MeridianMotion.Showcase.Levels.LevelSpecTools.ExportFromCommandLine` (optionally `-level "Level Playground"`, and `-force` to replace specs changed since they were exported; it exits with 1 if a spec doesn't build its level or was kept) and `...LevelSpecTools.BuildFromCommandLine -spec Assets/Showcase/Levels/LevelPlayground.level.json`.

**The Level Editor** (Tools > Meridian Motion > Showcase > Levels > Level Editor) edits a spec and shows it as it changes. Open a spec (or start a new level), then Preview builds it in an unsaved scene, without bakes.

The window shows the spec's parts and pieces in a list, the picked piece's settings, and a plan of the level drawn from the preview, shaded by height. On the plan, right-drag or Alt-drag pans and the wheel zooms.

Pick a piece in the list, on the plan or in the Scene view (each selects the others), and change its settings in the inspector, drag it on the plan, or move it with the Scene view's handle (both snap to the grid: the Snap setting in the toolbar). Each edit rebuilds just the piece it changed, with the finishing touches that look at what's round them (lawns, trees, spawn pads), so the preview stays as a full build would make it.

Adding and removing pieces:
- **Add** (above the list, or in the panel the editor shows in the Scene view's corner) lists every kind of piece by what it's for. Pick one, then click where it goes on the plan or in the Scene view: it's put there, standing on whatever's under the click, in the picked piece's group (or the picked group), after the picked piece. Enter puts it at the middle of the plan; Esc cancels. Each kind starts small, ready to be sized in the inspector.
- **Right-click** on the plan for Add Here (at the click), and Duplicate, Delete, Move Up/Down or Frame for the piece under it; on a piece in the list for the same; on a part's name to add a piece or part, or delete the part.
- **Duplicate** (Ctrl/Cmd+D) copies the picked piece, put a little way from it (`DuplicateOffset` in `LevelEditorWindow`); **Delete** (the Delete key) removes it, in the window or the Scene view, from the spec and the scene. The inspector has both buttons too.
- Pieces whose objects are deleted from the scene another way (the Hierarchy's Delete) are taken out of the spec as well, so the two agree (undo that with Edit > Undo).

Pieces can be reordered in their part (▲ ▼), and parts added, renamed, reordered and deleted. Edit > Undo and Redo step through the edits.

Save writes the spec. Build Level builds the scene, with its environment, bakes and mechanics; then Set Up Scene… makes it playable (the character, its camera and input). Schematic opens the level's schematic with the movement checks (it needs Python 3).

**Outside Unity**, the levelspec tool (Tools/LevelSpec, Python 3 with nothing else to install) reads specs written as JSON, XML or CSV, checks them, converts between them, and draws schematics:

```
python3 Tools/LevelSpec/levelspec validate my_level.xml          # against the schema: every problem, where it is
python3 Tools/LevelSpec/levelspec check my_level.xml             # against what the character can do
python3 Tools/LevelSpec/levelspec schematic my_level.xml plan.html   # or plan.svg
python3 Tools/LevelSpec/levelspec convert my_level.xml my_level.csv  # any format to any other
python3 Tools/LevelSpec/levelspec build my_level.xml --unity     # write Assets/Showcase/Levels/[scene].level.json and its schematic, then have Unity build and set up the scene
python3 Tools/LevelSpec/levelspec pieces [Platform]              # the pieces, or one piece's settings
```

**Formats.** XML and CSV take settings as text: numbers, points as `136, 23`, lists with `;` between items (`18, 7.5; 7.5, 18`), or any value as JSON. In XML, each piece is an element named for it (`<Platform name="A" x0="0" x1="12" .../>`), and pieces inside a `<Group>` are put in it. In CSV, each row is a piece and each column a setting, with a `Level` row giving the level's name and scene.

See Tools/LevelSpec/examples for a level written in each, and the notes in Tools/LevelSpec/levelspec/formats.py. Converting a spec to XML or CSV and back gives the same spec, byte for byte.

**Movement checks** hold a level to what the character can do: Tools/LevelSpec/envelope.json, written from the default character's [capabilities](../movement.md#capabilities) (the Character prefab with its model, measured by running its movement) by Tools > Meridian Motion > Showcase > Levels > Specs > Write Movement Envelope (or `-executeMethod MeridianMotion.Showcase.LevelChecks.MovementEnvelopeWriter.WriteFromCommandLine`). The writer is in Assets/Showcase/Editor/LevelChecks, the level checks' own editor assembly (`MeridianMotion.Showcase.LevelChecks`), which isn't part of the package. Don't edit envelope.json. Saving a change to the tuning, the character or its model in the editor writes it again (`MovementEnvelopeAutoWriter`), and the Console logs what changed (a jump that reaches further, a move added or gone); `MovementEnvelopeTests` fails if it's out of date anyway (after a change made outside the editor, say). Its `maxRampGradient` is the levels' own rule, LevelBuilderSettings' Max Ramp Gradient under Stairs, gentler than the character's walkable slope (`walkableSlope`). The checks report stairs with steps over its `stepHeight`, ramps steeper than its `maxRampGradient`, and low beams lower than its `crouchHeight`.

Between the level's blocks and platforms, they report each gap at the same height and each climb to a neighbour, with the move it takes (the first of the envelope's `jumps` or `climbs` that reaches: a sprint jump across a gap, say, or a double jump up to a ledge) or that it's out of reach. Walls (Panel blocks) aren't counted as somewhere to stand. The checks are reports, not errors: a pit too wide to jump may have stairs out of it.

**Schematics** are drawn from a spec: a plan of every piece where it is, shaded by height, with stairs and ramps, guardrails, mechanics (moving platforms with their travel, crates, timing gates, low beams), markings, lawns and trees, and the movement checks over it. As HTML (pan and zoom, layers to show and hide, each piece's settings on hover, the checks listed and found on the plan) or SVG. The plans are worked out from the pieces' settings (Tools/LevelSpec/levelspec/geometry.py), so a new piece needs an outline there to be drawn as more than a mark.

## The lock check
A level written by a script can hold each of its crossings (a gap, a climb, a low passage) to the moves it's meant for: the Tutorial Ring's script does (see [The Tutorial Ring](levels.md#the-tutorial-ring)). The lock check, Tools/LevelSpec/levelspec/locks.py, takes each crossing (a `Crossing`: its kind, how far or high or long it is, the moves meant for it, and its geometry) with the moves the player has by then, and the envelope (without its air jumps, for the Tutorial Ring: `no_air_jumps`). A crossing passes when:
- each move meant for it reaches it with room to spare: what it needs is at most `INTENDED_SHARE` of the move's reach. Moves the envelope doesn't measure (a wall jump, a rope, a zipline, a ladder, a climbable panel, a crate, a bounce pad, swimming) reach only where the crossing has their fixture or geometry (`FIXTURE_MOVES`);
- and every other move falls short with room to spare (its reach at most `OTHER_SHARE` of the need), or the crossing's geometry rules it out: a ceiling lower than the standing height rules out every jump and the wall run (one higher only shortens a jump, so it isn't counted); no wall beside it, the wall run and wall jump; something in the dash's line, the dash; no run-up, the sprint jump and the slide; moving blockers, crouch-walking; deep water, everything but swimming.

A crossing can accept a move besides its own (its `also_allowed`): a gap a sprint jump is meant for can always be dashed across too, as nothing in a gap stops a dash's straight line without stopping a jump's. A move missing from the envelope (or NaN) raises `LockError`, rather than the level being sized for a character it isn't played with. A move the envelope doesn't measure and that has no fixture (the dash on its own) counts as reaching nothing, so the check can't see it: a crossing it could cross is only as locked as the dash's reach allows, and the Tutorial Ring's notes say where that's accepted. The check is in the script's terms, the envelope's figures and each crossing's description of itself: what it can't see (a blocker's timing, whether a slide carries over a gap) is checked in play, by the level's PlayMode tests.
