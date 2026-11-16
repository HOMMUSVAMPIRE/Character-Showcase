using System;
using Unity.Profiling;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// One part of a character's movement (an ability such as riding ziplines), in a ComposedMovementMode's list of
    /// components: it holds its own settings, and the mode calls it at each point of a physics step. A component keeps no
    /// per-character state of its own (modes are shared assets: that's on the MovementController, see
    /// the state classes beside each component), and asks its mode for what the rest of the movement knows (gravity, the jump press, other
    /// abilities in progress). Turned off (Enabled), or removed from the list, the movement goes on as if it weren't
    /// there. A mode has one component of each type; another mode type reuses the same components by listing them.
    /// </summary>
    [Serializable]
    public abstract class MovementComponent
    {
        [Tooltip("Whether the character uses this part of its movement. Turned off, the movement goes on as if it weren't in the list")]
        [SerializeField] private bool enabled = true;

        [NonSerialized] private ComposedMovementMode mode;
        [NonSerialized] private ProfilerMarker marker;
        [NonSerialized] private bool hasMarker;

        /// <summary>Whether the character uses this part of its movement.</summary>
        public bool Enabled { get => enabled; set => enabled = value; }

        /// <summary>
        /// The profiler marker the mode times this component's hooks under, together ("Movement.ClassName" in the
        /// Profiler's CPU Usage), made the first time it's asked for.
        /// </summary>
        internal ProfilerMarker Marker
        {
            get
            {
                if (!hasMarker) { marker = new ProfilerMarker($"Movement.{GetType().Name}"); hasMarker = true; }
                return marker;
            }
        }

        /// <summary>The movement mode this is a component of (set by the mode before it calls the component).</summary>
        public ComposedMovementMode Mode { get => mode; internal set => mode = value; }

        /// <summary>
        /// The settings of the mode's component of type T, turned on or not, or T's defaults if the mode has none: for a
        /// component whose behaviour is shaped by another's settings (a vault keeps below the heights a ledge is grabbed
        /// at, whether or not ledges are grabbed). To hand the character to another component, use Mode.Get instead,
        /// which finds only one that's turned on.
        /// </summary>
        protected T SettingsOf<T>() where T : MovementComponent, new() =>
            mode != null ? mode.SettingsOf<T>() : ComposedMovementMode.DefaultsOf<T>.Instance;

        /// <summary>
        /// Every step, before anything moves the character, whatever has it (an ability's cooldowns run out meanwhile).
        /// </summary>
        public virtual void AdvanceTimers(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
        }

        /// <summary>
        /// Every step, once the timers have advanced and before an ability takes over the character: for what acts on it
        /// whatever has it (gravity, which the abilities that move it without gravity override).
        /// </summary>
        public virtual void BeforeAbilities(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
        }

        /// <summary>
        /// Carries on with this ability if it's under way, or starts it if the inputs and surroundings call for it, and
        /// returns whether it has the character this step. The mode asks its components in list order, and stops at the
        /// first that has the character: earlier in the list comes first.
        /// </summary>
        public virtual bool TakeOver(ref MovementData movementData, Inputs inputs, MovementController controller) => false;

        /// <summary>Whether this ability is under way for the character (so no other ability may start).</summary>
        public virtual bool IsActive(MovementController controller) => false;

        /// <summary>
        /// When no ability has the character this step (none took it over), before it walks, dashes and jumps: for what
        /// changes how it moves on its own (crouching, say).
        /// </summary>
        public virtual void BeforeLocomotion(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
        }

        /// <summary>
        /// When no ability has the character this step, after BeforeLocomotion: for how it moves by itself (walking on the
        /// ground, steering in the air, the ground's friction). In list order, so what changes the momentum the character
        /// carries (friction) comes before what reads it (walking makes up the difference to its speed).
        /// </summary>
        public virtual void Locomotion(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
        }

        /// <summary>
        /// When no ability has the character this step, once it has walked: for what acts on top of walking (a dash, which
        /// takes over the character's movement while it lasts; a jump). In list order, so a dash comes before the jump
        /// that can cancel it.
        /// </summary>
        public virtual void AfterWalking(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
        }

        /// <summary>
        /// Whether this keeps the character low this step, its collider at the crouch height (crouched, sliding, rolling):
        /// the mode lowers it while any turned-on component does, and stands it back up where there's room otherwise.
        /// </summary>
        public virtual bool KeepsLow(MovementController controller) => false;

        /// <summary>
        /// Every step, last of all, whatever has the character (once its posture is set): for limits on the step's
        /// movement (how fast it falls).
        /// </summary>
        public virtual void FinishStep(ref MovementData movementData, MovementController controller)
        {
        }

        /// <summary>
        /// Ends this ability if it's under way, because another took the character (see ComposedMovementMode.InterruptOthers),
        /// or because this was turned off while it had the character.
        /// </summary>
        public virtual void Interrupt(ref MovementData movementData, MovementController controller)
        {
        }

        /// <summary>Once the step's movement is resolved against the world (see MovementMode.AfterMovementResolved).</summary>
        public virtual void AfterMovementResolved(MovementData movementData, MovementController controller)
        {
        }

        /// <summary>A component type's name in the inspector: its MovementComponentName if it has one, else its class name spaced out.</summary>
        public static string DisplayNameOf(Type type)
        {
            if (Attribute.GetCustomAttribute(type, typeof(MovementComponentNameAttribute)) is MovementComponentNameAttribute named) { return named.Name; }
            var spaced = new System.Text.StringBuilder(type.Name.Length + 4);
            for (int i = 0; i < type.Name.Length; i++)
            {
                if (i > 0 && char.IsUpper(type.Name[i]) && !char.IsUpper(type.Name[i - 1])) { spaced.Append(' '); }
                spaced.Append(type.Name[i]);
            }
            return spaced.ToString();
        }

        /// <summary>Adds what this lets the character do to its capabilities (see MovementMode.MeasureCapabilities).</summary>
        public virtual void MeasureCapabilities(MovementSimulation simulation, MovementCapabilities capabilities)
        {
        }
    }

    /// <summary>
    /// The name a movement component goes by in the inspector, where its class name can't say it (an ampersand, say).
    /// Only the name shown: the class name is what's saved.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class MovementComponentNameAttribute : Attribute
    {
        public MovementComponentNameAttribute(string name) { Name = name; }

        /// <summary>The name shown in the inspector.</summary>
        public string Name { get; }
    }
}
