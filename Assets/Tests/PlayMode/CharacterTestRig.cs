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
    public sealed partial class CharacterTestRig : IDisposable
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
            stepper = new GameObject("Test Stepper").AddComponent<FixedStepper>();
            created.Add(stepper.gameObject);

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

        public void SpawnCharacter(Vector3 position) => SpawnCharacter(position, null);

        /// <summary>Spawns the given character prefab (another than the one tested, a creature's, say), or the tested one if null.</summary>
        public void SpawnCharacter(Vector3 position, GameObject prefab)
        {
            prefab ??= CharacterPrefab();
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

        /// <summary>A prefab by its asset path (in the editor; null in a player).</summary>
        public static GameObject LoadPrefab(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
            return null;
#endif
        }

        /// <summary>The character prefab's body as authored (before it's spawned, to build a world around it).</summary>
        public static BodyShape PrefabBody() => CharacterPrefab().GetComponent<MovementController>().AuthoredBody;
        /// <summary>The character prefab's step height at its authored size (before it's spawned).</summary>
        public static float PrefabStepHeight() => CharacterPrefab().GetComponent<MovementController>().MaxStepHeight;

        // ---- The character's size and tuning, as it is now (at its current size) ----

        /// <summary>The character's body as it is now, at its size and posture (see PrimitiveBody.Current).</summary>
        public BodyShape BodyNow => ((PrimitiveBody)Movement.Body).Current;

        /// <summary>
        /// Gives the character a collider of its body's kind and shape as authored (a Body Collider, which its body keeps in
        /// shape from then on): the movement doesn't need one, so the character should move exactly as it did without
        /// it. After spawning it.
        /// </summary>
        public Collider AddBodyCollider()
        {
            BodyShape shape = Movement.AuthoredBody;
            var collider = (Collider)Character.AddComponent(shape.ColliderType);
            switch (collider)
            {
                case CapsuleCollider capsule:
                    capsule.direction = shape.direction;
                    capsule.radius = shape.radius;
                    capsule.height = shape.height;
                    capsule.center = shape.center;
                    break;
                case BoxCollider box:
                    box.size = shape.size;
                    box.center = shape.center;
                    break;
                case SphereCollider sphere:
                    sphere.radius = shape.radius;
                    sphere.center = shape.center;
                    break;
            }
            Movement.RebuildBody(collider);
            // A collider doesn't hold all of the body (its lying thickness): given it again, whole.
            Movement.SetBody(shape, checkRoom: false);
            Physics.SyncTransforms();
            return collider;
        }

        /// <summary>
        /// Takes the character's collider away (its Body Collider, which its body keeps in shape): the movement doesn't
        /// need it, so the character should move exactly as it did with it. After spawning it.
        /// </summary>
        public void RemoveCollider()
        {
            Collider collider = Movement.Body.Collider;
            Assert.That(collider, Is.Not.Null, "Test setup: the character should have a collider to take away.");
            Object.DestroyImmediate(collider);
            Physics.SyncTransforms();
        }

        /// <summary>Whether the character's body, as it is now, overlaps nothing (more than the skin width).</summary>
        public bool BodyFits => Movement.Body.Fits(Body.position, MovementController.Attitude(Body.rotation, Movement.BodyPitch), Movement.Scale, Movement.Posture);

        /// <summary>How far (m) the character's body reaches out in front of its feet (see CharacterBody.FrontExtent).</summary>
        public float FrontExtent => Movement.Body.FrontExtent;
        /// <summary>How far (m) the character's body reaches out behind its feet (see CharacterBody.BackExtent).</summary>
        public float BackExtent => Movement.Body.BackExtent;
        /// <summary>How far (m) the character's body reaches out to either side of its feet (see CharacterBody.SideExtent).</summary>
        public float SideExtent => Movement.Body.SideExtent;
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

        /// <summary>Destroys the object with the rig's world, when the test is done (for objects a test makes itself).</summary>
        public T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
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

        /// <summary>The character's body's height now, in metres (lower than its standing height while crouched).</summary>
        public float CurrentHeight => Movement.Body.Height;
        /// <summary>How tall the character stands, in metres at its size (see CharacterBody.StandingHeight).</summary>
        public float StandingHeight => Movement.Body.StandingHeight;
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
        public IEnumerator SpawnSettled(Vector3 position, float settleSeconds = 0.3f) => SpawnSettled(position, null, settleSeconds);

        /// <summary>The same, for the given character prefab (the tested one if null).</summary>
        public IEnumerator SpawnSettled(Vector3 position, GameObject prefab, float settleSeconds = 0.3f)
        {
            SpawnCharacter(position, prefab);
            yield return WaitForModel();
            if (settleSeconds > 0f) { yield return Run(settleSeconds, _ => Idle()); }
        }

        /// <summary>
        /// Moves the character's feet to the given position at once, between steps (for placing it where its own size
        /// decides, which isn't known until it's spawned and its model has loaded). See MovementController.Teleport.
        /// </summary>
        public void Teleport(Vector3 feet)
        {
            Movement.Teleport(feet);
            Physics.SyncTransforms();
        }

        /// <summary>Sets the character moving at the given velocity (for one step, before its movement takes over), with the given input.</summary>
        public IEnumerator Launch(Vector3 velocity, Inputs inputs)
        {
            Movement.OverrideVelocity(velocity);
            yield return Run(1, _ => inputs);
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
