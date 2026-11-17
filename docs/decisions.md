# Design Decisions
Choices about how the project is built, why they were made, and what was decided against. Read this before undoing one: each had a reason. Add an entry when a decision like these is made.

## Movement is a list of components, not one class (November 2026)
**Decided:** a movement mode (`ComposedMovementMode`) is a list of `MovementComponent`s, each part of the movement with its own settings, which can be turned off, removed, added and reordered on the asset. Every part is a component, walking and falling included: `BasicMovementMode` keeps only what they share.

**Why:** `BasicMovementMode` had grown into one class holding every ability, where each new ability touched the same files. As components, an ability is one file, a designer can turn it off or take it out without code, and another kind of movement reuses the same components in a different list.

**Against:** inheritance (a mode subclass per combination of abilities), which multiplies classes and can't be changed on an asset.

## A component reads another's settings, but takes over only from one turned on (November 2026)
**Decided:** a component shaped by another's settings reads them whether that one is turned on or not, or its defaults if it's been removed (`SettingsOf<T>`): a roll goes as low as Crouching's Crouch Height even with crouching off. One handing the character to another finds it only turned on (`Mode.Get<T>`): climbing over a wall's top becomes a ledge climb only with Ledges on.

**Why:** turning an ability off should remove that ability, not quietly change the others.

## Crouching and sliding are one component (November 2026)
**Decided:** sliding is part of Crouching, with a Sliding Enabled toggle, rather than a component of its own.

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
**Decided, not yet built:** each character will run its own copy of its movement mode, with tuning presets applied on top of it. Tuning zones apply a preset while the character is inside and undo it when it leaves (zones inside zones stack, and respawning clears them), and a level can set one for all of itself.

**Why:** single levels, or parts of them, can then be played with different movement than the character usually has, without changing the asset.

## The body is a shape; hitboxes meet the world (November 2026)
**Decided:** the character's movement no longer depends on a collider: its body (`CharacterBody`) is a shape the movement sweeps and overlaps itself, and the Character and Fox prefabs have no collider of their own. What meets the character meets its model's hitboxes (`CharacterHitboxes`): a capsule on each body bone, fitted to the mesh, on the CharacterHitbox layer, which detect (triggers) unless set to collide.

**Why:** one collider round the whole character fit no one: the fox's box covered its head, body and legs, so its corners caught on things far from its paws, and the body's resting contact with the ground was fixed up in shape-specific ways. With the body a shape of its own, it can be shaped for moving (and next held above the ground on probes: see [Collision](movement.md#collision)) while the hitboxes follow the model closely.

**Against:** a trimmed root collider beside the hitboxes, for other characters to meet: the hitboxes are the character's presence, and characters will meet one another through their bodies (a list of them, as ropes and water are found) rather than colliders. Solid hitboxes by default: what comes at the character is only detected for now, as the Response setting allows changing later.

## The body hovers over steps (November 2026)
**Decided:** the character's body is held a step's height above its feet (Hover Over Steps, on by default), and probes under the feet keep them on the ground: steps pass under the body, and the feet step up and down onto them. The body standing on its feet (the setting off) is kept, as it was, for comparison and for bodies that can't be lifted (a sphere).

**Why:** the body's bottom resting on the ground was where the jitter came from (see the ground contact traces, in [Testing](testing.md)): a rounded bottom rolled over step edges and slopes with the feet in the air under it, the root jumping a tread at once and then settling, and a box rested on its corners, its feet up to most of a metre off the ground. Hovering, the mannequin's feet stay on the ground over ramps, stairs and edges.

**Against:** a fractional lift (part of the step height): steps between the lift and the step height would need the body's own step-up, which hovering otherwise retires. And treating the long fox's body differently: it still rides slopes and stairs on the ground ahead of its paws, which a rounder or pitched body is next to fix.

