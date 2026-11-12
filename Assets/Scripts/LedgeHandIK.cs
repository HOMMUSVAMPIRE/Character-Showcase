using UnityEngine;

namespace Character
{
    /// <summary>
    /// Places a hanging character's hands on the ledge it holds, and its body against the wall below. The character's
    /// collider keeps its radius from the wall, but the model is far thinner than that: unaided it hangs in mid-air in
    /// front of the wall, its hands wherever the hanging animation holds them. While hanging, this moves the body
    /// (through the Animator's body position) flush against the wall and to the height that lets the hands reach the
    /// ledge, then pulls each hand onto the ledge's top at its edge, shoulder width apart along it, palm down with the
    /// fingers over the edge. It eases in as the character catches hold, and out partway up a climb.
    ///
    /// The ledge comes from the character's animation state (CharacterAnimationState.ledgeEdge and ledgeNormal), found
    /// in the model's parents, as FootPlacementIK finds it; hands are placed through CharacterIK's procedural goals.
    /// </summary>
    [RequireComponent(typeof(CharacterIK))]
    [DisallowMultipleComponent]
    public class LedgeHandIK : MonoBehaviour
    {
        [Tooltip("How strongly the hands are placed on the ledge (and the body against the wall). 0 turns it off.")]
        [Range(0f, 1f)] public float weight = 1f;
        [Tooltip("Seconds to ease in on catching hold of a ledge, and out on letting go.")]
        [Min(0f)] [SerializeField] private float fadeTime = 0.1f;
        [Tooltip("How far (m, at the character's authored size) the wrists sit above the ledge's top: about the hand's thickness.")]
        [SerializeField] private float wristAboveLedge = 0.03f;
        [Tooltip("How far (m, at the character's authored size) the wrists sit back from the edge, on top of the ledge, so the " +
                 "fingers hook over the edge.")]
        [SerializeField] private float wristBackFromEdge = 0.04f;
        [Tooltip("How far apart the hands are along the edge, as a share of the model's shoulder width (about 0.8 m on the " +
                 "mannequin, within the 0.75 to 1.4 m the Quaternius library's climbs grip ledges at).")]
        [SerializeField] private float handSpacing = 2.35f;
        [Tooltip("How far (m, at the character's authored size) the body's centre hangs from the wall: about half the body's depth.")]
        [SerializeField] private float bodyFromWall = 0.12f;
        [Tooltip("The furthest (m, at the character's authored size) the body is moved, up or down, so the hands reach the ledge.")]
        [Min(0f)] [SerializeField] private float maxBodyShift = 0.4f;
        [Tooltip("How far up a climb (0 to 1) the hands let go of the ledge: they ease off from halfway to this.")]
        [Range(0.5f, 1f)] [SerializeField] private float climbReleaseAt = 0.8f;
        [Tooltip("How strongly the hands are turned palm down on the ledge (0 = as the animation holds them).")]
        [Range(0f, 1f)] [SerializeField] private float handRotationWeight = 0.8f;

        private CharacterIK ik;
        private ICharacterAnimationStateSource stateSource;
        private float activeWeight;
        // The last ledge held, kept while easing out after letting go so the hands don't snap.
        private Vector3 edge, wallNormal;

        /// <summary>How far hand placement is eased in now (0 to weight).</summary>
        public float ActiveWeight => activeWeight;

        private void Awake() => ik = GetComponent<CharacterIK>();
        private void OnEnable() => ik.BeforeSolve += Solve;
        private void OnDisable()
        {
            ik.BeforeSolve -= Solve;
            activeWeight = 0f;
        }

        private void Start() => stateSource = GetComponentInParent<ICharacterAnimationStateSource>();

