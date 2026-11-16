using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Character;
using Level;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Builds a tiny physics world around a character prefab and drives its MovementController directly with scripted
    /// inputs, one call per physics step, so tests don't depend on input devices or the input manager. The prefab is
    /// the one a CharacterTestSettings asset names, or else the package's own. Tests size their worlds from the
    /// character (its capsule, step height, slope limit and speeds, below), so they hold for any character's tuning.
    /// </summary>
    public sealed class CharacterTestRig : IDisposable
    {
        private const string DefaultCharacterPrefabPath = "Assets/Prefabs/Character.prefab";

        private readonly List<Object> created = new();
        private readonly float previousTimeScale;

        /// <summary>
        /// How much faster than real time Unity's physics steps run while the rig waits for them (see StepsPhysicsItself
        /// for when it doesn't): the steps stay the same size, so only how long a test takes changes. The fastest the whole
        /// suite passes at reliably (at twice this, a model test's per-frame updates fall too far behind its steps).
        /// </summary>
        public const float TimeScale = 50f;

        /// <summary>
        /// The most game time (s) one frame may cover while the rig waits for Unity's steps (see FrameTimeLimit): what's
        /// done once a frame (animation, IK, foot placement) is checked against the steps, and needs frames about as small
        /// as batch mode's at TimeScale. Sped up as much, a frame of the editor's would cover most of a second or more.
        /// </summary>
        public const float MaxFrameTime = 0.05f;

        public GameObject Character { get; private set; }
        public MovementController Movement { get; private set; }
        public Rigidbody Body { get; private set; }

        /// <summary>The character's physics position (its feet), not the interpolated render position.</summary>
        public Vector3 Position => Body.position;
        public bool Grounded => Movement.lastFrameMovementData != null
                                && Movement.lastFrameMovementData.state.HasFlag(CharacterState.Grounded);

        public CharacterTestRig()
        {
            // Run the simulation faster than real time. Physics steps stay the same size, so results are unchanged.
            previousTimeScale = Time.timeScale;
            created.Add(FrameTimeLimit.Start(TimeScale, MaxFrameTime).gameObject);

            // MovementController makes inputs relative to Camera.main.
            var camera = new GameObject("Test Camera") { tag = "MainCamera" };
            camera.AddComponent<Camera>();
            created.Add(camera);
        }

        /// <summary>A floor, and the character standing on it at the origin, its model loaded and settled.</summary>
        public IEnumerator StandOnFloor()
        {
            Floor();
            yield return SpawnSettled(new Vector3(0f, 0.05f, 0f));
        }

        public void SpawnCharacter(Vector3 position)
        {
            GameObject prefab = CharacterPrefab();
            if (prefab == null) { throw new InvalidOperationException($"Could not load the character prefab ('{DefaultCharacterPrefabPath}', or a CharacterTestSettings asset's)."); }

            Character = Object.Instantiate(prefab, position, Quaternion.identity);
            created.Add(Character);
            // The tests drive movement themselves; the controller would otherwise read the (absent) input manager.
            Character.GetComponent<ACCharacterController>().enabled = false;
            Movement = Character.GetComponent<MovementController>();
            Body = Character.GetComponent<Rigidbody>();
            ClearEvents();
            Movement.MovementEventRaised += (movementEvent, _) =>
            {
                events.Add((runStep, movementEvent));
                eventsRaised.Add(movementEvent);
            };
            // The character moves with its own copy of its movement mode, so a test can change settings on it (see
            // SetMovementSetting) without changing the asset: in the editor, a change to the asset itself would stay.
            // The copy starts from the asset, so the tests still check the movement as it's tuned.
            if (Movement.movementHandler != null)
            {
                Movement.movementHandler = Object.Instantiate(Movement.movementHandler);
                created.Add(Movement.movementHandler);
            }
        }

        // ---- The character's movement events ----

        // Every movement event since the character was spawned (or ClearEvents), with the step of the latest Run it was
        // raised on (counted from 0, for StepOf); and that step, as the rig runs.
        private readonly List<(int step, MovementEvents movementEvent)> events = new();
        private readonly List<MovementEvents> eventsRaised = new();
        private int runStep;

        /// <summary>The movement events raised since the character was spawned (or ClearEvents), in order: a live list.</summary>
        public IReadOnlyList<MovementEvents> EventsRaised => eventsRaised;

        /// <summary>How many times the event has been raised since the character was spawned (or ClearEvents).</summary>
        public int Count(MovementEvents movementEvent) => events.Count(e => e.movementEvent == movementEvent);

        /// <summary>The step (of the latest Run) the event was first raised on, or -1 if it hasn't been.</summary>
        public int StepOf(MovementEvents movementEvent)
        {
            foreach (var (step, raised) in events) { if (raised == movementEvent) { return step; } }
            return -1;
        }

        /// <summary>Forgets the events recorded so far, to record from here on.</summary>
        public void ClearEvents()
        {
            events.Clear();
            eventsRaised.Clear();
        }

        /// <summary>The character prefab the tests run on: a CharacterTestSettings asset's, or the package's own.</summary>
        public static GameObject CharacterPrefab()
        {
#if UNITY_EDITOR
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets($"t:{nameof(CharacterTestSettings)}"))
            {
                var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterTestSettings>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (settings != null && settings.characterPrefab != null) { return settings.characterPrefab; }
            }
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DefaultCharacterPrefabPath);
#else
            return null;
