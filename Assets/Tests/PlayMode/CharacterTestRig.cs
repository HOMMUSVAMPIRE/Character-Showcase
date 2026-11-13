using System;
using System.Collections;
using System.Collections.Generic;
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
            Time.timeScale = 4f;

            // MovementController makes inputs relative to Camera.main.
            var camera = new GameObject("Test Camera") { tag = "MainCamera" };
            camera.AddComponent<Camera>();
            created.Add(camera);
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
            // The character moves with its own copy of its movement mode, so a test can change settings on it (see
            // SetMovementSetting) without changing the asset: in the editor, a change to the asset itself would stay.
            // The copy starts from the asset, so the tests still check the movement as it's tuned.
            if (Movement.movementHandler != null)
            {
                Movement.movementHandler = Object.Instantiate(Movement.movementHandler);
                created.Add(Movement.movementHandler);
            }
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
        /// <summary>The character's walking speed in m/s at full stick (WalkRunMode), at its size.</summary>
        public float WalkSpeed => Mode<WalkRunMode>().walkSpeed * Movement.SpeedScale;
        /// <summary>The character's sprinting speed (its run speed) at its current size.</summary>
        public float RunSpeed => Mode<WalkRunMode>().runSpeed * Movement.SpeedScale;
        /// <summary>Seconds to walk the given distance at full stick, plus a margin for speeding up and settling.</summary>
        public float SecondsToWalk(float metres, float margin = 0.5f) => metres / WalkSpeed + margin;

        /// <summary>
        /// The character's movement mode as the given interface (WalkRunMode, JumpMode, DashMode). Ignores the test if it
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

        /// <summary>A large flat floor whose top surface is at the given height, 100 m across unless given a width.</summary>
        public GameObject Floor(float topHeight = 0f, float width = 100f) =>
            Box("Floor", new Vector3(0f, topHeight - 0.5f, 0f), new Vector3(width, 1f, width));

        /// <summary>A box the character can grab, push and pull (a PushableObject), resting with its bottom at center.y - size.y / 2.</summary>
        public PushableObject PushableBox(Vector3 center, Vector3 size, float speedMultiplier = 1f, bool canBePulled = true)
        {
            GameObject box = Box("Pushable Box", center, size);
            box.AddComponent<Rigidbody>();
            var pushable = box.AddComponent<PushableObject>();
            pushable.SpeedMultiplier = speedMultiplier;
            pushable.CanBePulled = canBePulled;
            return pushable;
        }

        public MovingPlatform Platform(Vector3 topCenter, Vector3 size, Vector3 travel, float period, Vector3 spin)
        {
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
            for (int i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();
                // The previous step's Rigidbody.Move has now been simulated, so Position reflects it.
                if (i > 0) { afterStep?.Invoke(i - 1); }
                // As ACCharacterController does each physics step: any change of size first, then movement.
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
        /// cooldown, say) from the character's movement mode by name. Ignores the test if the mode has no such value: it
        /// tests a behaviour of BasicMovementMode, which another mode may not have.
        /// </summary>
        public T MovementSetting<T>(string fieldName)
        {
            object handler = Movement.movementHandler;
            FieldInfo field = handler?.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || field.FieldType != typeof(T))
            {
                Assert.Ignore($"The character's movement mode ({handler?.GetType().Name ?? "none"}) has no {typeof(T).Name} '{fieldName}' setting.");
            }
            return (T)field.GetValue(handler);
        }

        /// <summary>
        /// Changes a setting on the character's movement mode by name, for this test only: the mode is the rig's own copy
        /// of the asset (see SpawnCharacter). Ignores the test if the mode has no such setting, as MovementSetting does.
        /// </summary>
        public void SetMovementSetting<T>(string fieldName, T value)
        {
            object handler = Movement.movementHandler;
            FieldInfo field = handler?.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || field.FieldType != typeof(T))
            {
                Assert.Ignore($"The character's movement mode ({handler?.GetType().Name ?? "none"}) has no {typeof(T).Name} '{fieldName}' setting.");
            }
            field.SetValue(handler, value);
        }

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
