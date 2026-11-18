using System.Collections;
using System.Collections.Generic;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of bodies of the other primitive shapes (see BodyShape and PrimitiveBody), in place of the character's upright
    /// capsule: a box, a sphere, and a capsule centred off the feet each walk up to a wall and stop with their front
    /// against it, stand on the floor, and hold postures: lowered from the top (crouched), and laid down along the facing,
    /// resting on the feet (lying). Bodies that aren't round about the feet turn with the character.
    /// </summary>
    [Category(TestArea.Movement)]
    public class BodyShapeTests : CharacterRigFixture
    {
        private const float WallDistance = 2f;

        private static BodyShape ABox => BodyShape.Box(new Vector3(0.8f, 1.8f, 0.6f));

        private static BodyShape ASphere => BodyShape.Sphere(0.45f);

        // A capsule whose middle is ahead of the feet (a body leaning forward, or one carried in front of its legs).
        private static BodyShape ACapsuleAhead
        {
            get
            {
                BodyShape capsule = BodyShape.Capsule(0.4f, 1.8f);
                capsule.center = new Vector3(0f, 0.9f, 0.3f);
                return capsule;
            }
        }

        /// <summary>Gives the character the given body in place of its own (after spawning it), returning it.</summary>
        private BodyShape UseBody(BodyShape shape)
        {
            Assert.That(rig.Movement.SetBody(shape, checkRoom: false), Is.True, $"Test setup: the character should take the body {shape}.");
            Physics.SyncTransforms();
            return shape;
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
            BodyShape box = UseBody(ABox);
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
            BodyShape sphere = UseBody(ASphere);
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
            BodyShape capsule = UseBody(ACapsuleAhead);
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
            BodyShape box = UseBody(ABox);
            float standing = rig.StandingHeight;
            Assert.That(standing, Is.EqualTo(box.size.y).Within(1e-4f));
            yield return rig.Run(0.3f, step => step == 0 ? CharacterTestRig.Crouch() : CharacterTestRig.Idle());
            Assert.That(rig.Movement.Posture.Kind, Is.EqualTo(BodyPostureKind.Low));
            Assert.That(rig.CurrentHeight, Is.EqualTo(rig.CrouchHeight * standing).Within(0.01f), "Crouched, the box should be the crouch height tall.");
            Assert.That(rig.BodyNow.center.y - rig.BodyNow.size.y / 2f, Is.EqualTo(0f).Within(1e-4f), "Its bottom should stay at the feet.");
            yield return rig.Run(0.3f, step => step == 0 ? CharacterTestRig.Crouch() : CharacterTestRig.Idle());
            Assert.That(rig.CurrentHeight, Is.EqualTo(standing).Within(0.01f), "Pressing Crouch again, it should stand back up.");
        }

        private const string FoxPrefabPath = "Assets/Prefabs/Fox.prefab";

        /// <summary>
        /// Without a collider, a character moves exactly as it does with one (the movement doesn't need it: see
        /// CharacterBody): the tested character and the fox (a box body), up a flight of stairs, along a wall it steers
        /// into, and jumping. (The golden paths hold the tested character to it over all of its movement.)
        /// </summary>
        [UnityTest]
        public IEnumerator WithoutACollider_ACharacterMovesExactlyAsWithOne([Values("Character", "Fox")] string character)
        {
            var paths = new List<Vector3>[2];
            for (int run = 0; run < 2; run++)
            {
                if (run > 0) { yield return ReplaceRig(); }
                GameObject prefab = character == "Fox" ? CharacterTestRig.LoadPrefab(FoxPrefabPath) : CharacterTestRig.CharacterPrefab();
                if (prefab == null) { Assert.Ignore($"There's no fox ('{FoxPrefabPath}'): generate it with Tools > Character Showcase > Generate Fox."); }
                rig.Floor(0f, 200f);
                yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f), prefab, settleSeconds: 0f);
                bool withCollider = run == 0;
                if (withCollider) { rig.AddBodyCollider(); }

                // Six steps up to a landing, with a wall along the landing's right.
                float rise = 0.6f * rig.StepHeight, tread = 4f / 3f * rise, top = 6f * rise, start = rig.FrontExtent + 1f;
                for (int i = 0; i < 5; i++)
                {
                    rig.Box($"Step {i + 1}", new Vector3(0f, rise * (i + 1) / 2f, start + i * tread + tread / 2f), new Vector3(10f, rise * (i + 1), tread));
                }
                float landing = start + 5f * tread;
                rig.Box("Landing", new Vector3(0f, top / 2f, landing + 15f), new Vector3(10f, top, 30f));
                rig.Box("Wall", new Vector3(rig.SideExtent + 1.5f, top + 1.5f, landing + 15f), new Vector3(1f, 3f, 30f));
                Physics.SyncTransforms();
                rig.Teleport(new Vector3(0f, 0.02f, 0f));

                float dt = Time.fixedDeltaTime;
                var path = new List<Vector3>();
                paths[run] = path;
                yield return rig.Run(6f, step =>
                {
                    float t = step * dt;
                    if (t < 2.5f) { return CharacterTestRig.Move(Vector3.forward); }
                    Vector3 alongTheWall = new Vector3(0.5f, 0f, 1f).normalized;
                    if (t >= 4f && t < 4.3f) { return CharacterTestRig.Jump(alongTheWall); }
                    return CharacterTestRig.Move(alongTheWall);
                }, _ => path.Add(rig.Position));
            }

            Assert.That(paths[1].Count, Is.EqualTo(paths[0].Count));
            for (int i = 0; i < paths[0].Count; i++)
            {
                Assert.That(Vector3.Distance(paths[1][i], paths[0][i]), Is.LessThan(1e-5f),
                    $"At step {i}, without a collider the character was at {paths[1][i]:F5}; with one, at {paths[0][i]:F5}.");
            }
        }

        [UnityTest]
        public IEnumerator TheCharacterPrefab_HasABodyDefinition_AndNoCollider()
        {
            yield return OnTheFloor();
            BodyDefinition definition = rig.Movement.BodyDefinition;
            Assert.That(definition, Is.Not.Null, "The character should be shaped from a Body Definition.");
            Assert.That(CharacterTestRig.CharacterPrefab().GetComponents<Collider>(), Is.Empty,
                "The prefab should have no collider of its own: its body is a shape, and its model's hitboxes meet the world.");
            Assert.That(rig.Movement.Body.Collider, Is.Null, "Nor should one be added for its body.");
            // Its body is the definition's, unless it takes its model's (see CharacterModelHost's Body Fit).
            BodyShape expected = rig.Models.BodyFor(rig.Models.ModelSource, rig.Movement) ?? definition.Shape;
            Assert.That(rig.Movement.AuthoredBody.Equals(expected), Is.True, $"Its body should be {expected}, not {rig.Movement.AuthoredBody}.");
        }

        [UnityTest]
        public IEnumerator ALyingThickness_LaysTheBodyDownThinner([Values("box", "capsule")] string kind)
        {
            yield return OnTheFloor();
            if (kind == "box") { UseBody(ABox); }
            BodyShape shape = rig.Movement.AuthoredBody;
            float length = rig.StandingHeight, wide = 2f * rig.SideExtent;
            const float thickness = 0.3f;
            shape.lyingThickness = thickness;
            Assert.That(rig.Movement.SetBody(shape), Is.True);
            Assert.That(rig.StandingHeight, Is.EqualTo(length).Within(1e-4f), "Standing, a lying thickness changes nothing.");
            Assume.That(rig.Movement.SetPosture(BodyPosture.Lying, rig.Body.position), Is.True, "Test setup: there's room to lie down.");
            Assert.That(rig.CurrentHeight, Is.EqualTo(thickness).Within(1e-4f), "Lying, the body should be as thick as its lying thickness.");
            Assert.That(rig.FrontExtent + rig.BackExtent, Is.EqualTo(length).Within(1e-4f), "And as long as it stood tall.");
            Assert.That(rig.BodyNow.center.y - 0.5f * rig.CurrentHeight, Is.EqualTo(0f).Within(0.01f), "Resting on its feet.");
            if (kind == "box") { Assert.That(2f * rig.SideExtent, Is.EqualTo(wide).Within(1e-4f), "A box stays as wide."); }
        }

        [UnityTest]
        public IEnumerator SetBody_OfAnotherKind_ReplacesTheBody_OnceThereIsRoom()
        {
            // A ceiling just above the character's head, from z = -2 to z = 2.
            rig.Floor();
            yield return rig.SpawnSettled(Vector3.zero, settleSeconds: 0f);
            float standing = rig.StandingHeight;
            rig.Box("Low Ceiling", new Vector3(0f, standing + 0.1f + 0.5f, 0f), new Vector3(10f, 1f, 4f));
            Physics.SyncTransforms();
            BodyShape taller = BodyShape.Box(new Vector3(0.8f, standing + 0.5f, 0.6f));
            Assert.That(rig.Movement.SetBody(taller), Is.False, "Under the ceiling, a taller body shouldn't fit.");
            Assert.That(rig.BodyNow.kind, Is.EqualTo(BodyShapeKind.Capsule), "Nothing should change meanwhile.");

            yield return rig.Run(rig.SecondsToWalk(3f + rig.FrontExtent, 1f), _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(rig.Movement.SetBody(taller), Is.True, "Out in the open, it should fit.");
            Assert.That(rig.BodyNow.kind, Is.EqualTo(BodyShapeKind.Box), "Its body should be a box now.");
            Assert.That(rig.Movement.Body.Collider, Is.Null, "Without a collider, none is added for it.");
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
            if (kind == "box") { BodyShape box = UseBody(ABox); tall = box.size.y; deep = box.size.z; }
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
            Assert.That(rig.BodyNow.center.y - 0.5f * rig.CurrentHeight, Is.EqualTo(0f).Within(0.01f), "It should rest on the feet.");
            Assert.That(rig.Movement.Body.TurnsWithFacing, Is.True, "Lying, it turns with the character.");
        }
    }
}
