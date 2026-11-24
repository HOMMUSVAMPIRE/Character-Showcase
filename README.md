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

- **Use it in your own project:** what it needs, copying the package in, and a character in your own scene ([Getting Started in Your Project](docs/getting-started.md)).

New to Unity? [Unity Basics](docs/unity-basics.md) explains the Unity words the docs use.

## How to open this Project
1. Install Git LFS (https://git-lfs.com), then clone the repo locally (more details here if needed: https://docs.github.com/en/repositories/creating-and-managing-repositories/cloning-a-repository). The scenes, models and animation clips are stored with Git LFS: cloned without it, they're placeholder text files
2. Download UnityHub from https://unity.com/download
3. Download and install the Unity version in ProjectSettings/ProjectVersion.txt via UnityHub or from https://unity.com/releases/editor/archive
4. Open UnityHub
5. Navigate to Add => Add project from disk
6. Select the location of the cloned repo from step 1
7. Open the project
8. Open Assets/Showcase/Scenes/LevelPlayground/LevelPlayground.unity (the graybox level with every mechanic) and click "Play"

The test levels are in Assets/Showcase/Scenes: the Meridian Complex and the Level Playground (see [docs/showcase/levels.md](docs/showcase/levels.md)), and the Meridian Basin, a variant of the Meridian Complex built from its level spec (Assets/Showcase/Levels/MeridianBasin.level.json, written by Tools/LevelSpec/examples/meridian_basin.py; see [docs/showcase/level-specs.md](docs/showcase/level-specs.md)).

## How to see the code in this project
The project is two folders. **Assets/MeridianMotion** is the package, what would ship: its code (Runtime/, Editor/), the assets it comes with (Content/: the prefabs, the mannequin and its animation, the water shaders) and its tests (Tests/). **Assets/Showcase** is everything else: the levels and their builders, the Blender pipeline, the fox and the sample models.

The runtime code is in Assets/MeridianMotion/Runtime:
- Characters/: the character, in folders by part:
  - Character: its controller (MMotionCharacter), spawner, and the states and events the movement and animation share
  - Motor: the MMotionMotor and the data it works on (MovementStep, its capabilities and simulation)
  - Body: the character's body, a shape of its own rather than a collider
  - Input: the per-step input frames, the player's input reader, recording and replay; InputSources/: what drives a character (the player's device, a script, an NPC's route)
  - Determinism: saving and restoring its state, and writing it to bytes
  - MovementModes: the movement modes, StandardMovementMode and its movement components (Components/: one file per part of the movement, walking and falling included)
  - Animation: the MMotionAnimator, the animation state it builds, and the translators that drive an Animator from it
  - IK: HumanoidIK and the IK components on every model (foot placement, hands on ledges, walls, ladders and lines)
  - Models: the character's model slot, the sources it gets models from, and the hitboxes on its models' bones
  - Effects: the water's splashes and ripples, played from the movement's events
- LevelElements/: the level elements, an assembly of their own that the character and the level builders both use (moving platforms, ladders, climbable surfaces, ropes, ziplines, bounce pads, water, crates, timing gates)
- Cameras/: the camera rig (`MeridianMotion.Cameras`), which knows nothing about the character
- PhysicsQueries/: the growing physics query buffers the character and the camera share (`MeridianMotion.PhysicsQueries`)

The editor tools are in Assets/MeridianMotion/Editor (importing models and animations, Set Up Scene, New Character and New Ability, the Character Doctor, the movement's inspectors; LevelElements/: turning a level's named fixtures into level elements, and painting climbable surfaces). The showcase's own are in Assets/Showcase/Editor, each in an assembly of its own: Pipeline/ (generating the mannequin and the fox in Blender), LevelChecks/ (the movement envelope the levels are checked against) and Levels/ (the level builders and the Level Editor). The tests are in Assets/MeridianMotion/Tests, and the showcase's in Assets/Showcase/Tests.

If you intend to get an idea of my coding style, how I write code, and how I solve problems etc. read through Assets/MeridianMotion/Runtime/Characters/Motor/MMotionMotor.cs and the movement components in Assets/MeridianMotion/Runtime/Characters/MovementModes/Components

If you intend to get a full understanding of the project, start with Characters/Character/MMotionCharacter.cs, then go through Characters/Motor/MMotionMotor.cs, MovementModes/AbilityMovementMode.cs and MovementComponent.cs, and MovementModes/StandardMovementMode.cs and its components, and from there, read through anything you like.

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
| Pause menu (frees the cursor) | Escape | B (the Cancel button) |
| Release cursor, in a scene without the UI | Escape (click to re-lock) | |

## Documentation
### Using the package
For using Meridian Motion in a project of your own: about what ships (Assets/MeridianMotion), with the showcase's own examples (the fox, the levels) where they help.
- [Getting started in your project](docs/getting-started.md): what the package needs, installing it, the hitbox layer, the output folder, a character in your own scene
- [Architecture](docs/architecture.md): how the parts fit together, one physics step, what may reference what
- [Movement](docs/movement.md): walking and sprinting, jumping, dashing, turning and leaning, collision, character size, moving ground
- [Movement abilities](docs/abilities.md): crouching, sliding and rolling, wall running, wall sliding, ledges, vaulting
- [Climbing and traversal](docs/traversal.md): wall climbing, ladders, pushing and pulling, ropes, ziplines, bounce pads, swimming; the level fixtures that set up a level's mechanics
- [Camera system](docs/camera.md)
- [UI framework](docs/ui.md): windows and the Back key, dialogs, the pause menu, data lists, tabs, context menus, notifications, rule-gated controls, labels in the world
- [Character model and animations](docs/animation.md): the mannequin, replacing the model, the animation libraries, IK
- [Spawning the character](docs/spawning.md): players, NPCs, input sources
- [Generic rigs](docs/generic-rigs.md): the fox, a character on four legs, and generating it
- [Tuning the character](docs/tuning.md): where the settings are, tuning presets, what changes when you save
- [Adding a character](docs/adding-a-character.md): a new look for the humanoid, a character of your own, or a generic-rig character; the place to start
- [Your first ability](docs/tutorial-first-ability.md): a tutorial building one small ability (Ground Pound) end to end
- [Adding a movement ability](docs/adding-an-ability.md): the reference, from the component to its animation, tests and docs
- [Automated tests](docs/testing.md): writing tests on the test rig and the ability kit, and what each fixture checks
- [Running the tests](docs/running-tests.md): the Test Runner and the runner script, how long each takes, profiling and recording them
- [Troubleshooting](docs/troubleshooting.md)
- [Design decisions](docs/decisions.md): why it's built the way it is
- [Glossary](docs/glossary.md)
- [Unity basics](docs/unity-basics.md): the Unity terms the docs use, for newcomers to Unity

### The showcase
About this repository's own levels and tools, which don't ship with the package (Assets/Showcase, docs/showcase).
- [Test levels](docs/showcase/levels.md): the Meridian Complex, the Level Playground, the mechanics in them
- [Level specs](docs/showcase/level-specs.md): the level format, the Level Editor and the levelspec tool
- [Adding level content](docs/showcase/adding-level-content.md): a new level mechanic, a new piece, changing a builder

Every tunable value is a setting in the inspector: the movement assets in Assets/MeridianMotion/Content/Movement for the movement and its abilities (StandardMovementMode for the character, Fox Movement for the fox; Character Body holds the character's body), the components on the Character prefab and its models, the camera rig, and Assets/Showcase/Editor/Levels/LevelBuilderSettings.asset for the levels. The docs name the setting rather than repeating its value, so they stay right when it's retuned.

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
