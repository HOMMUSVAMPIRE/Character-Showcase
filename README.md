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
- slides along walls and walkable slopes, and steps up stairs and low ledges
- slides the character off surfaces too steep to stand on, including ledge edges caught by the capsule's rounded bottom
- snaps to the ground when walking down slopes and stairs

Momentum that a collision blocks is lost: bumping a ceiling ends the jump, and momentum into a wall doesn't build up and fling the character once they move clear of it. Each step's MovementData records which sides were blocked (`collisions`, as Unity's CollisionFlags), and the next step can read them (`previousCollisions`).

## Moving Ground
The character rides along with whatever it stands on. Each physics step, before moving, the character is carried by however far its ground moved, and turned with it if it spins. It then moves relative to the ground as normal. Leaving moving ground, by jumping or walking off, keeps the ground's velocity, so the character doesn't stop dead in mid-air. These options can be toggled on the MovementController.

Moving platforms should be kinematic Rigidbodies moved in FixedUpdate with interpolation enabled; the MovingPlatform component does this. Tools > Character Showcase > Create Foot Placement Test Area adds a sliding platform, an elevator and a spinning platform to try it.

## Camera System
The camera lives in Assets/Scripts/CameraSystem, in its own assembly with no dependency on the character code. Add a CameraRig component to a camera, give it a target, and every feature can be tuned or toggled from its inspector.

Features: first person and over-the-shoulder third person with smooth blending between them, zoom (zooming all the way in switches to first person), shoulder swapping (automatic when a wall blocks the current side), aiming, camera collision, follow damping, target interpolation for physics-driven targets, speed-based FOV, head bob, landing dips, trauma-based camera shake, optional auto-recentering, hiding the character up close, and CameraZone volumes that override camera behaviour in an area.

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

- **CharacterModelSource** is a ScriptableObject that gives the character its model: the AnimationController calls its `CreateModelAsync(context, cancellationToken)` at runtime, which creates the model under the character, sets it up and hands it back as a `CharacterModel` (the instance, its Animator, IK and translator). It also holds the settings that apply however a model is stored: the translator that animates it, optional Animator Controller and Avatar overrides, placement, and whether to add IK to humanoids. To give the character a new look, assign a different source to the Character prefab's AnimationController (or a scene's instance of it).
- **DirectCharacterModelSource** (Create > Character Showcase > Character Model (Direct Reference)) is the source that references its model directly: a prefab or model file, instantiated with `Object.InstantiateAsync` so even a heavy model doesn't stall the game. Prepare Character Models makes one for every model it sets up.
- **Other sources** subclass CharacterModelSource. One that loads from Addressables or an asset bundle overrides `CreateModelAsync` to load and instantiate the model, then calls `SetUpModelAsync` to apply the shared settings, and overrides `ReleaseModel` to release the instance (`Addressables.ReleaseInstance`) rather than destroy it. `SetUpModelAsync` is virtual and awaitable too, for applying a character creator's choices. If the cancellation token fires (the character was destroyed or given another model first), a source cleans up whatever it has loaded and throws `OperationCanceledException`; awaiting `Awaitable.NextFrameAsync(cancellationToken)` and similar does that.
- **The model is loaded at runtime**, as the character's first step, so it never stalls the game, and the character moves normally while it loads. `await AnimationController.LoadModelAsync(source)` swaps models at runtime, returning whether the new model loaded; the old model stays until the new one is ready, and goes back to the source that made it with `ReleaseModel`.
- **CharacterAnimationState** is the layer between movement and animation: velocity (world and relative to facing), speeds, turn rate, grounded (with a short grace period for small step-offs), time in the air, moving, jumping and dashing. Animation code reads this rather than the movement system's data. Components on a model, like FootPlacementIK, find it through `ICharacterAnimationStateSource` in their parents, so they work on any character.
- **Movement events** are raised by `MovementController.MovementEventRaised` after each physics step: Jumped, AirJumped, Landed, LeftGround, DashStarted, DashEnded, HitCeiling and HitWall. Movement modes report the ones they cause (jumps and dashes), and the controller detects the rest. `AnimationController.MovementEventReceived` passes them on with the animation state, for effects and sound.
- **CharacterAnimationTranslator** (a ScriptableObject) turns the animation state and events into a particular model's animations. The included **AnimatorParameterTranslator** sets Animator parameters you name (speed, vertical, forward and sideways speed, turn speed, grounded, jumping, dashing, moving) and fires triggers on chosen movement events, skipping any the controller doesn't have. A different animation system can be supported by writing another translator.

In the editor, where the model isn't loaded, the AnimationController draws it as a gizmo so the character can still be seen and placed.

### Character models
Character models live in subfolders of Assets/Models/CharacterModels: the generated mannequin, the X Bot from Mixamo, and a custom model with three material variants. Any rigged humanoid model can be added the same way. Every model plays the mannequin's animations through Humanoid retargeting. Three tools under Tools > Character Showcase manage them:

