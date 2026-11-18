using UnityEngine;

namespace Character
{
    /// <summary>
    /// The shared part of the IK that places limbs through CharacterIK: the feet on the ground (FootPlacementIK), and the
    /// hands (and feet, and the body) for an ability: on a ladder's rungs (LadderIK), a ledge (LedgeHandIK), a rope or
    /// zipline (LineGripIK), a wall slid down (WallSlideIK). Each solves in CharacterIK's BeforeSolve, reads the
    /// character's animation state from the model's parents, and eases its placement in and out (ActiveWeight, toward
    /// its Weight, over its Fade Time). A subclass declares its
    /// own weight and fade time settings, with their own tooltips and defaults, and gives them here.
    /// </summary>
    [RequireComponent(typeof(CharacterIK))]
    public abstract class AbilityIK : MonoBehaviour
    {
        /// <summary>The CharacterIK this places limbs through.</summary>
        protected CharacterIK ik;
        /// <summary>Where the character's animation state comes from (found in the model's parents), or null if nowhere.</summary>
        protected ICharacterAnimationStateSource stateSource;
        /// <summary>How far the placement is eased in now, from 0 to Weight.</summary>
        protected float activeWeight;

        /// <summary>How far the placement is eased in now (0 to the component's weight).</summary>
        public float ActiveWeight => activeWeight;

        /// <summary>How strongly the placement applies when it's fully in (the subclass's weight setting).</summary>
        protected abstract float Weight { get; }

        /// <summary>Seconds the placement takes to ease in and out (the subclass's fade time setting): 0 is at once.</summary>
        protected abstract float FadeTime { get; }

        protected virtual void Awake()
        {
            ik = GetComponent<CharacterIK>();
            timedBy = GetComponent<Animator>();
        }

        // The Animator whose update this solves in (see DeltaTime).
        private Animator timedBy;
        protected virtual void OnEnable() => ik.BeforeSolve += Solve;

        protected virtual void OnDisable()
        {
            ik.BeforeSolve -= Solve;
            activeWeight = 0f;
        }

        protected virtual void Start() => stateSource = GetComponentInParent<ICharacterAnimationStateSource>();

        /// <summary>Places the limbs for this frame (from CharacterIK's BeforeSolve, before it solves).</summary>
        protected abstract void Solve(Animator animator);

        /// <summary>
        /// The time this solve eases by: the Animator update's it's solved in (see CharacterAnimationStates.DeltaTimeOf),
        /// so a test stepping the Animator by hand gets its step's, not the frame's.
        /// </summary>
        protected float DeltaTime => CharacterAnimationStates.DeltaTimeOf(stateSource, timedBy);

        /// <summary>Eases ActiveWeight toward goal over the fade time (at once with none).</summary>
        protected void FadeTo(float goal) =>
            activeWeight = FadeTime > 0f ? Mathf.MoveTowards(activeWeight, goal, DeltaTime / FadeTime) : goal;
    }

    /// <summary>
    /// Marks an AbilityIK every humanoid model gets: Prepare Character Models adds it to each model prefab it saves (with the
    /// settings the model's earlier prefab had, else the mannequin's), so a new one is on every model with no change to the
    /// importer. Order puts it among the others on the model (lower first), which is the order they're added in.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)]
    public sealed class HumanoidIKAttribute : System.Attribute
    {
        /// <summary>Where it goes among the others (lower first; the built-in ones are 0 to 40).</summary>
        public int Order { get; }

        public HumanoidIKAttribute(int order = 100) { Order = order; }
    }
}
