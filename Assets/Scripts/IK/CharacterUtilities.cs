using UnityEngine;

namespace Character
{
    /// <summary>
    /// Helpers for placing IK goals correctly on models stretched along one axis, which Unity's humanoid IK gets wrong
    /// (see CharacterIK, FootPlacementIK and the other IK components).
    /// </summary>
    public static class CharacterUtilities
    {
        /// <summary>
        /// Where the animation puts an IK goal, in world space. Call only while the Animator's IK API is usable (from
        /// OnAnimatorIK). Unity's humanoid IK reports foot goals as if the model were scaled evenly by its width (x)
        /// scale, so for a model stretched along one axis (taller, say) a foot goal's height is off by the difference: a
        /// model stretched to twice its height has its foot goals reported at half their height above its origin. (Hand
        /// goals are reported where they are.) SetIKPosition takes true world positions, so a foot goal placed from a
        /// reported one (a foot planted relative to it, or a goal aimed at it to let a knee hint apply) would be lifted
        /// or sunk. This puts the height right.
        /// </summary>
        public static Vector3 AnimatedIKGoalPosition(Animator animator, AvatarIKGoal goal)
        {
            Vector3 position = animator.GetIKPosition(goal);
            if (goal != AvatarIKGoal.LeftFoot && goal != AvatarIKGoal.RightFoot) { return position; }
            Transform root = animator.transform;
            Vector3 scale = root.lossyScale;
            if (Mathf.Abs(scale.x) < 1e-6f || Mathf.Abs(scale.y - scale.x) <= 1e-5f * Mathf.Abs(scale.x)) { return position; }
            Vector3 local = Quaternion.Inverse(root.rotation) * (position - root.position);
            local.y *= scale.y / scale.x;
            return root.position + root.rotation * local;
        }

        /// <summary>
        /// The position to give SetIKPosition for a hand or foot to land at the given world position. Unity solves a
        /// limb's goal relative to where the limb starts (a foot's from its hip joint, a hand's from its collarbone), and
        /// on a model stretched along one axis it moves the hand or foot further from there, across and along the model,
        /// than it's asked to: by the model's width (x) and depth (z) scale over its height (y) scale. On a model
        /// stretched to twice its depth, a goal 5 cm in front of the hip puts the foot 10 cm in front; on one twice as
        /// wide, a hand reaching across misses by tens of centimetres. This scales those offsets back. The limb's start
        /// is taken where it was last posed, as it can't be known this frame until the IK is done; it moves little from
        /// frame to frame, and only that movement, times the stretch, matters.
        /// </summary>
        public static Vector3 IKGoalPositionToSet(Animator animator, AvatarIKGoal goal, Vector3 position)
        {
            Transform root = animator.transform;
            Vector3 scale = root.lossyScale;
            if (Mathf.Abs(scale.x) < 1e-6f || Mathf.Abs(scale.z) < 1e-6f
                || (Mathf.Abs(scale.x - scale.y) <= 1e-5f * Mathf.Abs(scale.y) && Mathf.Abs(scale.z - scale.y) <= 1e-5f * Mathf.Abs(scale.y)))
            {
                return position;
            }
            Transform start = LimbStart(animator, goal);
            if (start == null) { return position; }
            Vector3 fromStart = Quaternion.Inverse(root.rotation) * (position - start.position);
            fromStart.x *= scale.y / scale.x;
            fromStart.z *= scale.y / scale.z;
            return start.position + root.rotation * fromStart;
        }

        // Where Unity's IK takes a limb to start: the hip joint, or the collarbone (the upper arm, for a rig without one).
        private static Transform LimbStart(Animator animator, AvatarIKGoal goal)
        {
            switch (goal)
            {
                case AvatarIKGoal.LeftFoot: return animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                case AvatarIKGoal.RightFoot: return animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                case AvatarIKGoal.LeftHand:
                    Transform left = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
                    return left != null ? left : animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                default:
                    Transform right = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
                    return right != null ? right : animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            }
        }

        /// <summary>
        /// Moves a ground hit to where the ground is drawn, not where physics has it. A moving platform's Rigidbody is
        /// interpolated, drawn between its physics steps as the character is, while a ray hits it at its latest step: a
        /// foot or paw placed at the hit jumped up and down against the platform each frame. Hits on anything else
        /// (not interpolated) are left as they are.
        /// </summary>
        public static void ToDrawnPose(ref RaycastHit hit)
        {
            Rigidbody moving = hit.rigidbody;
            if (moving == null || moving.interpolation == RigidbodyInterpolation.None) { return; }
            Transform drawn = moving.transform;
            hit.point = drawn.position + drawn.rotation * Quaternion.Inverse(moving.rotation) * (hit.point - moving.position);
        }
    }
}
