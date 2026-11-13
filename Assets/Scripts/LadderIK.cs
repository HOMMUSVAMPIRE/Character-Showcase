using UnityEngine;

namespace Character
{
    /// <summary>
    /// Climbs a ladder procedurally: puts the hands and feet on its rungs, over a climbing pose (the Animator's Ladder
    /// state), and moves them rung to rung as the character climbs, in diagonal pairs (the left hand with the right foot,
    /// then the right hand with the left foot), and moves the body in close to the ladder.
    ///
    /// Where each limb is depends only on how high the character is up the ladder, so it's the same climbing up or down,
    /// at any speed, and holding still holds every rung: each limb holds a rung while the body climbs past it, and
    /// over the last part of each two rungs' climb (the swing share) moves up two rungs to the next, out from the ladder
    /// and back. A hand holds a rung between its reach and two rungs below it; a foot stands on one between its reach and
    /// two rungs below (or on the ground, at the bottom). It eases in as the character steps onto the ladder and out as it
    /// steps off, or climbs off the top.
    ///
    /// The ladder comes from the character's animation state (CharacterAnimationState.ladder), found in the model's
    /// parents; limbs are placed through CharacterIK's procedural goals.
    /// </summary>
    [RequireComponent(typeof(CharacterIK))]
    [DisallowMultipleComponent]
    public class LadderIK : MonoBehaviour
    {
        [Tooltip("How strongly the hands and feet are put on the rungs (and the body in to the ladder). 0 turns it off.")]
        [Range(0f, 1f)] public float weight = 1f;
        [Tooltip("Seconds to ease in on taking hold of a ladder in mid-air, and out on letting go (stepping on and off at " +
                 "the bottom eases over the step instead)")]
        [Min(0f)] [SerializeField] private float fadeTime = 0.15f;
        [Header("Reach (m above the feet, at the character's authored size)")]
        [Tooltip("The highest a hand holds a rung: it moves up to the next one by then")]
        [SerializeField] private float handReach = 1.75f;
        [Tooltip("The highest a foot stands on a rung")]
        [SerializeField] private float footReach = 0.55f;
        [Header("Placement (m, at the character's authored size)")]
        [Tooltip("How far in from the ladder's rails each hand holds a rung")]
        [SerializeField] private float handInset = 0.1f;
        [Tooltip("How far out from the ladder's middle each foot stands")]
        [SerializeField] private float footSpread = 0.12f;
        [Tooltip("How far out of the ladder's front the wrists are, holding a rung (about the hand's thickness)")]
        [SerializeField] private float wristOut = 0.04f;
        [Tooltip("How far above a rung the ankles are, standing on it, and how far out from it")]
        [SerializeField] private Vector2 ankleAboveAndOut = new(0.08f, 0.07f);
        [Tooltip("How far (m) the body's centre is from the ladder's front")]
        [SerializeField] private float bodyFromLadder = 0.32f;
        [Header("Moving rung to rung")]
        [Tooltip("The share of each limb's two rungs of climbing spent moving it to the next rung (the rest it holds on)")]
        [Range(0.05f, 0.95f)] [SerializeField] private float swingShare = 0.4f;
        [Tooltip("How far (m, at the authored size) out from the ladder a limb moves on its way to the next rung")]
        [SerializeField] private float swingLift = 0.08f;
        [Tooltip("How strongly the hands are turned to grip (palms to the ladder), and the feet laid flat on the rungs")]
        [Range(0f, 1f)] [SerializeField] private float rotationWeight = 0.7f;

        private CharacterIK ik;
        private ICharacterAnimationStateSource stateSource;
        private float activeWeight;
        // The last ladder climbed, kept while easing out
        private LadderPose ladder;
        private bool hasLadder;

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
            if (state.onLadder && state.ladder.rungSpacing > 0f)
            {
                ladder = state.ladder;
                hasLadder = true;
            }
            // Stepping on or off at the bottom: eased over the step, as the feet go onto the rungs or back down.
            float goal = !state.onLadder ? 0f
                : state.climbEntering ? weight * state.climbStepProgress
                : state.climbExiting ? weight * (1f - state.climbStepProgress) : weight;
            bool stepping = state.onLadder && (state.climbEntering || state.climbExiting);
            activeWeight = stepping || fadeTime <= 0f ? goal : Mathf.MoveTowards(activeWeight, goal, Time.deltaTime / fadeTime);
            if (activeWeight <= 0f || !hasLadder) { return; }