1. **Generate Mannequin** runs Tools/Mannequin/generate_mannequin.py in Blender (asking where Blender is the first time) to build the mannequin's mesh, rig and animation clips. It then imports them as looping, in-place Humanoid clips, builds the Animator Controller every model shares (Assets/Animation/Mannequin.controller, rebuilt in place so the models' references to it never break) and its translator, prepares the mannequin like any other model, and makes it the Character prefab's model. If Blender isn't available, it can rebuild everything from the Mannequin.fbx already in the project. To change the mannequin or its animations, edit generate_mannequin.py or mannequin_animations.py and run this again.
2. **Prepare Character Models** imports and sets up every model file in Assets/Models/CharacterModels. It makes each a Humanoid, and straightens its arms and legs into a T-pose for the avatar's reference pose, so models rigged in an A-pose don't hold their arms up in every animation. It saves a model prefab next to each model ("[name] Model.prefab", with its Animator, CharacterIK and FootPlacementIK) and a DirectCharacterModelSource ("[name].asset"). A folder holding several model files, such as variants of one model, gives one choice per file, named "Folder (file)". Models that aren't rigged, or whose bones Unity can't map to a Humanoid, are reported and skipped. Each saved prefab is read back from disk and checked for its avatar and the shared Animator Controller, so a model is only reported ready, and listed in the window, once it can actually animate. Models with lost units (a 180 m or 1.8 cm character) get their source scaled to 1.8 m. IK settings and edits to the model sources are kept.
3. **Choose Character Model…** opens a window listing the models Prepare Character Models has set up, with previews and heights, and sets which one the sample scene's character uses. The choice is an override on the scene's instance of the Character prefab, so the prefab itself, which the tests spawn, keeps the mannequin.

All three also run from the command line for automation: `-executeMethod CharacterShowcase.EditorTools.MannequinGenerator.GenerateFromCommandLine` (with `-blender <path>` or `-skipBlender`), `...CharacterModelImporter.PrepareAll`, and `...CharacterModelWindow.UseInSceneFromCommandLine -model "X Bot"`.

A Humanoid avatar's reference pose decides how every clip looks on that model, so Prepare Character Models builds it carefully. It starts from the skin's bind pose, the pose the mesh was modelled in, rather than the pose the skeleton is saved in: Blender's FBX export saves the mannequin's skeleton in the last clip's pose (the dash), and an avatar made from that stored every mannequin clip relative to a forward lean, so other models played them leaning about 45 degrees back. It then straightens the spine up, the arms out and the legs down. With that, every model holds its torso at the mannequin's lean and keeps its planted feet in place to within a few percent of its speed, with no per-model tuning.

### IK
CharacterIK (on each model, reachable via AnimationController.IK) layers inverse kinematics over the animations. Hands and feet can be pulled to target positions and rotations, and elbows and knees bent toward hint positions. Each has its own 0-1 weight that fades smoothly in and out, blending with the animation underneath. Targets can be Transforms assigned in the inspector or positions set from code:

```csharp
ik.SetGoal(IKLimb.RightHand, doorHandle.position, doorHandle.rotation); // reach for a handle
ik.SetHint(IKLimb.LeftFoot, kneeTarget.position, 0.5f);                 // push the left knee outward
ik.ClearGoal(IKLimb.RightHand);                                         // fade back to the animation
```

FootPlacementIK uses the same system to plant the feet on slopes, stairs and bumps automatically: it raycasts under each animated foot, shifts the foot to the real ground while keeping the animation's foot lift, tilts planted feet to the slope, and lowers the pelvis so the lower foot can reach. It feeds a per-frame procedural channel underneath the manual goals, so targets set from code still take priority. To try it, run Tools > Character Showcase > Create Foot Placement Test Area to add stairs, ramps and bumps in front of the character.

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
- every prepared character model: planted feet staying in place while walking, arms hanging down when standing, and the torso leaning as the mannequin's does when standing, walking and running (checks that its avatar's reference pose was set up)
- the model: loading at runtime and being set up, swapping models (including one without an Animator), cancelling loads when the character is destroyed or given another model, the editor preview, and the Animator parameter translator
- the camera rig, which follows a plain Transform, independent of the character code:
  - third and first person placement, and mode blending and events
  - pitch limits and zoom limits
  - collision with walls, while ignoring the target's own colliders
  - smooth follow and teleport snapping
  - shake and ShakeAll
  - first person target hiding, including renderers added to the target later
  - speed FOV and the landing dip
  - CameraZone overrides

Run them from Window > General > Test Runner > PlayMode > Run All, or from the command line with Unity closed:

```
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults TestResults.xml
```

The character scripts are in the CharacterShowcase assembly (Assets/Scripts), and editor tools are in CharacterShowcase.Editor (Assets/Editor), so the tests can reference them.

## Future Changes:
### Medium Priority Changes
- Tune performance
- Refactor for improved readability in some areas

### Low Priority Changes (these would be really cool but realistically I won't get to them for a while)
- Add Juice and polish
- Coordinate system re-centering to avoid floating point precision related bugs
- Add AI system which uses the same character scripts to pathfind and perform actions
