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
8. Click "Play"

## How to see the code in this project
All of the code in this project is in the Assets/Scripts folder.

If you intend to get an idea of my coding style, how I write code, and how I solve problems etc. read through Assets/Scripts/MovementModes/BasicMovementMode

If you intend to get a full understanding of the project, start with ACCharacterController.cs, then go through MovementController.cs, MovementMode.cs, and BasicMovementMode.cs (the latter two are in a subfolder of the main scripts folder) and from there, read through anything you like.

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
- **The camera:** first person follows the eye point. Third person's pivot height, shoulder offset and distance, and the head bob, scale with the camera rig's Target Size Reference, which the Choose Character Model window sets to the character's model parent. Speed effects (the speed-based field of view and head bob strength) judge the target's speed relative to its size, dividing it by its height, so a character at its own top speed gets the same effect at any size. Every other distance and speed the camera uses scales with the target too: follow lag, landing dip and its speed threshold, collision probe and minimum distance, hide distance, shake, teleport distance and the clip planes (near always, far only for a bigger target, so a small one still sees the world).

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
The character is a mannequin generated by a Blender script (Tools/Mannequin), rigged as a Unity Humanoid, with procedurally keyframed animations: idle, walk, run, rise, fall and dash. In the walk and run, each planted foot is placed by IK and moves back at exactly the speed the Animator plays that clip at, rolling from heel to toe, so the feet don't slide or sink into the floor. An Animator blends between them using the character's speed, vertical speed, grounded and dashing state.

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
Character models live in subfolders of Assets/Models/CharacterModels: the generated mannequin and a custom model with three material variants. Any rigged humanoid model can be added the same way. Every model plays the mannequin's animations through Humanoid retargeting. Three tools under Tools > Character Showcase manage them:

1. **Generate Mannequin** runs Tools/Mannequin/generate_mannequin.py in Blender (asking where Blender is the first time) to build the mannequin's mesh, rig and animation clips. It then imports them as looping, in-place Humanoid clips, builds the Animator Controller every model shares (Assets/Animation/Mannequin.controller, rebuilt in place so the models' references to it never break) and its translator, prepares the mannequin like any other model, and makes it the Character prefab's model. If Blender isn't available, it can rebuild everything from the Mannequin.fbx already in the project. To change the mannequin or its animations, edit generate_mannequin.py or mannequin_animations.py and run this again.
2. **Prepare Character Models** imports and sets up every model file in Assets/Models/CharacterModels. It makes each a Humanoid, and straightens its arms and legs into a T-pose for the avatar's reference pose, so models rigged in an A-pose don't hold their arms up in every animation. It saves a model prefab next to each model ("[name] Model.prefab", with its Animator, CharacterIK and FootPlacementIK) and a DirectCharacterModelSource ("[name].asset"). A folder can hold several model files, such as variants of one model; each is its own choice, and its prefab and source are always named after its file, so adding or removing other models never renames them. Models that aren't rigged, or whose bones Unity can't map to a Humanoid, are reported and skipped. Each saved prefab is read back from disk and checked for its avatar and the shared Animator Controller, so a model is only reported ready, and listed in the window, once it can actually animate. Models with lost units (a 180 m or 1.8 cm character) get their source scaled to 1.8 m. IK settings and edits to the model sources are kept. It runs by itself: when model files in the folder are added, changed, moved or deleted, or animation they play changes (an Animator Controller, a clip, or a model source naming a different controller), it prepares whichever are new or changed once the editor is idle (a new model is imported as a Humanoid from the start), and skips the rest; the menu item prepares them all regardless. Prepared assets found under other names (an older naming scheme, or a renamed file) are moved to the current names, keeping their references, and sources whose model file is gone are reported rather than deleted.
3. **Choose Character Model…** opens a window listing the models Prepare Character Models has set up, with previews and heights, and sets which one the sample scene's character uses. The choice is an override on the scene's instance of the Character prefab, so the prefab itself, which the tests spawn, keeps the mannequin.

All three also run from the command line for automation: `-executeMethod CharacterShowcase.EditorTools.MannequinGenerator.GenerateFromCommandLine` (with `-blender <path>` or `-skipBlender`), `...CharacterModelImporter.PrepareAll`, and `...CharacterModelWindow.UseInSceneFromCommandLine -model "X Bot"`.

