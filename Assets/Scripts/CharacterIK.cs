using UnityEngine;

namespace Character
{
    /// <summary>The four IK limbs of a humanoid character.</summary>
    public enum IKLimb
    {
        LeftFoot,
        RightFoot,
        LeftHand,
        RightHand,
    }

    /// <summary>
    /// IK settings and live state for one limb: a goal for the hand or foot, and a hint for the elbow or knee.
    /// Targets can be Transforms (assigned here or via CharacterIK.SetGoalTarget / SetHintTarget) or fixed
    /// world-space positions (via CharacterIK.SetGoal / SetHint). A Transform target takes priority when assigned.
    /// </summary>
    [System.Serializable]
    public class IKLimbSettings
    {
        [Header("Goal (hand / foot)")]
        [Tooltip("Transform the hand or foot reaches for. Leave empty to use Goal Position / Goal Rotation instead.")]
        public Transform goalTarget;
        [Tooltip("World-space position the hand or foot reaches for when there is no Goal Target. This is the ankle / wrist " +
                 "position (Unity's humanoid IK goal), so for feet on the ground add the animator's feet bottom height.")]
        public Vector3 goalPosition;
        [Tooltip("World-space rotation of the hand or foot when there is no Goal Target.")]
        public Quaternion goalRotation = Quaternion.identity;
        [Tooltip("How strongly the hand or foot is pulled to the goal position. 0 = pure animation, 1 = exactly on target.")]
        [Range(0f, 1f)] public float positionWeight;
        [Tooltip("How strongly the hand or foot is turned to the goal rotation. 0 = pure animation, 1 = exactly on target.")]
        [Range(0f, 1f)] public float rotationWeight;

        [Header("Hint (elbow / knee)")]
        [Tooltip("Transform the elbow or knee bends toward. Leave empty to use Hint Position instead.")]
        public Transform hintTarget;
        [Tooltip("World-space position the elbow or knee bends toward when there is no Hint Target.")]
        public Vector3 hintPosition;
        [Tooltip("How strongly the elbow or knee bends toward the hint. Works with or without a goal: without one, the " +
                 "hand or foot stays where the animation put it while the joint swings toward the hint.")]
        [Range(0f, 1f)] public float hintWeight;

        [Header("Blending")]
        [Tooltip("Seconds taken to fade in or out when a weight changes, so IK blends smoothly with the animation.")]
        [Min(0f)] public float blendTime = 0.2f;

        // Weights actually applied this frame, easing toward the weights above.
        [System.NonSerialized] internal float currentPositionWeight, currentRotationWeight, currentHintWeight;
        // Last resolved targets, kept while fading out so the limb doesn't snap when a target is cleared or destroyed.
        [System.NonSerialized] internal Vector3 lastGoalPosition, lastHintPosition;
        [System.NonSerialized] internal Quaternion lastGoalRotation = Quaternion.identity;
        // True while the goal/hint came from a Transform. If that Transform is then destroyed, the limb holds its last
        // position (rather than jumping to goalPosition/hintPosition) until a new goal is set or the weight fades out.
        [System.NonSerialized] internal bool followingGoalTarget, followingHintTarget;
        // Per-frame procedural goal (see CharacterIK.SetProceduralGoal), layered under the manual goal above.
        [System.NonSerialized] internal Vector3 proceduralPosition;
        [System.NonSerialized] internal Quaternion proceduralRotation = Quaternion.identity;
        [System.NonSerialized] internal float proceduralPositionWeight, proceduralRotationWeight;

        public float CurrentPositionWeight => currentPositionWeight;
        public float CurrentRotationWeight => currentRotationWeight;
        public float CurrentHintWeight => currentHintWeight;
        internal bool IsActive => positionWeight > 0f || rotationWeight > 0f || hintWeight > 0f
                                  || currentPositionWeight > 0f || currentRotationWeight > 0f || currentHintWeight > 0f;
    }

