using System.Collections;
using MeridianMotion.Characters;
using MeridianMotion.Levels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeridianMotion.Tests.Movement
{
    /// <summary>
    /// Tests of MMotionMotor.Respawn beyond the ability kit's (every ability lets go when respawned while it's under way,
    /// and the character stands still: AbilityFixture): it gives the air dashes and jumps back, stands a
    /// crouched character up, isn't carried back by
    /// the moving ground it stood on, leaves a rope and a crate it held free and where they were, and works right after
    /// the character is spawned.
    /// </summary>
    [Category(TestArea.Movement)]
    public class RespawnTests : CharacterRigFixture
    {
        private static readonly Vector3 Point = new(20f, 0f, 20f);

        private IEnumerator RespawnAndSettle(int steps = 2)
        {
            rig.Movement.Respawn(Point, Quaternion.identity);
            yield return rig.Run(steps, _ => CharacterTestRig.Idle());
        }

        private void AssertStandingStillAtThePoint()
        {
            Vector3 off = rig.Position - Point;
            Assert.That(new Vector2(off.x, off.z).magnitude, Is.LessThan(0.01f), $"The character should be where it was respawned (it's at {rig.Position}).");
            Vector3 velocity = rig.Velocity;
            Assert.That(new Vector2(velocity.x, velocity.z).magnitude, Is.LessThan(0.01f), $"The character should keep no momentum (it's moving at {velocity}).");
        }

        [UnityTest]
        public IEnumerator Respawn_AfterUsingTheAirDashAndJumps_HasThemBack()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 15f, 0f));
            yield return rig.Run(1, _ => CharacterTestRig.Dash(Vector3.forward));
            yield return rig.RunUntil(() => !rig.Dashing, _ => CharacterTestRig.Idle(), timeoutSeconds: 2f);
            Assume.That(rig.Movement.dash.airDashesUsed, Is.GreaterThan(0), "Test setup: the character should have used an air dash.");
            rig.Movement.jump.remaining = 0;

            yield return RespawnAndSettle();

            Assert.That(rig.Movement.dash.airDashesUsed, Is.EqualTo(0), "Respawned on the ground, the air dashes should be back.");
            Assert.That(rig.Movement.jump.remaining, Is.EqualTo(rig.MaxJumps), "Respawned on the ground, the jumps should be back.");
            AssertStandingStillAtThePoint();
        }

        [UnityTest]
        public IEnumerator Respawn_StandingOnAMovingPlatform_IsntCarriedBack()
        {
            rig.Floor(-5f);
            rig.Box("Pad", Point + Vector3.down * 0.5f, new Vector3(6f, 1f, 6f));
            TickedPlatform platform = rig.Platform(Vector3.zero, new Vector3(4f, 0.3f, 4f), new Vector3(1.5f, 0f, 0f), 3f, Vector3.zero);
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());
            Assume.That(rig.Movement.groundTransform, Is.EqualTo(platform.transform), "Test setup: the character should be standing on the platform.");

            yield return RespawnAndSettle(CharacterTestRig.StepsFor(1f));

            AssertStandingStillAtThePoint();
        }

        [UnityTest]
        public IEnumerator Respawn_HoldingACrate_LetsGo_AndTheCrateStaysWhereItIs()
        {
            rig.Floor();
            rig.Box("Pad", Point + Vector3.down * 0.5f, new Vector3(6f, 1f, 6f));
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
            const float size = 1.5f; // As PushPullTests': one the character takes hold of.
            PushableBlock crate = rig.PushableBox(new Vector3(0f, size / 2f, rig.Position.z + rig.FrontExtent + 0.1f + size / 2f), Vector3.one * size);
            yield return rig.Run(0.1f, _ => CharacterTestRig.Idle());
            yield return rig.RunUntil(() => rig.InState(CharacterState.Grabbing), _ => CharacterTestRig.Interact(Vector3.forward), timeoutSeconds: 1f);
            Assume.That(crate.IsHeld, Is.True, "Test setup: the character should be holding the crate.");
            Vector3 where = crate.Position;

            yield return RespawnAndSettle(CharacterTestRig.StepsFor(0.5f));

            Assert.That(crate.IsHeld, Is.False, "The crate should be let go of.");
            Assert.That(Vector3.Distance(crate.Position, where), Is.LessThan(0.01f), "The crate should stay where it was.");
            AssertStandingStillAtThePoint();
        }

        [UnityTest]
        public IEnumerator Respawn_OnARope_LetsGo_AndTheRopeIsFree()
        {
            rig.Floor();
            rig.Box("Pad", Point + Vector3.down * 0.5f, new Vector3(6f, 1f, 6f));
            SwingRope rope = rig.Rope(new Vector3(0f, 10f, 0f), 8f);
            yield return rig.SpawnSettled(Vector3.zero, settleSeconds: 0f);
            float grip = rig.MovementSetting<float>("ropeGripHeight") * rig.StandingHeight;
            rig.Teleport(new Vector3(0f, 6f - grip, -1.5f));
            rig.Movement.OverrideVelocity(new Vector3(0f, 0f, 6f));
            yield return rig.RunUntil(() => rig.InState(CharacterState.RopeSwinging), _ => CharacterTestRig.Idle(), timeoutSeconds: 1f);
            Assume.That(rope.IsHeld, Is.True, "Test setup: the character should be holding the rope.");

            yield return RespawnAndSettle();

            Assert.That(rope.IsHeld, Is.False, "The rope should be let go of.");
            AssertStandingStillAtThePoint();
        }

        [UnityTest]
        public IEnumerator Respawn_Crouched_StandsUp()
        {
            rig.Floor();
            rig.Box("Pad", Point + Vector3.down * 0.5f, new Vector3(6f, 1f, 6f));
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f));
            yield return rig.Run(3, _ => CharacterTestRig.Crouch());
            Assume.That(rig.InState(CharacterState.Crouching), Is.True, "Test setup: the character should be crouched.");

            yield return RespawnAndSettle(CharacterTestRig.StepsFor(0.3f));

            Assert.That(rig.InState(CharacterState.Crouching), Is.False, "Respawned where there's room, the character should stand.");
            Assert.That(rig.CurrentHeight, Is.EqualTo(rig.StandingHeight).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Respawn_RightAfterSpawning_PutsItThere()
        {
            rig.Floor();
            rig.Box("Pad", Point + Vector3.down * 0.5f, new Vector3(6f, 1f, 6f));
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));

            yield return RespawnAndSettle();

            AssertStandingStillAtThePoint();
        }
    }
}
