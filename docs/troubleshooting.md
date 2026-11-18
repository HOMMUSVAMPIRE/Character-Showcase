# Troubleshooting
Problems that are easy to run into in this project, what causes them, and the fix.

## The movement asset
**A component shows as "Missing component" in the movement asset.**
A component's class name is saved in the asset (its list is `[SerializeReference]`), so renaming or moving the class, or changing its namespace, loses it. Put the old name back, or add `[UnityEngine.Scripting.APIUpdating.MovedFrom]` with the old name, namespace or assembly to the class. `MovementCompositionTests` fails while the asset has a missing component.

**A setting went back to its default after a code change.**
Unity drops a saved value whose field it can't find. A renamed field keeps its value with `[UnityEngine.Serialization.FormerlySerializedAs("oldName")]`. A setting moved into another component can't follow that way: set it again on the asset, and in any tuning presets that had it (a preset reports a setting its component no longer has when it's applied).

**Changes made while playing were kept.**
The character uses the movement asset itself, so changes to it in play mode are changes to the asset. Save a tuning preset first if you want to go back (see [Tuning the Character](tuning.md#tuning-presets)).

**New Ability refuses to make an ability.**
It says why at the bottom of its window. A name that's taken (a type, a state, an event, a setting or a member of the MovementController) needs another. One that can't find its New Ability line in `CharacterState.cs` or `MovementEvents.cs` needs the line put back, above the enum's closing brace (`// New Ability (NewAbilityWindow) adds …`). A new ability whose tests fail at once has had its component, or a template, changed: as made, they pass.

**A setting of a 64-bit enum type was lost.**
Unity silently drops enum fields wider than 32 bits. That's why a saved set of `MovementEvents` (a 64-bit flags enum) is stored as a `long`, as `AnimatorParameterTranslator.EventTrigger` does, drawn by `EventTriggerDrawer`.

## The tests
**`MovementEnvelopeTests` fails.**
Tools/LevelSpec/envelope.json doesn't match what the character can do now. Saving a change in the editor writes it again, but one made otherwise (an edit to the asset's file, a change in batch mode) doesn't. Run Tools > Character Showcase > Level > Specs > Write Movement Envelope, and look at what changed (see [Tuning the Character](tuning.md#what-changes-when-you-save)).

**A test fails after retuning.**
Tests should read expected values from the settings (`rig.MovementSetting<T>("name")`), not hard-code them. If a test needs a particular tuning to make sense (no air jumps, say), it sets it on the rig with `rig.SetMovementSetting`, which changes only the rig's own copy.

**A test is ignored rather than run.**
`CharacterTestRig` ignores a test whose ability the character doesn't have: `rig.Component<T>()` when the mode has no T, or `rig.MovementSetting` for a setting no component has.

**Something removed in a test is still there.**
`Object.Destroy` waits for the end of the frame, and a movement-only test steps the physics itself, with no frames between its steps. Use `Object.DestroyImmediate` in tests (see [Automated Tests](testing.md#how-long-each-test-takes)).

**Running the tests from the command line gives no results.**
Unity can't open a project that's open in the editor, so a batch-mode run quits at once, with no results file. Close the editor first, or run the tests from the Test Runner window.

**The hygiene check fails.** (`python3 Tools/CI/check_project_hygiene.py`)
- *A missing or orphaned `.meta`:* every file and folder under Assets needs one, moved and deleted with it. Copy a neighbouring `.meta` of the same type with a fresh GUID (`python3 -c "import uuid; print(uuid.uuid4().hex)"`).
- *A duplicate GUID:* a `.meta` was copied without changing its GUID.
- *A field the tests read by name doesn't exist:* a setting was renamed or moved. Update the test, or keep the name.
- *Two settings share a name:* setting names must be unique across the movement mode and all its components, since tests find them by name alone.
- *A field the tests read by name is read as another type* (`MovementSetting<int>` for a float setting): ask for it as the type it's declared, or the test fails (the rig won't skip it).
- *An assembly references what it may not, or isn't in the table:* AGENTS.md's assembly table says what each may reference; the check's `ALLOWED_REFERENCES` holds it. A new assembly or reference goes in both, if it keeps the rules (the camera, the level elements and the level builders know nothing of the character, and nothing that ships references what doesn't: AGENTS.md, The package line).
- *The camera, a level element or a level builder uses character code* (`using Character;`): for the camera, see [Adding a camera feature that needs the character](camera.md#adding-a-camera-feature-that-needs-the-character). A level element knows nothing of the character (the character finds it), and a level builder makes geometry, lighting, fixtures and markers and attaches the level elements; Set Up Scene connects the character side (see [Adding Level Content](adding-level-content.md)).
- *A movement asset names a class that no longer exists:* a movement component was renamed or moved. Give the class `[UnityEngine.Scripting.APIUpdating.MovedFrom(... sourceClassName: "OldName")]`, or the asset shows it as a missing component, its settings lost.

## New characters
What can go wrong adding a model or a character (see [Adding a Character](adding-a-character.md)). Prepare Character Models reports each model in the Console (a line from `[CharacterModelImporter]`, a warning if any model failed), or in a window when it's run from the menu: "not prepared" and the reason, or "ready", with notes.

**A model is skipped: "Unity couldn't map its skeleton to a Humanoid."**
Unity couldn't match the model's bones to a person's. Select the model, open the Rig tab in the Inspector and press Configure: the bones it couldn't match are shown in red. Assign them, press Done, then run Tools > Character Showcase > Prepare Character Models again. A creature that isn't a humanoid can't be prepared at all: move it out of Assets/Models/CharacterModels (see [A creature that isn't a humanoid](adding-a-character.md#a-creature-that-isnt-a-humanoid)). While it's there, `PreparedModelTests` fails, since every model file in the folder must have a prepared source.

**A model is skipped: "It has no skinned mesh, so it isn't rigged."**
The file has no mesh bound to a skeleton. Rig it (Mixamo can), and export it with its skeleton and skin.

**Prepare Character Models does nothing, with "Run Tools > Character Showcase > Generate Mannequin first" in the Console.**
The Animator Controller and translator every model shares (`CharacterModelImporter.ControllerPath` and `TranslatorPath`) are missing or empty. Check Git LFS fetched the project's files (README.md, How to open this Project), then run Generate Mannequin, which, if Blender isn't found, offers to rebuild them from the Mannequin.fbx in the project (see [Generate Mannequin](animation.md#generate-mannequin)).

**A model isn't in Set Up Scene's list.**
Set Up Scene lists the models prepared in Assets/Models/CharacterModels. A model that isn't ready is listed under "Not available", with the reason: "not prepared yet" (press Prepare Character Models, then Refresh) or the problem its source has. A model source somewhere else (a creature's) isn't listed: give it to a character prefab and choose that.

**A character prefab isn't in Set Up Scene's Choose list.**
Only prefabs with an ACCharacterController on their top object are listed. Make the character a prefab variant of Character.prefab (see [A new character of your own](adding-a-character.md#a-new-character-of-your-own)), then press Refresh.

**A model is far too big or far too small.**
Its file's units were probably lost in export. When a model's source is first made, a model outside the height range `MinHeight` to `MaxHeight` (in CharacterModelImporter.cs) has its source's Scale set to bring it to `TypicalHeight`, with a warning in the Console; a source made before keeps its Scale. Set the source's Scale, or its Height to Fit To Height with the Fit Height you want, or export the model again at the right size (in Blender, apply its scale). With the CharacterModelHost's Body Fit at Model's Body, the body is sized with the model.

**A model faces backwards or sideways.**
Prepare Character Models measures the way the model faces as it stands, and turns it to the nearest quarter turn to face forward (its source's Facing Correction, with a note saying so). A model still turned (a note saying it "stands turned" so many degrees) needs its source's Rotation Offset set to face it forward, or exporting again facing +Z.

**A model's feet are in the floor or above it.**
Prepare Character Models measures where its soles are as it stands (its source's Ground Offset). If it couldn't, it says so in a note ("Couldn't measure how high its feet stand"), and the Ground Offset is left as it was. Run Prepare Character Models again after fixing the model; a model that's re-prepared is measured again. A source made by hand has Measure Ground On Load ticked, and measures its model as it loads. To raise or lower a model by hand, use its source's Position Offset, which preparing keeps (it measures Ground Offset again). If the body's at fault rather than the model, see [The body and the ground](#the-body-and-the-ground).

**The body is too big or too small for the model** (the character stops short of walls, or its model goes into them, or it won't fit through gaps it should).
With the CharacterModelHost's Body Fit Off, the body is the MovementController's Body Definition as it is, whatever the model, and the Console warns when a model is taller than it, or shorter than the host's Minimum Height Fraction of it ("Set its source's Height to Fit To Height, or change its Scale"). Set Body Fit to Model's Body (the body Prepare Character Models fitted, `[name] Body.asset`) or Height (the body as authored, scaled to the model), or shape the Body Definition to the model. Prepare Character Models refits `[name] Body.asset` each time it prepares the model, unless its Refit On Prepare is off.

**Changing my character changed the default one too.**
The change was made to Character.prefab, BasicMovementMode.asset or Character Body.asset, which the default character (and the tests) use, rather than to your prefab variant and its own copies. Change it back there, and make the change on yours.

**A scene doesn't use the prefab's model or movement.**
The scene's Character Spawner has its own Model Source or Movement Mode set, which replace the prefab's. Empty them, or set the scene up again choosing the prefab's own.

## Generated assets
**A change to a generated asset disappeared.**
The mannequin, its Animator Controller, the model prefabs, the level scenes, the level specs, the climbable paint masks and the movement envelope are all written by editor tools, which overwrite them. Change the generator and run it again (the table in AGENTS.md, Generated assets, says which tool makes which).

**A level has no character, camera or spawn points after rebuilding it.**
A build attaches the level's mechanics (moving platforms, ropes, water, course timers) itself, but makes a new scene, without its game setup. Run Set Up Scene on it again, which also tells the course timers who's racing (see [Spawning the Character](spawning.md)).

**A fixture doesn't work in a scene that wasn't built by a builder.**
Its name has to start with one of `LevelFixtureSetup`'s names, with its settings after it ("Rope | length=m"): Set Up Scene runs `LevelFixtureSetup`, which sets up what matches and leaves the rest (see [Adding Level Content](adding-level-content.md)).

**Exporting a level's spec refuses to replace it.**
The spec has changed since it was exported (in the Level Editor, say). Check that only your changes differ, then export with `-force` (see [Level Specs](level-specs.md)).

## Movement oddities
**Something reads a lagging position.**
The character's Rigidbody is interpolated, and moving platforms interpolate themselves, so their Transforms show a pose between physics steps. Movement code reads `Rigidbody.position` and `rotation` (see `MovementController.GetPhysicsPose`).

**A timer behaves differently in the capability measurements.**
Movement must time things by its own clock, in ticks (`MovementController.StepTick`, `SecondsSince`), never `Time.time` or `Time.fixedTime`: the measurements step a copy of the character faster than real time.

**A character teleported a short way snaps back.**
Standing, the next step carries the character with the ground it stood on, back to where it stood on it. Move it with `MovementController.Teleport`, which forgets that ground, not by setting its Rigidbody's position.

**The camera stutters behind the character.**
The camera's Follow section interpolates a physics-driven target between steps. Check it's turned on, and that the character's Rigidbody is interpolated.

## Input, NPCs and determinism
**An NPC stands still.**
A character whose owner is AI is moved only by its input source, and stands idle without one. Give it one (a Waypoint Input Source or a Scripted Input Source, on the character or set as its ACCharacterController's Input Source), or spawn it from the spawner's NPCs list, which gives it its route (see [NPCs and input sources](spawning.md#npcs-and-input-sources)).

**A character doesn't move at all, not even falling.**
Its owner is Remote and it has no input source: a remote character without one isn't stepped (something else is meant to place it). Give it a source, or set its owner.

**The player's character doesn't respond to input.**
The local player's character reads the device through the scene's `ACInputManager`, and without one it's given idle frames. Run Set Up Scene, which adds one (see [Spawning the Character](spawning.md)).

**Two runs from the same start move differently.**
The movement is deterministic on one build, so something has slipped out of it:
- a tie between equally good choices (two hits as near, two ropes) broken by the order results came in, or by instance IDs, rather than by `StableOrder`;
- a level mechanic that moves a character timed by its own time since it was enabled, rather than by the level's clock (`Level.LevelClock`);
- movement code reading `Time` or the device, rather than its own clock (`MovementController.StepTick`, `SecondsSince`) and its input frame.

`DeterminismTests` holds the movement to it (see [Automated Tests](testing.md)).

**A recorded replay drifts from what was recorded.**
A replay moves exactly as the recording only with the same tuning, in a level built the same way. Compare the recording's `InputRecording.Tuning` with the character's `MovementController.TuningFingerprint()`: they differ when the movement was retuned (or the character resized or given another body) since. A level rebuilt, or changed, since the recording also changes what the frames meet.

**A tap is missed, or counted twice.**
Something is reading the device outside the `InputLatch`: a render frame may run no physics step, or several, and only the latch turns what it read into one frame per step (a tap shorter than a step kept, a press not repeated in a second step). Read new buttons in `ACInputManager`, into the latch (AGENTS.md, Things that aren't obvious, lists where a new button goes).

## The body and the ground
**The character's feet float above slopes or stairs, or the root jumps a tread at a time and settles.**
Check Hover Over Steps is on (the MovementController, on the Character prefab): standing on its feet, the body's rounded bottom rests on edges and slopes with the feet in the air under it. Hovering, a body reaching out over a slope further than a step clears rests on it (a long body's back end on the steps behind, going down a flight): its parts' extents say how far. `GroundContactTests` writes each run's steps to Logs/StepTraces, with how far the feet were above the ground under them (see [Testing](testing.md)).

**A character's feet sink into the floor.**
Its body sits above its feet (a Body Definition whose parts' bottoms are above them, as the fox's back is), and Hover Over Steps is off: only hovering stands such a body on its feet. Turn it on.

**A character can't climb a flight of stairs it should.**
Hovering, a long body meets the next step ahead of its feet, and steps up onto it only if it's no more than a step above the ground in front of it (each step of a flight is). A taller first step, or a gap before the flight, stops it as a wall would.

**Something passes through the character, or isn't reported hitting it.**
What meets the character meets its model's hitboxes (see [Hitboxes](animation.md#hitboxes)). As made, they detect (triggers): things pass through, and only what has a Rigidbody is reported (Report Static Contacts reports the rest). Set Response to Collide for them to be solid. A model with none hasn't been fitted: run Tools > Character Showcase > Fit Hitboxes.

**The feet's IK, the movement or another character stands on the character's own limbs.**
The CharacterHitbox layer has crept into a layer mask (the MovementController's, a foot placement's Ground Layers, a camera's): hitboxes must stay out of them (an EditMode check, `PreparedModelTests`, holds the Character prefab and the models to it).

**A new setting's default didn't reach a prefab.**
Unity keeps an imported copy of each prefab: a serialized field added with one default, and then given another in code, keeps the first in prefabs that don't set it, until they're imported again. Set the value on the asset itself (as Hover Over Steps is, on the Character prefab).

