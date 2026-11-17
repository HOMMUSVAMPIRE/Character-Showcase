# Creatures

Characters don't have to be humanoids. The first creature is a fox: a wolf-sized, low-poly fox with flat-coloured regions (an orange coat, a white chest, muzzle and tail tip, dark legs, ear tips and nose), on four legs, with its own body, movement and animations. It's built the way the mannequin is: generated in Blender by scripts, then set up in Unity by a generator.

## Playing the fox

`Assets/Prefabs/Fox.prefab` is a character like `Character.prefab` (it's a variant of it), with the fox's body, movement mode and model. To play it in a scene, set the scene's character spawner's Character Prefab to it (see [Spawning the character](spawning.md)); the camera follows it as it does the humanoid, from its eyes.

## What the fox is

- **Its body** is a box (`Assets/Models/Creatures/Fox/Fox Body.asset`, a Body Definition): about its body's size, from just behind its nose to its rump, standing on its paws, its middle a little ahead of its feet. Not its ears or its tail. Being longer than it's wide, it turns only as far as it has room to (see [Collision](movement.md#collision)), and being flat-bottomed, it's on a step once it's over its top, however far ahead of its feet that is.
- **Its movement** is `Assets/ScriptableObjects/Fox Movement.asset`: the humanoid's movement mode with what a fox can't do turned off (ledges, vaulting, climbing, ladders, pushing and pulling, ropes, ziplines, wall runs and slides, rolling, dashing and, for now, swimming) and its own tuning: it trots at its Walk Speed and gallops sprinting (Run Speed), jumps once, and crouches (stalks) without sliding. Tune it there.
- **Its model** is `Assets/Models/Creatures/Fox/Fox.fbx`, a Generic rig (not a Unity Humanoid): Hips, Spine1 and Spine2, Neck, Head, Muzzle, the ears, four tail bones, and each leg's bones (FrontUpperLeg, FrontLowerLeg and FrontPaw; HindUpperLeg, HindLowerLeg, HindFoot and HindPaw; each `_L` and `_R`). Its model prefab carries a transform named Eyes on its head, which the character's eye point follows (a model that isn't a humanoid marks its eyes so: see `CharacterModelHost.EyesName`).
- **Its animations** are in place and loop: Idle, Walk, Trot, Gallop, Jump Rise, Jump Fall, Crouch Idle and Crouch Walk. Its Animator Controller (`Fox.controller`) plays Locomotion (Idle, Walk, Trot and Gallop by Speed), Crouch (Crouch Idle and Crouch Walk by Speed, while Crouching) and Airborne (Jump Fall and Jump Rise by VerticalSpeed, while not Grounded), driven by its own Animator parameter translator, as the humanoid's is.

Humanoid IK (foot placement, hands on ledges) is for humanoids only, so the fox has none: its paws are where its animations put them, on slopes and stairs too.

## Generating the fox

Tools > Character Showcase > Generate Fox (or `-executeMethod CharacterShowcase.EditorTools.FoxGenerator.GenerateFromCommandLine`, with `-skipBlender` to rebuild from the Fox.fbx that's there) runs `Tools/Fox/generate_fox.py` in Blender, which builds, rigs, skins and animates the fox (its clips in `Tools/Fox/fox_animations.py`), then imports it and builds its controller, model prefab, translator, model source and Fox.prefab. Its body and movement mode are made only if they aren't there, and left as they're tuned. Run the script by hand with `--preview <folder>` to render it from the side, front and three-quarters, and from the side through each clip.

The gaits are authored for speeds (`SPEEDS` in fox_animations.py) that FoxGenerator's Speed blend thresholds match, so the paws keep to the ground at them; change both together.
