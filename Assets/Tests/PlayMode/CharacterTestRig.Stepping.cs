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
    // The rig's stepping: running the character for steps or seconds, until something happens, as the game does.
    public sealed partial class CharacterTestRig
    {
        /// <summary>
        /// Runs the character for a number of physics steps. inputsForStep is called with the step index (only its move
        /// input and which buttons are down count: the character times presses and holds itself, from the steps);
        /// afterStep is called after each step's movement has been simulated by physics.
        /// </summary>
        public IEnumerator Run(int steps, Func<int, Inputs> inputsForStep, Action<int> afterStep = null) =>
            RunFrames(steps, Frames(inputsForStep), afterStep);

        /// <summary>
        /// Scripted inputs as the frames the character is given (see CharacterInputFrame.Holding), tagged with its tick and
        /// looking along Look.
        /// </summary>
        private Func<int, CharacterInputFrame> Frames(Func<int, Inputs> inputsForStep) => step => Frame(inputsForStep(step));

        private CharacterInputFrame Frame(Inputs inputs) => CharacterInputFrame.Holding(inputs, Movement.Tick).LookingAlong(Look);

        /// <summary>
        /// The way the scripted inputs look (each frame's look: see CharacterInputFrame), which their move input is relative
        /// to: along +Z unless a test turns it.
        /// </summary>
        public Quaternion Look { get; set; } = Quaternion.identity;

        /// <summary>Runs the character for a number of physics steps, given each step's input frame (see Run).</summary>
        public IEnumerator RunFrames(int steps, Func<int, CharacterInputFrame> framesForStep, Action<int> afterStep = null) =>
            RunSources(steps, step => Scripted(framesForStep(step)), afterStep);

        /// <summary>
        /// Runs the character for a number of physics steps driven by the input source given (an AI's, say), as its
        /// ACCharacterController steps it in the game: the source is asked for each step's frame.
        /// </summary>
        public IEnumerator RunWithSource(int steps, ICharacterInputSource source, Action<int> afterStep = null) =>
            RunSources(steps, _ => source, afterStep);

        public IEnumerator RunWithSource(float seconds, ICharacterInputSource source, Action<int> afterStep = null) =>
            RunWithSource(StepsFor(seconds), source, afterStep);

        // The rig's own source: the scripted frame for the step being taken, given to the character as any source's is.
        private sealed class ScriptedFrame : ICharacterInputSource
        {
            public CharacterInputFrame next;
            public CharacterInputFrame FrameFor(uint tick, MovementController character) => next;
        }

        private readonly ScriptedFrame scripted = new();

        private ICharacterInputSource Scripted(in CharacterInputFrame frame)
        {
            scripted.next = frame;
            return scripted;
        }

        private IEnumerator RunSources(int steps, Func<int, ICharacterInputSource> sourceForStep, Action<int> afterStep)
        {
            if (CanStepPhysicsItself)
            {
                RunDirectly(steps, sourceForStep, afterStep);
                yield break;
            }
            for (int i = 0; i < steps; i++)
            {
                int step = i;
                // Stepped in the coming physics step's FixedUpdate, after the level's moving platforms have moved (see
                // FixedStepper), as ACCharacterController steps it in the game.
                stepper.Next = () => Step(step, sourceForStep(step));
                yield return new WaitForFixedUpdate();
                // The step's Rigidbody.Move has now been simulated, so Position reflects it.
                afterStep?.Invoke(i);
            }
        }

        /// <summary>
        /// Whether the rig counts the managed heap allocations the character's steps make (see AllocationCounter), adding
        /// them to StepAllocations: the step itself (any change of size, and the movement), not the test's inputs for it.
        /// </summary>
        public bool MeasuresStepAllocations { get; set; }

        /// <summary>How many allocations the character's steps made while MeasuresStepAllocations was on.</summary>
        public int StepAllocations { get; set; }

        /// <summary>The frames the character is given, recorded as it's stepped (if set: see InputRecording).</summary>
        public InputRecording Recording { get; set; }

        /// <summary>
        /// Runs the character through a recording's frames, one a step from its oldest, as a replay does (see
        /// InputRecording): the same frames, so it should move as it did when they were recorded.
        /// </summary>
        public IEnumerator Replay(InputRecording recording, Action<int> afterStep = null) =>
            RunFrames(recording.Count, step => recording[step], afterStep);

        /// <summary>
        /// One physics step of the character, through its ACCharacterController as the game steps it
        /// (ACCharacterController.StepMovement: any change of size, then movement), driven by the source given.
        /// </summary>
        private void Step(int step, ICharacterInputSource source)
        {
            runStep = step;
            Controller.InputSource = source;
            if (MeasuresStepAllocations) { AllocationCounter.Start(); }
            Controller.StepMovement(Time.fixedDeltaTime);
            if (MeasuresStepAllocations) { StepAllocations += AllocationCounter.Stop(); }
            Recording?.Add(Movement.InputState.Frame);
        }

        private readonly FixedStepper stepper;

        /// <summary>
        /// Runs the rig's next character step in FixedUpdate, after the level's moving platforms (MovingPlatform.MoveOrder)
        /// have moved and before the physics step, as ACCharacterController does in the game. Stepped after the physics
        /// step instead (on resuming from WaitForFixedUpdate), the character read the platforms a step before they moved,
        /// and rode them a step behind.
        /// </summary>
        [DefaultExecutionOrder(Level.MovingPlatform.MoveOrder + 200)]
        private sealed class FixedStepper : MonoBehaviour
        {
            /// <summary>The step to run in the next FixedUpdate, once.</summary>
            public Action Next;

            private void FixedUpdate()
            {
                Action next = Next;
                Next = null;
                next?.Invoke();
            }
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
        private void RunDirectly(int steps, Func<int, ICharacterInputSource> sourceForStep, Action<int> afterStep)
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
                    Step(i, sourceForStep(i));
                    // As the model host's FixedUpdate would, before the physics step.
                    if (models != null) { models.TryFitBodyNow(); }
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
                if (CanStepPhysicsItself) { RunDirectly(1, _ => Scripted(Frame(inputsForStep(i))), _ => afterStep?.Invoke(i)); }
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
    }
}
