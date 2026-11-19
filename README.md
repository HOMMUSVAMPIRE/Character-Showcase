# Character Showcase
This Unity project is intended to showcase my capabilities in the area of creating satisfying character controllers which feel responsive, satisfying, and intuitive. It is also meant to showcase my coding style for possible employers and demonstrate expertise with C# and Unity. Peruse at your own discretion.

The same character can be the player's or an NPC's (NPCs walk routes of waypoints), its input can be recorded and replayed, and its movement is deterministic and ready for networking (its state can be saved, restored and sent; no networking is included).

This project is still a work in progress. I will be adding to it in my free time as I search for new employment.

Once the project meets my standards for completion, I may release it as a Unity Asset Store package for others to use in their games.

The character controller is **Meridian Motion**: its code is in `MeridianMotion.*` namespaces and assemblies, and its tools, components and assets are under Meridian Motion in the editor's menus. This project is its showcase: the levels (Meridian Complex, Meridian Basin, the Level Playground), the generated mannequin and fox, and the tools that make them are the showcase's own (`MeridianMotion.Showcase.*`), not part of the package.

## Start here
In the editor, **Tools > Meridian Motion > Start Here** opens a window with every everyday tool, each with a line saying what it does (it opens by itself the first time you open the project).

- **Play it:** open the project and a level, and press Play ([How to open this Project](#how-to-open-this-project)).
- **Add a character:** a new look for the humanoid, a character of your own, or a generic-rig character ([Adding a Character](docs/adding-a-character.md)).
- **Add an ability:** a new move for the character. New to it? Build one step by step in [Your First Ability](docs/tutorial-first-ability.md); the full reference is [Adding a Movement Ability](docs/adding-an-ability.md).

New to Unity? [Unity Basics](docs/unity-basics.md) explains the Unity words the docs use.

## How to open this Project
1. Install Git LFS (https://git-lfs.com), then clone the repo locally (more details here if needed: https://docs.github.com/en/repositories/creating-and-managing-repositories/cloning-a-repository). The scenes, models and animation clips are stored with Git LFS: cloned without it, they're placeholder text files
2. Download UnityHub from https://unity.com/download
3. Download and install the Unity version in ProjectSettings/ProjectVersion.txt via UnityHub or from https://unity.com/releases/editor/archive
4. Open UnityHub
5. Navigate to Add => Add project from disk
6. Select the location of the cloned repo from step 1
7. Open the project
8. Open Assets/Scenes/LevelPlayground/LevelPlayground.unity (the graybox level with every mechanic) and click "Play"

The test levels are in Assets/Scenes: the Meridian Complex and the Level Playground (see [docs/levels.md](docs/levels.md)), and the Meridian Basin, a variant of the Meridian Complex built from its level spec (Assets/Levels/MeridianBasin.level.json, written by Tools/LevelSpec/examples/meridian_basin.py; see [docs/level-specs.md](docs/level-specs.md)).

## How to see the code in this project
All of the runtime code is in the Assets/Scripts folder:
- Character: the character's controller, input (the per-step input frames, and their recording and replay), spawner, and the MMotionMotor with the data it works on (MovementStep, Inputs, CharacterState, MovementEvents) and its saved state (snapshots, and writing them to bytes); InputSources/: what drives a character (the player's device, a script, an NPC's route)
- MovementModes: the movement modes, StandardMovementMode and its movement components (Components/: one file per part of the movement, walking and falling included)
- Animation: the MMotionAnimator, the animation state it builds, and the translators that drive an Animator from it
- IK: HumanoidIK and the IK components on every model (foot placement, hands on ledges, walls, ladders and lines)
- Models: the character's model host, the sources it gets models from, and the hitboxes on its models' bones
- Effects: the water's splashes and ripples, played from the movement's events
- Level: the level elements, an assembly of their own that the character and the level builders both use (moving platforms, ladders, climbable surfaces, ropes, ziplines, bounce pads, water, crates, timing gates); Editor/: turning a level's named fixtures into them, and painting climbable surfaces
- CameraSystem: the camera rig (`MeridianMotion.Cameras`), which knows nothing about the character
- PhysicsQueries: the growing physics query buffers the character and the camera share (`MeridianMotion.PhysicsQueries`)

The editor tools are in Assets/Editor: the character's (importing models and animations, Set Up Scene, the movement's inspectors), and, each in an assembly of its own that a package of the character would leave out, Pipeline/ (generating the mannequin and the fox in Blender), LevelChecks/ (the movement envelope the levels are checked against) and Level/ (the level builders and the Level Editor). The tests are in Assets/Tests.

If you intend to get an idea of my coding style, how I write code, and how I solve problems etc. read through Assets/Scripts/Character/MMotionMotor.cs and the movement components in Assets/Scripts/MovementModes/Components

If you intend to get a full understanding of the project, start with Character/MMotionCharacter.cs, then go through Character/MMotionMotor.cs, MovementModes/AbilityMovementMode.cs and MovementComponent.cs, and MovementModes/StandardMovementMode.cs and its components, and from there, read through anything you like.

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
- [Spawning the character](docs/spawning.md): players, NPCs, input sources
- [Generic rigs](docs/generic-rigs.md): the fox, a character on four legs, and generating it
- [Automated tests](docs/testing.md)
- [Tuning the character](docs/tuning.md): where the settings are, tuning presets, what changes when you save
- [Adding a character](docs/adding-a-character.md): a new look for the humanoid, a character of your own, or a generic-rig character; the place to start
- [Your first ability](docs/tutorial-first-ability.md): a tutorial building one small ability (Ground Pound) end to end
- [Adding a movement ability](docs/adding-an-ability.md): the steps, from the component to its animation, tests and docs
- [Adding level content](docs/adding-level-content.md): a new level mechanic, a new piece, changing a builder
- [Troubleshooting](docs/troubleshooting.md)
- [Design decisions](docs/decisions.md): why it's built the way it is
- [Glossary](docs/glossary.md)
- [Unity basics](docs/unity-basics.md): the Unity terms the docs use, for newcomers to Unity

Every tunable value is a setting in the inspector: the movement assets in Assets/ScriptableObjects for the movement and its abilities (StandardMovementMode for the character, Fox Movement for the fox; Character Body holds the character's body), the components on the Character prefab and its models, the camera rig, and Assets/Editor/Level/LevelBuilderSettings.asset for the levels. The docs name the setting rather than repeating its value, so they stay right when it's retuned.

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
- Pathfinding for the NPCs, which use the same character scripts (they walk routes of waypoints now)