A Humanoid avatar's reference pose decides how every clip looks on that model, so Prepare Character Models builds it carefully. It starts from the skin's bind pose, the pose the mesh was modelled in, rather than the pose the skeleton is saved in, which can be an animation frame: Unity takes an FBX's default pose from its first take, and the mannequin's used to be the dash, so every mannequin clip was stored relative to a forward lean and other models played them leaning about 45 degrees back. (The mannequin now exports a one-frame rest-pose take first, which isn't imported as a clip.) It then straightens the spine up, the arms out and the legs down. With that, every model holds its torso at the mannequin's lean and keeps its planted feet in place to within a few percent of its speed, with no per-model tuning.

Two more steps keep every model's feet on the ground at any size. Blender's FBX export rotates the armature node -90 degrees about X to convert axes. The Animator poses that node as the avatar stores it and places the body through it, so when the character was stretched along one axis only, the rotation turned the stretch onto another axis: twice the height put the hips four times as high and lifted the feet off the ground. The reference pose is now saved with nodes above the skeleton unrotated (and everything under them where it was), as models without such a node have it. A node that's also unevenly scaled has its scale's axes swapped with the turn, which keeps everything under it exactly as it was for turns of whole quarter turns (any axis conversion); one turned any other way can't be straightened without shearing what's under it, so it's left and reported. (An uneven scale on its own doesn't distort a stretched model, so it isn't reported.) Then the model's soles are measured standing in its idle clip, with its feet where foot placement holds them, and saved as its source's Ground Offset, by which the model is lowered so its soles, not its origin, rest on the ground. Its soles are also measured with its feet where its animation puts them, as its Animated Ground Offset: on a model of other proportions than the clips' own, the two differ by up to a centimetre, and foot placement raises or lowers the body by the difference, in proportion to how far it's faded out (in the air, dashing, or easing in on landing), so the soles rest on the floor whether it's on or off. (While it's partly on, for the eighth of a second it fades, Unity's IK blends the legs' joint rotations, which dips the feet up to a few millimetres.) And the way the body faces as it stands is measured: humanoid animation keeps the facing a model was made with (though it centres the body on the model's origin, however far off it was made), so a model made facing away from +Z gets a Facing Correction, to the nearest quarter turn, that turns it to face forward. Every mesh counts, including ones carried by a bone rather than skinned, such as shoes. Being inside the scaled model parent, the offset scales with the character. Sources made by hand or in code, which haven't been measured, have Measure Ground On Load set, and measure their model the same way when it loads (StandingPose, in the runtime code, does the measuring both here and at import). A source measures its model once, for every copy: the measurement is kept until its model, Animator Controller or Avatar changes, the source is edited, `ForgetMeasurement()` is called, or Play mode starts again. A source whose models may each differ (a character creator's, say) returns null from `ModelTemplate`, so each model is measured as it loads. It's measured again whenever the model, its controller or the clips those play change, since it depends on them: each model's record (in Library/CharacterShowcase/PreparedModels.txt) holds a hash of its file and of its controller with everything the controller depends on.

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

