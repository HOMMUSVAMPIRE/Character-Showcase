# Test Levels
The generated graybox levels, their mechanics, and how they are built. Level specs, the format the levels are recorded in, are in [level-specs.md](level-specs.md).

The exact dimensions of every layout are in its builder (Assets/Editor/Level), and for the Meridian Complex also in each area's notes (the *_Blockout_Notes.md files in Assets/Scenes/MeridianComplex), which say how its diagram was read and what was chosen where it was ambiguous. This page describes the layouts in words.

## Test Level: The Meridian Complex
The Meridian Complex is a large graybox level for putting the character through its paces: terraces, stairs, ramps, bridges, towers, tunnels and measured test courses, in connected areas round a central hub. It's built from blockout diagrams (the MeridianComplex-*.png sheets at the repository's root).

The character isn't placed in the scene: the level marks where it can start (spawn markers on the Concourse plaza), and Set Up Scene makes it spawn at one of them when you press Play (see [Spawning the Character](spawning.md)).

| Area | Where | What it's for |
| --- | --- | --- |
| 01 Concourse | Centre | The hub: a plaza, a raised terrace ring, upper terraces above it, and bridges out to the areas around it |
| 02 Kinetic Gardens | North-west | Open-air traversal: a beginner route of stairs, ramps and short gaps, and an advanced one of longer gaps, a beam and a ledge chain |
| 03 Ascension Spire | North | Vertical traversal: a raised central core and a summit tower, the complex's highest point, climbed by a switchback stair |
| 04 Fabrication Hall | East | An industrial building: rooms on floors from below ground to above it, catwalks over them, crates and shelves |
| 05 Suspension Yard | South-east | Aerial: tower platforms at three heights, suspended bridges, and jump and dash gaps |
| 06 Service Network | Underground | Lit corridors and tunnels joining the gardens, the spire and the hall |
| 07 Calibration Annex | South-west | Measured tests: step heights either side of the step limit, slopes from gentle to far too steep, jump gaps graded from easy to beyond reach, tunnels with falling ceilings, a jump lane, wall-run and ledge-climb faces |

