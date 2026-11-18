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

        /// <summary>How long (s) a measured move may run at most, before it's given up on.</summary>
        public const float MeasureTimeout = 8f;
        /// <summary>How long (s) the character runs up before a move is measured: long enough to be at full speed.</summary>
        public const float MeasureRunUp = 0.8f;

        private readonly MovementController source;
        private readonly BodyShape? authoredBody;
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
        /// <summary>Whether the character was on the ground in the last step.</summary>
        public bool Grounded => (State & CharacterState.Grounded) != 0;
        /// <summary>The character's velocity in the last step (m/s).</summary>
        public Vector3 Velocity => Controller.lastFrameMovementData != null ? Controller.lastFrameMovementData.finalVelocity : Vector3.zero;
        /// <summary>The physics step (s).</summary>
        public static float DeltaTime => Time.fixedDeltaTime;

        /// <summary>
        /// A world to simulate source's movement in. Its body is the one source has, unless a body as authored is given: the
        /// one a model would give it (see CharacterModelHost.BodyFor).
        /// </summary>
        public MovementSimulation(MovementController source, BodyShape? authoredBody = null)
        {
            this.source = source;
            this.authoredBody = authoredBody;
            root = new GameObject("Movement Simulation") { hideFlags = HideFlags.HideAndDontSave };
            root.transform.position = WorldOrigin;
            Box(new Vector3(0f, -0.5f, 0f), new Vector3(FloorSize, 1f, FloorSize));
            Reset(Vector3.zero, source.movementHandler);
        }

        /// <summary>Measures what source can do (see MovementController.Capabilities), with the given body if any.</summary>
        public static MovementCapabilities Measure(MovementController source, BodyShape? authoredBody = null)
        {
            using var simulation = new MovementSimulation(source, authoredBody);
            MovementController copy = simulation.Controller;
            var capabilities = new MovementCapabilities
            {
                StepHeight = copy.MaxStepHeight * copy.HeightScale,
                WalkableSlopeAngle = copy.MaxSlopeAngle,
                StandHeight = copy.Body.StandingHeight,
                HalfWidth = copy.Body.SideExtent,
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
            body = character.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
            Controller = character.AddComponent<MovementController>();
            Controller.CopyForSimulation(source, body, mode != null ? mode : source.movementHandler, authoredBody);
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

        /// <summary>
        /// One physics step of movement with the given inputs (the move input and which buttons are down: the character
        /// times its presses itself); the character ends where its movement took it.
        /// </summary>
        public void Step(Inputs inputs)
        {
            LastEvents = MovementEvents.None;
            Controller.ProcessMovementInputs(CharacterInputFrame.Holding(inputs, Controller.Tick), DeltaTime);
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

        /// <summary>
        /// A button down from the step of its press (step 0) on, or up (a negative step): the character works out the press
        /// and how long it's held from the steps it's down in (see InputState).
        /// </summary>
        public static InputButton Held(int step) => new() { value = step >= 0 };

        /// <summary>Movement input along direction (relative to the simulation: +Z forward), sprinting or not.</summary>
        public static Inputs Move(Vector3 direction, bool sprint = false) => new()
        {
            movementVector = direction,
            jump = new InputButton(),
            dash = new InputButton(),
            sprint = Held(sprint ? 0 : -1),
        };

        /// <summary>
        /// The simulated character's state saved partway through a measurement (see Save), to start later attempts from
        /// rather than running up to the same point again.
        /// </summary>
        public sealed class Snapshot
        {
            internal MovementController controller;
            internal ObjectGraphSnapshot state;
            internal Vector3 position;
            internal Quaternion rotation;
            internal MovementEvents lastEvents;
            /// <summary>Whatever else the attempt that saved it needs to carry on from it (its own counters, say).</summary>
            public object extra;
        }

        // Snapshots saved by Begin, by what they're of.
        private readonly Dictionary<string, Snapshot> saved = new();

        /// <summary>
        /// Saves the character as it is now: everything about it a step reads or changes (the movement controller's state,
        /// its abilities', its body's pose), so Restore puts it back exactly, and the steps after behave exactly as they
        /// would have.
        /// </summary>
        public Snapshot Save() => new()
        {
            controller = Controller,
            state = ObjectGraphSnapshot.Capture(Controller, typeof(MonoBehaviour)),
            position = body.position,
            rotation = body.rotation,
            lastEvents = LastEvents,
        };

        /// <summary>
        /// Puts the character back as it was when the snapshot was saved. False, changing nothing, if the character has
        /// been made afresh since (Reset), which a snapshot can't be put into.
        /// </summary>
        public bool Restore(Snapshot snapshot)
        {
            if (snapshot == null || snapshot.controller != Controller || Controller == null) { return false; }
            snapshot.state.Restore();
            body.position = snapshot.position;
            body.rotation = snapshot.rotation;
            character.transform.SetPositionAndRotation(snapshot.position, snapshot.rotation);
            LastEvents = snapshot.lastEvents;
            Physics.SyncTransforms();
            return true;
        }

        /// <summary>
        /// Starts an attempt from the point named key: from the snapshot saved there, if there is one this character can
        /// be put back into (see Restore); else by running prepare (which may Reset the character), saving where that
        /// leaves it under key for the next. So attempts that all start the same way (a run-up) run it once. prepare must
        /// leave the character the same way each time it's run, for a key.
        /// </summary>
        public void Begin(string key, Action prepare)
        {
            if (saved.TryGetValue(key, out Snapshot snapshot) && Restore(snapshot)) { return; }
            prepare();
            saved[key] = Save();
        }

        // Snapshots along paths that attempts share (see KeepOnPath), by path, and by step along it.
        private readonly Dictionary<string, SortedList<int, Snapshot>> paths = new();

        /// <summary>
        /// Saves the character as it is now, before step step of the path named path (the steps every attempt with the
        /// same start and inputs so far takes alike), with extra (the attempt's own counters), for later attempts on the
        /// path to start from (see RestoreFurthestOnPath). Kept once: saving the same step of a path again does nothing.
        /// </summary>
        public void KeepOnPath(string path, int step, object extra)
        {
            if (!paths.TryGetValue(path, out SortedList<int, Snapshot> along)) { paths[path] = along = new SortedList<int, Snapshot>(); }
            if (along.TryGetValue(step, out Snapshot existing) && existing.controller == Controller) { return; }
            Snapshot snapshot = Save();
            snapshot.extra = extra;
            along[step] = snapshot;
        }

        /// <summary>
        /// Puts the character back as it was at the furthest step (no further than upTo) saved on the path, giving the
        /// step and its extra; false if none is saved there that this character can be put back into.
        /// </summary>
        public bool RestoreFurthestOnPath(string path, int upTo, out int step, out object extra)
        {
            step = 0;
            extra = null;
            if (!paths.TryGetValue(path, out SortedList<int, Snapshot> along)) { return false; }
            IList<int> steps = along.Keys;
            // The last saved step no further than upTo (steps are sorted).
            int low = 0, high = steps.Count - 1, found = -1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (steps[mid] <= upTo) { found = mid; low = mid + 1; } else { high = mid - 1; }
            }
            if (found < 0 || !Restore(along.Values[found])) { return false; }
            step = steps[found];
            extra = along.Values[found].extra;
            return true;
        }

        /// <summary>Destroys the simulation's world and its copies of the movement modes.</summary>
        public void Dispose()
        {
            if (root != null) { Object.DestroyImmediate(root); }
            foreach (Object mode in modes) { if (mode != null) { Object.DestroyImmediate(mode); } }
            modes.Clear();
            Physics.SyncTransforms();
        }
    }
}