        private void Solve(Animator animator)
        {
            if (stateSource == null || !animator.isHuman) { return; }
            CharacterAnimationState state = stateSource.AnimationState;
            bool holding = (state.hanging || state.climbing) && state.ledgeNormal != Vector3.zero;
            if (holding)
            {
                edge = state.ledgeEdge;
                wallNormal = state.ledgeNormal;
            }

            // Held while hanging; let go of partway up a climb, as the body comes up over the hands.
            float goal = 0f;
            if (holding)
            {
                goal = state.climbing
                    ? weight * (1f - Mathf.InverseLerp(0.5f, climbReleaseAt, state.climbProgress))
                    : weight;
            }
            activeWeight = fadeTime > 0f ? Mathf.MoveTowards(activeWeight, goal, Time.deltaTime / fadeTime) : goal;
            if (activeWeight <= 0f || wallNormal == Vector3.zero) { return; }

            float size = state.scale.y > 0f ? state.scale.y : 1f;
            Vector3 along = Vector3.Cross(Vector3.up, wallNormal); // To the character's left, facing the wall.

            // The hands' places: on the ledge's top, just back from the edge, either side of where it was grabbed.
            Transform leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            float shoulderWidth = leftShoulder != null && rightShoulder != null
                ? Vector3.Distance(leftShoulder.position, rightShoulder.position) : 0.35f * size;
            Vector3 onLedge = edge + Vector3.up * (wristAboveLedge * size) - wallNormal * (wristBackFromEdge * size);
            Vector3 leftHand = onLedge + along * (0.5f * handSpacing * shoulderWidth);
            Vector3 rightHand = onLedge - along * (0.5f * handSpacing * shoulderWidth);

            // The body: against the wall, and as high as lets the hands reach the ledge as the animation holds them up
            // (within reason, should the ledge be out of the animation's reach).
            Vector3 animatedLeft = animator.GetIKPosition(AvatarIKGoal.LeftHand), animatedRight = animator.GetIKPosition(AvatarIKGoal.RightHand);
            Vector3 animatedHands = 0.5f * (animatedLeft + animatedRight);
            // Spreading the hands wider than the animation holds them takes reach: the arms angle out, so they reach
            // less high. The body hangs that much higher, so the hands still get to the ledge.
            float reachLost = ReachLostToSpread(animator, along, animatedLeft, animatedRight, 0.5f * handSpacing * shoulderWidth);
            float rise = Mathf.Clamp(onLedge.y - animatedHands.y + reachLost, -maxBodyShift * size, maxBodyShift * size);
            Vector3 body = animator.bodyPosition;
            float fromWall = Vector3.Dot(body - edge, wallNormal);
            Vector3 toWall = -wallNormal * (fromWall - bodyFromWall * size);
            animator.bodyPosition = body + (toWall + Vector3.up * rise) * activeWeight;

            // Palm down, the fingers pointing over the edge, into the ledge. A humanoid hand's IK rotation is relative to
            // the T-pose, where the fingers point out along the arm (to the left for the left hand) with the palm down.
            Quaternion facingWall = Quaternion.LookRotation(-wallNormal, Vector3.up);
            Quaternion leftRotation = facingWall * Quaternion.Euler(0f, 90f, 0f);
            Quaternion rightRotation = facingWall * Quaternion.Euler(0f, -90f, 0f);
            float rotationWeight = handRotationWeight * activeWeight;
            // World positions: CharacterIK corrects them for a stretched model.
            ik.SetProceduralGoal(IKLimb.LeftHand, leftHand, leftRotation, activeWeight, rotationWeight);
            ik.SetProceduralGoal(IKLimb.RightHand, rightHand, rightRotation, activeWeight, rotationWeight);
        }

        /// <summary>
        /// How much less high the hands reach with each one halfSpread from the middle along the ledge than as the
        /// animation holds them, the arms (their upper and lower lengths) at the same stretch. From the bones' pose last
        /// frame, which hardly changes while hanging.
        /// </summary>
        private static float ReachLostToSpread(Animator animator, Vector3 along, Vector3 animatedLeft, Vector3 animatedRight, float halfSpread)
        {
            Transform upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand), otherUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (upper == null || lower == null || hand == null || otherUpper == null) { return 0f; }
            float arm = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position);
            float shoulder = 0.5f * Mathf.Abs(Vector3.Dot(upper.position - otherUpper.position, along));
            // How far each hand is out beyond its shoulder along the ledge: as animated, and as placed.
            float animatedOut = Mathf.Max(0f, 0.5f * Mathf.Abs(Vector3.Dot(animatedLeft - animatedRight, along)) - shoulder);
            float placedOut = Mathf.Max(0f, halfSpread - shoulder);
            float Up(float sideways) => Mathf.Sqrt(Mathf.Max(0f, arm * arm - sideways * sideways));
            return Mathf.Max(0f, Up(animatedOut) - Up(placedOut));
        }
    }
}
