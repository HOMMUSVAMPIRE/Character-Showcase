using System.Collections;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of the fox (Fox.prefab, made by FoxGenerator), the first creature that isn't a humanoid: it stands on its
    /// paws in a box body, its eye point at its eyes; trots as it walks and gallops sprinting, at its speeds; jumps once,
    /// to its jump height; crouches under what's too low to stand under; turns only as far as a corridor narrower than
    /// it's long lets it; walks up and down stairs; and plays its own Animator's states (Locomotion, Crouch, Airborne).
    /// Its tuning is read from its own movement mode, so retuning it doesn't fail them.
    /// </summary>
    [Category(TestArea.Movement)]
    public class FoxTests : CharacterRigFixture
    {
        private const string FoxPrefabPath = "Assets/Prefabs/Fox.prefab";

        private IEnumerator SpawnFox()
        {
            GameObject prefab = CharacterTestRig.LoadPrefab(FoxPrefabPath);
            if (prefab == null) { Assert.Ignore($"There's no fox ('{FoxPrefabPath}'): generate it with Tools > Character Showcase > Generate Fox."); }
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f), prefab);
        }

        [UnityTest]
        public IEnumerator TheFox_StandsOnItsPaws_InABoxBody_ItsEyesOnItsHead()
        {
            yield return SpawnFox();
            Assert.That(rig.Movement.Body.Collider, Is.InstanceOf<BoxCollider>(), "The fox's body should be a box.");
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
            System.Func<int, Inputs> run = step => sprint ? CharacterTestRig.Sprint(Vector3.forward, step) : CharacterTestRig.Move(Vector3.forward);
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
            yield return rig.Run(1.4f, step => step < 40 ? CharacterTestRig.Jump(step) : CharacterTestRig.Jump(step - 40), _ => highest = Mathf.Max(highest, rig.Position.y));
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
            yield return rig.Run(0.2f, step => step == 0 ? CharacterTestRig.Crouch(0) : CharacterTestRig.Idle());
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
                yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            }
            float end = 1.5f + steps * run + 1f;
            yield return rig.RunUntil(() => rig.Position.z > end, _ => CharacterTestRig.Move(Vector3.forward), timeoutSeconds: 10f);
            Assert.That(rig.Position.z, Is.GreaterThan(end), "The fox should get to the end of the stairs.");
            Assert.That(rig.Position.y, Is.EqualTo(up ? top : 0f).Within(0.05f), up ? "It should be on the landing at the top." : "It should be at the bottom.");
        }

        [UnityTest]
        public IEnumerator TheFox_PlaysLocomotion_Crouch_AndAirborne()
        {
            yield return SpawnFox();
            Animator animator = rig.Models.Animator;
            AnimationController animation = rig.Animation;
            animator.enabled = false; // Stepped here, with the movement, as the character's own update would each frame.
            void Step(int _)
            {
                animation.UpdateAnimator(rig.Movement.lastFrameMovementData, Time.fixedDeltaTime);
                animator.Update(Time.fixedDeltaTime);
            }
            bool InState(string state) => animator.GetCurrentAnimatorStateInfo(0).IsName(state);

            yield return rig.Run(0.5f, _ => CharacterTestRig.Move(Vector3.forward), Step);
            Assert.That(InState("Locomotion"), Is.True, "Trotting, the fox should play Locomotion.");
            yield return rig.Run(0.5f, step => step == 0 ? CharacterTestRig.Crouch(0) : CharacterTestRig.Idle(), Step);
            Assert.That(InState("Crouch"), Is.True, "Crouched, the fox should play Crouch.");
            yield return rig.Run(0.5f, step => step == 0 ? CharacterTestRig.Crouch(0) : CharacterTestRig.Idle(), Step);
            bool airborne = false;
            yield return rig.Run(0.6f, step => CharacterTestRig.Jump(step), step => { Step(step); airborne |= InState("Airborne"); });
            Assert.That(airborne, Is.True, "Jumping, the fox should play Airborne.");
        }
    }
}
