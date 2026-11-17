using UnityEngine;

namespace Character
{
    /// <summary>
    /// Plants a four-legged creature's paws on uneven ground (slopes, stairs, bumps), for a model with a Generic rig, which
    /// Unity's humanoid IK (CharacterIK, FootPlacementIK) can't pose.
    ///
    /// The animations assume flat ground at the model's origin. After the Animator has posed the model each frame, this
    /// casts down at each animated paw to find the real ground under it, and then:
    /// - pitches the body (turning its root bone about its middle, the point between its legs' tops, at their height) to
    ///   the slope from the ground under its hips to the ground under its shoulders, at their height, so its legs stand on
    ///   that slope as the animation has them stand on the floor (probed under the legs' tops, which stay put, not the
    ///   paws, which swing), and turns its tail and neck back against the pitch (Counter Pitch Bones);
    /// - lowers it further as far as a planted leg, pitched so, would have to reach further down than the animation
    ///   reaches, so it can;
    /// - bends each leg (a two-bone solve of its upper and lower bones, its middle joint always bent the way it folds:
    ///   Bend Direction) to put its paw on its own ground, keeping the animation's lift (so a swinging paw still clears
    ///   the ground, and never eases into it) and the paw's animated turn, tilted to the ground's slope while it's planted.
    /// Placement fades out in the air, so jumps play as animated.
    ///
    /// It runs in LateUpdate while the Animator plays (Animator writes the pose before LateUpdate). A model whose Animator
    /// is stepped by hand (the tests do) calls Solve itself after each Animator.Update instead: solving twice on one
    /// evaluated pose would bend the legs twice.
    /// </summary>
    [DisallowMultipleComponent]
    public class PawPlacementIK : MonoBehaviour
    {
        /// <summary>One leg: the bones a two-bone solve bends, and where its paw touches the ground.</summary>
        [System.Serializable]
        public class Leg
        {
            [Tooltip("The leg's upper bone (the upper arm or thigh), turned at the shoulder or hip.")]
            public Transform upper;
            [Tooltip("The leg's lower bone (the forearm or shin), turned at the elbow or knee.")]
            public Transform lower;
            [Tooltip("The bone below the lower one (the paw, or the hind leg's foot below the hock): it and everything under " +
                     "it keep the animation's turn, the two bones above bending to put it where the ground is.")]
            public Transform foot;
            [Tooltip("Where the paw touches the ground (its toes, which stay down as it rolls over them), in the Foot bone's " +
                     "own space: set from the model standing in its bind pose (see SetContact).")]
            public Vector3 contact;
            [Tooltip("The way the leg's middle joint points, in the model's space: back (0, 0, -1) for a front leg's elbow, " +
                     "forward (0, 0, 1) for a hind leg's stifle. The solve always bends it that way, however straight the " +
                     "leg is. Zero bends it the way the animation has it bent.")]
            public Vector3 bendDirection;
        }

        /// <summary>A bone turned back against the body's pitch, by a share of it.</summary>
        [System.Serializable]
        public class CounterPitchBone
        {
            [Tooltip("The bone (the tail's root, the neck): it and everything under it are turned back against the body's " +
                     "pitch, about it.")]
            public Transform bone;
            [Tooltip("How much of the body's pitch it's turned back by, nose up (climbing): 0 pitches it with the body, 1 " +
                     "keeps it as level as animated.")]
            [Range(0f, 1f)] public float noseUpShare = 0.5f;
            [Tooltip("How much of the body's pitch it's turned back by, nose down (descending). A tail is best left " +
                     "pitched with the body going down: held level, it reaches into the slope rising behind.")]
            [Range(0f, 1f)] public float noseDownShare = 0.5f;
        }

        [Tooltip("Overall strength of paw placement. 0 = off (the paws where the animation puts them), 1 = fully placed.")]
        [Range(0f, 1f)] public float weight = 1f;

