using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of the movement events animation, effects and sound hook into: each is raised once, at the right moment,
    /// and the AnimationController passes them on along with the animation state.
    /// </summary>
    public class MovementEventTests
    {
        private CharacterTestRig rig;
        private readonly List<(int step, MovementEvents movementEvent)> events = new();
        private int step;

        [SetUp]
        public void SetUp()
        {
            rig = new CharacterTestRig();
            events.Clear(); // NUnit reuses the fixture instance between tests.
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            rig.Dispose();
            yield return null;
        }

        private int Count(MovementEvents movementEvent) => events.Count(e => e.movementEvent == movementEvent);
        private int StepOf(MovementEvents movementEvent) => events.First(e => e.movementEvent == movementEvent).step;

        /// <summary>Spawns the character on a floor, lets it settle, then starts recording its events.</summary>
        private IEnumerator SettleAndRecord(Vector3 position)
        {
            rig.SpawnCharacter(position);
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            rig.Movement.MovementEventRaised += (movementEvent, _) => events.Add((step, movementEvent));
        }

        /// <summary>Runs the character, numbering steps from 0 so recorded events can be matched to inputs.</summary>
        private IEnumerator Run(float seconds, System.Func<int, Inputs> inputsForStep)
        {
            step = 0;
            yield return rig.Run(seconds, i => { step = i; return inputsForStep(i); });
        }

        [UnityTest]
        public IEnumerator Jumping_RaisesJumpedAndLeftGround_ThenLandedOnTouchdown()
        {
            rig.Floor();
            yield return SettleAndRecord(new Vector3(0f, 0.05f, 0f));

            yield return Run(1.5f, i => i < 5 ? CharacterTestRig.Jump(i) : CharacterTestRig.Idle());

            Assert.That(Count(MovementEvents.Jumped), Is.EqualTo(1));
            Assert.That(Count(MovementEvents.LeftGround), Is.EqualTo(1));
            Assert.That(Count(MovementEvents.Landed), Is.EqualTo(1));
            Assert.That(Count(MovementEvents.AirJumped), Is.Zero);
            Assert.That(StepOf(MovementEvents.Jumped), Is.Zero, "Jumped should be raised on the step the jump starts.");
            Assert.That(StepOf(MovementEvents.LeftGround), Is.Zero, "The jump leaves the ground on the same step.");
            Assert.That(StepOf(MovementEvents.Landed), Is.GreaterThan(10));
            Assert.That(rig.Grounded, Is.True);
        }

        [UnityTest]
        public IEnumerator JumpingAgainInMidAir_RaisesAirJumped()
        {
            rig.Floor();
            yield return SettleAndRecord(new Vector3(0f, 0.05f, 0f));

            // Hold the first jump briefly, so the character is still in the air for the second press.
            yield return Run(0.5f, i => i < 5 ? CharacterTestRig.Jump(i) : i == 12 ? CharacterTestRig.Jump(0) : CharacterTestRig.Idle());

            Assert.That(Count(MovementEvents.Jumped), Is.EqualTo(1));
            Assert.That(Count(MovementEvents.AirJumped), Is.EqualTo(1));
            Assert.That(StepOf(MovementEvents.AirJumped), Is.EqualTo(12));
        }

        [UnityTest]
        public IEnumerator WalkingOffALedge_RaisesLeftGround_WithoutAJump()
        {
            rig.Box("Ledge", new Vector3(0f, -0.5f, -10f), new Vector3(10f, 1f, 20f));
            rig.Floor(-3f);
            yield return SettleAndRecord(new Vector3(0f, 0.05f, -1f));

            yield return Run(1.5f, _ => CharacterTestRig.Move(Vector3.forward));

            Assert.That(Count(MovementEvents.LeftGround), Is.EqualTo(1));
            Assert.That(Count(MovementEvents.Jumped), Is.Zero);
            Assert.That(Count(MovementEvents.Landed), Is.EqualTo(1), "The character should land on the floor below.");
        }

        [UnityTest]
        public IEnumerator Dashing_RaisesDashStarted_ThenDashEndedWhenItFinishes()
        {
            rig.Floor();
            yield return SettleAndRecord(new Vector3(0f, 0.05f, 0f));
            int dashSteps = CharacterTestRig.StepsFor(rig.MovementSetting<float>("dashDuration"));

            yield return Run(0.5f, i => i == 0 ? CharacterTestRig.Dash(0, Vector3.right) : CharacterTestRig.Idle());

            Assert.That(Count(MovementEvents.DashStarted), Is.EqualTo(1));
            Assert.That(Count(MovementEvents.DashEnded), Is.EqualTo(1));
            Assert.That(StepOf(MovementEvents.DashStarted), Is.Zero);
            Assert.That(StepOf(MovementEvents.DashEnded), Is.EqualTo(dashSteps - 1).Within(1));
        }

        [UnityTest]
        public IEnumerator JumpingOutOfADash_RaisesDashEndedAndJumpedTogether()
        {
            rig.Floor();
            yield return SettleAndRecord(new Vector3(0f, 0.05f, 0f));

            yield return Run(0.3f, i => i == 0 ? CharacterTestRig.Dash(0, Vector3.right)
                : i == 3 ? CharacterTestRig.Jump(0) : CharacterTestRig.Idle());

            Assert.That(Count(MovementEvents.DashEnded), Is.EqualTo(1));
            Assert.That(StepOf(MovementEvents.DashEnded), Is.EqualTo(3));
            Assert.That(StepOf(MovementEvents.Jumped), Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator BumpingACeilingOrWall_RaisesEachOnceOnContact()
        {
            rig.Floor();
            rig.Box("Ceiling", new Vector3(0f, 3.3f, 0f), new Vector3(10f, 1f, 10f)); // Underside 0.8 m above the head.
            rig.Box("Wall", new Vector3(0f, 1.5f, 2.5f), new Vector3(10f, 3f, 1f));
            yield return SettleAndRecord(new Vector3(0f, 0.05f, 0f));

            yield return Run(0.6f, i => CharacterTestRig.Jump(i));
            yield return Run(1f, _ => CharacterTestRig.Move(Vector3.forward));

            Assert.That(Count(MovementEvents.HitCeiling), Is.EqualTo(1));
            Assert.That(Count(MovementEvents.HitWall), Is.EqualTo(1), "Pressing into the wall shouldn't raise it again every step.");
        }

        [UnityTest]
        public IEnumerator AnimationController_PassesEventsOn_WithTheAnimationState()
        {
            rig.Floor();
            yield return SettleAndRecord(new Vector3(0f, 0.05f, 0f));
            var received = new List<(MovementEvents movementEvent, CharacterAnimationState state)>();
            rig.Animation.MovementEventReceived += (movementEvent, state) => received.Add((movementEvent, state));

            yield return Run(0.1f, i => i == 0 ? CharacterTestRig.Jump(0) : CharacterTestRig.Idle());

            Assert.That(received.Select(r => r.movementEvent), Is.EqualTo(events.Select(e => e.movementEvent)),
                "The AnimationController should pass on every movement event, in order.");
            var jumped = received.First(r => r.movementEvent == MovementEvents.Jumped);
            Assert.That(jumped.state.jumping, Is.True);
            Assert.That(jumped.state.verticalSpeed, Is.GreaterThan(0f));
        }
    }
}
