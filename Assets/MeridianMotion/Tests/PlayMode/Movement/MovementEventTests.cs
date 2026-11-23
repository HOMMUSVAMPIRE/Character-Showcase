using System.Collections;
using System.Linq;
using MeridianMotion.Characters;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace MeridianMotion.Tests.Movement
{
    /// <summary>
    /// Tests of the movement events animation, effects and sound hook into: each is raised once, at the right moment.
    /// (That the MMotionAnimator passes them on is AnimationEventTests'; that each ability's state has its events,
    /// declared once, is MovementCompositionTests'.)
    /// </summary>
    [Category(TestArea.Movement)]
    public class MovementEventTests : CharacterRigFixture
    {
        /// <summary>Spawns the character on a floor, lets it settle, then records its events from there.</summary>
        private IEnumerator SettleAndRecord(Vector3 position)
        {
            rig.SpawnCharacter(position);
            yield return rig.Settle();
            rig.ClearEvents();
        }

        /// <summary>Where the event comes in the order events were raised, or -1 if it hasn't been.</summary>
        private int OrderOf(MovementEvents movementEvent) => rig.EventsRaised.ToList().IndexOf(movementEvent);

        [UnityTest]
        public IEnumerator Jumping_RaisesJumpedAndLeftGround_ThenLandedOnTouchdown()
        {
            rig.Floor();
            yield return SettleAndRecord(new Vector3(0f, 0.05f, 0f));

            yield return rig.Run(5, i => CharacterTestRig.Jump());
            // However long the jump takes.
            yield return rig.RunUntilLanded();

            Assert.That(rig.Count(MovementEvents.Jumped), Is.EqualTo(1));
            Assert.That(rig.Count(MovementEvents.LeftGround), Is.EqualTo(1));
            Assert.That(rig.Count(MovementEvents.Landed), Is.EqualTo(1));
            Assert.That(rig.Count(MovementEvents.AirJumped), Is.Zero);
            Assert.That(rig.StepOf(MovementEvents.Jumped), Is.Zero, "Jumped should be raised on the step the jump starts.");
            Assert.That(rig.StepOf(MovementEvents.LeftGround), Is.Zero, "The jump leaves the ground on the same step.");
            Assert.That(OrderOf(MovementEvents.Landed), Is.GreaterThan(OrderOf(MovementEvents.Jumped)), "Landed should come after the jump.");
            Assert.That(rig.Grounded, Is.True);
        }

        [UnityTest]
        public IEnumerator JumpingAgainInMidAir_RaisesAirJumped()
        {
            rig.Floor();
            yield return SettleAndRecord(new Vector3(0f, 0.05f, 0f));
            if (rig.Component<Jumping>().MaxJumps < 2) { Assert.Ignore("The character can't jump in mid-air."); }

            // A tap of Jump, a step with it let go of (so the next press is a new one), then a press in mid-air: checked to
            // be in the air, so it holds however short the jump is tuned.
            yield return rig.Run(1, _ => CharacterTestRig.Jump());
            yield return rig.Run(1, _ => CharacterTestRig.Idle());
            Assume.That(rig.Grounded, Is.False, "Test setup: the character should still be in the air from the first jump.");
            yield return rig.Run(1, _ => CharacterTestRig.Jump());

            Assert.That(rig.Count(MovementEvents.Jumped), Is.EqualTo(1));
            Assert.That(rig.Count(MovementEvents.AirJumped), Is.EqualTo(1));
            Assert.That(rig.StepOf(MovementEvents.AirJumped), Is.Zero, "AirJumped should be raised on the step it's pressed.");
        }

        [UnityTest]
        public IEnumerator WalkingOffALedge_RaisesLeftGround_WithoutAJump()
        {
            rig.Box("Ledge", new Vector3(0f, -0.5f, -10f), new Vector3(10f, 1f, 20f));
            rig.Floor(-3f);
            yield return SettleAndRecord(new Vector3(0f, 0.05f, -1f));

            yield return rig.Run(rig.SecondsToWalk(1f, 1f), _ => CharacterTestRig.Move(Vector3.forward));

            Assert.That(rig.Count(MovementEvents.LeftGround), Is.EqualTo(1));
            Assert.That(rig.Count(MovementEvents.Jumped), Is.Zero);
            Assert.That(rig.Count(MovementEvents.Landed), Is.EqualTo(1), "The character should land on the floor below.");
        }

        [UnityTest]
        public IEnumerator Dashing_RaisesDashStarted_ThenDashEndedWhenItFinishes()
        {
            rig.Floor();
            yield return SettleAndRecord(new Vector3(0f, 0.05f, 0f));
            int dashSteps = CharacterTestRig.StepsFor(rig.Component<Dashing>().DashDuration);

            yield return rig.Run(0.5f, i => i == 0 ? CharacterTestRig.Dash(Vector3.right) : CharacterTestRig.Idle());

            Assert.That(rig.Count(MovementEvents.DashStarted), Is.EqualTo(1));
            Assert.That(rig.Count(MovementEvents.DashEnded), Is.EqualTo(1));
            Assert.That(rig.StepOf(MovementEvents.DashStarted), Is.Zero);
            // On the first step it isn't dashing, as every ability's end is raised (its state's going: see StateEvents).
            Assert.That(rig.StepOf(MovementEvents.DashEnded), Is.EqualTo(dashSteps));
        }

        [UnityTest]
        public IEnumerator JumpingOutOfADash_RaisesDashEndedAndJumpedTogether()
        {
            rig.Floor();
            yield return SettleAndRecord(new Vector3(0f, 0.05f, 0f));
            // Jump halfway through the dash, however long it's tuned to last.
            int dashSteps = CharacterTestRig.StepsFor(rig.Component<Dashing>().DashDuration * rig.Movement.DurationScale);
            Assume.That(dashSteps, Is.GreaterThanOrEqualTo(2), "Test setup: the dash should last long enough to jump out of.");
            int jumpAt = Mathf.Clamp(dashSteps / 2, 1, dashSteps - 1);

            yield return rig.Run(dashSteps + 5, i => i == 0 ? CharacterTestRig.Dash(Vector3.right)
                : i == jumpAt ? CharacterTestRig.Jump() : CharacterTestRig.Idle());

            Assert.That(rig.Count(MovementEvents.DashEnded), Is.EqualTo(1));
            Assert.That(rig.StepOf(MovementEvents.DashEnded), Is.EqualTo(jumpAt));
            Assert.That(rig.StepOf(MovementEvents.Jumped), Is.EqualTo(jumpAt));
        }

        [UnityTest]
        public IEnumerator BumpingACeilingOrWall_RaisesEachOnceOnContact()
        {
            rig.Floor();
            // The ceiling half a step height above the head (the jump clears a step), the wall 2 m ahead.
            BodyShape capsule = CharacterTestRig.PrefabBody();
            float ceiling = capsule.height + 0.5f * CharacterTestRig.PrefabStepHeight(), face = capsule.radius + 2f;
            rig.Box("Ceiling", new Vector3(0f, ceiling + 0.5f, 0f), new Vector3(10f, 1f, 10f));
            rig.Box("Wall", new Vector3(0f, ceiling / 2f, face + 0.5f), new Vector3(10f, ceiling, 1f));
            yield return SettleAndRecord(new Vector3(0f, 0.05f, 0f));

            yield return rig.Run(0.6f, i => CharacterTestRig.Jump());
            yield return rig.Run(rig.SecondsToWalk(2f), _ => CharacterTestRig.Move(Vector3.forward));

            Assert.That(rig.Count(MovementEvents.HitCeiling), Is.EqualTo(1));
            Assert.That(rig.Count(MovementEvents.HitWall), Is.EqualTo(1), "Pressing into the wall shouldn't raise it again every step.");
        }
    }
}
