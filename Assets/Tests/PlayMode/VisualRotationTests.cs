using System.Collections;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests of how the character's model turns after the character: the character (its root) turns at once where it's
    /// steered, and the model (its model parent) eases round after it, no faster than its turn speed limit (lower at a
    /// run), leaning into turns and with acceleration, upright standing still; facings abilities set are turned to at
    /// once, without leaning; and it turns with spinning ground. The model's turning is stepped with the physics, as
    /// ACCharacterController does each frame in the game.
    /// </summary>
    public class VisualRotationTests
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

        private T Setting<T>(string name)
        {
            var field = typeof(AnimationController).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"AnimationController has no '{name}' setting.");
            return (T)field.GetValue(rig.Animation);
        }

        private Transform Model => rig.Models.ModelParent;
        private float ModelYaw => Model.eulerAngles.y;
        private float RootYaw => rig.Body.rotation.eulerAngles.y;

        /// <summary>Turns and animates the model for the step just taken, as ACCharacterController does each frame.</summary>
        private void Feed(int _)
        {
            rig.Animation.SmoothRotateVisuals(Time.fixedDeltaTime, rig.Movement.ConsumeGroundRotation());
            rig.Animation.UpdateAnimator(rig.Movement.lastFrameMovementData, Time.fixedDeltaTime);
        }

        private IEnumerator Stand()
        {
            rig.Floor(0f, 300f);
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f), settleSeconds: 0f);
            if (Model == null || Model == rig.Character.transform) { Assert.Ignore("The character has no model parent of its own."); }
            yield return rig.Run(0.5f, _ => CharacterTestRig.Idle(), Feed);
        }

        [UnityTest]
        public IEnumerator TurningASharpCorner_TheCharacterTurnsAtOnce_AndTheModelEasesRoundAfterIt()
        {
            yield return Stand();
            yield return rig.Run(0.5f, _ => CharacterTestRig.Move(Vector3.forward), Feed);

            float firstStepYaw = -1f, overshoot = 0f;
            int settledAt = -1;
            yield return rig.Run(1f, _ => CharacterTestRig.Move(Vector3.right), step =>
            {
                Feed(step);
                if (step == 0) { firstStepYaw = ModelYaw; Assert.That(RootYaw, Is.EqualTo(90f).Within(1f), "The character should turn at once."); }
                overshoot = Mathf.Max(overshoot, Mathf.DeltaAngle(90f, ModelYaw));
                if (settledAt < 0 && Mathf.Abs(Mathf.DeltaAngle(ModelYaw, 90f)) < 2f) { settledAt = step; }
            });

            Assert.That(Mathf.DeltaAngle(0f, firstStepYaw), Is.LessThan(30f), "The model should ease into the turn, not snap most of the way round at once.");
            Assert.That(settledAt, Is.InRange(1, CharacterTestRig.StepsFor(0.6f)), "The model should finish the turn soon after the character.");
            Assert.That(overshoot, Is.LessThan(2f), "The model shouldn't swing past the way the character faces.");
        }

        [UnityTest]
        public IEnumerator TurningAroundAtARun_IsSlowerThanStandingStill_NoFasterThanTheMovingLimit()
        {
            yield return Stand();
            if (!(rig.RunSpeed > rig.WalkSpeed)) { Assert.Ignore("The character doesn't sprint."); }
            float limit = Setting<float>("maxTurnSpeedMoving"), fullAt = Setting<float>("fullTurnSpeedAt");
            if (rig.Mode<WalkRunMode>().runSpeed < fullAt) { Assert.Ignore("The character's sprint is slower than full turning speed."); }

            // Standing still, turning around.
            float fastestStanding = 0f, previous = ModelYaw;
            yield return rig.Run(0.6f, _ => CharacterTestRig.Move(0.05f * Vector3.back), step =>
            {
                Feed(step);
                fastestStanding = Mathf.Max(fastestStanding, Mathf.Abs(Mathf.DeltaAngle(previous, ModelYaw)) / Time.fixedDeltaTime);
                previous = ModelYaw;
            });

            // Sprinting, turning around.
            yield return rig.Run(0.8f, step => CharacterTestRig.Sprint(Vector3.forward, step), Feed);
            float fastestRunning = 0f;
            previous = ModelYaw;
            yield return rig.Run(0.8f, step => CharacterTestRig.Sprint(Vector3.back, step + 100), step =>
            {
                Feed(step);
                fastestRunning = Mathf.Max(fastestRunning, Mathf.Abs(Mathf.DeltaAngle(previous, ModelYaw)) / Time.fixedDeltaTime);
                previous = ModelYaw;
            });

            Assert.That(fastestRunning, Is.LessThanOrEqualTo(1.05f * limit), "At a run, the model should turn no faster than the moving limit.");
            Assert.That(fastestStanding, Is.GreaterThan(1.2f * fastestRunning), "Standing still, the model should turn more briskly than at a run.");
        }

        /// <summary>Sprints round a circle (steering turning at the given rate, degrees per second; positive = right) and gives the model's lean once settled.</summary>
        private IEnumerator CircleAndMeasureBank(float degreesPerSecond, System.Action<float, float> bankAndSpeed)
        {
            yield return Stand();
            float heading = 0f, sum = 0f, speedSum = 0f;
            int samples = 0;
            const float seconds = 2f;
            yield return rig.Run(seconds, step =>
            {
                heading += degreesPerSecond * Time.fixedDeltaTime;
                return CharacterTestRig.Sprint(Quaternion.Euler(0f, heading, 0f) * Vector3.forward, step);
            }, step =>
            {
                Feed(step);
                if (step < CharacterTestRig.StepsFor(1f)) { return; }
                sum += rig.Animation.Bank;
                speedSum += rig.HorizontalSpeed;
                samples++;
            });
            bankAndSpeed(sum / samples, speedSum / samples);
        }

        [UnityTest]
        public IEnumerator RunningRoundACurve_TheModelLeansIntoIt_ByAShareOfTheBalancingLean([Values(90f, -90f)] float degreesPerSecond)
        {
            float bank = 0f, speed = 0f;
            yield return CircleAndMeasureBank(degreesPerSecond, (b, s) => { bank = b; speed = s; });
            float share = Setting<float>("turnLean"), maxLean = Setting<float>("maxLean");
            float limit = Mathf.Min(maxLean, Setting<float>("maxLeanPerSpeed") * speed / rig.Movement.SpeedScale);
            float expected = Mathf.Clamp(share * Mathf.Atan(speed / rig.Movement.SpeedScale * degreesPerSecond * Mathf.Deg2Rad / 9.81f) * Mathf.Rad2Deg, -limit, limit);
            Assert.That(Mathf.Sign(bank), Is.EqualTo(Mathf.Sign(degreesPerSecond)), "The model should lean into the turn, not out of it.");
            Assert.That(bank, Is.EqualTo(expected).Within(0.25f * Mathf.Abs(expected)), "The model should lean by the configured share of the balancing lean.");
            Assert.That(Mathf.Abs(bank), Is.LessThanOrEqualTo(maxLean + 0.01f));
        }

        [UnityTest]
        public IEnumerator RoundTheSameCurve_WalkingLeansAboutHalfAsMuchAsSprinting()
        {
            const float degreesPerSecond = 25f;
            float walkBank = 0f, sprintBank = 0f;
            yield return Stand();
            if (!(rig.RunSpeed > 1.5f * rig.WalkSpeed)) { Assert.Ignore("The character's sprint isn't much faster than its walk."); }
            yield return Circle(degreesPerSecond, sprint: false, b => walkBank = b);
            yield return Circle(degreesPerSecond, sprint: true, b => sprintBank = b);
            float maxLean = Setting<float>("maxLean");
            if (sprintBank >= maxLean - 0.1f) { Assert.Ignore("Sprinting round the test curve already leans the most the model leans."); }
            float speedRatio = rig.RunSpeed / rig.WalkSpeed;
            Assert.That(walkBank, Is.GreaterThan(0f));
            Assert.That(sprintBank / walkBank, Is.EqualTo(speedRatio).Within(0.35f * speedRatio),
                "Leaning should follow how hard the character's path turns it (its speed times its path's turn rate), so walking a curve leans less than sprinting it.");
        }

        [UnityTest]
        public IEnumerator WalkingRoundASharpCorner_LeansOnlyALittle()
        {
            yield return Stand();
            yield return rig.Run(0.6f, _ => CharacterTestRig.Move(Vector3.forward), Feed);
            float most = 0f;
            yield return rig.Run(0.8f, _ => CharacterTestRig.Move(Vector3.right), step => { Feed(step); most = Mathf.Max(most, Mathf.Abs(rig.Animation.Bank)); });
            TestContext.WriteLine($"Most lean walking round a right angle: {most:0.0} degrees");
            float limit = Mathf.Min(Setting<float>("maxLean"), Setting<float>("maxLeanPerSpeed") * rig.WalkSpeed / rig.Movement.SpeedScale);
            Assert.That(most, Is.LessThanOrEqualTo(limit + 0.05f), "A walking turn should lean no further than walking speed allows.");
            Assert.That(most, Is.LessThan(0.6f * Setting<float>("maxLean")), "A walking turn shouldn't throw the model over as far as it can lean.");
        }

        /// <summary>Walks or sprints round a circle (steering turning at the given rate) from standing, and gives the model's lean once settled.</summary>
        private IEnumerator Circle(float degreesPerSecond, bool sprint, System.Action<float> bank)
        {
            yield return rig.Run(0.6f, _ => CharacterTestRig.Idle(), Feed);
            float heading = rig.Body.rotation.eulerAngles.y, sum = 0f;
            int samples = 0;
            yield return rig.Run(2.5f, step =>
            {
                heading += degreesPerSecond * Time.fixedDeltaTime;
                Vector3 direction = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
                return sprint ? CharacterTestRig.Sprint(direction, step) : CharacterTestRig.Move(direction);
            }, step =>
            {
                Feed(step);
                if (step < CharacterTestRig.StepsFor(1.2f)) { return; }
                sum += rig.Animation.Bank;
                samples++;
            });
            bank(sum / samples);
        }

        [UnityTest]
        public IEnumerator SpeedingUpAndStopping_TheModelLeansForwardThenBack_AndIsUprightAtASteadySpeedOrStandingStill()
        {
            yield return Stand();
            Assert.That(Mathf.Abs(rig.Animation.Bank) + Mathf.Abs(rig.Animation.Pitch), Is.LessThan(0.1f), "Standing still, the model should be upright.");

            float mostForward = 0f, mostBack = 0f;
            yield return rig.Run(0.4f, step => CharacterTestRig.Sprint(Vector3.forward, step), step => { Feed(step); mostForward = Mathf.Max(mostForward, rig.Animation.Pitch); });
            yield return rig.Run(1f, step => CharacterTestRig.Sprint(Vector3.forward, step + 100), Feed);
            float steady = rig.Animation.Pitch;
            yield return rig.Run(0.6f, _ => CharacterTestRig.Idle(), step => { Feed(step); mostBack = Mathf.Min(mostBack, rig.Animation.Pitch); });
            yield return rig.Run(1f, _ => CharacterTestRig.Idle(), Feed);

            Assert.That(mostForward, Is.GreaterThan(1f), "Speeding up, the model should lean forward.");
            Assert.That(Mathf.Abs(steady), Is.LessThan(0.5f), "At a steady speed, the model should be upright again.");
            Assert.That(mostBack, Is.LessThan(-1f), "Stopping, the model should lean back.");
            Assert.That(Mathf.Abs(rig.Animation.Pitch), Is.LessThan(0.1f), "Standing still again, the model should be upright.");
        }

        [UnityTest]
        public IEnumerator Rolling_TheModelTurnsToTheRollAtOnce_WithoutLeaning()
        {
            yield return Stand();
            float yawAfterFewSteps = 0f, mostLean = 0f;
            int rollSteps = CharacterTestRig.StepsFor(rig.Mode<RollMode>().rollDuration);
            yield return rig.Run(rollSteps - 2, step => step == 0 ? CharacterTestRig.Roll(0, Vector3.right) : CharacterTestRig.Idle(), step =>
            {
                Feed(step);
                if (step == 5) { yawAfterFewSteps = ModelYaw; }
                if (step > 5) { mostLean = Mathf.Max(mostLean, Mathf.Abs(rig.Animation.Bank), Mathf.Abs(rig.Animation.Pitch)); }
            });
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yawAfterFewSteps, 90f)), Is.LessThan(5f), "The model should face a roll almost at once.");
            Assert.That(mostLean, Is.LessThan(1f), "The model shouldn't lean during a roll.");
        }

        [UnityTest]
        public IEnumerator OnASpinningPlatform_TheModelTurnsWithIt_WithoutLagging()
        {
            rig.Floor(-5f, 50f);
            var platform = rig.Platform(Vector3.zero, new Vector3(8f, 1f, 8f), Vector3.zero, 1f, new Vector3(0f, 90f, 0f));
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f), settleSeconds: 0f);
            if (Model == null || Model == rig.Character.transform) { Assert.Ignore("The character has no model parent of its own."); }
            float worstLag = 0f;
            yield return rig.Run(2f, _ => CharacterTestRig.Idle(), step =>
            {
                Feed(step);
                if (step > CharacterTestRig.StepsFor(0.5f)) { worstLag = Mathf.Max(worstLag, Mathf.Abs(Mathf.DeltaAngle(ModelYaw, RootYaw))); }
            });
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, RootYaw)), Is.GreaterThan(30f), "Test setup: the platform should have turned the character.");
            Assert.That(worstLag, Is.LessThan(1f), "The model should turn with the platform, not trail behind it.");
        }
    }
}
