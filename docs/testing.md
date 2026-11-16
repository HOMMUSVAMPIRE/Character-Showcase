# Automated Tests

The tests are written to hold for any team's setup, not just this project's: they read what they need from the character (its capsule, step height, slope limit, walking and dashing speeds, jump count) and from whatever model, animations and camera settings it has, and size their test worlds from those.

Play mode tests run on the package's Character prefab unless a CharacterTestSettings asset (Create > Character Showcase > Tests > Character Test Settings) names another: put one anywhere in the project to run the whole suite on your own character. A test of something the character doesn't have (an air jump on a character with one jump, foot placement on a model that isn't a humanoid) is skipped, with the reason, rather than failed. They've been run on a character tuned differently (a smaller capsule, a lower step height and slope limit, a much slower walk, a slower dash and a single jump).

Tests of the package's own content (the mannequin and other prepared models, the motion-capture clips, the shared Animator Controller) are in the SampleContent category, and skipped where that content isn't there. A team using its own models and animations can leave them out: in the Test Runner, or with `-testCategory "!SampleContent"` on the command line.

## Play mode tests
Play mode tests in Assets/Tests/PlayMode spawn the character with real physics and drive its MovementController with scripted inputs. Each test's character moves with its own copy of its movement mode asset, so a test can change settings without changing the asset, and the asset can be retuned freely: the tests read the values they expect from the copy. The sizes, speeds and tolerances a test tries are in the test itself. Each fixture below is one file, named for it.

The settings that turn an ability off at 0 are each tested doing so, in the ability's fixture: the slide's longest time, the wall run's and wall slide's longest times, the ledge climb's duration, the max jump height, the rope's and zipline's grab reach, and the bounce pads' height multiplier.

### Moving about
- **MovementTests**: settling on the ground without hovering, sinking or drifting; falling within gravity's limits, including walking off a ledge after standing still; stopping at walls, jumping and landing; climbing stairs and standing on a ramp without sliding (each well within the character's step height and slope limit); climbing any step up to the step height, and not taller ones; coyote time easing gravity in after leaving the ground, and jumps not being refunded by it; keeping speed when walking off edges; walking smoothly down stairs, and walking and sprinting down a long flight of steps nearly the step height (shallower than the character is wide) from different starting points and headings, without leaving them or speeding up; air steering that doesn't stack on carried momentum; and the jump input buffer.
- **SprintTests**: sprinting at the run speed and walking again once Sprint is let go; Sprint alone not moving the character; a sprint's speed carried through a jump without stacking.
- **MovementCapabilitiesTests**: the character's capabilities matching what it does (a jump and a sprint jump carrying it as far as they say, its step, stand and jump heights), following its settings (a higher jump crossing further and reaching a ledge that much higher) and its size (twice the size jumping and sliding twice as far), leaving out what's turned off, and adding double jumps with air jumps.
- **JumpHeightTests**: a tap rising the min jump height, Jump held rising the max, holding longer rising higher between them, letting go past the min height stopping the rise at once, and the heights following the settings.
- **GroundMomentumTests**: landing from a jump not adding walking on top of the air speed; hopping again and again (walking or sprinting) never building up speed; landing faster than walking carrying the momentum and easing down to walking, never below it.
- **DashTests**: distance and direction, ignoring gravity, momentum afterwards, cooldown and input buffer, one dash per press, air dash limits, and jump and dash cancelling each other.
- **MovingGroundTests**: riding sliding, rising and spinning platforms, and keeping a platform's momentum when jumping off.
- **CollisionTests**: falling away from a ceiling the character jumps into, losing momentum into a wall, sliding off the edge of a drop taller than a step, sliding off slopes too steep for it, and being pushed by moving walls.
- **MovementEventTests**: each event raised once at the right moment, and passed on by the AnimationController.
- **EdgeCaseTuningTests**: movement at the edges of its settings, each set on the test's own copy of the movement mode:
  - no walking or running speed (standing still without breaking), and a run speed no faster than walking (Sprint walks);
  - no jumps and many (exactly that many);
  - no gravity (a jump drifting up without breaking), and extreme gravity with no fall speed limit (landing without going through the floor);
  - walking and dashing at extreme speeds into a very thin wall (stopping at it);
  - a dash with no length (no dash), no speed (holding still, then ending), an empty speed curve, or no cooldown (still one dash per press); no air dashes (ground dashes only);
  - no jump input buffer (only a fresh press jumps) and no coyote time (a jump just after walking off is an air jump).

