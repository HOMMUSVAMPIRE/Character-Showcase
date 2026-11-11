using System.Collections;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of the dash: its burst distance and direction, ignoring gravity, momentum afterwards, the cooldown, the
    /// input buffer, one dash per press, the air dash limit, and not wasting jumps mid-dash.
    /// Expected values are read from the movement mode's dash settings, so retuning the dash doesn't break the tests.
    /// </summary>
    public class DashTests
    {
        private CharacterTestRig rig;

        [SetUp]
        public void SetUp() => rig = new CharacterTestRig();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            rig.Dispose();
            yield return null;
        }

        private float DashSpeed => rig.Mode<DashMode>().dashSpeed;
        private float DashDuration => rig.Mode<DashMode>().dashDuration;
        private float DashCooldown => rig.MovementSetting<float>("dashCooldown");
        private float DashInputBuffer => rig.MovementSetting<float>("dashInputBuffer");
        private float WalkSpeed => rig.Mode<WalkRunMode>().walkSpeed;
        private int DashSteps => CharacterTestRig.StepsFor(DashDuration);

        private IEnumerator Settle()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
        }

        /// <summary>Press dash for one step (then release) and count how many steps the character spends dashing.</summary>
        private IEnumerator DashOnce(Vector3 direction, System.Action<int> dashingSteps)
        {
            int count = 0;
            yield return rig.Run(DashSteps + 1, step => step == 0 ? CharacterTestRig.Dash(0, direction) : CharacterTestRig.Idle(),
                _ => { if (rig.Dashing) { count++; } });
            dashingSteps(count);
        }

        [UnityTest]
        public IEnumerator GroundDash_BurstsInTheInputDirection_AndFacesIt()
        {
            yield return Settle();
            Vector3 start = rig.Position;

            int dashingSteps = 0;
            yield return DashOnce(Vector3.right, n => dashingSteps = n);

            Vector3 travelled = rig.Position - start;
            Assert.That(dashingSteps, Is.EqualTo(DashSteps).Within(1), "Dash should last its duration.");
            Assert.That(travelled.x, Is.GreaterThan(0.6f * DashSpeed * DashDuration), "Dash should cover a burst of distance.");
            Assert.That(travelled.x, Is.GreaterThan(2f * WalkSpeed * DashDuration), "Dash should be much faster than walking.");
            Assert.That(Mathf.Abs(travelled.z), Is.LessThan(0.01f), "Dash should go straight in the input direction.");
            Assert.That(Vector3.Angle(rig.Body.rotation * Vector3.forward, Vector3.right), Is.LessThan(1f), "Character should face the dash.");
        }

        [UnityTest]
        public IEnumerator DashWithoutInput_GoesTheWayTheCharacterFaces()
        {
            yield return Settle();
            Vector3 start = rig.Position;

            yield return DashOnce(Vector3.zero, _ => { });

            Vector3 travelled = rig.Position - start;
            Assert.That(travelled.z, Is.GreaterThan(0.6f * DashSpeed * DashDuration), "Dash should go forward.");
            Assert.That(Mathf.Abs(travelled.x), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator Dash_EndsWithMomentum_ThatEasesOut()
        {
            yield return Settle();
            yield return DashOnce(Vector3.right, _ => { });

            float justAfter = 0f;
            yield return rig.Run(1, _ => CharacterTestRig.Idle(), _ => justAfter = rig.Movement.lastFrameMovementData.finalVelocity.x);
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());
            Vector3 rest = rig.Position;
            yield return rig.Run(0.2f, _ => CharacterTestRig.Idle());

            Assert.That(justAfter, Is.GreaterThan(0.5f), "Character should keep some momentum as the dash ends, not stop dead.");
            Assert.That(justAfter, Is.LessThan(DashSpeed), "Momentum after the dash should be less than the dash itself.");
            Assert.That(Vector3.Distance(rig.Position, rest), Is.LessThan(0.01f), "Momentum should ease out until the character stops.");
        }

        [UnityTest]
        public IEnumerator AirDash_IgnoresGravityWhileDashing()
        {
            rig.SpawnCharacter(new Vector3(0f, 20f, 0f));
            float startY = rig.Position.y;
            float lowest = startY;

            yield return rig.Run(DashSteps, step => step == 0 ? CharacterTestRig.Dash(0, Vector3.right) : CharacterTestRig.Idle(),
                _ => lowest = Mathf.Min(lowest, rig.Position.y));

            Assert.That(startY - lowest, Is.LessThan(0.01f), "Character should not fall during a dash.");
            Assert.That(rig.Position.x, Is.GreaterThan(0.6f * DashSpeed * DashDuration));
        }

        [UnityTest]
        public IEnumerator HoldingDash_OnlyDashesOnce()
        {
            yield return Settle();

            int dashStarts = 0;
            bool wasDashing = false;
            // Hold well past the cooldown.
            yield return rig.Run(DashCooldown * 4f, step => CharacterTestRig.Dash(step, Vector3.right), _ =>
            {
                if (rig.Dashing && !wasDashing) { dashStarts++; }
                wasDashing = rig.Dashing;
            });

            Assert.That(dashStarts, Is.EqualTo(1), "Holding the button should not repeat the dash.");
        }

        [UnityTest]
        public IEnumerator PressDuringCooldown_IsIgnored_UnlessWithinTheInputBuffer()
        {
            yield return Settle();
            int cooldownSteps = CharacterTestRig.StepsFor(DashCooldown);

            // Press again too early (long before the cooldown ends) and hold: never dashes again.
            int earlyPress = DashSteps + 2;
            int earlyStarts = 0;
            yield return rig.Run(cooldownSteps * 2, step =>
                    step == 0 ? CharacterTestRig.Dash(0) :
                    step < earlyPress ? CharacterTestRig.Idle() :
                    CharacterTestRig.Dash(step - earlyPress),
                step => { if (step >= earlyPress && rig.Dashing) { earlyStarts++; } });
            Assert.That(cooldownSteps - earlyPress, Is.GreaterThan(CharacterTestRig.StepsFor(DashInputBuffer)),
                "Test setup: the early press must fall outside the input buffer.");
            Assert.That(earlyStarts, Is.EqualTo(0), "A press long before the cooldown ends should be ignored.");

            // Press just before the cooldown ends (inside the buffer) and hold: dashes as soon as it can.
            yield return rig.Run(0.2f, _ => CharacterTestRig.Idle());
            int bufferedPress = cooldownSteps - CharacterTestRig.StepsFor(DashInputBuffer * 0.5f);
            int dashedAt = -1;
            yield return rig.Run(cooldownSteps * 2, step =>
                    step == 0 ? CharacterTestRig.Dash(0) :
                    step < bufferedPress ? CharacterTestRig.Idle() :
                    CharacterTestRig.Dash(step - bufferedPress),
                step => { if (step >= bufferedPress && dashedAt < 0 && rig.Dashing) { dashedAt = step; } });
            Assert.That(dashedAt, Is.GreaterThanOrEqualTo(0), "A press just before the cooldown ends should be buffered.");
            Assert.That(dashedAt, Is.LessThanOrEqualTo(cooldownSteps + 1), "The buffered dash should start as soon as the cooldown ends.");
        }

        [UnityTest]
        public IEnumerator AirDashes_AreLimited_UntilLanding()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 15f, 0f));
            int maxAirDashes = rig.Mode<DashMode>().maxAirDashes;
            int cooldownSteps = CharacterTestRig.StepsFor(DashCooldown) + 1;

            // Try to dash more times than allowed before landing, each press after the cooldown.
            int airDashes = 0;
            bool wasDashing = false;
            yield return rig.Run(cooldownSteps * (maxAirDashes + 1), step =>
                    step % cooldownSteps == 0 ? CharacterTestRig.Dash(0) : CharacterTestRig.Idle(),
                _ =>
                {
                    Assert.That(rig.Grounded, Is.False, "Test setup: the character should still be in the air.");
                    if (rig.Dashing && !wasDashing) { airDashes++; }
                    wasDashing = rig.Dashing;
                });
            Assert.That(airDashes, Is.EqualTo(maxAirDashes), "Only the allowed number of air dashes should happen.");

            // Land, then dash again.
            yield return rig.Run(2f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Grounded, Is.True, "Test setup: the character should have landed.");
            int groundDashSteps = 0;
            yield return DashOnce(Vector3.forward, n => groundDashSteps = n);
            Assert.That(groundDashSteps, Is.GreaterThan(0), "Landing should give back the ability to dash.");
        }

        [UnityTest]
        public IEnumerator JumpDuringDash_CancelsItIntoAJump_KeepingSomeDashSpeed()
        {
            yield return Settle();
            float retention = rig.MovementSetting<float>("dashJumpSpeedRetention");
            const int jumpAt = 3;

            bool dashingAfterJump = false;
            float riseSpeed = 0f, horizontalSpeed = 0f;
            yield return rig.Run(jumpAt + 4, step =>
                    step == 0 ? CharacterTestRig.Dash(0, Vector3.right) :
                    step < jumpAt ? CharacterTestRig.Idle() :
                    CharacterTestRig.Jump(step - jumpAt),
                step =>
                {
                    if (step < jumpAt) { return; }
                    dashingAfterJump |= rig.Dashing;
                    Vector3 v = rig.Movement.lastFrameMovementData.finalVelocity;
                    if (step == jumpAt + 2) { riseSpeed = v.y; horizontalSpeed = new Vector2(v.x, v.z).magnitude; }
                });

            Assert.That(dashingAfterJump, Is.False, "Jumping should end the dash immediately.");
            Assert.That(riseSpeed, Is.GreaterThan(1f), "The jump should happen straight away.");
            Assert.That(horizontalSpeed, Is.EqualTo(DashSpeed * retention).Within(0.25f * DashSpeed * retention),
                "The jump should carry the configured share of the dash's speed.");
        }

        [UnityTest]
        public IEnumerator JumpHeldFromBeforeTheDash_DoesNotCancelIt()
        {
            yield return Settle();
            // Jump and keep holding the button, then dash in the air.
            yield return rig.Run(4, step => CharacterTestRig.Jump(step));

            int dashingSteps = 0;
            yield return rig.Run(DashSteps + 1, step =>
                {
                    Inputs inputs = CharacterTestRig.Jump(4 + step, Vector3.right);
                    inputs.dash = CharacterTestRig.Dash(step).dash;
                    return inputs;
                },
                _ => { if (rig.Dashing) { dashingSteps++; } });

            Assert.That(dashingSteps, Is.EqualTo(DashSteps).Within(1), "A jump held since before the dash should not cancel it.");
        }

        [UnityTest]
        public IEnumerator DashDuringJump_CancelsVerticalMomentum_AndTheJumpDoesNotResume()
        {
            yield return Settle();
            // Start a jump and keep holding it through a dash and beyond.
            const int dashAt = 4;
            float fastestRiseDuringDash = float.NegativeInfinity, fastestRiseAfterDash = float.NegativeInfinity;
            yield return rig.Run(dashAt + DashSteps + 10, step =>
                {
                    Inputs inputs = CharacterTestRig.Jump(step);
                    if (step >= dashAt) { inputs.dash = CharacterTestRig.Dash(step - dashAt).dash; }
                    return inputs;
                },
                step =>
                {
                    float vy = rig.Movement.lastFrameMovementData.finalVelocity.y;
                    if (step >= dashAt && step < dashAt + DashSteps) { fastestRiseDuringDash = Mathf.Max(fastestRiseDuringDash, vy); }
                    if (step >= dashAt + DashSteps) { fastestRiseAfterDash = Mathf.Max(fastestRiseAfterDash, vy); }
                });

            Assert.That(fastestRiseDuringDash, Is.EqualTo(0f).Within(0.01f), "The dash should cancel the jump's upward momentum.");
            Assert.That(fastestRiseAfterDash, Is.LessThanOrEqualTo(0.01f), "Still holding jump after the dash should not resume the jump.");
        }
    }
}
