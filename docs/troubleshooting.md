# Troubleshooting
Problems that are easy to run into in this project, what causes them, and the fix.

## The movement asset
**A component shows as "Missing component" in the movement asset.**
A component's class name is saved in the asset (its list is `[SerializeReference]`), so renaming or moving the class, or changing its namespace, loses it. Put the old name back, or add `[UnityEngine.Scripting.APIUpdating.MovedFrom]` with the old name, namespace or assembly to the class. `MovementCompositionTests` fails while the asset has a missing component.

**A setting went back to its default after a code change.**
Unity drops a saved value whose field it can't find. A renamed field keeps its value with `[UnityEngine.Serialization.FormerlySerializedAs("oldName")]`. A setting moved into another component can't follow that way: set it again on the asset, and in any tuning presets that had it (a preset reports a setting its component no longer has when it's applied).

**Changes made while playing were kept.**
The character uses the movement asset itself, so changes to it in play mode are changes to the asset. Save a tuning preset first if you want to go back (see [Tuning the Character](tuning.md#tuning-presets)).

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
- *An assembly references what it may not, or isn't in the table:* AGENTS.md's assembly table says what each may reference; the check's `ALLOWED_REFERENCES` holds it. A new assembly or reference goes in both, if it keeps the rules (the camera and the level builders know nothing of the character).
- *The camera or a level builder uses character code* (`using Character;`): for the camera, see [Adding a camera feature that needs the character](camera.md#adding-a-camera-feature-that-needs-the-character); a level builder makes geometry, lighting and markers only, and game setup connects them (see [Adding Level Content](adding-level-content.md)).
- *A movement asset names a class that no longer exists:* a movement component was renamed or moved. Give the class `[UnityEngine.Scripting.APIUpdating.MovedFrom(... sourceClassName: "OldName")]`, or the asset shows it as a missing component, its settings lost.

## Generated assets
**A change to a generated asset disappeared.**
The mannequin, its Animator Controller, the model prefabs, the level scenes, the level specs, the climbable paint masks and the movement envelope are all written by editor tools, which overwrite them. Change the generator and run it again (the table in AGENTS.md, Generated assets, says which tool makes which).

**A level's mechanics (moving platforms, ropes, spawn points) don't work after rebuilding it.**
Rebuilding a level removes its game setup. Run Set Up Scene on it again (see [Spawning the Character](spawning.md)).

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