### Abilities
- **CrouchTests**: lowering the collider to the crouch height (in proportion at a larger size too) and moving at the crouch speed; standing again on a second press; staying crouched under a low ceiling and standing by itself once out from under it; hold to crouch; one crouch per held press; jumping from a crouch (and not under a low ceiling); its events.
- **SlideTests**: crouching while sprinting slides, starting at least at the slide start speed, only slowing, low, ending crouched within its longest time; crouching while walking just crouches; sliding under a bar too low to stand under; jumping out keeping its speed; stopping at a wall; a second press standing up.
- **RollTests**: rolling in the input direction for its duration, low, fast, facing it and standing afterwards; straight ahead without input and not steerable; one roll per press, a buffered press waiting out the cooldown; not in the air; ending crouched under a low ceiling; going farther in proportion at a larger size; handing straight over to walking without a burst of speed. On landing from a high fall it rolls on impact the way it's moving, but not standing still, from a lower fall, from an ordinary jump, after a dash or an air jump just before landing (which start the fall again), or with a jump pressed for the landing (which jumps).
- **WallRunTests**:
  - running along a wall in mid-air, falling much slower than falling freely, staying against it and facing along it;
  - ending after its longest time, past the end of the wall, and not starting again on the same wall;
  - wall jumps away and up, giving back jumps, and chaining into a run on a facing wall;
  - no run without steering mostly along the wall, heading into the wall, or close to the ground;
  - steering away for a moment not dropping off it, but for the release delay letting go;
  - giving back air dashes and jumps, so a character that air dashed into one can dash again after dropping off it;
  - ending once the feet drop below the wall's bottom edge, and not starting beside a wall that doesn't reach down to them.
- **WallSlideTests**:
  - sliding slowly down a wall fallen into head-on, against it and facing it; a fast fall braked to the slide speed;
  - jumping off upward and away, gaining height and giving back jumps, but not off the same wall twice in a row; only so many jumps in a row off one wall before landing;
  - hops alone not climbing a wall, but a narrow chimney climbed by kicking between its walls;
  - air steering coming back only gradually after a jump; air dashes given back by a kick off a wall, but not off the same wall again;
  - not while rising fast, close to the ground, or glancing along the wall, but on hitting a wall moving into it whatever the steering;
  - letting go by steering away (for the release delay, not a moment) or with Crouch; ending on landing and after its longest time.
- **LedgeTests**: grabbing a ledge falling in front of it and hanging still below it, which gives back air dashes and jumps; climbing up onto it, or crouched with only room to crouch; letting go with Crouch or steering away; not grabbing ledges out of reach, or while rising fast; hanging from a moving ledge; climbing from a ledge onto stairs rising from it; letting go when the ledge's object is deactivated; jumping up to a ledge, grabbing and climbing it with its events raised once each.
- **VaultTests**:
  - heading into the top of a low wall in mid-air vaults over it (giving back air dashes and jumps), rising over the rise time and leaving at its share of the speed, without sliding down it or hanging from it;
  - a low platform stepped up onto, keeping the speed, rather than vaulted; a higher block vaulted onto; at the vault's slowest speed with no speed of its own;
  - sprinting into a low wall on the ground vaults it, but walking into it (or sprinting, with vaulting from the ground off), glancing along it, or a railing at the edge of a drop doesn't;
  - stairs sprinted up or jumped onto climbed, not vaulted; not without room over the top;
  - with vaulting off, a low wall not slid down, its top grabbed once in reach.

