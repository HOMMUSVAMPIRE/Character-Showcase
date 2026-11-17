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
    /// - holds each planted paw where it was put down (Paw Lock), so the model turning after the character, about its feet,
    ///   doesn't drag the paws round with it;
    /// - bends the body into a turn (Turn Bend Bones), as fast as the model turns;
    /// - running along a wall, rolls the body toward it until its legs reach it, its paws on the wall's face;
    /// - shortens and quickens the strides on a slope (Stride Shortening), and puts each paw down on the nearest foothold
    ///   that keeps its leg nearest its animated length (Foothold Reach).
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
            [Tooltip("How far (m, for the model at its authored size) the paw's mesh reaches beyond its contact toward a wall " +
                     "it's put on, running along one (its pad and toes, the paw rolled to the wall): it's put that far out " +
                     "from the wall's face.")]
            [Min(0f)] public float wallClearance = 0.03f;
        }

        /// <summary>A bone turned toward the way the creature turns, by a share of the bend (see Turn Bend Bones).</summary>
        [System.Serializable]
        public class TurnBendBone
        {
            [Tooltip("The bone (a spine bone, the neck, the head, the tail's): it and everything under it turn about it.")]
            public Transform bone;
            [Tooltip("Its share of the bend: positive toward the turn (the spine, neck and head), negative away from it (the " +
                     "tail, so its tip swings round toward the turn too, the body a curve).")]
            [Range(-1f, 1f)] public float share = 0.25f;
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
            [Tooltip("How far (m, for the model at its authored size) the tip of the bone's chain (its last bone under it: " +
                     "the tail's tip) is kept above the ground under it, once pitched: it's turned up about the bone as far " +
                     "as that takes. Going down stairs, the steps behind rise faster than the pitched body lifts a long tail. " +
                     "0: not kept")]
            [Min(0f)] public float groundClearance;
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
        [Tooltip("How far (m, for the model at its authored size) ahead of a swinging paw's toes the ground is looked for: a " +
                 "paw swinging up a flight of stairs is lifted over the next step's edge as it comes to it, rather than " +
                 "through its riser. Only ever raises a paw, and only as it swings (not planted). 0 doesn't look ahead")]
        [Min(0f)] [SerializeField] private float swingLookAhead = 0.1f;
        // How much higher (m, at the authored size) than the slope under a swinging paw's toes carries on to, the ground
        // ahead of them must be to be a step it lifts over (see Swing Look Ahead): more than a bump's rounding.
        private const float SwingStepThreshold = 0.02f;
        [Tooltip("Animated paw lift (m above the floor, for the model at its authored size) below which a paw counts as " +
                 "planted, so it tilts to the ground.")]
        [Min(0f)] [SerializeField] private float plantedHeight = 0.02f;
        [Tooltip("Animated paw lift (m, for the model at its authored size) above which a paw counts as lifted, so it keeps " +
                 "its animated turn.")]
        [Min(0f)] [SerializeField] private float liftedHeight = 0.1f;
        [Tooltip("How fast (m/s, for the model at its authored size) the animation carries a paw forward, relative to the " +
                 "model, for it to count as lifted, however low it is: a planted paw goes back as the body passes over it, " +
                 "a swinging one forward. (A hind paw swings with its toes curled down, barely off the ground.)")]
        [Min(0.001f)] [SerializeField] private float swingSpeed = 0.3f;
        [Tooltip("How fast (m/s, for the model at its authored size) the lowest a paw has been lately forgets: a paw's lift " +
                 "is measured from there (a paw whose stance isn't quite at the floor still counts as planted in it), and " +
                 "a stride or two later it's forgotten.")]
        [Min(0f)] [SerializeField] private float liftBaselineRecovery = 0.1f;
        [Tooltip("How strongly planted paws tilt to the ground's slope.")]
        [Range(0f, 1f)] [SerializeField] private float alignToGround = 1f;

        [Header("Turning")]
        [Tooltip("Bones turned toward the way the creature is turning, each by a share of the bend: the spine and neck " +
                 "toward the turn, the tail (a negative share) the other way, so the body curves into it. Each turns " +
                 "everything under it, so the shares add up along the spine.")]
        [SerializeField] private TurnBendBone[] turnBendBones = new TurnBendBone[0];
        [Tooltip("The most (degrees) the body is bent into a turn.")]
        [Range(0f, 90f)] [SerializeField] private float maxTurnBend = 25f;
        [Tooltip("How fast (degrees a second) the model turning bends the body fully.")]
        [Min(1f)] [SerializeField] private float fullBendTurnSpeed = 150f;
        [Tooltip("Seconds to ease the bend in and out as the turning starts and stops.")]
        [Range(0f, 0.5f)] [SerializeField] private float turnBendSmoothTime = 0.12f;

        [Header("Wall running")]
        [Tooltip("The furthest (degrees) the body is rolled toward a wall it runs along, for its legs to reach the wall.")]
        [Range(0f, 90f)] [SerializeField] private float maxWallRoll = 45f;
        [Tooltip("Seconds to roll the body to the wall as a wall run starts, and back as it ends.")]
        [Min(0.001f)] [SerializeField] private float wallRollBlendTime = 0.12f;

        [Header("Strides on slopes")]
        [Tooltip("How much shorter the strides are on a slope, by the sine of the body's pitch (0.7 takes about two fifths " +
                 "off them up or down a 37° flight of stairs), and how much quicker, so the paws still keep pace with the " +
                 "ground: the Animator plays that much faster while the creature moves. A planted paw stays put while the " +
                 "body goes on over it the length of a stance, rising or falling as far along a slope, which a leg can't " +
                 "stretch or fold to. 0 = off.")]
        [Range(0f, 1f)] [SerializeField] private float strideShortening = 0.7f;
        [Tooltip("The shortest the strides get on a slope, as a share of their length on the flat.")]
        [Range(0.1f, 1f)] [SerializeField] private float shortestStride = 0.5f;
        [Tooltip("The speed (m/s, for the model at its authored size) at which the creature counts as fully moving, so its " +
                 "strides are fully shortened on a slope (standing, its animation plays as it is).")]
        [Min(0.01f)] [SerializeField] private float strideMovingSpeed = 0.5f;

        [Header("Footholds")]
        [Tooltip("How far (m, for the model at its authored size) ahead of or behind where the animation puts a paw down it " +
                 "may be put instead, onto ground nearer the body's line, so its leg stays nearer the length the animation " +
                 "gives it. On a flight of stairs, a paw put down at the back of a tread, a riser below the steps' edges, is " +
                 "out of its leg's reach, and one by an edge folds its leg up under the body; moved, it stands where the " +
                 "leg reaches. On even ground every foothold is as good, and the paw is put where the animation has it. 0 = off.")]
        [Min(0f)] [SerializeField] private float footholdReach = 0.12f;
        [Tooltip("Seconds to ease a paw from one foothold to another as it swings (a planted paw stays where it was put down).")]
        [Range(0f, 0.3f)] [SerializeField] private float footholdSmoothTime = 0.06f;

        [Header("Paw locking")]
        [Tooltip("How firmly a planted paw stays where it was put down, rather than where the animation has it now. The " +
                 "model turns after the character, about its feet, which would otherwise drag planted paws round with it " +
                 "(a long body's paws furthest of all), and any mismatch between a stride and the character's speed slides " +
                 "them. 0 = off.")]
        [Range(0f, 1f)] [SerializeField] private float pawLock = 1f;
        [Tooltip("How far (m, for the model at its authored size) the animation's paw can get from where a planted paw was " +
                 "locked before the lock lets go, so the leg is never stretched to hold it.")]
        [Min(0f)] [SerializeField] private float maxLockDistance = 0.15f;
        [Tooltip("Seconds a paw's lock takes to take hold, and to let go as the paw lifts, so it never pops.")]
        [Min(0.001f)] [SerializeField] private float lockBlendTime = 0.08f;

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
            // Where the animation had the paw, ahead of the model's origin, last frame (and whether it had one), and how
            // fast it carries it forward.
            public float lastModelAhead, forwardSpeed;
            // The lowest the animation has had the paw lately (see Lift Baseline Recovery): its lift is measured from there.
            public float lowestLift;
            // How far across the ground the paw is moved with its leg's top as the body pitches; and how far ahead (or
            // behind, negative) of there it's put down to stand on a better foothold (see Foothold Reach), eased.
            public Vector3 shift;
            public float foothold, footholdVelocity;
            public bool hasLastModelAhead;
            // Debug
            public Vector3 rayOrigin, hitPoint;
            public bool hit;
            // The ground under the paw this frame (null if none), which a lock holds it in the space of.
            public Transform groundTransform;
            // What the paw stood on last frame, and how high it was then: its eased ground is carried along with it.
            public Transform lastGroundTransform;
            public float lastGroundHeight;
            // Paw lock: whether the paw is locked, where (in the space of the ground it was put down on, or the world if
            // none), and how far the lock is blended in.
            public bool locked;
            public Transform lockGround;
            public Vector3 lockPoint;
            public float lockWeight;
        }

        private Animator animator;
        // Where the character's grounded state comes from (its AnimationController, found in the model's parents).
        // Without one, the paws are always placed.
        private ICharacterAnimationStateSource stateSource;
        private PawState[] paws = new PawState[0];
        private float activeWeight;
        private float bodyPitch, bodyPitchVelocity;
        // The bend into a turn (degrees, eased), and the model's facing last frame, which its turn rate is measured from.
        private float turnBend, turnBendVelocity, lastYaw;
        // How far the body is rolled to a wall it runs along (0 to 1, eased), and the wall (kept as the run ends, to roll
        // back from it).
        private float wallWeight;
        private Vector3 wallNormal, wallPoint;
        private bool hasLastYaw;
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

        /// <summary>Sets the bones bent into a turn (the generator does).</summary>
        public void SetTurnBendBones(TurnBendBone[] bones) => turnBendBones = bones ?? new TurnBendBone[0];

        /// <summary>How far (0 to 1) the body is rolled to a wall it runs along now.</summary>
        public float WallRoll => wallWeight;

        /// <summary>How far (degrees, positive turning right) the body is bent into a turn now.</summary>
        public float TurnBend => turnBend;

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
            if (animator != null) { animator.speed = 1f; }
            turnBend = turnBendVelocity = wallWeight = 0f;
            hasLastYaw = false;
            activeWeight = 0f;
            bodyPitch = bodyPitchVelocity = bodyHeight = bodyLevelVelocity = 0f;
            hasBodyLevel = false;
            for (int i = 0; i < paws.Length; i++)
            {
                paws[i].offset = paws[i].groundVelocity = paws[i].lockWeight = 0f;
                paws[i].hasGround = paws[i].locked = paws[i].hasLastModelAhead = false;
            }
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
            // On the ground only: swimming, the paddle plays as animated; wall-running, the body is rolled to the wall
            // (see SolveWallRun).
            float goal = state.grounded && !state.swimming && !state.wallRunning ? weight : 0f;
            activeWeight = groundedFadeTime > 0f ? Mathf.MoveTowards(activeWeight, goal, deltaTime / groundedFadeTime) : goal;

            BendIntoTurn(deltaTime);

            float scale = Mathf.Max(transform.lossyScale.y, 1e-4f);
            float floor = transform.position.y; // The animations' floor: the model's origin, at the character's feet.
            Vector3 up = Vector3.up, forward = Vector3.ProjectOnPlane(transform.forward, up).normalized;
            if (forward == Vector3.zero) { forward = Vector3.forward; }

            // Each paw as the animation has it (its ground is found once the body's pitch is known: see below).
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
                // How high the animation lifts the paw: above the floor, and above the lowest it's had it lately, which a
                // paw's stance may not quite be at the floor (a hind paw rolls a little onto its toes as it pushes off).
                float aboveFloor = paw.animatedContact.y - floor;
                paw.lowestLift = paw.hasLastModelAhead
                    ? Mathf.Min(aboveFloor, paw.lowestLift + liftBaselineRecovery * scale * deltaTime)
                    : aboveFloor;
                paw.lift = Mathf.Min(aboveFloor, aboveFloor - Mathf.Max(paw.lowestLift, 0f));
                float modelAhead = transform.InverseTransformPoint(paw.animatedContact).z;
                paw.forwardSpeed = paw.hasLastModelAhead && deltaTime > 0f ? (modelAhead - paw.lastModelAhead) / deltaTime : 0f;
                paw.lastModelAhead = modelAhead;
                paw.hasLastModelAhead = true;
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

            // Each paw moved with its leg's top as the body pitches (its shoulder or hip, swung about the body's middle,
            // comes back or forward along the slope), so the legs keep the stance the animation gives them, upright under
            // the body; and its ground found there, probed from inside the character's body. (Left where the flat-ground
            // animation puts them, the front paws stood up a slope ahead of the shoulders, on higher ground, their legs
            // folded, and the hind paws behind the hips, on lower, their legs stretched short of it.)
            Quaternion slopePitch = Quaternion.AngleAxis(-pitchGoal, right); // Turning about +right pitches the nose down.
            // Shorter, quicker strides on a slope (see Stride Shortening): each paw drawn in toward its leg's top along the
            // way the body faces, and the Animator played as much faster (from the next frame), while moving.
            float moving = Mathf.Clamp01(state.horizontalSpeed / (strideMovingSpeed * scale));
            float stride = Mathf.Lerp(1f, Mathf.Max(shortestStride, 1f - strideShortening * Mathf.Abs(Mathf.Sin(bodyPitch * Mathf.Deg2Rad))), moving * activeWeight);
            if (animator != null) { animator.speed = 1f / stride; }
            for (int i = 0; i < paws.Length; i++)
            {
                Leg leg = legs[i];
                if (leg?.upper == null || leg.lower == null || leg.foot == null) { continue; }
                ref PawState paw = ref paws[i];
                Vector3 top = leg.upper.position;
                paw.shift = Vector3.ProjectOnPlane(pivot + slopePitch * (top - pivot) - top, up)
                            + forward * ((stride - 1f) * Vector3.Dot(paw.animatedContact - top, forward));
                // The ground under the paw's toes, and under the leg's middle joint (knee or elbow) where that leads the
                // toes: a leg leaning forward over an edge (a hind leg, its paw on the last step of a flight as the body
                // goes on onto the landing) puts its paw up on the edge's top, rather than its shin through the edge.
                // Likewise the paw's own joint (wrist or hock) only where it leads the toes: a paw curls back as it swings,
                // its joint ahead, and up a stair the joint would cross into the next step while the toes are over the one
                // below. Behind the toes (a planted paw's wrist, going down a flight, over the step it's come from), it
                // would lift the paw onto that step, folding the leg up under the body.
                Vector3 animated = paw.animatedContact;
                ChooseFoothold(ref paw, animated + paw.shift, leg.foot.position - animated, leg.lower.position - animated, forward,
                    floor + groundGoal, slopeGoal, middleAlong, probeTop, floor, scale, deltaTime);
                Vector3 contact = animated + paw.shift + forward * paw.foothold;
                // A joint behind the toes only needs to clear the ground under it: the paw goes no lower than that
                // ground less the joint's height above the paw (going down a flight, a hind paw put down by the riser
                // behind it is raised only so far as keeps its hock out of the step above, not up onto that step, which
                // folded the leg up under the hips).
                Vector3 moved = paw.shift + forward * paw.foothold;
                Vector3 pawJoint = leg.foot.position + moved, middleJoint = leg.lower.position + moved;
                float pawJointClear = Vector3.Dot(leg.foot.position - animated, forward) > 0f ? 0f : Mathf.Max(leg.foot.position.y - animated.y, 0f);
                float middleJointClear = Vector3.Dot(leg.lower.position - animated, forward) > 0f ? 0f : Mathf.Max(leg.lower.position.y - animated.y, 0f);
                // Where a swinging paw is going: as far ahead of its toes as it's lifted (not planted, as last placed).
                Vector3 lookAhead = forward * (swingLookAhead * scale * (1f - paw.planted));
                FindPawGround(ref paw, contact, pawJoint, pawJointClear, middleJoint, middleJointClear, lookAhead, probeTop, floor, scale, deltaTime);
            }
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
                // Planted while low and not swinging forward (its speed is in the model's own units, as its scale is 1).
                paw.planted = Mathf.Min(1f - Mathf.InverseLerp(plantedLift, liftedLift, paw.lift),
                                        1f - Mathf.InverseLerp(0f, swingSpeed, paw.forwardSpeed));
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
                // Lowered under the middle as far as either end needs, with the tilt the pitch can take (Max Body Pitch
                // may not let it take all of it: on a steep slope, lowering the middle for a tilt it didn't get dropped
                // the front end onto its folded legs and left the hind legs stretched short of the ground).
                float applied = Mathf.Tan(pitchGoal * Mathf.Deg2Rad) - slopeGoal;
                reach = Mathf.Min(reachAhead - applied * alongAhead, reachBehind - applied * alongBehind);
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
            if (activeWeight <= 0f)
            {
                // Off the ground, nothing stays locked: landing, the paws lock where they come down.
                for (int i = 0; i < paws.Length; i++) { paws[i].locked = false; paws[i].lockWeight = 0f; }
                SolveWallRun(state, floor, scale, deltaTime);
                return;
            }

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
            foreach (CounterPitchBone counter in counterPitchBones)
            {
                if (counter?.bone == null || counter.groundClearance <= 0f) { continue; }
                KeepTipClear(counter.bone, counter.groundClearance * scale * activeWeight, right, up, forward);
            }

            // Each leg bent to put its paw on its ground, lifted as animated, turned as animated (tilted to the ground when
            // planted).
            for (int i = 0; i < paws.Length; i++)
            {
                Leg leg = legs[i];
                if (leg?.upper == null || leg.lower == null || leg.foot == null) { continue; }
                ref PawState paw = ref paws[i];
                Quaternion tilt = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(up, paw.normal), paw.planted * alignToGround * activeWeight);
                Quaternion footRotation = tilt * paw.animatedFootRotation;
                // Locked (or not) fully placed, then faded in over the animation's paw, so a paw put down while placement
                // fades in (landing) is locked where placement puts it, not where it is part of the way there.
                Vector3 contact = Vector3.Lerp(paw.animatedContact, LockPaw(ref paw, paw.animatedContact + paw.shift + forward * paw.foothold + up * paw.offset, scale, deltaTime), activeWeight);
                Vector3 footTarget = contact - footRotation * Vector3.Scale(leg.contact, leg.foot.lossyScale);
                // The bend direction turned with the body's pitch, as the leg's joints are.
                Vector3 bend = leg.bendDirection == Vector3.zero ? Vector3.zero : pitch * transform.TransformDirection(leg.bendDirection);
                SolveTwoBone(leg.upper, leg.lower, leg.foot, footTarget, bend);
                leg.foot.rotation = footRotation;
            }
            SolveWallRun(state, floor, scale, deltaTime);
        }

        /// <summary>
        /// Running along a wall: the body rolled toward the wall (about its legs' tops, along the way it faces) as far as
        /// its legs need to reach the wall (Max Wall Roll at most), eased in and out over Wall Roll Blend Time; and each
        /// paw kept out of the wall, put on its face where the animation has it through it, and drawn onto it as it's
        /// planted (low, in the animation), so the creature runs on the wall, paws to it.
        /// </summary>
        private void SolveWallRun(CharacterAnimationState state, float floor, float scale, float deltaTime)
        {
            bool running = state.wallRunning && state.wallNormal != Vector3.zero;
            if (running)
            {
                wallNormal = state.wallNormal;
                wallPoint = state.wallPoint;
            }
            wallWeight = wallRollBlendTime > 0f ? Mathf.MoveTowards(wallWeight, running ? 1f : 0f, deltaTime / wallRollBlendTime) : running ? 1f : 0f;
            if (wallWeight <= 0f || wallNormal == Vector3.zero) { return; }

            // Where the legs' tops are, and how long the legs are as animated (to their paws).
            Vector3 pivot = Vector3.zero;
            float legLength = 0f;
            int count = 0;
            for (int i = 0; i < paws.Length; i++)
            {
                Leg leg = legs[i];
                if (leg?.upper == null || leg.lower == null || leg.foot == null) { continue; }
                pivot += leg.upper.position;
                legLength += Vector3.Distance(leg.upper.position, leg.foot.TransformPoint(leg.contact));
                count++;
            }
            if (count == 0) { return; }
            pivot /= count;
            legLength /= count;

            // Rolled so the legs, hanging from their tops, reach the wall: the roll whose sine is the wall's distance
            // over the legs' length. Toward the wall: the body's down turned to point at it.
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, wallNormal).normalized;
            if (forward == Vector3.zero) { return; }
            float distance = Vector3.Dot(pivot - wallPoint, wallNormal);
            float roll = Mathf.Min(Mathf.Asin(Mathf.Clamp01(distance / Mathf.Max(legLength, 1e-3f))) * Mathf.Rad2Deg, maxWallRoll) * wallWeight;
            if (Vector3.Dot(Quaternion.AngleAxis(roll, forward) * Vector3.down, wallNormal) > 0f) { roll = -roll; }
            Quaternion rolled = Quaternion.AngleAxis(roll, forward);

            // Each paw's lift as animated (before the roll), for how planted it is.
            float plantedLift = plantedHeight * scale, liftedLift = Mathf.Max(liftedHeight * scale, plantedLift + 1e-3f);
            for (int i = 0; i < paws.Length; i++)
            {
                Leg leg = legs[i];
                if (leg?.foot == null) { continue; }
                paws[i].lift = leg.foot.TransformPoint(leg.contact).y - floor;
            }
            rootBone.SetPositionAndRotation(pivot + rolled * (rootBone.position - pivot), rolled * rootBone.rotation);

            for (int i = 0; i < paws.Length; i++)
            {
                Leg leg = legs[i];
                if (leg?.upper == null || leg.lower == null || leg.foot == null) { continue; }
                Vector3 contact = leg.foot.TransformPoint(leg.contact);
                // Out from the wall's face (less the paw's thickness beyond its contact, which would go through it, the
                // paw rolled to face the wall); negative, through it.
                float off = Vector3.Dot(contact - wallPoint, wallNormal) - leg.wallClearance * scale;
                float planted = 1f - Mathf.InverseLerp(plantedLift, liftedLift, paws[i].lift);
                float onto = off < 0f ? off : off * planted * wallWeight;
                if (Mathf.Abs(onto) < 1e-4f) { continue; }
                Vector3 target = contact - wallNormal * onto;
                Quaternion footRotation = leg.foot.rotation;
                Vector3 footTarget = target - footRotation * Vector3.Scale(leg.contact, leg.foot.lossyScale);
                Vector3 bend = leg.bendDirection == Vector3.zero ? Vector3.zero : rolled * transform.TransformDirection(leg.bendDirection);
                SolveTwoBone(leg.upper, leg.lower, leg.foot, footTarget, bend);
                leg.foot.rotation = footRotation;
            }
        }

        /// <summary>
        /// Finds the ground under a paw and eases toward it, and the ground's normal. Eased in the world, not from the
        /// animations' floor: that rises and falls with the character (walking up a ramp, say), and a paw planted on still
        /// ground would trail it by as much as it moved in the easing's time, standing above the ramp.
        /// </summary>
        private void FindPawGround(ref PawState paw, Vector3 contact, Vector3 joint, float jointClear, Vector3 middleJoint,
            float middleJointClear, Vector3 lookAhead, float top, float floor, float scale, float deltaTime)
        {
            float stepUp = maxStepUp * scale, stepDown = maxStepDown * scale;
            // Under the paw's contact (its toes), and under its joint and the leg's middle joint, less how high above the
            // paw each may stay (see Solve): the highest.
            paw.rayOrigin = new Vector3(contact.x, top, contact.z);
            paw.hit = FindGround(paw.rayOrigin, paw.rayOrigin.y - (floor - stepDown), out RaycastHit hit);
            float groundGoal = paw.hit ? hit.point.y : floor;
            for (int j = 0; j < 2; j++)
            {
                Vector3 at = j == 0 ? joint : middleJoint;
                float clear = j == 0 ? jointClear : middleJointClear;
                var origin = new Vector3(at.x, top, at.z);
                if (FindGround(origin, origin.y - (floor - stepDown), out RaycastHit jointHit) && (!paw.hit || jointHit.point.y - clear > groundGoal))
                {
                    groundGoal = jointHit.point.y - clear;
                    if (!paw.hit) { hit = jointHit; }
                    paw.hit = true;
                }
            }
            // Swinging, the ground just ahead of its toes too, if it's a step up: higher than the ground under the toes,
            // carried on along its slope, reaches there (the next step's edge, which it's coming up onto; not a ramp's rise).
            if (lookAhead.sqrMagnitude > 1e-10f)
            {
                var ahead = new Vector3(contact.x + lookAhead.x, top, contact.z + lookAhead.z);
                float alongSlope = paw.hit && hit.normal.y > 0.1f ? -(hit.normal.x * lookAhead.x + hit.normal.z * lookAhead.z) / hit.normal.y : 0f;
                float stepAbove = Mathf.Max(groundGoal, (paw.hit ? hit.point.y : groundGoal) + alongSlope) + SwingStepThreshold * scale;
                if (FindGround(ahead, ahead.y - (floor - stepDown), out RaycastHit aheadHit) && aheadHit.point.y > stepAbove)
                {
                    groundGoal = aheadHit.point.y;
                    if (!paw.hit) { hit = aheadHit; paw.hit = true; }
                }
            }
            Vector3 normalGoal = Vector3.up;
            paw.groundTransform = null;
            if (paw.hit)
            {
                paw.hitPoint = hit.point;
                paw.groundTransform = hit.transform;
                normalGoal = hit.normal;
            }
            // Carried with what it stands on (a moving platform), so the easing smooths only the ground changing under the
            // paw, not the platform's travel, which it would trail.
            if (paw.hasGround && paw.groundTransform != null && paw.groundTransform == paw.lastGroundTransform)
            {
                paw.ground += paw.groundTransform.position.y - paw.lastGroundHeight;
            }
            paw.lastGroundTransform = paw.groundTransform;
            paw.lastGroundHeight = paw.groundTransform != null ? paw.groundTransform.position.y : 0f;
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

        // How much better (m nearer the body's line) a foothold must be than where the animation has the paw, for each
        // metre it's moved, for the paw to be moved there: on even ground, where every foothold is as good, it stays put.
        private const float FootholdMovePenalty = 0.25f;
        // How far (m, for the model at its authored size) ahead of its contact a paw's toes reach as it rolls onto them to
        // push off: a foothold with the ground higher there is against a riser.
        private const float ToeClearance = 0.1f;

        /// <summary>
        /// Bends the body into the way the model is turning (it turns after the character): each of Turn Bend Bones
        /// turned about the model's up by its share of the bend, eased, as far as Max Turn Bend at Full Bend Turn Speed.
        /// Before the legs are placed, so they're placed from the bent body.
        /// </summary>
        private void BendIntoTurn(float deltaTime)
        {
            float yaw = transform.eulerAngles.y;
            float rate = hasLastYaw && deltaTime > 0f ? Mathf.DeltaAngle(lastYaw, yaw) / deltaTime : 0f;
            lastYaw = yaw;
            hasLastYaw = true;
            float goal = Mathf.Clamp(rate / fullBendTurnSpeed, -1f, 1f) * maxTurnBend;
            turnBend = turnBendSmoothTime > 0f
                ? Mathf.SmoothDamp(turnBend, goal, ref turnBendVelocity, turnBendSmoothTime, Mathf.Infinity, deltaTime)
                : goal;
            if (Mathf.Abs(turnBend) < 1e-3f) { return; }
            Vector3 up = transform.up;
            foreach (TurnBendBone bend in turnBendBones)
            {
                if (bend?.bone == null || bend.share == 0f) { continue; }
                bend.bone.rotation = Quaternion.AngleAxis(turnBend * bend.share, up) * bend.bone.rotation;
            }
        }

        /// <summary>
        /// Chooses where a paw is put down (paw.foothold, m ahead of where the animation has it, eased): there, Foothold
        /// Reach ahead or behind, whichever has its ground nearest the body's line (the line at lineAtMiddle under the
        /// body's middle, rising slope per metre ahead), so the leg stays nearest the length the animation gives it.
        /// </summary>
        private void ChooseFoothold(ref PawState paw, Vector3 at, Vector3 toJoint, Vector3 toMiddleJoint, Vector3 forward,
            float lineAtMiddle, float slope, float middleAlong, float top, float floor, float scale, float deltaTime)
        {
            float reach = footholdReach * scale;
            float goal = 0f;
            if (reach > 0f)
            {
                float best = float.PositiveInfinity, depth = top - (floor - maxStepDown * scale);
                for (int k = -1; k <= 1; k++)
                {
                    float offset = k * reach;
                    Vector3 point = at + forward * offset;
                    if (!FindGround(new Vector3(point.x, top, point.z), depth, out RaycastHit hit)) { continue; }
                    float along = Vector3.Dot(point - transform.position, forward) - middleAlong;
                    float cost = Mathf.Abs(hit.point.y - (lineAtMiddle + slope * along)) + FootholdMovePenalty * Mathf.Abs(offset);
                    // Not where the leg's joints would be in the ground there (going down a flight, a hind paw put down by
                    // the riser behind it has its hock in the step above).
                    // Nor with its toes against a riser ahead (up a flight, a paw put down at the back of a tread pokes its
                    // toes into the step above).
                    if (InGround(point, toJoint, hit.point.y, top, depth) || InGround(point, toMiddleJoint, hit.point.y, top, depth)
                        || InGround(point, forward * (ToeClearance * scale), hit.point.y, top, depth))
                    {
                        cost += 1f;
                    }
                    if (cost < best) { best = cost; goal = offset; }
                }
            }
            // A paw put down stays where it was (its lock holds it); a swinging one eases to its next foothold.
            if (paw.planted >= 0.5f && paw.hasGround) { return; }
            paw.foothold = footholdSmoothTime > 0f
                ? Mathf.SmoothDamp(paw.foothold, goal, ref paw.footholdVelocity, footholdSmoothTime, Mathf.Infinity, deltaTime)
                : goal;
        }

        /// <summary>
        /// Whether a joint, its animated offset from the paw (toJoint) above a paw put down at point, on ground at
        /// groundHeight, would be in the ground there.
        /// </summary>
        private bool InGround(Vector3 point, Vector3 toJoint, float groundHeight, float top, float depth)
        {
            Vector3 joint = point + toJoint;
            joint.y = groundHeight + Mathf.Max(toJoint.y, 0f);
            return FindGround(new Vector3(joint.x, top, joint.z), depth, out RaycastHit hit) && hit.point.y > joint.y;
        }

        /// <summary>
        /// Holds a planted paw where it was put down: locked as the animation plants it, held there (in the space of the
        /// ground under it, so it rides moving ground) while the animation keeps it planted, and let go as it lifts, or if
        /// the animation's paw gets further than Max Lock Distance from the lock. The lock is blended in and out over the
        /// animation's paw. Its height follows the ground where it was put down (and the animation's lift above it), so on a
        /// slope it stays on the ground there.
        /// </summary>
        private Vector3 LockPaw(ref PawState paw, Vector3 placed, float scale, float deltaTime)
        {
            if (pawLock <= 0f) { return placed; }
            if (paw.locked)
            {
                Vector3 held = LockPosition(paw);
                Vector2 apart = new Vector2(held.x - placed.x, held.z - placed.z);
                if (paw.planted < 0.5f || apart.magnitude > maxLockDistance * scale) { paw.locked = false; }
            }
            else if (paw.planted >= 0.999f && paw.lockWeight <= 0f)
            {
                // Locked at the paw's place across the ground, on the ground there.
                var groundPoint = new Vector3(placed.x, paw.ground, placed.z);
                paw.locked = true;
                paw.lockGround = paw.groundTransform;
                paw.lockPoint = paw.lockGround != null ? paw.lockGround.InverseTransformPoint(groundPoint) : groundPoint;
            }
            paw.lockWeight = Mathf.MoveTowards(paw.lockWeight, paw.locked ? 1f : 0f, deltaTime / lockBlendTime);
            if (paw.lockWeight <= 0f) { return placed; }
            // Held where it was put down, as high above the ground there as the animation has it above its ground now: on
            // a slope, the ground where the animation has the paw now isn't as high as where it was put down.
            Vector3 lockAt = LockPosition(paw);
            lockAt.y += placed.y - paw.ground;
            return Vector3.Lerp(placed, lockAt, paw.lockWeight * pawLock);
        }

        /// <summary>Where a paw's lock is now, in the world (it moves with the ground it was put down on).</summary>
        private static Vector3 LockPosition(in PawState paw) =>
            paw.lockGround != null ? paw.lockGround.TransformPoint(paw.lockPoint) : paw.lockPoint;

        /// <summary>Whether a leg's paw is locked where it was put down now (see Paw Lock), and how far the lock is blended in.</summary>
        public float LockWeight(int leg) => leg >= 0 && leg < paws.Length && paws[leg].locked ? paws[leg].lockWeight : 0f;

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

        /// <summary>
        /// Turns the bone up (about right, at the bone) as far as keeps the end of its chain (past its first child's first
        /// child's ..., its last bone) clearance above the ground under it, keeping how far the end is from it.
        /// </summary>
        private void KeepTipClear(Transform bone, float clearance, Vector3 right, Vector3 up, Vector3 forward)
        {
            Transform last = bone;
            for (Transform next = FirstBone(last); next != null; next = FirstBone(last)) { last = next; }
            if (last == bone) { return; }
            // The chain's end: as far past its last bone as that is past the one before (a bone's origin is where it
            // starts, and the last one's mesh reaches on past it: the tail's tip).
            Vector3 end = last.position + (last.position - last.parent.position);
            Vector3 toTip = Vector3.ProjectOnPlane(end - bone.position, right);
            float reach = toTip.magnitude;
            if (reach < 1e-4f) { return; }
            bool hasGround = FindGround(end + up * reach, 2f * reach + clearance, out RaycastHit ground);
            if (!hasGround) { return; }
            float shortfall = ground.point.y + clearance - end.y;
            if (shortfall <= 0f) { return; }
            float height = Vector3.Dot(toTip, up);
            float raised = Mathf.Asin(Mathf.Clamp(Mathf.Min(height + shortfall, reach) / reach, -1f, 1f))
                           - Mathf.Asin(Mathf.Clamp(height / reach, -1f, 1f));
            // About right, a positive turn raises what's behind the bone; what's ahead of it, the other way.
            float sign = Vector3.Dot(toTip, forward) < 0f ? 1f : -1f;
            bone.rotation = Quaternion.AngleAxis(sign * raised * Mathf.Rad2Deg, right) * bone.rotation;
        }

        /// <summary>The bone's first child that's a bone (not its hitbox: see Hitbox), or null.</summary>
        private static Transform FirstBone(Transform bone)
        {
            for (int i = 0; i < bone.childCount; i++)
            {
                Transform child = bone.GetChild(i);
                if (!child.TryGetComponent(out Hitbox _)) { return child; }
            }
            return null;
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
            if (best == float.PositiveInfinity) { return false; }
            // Where the ground is drawn, not where physics has it: a moving platform's Rigidbody is interpolated, drawn
            // between its physics steps, as the character is, while a ray hits it at its latest step; placed there, the
            // paws jumped up and down against it each frame.
            Rigidbody moving = closest.rigidbody;
            if (moving != null && moving.interpolation != RigidbodyInterpolation.None)
            {
                Transform drawn = moving.transform;
                closest.point = drawn.position + drawn.rotation * Quaternion.Inverse(moving.rotation) * (closest.point - moving.position);
            }
            return true;
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
