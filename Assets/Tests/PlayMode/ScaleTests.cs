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
        public IEnumerator GrowingUnderALowCeiling_WaitsForRoom()
        {
            // The ceiling's underside at 1.3 times the character's height: room to stand, not to double in size.
            float underside = 1.3f * CharacterTestRig.PrefabCapsule().height;
            GameObject ceiling = rig.Box("Ceiling", new Vector3(0f, underside + 0.5f, 0f), new Vector3(10f, 1f, 10f));
            yield return Spawn();
            rig.Controller.SetScale(new Vector3(2f, 2f, 2f)); // Twice as tall: doesn't fit.
            yield return rig.Run(1f, _ => CharacterTestRig.Idle());

            Assert.That(rig.Controller.IsResizing, Is.True, "The character should still be waiting to finish growing.");
            Assert.That(Capsule.height, Is.LessThanOrEqualTo(underside), "It shouldn't grow into the ceiling.");
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
            // A wall a couple of body lengths ahead, tall enough for any of the scales.
            CapsuleCollider prefab = CharacterTestRig.PrefabCapsule();
            float face = 3f * Mathf.Max(prefab.radius * 3f, prefab.height);
            rig.Box("Wall", new Vector3(0f, 3f * prefab.height, face + 0.5f), new Vector3(10f, 6f * prefab.height, 1f));
            yield return Spawn();
            float baseRadius = Capsule.radius, baseHeight = Capsule.height;
            rig.Controller.SetScale(scale, immediately: true);

            Assert.That(ModelParent.localScale, Is.EqualTo(scale));
            Assert.That(Capsule.height, Is.EqualTo(baseHeight * scale.y).Within(1e-4f));
            Assert.That(Capsule.radius, Is.EqualTo(Mathf.Min(baseRadius * Mathf.Max(scale.x, scale.z), baseHeight * scale.y / 2f)).Within(1e-4f));

            yield return rig.Run(rig.SecondsToWalk(face, 1f), _ => CharacterTestRig.Move(Vector3.forward));
            Vector3 position = rig.Position;
            Assert.That(float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z), Is.False);
            Assert.That(rig.Grounded, Is.True);
            Assert.That(position.y, Is.EqualTo(0f).Within(0.03f), "It should stay on the floor.");
            Assert.That(position.z, Is.EqualTo(face - Capsule.radius).Within(0.05f), "It should walk up to the wall and stop there (as far from it as it's wide).");
        }

        private static readonly Vector3[] NonUniformScales =
        {
            new(2f, 0.5f, 1f), // Wide and flat: the radius is capped at half the height
            new(0.5f, 2f, 0.5f), // Tall and thin
            new(1f, 1f, 3f), // Long
        };

        /// <summary>Running speed (horizontal) at the end of the last physics step.</summary>
        private float Speed()
        {
            Vector3 v = rig.Movement.lastFrameMovementData.finalVelocity;
            return new Vector2(v.x, v.z).magnitude;
        }

        [UnityTest]
        public IEnumerator NaturalSizeScaling_ScalesSpeedWithTheSquareRootOfHeight()
        {
            yield return Spawn();
            rig.Movement.SizeScaling = MovementSizeScaling.Natural;
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
            Assert.That(jumpHeight, Is.GreaterThan(rig.StepHeight), "Test setup: the character should jump.");
            Assert.That(bigJumpHeight / jumpHeight, Is.EqualTo(4f).Within(0.6f), "Jump height should scale with height.");
        }

        [UnityTest]
        public IEnumerator TheModelsMeasurements_FollowTheScale()
        {
            yield return Spawn();
            yield return rig.WaitForModel();
            CharacterModelHost models = rig.Models;
            float eyes = models.EyeHeight, height = models.ModelHeightMeters;
            Transform eyePoint = models.EyePoint;

            rig.Controller.SetScale(new Vector3(1f, 2f, 1f), immediately: true);
            Assert.That(models.EyeHeight, Is.EqualTo(eyes * 2f).Within(1e-3f));
            Assert.That(models.ModelHeightMeters, Is.EqualTo(height * 2f).Within(1e-3f));
            if (eyePoint != null) { Assert.That(eyePoint.localPosition.y, Is.EqualTo(models.EyeHeight).Within(1e-4f), "The eye point should follow."); }
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
            if (rig.Models.Placeholder == null) { Assert.Ignore("The character has no placeholder."); }
            Transform placeholder = rig.Models.Placeholder.transform;
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
            // Counted, rather than checked with NoUnexpectedReceived, so other messages (a model a little taller than
            // the character's collider, say) don't count.
            int reported = 0;
            void Count(string message, string stack, LogType type)
            {
                if (type == LogType.Error && message.Contains("has its own transform scaled")) { reported++; }
            }
            Application.logMessageReceived += Count;
            try
            {
                rig.Character.transform.localScale = new Vector3(2f, 2f, 2f);
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("has its own transform scaled"));
                yield return rig.Run(0.2f, _ => CharacterTestRig.Idle());
                Assert.That(reported, Is.EqualTo(1), "Reported once, not every physics step.");

                // Put right and scaled again: reported again.
                rig.Character.transform.localScale = Vector3.one;
                yield return rig.Run(2, _ => CharacterTestRig.Idle());
                rig.Character.transform.localScale = new Vector3(1f, 3f, 1f);
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("has its own transform scaled"));
                yield return rig.Run(2, _ => CharacterTestRig.Idle());
                Assert.That(reported, Is.EqualTo(2), "Scaled again, it should be reported again.");
                rig.Character.transform.localScale = Vector3.one;
            }
            finally
            {
                Application.logMessageReceived -= Count;
            }
        }

        // --- The same movement at any size ---

        private static readonly float[] AnySize = { 0.1f, 0.25f, 0.5f, 2f, 10f, 100f };

        /// <summary>Where the character was after one physics step, relative to its start and divided by its size.</summary>
        private struct CourseStep
        {
            public Vector3 position;
            public bool grounded;
            public string phase;
        }

        /// <summary>
        /// The course, laid out for the character at full size, from its own height, step height, slope limit, walking
        /// speed and dash, so any character gets the same workout. Scaled with the character when it's run at another
        /// size. Along +z: ramps up onto a platform and back down, a step up onto a long block, room to jump and dash
        /// along it, then a low ceiling to walk under and a step too tall to climb, which stops it.
        /// </summary>
        private sealed class Course
        {
            public float rampAngle, rise, run, blockHeight, tallStep, ceiling;
            public float rampStart, platformStart, downStart, blockStart, ceilingStart, tallStepStart;
            public float settleTime, walkTime, jumpTime, jumpHoldTime, dashTime, approachTime, standTime;

            public Course(CharacterTestRig rig)
            {
                float height = rig.Height, speed = rig.WalkSpeed;
                var dash = rig.Movement.movementHandler as DashMode;
                float dashDistance = dash != null ? dash.dashSpeed * rig.Movement.SpeedScale * dash.dashDuration : 0f;
                rampAngle = Mathf.Min(20f, 0.5f * rig.MaxSlopeAngle);
                rise = 0.5f * height;
                blockHeight = 0.85f * rig.StepHeight;
                tallStep = 1.15f * rig.StepHeight;
                ceiling = 1.3f * height;
                run = rise / Mathf.Tan(rampAngle * Mathf.Deg2Rad);
                rampStart = 3f * height;
                platformStart = rampStart + run;
                downStart = platformStart + 3f * height;
                blockStart = downStart + run + 2f * height;
                settleTime = 0.5f;
                // Onto the block and a little way along it.
                walkTime = (blockStart + 2f * height) / speed + 0.5f;
                jumpTime = 1.2f;
                jumpHoldTime = 0.3f;
                dashTime = 0.5f;
                // Past where the jump and dash take it, the low ceiling, then the tall step.
                float alongBlock = blockStart + 2f * height + speed * (jumpTime + dashTime) + dashDistance;
                ceilingStart = alongBlock + 2f * height;
                tallStepStart = ceilingStart + 4f * height;
                approachTime = (tallStepStart - alongBlock) / speed + 1f;
                standTime = 1f;
            }

            public float Length => tallStepStart + 20f * rise;

            public void Build(CharacterTestRig rig, float size)
            {
                rig.Floor(0f, 4f * Length * size);
                const float thickness = 1f;
                float slope = rise / Mathf.Sin(rampAngle * Mathf.Deg2Rad);
                void Ramp(string name, float startZ, bool up)
                {
                    Quaternion tilt = Quaternion.Euler(up ? -rampAngle : rampAngle, 0f, 0f);
                    Vector3 topMiddle = new Vector3(0f, rise / 2f, startZ + run / 2f);
                    Vector3 center = topMiddle - tilt * Vector3.up * (thickness / 2f);
                    rig.Box(name, center * size, new Vector3(10f, thickness, slope) * size, tilt);
                }
                Ramp("Ramp Up", rampStart, up: true);
                rig.Box("Platform", new Vector3(0f, rise / 2f, (platformStart + downStart) / 2f) * size, new Vector3(10f, rise, downStart - platformStart) * size);
                Ramp("Ramp Down", downStart, up: false);
                float blockLength = Length - blockStart;
                rig.Box("Block", new Vector3(0f, blockHeight / 2f, blockStart + blockLength / 2f) * size, new Vector3(10f, blockHeight, blockLength) * size);
                rig.Box("Low Ceiling", new Vector3(0f, blockHeight + ceiling + 0.5f, (ceilingStart + tallStepStart) / 2f) * size,
                    new Vector3(10f, 1f, tallStepStart - ceilingStart) * size);
                rig.Box("Tall Step", new Vector3(0f, blockHeight + tallStep / 2f, tallStepStart + 10f * rise) * size,
                    new Vector3(10f, tallStep, 20f * rise) * size);
            }
        }

        /// <summary>Runs the character over the course at the given size, recording every physics step.</summary>
        private IEnumerator RunCourse(float size, List<CourseStep> steps, System.Action<Course> laidOut = null)
        {
            // Spawned first, at full size, to lay the course out for it (no physics step passes before the course is in).
            rig.SpawnCharacter(Vector3.zero);
            var course = new Course(rig);
            laidOut?.Invoke(course);
            course.Build(rig, size);
            Physics.SyncTransforms(); // So the room check sees the course.
            if (size != 1f) { Assert.That(rig.Controller.SetScale(Vector3.one * size, immediately: true), Is.True); }

            int settle = CharacterTestRig.StepsFor(course.settleTime), walk = CharacterTestRig.StepsFor(course.walkTime);
            int jump = CharacterTestRig.StepsFor(course.jumpTime), jumpHold = CharacterTestRig.StepsFor(course.jumpHoldTime);
            int dash = CharacterTestRig.StepsFor(course.dashTime), approach = CharacterTestRig.StepsFor(course.approachTime);
            int stand = CharacterTestRig.StepsFor(course.standTime);
            int total = settle + walk + jump + dash + approach + stand;
            string PhaseOf(int step) => step < settle ? "settle" : step < settle + walk ? "walk" : step < settle + walk + jump ? "jump"
                : step < settle + walk + jump + dash ? "dash" : step < settle + walk + jump + dash + approach ? "approach" : "stand";
            Inputs InputsFor(int step)
            {
                int t = step;
                if (t < settle) { return CharacterTestRig.Idle(); }
                if ((t -= settle) < walk) { return CharacterTestRig.Move(Vector3.forward); }
                if ((t -= walk) < jump) { return t < jumpHold ? CharacterTestRig.Jump(t, Vector3.forward) : CharacterTestRig.Move(Vector3.forward); }
                if ((t -= jump) < dash) { return t == 0 ? CharacterTestRig.Dash(0, Vector3.forward) : CharacterTestRig.Move(Vector3.forward); }
                if ((t -= dash) < approach) { return CharacterTestRig.Move(Vector3.forward); }
                return CharacterTestRig.Idle();
            }
            Vector3 start = rig.Position;
            yield return rig.Run(total, InputsFor, step => steps.Add(new CourseStep
            {
                position = (rig.Position - start) / size,
                grounded = rig.Grounded,
                phase = PhaseOf(step),
            }));
        }

        /// <summary>
        /// The character moves exactly like a scaled copy of itself at any size: over a course scaled with it (ramps up
        /// and down, a step, a jump, a dash, a low ceiling and a step too tall for it), every physics step puts it in the
        /// same place for its size as at full size, on the ground or in the air at the same moments. So its speed for
        /// its size, its jump's airtime and height for its size, how it handles slopes and steps (climbing those its
        /// size can and stopping at those it can't), and how it fits under ceilings and against walls are all the same.
        /// </summary>
        [UnityTest]
        public IEnumerator AtAnySize_TheCharacterMovesLikeAScaledCopyOfItself([ValueSource(nameof(AnySize))] float size)
        {
            var normal = new List<CourseStep>();
            Course course = null;
            yield return RunCourse(1f, normal, c => course = c);
            float radius = rig.Radius;
            rig.Dispose();
            yield return null;
            rig = new CharacterTestRig();
            var scaled = new List<CourseStep>();
            yield return RunCourse(size, scaled);

            Assert.That(scaled.Count, Is.EqualTo(normal.Count));
            float worst = 0f;
            int worstStep = 0, groundedMismatches = 0;
            for (int i = 0; i < normal.Count; i++)
            {
                float error = Vector3.Distance(scaled[i].position, normal[i].position);
                if (error > worst) { worst = error; worstStep = i; }
                if (scaled[i].grounded != normal[i].grounded) { groundedMismatches++; }
            }
            TestContext.WriteLine($"At {size}x: furthest from the full-size path {worst * 100f:0.00} cm (for its size) at step " +
                                  $"{worstStep} ({normal[worstStep].phase}); on the ground at different steps {groundedMismatches} times.");

            // The full-size run is what's being matched, so check it does what the course asks first.
            Assert.That(normal.Where(st => st.phase == "walk").All(st => st.grounded), Is.True,
                "Test setup: at full size, the character should stay on the ground walking over the ramps and the step.");
            Assert.That(normal.Count(st => st.phase == "jump" && !st.grounded), Is.GreaterThan(5), "Test setup: it should jump.");
            CourseStep end = normal.Last();
            Assert.That(end.position.y, Is.EqualTo(course.blockHeight).Within(0.01f), "Test setup: it should end up on the block, under the low ceiling, not on the tall step.");
            Assert.That(end.position.z, Is.EqualTo(course.tallStepStart - radius).Within(0.05f), "Test setup: the tall step should have stopped it.");

            Assert.That(scaled.Where(st => st.phase == "walk").All(st => st.grounded), Is.True,
                "It should stay on the ground walking over the ramps and the step, as at full size.");
            Assert.That(worst, Is.LessThan(0.01f), $"Every step should put it where it is at full size, for its size (step {worstStep}, {normal[worstStep].phase}).");
            Assert.That(groundedMismatches, Is.Zero, "It should be on the ground or in the air at the same moments as at full size.");

            // Spelled out: the jump lasts as long, and goes as high for its size.
            int Airtime(List<CourseStep> run) => run.Count(st => st.phase == "jump" && !st.grounded);
            float Peak(List<CourseStep> run) => run.Where(st => st.phase == "jump").Max(st => st.position.y);
            Assert.That(Airtime(scaled), Is.EqualTo(Airtime(normal)), "The jump should last as long.");
            Assert.That(Peak(scaled), Is.EqualTo(Peak(normal)).Within(0.002f), "The jump should go as high, for its size.");
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