    /// <summary>
    /// Layers inverse kinematics on top of the character's animations. Hands and feet can be pulled to target
    /// positions and rotations, and elbows and knees bent toward hint positions, each with its own weight that blends
    /// smoothly with the underlying animation.
    ///
    /// Uses Unity's humanoid IK, so it must sit on the same GameObject as a Humanoid Animator, and the Animator
    /// Controller layer must have IK Pass enabled (the mannequin installer sets this up).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public class CharacterIK : MonoBehaviour
    {
        [Tooltip("Scales every limb's IK weights. Set to 0 to turn all IK off without losing per-limb settings.")]
        [Range(0f, 1f)] public float masterWeight = 1f;

        [SerializeField] private IKLimbSettings leftFoot = new();
        [SerializeField] private IKLimbSettings rightFoot = new();
        [SerializeField] private IKLimbSettings leftHand = new();
        [SerializeField] private IKLimbSettings rightHand = new();

        [Tooltip("Draw targets, hints, and lines from the limbs to them in the Scene view.")]
        [SerializeField] private bool drawGizmos = true;

        /// <summary>
        /// Raised at the start of every IK pass, before any limb is solved. Procedural IK systems (such as
        /// FootPlacementIK) subscribe to read the animated pose via the Animator (GetIKPosition, bodyPosition, ...),
        /// adjust the body, and submit goals with SetProceduralGoal. Only valid to use the Animator's IK API inside it.
        /// </summary>
        public event System.Action<Animator> BeforeSolve;

        private Animator animator;
        private bool receivedIKCallback;
        private float activeTime;
        private bool warnedAboutIKPass;

        #region Public API

        /// <summary>The settings and live weights for a limb. Changing fields here takes effect next frame.</summary>
        public IKLimbSettings GetLimb(IKLimb limb) => limb switch
        {
            IKLimb.LeftFoot => leftFoot,
            IKLimb.RightFoot => rightFoot,
            IKLimb.LeftHand => leftHand,
            _ => rightHand,
        };

        /// <summary>Pulls a hand or foot to a world-space position, keeping the animated rotation.</summary>
        public void SetGoal(IKLimb limb, Vector3 position, float weight = 1f)
        {
            IKLimbSettings s = GetLimb(limb);
            s.goalTarget = null;
            s.followingGoalTarget = false;
            s.goalPosition = position;
            s.positionWeight = Mathf.Clamp01(weight);
            s.rotationWeight = 0f;
        }

        /// <summary>Pulls a hand or foot to a world-space position and rotation.</summary>
        public void SetGoal(IKLimb limb, Vector3 position, Quaternion rotation, float positionWeight = 1f, float rotationWeight = 1f)
        {
            IKLimbSettings s = GetLimb(limb);
            s.goalTarget = null;
            s.followingGoalTarget = false;
            s.goalPosition = position;
            s.goalRotation = rotation;
            s.positionWeight = Mathf.Clamp01(positionWeight);
            s.rotationWeight = Mathf.Clamp01(rotationWeight);
        }

        /// <summary>Makes a hand or foot follow a Transform's position (and rotation, if rotationWeight is above 0).</summary>
        public void SetGoalTarget(IKLimb limb, Transform target, float positionWeight = 1f, float rotationWeight = 1f)
        {
            IKLimbSettings s = GetLimb(limb);
            s.goalTarget = target;
            s.positionWeight = Mathf.Clamp01(positionWeight);
            s.rotationWeight = Mathf.Clamp01(rotationWeight);
        }

        /// <summary>Fades a hand or foot back to its animation. The last target is held while fading out.</summary>
        public void ClearGoal(IKLimb limb)
        {
            IKLimbSettings s = GetLimb(limb);
            s.positionWeight = 0f;
            s.rotationWeight = 0f;
        }

        /// <summary>Bends an elbow or knee toward a world-space position.</summary>
        public void SetHint(IKLimb limb, Vector3 position, float weight = 1f)
        {
            IKLimbSettings s = GetLimb(limb);
            s.hintTarget = null;
            s.followingHintTarget = false;
            s.hintPosition = position;
            s.hintWeight = Mathf.Clamp01(weight);
        }

        /// <summary>Bends an elbow or knee toward a Transform.</summary>
        public void SetHintTarget(IKLimb limb, Transform target, float weight = 1f)
        {
            IKLimbSettings s = GetLimb(limb);
            s.hintTarget = target;
            s.hintWeight = Mathf.Clamp01(weight);
        }

        /// <summary>Fades an elbow or knee back to its animation.</summary>
        public void ClearHint(IKLimb limb) => GetLimb(limb).hintWeight = 0f;

        /// <summary>
        /// Submits a procedural goal for this frame only. Call it from a BeforeSolve handler every frame it should
        /// apply; it has no blend time of its own, so the caller smooths it. Procedural goals sit underneath goals set
        /// with SetGoal / SetGoalTarget, which blend on top and so take priority.
        /// </summary>
        public void SetProceduralGoal(IKLimb limb, Vector3 position, Quaternion rotation, float positionWeight, float rotationWeight)
        {
            IKLimbSettings s = GetLimb(limb);
            s.proceduralPosition = position;
            s.proceduralRotation = rotation;
            s.proceduralPositionWeight = Mathf.Clamp01(positionWeight);
            s.proceduralRotationWeight = Mathf.Clamp01(rotationWeight);
        }

        /// <summary>Fades every limb back to pure animation.</summary>
        public void ClearAll()
        {
            for (int i = 0; i < 4; i++)
            {
                ClearGoal((IKLimb)i);
                ClearHint((IKLimb)i);
            }
        }

        /// <summary>Sets every weight instantly, skipping the blend (e.g. after teleporting the character).</summary>
        public void SnapWeights()
        {
            for (int i = 0; i < 4; i++)
            {
                IKLimbSettings s = GetLimb((IKLimb)i);
                s.currentPositionWeight = s.positionWeight;
                s.currentRotationWeight = s.rotationWeight;
                s.currentHintWeight = s.hintWeight;
            }
        }

        #endregion

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (!animator.isHuman)
            {
                Debug.LogWarning($"[CharacterIK] The Animator on '{name}' is not Humanoid; IK will have no effect.", this);
            }
        }

