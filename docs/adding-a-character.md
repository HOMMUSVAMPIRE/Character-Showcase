# Adding a Character

Start here to put a character of your own in the project. This page is written for someone new to Unity: the Unity words it uses (prefab, inspector, Project window and so on) are explained in [Unity Basics](unity-basics.md). It gives the steps; each links to the page with the details.

Before you start, open the project as the README says ([How to open this Project](../README.md#how-to-open-this-project)), with Git LFS installed: without it, the models are placeholder text files.

## What do you have?

| You have | Follow |
| --- | --- |
| A rigged humanoid model (a person-shaped model with a skeleton, usually an FBX file), and you want the existing character to wear it | [A new look for the humanoid](#a-new-look-for-the-humanoid) |
| A humanoid model, and you want a character of your own: its own prefab, movement, body and tuning, leaving the default character as it is | [A new character of your own](#a-new-character-of-your-own) |
| A rigged creature that isn't a humanoid (on four legs, say) | [A creature that isn't a humanoid](#a-creature-that-isnt-a-humanoid) |

Not sure whether your model counts as a humanoid? Unity calls a model a Humanoid when it can match its bones to a person's: hips, a spine, a head, two arms and two legs. If yours has those, try the first path: Unity tells you if it can't match them.

## A new look for the humanoid

You need a model file with a skinned mesh and its skeleton (a model that isn't rigged can be rigged by a tool such as Mixamo). It plays the mannequin's animations, so you don't need any animations of your own.

1. In the Project window, open the folder `Assets/Models/CharacterModels`. Make a folder there for your model: right-click, Create > Folder, and name it (`MyHero`, say).
2. Drag your model file (and its textures, if they're separate files) from your computer's file browser into that folder.
3. Wait for Unity to finish importing. Prepare Character Models then runs by itself: it sets the model up as a Humanoid and saves three files beside it, named after the model file:
   - `MyHero Model.prefab`: the model, ready to animate, with its IK and hitboxes;
   - `MyHero.asset`: its model source, the asset a character is given to wear the model;
   - `MyHero Body.asset`: a body fitted to the model (the shape the movement moves: see [Collision](movement.md#collision)).

   If nothing seems to happen, run **Tools > Character Showcase > Prepare Character Models**, which prepares every model in the folder and shows how each went in a window.
4. Open the scene to play in: double-click it in the Project window (`Assets/Scenes/LevelPlayground/LevelPlayground.unity`, say).
5. Open **Tools > Character Showcase > Set Up Scene…**. In its window:
   - leave Character Prefab as Character (if it shows another, press Choose beside it and pick Character);
   - under Model, find your model and press its Choose button;
   - leave Movement Mode as it is ("The prefab's own.");
   - press the Set Up button at the bottom.
6. Save the scene (File > Save) and press Play.

**What success looks like.**
- After step 3, the three files are beside your model, and the Console has a line from `[CharacterModelImporter]` saying your model is ready and how tall it is.
- In step 5, your model is listed under Model with a picture and its height, not under "Not available".
- In Play mode, the character is your model: it walks, runs, jumps and climbs with the mannequin's animations, its feet on the ground, facing the way it moves.

The model you choose is stored on that scene's Character Spawner (in its Game Setup object), not on `Character.prefab`, which keeps the mannequin (the tests spawn it). Set up each scene you want it in. To give the model to a character prefab of its own, follow [A new character of your own](#a-new-character-of-your-own).

**If something's wrong**, see [New characters](troubleshooting.md#new-characters) in Troubleshooting: a model skipped because Unity couldn't map it to a Humanoid, one far too big or small, facing backwards, its feet in or above the floor, or missing from Set Up Scene's list.

The details: [Character models](animation.md#character-models) (what Prepare Character Models does), [Feet on the ground at any size](animation.md#feet-on-the-ground-at-any-size) (how it measures the feet and the facing), [Replacing the model](animation.md#replacing-the-model) (model sources and Body Fit) and [Setting up a scene](spawning.md#setting-up-a-scene).

## A new character of your own

A character of your own is a prefab of its own, so its model, its movement and its body can be changed without changing the default character. There's no tool for this yet: it's done by hand, as `Assets/Prefabs/Fox.prefab` was (a prefab variant of `Character.prefab` with its own model source, body and movement mode).

1. **Prepare its model:** steps 1 to 3 of [A new look for the humanoid](#a-new-look-for-the-humanoid). You now have its model source, `MyHero.asset`.
2. **Make the prefab.** In the Project window, open `Assets/Prefabs`, right-click `Character.prefab` and choose Create > Prefab Variant. Name the new prefab (`MyHero`). A variant starts as a copy of Character.prefab and keeps only what you change on it, so improvements to Character.prefab reach it too.
3. **Make its movement mode.** Open `Assets/ScriptableObjects`, select `BasicMovementMode.asset` and choose Edit > Duplicate. Name the copy (`MyHero Movement`). Its tuning is its own: changing it changes only the characters that use it. (To start from the fox's tuning instead, duplicate `Fox Movement.asset`.)
4. **Set the prefab up.** Double-click your prefab to open it, and select its top object in the Hierarchy. In the Inspector:
   - on the **MovementController**, set Movement Handler to your movement mode (drag it from the Project window onto the field);
   - on the **CharacterModelHost**, set Model Source to your model's source (`MyHero.asset`);
   - on the **CharacterModelHost**, choose its Body Fit, which says where the character's body comes from:
     - **Model's Body** (what Character.prefab has, and the usual choice for a humanoid): the body Prepare Character Models fitted to the model, `MyHero Body.asset`. To shape it by hand, open it, untick Refit On Prepare (or preparing the model again refits it), and change its settings.
     - **Height**: the prefab's own body (the MovementController's Body Definition), scaled to be as tall as the model.
     - **Off** (what the fox has): the prefab's own body as it is. Give the prefab a body of its own first: duplicate `Assets/ScriptableObjects/Character Body.asset` (or Create > Character Showcase > Body Definition), set it as the MovementController's Body Definition, and shape it in its inspector.

   Save the prefab (File > Save) and go back to the scene with the arrow at the top of the Hierarchy.
5. **Tune it** in your movement mode's inspector: each part of the movement is a component in its list, with its own settings and a toggle to turn it off (Fox Movement turns off what a fox can't do). See [Tuning the Character](tuning.md).
6. **Put it in a scene.** Open the scene and **Tools > Character Showcase > Set Up Scene…**. Press Choose beside Character Prefab and pick your prefab; Model then says "The prefab's own (MyHero)" and Movement Mode "The prefab's own." Press Set Up, save the scene and press Play.

   To change a scene that's already set up without setting it up again, select its Game Setup > Character Spawner and set its Character Prefab. The spawner's Model Source and Movement Mode, when set, replace the prefab's own, so empty them to use your prefab's.

**What success looks like.** Your prefab is in Set Up Scene's Choose list; in Play mode the character is your model, moving with your tuning; and the default character, in other scenes, is as it was.

**Things to know.**
- Change your own copies, never `Character.prefab`, `BasicMovementMode.asset` or `Character Body.asset`: those are the default character's, and the tests'.
- The walk and run speeds suit the locomotion animations, so feet slide at speeds far from them: retune them together (see [Things to tune together](tuning.md#things-to-tune-together)).
- The swimming animations are floated for the Character prefab's body and its Swim Depth, so a character much taller or shorter, or with another Swim Depth, floats a little high or low in water (see [Floating the swimming clips](animation.md#floating-the-swimming-clips)).

**If something's wrong**, see [New characters](troubleshooting.md#new-characters): a prefab missing from Set Up Scene's list, a body too big or small for the model, a change that also changed the default character.

## A creature that isn't a humanoid

There's no general way to add one yet. Prepare Character Models only prepares Humanoids: a creature put in `Assets/Models/CharacterModels` is imported as a Humanoid, then skipped, with "Unity couldn't map its skeleton to a Humanoid" in the Console. Keep creatures out of that folder (the fox's files are in `Assets/Models/Creatures/Fox`). A general creature importer is planned.

The one creature, the fox, is built by a generator of its own, `FoxGenerator` (**Tools > Character Showcase > Generate Fox**), which also makes the fox's mesh in Blender. It's part of the model pipeline, not of the package, and it only makes the fox, but it's the worked example of everything a creature needs.

**To play the fox:** open a scene, **Tools > Character Showcase > Set Up Scene…**, press Choose beside Character Prefab and pick Fox. See [Playing the fox](creatures.md#playing-the-fox).

**To set up a creature of your own by hand,** make what FoxGenerator makes for the fox (each of the fox's is in `Assets/Models/Creatures/Fox`, to compare with):
1. Import the model outside `Assets/Models/CharacterModels`, and in its Rig tab (select the model, then the Inspector) set Animation Type to Generic and press Apply.
2. An Animator Controller for its animations (Create > Animator Controller), driven by Animator parameters: the fox's is `Fox.controller` (see [What the fox is](creatures.md#what-the-fox-is)).
3. An Animator Parameter Translator (Create > Character Showcase > Animation > Animator Parameter Translator), naming the controller's parameters, as `Fox Animator Translator.asset` does.
4. A model prefab: the model with an Animator using the controller, a child object named `Eyes` on its head (where the camera's first-person view goes: `CharacterModelHost.EyesName`), and PawPlacementIK set up for its legs, as the "On another creature" paragraph of [Paw placement](creatures.md#paw-placement) says.
5. A model source (Create > Character Showcase > Character Model (Direct Reference)), with the model prefab as its Model Prefab and the translator as its Translator. A source made by hand has Measure Ground On Load ticked, so it measures where its feet are when it loads.
6. Its hitboxes: **Tools > Character Showcase > Fit Hitboxes** fits them to every model a model source names (see [Hitboxes](animation.md#hitboxes)).
7. A character prefab: [A new character of your own](#a-new-character-of-your-own) from step 2, with the source from step 5, Body Fit Off and a Body Definition shaped for the creature (the fox's is `Fox Body.asset`), and a movement mode with what it can't do turned off.

## Where to go next
- [Spawning the Character](spawning.md): spawn points, NPCs, spawning from code.
- [Tuning the Character](tuning.md): where the settings are, and saving tunings as presets.
- [Character Model and Animations](animation.md): models, animations and IK in full.
- [Creatures](creatures.md): the fox, and its paw placement.
- [Troubleshooting](troubleshooting.md#new-characters): when something's wrong.