        [Header("Legs")]
        [Tooltip("The creature's legs: their paws are placed on the ground, and the body pitched and raised so they reach it.")]
        [SerializeField] private Leg[] legs = new Leg[0];
        [Tooltip("The bone everything else hangs from (the hips): moved and turned to raise, lower and pitch the body.")]
        [SerializeField] private Transform rootBone;

        [Header("Ground detection")]
        [Tooltip("Layers the paws can stand on. The character's own colliders are always ignored.")]
        [SerializeField] private LayerMask groundLayers = 1;
        [Tooltip("How high (m above the model's feet, for the model at its authored size) the ground is looked for from: " +
                 "inside the character's body, which nothing else can be in (from inside a stair, a ray finds nothing).")]
        [Min(0f)] [SerializeField] private float probeHeight = 0.9f;
        [Tooltip("Highest ground (m, for the model at its authored size) above the model's feet a paw will step up to (the " +
                 "next stair; the model lags below the character as it steps up). No higher than Probe Height, which the " +
                 "ground is looked for from.")]
        [Min(0f)] [SerializeField] private float maxStepUp = 0.5f;
        [Tooltip("Lowest ground (m, for the model at its authored size) below the character's feet a paw will reach down " +
                 "to. A long body on stairs rests on the step under its front end (going up) or back end (going down), " +
                 "its other legs over steps well below its feet: about as far as from that end to them, times the stairs' " +
                 "steepness.")]
        [Min(0f)] [SerializeField] private float maxStepDown = 1f;
        [Tooltip("Surfaces steeper than this (degrees) are ignored for paw placement.")]
        [Range(0f, 89f)] [SerializeField] private float maxSlopeAngle = 50f;
        [Tooltip("Transform whose colliders (and children's) the ground rays ignore. Defaults to the nearest parent with a " +
                 "Rigidbody: the character.")]
        [SerializeField] private Transform ignoreCollidersUnder;

        [Header("Body")]
        [Tooltip("How far (m, for the model at its authored size) ahead of and behind its middle the ground is probed for " +
                 "the body's slope and height: a line is fitted through the ground there, so on stairs it lies along the " +
                 "flight, not a step at a time. About half its length, within the character's body.")]
        [Min(0.01f)] [SerializeField] private float bodyProbeReach = 0.45f;
        [Tooltip("Furthest (degrees) the body is pitched to the ground's slope, nose up or down.")]
        [Range(0f, 60f)] [SerializeField] private float maxBodyPitch = 40f;
        [Tooltip("Furthest (m, for the model at its authored size) the body is lowered below the ground's slope so a planted " +
                 "paw on lower ground (a stair's tread, under the line along the flight) can reach it.")]
        [Min(0f)] [SerializeField] private float maxBodyDrop = 0.25f;
        [Tooltip("The least height (m, for the model at its authored size) the body keeps its legs' tops (shoulders and " +
                 "hips) above the ground under them, lowering an end for its legs to reach: the thighs and the chest clear " +
                 "of the ground.")]
        [Min(0f)] [SerializeField] private float legTopClearance = 0.25f;
        [Tooltip("Seconds to smooth the body's pitch, and how far it's lowered for its legs to reach, so a paw planting on a " +
                 "low tread doesn't jolt it.")]
        [Range(0f, 0.5f)] [SerializeField] private float bodySmoothTime = 0.1f;
        [Tooltip("Bones turned back against the body's pitch: the tail (which, pitched nose up with the body on a steep " +
                 "ramp, swings down into it), and the neck (so the head looks ahead up or down a slope, not at the sky or " +
                 "its paws).")]
        [SerializeField] private CounterPitchBone[] counterPitchBones = new CounterPitchBone[0];

