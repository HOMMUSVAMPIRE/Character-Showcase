using System.Collections;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of vaulting: a character in mid-air heading into a wall whose top is too low to grab as a ledge but too
    /// high to step onto goes over it, rising over the vault's rise time and leaving at its share of the speed it came
    /// in at (or the vault's slowest), rather than sliding down it from its knees; onto something low and deep it steps
    /// up instead, keeping its speed; sprinting into a low wall on the ground vaults it, but walking into it, glancing
    /// along it, or a railing at the edge of a drop doesn't; stairs are climbed, not vaulted, run up or jumped onto;
    /// without room over the top it doesn't vault; and with vaulting off, a low wall isn't slid down either, its top
    /// grabbed once the character has fallen far enough to reach it. Expected values are read from the movement mode's
    /// settings.
    /// </summary>
    public class VaultTests
    {
        private CharacterTestRig rig;
        // The wall's top (y), its face (z), which faces -Z, toward the character, and its far side (z).
        private float top, face, back;

        [SetUp]
        public void SetUp() => rig = new CharacterTestRig();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            rig.Dispose();
            yield return null;
        }

        private bool Vaulting => rig.InState(CharacterState.Vaulting);
        private float MinSpeed => rig.MovementSetting<float>("vaultMinSpeed") * rig.Movement.SpeedScale;
        private float RiseDuration => rig.Mode<VaultMode>().vaultRiseDuration * rig.Movement.DurationScale;
        /// <summary>The speed a vault leaves the character with, coming in at the given speed.</summary>
        private float ExitSpeed(float speed) => Mathf.Max(MinSpeed, speed * rig.MovementSetting<float>("vaultSpeedRetention"));
        private float StepUpShare => rig.MovementSetting<float>("vaultStepUpHeight");
        private float LedgeShare => rig.MovementSetting<float>("ledgeGrabMinHeight");
        // Between the step height and the lowest ledge the character could grab, as a share of its standing height.
        private float LowWallShare => Mathf.Lerp(rig.StepHeight / rig.StandingHeight, LedgeShare, 0.5f);

        /// <summary>
        /// A floor, the character in mid-air (or standing on the floor) facing +Z (its model loaded), and in front of it
        /// a wall whose top is topAbove() standing heights above the character's feet (by default, halfway between the step
        /// height and the lowest ledge it could grab), its face distance ahead of the character, and thickness deep.
        /// </summary>
        private IEnumerator SetUpLowWall(System.Func<float> topAbove = null, float thickness = 0.4f, bool onTheGround = false, float distance = 0.25f)
        {
            rig.Floor();
            if (onTheGround) { yield return rig.SpawnSettled(Vector3.zero); }
            else { yield return rig.SpawnSettled(new Vector3(0f, 3f, 0f), settleSeconds: 0f); }
            top = rig.Position.y + (topAbove != null ? topAbove() : LowWallShare) * rig.StandingHeight;
            face = rig.Position.z + rig.Radius + distance;
            back = face + thickness;
            rig.Box("Low Wall", new Vector3(0f, top / 2f, face + thickness / 2f), new Vector3(10f, top, thickness));
        }

        /// <summary>
        /// Runs the character, launched at the given speed (if any) with the given input, for the given time, recording
        /// whether it vaulted, stepped up, slid down a wall or hung from it, and its horizontal speed on the step after
        /// its vault ended.
        /// </summary>
        private IEnumerator RunInto(float speed, Inputs inputs, float seconds, VaultRecord record)
        {
            if (speed > 0f) { yield return rig.Launch(Vector3.forward * speed, inputs); }
            bool wasVaulting = false;
            yield return rig.Run(seconds, _ => inputs, _ =>
            {
                bool vaulting = Vaulting;
                record.vaulted |= vaulting;
                record.steppedUp |= rig.Movement.vault.IsSteppingUp;
                if (vaulting) { record.vaultingSteps++; }
                record.wallSlid |= rig.InState(CharacterState.WallSliding);
                record.hung |= rig.InState(CharacterState.LedgeHanging);
                if (wasVaulting && !vaulting && record.speedAfter < 0f) { record.speedAfter = rig.HorizontalSpeed; }
                wasVaulting = vaulting;
            });
        }

        private class VaultRecord
        {
            public bool vaulted, steppedUp, wallSlid, hung;
            public int vaultingSteps;
            public float speedAfter = -1f;
        }

        [UnityTest]
        public IEnumerator HeadingIntoTheTopOfALowWallInMidAir_VaultsOverIt_LeavingWithItsShareOfTheSpeed()
        {
            yield return SetUpLowWall();
            float speed = rig.RunSpeed;
            var record = new VaultRecord();
            yield return RunInto(speed, CharacterTestRig.Sprint(Vector3.forward), 1.6f, record);

            Assert.That(record.vaulted, Is.True, "The character should vault over the low wall.");
            Assert.That(record.wallSlid, Is.False, "The character shouldn't slide down a wall it only catches with its legs.");
            Assert.That(record.hung, Is.False, "A wall too low to grab shouldn't be hung from.");
            Assert.That(record.vaultingSteps, Is.GreaterThan(CharacterTestRig.StepsFor(RiseDuration)), "A vault should rise over its rise time, then swing over.");
            Assert.That(record.speedAfter, Is.GreaterThan(0.95f * ExitSpeed(speed)), "The vault should leave the character with its share of the speed.");
            Assert.That(rig.Position.z - rig.Radius, Is.GreaterThan(back), "The character should end up beyond the wall.");
            Assert.That(rig.Grounded, Is.True, $"The character should land on the far side (it's at {rig.Position}, the wall's top at {top}).");
        }

        [UnityTest]
        public IEnumerator HeadingOntoALowPlatformInMidAir_StepsUpOntoIt_KeepingItsSpeed_WithoutVaulting()
        {
            yield return SetUpLowWall(topAbove: () => Mathf.Lerp(rig.StepHeight / rig.StandingHeight, StepUpShare, 0.6f), thickness: 6f);
            var record = new VaultRecord();
            float speed = rig.WalkSpeed;
            yield return RunInto(speed, CharacterTestRig.Move(Vector3.forward), 0.5f, record);

            Assert.That(record.steppedUp, Is.True, "The character should step up onto the low platform.");
            Assert.That(record.vaulted, Is.False, "Something low and deep should be stepped up onto, not vaulted onto.");
            Assert.That(rig.Grounded, Is.True, "The character should stand on the platform.");
            Assert.That(rig.Position.y, Is.EqualTo(top).Within(0.03f), "The character should be on the platform's top.");
            Assert.That(rig.HorizontalSpeed, Is.GreaterThan(0.9f * speed), "Stepping up should keep the character's speed.");
        }

        [UnityTest]
        public IEnumerator HeadingOntoAHigherBlockInMidAir_VaultsOntoItsTop()
        {
            yield return SetUpLowWall(topAbove: () => Mathf.Lerp(StepUpShare, LedgeShare, 0.5f), thickness: 6f);
            var record = new VaultRecord();
            yield return RunInto(rig.WalkSpeed, CharacterTestRig.Move(Vector3.forward), 1f, record);

            Assert.That(record.vaulted, Is.True, "The character should vault up onto the block, too high to step up onto.");
            Assert.That(rig.Grounded, Is.True, "The character should stand on the block's top.");
            Assert.That(rig.Position.y, Is.EqualTo(top).Within(0.03f), "The character should be on the block's top.");
        }

        [UnityTest]
        public IEnumerator SteeringIntoALowWallWithoutSpeed_VaultsAtTheVaultsSlowestSpeed()
        {
            yield return SetUpLowWall();
            var record = new VaultRecord();
            yield return RunInto(0f, CharacterTestRig.Move(Vector3.forward), 1.2f, record);

            Assert.That(record.vaulted, Is.True, "Steering into the low wall should vault over it.");
            Assert.That(record.speedAfter, Is.GreaterThan(0.95f * MinSpeed), "The vault should leave the character at its slowest speed at least.");
            Assert.That(rig.Position.z - rig.Radius, Is.GreaterThan(back), "The character should end up beyond the wall.");
        }

        [UnityTest]
        public IEnumerator WithoutRoomOverTheTop_DoesNotVault()
        {
            yield return SetUpLowWall();
            // A ceiling just above the wall's top, too low for the character to fit over it.
            rig.Box("Low Ceiling", new Vector3(0f, top + 0.5f * rig.StandingHeight + 0.5f, face + 2f), new Vector3(10f, 1f, 4f));
            var record = new VaultRecord();
            yield return RunInto(rig.RunSpeed, CharacterTestRig.Sprint(Vector3.forward), 0.5f, record);
            Assert.That(record.vaulted || record.steppedUp, Is.False, "Without room over the top, the character shouldn't vault.");
        }

        [UnityTest]
        public IEnumerator WithVaultingOff_ALowWallIsNotSlidDown_AndItsTopIsGrabbedOnceInReach()
        {
            yield return SetUpLowWall();
            rig.SetMovementSetting("vaultRiseDuration", 0f);
            var record = new VaultRecord();
            yield return RunInto(rig.WalkSpeed, CharacterTestRig.Move(Vector3.forward), 0.8f, record);

            Assert.That(record.vaulted, Is.False, "Test setup: vaulting should be off.");
            Assert.That(record.wallSlid, Is.False, "The character shouldn't slide down a wall it only catches with its legs.");
            Assert.That(record.hung, Is.True, "Falling beside the wall, the character should grab its top once it's within reach.");
        }

        [UnityTest]
        public IEnumerator SprintingIntoALowWallOnTheGround_VaultsOverIt()
        {
            yield return SetUpLowWall(onTheGround: true, distance: 2f);
            var record = new VaultRecord();
            yield return RunInto(0f, CharacterTestRig.Sprint(Vector3.forward), 1.5f, record);

            Assert.That(record.vaulted, Is.True, "Sprinting into the low wall should vault over it.");
            Assert.That(record.speedAfter, Is.GreaterThan(0.95f * ExitSpeed(rig.RunSpeed)), "The vault should leave the character with its share of the sprint's speed.");
            Assert.That(rig.Position.z - rig.Radius, Is.GreaterThan(back), "The character should end up beyond the wall.");
            Assert.That(rig.Grounded, Is.True, "The character should land on the far side.");
        }

        [UnityTest]
        public IEnumerator WalkingIntoALowWallOnTheGround_DoesNotVault([Values(false, true)] bool vaultingFromTheGroundOff)
        {
            yield return SetUpLowWall(onTheGround: true, distance: 1f);
            if (vaultingFromTheGroundOff) { rig.SetMovementSetting("vaultFromGround", false); }
            var record = new VaultRecord();
            Inputs inputs = vaultingFromTheGroundOff ? CharacterTestRig.Sprint(Vector3.forward) : CharacterTestRig.Move(Vector3.forward);
            yield return RunInto(0f, inputs, 1f, record);

            Assert.That(record.vaulted, Is.False, vaultingFromTheGroundOff
                ? "With vaulting from the ground off, sprinting into the wall shouldn't vault it."
                : "Walking into the low wall shouldn't vault it: only sprinting does.");
            Assert.That(rig.Position.z + rig.Radius, Is.LessThan(face + 0.05f), "The character should stay in front of the wall.");
        }

        [UnityTest]
        public IEnumerator SprintingAlongALowWallAtAnAngle_DoesNotVaultIt()
        {
            yield return SetUpLowWall(onTheGround: true, distance: 1f);
            float angle = rig.MovementSetting<float>("vaultGroundMaxAngle") + 15f;
            Vector3 glancing = Quaternion.Euler(0f, angle, 0f) * Vector3.forward; // angle off head-on (+Z), toward +X.
            var record = new VaultRecord();
            yield return RunInto(0f, CharacterTestRig.Sprint(glancing), 1f, record);
            Assert.That(record.vaulted, Is.False, "Sprinting into a low wall at a glancing angle shouldn't vault it.");
        }

        [UnityTest]
        public IEnumerator SprintingIntoARailingAtTheEdgeOfADrop_DoesNotVaultOverIt()
        {
            // A deck, its far edge at a railing, with the ground far below beyond it.
            rig.Box("Deck", new Vector3(0f, -0.5f, -10f), new Vector3(20f, 1f, 22f));
            rig.Box("Ground Below", new Vector3(0f, -20.5f, 0f), new Vector3(100f, 1f, 100f));
            yield return rig.SpawnSettled(new Vector3(0f, 0f, -2f));
            float height = LowWallShare * rig.StandingHeight;
            face = 1f - 0.08f;
            rig.Box("Railing", new Vector3(0f, height / 2f, 0.96f), new Vector3(20f, height, 0.08f));
            var record = new VaultRecord();
            yield return RunInto(0f, CharacterTestRig.Sprint(Vector3.forward), 1f, record);

            Assert.That(record.vaulted, Is.False, "A railing at the edge of a drop shouldn't be vaulted over from the ground.");
            Assert.That(rig.Position.y, Is.GreaterThan(-0.1f), "The character should stay on the deck.");
        }

        /// <summary>A staircase rising toward +Z from the floor, its first step's riser at z = start, and a long landing at its top.</summary>
        private float BuildStairs(float start, int steps, float rise, float tread)
        {
            for (int i = 0; i < steps; i++)
            {
                float height = (i + 1) * rise;
                rig.Box($"Step {i}", new Vector3(0f, height / 2f, start + (i + 0.5f) * tread), new Vector3(4f, height, tread));
            }
            float landing = steps * rise;
            rig.Box("Landing", new Vector3(0f, landing / 2f, start + steps * tread + 20f), new Vector3(4f, landing, 40f));
            return landing;
        }

        [UnityTest]
        public IEnumerator SprintingUpAndJumpingOntoStairs_ClimbsThem_WithoutVaulting([Values(false, true)] bool jump)
        {
            rig.Floor();
            yield return rig.SpawnSettled(Vector3.zero);
            float rise = 0.85f * rig.StepHeight;
            float landing = BuildStairs(jump ? 3f : 1.5f, 8, rise, 0.3f);
            var record = new VaultRecord();
            yield return rig.Run(2.5f, step => jump && step == 0 ? JumpSprint() : CharacterTestRig.Sprint(Vector3.forward), _ =>
            {
                record.vaulted |= Vaulting;
                record.steppedUp |= rig.Movement.vault.IsSteppingUp;
            });

            Assert.That(record.vaulted, Is.False, "Stairs should be climbed, not vaulted.");
            Assert.That(rig.Position.y, Is.EqualTo(landing).Within(0.05f), "The character should reach the top of the stairs.");
        }

        private static Inputs JumpSprint()
        {
            Inputs inputs = CharacterTestRig.Sprint(Vector3.forward);
            inputs.jump = CharacterTestRig.Held(0);
            return inputs;
        }
    }
}
