# Camera System

The camera lives in Assets/MeridianMotion/Runtime/Cameras, in its own assembly with no dependency on the character code. Add a MMotionCameraRig component to a camera, give it a target, and every feature can be tuned or toggled from its inspector (the MMotionCameraRig's settings, grouped by feature). How it frames a resized target is under [At any size](#at-any-size).

Features: first person (at a fixed eye height, or following an eye anchor Transform such as a character's eye point) and over-the-shoulder third person (scaled with an optional target size reference, so resized targets are framed alike) with smooth blending between them, zoom (zooming all the way in switches to first person), shoulder swapping (automatic when a wall blocks the current side), aiming, camera collision, follow damping, target interpolation for physics-driven targets, speed-based FOV, head bob, landing dips, trauma-based camera shake, optional auto-recentering, hiding the character up close, and CameraZone volumes that override camera behaviour in an area.

Beside a wall that blocks its shoulder even partly (running along one, say), the camera glides to the other shoulder and keeps its distance, rather than being squeezed in behind the target's head against the wall; it returns once the wall is gone. A target closer to a wall or ceiling than the collision probe (Collision's Probe Radius) is wide, a slim character pressed against one or squeezing through a tight space, doesn't pull the camera in either: the probe starts from just clear of it, and where even that leaves no room (a corridor narrower than the probe), the probe shrinks to fit.

A wall alongside never pulls the camera in: each collision sweep stops a little short of what it hits, so the camera's backward sweep never starts pressed against a wall it's running parallel to.

## Its settings
Everything is on the MMotionCameraRig component, grouped by feature in its inspector (each setting's tooltip says what it does):

| Section | What it covers |
| --- | --- |
| Target, Target Size Reference | What the camera follows, and an optional Transform whose scale frames resized targets alike |
| Input | Where camera input comes from: a PlayerInput (the scene's first, if none is set) or an Input Action asset, and the action names |
| Modes | The view mode it starts in, which modes are allowed (Allow First Person, Allow Third Person: with neither, third person is), and the blend between first and third person |
| Look | Mouse and stick sensitivity, the stick's response curve, smoothing |
| Cursor | Locking the cursor during play, and the keys to release and re-lock it |
| First Person | Eye height (or an eye anchor Transform), eye offset, near clip, hiding the target's own renderers |
| Third Person | Pivot height, shoulder offset and swapping (and when it swaps by itself, away from a wall: the Auto Swap settings), follow smoothing, hiding the target up close |
| Zoom | Distance range, zoom speed, and switching to first person at the closest |
| Aim | Field of view, distance, shoulder and sensitivity while aiming, and how aiming is held or toggled |
| Collision | The layers it collides with, the probe's size, the closest it may be pulled in |
| Follow | Interpolating a physics-driven target, the teleport distance, and how far the follow may trail |
| Speed FOV, Head Bob, Landing | Widening the view with speed, bobbing in first person, dipping on landing |
| Shake | Trauma-based shake: how fast it fades and how strong it gets |
| Recenter | Swinging behind the target after a while without look input (not when it'd swing further round than Max Turn Angle) |
| Debug | Drawing the pivot, shoulder point and collision probe in the Scene view |

Distances and speeds are for a target of normal size, and scale with the Target Size Reference (below).

### At any size
Given a Target Size Reference (a spawned character hands it its model parent, whose scale is its size: see [Spawning the Character](spawning.md)), the camera frames a resized target as it does one of normal size (how the character is resized is in [Character Size](movement.md#character-size)):
- First person follows the eye point. Third person's pivot height, shoulder offset and distance, and the head bob, scale with the size reference.
- Speed effects (the speed-based field of view and head bob strength) judge the target's speed relative to its size, dividing it by its height, so a character at its own top speed gets the same effect at any size.
- Every other distance and speed the camera uses scales with the target too: follow lag, landing dip and its speed threshold, collision probe and minimum distance, hide distance, shake, teleport distance and the clip planes (near always, far only for a bigger target, so a small one still sees the world).

## Connecting it to something
The camera follows a plain Transform: `MMotionCameraRig.Follow(target)`, or `Follow(target, eyeAnchor, sizeReference)` to give it an eye point for first person and a size reference too (`SetTarget` changes only the target). Nothing in it knows what it follows, so it works the same on a character, a vehicle or a moving prop.

From code, the rig can also be steered: `SetMode(mode)` and `ToggleMode()` change the view (each returns false for a mode that isn't allowed), `SetLookRotation(yaw, pitch)` points it (the pitch clamped to the mode's limits), `SetDistance(distance, instant)` zooms, `SetShoulder(right)` picks the third-person shoulder, and `Snap()` skips this frame's smoothing, jumping straight to where it should be.

How fast the target moves, when it lands and how it's scaled are worked out by watching the Transform (`CameraTargetTracker`), not by asking the target's code.

In the levels, the character and the camera are connected in the scene: Set Up Scene wires the CharacterSpawner's On Camera Target Spawned event to the rig's Follow, which then gets each spawned character, its eye point and its model parent as the size reference (see [Spawning the Character](spawning.md)).

## Camera zones
A CameraZone is a volume (a trigger collider for its shape: a primitive or a convex mesh) that changes the camera while its target is inside: forcing first or third person (in a cramped vent, say), capping the third person distance (in a narrow corridor), or adding to or taking from the field of view (eased in and out over its Fov Blend Time). Where zones overlap, the one with the highest Priority wins. It checks whether the target is inside by itself, so the target needs no Rigidbody.

## Shake
`MMotionCameraRig.AddTrauma(amount)` shakes one camera, and `MMotionCameraRig.ShakeAll(amount)` every camera rig, by adding trauma (0 to 1), which fades over time (the Shake section). Landings add some by themselves. A landing is a stop after falling fast (Landing's Min Impact Speed) for at least its Min Fall Time, judged from the target's height each physics step (not each frame, which at a low frame rate can average several steps' drops into what looks like a fall), so stepping down stairs, which drops the target onto each step in a physics step or two, doesn't dip or shake the camera at any frame rate.

## Adding a camera feature that needs the character
The camera assembly references nothing of the character's (see [Architecture](architecture.md#what-may-know-about-what)), and a feature mustn't change that. If a feature needs something only the character knows (that it's wall running, say), the camera exposes a generic way to be told: a public method or settable value (as `Follow`, `SetMode`, `SetDistance` and `AddTrauma` are), an event, or an interface the camera defines. The character's side then calls it, from the character's code or wired in the scene (as the spawner's event is wired to `Follow`).

Prefer working it out from the Transform where that's enough, as the tracker does for speed and landings: then it works for any target.

## Controls
| Action | Keyboard & Mouse | Gamepad |
| --- | --- | --- |
| Look | Mouse | Right stick |
| Zoom | Scroll wheel | D-pad up / down |
| Toggle first / third person | V | Right stick press |
| Swap shoulder | Q | Right bumper |
| Aim | Right mouse button | Left trigger |
| Release cursor | Escape (click to re-lock) | |