        [Header("Paws")]
        [Tooltip("Seconds to smooth each paw's ground height, so crossing a stair's edge doesn't pop.")]
        [Range(0f, 0.3f)] [SerializeField] private float pawSmoothTime = 0.05f;
        [Tooltip("Animated paw lift (m above the floor, for the model at its authored size) below which a paw counts as " +
                 "planted, so it tilts to the ground.")]
        [Min(0f)] [SerializeField] private float plantedHeight = 0.02f;
        [Tooltip("Animated paw lift (m, for the model at its authored size) above which a paw counts as lifted, so it keeps " +
                 "its animated turn.")]
        [Min(0f)] [SerializeField] private float liftedHeight = 0.1f;
        [Tooltip("How strongly planted paws tilt to the ground's slope.")]
        [Range(0f, 1f)] [SerializeField] private float alignToGround = 1f;

        [Header("Activation")]
        [Tooltip("Seconds to fade placement in on landing and out on leaving the ground.")]
        [Min(0f)] [SerializeField] private float groundedFadeTime = 0.12f;

        [Header("Debug")]
        [Tooltip("Draw the paws' ground probes in the Scene view.")]
        [SerializeField] private bool drawGizmos = true;

        // The most legs placed: the buffers are sized once, so solving allocates nothing.
        private const int MaxLegs = 8;
        // How many points the ground under the body is probed at, from Body Probe Reach behind its middle to as far ahead.
        private const int BodyProbes = 7;
        // A leg is never solved quite straight, so the solve's bend stays well defined.
        private const float StraightLegMargin = 1e-3f;

        private struct PawState
        {
            // The ground under the paw: its height in the world, eased (and whether it's been found yet), and how far it
            // is above the animations' floor this frame.
            public float ground, groundVelocity, offset;
            public bool hasGround;
            public Vector3 normal;
            // This frame: the paw's contact and turn as animated, and how high the animation lifts it off its floor.
            public Vector3 animatedContact;
            public Quaternion animatedFootRotation;
            public float lift;
            // How far ahead of the model's origin the paw is, and how planted it is (1 down, 0 lifted).
            public float along, planted;
            // Debug
            public Vector3 rayOrigin, hitPoint;
            public bool hit;
        }

        private Animator animator;
        // Where the character's grounded state comes from (its AnimationController, found in the model's parents).
        // Without one, the paws are always placed.
        private ICharacterAnimationStateSource stateSource;
        private PawState[] paws = new PawState[0];
        private float activeWeight;
        private float bodyPitch, bodyPitchVelocity;
        // How far the body is lowered below the ground's line for its legs to reach, eased (and whether it's been set yet);
        // and its height above the animations' floor.
        private float bodyLevel, bodyLevelVelocity, bodyHeight;
        private bool hasBodyLevel;
        private readonly RaycastHit[] hits = new RaycastHit[8];
        // The ground probed under the body this frame: how far along it (from its middle) and how high.
        private readonly float[] probeAlong = new float[BodyProbes], probeHeightFound = new float[BodyProbes];

        /// <summary>The legs placed.</summary>
        public Leg[] Legs => legs;
        /// <summary>The bone the body is pitched and moved by.</summary>
        public Transform RootBone { get => rootBone; set => rootBone = value; }
        /// <summary>How far placement is faded in (0 to Weight): it fades out in the air.</summary>
        public float ActiveWeight => activeWeight;
        /// <summary>How far (degrees, positive nose up) the body is pitched to the ground now.</summary>
        public float BodyPitch => bodyPitch;
        /// <summary>
        /// How planted a leg's paw was in the pose last placed, by how high the animation lifted it: 1 down, 0 lifted (see
        /// Planted Height and Lifted Height).
        /// </summary>
        public float Planted(int leg) => leg >= 0 && leg < paws.Length ? paws[leg].planted : 0f;

        /// <summary>Sets the legs placed (the generator does, when it builds the model).</summary>
        public void SetLegs(Transform root, Leg[] newLegs)
        {
            rootBone = root;
            legs = newLegs;
            paws = new PawState[legs.Length];
        }

        /// <summary>Sets the bones turned back against the body's pitch (the generator does).</summary>
        public void SetCounterPitchBones(CounterPitchBone[] bones) => counterPitchBones = bones ?? new CounterPitchBone[0];