#endif
        }

        /// <summary>The character prefab's capsule (before it's spawned, to build a world around it).</summary>
        public static CapsuleCollider PrefabCapsule() => CharacterPrefab().GetComponent<CapsuleCollider>();
        /// <summary>The character prefab's step height at its authored size (before it's spawned).</summary>
        public static float PrefabStepHeight() => CharacterPrefab().GetComponent<MovementController>().MaxStepHeight;

        // ---- The character's size and tuning, as it is now (at its current size) ----

        public CapsuleCollider Capsule => Character.GetComponent<CapsuleCollider>();
        /// <summary>The capsule's height in metres.</summary>
        public float Height => Capsule.height * Character.transform.lossyScale.y;
        /// <summary>The capsule's radius in metres.</summary>
        public float Radius => Capsule.radius * Mathf.Max(Character.transform.lossyScale.x, Character.transform.lossyScale.z);
        /// <summary>The tallest step the character climbs, in metres.</summary>
        public float StepHeight => Movement.MaxStepHeight * Movement.HeightScale;
        /// <summary>The steepest walkable slope, in degrees.</summary>
        public float MaxSlopeAngle => Movement.MaxSlopeAngle;
        /// <summary>The character's walking speed in m/s at full stick (Walking &amp; Running's), at its size.</summary>
        public float WalkSpeed => Component<WalkingAndRunning>().WalkSpeed * Movement.SpeedScale;
        /// <summary>The character's sprinting speed (its run speed) at its current size.</summary>
        public float RunSpeed => Component<WalkingAndRunning>().RunSpeed * Movement.SpeedScale;
        /// <summary>How many jumps the character gets before landing (its Jumping's Max Jumps; 0 with Jumping turned off).</summary>
        public int MaxJumps => MovementHelpers.MaxJumps(Movement);
        /// <summary>Seconds to walk the given distance at full stick, plus a margin for speeding up and settling.</summary>
        public float SecondsToWalk(float metres, float margin = 0.5f) => metres / WalkSpeed + margin;

        /// <summary>
        /// The character's movement mode as the given interface or type (ComposedMovementMode, BasicMovementMode). Ignores the test if it
        /// isn't one: a test of dashing has nothing to test on a character that can't dash.
        /// </summary>
        public T Mode<T>() where T : class
        {
            if (Movement.movementHandler is T mode) { return mode; }
            Assert.Ignore($"The character's movement mode ({Movement.movementHandler?.GetType().Name ?? "none"}) isn't a {typeof(T).Name}.");
            return null;
        }

        /// <summary>The character's AnimationController, which animates its model.</summary>
        public AnimationController Animation => Character.GetComponent<AnimationController>();
        /// <summary>The character's ACCharacterController (disabled: the rig steps its movement and scale itself).</summary>
        public ACCharacterController Controller => Character.GetComponent<ACCharacterController>();
        /// <summary>The character's CharacterModelHost, which loads its model at runtime.</summary>
        public CharacterModelHost Models => Character.GetComponent<CharacterModelHost>();

        /// <summary>Waits (in real frames, not physics steps) for the character's model to finish loading.</summary>
        public IEnumerator WaitForModel(float timeoutSeconds = 10f)
        {
            float giveUp = Time.realtimeSinceStartup + timeoutSeconds;
            while (!Models.IsModelReady)
            {
                if (Time.realtimeSinceStartup > giveUp) { throw new TimeoutException("The character's model didn't load."); }
                yield return null;
            }
        }

        /// <summary>
        /// Waits (in real frames) for an Awaitable to finish, so coroutine tests can wait on async methods, and passes
        /// on its result.
        /// </summary>
        public static IEnumerator WaitFor<T>(Awaitable<T> awaitable, Action<T> result = null, float timeoutSeconds = 10f)
        {
            Awaitable<T>.Awaiter awaiter = awaitable.GetAwaiter();
            float giveUp = Time.realtimeSinceStartup + timeoutSeconds;
            while (!awaiter.IsCompleted)
            {
                if (Time.realtimeSinceStartup > giveUp) { throw new TimeoutException("The awaited operation didn't finish."); }
                yield return null;
            }
            T value = awaiter.GetResult();
            result?.Invoke(value);
        }

        public GameObject Box(string name, Vector3 center, Vector3 size, Quaternion? rotation = null)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetPositionAndRotation(center, rotation ?? Quaternion.identity);
            box.transform.localScale = size;
            created.Add(box);
            return box;
        }

        /// <summary>A box the character can climb (a ClimbableSurface), of the given climbing speed multiplier.</summary>
        public ClimbableSurface ClimbableBox(string name, Vector3 center, Vector3 size, Quaternion? rotation = null, float speedMultiplier = 1f)
        {
            var surface = Box(name, center, size, rotation).AddComponent<ClimbableSurface>();
            surface.SpeedMultiplier = speedMultiplier;
            return surface;
        }

        /// <summary>
        /// A ladder of the given height standing at bottom (the middle of its foot), facing -Z (climbed from that side,
        /// toward +Z, as the test camera looks), unless turned otherwise. It builds its own mesh and collider.
        /// </summary>
        public Ladder Ladder(Vector3 bottom, float height, Quaternion? rotation = null, float width = 0.6f)
        {
            var obj = new GameObject("Ladder");
            created.Add(obj);
            obj.transform.SetPositionAndRotation(bottom, rotation ?? Quaternion.Euler(0f, 180f, 0f));
            var ladder = obj.AddComponent<Ladder>();
            ladder.Height = height;
            ladder.Width = width;
            ladder.Rebuild();
            return ladder;
        }

        /// <summary>A bounce pad: a box of the given size whose top is at topCenter, launching characters the given height.</summary>
        public BouncePad BouncePad(Vector3 topCenter, Vector3 size, float launchHeight, Quaternion? rotation = null)
        {
            Quaternion turn = rotation ?? Quaternion.identity;
            GameObject box = Box("Bounce Pad", topCenter - turn * Vector3.up * (size.y / 2f), size, turn);
            var pad = box.AddComponent<BouncePad>();
            pad.LaunchHeight = launchHeight;
            return pad;
        }

        /// <summary>A zipline from start to end (world space), drawing its own cable, with the given top speed.</summary>
        public Zipline Zipline(Vector3 start, Vector3 end, float maxSpeed = 16f)
        {
            var obj = new GameObject("Zipline");
            created.Add(obj);
            obj.transform.position = start;
            var line = obj.AddComponent<Zipline>();
            line.End = end;
            line.MaxSpeed = maxSpeed;
            line.Rebuild();
            return line;
        }

        /// <summary>A rope hanging from anchor, the given length, drawing itself.</summary>
        public Rope Rope(Vector3 anchor, float length)
        {
            needsUnityPhysicsSteps = true;
            var obj = new GameObject("Rope");
            created.Add(obj);
            obj.transform.position = anchor;
            var rope = obj.AddComponent<Rope>();
            rope.Length = length;
            rope.Rebuild();
            return rope;
        }

        /// <summary>
        /// A body of water whose surface is at surfaceCentre, size across (x), deep (y) and long (z), drawing its own
        /// surface. It has no collider: build what's under it (a floor) separately.
        /// </summary>
        public Water Water(Vector3 surfaceCentre, Vector3 size)
        {
            var obj = new GameObject("Water");
            created.Add(obj);
            obj.transform.position = surfaceCentre;
            var water = obj.AddComponent<Water>();
            water.Size = size;
            water.Rebuild();
            return water;
        }

        /// <summary>A large flat floor whose top surface is at the given height, 100 m across unless given a width.</summary>
        public GameObject Floor(float topHeight = 0f, float width = 100f) =>
            Box("Floor", new Vector3(0f, topHeight - 0.5f, 0f), new Vector3(width, 1f, width));

        /// <summary>A box the character can grab, push and pull (a PushableObject), resting with its bottom at center.y - size.y / 2.</summary>
        public PushableObject PushableBox(Vector3 center, Vector3 size, float speedMultiplier = 1f, bool canBePulled = true)
        {
            needsUnityPhysicsSteps = true;
            GameObject box = Box("Pushable Box", center, size);
            box.AddComponent<Rigidbody>();
            var pushable = box.AddComponent<PushableObject>();
            pushable.SpeedMultiplier = speedMultiplier;
            pushable.CanBePulled = canBePulled;
            return pushable;
        }

        public MovingPlatform Platform(Vector3 topCenter, Vector3 size, Vector3 travel, float period, Vector3 spin)
        {
            needsUnityPhysicsSteps = true;
            GameObject box = Box("Platform", topCenter - Vector3.up * (size.y / 2f), size);
            box.AddComponent<Rigidbody>();
            var platform = box.AddComponent<MovingPlatform>();
            platform.travel = travel;
            platform.period = period;
            platform.spinDegreesPerSecond = spin;
            platform.smooth = false;
            platform.pauseAtEnds = 0f;
            return platform;
        }

        /// <summary>
        /// Runs the character for a number of physics steps. inputsForStep is called with the step index;
        /// afterStep is called after each step's movement has been simulated by physics.
        /// </summary>
        public IEnumerator Run(int steps, Func<int, Inputs> inputsForStep, Action<int> afterStep = null)
        {
            if (CanStepPhysicsItself)
            {
                RunDirectly(steps, inputsForStep, afterStep);
                yield break;
            }
            for (int i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();
                // The previous step's Rigidbody.Move has now been simulated, so Position reflects it.
                if (i > 0) { afterStep?.Invoke(i - 1); }
                // As ACCharacterController does each physics step: any change of size first, then movement.
                runStep = i;
                Controller.UpdateScale(Time.fixedDeltaTime);
                Movement.ProcessMovementInputs(inputsForStep(i));
                Movement.MovementCleanup();
            }
            yield return new WaitForFixedUpdate();
            afterStep?.Invoke(steps - 1);
        }

        public IEnumerator Run(float seconds, Func<int, Inputs> inputsForStep, Action<int> afterStep = null) =>
            Run(StepsFor(seconds), inputsForStep, afterStep);

        /// <summary>
        /// Whether the rig may step the physics itself (Physics.Simulate, in a loop) rather than wait for each of
        /// Unity's physics steps, which takes real time even sped up: far faster, and the same steps, since the movement
        /// times everything by its own clock (MovementController.StepTime). For tests of the movement alone: a test that
        /// needs Unity's frames (animation, IK, the model's or camera's per-frame updates) leaves it off. Even on, the rig
        /// waits for Unity's steps while the world has something that moves in them (a moving platform, a pushable
        /// object, a rope). It does what Unity's steps would for the character itself (fitting its collider to its model).
        /// </summary>
        public bool StepsPhysicsItself { get; set; }

        // Whether the world has something only Unity's own physics steps move (made by Platform, PushableBox or Rope).
        private bool needsUnityPhysicsSteps;

        private bool CanStepPhysicsItself => StepsPhysicsItself && !needsUnityPhysicsSteps && Character != null;

        /// <summary>Runs the steps at once, stepping the physics itself (see StepsPhysicsItself), in the same order as Run.</summary>
        private void RunDirectly(int steps, Func<int, Inputs> inputsForStep, Action<int> afterStep)
        {
            SimulationMode previous = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            try
            {
                // Objects made or moved since the last step reach the physics scene first, as Unity's own step would.
                Physics.SyncTransforms();
                CharacterModelHost models = Models;
                for (int i = 0; i < steps; i++)
                {
                    runStep = i;
                    Controller.UpdateScale(Time.fixedDeltaTime);
                    Movement.ProcessMovementInputs(inputsForStep(i));
                    Movement.MovementCleanup();
                    // As the model host's FixedUpdate would, before the physics step.
                    if (models != null) { models.TryFitColliderNow(); }
                    Physics.Simulate(Time.fixedDeltaTime);
                    afterStep?.Invoke(i);
                }
            }
            finally
            {
                Physics.simulationMode = previous;
            }
        }

        /// <summary>
        /// Runs the character until done says so (checked after each step) or the timeout passes, so a test waits as
        /// long as the movement takes, however it's tuned, rather than a time chosen for one tuning. Doesn't fail by
        /// itself on the timeout: assert what should then be true.
        /// </summary>
        public IEnumerator RunUntil(Func<bool> done, Func<int, Inputs> inputsForStep, Action<int> afterStep = null, float timeoutSeconds = DefaultTimeout)
        {
            for (int step = 0; step < StepsFor(timeoutSeconds) && !done(); step++)
            {
                int i = step;
                // Stepping the physics itself, the steps run back to back: a yield costs a frame however little it waits
                // for, and in the editor a frame is long.
                if (CanStepPhysicsItself) { RunDirectly(1, _ => inputsForStep(i), _ => afterStep?.Invoke(i)); }
                else { yield return Run(1, _ => inputsForStep(i), _ => afterStep?.Invoke(i)); }
            }
        }

        /// <summary>
        /// Runs the character, without input, until it's on the ground (after a jump, a fall or a slide), or the timeout
        /// passes; afterStep, if given, is called after each step.
        /// </summary>
        public IEnumerator RunUntilLanded(Action<int> afterStep = null, float timeoutSeconds = DefaultTimeout) =>
            RunUntil(() => Grounded, _ => Idle(), afterStep, timeoutSeconds);

        /// <summary>
        /// Runs the character with the given inputs until it has left the ground and landed again (a jump, from the
        /// ground), or the timeout passes; afterStep, if given, is called after each step.
        /// </summary>
        public IEnumerator RunUntilBackOnTheGround(Func<int, Inputs> inputsForStep, Action<int> afterStep = null, float timeoutSeconds = DefaultTimeout)
        {
            bool left = false;
            yield return RunUntil(() => left && Grounded, inputsForStep, step => { left |= !Grounded; afterStep?.Invoke(step); }, timeoutSeconds);
        }

        /// <summary>How long (s) RunUntil waits at most by default: far longer than any move should take.</summary>
        public const float DefaultTimeout = 10f;

        /// <summary>The character's horizontal speed (m/s), averaged over the steps of the given time with the given inputs, passed to result.</summary>
        public IEnumerator MeanSpeed(float seconds, Func<int, Inputs> inputsForStep, Action<float> result)
        {
            float total = 0f;
            int steps = 0;
            yield return Run(seconds, inputsForStep, _ => { total += HorizontalSpeed; steps++; });
            result(steps > 0 ? total / steps : 0f);
        }

        /// <summary>
        /// Physics steps covering the given time. The small tolerance stops float division (0.6 / 0.02 = 30.000002)
        /// from adding a whole extra step.
        /// </summary>
        public static int StepsFor(float seconds) => Mathf.CeilToInt(seconds / Time.fixedDeltaTime - 1e-3f);

        public static Inputs Idle() => Move(Vector3.zero);

        /// <summary>Movement input in world space (the test camera looks down +Z).</summary>
        public static Inputs Move(Vector3 direction) => new()
        {
            movementVector = Vector3.ClampMagnitude(new Vector3(direction.x, 0f, direction.z), 1f),
            jump = new InputButton(),
            dash = new InputButton(),
        };

        /// <summary>Movement input in world space with Sprint held (on the given step of the press; 0 = the step it was pressed).</summary>
        public static Inputs Sprint(Vector3 direction, int heldSteps = 1)
        {
            Inputs inputs = Move(direction);
            inputs.sprint = new InputButton
            {
                value = true,
                justPressed = heldSteps == 0,
                holdDuration = heldSteps * Time.fixedDeltaTime,
            };
            return inputs;
        }

        /// <summary>Jump held for the given step of the press (0 = the step it was pressed).</summary>
        public static Inputs Jump(int heldSteps, Vector3 direction = default)
        {
            Inputs inputs = Move(direction);
            inputs.jump = new InputButton
            {
                value = true,
                justPressed = heldSteps == 0,
                holdDuration = heldSteps * Time.fixedDeltaTime,
            };
            return inputs;
        }

        /// <summary>Dash held for the given step of the press (0 = the step it was pressed), with optional movement input.</summary>
        public static Inputs Dash(int heldSteps, Vector3 direction = default)
        {
            Inputs inputs = Move(direction);
            inputs.dash = new InputButton
            {
                value = true,
                justPressed = heldSteps == 0,
                holdDuration = heldSteps * Time.fixedDeltaTime,
            };
            return inputs;
        }

        public bool Dashing => Movement.lastFrameMovementData != null
                               && Movement.lastFrameMovementData.state.HasFlag(CharacterState.Dashing);

        /// <summary>Whether the character was in the given state (crouching, sliding, wall-running...) in the last step.</summary>
        public bool InState(CharacterState state) => Movement.lastFrameMovementData != null
                                                     && Movement.lastFrameMovementData.state.HasFlag(state);

        /// <summary>The character's horizontal speed in the last step, in m/s.</summary>
        public float HorizontalSpeed
        {
            get
            {
                if (Movement.lastFrameMovementData == null) { return 0f; }
                Vector3 v = Movement.lastFrameMovementData.finalVelocity;
                return new Vector2(v.x, v.z).magnitude;
            }
        }

        /// <summary>The character's collider height now, in metres (lower than its standing height while crouched).</summary>
        public float CurrentHeight => Capsule.height;
        /// <summary>The character's collider height standing up, in metres at its size.</summary>
        public float StandingHeight => Movement.StandingHeight;
        /// <summary>The character's velocity (m/s) as its last step left it.</summary>
        public Vector3 Velocity => Movement.lastFrameMovementData.finalVelocity;
        /// <summary>The character's gravity (m/s²) at its size: Fall &amp; Air Handling's Gravity Strength, scaled.</summary>
        public float Gravity => MovementSetting<float>("gravityStrength") * Movement.AccelerationScale;
        /// <summary>How low (as a share of its standing height) the character crouches: Crouching's Crouch Height.</summary>
        public float CrouchHeight => Component<Crouching>().CrouchHeight;
        /// <summary>How fast (m/s) the character climbs a climbable surface at full input, at its size.</summary>
        public float ClimbSpeed => Component<WallClimbing>().WallClimbSpeed * Movement.SpeedScale;
        /// <summary>How high (m) the character's hands hold a climbable surface or ladder above its feet, at its size.</summary>
        public float WallClimbGripHeight => MovementSetting<float>("wallClimbGripHeight") * StandingHeight;
        /// <summary>How far (m) below a zipline's cable the character's feet hang, at its size.</summary>
        public float ZiplineHangHeight => MovementSetting<float>("ziplineHangHeight") * StandingHeight;
        /// <summary>Whether the character is riding a zipline.</summary>
        public bool Ziplining => InState(CharacterState.Ziplining);
        /// <summary>How far (m) below the water's surface the character's feet hang as it swims, at its size.</summary>
        public float SwimDepth => MovementSetting<float>("swimDepth") * StandingHeight;
        /// <summary>Whether the character is swimming.</summary>
        public bool Swimming => InState(CharacterState.Swimming);

        /// <summary>
        /// Spawns the character, waits for its model to load (which fits its collider to the model, so its size is
        /// settled before a test builds a world around it), then lets it stand for a moment. With no settling time, the
        /// character stays exactly where it was spawned (in mid-air, say), as nothing moves it until the test runs it.
        /// </summary>
        public IEnumerator SpawnSettled(Vector3 position, float settleSeconds = 0.3f)
        {
            SpawnCharacter(position);
            yield return WaitForModel();
            if (settleSeconds > 0f) { yield return Run(settleSeconds, _ => Idle()); }
        }

        /// <summary>
        /// Moves the character's feet to the given position at once, between steps (for placing it where its own size
        /// decides, which isn't known until it's spawned and its model has loaded).
        /// </summary>
        public void Teleport(Vector3 feet)
        {
            Body.position = feet;
            Character.transform.position = feet;
            Physics.SyncTransforms();
        }

        /// <summary>Sets the character moving at the given velocity (for one step, before its movement takes over), with the given input.</summary>
        public IEnumerator Launch(Vector3 velocity, Inputs inputs)
        {
            Movement.OverrideVelocity(velocity);
            yield return Run(1, _ => inputs);
        }

        /// <summary>A button held for the given step of its press (0 = the step it was pressed).</summary>
        public static InputButton Held(int heldSteps) => new()
        {
            value = true,
            justPressed = heldSteps == 0,
            holdDuration = heldSteps * Time.fixedDeltaTime,
        };

        /// <summary>Crouch held for the given step of the press (0 = the step it was pressed), with optional movement input.</summary>
        public static Inputs Crouch(int heldSteps, Vector3 direction = default)
        {
            Inputs inputs = Move(direction);
            inputs.crouch = Held(heldSteps);
            return inputs;
        }

        /// <summary>Roll held for the given step of the press (0 = the step it was pressed), with optional movement input.</summary>
        public static Inputs Roll(int heldSteps, Vector3 direction = default)
        {
            Inputs inputs = Move(direction);
            inputs.roll = Held(heldSteps);
            return inputs;
        }

        /// <summary>Grab held for the given step of the press (0 = the step it was pressed), with optional movement input.</summary>
        public static Inputs Grab(int heldSteps, Vector3 direction = default)
        {
            Inputs inputs = Move(direction);
            inputs.grab = Held(heldSteps);
            return inputs;
        }

        /// <summary>
        /// Reads a tuning value the movement mode interfaces don't have (BasicMovementMode's gravity, coyote time and dash
        /// cooldown, say) from the character's movement mode, or one of its components, by name (setting names are unique
        /// across a mode and its components). Ignores the test if the mode has no such value: it tests a behaviour of
        /// BasicMovementMode, which another mode may not have.
        /// </summary>
        public T MovementSetting<T>(string fieldName)
        {
            (object owner, FieldInfo field) = FindSetting<T>(fieldName);
            return (T)field.GetValue(owner);
        }

        /// <summary>
        /// Changes a setting on the character's movement mode, or one of its components, by name, for this test only: the
        /// mode is the rig's own copy of the asset (see SpawnCharacter). Ignores the test if the mode has no such setting,
        /// as MovementSetting does.
        /// </summary>
        public void SetMovementSetting<T>(string fieldName, T value)
        {
            (object owner, FieldInfo field) = FindSetting<T>(fieldName);
            field.SetValue(owner, value);
        }

        private (object owner, FieldInfo field) FindSetting<T>(string fieldName)
        {
            const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            object handler = Movement.movementHandler;
            var owners = new List<object> { handler };
            if (handler is ComposedMovementMode composed) { owners.AddRange(composed.Components.Where(c => c != null)); }
            foreach (object owner in owners)
            {
                for (Type type = owner?.GetType(); type != null; type = type.BaseType)
                {
                    FieldInfo field = type.GetField(fieldName, Fields | BindingFlags.DeclaredOnly);
                    if (field != null && field.FieldType == typeof(T)) { return (owner, field); }
                }
            }
            Assert.Ignore($"The character's movement mode ({handler?.GetType().Name ?? "none"}) has no {typeof(T).Name} '{fieldName}' setting.");
            return default;
        }

        /// <summary>
        /// The character's movement mode's component of type T (the rig's own copy of it, for this test only), turned on
        /// or not. Ignores the test if the mode has none: a test of riding ziplines has nothing to test on a character
        /// that can't.
        /// </summary>
        public T Component<T>() where T : MovementComponent
        {
            T component = (Movement.movementHandler as ComposedMovementMode)?.Get<T>(includeDisabled: true);
            if (component == null) { Assert.Ignore($"The character's movement mode has no {typeof(T).Name} component."); }
            return component;
        }

        /// <summary>The rig's own copy of the character's movement mode, as one made of components (ignores the test if it isn't).</summary>
        public ComposedMovementMode ComposedMode => Mode<ComposedMovementMode>();

        public void Dispose()
        {
            foreach (Object obj in created)
            {
                if (obj != null) { Object.Destroy(obj); }
            }
            created.Clear();
            Time.timeScale = previousTimeScale;
        }
    }
}
