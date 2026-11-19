# Adding a Character

Start here to put a character of your own in the project. This page is written for someone new to Unity: the Unity words it uses (prefab, inspector, Project window and so on) are explained in [Unity Basics](unity-basics.md). It gives the steps; each links to the page with the details.

Before you start, open the project as the README says ([How to open this Project](../README.md#how-to-open-this-project)), with Git LFS installed: without it, the models are placeholder text files.

## What do you have?

| You have | Follow |
| --- | --- |
| A rigged humanoid model (a person-shaped model with a skeleton, usually an FBX file), and you want the existing character to wear it | [A new look for the humanoid](#a-new-look-for-the-humanoid) |
| A humanoid model, and you want a character of your own: its own prefab, movement, body and tuning, leaving the default character as it is (New Character makes it) | [A new character of your own](#a-new-character-of-your-own) |
| A rigged model that isn't a humanoid (on four legs, say), with its own animations: a generic rig | [A generic rig (not a humanoid)](#a-generic-rig-not-a-humanoid) |

Not sure whether your model counts as a humanoid? Unity calls a model a Humanoid when it can match its bones to a person's: hips, a spine, a head, two arms and two legs. If yours has those, try the first path: Unity tells you if it can't match them.

**Humanoid or generic rig?** The line is the skeleton and the animation, not what the character is. A model built like a person (hips, a spine, a head, two arms and two legs, moving as a person does) is a humanoid, however monstrous: an ogre, a goblin, a devil or angel, a werewolf standing upright. It plays the shared humanoid animations and IK. Anything else uses a generic rig with clips of its own: a horse, a rat, a bat, a slime, and also a bear, even though it can stand on two legs, since it doesn't move like a person.

## A new look for the humanoid

You need a model file with a skinned mesh and its skeleton (a model that isn't rigged can be rigged by a tool such as Mixamo). It plays the mannequin's animations, so you don't need any animations of your own.

1. In the Project window, open `Models/Humanoid` in [the output folder](getting-started.md#the-output-folder) (`Assets/Showcase/Models/Humanoid` in this project; make the folders if they aren't there). Make a folder there for your model: right-click, Create > Folder, and name it (`MyHero`, say).
2. Drag your model file (and its textures, if they're separate files) from your computer's file browser into that folder.
3. Wait for Unity to finish importing. Prepare Humanoid Models then runs by itself: it sets the model up as a Humanoid and saves three files beside it, named after the model file:
   - `MyHero Model.prefab`: the model, ready to animate, with its IK and hitboxes;
   - `MyHero.asset`: its model source, the asset a character is given to wear the model;
   - `MyHero Body.asset`: a body fitted to the model (the shape the movement moves: see [Collision](movement.md#collision)).

   If nothing seems to happen, run **Tools > Meridian Motion > Prepare > Humanoid Models**, which prepares every model in the folder and shows how each went in a window.
4. Open the scene to play in: double-click it in the Project window (`Assets/Showcase/Scenes/LevelPlayground/LevelPlayground.unity`, say).
5. Open **Tools > Meridian Motion > Set Up Scene…**. In its window:
   - leave Character Prefab as Character (if it shows another, press Choose beside it and pick Character);
   - under Model, find your model and press its Choose button;
   - leave Movement Mode as it is ("The prefab's own.");
   - press the Set Up button at the bottom.
6. Save the scene (File > Save) and press Play.

**What success looks like.**
- After step 3, the three files are beside your model, and the Console has a line from `[HumanoidModelImporter]` saying your model is ready and how tall it is.
- In step 5, your model is listed under Model with a picture and its height, not under "Not available".
- In Play mode, the character is your model: it walks, runs, jumps and climbs with the mannequin's animations, its feet on the ground, facing the way it moves.

The model you choose is stored on that scene's Character Spawner (in its Meridian Motion Setup object), not on `Character.prefab`, which keeps the mannequin (the tests spawn it). Set up each scene you want it in. To give the model to a character prefab of its own, follow [A new character of your own](#a-new-character-of-your-own).

**If something's wrong**, see [New characters](troubleshooting.md#new-characters) in Troubleshooting: a model skipped because Unity couldn't map it to a Humanoid, one far too big or small, facing backwards, its feet in or above the floor, or missing from Set Up Scene's list.

The details: [Character models](animation.md#character-models) (what Prepare Humanoid Models does), [Feet on the ground at any size](animation.md#feet-on-the-ground-at-any-size) (how it measures the feet and the facing), [Replacing the model](animation.md#replacing-the-model) (model sources and Body Fit) and [Setting up a scene](spawning.md#setting-up-a-scene).

## A new character of your own

A character of your own is a prefab of its own, so its model, its movement and its body can be changed without changing the default character. **Tools > Meridian Motion > New Character…** makes it:

1. Give it a **Name**: its folder (`Assets/Showcase/Characters/<name>`) and assets are named after it.
2. Choose its **Prepared Model**: any model Prepare Humanoid Models has made ready. Or leave that on "(a model file to prepare)" and drop a rigged humanoid FBX on **Model File**: it's copied into a folder of its own in the output folder's `Models/Humanoid` and prepared on the spot.
3. Choose its **Movement**. **Copy The Default** gives it its own copy of the default character's movement mode to tune (its tuning is its own). **Share The Default** uses the same asset (tuning it tunes both). **Copy The Fox's** starts from the fox's.
4. Optionally, give it an **Animation Set**: clips of its own in some slots (its own idle or run, say), over the shared animation (see [Animation sets](animation.md#animation-sets)).
5. Leave **Set Up The Open Scene** ticked to have the open scene spawn it, and press **Make It**.

It makes its prefab (a variant of `Character.prefab`, so improvements to that reach yours too, wearing your model, its body fitted to it), its movement copy, and, with an animation set, a model source of its own playing the set's controller. Then it opens the **Character Doctor** on it (below). To undo it, delete its folder. From the command line: `-executeMethod MeridianMotion.EditorTools.NewCharacter.MakeFromCommandLine -characterName <name> -model <prepared model's name> -movement CopyTheDefault`.

**Tune it** in its movement mode's inspector: each part of the movement is a component in its list, with its own settings and a toggle to turn it off (Fox Movement turns off what a fox can't do). See [Tuning the Character](tuning.md).

**Check it** with the **Character Doctor** (Tools > Meridian Motion > Character Doctor, or the button on its CharacterModelSlot or its model source): each check of its model, rig, measurements, hitboxes, body, eye point and animation, with a light (pass, warn, fail), what it found and why it matters, and a button for the fix where a tool makes it. A warning about an ability with no animation state of its own means it plays the character's walking for that ability: give it a state in its layout, or turn the ability off on its movement mode.

**By hand,** the same is: a prefab variant of `Character.prefab` (right-click it, Create > Prefab Variant); a duplicate of its movement mode (`Assets/MeridianMotion/Content/Movement/StandardMovementMode.asset`, Edit > Duplicate) set as the variant's MMotionMotor's Movement Mode; and the model's source set as its CharacterModelSlot's Model Source. Its CharacterModelSlot's Body Fit says where its body comes from: **Model's Body** (as Character.prefab has: the body Prepare Humanoid Models fitted to the model, `[name] Body.asset`; untick its Refit On Prepare to shape it by hand), **Height** (its own body, scaled to the model's height) or **Off** (its own body as it is, as the fox has: give it a Body Definition of its own first).

**Put it in another scene:** open the scene and **Tools > Meridian Motion > Set Up Scene…**, press Choose beside Character Prefab and pick it, then Set Up. In a scene already set up, its Meridian Motion Setup > Character Spawner's Character Prefab is it; the spawner's Model Source and Movement Mode, when set, replace the prefab's own, so empty them to use your prefab's.

**What success looks like.** Your prefab is in Set Up Scene's Choose list; in Play mode the character is your model, moving with your tuning; and the default character, in other scenes, is as it was.

**Things to know.**
- Change your own copies, never `Character.prefab`, `StandardMovementMode.asset` or `Character Body.asset`: those are the default character's, and the tests'.
- The walk and run speeds suit the locomotion animations, so feet slide at speeds far from them: retune them together (see [Things to tune together](tuning.md#things-to-tune-together)).
- The swimming animations are floated for the Character prefab's body and its Swim Depth, so a character much taller or shorter, or with another Swim Depth, floats a little high or low in water (see [Floating the swimming clips](animation.md#floating-the-swimming-clips)).

**If something's wrong**, see [New characters](troubleshooting.md#new-characters): a prefab missing from Set Up Scene's list, a body too big or small for the model, a change that also changed the default character.

## A generic rig (not a humanoid)

A generic-rig character (on four legs, two, or more; anything that isn't built and animated as a person: see Humanoid or generic rig?, above) is prepared much as a humanoid is, by Prepare Generic Rig Models, from a folder of its own in `Assets/Showcase/Models/Generic`. You need a model file with a skinned mesh and its skeleton (a Generic rig), and its own animations: a generic-rig character can't play the mannequin's. Only an idle is needed to start; each animation it has is used, and the rest borrow one it has (see [A generic rig's animations](generic-rigs.md#a-generic-rigs-animations)).

1. In the Project window, open `Models/Generic` in [the output folder](getting-started.md#the-output-folder) (`Assets/Showcase/Models/Generic` in this project; make it if it isn't there) and make a folder there for your generic-rig character: right-click, Create > Folder, and name it (`MyWolf`, say). Keep generic-rig characters out of `Models/Humanoid`, which is for humanoids only.
2. Drag your model file (and its textures, and any files holding only its animations) into that folder.
3. Wait for Unity to finish importing. Prepare Generic Rig Models then runs by itself: it imports the model as a Generic rig, makes its **Generic Rig Setup** (`MyWolf Setup.asset`) and an **Animation Set** (`MyWolf Animation Set.asset`), guessing its bones and clips from their names, and saves beside the model:
   - `MyWolf Model.prefab`: the model, ready to animate, with its paw placement, hitboxes and eyes;
   - `MyWolf.asset`: its model source, the asset a character wears;
   - `MyWolf Body.asset`: a body fitted to its mesh (capsules along its back and a sphere for its head), the shape the movement moves;
   - `MyWolf Animator Translator.asset`, and its Animator Controller (beside its animation set).

   If nothing seems to happen, run **Tools > Meridian Motion > Prepare > Generic Rig Models**.
4. **Check its setup.** Select `MyWolf Setup.asset`. At the top of its inspector, a warning lists anything preparing couldn't find (a leg it couldn't tell apart, say). Fill in what's missing, using the bone names you see under the model in the Hierarchy or the Project window: its Root Bone (the hips), each leg's Upper, Lower and Foot bones, and its Eyes Bone (its head). Then check its animation set: select `MyWolf Animation Set.asset` and drop a clip on any slot it guessed wrong or left empty (Idle, Walk, Run, Sprint, Jump Rise, ...). Each change prepares the generic-rig character again by itself. What each setting means: [A generic rig of your own](generic-rigs.md#a-generic-rig-of-your-own).
5. Make it a character: **Tools > Meridian Motion > New Character…**, give it a name, choose `MyWolf (generic rig)` as its Prepared Model, leave Movement as Copy The Fox's (a generic-rig character moves as the fox does, not as a person: what a fox can't do is turned off) and press Make It. Its prefab keeps the body Prepare Generic Rig Models fitted, held above its legs over steps.

   (Or, in step 1, skip making the folder: give New Character the model file, tick Generic Rig (Not Humanoid), and it copies the model into a folder of its own in the output folder's `Models/Generic` and prepares it.)
6. Check it with the Character Doctor (New Character opens it), and press Play in the scene it set up.

**What success looks like.**
- After step 3, the files are beside your model, and the Console has a line from `[Prepare Generic Rig Models]` saying it's prepared, with anything it couldn't find.
- After step 4, the setup's inspector shows no warning.
- In Play mode, the generic-rig character walks and runs with its own animations, its paws on the ground on slopes and stairs.

**If something's wrong**, see [Generic rigs](troubleshooting.md#generic-rigs) in Troubleshooting.

**The fox** is a generic-rig character prepared the same way: its setup (`Assets/Showcase/Models/Generic/Fox/Fox Setup.asset`) and everything Prepare Generic Rig Models made from it are in its folder, to compare yours with. **To play the fox:** open a scene, **Tools > Meridian Motion > Set Up Scene…**, press Choose beside Character Prefab and pick Fox. See [Playing the fox](generic-rigs.md#playing-the-fox).

## Where to go next
- [Spawning the Character](spawning.md): spawn points, NPCs, spawning from code.
- [Tuning the Character](tuning.md): where the settings are, and saving tunings as presets.
- [Character Model and Animations](animation.md): models, animations and IK in full.
- [Generic rigs](generic-rigs.md): generic-rig characters of your own, the fox, and paw placement.
- [Troubleshooting](troubleshooting.md#new-characters): when something's wrong.