        /// <summary>
        /// Sets where a leg's paw touches the ground: at a point given in the model's own space (from its origin, on its
        /// floor), with the model in its bind pose. For a paw that rolls over its toes as it pushes off, the toes: they
        /// stay down through the stride, where a point under the paw's joint lifts and sinks as the paw tilts.
        /// </summary>
        public void SetContact(int leg, Vector3 modelPoint)
        {
            if (leg < 0 || leg >= legs.Length || legs[leg]?.foot == null) { return; }
            legs[leg].contact = legs[leg].foot.InverseTransformPoint(transform.TransformPoint(modelPoint));
        }

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            if (ignoreCollidersUnder == null)
            {
                Rigidbody body = GetComponentInParent<Rigidbody>();
                ignoreCollidersUnder = body != null ? body.transform : transform.root;
            }
            if (legs.Length > MaxLegs) { Debug.LogWarning($"[{nameof(PawPlacementIK)}] Only the first {MaxLegs} legs are placed.", this); }
            paws = new PawState[Mathf.Min(legs.Length, MaxLegs)];
            for (int i = 0; i < paws.Length; i++) { paws[i].normal = Vector3.up; }
        }

        private void Start() => stateSource = GetComponentInParent<ICharacterAnimationStateSource>();

        private void OnDisable()
        {
            activeWeight = 0f;
            bodyPitch = bodyPitchVelocity = bodyHeight = bodyLevelVelocity = 0f;
            hasBodyLevel = false;
            for (int i = 0; i < paws.Length; i++) { paws[i].offset = paws[i].groundVelocity = 0f; paws[i].hasGround = false; }
        }

        private void LateUpdate()
        {
            // Only on a pose the Animator has just written: one stepped by hand is solved by whoever steps it.
            if (animator != null && animator.isActiveAndEnabled) { Solve(Time.deltaTime); }
        }

