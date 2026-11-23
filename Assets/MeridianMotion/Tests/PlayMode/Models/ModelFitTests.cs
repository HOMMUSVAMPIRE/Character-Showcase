using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MeridianMotion.Characters;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace MeridianMotion.Tests.Models
{
    /// <summary>
    /// Tests of how the character is fitted to its model: the model scaled to the character's height, its eyes
    /// following; the body made as tall as the model (waiting for room to grow), or the source's own body taken (a
    /// fallback model's too); and a model measured once on loading, until its measurement is forgotten.
    /// </summary>
    [Category(TestArea.Models)]
    public class ModelFitTests : ModelTestsBase
    {
        /// <summary>
        /// Fit To Height scales the model to the height asked for, for the character as authored, and its eyes (and the
        /// eye point) follow; on a resized character, the character's size multiplies that, as it does any model's.
        /// </summary>
        [UnityTest]
        public IEnumerator FitToHeight_ScalesTheModel_InProportionToTheCharacter_AndTheEyesFollow([Values(1f, 2f)] float size)
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            CharacterModelSlot models = rig.Models;
            yield return rig.WaitForModel();
            yield return new WaitForFixedUpdate();
            float ownEyes = models.EyeHeight, ownHeight = models.ModelHeightMeters;
            Assert.That(ownEyes, Is.InRange(0.5f * ownHeight, ownHeight), "The eyes should be in the model's upper half.");
            if (models.EyePoint != null) { Assert.That(models.EyePoint.localPosition.y, Is.EqualTo(ownEyes).Within(1e-4f)); }
            if (size != 1f) { rig.Controller.SetScale(Vector3.one * size, immediately: true); }

            // The same model fitted to a smaller height: everything about its size should follow.
            const float fit = 1.2f;
            PrefabModelSource fitted = Fitted(fit);
            yield return CharacterTestRig.WaitFor(models.LoadModelAsync(fitted));

            Assert.That(models.ModelHeightMeters, Is.EqualTo(fit * size).Within(0.01f * size), $"The model should be fitted to {fit} m, times the character's size.");
            float expectedEyes = ownEyes * fit / ownHeight * size;
            Assert.That(models.EyeHeight, Is.EqualTo(expectedEyes).Within(0.02f * size), "The eyes should scale with the model.");
            if (models.EyePoint != null) { Assert.That(models.EyePoint.localPosition.y, Is.EqualTo(models.EyeHeight).Within(1e-4f)); }
        }

        /// <summary>
        /// Body Fit makes the character's body as tall as the model, standing on the character's feet: its width in
        /// proportion as authored (Height), or, for a source without a body of its own, as wide as the model's shoulders,
        /// times the margin (Model's Body). The character's size still multiplies it.
        /// </summary>
        [UnityTest]
        public IEnumerator BodyFit_MakesTheBodyAsTallAsTheModel_AndAsWideAsAsked(
            [Values(BodyFit.Height, BodyFit.ModelsBody)] BodyFit fit)
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.WaitForModel();
            yield return new WaitForFixedUpdate();
            float radius = rig.BodyNow.radius, height = rig.BodyNow.height;
            rig.Models.BodyFit = fit;
            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(Fitted(1.2f)));

            Assert.That(rig.BodyNow.height, Is.EqualTo(1.2f).Within(0.01f), "The body should be as tall as the model.");
            Assert.That(rig.BodyNow.center.y, Is.EqualTo(rig.BodyNow.height / 2f).Within(1e-4f), "It should stand on the character's feet.");
            if (fit == BodyFit.Height)
            {
                Assert.That(rig.BodyNow.radius, Is.EqualTo(radius * 1.2f / height).Within(0.005f), "Its width should keep its proportions.");
            }
            else
            {
                Animator animator = rig.Models.Animator;
                if (animator == null || !animator.isHuman) { Assert.Ignore("The character's model isn't a humanoid, so it has no shoulders to fit."); }
                float shoulders = Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position,
                    animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position);
                Assert.That(rig.BodyNow.radius, Is.EqualTo(0.5f * shoulders * rig.Models.BodyWidthMargin).Within(0.01f),
                    "The body should be as wide as the shoulders, times the margin.");
            }
            yield return rig.Settle();
            Assert.That(rig.Grounded, Is.True);
            Assert.That(rig.Position.y, Is.EqualTo(0f).Within(0.005f));

            // The character's size still multiplies it.
            float fitted = rig.BodyNow.height;
            rig.Controller.SetScale(Vector3.one * 2f, immediately: true);
            Assert.That(rig.BodyNow.height, Is.EqualTo(2f * fitted).Within(0.02f));
        }

        [UnityTest]
        public IEnumerator BodyFit_WaitsForRoom_BeforeGrowing()
        {
            // A ceiling 1.5 m up from z = -2 to z = 2: room for a 1.2 m body, not a 1.8 m one.
            rig.Box("Low Ceiling", new Vector3(0f, 2f, 0f), new Vector3(10f, 1f, 4f));
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.WaitForModel();
            rig.Models.BodyFit = BodyFit.Height;
            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(Fitted(1.2f)));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            Assert.That(rig.BodyNow.height, Is.EqualTo(1.2f).Within(0.01f), "Test setup: the shorter body should fit.");

            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(Fitted(1.8f)));
            yield return rig.Run(0.3f, _ => CharacterTestRig.Idle());
            Assert.That(rig.Models.IsFittingBody, Is.True, "Under the ceiling, it should wait to grow.");
            Assert.That(rig.BodyNow.height, Is.EqualTo(1.2f).Within(0.01f), "It shouldn't grow into the ceiling.");
            Assert.That(rig.Position.y, Is.EqualTo(0f).Within(0.005f), "It shouldn't be pushed through the floor.");

            MMotionAnimator animation = rig.Animation;
            // The rig moves the character itself; the animation is fed each step, as the character's own update would.
            void Feed(int _) => animation.UpdateAnimator(rig.Movement.lastStep, Time.fixedDeltaTime);
            yield return rig.Run(rig.SecondsToWalk(3f + rig.FrontExtent, 1f), _ => CharacterTestRig.Move(Vector3.forward), Feed);
            Assert.That(rig.Position.z, Is.GreaterThan(3f), "Test setup: it should have walked out from under the ceiling.");
            Assert.That(rig.Models.IsFittingBody, Is.False);
            Assert.That(rig.BodyNow.height, Is.EqualTo(1.8f).Within(0.01f), "Out in the open, it should grow to fit the model.");
        }

        /// <summary>
        /// With Model's Body, a source's own body (its Body Definition, in the model's units) is the character's, sized as
        /// the source sizes the model; one of another kind than the character's body replaces it with one of its kind.
        /// </summary>
        [UnityTest]
        public IEnumerator ModelsBody_TakesTheSourcesBodyDefinition_SizedWithTheModel()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.WaitForModel();
            yield return new WaitForFixedUpdate();
            rig.Models.BodyFit = BodyFit.ModelsBody;
            PrefabModelSource source = OwnCopy();
            const float modelScale = 1.25f;
            source.Scale = modelScale;
            var size = new Vector3(0.6f, 1.6f, 0.4f);
            source.BodyDefinition = Track(BodyDefinition.Of(BodyShape.Box(size)));
            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(source));

            BodyShape box = rig.BodyNow;
            Assert.That(box.kind, Is.EqualTo(BodyShapeKind.Box), "The character's body should be a box, as the model's is.");
            Assert.That(Vector3.Distance(box.size, size * modelScale), Is.LessThan(1e-4f), "Sized as the source sizes the model.");
            Assert.That(box.center.y - box.size.y / 2f, Is.EqualTo(0f).Within(1e-4f), "Standing on the character's feet.");
            yield return rig.Settle();
            Assert.That(rig.Grounded, Is.True);
            Assert.That(rig.Position.y, Is.EqualTo(0f).Within(0.005f));
        }

        /// <summary>
        /// A fallback model fitted to its source's body takes the fallback's body, not the body of the source that failed.
        /// </summary>
        [UnityTest]
        public IEnumerator ModelsBody_OfAFallbackModel_IsTheFallbacksBody()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.WaitForModel();
            yield return new WaitForFixedUpdate();
            rig.Models.BodyFit = BodyFit.ModelsBody;
            var failing = Track(ScriptableObject.CreateInstance<FailingSource>());
            failing.BodyDefinition = Track(BodyDefinition.Of(BodyShape.Box(new Vector3(0.9f, 1.2f, 0.9f))));
            PrefabModelSource fallback = OwnCopy();
            var fallbackSize = new Vector3(0.5f, 1.7f, 0.3f);
            fallback.BodyDefinition = Track(BodyDefinition.Of(BodyShape.Box(fallbackSize)));
            rig.Models.FallbackSource = fallback;

            bool loaded = false;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("didn't create a model"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Using the fallback model"));
            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(failing), l => loaded = l);

            Assert.That(loaded, Is.True, "The fallback should load in the failed model's place.");
            BodyShape box = rig.BodyNow;
            Assert.That(box.kind, Is.EqualTo(BodyShapeKind.Box));
            Assert.That(Vector3.Distance(box.size, fallbackSize), Is.LessThan(1e-4f),
                "The body should be the fallback's, which the model came from, not the failed source's.");
        }

        /// <summary>
        /// A source measuring on load measures a model once, for every copy of it: the measurement is kept until it's
        /// forgotten (or the source edited, or its model, controller or avatar changed), as a changed model shows.
        /// </summary>
        [UnityTest]
        public IEnumerator MeasuringOnLoad_MeasuresAModelOnce_UntilTheMeasurementIsForgotten()
        {
            rig.Floor();
            rig.SpawnCharacter(new Vector3(0f, 0.05f, 0f));
            yield return rig.WaitForModel();
            PrefabModelSource own = OwnSource();
            if (rig.Models.Animator == null || rig.Models.Animator.GetComponent<HumanoidFootPlacementIK>() == null) { Assert.Ignore("The character's model has no foot placement to measure for."); }
            GameObject template = Track(Object.Instantiate(own.ModelPrefab));
            template.SetActive(false);
            PrefabModelSource source = OwnCopy(template);
            source.MeasureGroundOnLoad = true;
            float FloorOffset() => rig.Models.Animator.GetComponent<HumanoidFootPlacementIK>().FloorOffset;

            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(source));
            float first = FloorOffset();
            if (!own.MeasureGroundOnLoad) { Assert.That(first, Is.EqualTo(own.GroundOffset).Within(1e-4f), "Test setup: measured as Prepare Humanoid Models measured it."); }

            // A block under the model's feet: its lowest point, so its soles would be measured there.
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(block.GetComponent<Collider>());
            block.transform.SetParent(template.transform, false);
            block.transform.localPosition = new Vector3(0f, -0.3f, 0f);
            block.transform.localScale = Vector3.one * 0.2f;

            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(source));
            Assert.That(FloorOffset(), Is.EqualTo(first), "Another copy of the model should use the measurement already made.");

            source.ForgetMeasurement();
            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(source));
            Assert.That(FloorOffset(), Is.EqualTo(-0.4f).Within(0.01f), "Once forgotten, the model should be measured again.");
        }
    }
}
