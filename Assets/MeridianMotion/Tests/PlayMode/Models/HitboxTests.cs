using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MeridianMotion.Characters;
using MeridianMotion.Levels;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace MeridianMotion.Tests.Models
{
    /// <summary>
    /// Tests of the character's hitboxes (see CharacterHitboxes), for the tested character and the fox: its model brings
    /// them (the character itself has no collider), on the CharacterHitbox layer, moving with the character's Rigidbody;
    /// they cover its model, posed as it's animated, without reaching far beyond it; detecting (as they do unless set
    /// otherwise), a ball dropped on the character is reported and passes through; colliding, it bounces off, while the
    /// character walks as freely as ever; and a timing gate's trigger still sees the character walk through it.
    /// </summary>
    [Category(TestArea.Models)]
    public class HitboxTests : CharacterRigFixture
    {

        // The hitboxes move with the animated model, and what meets them is reported by Unity's physics steps.
        protected override bool MovementOnly => false;

        /// <summary>The characters tested.</summary>
        public static IEnumerable<string> Characters() => new[] { "Character", "Fox" };

        private IEnumerator Spawn(string character) => rig.StandOnFloor(CharacterTestRig.PrefabFor(character));

        private CharacterHitboxes Hitboxes
        {
            get
            {
                CharacterHitboxes hitboxes = rig.Models.Hitboxes;
                Assert.That(hitboxes, Is.Not.Null, "The character's model should have hitboxes (run Tools > Meridian Motion > Prepare > Fit Hitboxes).");
                return hitboxes;
            }
        }

        [UnityTest]
        public IEnumerator TheModel_BringsHitboxes_OnTheirLayer_MovingWithTheCharacter([ValueSource(nameof(Characters))] string character)
        {
            yield return Spawn(character);
            Assert.That(rig.Character.GetComponents<Collider>(), Is.Empty, "The character itself should have no collider: its model's hitboxes meet the world.");
            CharacterHitboxes hitboxes = Hitboxes;
            Assert.That(hitboxes.Hitboxes.Count, Is.GreaterThanOrEqualTo(10), "A hitbox for each part of the body.");
            int layer = rig.Models.HitboxLayerIndex;
            Assert.That(layer, Is.GreaterThanOrEqualTo(0), $"The project should have the '{rig.Models.HitboxLayer}' layer its character names.");
            foreach (BoneHitbox hitbox in hitboxes.Hitboxes)
            {
                Assert.That(hitbox.gameObject.layer, Is.EqualTo(layer), $"'{hitbox.name}' should be on the {rig.Models.HitboxLayer} layer.");
                Assert.That(hitbox.Collider.isTrigger, Is.True, $"Detecting (as they do unless set otherwise), '{hitbox.name}' should be a trigger.");
                Assert.That(hitbox.Collider.attachedRigidbody, Is.SameAs(rig.Body), $"'{hitbox.name}' should move with the character's Rigidbody.");
            }
            int movementMask = rig.Movement.MovementLayerMask;
            Assert.That(movementMask & (1 << layer), Is.Zero, "The movement's layers should leave hitboxes out.");
        }

        /// <summary>
        /// A character's hitboxes go on the layer its CharacterModelSlot names (Hitbox Layer), changed at once when it's
        /// changed: so a project without the CharacterHitbox layer can put them on one of its own.
        /// </summary>
        [UnityTest]
        public IEnumerator TheHitboxes_GoOnTheLayerTheirCharacterNames()
        {
            yield return Spawn("Character");
            rig.Models.HitboxLayer = "Water";
            Assert.That(Hitboxes.Hitboxes.Select(h => h.gameObject.layer), Is.All.EqualTo(LayerMask.NameToLayer("Water")));
            rig.Models.HitboxLayer = CharacterHitboxes.LayerName;
            Assert.That(Hitboxes.Hitboxes.Select(h => h.gameObject.layer), Is.All.EqualTo(CharacterHitboxes.Layer));
        }

        [UnityTest]
        public IEnumerator TheHitboxes_CoverTheModel_AsItsAnimated_WithoutReachingFarBeyondIt([ValueSource(nameof(Characters))] string character)
        {
            yield return Spawn(character);
            CharacterHitboxes hitboxes = Hitboxes;
            // Walking, so they're checked against the model as it's posed, not only as it stands.
            yield return rig.Run(0.6f, _ => CharacterTestRig.Move(Vector3.forward));
            Physics.SyncTransforms();

            float height = rig.StandingHeight;
            // Near enough to count as covered: a few percent of the character's height (the capsules are fitted a little
            // inside the skin).
            float near = 0.04f * height;
            var bounds = new Bounds();
            bool hasBounds = false;
            int covered = 0, vertices = 0;
            var baked = new Mesh();
            try
            {
                foreach (SkinnedMeshRenderer skin in rig.Models.LoadedModel.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.BakeMesh(baked, true);
                    Matrix4x4 toWorld = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                    foreach (Vector3 vertex in baked.vertices)
                    {
                        Vector3 point = toWorld.MultiplyPoint3x4(vertex);
                        if (!hasBounds) { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                        bounds.Encapsulate(point);
                        vertices++;
                        if (hitboxes.Hitboxes.Any(h => Vector3.Distance(h.Collider.ClosestPoint(point), point) <= near)) { covered++; }
                    }
                }
            }
            finally { Object.Destroy(baked); }

            Assert.That(vertices, Is.GreaterThan(0), "Test setup: the model should have a skinned mesh.");
            float share = (float)covered / vertices;
            Assert.That(share, Is.GreaterThan(0.9f), $"The hitboxes should cover the model: only {share:P0} of its mesh is within {near:0.###} m of one.");
            bounds.Expand(2f * near);
            foreach (BoneHitbox hitbox in hitboxes.Hitboxes)
            {
                Bounds own = hitbox.Collider.bounds;
                Assert.That(bounds.Contains(own.min) && bounds.Contains(own.max), Is.True,
                    $"'{hitbox.name}' ({own}) reaches well beyond the model ({bounds}).");
            }
        }

        /// <summary>
        /// A ball dropped from above the character's broadest hitbox (its chest, or a fox's back: the middle of its body,
        /// not the gap between two parts), half a metre over the top of its body (top).
        /// </summary>
        private Rigidbody DropABall(out float top)
        {
            top = rig.StandingHeight;
            BoneHitbox broadest = rig.Models.Hitboxes.Hitboxes.OrderByDescending(h => h.Collider.radius * h.transform.lossyScale.x).First();
            Vector3 over = broadest.Collider.bounds.center;
            over.y = rig.Position.y + top + 0.5f;
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Ball";
            ball.transform.localScale = Vector3.one * 0.2f;
            ball.transform.position = over;
            var ballBody = ball.AddComponent<Rigidbody>();
            ballBody.mass = 0.2f;
            ballBody.interpolation = RigidbodyInterpolation.None;
            ballBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rig.Track(ball);
            return ballBody;
        }

        [UnityTest]
        public IEnumerator Detecting_ABallDroppedOnTheCharacter_IsReported_AndPassesThrough([ValueSource(nameof(Characters))] string character)
        {
            yield return Spawn(character);
            CharacterHitboxes hitboxes = Hitboxes;
            var hits = new List<(BoneHitbox hitbox, Collider other)>();
            hitboxes.HitStarted += (hitbox, other) => hits.Add((hitbox, other));
            Rigidbody ball = DropABall(out float top);
            Collider ballCollider = ball.GetComponent<Collider>();

            yield return rig.Run(1.5f, _ => CharacterTestRig.Idle());

            Assert.That(hits.Any(h => h.other == ballCollider), Is.True, "The ball should have been reported as it met the character.");
            Assert.That(hits.All(h => h.hitbox.Owner == hitboxes), Is.True);
            Assert.That(ball.position.y, Is.LessThan(0.5f * top), "Detecting, the ball should pass through the character to the floor.");
        }

        [UnityTest]
        public IEnumerator Colliding_ABallDroppedOnTheCharacter_BouncesOff_AndTheCharacterStillWalksFreely([ValueSource(nameof(Characters))] string character)
        {
            yield return Spawn(character);
            CharacterHitboxes hitboxes = Hitboxes;
            hitboxes.Response = HitboxResponse.Collide;
            Assert.That(hitboxes.Hitboxes.All(h => !h.Collider.isTrigger), Is.True, "Colliding, the hitboxes should be solid.");
            bool hit = false;
            hitboxes.HitStarted += (_, _) => hit = true;
            Rigidbody ball = DropABall(out float top);
            Vector3 start = ball.position;
            float lowestOver = float.PositiveInfinity, lowestFeet = float.PositiveInfinity;
            float footprint = Mathf.Max(rig.SideExtent, 0.5f * (rig.FrontExtent + rig.BackExtent));
            yield return rig.Run(1.5f, _ => CharacterTestRig.Idle(), _ =>
            {
                Vector3 offset = ball.position - start;
                if (new Vector2(offset.x, offset.z).magnitude < 0.25f * footprint) { lowestOver = Mathf.Min(lowestOver, ball.position.y); }
                lowestFeet = Mathf.Min(lowestFeet, rig.Position.y);
            });
            Assert.That(hit, Is.True, "The ball should have been reported as it met the character.");
            // A ball resting on a fox's back is inside its (head-high) body: it mustn't push the character into the floor.
            Assert.That(lowestFeet, Is.GreaterThan(-0.01f), "A loose ball resting on the character shouldn't push it down through the floor.");
            Assert.That(lowestOver, Is.GreaterThan(0.5f * top), "Colliding, the ball shouldn't pass through the character.");

            // Solid hitboxes are the character's own: its movement doesn't meet them.
            float speed = 0f;
            yield return rig.MeanSpeed(1f, _ => CharacterTestRig.Move(Vector3.forward), result => speed = result);
            Assert.That(speed, Is.GreaterThan(0.6f * rig.WalkSpeed), "With solid hitboxes, the character should still walk freely.");
        }

        /// <summary>
        /// A timing gate sees the fox walk through it, by its hitboxes (it has no collider either). Only the fox: the
        /// Character's hitboxes triggering gates is what every TimingGateTests test already rests on.
        /// </summary>
        [UnityTest]
        public IEnumerator ATimingGate_SeesTheCharacterWalkThroughIt()
        {
            yield return Spawn("Fox");
            GameObject gate = rig.Box("Start Gate", new Vector3(0f, 1f, 3f), new Vector3(4f, 2f, 0.2f));
            var timer = gate.AddComponent<CourseTimer>();
            var timing = gate.AddComponent<TimingGate>();
            timing.Timer = timer;
            timing.Role = TimingGateRole.Start;
            timer.SetRacer(rig.Character.transform);
            yield return rig.RunUntil(() => timer.IsRunning, _ => CharacterTestRig.Move(Vector3.forward), timeoutSeconds: 5f);
            Assert.That(timer.IsRunning, Is.True, "Walking through the start gate, the character's hitboxes should start the timer.");
        }
    }
}