        /// <summary>
        /// Places the paws on the pose the Animator has just evaluated. Called each frame (LateUpdate) while the Animator
        /// plays; call it after each Animator.Update when stepping the Animator by hand.
        /// </summary>
        public void Solve(float deltaTime)
        {
            if (rootBone == null || paws.Length == 0) { return; }
            CharacterAnimationState state = stateSource?.AnimationState ?? new CharacterAnimationState { grounded = true };
            float goal = state.grounded ? weight : 0f;
            activeWeight = groundedFadeTime > 0f ? Mathf.MoveTowards(activeWeight, goal, deltaTime / groundedFadeTime) : goal;

            float scale = Mathf.Max(transform.lossyScale.y, 1e-4f);
            float floor = transform.position.y; // The animations' floor: the model's origin, at the character's feet.
            Vector3 up = Vector3.up, forward = Vector3.ProjectOnPlane(transform.forward, up).normalized;
            if (forward == Vector3.zero) { forward = Vector3.forward; }

            // Each paw's ground, under where the animation puts it, probed from inside the character's body.
            float probeTop = floor + probeHeight * scale;
            float middleAlong = 0f, topHeight = 0f;
            int count = 0;
            for (int i = 0; i < paws.Length; i++)
            {
                Leg leg = legs[i];
                if (leg?.upper == null || leg.lower == null || leg.foot == null) { continue; }
                ref PawState paw = ref paws[i];
                paw.animatedContact = leg.foot.TransformPoint(leg.contact);
                paw.animatedFootRotation = leg.foot.rotation;
                paw.lift = paw.animatedContact.y - floor;
                // The leg's middle joint (knee or elbow), if it's ahead of the paw: a leg leaning forward over an edge
                // (a hind leg, its paw on the last step of a flight as the body goes on onto the landing) puts its paw up
                // on the edge's top, rather than its shin through the edge.
                Vector3 middleJoint = Vector3.Dot(leg.lower.position - paw.animatedContact, forward) > 0f ? leg.lower.position : leg.foot.position;
                FindPawGround(ref paw, leg.foot.position, middleJoint, probeTop, floor, scale, deltaTime);
                middleAlong += Vector3.Dot(leg.upper.position - transform.position, forward);
                topHeight += leg.upper.position.y;
                count++;
            }
            if (count == 0) { return; }
            middleAlong /= count;

            // The ground under the body, as a line: fitted (least squares) through the ground probed along it, so on a
            // flight of stairs it runs along the flight, rather than jumping a step as each leg's top crosses a step's edge.
            float sumU = 0f, sumH = 0f, sumUU = 0f, sumUH = 0f;
            int found = 0;
            float reachAlong = bodyProbeReach * scale;
            for (int k = 0; k < BodyProbes; k++)
            {
                float u = Mathf.Lerp(-reachAlong, reachAlong, k / (BodyProbes - 1f));
                Vector3 at = transform.position + forward * (middleAlong + u);
                if (!FindGround(new Vector3(at.x, probeTop, at.z), probeTop - (floor - maxStepDown * scale), out RaycastHit hit)) { continue; }
                float h = Mathf.Clamp(hit.point.y - floor, -maxStepDown * scale, maxStepUp * scale);
                sumU += u; sumH += h; sumUU += u * u; sumUH += u * h;
                probeAlong[found] = u;
                probeHeightFound[found] = h;
                found++;
            }
            float slopeGoal = 0f, groundGoal = 0f;
            if (found >= 2)
            {
                float denominator = found * sumUU - sumU * sumU;
                slopeGoal = denominator > 1e-8f ? (found * sumUH - sumU * sumH) / denominator : 0f;
                groundGoal = (sumH - slopeGoal * sumU) / found; // The line's height under the body's middle.
                // Raised onto the highest ground under it: on stairs the fitted line runs between the steps' edges and
                // their treads, and the body, held over it, would sink its belly into the edges.
                float above = 0f;
                for (int k = 0; k < found; k++) { above = Mathf.Max(above, probeHeightFound[k] - (groundGoal + slopeGoal * probeAlong[k])); }
                groundGoal += above;
            }
            else if (found == 1) { groundGoal = sumH; }

            // The body is pitched about its own middle: the point midway between its legs' tops, at their height. (About a
            // point on the floor, its back rides forward or back along the slope as it pitches: half a metre down a
            // flight of stairs, into the steps.)
            Vector3 pivot = transform.position + forward * middleAlong;
            pivot.y = topHeight / count;
            Vector3 right = Vector3.Cross(up, forward);

            // The body pitched to the ground's slope and raised or lowered to its line. Pitched by θ about its middle, a
            // leg's top u ahead of it rises u·sin θ and comes back to u·cos θ ahead, where the line is u·cos θ·tan θ =
            // u·sin θ higher: so every leg's top stays as high above the line as animated.
            float pitchGoal = Mathf.Clamp(Mathf.Atan(slopeGoal) * Mathf.Rad2Deg, -maxBodyPitch, maxBodyPitch);
            // (Reworked below once the legs say how far each end must come down to reach.)
            float baseGoal = groundGoal;
            // Then lowered as far as a planted leg, pitched so, would have to reach further down to its paw's ground than
            // the animation reaches (a lifted paw lowers it as it comes down): the leg's top where pitching takes it,
            // and its paw where the animation has it, on its ground.
            // Each end lowered only as far as its own planted legs need: a hind paw on a low tread drops the hips, not the
            // shoulders (which would take the front legs into the steps above). The legs ahead of the middle and behind
            // it each give how much further down they'd have to reach than animated; the body is tilted and lowered
            // along the line through the two.
            Quaternion pitchTo = Quaternion.AngleAxis(-pitchGoal, right); // Turning about +right pitches the nose down.
            float plantedLift = plantedHeight * scale, liftedLift = Mathf.Max(liftedHeight * scale, plantedLift + 1e-3f);
            float reachAhead = 0f, reachBehind = 0f, alongAhead = 0f, alongBehind = 0f, topAhead = 0f, topBehind = 0f;
            int ahead = 0, behind = 0;
            for (int i = 0; i < paws.Length; i++)
            {
                Leg leg = legs[i];
                if (leg?.upper == null || leg.lower == null || leg.foot == null) { continue; }
                ref PawState paw = ref paws[i];
                paw.planted = 1f - Mathf.InverseLerp(plantedLift, liftedLift, paw.lift);
                Vector3 top = leg.upper.position;
                float animatedReach = top.y - paw.animatedContact.y;
                float pitchedTop = pivot.y + (pitchTo * (top - pivot)).y + baseGoal;
                float wantedReach = pitchedTop - (paw.animatedContact.y + paw.offset);
                float legReach = Mathf.Min(0f, (animatedReach - wantedReach) * paw.planted);
                float along = Vector3.Dot(top - transform.position, forward) - middleAlong;
                float topAboveFloor = pitchedTop - floor;
                if (along >= 0f) { reachAhead = Mathf.Min(reachAhead, legReach); alongAhead += along; topAhead += topAboveFloor; ahead++; }
                else { reachBehind = Mathf.Min(reachBehind, legReach); alongBehind += along; topBehind += topAboveFloor; behind++; }
            }
            float maxDrop = maxBodyDrop * scale;
            reachAhead = Mathf.Max(reachAhead, -maxDrop);
            reachBehind = Mathf.Max(reachBehind, -maxDrop);
            float reach = Mathf.Min(reachAhead, reachBehind);
            if (ahead > 0 && behind > 0)
            {
                alongAhead /= ahead;
                alongBehind /= behind;
                // But never so low that the legs' tops come nearer the ground under them than Leg Top Clearance (raised
                // that far, if need be): where a flight of stairs meets the landing at its top, lowering the hips for the
                // hind paws on the last steps would take the thighs into the landing's edge.
                float clearance = legTopClearance * scale;
                if (GroundNear(alongAhead, found, scale, out float groundAhead))
                {
                    reachAhead = Mathf.Max(reachAhead, groundAhead + clearance - topAhead / ahead);
                }
                if (GroundNear(alongBehind, found, scale, out float groundBehind))
                {
                    reachBehind = Mathf.Max(reachBehind, groundBehind + clearance - topBehind / behind);
                }
                float tilt = (reachAhead - reachBehind) / Mathf.Max(alongAhead - alongBehind, 0.05f * scale);
                pitchGoal = Mathf.Clamp(Mathf.Atan(slopeGoal + tilt) * Mathf.Rad2Deg, -maxBodyPitch, maxBodyPitch);
                reach = reachAhead - tilt * alongAhead; // The line through the two, under the middle.
            }
            float heightGoal = baseGoal + reach;
            bodyPitch = bodySmoothTime > 0f
                ? Mathf.SmoothDamp(bodyPitch, pitchGoal, ref bodyPitchVelocity, bodySmoothTime, Mathf.Infinity, deltaTime)
                : pitchGoal;
            // The line is found in the world each frame, so it rises smoothly up a flight of stairs, though the
            // animations' floor (the character's feet) jumps a step at a time: the body follows it as it is. Only how far
            // the legs need it lowered below it is eased, as paws plant and lift. (Easing the body's whole height would
            // trail a climb: up a steep flight it rises two metres a second, and eased over a tenth of one, it hung
            // a third of a metre below where it should be, its legs folded into the steps.)
            if (!hasBodyLevel || bodySmoothTime <= 0f)
            {
                bodyLevel = reach;
                bodyLevelVelocity = 0f;
                hasBodyLevel = true;
            }
            else
            {
                bodyLevel = Mathf.SmoothDamp(bodyLevel, reach, ref bodyLevelVelocity, bodySmoothTime, Mathf.Infinity, deltaTime);
            }
            heightGoal = baseGoal + bodyLevel;
            bodyHeight = Mathf.Clamp(heightGoal, -maxStepDown * scale, maxStepUp * scale);
            if (activeWeight <= 0f) { return; }

            // Pitched about its middle, then raised or lowered; the tail and neck turned back against the pitch.
            float pitched = bodyPitch * activeWeight;
            Quaternion pitch = Quaternion.AngleAxis(-pitched, right);
            rootBone.SetPositionAndRotation(pivot + pitch * (rootBone.position - pivot) + up * (bodyHeight * activeWeight),
                pitch * rootBone.rotation);
            foreach (CounterPitchBone counter in counterPitchBones)
            {
                float share = pitched >= 0f ? counter?.noseUpShare ?? 0f : counter?.noseDownShare ?? 0f;
                if (counter?.bone == null || share <= 0f) { continue; }
                counter.bone.rotation = Quaternion.AngleAxis(pitched * share, right) * counter.bone.rotation;
            }

            // Each leg bent to put its paw on its ground, lifted as animated, turned as animated (tilted to the ground when
            // planted).
            for (int i = 0; i < paws.Length; i++)
            {
                Leg leg = legs[i];
                if (leg?.upper == null || leg.lower == null || leg.foot == null) { continue; }
                PawState paw = paws[i];
                Quaternion tilt = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(up, paw.normal), paw.planted * alignToGround * activeWeight);
                Quaternion footRotation = tilt * paw.animatedFootRotation;
                Vector3 contact = paw.animatedContact + up * (paw.offset * activeWeight);
                Vector3 footTarget = contact - footRotation * Vector3.Scale(leg.contact, leg.foot.lossyScale);
                // The bend direction turned with the body's pitch, as the leg's joints are.
                Vector3 bend = leg.bendDirection == Vector3.zero ? Vector3.zero : pitch * transform.TransformDirection(leg.bendDirection);
                SolveTwoBone(leg.upper, leg.lower, leg.foot, footTarget, bend);
                leg.foot.rotation = footRotation;
            }
        }

        /// <summary>
        /// Finds the ground under a paw and eases toward it, and the ground's normal. Eased in the world, not from the
        /// animations' floor: that rises and falls with the character (walking up a ramp, say), and a paw planted on still
        /// ground would trail it by as much as it moved in the easing's time, standing above the ramp.
        /// </summary>
        private void FindPawGround(ref PawState paw, Vector3 joint, Vector3 middleJoint, float top, float floor, float scale, float deltaTime)
        {
            float stepUp = maxStepUp * scale, stepDown = maxStepDown * scale;
            // Under the paw's contact (its toes), under its joint (the wrist, or the hind leg's hock), and under the leg's
            // middle joint where that leans out ahead of it: the highest. A paw curls back as it swings, its joint leading,
            // and stepping up a stair the joint would cross into the next step while the toes are still over the one below.
            paw.rayOrigin = new Vector3(paw.animatedContact.x, top, paw.animatedContact.z);
            paw.hit = FindGround(paw.rayOrigin, paw.rayOrigin.y - (floor - stepDown), out RaycastHit hit);
            for (int j = 0; j < 2; j++)
            {
                Vector3 at = j == 0 ? joint : middleJoint;
                var origin = new Vector3(at.x, top, at.z);
                if (FindGround(origin, origin.y - (floor - stepDown), out RaycastHit jointHit) && (!paw.hit || jointHit.point.y > hit.point.y))
                {
                    hit = jointHit;
                    paw.hit = true;
                }
            }
            float groundGoal = floor;
            Vector3 normalGoal = Vector3.up;
            if (paw.hit)
            {
                paw.hitPoint = hit.point;
                groundGoal = hit.point.y;
                normalGoal = hit.normal;
            }
            if (!paw.hasGround || pawSmoothTime <= 0f)
            {
                paw.ground = groundGoal;
                paw.groundVelocity = 0f;
                paw.hasGround = true;
            }
            else
            {
                paw.ground = Mathf.SmoothDamp(paw.ground, groundGoal, ref paw.groundVelocity, pawSmoothTime, Mathf.Infinity, deltaTime);
            }
            // Eased, but never into the ground: a paw swinging up a slope, or onto a stair, would trail below it by as much
            // as the ground rises in the easing's time. Its animated lift keeps it that much above wherever it's placed.
            float lowest = groundGoal - Mathf.Max(paw.lift, 0f);
            if (paw.ground < lowest)
            {
                paw.ground = lowest;
                paw.groundVelocity = Mathf.Max(paw.groundVelocity, 0f);
            }
            paw.offset = Mathf.Clamp(paw.ground - floor, -stepDown, stepUp);
            paw.normal = pawSmoothTime > 0f ? Vector3.Slerp(paw.normal, normalGoal, 1f - Mathf.Exp(-deltaTime / pawSmoothTime)) : normalGoal;
        }

        /// <summary>
        /// The highest ground (m above the floor) probed under the body (this frame's body probes) within a probe's spacing
        /// of a point along it (m ahead of its middle); false if none was found there.
        /// </summary>
        private bool GroundNear(float along, int found, float scale, out float ground)
        {
            float window = 2f * bodyProbeReach * scale / (BodyProbes - 1);
            ground = float.NegativeInfinity;
            for (int k = 0; k < found; k++)
            {
                if (Mathf.Abs(probeAlong[k] - along) <= window) { ground = Mathf.Max(ground, probeHeightFound[k]); }
            }
            return ground > float.NegativeInfinity;
        }

        /// <summary>
        /// Bends a two-bone chain so its end (the end bone's start) reaches the target, or as near as the chain reaches:
        /// the middle joint placed by the law of cosines, on the side `bend` points to across the chain (or, with no
        /// bend given, the side it's bent to already), then the lower bone turned to the target.
        /// </summary>
        private void SolveTwoBone(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 bend)
        {
            Vector3 a = upper.position, b = lower.position, c = end.position;
            float upperLength = Vector3.Distance(a, b), lowerLength = Vector3.Distance(b, c);
            Vector3 toTarget = target - a;
            if (upperLength < 1e-5f || lowerLength < 1e-5f || toTarget.sqrMagnitude < 1e-10f) { return; }
            Vector3 along = toTarget.normalized;
            float reach = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(upperLength - lowerLength) + StraightLegMargin,
                upperLength + lowerLength - StraightLegMargin);

            // The side the middle joint goes, square to the chain: the bend direction, else where it is now, else across
            // the model's back.
            Vector3 side = Vector3.ProjectOnPlane(bend != Vector3.zero ? bend : b - a, along);
            if (side.sqrMagnitude < 1e-8f) { side = Vector3.ProjectOnPlane(b - a, along); }
            if (side.sqrMagnitude < 1e-8f) { side = Vector3.ProjectOnPlane(-transform.forward, along); }
            side.Normalize();

            float x = (upperLength * upperLength - lowerLength * lowerLength + reach * reach) / (2f * reach);
            float y = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - x * x));
            Vector3 middle = a + along * x + side * y;
            upper.rotation = Quaternion.FromToRotation(b - a, middle - a) * upper.rotation;
            Vector3 from = end.position - lower.position, to = a + along * reach - lower.position;
            if (from.sqrMagnitude > 1e-10f && to.sqrMagnitude > 1e-10f)
            {
                lower.rotation = Quaternion.FromToRotation(from, to) * lower.rotation;
            }
        }

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
            foreach (PawState paw in paws)
            {
                Gizmos.color = paw.hit ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.3f, 0.3f);
                Gizmos.DrawLine(paw.rayOrigin, paw.hit ? paw.hitPoint : paw.rayOrigin + Vector3.down * (maxStepUp + maxStepDown));
                if (paw.hit) { Gizmos.DrawWireSphere(paw.hitPoint, 0.02f); }
            }
        }
    }
}
