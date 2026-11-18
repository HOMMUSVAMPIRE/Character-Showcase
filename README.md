# Character Showcase
This Unity project is intended to showcase my capabilities in the area of creating satisfying character controllers which feel responsive, satisfying, and intuitive. It is also meant to showcase my coding style for possible employers and demonstrate expertise with C# and Unity. Peruse at your own discretion.

This project is still a work in progress. I will be adding to it in my free time as I search for new employment.

Once the project meets my standards for completion, I may release it as a Unity Asset Store package for others to use in their games.

## How to open this Project
1. Install Git LFS (https://git-lfs.com), then clone the repo locally (more details here if needed: https://docs.github.com/en/repositories/creating-and-managing-repositories/cloning-a-repository). The scenes, models and animation clips are stored with Git LFS: cloned without it, they're placeholder text files
2. Download UnityHub from https://unity.com/download
3. Download and install the Unity version in ProjectSettings/ProjectVersion.txt via UnityHub or from https://unity.com/releases/editor/archive
4. Open UnityHub
5. Navigate to Add => Add project from disk
6. Select the location of the cloned repo from step 1
7. Open the project
8. Click "Play" (in Assets/Scenes/LevelPlayground, the graybox level with every mechanic, which opens by default)

The test levels are in Assets/Scenes: the Meridian Complex and the Level Playground (see [docs/levels.md](docs/levels.md)).

## How to see the code in this project
All of the runtime code is in the Assets/Scripts folder:
- Character: the character's controller, input, spawner, and the MovementController with the data it works on (MovementData, Inputs, CharacterState, MovementEvents)
- MovementModes: the movement modes, BasicMovementMode and its movement components (Components/: one file per part of the movement, walking and falling included)
- Animation: the AnimationController, the animation state it builds, and the translators that drive an Animator from it
- IK: CharacterIK and the IK components on every model (foot placement, hands on ledges, walls, ladders and lines)
- Models: the character's model host, the sources it gets models from, and the hitboxes on its models' bones
- Level: the level mechanics (moving platforms, ladders, climbable surfaces, ropes, ziplines, bounce pads, water, crates, timing gates)
- CameraSystem: the camera rig, which knows nothing about the character

The editor tools that generate the character, its animations and the levels are in Assets/Editor, and the tests in Assets/Tests.

If you intend to get an idea of my coding style, how I write code, and how I solve problems etc. read through Assets/Scripts/Character/MovementController.cs and the movement components in Assets/Scripts/MovementModes/Components

If you intend to get a full understanding of the project, start with Character/ACCharacterController.cs, then go through Character/MovementController.cs, MovementModes/ComposedMovementMode.cs and MovementComponent.cs, and MovementModes/BasicMovementMode.cs and its components, and from there, read through anything you like.

## Controls
| Action | Keyboard & Mouse | Gamepad |
| --- | --- | --- |
| Move | WASD or arrow keys | Left stick |
| Sprint (held) | Left Shift | Left stick press |
| Jump | Space | South face button |
| Dash | F | Right trigger |
| Crouch (and slide, from a sprint) | C | East face button |
| Roll | Left Ctrl | Left bumper |
| Interact (held: push and pull) | E | North face button |
| Look | Mouse | Right stick |
| Zoom | Scroll wheel | D-pad up / down |
| Toggle first / third person | V | Right stick press |
| Swap shoulder | Q | Right bumper |
| Aim | Right mouse button | Left trigger |
| Release cursor | Escape (click to re-lock) | |

## Documentation
- [Architecture](docs/architecture.md): how the parts fit together, one physics step, what may reference what
- [Movement](docs/movement.md): walking and sprinting, jumping, dashing, turning and leaning, collision, character size, moving ground
- [Movement abilities](docs/abilities.md): crouching, sliding and rolling, wall running, wall sliding, ledges, vaulting
- [Climbing and traversal](docs/traversal.md): wall climbing, ladders, pushing and pulling, ropes, ziplines, bounce pads, swimming
- [Camera system](docs/camera.md)
- [Character model and animations](docs/animation.md): the mannequin, replacing the model, the animation libraries, IK
- [Test levels](docs/levels.md): the Meridian Complex, the Level Playground, level mechanics
- [Level specs](docs/level-specs.md): the level format, the Level Editor and the levelspec tool
- [Spawning the character](docs/spawning.md)
- [Creatures](docs/creatures.md): the fox, a character on four legs, and generating it
- [Automated tests](docs/testing.md)
- [Tuning the character](docs/tuning.md): where the settings are, tuning presets, what changes when you save
- [Adding a movement ability](docs/adding-an-ability.md): the steps, from the component to its animation, tests and docs
- [Adding level content](docs/adding-level-content.md): a new level mechanic, a new piece, changing a builder
- [Troubleshooting](docs/troubleshooting.md)
- [Design decisions](docs/decisions.md): why it's built the way it is
- [Glossary](docs/glossary.md)

Every tunable value is a setting in the inspector: the BasicMovementMode asset (Assets/ScriptableObjects) for the movement and its abilities, the components on the Character prefab and its models, the camera rig, and Assets/Editor/Level/GrayboxSettings.asset for the levels. The docs name the setting rather than repeating its value, so they stay right when it's retuned.

## Future Changes:
### Medium Priority Changes
- Movement tuning at runtime: each character with its own copy of its movement, and tuning zones that change it in part of a level (undone on leaving)
- Import and export the movement tuning as CSV spreadsheets
- Tune performance
- Refactor for improved readability in some areas

### Low Priority Changes (these would be really cool but realistically I won't get to them for a while)
- Add Juice and polish
- Coordinate system re-centering to avoid floating point precision related bugs
- Respawning: putting a character back at a spawn point with its movement reset (its momentum, and any ability it's in the middle of), rather than destroying it and spawning another
- Gravity in any direction: the movement assumes it's straight down (gravity, the grounded check and the ground's carry all do)
- Add AI system which uses the same character scripts to pathfind and perform actions
