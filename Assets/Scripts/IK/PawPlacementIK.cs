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
    public partial class PawPlacementIK : MonoBehaviour, IModelHandover
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

            // The last bone of its chain (see KeepTipClear), and the bone it was found for: found once, not every frame
            // (a walk down the chain, asking each child whether it's a hitbox), and again if the bone is changed.
            [System.NonSerialized] internal Transform chainEnd, chainEndOf;
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
            // Paw lock: held where the paw was put down (see Paw Lock).
            public PlantLock plantLock;
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
        private readonly GroundProbe probe = new();
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
            ResetPaws();
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
            if (ignoreCollidersUnder == null) { ignoreCollidersUnder = GroundProbe.DefaultIgnoredRoot(this); }
            if (legs.Length > MaxLegs) { Debug.LogWarning($"[{nameof(PawPlacementIK)}] Only the first {MaxLegs} legs are placed.", this); }
            // Kept if it's been carried on from the model this one replaced (ContinueFrom, called before Awake).
            if (paws.Length != Mathf.Min(legs.Length, MaxLegs)) { ResetPaws(); }
        }

        /// <summary>
        /// A paw state for each leg placed (the first Max Legs: the buffers are sized for that many), each starting with
        /// its ground level. As Awake makes them, and SetLegs (which set as many as there were legs, uncapped, unlevelled).
        /// </summary>
        private void ResetPaws()
        {
            paws = new PawState[Mathf.Min(legs.Length, MaxLegs)];
            for (int i = 0; i < paws.Length; i++) { paws[i].normal = Vector3.up; }
        }

        /// <summary>Whether a leg has the bones a two-bone solve needs (its upper and lower bones and its paw).</summary>
        private static bool IsSolvable(Leg leg) => leg?.upper != null && leg.lower != null && leg.foot != null;

        /// <summary>
        /// Carries on from another model's paw placement, as it was this frame: how far it was faded in, the body's
        /// pitch, level and turn, and, if the two have as many legs, each paw's (its ground, offset and lock). For a model
        /// replacing another on the same character, so paws already planted stay planted rather than fading back in. Call
        /// before this one's first update.
        /// </summary>
        public void ContinueFrom(PawPlacementIK other)
        {
            if (other == null || other == this) { return; }
            activeWeight = Mathf.Min(other.activeWeight, weight);
            bodyPitch = other.bodyPitch;
            bodyPitchVelocity = other.bodyPitchVelocity;
            turnBend = other.turnBend;
            turnBendVelocity = other.turnBendVelocity;
            lastYaw = other.lastYaw;
            hasLastYaw = other.hasLastYaw;
            wallWeight = other.wallWeight;
            wallNormal = other.wallNormal;
            wallPoint = other.wallPoint;
            bodyLevel = other.bodyLevel;
            bodyLevelVelocity = other.bodyLevelVelocity;
            bodyHeight = other.bodyHeight;
            hasBodyLevel = other.hasBodyLevel;
            if (other.paws.Length == Mathf.Min(legs.Length, MaxLegs)) { paws = (PawState[])other.paws.Clone(); }
        }

        /// <summary>Carries on from the old model's paw placement, if it has one (see IModelHandover).</summary>
        void IModelHandover.ContinueFrom(GameObject oldModel) =>
            ContinueFrom(oldModel != null ? oldModel.GetComponentInChildren<PawPlacementIK>() : null);

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
                paws[i].offset = paws[i].groundVelocity = 0f;
                paws[i].hasGround = paws[i].hasLastModelAhead = false;
                paws[i].plantLock.Release();
            }
        }

        private void LateUpdate()
        {
            // Only on a pose the Animator has just written: one stepped by hand is solved by whoever steps it.
            if (animator != null && animator.isActiveAndEnabled) { Solve(CharacterAnimationStates.DeltaTimeOf(stateSource, animator)); }
        }

        /// <summary>
        /// Places the paws on the pose the Animator has just evaluated. Called each frame (LateUpdate) while the Animator
        /// plays; call it after each Animator.Update when stepping the Animator by hand.
        /// </summary>
        /// <summary>
        /// Places the paws on the pose the Animator has just evaluated. Called each frame (LateUpdate) while the Animator
        /// plays; call it after each Animator.Update when stepping the Animator by hand. In phases, each its own method
        /// (in the parts of this class), passing what they work out on in a Frame.
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
            var frame = new Frame { state = state, deltaTime = deltaTime, scale = scale, floor = floor, up = up, forward = forward };

            if (!SampleAnimatedPaws(ref frame)) { return; }
            FitGroundLine(ref frame);
            PlacePawsOnTheirGround(ref frame);
            PitchAndLevelBody(ref frame);
            if (activeWeight <= 0f)
            {
                // Off the ground, nothing stays locked: landing, the paws lock where they come down.
                for (int i = 0; i < paws.Length; i++) { paws[i].plantLock.Release(); }
                SolveWallRun(state, floor, scale, deltaTime);
                return;
            }
            Quaternion pitch = PoseBody(ref frame);
            SolveLegs(ref frame, pitch);
            SolveWallRun(state, floor, scale, deltaTime);
        }

        /// <summary>What Solve's phases work out this frame, handed from each to the next.</summary>
        private struct Frame
        {
            public CharacterAnimationState state;
            public float deltaTime, scale, floor, probeTop;
            public Vector3 up, forward, right, pivot;
            // How far ahead of the model's origin the legs' tops are on average, and how many legs are placed.
            public float middleAlong;
            public int count;
            // The ground's line under the body: its slope, and height above the floor under the body's middle; and how many
            // body probes found ground.
            public float slopeGoal, groundGoal;
            public int found;
            // The pitch the slope calls for (degrees), and the height the body's raised to its line before the legs' reach.
            public float pitchGoal, baseGoal;
        }

        private bool FindGround(Vector3 origin, float distance, out RaycastHit closest) =>
            probe.Find(origin, distance, groundLayers, ignoreCollidersUnder, maxSlopeAngle, out closest);

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
