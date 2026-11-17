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
    /// Tests of crouching: Crouch lowers the character's collider and slows it to its crouch speed, pressing again stands
    /// it up, only where there's room (it waits for room, then stands by itself), the hold-to-crouch option, and
    /// jumping from a crouch. Expected values are read from the movement mode's settings.
    /// </summary>
    [Category(TestArea.Abilities)]
    public class CrouchTests : CharacterRigFixture
    {

        private float CrouchSpeed => rig.Component<Crouching>().CrouchSpeed * rig.Movement.SpeedScale;
        private bool Crouched => rig.InState(CharacterState.Crouching);


        /// <summary>Presses Crouch for one step, then lets go, with the given movement input throughout.</summary>
        private IEnumerator PressCrouch(Vector3 direction = default) =>
            rig.Run(3, step => step == 0 ? CharacterTestRig.Crouch(0, direction) : CharacterTestRig.Move(direction));


        [UnityTest]
        public IEnumerator PressingCrouch_LowersTheCollider_AndMovesAtCrouchSpeed()
        {
            yield return rig.StandOnFloor();
            float standing = rig.StandingHeight;
            yield return PressCrouch();

            Assert.That(Crouched, Is.True, "Pressing Crouch should crouch.");
            Assert.That(rig.CurrentHeight, Is.EqualTo(standing * rig.CrouchHeight).Within(0.01f), "Crouching should lower the collider to the crouch height.");
            Assert.That(rig.Grounded, Is.True, "Crouching should keep the character's feet on the ground.");

            yield return rig.Run(0.2f, _ => CharacterTestRig.Move(Vector3.forward));
            float speed = 0f;
            yield return rig.MeanSpeed(0.4f, _ => CharacterTestRig.Move(Vector3.forward), s => speed = s);
            Assert.That(speed, Is.EqualTo(CrouchSpeed).Within(0.05f * CrouchSpeed), "A crouched character should move at its crouch speed.");
        }

        /// <summary>
        /// The eye point (which a camera follows) eases down with the body as the character crouches, to the same share of
        /// the eye height as the body is of its standing height, and back up as it stands.
        /// </summary>
        [UnityTest]
        public IEnumerator Crouching_LowersTheEyePoint_WithTheBody_AndStandingRaisesIt()
        {
            yield return rig.StandOnFloor();
            CharacterModelHost models = rig.Models;
            if (models.EyePoint == null) { Assert.Ignore("The character has no eye point."); }
            void Visuals(int _) => rig.Animation.SmoothRotateVisuals(Time.fixedDeltaTime, Quaternion.identity);
            yield return rig.Run(0.1f, _ => CharacterTestRig.Idle(), Visuals);
            Assert.That(models.EyePoint.localPosition.y, Is.EqualTo(models.EyeHeight).Within(1e-3f), "Standing, the eye point is at the eye height.");

            yield return rig.Run(0.6f, step => step == 0 ? CharacterTestRig.Crouch(0) : CharacterTestRig.Idle(), Visuals);
            Assume.That(Crouched, Is.True, "Test setup: the character should be crouched.");
            Assert.That(models.EyePoint.localPosition.y, Is.EqualTo(models.EyeHeight * rig.CrouchHeight).Within(0.01f),
                "Crouched, the eye point should be lowered with the body.");

            yield return rig.Run(0.6f, step => step == 0 ? CharacterTestRig.Crouch(0) : CharacterTestRig.Idle(), Visuals);
            Assert.That(models.EyePoint.localPosition.y, Is.EqualTo(models.EyeHeight).Within(1e-3f), "Standing again, it's back up.");
        }

        [UnityTest]
        public IEnumerator PressingCrouchAgain_StandsUp_AndWalksAtWalkSpeed()
        {
            yield return rig.StandOnFloor();
            float standing = rig.StandingHeight;
            yield return PressCrouch();
            yield return PressCrouch();

            Assert.That(Crouched, Is.False, "A second press should stand the character up.");
            Assert.That(rig.CurrentHeight, Is.EqualTo(standing).Within(0.01f), "Standing should raise the collider again.");

            yield return rig.Run(0.2f, _ => CharacterTestRig.Move(Vector3.forward));
            float speed = 0f;
            yield return rig.MeanSpeed(0.4f, _ => CharacterTestRig.Move(Vector3.forward), s => speed = s);
            Assert.That(speed, Is.EqualTo(rig.WalkSpeed).Within(0.05f * rig.WalkSpeed), "Once standing, the character should walk at its walk speed.");
        }

        [UnityTest]
        public IEnumerator UnderALowCeiling_StaysCrouched_UntilThereIsRoom_ThenStandsByItself()
        {
            yield return rig.StandOnFloor();
            float standing = rig.StandingHeight;
            // A ceiling between the crouch height and the standing height, starting a little ahead of the character.
            float underside = Mathf.Lerp(standing * rig.CrouchHeight, standing, 0.5f);
            float start = rig.FrontExtent + 0.5f, length = 2f;
            rig.Box("Low Ceiling", new Vector3(0f, underside + 0.5f, start + length / 2f), new Vector3(4f, 1f, length));

            // Crouch, walk in under the ceiling, and press Crouch to stand there.
            yield return PressCrouch();
            yield return rig.Run(rig.SecondsToWalk(start + length / 2f, 0f) * rig.WalkSpeed / CrouchSpeed, _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(rig.Position.z, Is.GreaterThan(start + rig.FrontExtent), "Test setup: the character should be under the ceiling.");
            yield return PressCrouch();
            Assert.That(Crouched, Is.True, "With no room above, the character should stay crouched.");
            Assert.That(rig.CurrentHeight, Is.LessThan(underside), "The collider should stay under the ceiling.");

            // Walk out the far side: it stands up by itself as soon as there's room.
            bool stoodUnderCeiling = false;
            yield return rig.Run(rig.SecondsToWalk(length, 0f) * rig.WalkSpeed / CrouchSpeed, _ => CharacterTestRig.Move(Vector3.forward), _ =>
            {
                bool under = rig.Position.z + rig.FrontExtent > start && rig.Position.z - rig.BackExtent < start + length;
                if (under && rig.CurrentHeight > underside) { stoodUnderCeiling = true; }
            });
            Assert.That(stoodUnderCeiling, Is.False, "The character stood up into the ceiling.");
            Assert.That(rig.Position.z - rig.BackExtent, Is.GreaterThan(start + length), "Test setup: the character should be past the ceiling.");
            Assert.That(Crouched, Is.False, "Once there's room, the character should stand up by itself.");
            Assert.That(rig.CurrentHeight, Is.EqualTo(standing).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator HoldToCrouch_CrouchesWhileHeld_AndStandsOnRelease()
        {
            yield return rig.StandOnFloor();
            rig.SetMovementSetting("holdToCrouch", true);

            yield return rig.Run(0.3f, step => CharacterTestRig.Crouch(step));
            Assert.That(Crouched, Is.True, "Holding Crouch should crouch.");
            yield return rig.Run(0.3f, step => CharacterTestRig.Crouch(step + 15));
            Assert.That(Crouched, Is.True, "Still holding Crouch should stay crouched.");
            yield return rig.Run(2, _ => CharacterTestRig.Idle());
            Assert.That(Crouched, Is.False, "Letting go of Crouch should stand up.");
        }

        [UnityTest]
        public IEnumerator HoldingCrouch_InToggleMode_CrouchesOnlyOnce()
        {
            yield return rig.StandOnFloor();
            int crouchStarts = 0;
            bool was = false;
            yield return rig.Run(1f, step => CharacterTestRig.Crouch(step), _ =>
            {
                if (Crouched && !was) { crouchStarts++; }
                was = Crouched;
            });
            Assert.That(crouchStarts, Is.EqualTo(1), "Holding Crouch should crouch once, not toggle every step.");
            Assert.That(Crouched, Is.True);
        }

        [UnityTest]
        public IEnumerator JumpingFromACrouch_StandsUpAndJumps_ButNotUnderALowCeiling()
        {
            yield return rig.StandOnFloor();
            yield return PressCrouch();
            float highest = rig.Position.y;
            yield return rig.Run(0.4f, step => CharacterTestRig.Jump(step), _ => highest = Mathf.Max(highest, rig.Position.y));
            Assert.That(highest, Is.GreaterThan(0.5f), "Jumping from a crouch should jump.");
            Assert.That(rig.CurrentHeight, Is.EqualTo(rig.StandingHeight).Within(0.01f), "Jumping from a crouch should stand up.");

            yield return rig.RunUntilLanded();
            Assert.That(rig.Grounded, Is.True, "Test setup: the character should have landed.");

            // Under a ceiling too low to stand in: no jump.
            float underside = rig.StandingHeight * Mathf.Lerp(rig.CrouchHeight, 1f, 0.5f);
            yield return PressCrouch();
            rig.Box("Low Ceiling", rig.Position + Vector3.up * (underside + 0.5f), new Vector3(4f, 1f, 4f));
            float startY = rig.Position.y;
            highest = startY;
            yield return rig.Run(0.4f, step => CharacterTestRig.Jump(step), _ => highest = Mathf.Max(highest, rig.Position.y));
            Assert.That(highest - startY, Is.LessThan(0.01f), "With no room to stand, jumping from a crouch should do nothing.");
            Assert.That(Crouched, Is.True);
        }

        [UnityTest]
        public IEnumerator JumpingDuringACrouchedDash_UnderALowCeiling_KeepsDashing()
        {
            yield return rig.StandOnFloor();
            if (rig.Component<Dashing>().DashDuration <= 0f) { Assert.Ignore("The character can't dash."); }
            yield return PressCrouch();
            float underside = rig.StandingHeight * Mathf.Lerp(rig.CrouchHeight, 1f, 0.5f);
            rig.Box("Low Ceiling", new Vector3(0f, underside + 0.5f, 10f), new Vector3(4f, 1f, 30f));
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());

            int dashSteps = CharacterTestRig.StepsFor(rig.Component<Dashing>().DashDuration);
            int dashingAfterJump = 0;
            yield return rig.Run(dashSteps, step =>
            {
                Inputs inputs = CharacterTestRig.Dash(step, Vector3.forward);
                if (step >= 2) { inputs.jump = CharacterTestRig.Held(step - 2); }
                return inputs;
            }, step => { if (step >= 2 && rig.Dashing) { dashingAfterJump++; } });
            Assert.That(dashingAfterJump, Is.GreaterThan(0), "With no room to stand and jump, a jump press shouldn't cancel the dash.");
            Assert.That(Crouched, Is.True);
        }

        [UnityTest]
        public IEnumerator Crouching_RaisesCrouchStartedAndCrouchEnded_OnceEach()
        {
            yield return rig.StandOnFloor();
            rig.ClearEvents();
            var events = rig.EventsRaised;

            yield return PressCrouch();
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            yield return PressCrouch();
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());

            Assert.That(events.Count(e => e == MovementEvents.CrouchStarted), Is.EqualTo(1));
            Assert.That(events.Count(e => e == MovementEvents.CrouchEnded), Is.EqualTo(1));
            Assert.That(events.ToList().IndexOf(MovementEvents.CrouchStarted), Is.LessThan(events.ToList().IndexOf(MovementEvents.CrouchEnded)));
        }

        [UnityTest]
        public IEnumerator CrouchingAtTwiceTheSize_LowersTheColliderInProportion()
        {
            yield return rig.StandOnFloor();
            rig.Controller.SetScale(Vector3.one * 2f, immediately: true);
            yield return rig.Run(0.2f, _ => CharacterTestRig.Idle());
            float standing = rig.StandingHeight;
            yield return PressCrouch();
            Assert.That(rig.CurrentHeight, Is.EqualTo(standing * rig.CrouchHeight).Within(0.02f), "The crouch height is a share of the character's height at any size.");

            yield return PressCrouch();
            Assert.That(rig.CurrentHeight, Is.EqualTo(standing).Within(0.02f), "Standing up should restore the collider at the character's size.");
        }
    }
}
