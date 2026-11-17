# Glossary
Terms this project uses with a meaning of its own, and where each is explained.

**Ability.** A part of the character's movement that takes it over while it lasts (a wall run, a ledge hang, a zipline ride) or acts on top of walking (a dash, a jump). Each is a movement component. See [Movement Abilities](abilities.md) and [Climbing and Traversal](traversal.md).

**Animation translator.** What turns the character's animation state into a particular model's Animator parameters (`CharacterAnimationTranslator`), so movement never needs to know which model it's animating. See [Character Model and Animations](animation.md).

**Builder.** An editor script that generates one level from pieces (`MeridianComplexBuilder`, `LevelPlaygroundBuilder`). See [Test Levels](levels.md).

**Capabilities.** What the character can do as it's tuned and sized now (how far each kind of jump crosses, how high each climb reaches, how far a slide carries it), measured by running its own movement in a movement simulation (`MovementController.Capabilities`). See [Capabilities](movement.md#capabilities).

**Character state.** The flags saying what the character is doing in a physics step (grounded, jumping, wall running, ...): `CharacterState`. Changes in them raise movement events.

**Component.** See movement component.

**Envelope.** The default character's capabilities written to a file (Tools/LevelSpec/envelope.json) for the levelspec tool, which checks levels against it. Written again whenever the tuning is saved in the editor. See [Tuning the Character](tuning.md#what-changes-when-you-save).

**Fixture.** A level mechanic as a builder makes it: geometry named "Name | key=value | ..." that Set Up Scene makes work (a moving platform, a rope, a bounce pad). See [Adding Level Content](adding-level-content.md).

**Hitbox.** One of the capsules on a model's bones that meet the world for the character (thrown things, triggers), which has no collider of its own: `CharacterHitboxes`, on the model. The movement never uses them. See [Hitboxes](animation.md#hitboxes).

**Generated asset.** An asset written by an editor tool (the mannequin, the level scenes, the envelope, ...), changed by changing its generator and running it again, never by hand. The table in AGENTS.md lists them.

**Graybox.** The untextured blockout style of the levels, and the library of pieces they're built from (`Graybox`).

**Hygiene check.** A quick check of the project that needs no Unity (`Tools/CI/check_project_hygiene.py`): `.meta` files, GUIDs, assembly dependencies and setting names. See [Troubleshooting](troubleshooting.md#the-tests).

**Level spec.** A level recorded as data: the list of pieces its builder called, with their settings, which builds the same level again. Edited in the Level Editor or by the levelspec tool. See [Level Specs](level-specs.md).

**Marker.** An object a builder places only to be found later: a spawn marker ("Spawn Point (...)") becomes a spawn point, and a "(future)" marker shows where a mechanic is planned.

**Model source.** Where the character's model comes from (`CharacterModelSource`): the character asks its source for a model and gives it back. See [Character Model and Animations](animation.md).

**Movement component.** One part of the character's movement, with its own settings, in a movement mode's list (`MovementComponent`): walking and running, falling and air handling, and each ability. See [Movement Components](movement.md#movement-components).

**Movement events.** One-off happenings in a physics step (jumped, landed, a ledge grabbed): `MovementEvents`, which animation and other systems listen for.

**Movement mode.** The ScriptableObject asset that defines how characters move, made of movement components (`BasicMovementMode`, at Assets/ScriptableObjects/BasicMovementMode.asset). Shared by every character using it.

**Movement simulation.** A copy of the character stepped faster than real time in a world of its own, far below everything else (`MovementSimulation`), used to measure its capabilities.

**Piece.** One kind of thing levels are built from (a platform, a stair, a railing), a method of `Graybox` marked `[Piece]`. A level spec is a list of pieces.

**Posture.** How tall the character's body is: lowered to the crouch height while crouched, sliding or rolling, and raised again where there's room.

**Set Up Scene.** The editor tool that makes a built level playable: it adds the spawner, input manager and camera, and makes the fixtures and spawn markers work (`SceneGameSetup`, `LevelFixtureSetup`). See [Spawning the Character](spawning.md).

**Size scaling.** How the movement changes with the character's size: in proportion (a scaled copy) or naturally (as real animals do). The MovementController's Size Scaling. See [Character Size](movement.md#character-size).

**Step time.** The character's own clock (`MovementController.StepTime`), which movement times things by, so a movement simulation stepped faster than real time behaves the same.

**Takeover.** An ability taking the character for a physics step: the first component in the list whose `TakeOver` says it has the character moves it, and the others wait.

**Test rig.** The PlayMode tests' harness (`CharacterTestRig`), which spawns the real character, steps its movement with scripted inputs, and gives it its own copy of the movement mode to change. See [Automated Tests](testing.md).

**Tuning preset.** A saved set of movement settings, some or all, to apply to a movement mode again (`MovementTuningPreset`). See [Tuning the Character](tuning.md#tuning-presets).
