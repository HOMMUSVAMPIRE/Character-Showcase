# Unity Basics

The Unity words the rest of the docs use, each in a few sentences, with where it shows up in this project. For the project's own terms (movement mode, Body Fit, Set Up Scene and so on), see the [Glossary](glossary.md).

## The editor

**Unity Hub and the Unity Editor.** Unity Hub installs Unity and opens projects; the Unity Editor is the program you work in. This project needs the exact version in `ProjectSettings/ProjectVersion.txt` (the README's [How to open this Project](../README.md#how-to-open-this-project) says how to install it). Opening it with another version can change files throughout the project.

**Project window.** The editor's file browser: everything under the `Assets` folder (scripts, models, prefabs, scenes, settings). Double-click a scene to open it, or a prefab to edit it; right-click for Create, the menu that makes new assets.

**Scene, Scene view and Hierarchy.** A scene is a level, saved as a `.unity` file: `Assets/Scenes/LevelPlayground/LevelPlayground.unity` is one. The Scene view shows the open scene to look round and edit, and the Hierarchy lists the objects in it, nested under each other (a scene set up to play has a Game Setup object, with the Character Spawner under it).

**GameObject and component.** Everything in a scene is a GameObject, which does nothing by itself: what it does comes from the components on it. A component written in C# is a MonoBehaviour: the character's ACCharacterController, MovementController and CharacterModelHost are components on the Character prefab's top object.

**Inspector.** The panel showing what's selected: a GameObject's components, or an asset's settings, each a field you can change. Hover over a field to see its tooltip, which says what it does. The docs name a setting by its name in the Inspector (Body Fit, on the CharacterModelHost) rather than quoting its value.

**Play mode.** Pressing Play runs the open scene as the game. Changes to the scene's objects made while playing are undone when you stop, but changes to assets (a movement mode's settings, say) are kept: see [Traps](tuning.md#traps).

**Console.** The window of messages, warnings and errors (Window > General > Console). The project's tools write there: Prepare Character Models reports each model it prepared, and Set Up Scene says what it set up. Look there first when something doesn't work.

**Menus.** The project's tools are under Tools > Character Showcase in the editor's menu bar (Set Up Scene…, Prepare Character Models, and more). New assets are made with Create, in the Project window's right-click menu (or Assets > Create): the project's own are under Create > Character Showcase and Create > Scriptable Objects.

## Assets

**Asset.** Any file under `Assets` that Unity uses: a model, a texture, a scene, a prefab, a settings file. Unity imports each one into a form it can use, and keeps the result in the `Library` folder (which isn't in git: Unity rebuilds it).

**Prefab.** A GameObject saved as an asset, with its components and their settings, to put in scenes as often as you like. `Assets/Prefabs/Character.prefab` is a prefab: the character, which a scene's Character Spawner makes a copy of when the game starts. Double-click a prefab to edit it in Prefab Mode, and the arrow at the top of the Hierarchy goes back to the scene.

**Prefab variant.** A prefab made from another, keeping everything it doesn't change. `Assets/Prefabs/Fox.prefab` is a variant of Character.prefab: it changes the model, the body and the movement mode, and everything else comes from Character.prefab, so a fix there reaches the fox too. Make one by right-clicking a prefab and choosing Create > Prefab Variant (see [Adding a Character](adding-a-character.md)).

**ScriptableObject.** A settings asset, saved as a `.asset` file, that many objects can share. The character's movement is one (`Assets/ScriptableObjects/BasicMovementMode.asset`, a movement mode), as are its body (`Character Body.asset`) and each model source. Every character using a ScriptableObject uses the same one, so changing it changes them all.

**Serialized field.** A setting Unity saves and shows in the Inspector. Its value is saved by the field's name, so renaming a field in code loses its saved values (see [Files to treat with care](../AGENTS.md#files-to-treat-with-care)).

**Model files, rigs, Humanoid and Avatar.** A model is usually an `.fbx` file: a mesh, and often a skeleton (its rig) with animations. Select one and its Rig tab in the Inspector says what kind of rig Unity treats it as. Humanoid means Unity has matched its bones to a person's (its Avatar, the matching), so any humanoid animation plays on it: that's how every humanoid model here plays the mannequin's animations. A creature, such as the fox (`Assets/Models/Creatures/Fox/Fox.fbx`), is Generic: its animations are made for its own skeleton.

**Animator and Animator Controller.** The Animator component plays a model's animations. Its Animator Controller is the asset saying which animation plays when, driven by values (parameters) such as speed and grounded: `Assets/Animation/Mannequin.controller` is the one every humanoid model shares, and the fox has its own, `Fox.controller`. The project sets the parameters from the character's movement (see [Character Model and Animations](animation.md)).

**`.meta` files.** Every file and folder under `Assets` has a `.meta` file beside it, holding its import settings and its ID (a GUID), which is how other assets refer to it. Unity makes them; keep each with its file when moving, copying into git or deleting (move files inside the Unity Editor and it moves them for you).

## Code and packages

**Physics step, FixedUpdate and Update.** Unity moves physics on in fixed steps, calling each component's FixedUpdate once per step, and draws the screen in frames, calling Update once per frame. The character moves in FixedUpdate and its model is animated in Update (see [Architecture](architecture.md)).

**Rigidbody, collider and layers.** A Rigidbody is what physics moves; a collider is a shape physics can hit. Every GameObject is on a layer, and physics checks can leave layers out. The character has a Rigidbody, but its movement checks the world with its own body shape rather than a collider, and what hits the character hits its model's hitboxes, on the CharacterHitbox layer (see [Collision](movement.md#collision) and [Hitboxes](animation.md#hitboxes)).

**Assembly definitions.** An `.asmdef` file makes the scripts in its folder a separate assembly (a compiled library), which can use only the assemblies it lists. The project is split this way so that, for example, the camera can't use the character's code: AGENTS.md's assembly table says what may use what.

**Package Manager.** Unity's add-ons (the Input System, the Universal Render Pipeline, the test framework) are packages, listed in `Packages/manifest.json` and managed in Window > Package Manager. This project's packages are set; don't add, remove or update them unless that's the task.

**Test Runner.** Window > General > Test Runner runs the project's automated tests: EditMode tests check the editor tools and assets, and PlayMode tests run the character in a scene. See [Automated Tests](testing.md).

**Batch mode and `-executeMethod`.** Unity can be run from the command line without its window (`-batchmode`), to run the tests or a tool (`-executeMethod` and the tool's name). The docs give the command for each tool that can. Unity can't open a project that's already open in the editor, so close it first.

## Git

**Git LFS.** Git Large File Storage keeps big files (the models, the level scenes, the animations) outside git's history, downloading them when you clone. Without it installed before cloning, those files are short placeholder text files, and the models and scenes don't open. Install it from https://git-lfs.com, run `git lfs install` once, then clone (or run `git lfs pull` in a clone made without it).

**What isn't in git.** `Library`, `Temp`, `Logs` and `UserSettings` are Unity's working folders, made again when the project opens, so they're left out of git (`.gitignore`).
