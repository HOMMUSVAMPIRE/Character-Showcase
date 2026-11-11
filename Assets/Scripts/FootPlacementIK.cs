using UnityEngine;

namespace Character
{
    /// <summary>
    /// Automatically plants the character's feet on uneven ground (slopes, stairs, bumps) using IK.
    ///
    /// The animations assume flat ground at the character's origin. Each frame, for each foot, this raycasts down
    /// at the animated foot position, shifts the foot by the difference between the real ground and that flat
    /// floor (keeping the animation's own foot lift, so swinging feet still clear the ground), and tilts planted
    /// feet to match the slope. The pelvis is lowered so the lower foot can actually reach its ground.
    ///
    /// Feeds CharacterIK's procedural channel, so goals set manually on CharacterIK still take priority.
    /// </summary>
    [RequireComponent(typeof(CharacterIK))]
    [DisallowMultipleComponent]
    public class FootPlacementIK : MonoBehaviour
    {
        [Tooltip("Overall strength of foot placement. 0 = off, 1 = feet fully placed on the ground.")]
        [Range(0f, 1f)] public float weight = 1f;

        [Header("Ground detection")]
        [Tooltip("Layers the feet can stand on. The character's own colliders are always ignored.")]
        [SerializeField] private LayerMask groundLayers = 1;
        [Tooltip("Highest ground above the character's origin a foot will step up to (e.g. the next stair).")]
        [Min(0f)] [SerializeField] private float maxStepUp = 0.45f;
        [Tooltip("Lowest ground below the character's origin a foot will reach down to.")]
        [Min(0f)] [SerializeField] private float maxStepDown = 0.45f;
        [Tooltip("Surfaces steeper than this (degrees) are ignored for foot placement.")]
        [Range(0f, 89f)] [SerializeField] private float maxSlopeAngle = 50f;
        [Tooltip("Transform whose colliders (and children's) are ignored by the ground rays. " +
                 "Defaults to the nearest parent with a Rigidbody, which is the character root.")]
        [SerializeField] private Transform ignoreCollidersUnder;

        [Header("Feet")]
        [Tooltip("Animated foot lift (meters above the floor, for a model of average size) below which a foot counts as fully planted, so it tilts to the ground. Scaled with the model's size.")]
        [Min(0f)] [SerializeField] private float plantedHeight = 0.04f;
        [Tooltip("Animated foot lift (for a model of average size) above which a foot counts as fully lifted, so it keeps its animated rotation. Scaled with the model's size.")]
        [Min(0f)] [SerializeField] private float liftedHeight = 0.18f;
        [Tooltip("How strongly planted feet tilt to match the ground's slope.")]
        [Range(0f, 1f)] [SerializeField] private float alignToGround = 1f;
        [Tooltip("Seconds to smooth each foot's height offset, so stepping across a stair edge doesn't pop.")]
        [Range(0f, 0.3f)] [SerializeField] private float footSmoothTime = 0.05f;

        [Header("Pelvis")]
        [Tooltip("Lower the pelvis so the lower foot can reach its ground (otherwise the leg can only stretch).")]
        [SerializeField] private bool adjustPelvis = true;
        [Tooltip("Furthest the pelvis may be lowered.")]
        [Min(0f)] [SerializeField] private float maxPelvisDrop = 0.4f;
        [Tooltip("Seconds to smooth pelvis height changes.")]
        [Range(0f, 0.5f)] [SerializeField] private float pelvisSmoothTime = 0.1f;

        [Header("Activation")]
        [Tooltip("Seconds to fade foot placement in on landing and out on leaving the ground.")]
        [Min(0f)] [SerializeField] private float groundedFadeTime = 0.12f;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        private struct FootState
        {
            public float offset, offsetVelocity;
            public Vector3 normal;
            // Debug
            public Vector3 rayOrigin, rayEnd, hitPoint;
            public bool hit;
        }

        private CharacterIK ik;
        // Where the character's grounded and dashing state comes from (the character's AnimationController, found in
        // the model's parents). Without one, the feet are always placed.
        private ICharacterAnimationStateSource stateSource;
        private FootState left, right;
        private float pelvisOffset, pelvisVelocity;
        private float activeWeight;
        private readonly RaycastHit[] hits = new RaycastHit[8];

        /// <summary>How far the pelvis is currently lowered, in meters (negative = lower).</summary>
        public float PelvisOffset => pelvisOffset;

        private void Awake()
        {
            ik = GetComponent<CharacterIK>();
            if (ignoreCollidersUnder == null)
            {
                Rigidbody body = GetComponentInParent<Rigidbody>();
                ignoreCollidersUnder = body != null ? body.transform : transform.root;
            }
            left.normal = right.normal = Vector3.up;
        }

        private void OnEnable() => ik.BeforeSolve += Solve;
        private void OnDisable()
        {
            ik.BeforeSolve -= Solve;
            activeWeight = 0f;
            pelvisOffset = pelvisVelocity = 0f;
            left.offset = left.offsetVelocity = right.offset = right.offsetVelocity = 0f;
        }

        private void Start() => stateSource = GetComponentInParent<ICharacterAnimationStateSource>();

