# Glossary
Terms this project uses with a meaning of its own, and where each is explained. Entries marked *(showcase levels)* are about the level builders and their tools, which a package of the character leaves out.

**Ability.** A part of the character's movement that takes it over while it lasts (a wall run, a ledge hang, a zipline ride) or acts on top of walking (a dash, a jump). Each is a movement component. See [Movement Abilities](abilities.md) and [Climbing and Traversal](traversal.md).

**Ability traits.** What an ability does with the character while it's under way (`MovementComponent.Traits`, an `AbilityTraits`: it holds the character up, it holds on to something), which other abilities ask the mode about (`AbilityInProgressWith`) rather than listing the abilities they mean. See [Adding a Movement Ability](adding-an-ability.md).

**Animation translator.** What turns the character's animation state into a particular model's Animator parameters (`CharacterAnimationTranslator`), so movement never needs to know which model it's animating. See [Character Model and Animations](animation.md).

**Body Definition.** An asset holding a character's body (a `BodyDefinition`: its shape and any extra parts), the MovementController's Body Definition; the Character prefab's is Assets/ScriptableObjects/Character Body.asset, and Prepare Character Models writes one beside each model's source. See [Collision](movement.md#collision).

**Body Fit.** The CharacterModelHost setting that fits the character's body to its model (by height, or the model's own Body Definition). The body its source was prepared with is fitted in `Start`, before the first step, so the character moves the same body before its model arrives, or without one (`LoadModelOnStart` off). See [Replacing the model](animation.md#replacing-the-model).

**Builder.** *(showcase levels)* An editor script that generates one level from pieces (`MeridianComplexBuilder`, `LevelPlaygroundBuilder`). See [Test Levels](levels.md).

**Capabilities.** What the character can do as it's tuned and sized now (how far each kind of jump crosses, how high each climb reaches, how far a slide carries it), measured by running its own movement in a movement simulation (`MovementController.Capabilities`). See [Capabilities](movement.md#capabilities).

**Character state.** The flags saying what the character is doing in a physics step (grounded, jumping, wall running, ...): `CharacterState`. Changes in them raise movement events.

**Component.** See movement component.

**Envelope.** *(showcase levels)* The default character's capabilities written to a file (Tools/LevelSpec/envelope.json) for the levelspec tool, which checks levels against it. Written again whenever the tuning is saved in the editor. See [Tuning the Character](tuning.md#what-changes-when-you-save).

**Extra parts.** More primitives beside a body's shape, together one body (a Body Definition's Extra Parts): the fox's back and head. See [Collision](movement.md#collision).

**Fixture.** A level element as a level is built: geometry named "Name | key=value | ..." (a moving platform, a rope, a bounce pad) that `LevelFixtureSetup`, in the level elements' editor assembly, gives its component. The builders' levels have theirs attached as they're built; Set Up Scene runs it too, for a scene built some other way. The names are `LevelFixtureSetup`'s constants, the one source the builders and Set Up Scene use. See [Adding Level Content](adding-level-content.md).

**Generated asset.** An asset written by an editor tool (the mannequin, the level scenes, the envelope, ...), changed by changing its generator and running it again, never by hand. The table in AGENTS.md lists them.

**Graybox.** *(showcase levels)* The untextured blockout style of the levels, and the library of pieces they're built from (`Graybox`).

