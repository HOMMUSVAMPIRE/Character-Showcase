using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Character;
using Level;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of climbing ladders, which are climbed as climbable surfaces are, but only straight up and down, on the
    /// ladder's middle, from its front: walking into one steps onto it and climbs up its middle facing it, sideways input
    /// doing nothing; one isn't taken hold of from behind; a ladder standing on its own is climbed to its top and no
    /// further, while one against a platform is climbed off onto it; and Jump kicks off it. The test camera looks down +Z,
    /// so up on the stick is +Z input. Expected values are read from the movement mode's settings.
    /// </summary>
    public class LadderTests
    {
        private CharacterTestRig rig;
        private Ladder ladder;
        // The ladder's front face (z), toward the character.
        private float face;

        [SetUp]
        public void SetUp() => rig = new CharacterTestRig();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            rig.Dispose();
            yield return null;
        }

        private float ClimbSpeed => rig.Mode<WallClimbMode>().wallClimbSpeed * rig.Movement.SpeedScale;
        private bool Climbing => rig.InState(CharacterState.WallClimbing);
        private bool OnLadder => Climbing && rig.Movement.wallClimb.ladder != null;
        private float GripHeight => rig.MovementSetting<float>("wallClimbGripHeight") * rig.StandingHeight;
        private Vector3 Forward => rig.Body.rotation * Vector3.forward;
        private static Inputs Up => CharacterTestRig.Move(Vector3.forward);

        /// <summary>A floor, the character standing on it facing +Z, and a ladder of the given height a little in front of it, offset to its side.</summary>
        private IEnumerator SetUpLadder(float height = 6f, float sideways = 0.15f)
        {
            rig.Floor();
            yield return rig.SpawnSettled(Vector3.zero);
            face = rig.Radius + 0.3f;
            ladder = rig.Ladder(new Vector3(sideways, 0f, face + 0.03f), height);
        }

        /// <summary>Walks into the ladder until on it, and waits out stepping onto it.</summary>
        private IEnumerator TakeHold()
        {
            for (int step = 0; step < CharacterTestRig.StepsFor(0.5f) && !Climbing; step++) { yield return rig.Run(1, _ => Up); }
            Assert.That(OnLadder, Is.True, $"Test setup: the character should be on the ladder (it's at {rig.Position}).");
            for (int step = 0; step < CharacterTestRig.StepsFor(1f) && rig.Movement.wallClimb.IsEntering; step++) { yield return rig.Run(1, _ => Up); }
        }

        [UnityTest]
        public IEnumerator WalkingIntoALadder_ClimbsStraightUpItsMiddle_FacingIt()
        {
            yield return SetUpLadder();
            yield return TakeHold();
            float start = rig.Position.y;
            const float seconds = 1f;
            yield return rig.Run(seconds, _ => Up);
            Assert.That(OnLadder, Is.True);
            Assert.That(rig.Position.y - start, Is.EqualTo(ClimbSpeed * seconds).Within(0.1f * ClimbSpeed * seconds), "Up should climb the ladder at the climbing speed.");
            Assert.That(rig.Position.x, Is.EqualTo(ladder.transform.position.x).Within(0.02f), "The character should climb on the ladder's middle.");
            Assert.That(face - (rig.Position.z + rig.Radius), Is.InRange(-0.01f, 0.08f), "The character should be against the ladder.");
            Assert.That(Vector3.Angle(Forward, Vector3.forward), Is.LessThan(1f), "The character should face the ladder.");
        }

        [UnityTest]
        public IEnumerator SidewaysInputOnALadder_DoesNothing()
        {
            yield return SetUpLadder(sideways: 0f);
            yield return TakeHold();
            yield return rig.Run(0.5f, _ => Up);
            Vector3 start = rig.Position;
            yield return rig.Run(1f, _ => CharacterTestRig.Move(Vector3.right));
            Assert.That(OnLadder, Is.True);
            Assert.That(Vector3.Distance(rig.Position, start), Is.LessThan(0.02f), "A ladder should only be climbed up and down.");
        }

        [UnityTest]
        public IEnumerator ALadder_IsNotTakenHoldOfFromBehind()
        {
            rig.Floor();
            yield return rig.SpawnSettled(Vector3.zero);
            face = rig.Radius + 0.3f;
            // Facing +Z: the character comes at its back.
            rig.Ladder(new Vector3(0f, 0f, face + 0.03f), 6f, Quaternion.identity);
            bool climbed = false;
            yield return rig.Run(1f, _ => Up, _ => climbed |= Climbing);
            Assert.That(climbed, Is.False, "A ladder should only be climbed from its front.");
        }

        [UnityTest]
        public IEnumerator ALadderStandingOnItsOwn_IsClimbedToItsTop_AndNoFurther()
        {
            const float height = 4f;
            yield return SetUpLadder(height);
            yield return TakeHold();
            yield return rig.Run(height / ClimbSpeed + 1.5f, _ => Up);
            Assert.That(OnLadder, Is.True, "The character should still be on the ladder at its top, with nothing to climb off onto.");
            Assert.That(rig.Position.y + GripHeight, Is.LessThanOrEqualTo(height + 0.05f), "The character shouldn't climb past the ladder's top.");
            Assert.That(rig.Position.y + GripHeight, Is.GreaterThan(height - 0.3f), "The character should have climbed to the ladder's top.");
        }

        // Of all heights: the climb off the top once missed the floor there by float rounding at some heights (3, 8, 10
        // and 12 m among them; see TryDropOntoLedge), leaving the character stuck at the top.
        [UnityTest]
        public IEnumerator ALadderUpToAPlatform_IsClimbedOffOntoIt([Values(2f, 2.7f, 3f, 4f, 5f, 6f, 7.3f, 8f, 10f, 12f, 16f)] float height)
        {
            yield return SetUpLadder(height, sideways: 0f);
            // A platform behind the ladder, its top level with the ladder's (deep, so walking on doesn't walk off it).
            rig.Box("Platform", new Vector3(0f, height / 2f, face + 0.06f + 15f), new Vector3(6f, height, 30f));
            yield return TakeHold();
            yield return rig.Run(height / ClimbSpeed + rig.Mode<LedgeMode>().ledgeClimbDuration * rig.Movement.DurationScale + 1f, _ => Up);
            Assert.That(Climbing, Is.False, "The character should have climbed off the ladder.");
            Assert.That(rig.Grounded, Is.True, "The character should be standing on the platform.");
            Assert.That(rig.Position.y, Is.EqualTo(height).Within(0.05f), "The character should be on top of the platform.");
            Assert.That(rig.Position.z, Is.GreaterThan(face + 0.06f), "The character should be on the platform, past the ladder.");
        }

        [UnityTest]
        public IEnumerator JumpingOffALadder_KicksAwayFromIt()
        {
            yield return SetUpLadder();
            yield return TakeHold();
            yield return rig.Run(0.6f, _ => Up);
            var events = new List<MovementEvents>();
            rig.Movement.MovementEventRaised += (e, _) => events.Add(e);
            yield return rig.Run(0.3f, step => step == 0 ? CharacterTestRig.Jump(0) : CharacterTestRig.Idle());
            Assert.That(Climbing, Is.False);
            Assert.That(rig.Position.z + rig.Radius, Is.LessThan(face - 0.5f), "Jumping off should push the character away from the ladder.");
            Assert.That(events.Contains(MovementEvents.WallJumped), Is.True);
        }
    }
}