        private void Solve(Animator animator)
        {
            float dt = Time.deltaTime;

            // Only place feet while on the ground. The animation state's grounded flag already includes a short grace
            // period, so small step-offs don't switch placement off.
            CharacterAnimationState state = stateSource?.AnimationState ?? new CharacterAnimationState { grounded = true };
            bool grounded = state.grounded;
            // Not while dashing either: the dash pose trails the legs behind, which planting the feet would undo.
            bool dashing = state.dashing;
            float goal = grounded && !dashing ? weight : 0f;
            activeWeight = groundedFadeTime > 0f ? Mathf.MoveTowards(activeWeight, goal, dt / groundedFadeTime) : goal;
            if (activeWeight <= 0f)
            {
                pelvisOffset = Mathf.SmoothDamp(pelvisOffset, 0f, ref pelvisVelocity, pelvisSmoothTime, Mathf.Infinity, dt);
                return;
            }

            // The animations are authored for flat ground at the model's origin (its feet).
            float floorHeight = transform.position.y;

            Vector3 leftTarget = PlaceFoot(animator, AvatarIKGoal.LeftFoot, animator.leftFeetBottomHeight, floorHeight, ref left, dt,
                out Quaternion leftRotation, out float leftRotationWeight);
            Vector3 rightTarget = PlaceFoot(animator, AvatarIKGoal.RightFoot, animator.rightFeetBottomHeight, floorHeight, ref right, dt,
                out Quaternion rightRotation, out float rightRotationWeight);

            if (adjustPelvis)
            {
                // Drop the body by the lower foot's offset so that foot can reach. Never raise it: a foot on higher
                // ground is reached by bending the knee instead.
                float pelvisGoal = Mathf.Clamp(Mathf.Min(left.offset, right.offset), -maxPelvisDrop * WorldScale, 0f);
                pelvisOffset = pelvisSmoothTime > 0f
                    ? Mathf.SmoothDamp(pelvisOffset, pelvisGoal, ref pelvisVelocity, pelvisSmoothTime, Mathf.Infinity, dt)
                    : pelvisGoal;
                animator.bodyPosition += Vector3.up * (pelvisOffset * activeWeight);
            }

            ik.SetProceduralGoal(IKLimb.LeftFoot, leftTarget, leftRotation, activeWeight, leftRotationWeight * activeWeight);
            ik.SetProceduralGoal(IKLimb.RightFoot, rightTarget, rightRotation, activeWeight, rightRotationWeight * activeWeight);
        }

        private Vector3 PlaceFoot(Animator animator, AvatarIKGoal goal, float soleHeight, float floorHeight, ref FootState foot,
            float dt, out Quaternion rotation, out float rotationWeight)
        {
            Vector3 animated = animator.GetIKPosition(goal);
            Quaternion animatedRotation = animator.GetIKRotation(goal);
            // How high the animation lifts this foot off its (assumed flat) floor.
            float lift = animated.y - soleHeight - floorHeight;

            // Search for ground under the foot between the step-up and step-down limits around the floor.
            // The model's world scale (the character's size) scales how far a foot reaches, as it does the steps the
            // character can climb.
            float stepUp = maxStepUp * WorldScale, stepDown = maxStepDown * WorldScale;
            Vector3 origin = new Vector3(animated.x, floorHeight + stepUp, animated.z);
            float distance = stepUp + stepDown;
            foot.rayOrigin = origin;
            foot.rayEnd = origin + Vector3.down * distance;
            foot.hit = FindGround(origin, distance, out RaycastHit hit);

            float offsetGoal = 0f;
            Vector3 normalGoal = Vector3.up;
            if (foot.hit)
            {
                foot.hitPoint = hit.point;
                offsetGoal = Mathf.Clamp(hit.point.y - floorHeight, -stepDown, stepUp);
                normalGoal = hit.normal;
            }

            foot.offset = footSmoothTime > 0f
                ? Mathf.SmoothDamp(foot.offset, offsetGoal, ref foot.offsetVelocity, footSmoothTime, Mathf.Infinity, dt)
                : offsetGoal;
            foot.normal = footSmoothTime > 0f
                ? Vector3.Slerp(foot.normal, normalGoal, 1f - Mathf.Exp(-dt / footSmoothTime))
                : normalGoal;

            // Planted feet tilt to the ground; lifted feet keep the animated rotation.
            // Humanoid animation lifts a bigger model's feet proportionally higher, so the thresholds scale with it.
            // (Animator.humanScale is the model's size relative to an average adult; the mannequin's is about 1.)
            float size = animator.isHuman && animator.humanScale > 0f ? animator.humanScale * transform.lossyScale.y : 1f;
            float plantedLift = plantedHeight * size, liftedLift = liftedHeight * size;
            float planted = 1f - Mathf.InverseLerp(plantedLift, Mathf.Max(liftedLift, plantedLift + 1e-3f), lift);
            rotation = Quaternion.FromToRotation(Vector3.up, foot.normal) * animatedRotation;
            rotationWeight = planted * alignToGround;

            return animated + Vector3.up * foot.offset;
        }

        /// <summary>How much the model is scaled vertically in the world (by the character's size).</summary>
        private float WorldScale => Mathf.Max(transform.lossyScale.y, 1e-4f);

        private bool FindGround(Vector3 origin, float distance, out RaycastHit closest)
        {
            closest = default;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, distance, groundLayers, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = hits[i];
                if (ignoreCollidersUnder != null && h.transform.IsChildOf(ignoreCollidersUnder)) { continue; }
                if (Vector3.Angle(h.normal, Vector3.up) > maxSlopeAngle) { continue; }
                if (h.distance < best) { best = h.distance; closest = h; }
            }
            return best < float.PositiveInfinity;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || !Application.isPlaying) { return; }
            DrawFoot(left);
            DrawFoot(right);
        }

        private static void DrawFoot(FootState foot)
        {
            Gizmos.color = foot.hit ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.3f, 0.3f);
            Gizmos.DrawLine(foot.rayOrigin, foot.hit ? foot.hitPoint : foot.rayEnd);
            if (foot.hit)
            {
                Gizmos.DrawWireSphere(foot.hitPoint, 0.03f);
                Gizmos.DrawLine(foot.hitPoint, foot.hitPoint + foot.normal * 0.2f);
            }
        }
    }
}
