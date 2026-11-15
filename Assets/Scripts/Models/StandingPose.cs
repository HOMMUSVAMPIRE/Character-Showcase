using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Character
{
    /// <summary>
    /// Where a model's soles are as it stands still, measured from its standing clip: how far above its origin they are
    /// with its feet where its animation puts them, and with its feet at their IK goals (where foot placement holds
    /// them). Retargeted onto a model of other proportions than the clip's own, the two differ by up to a centimetre or
    /// so. In the model's own units (unscaled). Also which way the body faces: humanoid animation keeps the facing the
    /// model was made with, so one made facing away from +Z plays its animations facing that way too. (It does centre
    /// the body on the model's origin, however far off the model was made.)
    /// </summary>
    public readonly struct StandingPose
    {
        public StandingPose(float soleHeight, float placedSoleHeight, float facing = 0f)
        {
            SoleHeight = soleHeight;
            PlacedSoleHeight = placedSoleHeight;
            Facing = facing;
        }

        /// <summary>
        /// Which way the body faces as it stands, in degrees about the up axis from the model's +Z (clockwise seen from
        /// above, as Unity measures yaw): from its hips and shoulders, the way its chest faces. 0 for a model that isn't
        /// a humanoid.
        /// </summary>
        public float Facing { get; }

        /// <summary>
        /// The turn about the up axis, in degrees, that faces a model standing this way along its +Z, as the character
        /// expects: the facing undone, to the nearest quarter turn. Only whole quarter turns, since a model is made facing
        /// along an axis (and a turn of a few degrees is more likely its stance than a mistake).
        /// </summary>
        public float FacingCorrection
        {
            get
            {
                float turn = -Mathf.Round(Facing / 90f) * 90f;
                turn = Mathf.Repeat(turn + 180f, 360f) - 180f; // -180 to 180.
                return Mathf.Approximately(turn, -180f) ? 180f : turn + 0f; // + 0 turns -0 into 0.
            }
        }
        /// <summary>How far above its origin the soles are, posed as the animation has them.</summary>
        public float SoleHeight { get; }
        /// <summary>How far above its origin the soles are with the feet at their IK goals, as foot placement holds them.</summary>
        public float PlacedSoleHeight { get; }

        /// <summary>
        /// Measures a model standing in the clip its Animator Controller plays standing still. The model is copied for
        /// this (under a hidden, temporary object, at the origin), so the model itself, which may be inactive and not yet
        /// set up, isn't touched: works on a prefab, or on a model just created and still hidden. The standing clip is
        /// the one given, or else the one the controller's default state plays with its parameters at their defaults
        /// (for a blend tree on speed, standing still), its strongest at the first frame. False if the model has no
        /// Animator, the controller no clip, or the model no meshes.
        /// </summary>
        public static bool TryMeasure(GameObject model, RuntimeAnimatorController controller, Avatar avatarOverride,
            out StandingPose pose, AnimationClip standingClip = null)
        {
            pose = default;
            if (model == null || controller == null) { return false; }
            var holder = new GameObject("Measuring Standing Pose") { hideFlags = HideFlags.HideAndDontSave };
            PlayableGraph graph = default;
            var baked = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                GameObject copy = Object.Instantiate(model, holder.transform, false);
                copy.hideFlags = HideFlags.HideAndDontSave;
                copy.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                copy.transform.localScale = Vector3.one;
                copy.SetActive(true);
                Animator animator = copy.GetComponentInChildren<Animator>(true);
                if (animator == null) { return false; }
                if (avatarOverride != null) { animator.avatar = avatarOverride; }
                animator.applyRootMotion = false;
                // Posed by the graph below only: in Play mode, the copy's own controller would otherwise start playing too.
                animator.runtimeAnimatorController = null;

                graph = PlayableGraph.Create("Measure Standing Pose");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
                if (standingClip == null) { standingClip = DefaultStateClip(graph, output, controller); }
                if (standingClip == null) { return false; }

                AnimationClipPlayable clip = AnimationClipPlayable.Create(graph, standingClip);
                output.SetSourcePlayable(clip);
                clip.SetApplyFootIK(false);
                graph.Evaluate(0f);
                float soles = LowestPoint(copy, baked);
                clip.SetApplyFootIK(true);
                graph.Evaluate(0f);
                float placedSoles = LowestPoint(copy, baked);
                if (!float.IsFinite(soles) || !float.IsFinite(placedSoles)) { return false; }
                float facing = animator.isHuman ? BodyFacing(animator) : 0f;
                // Rounded, so measuring again doesn't change a stored value over floating point noise.
                pose = new StandingPose(Mathf.Round(soles * 1e5f) / 1e5f, Mathf.Round(placedSoles * 1e5f) / 1e5f, Mathf.Round(facing * 100f) / 100f);
                return true;
            }
            finally
            {
                if (graph.IsValid()) { graph.Destroy(); }
                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(holder);
            }
        }

        /// <summary>The way the posed body faces, from the model's +Z, in degrees: see Facing. (The model is at the origin, unturned.)</summary>
        private static float BodyFacing(Animator animator)
        {
            Vector3 Across(HumanBodyBones left, HumanBodyBones right)
            {
                Transform l = animator.GetBoneTransform(left), r = animator.GetBoneTransform(right);
                return l != null && r != null ? l.position - r.position : Vector3.zero;
            }
            // The body's left, from both hips and shoulders, so a twisted stance evens out.
            Vector3 left = Vector3.ProjectOnPlane(Across(HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg)
                                                  + Across(HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm), Vector3.up);
            if (left.sqrMagnitude < 1e-8f) { return 0f; }
            Vector3 forward = Vector3.Cross(Vector3.up, left); // Facing +Z, a body's left is -X.
            return Vector3.SignedAngle(Vector3.forward, forward, Vector3.up);
        }

        /// <summary>The strongest clip the controller's default state plays at its first frame, parameters at their defaults.</summary>
        private static AnimationClip DefaultStateClip(PlayableGraph graph, AnimationPlayableOutput output, RuntimeAnimatorController controller)
        {
            AnimatorControllerPlayable states = AnimatorControllerPlayable.Create(graph, controller);
            output.SetSourcePlayable(states);
            graph.Evaluate(0f);
            AnimationClip strongest = null;
            float weight = 0f;
            foreach (AnimatorClipInfo info in states.GetCurrentAnimatorClipInfo(0))
            {
                if (info.clip != null && info.weight > weight) { strongest = info.clip; weight = info.weight; }
            }
            states.Destroy();
            return strongest;
        }

        /// <summary>The lowest point of the model's meshes as posed now, skinned or carried by a bone (shoes, say).</summary>
        private static float LowestPoint(GameObject model, Mesh baked)
        {
            float lowest = float.PositiveInfinity;
            foreach (SkinnedMeshRenderer skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinned.sharedMesh == null) { continue; }
                // Baked without the renderer's scale taken out: world units, relative to its position and rotation.
                skinned.BakeMesh(baked, false);
                Matrix4x4 toWorld = Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
                foreach (Vector3 vertex in baked.vertices) { lowest = Mathf.Min(lowest, toWorld.MultiplyPoint3x4(vertex).y); }
            }
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || !filter.TryGetComponent(out MeshRenderer _)) { continue; }
                lowest = Mathf.Min(lowest, LowestPoint(filter.sharedMesh, filter.transform.localToWorldMatrix));
            }
            return lowest;
        }

        /// <summary>
        /// The lowest point of a mesh placed by the given matrix, from its vertices. A mesh imported without Read/Write
        /// enabled is read through its read-only mesh data where that's allowed (in the editor), or else its bounds
        /// (which can only be lower).
        /// </summary>
        private static float LowestPoint(Mesh mesh, Matrix4x4 toWorld)
        {
            float lowest = float.PositiveInfinity;
            if (mesh.isReadable)
            {
                foreach (Vector3 vertex in mesh.vertices) { lowest = Mathf.Min(lowest, toWorld.MultiplyPoint3x4(vertex).y); }
                return lowest;
            }
            try
            {
                using Mesh.MeshDataArray meshes = Mesh.AcquireReadOnlyMeshData(mesh);
                using var vertices = new Unity.Collections.NativeArray<Vector3>(meshes[0].vertexCount, Unity.Collections.Allocator.Temp);
                meshes[0].GetVertices(vertices);
                foreach (Vector3 vertex in vertices) { lowest = Mathf.Min(lowest, toWorld.MultiplyPoint3x4(vertex).y); }
                return lowest;
            }
            catch (System.Exception)
            {
                Bounds bounds = mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    lowest = Mathf.Min(lowest, toWorld.MultiplyPoint3x4(point).y);
                }
                return lowest;
            }
        }
    }
}
