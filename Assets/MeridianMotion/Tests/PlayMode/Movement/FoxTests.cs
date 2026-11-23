using System.Collections;
using System.Linq;
using MeridianMotion.Characters;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace MeridianMotion.Tests.Movement
{
    /// <summary>
    /// Tests of the fox (Fox.prefab, the showcase's), the first generic-rig character that isn't a humanoid: it stands on its
    /// paws in a box body, its eye point at its eyes; trots as it walks and gallops sprinting, at its speeds; jumps once,
    /// to its jump height; crouches under what's too low to stand under; turns only as far as a corridor narrower than
    /// it's long lets it; walks up and down stairs; and, jumping onto a wall at an angle, runs along it. (Its Animator's states are
    /// FoxAbilityTests'.)
    /// Its tuning is read from its own movement mode, so retuning it doesn't fail them.
    /// </summary>
    [Category(TestArea.Movement)]
    public class FoxTests : CharacterRigFixture
    {

        private IEnumerator SpawnFox() => rig.StandOnFloor(CharacterTestRig.FoxOrSkip());

        [UnityTest]
        public IEnumerator TheFox_StandsOnItsPaws_InARoundedBodyAboveItsLegs_ItsEyesOnItsHead()
        {
            yield return SpawnFox();
            var body = (PrimitiveBody)rig.Movement.Body;
            Assert.That(body.AuthoredExtraParts.Count, Is.GreaterThan(0), "The fox's body should be of several parts (its back and its head).");
            Assert.That(body.AuthoredExtraParts.Append(body.AuthoredShape).Any(p => p.kind == BodyShapeKind.Box), Is.False,
                "The fox's body should be rounded, with no corners to catch on things.");
            Assert.That(rig.Movement.HoverOverSteps, Is.True, "The fox should hover over steps, its legs under its body.");
            Assert.That(rig.Grounded, Is.True, "The fox should stand on the floor.");
            Assert.That(rig.Position.y, Is.EqualTo(0f).Within(0.02f));
            Assert.That(rig.FrontExtent, Is.GreaterThan(2f * rig.SideExtent), "The fox should be longer than it's wide.");
            Animator animator = rig.Models.Animator;
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.isHuman, Is.False, "The fox's rig isn't a humanoid.");
            Assert.That(rig.Models.EyeHeight, Is.InRange(0.8f * rig.StandingHeight, 1.15f * rig.StandingHeight),
                "Its eye point should be at its eyes, about as high as its body.");
        }

        [UnityTest]
        public IEnumerator TheFox_TrotsAtItsWalkSpeed_AndGallopsAtItsRunSpeed([Values(false, true)] bool sprint)
        {
            yield return SpawnFox();
            System.Func<int, StepInputs> run = step => sprint ? CharacterTestRig.Sprint(Vector3.forward) : CharacterTestRig.Move(Vector3.forward);
            yield return rig.Run(0.3f, run);
            float speed = 0f;
            yield return rig.MeanSpeed(0.4f, run, s => speed = s);
            float expected = sprint ? rig.RunSpeed : rig.WalkSpeed;
            Assert.That(speed, Is.EqualTo(expected).Within(0.05f * expected));
        }

        [UnityTest]
        public IEnumerator TheFox_JumpsOnce_ToItsJumpHeight()
        {
            yield return SpawnFox();
            Assume.That(rig.MaxJumps, Is.EqualTo(1), "Test setup: the fox has one jump.");
            float start = rig.Position.y, highest = start;
            // Held all the way up, then pressed again at the top: there's no second jump.
            yield return rig.Run(1.4f, step => step < 40 ? CharacterTestRig.Jump() : CharacterTestRig.Jump(), _ => highest = Mathf.Max(highest, rig.Position.y));
            float jumpHeight = rig.MovementSetting<float>("maxJumpHeight") * rig.Movement.HeightScale;
            Assert.That(highest - start, Is.EqualTo(jumpHeight).Within(0.1f), "Held, the jump should rise the fox's jump height, and only once.");
        }

        [UnityTest]
        public IEnumerator TheFox_Crouched_PassesUnderABarTooLowToStandUnder()
        {
            yield return SpawnFox();
            float standing = rig.StandingHeight, crouched = rig.CrouchHeight * standing;
            float underside = Mathf.Lerp(crouched, standing, 0.5f), barZ = 1.5f + rig.FrontExtent;
            rig.Box("Bar", new Vector3(0f, underside + 0.5f, barZ), new Vector3(6f, 1f, 0.3f));
            Physics.SyncTransforms();
            yield return rig.Run(0.2f, step => step == 0 ? CharacterTestRig.Crouch() : CharacterTestRig.Idle());
            Assume.That(rig.CurrentHeight, Is.EqualTo(crouched).Within(0.02f), "Test setup: the fox should crouch.");
            yield return rig.RunUntil(() => rig.Position.z - rig.BackExtent > barZ + 0.15f, _ => CharacterTestRig.Move(Vector3.forward), timeoutSeconds: 8f);
            Assert.That(rig.Position.z - rig.BackExtent, Is.GreaterThan(barZ + 0.15f), "Crouched, the fox should pass under the bar.");
        }

        [UnityTest]
        public IEnumerator TheFox_InACorridorNarrowerThanItsLength_TurnsOnlyAsFarAsItHasRoom()
        {
            yield return SpawnFox();
            float wide = 2f * rig.SideExtent, length = rig.FrontExtent + rig.BackExtent;
            float width = Mathf.Lerp(wide, length, 0.4f);
            foreach (float side in new[] { -1f, 1f })
            {
                rig.Box(side < 0f ? "Corridor Wall (West)" : "Corridor Wall (East)", new Vector3(side * (width / 2f + 0.5f), 1f, 0f), new Vector3(1f, 2f, 30f));
            }
            Physics.SyncTransforms();
            float widest = 0f;
            bool alwaysFits = true;
            yield return rig.Run(1f, _ => CharacterTestRig.Move(Vector3.right), _ =>
            {
                widest = Mathf.Max(widest, Vector3.Angle(rig.Body.rotation * Vector3.forward, Vector3.forward));
                alwaysFits &= rig.BodyFits;
            });
            Assert.That(widest, Is.LessThan(80f), "In a corridor narrower than it's long, the fox shouldn't turn across it.");
            Assert.That(widest, Is.GreaterThan(1f), "It should still turn as far as it has room to.");
            Assert.That(alwaysFits, Is.True, "Turning shouldn't take the fox's body into the walls.");
        }

        [UnityTest]
        public IEnumerator TheFox_WalksUpAndDownAFlightOfStairs([Values(true, false)] bool up)
        {
            yield return SpawnFox();
            // As steep as the levels' stairs (3 up to 4 along), twelve steps a little under the step height.
            const int steps = 12;
            float rise = 0.85f * rig.StepHeight, run = 4f / 3f * rise, top = steps * rise;
            for (int i = 0; i < steps - 1; i++)
            {
                float height = up ? rise * (i + 1) : top - rise * (i + 1);
                rig.Box($"Step {i + 1}", new Vector3(0f, height / 2f, 1.5f + i * run + run / 2f), new Vector3(6f, height, run));
            }
            float landingZ = up ? 1.5f + (steps - 1) * run + 5f : -3.5f;
            rig.Box("Landing", new Vector3(0f, top / 2f, landingZ), new Vector3(6f, top, 10f));
            Physics.SyncTransforms();
            if (!up)
            {
                rig.Teleport(new Vector3(0f, top + 0.02f, -1f));
                yield return rig.Settle();
            }
            float end = 1.5f + steps * run + 1f;
            yield return rig.RunUntil(() => rig.Position.z > end, _ => CharacterTestRig.Move(Vector3.forward), timeoutSeconds: 10f);
            Assert.That(rig.Position.z, Is.GreaterThan(end), "The fox should get to the end of the stairs.");
            Assert.That(rig.Position.y, Is.EqualTo(up ? top : 0f).Within(0.05f), up ? "It should be on the landing at the top." : "It should be at the bottom.");
        }

        /// <summary>
        /// Sprinting at a wall at an angle and jumping onto it, steering along it, the fox runs along the wall, for most of
        /// a wall run's longest time. (Its long box reaches the wall first with a corner; the run is judged by how far its
        /// side will be from the wall once it's turned along it, or it turned along the wall, found it out of reach, and
        /// fell off in the next step.)
        /// </summary>
        [UnityTest]
        public IEnumerator TheFox_JumpingOntoAWallAtAnAngle_RunsAlongIt([Values(20f, 40f, 60f)] float angle, [Values(1f, -1f)] float side)
        {
            yield return JumpOntoAWall(angle, side, steerAlong: true);
        }

        /// <summary>
        /// Steering straight at a wall, steeply (as steep as the wall run's Max Steer Angle lets a run start), sprinting at
        /// it and jumping onto it, the fox runs along it: its box meets the wall with a corner while its side is still
        /// further from it than a person's would be, and it turns along the wall and is drawn in to it (see
        /// WallRunning.Reach), rather than colliding with it.
        /// </summary>
        [UnityTest]
        public IEnumerator TheFox_JumpingSteeplyAtAWall_SteeringAtIt_RunsAlongIt([Values(50f, 65f)] float angle, [Values(1f, -1f)] float side)
        {
            yield return JumpOntoAWall(angle, side, steerAlong: false);
        }

        /// <summary>
        /// Sprints at a wall at the angle given (on the side given) and, a metre from it, jumps, steering along it (or on at
        /// it), and asserts the fox ran along the wall for most of a wall run's longest time.
        /// </summary>
        private IEnumerator JumpOntoAWall(float angle, float side, bool steerAlong)
        {
            yield return SpawnFox();
            if (!steerAlong)
            {
                Assume.That(rig.MovementSetting<float>("wallRunMaxSteerAngle"), Is.GreaterThanOrEqualTo(angle), "Test setup: a run should be able to start steering this steeply.");
            }
            const float wallFace = 3f;
            rig.Box("Wall", new Vector3(side * (wallFace + 0.5f), 5f, 15f), new Vector3(1f, 10f, 40f));
            Physics.SyncTransforms();
            float a = angle * Mathf.Deg2Rad;
            Vector3 approach = new Vector3(side * Mathf.Sin(a), 0f, Mathf.Cos(a)), along = new Vector3(side * 0.35f, 0f, 1f).normalized;
            int jumpAt = -1;
            float ran = 0f;
            yield return rig.Run(2.2f, step =>
            {
                // Sprinting at the wall, then a metre from it, jumping, steering along it.
                if (jumpAt < 0 && side * rig.Position.x > wallFace - rig.SideExtent - 1f) { jumpAt = step; }
                if (jumpAt < 0) { return CharacterTestRig.Sprint(approach); }
                StepInputs inputs = CharacterTestRig.Sprint(steerAlong ? along : approach);
                inputs.jump = CharacterTestRig.Held();
                return inputs;
            }, _ => { if (rig.Movement.wallRun.IsWallRunning) { ran += Time.fixedDeltaTime; } });
            float longest = rig.Component<WallRunning>().WallRunMaxDuration * rig.Movement.DurationScale;
            TestContext.WriteLine($"Jumping onto a wall at {angle}° on the {(side > 0 ? "right" : "left")}, the fox ran along it for {ran:0.00} s (at most {longest:0.00} s).");
            Assert.That(ran, Is.GreaterThan(0.8f * longest), "The fox should run along the wall.");
        }
    }
}
