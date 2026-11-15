using UnityEngine;

namespace Character
{
    /// <summary>
    /// Places a character sliding down a wall against it: as LedgeHandIK does for a ledge, it moves the body (through
    /// the Animator's body position) flush against the wall, which the character's collider keeps its radius away from,
    /// and puts the hands flat on the wall where the sliding animation holds them up, palms to the wall and fingers up.
    /// It eases in as the slide starts and out as it ends.
    ///
    /// Climbing a climbable surface, it moves the body toward it until the nearer hand of the climbing animation is on
    /// the surface's face (the libraries' climbs are made for a wall nearer the body than the collider lets it come),
    /// easing between holds, and leaves the hands where the animation puts them, reaching and pulling.
    ///
    /// The wall comes from the character's animation state (CharacterAnimationState.wallPoint and wallNormal), found in
    /// the model's parents; hands are placed through CharacterIK's procedural goals.
    /// </summary>
    [RequireComponent(typeof(CharacterIK))]
    [DisallowMultipleComponent]
    public class WallSlideIK : MonoBehaviour
    {
        [Tooltip("How strongly the hands are placed on the wall (and the body against it). 0 turns it off.")]
        [Range(0f, 1f)] public float weight = 1f;
        [Tooltip("Seconds to ease in as a slide starts, and out as it ends.")]
        [Min(0f)] [SerializeField] private float fadeTime = 0.1f;
        [Tooltip("How far (m, at the character's authored size) the body's centre is from the wall: about half the body's depth.")]
        [SerializeField] private float bodyFromWall = 0.18f;
        [Tooltip("How far (m, at the character's authored size) the wrists are from the wall: about the hand's thickness.")]
        [SerializeField] private float wristFromWall = 0.03f;
        [Tooltip("How strongly the hands are turned flat to the wall (0 = as the animation holds them).")]
        [Range(0f, 1f)] [SerializeField] private float handRotationWeight = 0.8f;
        [Tooltip("Climbing: seconds over which the body eases to where the nearer hand is on the wall, so it doesn't jerk " +
                 "as the hands take turns holding on")]
        [Min(0f)] [SerializeField] private float climbSmoothTime = 0.2f;
        [Tooltip("Climbing: how far (m, at the character's authored size) the nearer hand's goal is kept from the surface. " +
                 "More than the wrist's distance from a wall slid down: a climbing hand's goal is a few centimetres past the " +
                 "wrist, toward the wall, so less than this put the wrists into the wall")]
        [SerializeField] private float climbHandFromWall = 0.1f;

        private CharacterIK ik;
        private ICharacterAnimationStateSource stateSource;
        private float activeWeight;
        // Climbing: how far the body is eased in now, and how far (m) it's moved toward the surface.
        private float climbWeight;
        private float climbShift;
        private bool hasClimbShift;
        // The last wall slid down, kept while easing out so the hands don't snap.
        private Vector3 wallPoint, wallNormal;

        /// <summary>How far placement is eased in now (0 to weight).</summary>
        public float ActiveWeight => activeWeight;

        private void Awake() => ik = GetComponent<CharacterIK>();
        private void OnEnable() => ik.BeforeSolve += Solve;
        private void OnDisable()
        {
            ik.BeforeSolve -= Solve;
            activeWeight = 0f;
            climbWeight = 0f;
            hasClimbShift = false;
        }

        private void Start() => stateSource = GetComponentInParent<ICharacterAnimationStateSource>();

