using System.Collections;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of rolling: a roll goes in the input direction (or straight ahead) for its duration, low, without steering;
    /// it only happens on the ground, once per press and not again until its cooldown is over; it ends standing, or
    /// crouched where there's no room to stand; and it scales with the character's size. Expected values are read from
    /// the movement mode's settings.
    /// </summary>
    public class RollTests
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

        private float RollSpeed => rig.Mode<RollMode>().rollSpeed * rig.Movement.SpeedScale;
        private float RollDuration => rig.Mode<RollMode>().rollDuration * rig.Movement.DurationScale;
        private int RollSteps => CharacterTestRig.StepsFor(RollDuration);
        private float CrouchHeight => rig.Mode<CrouchMode>().crouchHeight;
        private bool Rolling => rig.InState(CharacterState.Rolling);

        private IEnumerator Stand()
        {
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
        }

        /// <summary>Presses roll for one step with the given input, keeps the input up for the roll, and counts the steps spent rolling.</summary>
        private IEnumerator RollOnce(Vector3 direction, System.Action<int> rollingSteps, Vector3? inputDuringRoll = null)
        {
            int count = 0;
            Vector3 during = inputDuringRoll ?? direction;
            yield return rig.Run(RollSteps + 2, step => step == 0 ? CharacterTestRig.Roll(0, direction) : CharacterTestRig.Move(during),
                _ => { if (Rolling) { count++; } });
            rollingSteps(count);
        }

        [UnityTest]
        public IEnumerator Roll_GoesInTheInputDirection_LowAndFast_ForItsDuration()
        {
            yield return Stand();
            float standing = rig.StandingHeight;
            Vector3 start = rig.Position;
            float lowest = float.PositiveInfinity;
            int steps = 0;
            yield return rig.Run(RollSteps + 2, step => step == 0 ? CharacterTestRig.Roll(0, Vector3.right) : CharacterTestRig.Idle(), _ =>
            {
                if (Rolling) { steps++; lowest = Mathf.Min(lowest, rig.CurrentHeight); }
            });

            Vector3 travelled = rig.Position - start;
            float curveFloor = rig.MovementSetting<AnimationCurve>("rollSpeedCurve").keys[^1].value;
            Assert.That(steps, Is.EqualTo(RollSteps).Within(1), "A roll should last its duration.");
            Assert.That(travelled.x, Is.GreaterThan(0.9f * Mathf.Min(1f, curveFloor) * RollSpeed * RollDuration), "A roll should cover ground at its speed.");
            Assert.That(travelled.x, Is.GreaterThan(1.2f * rig.WalkSpeed * RollDuration), "A roll should be faster than walking.");
            Assert.That(Mathf.Abs(travelled.z), Is.LessThan(0.01f), "A roll should go straight in the input direction.");
            Assert.That(Vector3.Angle(rig.Body.rotation * Vector3.forward, Vector3.right), Is.LessThan(1f), "The character should face the roll.");
            Assert.That(lowest, Is.EqualTo(standing * CrouchHeight).Within(0.01f), "A roll should be low, at the crouch height.");
            Assert.That(rig.CurrentHeight, Is.EqualTo(standing).Within(0.01f), "After a roll the character should stand up again.");
        }

        [UnityTest]
        public IEnumerator RollWithoutInput_GoesTheWayTheCharacterFaces_AndCantBeSteered()
        {
            yield return Stand();
            Vector3 start = rig.Position;
            // No input on the press, then steering hard to the right through the roll.
            int steps = 0;
            yield return RollOnce(Vector3.zero, n => steps = n, inputDuringRoll: Vector3.right);
            Vector3 travelled = rig.Position - start;
            Assert.That(steps, Is.GreaterThan(0), "Test setup: the character should have rolled.");
            Assert.That(travelled.z, Is.GreaterThan(0.5f * RollSpeed * RollDuration), "A roll without input should go forward.");
            // Only the roll's last step (handing over to walking) and the steps after it walk right.
            Assert.That(Mathf.Abs(travelled.x), Is.LessThan(3.5f * rig.WalkSpeed * Time.fixedDeltaTime), "A roll shouldn't be steered.");
        }

        [UnityTest]
        public IEnumerator HoldingRoll_OnlyRollsOnce_AndAnotherWaitsForTheCooldown()
        {
            yield return Stand();
            int rollStarts = 0;
            bool was = false;
            yield return rig.Run(RollDuration * 4f, step => CharacterTestRig.Roll(step), _ =>
            {
                if (Rolling && !was) { rollStarts++; }
                was = Rolling;
            });
            Assert.That(rollStarts, Is.EqualTo(1), "Holding the button shouldn't repeat the roll.");

            // A press during the cooldown, just before it ends (within the input buffer), rolls as soon as it's over.
            float cooldown = rig.MovementSetting<float>("rollCooldown");
            float buffer = rig.MovementSetting<float>("rollInputBuffer");
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());
            int cooldownEnds = RollSteps + CharacterTestRig.StepsFor(cooldown);
            int pressAt = cooldownEnds - CharacterTestRig.StepsFor(0.5f * buffer);
            if (pressAt <= RollSteps) { Assert.Ignore("The roll's cooldown is too short to press during it."); }
            int secondRollAt = -1;
            yield return rig.Run(cooldownEnds + RollSteps, step =>
                    step == 0 ? CharacterTestRig.Roll(0) : step >= pressAt ? CharacterTestRig.Roll(step - pressAt) : CharacterTestRig.Idle(),
                step => { if (step >= pressAt && secondRollAt < 0 && Rolling) { secondRollAt = step; } });
            Assert.That(secondRollAt, Is.GreaterThan(0), "A press just before the cooldown ends should be buffered.");
            Assert.That(secondRollAt, Is.EqualTo(cooldownEnds).Within(1), "The buffered roll should start as the cooldown ends, not during it.");
        }

        [UnityTest]
        public IEnumerator RollingWhileMoving_HandsStraightOverToWalking_WithoutABurstOfSpeed()
        {
            yield return Stand();
            float endSpeed = RollSpeed * rig.MovementSetting<AnimationCurve>("rollSpeedCurve").Evaluate(1f);
            float fastest = 0f, slowest = float.PositiveInfinity;
            bool rolled = false;
            yield return rig.Run(RollSteps + CharacterTestRig.StepsFor(0.6f), step => step == 0 ? CharacterTestRig.Roll(0, Vector3.forward) : CharacterTestRig.Move(Vector3.forward), step =>
            {
                if (Rolling) { rolled = true; return; }
                if (!rolled) { return; }
                fastest = Mathf.Max(fastest, rig.HorizontalSpeed);
                slowest = Mathf.Min(slowest, rig.HorizontalSpeed);
            });
            Assert.That(rolled, Is.True, "Test setup: the character should have rolled.");
            Assert.That(fastest, Is.LessThanOrEqualTo(1.02f * Mathf.Max(rig.WalkSpeed, endSpeed)), "Walking on out of a roll shouldn't add the roll's momentum on top of the walk.");
            Assert.That(slowest, Is.GreaterThan(0.95f * Mathf.Min(rig.WalkSpeed, endSpeed)), "Walking on out of a roll shouldn't stall.");
            Assert.That(rig.HorizontalSpeed, Is.EqualTo(rig.WalkSpeed).Within(0.03f * rig.WalkSpeed), "The character should settle into walking.");
        }

        [UnityTest]
        public IEnumerator RollingInTheAir_DoesNothing()
        {
            rig.SpawnCharacter(new Vector3(0f, 20f, 0f));
            bool rolled = false;
            yield return rig.Run(0.4f, step => CharacterTestRig.Roll(step, Vector3.forward), _ => rolled |= Rolling);
            Assert.That(rolled, Is.False, "A roll only happens on the ground.");
        }

        [UnityTest]
        public IEnumerator RollingUnderALowCeiling_EndsCrouched_ThenStandsOnceThereIsRoom()
        {
            yield return Stand();
            float standing = rig.StandingHeight;
            // A short low ceiling the roll ends under: from just ahead to past where the roll ends.
            float underside = standing * Mathf.Lerp(CrouchHeight, 1f, 0.5f);
            float start = rig.Radius + 0.2f, length = RollSpeed * RollDuration + 1f;
            rig.Box("Low Ceiling", new Vector3(0f, underside + 0.5f, start + length / 2f), new Vector3(4f, 1f, length));

            yield return RollOnce(Vector3.forward, _ => { }, Vector3.zero);
            Assert.That(rig.Position.z, Is.LessThan(start + length - rig.Radius), "Test setup: the roll should end under the ceiling.");
            Assert.That(rig.InState(CharacterState.Crouching), Is.True, "With no room to stand, the roll should end crouched.");
            Assert.That(rig.CurrentHeight, Is.LessThan(underside));

            float crouchSpeed = rig.Mode<CrouchMode>().crouchSpeed * rig.Movement.SpeedScale;
            yield return rig.Run((length + rig.Radius) / crouchSpeed + 0.5f, _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(rig.InState(CharacterState.Crouching), Is.False, "Out from under the ceiling, the character should stand up by itself.");
        }

        [UnityTest]
        public IEnumerator RollAtTwiceTheSize_GoesTwiceAsFar()
        {
            yield return Stand();
            Vector3 start = rig.Position;
            yield return RollOnce(Vector3.forward, _ => { }, Vector3.zero);
            float normal = rig.Position.z - start.z;

            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());
            rig.Controller.SetScale(Vector3.one * 2f, immediately: true);
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            if (rig.Movement.SizeScaling != MovementSizeScaling.Proportional) { Assert.Ignore("The character's size scaling isn't proportional."); }
            start = rig.Position;
            yield return RollOnce(Vector3.forward, _ => { }, Vector3.zero);
            float doubled = rig.Position.z - start.z;

            Assert.That(doubled, Is.EqualTo(2f * normal).Within(0.05f * normal), "A roll should scale with the character's size.");
        }

        private float LandingRollMinFall => rig.MovementSetting<float>("landingRollMinFall") * rig.StandingHeight;

        /// <summary>A floor, and the character in mid-air above it, its feet the given share of the landing roll's fall above the floor.</summary>
        private IEnumerator SpawnAbove(float shareOfLandingRollFall)
        {
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, 30f, 0f), settleSeconds: 0f);
            rig.Body.position = new Vector3(0f, shareOfLandingRollFall * LandingRollMinFall, 0f);
            yield return null;
        }

        /// <summary>
        /// Drops the character from the given share of the landing roll's fall, launched along +X at speed with the given
        /// steering, and records whether it rolled, which way, and how many steps after touching down it started.
        /// </summary>
        private IEnumerator DropOnto(float shareOfLandingRollFall, float speed, Vector3 steer, LandingRecord record)
        {
            yield return SpawnAbove(shareOfLandingRollFall);
            yield return rig.Launch(Vector3.right * speed, CharacterTestRig.Move(steer));
            int sinceLanding = -1;
            yield return rig.Run(2f, _ => CharacterTestRig.Move(steer), _ =>
            {
                if (sinceLanding < 0 && rig.Grounded) { sinceLanding = 0; }
                else if (sinceLanding >= 0) { sinceLanding++; }
                if (Rolling && record.stepsAfterLanding < 0)
                {
                    record.stepsAfterLanding = Mathf.Max(0, sinceLanding);
                    record.direction = rig.Movement.roll.direction;
                }
            });
        }

        private class LandingRecord
        {
            public int stepsAfterLanding = -1;
            public Vector3 direction;
            public bool Rolled => stepsAfterLanding >= 0;
        }

        [UnityTest]
        public IEnumerator LandingFromAHighFall_WhileMoving_RollsOnImpact_TheWayItsGoing()
        {
            var record = new LandingRecord();
            yield return DropOnto(1.2f, 5f, Vector3.right, record);
            Assert.That(record.Rolled, Is.True, "Landing from a high fall while moving should roll.");
            Assert.That(record.stepsAfterLanding, Is.LessThanOrEqualTo(1), "The roll should start on impact.");
            Assert.That(Vector3.Angle(record.direction, Vector3.right), Is.LessThan(5f), "The roll should go the way the character was moving.");
        }

        [UnityTest]
        public IEnumerator LandingFromAHighFall_StandingStill_DoesNotRoll()
        {
            var record = new LandingRecord();
            yield return DropOnto(1.2f, 0f, Vector3.zero, record);
            Assert.That(record.Rolled, Is.False, "Landing from a fall without moving shouldn't roll.");
        }

        [UnityTest]
        public IEnumerator LandingFromALowFall_WhileMoving_DoesNotRoll()
        {
            var record = new LandingRecord();
            yield return DropOnto(0.6f, 5f, Vector3.right, record);
            Assert.That(record.Rolled, Is.False, "Landing from a fall lower than the landing roll's shouldn't roll.");
        }

        [UnityTest]
        public IEnumerator LandingFromAHighFall_WithAJumpPressed_JumpsRatherThanRolling()
        {
            yield return SpawnAbove(1.2f);
            yield return rig.Launch(Vector3.right * 5f, CharacterTestRig.Move(Vector3.right));
            var events = new System.Collections.Generic.List<MovementEvents>();
            rig.Movement.MovementEventRaised += (e, _) => events.Add(e);
            // Fall, pressing Jump just before landing (within its buffer), and holding it.
            bool rolled = false;
            int held = -1;
            yield return rig.Run(2f, _ =>
            {
                if (held >= 0 || rig.Position.y < 0.3f) { held++; return CharacterTestRig.Jump(held, Vector3.right); }
                return CharacterTestRig.Move(Vector3.right);
            }, _ => rolled |= Rolling);
            Assert.That(events.Contains(MovementEvents.Jumped), Is.True, "Test setup: the character should have jumped on landing.");
            Assert.That(rolled, Is.False, "A jump pressed for the landing should jump, not roll.");
        }

        [UnityTest]
        public IEnumerator AJumpOnFlatGround_DoesNotRollOnLanding()
        {
            yield return Stand();
            bool rolled = false;
            yield return rig.Run(2f, step => CharacterTestRig.Jump(step, Vector3.right), _ => rolled |= Rolling);
            Assert.That(rolled, Is.False, "Landing from an ordinary jump on flat ground shouldn't roll.");
        }

        [UnityTest]
        public IEnumerator ADashJustBeforeLanding_StartsTheFallAgain_SoTheLandingDoesNotRoll()
        {
            yield return SpawnAbove(1.5f);
            bool rolled = false, dashed = false;
            int dashStep = -1;
            yield return rig.Run(3f, step =>
            {
                // An air dash along +X once the feet are a third of the landing roll's fall above the floor.
                if (dashStep < 0 && rig.Position.y < LandingRollMinFall / 3f) { dashStep = step; }
                return dashStep == step ? CharacterTestRig.Dash(0, Vector3.right) : CharacterTestRig.Move(Vector3.right);
            }, _ => { rolled |= Rolling; dashed |= rig.Dashing; });
            Assert.That(dashed, Is.True, "Test setup: the character should have dashed.");
            Assert.That(rolled, Is.False, "A dash just before landing should start the fall again from it, too low to roll.");
        }

        [UnityTest]
        public IEnumerator AnAirJumpJustBeforeLanding_StartsTheFallAgain_SoTheLandingDoesNotRoll()
        {
            yield return SpawnAbove(1.5f);
            if (rig.Movement.maxJumps < 1) { Assert.Ignore("The character has no jumps."); }
            yield return rig.Launch(Vector3.right * 5f, CharacterTestRig.Move(Vector3.right));
            rig.Movement.jumpsRemaining = rig.Movement.maxJumps; // Never on the ground yet: as if it had walked off a ledge.
            var events = new System.Collections.Generic.List<MovementEvents>();
            rig.Movement.MovementEventRaised += (e, _) => events.Add(e);
            bool rolled = false;
            int jumpStep = -1;
            yield return rig.Run(3f, step =>
            {
                // A tap of an air jump half a metre above the floor.
                if (jumpStep < 0 && rig.Position.y < 0.5f && !rig.Grounded) { jumpStep = step; }
                return jumpStep == step ? CharacterTestRig.Jump(0, Vector3.right) : CharacterTestRig.Move(Vector3.right);
            }, _ => rolled |= Rolling);
            Assert.That(events.Contains(MovementEvents.AirJumped), Is.True, "Test setup: the character should have air jumped.");
            Assert.That(rolled, Is.False, "A jump just before landing should start the fall again from its top, too low to roll.");
        }
    }
}
