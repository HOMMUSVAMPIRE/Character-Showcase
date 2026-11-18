# Spawning the Character

A scene doesn't need the character placed in it: a **CharacterSpawner** (Add Component > CharacterSpawner) spawns it. It takes:

- a character prefab, and optionally a model and a movement mode asset to give the spawned character instead of the prefab's own;
- a list of spawn points: a world position for the character's feet and a rotation for the way it faces;
- a Spawn On Start checkbox.

With Spawn On Start ticked, it spawns the character when the scene starts, at one of its spawn points chosen at random. Untick it to spawn from code with `Spawn()` (a random point) or `SpawnAt(index)`. Each call spawns another character. The spawn points are drawn in the Scene view as capsules, with an arrow for the way each one faces.

The spawner tells whatever needs the new character about it through its On Character Spawned event, which can be wired up in the inspector, and the `CharacterSpawned` C# event. A camera gets the character from its On Camera Target Spawned event, with the character's eye point and size reference (its model parent, whose scale is its size), for the camera rig's `Follow(target, eyeAnchor, sizeReference)` (see [Camera System](camera.md)). The camera snaps to it, looks the way it faces, as it does at the start, puts the first-person view at its eyes and frames it at its size. (`Follow(Transform)` takes just a target.) The spawner's Start runs before other scripts' Start, so a camera's Start already sees the spawned character.

## Spawning from code
```csharp
CharacterSpawner spawner = ...;            // the scene's Game Setup has one
spawner.SpawnOnStart = false;              // before its Start, to spawn only from code
GameObject character = spawner.Spawn();    // at a random spawn point (or SpawnAt(index))
spawner.CharacterSpawned += c => { ... };  // after every spawn, from code or on Start
```

- **What's returned:** the spawned character's root, or null (with an error in the Console) if the spawner has no prefab, no spawn points, or no spawn point at that index. `Character` is the one spawned most recently, and `LastSpawnIndex` the point it was spawned at.
- **What's set before it starts:** the spawner's Model Source and Movement Mode (when set) go on the character as it's created, before its own Start, which is when it loads its model. Set `ModelSource`, `MovementMode`, `CharacterPrefab` or `SpawnPoints` from code before calling `Spawn`, and the next character uses them.
- **Where it starts:** the character is created at the spawn point's pose rather than moved there afterwards, so its first physics step, and its interpolation, start from the spawn point. It starts with no momentum and no ability under way.
- **The order things happen in:** the spawner runs its Start before other scripts' (its execution order is -100), so anything that looks for the character in its own Start finds it. The C# `CharacterSpawned` event comes first, then On Character Spawned, then On Camera Target Spawned.

## Edge cases
- **Respawning.** The spawner has no respawn of its own: each `Spawn` makes a new character and leaves the earlier ones. To respawn, destroy the old character (`spawner.Character`) and spawn again: the new one starts afresh. Moving an existing character instead (setting its Rigidbody's position) keeps its momentum and whatever ability it was in the middle of, such as riding a zipline, and nothing resets those for it yet.
- **More than one character.** A spawn says who the character is for: `Spawn(owner)` or `SpawnAt(index, owner)`, the local player's unless said (a `CharacterOwner`: Local Player, AI or Remote, kept on its ACCharacterController's Owner). Only the local player's character moves by the player's input (the scene's `ACInputManager`) and is handed to the camera (On Camera Target Spawned), so the camera follows the player whatever else is spawned. An AI's or a remote character stands idle for now: nothing else drives a character's input yet.
- **Characters share their movement mode.** The spawned character uses the movement mode asset itself, not a copy, so a change to it at runtime (or in play mode in the editor) changes every character using it, and stays on the asset.
- **The model arrives a moment later.** A model source creates its model asynchronously, so for its first frames the character may have no model (`CharacterModelHost.IsModelReady` says when it has, and its `ModelLoaded` event fires). Its movement works meanwhile, on the prefab's collider. Once the model is in, the collider is fitted to it, which waits for room if the bigger collider wouldn't fit where the character stands.
- **Destroying a character** gives its model back to its source (`CharacterModelHost` releases it), so a source that pools models gets it back. The spawner's `Character` reads as null afterwards.
- **Size.** A character spawns at its prefab's size. Resize it afterwards with `ACCharacterController.SetScale`, which eases to the new size and waits for room to grow (see [Character Size](movement.md#character-size)).
- **What the scene needs.** A local player's character needs an `ACInputManager` in the scene, which it reads every physics step. Its move input is relative to the way the player looks, which the input manager takes from the main camera (`Camera.main`; without one, along +Z): the character itself never reads a camera. Set Up Scene provides both.
- **Spawn points in the wrong place.** A spawn point is used as it is: one inside something or floating in the air spawns the character there, to be pushed out of what it overlaps or to fall. Set Up Scene puts the points it makes on the ground.
- **Random spawn points aren't repeatable.** `Spawn` picks with Unity's `Random`, so a run doesn't pick the same points again unless the game seeds it. Use `SpawnAt` where it must.

## Setting up a scene

**Tools > Character Showcase > Set Up Scene…** sets up the open scene to spawn a character. In its window, choose:
- the character prefab: chosen from the project's prefabs with an ACCharacterController (the Choose list beside the field), or dropped on the field, which takes only those
- its model: the prefab's own, or any model Prepare Character Models has set up (see [Character models](animation.md#character-models)), shown with a preview and its height
- its movement mode asset

The choices start from the scene: its existing setup's, or else its placed character's (the prefab it's an instance of, its model and movement mode), or else the package's Character prefab as it is. Then press Set Up.

The scene gets a Game Setup object with a Character Spawner that spawns the character on Start and hands it to a camera. The camera is the scene's own camera rig if it has one, or else an instance of the Camera Rig prefab (`Assets/Prefabs/Camera Rig.prefab`), so it's set up and tuned the same, and retuning the prefab retunes every scene set up with it. The input manager is likewise the scene's own, or an instance of the Input Manager prefab. Only the model and movement mode that differ from the prefab's are stored on the spawner, so later changes to the prefab carry through.

Where the character spawns, in order of preference:
1. The scene's existing setup's spawn points: setting up a scene again replaces its setup but keeps them.
2. The level's spawn markers (objects named "Spawn Point …", facing their forward) and where the scene's placed characters stand.
3. The ground below the middle of the Scene view, facing the way the view looks.

Move or add spawn points on the Character Spawner afterwards.

- Characters placed in the scene are replaced by spawn points where they stood, unless you untick that (they're then left alongside the spawned one).
- The scene's other cameras are turned off (their objects deactivated, not deleted), unless you untick that too.
- The change can be undone and isn't saved for you.

To change a level's model, open it and set it up again with the model you want: its spawn points are kept. The window's buttons set up the model files (Prepare Character Models) and the animation files (the Quaternius libraries and motion-capture clips), and Refresh lists newly prepared models.

## Levels and the command line

The Meridian Complex's builder places its spawn markers (see [Test Levels](levels.md)), so after rebuilding the level (which replaces its scene, setup included), open it and press Set Up to spawn there again.

From the command line, with Unity closed, `-executeMethod CharacterShowcase.EditorTools.SceneGameSetup.SetUpFromCommandLine -scene <scene path>` sets a scene up and saves it, optionally with `-prefab <path>`, `-model <a prepared model's name or source path>` and `-movementMode <asset path>`. Run after building the level, it sets that up too.
