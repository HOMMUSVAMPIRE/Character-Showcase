using System.Collections;
using Character;
using Level;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of swimming: falling into deep water, the character plunges and floats back up to swim with its feet the
    /// swim depth below the surface; it swims the way the input points at the swim speed (the sprint speed with Sprint
    /// held), and slows to a stop without input; Jump jumps it out, its feet rising the swim jump height above the
    /// surface, onto an edge a little above the water; it climbs a ladder out of the water; swimming into something low
    /// sticking out of the water it vaults over or onto it (a pool's side, a divider), and climbs out onto a taller side
    /// within reach of its hands (not one out of reach); walking down into deep water, or plunging to the bottom, it floats
    /// up to swim; swimming into shallows it finds its feet and walks out; and it doesn't swim with Swimming turned off, or
    /// a swim depth of 0.
    /// The test camera looks down +Z. Expected values are read from the movement mode's settings.
    /// </summary>
    [Category(TestArea.Traversal)]
    public class SwimTests : CharacterRigFixture
    {
        // The pool: its surface at 0, its floor this far down, wide enough to swim a long way in.
        private const float PoolDepth = 5f, PoolSize = 60f;

        private Water water;

        private float SwimSpeed => rig.MovementSetting<float>("swimSpeed") * rig.Movement.SpeedScale;
        private float SprintSwimSpeed => rig.MovementSetting<float>("swimSprintSpeed") * rig.Movement.SpeedScale;
        private float JumpHeight => rig.MovementSetting<float>("swimJumpHeight") * rig.Movement.HeightScale;
        /// <summary>How far (m) below the surface the character's feet are.</summary>
        private float FeetUnder => water.SurfaceHeight - rig.Position.y;

        /// <summary>A deep pool (its floor, and water over it), and the character spawned on it, in mid-air above the water.</summary>
        private IEnumerator SetUpPool(float dropFrom = 1f)
        {
            rig.Floor(-PoolDepth);
            water = rig.Water(Vector3.zero, new Vector3(PoolSize, PoolDepth, PoolSize));
            yield return rig.SpawnSettled(Vector3.up * dropFrom, settleSeconds: 0f);
            rig.ClearEvents();
        }

        /// <summary>The pool, the character dropped in and left to settle at its swimming depth.</summary>
        private IEnumerator SwimmingInThePool()
        {
            yield return SetUpPool();
            yield return rig.RunUntil(() => rig.Swimming, _ => CharacterTestRig.Idle(), timeoutSeconds: 3f);
            Assume.That(rig.Swimming, Is.True, "Test setup: the character should be swimming.");
            yield return rig.RunUntil(() => Mathf.Abs(FeetUnder - rig.SwimDepth) < 0.01f && Mathf.Abs(rig.Velocity.y) < 0.05f,
                _ => CharacterTestRig.Idle(), timeoutSeconds: 4f);
        }

        [UnityTest]
        public IEnumerator FallingIntoDeepWater_PlungesAndFloatsBackUp_ToSwimAtTheSwimDepth()
        {
            yield return SetUpPool(dropFrom: 6f);
            float deepest = 0f;
            yield return rig.RunUntil(() => rig.Swimming && rig.Velocity.y > 0f, _ => CharacterTestRig.Idle(),
                _ => deepest = Mathf.Max(deepest, FeetUnder), timeoutSeconds: 4f);
            Assert.That(rig.Swimming, Is.True, "Falling into deep water, the character should swim.");
            Assert.That(deepest, Is.GreaterThan(rig.SwimDepth + 0.2f), "Falling in from high, the character should plunge below its swimming depth.");
            Assert.That(deepest, Is.LessThan(PoolDepth - 0.1f), "The water should slow the plunge before the pool's floor.");

            float highest = float.PositiveInfinity;
            yield return rig.Run(3f, _ => CharacterTestRig.Idle(), _ => highest = Mathf.Min(highest, FeetUnder));
            Assert.That(rig.Swimming, Is.True);
            Assert.That(FeetUnder, Is.EqualTo(rig.SwimDepth).Within(0.02f), "The character should float with its feet the swim depth below the surface.");
            Assert.That(highest, Is.GreaterThan(rig.SwimDepth - 0.05f), "Floating back up, the character shouldn't overshoot its swimming depth.");
            Assert.That(rig.EventsRaised, Does.Contain(MovementEvents.SwimStarted));
            Assert.That(rig.EventsRaised, Has.No.Member(MovementEvents.Landed), "Falling into deep water isn't landing.");
        }

        [UnityTest]
        public IEnumerator Swimming_GoesTheWayTheInputPoints_AtTheSwimSpeed_FasterSprinting()
        {
            yield return SwimmingInThePool();
            yield return rig.Run(1.5f, _ => CharacterTestRig.Move(Vector3.forward));
            float swimming = 0f;
            yield return rig.MeanSpeed(0.5f, _ => CharacterTestRig.Move(Vector3.forward), speed => swimming = speed);
            Assert.That(swimming, Is.EqualTo(SwimSpeed).Within(0.05f), "The character should swim at the swim speed.");
            Assert.That(rig.Velocity.normalized.z, Is.GreaterThan(0.99f), "The character should swim the way the input points.");
            Assert.That(Vector3.Angle(rig.Body.rotation * Vector3.forward, Vector3.forward), Is.LessThan(2f), "The character should face the way it swims.");
            Assert.That(FeetUnder, Is.EqualTo(rig.SwimDepth).Within(0.02f), "Swimming along, the character should stay at its swimming depth.");

            yield return rig.Run(1.5f, _ => CharacterTestRig.Sprint(Vector3.forward));
            float sprinting = 0f;
            yield return rig.MeanSpeed(0.5f, _ => CharacterTestRig.Sprint(Vector3.forward), speed => sprinting = speed);
            Assert.That(sprinting, Is.EqualTo(SprintSwimSpeed).Within(0.05f), "With Sprint held, the character should swim at the swim sprint speed.");

            yield return rig.Run(2f, _ => CharacterTestRig.Idle());
            Assert.That(rig.HorizontalSpeed, Is.LessThan(0.01f), "Without input, the character should slow to a stop.");
            Assert.That(rig.Swimming, Is.True);
        }

        [UnityTest]
        public IEnumerator Jumping_JumpsOutOfTheWater_ItsFeetRisingTheSwimJumpHeightAboveTheSurface()
        {
            yield return SwimmingInThePool();
            rig.ClearEvents();
            float highest = float.NegativeInfinity;
            yield return rig.Run(1, _ => CharacterTestRig.Jump(0));
            Assert.That(rig.Swimming, Is.False, "Jump should jump out of the water.");
            Assert.That(rig.Movement.jump.remaining, Is.EqualTo(rig.MaxJumps - 1), "Jumping out should use one jump, leaving the air jumps.");
            yield return rig.RunUntil(() => rig.Velocity.y <= 0f, _ => CharacterTestRig.Idle(), _ => highest = Mathf.Max(highest, rig.Position.y), timeoutSeconds: 3f);
            Assert.That(highest - water.SurfaceHeight, Is.EqualTo(JumpHeight).Within(0.05f), "The feet should rise the swim jump height above the surface.");
            Assert.That(rig.EventsRaised, Does.Contain(MovementEvents.Jumped));
            Assert.That(rig.EventsRaised, Does.Contain(MovementEvents.SwimEnded));

            // And back into the water.
            yield return rig.RunUntil(() => rig.Swimming, _ => CharacterTestRig.Idle(), timeoutSeconds: 3f);
            Assert.That(rig.Swimming, Is.True, "Falling back in, the character should swim again.");
        }

        [UnityTest]
        public IEnumerator JumpingOutAtAnEdge_ClimbsOutOntoIt()
        {
            yield return SwimmingInThePool();
            // An edge a little above the water ahead, as a pool's side is.
            float edge = JumpHeight * 0.5f;
            rig.Box("Pool Side", new Vector3(0f, (edge - PoolDepth) / 2f, 15f), new Vector3(20f, edge + PoolDepth, 20f));
            Physics.SyncTransforms();
            yield return rig.RunUntil(() => rig.Position.z > 3f, _ => CharacterTestRig.Move(Vector3.forward), timeoutSeconds: 4f);
            yield return rig.Run(1, _ => CharacterTestRig.Jump(0, Vector3.forward));
            yield return rig.RunUntil(() => rig.Grounded && rig.Position.y > edge - 0.05f, _ => CharacterTestRig.Move(Vector3.forward), timeoutSeconds: 3f);
            Assert.That(rig.Swimming, Is.False);
            Assert.That(rig.Grounded, Is.True, "The character should be standing on the edge.");
            Assert.That(rig.Position.y, Is.EqualTo(edge).Within(0.05f), "The character should have climbed out onto the edge.");
        }

        // The pool's side, ahead of where the character swims: its face, and how far its top is above the water.
        private const float SideFace = 3f, LadderDepth = 0.06f;

        /// <summary>The pool's side ahead, its top edge above the water, with a ladder up its face from the pool's floor if asked for.</summary>
        private void PoolSide(float edge, bool ladder)
        {
            if (ladder) { rig.Ladder(new Vector3(0f, -PoolDepth, SideFace + LadderDepth / 2f), PoolDepth + edge); }
            float face = SideFace + (ladder ? LadderDepth : 0f);
            rig.Box("Pool Side", new Vector3(0f, (edge - PoolDepth) / 2f, face + 15f), new Vector3(20f, edge + PoolDepth, 30f));
            Physics.SyncTransforms();
        }

        // A side a little above the water (as the Level Playground's pool's is), and one well above it.
        [UnityTest]
        public IEnumerator SwimmingIntoALadder_ClimbsItOutOfTheWater([Values(0.4f, 2f)] float edge)
        {
            yield return SwimmingInThePool();
            PoolSide(edge, ladder: true);
            bool climbed = false;
            // Taking hold of the ladder ends the swim; with the top within reach, the character climbs straight over it
            // onto the side (see WallClimbing), the same step.
            yield return rig.RunUntil(() => rig.Grounded && rig.Position.y > edge - 0.05f, _ => CharacterTestRig.Move(Vector3.forward),
                _ => climbed |= rig.InState(CharacterState.WallClimbing) || rig.InState(CharacterState.LedgeClimbing), timeoutSeconds: 8f);
            Assert.That(climbed, Is.True, "Swimming into the ladder, the character should climb it.");
            Assert.That(rig.Swimming, Is.False);
            Assert.That(rig.Grounded, Is.True);
            Assert.That(rig.Position.y, Is.EqualTo(edge).Within(0.05f), "The character should have climbed out onto the pool's side.");
        }

        private float SwimVaultHeight => rig.MovementSetting<float>("swimVaultHeight") * rig.StandingHeight;

        /// <summary>Swims into the pool's side until standing on it (or the timeout), noting whether it vaulted and whether it climbed.</summary>
        private IEnumerator SwimOutOver(float edge, System.Action<bool, bool> result)
        {
            bool vaulted = false, climbed = false;
            yield return rig.RunUntil(() => rig.Grounded && rig.Position.y > edge - 0.05f, _ => CharacterTestRig.Move(Vector3.forward), _ =>
            {
                vaulted |= rig.InState(CharacterState.Vaulting) || rig.InState(CharacterState.SteppingUp);
                climbed |= rig.InState(CharacterState.LedgeClimbing);
            }, timeoutSeconds: 6f);
            result(vaulted, climbed);
        }

        [UnityTest]
        public IEnumerator SwimmingIntoALowSide_VaultsOntoIt()
        {
            yield return SwimmingInThePool();
            const float edge = 0.4f; // As the Level Playground's pool's sides are.
            Assume.That(edge, Is.LessThan(SwimVaultHeight), "Test setup: the side should be low enough to vault from the water.");
            PoolSide(edge, ladder: false);
            bool vaulted = false, climbed = false;
            yield return SwimOutOver(edge, (v, c) => { vaulted = v; climbed = c; });
            Assert.That(vaulted, Is.True, "Swimming into a side low enough, the character should vault onto it.");
            Assert.That(climbed, Is.False, "A side low enough to vault isn't climbed.");
            Assert.That(rig.Swimming, Is.False);
            Assert.That(rig.Grounded, Is.True);
            Assert.That(rig.Position.y, Is.EqualTo(edge).Within(0.05f), "The character should be standing on the side.");
        }

        [UnityTest]
        public IEnumerator SwimmingIntoATallerSideWithinReach_ClimbsOutOntoIt()
        {
            yield return SwimmingInThePool();
            float edge = SwimVaultHeight + 0.3f;
            PoolSide(edge, ladder: false);
            bool vaulted = false, climbed = false;
            yield return SwimOutOver(edge, (v, c) => { vaulted = v; climbed = c; });
            Assert.That(climbed, Is.True, "Swimming into a side too tall to vault but within reach, the character should climb out onto it.");
            Assert.That(vaulted, Is.False);
            Assert.That(rig.Position.y, Is.EqualTo(edge).Within(0.05f), "The character should be standing on the side.");
        }

        [UnityTest]
        public IEnumerator ASwimVaultHeightOfZero_ClimbsOutOntoALowSideInstead()
        {
            yield return SwimmingInThePool();
            rig.SetMovementSetting("swimVaultHeight", 0f);
            const float edge = 0.4f;
            PoolSide(edge, ladder: false);
            bool vaulted = false, climbed = false;
            yield return SwimOutOver(edge, (v, c) => { vaulted = v; climbed = c; });
            Assert.That(vaulted, Is.False, "With a swim vault height of 0, the character shouldn't vault from the water.");
            Assert.That(climbed, Is.True);
        }

        [UnityTest]
        public IEnumerator SwimmingIntoALowDivider_VaultsOverIt_BackIntoTheWater()
        {
            yield return SwimmingInThePool();
            // A thin wall across the pool, its top a little out of the water: deep water on both sides.
            const float top = 0.3f, at = 4f;
            rig.Box("Divider", new Vector3(0f, (top - PoolDepth) / 2f, at), new Vector3(20f, top + PoolDepth, 0.3f));
            Physics.SyncTransforms();
            bool vaulted = false;
            yield return rig.RunUntil(() => vaulted && rig.Swimming, _ => CharacterTestRig.Move(Vector3.forward),
                _ => vaulted |= rig.InState(CharacterState.Vaulting), timeoutSeconds: 6f);
            Assert.That(vaulted, Is.True, "Swimming into a low divider, the character should vault over it.");
            Assert.That(rig.Swimming, Is.True, "Over the divider, the character should be swimming again.");
            Assert.That(rig.Position.z, Is.GreaterThan(at), "The character should be past the divider.");
        }

        [UnityTest]
        public IEnumerator SwimmingIntoASideOutOfReach_KeepsSwimming()
        {
            yield return SwimmingInThePool();
            // Its top higher above the swimmer's feet than its hands reach for a ledge.
            float edge = rig.MovementSetting<float>("ledgeGrabMaxHeight") * rig.StandingHeight - rig.SwimDepth + 0.5f;
            PoolSide(edge, ladder: false);
            yield return rig.Run(2.5f, _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(rig.Swimming, Is.True, "A side out of reach isn't climbed out onto.");
            Assert.That(rig.Position.z, Is.LessThan(SideFace), "The side should have stopped the swimmer.");
        }

        [UnityTest]
        public IEnumerator WalkingDownASlopeIntoDeepWater_FloatsUpAndSwimsAtTheSwimDepth()
        {
            rig.Floor(-PoolDepth);
            water = rig.Water(Vector3.zero, new Vector3(PoolSize, PoolDepth, PoolSize));
            // A beach rising out of the water toward -Z, a 1:4 slope, from the pool's floor to above the surface.
            Vector3 foot = new Vector3(0f, -PoolDepth, -4f), top = new Vector3(0f, 1f, -4f - 4f * (PoolDepth + 1f));
            Vector3 along = top - foot;
            Quaternion tilt = Quaternion.LookRotation(along.normalized, Vector3.up);
            rig.Box("Beach", (foot + top) / 2f - tilt * Vector3.up * 0.5f, new Vector3(20f, 1f, along.magnitude), tilt);
            Physics.SyncTransforms();
            yield return rig.SpawnSettled(top + new Vector3(0f, 0.5f, 2f));
            Assume.That(rig.Grounded && rig.Position.y > 0f, Is.True, "Test setup: the character should be standing on the beach above the water.");

            // Down the beach and on past its foot, over deep water.
            yield return rig.RunUntil(() => rig.Position.z > foot.z + 3f, _ => CharacterTestRig.Move(Vector3.forward), timeoutSeconds: 15f);
            Assert.That(rig.Swimming, Is.True, "Walking down into deep water, the character should swim.");
            yield return rig.Run(1.5f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Swimming, Is.True);
            Assert.That(FeetUnder, Is.EqualTo(rig.SwimDepth).Within(0.02f), "The character should float at its swimming depth, not stay down on the beach.");
        }

        [UnityTest]
        public IEnumerator PlungingToTheBottom_FloatsBackUpToTheSwimDepth()
        {
            // Water not much deeper than the swim depth, fallen into from high: the plunge reaches the floor.
            // Spawned in mid-air (it stays there until run), so the pool can be made for its size.
            yield return rig.SpawnSettled(Vector3.up * 8f, settleSeconds: 0f);
            float depth = rig.SwimDepth + 0.6f;
            rig.Floor(-depth);
            water = rig.Water(Vector3.zero, new Vector3(PoolSize, depth, PoolSize));
            Physics.SyncTransforms();
            bool touchedBottom = false;
            yield return rig.RunUntil(() => touchedBottom && rig.Velocity.y > 0f, _ => CharacterTestRig.Idle(),
                _ => touchedBottom |= rig.Swimming && rig.Grounded, timeoutSeconds: 4f);
            Assume.That(touchedBottom, Is.True, "Test setup: the plunge should reach the floor.");
            yield return rig.Run(2f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Swimming, Is.True, "Resting on the pool's floor (where its water ends), the character is still in the water.");
            Assert.That(FeetUnder, Is.EqualTo(rig.SwimDepth).Within(0.02f), "Off the bottom, the character should float back up to its swimming depth.");
        }

        [UnityTest]
        public IEnumerator SwimmingIntoShallows_FindsItsFeet_AndWalksOut()
        {
            yield return SwimmingInThePool();
            // A slope rising ahead from the pool's floor to the surface and on above it, a 1:4 slope (as a beach is).
            Vector3 foot = new Vector3(0f, -PoolDepth, 4f), top = new Vector3(0f, 1f, 4f + 4f * (PoolDepth + 1f));
            Vector3 along = top - foot;
            Quaternion tilt = Quaternion.LookRotation(along.normalized, Vector3.up);
            rig.Box("Beach", (foot + top) / 2f - tilt * Vector3.up * 0.5f, new Vector3(20f, 1f, along.magnitude), tilt);
            Physics.SyncTransforms();
            rig.ClearEvents();
            yield return rig.RunUntil(() => !rig.Swimming && rig.Grounded && rig.Position.y > 0.5f, _ => CharacterTestRig.Move(Vector3.forward), timeoutSeconds: 15f);
            Assert.That(rig.Swimming, Is.False, "In the shallows, the character should stop swimming.");
            Assert.That(rig.Position.y, Is.GreaterThan(0.5f), "The character should have walked up out of the water.");
            Assert.That(rig.EventsRaised, Does.Contain(MovementEvents.SwimEnded));
        }

        [UnityTest]
        public IEnumerator SwimmingTurnedOff_FallsToTheBottom()
        {
            yield return SetUpPool();
            rig.Component<Swimming>().Enabled = false;
            bool swam = false;
            yield return rig.RunUntilLanded(_ => swam |= rig.Swimming);
            Assert.That(swam, Is.False, "With Swimming turned off, the character shouldn't swim.");
            Assert.That(rig.Position.y, Is.EqualTo(-PoolDepth).Within(0.05f), "It should have fallen to the pool's floor.");
        }

        [UnityTest]
        public IEnumerator TurningSwimmingOffWhileSwimming_LetsTheCharacterSink()
        {
            yield return SwimmingInThePool();
            rig.Component<Swimming>().Enabled = false;
            yield return rig.Run(1, _ => CharacterTestRig.Idle());
            Assert.That(rig.Movement.swim.IsSwimming, Is.False, "Turned off, Swimming should let go of the character.");
            Assert.That(rig.EventsRaised, Does.Contain(MovementEvents.SwimEnded));
        }

        [UnityTest]
        public IEnumerator ASwimDepthOfZero_TurnsSwimmingOff()
        {
            yield return SetUpPool();
            rig.SetMovementSetting("swimDepth", 0f);
            bool swam = false;
            yield return rig.RunUntilLanded(_ => swam |= rig.Swimming);
            Assert.That(swam, Is.False);
        }
    }
}
