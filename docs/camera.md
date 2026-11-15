# Camera System

The camera lives in Assets/Scripts/CameraSystem, in its own assembly with no dependency on the character code. Add a CameraRig component to a camera, give it a target, and every feature can be tuned or toggled from its inspector (the CameraRig's settings, grouped by feature). How the camera follows a resized character is in [Character Size](movement.md#character-size).

Features: first person (at a fixed eye height, or following an eye anchor Transform such as a character's eye point) and over-the-shoulder third person (scaled with an optional target size reference, so resized targets are framed alike) with smooth blending between them, zoom (zooming all the way in switches to first person), shoulder swapping (automatic when a wall blocks the current side), aiming, camera collision, follow damping, target interpolation for physics-driven targets, speed-based FOV, head bob, landing dips, trauma-based camera shake, optional auto-recentering, hiding the character up close, and CameraZone volumes that override camera behaviour in an area.

Beside a wall that blocks its shoulder even partly (running along one, say), the camera glides to the other shoulder and keeps its distance, rather than being squeezed in behind the target's head against the wall; it returns once the wall is gone.

A wall alongside never pulls the camera in: each collision sweep stops a little short of what it hits, so the camera's backward sweep never starts pressed against a wall it's running parallel to (which used to snap the camera in as a wall run began).

| Action | Keyboard & Mouse | Gamepad |
| --- | --- | --- |
| Look | Mouse | Right stick |
| Zoom | Scroll wheel | D-pad up / down |
| Toggle first / third person | V | Right stick press |
| Swap shoulder | Q | Right bumper |
| Aim | Right mouse button | Left trigger |
| Release cursor | Escape (click to re-lock) | |
