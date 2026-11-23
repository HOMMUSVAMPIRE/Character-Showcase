# Getting Started in Your Project
How to use Meridian Motion in a Unity project of your own: what it needs, putting it in, the two things a new project has to set up (the hitbox layer and the output folder), and a character in a scene you already have. To try it in this repository instead, open a showcase level and press Play (see the [README](../README.md#how-to-open-this-project)). New to Unity? [Unity Basics](unity-basics.md) explains the words used here.

*See also:* [Glossary](glossary.md).

## What it needs
- **Unity:** the version in the package's `package.json` (its `unity` field), or newer.
- **Packages:** the ones `package.json` lists under `dependencies` (the Input System and the Universal Render Pipeline), installed with the Package Manager (Window > Package Manager). Your project should already render with URP: the package's water shaders are URP's.
- **Input handling:** Project Settings > Player > Active Input Handling set to Input System Package (New) or Both. The character reads the Input System's actions.

TextMesh Pro is used only by the showcase's levels (their signs). The package doesn't need it.

## Putting it in your project
The package is one folder: `Assets/MeridianMotion`. Copy it, with its `.meta` files, into your project's `Assets` folder, or import it from the Asset Store once it's published there. Keep the `.meta` files: the package's prefabs and assets refer to one another through them. The tools find the package's assets wherever the folder is (by their GUIDs: `MeridianMotionAssets`), so you can put it in a subfolder if you like.

Nothing in `Assets/Showcase` is needed. It's this repository's levels, the model pipeline and the sample characters (the fox, the extra humanoids).

When the folder is in, **Tools > Meridian Motion > Start Here** opens a window with every everyday tool. It also opens by itself the first time, and it warns you if the hitbox layer (below) is missing.

## The hitbox layer
A character's hitboxes, the capsules on its model's bones that what comes at it meets (see [Hitboxes](animation.md#hitboxes)), go on a layer of their own. By default that's the CharacterHitbox layer. A package can't bring layers with it, so a new project hasn't got one. Without it, the hitboxes stay on whatever layer their model was saved with.

The **Character Doctor** (Tools > Meridian Motion > Character Doctor) checks it as "Its hitboxes' layer" and offers two fixes:
- **Add the CharacterHitbox Layer:** adds it to Project Settings > Tags and Layers, in the first free user layer.
- **Or put them on [a layer] > Use:** puts the character's hitboxes on a layer your project already has. The choice leaves out Default, Ignore Raycast and the layers the character's movement walks on (they would meet its own hitboxes). It sets the character prefab's Character Model Slot > Hitbox Layer, which its variants take too.

Set Up Scene points you to the Character Doctor when the layer needs fixing. The character moves the same either way: only what meets its hitboxes depends on the layer.

## The output folder
The tools make things: prepared models, the characters New Character makes, and the water effects when they're missing. They put them all in one folder of your project, the **output folder**, and never write into the package's own folder (which a package installed under `Packages/` keeps read-only). Choose it in **Project Settings > Meridian Motion > Output Folder**: `Assets` unless you choose another (this repository's is `Assets/Showcase`). In it:
- `Models/Humanoid/[name]`: humanoid models, each in a folder of its own, prepared by Prepare Humanoid Models (see [Adding a Character](adding-a-character.md)).
- `Models/Generic/[name]`: generic-rig characters, the ones that aren't humanoids (see [Generic Rigs](generic-rigs.md)).
- `Characters/[name]`: characters made with New Character.
- `Abilities/`: abilities made with New Ability (its Where: Project), in their own assembly, with the ledger of their flags' bits (`Character Flags.json`: see [Adding a Movement Ability](adding-an-ability.md#an-ability-of-your-projects-own)).

The mannequin and its animation stay in the package. The tools read them from there.

## A character in your own scene
1. Open your scene. The character walks on colliders on the layers its movement queries: MMotionMotor > Movement Layer Mask on the Character prefab (`Assets/MeridianMotion/Content/Prefabs`). Put your level's ground on one of them, or add your layers to the mask on a character of your own (New Character makes one, a variant of Character.prefab, whose settings are its own).
2. **Tools > Meridian Motion > Set Up Scene…** adds the character's spawner, a spawn point, the camera rig and the input manager to the open scene, wearing the model you choose (see [Spawning the Character](spawning.md)). If the scene has spawn markers, it spawns at them. If not, it spawns below the middle of the Scene view.
3. Press Play. The controls are in the [README](../README.md#controls).

## Where next
- [Adding a Character](adding-a-character.md): your own model, a character of your own, or a generic-rig character.
- [Tuning the Character](tuning.md): where its settings are, and tuning presets.
- [Adding a Movement Ability](adding-an-ability.md#an-ability-of-your-projects-own): a move of your own, which New Ability makes in your project's folder and assembly (Abilities, in the output folder), so upgrading the package leaves it alone.
- [Your First Ability](tutorial-first-ability.md): a new move, built step by step.
- [Troubleshooting](troubleshooting.md): when something's wrong.
