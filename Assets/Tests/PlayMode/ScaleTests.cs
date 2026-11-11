using System.Collections;
using System.Linq;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of resizing the character at runtime with ACCharacterController.SetScale: only the model parent is scaled,
    /// and collision, movement, the model's measurements and mass follow, for uniform and non-uniform scales alike.
    /// Growing waits for room.
    /// </summary>
    public class ScaleTests
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

        private CapsuleCollider Capsule => rig.Character.GetComponent<CapsuleCollider>();
        private Transform ModelParent => rig.Models.ModelParent;

        /// <summary>Spawns the character on a floor at the origin and lets it settle.</summary>
        private IEnumerator Spawn()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.Run(0.2f, _ => CharacterTestRig.Idle());
        }

        [UnityTest]
        public IEnumerator SetScale_ScalesTheModelParent_NotTheCharacter_AndResizesItsCollision()
        {
            yield return Spawn();
            float radius = Capsule.radius, height = Capsule.height, mass = rig.Body.mass;
            Vector3 center = Capsule.center;

            rig.Controller.SetScale(new Vector3(2f, 2f, 2f), immediately: true);

            Assert.That(rig.Character.transform.localScale, Is.EqualTo(Vector3.one), "The character's own transform shouldn't be scaled.");
            Assert.That(ModelParent.localScale, Is.EqualTo(new Vector3(2f, 2f, 2f)));
            Assert.That(rig.Controller.Scale, Is.EqualTo(new Vector3(2f, 2f, 2f)));
            Assert.That(Capsule.height, Is.EqualTo(height * 2f).Within(1e-4f));
            Assert.That(Capsule.radius, Is.EqualTo(radius * 2f).Within(1e-4f));
            Assert.That(Capsule.center, Is.EqualTo(center * 2f));
            Assert.That(rig.Body.mass, Is.EqualTo(mass * 8f).Within(1e-3f), "Mass should grow with volume.");

            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Grounded, Is.True);
            Assert.That(rig.Position.y, Is.EqualTo(0f).Within(0.03f), "The bigger character should still stand on the floor.");
        }

        [UnityTest]
        public IEnumerator ScaleChanges_TakeTheirTime()
        {
            yield return Spawn();
            rig.Controller.SetScale(new Vector3(2f, 2f, 2f));
            Assert.That(rig.Controller.IsResizing, Is.True);
            Assert.That(rig.Controller.Scale, Is.EqualTo(Vector3.one), "Nothing changes before the next physics step.");

            yield return rig.Run(2, _ => CharacterTestRig.Idle());
            Assert.That(rig.Controller.Scale.y, Is.InRange(1.01f, 1.99f), "Partway through, the character should be between sizes.");

            yield return rig.Run(1f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Controller.IsResizing, Is.False);
            Assert.That(rig.Controller.Scale, Is.EqualTo(new Vector3(2f, 2f, 2f)));
        }

        [UnityTest]
        public IEnumerator ABiggerCharacter_ClimbsTallerSteps()
        {
            // 0.5 m tall (too tall at 1x, whose limit is 0.35 m), from z = 2, and long enough not to walk off the end.
            rig.Box("Step", new Vector3(0f, 0.25f, 22f), new Vector3(10f, 0.5f, 40f));
            yield return Spawn();
            yield return rig.Run(1.5f, _ => CharacterTestRig.Move(Vector3.forward * 0.3f));
            Assert.That(rig.Position.y, Is.LessThan(0.1f), "Test setup: at normal size the step is too tall to climb.");

            // Back off first: grown where it stands, against the step, the bigger capsule wouldn't fit.
            yield return rig.Run(1f, _ => CharacterTestRig.Move(Vector3.back * 0.5f));
            rig.Controller.SetScale(new Vector3(2f, 2f, 2f), immediately: true);
            Assert.That(rig.Controller.Scale, Is.EqualTo(new Vector3(2f, 2f, 2f)), "Test setup: there should be room to grow here.");
            yield return rig.Run(3f, _ => CharacterTestRig.Move(Vector3.forward * 0.3f));
            Assert.That(rig.Position.y, Is.EqualTo(0.5f).Within(0.05f), "At twice the size the character should step up onto it.");
        }

        [UnityTest]
        public IEnumerator AWiderCharacter_StopsFurtherFromWalls()
        {
            rig.Box("Wall", new Vector3(0f, 2f, 4.5f), new Vector3(10f, 4f, 1f)); // Face at z = 4.
            yield return Spawn();
            rig.Controller.SetScale(new Vector3(2f, 1f, 1f), immediately: true); // Twice as wide: radius 1 m.
            yield return rig.Run(2f, _ => CharacterTestRig.Move(Vector3.forward));

            Assert.That(rig.Position.z, Is.EqualTo(4f - Capsule.radius).Within(0.05f), "The wider capsule should touch the wall.");
            Assert.That(Capsule.radius, Is.EqualTo(1f).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator GrowingUnderALowCeiling_WaitsForRoom()
        {
            GameObject ceiling = rig.Box("Ceiling", new Vector3(0f, 3.1f, 0f), new Vector3(10f, 1f, 10f)); // Underside at 2.6 m.
            yield return Spawn();
            rig.Controller.SetScale(new Vector3(2f, 2f, 2f)); // 4 m tall: doesn't fit.
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());

            Assert.That(rig.Controller.IsResizing, Is.True, "The character should still be waiting to finish growing.");
            Assert.That(Capsule.height, Is.LessThanOrEqualTo(2.6f), "It shouldn't grow into the ceiling.");
            Assert.That(rig.Position.y, Is.EqualTo(0f).Within(0.03f), "It shouldn't be pushed through the floor.");
            Assert.That(rig.Grounded, Is.True);

            Object.Destroy(ceiling);
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Controller.IsResizing, Is.False);
            Assert.That(rig.Controller.Scale, Is.EqualTo(new Vector3(2f, 2f, 2f)), "Once there's room it should finish growing.");
        }

        [UnityTest]
        public IEnumerator NonUniformScales_KeepTheCharacterMovingAndColliding(
            [ValueSource(nameof(NonUniformScales))] Vector3 scale)
        {
            rig.Box("Wall", new Vector3(0f, 3f, 5.5f), new Vector3(10f, 6f, 1f)); // Face at z = 5.
            yield return Spawn();
            float baseRadius = Capsule.radius, baseHeight = Capsule.height;
            rig.Controller.SetScale(scale, immediately: true);

            Assert.That(ModelParent.localScale, Is.EqualTo(scale));
            Assert.That(Capsule.height, Is.EqualTo(baseHeight * scale.y).Within(1e-4f));
            Assert.That(Capsule.radius, Is.EqualTo(Mathf.Min(baseRadius * Mathf.Max(scale.x, scale.z), baseHeight * scale.y / 2f)).Within(1e-4f));

            yield return rig.Run(3f, _ => CharacterTestRig.Move(Vector3.forward));
            Vector3 position = rig.Position;
            Assert.That(float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z), Is.False);
            Assert.That(rig.Grounded, Is.True);
            Assert.That(position.y, Is.EqualTo(0f).Within(0.03f), "It should stay on the floor.");
            Assert.That(position.z, Is.EqualTo(5f - Capsule.radius).Within(0.05f), "It should walk up to the wall and stop there.");
        }

        private static readonly Vector3[] NonUniformScales =
        {
            new(2f, 0.5f, 1f), // Wide and flat: the radius is capped at half the height
            new(0.5f, 2f, 0.5f), // Tall and thin
            new(1f, 1f, 3f), // Long
        };

        [UnityTest]
        public IEnumerator Movement_ScalesNaturally_WithHeight()
        {
            yield return Spawn();

            // Running speed at 1x and at 4x the height.
            float Speed()
            {
                Vector3 v = rig.Movement.lastFrameMovementData.finalVelocity;
                return new Vector2(v.x, v.z).magnitude;
            }
            yield return rig.Run(1f, _ => CharacterTestRig.Move(Vector3.forward));
            float speed = Speed();
            rig.Controller.SetScale(new Vector3(4f, 4f, 4f), immediately: true);
            yield return rig.Run(1f, _ => CharacterTestRig.Move(Vector3.forward));
            Assert.That(Speed(), Is.EqualTo(speed * 2f).Within(speed * 0.05f), "At 4x the height, speed should double (the square root).");

            // Jump height at 4x the height should be about 4x.
            float jumpHeight = 0f;
            rig.Controller.SetScale(Vector3.one, immediately: true);
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());
            float floor = rig.Position.y;
            yield return rig.Run(3f, i => CharacterTestRig.Jump(i), _ => jumpHeight = Mathf.Max(jumpHeight, rig.Position.y - floor));
            float bigJumpHeight = 0f;
            rig.Controller.SetScale(new Vector3(4f, 4f, 4f), immediately: true);
            yield return rig.Run(3f, _ => CharacterTestRig.Idle());
            yield return rig.Run(6f, i => CharacterTestRig.Jump(i), _ => bigJumpHeight = Mathf.Max(bigJumpHeight, rig.Position.y - floor));
            Assert.That(jumpHeight, Is.GreaterThan(0.3f), "Test setup: the character should jump.");
            Assert.That(bigJumpHeight / jumpHeight, Is.EqualTo(4f).Within(0.6f), "Jump height should scale with height.");
        }

        [UnityTest]
        public IEnumerator TheModelsMeasurements_FollowTheScale()
        {
            yield return Spawn();
            yield return rig.WaitForModel();
            CharacterModelHost models = rig.Models;
            float eyes = models.EyeHeight, height = models.ModelHeightMeters;
            Transform eyePoint = rig.Character.transform.Find("Eye Point");

            rig.Controller.SetScale(new Vector3(1f, 2f, 1f), immediately: true);
            Assert.That(models.EyeHeight, Is.EqualTo(eyes * 2f).Within(1e-3f));
            Assert.That(models.ModelHeightMeters, Is.EqualTo(height * 2f).Within(1e-3f));
            Assert.That(eyePoint.localPosition.y, Is.EqualTo(models.EyeHeight).Within(1e-4f), "The eye point should follow.");
            Assert.That(CharacterModelSource.MeasureHeight(models.Model), Is.EqualTo(height * 2f).Within(0.02f),
                "The model itself should be twice as tall.");

            // A model loaded while the character is scaled is measured at its size too.
            yield return CharacterTestRig.WaitFor(models.LoadModelAsync(models.ModelSource));
            Assert.That(models.EyeHeight, Is.EqualTo(eyes * 2f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator ThePlaceholder_ScalesWithTheCharacter()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            Transform placeholder = rig.Character.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Model Placeholder");
            Assert.That(placeholder, Is.Not.Null);
            float height = placeholder.lossyScale.y;
            // The floor made this frame only reaches physics queries (and so the room check) after a physics step.
            yield return new WaitForFixedUpdate();
            rig.Controller.SetScale(new Vector3(1f, 3f, 1f), immediately: true);
            Assert.That(placeholder.lossyScale.y, Is.EqualTo(height * 3f).Within(1e-3f));
        }

        // --- Invalid scales and a scaled root ---

        [UnityTest]
        public IEnumerator NegativeOrZeroScales_AreRefused([ValueSource(nameof(InvalidScales))] Vector3 invalid)
        {
            yield return Spawn();
            rig.Controller.SetScale(new Vector3(2f, 2f, 2f), immediately: true);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("must be positive and finite"));
            bool accepted = rig.Controller.SetScale(invalid, immediately: true);

            Assert.That(accepted, Is.False);
            Assert.That(rig.Controller.Scale, Is.EqualTo(new Vector3(2f, 2f, 2f)), "The scale shouldn't change.");
            Assert.That(rig.Controller.TargetScale, Is.EqualTo(new Vector3(2f, 2f, 2f)));
            Assert.That(ModelParent.localScale, Is.EqualTo(new Vector3(2f, 2f, 2f)));
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Controller.Scale, Is.EqualTo(new Vector3(2f, 2f, 2f)));
        }

        private static readonly Vector3[] InvalidScales =
        {
            new(-1f, 1f, 1f),
            new(1f, -0.5f, 1f),
            new(-2f, -2f, -2f),
            new(1f, 0f, 1f),
            new(float.NaN, 1f, 1f),
        };

        [UnityTest]
        public IEnumerator TinyScales_AreRaisedToTheMinimum()
        {
            yield return Spawn();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("smaller than the minimum"));
            Assert.That(rig.Controller.SetScale(new Vector3(0.001f, 1f, 1f), immediately: true), Is.True);
            Assert.That(rig.Controller.Scale.x, Is.EqualTo(0.05f).Within(1e-5f));
        }

        [UnityTest]
        public IEnumerator AScaledRootTransform_IsReportedAsAnError_Once()
        {
            yield return Spawn();
            rig.Character.transform.localScale = new Vector3(2f, 2f, 2f);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("has its own transform scaled"));
            yield return rig.Run(0.2f, _ => CharacterTestRig.Idle());
            LogAssert.NoUnexpectedReceived(); // Reported once, not every physics step.

            // Put right and scaled again: reported again.
            rig.Character.transform.localScale = Vector3.one;
            yield return rig.Run(2, _ => CharacterTestRig.Idle());
            rig.Character.transform.localScale = new Vector3(1f, 3f, 1f);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("has its own transform scaled"));
            yield return rig.Run(2, _ => CharacterTestRig.Idle());
            rig.Character.transform.localScale = Vector3.one;
        }

        // --- Fractional scales ---

        private static readonly float[] FractionalSizes = { 0.5f, 0.25f };

        [UnityTest]
        public IEnumerator AtFractionalSizes_TheCharacterStandsOnTheFloor([ValueSource(nameof(FractionalSizes))] float size)
        {
            yield return Spawn();
            rig.Controller.SetScale(Vector3.one * size, immediately: true);
            Assert.That(Capsule.height, Is.EqualTo(2f * size).Within(1e-4f));
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());

            Assert.That(rig.Grounded, Is.True);
            Assert.That(rig.Position.y, Is.EqualTo(0f).Within(0.005f), "It shouldn't hover or sink.");
            Assert.That(new Vector2(rig.Position.x, rig.Position.z).magnitude, Is.LessThan(0.01f), "It shouldn't drift.");
        }

        [UnityTest]
        public IEnumerator AtFractionalSizes_TheCharacterWalksUpToWallsAndStops([ValueSource(nameof(FractionalSizes))] float size)
        {
            rig.Box("Wall", new Vector3(0f, 1f, 3.5f), new Vector3(10f, 2f, 1f)); // Face at z = 3.
            yield return Spawn();
            rig.Controller.SetScale(Vector3.one * size, immediately: true);
            yield return rig.Run(3f, _ => CharacterTestRig.Move(Vector3.forward));

            Assert.That(rig.Position.z, Is.EqualTo(3f - Capsule.radius).Within(0.03f), "It should stop against the wall.");
            Assert.That(rig.Grounded, Is.True);
            Assert.That(rig.Position.y, Is.EqualTo(0f).Within(0.005f));
        }

        [UnityTest]
        public IEnumerator AtFractionalSizes_TheCharacterClimbsStepsInProportion([ValueSource(nameof(FractionalSizes))] float size)
        {
            // A step 0.3 m tall at full size (under the 0.35 m step height), scaled down with the character.
            float step = 0.3f * size;
            rig.Box("Step", new Vector3(0f, step / 2f, 22f), new Vector3(10f, step, 40f)); // From z = 2.
            yield return Spawn();
            rig.Controller.SetScale(Vector3.one * size, immediately: true);
            yield return rig.Run(3f, _ => CharacterTestRig.Move(Vector3.forward * 0.3f));
            Assert.That(rig.Position.y, Is.EqualTo(step).Within(0.01f), "It should climb a step scaled with it.");
            Assert.That(rig.Grounded, Is.True);
        }

        [UnityTest]
        public IEnumerator AtFractionalSizes_StepsTooTallForItsSize_StopIt([ValueSource(nameof(FractionalSizes))] float size)
        {
            float step = 0.45f * size; // Over the step height (0.35 m at full size) at this size.
            rig.Box("Step", new Vector3(0f, step / 2f, 22f), new Vector3(10f, step, 40f));
            yield return Spawn();
            rig.Controller.SetScale(Vector3.one * size, immediately: true);
            yield return rig.Run(2f, _ => CharacterTestRig.Move(Vector3.forward * 0.3f));
            Assert.That(rig.Position.y, Is.EqualTo(0f).Within(0.005f), "It shouldn't climb a step too tall for it.");
            Assert.That(rig.Position.z, Is.LessThan(2f));
        }

        [UnityTest]
        public IEnumerator AtFractionalSizes_TheCharacterFitsThroughGapsTooLowForFullSize([ValueSource(nameof(FractionalSizes))] float size)
        {
            // A ceiling 1.2 m up from z = 3 to z = 6: too low for the 2 m character, not for a smaller one.
            rig.Box("Low Ceiling", new Vector3(0f, 1.7f, 4.5f), new Vector3(10f, 1f, 3f));
            yield return Spawn();
            rig.Controller.SetScale(Vector3.one * size, immediately: true);
            yield return rig.Run(3f, _ => CharacterTestRig.Move(Vector3.forward * 0.5f));
            Assert.That(rig.Position.z, Is.GreaterThan(6.5f), "It should walk under the low ceiling.");
            Assert.That(rig.Position.y, Is.EqualTo(0f).Within(0.005f));

            // Back under the ceiling, it can't grow back to full size until it comes out.
            rig.Box("Back Stop", new Vector3(0f, 1f, 3.5f), new Vector3(10f, 2f, 1f)); // A wall to back into, at z = 4.
            Physics.SyncTransforms();
            yield return rig.Run(2f, _ => CharacterTestRig.Move(Vector3.back * 0.5f));
            Assert.That(rig.Position.z, Is.InRange(4f, 6f), "Test setup: it should be under the ceiling.");
            rig.Controller.SetScale(Vector3.one);
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Controller.IsResizing, Is.True, "It should wait to grow.");
            Assert.That(Capsule.height, Is.LessThanOrEqualTo(1.2f), "It shouldn't grow into the ceiling.");
            Assert.That(rig.Position.y, Is.EqualTo(0f).Within(0.005f), "It shouldn't be pushed through the floor.");
        }

        [UnityTest]
        public IEnumerator AtFractionalSizes_JumpingAndFallingWorkInProportion([ValueSource(nameof(FractionalSizes))] float size)
        {
            rig.Box("Ledge", new Vector3(0f, -0.5f, -20f), new Vector3(10f, 1f, 40f)); // Top at y = 0, ending at z = 0.
            rig.Floor(-1f);
            rig.SpawnCharacter(new Vector3(0f, 0.05f, -5f));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            float fullJump = 0f;
            yield return rig.Run(2f, i => CharacterTestRig.Jump(i), _ => fullJump = Mathf.Max(fullJump, rig.Position.y));
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());

            rig.Controller.SetScale(Vector3.one * size, immediately: true);
            float jump = 0f;
            yield return rig.Run(2f, i => CharacterTestRig.Jump(i), _ => jump = Mathf.Max(jump, rig.Position.y));
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());
            Assert.That(jump / fullJump, Is.EqualTo(size).Within(size * 0.2f), "Jump height should scale with height.");
            Assert.That(rig.Grounded, Is.True, "It should land again.");

            // Walk off the ledge and land on the floor below.
            yield return rig.Run(5f, _ => CharacterTestRig.Move(Vector3.forward * 0.5f));
            Assert.That(rig.Grounded, Is.True);
            Assert.That(rig.Position.y, Is.EqualTo(-1f).Within(0.005f), "It should land on the floor below.");
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator EditingScaleInTheInspector_ResizesTheCharacter()
        {
            yield return Spawn();
            var inspector = new UnityEditor.SerializedObject(rig.Controller);
            inspector.FindProperty("scale").vector3Value = new Vector3(1.5f, 0.75f, 1.5f);
            inspector.ApplyModifiedProperties(); // Calls OnValidate, as editing the field in the inspector does.

            Assert.That(rig.Controller.TargetScale, Is.EqualTo(new Vector3(1.5f, 0.75f, 1.5f)));
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Controller.Scale, Is.EqualTo(new Vector3(1.5f, 0.75f, 1.5f)));
            Assert.That(ModelParent.localScale, Is.EqualTo(new Vector3(1.5f, 0.75f, 1.5f)));
        }
#endif
    }
}
