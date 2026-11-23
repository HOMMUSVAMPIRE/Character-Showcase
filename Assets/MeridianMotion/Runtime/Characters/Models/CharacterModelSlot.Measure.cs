using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace MeridianMotion.Characters
{
    // Measuring the model (its height, eyes and shoulders), and fitting the character's body to it.
    public partial class CharacterModelSlot
    {
        /// <summary>Works out the model's height and eye height, and warns if it doesn't fit the character's body.</summary>
        private void MeasureModel(LoadedCharacterModel created)
        {
            // Measured as the model stands, at the character's current size, then stored per unit of that size.
            float size = Mathf.Max(ScaleY, 1e-4f);
            baseModelHeight = CharacterModelSource.MeasureHeight(created.Root) / size;
            // Across and front to back as the character faces (its model parent's space, which also takes out its scale).
            baseModelSize = CharacterModelSource.MeasureSize(created.Root, modelParent);
            baseEyeHeight = FindEyeHeight(created) / size;
            baseShoulderWidth = FindShoulderWidth(created);
            PlaceEyePoint();
            if (bodyFit != BodyFit.Off && baseModelHeight > 0f)
            {
                // Already given its source's prepared body (FitBodyFromSource): kept, not measured again, so the body
                // doesn't change when (or whether) the model arrives. A model from another source (a fallback) is fitted.
                if (bodyFromSource == null || bodyFromSource != modelOwner) { PlanBodyFit(); }
                return; // Fitted, so it can't fail to fit.
            }

            if (ModelHeightMeters <= 0f || movement == null) { return; }
            float bodyHeight = movement.Body.Height * transform.lossyScale.y;
            if (ModelHeightMeters > bodyHeight + 0.01f || ModelHeightMeters < bodyHeight * minimumHeightFraction)
            {
                Debug.LogWarning($"[{nameof(CharacterModelSlot)}] The model '{created.Root.name}' is {ModelHeightMeters:0.00} m tall, " +
                                 $"but the character's body is {bodyHeight:0.00} m. Set its source's Height to Fit To Height, " +
                                 "or change its Scale.", this);
            }
        }

        /// <summary>The name of a transform marking a model's eyes, for one that isn't a humanoid (see FindEyeHeight).</summary>
        public const string EyesName = "Eyes";

        /// <summary>
        /// The eye height above the character's feet: from the eye bones if the model has them, else partway up the
        /// head (eyes sit a little under halfway from the head bone at the skull's base to the top of the head), else
        /// from a transform named Eyes on a model that isn't a humanoid (a fox, say), else most of the way up the model. Measured as the model was created, before it's animated, so it's the rest pose.
        /// </summary>
        private float FindEyeHeight(LoadedCharacterModel created)
        {
            float HeightOf(Vector3 point) => transform.InverseTransformPoint(point).y;
            Animator animator = created.Animator;
            if (animator != null && animator.isHuman)
            {
                Transform leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
                Transform rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
                if (leftEye != null && rightEye != null) { return HeightOf(0.5f * (leftEye.position + rightEye.position)); }
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null && ModelHeightMeters > 0f)
                {
                    float headHeight = HeightOf(head.position);
                    float top = HeightOf(modelParent.position) + ModelHeightMeters;
                    if (top > headHeight) { return headHeight + 0.45f * (top - headHeight); }
                }
            }
            // A model that isn't a humanoid (a fox, say) can mark where its eyes are with a transform named Eyes.
            Transform marked = created.Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == EyesName);
            if (marked != null) { return HeightOf(marked.position); }
            return ModelHeightMeters > 0f ? 0.93f * ModelHeightMeters : defaultEyeHeight * ScaleY;
        }

        /// <summary>
        /// The distance between the model's upper arm joints, across the character, with the model parent unscaled: how
        /// broad its shoulders are. 0 if it isn't a humanoid.
        /// </summary>
        private float FindShoulderWidth(LoadedCharacterModel created)
        {
            Animator animator = created.Animator;
            if (animator == null || !animator.isHuman) { return 0f; }
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), right = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (left == null || right == null) { return 0f; }
            // In the model parent's space, which takes out its scale (the character's size) and its turn.
            Vector3 across = modelParent.InverseTransformPoint(left.position) - modelParent.InverseTransformPoint(right.position);
            return new Vector2(across.x, across.z).magnitude;
        }

        /// <summary>Works out the body the model wants (see bodyFit), and fits it if there's room.</summary>
        private void PlanBodyFit()
        {
            if (movement == null) { return; }
            BodyShape? body = bodyFit == BodyFit.Height ? BodyFitting.ScaledToHeight(movement.AuthoredBody, baseModelHeight)
                // The source the model came from, not the one asked for: a fallback model has its own body.
                : modelOwner != null && modelOwner.ModelBody is { } own ? own
                : BodyFitting.ForModel(new Vector3(baseModelSize.x, baseModelHeight, baseModelSize.z), baseShoulderWidth, bodyWidthMargin);
            fitBody = body.Value;
            pendingBodyFit = true;
            TryFitBody();
        }

        /// <summary>
        /// The body (as authored, at the character's authored size) a character with this host and the given movement gets
        /// with a model from source, as it would fit its body to a model it loads (see bodyFit), from what source
        /// measured when it was prepared; null for the movement's own (Body Fit off, or the model not prepared). For
        /// working out what a character can do before it's spawned (see MovementSimulation).
        /// </summary>
        public BodyShape? BodyFor(CharacterModelSource source, MMotionMotor motor)
        {
            if (bodyFit == BodyFit.Off || source == null || !source.HasMeasurements) { return null; }
            if (bodyFit == BodyFit.ModelsBody && source.ModelBody is { } own) { return own; }
            // Without a body of its own (prepared before models had one), as tall as it is.
            return BodyFitting.ScaledToHeight(motor.AuthoredBody, source.MeasuredHeight);
        }

        private void TryFitBody()
        {
            if (movement == null) { pendingBodyFit = false; return; }
            if (movement.SetBody(fitBody)) { pendingBodyFit = false; }
        }

        /// <summary>
        /// Updates what depends on the character's size (the eye point) after its model parent has been scaled. Called by
        /// MMotionCharacter.SetScale.
        /// </summary>
        public void RefreshScale() => PlaceEyePoint();

        private void PlaceEyePoint()
        {
            if (eyePoint != null) { eyePoint.localPosition = new Vector3(eyePoint.localPosition.x, EyeHeight * eyeHeightShare, eyePoint.localPosition.z); }
        }
    }
}
