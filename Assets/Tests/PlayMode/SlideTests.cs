using System.Collections;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of sliding: pressing Crouch while sprinting slides on the sprint's momentum, low enough to pass under things
    /// the character can't stand under, slowing down until it ends in a crouch; walking and crouching doesn't slide,
    /// jumping out of a slide keeps its speed, and a wall stops it. Expected values are read from the movement mode.
    /// </summary>
    public class SlideTests
    {
        private CharacterTestRig rig;

        [SetUp]
        public void SetUp() => rig = new CharacterTestRig();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            rig.Dispose();
            yield return null;
        }

        private float SlideStartSpeed => rig.Mode<SlideMode>().slideStartSpeed * rig.Movement.SpeedScale;
        private float SlideMaxDuration => rig.Mode<SlideMode>().slideMaxDuration;
        private float CrouchHeight => rig.Mode<CrouchMode>().crouchHeight;
        private bool Sliding => rig.InState(CharacterState.Sliding);

        /// <summary>Stands the character on a floor and gets it sprinting forward, skipping the test if it can't slide from a sprint.</summary>
        private IEnumerator SprintForward()
        {
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, -20f));
            float minStart = rig.MovementSetting<float>("slideMinStartSpeed") * rig.Movement.SpeedScale;
            if (!(rig.RunSpeed >= minStart)) { Assert.Ignore($"The character's sprint ({rig.RunSpeed:0.0} m/s) is too slow to slide ({minStart:0.0} m/s)."); }
            yield return rig.Run(0.4f, step => CharacterTestRig.Sprint(Vector3.forward, step));
        }

        /// <summary>Sprint forward with Crouch pressed on the first step (and held for the given number of steps).</summary>
        private static Inputs SprintCrouching(int step, int crouchSteps = 1)
        {
            Inputs inputs = CharacterTestRig.Sprint(Vector3.forward, step + 100);
            if (step < crouchSteps) { inputs.crouch = CharacterTestRig.Held(step); }
            return inputs;
        }

        [UnityTest]
        public IEnumerator CrouchingWhileSprinting_Slides_SlowingDown_ThenEndsCrouched()
        {
            yield return SprintForward();
            float standing = rig.StandingHeight;
            float firstSpeed = -1f, lastSlidingSpeed = 0f, lowest = float.PositiveInfinity;
            int slidingSteps = 0;
            bool slowedDown = true;
            float previous = float.PositiveInfinity;
            yield return rig.Run(SlideMaxDuration + 0.5f, step => SprintCrouching(step), _ =>
            {
                if (!Sliding) { return; }
                slidingSteps++;
                if (firstSpeed < 0f) { firstSpeed = rig.HorizontalSpeed; }
                if (rig.HorizontalSpeed > previous + 1e-3f) { slowedDown = false; }
                previous = lastSlidingSpeed = rig.HorizontalSpeed;
                lowest = Mathf.Min(lowest, rig.CurrentHeight);
            });

            Assert.That(slidingSteps, Is.GreaterThan(5), "Pressing Crouch while sprinting should slide.");
            Assert.That(firstSpeed, Is.GreaterThanOrEqualTo(0.95f * Mathf.Max(SlideStartSpeed, rig.RunSpeed)), "A slide should start at least at its start speed, or the sprint's.");
            Assert.That(slowedDown, Is.True, "A slide should only slow down.");
            Assert.That(lastSlidingSpeed, Is.LessThan(firstSpeed), "A slide should slow down.");
            Assert.That(slidingSteps, Is.LessThanOrEqualTo(CharacterTestRig.StepsFor(SlideMaxDuration) + 1), "A slide should last no longer than its longest time.");
            Assert.That(lowest, Is.EqualTo(standing * CrouchHeight).Within(0.01f), "A slide should be low, at the crouch height.");
            Assert.That(Sliding, Is.False, "The slide should have ended.");
            Assert.That(rig.InState(CharacterState.Crouching), Is.True, "A slide should end in a crouch.");
        }

        [UnityTest]
        public IEnumerator CrouchingWhileWalking_JustCrouches()
        {
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Move(Vector3.forward));
            bool slid = false;
            yield return rig.Run(0.5f, step => step == 0 ? CharacterTestRig.Crouch(0, Vector3.forward) : CharacterTestRig.Move(Vector3.forward),
                _ => slid |= Sliding);
            Assert.That(slid, Is.False, "Crouching while walking shouldn't slide.");
            Assert.That(rig.InState(CharacterState.Crouching), Is.True, "Crouching while walking should crouch.");
        }

        [UnityTest]
        public IEnumerator Sliding_PassesUnderABarTooLowToStandUnder()
        {
            yield return SprintForward();
            float standing = rig.StandingHeight;
            // A bar between the crouch height and the standing height, a little ahead.
            float underside = standing * Mathf.Lerp(CrouchHeight, 1f, 0.5f);
            float barZ = rig.Position.z + rig.Radius + 1.5f;
            rig.Box("Low Bar", new Vector3(0f, underside + 0.25f, barZ), new Vector3(6f, 0.5f, 0.3f));

            yield return rig.Run(SlideMaxDuration + 1f, step => SprintCrouching(step));
            Assert.That(rig.Position.z - rig.Radius, Is.GreaterThan(barZ + 0.15f), "The slide should carry the character under the bar.");
        }

        [UnityTest]
        public IEnumerator JumpingOutOfASlide_KeepsItsSpeed()
        {
            yield return SprintForward();
            const int jumpAt = 5;
            float speedAtJump = 0f, airSpeed = 0f;
            bool slidAtJump = false;
            yield return rig.Run(jumpAt + 10, step =>
            {
                Inputs inputs = SprintCrouching(step);
                if (step >= jumpAt) { inputs.jump = CharacterTestRig.Held(step - jumpAt); }
                return inputs;
            }, step =>
            {
                if (step == jumpAt - 1) { speedAtJump = rig.HorizontalSpeed; slidAtJump = Sliding; }
                if (step == jumpAt + 5) { airSpeed = rig.HorizontalSpeed; }
            });
            Assert.That(slidAtJump, Is.True, "Test setup: the character should be sliding when it jumps.");
            Assert.That(rig.Grounded, Is.False, "Jumping should jump out of the slide.");
            Assert.That(Sliding, Is.False, "Jumping should end the slide.");
            Assert.That(airSpeed, Is.GreaterThan(0.85f * speedAtJump), "The jump should keep the slide's speed.");
            Assert.That(rig.CurrentHeight, Is.EqualTo(rig.StandingHeight).Within(0.01f), "Jumping out of a slide should stand up.");
        }

        [UnityTest]
        public IEnumerator SlidingIntoAWall_Stops()
        {
            yield return SprintForward();
            float face = rig.Position.z + rig.Radius + 1f;
            rig.Box("Wall", new Vector3(0f, 2f, face + 0.5f), new Vector3(10f, 4f, 1f));
            yield return rig.Run(SlideMaxDuration + 0.5f, step => SprintCrouching(step));
            Assert.That(rig.Position.z + rig.Radius, Is.LessThanOrEqualTo(face + 0.01f), "A slide shouldn't pass through a wall.");
            Assert.That(Sliding, Is.False, "Hitting a wall should end the slide.");
        }

        [UnityTest]
        public IEnumerator PressingCrouchAgainDuringASlide_StandsUp()
        {
            yield return SprintForward();
            const int pressAgainAt = 6;
            yield return rig.Run(pressAgainAt + 3, step =>
            {
                Inputs inputs = SprintCrouching(step);
                if (step == pressAgainAt) { inputs.crouch = CharacterTestRig.Held(0); }
                return inputs;
            });
            Assert.That(Sliding, Is.False, "A second press should end the slide.");
            Assert.That(rig.InState(CharacterState.Crouching), Is.False, "A second press should stand up.");
            Assert.That(rig.CurrentHeight, Is.EqualTo(rig.StandingHeight).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator ASlideMaxDurationOfZero_TurnsSlidingOff()
        {
            yield return SprintForward();
            rig.SetMovementSetting("slideMaxDuration", 0f);
            bool slid = false;
            yield return rig.Run(0.5f, step => SprintCrouching(step), _ => slid |= Sliding);
            Assert.That(slid, Is.False, "With no slide length, crouching while sprinting shouldn't slide.");
            Assert.That(rig.InState(CharacterState.Crouching), Is.True, "It should crouch instead.");
        }
    }
}
