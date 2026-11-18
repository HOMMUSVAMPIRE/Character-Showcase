using System.Collections;
using Character;
using Level;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of diving and swimming underwater: Crouch at the surface dives under it, the body lying flat along the way it
    /// swims, resting on its feet (the model drawn lowered to it), so it swims along the floor under an arch too low to
    /// stand under, and turns in a narrow channel only as far as it has room; underwater, the character swims the way the camera looks (looking down, forward swims down), and holding
    /// Jump or Crouch rises or sinks whichever way it looks; without input it drifts up (by its buoyancy, or holds its
    /// depth with none) and is back at the surface, its body standing again; a press of Jump underwater doesn't jump out at the surface; and a dive speed of 0 turns
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

        /// <summary>Turns the inputs' look the given degrees down (negative: up), along +Z.</summary>
        private void LookDown(float degrees) => rig.Look = Quaternion.Euler(degrees, 0f, 0f);

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
            yield return rig.Run(1, _ => CharacterTestRig.Crouch());
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
            Assert.That(rig.BodyNow.direction, Is.EqualTo(2), "Lying, the capsule should lie along the way the character faces.");
            Assert.That(rig.BodyNow.center.y - rig.BodyNow.radius, Is.EqualTo(0f).Within(1e-4f), "Lying, the body should rest on its feet.");
            Assert.That(rig.FrontExtent, Is.GreaterThan(rig.SideExtent), "Lying, the body should reach further ahead than to the sides.");
            Assert.That(rig.Movement.lastFrameMovementData.modelDrop, Is.EqualTo(rig.SwimDepth - rig.CurrentHeight / 2f).Within(1e-3f),
                "The model should be drawn lowered from the swim depth, where the swimming animations float it, to the body's middle.");
            Assert.That(rig.Position.y + rig.CurrentHeight, Is.LessThan(water.SurfaceHeight), "The dive should take the character under the surface.");
        }

        [UnityTest]
        public IEnumerator Underwater_SwimsAlongThePoolFloor_UnderAnArchTooLowToStandUnder()
        {
            yield return DivedInThePool();
            yield return rig.RunUntil(() => rig.Grounded, step => CharacterTestRig.Crouch(), timeoutSeconds: 10f);
            Assume.That(rig.Grounded, Is.True, "Test setup: the character should have sunk to the pool's floor.");
            // An arch across the pool ahead, its underside between the lying body's top and the character's standing
            // height above the floor (as the Level Playground's is), from there to above the surface.
            float floor = rig.Position.y, underside = floor + Mathf.Lerp(rig.CurrentHeight, rig.StandingHeight, 0.5f), top = water.SurfaceHeight + 1f;
            const float at = 6f, thickness = 1f;
            rig.Box("Arch", new Vector3(0f, (underside + top) / 2f, at), new Vector3(PoolSize, top - underside, thickness));
            Physics.SyncTransforms();
            yield return rig.RunUntil(() => rig.Position.z - rig.BackExtent > at + thickness / 2f,
                step => CharacterTestRig.Crouch(Vector3.forward), timeoutSeconds: 8f);
            Assert.That(rig.Position.z - rig.BackExtent, Is.GreaterThan(at + thickness / 2f), "Underwater, the character should swim along the floor under an arch too low to stand under.");
            Assert.That(Diving, Is.True);
        }

        [UnityTest]
        public IEnumerator Underwater_TurningInANarrowChannel_TurnsOnlyAsFarAsItHasRoom()
        {
            yield return DivedInThePool();
            rig.SetMovementSetting("swimBuoyancy", 0f);
            yield return rig.Run(1f, step => CharacterTestRig.Crouch());
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
        public IEnumerator Underwater_SwimsTheWayItsInputLooks([Values(45f, -30f)] float lookDown)
        {
            yield return DivedInThePool();
            yield return rig.Run(1.5f, step => CharacterTestRig.Crouch()); // Well under, so looking up doesn't surface.
            LookDown(lookDown);
            yield return rig.Run(1.5f, _ => CharacterTestRig.Move(Vector3.forward));
            Vector3 looking = rig.Look * Vector3.forward;
            Assert.That(Vector3.Angle(rig.Velocity, looking), Is.LessThan(2f), "Pushing forward underwater, the character should swim the way its input looks.");
            Assert.That(rig.Velocity.magnitude, Is.EqualTo(SwimSpeed).Within(0.05f), "Underwater, the character should swim at the swim speed.");
            Assert.That(Diving, Is.True);
        }

        [UnityTest]
        public IEnumerator Underwater_SteeredUpOrDown_PitchesTheBodyAlongTheWayItSwims_UpToTheMaxPitch([Values(30f, 80f, -45f)] float lookDown)
        {
            yield return DivedInThePool();
            yield return rig.Run(1.5f, step => CharacterTestRig.Crouch()); // Well under, so looking up doesn't surface.
            LookDown(lookDown);
            yield return rig.Run(0.5f, _ => CharacterTestRig.Move(Vector3.forward));
            float maxPitch = rig.MovementSetting<float>("swimMaxPitch");
            Assert.That(rig.Movement.BodyPitch, Is.EqualTo(Mathf.Clamp(-lookDown, -maxPitch, maxPitch)).Within(0.5f),
                "Steered, the body should pitch along the way it swims, no further than the swim max pitch.");
            Assert.That(rig.Body.rotation * Vector3.up, Is.EqualTo(Vector3.up).Using(Vector3EqualityComparer.Instance),
                "Only the body pitches: the character itself stays upright.");
            Assert.That(rig.BodyFits, Is.True);

            yield return rig.Run(0.2f, step => CharacterTestRig.Crouch());
            Assert.That(rig.Movement.BodyPitch, Is.EqualTo(0f), "Sinking with Crouch alone isn't steering: the body levels out.");
        }

        [UnityTest]
        public IEnumerator Underwater_AtThePoolFloor_PitchesDownOnlyAsFarAsItHasRoom()
        {
            yield return DivedInThePool();
            yield return rig.RunUntil(() => rig.Grounded, step => CharacterTestRig.Crouch(), timeoutSeconds: 10f);
            Assume.That(rig.Grounded, Is.True, "Test setup: the character should have sunk to the pool's floor.");
            LookDown(45f);
            float lowest = 0f;
            bool alwaysFits = true;
            yield return rig.Run(1f, _ => CharacterTestRig.Move(Vector3.forward), _ =>
            {
                lowest = Mathf.Min(lowest, rig.Movement.BodyPitch);
                alwaysFits &= rig.BodyFits;
            });
            Assert.That(lowest, Is.GreaterThan(-5f), "Lying on the floor, the body shouldn't pitch its front down into it.");
            Assert.That(rig.Grounded, Is.True, "Nor be lifted off the floor it's steered into.");
            Assert.That(alwaysFits, Is.True, "Pitching shouldn't take the body into the floor.");
            Assert.That(rig.Position.z, Is.GreaterThan(0.5f * SwimSpeed), "It should still swim along the floor.");

            LookDown(-30f);
            // (Pitching about its feet, its tail dips as its nose rises: once it's risen clear of the floor.)
            yield return rig.Run(1f, _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(rig.Movement.BodyPitch, Is.EqualTo(30f).Within(0.5f), "Steered up off the floor, it has room to pitch up.");
        }

        /// <summary>
        /// Swimming underwater into a ladder up the pool's wall takes hold of it and climbs it, also from along the pool's
        /// floor. Lying down underwater, the body reaches far ahead of its feet, so it takes hold further from the ladder
        /// than it climbs; it once let go as climbing stood it up, the ladder then out of its reach, before it closed in.
        /// </summary>
        [UnityTest]
        public IEnumerator Underwater_SwimmingIntoALadder_TakesHoldOfIt_AndClimbsIt([Values(false, true)] bool alongTheFloor)
        {
            yield return DivedInThePool();
            if (alongTheFloor) { yield return rig.RunUntil(() => rig.Grounded, step => CharacterTestRig.Crouch(), timeoutSeconds: 10f); }
            else { yield return rig.Run(0.8f, step => CharacterTestRig.Crouch()); }
            float face = rig.Position.z + rig.FrontExtent + 0.6f;
            Ladder ladder = rig.Ladder(new Vector3(rig.Position.x, -PoolDepth, face + 0.03f), PoolDepth + 3f);
            rig.Box("Pool Wall", new Vector3(0f, -PoolDepth / 2f, face + 0.03f + ladder.Depth + 0.5f), new Vector3(PoolSize, PoolDepth + 4f, 1f));
            Physics.SyncTransforms();
            bool tookHold = false, letGo = false;
            float start = rig.Position.y;
            yield return rig.Run(4f, _ => CharacterTestRig.Move(Vector3.forward), _ =>
            {
                bool climbing = rig.InState(CharacterState.WallClimbing);
                // Back to swimming (climbing over the top of the wall, out of the water, ends the climb too).
                letGo |= tookHold && rig.Swimming;
                tookHold |= climbing;
            });
            Assert.That(tookHold, Is.True, "Swimming into the ladder underwater, the character should take hold of it.");
            Assert.That(letGo, Is.False, "It shouldn't let go of it, back into the water.");
            Assert.That(rig.Position.y - start, Is.GreaterThan(2f), "It should climb up it.");
        }

        /// <summary>
        /// Swimming underwater up a beach out of the water surfaces once the feet are shallower than the swimming depth,
        /// finds its feet in the shallows and walks out. Lying on the slope, the body rode up it on its front, its feet off
        /// it, and swam on underwater, floating, until its feet were out of the water.
        /// </summary>
        [UnityTest]
        public IEnumerator Underwater_SwimmingUpABeach_SurfacesInTheShallows_AndWalksOut([Values(-20f, 0f, 20f)] float lookDown)
        {
            // As the Level Playground's shallow end: water 1.6 m deep over a shelf (deeper than the swimming depth), and a
            // 1:4 beach up out of it ahead, to the ground beyond.
            const float surface = -0.4f, shelf = -2f, rampStart = 6f, run = 4f * -shelf;
            rig.Floor(shelf);
            water = rig.Water(new Vector3(0f, surface, 0f), new Vector3(60f, surface - shelf + 1f, 60f));
            Quaternion tilt = Quaternion.Euler(-Mathf.Atan2(-shelf, run) * Mathf.Rad2Deg, 0f, 0f);
            rig.Box("Beach", new Vector3(0f, shelf / 2f, rampStart + run / 2f) - tilt * Vector3.up * 0.5f,
                new Vector3(20f, 1f, Mathf.Sqrt(shelf * shelf + run * run)), tilt);
            rig.Box("Ground", new Vector3(0f, -0.5f, rampStart + run + 10f), new Vector3(20f, 1f, 20f));
            yield return rig.SpawnSettled(new Vector3(0f, surface, 0f), settleSeconds: 0f);
            float depth = rig.SwimDepth;
            Assume.That(depth, Is.LessThan(surface - shelf), "Test setup: the water should be deep enough to swim in.");
            yield return rig.RunUntil(() => rig.Swimming && Mathf.Abs(rig.Velocity.y) < 0.05f, _ => CharacterTestRig.Idle(), timeoutSeconds: 5f);
            yield return rig.Run(1, _ => CharacterTestRig.Crouch());
            yield return rig.Run(0.4f, _ => CharacterTestRig.Idle());
            Assume.That(Diving, Is.True, "Test setup: the character should be underwater.");
            LookDown(lookDown);
            float shallowestDiving = float.PositiveInfinity, wadedOutAt = float.NaN;
            yield return rig.RunUntil(() => !rig.Swimming && rig.Grounded, _ => CharacterTestRig.Move(Vector3.forward), _ =>
            {
                if (Diving) { shallowestDiving = Mathf.Min(shallowestDiving, FeetUnder); }
            }, timeoutSeconds: 10f);
            wadedOutAt = FeetUnder;
            Assert.That(shallowestDiving, Is.GreaterThan(depth - 0.1f), "Underwater, it should surface once its feet are shallower than its swimming depth.");
            Assert.That(rig.Swimming, Is.False, "In the shallows, it should find its feet.");
            Assert.That(wadedOutAt, Is.GreaterThan(0.25f * depth), "It should find its feet while they're still well under the water, not float out of it.");
        }

        [UnityTest]
        public IEnumerator HoldingCrouchOrJump_SinksOrRises_WhicheverWayTheCameraLooks()
        {
            yield return DivedInThePool();
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            LookDown(-30f); // Looking up: Crouch still sinks.
            yield return rig.Run(1.5f, step => CharacterTestRig.Crouch(Vector3.forward));
            Assert.That(rig.Velocity.y, Is.EqualTo(-VerticalSpeed).Within(0.05f), "Holding Crouch underwater should sink, even looking up.");
            Assert.That(rig.Velocity.z, Is.GreaterThan(0.5f * SwimSpeed), "The character should still swim forward meanwhile.");

            LookDown(30f); // Looking down: Jump still rises.
            yield return rig.Run(1f, step => CharacterTestRig.Jump(Vector3.forward));
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
            Assert.That(rig.CurrentHeight, Is.EqualTo(rig.StandingHeight).Within(0.01f), "At the surface, the body should stand again.");
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
            yield return rig.RunUntil(() => !Diving, step => CharacterTestRig.Jump(), timeoutSeconds: 5f);
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
            yield return rig.Run(1f, step => step == 0 ? CharacterTestRig.Crouch() : CharacterTestRig.Idle(), _ => dived |= Diving);
            Assert.That(dived, Is.False);
        }
    }
}
