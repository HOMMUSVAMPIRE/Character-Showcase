using System.Collections;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of bodies of Unity's other primitive colliders (see PrimitiveBody), in place of the character's upright
    /// capsule: a box, a sphere, and a capsule centred off the feet each walk up to a wall and stop with their front
    /// against it, stand on the floor, and hold postures: lowered from the top (crouched), and laid down along the facing,
    /// resting on the feet (lying). Bodies that aren't round about the feet turn with the character.
    /// </summary>
    [Category(TestArea.Movement)]
    public class BodyShapeTests : CharacterRigFixture
    {
        private const float WallDistance = 2f;

        private static void ABox(BoxCollider box)
        {
            box.size = new Vector3(0.8f, 1.8f, 0.6f);
            box.center = new Vector3(0f, 0.9f, 0f);
        }

        private static void ASphere(SphereCollider sphere)
        {
            sphere.radius = 0.45f;
            sphere.center = new Vector3(0f, 0.45f, 0f);
        }

        // A capsule whose middle is ahead of the feet (a body leaning forward, or one carried in front of its legs).
        private static void ACapsuleAhead(CapsuleCollider capsule)
        {
            capsule.direction = 1;
            capsule.radius = 0.4f;
            capsule.height = 1.8f;
            capsule.center = new Vector3(0f, 0.9f, 0.3f);
        }

        private IEnumerator OnTheFloor()
        {
            rig.Floor();
            yield return rig.SpawnSettled(Vector3.zero, settleSeconds: 0f);
        }

        /// <summary>Walks the character forward into a wall WallDistance beyond its front, returning where its front ends up and the wall's face.</summary>
        private IEnumerator WalkIntoAWall(System.Action<float, float> result)
        {
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            Assume.That(rig.Grounded, Is.True, "Test setup: the body should stand on the floor.");
            float face = rig.Position.z + rig.FrontExtent + WallDistance;
            rig.Box("Wall", new Vector3(0f, 2f, face + 0.5f), new Vector3(10f, 4f, 1f));
            Physics.SyncTransforms();
            yield return rig.Run(rig.SecondsToWalk(WallDistance, 1f), _ => CharacterTestRig.Move(Vector3.forward));
            result(rig.Position.z + rig.FrontExtent, face);
        }

        [UnityTest]
        public IEnumerator ABox_WalksUpToAWall_AndStopsWithItsFrontAgainstIt()
        {
            yield return OnTheFloor();
            BoxCollider box = rig.UseBody<BoxCollider>(ABox);
            Assert.That(rig.FrontExtent, Is.EqualTo(box.size.z / 2f).Within(1e-4f), "A box reaches half its depth ahead.");
            Assert.That(rig.SideExtent, Is.EqualTo(box.size.x / 2f).Within(1e-4f), "A box reaches half its width to the sides.");
            Assert.That(rig.Movement.Body.TurnsWithFacing, Is.True, "A box isn't round: it turns with the character.");
            float front = 0f, face = 0f;
            yield return WalkIntoAWall((f, w) => { front = f; face = w; });
            Assert.That(front, Is.EqualTo(face).Within(0.05f), "The box should stop with its front against the wall.");
            Assert.That(rig.BodyFits, Is.True, "The box shouldn't end up inside the wall.");
        }

        [UnityTest]
        public IEnumerator ASphere_WalksUpToAWall_AndStopsWithItsFrontAgainstIt()
        {
            yield return OnTheFloor();
            SphereCollider sphere = rig.UseBody<SphereCollider>(ASphere);
            Assert.That(rig.FrontExtent, Is.EqualTo(sphere.radius).Within(1e-4f));
            Assert.That(rig.Movement.Body.TurnsWithFacing, Is.False, "A sphere on the feet is round: turning doesn't move it.");
            Assert.That(rig.StandingHeight, Is.EqualTo(2f * sphere.radius).Within(1e-4f), "A sphere stands as tall as it's across.");
            float front = 0f, face = 0f;
            yield return WalkIntoAWall((f, w) => { front = f; face = w; });
            Assert.That(front, Is.EqualTo(face).Within(0.05f), "The sphere should stop with its front against the wall.");
        }

        [UnityTest]
        public IEnumerator ACapsuleCentredAheadOfTheFeet_StopsWithItsFrontAgainstAWall()
        {
            yield return OnTheFloor();
            CapsuleCollider capsule = rig.UseBody<CapsuleCollider>(ACapsuleAhead);
            Assert.That(rig.FrontExtent, Is.EqualTo(capsule.center.z + capsule.radius).Within(1e-4f), "It reaches its radius beyond its middle, ahead.");
            Assert.That(rig.BackExtent, Is.EqualTo(capsule.radius - capsule.center.z).Within(1e-4f), "And its radius short of it, behind.");
            Assert.That(rig.Movement.Body.TurnsWithFacing, Is.True, "Off the feet, it swings round as the character turns.");
            float front = 0f, face = 0f;
            yield return WalkIntoAWall((f, w) => { front = f; face = w; });
            Assert.That(front, Is.EqualTo(face).Within(0.05f), "Its front, ahead of the feet, should stop against the wall.");
        }

        [UnityTest]
        public IEnumerator ABox_Crouched_IsLoweredFromTheTop()
        {
            yield return OnTheFloor();
            BoxCollider box = rig.UseBody<BoxCollider>(ABox);
            float standing = rig.StandingHeight;
            Assert.That(standing, Is.EqualTo(box.size.y).Within(1e-4f));
            yield return rig.Run(0.3f, step => step == 0 ? CharacterTestRig.Crouch(0) : CharacterTestRig.Idle());
            Assert.That(rig.Movement.Posture.Kind, Is.EqualTo(BodyPostureKind.Low));
            Assert.That(rig.CurrentHeight, Is.EqualTo(rig.CrouchHeight * standing).Within(0.01f), "Crouched, the box should be the crouch height tall.");
            Assert.That(box.center.y - box.size.y / 2f, Is.EqualTo(0f).Within(1e-4f), "Its bottom should stay at the feet.");
            yield return rig.Run(0.3f, step => step == 0 ? CharacterTestRig.Crouch(0) : CharacterTestRig.Idle());
            Assert.That(rig.CurrentHeight, Is.EqualTo(standing).Within(0.01f), "Pressing Crouch again, it should stand back up.");
        }

        [UnityTest]
        public IEnumerator TheCharacterPrefab_HasABodyDefinition_ThatsItsCollider()
        {
            yield return OnTheFloor();
            BodyDefinition definition = rig.Movement.BodyDefinition;
            Assert.That(definition, Is.Not.Null, "The character should be shaped from a Body Definition.");
            Assert.That(BodyShape.TryRead(CharacterTestRig.PrefabCapsule(), out BodyShape prefabShape), Is.True);
            Assert.That(definition.Shape.Equals(prefabShape), Is.True,
                $"The prefab's collider should be its body as defined ({definition.Shape}), not {prefabShape}.");
            // Its body is the definition's, unless it takes its model's (see CharacterModelHost's Collider Fit).
            BodyShape expected = rig.Models.BodyFor(rig.Models.ModelSource, rig.Movement) ?? definition.Shape;
            Assert.That(rig.Movement.AuthoredBody.Equals(expected), Is.True, $"Its body should be {expected}, not {rig.Movement.AuthoredBody}.");
        }

        [UnityTest]
        public IEnumerator ALyingThickness_LaysTheBodyDownThinner([Values("box", "capsule")] string kind)
        {
            yield return OnTheFloor();
            if (kind == "box") { rig.UseBody<BoxCollider>(ABox); }
            BodyShape shape = rig.Movement.AuthoredBody;
            float length = rig.StandingHeight, wide = 2f * rig.SideExtent;
            const float thickness = 0.3f;
            shape.lyingThickness = thickness;
            Assert.That(rig.Movement.SetBody(shape), Is.True);
            Assert.That(rig.StandingHeight, Is.EqualTo(length).Within(1e-4f), "Standing, a lying thickness changes nothing.");
            Assume.That(rig.Movement.SetPosture(BodyPosture.Lying, rig.Body.position), Is.True, "Test setup: there's room to lie down.");
            Assert.That(rig.CurrentHeight, Is.EqualTo(thickness).Within(1e-4f), "Lying, the body should be as thick as its lying thickness.");
            Assert.That(rig.FrontExtent + rig.BackExtent, Is.EqualTo(length).Within(1e-4f), "And as long as it stood tall.");
            Assert.That(rig.Movement.Body.Collider.bounds.min.y - rig.Body.position.y, Is.EqualTo(0f).Within(0.01f), "Resting on its feet.");
            if (kind == "box") { Assert.That(2f * rig.SideExtent, Is.EqualTo(wide).Within(1e-4f), "A box stays as wide."); }
        }

        [UnityTest]
        public IEnumerator SetBody_OfAnotherKind_ReplacesTheCollider_OnceThereIsRoom()
        {
            // A ceiling just above the character's head, from z = -2 to z = 2.
            rig.Floor();
            yield return rig.SpawnSettled(Vector3.zero, settleSeconds: 0f);
            float standing = rig.StandingHeight;
            rig.Box("Low Ceiling", new Vector3(0f, standing + 0.1f + 0.5f, 0f), new Vector3(10f, 1f, 4f));
            Physics.SyncTransforms();
            BodyShape taller = BodyShape.Box(new Vector3(0.8f, standing + 0.5f, 0.6f));
            Assert.That(rig.Movement.SetBody(taller), Is.False, "Under the ceiling, a taller body shouldn't fit.");
            Assert.That(rig.Movement.Body.Collider, Is.InstanceOf<CapsuleCollider>(), "Nothing should change meanwhile.");

            yield return rig.Run(rig.SecondsToWalk(3f + rig.FrontExtent, 1f), _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(rig.Movement.SetBody(taller), Is.True, "Out in the open, it should fit.");
            Assert.That(rig.Movement.Body.Collider, Is.InstanceOf<BoxCollider>(), "A box body is a box collider.");
            Assert.That(rig.StandingHeight, Is.EqualTo(taller.size.y).Within(1e-4f));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Grounded, Is.True);
            Assert.That(rig.BodyFits, Is.True);
        }

        [UnityTest]
        public IEnumerator LyingDown_LaysTheBodyAlongTheFacing_OnItsFeet([Values("box", "capsule")] string kind)
        {
            yield return OnTheFloor();
            float tall, deep;
            if (kind == "box") { BoxCollider box = rig.UseBody<BoxCollider>(ABox); tall = box.size.y; deep = box.size.z; }
            else
            {
                tall = rig.StandingHeight;
                // As deep as it was standing, or its lying thickness, if it has one (a body fitted to a model has).
                float thickness = rig.Movement.AuthoredBody.lyingThickness * rig.Movement.Scale.y;
                deep = thickness > 0f ? Mathf.Min(thickness, 2f * rig.FrontExtent) : 2f * rig.FrontExtent;
            }
            // A capsule laid down thinner (its lying thickness) is that much narrower too: it's round.
            float wide = kind == "box" ? 2f * rig.SideExtent : Mathf.Min(2f * rig.SideExtent, deep);
            Assume.That(rig.Movement.SetPosture(BodyPosture.Lying, rig.Body.position), Is.True, "Test setup: there's room to lie down.");
            Physics.SyncTransforms();
            Assert.That(rig.CurrentHeight, Is.EqualTo(deep).Within(1e-4f), "Lying, the body should be as tall as it was deep standing.");
            Assert.That(rig.FrontExtent + rig.BackExtent, Is.EqualTo(tall).Within(1e-4f), "And as long as it was tall.");
            Assert.That(2f * rig.SideExtent, Is.EqualTo(wide).Within(1e-4f), "And as wide.");
            Assert.That(rig.Movement.Body.Collider.bounds.min.y - rig.Body.position.y, Is.EqualTo(0f).Within(0.01f),
                "It should rest on the feet.");
            Assert.That(rig.Movement.Body.TurnsWithFacing, Is.True, "Lying, it turns with the character.");
        }
    }
}
