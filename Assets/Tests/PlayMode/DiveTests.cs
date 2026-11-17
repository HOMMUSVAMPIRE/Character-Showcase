using System.Collections;
using Character;
using Level;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of diving and swimming underwater: Crouch at the surface dives under it, the body lying flat along the way it
    /// swims, resting on its feet (the model drawn lowered to it), so it swims along the floor under an arch too low to
    /// stand under, and turns in a narrow channel only as far as it has room; underwater, the character swims the way the camera looks (looking down, forward swims down), and holding
    /// Jump or Crouch rises or sinks whichever way it looks; without input it drifts up (by its buoyancy, or holds its
    /// depth with none) and is back at the surface, its collider standing again; a press of Jump underwater doesn't jump out at the surface; and a dive speed of 0 turns
    /// diving off. The test camera looks down +Z (turned here to look up or down). Expected values are read from the
    /// movement mode's settings.
    /// </summary>
    [Category(TestArea.Traversal)]
    public class DiveTests : CharacterRigFixture
    {
        // The pool: its surface at 0, deep enough to swim down a long way in.
        private const float PoolDepth = 20f, PoolSize = 80f;

        private Water water;

        private float SwimSpeed => rig.MovementSetting<float>("swimSpeed") * rig.Movement.SpeedScale;
        private float VerticalSpeed => rig.MovementSetting<float>("swimVerticalSpeed") * rig.Movement.SpeedScale;
        private bool Diving => rig.InState(CharacterState.Diving);
        private float FeetUnder => water.SurfaceHeight - rig.Position.y;

        /// <summary>Turns the test camera to look the given degrees down (negative: up), along +Z.</summary>
        private static void LookDown(float degrees) => Camera.main.transform.rotation = Quaternion.Euler(degrees, 0f, 0f);

        /// <summary>A deep pool, the character swimming at its surface, then dived under it with a press of Crouch.</summary>
        private IEnumerator DivedInThePool()
        {
            rig.Floor(-PoolDepth);
            water = rig.Water(Vector3.zero, new Vector3(PoolSize, PoolDepth, PoolSize));
            yield return rig.SpawnSettled(Vector3.up, settleSeconds: 0f);
            yield return rig.RunUntil(() => rig.Swimming && Mathf.Abs(rig.Velocity.y) < 0.05f && rig.Movement.swim.verticalSpeed > -0.05f,
                _ => CharacterTestRig.Idle(), timeoutSeconds: 5f);
            Assume.That(rig.Swimming, Is.True, "Test setup: the character should be swimming.");
            rig.ClearEvents();
            yield return rig.Run(1, _ => CharacterTestRig.Crouch(0));
            Assume.That(Diving, Is.True, "Test setup: Crouch should have dived.");
        }

        [UnityTest]
        public IEnumerator CrouchAtTheSurface_DivesUnderIt_LyingFlat()
        {
            yield return DivedInThePool();
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());
            Assert.That(Diving, Is.True);
            Assert.That(rig.EventsRaised, Does.Contain(MovementEvents.DiveStarted));
            Assert.That(rig.Movement.Posture.Kind, Is.EqualTo(BodyPostureKind.Lying), "Underwater, the body should lie flat.");
            Assert.That(rig.Capsule.direction, Is.EqualTo(2), "Lying, the capsule should lie along the way the character faces.");
            Assert.That(rig.Capsule.center.y - rig.Capsule.radius, Is.EqualTo(0f).Within(1e-4f), "Lying, the body should rest on its feet.");
            Assert.That(rig.FrontExtent, Is.GreaterThan(rig.SideExtent), "Lying, the body should reach further ahead than to the sides.");
            Assert.That(rig.Movement.lastFrameMovementData.modelDrop, Is.EqualTo(rig.SwimDepth - rig.CurrentHeight / 2f).Within(1e-3f),
                "The model should be drawn lowered from the swim depth, where the swimming animations float it, to the body's middle.");
            Assert.That(rig.Position.y + rig.CurrentHeight, Is.LessThan(water.SurfaceHeight), "The dive should take the character under the surface.");
        }

        [UnityTest]
        public IEnumerator Underwater_SwimsAlongThePoolFloor_UnderAnArchTooLowToStandUnder()
        {
            yield return DivedInThePool();
            yield return rig.RunUntil(() => rig.Grounded, step => CharacterTestRig.Crouch(step + 1), timeoutSeconds: 10f);
            Assume.That(rig.Grounded, Is.True, "Test setup: the character should have sunk to the pool's floor.");
            // An arch across the pool ahead, its underside between the lying body's top and the character's standing
            // height above the floor (as the Level Playground's is), from there to above the surface.
            float floor = rig.Position.y, underside = floor + Mathf.Lerp(rig.CurrentHeight, rig.StandingHeight, 0.5f), top = water.SurfaceHeight + 1f;
            const float at = 6f, thickness = 1f;
            rig.Box("Arch", new Vector3(0f, (underside + top) / 2f, at), new Vector3(PoolSize, top - underside, thickness));
            Physics.SyncTransforms();
            yield return rig.RunUntil(() => rig.Position.z - rig.BackExtent > at + thickness / 2f,
                step => CharacterTestRig.Crouch(step + 1, Vector3.forward), timeoutSeconds: 8f);
            Assert.That(rig.Position.z - rig.BackExtent, Is.GreaterThan(at + thickness / 2f), "Underwater, the character should swim along the floor under an arch too low to stand under.");
            Assert.That(Diving, Is.True);
        }

        [UnityTest]
        public IEnumerator Underwater_TurningInANarrowChannel_TurnsOnlyAsFarAsItHasRoom()
        {
            yield return DivedInThePool();
            rig.SetMovementSetting("swimBuoyancy", 0f);
            yield return rig.Run(1f, step => CharacterTestRig.Crouch(step + 1));
            yield return rig.RunUntil(() => Mathf.Abs(rig.Velocity.y) < 0.01f, _ => CharacterTestRig.Idle(), timeoutSeconds: 5f);
            // A channel along +Z, wider than the lying body is thick, narrower than it is long.
            float thick = 2f * rig.SideExtent, length = rig.FrontExtent + rig.BackExtent;
            float width = Mathf.Lerp(thick, length, 0.5f);
            float x = rig.Position.x;
            foreach (float side in new[] { -1f, 1f })
            {
                rig.Box(side < 0f ? "Channel Side (West)" : "Channel Side (East)",
                    new Vector3(x + side * (width / 2f + 0.5f), -PoolDepth / 2f, rig.Position.z), new Vector3(1f, PoolDepth + 2f, 40f));
            }
            Physics.SyncTransforms();
            float widest = 0f;
            bool alwaysFits = true;
            yield return rig.Run(1f, _ => CharacterTestRig.Move(Vector3.right), _ =>
            {
                widest = Mathf.Max(widest, Vector3.Angle(rig.Body.rotation * Vector3.forward, Vector3.forward));
                alwaysFits &= rig.BodyFits;
            });
            Assert.That(widest, Is.LessThan(60f), "In a channel narrower than it's long, the lying body shouldn't turn across it.");
            Assert.That(widest, Is.GreaterThan(1f), "It should still turn as far as it has room to.");
            Assert.That(alwaysFits, Is.True, "Turning shouldn't take the body into the channel's sides.");
        }

        [UnityTest]
        public IEnumerator Underwater_SwimsTheWayTheCameraLooks([Values(45f, -30f)] float lookDown)
        {
            yield return DivedInThePool();
            yield return rig.Run(1.5f, step => CharacterTestRig.Crouch(step + 1)); // Well under, so looking up doesn't surface.
            LookDown(lookDown);
            yield return rig.Run(1.5f, _ => CharacterTestRig.Move(Vector3.forward));
            Vector3 looking = Camera.main.transform.forward;
            Assert.That(Vector3.Angle(rig.Velocity, looking), Is.LessThan(2f), "Pushing forward underwater, the character should swim the way the camera looks.");
            Assert.That(rig.Velocity.magnitude, Is.EqualTo(SwimSpeed).Within(0.05f), "Underwater, the character should swim at the swim speed.");
            Assert.That(Diving, Is.True);
        }

        [UnityTest]
        public IEnumerator HoldingCrouchOrJump_SinksOrRises_WhicheverWayTheCameraLooks()
        {
            yield return DivedInThePool();
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            LookDown(-30f); // Looking up: Crouch still sinks.
            yield return rig.Run(1.5f, step => CharacterTestRig.Crouch(step + 1, Vector3.forward));
            Assert.That(rig.Velocity.y, Is.EqualTo(-VerticalSpeed).Within(0.05f), "Holding Crouch underwater should sink, even looking up.");
            Assert.That(rig.Velocity.z, Is.GreaterThan(0.5f * SwimSpeed), "The character should still swim forward meanwhile.");

            LookDown(30f); // Looking down: Jump still rises.
            yield return rig.Run(1f, step => CharacterTestRig.Jump(step + 1, Vector3.forward));
            Assert.That(rig.Velocity.y, Is.EqualTo(VerticalSpeed).Within(0.05f), "Holding Jump underwater should rise, even looking down.");
        }

        [UnityTest]
        public IEnumerator WithoutInput_DriftsBackUpToTheSurface_StandingUpAgain()
        {
            yield return DivedInThePool();
            rig.SetMovementSetting("swimBuoyancy", 0.5f); // Whatever the asset's tuned to (0 stays put: see below).
            yield return rig.RunUntil(() => !Diving, _ => CharacterTestRig.Idle(), timeoutSeconds: 15f);
            Assert.That(Diving, Is.False, "Without input, the character should drift back up to the surface.");
            Assert.That(rig.Swimming, Is.True, "Back at the surface, the character should swim there.");
            Assert.That(rig.EventsRaised, Does.Contain(MovementEvents.DiveEnded));
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());
            Assert.That(FeetUnder, Is.EqualTo(rig.SwimDepth).Within(0.02f), "At the surface, the character should float at its swimming depth.");
            Assert.That(rig.CurrentHeight, Is.EqualTo(rig.StandingHeight).Within(0.01f), "At the surface, the collider should stand again.");
        }

        [UnityTest]
        public IEnumerator ABuoyancyOfZero_StaysUnderwater_WithoutInput()
        {
            yield return DivedInThePool();
            rig.SetMovementSetting("swimBuoyancy", 0f);
            yield return rig.Run(1f, _ => CharacterTestRig.Idle()); // The dive's push wears off.
            float depth = rig.Position.y;
            yield return rig.Run(2f, _ => CharacterTestRig.Idle());
            Assert.That(Diving, Is.True, "With no buoyancy, the character should stay underwater.");
            Assert.That(rig.Position.y, Is.EqualTo(depth).Within(0.02f), "With no buoyancy, the character should hold its depth.");
        }

        [UnityTest]
        public IEnumerator PressingJumpUnderwater_RisesToTheSurface_WithoutJumpingOut()
        {
            yield return DivedInThePool();
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());
            yield return rig.RunUntil(() => !Diving, step => CharacterTestRig.Jump(step), timeoutSeconds: 5f);
            Assume.That(Diving, Is.False, "Test setup: Jump held should have risen to the surface.");
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Swimming, Is.True, "Rising with Jump, the character should arrive at the surface swimming, not jump out.");
            Assert.That(rig.EventsRaised, Has.No.Member(MovementEvents.Jumped));
        }

        [UnityTest]
        public IEnumerator ADiveSpeedOfZero_TurnsDivingOff()
        {
            rig.Floor(-PoolDepth);
            water = rig.Water(Vector3.zero, new Vector3(PoolSize, PoolDepth, PoolSize));
            yield return rig.SpawnSettled(Vector3.up, settleSeconds: 0f);
            yield return rig.RunUntil(() => rig.Swimming, _ => CharacterTestRig.Idle(), timeoutSeconds: 3f);
            rig.SetMovementSetting("swimDiveSpeed", 0f);
            bool dived = false;
            yield return rig.Run(1f, step => step == 0 ? CharacterTestRig.Crouch(0) : CharacterTestRig.Idle(), _ => dived |= Diving);
            Assert.That(dived, Is.False);
        }
    }
}
