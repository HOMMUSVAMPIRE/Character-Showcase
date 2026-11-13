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
    /// Tests of wall-running: moving fast along a wall in mid-air while steering along it runs along the wall, falling
    /// slowly, until the run lasts its longest, the wall ends (or the feet drop below its bottom edge), or steering has
    /// stopped for the release delay (not just for a moment); jumping jumps off the wall and gives back air jumps; and
    /// no wall run starts without steering mostly along the wall, heading into the wall, close to the ground, or beside a wall that doesn't
    /// reach down to the feet. The
    /// character is launched in mid-air beside a long wall on its right. Expected values are read from the movement mode.
    /// </summary>
    public class WallRunTests
    {
        private CharacterTestRig rig;
        private float wallFace;

        [SetUp]
        public void SetUp() => rig = new CharacterTestRig();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            rig.Dispose();
            yield return null;
        }

        private float WallRunSpeed => rig.Mode<WallRunningMode>().wallRunSpeed * rig.Movement.SpeedScale;
        private float WallRunMaxDuration => rig.Mode<WallRunningMode>().wallRunMaxDuration * rig.Movement.DurationScale;
        private float Gravity => rig.MovementSetting<float>("gravityStrength") * rig.Movement.AccelerationScale;
        private bool WallRunning => rig.InState(CharacterState.WallRunning);

        /// <summary>
        /// A floor, and a wall along +Z on the character's right (its face a little beside the character), wallLength
        /// long from just behind the character; the character in mid-air beside it, feet height above the floor.
        /// </summary>
        private IEnumerator SetUpWall(float height = 12f, float wallLength = 60f)
        {
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, height, -25f), settleSeconds: 0f);
            wallFace = rig.Radius + 0.1f;
            rig.Box("Wall", new Vector3(wallFace + 0.5f, 20f, -26f + wallLength / 2f), new Vector3(1f, 40f, wallLength));
        }

        /// <summary>Launches the character forward along the wall at the wall-run speed, steering forward.</summary>
        private IEnumerator LaunchAlongWall() => rig.Launch(Vector3.forward * WallRunSpeed, CharacterTestRig.Move(Vector3.forward));

        [UnityTest]
        public IEnumerator MovingAlongAWallInMidAir_RunsAlongIt_FallingSlowly()
        {
            yield return SetUpWall();
            yield return LaunchAlongWall();
            Vector3 start = rig.Position;
            const float seconds = 0.6f;
            int steps = 0, wallRunningSteps = 0;
            yield return rig.Run(seconds, _ => CharacterTestRig.Move(Vector3.forward), _ =>
            {
                steps++;
                if (WallRunning) { wallRunningSteps++; }
            });

            float drop = start.y - rig.Position.y;
            float freeFall = 0.5f * Gravity * seconds * seconds;
            Assert.That(wallRunningSteps, Is.EqualTo(steps), "The character should run along the wall.");
            Assert.That(drop, Is.LessThan(0.5f * freeFall), "A wall run should fall much slower than falling freely.");
            Assert.That(rig.Position.z - start.z, Is.GreaterThan(0.8f * WallRunSpeed * seconds), "A wall run should keep going along the wall.");
            Assert.That(rig.Position.x + rig.Radius, Is.EqualTo(wallFace).Within(0.05f), "A wall run should stay against the wall.");
            Assert.That(rig.Movement.wallRun.side, Is.EqualTo(1f), "The wall is on the character's right.");
            Assert.That(Vector3.Angle(rig.Body.rotation * Vector3.forward, Vector3.forward), Is.LessThan(2f), "The character should face along the wall.");
        }

        [UnityTest]
        public IEnumerator AWallRun_EndsAfterItsLongestTime_AndTheCharacterFalls()
        {
            yield return SetUpWall();
            yield return LaunchAlongWall();
            int wallRunningSteps = 0;
            yield return rig.Run(WallRunMaxDuration + 0.4f, _ => CharacterTestRig.Move(Vector3.forward), _ => { if (WallRunning) { wallRunningSteps++; } });
            Assert.That(wallRunningSteps, Is.EqualTo(CharacterTestRig.StepsFor(WallRunMaxDuration)).Within(2), "A wall run should last its longest time, then end.");
            Assert.That(WallRunning, Is.False);
            Assert.That(rig.Movement.lastFrameMovementData.finalVelocity.y, Is.LessThan(-1f), "After a wall run the character should fall.");
        }

        [UnityTest]
        public IEnumerator JumpingDuringAWallRun_JumpsAwayFromTheWallAndUp_AndGivesBackJumps()
        {
            yield return SetUpWall();
            yield return LaunchAlongWall();
            yield return rig.Run(0.3f, _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(WallRunning, Is.True, "Test setup: the character should be wall-running.");

            var events = new List<MovementEvents>();
            rig.Movement.MovementEventRaised += (movementEvent, _) => events.Add(movementEvent);
            Vector3 velocity = Vector3.zero;
            yield return rig.Run(3, step => CharacterTestRig.Jump(step, Vector3.forward),
                step => { if (step == 0) { velocity = rig.Movement.lastFrameMovementData.finalVelocity; } });

            float away = rig.MovementSetting<float>("wallJumpAwaySpeed") * rig.Movement.SpeedScale;
            float up = rig.MovementSetting<float>("wallJumpUpSpeed") * rig.Movement.SpeedScale;
            Assert.That(WallRunning, Is.False, "Jumping should end the wall run.");
            Assert.That(-velocity.x, Is.EqualTo(away).Within(0.1f * away), "A wall jump should push away from the wall.");
            Assert.That(velocity.y, Is.EqualTo(up).Within(0.1f * up), "A wall jump should launch upward.");
            Assert.That(velocity.z, Is.GreaterThan(0.5f * WallRunSpeed), "A wall jump should keep the run's speed along the wall.");
            Assert.That(rig.Movement.jumpsRemaining, Is.EqualTo(rig.Movement.maxJumps - 1), "A wall jump should give back the character's jumps, less itself.");
            Assert.That(events.Count(e => e == MovementEvents.WallJumped), Is.EqualTo(1));
            Assert.That(events.Count(e => e == MovementEvents.WallRunEnded), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AWallJump_CanChainIntoAWallRunOnAFacingWall()
        {
            yield return SetUpWall();
            // A second wall on the left, facing the first.
            rig.Box("Facing Wall", new Vector3(-3f - 0.5f, 20f, 4f), new Vector3(1f, 40f, 60f));
            yield return LaunchAlongWall();
            yield return rig.Run(0.2f, _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(WallRunning, Is.True, "Test setup: the character should be running along the right-hand wall.");

            // Jump, steering forward and over toward the facing wall.
            Vector3 steer = new Vector3(-0.5f, 0f, 1f).normalized;
            bool ranOnLeft = false;
            yield return rig.Run(1f, step => step == 0 ? CharacterTestRig.Jump(0, steer) : CharacterTestRig.Move(steer),
                _ => ranOnLeft |= WallRunning && rig.Movement.wallRun.side < 0f);
            Assert.That(ranOnLeft, Is.True, "A wall jump should be able to carry the character into a wall run on the facing wall.");
        }

        [UnityTest]
        public IEnumerator WithoutSteering_NoWallRunStarts()
        {
            yield return SetUpWall();
            yield return rig.Launch(Vector3.forward * WallRunSpeed, CharacterTestRig.Idle());
            bool ran = false;
            yield return rig.Run(0.4f, _ => CharacterTestRig.Idle(), _ => ran |= WallRunning);
            Assert.That(ran, Is.False, "A wall run needs steering along the wall.");
        }

        [UnityTest]
        public IEnumerator HeadingStraightIntoTheWall_NoWallRunStarts()
        {
            yield return SetUpWall();
            yield return rig.Launch(Vector3.right * WallRunSpeed, CharacterTestRig.Move(Vector3.right));
            bool ran = false;
            yield return rig.Run(0.4f, _ => CharacterTestRig.Move(Vector3.right), _ => ran |= WallRunning);
            Assert.That(ran, Is.False, "Running into a wall head-on shouldn't wall-run.");
        }

        [UnityTest]
        public IEnumerator CloseToTheGround_NoWallRunStarts()
        {
            float minHeight = 0f;
            yield return SetUpWall(height: 0.05f);
            minHeight = rig.MovementSetting<float>("wallRunMinHeight") * rig.Movement.HeightScale;
            rig.Body.position = new Vector3(0f, 0.5f * minHeight, -25f);
            bool ran = false;
            yield return rig.Launch(Vector3.forward * WallRunSpeed, CharacterTestRig.Move(Vector3.forward));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Move(Vector3.forward), _ => ran |= WallRunning);
            Assert.That(ran, Is.False, "A wall run shouldn't start close to the ground.");
        }

        [UnityTest]
        public IEnumerator WhenTheWallEnds_TheWallRunEnds_KeepingItsMomentum()
        {
            float wallLength = 4f;
            yield return SetUpWall(wallLength: wallLength);
            yield return LaunchAlongWall();
            float wallEnd = -26f + wallLength;
            yield return rig.Run((wallLength + 1f) / WallRunSpeed + 0.2f, _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(rig.Position.z - rig.Radius, Is.GreaterThan(wallEnd), "Test setup: the character should be past the wall.");
            Assert.That(WallRunning, Is.False, "Past the end of the wall, the wall run should end.");
            Assert.That(rig.Movement.lastFrameMovementData.finalVelocity.z, Is.GreaterThan(0.8f * WallRunSpeed), "The character should keep its momentum.");
        }

        [UnityTest]
        public IEnumerator AWallRunMaxDurationOfZero_TurnsWallRunningOff()
        {
            yield return SetUpWall();
            rig.SetMovementSetting("wallRunMaxDuration", 0f);
            yield return LaunchAlongWall();
            bool ran = false;
            yield return rig.Run(0.4f, _ => CharacterTestRig.Move(Vector3.forward), _ => ran |= WallRunning);
            Assert.That(ran, Is.False);
        }

        [UnityTest]
        public IEnumerator SteeringAwayForAMoment_KeepsRunning_UntilTheReleaseDelay()
        {
            yield return SetUpWall();
            yield return LaunchAlongWall();
            yield return rig.Run(0.15f, _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(WallRunning, Is.True, "Test setup: the character should be wall-running.");
            float delay = rig.MovementSetting<float>("wallRunReleaseDelay");
            Vector3 away = new Vector3(-1f, 0f, 0.3f).normalized; // The wall is on the right (+X).
            yield return rig.Run(0.5f * delay, _ => CharacterTestRig.Move(away));
            Assert.That(WallRunning, Is.True, "Steering away for a moment shouldn't drop the character off the wall.");
            yield return rig.Run(delay + 0.05f, _ => CharacterTestRig.Move(away));
            Assert.That(WallRunning, Is.False, "Steering away for the release delay should let go of the wall.");
        }

        [UnityTest]
        public IEnumerator WhenTheFeetDropBelowTheWallsBottomEdge_TheWallRunEnds()
        {
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, 12f, -25f), settleSeconds: 0f);
            wallFace = rig.Radius + 0.1f;
            // A wall hanging in the air, its bottom edge a little below the character's feet.
            float bottom = rig.Position.y - 0.4f;
            rig.Box("Hanging Wall", new Vector3(wallFace + 0.5f, bottom + 10f, 4f), new Vector3(1f, 20f, 60f));
            float footHeight = rig.MovementSetting<float>("wallRunFootHeight") * rig.StandingHeight;
            yield return LaunchAlongWall();
            bool ran = false, ranBelow = false;
            yield return rig.Run(1f, _ => CharacterTestRig.Move(Vector3.forward), _ =>
            {
                ran |= WallRunning;
                if (WallRunning && rig.Position.y + footHeight < bottom - 0.05f) { ranBelow = true; }
            });
            Assert.That(ran, Is.True, "Test setup: the character should run along the wall at first.");
            Assert.That(ranBelow, Is.False, "The wall run should end once the feet are below the wall's bottom edge.");
            Assert.That(WallRunning, Is.False, "The wall run should have ended.");
        }

        [UnityTest]
        public IEnumerator AWallAboveTheFeet_IsNotRunAlong()
        {
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, 12f, -25f), settleSeconds: 0f);
            wallFace = rig.Radius + 0.1f;
            // Beside the character's upper body only: its bottom edge at the character's waist.
            float bottom = rig.Position.y + 0.5f * rig.StandingHeight;
            rig.Box("High Wall", new Vector3(wallFace + 0.5f, bottom + 10f, 4f), new Vector3(1f, 20f, 60f));
            yield return LaunchAlongWall();
            bool ran = false;
            yield return rig.Run(0.5f, _ => CharacterTestRig.Move(Vector3.forward), _ => ran |= WallRunning);
            Assert.That(ran, Is.False, "A wall that doesn't reach down to the character's feet shouldn't be run along.");
        }

        [UnityTest]
        public IEnumerator SteeringIntoTheWallAtAnAngle_NoWallRunStarts()
        {
            yield return SetUpWall();
            // Moving along the wall, but steering well into it (the wall is on the right, +X).
            float angle = rig.MovementSetting<float>("wallRunMaxSteerAngle") + 15f;
            Vector3 steer = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            yield return rig.Launch(Vector3.forward * WallRunSpeed, CharacterTestRig.Move(steer));
            bool ran = false;
            yield return rig.Run(0.4f, _ => CharacterTestRig.Move(steer), _ => ran |= WallRunning);
            Assert.That(ran, Is.False, "A wall run needs steering mostly along the wall.");
        }
    }
}
