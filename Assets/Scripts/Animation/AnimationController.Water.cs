using System;
using Level;
using UnityEngine;

namespace Character
{
    // The character's head and the water: whether it's under the surface, and events as it goes under and comes up.
    public partial class AnimationController
    {
        /// <summary>
        /// Raised when the character's head goes under a water's surface (see Level.Water), with that water: for what
        /// tracks breath, muffles sound or switches an underwater look. The head is the model's head bone as it's posed
        /// this frame (animated, after IK), or its eye point for a model without a humanoid head. It must go Head Water
        /// Margin under the surface to count, and that far back above it to come up (HeadSurfaced).
        /// </summary>
        public event Action<Water> HeadSubmerged;

        /// <summary>Raised when the character's head comes back above the water it was under (see HeadSubmerged), with that water.</summary>
        public event Action<Water> HeadSurfaced;

        /// <summary>Whether the character's head is under a water's surface, as of the last frame.</summary>
        public bool IsHeadUnderwater => headWater != null;

        /// <summary>The water the character's head is under, as of the last frame, or null.</summary>
        public Water HeadWater => headWater;

        private Water headWater;

        // After the model's animation (and the IK on it) has posed it for the frame.
        private void LateUpdate() => CheckHeadInWater();

        /// <summary>
        /// Checks the head against the water, now, raising HeadSubmerged or HeadSurfaced if it's gone under or come up
        /// since the last check (or moved from one water straight into another). Called every frame (LateUpdate).
        /// </summary>
        public void CheckHeadInWater()
        {
            Water now = null;
            if (TryGetHead(out Vector3 head))
            {
                // Under, the head must rise the margin above the surface to come up; above, sink the margin under it to
                // go under.
                float margin = headWaterMargin * (modelHost != null && modelHost.ModelParent != null ? modelHost.ModelParent.lossyScale.y : 1f);
                now = headWater != null && headWater.isActiveAndEnabled && headWater.Covers(head) && head.y < headWater.SurfaceHeight + margin
                    ? headWater
                    : Water.At(head + Vector3.up * margin);
            }
            if (now == headWater) { return; }
            Water was = headWater;
            headWater = now;
            if (was != null) { HeadSurfaced?.Invoke(was); }
            if (now != null) { HeadSubmerged?.Invoke(now); }
        }

        /// <summary>
        /// Where the character's head is: its model's head bone, or else the eye point (a model without a humanoid head).
        /// False without a model.
        /// </summary>
        private bool TryGetHead(out Vector3 head)
        {
            head = default;
            if (modelHost == null) { return false; }
            Animator modelAnimator = modelHost.Animator;
            Transform bone = modelAnimator != null && modelAnimator.isHuman ? modelAnimator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (bone == null) { bone = modelHost.EyePoint; }
            if (bone == null) { return false; }
            head = bone.position;
            return true;
        }
    }
}
