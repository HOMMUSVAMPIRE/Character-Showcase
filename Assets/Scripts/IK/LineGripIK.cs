using UnityEngine;

namespace Character
{
    /// <summary>
    /// Places the hands of a character swinging on a rope or riding a zipline on the line it holds. On a rope, the right
    /// hand holds it where the character caught hold and the left a little below, the body hangs back from it (the rope
    /// in front of the chest) and leans with it, as a body hanging from its hands would, so it swings out along the rope.
    /// On a zipline, both hands hold the cable overhead, one a little ahead of the other. The body is raised or lowered
    /// (within reason) so the hands, as the hanging animation holds them, reach the line. It eases in on catching hold and
    /// out on letting go.
    ///
    /// The line comes from the character's animation state (CharacterAnimationState.gripPoint and gripDirection), found in
    /// the model's parents, as FootPlacementIK finds it; hands are placed through CharacterIK's procedural goals.
    /// </summary>
    [RequireComponent(typeof(CharacterIK))]
    [DisallowMultipleComponent]
    public class LineGripIK : MonoBehaviour
    {
        [Tooltip("How strongly the hands are placed on the line (and the body moved to them). 0 turns it off.")]
        [Range(0f, 1f)] public float weight = 1f;
        [Tooltip("Seconds to ease in on catching hold of a line, and out on letting go.")]
        [Min(0f)] [SerializeField] private float fadeTime = 0.12f;
        [Tooltip("How far (m, at the character's authored size) the wrists sit from the line, so the fingers close round it: " +
                 "below a zipline's cable, and on the body's side of a rope.")]
        [SerializeField] private float wristFromLine = 0.04f;
        [Tooltip("Rope: how far (m, at the character's authored size) below the right hand the left hand holds the rope.")]
        [SerializeField] private float ropeHandGap = 0.28f;
        [Tooltip("Rope: how far (m, at the character's authored size) the body's centre hangs back from the rope, behind it.")]
        [SerializeField] private float ropeBodyBack = 0.16f;
        [Tooltip("Rope: how much the body leans with the rope as it swings (0 = stays upright, 1 = hangs straight along it from the hands).")]
        [Range(0f, 1f)] [SerializeField] private float ropeLean = 0.85f;
        [Tooltip("Zipline: how far apart (m, at the character's authored size) along the cable the hands hold it.")]
        [SerializeField] private float cableHandGap = 0.12f;
        [Tooltip("The furthest (m, at the character's authored size) the body is moved, up or down, so the hands reach the line.")]
        [Min(0f)] [SerializeField] private float maxBodyShift = 0.4f;

        private CharacterIK ik;
        private ICharacterAnimationStateSource stateSource;
        private float activeWeight;
        // The last line held, kept while easing out after letting go so the hands don't snap.
        private Vector3 point, direction;
        private bool onRope;

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
            bool holding = (state.swinging || state.ziplining) && state.gripDirection != Vector3.zero;
            if (holding)
            {
                point = state.gripPoint;
                direction = state.gripDirection;
                onRope = state.swinging;
            }
            float goal = holding ? weight : 0f;
            activeWeight = fadeTime > 0f ? Mathf.MoveTowards(activeWeight, goal, Time.deltaTime / fadeTime) : goal;
            if (activeWeight <= 0f || direction == Vector3.zero) { return; }

            float size = state.scale.y > 0f ? state.scale.y : 1f;
            Vector3 facing = state.facing * Vector3.forward;
            Vector3 rightHand, leftHand;
            if (onRope)
            {
                // The rope's up, the right hand at the grip and the left below it, both on the body's side of the rope.
                Vector3 toBody = -facing * (wristFromLine * size);
                rightHand = point + toBody;
                leftHand = point - direction * (ropeHandGap * size) + toBody;
            }
            else
            {
                // Overhead, either side of the grip along the cable (the right hand ahead), the wrists just under it.
                Vector3 under = Vector3.down * (wristFromLine * size);
                rightHand = point + direction * (0.5f * cableHandGap * size) + under;
                leftHand = point - direction * (0.5f * cableHandGap * size) + under;
            }

            // The body: as high as lets the hands reach the line as the animation holds them up (within reason), and on a
            // rope, back behind it, then leaning with it about the grip.
            Vector3 animatedHands = 0.5f * (animator.GetIKPosition(AvatarIKGoal.LeftHand) + animator.GetIKPosition(AvatarIKGoal.RightHand));
            Vector3 targetHands = 0.5f * (rightHand + leftHand);
            float limit = maxBodyShift * size;
            Vector3 shift = Vector3.up * Mathf.Clamp(targetHands.y - animatedHands.y, -limit, limit);
            Vector3 body = animator.bodyPosition;
            Quaternion bodyRotation = animator.bodyRotation;
            if (onRope)
            {
                Vector3 behind = new Vector3(point.x, 0f, point.z) - facing * (ropeBodyBack * size) - new Vector3(body.x, 0f, body.z);
                shift += Vector3.ClampMagnitude(behind, limit);
                Quaternion lean = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, direction), ropeLean);
                Vector3 shifted = body + shift;
                shift = point + lean * (shifted - point) - body;
                bodyRotation = Quaternion.Slerp(bodyRotation, lean * bodyRotation, activeWeight);
            }
            animator.bodyPosition = body + shift * activeWeight;
            animator.bodyRotation = bodyRotation;

            // World positions: CharacterIK corrects them for a stretched model. The hands keep the animation's grip.
            ik.SetProceduralGoal(IKLimb.RightHand, rightHand, Quaternion.identity, activeWeight, 0f);
            ik.SetProceduralGoal(IKLimb.LeftHand, leftHand, Quaternion.identity, activeWeight, 0f);
        }
    }
}