Every area can be reached on foot, by stairs and ramps that keep to the character's step height and slope limit; the pieces that need a jump, a climb or a crouch are marked as such. The mechanics, under Mechanics in each area, add routes of their own (see [Level mechanics](#level-mechanics) for how they're made):
- **Ascension Spire:** a ladder walk from the west platform up to the core, a tall ladder on a pier from the core to a walk round onto the balcony, a climbable panel up the summit tower's west face from the balcony to the summit, a zipline from the summit landing down to the east platform, and a wall-run face along the south terrace.
- **Fabrication Hall:** pushable crates (one pushed against the crate store's stack to climb to the north catwalk), a lift from the assembly wing up to a spur deck at the catwalks' height, a ladder from the workshop to the ring, a ladder from a catwalk up to a roofline platform on the walls' tops, and a crouch passage between the storage shelves.
- **Suspension Yard:** two chains of rope swings, each hung from a beam on masts: one between the north and north-east towers, one along the diagonal from the hub to the south-east tower.
- **Service Network:** a ladder up each ventilation shaft to a deck outside it at the top; the west one meets the Ascension Spire's skyway (over its guardrail), the east one starts a zipline down to the Fabrication Hall's approach.
- **Calibration Annex:** a ledge-climb test up the arrival platform's south face (a climbable panel, and lanes of shelves stepping up at graded heights, the highest out of reach on purpose), and two wall-run lanes beside the wall-run wall: one a wall run alone crosses and one it needs a jump or a dash off the wall to.

Mechanics the character doesn't have yet (sliding doors, shutters, ceiling climbing, crawl spaces, hidden routes, a higher route over the Suspension Yard) have markers named "(future)", under Future Mechanics.

### Building it
The whole scene is generated: Tools > Character Showcase > Level > Build Meridian Complex rebuilds it (also `-executeMethod CharacterShowcase.EditorTools.Level.MeridianComplexBuilder.BuildFromCommandLine`), replacing it and its environment assets. Rebuilding replaces the scene, so change the builders (Assets/Editor/Level, one per area) rather than the scene.

From the menu it builds in the background, one area at a time, and bakes the lighting in the background too, with a window showing its progress (also under the editor's Background Tasks), so the editor keeps responding. The whole build takes a few seconds. The level's pieces aren't marked Contribute GI (it has no baked or realtime GI to use it), which used to make the bake pack lightmaps for every piece, taking minutes.

Until the environment is replaced, Cancel (or closing the window) leaves the level as it was. After that, closing the window lets the build finish on its own, and cancelling the bake saves the level without its baked environment lighting.

### Collision and pieces
Collision is all box colliders: the angled solids (octagonal platforms, terraces cut at 45°) are filled exactly by several boxes each, rather than with mesh colliders.

Each flight of stairs is one object: a single mesh of the whole flight (treads with an orange nosing along each front edge, risers, a stair-shaped face down each side, no hidden or overlapping faces) with a box collider for each step.

Guardrails are graphite posts (spaced by the generation settings' Post Spacing) under an orange top rail, with a mid rail and an orange fascia band along the edge they stand on, one mesh per run. Each run's collider is still one solid box the rail's full height, so nothing slips between the rails. Underground rooms have a graphite skirting along their walls (to see only: it has no collider).

### Architectural detail and structure
The pieces carry the style reference's architectural detail and structure:
- Long walls have concrete pilasters (spaced by Pilaster Spacing), and every opening a graphite frame with an orange lip (jambs, a header and a sill where the opening has wall above or below it).
- Platforms without guardrails have a graphite coping round their top edge, so their edges (and the ledges to catch) read from a distance.
- Free-standing and tower columns are chamfered, with an orange strip up each face.
- Walkways and bridges stand on steel I-beam columns, suspended bridges hang on a truss under each side, ramps rest on I-beam supports, and the Fabrication Hall's north catwalk stands on braced frames on the floors below it, with brackets where it goes through the walls.

All of the detail is to see only: columns keep the box collider of their footprint, and pilasters, frames, copings and brackets have none. Trusses and braced frames, which can be walked into, each have one box collider round the whole of them, so nothing slips between their members.

### Lighting and utilities
Lighting and utilities follow the style reference too. Underground, each room has a ceiling panel at each of its lights and each corridor a strip light down its ceiling, with a steel and an orange pipe run along every ceiling on hangers, and utility stations (a cabinet, a conduit and a vent) on the walls of the Service Network's rooms. The Fabrication Hall's rooms have utility stations too.

Every doorway with wall above it has a light over it; the Concourse plaza has bollards round its edge; the larger Suspension Yard towers each have a floodlight mast; and every bridge between areas has a cyan accent post at each corner, so the ways between areas read from a distance.

The fixtures in daylight glow (with the palette's two glowing materials, Light and Cyan) but cast no light unless you turn on Fixtures Cast Light in the generation settings, since in the midday sun they'd add real-time lights for little; with it off, they add none to the level's count. Bollards, floodlight masts and the stations' cabinets stand where they can be walked into and have box colliders; the rest is to see only.

### Markings and wayfinding
Markings and wayfinding finish it:
- hazard stripes (orange and graphite, raised slightly like thick paint: the markings' Thickness) along the edges you jump from at every marked gap;
- corner brackets and painted values (the step height, gap or slope each lane tests) round the Calibration Annex's test lanes and the playground's;
- a sign gantry over the start of every bridge between areas, naming where it goes;
- and each area's number and name ("01 Concourse" to "07 Calibration Annex") on a sign at its entrance.

The lettering is TextMeshPro; its essential resources are in Assets/TextMesh Pro. Sign panels and their posts have box colliders; markings and lettering are to see only.

### Polish
Polish after the concept art and the icons and decals sheet:
- orange banners with the Meridian mark ("///") on the Concourse's gate towers, the Ascension Spire's summit tower, the Suspension Yard's tall columns and beside the Fabrication Hall's entrance; orange bands up the summit tower;
- the Concourse plaza's tree on the dais, orange and white guide rings round it, orange lanes out to the broad stairs, and planters with trees;
- orange copings on the Kinetic Gardens' platforms;
- lawns with grass tufts and the odd shrub along their edges over the gardens' open ground and every platform top there, and over the Suspension Yard's ground (each lawn keeps back from platform edges and leaves paving round anything standing on the floor, so stairs, columns and routes stay paved: the foliage's Lawn Margins);
- low-poly trees scattered over the gardens' and the Suspension Yard's open ground, only where nothing's within the foliage's Scatter Clearance, so they never crowd a route;
- an orange overhead crane over the Fabrication Hall's workshop, and a measuring ruler beside the Calibration Annex's jump lanes;
- a bracketed pad with cyan chevrons under each spawn point;
- and, under every marker for a mechanic to come, a cyan bracketed square with its name (LADDER, ROPE SWING, PUSHABLE CRATES).

Pads, marks, lawns and trees find the floor under them once the whole level's built (the build's "finishing touches" step). Trees, planters and the crane have box colliders; the rest is to see only. The trees are seeded (the foliage's Seed), so every build grows the same ones.

### Culling
Cameras skip what's out of view (each railing run, stair, lawn chunk and so on is its own object) and, with each level's baked occlusion culling data, what's hidden behind walls, terraces and the ground. Building a level bakes it after the lighting (a few seconds; Assets/Scenes/[Level]/[Level]/OcclusionCullingData.asset), with only the solid pieces (blocks, solids, stairs, columns) as occluders.

Grass tufts cast no shadows and aren't drawn beyond the foliage's Tuft Draw Distance (the lawn under them still is). The game camera needs Occlusion Culling ticked, as the demo's Main Camera has. To see the culling at work, open Window > Rendering > Occlusion Culling, select a camera, and use the Visualization tab.

### Generation settings
The numbers the shared pieces are made with are in one asset, Assets/Editor/Level/GrayboxSettings.asset (Tools > Character Showcase > Level > Generation Settings selects it). Each section of its inspector covers a family of pieces:
- stair profile and nosings;
- deck thickness, bridge length, walkway columns and trusses;
- guardrail height, posts and rails;
- wall pilasters, opening frames and skirtings;
- platform copings;
- column chamfers and accent strips;
- I-beams, trusses, braced frames and brackets;
- light fixtures (ceiling panels, strip lights, door lights, bollards, floodlights, accent posts) and the underground lights;
- pipes, utility stations and vents;
- markings (hazard stripes, corner brackets, floor labels) and signs (gantries, area signs, lettering);
- dressing (banners, guide lines, spawn pads, future-mechanic marks, rulers, tower bands);
- foliage (tree and shrub sizes, planters, how far apart and how much room the scattered trees need, lawn cells and margins, grass tufts' density, height, draw distance and shadows, and the seed);
- mechanics (edge bands, timing gates, low beams, ladders, ropes, ziplines, bounce pads);
- culling (whether to bake occlusion, and its smallest occluder, smallest hole and backface threshold);
- and the sun and haze.

Change a value and rebuild a level to see it (the Level Playground builds in a couple of seconds). It's made with its defaults the first time a level's built, if it's missing. Where the areas put things (their layouts, a stair's width) stays in each area's builder, and how the surfaces look in the materials themselves.

### Materials
The levels share a palette of materials, after the style reference's colours (graphite, concrete, steel grey and an orange accent), all made with the noise-built concrete Shader Graph (Assets/Materials/NoiseConcrete.shadergraph: world-space noise, no textures, with recessed panel seams, metallic and emission settings):
- Concrete (decks, floors, steps);
- Panel (walls and large masses, in panels with recessed seams, each panel a shade lighter or darker);
- Ground (darker, in larger paving slabs);
- Graphite (posts, rails, trims, machinery);
- Steel (columns, supports, catwalks, crates);
- Accent (orange: top rails, nosings, fascias);
- and the glowing Light and Cyan, and White.

The panel and slab sizes are each material's Panel Size. Each piece chooses its own surfaces; builders pick one for a box or solid where the default, concrete, isn't right (`Graybox.Surface`). The materials (Assets/Materials/Level) are made the first time a level's built (from the looks in `GrayboxScene`) and then kept, so they can be tuned by hand; delete one to have the next build make it afresh.

### Environment
It's lit as a sunny midday for URP: a high sun with soft shadows, a procedural sky that lights the ambient and reflections, light haze, and a global post-processing volume (ACES tonemapping, light bloom, a little contrast). The sun and haze are the generation settings' Environment section. The PC render pipeline asset (Assets/Settings/PC_RPAsset.asset) has its shadow Max Distance raised well above URP's default, so the terraces' shadows reach across the hub.

## The Level Playground
The Level Playground (Assets/Scenes/LevelPlayground) is a small test scene with one of each piece the complex is built from, made by the same builders' pieces in the same material and lighting:
- an octagonal dais and a turned column at the spawn point;
- a terrace cut at 45° up a ramp, with guardrails;
- a stair to a walkway on columns, two octagonal towers joined by a suspended walkway, a jump gap and a diagonal bridge out to a connection marker;
- a walled building with a doorway, a window, a crate and a shelf;
- an underground stairwell, passage, lit room and ventilation shaft;
- one of each of the Calibration Annex's tests (steps either side of the step limit, a ledge, a slope, a low tunnel, a narrow beam beside a wall-run wall);
- one of each mechanic (a lift, a ladder up to the lift's landing, a slide gate, a timed run and a crate to push; see [Level mechanics](#level-mechanics));
- in the far west, the mechanics in the air: a tower, up a ladder or a bounce pad, with a zipline from its top south down to the ground, and a rope on a gantry over a gap between two raised platforms, too far for a sprint jump (a sprint jump and a well-timed air dash can just cross it), the near one with a climbable panel up its side;
- and placeholders for mechanics to come.

Its dimensions are in `LevelPlaygroundBuilder`. It builds in a couple of seconds (Tools > Character Showcase > Level > Build Level Playground, or `-executeMethod CharacterShowcase.EditorTools.Level.LevelPlaygroundBuilder.BuildFromCommandLine`), so changes to the level's pieces can be tried there before rebuilding the whole complex. Like the complex, rebuilding it removes its game setup: run Set Up Scene again afterwards.

## Test Level: The Proving Ground
The Proving Ground (Assets/Scenes/ProvingGround) is a level for trying out the movement: one circuit through a series of zones from a start hall, each pushing one family of moves. Its gaps, runs and ledges are set against what the character can do with the movement mode as it's tuned now, measured with the real movement code: how far each kind of jump crosses, how high a ledge can be caught from a jump and a double jump, and how far a slide, a roll and a wall run go. Those limits are the character's [capabilities](movement.md#capabilities), written to Tools/LevelSpec/envelope.json; the layout constants at the top of `ProvingGroundBuilder` are set against them, and retuning the movement may call for the layout to be revised with them. Every challenge has a way back up from a fall.

- **P0 Start Hall**: an octagonal plaza with spawn points, and the lap gate, timing each lap of the circuit.
- **P1 Sprint Line**: a long straight east. The north lane is a speed trap (timed between two gates, with its time and speed on the finish gate, and a ruler marked along it); the south lane has slide gates too low to stand under, spaced further apart than one slide goes, and kerbs either side of the step height (one to hop, one to run up).
- **P2 Gap Run**: a ramp up to raised platforms heading north over a park, the gaps between them graded to the jumps, from a sprint jump through to a double jump and air dash, each painted with its width, with stairs back up from below.
- **P3 Wall-Run Canyon**: a narrow canyon between tall walls, over a pit, from a run-up west to two landings and an exit: two runs with a wall jump between, and one longer than a single run can go, so it has to end in a jump.
- **P4 Ledge Tower**: a suspended walkway from the canyon to a deck, and ledges up to a summit, each a jump or a double jump above the last (as painted on each); a chimney to climb by wall jumps; and a stair back down.
- **P5 Low Works**: an enclosed, lit hall with a long crawl tunnel too low to stand in, slide gates, a roll lane under hoods, a ledge out of reach without a crate to stand on and a double jump, and two crates to push (one, in an alcove, has to be pulled out).
- **P6 Moving Ground**: a pit crossed on shuttles, a turntable and islands, with a lift up from its floor and stairs out at both ends.

Orange guide lines on the ground lead from each zone to the next. Build it with Tools > Character Showcase > Level > Build Proving Ground (or `-executeMethod CharacterShowcase.EditorTools.Level.ProvingGroundBuilder.BuildFromCommandLine`), then Set Up Scene: as with the other levels, its moving platforms, crates and timing gates only work once the scene is set up.

## Level mechanics
The level builders can't reference the game's code, so a mechanic is built as a fixture: its geometry, named for what it is, with its settings after the name. Set Up Scene turns each into the real thing (`LevelFixtureSetup`):
- "Moving Platform (…) | travel=x,y,z | period=s | pause=s | spin=x,y,z | phase=f | smooth=0/1" gets a kinematic Rigidbody and a MovingPlatform with those settings;
- "Pushable Crate | pull=0/1 | speed=f" gets a Rigidbody and a PushableObject (Can Be Pulled, Speed Multiplier);
- "Ladder | height=m | width=m | spacing=m | rail=m | rung=m" gets a Ladder with those settings, which keeps the level's mesh ("Ladder (future): …" markers aren't ladders);
- "Rope | length=m | thickness=m" (at its anchor) gets a Rope, which draws itself as it swings, in the level's material ("Rope Anchor" placeholders and "Rope Swing (future)" markers aren't ropes);
- "Zipline | length=m | speed=m/s" (at the cable's start, its +Z along it) gets a Zipline that keeps the level's cable;
- "Bounce Pad | height=m | keep=f" gets a BouncePad, its "Spring" child squashing on each launch;
- "Climbable Panel | speed=f" gets a ClimbableSurface (the whole panel climbable, at that speed multiplier); only the panel is climbable, not the wall it stands on, so a climb stops at its edges;
- "Timing Gate | course=name | role=start-finish/start/finish | distance=m" (a trigger) gets a TimingGate, and each course a CourseTimer under the Game Setup object, told by the spawner who's racing; a gate's display, "Timing Display | course=name", shows its course's time under way, the last and best, and (with a distance) the speed.

A CourseTimer times laps through one start-and-finish gate, or runs from a start gate to a finish gate, in game time. A pass too soon after a lap starts (the CourseTimer's Minimum Run Time) is ignored. Only its racer counts once it has one; until then anything passing with a Rigidbody does. The fixtures can also be added and named by hand in any scene that's then set up.