**Ground probes.** What keeps a hovering character's feet on the ground: a ray straight down under the feet (or a few round them), and the ground check's sphere for an edge beside them. See [Collision](movement.md#collision).

**Hitbox.** One of the capsules on a model's bones that meet the world for the character (thrown things, triggers), which has no collider of its own: `CharacterHitboxes`, on the model. The movement never uses them. See [Hitboxes](animation.md#hitboxes).

**Hover Over Steps.** The MovementController setting holding the body's bottom a step's height above the feet, which probes keep on the ground: steps pass under the body, which never rests on an edge or a corner. See [Collision](movement.md#collision).

**Hygiene check.** A quick check of the project that needs no Unity (`Tools/CI/check_project_hygiene.py`): `.meta` files, GUIDs, assembly dependencies and the code the camera, the level elements and the level builders may use, setting names and types, test fixtures, and the component classes movement assets name. See [Troubleshooting](troubleshooting.md#the-tests).

**Input frame.** What a character's input does in one physics step (`CharacterInputFrame`): the move input, the look and the buttons held, tagged with the step's tick. Unmanaged, so it can be recorded, saved and sent as it is; presses and hold durations aren't in it (see input state). See [Input](movement.md#input).

**Input latch.** What turns the player's device, read every render frame, into one input frame per physics step (`InputLatch`, in `ACInputManager`), so a tap shorter than a step still counts and one render frame running two steps doesn't press a button twice. See [Input](movement.md#input).

**Input recording.** A character's input frames, oldest first, with the step length and the tuning they were recorded with (`InputRecording`, made by an `InputRecorder`), saved to a file and replayed to move the character exactly as it moved. See [Input](movement.md#input).

**Input source and owner.** What gives a character its input frames (`ICharacterInputSource`: `PlayerInputSource`, `ScriptedInputSource`, `WaypointInputSource`), set on its ACCharacterController or found on the character. Without one, its owner (`CharacterOwner`) decides: the local player's reads the device, an AI's stands idle, and a remote one isn't stepped. See [NPCs and input sources](spawning.md#npcs-and-input-sources).

**Input state.** The character's input as its movement reads it (`InputState`, read through `Inputs`), worked out each step from the input frames: which buttons went down or came up, and how long each has been held, counted in steps. Part of the character's state. See [Input](movement.md#input).

**Level clock.** The tick the level's moving mechanics (platforms, pushables, ropes) go by (`Level.LevelClock`): Unity's fixed steps, unless something sets it (a networked game's tick, the test rig's). The test rig owns it while it's in use (`LevelClock.TickSource`) and resets it when it's disposed; a test that sets it without the rig must set it back to null.

**Level element.** A part of a level the character traverses or meets (water, a rope, a ladder, a moving platform, a timing gate, ...): a component in Assets/Scripts/Level, the `CharacterShowcase.LevelElements` assembly, which the character finds and uses but which knows nothing of the character. See [Climbing and Traversal](traversal.md) and [Test Levels](levels.md).

**Level spec.** *(showcase levels)* A level recorded as data: the list of pieces its builder called, with their settings, which builds the same level again. Edited in the Level Editor or by the levelspec tool. See [Level Specs](level-specs.md).

**Marker.** An object a level places only to be found later: a spawn marker ("Spawn Point (...)", named by `LevelFixtureSetup.SpawnMarkerPrefix`) becomes one of the spawner's spawn points when Set Up Scene runs, and a "(future)" marker shows where a mechanic is planned.

**Model source.** Where the character's model comes from (`CharacterModelSource`): the character asks its source for a model and gives it back. See [Character Model and Animations](animation.md).

**Movement component.** One part of the character's movement, with its own settings, in a movement mode's list (`MovementComponent`): walking and running, falling and air handling, and each ability. See [Movement Components](movement.md#movement-components).

**Movement events.** One-off happenings in a physics step (jumped, landed, a ledge grabbed): `MovementEvents`, which animation and other systems listen for.

**Movement mode.** The ScriptableObject asset that defines how characters move, made of movement components (`BasicMovementMode`, at Assets/ScriptableObjects/BasicMovementMode.asset). Shared by every character using it.

**Movement simulation.** A copy of the character stepped faster than real time in a world of its own, far below everything else (`MovementSimulation`), used to measure its capabilities.

**Piece.** *(showcase levels)* One kind of thing levels are built from (a platform, a stair, a railing), a method of `Graybox` marked `[Piece]`. A level spec is a list of pieces.

**Posture.** How the character's body is held (`BodyPosture`): standing; low, lowered to the crouch height while crouched, sliding or rolling, and raised again where there's room; or lying, laid flat along the way it faces while swimming underwater.

**Query buffer.** A reused buffer for a physics query that grows and runs the query again when it fills (`RaycastHitBuffer`, `ColliderBuffer`; `PhysicsQueries.QueryBuffer`), since Unity's results come in no order and a full one may leave out the nearest hit. Its own assembly, shared by the character and the camera. See [Automated Tests](testing.md) (`QueryBufferTests`).

**Set Up Scene.** The editor tool that makes a level playable (`SceneGameSetup`): it adds the spawner, with the level's spawn markers as its spawn points, the input manager and the camera, and tells the course timers who's racing. It runs `LevelFixtureSetup` too, for fixtures not set up yet. See [Spawning the Character](spawning.md).

**Size scaling.** How the movement changes with the character's size: in proportion (a scaled copy) or naturally (as real animals do). The MovementController's Size Scaling. See [Character Size](movement.md#character-size).

**Stable ID.** An ID for a level object that's the same on every machine that loads the level (`Level.StableId`): a hash of its scene and path of names. What a character's written state names the ground it's on, or the rope it holds, by.

**State snapshot.** A character's movement state saved between steps, to be put back (`MovementController.SaveState`, `RestoreState`: a `MovementStateSnapshot`, in this process only), with the level's ropes and pushables saved beside it (`WorldStateSnapshot`). `MovementStateSummary` is its compact, unmanaged part (pose, momentum, state, clock, latest input frame). `StateSerializer` writes either to bytes and reads them into a character or level built the same way, naming the level's objects by their stable IDs. See [Automated Tests](testing.md) (`DeterminismTests`).

**Step tick.** The character's own clock, in whole steps, which movement times things by, so a movement simulation stepped faster than real time behaves the same. It has two counts: `MovementController.Tick`, the steps the character has taken, which its input frames are tagged with; and `StepTick`, the step under way (inside a step, one more than `Tick`), which timing goes by (keep the tick something happened at and ask `SecondsSince`; `StepTime` is it in seconds). `SetClock` starts both anywhere. See [State and assets](architecture.md#state-and-assets).

**Step trace.** A record of a test run step by step (`StepTrace`): where the character was, how it was found on the ground, what collision did and how far its feet were above the ground under them, written to Logs/StepTraces by the ground contact tests. See [Testing](testing.md).

**Takeover.** An ability taking the character for a physics step: the first component in the list whose `TakeOver` says it has the character moves it, and the others wait.

**Test rig.** The PlayMode tests' harness (`CharacterTestRig`), which spawns the real character, steps its movement with scripted inputs, and gives it its own copy of the movement mode to change. See [Automated Tests](testing.md).

**Tuning preset.** A saved set of movement settings, some or all, to apply to a movement mode again (`MovementTuningPreset`). See [Tuning the Character](tuning.md#tuning-presets).
