# Test Levels
The generated graybox levels, their mechanics, and how they are built. Level specs, the format the levels are recorded in, are in [level-specs.md](level-specs.md).

The exact dimensions of every layout are in its builder (Assets/Showcase/Editor/Levels), or for the [Meridian Basin](#the-meridian-basin), which has none, in the script that writes its spec, and for the Meridian Complex also in each area's notes (the *_Blockout_Notes.md files in Assets/Showcase/Scenes/MeridianComplex), which say how its diagram was read and what was chosen where it was ambiguous. This page describes the layouts in words.

## Test Level: The Meridian Complex
The Meridian Complex is a large graybox level for putting the character through its paces: terraces, stairs, ramps, bridges, towers, tunnels and measured test courses, in connected areas round a central hub. It's built from blockout diagrams (the MeridianComplex-*.png sheets in ReferenceAssets/Image References/Schematics/, which git ignores, so they're only on the machine that has them, not in a clone).

The character isn't placed in the scene: the level marks where it can start (spawn markers on the Concourse plaza), and Set Up Scene makes it spawn at one of them when you press Play (see [Spawning the Character](../spawning.md)).

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
The whole scene is generated: Tools > Meridian Motion > Showcase > Levels > Build Meridian Complex rebuilds it (also `-executeMethod MeridianMotion.Showcase.Levels.MeridianComplexBuilder.BuildFromCommandLine`), replacing it and its environment assets. Rebuilding replaces the scene, so change the builders (Assets/Showcase/Editor/Levels, one per area) rather than the scene.

