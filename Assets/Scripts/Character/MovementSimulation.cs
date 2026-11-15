using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Character
{
    /// <summary>
    /// A copy of a character's movement, run in a world of its own, faster than real time, to measure what it does (see
    /// MovementController.Capabilities). The copy is a bare collider and MovementController with the character's settings,
    /// movement mode, collider and size, and none of its state; the world is a large floor far below everything else, and
    /// whatever a measurement adds to it (walls, ledges). It's stepped one physics step at a time with scripted inputs,
    /// moving exactly as the character would (the movement is deterministic: a fixed step, no randomness), but without
    /// rendering, animation or waiting for real time. Works in edit mode too. Dispose of it when done: it removes all it made.
    ///
    /// Positions are relative to the floor's top middle (the simulation's origin), with +Z forward: inputs are in that
    /// space (the simulation is its own input camera, looking along +Z).
    /// </summary>
    public sealed class MovementSimulation : IDisposable
    {
        // Far below anything a level puts anywhere, and near enough to the origin that floats stay fine-grained.
        private static readonly Vector3 WorldOrigin = new(0f, -2000f, 0f);
        private const float FloorSize = 1000f;

        private readonly MovementController source;
        private readonly float? baseRadius, baseHeight;
        private readonly GameObject root;
        private readonly List<GameObject> obstacles = new();
        private readonly List<Object> modes = new();
        private GameObject character;
        private Rigidbody body;

        /// <summary>The copy of the character's movement being stepped (made again by Reset).</summary>
        public MovementController Controller { get; private set; }
        /// <summary>The movement events the last step raised.</summary>
        public MovementEvents LastEvents { get; private set; }
        /// <summary>The character's feet, relative to the origin.</summary>
        public Vector3 Position => body.position - WorldOrigin;
        /// <summary>The character's state in the last step.</summary>
        public CharacterState State => Controller.lastFrameMovementData != null ? Controller.lastFrameMovementData.state : CharacterState.Idle;
        public bool Grounded => (State & CharacterState.Grounded) != 0;
        /// <summary>The character's velocity in the last step (m/s).</summary>
        public Vector3 Velocity => Controller.lastFrameMovementData != null ? Controller.lastFrameMovementData.finalVelocity : Vector3.zero;
        /// <summary>The physics step (s).</summary>
        public static float DeltaTime => Time.fixedDeltaTime;

        /// <summary>
        /// A world to simulate source's movement in. Its collider is the one source has, unless an authored (unscaled) radius
        /// and height are given: the collider a model would give it (see CharacterModelHost.ColliderFor).
        /// </summary>
        public MovementSimulation(MovementController source, float? authoredRadius = null, float? authoredHeight = null)
        {
            this.source = source;
            baseRadius = authoredRadius;
            baseHeight = authoredHeight;
            root = new GameObject("Movement Simulation") { hideFlags = HideFlags.HideAndDontSave };
            root.transform.position = WorldOrigin;
            Box(new Vector3(0f, -0.5f, 0f), new Vector3(FloorSize, 1f, FloorSize));
            Reset(Vector3.zero, source.movementHandler);
        }

        /// <summary>Measures what source can do (see MovementController.Capabilities), with the given collider if any.</summary>
        public static MovementCapabilities Measure(MovementController source, float? authoredRadius = null, float? authoredHeight = null)
        {
            using var simulation = new MovementSimulation(source, authoredRadius, authoredHeight);
            MovementController copy = simulation.Controller;
            var capabilities = new MovementCapabilities
            {
                StepHeight = copy.MaxStepHeight * copy.HeightScale,
                WalkableSlopeAngle = copy.MaxSlopeAngle,
                StandHeight = copy.StandingHeight,
                Radius = copy.Radius,
            };
            if (source.movementHandler != null) { source.movementHandler.MeasureCapabilities(simulation, capabilities); }
            return capabilities;
        }

        /// <summary>
        /// Starts again with a fresh copy of the character (no state carried over) standing at feet (relative to the
        /// origin) facing +Z, moving with the given movement mode (by default the character's own; a measurement may pass a
        /// copy with some abilities turned off: see Copy).
        /// </summary>
        public void Reset(Vector3 feet, MovementMode mode = null)
        {
            if (character != null) { Object.DestroyImmediate(character); }
            character = new GameObject("Simulated Character") { hideFlags = HideFlags.HideAndDontSave, layer = source.gameObject.layer };
            character.SetActive(false);
            character.transform.SetParent(root.transform, false);
            var collider = character.AddComponent<CapsuleCollider>();
            body = character.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
            Controller = character.AddComponent<MovementController>();
            Controller.CopyForSimulation(source, body, collider, mode != null ? mode : source.movementHandler, baseRadius, baseHeight);
            Controller.InputCamera = root.transform;
            Controller.MovementEventRaised += (e, _) => LastEvents |= e;
            character.SetActive(true);
            Place(feet);
        }

        /// <summary>A copy of a movement mode for a measurement to change (destroyed with the simulation).</summary>
        public T Copy<T>(T mode) where T : MovementMode
        {
            T copy = Object.Instantiate(mode);
            copy.hideFlags = HideFlags.HideAndDontSave;
            modes.Add(copy);
            return copy;
        }

        /// <summary>Moves the character's feet to the given position (relative to the origin), between steps.</summary>
        public void Place(Vector3 feet)
        {
            Vector3 world = WorldOrigin + feet;
            body.position = world;
            body.rotation = Quaternion.identity;
            character.transform.SetPositionAndRotation(world, Quaternion.identity);
            Physics.SyncTransforms();
        }

        /// <summary>Adds a solid box (centre and size relative to the origin), for the character to meet.</summary>
        public GameObject Box(Vector3 centre, Vector3 size)
        {
            var box = new GameObject("Box") { hideFlags = HideFlags.HideAndDontSave };
            box.transform.SetParent(root.transform, false);
            box.transform.localPosition = centre;
            box.AddComponent<BoxCollider>().size = size;
            obstacles.Add(box);
            Physics.SyncTransforms();
            return box;
        }

        /// <summary>Removes everything added with Box but the floor.</summary>
        public void ClearObstacles()
        {
            for (int i = obstacles.Count - 1; i >= 1; i--)
            {
                Object.DestroyImmediate(obstacles[i]);
                obstacles.RemoveAt(i);
            }
            Physics.SyncTransforms();
        }

        /// <summary>One physics step of movement with the given inputs; the character ends where its movement took it.</summary>
        public void Step(Inputs inputs)
        {
            LastEvents = MovementEvents.None;
            Controller.ProcessMovementInputs(inputs);
            MovementData data = Controller.currentMovementData;
            Vector3 position = data.initialPosition + data.finalMovementVector;
            Quaternion rotation = data.finalRotation;
            Controller.MovementCleanup();
            // Rigidbody.Move (which the controller used) only moves the body at the next physics simulation, which this
            // world doesn't run: the body is put there directly.
            body.position = position;
            body.rotation = rotation;
            character.transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Steps with the given inputs (by step index) the given number of steps, or until stop says so.</summary>
        public void Run(int steps, Func<int, Inputs> inputs, Func<int, bool> stop = null)
        {
            for (int i = 0; i < steps; i++)
            {
                Step(inputs(i));
                if (stop != null && stop(i)) { return; }
            }
        }

        /// <summary>Physics steps in the given time.</summary>
        public static int StepsFor(float seconds) => Mathf.CeilToInt(seconds / DeltaTime - 1e-3f);

        // ---- Inputs ----

        /// <summary>A button held for the given step of its press (0: pressed this step), or not held (negative).</summary>
        public static InputButton Held(int step) => step < 0 ? new InputButton() : new InputButton
        {
            value = true,
            justPressed = step == 0,
            holdDuration = step * DeltaTime,
        };

        /// <summary>Movement input along direction (relative to the simulation: +Z forward), sprinting or not.</summary>
        public static Inputs Move(Vector3 direction, bool sprint = false) => new()
        {
            movementVector = direction,
            jump = new InputButton(),
            dash = new InputButton(),
            sprint = sprint ? Held(1) : new InputButton(),
        };

        public void Dispose()
        {
            if (root != null) { Object.DestroyImmediate(root); }
            foreach (Object mode in modes) { if (mode != null) { Object.DestroyImmediate(mode); } }
            modes.Clear();
            Physics.SyncTransforms();
        }
    }
}