        private void Update()
        {
            // OnAnimatorIK only runs when the controller layer has IK Pass enabled. If IK is wanted but the callback
            // never arrives, say so once instead of silently doing nothing.
            bool anyActive = masterWeight > 0f && (BeforeSolve != null
                || leftFoot.IsActive || rightFoot.IsActive || leftHand.IsActive || rightHand.IsActive);
            activeTime = anyActive && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null ? activeTime + Time.deltaTime : 0f;
            if (!receivedIKCallback && !warnedAboutIKPass && activeTime > 0.5f)
            {
                warnedAboutIKPass = true;
                Debug.LogWarning($"[CharacterIK] IK weights are set on '{name}' but OnAnimatorIK is not being called. " +
                                 "Enable 'IK Pass' on the Animator Controller's layer (Tools > Character Showcase > " +
                                 "Generate Mannequin does this for the shared controller).", this);
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0) { return; }
            receivedIKCallback = true;
            float dt = Time.deltaTime;

            BeforeSolve?.Invoke(animator);

            ApplyLimb(leftFoot, AvatarIKGoal.LeftFoot, AvatarIKHint.LeftKnee, dt);
            ApplyLimb(rightFoot, AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee, dt);
            ApplyLimb(leftHand, AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, dt);
            ApplyLimb(rightHand, AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, dt);
        }