            float size = state.scale.y > 0f ? state.scale.y : 1f;
            float feet = ladder.HeightOf(animator.transform.position);

            // The body in close to the ladder.
            Vector3 body = animator.bodyPosition;
            float fromLadder = Vector3.Dot(body - ladder.frontBottom, ladder.forward);
            animator.bodyPosition = body - ladder.forward * ((fromLadder - bodyFromLadder * size) * activeWeight);

            // The climber faces the ladder, so its left is the ladder's own right.
            float handSide = Mathf.Max(0f, 0.5f * ladder.width - ladder.depth - handInset * size);
            float footSide = Mathf.Min(footSpread * size, handSide);
            Quaternion leftHand = Quaternion.LookRotation(Vector3.Cross(-ladder.up, ladder.forward), ladder.forward);
            Quaternion rightHand = Quaternion.LookRotation(Vector3.Cross(ladder.up, ladder.forward), ladder.forward);
            Quaternion foot = Quaternion.LookRotation(Vector3.ProjectOnPlane(-ladder.forward, ladder.up), ladder.up);
            float rotation = rotationWeight * activeWeight;
            ik.SetProceduralGoal(IKLimb.LeftHand, Limb(feet, handReach * size, 0, handSide, true, size), leftHand, activeWeight, rotation);
            ik.SetProceduralGoal(IKLimb.RightFoot, Limb(feet, footReach * size, 0, -footSide, false, size), foot, activeWeight, rotation);
            ik.SetProceduralGoal(IKLimb.RightHand, Limb(feet, handReach * size, 1, -handSide, true, size), rightHand, activeWeight, rotation);
            ik.SetProceduralGoal(IKLimb.LeftFoot, Limb(feet, footReach * size, 1, footSide, false, size), foot, activeWeight, rotation);
        }

        /// <summary>
        /// Where a limb is, with the feet feet metres up the ladder: on the rung it holds (the highest of every other rung
        /// at or below reach above the feet, starting from the first, or with shift 1 the second), or on its way up two
        /// rungs to the next, over the swing share before the next is in reach. Hands hold rungs; feet stand on them, or on
        /// the ground below the first.
        /// </summary>
        private Vector3 Limb(float feet, float reach, int shift, float side, bool hand, float size)
        {
            // Rung k is at (k + 1) spacings: where reach is, in rungs.
            float at = (feet + reach) / ladder.rungSpacing - 1f - shift;
            int pairs = Mathf.FloorToInt(at / 2f);
            float through = at / 2f - pairs;
            int held = 2 * pairs + shift;
            Vector3 from = Hold(held, side, hand, size);
            float swing = Mathf.InverseLerp(1f - swingShare, 1f, through);
            if (swing <= 0f || held + 2 > ladder.rungCount - 1) { return from; }
            float t = Mathf.SmoothStep(0f, 1f, swing);
            Vector3 to = Hold(held + 2, side, hand, size);
            return Vector3.Lerp(from, to, t) + ladder.forward * (swingLift * size * Mathf.Sin(Mathf.PI * t));
        }

        /// <summary>Where a hand holding rung k is, or a foot standing on it (or on the ground, below the first).</summary>
        private Vector3 Hold(int k, float side, bool hand, float size)
        {
            k = Mathf.Min(k, ladder.rungCount - 1);
            if (hand)
            {
                k = Mathf.Max(k, 0);
                return ladder.Rung(k, side) + ladder.forward * (0.5f * ladder.rungSize + wristOut * size);
            }
            if (k < 0)
            {
                // On the ground in front of the ladder.
                return ladder.frontBottom + ladder.right * side + ladder.up * (ankleAboveAndOut.x * size)
                       + ladder.forward * (ankleAboveAndOut.y * size);
            }
            return ladder.Rung(k, side) + ladder.up * (0.5f * ladder.rungSize + ankleAboveAndOut.x * size)
                   + ladder.forward * (ankleAboveAndOut.y * size);
        }
    }
}
