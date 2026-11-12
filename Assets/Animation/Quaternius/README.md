# Quaternius Universal Animation Libraries

The **Universal Animation Library** (its free Standard edition) and **Universal Animation Library 2** (its Source
edition, which has everything the Standard one does and more, wall runs among them), from [quaternius.com](https://quaternius.com/packs/universalanimationlibrary.html) and
[itch.io](https://quaternius.itch.io/universal-animation-library). Both are **CC0 1.0** (public domain; see
LICENSE.txt): free to use, change and redistribute, commercially too, with no attribution needed (credit is welcome).

| File | Contents |
|---|---|
| `UAL1_Standard.fbx` | 43 animations: idles, walk, jog, sprint, jump (start, loop, land), crouch, roll, swim, sit, drive, pistol, sword, spells, punches, hits, death, interact, dance |
| `UAL2.fbx` | 133 animations: wall runs (a wall on the left and on the right, and jumps off each), climbing up 1 m and 2 m, getting off a wall, stepping up, a safety vault, double jumps, slides, ninja jumps, strafing and backward walks, 180° turns, sword (light, heavy, aerial) and melee combos, shield, bow, fishing, mining, farming, chests, throw, consume, zombies, idle variants, carrying |

Each file is Quaternius's mannequin with every animation as a take of its own, on one humanoid rig. These are the
**in-place** versions (the packs' `_RM` files bake the root motion in; the character controller does the moving here
instead). The packs' glTF copies and setup images aren't included. Browse every animation in Quaternius's
[Animation Viewer](https://quaternius.com/animviewer.html).

**Tools > Character Showcase > Prepare Quaternius Animations** (`QuaterniusAnimationImporter`) sets every FBX here up
as Humanoid animations, as Quaternius's own Unity setup does: the axis conversion baked in, a Humanoid avatar (its
reference pose straightened into a T-pose, as every model's is), and a clip per take, named for it (`Walk_Loop`),
looping if its name ends in `_Loop`. It runs when the mannequin is generated, and skips files already set up.

**Using them:** any clip here plays on any humanoid model in the project. The character's Animator uses the walk,
jog, sprint and jump (see the main README). Generating the mannequin also raises those few clips onto the mannequin's
floor (their root height offsets): don't reset them by hand.

**The Pro editions** (paid, on itch.io) add more animations to each library. Drop their Unity FBX files in this folder
and run Prepare Quaternius Animations: every FBX here is set up the same way.

The libraries' female mannequin (CC0 too) is in `Assets/Models/CharacterModels/Quaternius`, as a character model.