        private void ApplyLimb(IKLimbSettings s, AvatarIKGoal goal, AvatarIKHint hint, float dt)
        {
            // Ease the applied weights toward the requested ones so IK fades in and out over the animation.
            float step = s.blendTime > 0f ? dt / s.blendTime : 1f;
            s.currentPositionWeight = Mathf.MoveTowards(s.currentPositionWeight, s.positionWeight, step);
            s.currentRotationWeight = Mathf.MoveTowards(s.currentRotationWeight, s.rotationWeight, step);
            s.currentHintWeight = Mathf.MoveTowards(s.currentHintWeight, s.hintWeight, step);

            // Resolve targets, remembering them so a cleared or destroyed target can still fade out in place.
            if (s.goalTarget != null)
            {
                s.lastGoalPosition = s.goalTarget.position;
                s.lastGoalRotation = s.goalTarget.rotation;
                s.followingGoalTarget = true;
            }
            else if (!s.followingGoalTarget && (s.positionWeight > 0f || s.rotationWeight > 0f))
            {
                s.lastGoalPosition = s.goalPosition;
                s.lastGoalRotation = s.goalRotation;
            }
            if (s.hintTarget != null)
            {
                s.lastHintPosition = s.hintTarget.position;
                s.followingHintTarget = true;
            }
            else if (!s.followingHintTarget && s.hintWeight > 0f)
            {
                s.lastHintPosition = s.hintPosition;
            }

            float positionWeight = s.currentPositionWeight * masterWeight;
            float rotationWeight = s.currentRotationWeight * masterWeight;
            float hintWeight = s.currentHintWeight * masterWeight;
            float proceduralPositionWeight = Mathf.Clamp01(s.proceduralPositionWeight) * masterWeight;
            float proceduralRotationWeight = Mathf.Clamp01(s.proceduralRotationWeight) * masterWeight;

            // Compose the result layer by layer: the animation, then procedural IK (e.g. foot placement) blended over
            // it, then manually set goals blended over that, so a scripted target always wins over automatic ones.
            Vector3 animatedPosition = animator.GetIKPosition(goal);
            Quaternion animatedRotation = animator.GetIKRotation(goal);
            Vector3 desiredPosition = Vector3.Lerp(
                Vector3.Lerp(animatedPosition, s.proceduralPosition, proceduralPositionWeight),
                s.lastGoalPosition, positionWeight);
            Quaternion desiredRotation = Quaternion.Slerp(
                Quaternion.Slerp(animatedRotation, s.proceduralRotation, proceduralRotationWeight),
                s.lastGoalRotation, rotationWeight);
            float combinedPositionWeight = 1f - (1f - proceduralPositionWeight) * (1f - positionWeight);
            float combinedRotationWeight = 1f - (1f - proceduralRotationWeight) * (1f - rotationWeight);

            // Unity blends a goal as lerp(animated, target, weight), so choose the target that makes that equal the
            // desired result. Unity also only applies a hint while its limb's goal is active, so to let an elbow or
            // knee be overridden on its own the goal weight is raised to the hint weight, with the target moved back
            // toward the animated position to compensate.
            float appliedPositionWeight = Mathf.Max(combinedPositionWeight, hintWeight);
            Vector3 goalPosition = appliedPositionWeight > 0f
                ? animatedPosition + (desiredPosition - animatedPosition) / appliedPositionWeight
                : animatedPosition;

            animator.SetIKPositionWeight(goal, appliedPositionWeight);
            // Rotation targets are absolute, so apply the already-blended rotation at full weight.
            animator.SetIKRotationWeight(goal, combinedRotationWeight > 0f ? 1f : 0f);
            animator.SetIKHintPositionWeight(hint, hintWeight);
            if (s.currentPositionWeight <= 0f && s.currentRotationWeight <= 0f && s.positionWeight <= 0f && s.rotationWeight <= 0f)
            {
                s.followingGoalTarget = false;
            }
            if (s.currentHintWeight <= 0f && s.hintWeight <= 0f) { s.followingHintTarget = false; }

            if (appliedPositionWeight > 0f) { animator.SetIKPosition(goal, goalPosition); }
            if (combinedRotationWeight > 0f) { animator.SetIKRotation(goal, desiredRotation); }
            if (hintWeight > 0f) { animator.SetIKHintPosition(hint, s.lastHintPosition); }

            // Procedural goals must be re-sent every frame by whatever produces them.
            s.proceduralPositionWeight = 0f;
            s.proceduralRotationWeight = 0f;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Creates one target and one hint Transform per limb, placed at the limb's current bones, and assigns them
        /// with zero weight. Raise a weight and drag the target in the Scene view to see that limb follow.
        /// </summary>
        [ContextMenu("Create Target Transforms For All Limbs")]
        private void CreateTargetTransforms()
        {
            var anim = GetComponent<Animator>();
            if (anim == null || !anim.isHuman)
            {
                Debug.LogWarning("[CharacterIK] Needs a Humanoid Animator to place targets.", this);
                return;
            }

            Transform root = transform.Find("IK Targets");
            if (root == null)
            {
                root = new GameObject("IK Targets").transform;
                UnityEditor.Undo.RegisterCreatedObjectUndo(root.gameObject, "Create IK Targets");
                root.SetParent(transform, false);
            }

            (IKLimb limb, HumanBodyBones end, HumanBodyBones middle)[] limbs =
            {
                (IKLimb.LeftFoot, HumanBodyBones.LeftFoot, HumanBodyBones.LeftLowerLeg),
                (IKLimb.RightFoot, HumanBodyBones.RightFoot, HumanBodyBones.RightLowerLeg),
                (IKLimb.LeftHand, HumanBodyBones.LeftHand, HumanBodyBones.LeftLowerArm),
                (IKLimb.RightHand, HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm),
            };
            UnityEditor.Undo.RecordObject(this, "Create IK Targets");
            foreach (var (limb, end, middle) in limbs)
            {
                IKLimbSettings s = GetLimb(limb);
                s.goalTarget = s.goalTarget != null ? s.goalTarget : CreateMarker(root, $"{limb} Target", anim.GetBoneTransform(end));
                // Place hints a little in front of knees / behind elbows, the direction those joints naturally bend.
                Transform joint = anim.GetBoneTransform(middle);
                bool isLeg = limb == IKLimb.LeftFoot || limb == IKLimb.RightFoot;
                Vector3 offset = transform.forward * (isLeg ? 0.4f : -0.4f);
                string hintName = isLeg ? limb.ToString().Replace("Foot", "Knee") : limb.ToString().Replace("Hand", "Elbow");
                s.hintTarget = s.hintTarget != null ? s.hintTarget : CreateMarker(root, $"{hintName} Hint", joint, offset);
            }
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private static Transform CreateMarker(Transform parent, string markerName, Transform at, Vector3 offset = default)
        {
            var marker = new GameObject(markerName).transform;
            UnityEditor.Undo.RegisterCreatedObjectUndo(marker.gameObject, "Create IK Targets");
            marker.SetParent(parent, false);
            if (at != null) { marker.SetPositionAndRotation(at.position + offset, at.rotation); }
            return marker;
        }
#endif

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) { return; }
            var anim = animator != null ? animator : GetComponent<Animator>();
            if (anim == null || !anim.isHuman) { return; }

            DrawLimbGizmos(anim, leftFoot, HumanBodyBones.LeftFoot, HumanBodyBones.LeftLowerLeg);
            DrawLimbGizmos(anim, rightFoot, HumanBodyBones.RightFoot, HumanBodyBones.RightLowerLeg);
            DrawLimbGizmos(anim, leftHand, HumanBodyBones.LeftHand, HumanBodyBones.LeftLowerArm);
            DrawLimbGizmos(anim, rightHand, HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm);
        }

