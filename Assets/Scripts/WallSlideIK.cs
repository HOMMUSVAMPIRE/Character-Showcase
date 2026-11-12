using UnityEngine;

namespace Character
{
    /// <summary>
    /// Places a character sliding down a wall against it: as LedgeHandIK does for a ledge, it moves the body (through
    /// the Animator's body position) flush against the wall, which the character's collider keeps its radius away from,
    /// and puts the hands flat on the wall where the sliding animation holds them up, palms to the wall and fingers up.
    /// It eases in as the slide starts and out as it ends.
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

        private CharacterIK ik;
        private ICharacterAnimationStateSource stateSource;
        private float activeWeight;
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
        }

        private void Start() => stateSource = GetComponentInParent<ICharacterAnimationStateSource>();

        private void Solve(Animator animator)
        {
            if (stateSource == null || !animator.isHuman) { return; }
            CharacterAnimationState state = stateSource.AnimationState;
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

        /// <summary>A point moved onto the wall's face (the wrist's distance out from it).</summary>
        private Vector3 OnWall(Vector3 point, float size) =>
            point - wallNormal * (Vector3.Dot(point - wallPoint, wallNormal) - wristFromWall * size);
    }
}
