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
    /// Stepping up onto something (from mid-air or a sprint), the model eases up after the character's quick rise (see
    /// AnimationController), and this tucks the feet up onto the top meanwhile: any foot over it, or about to reach its
    /// edge, is lifted to stand on it at least, so the knees bend up rather than the feet sinking into it, and they
    /// straighten as the body catches up. Ordinary placement takes over again once it has.
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

        [Tooltip("Height of the floor above the model's origin, in the model's own units: where its soles rest as it " +
                 "stands. Set by the model's source from its Ground Offset (see CharacterModelSource), so the feet are " +
                 "placed by their soles, wherever the model's origin is.")]
        [SerializeField] private float floorOffset;
        [Tooltip("The same, with the feet where the animation puts them rather than at their IK goals (which is where the " +
                 "feet are while this is off or fading in). Set by the model's source from its Animated Ground Offset. " +
                 "The body is raised (or lowered) by the difference while placement is partly or fully off, so the soles " +
                 "rest on the floor either way.")]
        [SerializeField] private float animatedFloorOffset;

        [Header("Feet")]
        [Tooltip("Animated foot lift (meters above the floor, for a model of average size) below which a foot counts as fully planted, so it tilts to the ground. Scaled with the model's size.")]
        [Min(0f)] [SerializeField] private float plantedHeight = 0.04f;
        [Tooltip("Animated foot lift (for a model of average size) above which a foot counts as fully lifted, so it keeps its animated rotation. Scaled with the model's size.")]
        [Min(0f)] [SerializeField] private float liftedHeight = 0.18f;
        [Tooltip("How strongly planted feet tilt to match the ground's slope.")]
        [Range(0f, 1f)] [SerializeField] private float alignToGround = 1f;
        [Tooltip("Seconds to smooth each foot's height offset, so stepping across a stair edge doesn't pop.")]
        [Range(0f, 0.3f)] [SerializeField] private float footSmoothTime = 0.05f;

        [Header("Foot locking")]
        [Tooltip("How firmly a planted foot stays where it was put down, rather than where the animation has it now. The " +
                 "model turns after the character, about its feet, which would otherwise drag a planted foot round with it " +
                 "(and any mismatch between a stride and the character's speed slides it). 0 = off.")]
        [Range(0f, 1f)] [SerializeField] private float footLock = 1f;
        [Tooltip("How far (meters, for a model of average size) the animation's foot can get from where a planted foot was " +
                 "locked before the lock lets go, so the leg is never stretched to hold it. Scaled with the model's size.")]
        [Min(0f)] [SerializeField] private float maxLockDistance = 0.25f;
        [Tooltip("Seconds a foot lock takes to take hold, and to let go as the foot lifts, so it never pops.")]
        [Min(0.001f)] [SerializeField] private float lockBlendTime = 0.08f;

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

        [Header("Stepping up")]
        [Tooltip("Stepping up onto something, how far (meters, for a model of average size) in front of its edge a foot is " +
                 "already lifted to its top, so the feet are tucked up as they reach it rather than catching on it.")]
        [Min(0f)] [SerializeField] private float stepUpTuckReach = 0.3f;
        [Tooltip("Seconds to ease the step up's tuck in, and out once the model has caught up with the character.")]
        [Min(0f)] [SerializeField] private float stepUpFadeTime = 0.05f;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        private struct FootState
        {
            public float offset, offsetVelocity;
            public Vector3 normal;
            // Foot lock: whether the foot is locked, where (in the space of the ground it was put down on, or the world
            // if none), and how far the lock is blended in.
            public bool locked;
            public Transform lockGround;
            public Vector3 lockPoint;
            public float lockWeight;
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
        // The step up's tuck: how far it's eased in, and the edge stepped up onto (kept while easing out).
        private float stepUpWeight;
        private Vector3 stepUpEdge, stepUpNormal;
        private readonly RaycastHit[] hits = new RaycastHit[8];

        /// <summary>How far the pelvis is currently lowered, in meters (negative = lower).</summary>
        public float PelvisOffset => pelvisOffset;
        /// <summary>Height of the floor (where the soles rest standing) above the model's origin, in the model's units.</summary>
        public float FloorOffset { get => floorOffset; set => floorOffset = value; }
        /// <summary>Height of the soles above the model's origin as the animation poses them, in the model's units.</summary>
        public float AnimatedFloorOffset { get => animatedFloorOffset; set => animatedFloorOffset = value; }
        /// <summary>How far foot placement is faded in (0 to Weight): it fades out in the air and while dashing.</summary>
        public float ActiveWeight => activeWeight;

        /// <summary>
        /// Carries on from another model's foot placement, as it was this frame: how far it was faded in, and how far
        /// it had moved the feet and pelvis. For a model replacing another on the same character, so feet already
        /// planted stay planted rather than fading back in. Call before this one's first update.
        /// </summary>
        public void ContinueFrom(FootPlacementIK other)
        {
            if (other == null || other == this) { return; }
            activeWeight = Mathf.Min(other.activeWeight, weight);
            pelvisOffset = other.pelvisOffset;
            pelvisVelocity = other.pelvisVelocity;
            left = other.left;
            right = other.right;
        }

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
            // Not while dashing, sliding or rolling either: their poses trail or tuck the legs, which planting the feet
            // would undo. Nor climbing a wall, whose feet are on the wall, even at its foot with the floor under them.
            bool posed = state.dashing || state.sliding || state.rolling || state.vaulting || state.wallClimbing;
            float goal = grounded && !posed ? weight : 0f;
            activeWeight = groundedFadeTime > 0f ? Mathf.MoveTowards(activeWeight, goal, dt / groundedFadeTime) : goal;

            // The model is placed for its soles where this holds the feet (at their IK goals). Where the animation has
            // them instead, the soles are a little higher or lower; while placement is off, or blended partly over the
            // animation, the body and the foot goals are moved by that share of the difference, so the soles still rest
            // on the floor. (Blended at weight w, a foot is w of the way from the animated foot, moved with the body, to
            // its goal: moving both by (1 - w) of the difference puts it on the floor.)
            // Stepping up, the feet are tucked onto the top instead (see SolveStepUp), until the model has caught up.
            bool steppingUp = (state.steppingUp || state.stepUpLag < 0f) && state.ledgeNormal != Vector3.zero;
            if (steppingUp)
            {
                stepUpEdge = state.ledgeEdge;
                stepUpNormal = state.ledgeNormal;
            }
            float stepUpGoal = steppingUp || state.stepUpLag < 0f ? weight : 0f;
            stepUpWeight = stepUpFadeTime > 0f ? Mathf.MoveTowards(stepUpWeight, stepUpGoal, dt / stepUpFadeTime) : stepUpGoal;
            if (stepUpWeight > 0f && stepUpNormal != Vector3.zero) { activeWeight = 0f; }

            float animationLift = (1f - activeWeight) * (floorOffset - animatedFloorOffset) * VerticalScale;
            if (animationLift != 0f) { animator.bodyPosition += Vector3.up * animationLift; }
            if (stepUpWeight > 0f && stepUpNormal != Vector3.zero)
            {
                SolveStepUp(animator, animationLift);
                pelvisOffset = Mathf.SmoothDamp(pelvisOffset, 0f, ref pelvisVelocity, pelvisSmoothTime, Mathf.Infinity, dt);
                left.locked = right.locked = false;
                left.lockWeight = right.lockWeight = 0f;
                return;
            }
            if (activeWeight <= 0f)
            {
                pelvisOffset = Mathf.SmoothDamp(pelvisOffset, 0f, ref pelvisVelocity, pelvisSmoothTime, Mathf.Infinity, dt);
                left.locked = right.locked = false;
                left.lockWeight = right.lockWeight = 0f;
                return;
            }

            // The animations are authored for flat ground under the model's soles: its origin, raised by the floor offset.
            float floorHeight = transform.position.y + floorOffset * VerticalScale;

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

            leftTarget += Vector3.up * animationLift;
            rightTarget += Vector3.up * animationLift;
            ik.SetProceduralGoal(IKLimb.LeftFoot, leftTarget, leftRotation, activeWeight, leftRotationWeight * activeWeight);
            ik.SetProceduralGoal(IKLimb.RightFoot, rightTarget, rightRotation, activeWeight, rightRotationWeight * activeWeight);
        }

        /// <summary>
        /// Tucks the feet up onto the top stepped up onto: a foot over it, or within stepUpTuckReach of its edge, is
        /// lifted (if it's lower) so its sole is on the top, and the leg's knee bends to reach; feet further back are
        /// left as animated, hanging in front of the wall.
        /// </summary>
        private void SolveStepUp(Animator animator, float animationLift)
        {
            float size = animator.isHuman && animator.humanScale > 0f ? animator.humanScale * transform.lossyScale.y : 1f;
            Vector3 TuckedFoot(AvatarIKGoal goal, float soleHeight)
            {
                Vector3 foot = CharacterUtilities.AnimatedIKGoalPosition(animator, goal) + Vector3.up * animationLift;
                float ahead = Vector3.Dot(foot - stepUpEdge, -stepUpNormal); // Positive over the top.
                float onTop = stepUpEdge.y + soleHeight * VerticalScale;
                if (ahead > -stepUpTuckReach * size && foot.y < onTop) { foot.y = onTop; }
                return foot;
            }
            Vector3 leftFoot = TuckedFoot(AvatarIKGoal.LeftFoot, animator.leftFeetBottomHeight);
            Vector3 rightFoot = TuckedFoot(AvatarIKGoal.RightFoot, animator.rightFeetBottomHeight);
            ik.SetProceduralGoal(IKLimb.LeftFoot, leftFoot, animator.GetIKRotation(AvatarIKGoal.LeftFoot), stepUpWeight, 0f);
            ik.SetProceduralGoal(IKLimb.RightFoot, rightFoot, animator.GetIKRotation(AvatarIKGoal.RightFoot), stepUpWeight, 0f);
        }

        private Vector3 PlaceFoot(Animator animator, AvatarIKGoal goal, float soleHeight, float floorHeight, ref FootState foot,
            float dt, out Quaternion rotation, out float rotationWeight)
        {
            Vector3 animated = CharacterUtilities.AnimatedIKGoalPosition(animator, goal);
            Quaternion animatedRotation = animator.GetIKRotation(goal);
            // How high the animation lifts this foot off its (assumed flat) floor. The sole's height under the goal is
            // given for the model unscaled.
            float lift = animated.y - soleHeight * VerticalScale - floorHeight;

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

            Vector3 placed = animated + Vector3.up * foot.offset;
            return LockFoot(ref foot, placed, planted, foot.hit ? hit.transform : null, size, dt);
        }

        /// <summary>
        /// Holds a planted foot where it was put down: locked as the animation plants it, held there (in the space of the
        /// ground under it, so it rides moving ground) while the animation keeps it planted, and let go as it lifts, or
        /// if the animation's foot gets too far from the lock. The lock is blended in and out over the animation's foot.
        /// Only the foot's position across the ground is held; its height stays as placed.
        /// </summary>
        private Vector3 LockFoot(ref FootState foot, Vector3 placed, float planted, Transform ground, float size, float dt)
        {
            if (footLock <= 0f) { return placed; }

            if (foot.locked)
            {
                Vector3 held = LockPosition(foot);
                Vector2 apart = new Vector2(held.x - placed.x, held.z - placed.z);
                if (planted < 0.5f || apart.magnitude > maxLockDistance * size) { foot.locked = false; }
            }
            else if (planted >= 0.999f && foot.lockWeight <= 0f)
            {
                foot.locked = true;
                foot.lockGround = ground;
                foot.lockPoint = ground != null ? ground.InverseTransformPoint(placed) : placed;
            }

            foot.lockWeight = Mathf.MoveTowards(foot.lockWeight, foot.locked ? 1f : 0f, dt / lockBlendTime);
            if (foot.lockWeight <= 0f) { return placed; }
            Vector3 lockAt = LockPosition(foot);
            float w = foot.lockWeight * footLock;
            return new Vector3(Mathf.Lerp(placed.x, lockAt.x, w), placed.y, Mathf.Lerp(placed.z, lockAt.z, w));
        }

        /// <summary>Where a foot's lock is now, in world space (it moves with the ground it was put down on).</summary>
        private static Vector3 LockPosition(in FootState foot) =>
            foot.lockGround != null ? foot.lockGround.TransformPoint(foot.lockPoint) : foot.lockPoint;

        /// <summary>How much the model is scaled vertically in the world (by the character's size).</summary>
        private float WorldScale => Mathf.Max(transform.lossyScale.y, 1e-4f);

        /// <summary>The model's vertical scale in the world.</summary>
        private float VerticalScale => transform.lossyScale.y;

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
