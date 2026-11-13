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
    /// Tests of sliding down walls: falling into a wall head-on slides slowly down it, a fast fall braked to the slide
    /// speed; jumping kicks off it upward and away, gaining height and giving back air jumps (but not off the same wall twice in a row), only so many times before landing, hops alone not climbing it, air steering coming back only gradually;
    /// no slide while rising fast, close to the ground, or glancing along the wall, but one on hitting a wall whatever the steering; a chimney climbed by kicking between its walls; and steering away (for the release delay, not
    /// just a moment), Crouch, landing or the slide's longest time end it. The character is in mid-air facing a tall wall just in front of it. Expected values
    /// are read from the movement mode's settings.
    /// </summary>
    public class WallSlideTests
    {
        private CharacterTestRig rig;
        // The wall's face (z); it faces -Z, toward the character.
        private float face;

        [SetUp]
        public void SetUp() => rig = new CharacterTestRig();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            rig.Dispose();
            yield return null;
        }

        private float SlideSpeed => rig.Mode<WallSlideMode>().wallSlideSpeed * rig.Movement.SpeedScale;
        private float MaxDuration => rig.Mode<WallSlideMode>().wallSlideMaxDuration * rig.Movement.DurationScale;
        private float Gravity => rig.MovementSetting<float>("gravityStrength") * rig.Movement.AccelerationScale;
        private bool Sliding => rig.InState(CharacterState.WallSliding);
        private Vector3 Velocity => rig.Movement.lastFrameMovementData.finalVelocity;

        /// <summary>A floor, the character in mid-air (its model loaded) facing +Z, and a tall wall just in front of it.</summary>
        private IEnumerator SetUpWall(float height = 12f)
        {
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, height, 0f), settleSeconds: 0f);
            face = rig.Radius + 0.1f;
            rig.Box("Wall", new Vector3(0f, 15f, face + 0.5f), new Vector3(10f, 30f, 1f));
        }

        /// <summary>Falls into the wall, steering into it, until sliding down it.</summary>
        private IEnumerator SlideDown()
        {
            yield return rig.Run(0.3f, _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(Sliding, Is.True, "Test setup: the character should be sliding down the wall.");
        }

        [UnityTest]
        public IEnumerator FallingIntoAWallHeadOn_SlidesSlowlyDownIt_AgainstItAndFacingIt()
        {
            yield return SetUpWall();
            Vector3 start = rig.Position;
            const float seconds = 1.2f;
            int steps = 0, slidingSteps = 0;
            float fastestFall = 0f;
            yield return rig.Run(seconds, _ => CharacterTestRig.Move(Vector3.forward), _ =>
            {
                steps++;
                if (Sliding) { slidingSteps++; }
                fastestFall = Mathf.Max(fastestFall, -Velocity.y);
            });

            float drop = start.y - rig.Position.y;
            Assert.That(slidingSteps, Is.EqualTo(steps), "Falling into the wall head-on should slide down it.");
            Assert.That(fastestFall, Is.LessThanOrEqualTo(SlideSpeed + 0.01f), "The slide should be no faster than the slide speed.");
            Assert.That(drop, Is.LessThan(0.5f * 0.5f * Gravity * seconds * seconds), "Sliding down a wall should be much slower than falling.");
            Assert.That(drop, Is.GreaterThan(0.5f * SlideSpeed * seconds), "The character should slide down, not stick.");
            Assert.That(rig.Position.z + rig.Radius, Is.EqualTo(face).Within(0.05f), "The character should stay against the wall.");
            Assert.That(Vector3.Angle(rig.Body.rotation * Vector3.forward, Vector3.forward), Is.LessThan(1f), "The character should face the wall.");
        }

        [UnityTest]
        public IEnumerator AFastFallIntoAWall_IsBrakedToTheSlideSpeed()
        {
            yield return SetUpWall(height: 20f);
            yield return rig.Launch(Vector3.down * (4f * SlideSpeed), CharacterTestRig.Move(Vector3.forward));
            float braking = rig.MovementSetting<float>("wallSlideBraking") * rig.Movement.AccelerationScale;
            yield return rig.Run(3f * SlideSpeed / braking + 0.2f, _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(Sliding, Is.True, "Test setup: the character should be sliding.");
            Assert.That(-Velocity.y, Is.EqualTo(SlideSpeed).Within(0.01f), "A fast fall should be braked down to the slide speed.");
        }

        [UnityTest]
        public IEnumerator JumpingOffAWall_KicksUpAndAway_GainingHeight_AndGivesBackJumps()
        {
            yield return SetUpWall();
            yield return SlideDown();
            var events = new List<MovementEvents>();
            rig.Movement.MovementEventRaised += (movementEvent, _) => events.Add(movementEvent);

            float jumpedFrom = rig.Position.y, highest = jumpedFrom;
            Vector3 launch = Vector3.zero;
            int jumpsAfter = -1;
            yield return rig.Run(0.8f, step => step == 0 ? CharacterTestRig.Jump(0) : CharacterTestRig.Idle(), step =>
            {
                if (step == 0) { launch = Velocity; jumpsAfter = rig.Movement.jumpsRemaining; }
                highest = Mathf.Max(highest, rig.Position.y);
            });

            float up = rig.MovementSetting<float>("wallSlideJumpUpSpeed") * rig.Movement.SpeedScale;
            float away = rig.MovementSetting<float>("wallSlideJumpAwaySpeed") * rig.Movement.SpeedScale;
            Assert.That(launch.y, Is.EqualTo(up).Within(0.05f * up), "Jumping off the wall should launch upward.");
            Assert.That(-launch.z, Is.EqualTo(away).Within(0.05f * away), "Jumping off the wall should push away from it.");
            Assert.That(highest - jumpedFrom, Is.GreaterThan(0.25f * up * up / Gravity), "Jumping off the wall should gain height.");
            Assert.That(jumpsAfter, Is.EqualTo(rig.Movement.maxJumps - 1), "Jumping off the wall should give back the character's jumps, less itself.");
            Assert.That(events.Count(e => e == MovementEvents.WallJumped), Is.EqualTo(1));
            Assert.That(events.Count(e => e == MovementEvents.WallSlideEnded), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator HoppingUpAWall_OnlyJumpsOffItSoManyTimes_BeforeLanding()
        {
            yield return SetUpWall();
            // Kicking straight up, so each jump comes back to the wall to slide on again.
            rig.SetMovementSetting("wallSlideJumpAwaySpeed", 0f);
            int maxJumps = rig.MovementSetting<int>("wallSlideMaxJumps");
            int wallJumps = 0;
            rig.Movement.MovementEventRaised += (movementEvent, _) => { if (movementEvent == MovementEvents.WallJumped) { wallJumps++; } };
            // Keep steering into the wall, pressing Jump again and again: each jump kicks off, then slides on again.
            yield return rig.Run(6f, step =>
            {
                Inputs inputs = CharacterTestRig.Move(Vector3.forward);
                if (step % 25 == 24) { inputs.jump = CharacterTestRig.Held(0); }
                return inputs;
            });
            Assert.That(wallJumps, Is.EqualTo(maxJumps), "Only so many jumps off walls should be allowed before landing.");
        }

        [UnityTest]
        public IEnumerator RisingFastIntoAWall_DoesNotSlideDownIt_UntilNearTheTopOfTheRise()
        {
            yield return SetUpWall();
            yield return rig.Launch(Vector3.up * 8f, CharacterTestRig.Move(Vector3.forward));
            float maxRise = rig.MovementSetting<float>("wallSlideMaxStartRiseSpeed") * rig.Movement.SpeedScale;
            bool startedRisingFast = false, slid = false;
            yield return rig.Run(1.5f, _ => CharacterTestRig.Move(Vector3.forward), _ =>
            {
                if (Sliding && !slid && rig.Movement.lastFrameMovementData.initialInertia.y > maxRise + 0.01f) { startedRisingFast = true; }
                slid |= Sliding;
            });
            Assert.That(startedRisingFast, Is.False, "Rising fast into a wall shouldn't slide down it.");
            Assert.That(slid, Is.True, "Near the top of the rise, the character should slide down the wall.");
        }

        [UnityTest]
        public IEnumerator HittingAWallMovingIntoIt_SlidesDownIt_WhateverTheSteering()
        {
            yield return SetUpWall();
            // Just short of the wall, so the character hits it before steering bends its path. Steering further from
            // head-on than a slide allows: hitting the wall head-on is what starts it.
            rig.Body.position += Vector3.back * 0.15f;
            Vector3 steer = Quaternion.Euler(0f, rig.MovementSetting<float>("wallSlideMaxAngle") + 15f, 0f) * Vector3.forward;
            yield return rig.Launch(Vector3.forward * 6f, CharacterTestRig.Move(steer));
            bool slid = false;
            yield return rig.Run(0.5f, _ => CharacterTestRig.Move(steer), _ => slid |= Sliding);
            Assert.That(slid, Is.True, "Hitting a wall moving into it should slide down it, whatever the steering.");
        }

        [UnityTest]
        public IEnumerator AChimney_CanBeClimbedByKickingBetweenItsWalls()
        {
            // As the Proving Ground's: a slot 2.5 m wide, 10.5 m deep from its floor to its top.
            const float width = 2.5f, depth = 10.5f;
            rig.Floor();
            rig.Box("Chimney (West)", new Vector3(-width / 2f - 0.5f, 15f, 0f), new Vector3(1f, 30f, 8f));
            rig.Box("Chimney (East)", new Vector3(width / 2f + 0.5f, 15f, 0f), new Vector3(1f, 30f, 8f));
            yield return rig.SpawnSettled(new Vector3(0f, 1.5f, 0f), settleSeconds: 0f);
            float start = rig.Position.y, highest = start;

            // Into the west wall; each time a slide takes hold, kick off toward the other wall, steering at it.
            Vector3 steer = Vector3.left;
            int slidingSteps = 0;
            yield return rig.Run(8f, _ =>
            {
                if (slidingSteps >= 2)
                {
                    steer = rig.Movement.wallSlide.wallNormal;
                    return CharacterTestRig.Jump(0, steer);
                }
                return CharacterTestRig.Move(steer);
            }, _ =>
            {
                slidingSteps = Sliding ? slidingSteps + 1 : 0;
                highest = Mathf.Max(highest, rig.Position.y);
            });
            Assert.That(highest - start, Is.GreaterThan(depth), "Kicking between a chimney's walls should climb it.");
        }

        [UnityTest]
        public IEnumerator CloseToTheGround_NoSlideStarts()
        {
            yield return SetUpWall(height: 0.2f);
            bool slid = false;
            yield return rig.Run(0.5f, _ => CharacterTestRig.Move(Vector3.forward), _ => slid |= Sliding);
            Assert.That(slid, Is.False, "Bumping into a wall just above the ground shouldn't slide down it.");
        }

        [UnityTest]
        public IEnumerator GlancingAlongAWall_DoesNotSlideDownIt()
        {
            yield return SetUpWall();
            float glancing = rig.MovementSetting<float>("wallSlideMaxAngle") + 15f;
            Vector3 steer = Quaternion.Euler(0f, glancing, 0f) * Vector3.forward;
            bool slid = false;
            yield return rig.Run(0.5f, _ => CharacterTestRig.Move(steer), _ => slid |= Sliding);
            Assert.That(slid, Is.False, "Heading along a wall rather than into it shouldn't slide down it.");
        }

        [UnityTest]
        public IEnumerator SteeringAway_OrPressingCrouch_LetsGoOfTheWall()
        {
            yield return SetUpWall();
            yield return SlideDown();
            float delay = rig.MovementSetting<float>("wallSlideReleaseDelay");
            yield return rig.Run(0.5f * delay, _ => CharacterTestRig.Move(Vector3.back));
            Assert.That(Sliding, Is.True, "Steering away for a moment shouldn't let go of the wall yet.");
            yield return rig.Run(0.2f, _ => CharacterTestRig.Move(Vector3.forward));
            yield return rig.Run(delay + 0.05f, _ => CharacterTestRig.Move(Vector3.back));
            Assert.That(Sliding, Is.False, "Steering away from the wall for the release delay should let go of it.");

            yield return rig.Run(0.6f, _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(Sliding, Is.True, "Test setup: the character should be sliding again.");
            yield return rig.Run(2, step => step == 0 ? CharacterTestRig.Crouch(0, Vector3.forward) : CharacterTestRig.Move(Vector3.forward));
            Assert.That(Sliding, Is.False, "Pressing Crouch should let go of the wall.");
        }

        [UnityTest]
        public IEnumerator ASlide_EndsOnLanding_AndAfterItsLongestTime()
        {
            yield return SetUpWall(height: 3f);
            yield return SlideDown();
            yield return rig.Run(3f / SlideSpeed + 0.5f, _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(rig.Grounded, Is.True, "The character should slide down to the ground.");
            Assert.That(Sliding, Is.False, "Landing should end the slide.");

            // From high up, the slide lasts only its longest time.
            rig.Body.position = new Vector3(0f, 25f, 0f);
            int slidingSteps = 0;
            yield return rig.Run(MaxDuration + 0.5f, _ => CharacterTestRig.Move(Vector3.forward), _ => { if (Sliding) { slidingSteps++; } });
            Assert.That(slidingSteps, Is.EqualTo(CharacterTestRig.StepsFor(MaxDuration)).Within(2), "A slide should last no longer than its longest time.");
        }

        [UnityTest]
        public IEnumerator AWallSlideMaxDurationOfZero_TurnsWallSlidingOff()
        {
            yield return SetUpWall();
            rig.SetMovementSetting("wallSlideMaxDuration", 0f);
            bool slid = false;
            yield return rig.Run(0.5f, _ => CharacterTestRig.Move(Vector3.forward), _ => slid |= Sliding);
            Assert.That(slid, Is.False);
        }

        [UnityTest]
        public IEnumerator HoppingUpAWall_WithoutJumpsOfItsOwn_DoesNotClimbIt()
        {
            yield return SetUpWall(height: 20f);
            // Keep steering into the wall, pressing Jump again and again: each kick carries the character away, and with
            // no jumps of its own given back, it comes back to the wall lower each time.
            var kickHeights = new List<float>();
            rig.Movement.MovementEventRaised += (movementEvent, _) => { if (movementEvent == MovementEvents.WallJumped) { kickHeights.Add(rig.Position.y); } };
            yield return rig.Run(4f, step =>
            {
                Inputs inputs = CharacterTestRig.Move(Vector3.forward);
                if (step % 25 == 24) { inputs.jump = CharacterTestRig.Held(0); }
                return inputs;
            });
            Assert.That(kickHeights.Count, Is.GreaterThan(0), "Test setup: the character should jump off the wall.");
            for (int i = 1; i < kickHeights.Count; i++)
            {
                Assert.That(kickHeights[i], Is.LessThan(kickHeights[i - 1]), "Each jump off the wall should be lower than the last: hops alone shouldn't climb it.");
            }
        }

        [UnityTest]
        public IEnumerator AfterJumpingOff_AirSteeringComesBackGradually()
        {
            yield return SetUpWall();
            yield return SlideDown();
            float away = rig.MovementSetting<float>("wallSlideJumpAwaySpeed") * rig.Movement.SpeedScale;
            // Jump, then steer straight back into the wall.
            yield return rig.Run(0.15f, step => step == 0 ? CharacterTestRig.Jump(0) : CharacterTestRig.Move(Vector3.forward));
            Assert.That(-Velocity.z, Is.GreaterThan(0.8f * away), "Steering back right after jumping off shouldn't undo the push away from the wall.");
            float recovery = rig.MovementSetting<float>("wallJumpSteeringRecovery") * rig.Movement.DurationScale;
            yield return rig.Run(recovery, _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(Velocity.z, Is.GreaterThan(-0.3f * away), "Once air steering has come back, steering should turn the character back.");
        }

        [UnityTest]
        public IEnumerator JumpingOffTheSameWallAgain_DoesNotGiveBackJumps()
        {
            yield return SetUpWall();
            // Kicking straight up, so the character comes back to the same wall.
            rig.SetMovementSetting("wallSlideJumpAwaySpeed", 0f);
            yield return SlideDown();
            yield return rig.Run(1, _ => CharacterTestRig.Jump(0, Vector3.forward));
            Assert.That(rig.Movement.jumpsRemaining, Is.EqualTo(rig.Movement.maxJumps - 1), "Test setup: the first jump off the wall should give back jumps.");
            // Use an air jump, then slide down the same wall again and jump off it.
            rig.Movement.jumpsRemaining = 0;
            yield return rig.Run(1.2f, _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(Sliding, Is.True, "Test setup: the character should be sliding down the wall again.");
            yield return rig.Run(1, _ => CharacterTestRig.Jump(0, Vector3.forward));
            Assert.That(rig.Movement.jumpsRemaining, Is.Zero, "Jumping off the same wall again shouldn't give back jumps.");
        }
    }
}
