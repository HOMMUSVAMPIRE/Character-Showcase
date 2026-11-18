using System.Collections;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of how the character's movement resolves against the world: momentum that a collision blocks is lost
    /// rather than stored up, steep surfaces can't be stood on, and moving geometry pushes the character instead of
    /// passing through them.
    /// </summary>
    [Category(TestArea.Movement)]
    public class CollisionTests : CharacterRigFixture
    {

        /// <summary>
        /// Standing and walking on a pile of more overlapping boxes than the movement's query buffers hold to start with
        /// (see PhysicsQueries.QueryBuffer), their tops at different heights, the character stands on the highest: a
        /// buffer that fills grows and queries again, rather than leaving out the nearest hit (Unity gives them in no
        /// order) and standing the character on a lower box, inside the higher ones.
        /// </summary>
        [UnityTest]
        public IEnumerator OnMoreOverlappingCollidersThanABufferHolds_StandsOnTheHighest()
        {
            // More than the largest of the movement's buffers starts with (32), each top a little higher than the last,
            // made in an order of their own (the highest in the middle), all well under a step high.
            const int boxes = 40;
            const float rise = 0.004f;
            float highest = (boxes - 1) * rise;
            for (int i = 0; i < boxes; i++)
            {
                int level = (i * 17) % boxes;
                float top = level * rise;
                rig.Box($"Pile {level}", new Vector3(0f, top - 0.5f, 0f), new Vector3(8f, 1f, 16f));
            }
            yield return rig.SpawnSettled(new Vector3(0f, highest + 0.05f, 0f));
            Assume.That(highest, Is.LessThan(0.5f * rig.StepHeight), "Test setup: the pile should be well under a step high.");
            Assert.That(rig.Grounded, Is.True, "The character should stand on the pile.");
            Assert.That(rig.Position.y, Is.EqualTo(highest).Within(0.002f), "Standing, it should be on the highest box, not a lower one.");
            float lowest = float.PositiveInfinity;
            yield return rig.Run(1f, _ => CharacterTestRig.Move(Vector3.forward), _ => lowest = Mathf.Min(lowest, rig.Position.y));
            Assume.That(rig.Position.z, Is.GreaterThan(0.5f), "Test setup: the character should walk.");
            Assume.That(rig.Position.z, Is.LessThan(7.5f), "Test setup: the character should stay on the pile.");
            Assert.That(lowest, Is.EqualTo(highest).Within(0.002f), "Walking, it should keep to the highest box.");
        }

        [UnityTest]
        public IEnumerator JumpingIntoLowCeiling_FallsAwayInsteadOfSticking()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            // Its model loaded first: the body it may take from it (see CharacterModelHost's Body Fit) is the one tested.
            yield return rig.WaitForModel();
            // Half a step height above the head: the jump clears a step, so it reaches the ceiling.
            float headroom = 0.5f * rig.StepHeight;
            float ceiling = rig.CurrentHeight + headroom;
            rig.Box("Ceiling", new Vector3(0f, ceiling + 0.5f, 0f), new Vector3(10f, 1f, 10f));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());

            // Hold jump for longer than the jump lasts, so only the ceiling can end it.
            int stepsAtCeiling = 0;
            float highest = float.NegativeInfinity;
            int fewestJumps = int.MaxValue;
            yield return rig.Run(0.5f, step => CharacterTestRig.Jump(), _ =>
            {
                highest = Mathf.Max(highest, rig.Position.y);
                if (rig.Position.y > headroom - 0.03f) { stepsAtCeiling++; }
                fewestJumps = Mathf.Min(fewestJumps, rig.Movement.jump.remaining);
            });

            Assert.That(highest, Is.LessThanOrEqualTo(headroom + 0.01f), "Character pushed into the ceiling.");
            Assert.That(highest, Is.GreaterThan(headroom - 0.05f), "Jump should reach the ceiling.");
            Assert.That(stepsAtCeiling, Is.LessThanOrEqualTo(3), "Character stuck to the ceiling instead of falling away from it.");
            Assert.That(fewestJumps, Is.EqualTo(rig.MaxJumps - 1),
                "The press that bumped the ceiling started another jump (it should only ever start one).");
        }

        [UnityTest]
        public IEnumerator AirDashIntoWall_LosesItsMomentum()
        {
            // No floor: the character stays airborne, where nothing else slows it down.
            rig.SpawnCharacter(new Vector3(0f, 20f, 0f));
            // Its model loaded first: the body it may take from it (see CharacterModelHost's Body Fit) is the one tested.
            yield return rig.WaitForModel();
            Dashing dash = rig.Component<Dashing>();
            // The wall's face halfway along the dash, so the dash reaches it with speed to spare.
            float face = rig.FrontExtent + 0.5f * dash.DashSpeed * rig.Movement.SpeedScale * dash.DashDuration;
            GameObject wall = rig.Box("Wall", new Vector3(0f, 20f, face + 0.5f), new Vector3(10f, 100f, 1f));

            int dashSteps = CharacterTestRig.StepsFor(dash.DashDuration);
            yield return rig.Run(dashSteps + 3, step => step == 0 ? CharacterTestRig.Dash(Vector3.forward) : CharacterTestRig.Idle());
            Assert.That(rig.Position.z, Is.EqualTo(face - rig.FrontExtent).Within(0.05f), "The dash should end against the wall.");

            // Take the wall away: momentum the wall blocked shouldn't still be there to carry the character onward.
            wall.SetActive(false);
            float before = rig.Position.z;
            const float seconds = 0.2f;
            yield return rig.Run(seconds, _ => CharacterTestRig.Idle());

            float speed = (rig.Position.z - before) / seconds;
            Assert.That(speed, Is.LessThan(1f), "Momentum into the wall was kept while blocked, and released once the wall was gone.");
        }

        [UnityTest]
        public IEnumerator PerchedOnTheEdgeOfADropTallerThanAStep_SlidesOffIt()
        {
            // A block 2 m tall whose edge the character starts perched on: its middle well past the edge, its rounded
            // bottom on the edge, too steeply to stand there. A step's edge counts as ground (stairs); a drop this
            // tall's doesn't.
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            float radius = rig.Movement.Body.FootprintRadius;
            rig.Box("Block", new Vector3(0f, 1f, -2f), new Vector3(4f, 2f, 4f)); // Its edge at z = 0.
            rig.Body.position = new Vector3(0f, 2f + 0.3f * radius, 0.85f * radius);
            yield return rig.Run(1.5f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Position.y, Is.LessThan(0.05f), "The character should slide off the edge and fall, not perch on it.");
        }

        [UnityTest]
        public IEnumerator LandingOnSteepSlope_SlidesOffIt()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 6f, -1f));
            // A slope 10 degrees steeper than the steepest the character stands on, facing +Z. Its surface, half the
            // box's thickness out from its centre, meets the floor at z = 0.5 / sin(angle).
            float angle = Mathf.Min(rig.MaxSlopeAngle + 10f, 85f);
            rig.Box("Steep Slope", Vector3.zero, new Vector3(6f, 1f, 10f), Quaternion.Euler(angle, 0f, 0f));
            float foot = 0.5f / Mathf.Sin(angle * Mathf.Deg2Rad);

            yield return rig.RunUntilLanded();

            Assert.That(rig.Grounded, Is.True, "Character should end up on the floor.");
            Assert.That(rig.Position.y, Is.LessThan(0.05f), "Character stayed perched on a slope too steep to stand on.");
            Assert.That(rig.Position.z, Is.GreaterThan(foot - 0.05f), "Character should slide down the slope, away from it.");
        }

        /// <summary>
        /// Falling onto a walkable ramp, the character stops where it lands, and stays there standing idle. (Removing only
        /// the part of the fall heading into the ramp's surface would turn the rest into sliding down it.)
        /// </summary>
        [UnityTest]
        public IEnumerator LandingOnAWalkableRamp_StopsWhereItLands_AndDoesntSlideIdle()
        {
            rig.SpawnCharacter(new Vector3(0f, 50f, 0f));
            yield return rig.WaitForModel();
            // A ramp a little over half as steep as the steepest walkable slope, rising along +Z from the origin.
            float angle = 0.55f * rig.MaxSlopeAngle;
            Quaternion tilt = Quaternion.Euler(-angle, 0f, 0f);
            rig.Box("Ramp", tilt * new Vector3(0f, -0.25f, 5f), new Vector3(6f, 0.5f, 10f), tilt);
            float surfaceHeight = Mathf.Tan(angle * Mathf.Deg2Rad) * 3f;
            Physics.SyncTransforms();
            rig.Teleport(new Vector3(0f, surfaceHeight + 2f, 3f));
            Vector3 dropped = rig.Position;

            yield return rig.RunUntilLanded();
            Assert.That(rig.Grounded, Is.True, "Test setup: the character should land on the ramp.");
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());

            Vector3 moved = rig.Position - dropped;
            Assert.That(new Vector2(moved.x, moved.z).magnitude, Is.LessThan(0.03f), "Landing on a walkable ramp turned the fall into a slide down it.");

            // Standing idle there a while longer, it doesn't creep down the ramp either.
            Assert.That(rig.Grounded, Is.True, "Character should be standing on the ramp.");
            Vector3 settled = rig.Position;
            yield return rig.Run(2f, _ => CharacterTestRig.Idle());

            Vector3 drift = rig.Position - settled;
            Assert.That(new Vector2(drift.x, drift.z).magnitude, Is.LessThan(0.02f), "Character slid down the ramp while idle.");
            Assert.That(Mathf.Abs(drift.y), Is.LessThan(0.02f), "Character slid down the ramp while idle.");
        }

        [UnityTest]
        public IEnumerator MovingWall_PushesTheCharacter_InsteadOfPassingThrough()
        {
            rig.Floor();
            // A wall sliding along +X at 2 m/s, its face starting 1 m from the character's side.
            var wall = rig.Platform(new Vector3(-2f, 2f, 0f), new Vector3(1f, 2f, 4f), new Vector3(4f, 0f, 0f), 4f, Vector3.zero);
            Rigidbody wallBody = wall.GetComponent<Rigidbody>();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));

            float deepest = 0f;
            yield return rig.Run(1.4f, _ => CharacterTestRig.Idle(), _ =>
            {
                float wallFace = wallBody.position.x + 0.5f;
                deepest = Mathf.Max(deepest, wallFace - (rig.Position.x - rig.SideExtent));
            });

            Assert.That(rig.Position.x, Is.GreaterThan(1f), "The wall should have pushed the character along.");
            // The wall moves once more after the character's step, so allow one step of its travel.
            Assert.That(deepest, Is.LessThan(2f * Time.fixedDeltaTime + 0.02f), "The wall passed into the character.");
            Assert.That(rig.Grounded, Is.True, "Being pushed shouldn't lift the character off the floor.");
            Assert.That(rig.Position.y, Is.InRange(-0.01f, 0.03f));
        }
    }
}
