# Glossary
Terms this project uses with a meaning of its own, and where each is explained. Entries marked *(showcase levels)* are about the level builders and their tools, which a package of the character leaves out. Unity's own terms (prefab, inspector, Play mode, ...) are in [Unity Basics](unity-basics.md).

**Ability.** A part of the character's movement that takes it over while it lasts (a wall run, a ledge hang, a zipline ride) or acts on top of walking (a dash, a jump). Each is a movement component. See [Movement Abilities](abilities.md) and [Climbing and Traversal](traversal.md).

**Ability traits.** What an ability does with the character while it's under way (`MovementComponent.Traits`, an `AbilityTraits`: it holds the character up, it holds on to something), which other abilities ask the mode about (`AbilityInProgressWith`) rather than listing the abilities they mean. See [Adding a Movement Ability](adding-an-ability.md).

**Ability value.** A number an ability publishes for its look during its step (`AbilityValues`, named in the component's `AbilityValueNames`), which an Animator Parameter Translator's float parameter can show with no animation code. See [Character Model and Animations](animation.md#replacing-the-model).

**Animation set.** A character's own clips in some slots (its idle, its run), over an Animator layout's, built into a controller of its own (`CharacterAnimationSet`). See [Animation sets](animation.md#animation-sets).

**Animation translator.** What turns the character's animation state into a particular model's Animator parameters (`AnimationTranslator`), so movement never needs to know which model it's animating. See [Character Model and Animations](animation.md).

**Animator layout.** An asset describing a character's Animator Controller as data (clip slots, parameters, blend trees, states, transitions, with measured numbers and notes), which Build Animator Controllers builds it from. See [Animator layouts](animation.md#animator-layouts).

**Back key.** The UI map's Cancel (Escape, the gamepad's Cancel button): it goes to the top window that takes it (the cancel stack), and with none, to the fallback handler, which opens the pause menu. See [The Back key](ui.md#the-back-key).

**Body Definition.** An asset holding a character's body (a `BodyDefinition`: its shape and any extra parts), the MMotionMotor's Body Definition; the Character prefab's is Assets/MeridianMotion/Content/Movement/Character Body.asset, and Prepare Humanoid Models writes one beside each model's source. See [Collision](movement.md#collision).

**Body Fit.** The CharacterModelSlot setting that fits the character's body to its model (by height, or the model's own Body Definition). The body its source was prepared with is fitted in `Start`, before the first step, so the character moves the same body before its model arrives, or without one (`LoadModelOnStart` off). See [Replacing the model](animation.md#replacing-the-model).

**Builder.** *(showcase levels)* An editor script that generates one level from pieces (`MeridianComplexBuilder`, `LevelPlaygroundBuilder`). See [Test Levels](showcase/levels.md).

**Cancel stack.** The windows that take the Back key, the last opened on top (`UIManager`): destroyed, hidden, closing and uncancellable ones are passed over. See [The Back key](ui.md#the-back-key).

**Capabilities.** What the character can do as it's tuned and sized now (how far each kind of jump crosses, how high each climb reaches, how far a slide carries it), measured by running its own movement in a movement simulation (`MMotionMotor.Capabilities`). See [Capabilities](movement.md#capabilities).

**Character Doctor.** The editor window that checks a character prefab (its model, rig, measurements, hitboxes, body, eye point and animation) and fixes what it can (`CharacterChecks`). See [Adding a Character](adding-a-character.md).

**Character state.** The flags saying what the character is doing in a physics step (grounded, jumping, wall running, ...): `CharacterState`. Changes in them raise movement events.

**Click blocker.** A full-screen panel opened behind a window (its Click Blocker Style: dim, invisible, darken or blackout) so clicks don't reach what's under it. See [Windows](ui.md#windows).

**Component.** See movement component.

**Envelope.** *(showcase levels)* The default character's capabilities written to a file (Tools/LevelSpec/envelope.json) for the levelspec tool, which checks levels against it. Written again whenever the tuning is saved in the editor. See [Tuning the Character](tuning.md#what-changes-when-you-save).

**Extra parts.** More primitives beside a body's shape, together one body (a Body Definition's Extra Parts): the fox's back and head. See [Collision](movement.md#collision).

**Fallback handler.** What the Back key does with no window to take it (`UIManager.RegisterFallbackHandler`): the pause menu, through `PauseMenuOnBack`. See [The Back key](ui.md#the-back-key).

**Fixture.** A level element as a level is built: geometry named "Name | key=value | ..." (a moving platform, a rope, a bounce pad) that `LevelFixtureSetup`, in the level elements' editor assembly, gives its component. The builders' levels have theirs attached as they're built; Set Up Scene runs it too, for a scene built some other way. The names are `LevelFixtureSetup`'s constants, the one source the builders and Set Up Scene use. See [Adding Level Content](showcase/adding-level-content.md).

**Generated asset.** An asset written by an editor tool (the mannequin, the level scenes, the envelope, ...), changed by changing its generator and running it again, never by hand. The table in AGENTS.md lists them.

**Generic Rig Setup.** What a generic-rig character (a model that isn't a humanoid) is made of, by its bones' names: its root bone, legs, eyes, the bones its paw placement turns and its hitboxes go on, and its animation set (`GenericRigSetup`, beside its model in Assets/Showcase/Models/Generic). Made by Prepare Generic Rig Models with its bones guessed from their names, then checked; Prepare Generic Rig Models makes the generic-rig character from it. See [A generic rig of your own](generic-rigs.md#a-generic-rig-of-your-own).

**Flora.** *(showcase levels)* The levels' leaf-card trees, bushes, ferns, ivy and vines: prefabs Generate Flora makes, each kind in variants from seeds of their own (Flora Seeds), which a level's flora seed picks between. See [Flora](showcase/levels.md#flora).

**Grass Bender.** *(showcase levels)* What pushes the lawns' grass aside as a character goes through it: its lower legs' and feet's hitboxes (a circle at its feet without any), into a map round the camera that springs back (`GrassBender`, `GrassBending`). Set Up Scene gives the showcase's characters one. See [Grass](showcase/levels.md#grass).

**Grass Field.** *(showcase levels)* The grass over a group of lawns: a mask of where they are, one layer per floor, from which a compute shader places blades on the GPU before each camera renders (`GrassField`). See [Grass](showcase/levels.md#grass).

**Grass Style.** *(showcase levels)* How a Grass Field's grass looks and how much there is: its blades' length, shape and spacing, clumps, colours, sway and distances (`GrassStyle`), read every frame, so a change shows at once. See [Grass](showcase/levels.md#grass).

**Graybox.** *(showcase levels)* The untextured blockout style of the levels, and the library of pieces they're built from (`Graybox`).

**Ground probes.** What keeps a hovering character's feet on the ground: a ray straight down under the feet (or a few round them), and the ground check's sphere for an edge beside them. See [Collision](movement.md#collision).

**Hitbox.** One of the capsules on a model's bones that meet the world for the character (thrown things, triggers), which has no collider of its own: `CharacterHitboxes`, on the model. The movement never uses them. See [Hitboxes](animation.md#hitboxes).

**Hover Over Steps.** The MMotionMotor setting holding the body's bottom a step's height above the feet, which probes keep on the ground: steps pass under the body, which never rests on an edge or a corner. See [Collision](movement.md#collision).

**Hygiene check.** A quick check of the project that needs no Unity (`Tools/CI/check_project_hygiene.py`): `.meta` files, GUIDs, assembly dependencies and the code the camera, the level elements and the level builders may use, setting names and types, test fixtures, and the component classes movement assets name. See [Troubleshooting](troubleshooting.md#the-tests).

**Input frame.** What a character's input does in one physics step (`CharacterInputFrame`): the move input, the look and the buttons held, tagged with the step's tick. Unmanaged, so it can be recorded, saved and sent as it is; presses and hold durations aren't in it (see input state). See [Input](movement.md#input).

**Input latch.** What turns the player's device, read every render frame, into one input frame per physics step (`InputLatch`, in `PlayerInputReader`), so a tap shorter than a step still counts and one render frame running two steps doesn't press a button twice. See [Input](movement.md#input).

**Input recording.** A character's input frames, oldest first, with the step length and the tuning they were recorded with (`InputRecording`, made by an `CharacterInputRecorder`), saved to a file and replayed to move the character exactly as it moved. See [Input](movement.md#input).

**Input source and owner.** What gives a character its input frames (`ICharacterInputSource`: `PlayerInputSource`, `ScriptedInputSource`, `WaypointInputSource`), set on its MMotionCharacter or found on the character. Without one, its owner (`ControlledBy`) decides: the local player's reads the device, an AI's stands idle, and a remote one isn't stepped. See [NPCs and input sources](spawning.md#npcs-and-input-sources).

**Input state.** The character's input as its movement reads it (`InputState`, read through `StepInputs`), worked out each step from the input frames: which buttons went down or came up, and how long each has been held, counted in steps. Part of the character's state. See [Input](movement.md#input).

**Level clock.** The tick the level's moving mechanics (platforms, pushables, ropes) go by (`MeridianMotion.Levels.LevelClock`): Unity's fixed steps, unless something sets it (a networked game's tick, the test rig's). The test rig owns it while it's in use (`LevelClock.TickSource`) and resets it when it's disposed; a test that sets it without the rig must set it back to null.

**Level element.** A part of a level the character traverses or meets (water, a rope, a ladder, a moving platform, a timing gate, ...): a component in Assets/MeridianMotion/Runtime/LevelElements, the `MeridianMotion.Levels` assembly, which the character finds and uses but which knows nothing of the character. See [Climbing and Traversal](traversal.md) and [Test Levels](showcase/levels.md).

**Level spec.** *(showcase levels)* A level recorded as data: the list of pieces its builder called, with their settings, which builds the same level again. Edited in the Level Editor or by the levelspec tool. See [Level Specs](showcase/level-specs.md).

**Marker.** An object a level places only to be found later: a spawn marker ("Spawn Point (...)", named by `LevelFixtureSetup.SpawnMarkerPrefix`) becomes one of the spawner's spawn points when Set Up Scene runs, and a "(future)" marker shows where a mechanic is planned.

**Model source.** Where the character's model comes from (`CharacterModelSource`): the character asks its source for a model and gives it back. See [Character Model and Animations](animation.md).

**Movement component.** One part of the character's movement, with its own settings, in a movement mode's list (`MovementComponent`): walking and running, falling and air handling, and each ability. See [Movement Components](movement.md#movement-components).

**Movement events.** One-off happenings in a physics step (jumped, landed, a ledge grabbed): `MovementEvents`, which animation and other systems listen for.

**Movement mode.** The ScriptableObject asset that defines how characters move, made of movement components (`StandardMovementMode`, at Assets/MeridianMotion/Content/Movement/StandardMovementMode.asset). Shared by every character using it.

**Movement simulation.** A copy of the character stepped faster than real time in a world of its own, far below everything else (`MovementSimulation`), used to measure its capabilities.

**Piece.** *(showcase levels)* One kind of thing levels are built from (a platform, a stair, a railing), a method of `Graybox` marked `[Piece]`. A level spec is a list of pieces.

**Posture.** How the character's body is held (`BodyPosture`): standing; low, lowered to the crouch height while crouched, sliding or rolling, and raised again where there's room; or lying, laid flat along the way it faces while swimming underwater.

**Query buffer.** A reused buffer for a physics query that grows and runs the query again when it fills (`RaycastHitBuffer`, `ColliderBuffer`; `MeridianMotion.PhysicsQueries.QueryBuffer`), since Unity's results come in no order and a full one may leave out the nearest hit. Its own assembly, shared by the character and the camera. See [Automated Tests](testing.md) (`QueryBufferTests`).

**Rule (UI).** A `UIRule` asset a control can be gated on (shown, or usable, only while it's permitted), which the game permits or not (`RulesService.Set`). See [Rule-gated controls](ui.md#rule-gated-controls).

**Set Up Scene.** The editor tool that makes a level playable (`CharacterSceneSetup`): it adds the spawner, with the level's spawn markers as its spawn points, the input manager and the camera, and tells the course timers who's racing. It runs `LevelFixtureSetup` too, for fixtures not set up yet. See [Spawning the Character](spawning.md).

**Size scaling.** How the movement changes with the character's size: in proportion (a scaled copy) or naturally (as real animals do). The MMotionMotor's Size Scaling. See [Character Size](movement.md#character-size).

**Stable ID.** An ID for a level object that's the same on every machine that loads the level (`MeridianMotion.Levels.StableId`): a hash of its scene and path of names. What a character's written state names the ground it's on, or the rope it holds, by.

**Start Here.** The editor window (Tools > Meridian Motion > Start Here, `StartHereWindow`) with every everyday tool in sections (play it, characters, abilities, checking your work, learning), each with a line saying what it does, and the commands for the test and hygiene scripts. It opens by itself the first time a project is opened on a machine (its Open On First Load turns that off).

**State snapshot.** A character's movement state saved between steps, to be put back (`MMotionMotor.SaveState`, `RestoreState`: a `MovementStateSnapshot`, in this process only), with the level's ropes and pushables saved beside it (`WorldStateSnapshot`). `MovementStateSummary` is its compact, unmanaged part (pose, momentum, state, clock, latest input frame). `StateSerializer` writes either to bytes and reads them into a character or level built the same way, naming the level's objects by their stable IDs. See [Automated Tests](testing.md) (`DeterminismTests`).

**Step tick.** The character's own clock, in whole steps, which movement times things by, so a movement simulation stepped faster than real time behaves the same. It has two counts: `MMotionMotor.InputTick`, the steps the character has taken, which its input frames are tagged with; and `StepIndex`, the step under way (inside a step, one more than `InputTick`), which timing goes by (keep the tick something happened at and ask `SecondsSince`; `StepTime` is it in seconds). `SetClock` starts both anywhere. See [State and assets](architecture.md#state-and-assets).

**Step trace.** A record of a test run step by step (`StepTrace`): where the character was, how it was found on the ground, what collision did and how far its feet were above the ground under them, written to Logs/StepTraces by the ground contact tests. See [Testing](testing.md).

**Takeover.** An ability taking the character for a physics step: the first component in the list whose `TakeOver` says it has the character moves it, and the others wait.

**Test rig.** The PlayMode tests' harness (`CharacterTestRig`), which spawns the real character, steps its movement with scripted inputs, and gives it its own copy of the movement mode to change. See [Automated Tests](testing.md).

**Tuning preset.** A saved set of movement settings, some or all, to apply to a movement mode again (`MovementTuningPreset`). See [Tuning the Character](tuning.md#tuning-presets).

**Unity terms.** Prefab, prefab variant, ScriptableObject, inspector, Play mode, assembly, `.meta` file and the rest of Unity's own vocabulary, each tied to this project's objects, are in [Unity Basics](unity-basics.md).


**Window.** A piece of UI opened, closed and stacked by the UI Manager: a prefab with a `RootUIPrefab` on its root, on one of its canvases. See [Windows](ui.md#windows).

**Window catalog.** The list of a project's own window prefabs (`UIWindowCatalog`, named on the UI Manager), which `UIManager.Open<T>` finds a window in by its component. See [A window of your own](ui.md#a-window-of-your-own).
