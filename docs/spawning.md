# Spawning the Character

A scene doesn't need the character placed in it: a **CharacterSpawner** (Add Component > CharacterSpawner) spawns it. It takes:

- a character prefab, and optionally a model and a movement mode asset to give the spawned character instead of the prefab's own;
- a list of spawn points: a world position for the character's feet and a rotation for the way it faces;
- a Spawn On Start checkbox.

With Spawn On Start ticked, it spawns the character when the scene starts, at one of its spawn points chosen at random. Untick it to spawn from code with `Spawn()` (a random point) or `SpawnAt(index)`. Each call spawns another character. The spawn points are drawn in the Scene view as capsules, with an arrow for the way each one faces.

The spawner tells whatever needs the new character about it through its On Character Spawned event, which can be wired up in the inspector, and the `CharacterSpawned` C# event. A camera gets the character from its On Camera Target Spawned event, with the character's eye point and size reference (its model parent, whose scale is its size), for the camera rig's `Follow(target, eyeAnchor, sizeReference)` (see [Camera System](camera.md)). The camera snaps to it, looks the way it faces, as it does at the start, puts the first-person view at its eyes and frames it at its size. (`Follow(Transform)` takes just a target.) The spawner's Start runs before other scripts' Start, so a camera's Start already sees the spawned character.

## Setting up a scene

**Tools > Character Showcase > Set Up Scene…** sets up the open scene to spawn a character. In its window, choose:
- the character prefab
- its model: the prefab's own, or any model Prepare Character Models has set up (see [Character models](animation.md#character-models)), shown with a preview and its height
- its movement mode asset

The choices start from the scene: its existing setup's, or else its placed character's (the prefab it's an instance of, its model and movement mode), or else the package's Character prefab as it is. Then press Set Up.

The scene gets a Game Setup object with a Character Spawner that spawns the character on Start and hands it to a camera. The camera is the scene's own camera rig if it has one, or else a copy of the demo scene's Main Camera (so it's set up and tuned the same). The input manager is likewise the scene's own, or a copy of the demo's. Only the model and movement mode that differ from the prefab's are stored on the spawner, so later changes to the prefab carry through.

Where the character spawns, in order of preference:
1. The scene's existing setup's spawn points: setting up a scene again replaces its setup but keeps them.
2. The level's spawn markers (objects named "Spawn Point …", facing their forward) and where the scene's placed characters stand.
3. The ground below the middle of the Scene view, facing the way the view looks.

Move or add spawn points on the Character Spawner afterwards.

- Characters placed in the scene are replaced by spawn points where they stood, unless you untick that (they're then left alongside the spawned one).
- The scene's other cameras are turned off (their objects deactivated, not deleted), unless you untick that too.
- The change can be undone and isn't saved for you.

To change the demo scene's model, open it and set it up with the model you want: its placed character, if it still has one, becomes a spawn point. The window's buttons set up the model files (Prepare Character Models) and the animation files (the Quaternius libraries and motion-capture clips), and Refresh lists newly prepared models.

## Levels and the command line

The Meridian Complex's builder places its spawn markers (see [Test Levels](levels.md)), so after rebuilding the level (which replaces its scene, setup included), open it and press Set Up to spawn there again.

From the command line, with Unity closed, `-executeMethod CharacterShowcase.EditorTools.SceneGameSetup.SetUpFromCommandLine -scene <scene path>` sets a scene up and saves it, optionally with `-prefab <path>`, `-model <a prepared model's name or source path>` and `-movementMode <asset path>`. Run after building the level, it sets that up too.
