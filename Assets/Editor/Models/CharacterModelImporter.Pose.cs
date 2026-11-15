using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    // A model's importer settings, its avatar, and putting it in a T-pose (straightening what's above the skeleton).
    public static partial class CharacterModelImporter
    {
        private static void ConfigureImporter(ModelImporter importer)
        {
            bool changed = false;
            void Set<T>(T current, T wanted, System.Action<T> apply)
            {
                if (!Equals(current, wanted)) { apply(wanted); changed = true; }
            }
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                // A model being set up for the first time. Its axis conversion is baked in, so its skeleton isn't hung
                // under a node turned to convert axes: straightening such a node (StraightenAboveSkeleton) didn't put
                // Quaternius's female mannequin's avatar in a T-pose, and every animation held her arms overhead.
                // (Models already set up keep their setting.)
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.bakeAxisConversion = true;
                changed = true;
            }
            Set(importer.importCameras, false, v => importer.importCameras = v);
            Set(importer.importLights, false, v => importer.importLights = v);
            if (changed) { importer.SaveAndReimport(); }
        }

        private static Avatar AvatarOf(ModelImporter importer, string modelPath) =>
            importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther
                ? importer.sourceAvatar
                : AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();

        /// <summary>
        /// Humanoid animation is stored relative to each avatar's T-pose, so an avatar whose reference pose isn't a T-pose
        /// plays every animation offset by the difference: a model rigged with its arms angled down holds them that much
        /// higher in every clip. This straightens the model's spine up, its arms out to the sides and its legs down, from
        /// its rest pose, and stores that as the avatar's reference pose (as the Rig tab's Enforce T-Pose does).
        ///
        /// The rest pose comes from the skin's bind poses rather than the pose the file's skeleton is saved in, which
        /// can be a frame of an animation: Blender's FBX export leaves the skeleton in the last clip's pose. An avatar
        /// made from a posed skeleton stores its own clips relative to that pose, so they look right on that model but
        /// lean or twist on every other one.
        /// Returns whether the reference pose changed.
        /// </summary>
        public static bool EnforceTPose(ModelImporter importer, string modelPath)
        {
            // Unity maps the bones automatically on import without saving the mapping, so read it from the avatar.
            HumanDescription description = importer.humanDescription;
            if (description.human == null || description.human.Length == 0)
            {
                Avatar avatar = AvatarOf(importer, modelPath);
                if (avatar == null || !avatar.isHuman) { return false; }
                description = avatar.humanDescription;
            }
            if (description.human == null || description.human.Length == 0 || description.skeleton == null) { return false; }

            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Dictionary<string, Transform> transforms = new();
                foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                {
                    transforms.TryAdd(t.name, t);
                }
                RestoreBindPose(instance);
                var boneNames = description.human.ToDictionary(h => h.humanName, h => h.boneName);
                Transform Bone(HumanBodyBones bone) =>
                    boneNames.TryGetValue(HumanTrait.BoneName[(int)bone], out string name) && transforms.TryGetValue(name, out Transform t) ? t : null;

                Transform leftLeg = Bone(HumanBodyBones.LeftUpperLeg), rightLeg = Bone(HumanBodyBones.RightUpperLeg);
                if (leftLeg == null || rightLeg == null) { return false; }
                // The character's left, from its hips, so this works whichever way the model faces.
                Vector3 left = Vector3.ProjectOnPlane(leftLeg.position - rightLeg.position, Vector3.up).normalized;
                if (left == Vector3.zero) { return false; }

                // Parents before children, so each bone is aimed after its parent has moved it.
                // The spine straight up, as the mannequin's is: its clips lean the torso relative to a straight spine,
                // so a model resting with a curved or tilted spine would carry that into every pose.
                Transform[] spine =
                {
                    Bone(HumanBodyBones.Hips), Bone(HumanBodyBones.Spine), Bone(HumanBodyBones.Chest),
                    Bone(HumanBodyBones.UpperChest), Bone(HumanBodyBones.Neck), Bone(HumanBodyBones.Head),
                };
                Transform[] chain = spine.Where(t => t != null).ToArray();
                for (int i = 0; i + 1 < chain.Length; i++) { Aim(chain[i], chain[i + 1], Vector3.up); }
                Aim(Bone(HumanBodyBones.LeftUpperArm), Bone(HumanBodyBones.LeftLowerArm), left);
                Aim(Bone(HumanBodyBones.LeftLowerArm), Bone(HumanBodyBones.LeftHand), left);
                Aim(Bone(HumanBodyBones.LeftHand), Bone(HumanBodyBones.LeftMiddleProximal), left);
                Aim(Bone(HumanBodyBones.RightUpperArm), Bone(HumanBodyBones.RightLowerArm), -left);
                Aim(Bone(HumanBodyBones.RightLowerArm), Bone(HumanBodyBones.RightHand), -left);
                Aim(Bone(HumanBodyBones.RightHand), Bone(HumanBodyBones.RightMiddleProximal), -left);
                Aim(leftLeg, Bone(HumanBodyBones.LeftLowerLeg), Vector3.down);
                Aim(Bone(HumanBodyBones.LeftLowerLeg), Bone(HumanBodyBones.LeftFoot), Vector3.down);
                Aim(rightLeg, Bone(HumanBodyBones.RightLowerLeg), Vector3.down);
                Aim(Bone(HumanBodyBones.RightLowerLeg), Bone(HumanBodyBones.RightFoot), Vector3.down);
                StraightenAboveSkeleton(instance.transform, Bone(HumanBodyBones.Hips));

                bool changed = false;
                SkeletonBone[] skeleton = description.skeleton;
                for (int i = 0; i < skeleton.Length; i++)
                {
                    if (!transforms.TryGetValue(skeleton[i].name, out Transform t) || t == instance.transform) { continue; }
                    if (Quaternion.Angle(skeleton[i].rotation, t.localRotation) > 0.01f
                        || Vector3.Distance(skeleton[i].position, t.localPosition) > 1e-4f
                        || Vector3.Distance(skeleton[i].scale, t.localScale) > 1e-4f * Mathf.Max(1f, t.localScale.magnitude)) { changed = true; }
                    skeleton[i].rotation = t.localRotation;
                    skeleton[i].position = t.localPosition;
                    skeleton[i].scale = t.localScale; // Straightening can swap a node's scale axes.
                }
                bool mappingWasSaved = importer.humanDescription.human != null && importer.humanDescription.human.Length > 0;
                if (!changed && mappingWasSaved) { return false; }
                description.skeleton = skeleton;
                importer.humanDescription = description;
                importer.SaveAndReimport();
                return changed;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// Puts every bone the meshes are skinned to back where it was when the meshes were bound to it: world pose =
        /// renderer's transform x inverse bind pose. Parents first, so placing a bone doesn't move one already placed.
        /// Bones no mesh uses keep their pose relative to their parent.
        /// </summary>
        private static void RestoreBindPose(GameObject instance)
        {
            var rest = new Dictionary<Transform, Matrix4x4>();
            foreach (SkinnedMeshRenderer skinned in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Matrix4x4[] bindposes = skinned.sharedMesh != null ? skinned.sharedMesh.bindposes : null;
                if (bindposes == null) { continue; }
                Transform[] bones = skinned.bones;
                Matrix4x4 meshToWorld = skinned.transform.localToWorldMatrix;
                for (int i = 0; i < bones.Length && i < bindposes.Length; i++)
                {
                    if (bones[i] != null) { rest.TryAdd(bones[i], meshToWorld * bindposes[i].inverse); }
                }
            }
            foreach (Transform bone in instance.GetComponentsInChildren<Transform>(true)) // Parents before children.
            {
                if (!rest.TryGetValue(bone, out Matrix4x4 world)) { continue; }
                bone.SetPositionAndRotation(world.GetColumn(3), world.rotation);
            }
        }

        /// <summary>
        /// Takes the rotation out of the nodes between the model's root and its hips, keeping everything under them where
        /// it is. Blender's FBX export rotates the armature node -90 degrees about X to convert axes. The Animator poses
        /// those nodes as the avatar stores them and places the body through them, and when the character is stretched
        /// along one axis only (taller, say) the rotation turns the stretch onto another axis: the body is distorted and
        /// lifted off the ground, its hips stretched by the scale twice over. Unrotated, as in models without such a
        /// node, the stretch applies as it should. Every bone's world pose is unchanged, so animation looks the same.
        ///
        /// A node scaled unevenly keeps its scale on the same axes in the world: its rotation is taken out only if it's
        /// a whole number of quarter turns about each axis (as an axis conversion is), which only swaps the scale's
        /// axes. Any other rotation of an unevenly scaled node can't be taken out without shearing what's under it, so
        /// the node is left as it is. (An uneven scale on its own is fine: it stretches the model along axes the
        /// character's own stretches line up with.) Returns whether every node above the hips is now unrotated.
        /// </summary>
        public static bool StraightenAboveSkeleton(Transform root, Transform hips)
        {
            if (hips == null) { return true; }
            var above = new List<Transform>();
            for (Transform node = hips.parent; node != null && node != root; node = node.parent) { above.Add(node); }
            above.Reverse(); // From the top down, so straightening one doesn't move the ones below it.
            bool allStraight = true;
            foreach (Transform node in above)
            {
                Quaternion rotation = node.localRotation;
                if (Quaternion.Angle(rotation, Quaternion.identity) < 1e-3f) { continue; }
                Vector3 scale = node.localScale;
                bool even = Mathf.Abs(scale.x - scale.y) <= 1e-5f * Mathf.Abs(scale.y) && Mathf.Abs(scale.z - scale.y) <= 1e-5f * Mathf.Abs(scale.y);
                Matrix4x4 turn = Matrix4x4.Rotate(rotation);
                if (!even && !IsQuarterTurns(turn))
                {
                    allStraight = false;
                    continue;
                }
                // Before, a child sits at node rotation x node scale x its own pose; after, at the node's new scale x its
                // new pose. With the scale's axes swapped as the rotation swaps them (the rotation applied to the scale,
                // sign dropped), the child's new pose is just the rotation applied to its old one.
                node.localScale = new Vector3(
                    Mathf.Abs(turn.m00) * scale.x + Mathf.Abs(turn.m01) * scale.y + Mathf.Abs(turn.m02) * scale.z,
                    Mathf.Abs(turn.m10) * scale.x + Mathf.Abs(turn.m11) * scale.y + Mathf.Abs(turn.m12) * scale.z,
                    Mathf.Abs(turn.m20) * scale.x + Mathf.Abs(turn.m21) * scale.y + Mathf.Abs(turn.m22) * scale.z);
                if (even) { node.localScale = scale; } // Exactly as it was, without rounding.
                node.localRotation = Quaternion.identity;
                foreach (Transform child in node)
                {
                    child.SetLocalPositionAndRotation(rotation * child.localPosition, rotation * child.localRotation);
                }
            }
            return allStraight;
        }

        /// <summary>Whether a rotation is a whole number of quarter turns: it only swaps axes (and their signs).</summary>
        private static bool IsQuarterTurns(Matrix4x4 turn)
        {
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    float value = Mathf.Abs(turn[row, column]);
                    if (value > 1e-4f && value < 1f - 1e-4f) { return false; }
                }
            }
            return true;
        }

        /// <summary>Turns the bone so its child lies in the given direction from it.</summary>
        private static void Aim(Transform bone, Transform child, Vector3 direction)
        {
            if (bone == null || child == null) { return; }
            Vector3 current = child.position - bone.position;
            if (current.sqrMagnitude < 1e-8f) { return; }
            bone.rotation = Quaternion.FromToRotation(current, direction) * bone.rotation;
        }

        /// <summary>
        /// Describes the nodes between the model's root and its hips that its avatar poses rotated, which distort the
        /// model when the character is stretched along one axis, or null if there are none. Preparing straightens them
        /// (see StraightenAboveSkeleton), so this reports what that couldn't: a node both unevenly scaled and turned by
        /// other than quarter turns. (Uneven scale on its own doesn't distort the model, so it isn't reported.)
        /// </summary>
        public static string RotatedAboveSkeleton(GameObject modelPrefab, Avatar avatarOverride)
        {
            Animator animator = modelPrefab != null ? modelPrefab.GetComponentInChildren<Animator>(true) : null;
            Avatar avatar = avatarOverride != null ? avatarOverride : animator != null ? animator.avatar : null;
            if (avatar == null || !avatar.isHuman) { return null; }
            HumanDescription description = avatar.humanDescription;
            string hipsName = description.human.FirstOrDefault(h => h.humanName == HumanTrait.BoneName[(int)HumanBodyBones.Hips]).boneName;
            Transform hips = string.IsNullOrEmpty(hipsName) ? null
                : modelPrefab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == hipsName);
            if (hips == null) { return null; }
            var problems = new List<string>();
            for (Transform node = hips.parent; node != null && node != modelPrefab.transform; node = node.parent)
            {
                SkeletonBone pose = description.skeleton.FirstOrDefault(bone => bone.name == node.name);
                if (pose.name == null) { continue; }
                if (Quaternion.Angle(pose.rotation, Quaternion.identity) > 0.5f) { problems.Add($"'{node.name}' is rotated"); }
            }
            return problems.Count == 0 ? null : string.Join(", ", problems) + ", above its skeleton.";
        }
    }
}