From the menu it builds in the background, one area at a time, and bakes the lighting in the background too, with a window showing its progress (also under the editor's Background Tasks), so the editor keeps responding. The whole build takes a few seconds. The level's pieces aren't marked Contribute GI (it has no baked or realtime GI to use it), which used to make the bake pack lightmaps for every piece, taking minutes.

Until the environment is replaced, Cancel (or closing the window) leaves the level as it was. After that, closing the window lets the build finish on its own, and cancelling the bake saves the level without its baked environment lighting.

### Collision and pieces
Collision is all box colliders: the angled solids (octagonal platforms, terraces cut at 45°) are filled exactly by several boxes each, rather than with mesh colliders.

Each flight of stairs is one object: a single mesh of the whole flight (treads with an orange nosing along each front edge, risers, a stair-shaped face down each side, no hidden or overlapping faces) with a box collider for each step.

Guardrails are graphite posts (spaced by the generation settings' Post Spacing) under an orange top rail, with a mid rail and an orange fascia band along the edge they stand on, one mesh per run. Each run's collider is still one solid box the rail's full height, so nothing slips between the rails. Underground rooms have a graphite skirting along their walls (to see only: it has no collider).

### Architectural detail and structure
The pieces carry the architectural detail and structure of the style reference (MeridianComplexStyleReference.png, in ReferenceAssets/Image References/Concept Art):
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
Polish after the concept art and the icons and decals sheet (MeridianComplexConceptArt.png and MeridianComplex_Icons&Decals.png, in ReferenceAssets/Image References/Concept Art too):
- orange banners with the Meridian mark ("///") on the Concourse's gate towers, the Ascension Spire's summit tower, the Suspension Yard's tall columns and beside the Fabrication Hall's entrance; orange bands up the summit tower;
- the Concourse plaza's tree on the dais, orange and white guide rings round it, orange lanes out to the broad stairs, and planters with trees;
- orange copings on the Kinetic Gardens' platforms;
- lawns with grass and the odd shrub along their edges over the gardens' open ground and every platform top there, and over the Suspension Yard's ground (each lawn keeps back from platform edges and leaves paving round anything standing on the floor, so stairs, columns and routes stay paved: the foliage's Lawn Margins);
- low-poly trees scattered over the gardens' and the Suspension Yard's open ground, only where nothing's within the foliage's Scatter Clearance, so they never crowd a route;
- an orange overhead crane over the Fabrication Hall's workshop, and a measuring ruler beside the Calibration Annex's jump lanes;
- a bracketed pad with cyan chevrons under each spawn point;
- and, under every marker for a mechanic to come, a cyan bracketed square with its name (LADDER, ROPE SWING, PUSHABLE CRATES).

Pads, marks, lawns and trees find the floor under them once the whole level's built (the build's "finishing touches" step). Trees, planters and the crane have box colliders; the rest is to see only. The trees are seeded (the foliage's Seed), so every build grows the same ones.

### Culling
Cameras skip what's out of view (each railing run, stair, lawn chunk and so on is its own object) and, with each level's baked occlusion culling data, what's hidden behind walls, terraces and the ground. Building a level bakes it after the lighting (a few seconds; Assets/Showcase/Scenes/[Level]/[Level]/OcclusionCullingData.asset), with only the solid pieces (blocks, solids, stairs, columns) as occluders.

The grass is drawn by its own culling, not by these: see Grass, below. The game camera needs Occlusion Culling ticked, as the demo's Main Camera has. To see the culling at work, open Window > Rendering > Occlusion Culling, select a camera, and use the Visualization tab.

### Grass
The lawns' grass isn't stored in the scene. Each Lawns piece adds a Grass Field (a "Grass" object in its Lawns group, `Assets/Showcase/Grass`) holding a mask of where its lawns are, one layer per floor: how much grass there is (thinning a little way in from the lawns' edges) and how near the edge it is. Before each camera renders, a compute shader places a blade on every cell of a fine grid over the mask's chunks in view, where the mask has grass, each nudged and sized by a hash of its cell, so it stays put as the camera moves. Blades grow in clumps (a pattern over the ground) that differ in length and tint and lean out from their middle, and grow longer at the lawns' edges, against walls and paving. The field draws them in two indirect draws: near the camera, curved blades with a segment for every so much of their length, drooping when they're long; beyond, a triangle each. Toward the draw distance the blades thin out, the ones left widened to cover, and shrink into the ground, so nothing pops. They have no collider.

The lawns under the grass are cut across their cells' corners rather than round them in steps, with a bevelled edge down to the floor, and drawn with the Lawn shader (`Assets/Showcase/Grass/Lawn.mat`). Near the camera a lawn is the blades' root colour in their shade; toward the draw distance it turns to the colour of the grass as a whole, tinted by the same clumps, so where the blades stop, the lawn carries on as the same surface. The field sets the lawns' colours from its style.

How the grass looks, and how much there is, is its Grass Style (Create > Meridian Motion > Showcase > Grass Style, set on the field; without one, the field uses the style's defaults):
- the blades: Length (from a short lawn to tall wild grass), Width, Bend, Droop, Spacing and Edge Length;
- the clumps: Clump Size, Clump Length and Clump Lean;
- the colour: Root Colour, Tip Colour, Colour Variation, Clump Tint, Root Shade, Ground Shade and Translucency, and whether the blades cast shadows (Casts Shadows);
- the distances and detail: Draw Distance, Detail Distance, Thin From, Thin To, Segment Length and Segments.

The grass sways in the level's wind: a Grass Wind object (the builders add one beside the first lawns) gives its Direction, Strength, gusts (Gust Size, Gust Speed, Gust Strength: patches of stronger wind blowing across the lawns) and each blade's Flutter; how much a lawn's grass moves in it is its style's Sway (the wind pushes a blade's tip by some of its length, so long grass sways further). Characters push it aside as they go through it: Set Up Scene adds a Grass Benders object to the showcase's levels with grass and connects the spawner's On Character Spawned and On Npc Spawned to it, so each character spawned gets a Grass Bender. It pushes with its legs: its hitboxes (capsules on its model's bones, on the Grass Benders' Hitbox Layer) whose middles are in the lowest Leg Share of its height, its lower legs and feet, however many legs it has. The grass near each bows out from it, and partly along the way it's going (behind it, all the way it went, so a walk leaves a trail laid forward), while the leg's down among the blades (a lifted foot lets them go), and springs back soon after. A character without hitboxes (no model, or before its model's loaded) pushes with a circle at its feet, the Grass Benders' Radius wide. Only what's near the camera is pushed (`GrassBending`, a map round it). The movement reads neither: they're only seen.

A field reads its style every frame, so a change shows at once, in the editor and in play mode. Two styles to start from are in `Assets/Showcase/Grass/Styles`: Lawn (short and dense; the builders give it to new lawns) and Wild Grass (long, looser and drooping). Tools > Meridian Motion > Showcase > Create Grass Assets makes them, and the lawn material, where they're missing. A style's inspector has a Use in the Open Scenes button, which gives it to every grass field in the open scenes (Undo puts the old ones back). A Lawns piece's density scales the grass's (1 is the style's full density).

Platforms without compute shaders draw no blades, only the lawn. Tools > Meridian Motion > Showcase > Measure Grass (also `-executeMethod MeridianMotion.Showcase.Grass.EditorTools.GrassBenchmark.MeasureFromCommandLine`, with `-scene [path]` for one level and `-style [path]` for every level's grass in that style) times each level's grass at fixed spots over its lawns, with and without it, saves a picture of each, and counts the pixels that aren't numbers or are brighter than bloom's threshold, in Logs/GrassBenchmark. For flashes as the camera moves, the PlayMode test `GrassFlashDiagnostic` (explicit: `python3 Tools/run_tests.py --platform PlayMode --filter GrassFlashDiagnostic`) plays MeridianComplex, walks the game camera across a lawn and reports one-frame flashes and pixels that aren't numbers, with what's under them. One such pixel is enough for bloom to spread it into a flash over the screen. `GrassMotionPictures` (explicit too) saves pictures of a lawn in the wind with the player's character walking through it (add `-stillAir` to Unity's command line for no wind). Both skip themselves in batch mode unless the test filter names them.

### Generation settings
The numbers the shared pieces are made with are in one asset, Assets/Showcase/Levels/LevelBuilderSettings.asset (Tools > Meridian Motion > Showcase > Levels > Generation Settings selects it). Each section of its inspector covers a family of pieces:
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
- foliage (tree and shrub sizes, planters, how far apart and how much room the scattered trees need, lawn cells and margins, and the seed; the grass on the lawns has a style of its own: see Grass);
- mechanics (edge bands, timing gates, low beams, ladders, ropes, ziplines, bounce pads, and the cells water's surface is drawn in);
- culling (whether to bake occlusion, and its smallest occluder, smallest hole and backface threshold);
- and the sun and haze.

Change a value and rebuild a level to see it (the Level Playground builds in a couple of seconds). It's made with its defaults the first time a level's built, if it's missing. Where the areas put things (their layouts, a stair's width) stays in each area's builder, and how the surfaces look in the materials themselves.

### Materials
The levels share a palette of materials, after the style reference's colours (graphite, concrete, steel grey and an orange accent), all made with the noise-built concrete Shader Graph (Assets/Showcase/Materials/NoiseConcrete.shadergraph: world-space noise, no textures, with recessed panel seams, metallic and emission settings):
- Concrete (decks, floors, steps);
- Panel (walls and large masses, in panels with recessed seams, each panel a shade lighter or darker);
- Ground (darker, in larger paving slabs);
- Graphite (posts, rails, trims, machinery);
- Steel (columns, supports, catwalks, crates);
- Accent (orange: top rails, nosings, fascias);
- the glowing Light and Cyan, and White;
- Asphalt (dark, rough ground: the Tutorial Ring's hazard), Plate (matte blue-grey painted plate: walls marked for wall runs or slides), and Glass (see-through, made as URP Lit rather than with the concrete shader, as Water has its own: a box of it hides nothing in the occlusion bake and casts no shadow);
- Foliage (the trees' and shrubs' leaves), Soil (trunks and planters' soil) and Grass (lawns);
- and Water, made with its own shader instead (see [The water's look](#the-waters-look)).

The panel and slab sizes are each material's Panel Size. Each piece chooses its own surfaces; builders pick one for a box or solid where the default, concrete, isn't right (`Graybox.GrayboxMaterial`). The materials (Assets/Showcase/Materials/Level) are made the first time a level's built (from the looks in `LevelDefinition`) and then kept, so they can be tuned by hand; delete one to have the next build make it afresh.

### Environment
It's lit as a sunny midday for URP: a high sun with soft shadows, a procedural sky that lights the ambient and reflections, light haze, and a global post-processing volume (ACES tonemapping, light bloom, a little contrast). The sun and haze are the generation settings' Environment section. The PC render pipeline asset (Assets/Settings/PC_RPAsset.asset) has its shadow Max Distance raised well above URP's default, so the terraces' shadows reach across the hub.

## The Meridian Basin
The Meridian Basin (Assets/Showcase/Scenes/MeridianBasin) is a variant of the Meridian Complex sunk into a terraced octagonal pit: a park on the floor and terrace rings stepping up from it to the rim (each part of its spec is a level, L0 Central Park to L5 Operations Ring), with interiors set into the terraces, the Ascension Spire standing in the park with bridges out to the terraces, and the Service Lift as the one tunnel, a shortcut. It's compact and vertical: the walls between the terraces are too high to climb, and besides a stair up each, the ways up are platforming (jumps to ledges, double jumps, lifts, and halls and hatches with a way up inside). It's built from the same pieces, materials and lighting as the complex, after a reference sheet in ReferenceAssets/Image References/Concept Art (MeridianBasinRef.png).

It has no builder. Its spec, Assets/Showcase/Levels/MeridianBasin.level.json, is written by a Python script, Tools/LevelSpec/examples/meridian_basin.py, which works the layout out (eight faces on each ring, the climbs graded to the movement's limits); the script's notes describe it, and its dimensions are in the script. To change it, edit the script, run it (`python3 Tools/LevelSpec/examples/meridian_basin.py`), check the spec (`levelspec check`), and build the scene from the spec: Build Level from Spec… in Unity, then Set Up Scene, or `levelspec build Assets/Showcase/Levels/MeridianBasin.level.json --unity` with Unity closed, which does both (see [Level Specs](level-specs.md)). Export Level Specs doesn't touch it, as only levels with builders are exported. The scene is set up to play (its Meridian Motion Setup: the spawner at its spawn markers, the camera, its moving platforms, crates and timing gates) and is in the build's scenes, after the Level Playground.

## The Tutorial Ring
The Tutorial Ring (Assets/Showcase/Scenes/TutorialRing) is the showcase's tutorial and playtest level: a ring whose first arc teaches every move, one lesson each, and whose second arc is an agility course mixing them, harder as it goes. It's built for playtesting the movement's feel once a player knows it, for footage, and as a base for a larger game's onboarding. The lessons and the course are sized from what the character can do, so retuning the movement regrades them by running the script again.

**The ring.** An octagonal loop of lanes over a ground that's one big hazard, run counterclockwise from the hub (the spawn point and the start checkpoint, at its south-east corner): the lessons up the east side to the north, then the course down the west side back to the hub. Two shortcuts stay closed until they're reached from their far side, then open both ways for the rest of the session (nothing is saved between sessions, so a playtester always starts with the lessons): the lessons shortcut, a shuttle platform straight across the middle between tall walls, from a dock at the lessons' end to one at the hub, quick across and waiting at each end (the script's `SHUTTLE_TRIP` and `SHUTTLE_PAUSE`), its gate on the hub's dock, opened by reaching the lessons' end (the ring's halfway point: its trigger spans the lane's corner there), and the course's end gate into the hub (its trigger on the course side).

**Walls.** The hub is walled all round but for its three ways in (the first lesson's lane, the course's lane and the shuttle's dock), and the shuttle's way has tall walls along all of it. Each shortcut gate is a doorway in the walls: walled above it up to their top, and the course's timing gates stand with their posts in the walls. Tall walls line both edges of the lanes the whole way round, from the ground up, gaps and climbs included; every deck has a wall under each of its edges, ends included, from the ground up to it (as do the hub and the shuttle's docks), so nothing is seen under one from a gap, and every wall and block of the lessons and course goes down to the ground too. Every wall along an edge stands just inside it, its outer face on the edge, so the tall walls, the walls under the decks, the pool's sides and the wall runs' walls line up; where walls turn a corner (the ring's, the hub's), a wedge joins them, and the lanes' and the shortcut's walls run on to meet the hub's. Only walls marked for it can be wall-run along or slid down (see [Walls for wall runs and slides](../traversal.md#walls-for-wall-runs-and-slides)), and the ring's walls aren't, so a wall beside a crossing is no help across it. The marked walls are the lessons' and stretches' that are for those moves, in the ring wall's place there: the wall-run lesson's and the bounce pad stretch's (wall runs, horizontal grooves), the wall-jump shaft's sides (wall slides, vertical grooves), and the corridor cut through solid wall in the course's third stretch (wall runs all along its sides, `CORRIDOR_WIDTH` apart: a run along one, a jump across to a run along the other, and so on up). A wall is for one move, never both; and where the way up is between two walls to the top of a third (the shaft's back, the corridor's end), that wall is plain: the way up is off the walls either side, the shaft's the script's `CHIMNEY_WIDTH` apart. The ring's walls leave open only the hub's and the shuttle's ways in and the roll blocker's way, and beside the pool its sides are the walls (as tall, its arch spanning them); they're too tall (`WALL_HEIGHT`) to get onto from any floor near them, which the script checks against the envelope as it runs.

**The lessons**, in order: move and look; jump; sprint and sprint jump; crouch; slide; roll; vault; ledge grab and climb; wall run; wall slide and wall jump; a climbable panel; a ladder; push and pull; a bounce pad; a rope swing; a zipline; swim and dive; and dash and air dash, last, as the move that most often gets across what another was meant to. Each has a checkpoint at its start, a lesson zone over it showing its prompt, and a lock: geometry it can't be passed without its move (a gap, a climb, a low passage, a shaft, a fixture), with the move it teaches reaching it with room to spare and every move taught before falling short or ruled out by the geometry (a ceiling over a gap rules out jumping, no wall beside it rules out a wall run). The slide and vault lessons only show their moves: a crouch-walk gets through the slide lesson's low slot too, and the vault is close to automatic. The roll lesson's lock is timing: a moving blocker across a low passage, open long enough for a roll and not for a crouch-walk.

**The course** is four stretches, each starting with a checkpoint and a split (the first with the course's start), timed on the course timer Agility with its finish at the end gate. Each mixes moves: slide into a jump, rope to zipline, a bounce pad into a wall run, a wall run into wall jumps, a dash to a ledge grab, a crate to a ledge to a climb. Every crossing a measured move is meant for is a share of the move's reach (the script's `SHARES`, rising stretch by stretch, never past the lock check's `INTENDED_SHARE`). A gap a sprint jump or wall run is meant for can also be dashed across (nothing in a gap stops a dash's straight line without stopping a jump's), and the lock check accepts it there.

**Its look**, all of it to see only (trim, stripes, labels and banners, with no colliders: none of it changes how the ring plays, and the lock check never sees it):
- Every tall wall has a graphite coping along its top and, on its lane side, a graphite band at the height of the deck beside it (stepping down and up with it, and none beside a gap) with an orange stripe along it. Warm lamps light every other bay between the pilasters, high up (the script's `LAMP_HEIGHT`), and pipe runs on brackets line the course's outer walls higher still (`PIPE_HEIGHT`). Where a ring wall stops for the roll blocker's way, a graphite end post with an orange strip down its lane side marks the break as meant, and a beam crosses it at the walls' top (for a break up to the script's `LINTEL_SPAN`); where walls stop at the hub's ways in, or the shuttle's walls meet the ring's and the hub's, a graphite pier stands over the end. A marked wall has the coping but no band: its plate and grooves are its look.
- Every gap is striped on both lips, as is each low passage's mouth, the slide beams, and the lip a climb lesson reaches for.
- Each lesson has its number and name on the floor at its start (the script's `LESSON_TITLES`), and each course stretch its number and a name of its own; each has a banner with its number on a wall at its start where there's one. Each stretch's number and name (`STRETCH_NAMES`) are before its gate. Every timing gate is walled over from its top (its sign's, at the start) to the walls' top (`GATE_TOP`, `GATE_SIGN`), so it's a doorway in them. The checkpoints aren't marked on the floor: a lesson's number and name say where it starts.
- The stretches are dressed by what they hold: I-beams across the hall from wall to wall that the ropes hang from (in the lesson and the course), on orange plates, footings under the zipline's posts, stripes round the bounce pad, the last climbable panel, then the finish: a lit line across the floor, a pylon of light up each wall, and FINISH beyond. The gate into the hub is framed, with HUB before it. Every deck has a dark fascia under its lip; the vault's low wall an orange top. The pool has white sides, a white rim at its ends, lane lines and a cyan light under its arch.
- Windows look out over the ring's middle: in the inner walls beside plain floor (one in the middle of each stretch of it the script's `GLASS_MIN` long or more, up to `GLASS_MAX`), and in the first and last thirds of the shuttle's walls (its middle third opaque). Each is glass flush with the wall's lane side, from over the deck's band to `GLASS_BELOW_TOP` under the walls' top, with graphite mullions every `MULLION_EVERY`; it blocks as the wall it's in does, so nothing about the play changes. Banners go on the outer walls, so the windows stay clear; the lamps skip them.
- A glass roof covers the way at the walls' top (the lanes, their corners, the hub and the shuttle's way, but not the open ground in the middle), on graphite ribs every `ROOF_RIB`: too high to reach, as the walls' tops are.
- The hub has an inlaid octagon, a white ring at its middle and START / FINISH on its floor (behind the middle, clear of the first lesson's start); the shuttle's mouth has a lit beam across it.
- The ground under and round the ring is dark asphalt, the hazard, with octagons marked out on it, inside and out, so the drop has a scale, and in front of each face what it is (its lessons, or its stretch), in letters as big as the lane is wide. The ground reaches far past the ring (the script's `GROUND_REACH`) into a denser haze than the other levels' (`TutorialRingSceneSetup.HazeDensity`, set by Set Up Scene on this scene only, with its sky below the horizon the haze's colour), and a skyline of blocks stands far off in it (`SKYLINE` of them, to see only), so the ground never ends against the sky.
- Everything is shaded by its height: darker the lower it stands, untouched at the walls' tops. It's a full-screen pass (the Height Shade shader, Assets/Showcase/Materials/HeightShade.shader, through a Full Screen Pass renderer feature named Height Shade on the project's URP renderers) that reads the height of each pixel from the depth. It does nothing unless a scene's `HeightShade` component turns it on (the Tutorial Ring's, on its Tutorial Ring object, which Set Up Scene adds: its Bottom, Top and Darkest are the gradient), so the other levels, which share the materials, are as they were. Create Tutorial Ring Assets adds the material and the renderer feature where they're missing. See [Decisions](../decisions.md#the-tutorial-rings-height-shading-is-a-full-screen-pass-november-2026).

**One jump.** The tutorial's character has one jump, no air jumps (with a double jump, no lesson could need the dash, the ledge climb or the wall run). Its spawner's Tuning Preset is Assets/Showcase/Movement/Tutorial No Air Jumps.asset (Jumping's Max Jumps at 1), applied to the character's own copy of its movement, so the movement asset is left as it is; the script sizes the level by the envelope without its double jumps, and a PlayMode test checks the character's measured capabilities match it.

**Changing it.** The spec, Assets/Showcase/Levels/TutorialRing.level.json, is written by Tools/LevelSpec/examples/tutorial_ring.py, which lays the ring out, sizes every lock from Tools/LevelSpec/envelope.json, and runs the lock check (`levelspec.locks`: see [Level Specs](level-specs.md#the-lock-check)) on every lesson and course crossing: if one doesn't lock on its moves, it writes nothing and says which. Edit the script, run it (`python3 Tools/LevelSpec/examples/tutorial_ring.py`), check the spec (`levelspec check`), then build the scene from the spec (Build Level from Spec…, or `levelspec build Assets/Showcase/Levels/TutorialRing.level.json --unity`) and run Set Up Scene, which adds the tutorial's game (below) to this scene only. The lessons' prompts and what does each are in Assets/Showcase/TutorialRing/Tutorial Lessons.asset; the lesson zones in the level name its ids.

**Its game** (`MeridianMotion.Showcase.TutorialRing`, Assets/Showcase/TutorialRing, the showcase's own assembly):
- `TutorialRingSetup` gives the player's character, as it spawns, a `TutorialLessonTracker` (which lesson zone it's in, and which lessons it has done: a lesson's movement event does it, once a session) and a `PlaytestSessionLog`.
- `TutorialPromptPresenter` shows the current lesson's prompt in the Lesson Prompt window at the screen's lower centre, each control named by its binding on the device in use (keyboard and mouse, or a gamepad: it follows a switch at once), then the lesson's follow-up once it's done, if it has one. Its Hidden (remembered between sessions) hides every prompt for footage, and a replay playing hides them too. While a replay plays, the lessons and the session log ignore what it does, and the lesson zones are taken again from where the character is once it stops.
- `PlaytestSessionLog` writes the session's results beside each replay the scene's Replay Recorder saves (the pause menu's Save Replay, or Quit in a build: see [Replays](../replays.md)), as `[replay].results.json`: per lesson, when it was first entered, how long it took and the respawns in it; per course run, its time, splits and respawns.
- `TutorialRingDevKeys`, in the editor and development builds only: its Hide Prompts Key shows and hides the prompts, Open Shortcuts Key opens both shortcuts, and End Session Key saves the session's replay and results (the keys are its settings).

Its assets (the lessons, the preset, the Lesson Prompt window, and the height shading's material and renderer feature) are made where missing by **Tools > Meridian Motion > Showcase > Tutorial Ring > Create Tutorial Ring Assets** (`-executeMethod MeridianMotion.Showcase.TutorialRing.EditorTools.TutorialRingAssets.CreateFromCommandLine`), then edited by hand. The scene is in the build's scenes, after the Meridian Basin.

## The Level Playground
The Level Playground (Assets/Showcase/Scenes/LevelPlayground) is a small test scene with one of each piece the complex is built from, made by the same builders' pieces in the same material and lighting:
- an octagonal dais and a turned column at the spawn point;
- a terrace cut at 45° up a ramp, with guardrails;
- a stair to a walkway on columns, two octagonal towers joined by a suspended walkway, a jump gap and a diagonal bridge out to a connection marker;
- a walled building with a doorway, a window, a crate and a shelf;
- an underground stairwell, passage, lit room and ventilation shaft;
- one of each of the Calibration Annex's tests (steps either side of the step limit, a ledge, a slope, a low tunnel, a narrow beam beside a wall-run wall);
- one of each mechanic (a lift, a ladder up to the lift's landing, a slide gate, a timed run and a crate to push; see [Level mechanics](#level-mechanics));
- in the far west, the mechanics in the air: a tower, up a ladder or a bounce pad, with a zipline from its top south down to the ground, and a rope on a gantry over a gap between two raised platforms, meant to be crossed on the rope (the levelspec tool's movement checks grade the gap against what the character can do: see [Level Specs](level-specs.md)), the near one with a climbable panel up its side;
- in the north-east, a pool sunk into the ground: water over a tiled basin, a beach sloping in at its west end to a shallow shelf, and a drop-off to a deep end to dive in, with an arch to swim through along its floor and a ladder up its east wall;
- and placeholders for mechanics to come.

Its dimensions are in `LevelPlaygroundBuilder`. It builds in a couple of seconds (Tools > Meridian Motion > Showcase > Levels > Build Level Playground, or `-executeMethod MeridianMotion.Showcase.Levels.LevelPlaygroundBuilder.BuildFromCommandLine`), so changes to the level's pieces can be tried there before rebuilding the whole complex. Like the complex, rebuilding it removes its game setup: run Set Up Scene again afterwards.

## The HUD
The showcase's levels have a HUD of their own on the UI framework (see [UI Framework](../ui.md)): each lap or run a course timer finishes shown as a notification, a new best as a banner across the middle (with the HUD's New Best Text), and each split as it's passed, with how far ahead of or behind the best run it is (its Split Title and Split Seconds); a hint on the first spawn that the pause menu lists the controls (its Controls Hint Title, Message and Seconds); and a name tag over each NPC, showing its route's name (its NPC Label Prefab). It's `ShowcaseHUD` (Assets/Showcase/HUD, an assembly of its own knowing the UI and the level elements, nothing of the character's), which Set Up Scene adds only to the showcase's own scenes (those in Assets/Showcase), connecting the spawner's On Character Spawned and On Npc Spawned to it; a project's scenes get no HUD. Its prefabs (Showcase HUD, NPC Label) are made where missing by **Tools > Meridian Motion > Showcase > Create Showcase HUD**, then edited by hand. Rebuilding a level removes its setup, HUD included: set it up again.

## Level mechanics
The level's mechanics (its moving platforms, ropes, ladders, water...) are fixtures: geometry named for what it is, which `LevelFixtureSetup` turns into the level element it stands for as each level's built. Their names and settings, and how to add a new one, are in [Adding Level Content](../traversal.md#level-fixtures).

## Mechanics in the showcase levels
Each mechanic is a piece of the levels' builders (Graybox.Mechanics.cs), whose geometry is named as its fixture (see [Level fixtures](../traversal.md#level-fixtures)), and the Level Playground has one of each:
- **Ladders:** the Ladder piece builds one (orange rails, steel rungs) with its back against a wall, climbed onto the floor level with its top. The Level Playground has one up the side of the lift's landing, and one up its pool's east wall.
- **Ropes:** the Rope piece hangs one from a small graphite bracket, which hangs from something (a gantry's beam). The Level Playground has one on a gantry over a gap meant to be crossed on the rope.
- **Ziplines:** the Zipline piece builds a steel cable between two graphite posts. The Level Playground has one from the top of a tower down to the ground.
- **Bounce pads:** the BouncePad piece builds a steel base with a cyan springy top (its "Spring", which squashes on each launch). The Level Playground has one up onto its zipline tower.
- **Climbable panels:** the climbable panel piece stands a steel panel with orange holds a little proud of a wall; only the panel is climbable, not the wall, so a climb stops at its edges. The Level Playground has one up the side of the near rope platform.
- **Water:** the Water piece draws the surface over a footprint (in the Water material, below), and what's under it (a pool's basin) is built as any other floor. The Level Playground's pool has a beach, a shallow shelf, a deep end and an arch to swim under along its floor.

### Climbable paint on the levels' materials
The levels' concrete shader (NoiseConcrete) shows painted climbable areas (see [Painting climbable areas](../traversal.md#painting-climbable-areas)): it has the Climbable Mask texture and a second set of its look's settings for climbable areas (Climbable Tint, Climbable Grain, Climbable Panels and the rest of them; by default an ochre, rougher concrete in finer panels), blended in where the mask is white. The level materials made with it are given ClimbableMask_None as their own mask when they're made, so unpainted objects show none.

The builders give their meshes the UV2 the paint needs (each face a chart of its own, packed into the square at one scale), and the levels' boxes use Graybox Cube (Assets/Showcase/Models/Level), Unity's cube with a UV2.

### The water's look
The levels' water is drawn with its own shader, Assets/MeridianMotion/Content/Materials/Water.shader (the Level_Water material, made the first time a level's built), whose whole look is built from noise in world space with no textures, as the other surfaces' is:
- layers of drifting noise make the ripples, which bend the surface's normal (Ripple Size, Ripple Speed, Ripple Strength) and lift and lower it a little (Wave Height);
- the sky reflects in it, more at a glancing look, and the sun glints off the ripples (Smoothness, Sky Reflection, Sun Glints);
- how much water is under each point (from the camera's depth texture) tints it from Shallow Colour to Deep Colour, from see-through to nearly opaque (Depth To Deep, Shallow Opacity, Deep Opacity);
- and foam rims what stands in the water, broken up by noise (Foam Colour, Foam Distance, Foam Breakup).

From under the surface it's a murky ceiling. The depth effects need the render pipeline asset's Depth Texture on (the PC asset has it); without it, the water all looks deep and has no foam. What a camera under the water sees is the UnderwaterView's (see [The view underwater](../traversal.md#the-view-underwater)).