### Climbing and traversal
- **LadderTests**: walking into one climbing straight up its middle facing it; sideways input doing nothing; not taken hold of from behind; one standing on its own climbed to its top and no further, and one up to a platform climbed off onto it; jumping off kicking away.
- **WallClimbTests**:
  - taking hold of a climbable wall giving back air dashes and jumps;
  - from the ground, stepping up onto it first, holding still for the enter time, then climbing, and stepping off the bottom holding still for the exit time, but neither taking hold in mid-air nor letting go higher up;
  - walking into a climbable wall taking hold of it and climbing up it at the climbing speed, against it and facing it, with its events; an unclimbable wall, or climbing turned off, not being climbed;
  - taking hold heading in at a glancing angle, and on falling into one rather than sliding down it; holding still without input; climbing sideways at the climbing speed;
  - stopping at the sides and top of a climbable panel on a plain wall; going round an outside corner onto the next face and into an inside corner onto the other wall;
  - climbing over a walkable top onto it, as soon as the hands reach it;
  - jumping off upward and away; letting go with Crouch and taking hold again only after the regrab delay; stepping off at the bottom by pressing down; moving along with a climbable surface that moves;
  - painted walls: climbing a painted patch only within the paint, and not taking hold of a wall's unpainted part; a box painted on one face not climbed round its corner until the face round it is painted too; paint being for the face painted, and erased as painted.
- **PushPullTests**: pushing and pulling at the push speed with the character keeping hold, slower for heavier objects, push-only objects staying put, stopping at walls without going into the object, letting go, objects falling off edges, objects just being obstacles without Interact, and not jumping while holding one.
- **RopeSwingTests**: reaching one in mid-air catching hold and swinging on it about its anchor, the hands staying the grip distance from it, carried on past it and back; no input never swinging higher, pumping with the stick swinging higher, and never past the largest swing angle; a wall stopping the swing; jumping off keeping the swing's speed and jumping up, the rope swinging on by itself; Crouch letting go, the same rope not caught again before the regrab delay.
- **ZiplineTests**: catching a cable in mid-air and riding it downhill to its end, letting go there at the ride's speed, with its events; hanging the hang height straight below it, facing the way it rides; speeding up downhill to no more than the zipline's top speed; a level cable carrying the rider at the slowest ride speed; jumping off up on top of the ride's speed; Crouch letting go.
- **BouncePadTests**: coming down on one launching the feet its launch height above it, once; Jump held bouncing higher by the jump boost; running across one carrying the run on; a tilted pad launching along its up; the bounce being the jump, leaving the air jumps.
- **TimingGateTests**: sprinting from a start gate to a finish gate timed at the course's length over the run speed (with its speed shown); a lap gate starting a lap on the first pass and ending it on the next, but not straight away; only the racer counting.

### Size
- **ScaleTests**: moving like a scaled copy of itself at any size, from tiny to huge, over a course laid out for the character (ramps up and down, a step, a jump, a dash, a low ceiling and a step too tall for it): every physics step puts it in the same place for its size as at full size, on the ground at the same moments, with the same jump airtime and height for its size. Also scaling only the model parent and resizing collision and mass to match; size changes taking their time; waiting for room to grow under a low ceiling; non-uniform scales still moving and colliding; natural speed scaling; the model's measurements and placeholder following; the inspector field; and refusing negative, zero and non-finite scales, raising tiny ones to the minimum, and reporting a scaled root transform.

### Models and animation
- **AnimationTests**:
  - the character's model standing with its soles on the floor (within a tight tolerance, scaled) at small, full and large sizes and stretched on one axis, with foot placement on, off or fading; an unmeasured source measuring its model on load;
  - its planted feet sliding no more at other sizes, or stretched, than at its own size;
  - IK on stretched models: moving a hand or foot goal moving the hand or foot as far, and a knee hint on its own moving the foot no more than unstretched;
  - sample content: every prepared model's planted feet staying in place walking at the walk loop's speed, standing on the floor, arms hanging down when standing, and the torso leaning as the mannequin's does when standing, walking and running (checks that its avatar's reference pose was set up).
