# Character Showcase
This Unity project is intended to showcase my capabilities in the area of creating satisfying character controllers which feel responsive, satisfying, and intuitive. It is also meant to showcase my coding style for possible employers and demonstrate expertise with C# and Unity. Peruse at your own discretion.

This project is still a work in progress. I will be adding to it in my free time as I search for new employment.

Once the project meets my standards for completion, I may release it as a Unity Asset Store package for others to use in their games.

## How to open this Project
1. Clone the repo locally (more details here if needed: https://docs.github.com/en/repositories/creating-and-managing-repositories/cloning-a-repository)
2. Download UnityHub from https://unity.com/download
3. Download and install Unity version 6000.6.7f1 via UnityHub or from https://unity.com/releases/editor/archive
4. Open UnityHub
5. Navigate to Add => Add project from disk
6. Select the location of the cloned repo from step 1
7. Open the project
8. Click "Play" (in Assets/Scenes/SampleScene, the character's demo scene)

The test level, the Meridian Complex, is Assets/Scenes/MeridianComplex/MeridianComplex.unity (see below).

## How to see the code in this project
All of the code in this project is in the Assets/Scripts folder.

If you intend to get an idea of my coding style, how I write code, and how I solve problems etc. read through Assets/Scripts/MovementModes/BasicMovementMode

If you intend to get a full understanding of the project, start with ACCharacterController.cs, then go through MovementController.cs, MovementMode.cs, and BasicMovementMode.cs (the latter two are in a subfolder of the main scripts folder) and from there, read through anything you like.

## Walking and Sprinting
The character moves at its walk speed (3 m/s), or slower with partial stick input, and sprints at its run speed (5.8 m/s) while Sprint is held: Left Shift (keyboard) or pressing the left stick (gamepad). Sprinting works in the air too: steering toward the run speed, and keeping a sprint's momentum through a jump without stacking more on top. Both speeds are in the Speed section of the BasicMovementMode asset; a run speed at or below the walk speed turns sprinting off.

At its walk speed the character walks briskly: Quaternius's walk is made for about 1 m/s, so between it and their jog (5.8 m/s, what sprinting shows) the mannequin's own brisk walk (2.2 m/s) and run (3.8 m/s) fill in. At 3 m/s the brisk walk is played a little faster than it's made for, so its planted feet slide by about an eighth of the speed: a trade for a walk, not a run, at the default speed (people break into a run at about 2 m/s). Sprinting plays Quaternius's jog at its own pace, with its feet planted (see Character Model and Animations).

## Dash
Press F (keyboard) or right trigger (gamepad) to dash: a short burst in the movement direction relative to the camera, or straight ahead without input. A dash moves in a straight line, ignoring gravity and steering, and hands over to momentum when it ends instead of stopping dead. It has a cooldown, a short input buffer (pressing just before the cooldown ends still dashes), one dash per button press, and a limited number of air dashes that reset on landing. Jumping during a dash cancels it straight into the jump, keeping part of the dash's speed. Dashing during a jump cancels the jump's upward momentum, and the jump doesn't resume afterwards. The character plays a dash animation: it slides low in the dash direction with its legs trailing behind. All of it is tunable in the Dashing section of the BasicMovementMode asset, including a speed curve over the dash and how much speed is kept afterwards.

## Collision
MovementController resolves the character's movement against the world itself, with capsule sweeps rather than the physics solver. Each physics step it:
- pushes the character out of anything that moved into them since the last step, so moving walls and platforms push the character instead of passing through
- slides along walls and walkable slopes, and steps up stairs and low ledges, anything up to the step height, at any pace (a step-up carries the character far enough onto the step that it doesn't perch on the edge and slide back off)
- slides the character off surfaces too steep to stand on, including ledge edges caught by the capsule's rounded bottom
- snaps to the ground when walking down slopes and stairs

Momentum that a collision blocks is lost: bumping a ceiling ends the jump, and momentum into a wall doesn't build up and fling the character once they move clear of it. Each step's MovementData records which sides were blocked (`collisions`, as Unity's CollisionFlags), and the next step can read them (`previousCollisions`).

## Character Size
The character can be resized at runtime with `ACCharacterController.SetScale(Vector3)`, or by editing the Scale field in the ACCharacterController's inspector while playing. x is width, y height and z depth, relative to the character as authored; any mix of positive values works, including non-uniform and fractional scales. Negative, zero and non-finite scales are refused with an error (the inspector field reverts), and values under 0.05 are raised to it. Only the model parent is scaled, never the character's own transform; a scaled root (by itself or a parent) is reported as an error, since it would break collision. Everything else follows:
- **Collision:** the capsule's height scales with y and its radius with the larger of x and z (capped at half the height, so a wide, flat character doesn't get a collider taller than itself). Step height, ground snapping, the grounded tolerance, the ground check and the skin width (the small gap kept from surfaces) scale with it, so a tiny character obeys the same collision rules as a full-size one.
- **Room to grow:** a change of size takes a moment (Scale Change Duration). Growing pauses while there isn't room, under a low ceiling say, and carries on once there is; shrinking always happens.
- **Movement:** by default it scales in proportion: speeds, accelerations and gravity scale with height, and timings stay the same, so the character moves exactly like a scaled copy of itself from 0.1 to 100 times its size. It crosses its own height in the same time, and a jump lasts as long and goes as high for its size, so a small character doesn't look zippy and a big one doesn't look floaty. A test runs it over a course of ramps, a step, a jump and a dash at sizes from 0.1 to 100 and checks every physics step lands it in the same place for its size as at full size. The MovementController's Size Scaling setting can instead scale it naturally, as real animals do (speeds with the square root of height, gravity unchanged, so smaller characters look quicker for their size), or keep speeds unchanged.
- **Mass** scales with volume.
- **Animation:** the Animator gets speeds divided by the model's stride scale, so planted feet stay put at any size or stretch. Vertical speed is divided by how speeds scale, so the rising and falling poses fall at the same points of a jump. Foot placement's step limits scale too, and IK places hands and feet right when the model is stretched on one axis. Unity's humanoid IK gets stretched models wrong in two ways, and CharacterIK corrects both. It reports foot goals as if the model were scaled evenly by its width, so a taller-stretched model's feet were placed below the floor, and a shorter one's above it. And it solves each limb from where the limb starts (the hip joint, or the collarbone), moving the hand or foot further from there across and along the model than asked, by the model's width and depth scale over its height scale: a goal 5 cm in front of a hip put the foot 10 cm in front on a model twice as deep, and a hand on a model twice as wide missed by 44 cm. Feet now land within a millimetre of their goals and hands within one centimetre, on every model and stretch tested.
- **Feet on the ground:** every model's soles stay on the floor at any size, including stretched on one axis only. Prepare Character Models measures how far each model's soles stand from its origin and stores it as its source's Ground Offset, and straightens rotated nodes above the skeleton, which used to distort stretched models (see below).
- **The model's measurements**, the eye point and the placeholder all follow, and models loaded while the character is resized are measured at its size.
- **The camera:** first person follows the eye point. Third person's pivot height, shoulder offset and distance, and the head bob, scale with the camera rig's Target Size Reference, which a spawned character hands the camera (its model parent), through its spawner's camera event (see Spawning the Character). Speed effects (the speed-based field of view and head bob strength) judge the target's speed relative to its size, dividing it by its height, so a character at its own top speed gets the same effect at any size. Every other distance and speed the camera uses scales with the target too: follow lag, landing dip and its speed threshold, collision probe and minimum distance, hide distance, shake, teleport distance and the clip planes (near always, far only for a bigger target, so a small one still sees the world).

## Moving Ground
The character rides along with whatever it stands on. Each physics step, before moving, the character is carried by however far its ground moved, and turned with it if it spins. It then moves relative to the ground as normal. Leaving moving ground, by jumping or walking off, keeps the ground's velocity, so the character doesn't stop dead in mid-air. These options can be toggled on the MovementController.

Moving platforms should be kinematic Rigidbodies moved in FixedUpdate with interpolation enabled; the MovingPlatform component does this. Tools > Character Showcase > Create Foot Placement Test Area adds a sliding platform, an elevator and a spinning platform to try it.

## Camera System
The camera lives in Assets/Scripts/CameraSystem, in its own assembly with no dependency on the character code. Add a CameraRig component to a camera, give it a target, and every feature can be tuned or toggled from its inspector.

Features: first person (at a fixed eye height, or following an eye anchor Transform such as a character's eye point) and over-the-shoulder third person (scaled with an optional target size reference, so resized targets are framed alike) with smooth blending between them, zoom (zooming all the way in switches to first person), shoulder swapping (automatic when a wall blocks the current side), aiming, camera collision, follow damping, target interpolation for physics-driven targets, speed-based FOV, head bob, landing dips, trauma-based camera shake, optional auto-recentering, hiding the character up close, and CameraZone volumes that override camera behaviour in an area.

| Action | Keyboard & Mouse | Gamepad |
| --- | --- | --- |
| Look | Mouse | Right stick |
| Zoom | Scroll wheel | D-pad up / down |
| Toggle first / third person | V | Right stick press |
| Swap shoulder | Q | Right bumper |
| Aim | Right mouse button | Left trigger |
| Release cursor | Escape (click to re-lock) | |

## Character Model and Animations
The character is a mannequin generated by a Blender script (Tools/Mannequin), rigged as a Unity Humanoid. It walks, jogs, sprints and jumps with Quaternius's Universal Animation Libraries (see Quaternius animations, below), and stands, walks briskly, runs and dashes with procedurally keyframed animations of its own (Tools/Mannequin/mannequin_animations.py): a still, evenly balanced stance, the gaits between the libraries' walk and jog, and a dash. An Animator blends between them using the character's speed, vertical speed, grounded and dashing state, and a Jump trigger.

The Locomotion blend tree plays, by speed, each loop at the speed its strides are made for, measured on the mannequin, so its planted feet stay put there:

| Speed (m/s) | Animation |
|---|---|
| 0 | the stance (the mannequin's) |
| 0.97 | Quaternius's walk |
| 2.17 | the mannequin's brisk walk, then sped up 1.25 times by 3.2 |
| 3.77 | the mannequin's run |
| 5.79 | Quaternius's jog (sprinting) |
| 8.77 | Quaternius's sprint, then sped up to 10 m/s (the momentum a dash leaves) |

Each loop is offset to land its left foot at the same moment (TouchDownPhase, measured on the mannequin), so blending between them never mixes one foot's stride with the other's.

Jumping plays Quaternius's jump start from its take-off (the Jump trigger, fired by the Jumped and AirJumped movement events), skipping the crouch before it, since the character has already left the ground; then their jump loop while in the air, and their landing as soon as the character is grounded and no longer rising. Moving on (faster than 0.5 m/s) cuts the landing short. Their clips are raised or lowered so their lowest foot stands where the mannequin's stance does (their root height offsets, set by Generate Mannequin), so every clip shares one floor; the landing's crouch still puts the soles within about 4 mm of it, as the libraries' proportions differ a little from the mannequin's.

Without the libraries in the project, Generate Mannequin uses the mannequin's own clips throughout: a walk, the brisk walk, run and sprint (a gait that keeps its planted feet from sliding: each foot's pivot moves straight back at the sprint's speed while down, reached by inverse kinematics), and rising, a tuck at the top and falling, blended by vertical speed.

### Replacing the model
The model and its animations are kept separate from the character's movement, so they can be replaced without touching the character:

- **CharacterModelSource** is a ScriptableObject that gives the character its model: the character's CharacterModelHost calls its `CreateModelAsync(context, cancellationToken)` at runtime, which creates the model under the character, sets it up and hands it back as a `CharacterModel` (the instance, its Animator, IK and translator). It also holds the settings that apply however a model is stored: the translator that animates it, optional Animator Controller and Avatar overrides, placement, and whether to add IK to humanoids. To give the character a new look, assign a different source to the Character prefab's CharacterModelHost (or a scene's instance of it). Its Height setting either keeps the model's own size (times Scale) or fits it to a given height, for the character at its authored size: the character's own size multiplies either, so a model fitted to 1.8 m on a character twice the size is 3.6 m tall. Previews and the Character Model window's heights are sized the same way.
- **DirectCharacterModelSource** (Create > Character Showcase > Character Model (Direct Reference)) is the source that references its model directly: a prefab or model file, instantiated with `Object.InstantiateAsync` so even a heavy model doesn't stall the game. Prepare Character Models makes one for every model it sets up.
- **Other sources** subclass CharacterModelSource. One that loads from Addressables or an asset bundle overrides `CreateModelAsync` to load and instantiate the model, then calls `SetUpModelAsync` to apply the shared settings, and overrides `ReleaseModel` to release the instance (`Addressables.ReleaseInstance`) rather than destroy it. `SetUpModelAsync` is virtual and awaitable too, for applying a character creator's choices. If the cancellation token fires (the character was destroyed or given another model first), a source cleans up whatever it has loaded and throws `OperationCanceledException`; awaiting `Awaitable.NextFrameAsync(cancellationToken)` and similar does that.
- **CharacterModelHost** owns the character's model. It loads it at runtime, as the character's first step, so it never stalls the game, and the character moves normally while it loads. `await CharacterModelHost.LoadModelAsync(source)` swaps models at runtime, returning whether the new model loaded. New models are created hidden, under an inactive staging transform, and swapped in the same frame the old one goes, so two models are never seen at once; the old one goes back to the source that made it with `ReleaseModel`. While there's no model it shows a placeholder (a capsule the size of the collider), and if a model fails to load it loads its fallback source (the mannequin) instead. It keeps an Eye Point child at the model's eye height (from its eye bones, or partway up its head), which the camera's first-person view follows, and warns if a model is taller than the character's collider or much shorter. Its Collider Fit setting can instead resize the collider to each model as it's swapped in: Height makes it as tall as the model, keeping its proportions, and Height And Width also makes it as wide as the model's shoulders (times a margin for the arms). The character's size still multiplies the fitted collider, and like growing, fitting a bigger collider waits until there's room. Off by default, so the collider stays as tuned. AnimationController hears about new models through its `ModelLoaded` event and animates them; it no longer handles models itself. A model swapped in mid-move carries on where the old one was, rather than starting over from standing. Just before the swap, `ModelReplacing` lets the old model's animation driver record its state (`Retire`), and the new model's driver takes it up (`ContinueFrom`). For an Animator, that's its parameters, and each layer's weight, state, how far through it, and any transition it was part-way through, matched by name, so variants of one controller carry over. Foot placement carries over too, so planted feet stay planted rather than fading back in. A translator for another animation system can do the same by implementing those two methods; by default they do nothing.
- **CharacterAnimationState** is the layer between movement and animation: velocity (world and relative to facing), speeds, turn rate, grounded (with a short grace period for small step-offs), time in the air, moving, jumping and dashing. Animation code reads this rather than the movement system's data. Components on a model, like FootPlacementIK, find it through `ICharacterAnimationStateSource` in their parents, so they work on any character.
- **Movement events** are raised by `MovementController.MovementEventRaised` after each physics step: Jumped, AirJumped, Landed, LeftGround, DashStarted, DashEnded, HitCeiling and HitWall. Movement modes report the ones they cause (jumps and dashes), and the controller detects the rest. `AnimationController.MovementEventReceived` passes them on with the animation state, for effects and sound.
- **CharacterAnimationTranslator** (a ScriptableObject) turns the animation state and events into a particular model's animations. The included **AnimatorParameterTranslator** sets Animator parameters you name (speed, vertical, forward and sideways speed, turn speed, grounded, jumping, dashing, moving) and fires triggers on chosen movement events, skipping any the controller doesn't have. A different animation system can be supported by writing another translator.

In the editor, where the model isn't loaded, the CharacterModelHost draws it as a gizmo so the character can still be seen and placed.

### Character models
Character models live in subfolders of Assets/Models/CharacterModels: the generated mannequin, Quaternius's female mannequin (CC0, from the Universal Animation Library), and a custom model with three material variants. Any rigged humanoid model can be added the same way. Every model plays the mannequin's animations through Humanoid retargeting. Three tools under Tools > Character Showcase manage them:

1. **Generate Mannequin** runs Tools/Mannequin/generate_mannequin.py in Blender (asking where Blender is the first time) to build the mannequin's mesh, rig and animation clips. It then imports them as looping, in-place Humanoid clips, builds the Animator Controller every model shares (Assets/Animation/Mannequin.controller, rebuilt in place so the models' references to it never break) and its translator, prepares the mannequin like any other model, and makes it the Character prefab's model. If Blender isn't available, it can rebuild everything from the Mannequin.fbx already in the project. To change the mannequin or its animations, edit generate_mannequin.py or mannequin_animations.py and run this again.
2. **Prepare Character Models** imports and sets up every model file in Assets/Models/CharacterModels. It makes each a Humanoid, and straightens its arms and legs into a T-pose for the avatar's reference pose, so models rigged in an A-pose don't hold their arms up in every animation. It saves a model prefab next to each model ("[name] Model.prefab", with its Animator, CharacterIK and FootPlacementIK) and a DirectCharacterModelSource ("[name].asset"). A folder can hold several model files, such as variants of one model; each is its own choice, and its prefab and source are always named after its file, so adding or removing other models never renames them. Models that aren't rigged, or whose bones Unity can't map to a Humanoid, are reported and skipped. Each saved prefab is read back from disk and checked for its avatar and the shared Animator Controller, so a model is only reported ready, and listed in the window, once it can actually animate. Models with lost units (a 180 m or 1.8 cm character) get their source scaled to 1.8 m. IK settings and edits to the model sources are kept. It runs by itself: when model files in the folder are added, changed, moved or deleted, or animation they play changes (an Animator Controller, a clip, or a model source naming a different controller), it prepares whichever are new or changed once the editor is idle (a new model is imported as a Humanoid from the start), and skips the rest; the menu item prepares them all regardless. Prepared assets found under other names (an older naming scheme, or a renamed file) are moved to the current names, keeping their references, and sources whose model file is gone are reported rather than deleted.
3. **Set Up Scene…** opens a window for choosing which character, model (any Prepare Character Models has set up, with previews and heights) and movement mode a scene spawns, and sets the open scene up to spawn it. See Spawning the Character. The choice is stored on the scene's Character Spawner, so the Character prefab itself, which the tests spawn, keeps the mannequin.

All three also run from the command line for automation: `-executeMethod CharacterShowcase.EditorTools.MannequinGenerator.GenerateFromCommandLine` (with `-blender <path>` or `-skipBlender`), `...CharacterModelImporter.PrepareAll`, and `...SceneGameSetup.SetUpFromCommandLine -scene Assets/Scenes/SampleScene.unity -model "base_basic_shaded"` (see Spawning the Character).

A Humanoid avatar's reference pose decides how every clip looks on that model, so Prepare Character Models builds it carefully. It starts from the skin's bind pose, the pose the mesh was modelled in, rather than the pose the skeleton is saved in, which can be an animation frame: Unity takes an FBX's default pose from its first take, and the mannequin's used to be the dash, so every mannequin clip was stored relative to a forward lean and other models played them leaning about 45 degrees back. (The mannequin now exports a one-frame rest-pose take first, which isn't imported as a clip.) It then straightens the spine up, the arms out and the legs down. With that, every model holds its torso at the mannequin's lean and keeps its planted feet in place to within a few percent of its speed, with no per-model tuning.

Two more steps keep every model's feet on the ground at any size. Blender's FBX export rotates the armature node -90 degrees about X to convert axes. The Animator poses that node as the avatar stores it and places the body through it, so when the character was stretched along one axis only, the rotation turned the stretch onto another axis: twice the height put the hips four times as high and lifted the feet off the ground. The reference pose is now saved with nodes above the skeleton unrotated (and everything under them where it was), as models without such a node have it. A node that's also unevenly scaled has its scale's axes swapped with the turn, which keeps everything under it exactly as it was for turns of whole quarter turns (any axis conversion); one turned any other way can't be straightened without shearing what's under it, so it's left and reported. (An uneven scale on its own doesn't distort a stretched model, so it isn't reported.) Then the model's soles are measured standing in its idle clip, with its feet where foot placement holds them, and saved as its source's Ground Offset, by which the model is lowered so its soles, not its origin, rest on the ground. Its soles are also measured with its feet where its animation puts them, as its Animated Ground Offset: on a model of other proportions than the clips' own, the two differ by up to a centimetre, and foot placement raises or lowers the body by the difference, in proportion to how far it's faded out (in the air, dashing, or easing in on landing), so the soles rest on the floor whether it's on or off. (While it's partly on, for the eighth of a second it fades, Unity's IK blends the legs' joint rotations, which dips the feet up to a few millimetres.) And the way the body faces as it stands is measured: humanoid animation keeps the facing a model was made with (though it centres the body on the model's origin, however far off it was made), so a model made facing away from +Z gets a Facing Correction, to the nearest quarter turn, that turns it to face forward. Every mesh counts, including ones carried by a bone rather than skinned, such as shoes. Being inside the scaled model parent, the offset scales with the character. Sources made by hand or in code, which haven't been measured, have Measure Ground On Load set, and measure their model the same way when it loads (StandingPose, in the runtime code, does the measuring both here and at import). A source measures its model once, for every copy: the measurement is kept until its model, Animator Controller or Avatar changes, the source is edited, `ForgetMeasurement()` is called, or Play mode starts again. A source whose models may each differ (a character creator's, say) returns null from `ModelTemplate`, so each model is measured as it loads. It's measured again whenever the model, its controller or the clips those play change, since it depends on them: each model's record (in Library/CharacterShowcase/PreparedModels.txt) holds a hash of its file and of its controller with everything the controller depends on.

### Quaternius animations
Assets/Animation/Quaternius holds Quaternius's Universal Animation Library and Universal Animation Library 2 (the free Standard editions, from quaternius.com), 86 animations on one humanoid rig, CC0 (public domain; see LICENSE.txt there). Besides the walk, jog, sprint and jump the character uses, they have crouching, rolling, sliding, climbing up, swimming, carrying, combat, interacting and more, ready for mechanics to come; the README there lists them. Tools > Character Showcase > Prepare Quaternius Animations makes each a looping or one-shot Humanoid clip named for its take (Generate Mannequin runs it). Their female mannequin (CC0 too) is in Assets/Models/CharacterModels/Quaternius as a character model of its own. The FBX files are stored with Git LFS, so run `git lfs install` once before cloning.

### Motion capture clips
Assets/Animation/CMU holds 145 motion-capture clips from the CMU Graphics Lab Motion Capture Database (http://mocap.cs.cmu.edu), free to use, modify and redistribute but not to sell as animations (see LICENSE.txt there). The character no longer plays them (the Quaternius animations replaced its walk, run and jump), but they're kept for mechanics to come. They cover walking, running, jumping, stairs and steps, crouching, sneaking and crawling, sitting and getting up, climbing, carrying, pushing and pulling, picking things up, falls and idles, sorted into folders and named for what they show; CLIPS.md lists each with CMU's description. They're stored with Git LFS too.

They've been edited for a character controller, in Blender, by the scripts in Tools/Animation. remove_root_motion.py takes the hips' travel across the ground out of each clip, keeping their height (jumps still rise) and the sway within each step. tpose_rest.py makes the T-pose each clip starts with its skeleton's rest pose. Tools > Character Showcase > Prepare Motion Capture Clips then makes each a Humanoid animation with its own avatar (each performer had their own proportions), mapped by the CMU skeleton's bone names, starting after the T-pose frame, its height based on the feet (so its floor is the model's own), and facing the way the body does, so every clip faces the character's forward whichever way it was captured. MotionCaptureLocomotion can cut loops from them: it searches a take's steady middle for the two frames a cycle apart whose poses, and how they're moving, match best. It also measures any loop's stride speed, the moment it lands its left foot, a jump's take-off and landing, and a clip's lowest foot, which Generate Mannequin uses on every clip.

Two things turned up fitting real motion capture to foot placement. On a model stretched along one axis, Unity reports foot goals scaled about the clip's root, so a clip whose height was kept as captured (the performer's floor, a little off the model's) had its feet planted centimetres too low on a stretched model; based on the feet, the clip's floor is the model's, and they're on the ground. And Unity's Foot IK option on a state, which would pin each model's feet to the clip's, has the same stretch problem, so it's left off.

### IK
CharacterIK (on each model, reachable via CharacterModelHost.IK) layers inverse kinematics over the animations. Hands and feet can be pulled to target positions and rotations, and elbows and knees bent toward hint positions. Each has its own 0-1 weight that fades smoothly in and out, blending with the animation underneath. Targets can be Transforms assigned in the inspector or positions set from code:

```csharp
ik.SetGoal(IKLimb.RightHand, doorHandle.position, doorHandle.rotation); // reach for a handle
ik.SetHint(IKLimb.LeftFoot, kneeTarget.position, 0.5f);                 // push the left knee outward
ik.ClearGoal(IKLimb.RightHand);                                         // fade back to the animation
```

FootPlacementIK uses the same system to plant the feet on slopes, stairs and bumps automatically: it raycasts under each animated foot, shifts the foot to the real ground while keeping the animation's foot lift, tilts planted feet to the slope, and lowers the pelvis so the lower foot can reach. It feeds a per-frame procedural channel underneath the manual goals, so targets set from code still take priority. To try it, run Tools > Character Showcase > Create Foot Placement Test Area to add stairs, ramps and bumps in front of the character.

On a model of other proportions than the clips were made for, Unity reports each hand's and foot's goal where the clip's own character had it, which can be a few centimetres from where this model has it (up to 7 cm for the CustomModel's hands). So a goal set to where the animation reports it moves the hand or foot there, and a goal at partial weight is blended from there. A goal at full weight lands where it's set.

To try manual targets in the editor, right-click the CharacterIK component and choose "Create Target Transforms For All Limbs", then enter Play mode, raise a weight, and drag the target around in the Scene view.

## Test Level: The Meridian Complex
The Meridian Complex is a large graybox level for putting the character through its paces: about 400 × 400 m of terraces, stairs, ramps, bridges, towers, tunnels and measured test courses, in seven connected areas. It's built from blockout diagrams (the MeridianComplex-*.png sheets at the repository's root). The character isn't placed in the scene: the level marks where it can start (four spawn markers on the Concourse plaza), and Set Up Scene makes it spawn at one of them when you press Play (see Spawning the Character, below).

| Area | Where | What it's for |
| --- | --- | --- |
| 01 Concourse | Centre | The hub: a plaza, a terrace ring at +8, upper terraces up to +20, and bridges out to the areas around it |
| 02 Kinetic Gardens | North-west | Open-air traversal: a beginner route of stairs, ramps and short gaps, and an advanced one of longer gaps, a beam and a ledge chain |
| 03 Ascension Spire | North | Vertical traversal: a central core at +20 and a summit tower climbed by a switchback stair to +56 |
| 04 Fabrication Hall | East | An industrial building: rooms on floors from -8 to +8, catwalks at +12, crates and shelves |
| 05 Suspension Yard | South-east | Aerial: tower platforms at +8, +16 and +24, suspended bridges, and jump and dash gaps |
| 06 Service Network | Underground | Corridors and tunnels at -12, lit, joining the gardens, the spire and the hall |
| 07 Calibration Annex | South-west | Measured tests: step heights either side of the step limit, slopes from 10° to 60°, jump gaps from 2 to 20 m, tunnels with falling ceilings, a jump lane, wall-run and ledge-climb faces |

Every area can be reached on foot, by stairs and ramps that keep to the character's step height and slope limit; the pieces that need a jump, a climb or a crouch are marked as such. Mechanics the character doesn't have yet (ladders, ropes, wall runs, climbable walls, pushable crates) have placeholder geometry and markers named "(future)", under Future Mechanics in each area. Each area's notes (in Assets/Scenes/MeridianComplex) say how its diagram was read and what was chosen where it was ambiguous.

The whole scene is generated: Tools > Character Showcase > Level > Build Meridian Complex rebuilds it (also `-executeMethod CharacterShowcase.EditorTools.Level.MeridianComplexBuilder.BuildFromCommandLine`), replacing it and its environment assets. From the menu it builds in the background, one area at a time, and bakes the lighting in the background too, with a window showing its progress (also under the editor's Background Tasks), so the editor keeps responding. The whole build takes a few seconds. The level's pieces aren't marked Contribute GI (it has no baked or realtime GI to use it), which used to make the bake pack lightmaps for every piece, taking about seven minutes. Until the environment is replaced, Cancel (or closing the window) leaves the level as it was. After that, closing the window lets the build finish on its own, and cancelling the bake saves the level without its baked environment lighting. Rebuilding replaces the scene, so change the builders (Assets/Editor/Level, one per area) rather than the scene. All of it uses one placeholder material, a smooth matte cream concrete (Assets/Scenes/MeridianComplex/Materials), which is made once and then kept, so it can be edited by hand. Collision is all box colliders: the angled solids (octagonal platforms, terraces cut at 45°) are filled exactly by several boxes each, rather than with mesh colliders.

It's lit as a sunny midday for URP: a high sun with soft shadows, a procedural sky that lights the ambient and reflections, light haze, and a global post-processing volume (ACES tonemapping, light bloom, a little contrast). The PC render pipeline asset's shadow distance is 150 m, up from 50, so the terraces' shadows reach across the hub.

## Spawning the Character
A scene doesn't need the character placed in it: a **CharacterSpawner** (Add Component > CharacterSpawner) spawns it. It takes a character prefab (and optionally a model and a movement mode asset to give the spawned character instead of the prefab's own), a list of spawn points (a world position for the character's feet and a rotation for the way it faces), and a Spawn On Start checkbox. With Spawn On Start ticked, it spawns the character when the scene starts, at one of its spawn points chosen at random; untick it to spawn from code with `Spawn()` (a random point) or `SpawnAt(index)`. Each call spawns another character. The spawn points are drawn in the Scene view as capsules, with an arrow for the way each one faces. The spawner tells whatever needs the new character about it through its On Character Spawned event, which can be wired up in the inspector, and the `CharacterSpawned` C# event. A camera gets the character from its On Camera Target Spawned event, with the character's eye point and size reference (its model parent, whose scale is its size), for the camera rig's `Follow(target, eyeAnchor, sizeReference)`: the camera snaps to it, looks the way it faces, as it does at the start, puts the first-person view at its eyes and frames it at its size. (`Follow(Transform)` takes just a target.) The spawner's Start runs before other scripts' Start, so a camera's Start already sees the spawned character.

**Tools > Character Showcase > Set Up Scene…** sets up the open scene to spawn a character. In its window, choose:
- the character prefab
- its model: the prefab's own, or any model Prepare Character Models has set up, shown with a preview and its height
- its movement mode asset

The choices start from the scene: its existing setup's, or else its placed character's (the prefab it's an instance of, its model and movement mode), or else the package's Character prefab as it is. Then press Set Up. The scene gets a Game Setup object with a Character Spawner that spawns the character on Start and hands it to a camera. The camera is the scene's own camera rig if it has one, or else a copy of the demo scene's Main Camera (so it's set up and tuned the same). The input manager is likewise the scene's own, or a copy of the demo's. Only the model and movement mode that differ from the prefab's are stored on the spawner, so later changes to the prefab carry through.

Where the character spawns, in order of preference:
1. The scene's existing setup's spawn points: setting up a scene again replaces its setup but keeps them.
2. The level's spawn markers (objects named "Spawn Point …", facing their forward) and where the scene's placed characters stand.
3. The ground below the middle of the Scene view, facing the way the view looks.

Move or add spawn points on the Character Spawner afterwards. Characters placed in the scene are replaced by spawn points where they stood, unless you untick that (they're then left alongside the spawned one). The scene's other cameras are turned off (their objects deactivated, not deleted), unless you untick that too. The change can be undone and isn't saved for you. To change the demo scene's model, open it and set it up with the model you want: its placed character, if it still has one, becomes a spawn point. The window's buttons set up the model files (Prepare Character Models) and the animation files (the Quaternius libraries and motion-capture clips), and Refresh lists newly prepared models.

The Meridian Complex's builder places its spawn markers, so after rebuilding the level (which replaces its scene, setup included), open it and press Set Up to spawn there again. From the command line, with Unity closed, `-executeMethod CharacterShowcase.EditorTools.SceneGameSetup.SetUpFromCommandLine -scene <scene path>` sets a scene up and saves it, optionally with `-prefab <path>`, `-model <a prepared model's name or source path>` and `-movementMode <asset path>`. Run after building the level, it sets that up too.

## Automated Tests
The tests are written to hold for any team's setup, not just this project's: they read what they need from the character (its capsule, step height, slope limit, walking and dashing speeds, jump count) and from whatever model, animations and camera settings it has, and size their test worlds from those. Play mode tests run on the package's Character prefab unless a CharacterTestSettings asset (Create > Character Showcase > Tests > Character Test Settings) names another: put one anywhere in the project to run the whole suite on your own character. A test of something the character doesn't have (an air jump on a character with one jump, foot placement on a model that isn't a humanoid) is skipped, with the reason, rather than failed. They've been run on a character tuned differently (a smaller capsule, a lower step height and slope limit, half the walking speed, a slower dash and a single jump).

Tests of the package's own content (the mannequin and other prepared models, the motion-capture clips, the shared Animator Controller) are in the SampleContent category, and skipped where that content isn't there. A team using its own models and animations can leave them out: in the Test Runner, or with `-testCategory "!SampleContent"` on the command line.

Play mode tests in Assets/Tests/PlayMode spawn the character with real physics and drive its MovementController with scripted inputs. Together with the camera tests, they cover:
- settling on the ground without hovering, sinking or drifting
- falling within gravity's limits, including walking off a ledge after standing still
- stopping at walls, jumping and landing
- climbing stairs and standing on a ramp without sliding (each a little over half the character's step height and slope limit)
- climbing any step up to the step height at walking and running pace, and not climbing taller ones
- riding sliding, rising and spinning platforms, and keeping a platform's momentum when jumping off
- coyote time easing gravity in after leaving the ground, and jumps not being refunded by it
- sprinting at the run speed and walking again once Sprint is let go, Sprint alone not moving the character, and a sprint's speed carried through a jump without stacking
- dashing: distance and direction, ignoring gravity, momentum afterwards, cooldown and input buffer, one dash per press, and air dash limits
- jump and dash cancelling each other
- keeping speed when walking off edges, walking smoothly down stairs, air steering that doesn't stack on carried momentum, and the jump input buffer
- collisions: falling away from a ceiling the character jumps into, losing momentum into a wall, sliding off slopes too steep for it, and being pushed by moving walls
- movement events: each raised once at the right moment, and passed on by the AnimationController
- moving like a scaled copy of itself at any size from 0.1 to 100: over a course laid out for the character (ramps up and down, a step, a jump, a dash, a low ceiling and a step too tall for it), every physics step puts it in the same place for its size as at full size, on the ground at the same moments, with the same jump airtime and height for its size
- character size: scaling only the model parent and resizing collision and mass to match, size changes taking their time, waiting for room to grow under a low ceiling, non-uniform scales still moving and colliding, natural speed scaling, the model's measurements and placeholder following, and the inspector field
- refusing negative, zero and non-finite scales, raising tiny ones to the minimum, and reporting a scaled root transform
- the character's model standing with its soles on the floor (within 2 mm, scaled) at 0.1, 1 and 10 times its size and stretched on one axis, with foot placement on, off or fading; and an unmeasured source measuring its model on load
- its planted feet sliding no more at other sizes, or stretched, than at its own size
- IK on stretched models: moving a hand or foot goal moving the hand or foot as far, and a knee hint on its own moving the foot no more than unstretched
- models made facing another way, or off their origin, facing forward and standing on the spot; and a model with an unevenly scaled armature staying in proportion when stretched
- model hosting: loading at runtime and being set up, swapping models (including one without an Animator), new models staying hidden until swapped in, the placeholder, falling back when a model fails, cancelling loads when the character is destroyed or given another model, sources releasing their models, a model swapped in mid-run or mid-transition carrying on where the old one was, measuring on load once per model, and the editor preview
- fitting a model to a height (in proportion to the character's size, the eyes following), fitting the collider to the model by height or shoulder width, and waiting for room to grow
- the Animator parameter translator
- the camera rig, which follows a plain Transform, independent of the character code, with expectations taken from its own settings:
  - third and first person placement, and mode blending and events
  - pitch limits and zoom limits
  - collision with walls (only as far in as needed, ignoring walls clear of it and the target's own colliders), at 0.1, 1 and 10 times the target's size
  - smooth follow and teleport snapping
  - shake and ShakeAll
  - first person target hiding, including renderers added to the target later
  - first person eyes following an eye anchor
  - framing and clip planes scaled with the target, at 0.1, 2, 10 and 100 times its size
  - speed FOV judging speed relative to the target's size, and settling once it stops
  - the landing dip, in proportion to the target's size, and recovering
  - CameraZone overrides
  - following a new target with `Follow`: snapping to it and looking the way it faces, and taking its eye anchor and size reference in place of the old target's
- the character spawner: spawning at a spawn point and facing its way, at a chosen point or one chosen at random from all of them, on Start or only when asked, telling listeners through both events, treating an all-zero rotation as none, giving the spawned character the chosen model and movement mode (and keeping the prefab's without them), giving the camera its eye point and size reference, and refusing with an error when there's no prefab, no spawn points or no such point
- sample content: every prepared model's planted feet staying in place walking at the walk loop's speed, standing on the floor, arms hanging down when standing, and the torso leaning as the mannequin's does when standing, walking and running (checks that its avatar's reference pose was set up)

Edit mode tests in Assets/Tests/EditMode check Prepare Character Models and the motion-capture clips without entering Play mode, so a change to preparing (or a model added without preparing it) fails fast:
- every model file has a source
- straightening nodes above the skeleton keeping everything under them where it was, unevenly scaled ones included, and leaving what can't be straightened
- facing corrections rounding to the nearest quarter turn
- ground offsets counting meshes carried by bones, not just skinned ones
- sample content: setting up a scene: replacing a placed character with a spawn point where it stood (or keeping it, when asked), spawning at the scene's spawn markers, keeping its spawn points and replacing its old setup when set up again, wiring the demo scene's camera (or the scene's own camera rig) to the spawner, copying the input manager once, and turning other cameras off rather than deleting them
- sample content: every prepared model's source is ready to use and plays the shared Animator Controller; its avatar reference pose is a T-pose (spine up, arms out, legs down, within 2 degrees) with nothing rotated above its skeleton; its stored Ground Offset, Animated Ground Offset and Facing Correction match a fresh measurement; what it was prepared against changes when its controller changes; and previews and listed heights are sized like the loaded model
- sample content: the shared Animator Controller's Locomotion blend tree lines up every loop to land its left foot at the same moment, and each loop shown at its own strides' speed (the walks, the jog, and the sprint sped up to the top) keeps its feet in step with the ground there
- sample content: every motion-capture clip is a Humanoid animation with a valid avatar, mapped by the CMU bone names and starting after its T-pose frame, and played on the mannequin, every clip that moves about on its feet stays on the spot (its hips within 32 cm, 9 cm typically) and faces forward (within 6 degrees); each check lists every clip that fails it

Run them from Window > General > Test Runner (the EditMode and PlayMode tabs), or from the command line with Unity closed:

```
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults EditModeResults.xml
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults PlayModeResults.xml
```

CI (.github/workflows/tests.yml) runs both.

The character scripts are in the CharacterShowcase assembly (Assets/Scripts), and editor tools are in CharacterShowcase.Editor (Assets/Editor), so the tests can reference them. The level builders are in their own assembly, CharacterShowcase.Level.Editor (Assets/Editor/Level), which doesn't reference the character or camera code.

## Future Changes:
### Medium Priority Changes
- Tune performance
- Refactor for improved readability in some areas

### Low Priority Changes (these would be really cool but realistically I won't get to them for a while)
- Add Juice and polish
- Coordinate system re-centering to avoid floating point precision related bugs
- Add AI system which uses the same character scripts to pathfind and perform actions
