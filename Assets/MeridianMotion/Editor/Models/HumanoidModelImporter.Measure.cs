using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MeridianMotion.Characters;
using UnityEditor.Animations;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    // Measuring a model: its height, and where its soles are standing still.
    public static partial class HumanoidModelImporter
    {
        /// <summary>
        /// How far above its origin the model's soles are as it stands idle, in its own units: its lowest point in its
        /// standing clip (the default state's clip, or for a blend tree, its clip for the lowest value, as standing
        /// still is), with its feet where foot placement holds them if it has foot placement, else where the animation
        /// puts them, and the latter on its own. Every mesh counts, skinned or carried by a bone. Used as its source's
        /// Ground Offset and Animated Ground Offset, so models whose feet sit a little above or below their origin (or
        /// whose animation, retargeted onto them, does) still stand on the ground. Measured by StandingPose, as sources
        /// without stored offsets are when they load. False if the model can't be posed or has no meshes.
        /// </summary>
        public static bool TryMeasureGroundOffsets(GameObject modelPrefab, RuntimeAnimatorController controller, Avatar avatarOverride,
            out float groundOffset, out float animatedGroundOffset) =>
            TryMeasureStanding(modelPrefab, controller, avatarOverride, out groundOffset, out animatedGroundOffset, out _);

        /// <summary>The same, and the way the model faces as it stands (see StandingPose.Facing).</summary>
        public static bool TryMeasureStanding(GameObject modelPrefab, RuntimeAnimatorController controller, Avatar avatarOverride,
            out float groundOffset, out float animatedGroundOffset, out StandingPose pose)
        {
            groundOffset = animatedGroundOffset = 0f;
            pose = default;
            AnimationClip standing = StandingClip(controller);
            if (modelPrefab == null || standing == null
                || !StandingPose.TryMeasure(modelPrefab, controller, avatarOverride, out pose, standing)) { return false; }
            bool placesFeet = modelPrefab.GetComponentsInChildren<HumanoidFootPlacementIK>(true).Any(feet => feet.enabled && feet.weight > 0f);
            groundOffset = placesFeet ? pose.PlacedSoleHeight : pose.SoleHeight;
            animatedGroundOffset = pose.SoleHeight;
            return true;
        }

        /// <summary>The model's Ground Offset, as TryMeasureGroundOffsets measures it.</summary>
        public static bool TryMeasureGroundOffset(GameObject modelPrefab, RuntimeAnimatorController controller, Avatar avatarOverride, out float offset) =>
            TryMeasureGroundOffsets(modelPrefab, controller, avatarOverride, out offset, out _);

        /// <summary>The clip the controller plays standing still (see TryMeasureGroundOffsets). Null if there's none.</summary>
        private static AnimationClip StandingClip(RuntimeAnimatorController controller)
        {
            if (controller is not AnimatorController editable || editable.layers.Length == 0) { return null; }
            AnimatorState state = editable.layers[0].stateMachine.defaultState;
            if (state == null) { return null; }
            Motion motion = state.motion;
            while (motion is BlendTree tree && tree.children.Length > 0)
            {
                motion = tree.children.OrderBy(child => child.threshold).First().motion;
            }
            return motion as AnimationClip;
        }

        /// <summary>The model's height in meters, from its meshes in their bind pose.</summary>
        public static float MeasureHeight(GameObject modelPrefab) => CharacterModelSource.MeasureHeight(modelPrefab);

        /// <summary>
        /// Fits the model's own body (its source's Body Definition: see BodyFitting) to it, from its meshes in their bind
        /// pose and its shoulders, as it faces once its source turns it (Facing Correction), in its own units. A source
        /// without one gets '[name] Body.asset' beside it (or the one already there). A body with Refit On Prepare off is
        /// left as it is.
        /// </summary>
        private static void FitBody(PrefabModelSource definition, string definitionPath, GameObject modelPrefab, Result result)
        {
            BodyDefinition body = definition.BodyDefinition;
            if (body == null)
            {
                string bodyPath = BodyPathFor(definitionPath);
                body = AssetDatabase.LoadAssetAtPath<BodyDefinition>(bodyPath);
                if (body == null)
                {
                    body = ScriptableObject.CreateInstance<BodyDefinition>();
                    AssetDatabase.CreateAsset(body, bodyPath);
                }
                definition.BodyDefinition = body;
            }
            if (!body.RefitOnPrepare)
            {
                result.notes.Add($"Its body ('{AssetDatabase.GetAssetPath(body)}') has Refit On Prepare off, so it was left as it is.");
                return;
            }
            Vector3 size = CharacterModelSource.MeasureSize(modelPrefab, modelPrefab.transform);
            // Turned a quarter turn to face forward, it's as wide as it was deep.
            if (Mathf.Abs(Mathf.DeltaAngle(definition.FacingCorrection, 90f)) < 45f || Mathf.Abs(Mathf.DeltaAngle(definition.FacingCorrection, -90f)) < 45f)
            {
                size = new Vector3(size.z, size.y, size.x);
            }
            body.Shape = BodyFitting.ForModel(size, MeasureShoulderWidth(modelPrefab));
            EditorUtility.SetDirty(body);
        }

        /// <summary>Where a source's body is made: '[name] Body.asset' beside the source ('[name].asset').</summary>
        private static string BodyPathFor(string definitionPath) =>
            Path.Combine(Path.GetDirectoryName(definitionPath) ?? "", Path.GetFileNameWithoutExtension(definitionPath) + " Body.asset").Replace('\\', '/');

        /// <summary>
        /// The distance between a humanoid model's upper arm joints, unscaled, across it (as CharacterModelSlot measures
        /// a model it loads): 0 if it isn't a humanoid.
        /// </summary>
        public static float MeasureShoulderWidth(GameObject modelPrefab)
        {
            if (modelPrefab == null) { return 0f; }
            GameObject instance = Object.Instantiate(modelPrefab);
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>();
                if (animator == null || !animator.isHuman) { return 0f; }
                Transform left = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), right = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                if (left == null || right == null) { return 0f; }
                Vector3 across = instance.transform.InverseTransformPoint(left.position) - instance.transform.InverseTransformPoint(right.position);
                return new Vector2(across.x, across.z).magnitude;
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