- **ModelVariantTests**: models made facing another way, or off their origin, facing forward and standing on the spot; a model with an unevenly scaled armature staying in proportion when stretched.
- **ModelTests**: model hosting (loading at runtime and being set up, swapping models, including one without an Animator, new models staying hidden until swapped in, the placeholder, falling back when a model fails, cancelling loads when the character is destroyed or given another model, sources releasing their models, a model swapped in mid-run or mid-transition carrying on where the old one was, measuring on load once per model, and the editor preview); fitting a model to a height (in proportion to the character's size, the eyes following), fitting the collider to the model by height or shoulder width, and waiting for room to grow; and the Animator parameter translator.
- **FootLockTests**: turning on the spot, planted feet staying where they were put down (none of the slide they have without it).
- **VisualRotationTests**: the model turning after the character: the character turning at once and the model easing round after it without overshooting; turning slower at a run than standing (no faster than its moving limit); leaning into curves by the configured share of the balancing lean (either way); walking a curve leaning about half as much as sprinting it; walking a sharp corner leaning no more than walking speed allows; leaning forward speeding up and back stopping, and upright otherwise; facing a roll at once without leaning; turning with a spinning platform without lagging.
- **AbilityAnimationTests** (sample content):
  - crouching, sliding, rolling, wall-running, sliding down a wall, hanging, climbing, vaulting and pushing each playing their Animator state;
  - a roll following the roll and blending into walking before it ends, a roll from a crouch blending into the crouch without standing up, and the roll playing on landing from a high fall;
  - the wall run for the side the wall is on (feet toward the wall, leaning away);
  - sliding down a wall with the hands flat on it at head height, spread as wide as the library's climbs hold them, the body against it;
  - a hanging model's wrists on the ledge's top just back from its edge, either side of the character and as far apart as the library's climbs grip, fingers over the edge, its body against the wall; staying in the climb when crouched;
  - the planted hand on the top mid-vault;
  - a step up onto a low platform easing the model up after the character, never sinking a foot into the top; landing on a staircase on the run (walking or sprinting up it) landing, not staying in the air;
  - a crouched model fitting its lowered collider;
  - the Ladder state holding still with the hands on different rungs and the hands and feet each on a rung;
  - the Wall Climb state with a hand on the wall and one reaching above the head climbing up, and Climb Enter and Climb Exit playing stepping on and off from the ground;
  - the rope swing with the hands on the rope, and the zipline with both hands on the cable.

### Camera and spawning
- **CameraRigTests**: the camera rig, which follows a plain Transform, independent of the character code, with expectations taken from its own settings:
  - third and first person placement, and mode blending and events; pitch limits and zoom limits;
  - collision with walls (only as far in as needed, ignoring walls clear of it and the target's own colliders), at several sizes of target;
  - moving to the other shoulder, at full distance, beside a wall partly blocking its own, returning once the wall is gone, and staying put beside a wall on the other side; never pulling in as a target comes up against a wall and runs along it;
  - smooth follow and teleport snapping; shake and ShakeAll;
  - first person target hiding, including renderers added to the target later; first person eyes following an eye anchor;
  - framing and clip planes scaled with the target, across a wide range of sizes;
  - speed FOV judging speed relative to the target's size, and settling once it stops;
  - the landing dip, as deep as the landing settings make it, in proportion to the target's size, and recovering (landings are judged by the fastest fall since the target last rose, so a frame holding several physics steps, as in the editor, doesn't halve the impact);
  - CameraZone overrides;
  - following a new target with `Follow`: snapping to it and looking the way it faces, and taking its eye anchor and size reference in place of the old target's.
- **SpawnerTests**: the character spawner spawning at a spawn point and facing its way, at a chosen point or one chosen at random from all of them, on Start or only when asked; telling listeners through both events; treating an all-zero rotation as none; giving the spawned character the chosen model and movement mode (and keeping the prefab's without them); giving the camera its eye point and size reference; and refusing with an error when there's no prefab, no spawn points or no such point.

## Edit mode tests
Edit mode tests in Assets/Tests/EditMode check Prepare Character Models, the motion-capture clips, the levels and their tools without entering Play mode, so a change to preparing (or a model added without preparing it) fails fast. As in Play mode, each check's tolerances are in the test.

- **PreparedModelTests**: every model file has a source; straightening nodes above the skeleton keeping everything under them where it was, unevenly scaled ones included, and leaving what can't be straightened; facing corrections rounding to the nearest quarter turn; ground offsets counting meshes carried by bones, not just skinned ones. Sample content: every prepared model's source is ready to use and plays the shared Animator Controller; its avatar reference pose is a T-pose (spine up, arms out, legs down) with nothing rotated above its skeleton; its stored Ground Offset, Animated Ground Offset and Facing Correction match a fresh measurement; what it was prepared against changes when its controller changes; and previews and listed heights are sized like the loaded model.
- **LocomotionBlendTests** (sample content): the shared Animator Controller's Locomotion blend tree lines up every loop to land its left foot at the same moment, and each loop shown at its own strides' speed (the walks, the jog, and the sprint sped up to the top) keeps its feet in step with the ground there.
- **MotionCaptureClipTests** (sample content): every motion-capture clip is a Humanoid animation with a valid avatar, mapped by the CMU bone names and starting after its T-pose frame; played on the mannequin, every clip that moves about on its feet stays on the spot and faces forward; each check lists every clip that fails it.
- **ClimbablePaintTests**: the concrete shader's climbable mask and climbable look settings; every climbable material sharing the black unpainted mask (Assets/Materials/ClimbableMask_None.png); a painted face, and only it, white in its renderer's mask, given to the renderer and taken off with masks turned off; no mask for a material that shows no climbable areas; and the level's boxes and prisms having a UV2 with no two triangles sharing a texel.
- **LevelEditorTests**: adding a piece where it's placed (in the picked piece's group, standing on it), deleting it as the Delete key does (the scene left as it was) and undoing that, duplicating a piece, and a piece whose objects are deleted from the scene taken out of the spec; moving a piece moving its positions and not its directions or sizes; a new piece of every kind being valid and building; the preview knowing which piece made each object; rebuilding one edited piece (a platform, a spawn marker, a turntable) leaving the level as building the edited spec afresh would; and an edit undone putting the spec and the preview back.
- **LevelSpecTests**: every level built again from its own spec (written as JSON and read back) building the same level, object for object; a hand-written spec building what it lists, with State entries and groups of the same name; mistakes (unknown pieces and settings, missing settings, wrong shapes, bad JSON, a missing parent) reported with where they are; numbers reading back as the same float; and the schema covering every piece.
- **MovementTuningPresetTests**: a preset saved from a mode gives the same tuning back, curves and on/off switches included; one of a few settings changes only those; a preset never changes a mode's components or their order, and reports what it couldn't set; and a component's settings are the fields Unity saves, of kinds a preset can copy.
- **MovementEnvelopeTests**: Tools/LevelSpec/envelope.json is what the default character can do now (the editor writes it again when the tuning is saved; this catches a change made outside it).
- **LevelFixtureSetupTests** (Set Up Scene's fixtures): settings read from a fixture's name; moving platforms and crates given what they need from them; a "Ladder | …" fixture made into a Ladder that keeps the level's mesh, but not a "Ladder (future)" marker; ropes, ziplines and bounce pads made from their names (a zipline keeping the level's cable, a pad its spring) but not the placeholders named for them; a climbable panel made climbable at its speed, but not a "Climbable Wall (future)" marker; and a timer for each course, its gates' roles and distance, and its display wired to it.
- **SceneGameSetupTests** (sample content): setting up a scene: replacing a placed character with a spawn point where it stood (or keeping it, when asked), spawning at the scene's spawn markers, keeping its spawn points and replacing its old setup when set up again, wiring the demo scene's camera (or the scene's own camera rig) to the spawner, copying the input manager once, and turning other cameras off rather than deleting them.

## The levelspec tool's tests
The levelspec tool's tests run with `python3 -m unittest discover -s Tools/LevelSpec/tests`: the exported specs valid, every format converting to every other and back unchanged, XML and CSV written by hand reading as meant, mistakes reported where they are, groups' placement carried to their contents, gaps and climbs graded by the moves that cross and reach them (made-up moves, and the character's own from envelope.json, whatever it's tuned to), and schematics drawn.

## Running them

Run them from Window > General > Test Runner (the EditMode and PlayMode tabs), or from the command line with Unity closed:

```
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults EditModeResults.xml
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults PlayModeResults.xml
```

CI (.github/workflows/tests.yml) runs both.

The character scripts are in the CharacterShowcase assembly (Assets/Scripts), and editor tools are in CharacterShowcase.Editor (Assets/Editor), so the tests can reference them. The level builders are in their own assembly, CharacterShowcase.Level.Editor (Assets/Editor/Level), which doesn't reference the character or camera code.
