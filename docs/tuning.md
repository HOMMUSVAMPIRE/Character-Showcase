# Tuning the Character
How to change how the character moves, keep tunings you like, and what changes with them. What each setting does is in its tooltip, and in [Movement](movement.md), [Movement Abilities](abilities.md) and [Climbing and Traversal](traversal.md).

## Where the settings are
- **The movement:** the BasicMovementMode asset (Assets/ScriptableObjects/BasicMovementMode.asset). Every part of the movement is a component in its Components list, each with its settings under its name: Walking & Running, Fall & Air Handling (gravity, the fall speed limit, air steering, and the ground's friction on landing), Jumping, Dashing, and each ability. Each character prefab has its own mode: the fox's is Assets/ScriptableObjects/Fox Movement.asset (see [Creatures](creatures.md)).
- **The character itself:** the MovementController on the Character prefab (Assets/Prefabs/Character.prefab): its step height, walkable slope, collision and moving-ground settings, and Size Scaling.
- **Its size:** the ACCharacterController's Scale (see [Character Size](movement.md#character-size)).
- **The camera:** the CameraRig (see [Camera System](camera.md)).

Change the asset, not the code: the defaults in the code are only what a new asset starts with.

## Turning things off
Each component's toggle turns that part of the movement off, as if it weren't there; turned off while it has the character, it lets go. Most abilities can also be turned off by their main setting at 0 (a duration, a reach: each says so in its tooltip). Some components' settings shape others whether or not they're turned on (Crouching's Crouch Height is how low a roll goes; Walking & Running's speeds are what air steering aims for): see [Movement Components](movement.md#movement-components).

The order of the components matters only where several could act at once: the higher one takes the character. The default order is the one `BasicMovementMode.DefaultComponents` gives a new asset.

## Tuning presets
A tuning preset (a MovementTuningPreset asset: Create > Scriptable Objects > MovementModes > Tuning Preset) saves settings to apply again: a whole tuning you liked, or a few settings (a floatier jump, wall running turned off).

1. To save one, use Save Tuning as Preset at the bottom of the movement asset's inspector: the preset gets every setting of every component.
2. In the preset's inspector, each component it covers is listed with a tick beside each setting: ticked settings are set by the preset, and the rest are left alone. Changing a value ticks it; All and None tick or untick a whole component, and Turned On is its toggle. Add Component's Settings adds another component, unticked.
3. Apply To Mode sets the preset on its Movement Mode (the Character prefab's, unless another is picked), with Undo, and says what changed and what it couldn't set. Save From Mode replaces the preset with that mode's tuning now.

A preset only sets values and toggles: it never adds, removes or reorders components, and a setting of a component the mode doesn't have is left out (and reported). Presets are made and edited in the editor only.

## Capabilities
What the character can do as it's tuned (how high it steps and jumps, how far each kind of jump crosses, how high each climb reaches) is measured by running its own movement, and read at runtime with `MovementController.Capabilities` (see [Capabilities](movement.md#capabilities)). It's measured again after the tuning changes, so there's nothing to update by hand.

## What changes when you save
Saving the tuning is all the character needs. In this project, saving also updates the level checks, an optional level-side export rather than part of tuning the character (the level checks' editor assembly, Assets/Editor/LevelChecks, which the character doesn't need): the showcase levels are checked against the default character's capabilities, written as the movement envelope (Tools/LevelSpec/envelope.json).

- **Saving the movement asset** (or the Character prefab, a model source, or the level generation settings) writes the envelope again by itself (`MovementEnvelopeAutoWriter`), and the Console logs what changed, such as a jump that reaches further, or a double jump added or gone.
- **Then check the levels:** the levelspec tool's movement checks grade every gap and climb in a level by the moves that cross it, and warn of any out of reach (see [Level Specs](level-specs.md)).
- **A change made outside the editor** (an edit to the asset's file, or in batch mode) doesn't write the envelope: run Tools > Character Showcase > Level > Specs > Write Movement Envelope, or `MovementEnvelopeTests` fails.

## Things to tune together
- **Walk and run speeds and the locomotion animations.** The walk, brisk walk, jog and run loops are placed by the speeds their strides suit, so feet slide at speeds far from those. See [Walking and running](animation.md#walking-and-running).
- **The swim depth and the swimming animation.** The swimming clips are floated at Swimming's Swim Depth by Generate Mannequin, for the height of the body the Character prefab has with its model (its model's own, when its Character Model Host's Body Fit takes it), so after changing either, run Generate Mannequin again. See [Swimming](animation.md#swimming).
- **Gravity and the jump.** Jumps are set by height (Jumping's Min Jump Height and Max Jump Height), and their launch speed is worked out from gravity, so changing Gravity Strength changes how long a jump takes, not how high it goes.
- **Size.** By default the movement scales in proportion with the character's size, so a small character moves like a scaled copy of a big one; the MovementController's Size Scaling can instead scale it as real animals do. See [Character Size](movement.md#character-size).

## Traps
- **Changes made in play mode stay.** The character uses the movement asset itself, so changes to it in the inspector while playing are kept when play mode ends (unlike changes to objects in the scene).
- **The tests don't change the asset.** They work on their own copy of the movement mode, and read expected values from the settings, so retuning doesn't break them. A test that needs a particular tuning (no air jumps, say) sets it on its copy.
- **Moving a setting into another component in code loses its saved value.** Set it again on the asset, and in any presets that had it.