## Automated Tests
Play mode tests in Assets/Tests/PlayMode spawn the real Character prefab with real physics and drive its MovementController with scripted inputs. Together with the camera tests, they cover:
- settling on the ground without hovering, sinking or drifting
- falling within gravity's limits, including walking off a ledge after standing still
- stopping at walls, jumping and landing
- climbing stairs and standing on a ramp without sliding
- riding sliding, rising and spinning platforms, and keeping a platform's momentum when jumping off
- coyote time easing gravity in after leaving the ground, and jumps not being refunded by it
- dashing: distance and direction, ignoring gravity, momentum afterwards, cooldown and input buffer, one dash per press, and air dash limits
- jump and dash cancelling each other, and the dash animation playing
- keeping speed when walking off edges, walking smoothly down stairs, air steering that doesn't stack on carried momentum, and the jump input buffer
- collisions: falling away from a ceiling the character jumps into, losing momentum into a wall, sliding off steep slopes, and being pushed by moving walls
- movement events: each raised once at the right moment, and passed on by the AnimationController
- climbing any step up to the step height at walking and running pace, and not climbing taller ones
- moving like a scaled copy of itself at any size from 0.1 to 100: over a course of ramps up and down, a step, a jump and a dash, every physics step puts it in the same place for its size as at full size, on the ground at the same moments, with the same jump airtime and height for its size
- every model standing with its soles on the floor (within 2 mm, scaled) at 0.1, 1 and 10 times its size and stretched on one axis
- character size at fractional scales (half and quarter): standing on the floor, stopping at walls, climbing steps in proportion and not taller ones, fitting through gaps too low for full size (and not growing back under them), jumping in proportion, and falling off ledges
- refusing negative, zero and non-finite scales, raising tiny ones to the minimum, and reporting a scaled root transform
- character size: scaling only the model parent and resizing collision and mass to match, size changes taking their time, climbing taller steps and stopping further from walls when bigger, waiting for room to grow under a low ceiling, non-uniform scales still moving and colliding, proportional speed scaling (and natural speed and jump scaling, if chosen), the model's measurements and placeholder following, and the inspector field
- planted feet staying in place at quarter, half and double size and when stretched
- model hosting: new models staying hidden until swapped in, the placeholder, falling back when a model fails, fitting a model to a height (in proportion to the character's size), and the eye point following the model's size
- fitting the collider to the model, by height and by shoulder width, and waiting for room to grow
- every model standing on the floor with foot placement off or fading, and an unmeasured source measuring its model on load
- models made facing another way, or off their origin, facing forward and standing on the spot; and a model with an unevenly scaled armature staying in proportion when stretched
- stretched models standing as they do unstretched with foot placement partly on, a knee hint on its own leaving a stretched model's foot where the animation has it, and moving a hand or foot goal moving the hand or foot as far, on every model at even and stretched scales
- every prepared character model: planted feet staying in place while walking, arms hanging down when standing, and the torso leaning as the mannequin's does when standing, walking and running (checks that its avatar's reference pose was set up)
- the model: loading at runtime and being set up, swapping models (including one without an Animator), a model swapped in mid-run or mid-transition carrying on where the old one was, measuring on load once per model, cancelling loads when the character is destroyed or given another model, the editor preview, and the Animator parameter translator
- the camera rig, which follows a plain Transform, independent of the character code:
  - third and first person placement, and mode blending and events
  - pitch limits and zoom limits
  - collision with walls, while ignoring the target's own colliders
  - smooth follow and teleport snapping
  - shake and ShakeAll
  - first person target hiding, including renderers added to the target later
  - first person eyes following an eye anchor
  - third person framing scaled by a target size reference
  - speed FOV judging speed relative to the target's size
  - framing, clip planes, collision and landing dips scaled with the target, at 0.1, 10 and 100 times its size
  - speed FOV and the landing dip
  - CameraZone overrides

Edit mode tests in Assets/Tests/EditMode check what Prepare Character Models leaves behind, without entering Play mode, so a change to preparing (or a model added without preparing it) fails fast:
- every model file has a source, which is ready to use and plays the shared Animator Controller
- every model's avatar reference pose is a T-pose (spine up, arms out, legs down, within 2 degrees) with nothing rotated above its skeleton
- every model's stored Ground Offset, Animated Ground Offset and Facing Correction match a fresh measurement, and its offset is near its origin
- straightening nodes above the skeleton keeping everything under them where it was, unevenly scaled ones included, and leaving what can't be straightened
- facing corrections rounding to the nearest quarter turn
- ground offsets counting meshes carried by bones, not just skinned ones
- what a model was prepared against changing when its controller changes, and staying the same otherwise
- previews and listed heights being sized like the loaded model, Fit To Height included

Run them from Window > General > Test Runner (the EditMode and PlayMode tabs), or from the command line with Unity closed:

```
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults EditModeResults.xml
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults PlayModeResults.xml
```

CI (.github/workflows/tests.yml) runs both.

The character scripts are in the CharacterShowcase assembly (Assets/Scripts), and editor tools are in CharacterShowcase.Editor (Assets/Editor), so the tests can reference them.

## Future Changes:
### Medium Priority Changes
- Tune performance
- Refactor for improved readability in some areas

### Low Priority Changes (these would be really cool but realistically I won't get to them for a while)
- Add Juice and polish
- Coordinate system re-centering to avoid floating point precision related bugs
- Add AI system which uses the same character scripts to pathfind and perform actions
