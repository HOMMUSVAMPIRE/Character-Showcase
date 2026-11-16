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

**Running the tests from the command line gives no results.**
Unity can't open a project that's open in the editor, so a batch-mode run quits at once, with no results file. Close the editor first, or run the tests from the Test Runner window.

**The hygiene check fails.** (`python3 Tools/CI/check_project_hygiene.py`)
- *A missing or orphaned `.meta`:* every file and folder under Assets needs one, moved and deleted with it. Copy a neighbouring `.meta` of the same type with a fresh GUID (`python3 -c "import uuid; print(uuid.uuid4().hex)"`).
- *A duplicate GUID:* a `.meta` was copied without changing its GUID.
- *A field the tests read by name doesn't exist:* a setting was renamed or moved. Update the test, or keep the name.
- *Two settings share a name:* setting names must be unique across the movement mode and all its components, since tests find them by name alone.
- *The camera depends on character code:* see [Adding a camera feature that needs the character](camera.md#adding-a-camera-feature-that-needs-the-character).

## Generated assets
**A change to a generated asset disappeared.**
The mannequin, its Animator Controller, the model prefabs, the level scenes, the level specs, the climbable paint masks and the movement envelope are all written by editor tools, which overwrite them. Change the generator and run it again (the table in AGENTS.md, Generated assets, says which tool makes which).

**A level's mechanics (moving platforms, ropes, spawn points) don't work after rebuilding it.**
Rebuilding a level removes its game setup. Run Set Up Scene on it again (see [Spawning the Character](spawning.md)).

**Exporting a level's spec refuses to replace it.**
The spec has changed since it was exported (in the Level Editor, say). Check that only your changes differ, then export with `-force` (see [Level Specs](level-specs.md)).

## Movement oddities
**Something reads a lagging position.**
The character's and moving platforms' Rigidbodies are interpolated, so their Transforms show a pose between physics steps. Movement code reads `Rigidbody.position` and `rotation` (see `MovementController.GetPhysicsPose`).

**A timer behaves differently in the capability measurements.**
Movement must time things by `MovementController.StepTime`, never `Time.time` or `Time.fixedTime`: the measurements step a copy of the character faster than real time.

**The camera stutters behind the character.**
The camera's Follow section interpolates a physics-driven target between steps. Check it's turned on, and that the character's Rigidbody is interpolated.