        private void Solve(Animator animator)
        {
            if (stateSource == null || !animator.isHuman) { return; }
            CharacterAnimationState state = stateSource.AnimationState;
            if (SolveClimbing(animator, state)) { return; }
            bool sliding = state.wallSliding && state.wallNormal != Vector3.zero;
            if (sliding)
            {
                wallPoint = state.wallPoint;
                wallNormal = state.wallNormal;
            }
            float goal = sliding ? weight : 0f;
            activeWeight = fadeTime > 0f ? Mathf.MoveTowards(activeWeight, goal, Time.deltaTime / fadeTime) : goal;
            if (activeWeight <= 0f || wallNormal == Vector3.zero) { return; }

            float size = state.scale.y > 0f ? state.scale.y : 1f;

            // The body against the wall.
            Vector3 body = animator.bodyPosition;
            float fromWall = Vector3.Dot(body - wallPoint, wallNormal);
            Vector3 toWall = -wallNormal * ((fromWall - bodyFromWall * size) * activeWeight);
            animator.bodyPosition = body + toWall;

            // The hands flat on the wall where the animation holds them up (moved with the body), pressed onto its face.
            Vector3 leftHand = OnWall(animator.GetIKPosition(AvatarIKGoal.LeftHand) + toWall, size);
            Vector3 rightHand = OnWall(animator.GetIKPosition(AvatarIKGoal.RightHand) + toWall, size);

            // Palms to the wall, fingers up. A humanoid hand's IK rotation is relative to the T-pose, where the palm faces
            // down and the fingers point out along the arm (-x for the left hand, +x for the right): turned so the palm
            // faces the wall (local -y to -normal) and the fingers point up.
            Quaternion leftRotation = Quaternion.LookRotation(Vector3.Cross(Vector3.down, wallNormal), wallNormal);
            Quaternion rightRotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, wallNormal), wallNormal);
            float rotationWeight = handRotationWeight * activeWeight;
            ik.SetProceduralGoal(IKLimb.LeftHand, leftHand, leftRotation, activeWeight, rotationWeight);
            ik.SetProceduralGoal(IKLimb.RightHand, rightHand, rightRotation, activeWeight, rotationWeight);
        }

        /// <summary>
        /// Climbing (or easing out of a climb): the body moved toward the surface until the nearer animated hand is the
        /// wrist's distance from it. Returns whether that's all there is to do this frame.
        /// </summary>
        private bool SolveClimbing(Animator animator, CharacterAnimationState state)
        {
            // Not on a ladder: LadderIK has the body and limbs there.
            bool climbing = state.wallClimbing && !state.onLadder && state.wallNormal != Vector3.zero;
            if (climbing)
            {
                wallPoint = state.wallPoint;
                wallNormal = state.wallNormal;
            }
            climbWeight = fadeTime > 0f ? Mathf.MoveTowards(climbWeight, climbing ? weight : 0f, Time.deltaTime / fadeTime) : (climbing ? weight : 0f);
            if (climbWeight <= 0f || wallNormal == Vector3.zero)
            {
                hasClimbShift = false;
                return false;
            }

            float size = state.scale.y > 0f ? state.scale.y : 1f;
            if (climbing)
            {
                float left = Vector3.Dot(animator.GetIKPosition(AvatarIKGoal.LeftHand) - wallPoint, wallNormal);
                float right = Vector3.Dot(animator.GetIKPosition(AvatarIKGoal.RightHand) - wallPoint, wallNormal);
                float target = Mathf.Min(left, right) - climbHandFromWall * size;
                // Eased toward the wall, but never so far a hand goes into it: the nearer of the two hands moves smoothly
                // (as each does), so following it back at once doesn't jerk, and easing after it put the hands through.
                climbShift = !hasClimbShift || climbSmoothTime <= 0f ? target
                    : Mathf.Min(target, Mathf.Lerp(climbShift, target, 1f - Mathf.Exp(-Time.deltaTime / climbSmoothTime)));
                hasClimbShift = true;
            }
            animator.bodyPosition -= wallNormal * (climbShift * climbWeight);
            return true;
        }

        /// <summary>A point moved onto the wall's face (the wrist's distance out from it).</summary>
        private Vector3 OnWall(Vector3 point, float size) =>
            point - wallNormal * (Vector3.Dot(point - wallPoint, wallNormal) - wristFromWall * size);
    }
}