        private static void DrawLimbGizmos(Animator anim, IKLimbSettings s, HumanBodyBones end, HumanBodyBones middle)
        {
            Transform endBone = anim.GetBoneTransform(end);
            Transform middleBone = anim.GetBoneTransform(middle);
            bool playing = Application.isPlaying;

            float goalWeight = playing ? s.currentPositionWeight : s.positionWeight;
            if (s.goalTarget != null || goalWeight > 0f)
            {
                Vector3 goal = s.goalTarget != null ? s.goalTarget.position : s.goalPosition;
                Gizmos.color = Color.Lerp(new Color(1f, 0.4f, 0.2f, 0.35f), new Color(1f, 0.4f, 0.2f, 1f), goalWeight);
                Gizmos.DrawWireSphere(goal, 0.06f);
                if (endBone != null) { Gizmos.DrawLine(endBone.position, goal); }
            }

            float hintWeight = playing ? s.currentHintWeight : s.hintWeight;
            if (s.hintTarget != null || hintWeight > 0f)
            {
                Vector3 hintPos = s.hintTarget != null ? s.hintTarget.position : s.hintPosition;
                Gizmos.color = Color.Lerp(new Color(0.2f, 0.7f, 1f, 0.35f), new Color(0.2f, 0.7f, 1f, 1f), hintWeight);
                Gizmos.DrawWireCube(hintPos, Vector3.one * 0.07f);
                if (middleBone != null) { Gizmos.DrawLine(middleBone.position, hintPos); }
            }
        }
    }
}
