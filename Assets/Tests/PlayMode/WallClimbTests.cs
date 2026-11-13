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
    /// Tests of climbing climbable surfaces: walking, glancing or falling into one takes hold of it (but not an
    /// unclimbable one, nor with climbing turned off); input climbs up and sideways at the climbing speed, facing the
    /// surface and without gravity, holding still without input; the climbable area holds the character in (a climbable
    /// panel on a plain wall, sideways and up); it goes round an outside corner and into an inside one onto the next
    /// climbable face; it climbs over a walkable top onto it, and moves with a surface that moves; Jump kicks off, up and
    /// away; Crouch lets go, taking hold again only after the regrab delay; and pressing down at the bottom steps off. The
    /// test camera looks down +Z, so up on the stick is +Z input. Painted climbable surfaces: only the painted part of a
    /// wall is taken hold of and climbed, the character staying within the paint; a box painted on one face isn't
    /// climbed round its corner, but is once the face round it is painted too; and paint is for the face painted, erased
    /// as painted. Expected values are read from the movement mode's settings.
    /// </summary>
    public class WallClimbTests
    {
        private CharacterTestRig rig;
        // The face (z) of the surface in front of the character, which faces -Z toward it.
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
        private Vector3 Velocity => rig.Movement.lastFrameMovementData.finalVelocity;
        private Vector3 Forward => rig.Body.rotation * Vector3.forward;
        // How high the character's grip is above its feet: the point that has to stay on the climbable area.
        private float GripHeight => rig.MovementSetting<float>("wallClimbGripHeight") * rig.StandingHeight;

        private static Inputs Up => CharacterTestRig.Move(Vector3.forward);
        private static Inputs Down => CharacterTestRig.Move(Vector3.back);
        private static Inputs Right => CharacterTestRig.Move(Vector3.right);

        /// <summary>A floor, the character standing on it facing +Z, and the face of something a little in front of it.</summary>
        private IEnumerator SetUpStanding()
        {
            rig.Floor();
            yield return rig.SpawnSettled(Vector3.zero);
            face = rig.Radius + 0.3f;
        }

        /// <summary>The character standing in front of a tall wall (climbable, unless asked otherwise), 10 m wide.</summary>
        private IEnumerator SetUpWall(bool climbable = true, float height = 10f)
        {
            yield return SetUpStanding();
            var center = new Vector3(0f, height / 2f, face + 0.5f);
            var size = new Vector3(10f, height, 1f);
            if (climbable) { rig.ClimbableBox("Wall", center, size); }
            else { rig.Box("Wall", center, size); }
        }

        private float EnterDuration => rig.MovementSetting<float>("wallClimbEnterDuration") * rig.Movement.DurationScale;
        private float ExitDuration => rig.MovementSetting<float>("wallClimbExitDuration") * rig.Movement.DurationScale;

        /// <summary>
        /// Walks into the surface in front, until it takes hold of it (the step it does), and then, from the ground, waits
        /// for the character to step up onto it.
        /// </summary>
        private IEnumerator TakeHold()
        {
            for (int step = 0; step < CharacterTestRig.StepsFor(0.5f) && !Climbing; step++)
            {
                yield return rig.Run(1, _ => Up);
            }
            Assert.That(Climbing, Is.True, $"Test setup: the character should be climbing (it's at {rig.Position}, " +
                                           $"{rig.Movement.lastFrameMovementData.state}).");
            for (int step = 0; step < CharacterTestRig.StepsFor(EnterDuration + 0.1f) && rig.Movement.wallClimb.IsEntering; step++)
            {
                yield return rig.Run(1, _ => Up);
            }
        }

        [UnityTest]
        public IEnumerator WalkingIntoAClimbableWall_TakesHoldOfIt_AndClimbsUpIt_AgainstItAndFacingIt()
        {
            yield return SetUpWall();
            var events = new List<MovementEvents>();
            rig.Movement.MovementEventRaised += (movementEvent, _) => events.Add(movementEvent);
            yield return TakeHold();
            Assert.That(events.Count(e => e == MovementEvents.WallClimbStarted), Is.EqualTo(1));

            float start = rig.Position.y;
            const float seconds = 1f;
            int steps = 0, climbingSteps = 0;
            yield return rig.Run(seconds, _ => Up, _ => { steps++; if (Climbing) { climbingSteps++; } });

            Assert.That(climbingSteps, Is.EqualTo(steps), "The character should keep climbing.");
            Assert.That(rig.Position.y - start, Is.EqualTo(ClimbSpeed * seconds).Within(0.1f * ClimbSpeed * seconds),
                "Up on the stick should climb up at the climbing speed.");
            Assert.That(rig.Position.z + rig.Radius, Is.EqualTo(face).Within(0.05f), "The character should stay against the wall.");
            Assert.That(Vector3.Angle(Forward, Vector3.forward), Is.LessThan(1f), "The character should face the wall.");
        }

        [UnityTest]
        public IEnumerator AnUnclimbableWall_IsNotClimbed()
        {
            yield return SetUpWall(climbable: false);
            bool climbed = false;
            yield return rig.Run(1.5f, _ => Up, _ => climbed |= Climbing);
            Assert.That(climbed, Is.False, "A wall that isn't climbable shouldn't be climbed.");
            Assert.That(rig.Position.y, Is.EqualTo(0f).Within(0.05f), "The character should stay on the ground.");
        }

        [UnityTest]
        public IEnumerator WithClimbingTurnedOff_AClimbableWall_IsNotClimbed()
        {
            yield return SetUpWall();
            rig.SetMovementSetting("wallClimbSpeed", 0f);
            bool climbed = false;
            yield return rig.Run(1f, _ => Up, _ => climbed |= Climbing);
            Assert.That(climbed, Is.False, "A climbing speed of 0 should turn climbing off.");
        }

        [UnityTest]
        public IEnumerator HeadingIntoAClimbableWall_AtAGlancingAngle_StillTakesHoldOfIt()
        {
            yield return SetUpWall();
            float angle = rig.MovementSetting<float>("wallClimbStartMaxAngle") - 10f;
            Vector3 glancing = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            bool climbed = false;
            yield return rig.Run(1f, _ => CharacterTestRig.Move(glancing), _ => climbed |= Climbing);
            Assert.That(climbed, Is.True, "Heading into a climbable wall anywhere within the start angle should take hold of it.");
        }

        [UnityTest]
        public IEnumerator FallingIntoAClimbableWall_TakesHoldOfIt_RatherThanSlidingDownIt()
        {
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, 6f, 0f), settleSeconds: 0f);
            face = rig.Radius + 0.1f;
            rig.ClimbableBox("Wall", new Vector3(0f, 6f, face + 0.5f), new Vector3(10f, 12f, 1f));
            bool slid = false;
            yield return rig.Run(0.3f, _ => Up, _ => slid |= rig.InState(CharacterState.WallSliding));
            Assert.That(Climbing, Is.True, "Falling into a climbable wall should take hold of it.");
            Assert.That(slid, Is.False, "A climbable wall should be climbed, not slid down.");

            float heldAt = rig.Position.y;
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Position.y, Is.EqualTo(heldAt).Within(0.01f), "Holding on without input, the character shouldn't fall.");
            Assert.That(Climbing, Is.True);
        }

        [UnityTest]
        public IEnumerator ClimbingSideways_MovesAlongTheWall_AtTheClimbingSpeed()
        {
            yield return SetUpWall();
            yield return TakeHold();
            yield return rig.Run(0.5f, _ => Up); // Off the ground.
            Vector3 start = rig.Position;
            const float seconds = 1f;
            yield return rig.Run(seconds, _ => Right);
            Assert.That(Climbing, Is.True);
            Assert.That(rig.Position.x - start.x, Is.EqualTo(ClimbSpeed * seconds).Within(0.1f * ClimbSpeed * seconds),
                "Right on the stick should climb to the right at the climbing speed.");
            Assert.That(rig.Position.y, Is.EqualTo(start.y).Within(0.01f), "Climbing sideways shouldn't rise or fall.");
        }

        /// <summary>
        /// A plain wall with a climbable panel on its face, 2 m wide (x from -1 to 1) and reaching from the ground to 4 m:
        /// the character starts climbing the panel, in front of its middle.
        /// </summary>
        private IEnumerator SetUpPanel()
        {
            yield return SetUpStanding();
            const float thickness = 0.1f;
            rig.ClimbableBox("Panel", new Vector3(0f, 2f, face + thickness / 2f), new Vector3(2f, 4f, thickness));
            rig.Box("Wall", new Vector3(0f, 5f, face + thickness + 0.5f), new Vector3(20f, 10f, 1f));
            yield return TakeHold();
        }

        [UnityTest]
        public IEnumerator ClimbingSideways_StopsAtTheEdgeOfTheClimbableArea()
        {
            yield return SetUpPanel();
            yield return rig.Run(0.5f, _ => Up);
            yield return rig.Run(3f, _ => Right);
            Assert.That(Climbing, Is.True, "The character should still be climbing at the edge of the climbable area.");
            Assert.That(rig.Position.x, Is.GreaterThan(0.5f), "The character should have climbed across the panel.");
            Assert.That(rig.Position.x, Is.LessThan(1f + rig.Radius), "The character should stop at the panel's edge, not climb on along the plain wall.");

            yield return rig.Run(5f, _ => CharacterTestRig.Move(Vector3.left));
            Assert.That(Climbing, Is.True);
            Assert.That(rig.Position.x, Is.GreaterThan(-1f - rig.Radius), "The character should stop at the panel's other edge too.");
        }

        [UnityTest]
        public IEnumerator ClimbingUp_StopsAtTheTopOfTheClimbableArea()
        {
            yield return SetUpPanel();
            yield return rig.Run(4f, _ => Up);
            Assert.That(Climbing, Is.True, "The character should still be climbing at the top of the climbable area.");
            Assert.That(rig.Position.y + GripHeight, Is.LessThan(4f + 0.5f), "The character should stop at the panel's top, not climb on up the plain wall.");
            Assert.That(rig.Position.y + GripHeight, Is.GreaterThan(4f - 0.5f), "The character should have climbed to the panel's top.");
        }

        [UnityTest]
        public IEnumerator ClimbingRoundAnOutsideCorner_CarriesOnOntoTheNextFace()
        {
            yield return SetUpStanding();
            // A climbable pillar 2 m wide and 4 m deep, its front face in front of the character, its right face at x = 1.
            rig.ClimbableBox("Pillar", new Vector3(0f, 5f, face + 2f), new Vector3(2f, 10f, 4f));
            yield return TakeHold();
            yield return rig.Run(0.5f, _ => Up);
            yield return rig.Run(1.5f, _ => Right);

            Assert.That(Climbing, Is.True, "The character should still be climbing, round the corner.");
            Assert.That(Vector3.Angle(Forward, Vector3.left), Is.LessThan(10f), "The character should face the pillar's right face.");
            Assert.That(rig.Position.x - rig.Radius, Is.EqualTo(1f).Within(0.08f), "The character should be against the pillar's right face.");
            Assert.That(rig.Position.z, Is.GreaterThan(face + 0.25f), "The character should have climbed along the right face.");
        }

        [UnityTest]
        public IEnumerator ClimbingIntoAnInsideCorner_CarriesOnOntoTheOtherWall()
        {
            yield return SetUpWall();
            // Another climbable wall, to the right, facing -X, meeting the first in a corner (and long, so the character
            // doesn't reach its far end).
            const float side = 1.5f;
            rig.ClimbableBox("Side Wall", new Vector3(side + 0.5f, 5f, face - 5f), new Vector3(1f, 10f, 10f));
            yield return TakeHold();
            yield return rig.Run(0.5f, _ => Up);
            yield return rig.Run(2f, _ => Right);
            Assert.That(Climbing, Is.True, "The character should still be climbing, in the corner.");
            Assert.That(Vector3.Angle(Forward, Vector3.right), Is.LessThan(10f), "The character should have turned to the side wall.");

            float cornerZ = rig.Position.z;
            yield return rig.Run(1f, _ => Right);
            Assert.That(rig.Position.z, Is.LessThan(cornerZ - 0.5f * ClimbSpeed), "Climbing on, the character should move along the side wall.");
            Assert.That(side - (rig.Position.x + rig.Radius), Is.EqualTo(0f).Within(0.05f), "The character should be against the side wall.");
        }

        [UnityTest]
        public IEnumerator ClimbingUpToAWalkableTop_ClimbsOverOntoIt()
        {
            yield return SetUpStanding();
            // Too high to reach from the ground: it has to be climbed.
            const float top = 3.5f;
            // Deep, so walking on after climbing onto it doesn't walk off its far side.
            rig.ClimbableBox("Block", new Vector3(0f, top / 2f, face + 5f), new Vector3(4f, top, 10f));
            var events = new List<MovementEvents>();
            rig.Movement.MovementEventRaised += (movementEvent, _) => events.Add(movementEvent);
            yield return TakeHold();
            yield return rig.Run(top / ClimbSpeed + rig.Mode<LedgeMode>().ledgeClimbDuration * rig.Movement.DurationScale + 0.5f, _ => Up);
            Assert.That(Climbing, Is.False);

            Assert.That(Climbing, Is.False, "The character should no longer be climbing.");
            Assert.That(rig.Grounded, Is.True, "The character should be standing on top.");
            Assert.That(rig.Position.y, Is.EqualTo(top).Within(0.05f), "The character should be on top of the block.");
            Assert.That(rig.Position.z, Is.GreaterThan(face), "The character should be over the block's top, past its face.");
            Assert.That(events.Count(e => e == MovementEvents.LedgeClimbStarted), Is.EqualTo(1), "Climbing over the top should be a climb up onto a ledge.");
            Assert.That(events.Count(e => e == MovementEvents.WallClimbEnded), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator JumpingOffAClimbableWall_KicksUpAndAway()
        {
            yield return SetUpWall();
            yield return TakeHold();
            yield return rig.Run(0.5f, _ => Up);
            var events = new List<MovementEvents>();
            rig.Movement.MovementEventRaised += (movementEvent, _) => events.Add(movementEvent);
            Vector3 launch = Vector3.zero;
            yield return rig.Run(0.3f, step => step == 0 ? CharacterTestRig.Jump(0) : CharacterTestRig.Idle(), step =>
            {
                if (step == 0) { launch = Velocity; }
            });

            float up = rig.MovementSetting<float>("wallClimbJumpUpSpeed") * rig.Movement.SpeedScale;
            float away = rig.MovementSetting<float>("wallClimbJumpAwaySpeed") * rig.Movement.SpeedScale;
            Assert.That(launch.y, Is.EqualTo(up).Within(0.05f * up), "Jumping off the wall should launch upward.");
            Assert.That(-launch.z, Is.EqualTo(away).Within(0.05f * away), "Jumping off the wall should push away from it.");
            Assert.That(Climbing, Is.False);
            Assert.That(events.Count(e => e == MovementEvents.WallJumped), Is.EqualTo(1));
            Assert.That(events.Count(e => e == MovementEvents.WallClimbEnded), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Crouch_LetsGo_AndTheWallIsTakenHoldOfAgainOnlyAfterTheRegrabDelay()
        {
            yield return SetUpWall();
            yield return TakeHold();
            yield return rig.Run(2f, _ => Up);
            float delay = rig.MovementSetting<float>("wallClimbRegrabDelay");
            float letGoAt = rig.Position.y;
            bool climbedDuringDelay = false;
            yield return rig.Run(delay - 0.05f, step => step == 0 ? CharacterTestRig.Crouch(0, Vector3.forward) : Up,
                _ => climbedDuringDelay |= Climbing);
            Assert.That(climbedDuringDelay, Is.False, "Crouch should let go, and the wall shouldn't be taken hold of again before the regrab delay.");
            Assert.That(rig.Position.y, Is.LessThan(letGoAt - 0.1f), "Having let go, the character should fall.");

            yield return rig.Run(0.15f, _ => Up);
            Assert.That(Climbing, Is.True, "Steering into the wall after the regrab delay should take hold of it again.");
            Assert.That(rig.Grounded, Is.False, "Test setup: the character should still be in mid-air.");
        }

        [UnityTest]
        public IEnumerator AtTheBottom_PressingDown_StepsOffOntoTheGround()
        {
            yield return SetUpWall();
            var events = new List<MovementEvents>();
            rig.Movement.MovementEventRaised += (movementEvent, _) => events.Add(movementEvent);
            yield return TakeHold();
            Assert.That(rig.Grounded, Is.True, "Test setup: the character should be at the bottom, on the ground.");
            float atWall = rig.Position.z;
            yield return rig.Run(ExitDuration + 0.5f, _ => Down);
            Assert.That(Climbing, Is.False, "Pressing down at the bottom should step off the wall.");
            Assert.That(rig.Position.z, Is.LessThan(atWall - 0.5f), "The character should walk away from the wall.");
            Assert.That(events.Count(e => e == MovementEvents.WallClimbStarted), Is.EqualTo(1));
            Assert.That(events.Count(e => e == MovementEvents.WallClimbEnded), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator HoldingOntoAMovingSurface_MovesAlongWithIt()
        {
            yield return SetUpStanding();
            // A climbable wall that moves sideways at 1 m/s, far enough (back and forth over 24 s) not to turn round
            // during the test.
            var platform = rig.Platform(new Vector3(0f, 8f, face + 0.5f), new Vector3(6f, 7.5f, 1f), new Vector3(12f, 0f, 0f), 24f, Vector3.zero);
            platform.gameObject.AddComponent<Level.ClimbableSurface>();
            yield return TakeHold();
            yield return rig.Run(0.4f, _ => Up);
            Vector3 platformStart = platform.GetComponent<Rigidbody>().position, characterStart = rig.Position;
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());

            Vector3 moved = platform.GetComponent<Rigidbody>().position - platformStart;
            Assert.That(Mathf.Abs(moved.x), Is.GreaterThan(0.3f), "Test setup: the wall should have moved.");
            Assert.That(Climbing, Is.True, "The character should still be holding on.");
            Assert.That(rig.Position.x - characterStart.x, Is.EqualTo(moved.x).Within(0.05f), "The character should move along with the wall.");
            Assert.That(rig.Position.y, Is.EqualTo(characterStart.y).Within(0.02f), "The character should hold still on the wall.");
        }

        /// <summary>The character standing in front of a tall plain wall, 10 m wide, with a PaintedClimbableSurface and no paint yet.</summary>
        private IEnumerator SetUpPaintableWall(Vector3 size = default)
        {
            yield return SetUpStanding();
            if (size == default) { size = new Vector3(10f, 10f, 1f); }
            painted = rig.Box("Paintable Wall", new Vector3(0f, size.y / 2f, face + size.z / 2f), size).AddComponent<Level.PaintedClimbableSurface>();
        }

        private Level.PaintedClimbableSurface painted;

        [UnityTest]
        public IEnumerator APaintedPatchOfAWall_IsClimbed_OnlyWithinThePaint()
        {
            yield return SetUpPaintableWall();
            // A round patch 3 m across on the wall's face, its middle 2.5 m up, in front of the character.
            const float radius = 1.5f, middle = 2.5f;
            painted.Paint(new Vector3(0f, middle, face), Vector3.back, radius);
            yield return TakeHold();
            // Up to the patch's middle, where it's widest, before going sideways.
            float toMiddle = middle - (rig.Position.y + GripHeight);
            yield return rig.Run(toMiddle / ClimbSpeed, _ => Up);

            yield return rig.Run(3f, _ => Right);
            Assert.That(Climbing, Is.True, "The character should still be climbing at the edge of the paint.");
            Assert.That(rig.Position.x, Is.GreaterThan(0.5f * radius), "The character should have climbed across the patch.");
            Assert.That(rig.Position.x, Is.LessThan(radius + 0.1f), "The character's grip shouldn't leave the paint.");

            yield return rig.Run(4f, _ => Up);
            Assert.That(Climbing, Is.True);
            Assert.That(rig.Position.y + GripHeight, Is.LessThan(middle + radius + 0.1f), "The character shouldn't climb up past the paint.");
            Assert.That(rig.Position.y + GripHeight, Is.GreaterThan(middle), "The character should have climbed up the patch.");
        }

        [UnityTest]
        public IEnumerator AnUnpaintedPartOfAPaintedWall_IsNotClimbed()
        {
            yield return SetUpPaintableWall();
            painted.Paint(new Vector3(4f, 2.5f, face), Vector3.back, 1f); // Off to the side, out of reach.
            bool climbed = false;
            yield return rig.Run(1f, _ => Up, _ => climbed |= Climbing);
            Assert.That(climbed, Is.False, "Only the painted part of a wall should be climbable.");
        }

        [UnityTest]
        public IEnumerator ABoxPaintedOnOneFace_IsNotClimbedRoundItsCorner_UntilTheFaceRoundItIsPaintedToo()
        {
            // A pillar 2 m wide and 4 m deep, its front face (z = face) in front of the character, its right face at x = 1.
            yield return SetUpPaintableWall(new Vector3(2f, 10f, 4f));
            painted.Paint(new Vector3(0f, 5f, face), Vector3.back, 6f);
            yield return TakeHold();
            yield return rig.Run(0.5f, _ => Up);
            yield return rig.Run(1.5f, _ => Right);
            Assert.That(Climbing, Is.True);
            Assert.That(Vector3.Angle(Forward, Vector3.forward), Is.LessThan(5f), "The character shouldn't go round onto the unpainted face.");
            Assert.That(rig.Position.x, Is.EqualTo(1f).Within(0.15f), "The character should stop at the corner.");

            painted.Paint(new Vector3(1f, 5f, face + 2f), Vector3.right, 6f);
            yield return rig.Run(1.5f, _ => Right);
            Assert.That(Climbing, Is.True);
            Assert.That(Vector3.Angle(Forward, Vector3.left), Is.LessThan(10f), "Painted round the corner, the character should climb onto the right face.");
            Assert.That(rig.Position.z, Is.GreaterThan(face + 0.25f));
        }

        [UnityTest]
        public IEnumerator Paint_IsForTheFacePainted_AndErasedAsPainted()
        {
            yield return SetUpPaintableWall(new Vector3(4f, 4f, 0.2f));
            var front = new Vector3(0f, 2f, face);
            var back = new Vector3(0f, 2f, face + 0.2f);
            Assert.That(painted.Paint(front, Vector3.back, 0.5f), Is.GreaterThan(0));
            Assert.That(painted.IsPaintedAt(front, Vector3.back, atEdge: false), Is.True, "The point painted should be painted.");
            Assert.That(painted.IsPaintedAt(front + Vector3.right * 0.3f, Vector3.back, atEdge: false), Is.True, "The brush's area should be painted.");
            Assert.That(painted.IsPaintedAt(front + Vector3.right * 1.2f, Vector3.back, atEdge: false), Is.False, "Beyond the brush shouldn't be painted.");
            Assert.That(painted.IsPaintedAt(back, Vector3.forward, atEdge: false), Is.False, "The back of a thin wall shouldn't be painted with its front.");

            Assert.That(painted.Erase(front, Vector3.back, 0.2f), Is.GreaterThan(0));
            Assert.That(painted.IsPaintedAt(front, Vector3.back, atEdge: false), Is.False, "An erased point shouldn't be painted.");
            Assert.That(painted.IsPaintedAt(front + Vector3.right * 0.4f, Vector3.back, atEdge: false), Is.True, "Erasing should leave the paint beyond it.");
        }

        [UnityTest]
        public IEnumerator TakingHoldOfAClimbableWall_GivesBackAirDashesAndJumps_AsLandingDoes()
        {
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, 8f, 0f), settleSeconds: 0f);
            face = rig.Radius + 2f;
            rig.ClimbableBox("Wall", new Vector3(0f, 8f, face + 0.5f), new Vector3(10f, 16f, 1f));
            int maxAirDashes = rig.MovementSetting<int>("maxAirDashes");
            if (maxAirDashes < 1) { Assert.Ignore("The character has no air dashes."); }
            // Never on the ground yet, and an air dash at the wall: no jumps or air dashes left.
            int dashesUsed = 0;
            yield return rig.Run(0.6f, step => step == 0 ? CharacterTestRig.Dash(0, Vector3.forward) : Up,
                _ => dashesUsed = Mathf.Max(dashesUsed, rig.Movement.dash.airDashesUsed));
            Assert.That(dashesUsed, Is.EqualTo(1), "Test setup: the character should have used an air dash.");
            Assert.That(Climbing, Is.True, "Test setup: the character should have taken hold of the wall.");
            Assert.That(rig.Movement.dash.airDashesUsed, Is.EqualTo(0), "Taking hold of a wall should give back the character's air dashes.");
            Assert.That(rig.Movement.jumpsRemaining, Is.EqualTo(rig.Movement.maxJumps), "Taking hold of a wall should give back the character's jumps.");
        }

        [UnityTest]
        public IEnumerator TakingHoldFromTheGround_StepsUpOntoTheWallFirst_HoldingStill_ThenClimbs()
        {
            yield return SetUpWall();
            for (int step = 0; step < CharacterTestRig.StepsFor(0.5f) && !Climbing; step++) { yield return rig.Run(1, _ => Up); }
            Assert.That(rig.Movement.wallClimb.IsEntering, Is.True, "Taking hold from the ground should step up onto the wall first.");
            Vector3 start = rig.Position;
            int enteringSteps = 0;
            yield return rig.Run(EnterDuration + 0.4f, _ => Up, _ =>
            {
                if (rig.Movement.wallClimb.IsEntering)
                {
                    enteringSteps++;
                    // Still, but for being drawn in to the holding distance from the wall.
                    Assert.That(Mathf.Abs(rig.Position.y - start.y) + Mathf.Abs(rig.Position.x - start.x), Is.LessThan(0.01f), "Stepping onto the wall, the character should hold still.");
                }
            });
            Assert.That(enteringSteps * Time.fixedDeltaTime, Is.EqualTo(EnterDuration).Within(2f * Time.fixedDeltaTime), "Stepping onto the wall should take the enter time.");
            Assert.That(rig.Position.y - start.y, Is.GreaterThan(0.5f * ClimbSpeed * 0.4f), "Once on the wall, the character should climb.");
        }

        [UnityTest]
        public IEnumerator TakingHoldInMidAir_DoesNotStepOntoTheWall()
        {
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, 6f, 0f), settleSeconds: 0f);
            face = rig.Radius + 0.1f;
            rig.ClimbableBox("Wall", new Vector3(0f, 6f, face + 0.5f), new Vector3(10f, 12f, 1f));
            bool entered = false;
            yield return rig.Run(0.3f, _ => Up, _ => entered |= rig.Movement.wallClimb.IsEntering);
            Assert.That(Climbing, Is.True, "Test setup: the character should have taken hold.");
            Assert.That(entered, Is.False, "Taking hold in mid-air shouldn't play stepping onto the wall.");
        }

        [UnityTest]
        public IEnumerator SteppingOffTheBottom_HoldsStillForTheExitTime_ButLettingGoOrJumpingOffDoesNot()
        {
            yield return SetUpWall();
            yield return TakeHold();
            Vector3 start = rig.Position;
            int exitingSteps = 0;
            yield return rig.Run(ExitDuration + 0.3f, _ => Down, _ =>
            {
                if (rig.Movement.wallClimb.IsExiting)
                {
                    exitingSteps++;
                    Assert.That(Mathf.Abs(rig.Position.y - start.y) + Mathf.Abs(rig.Position.x - start.x), Is.LessThan(0.01f), "Stepping off the wall, the character should hold still.");
                }
            });
            Assert.That(exitingSteps * Time.fixedDeltaTime, Is.EqualTo(ExitDuration).Within(2f * Time.fixedDeltaTime), "Stepping off should take the exit time.");
            Assert.That(Climbing, Is.False);

            // Higher up, letting go with Crouch drops at once.
            yield return rig.Run(0.6f, _ => Up);
            yield return TakeHold();
            yield return rig.Run(1f, _ => Up);
            bool exited = false;
            yield return rig.Run(0.2f, step => step == 0 ? CharacterTestRig.Crouch(0) : CharacterTestRig.Idle(), _ => exited |= rig.Movement.wallClimb.IsExiting);
            Assert.That(Climbing, Is.False, "Crouch should let go.");
            Assert.That(exited, Is.False, "Letting go in mid-air shouldn't play stepping off.");
        }

        [UnityTest]
        public IEnumerator PushingUpWithAWalkableTopWithinReach_ClimbsOntoIt_FromWhereTheHandsReachIt()
        {
            yield return SetUpStanding();
            const float top = 4f;
            rig.ClimbableBox("Block", new Vector3(0f, top / 2f, face + 5f), new Vector3(4f, top, 10f));
            yield return TakeHold();
            float climbStartFeet = float.NaN;
            yield return rig.Run((top - 1f) / ClimbSpeed + 0.5f, _ => Up, _ =>
            {
                if (float.IsNaN(climbStartFeet) && rig.InState(CharacterState.LedgeClimbing)) { climbStartFeet = rig.Position.y; }
            });
            Assert.That(climbStartFeet, Is.Not.NaN, "The character should climb onto the top.");
            float reach = rig.MovementSetting<float>("ledgeGrabMaxHeight") * rig.StandingHeight;
            float lowest = rig.MovementSetting<float>("ledgeGrabMinHeight") * rig.StandingHeight;
            Assert.That(top - climbStartFeet, Is.InRange(lowest, reach + 0.1f), "The climb onto the top should start as soon as the hands reach it, as from hanging.");
        }
    }
}
