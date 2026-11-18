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

        /// <summary>
        /// The hooks (the points of a step) this kind of component overrides: its mode calls only these, as the others
        /// are this class's, which do nothing. Every component is called at every point of every step otherwise, most of
        /// them doing nothing there, each call timed (see Marker): that was half of a step's time in the mode.
        /// </summary>
        internal Hooks Overridden
        {
            get
            {
                if (!hasHooks) { hooks = HooksOf(GetType()); hasHooks = true; }
                return hooks;
            }
        }

        [NonSerialized] private Hooks hooks;
        [NonSerialized] private bool hasHooks;
        private static readonly System.Collections.Generic.Dictionary<Type, Hooks> hooksByType = new();

        private static Hooks HooksOf(Type type)
        {
            if (hooksByType.TryGetValue(type, out Hooks found)) { return found; }
            Hooks overridden = Hooks.None;
            Type[] step = { typeof(MovementData), typeof(Inputs), typeof(MovementController) };
            Type[] finish = { typeof(MovementData), typeof(MovementController) };
            void Check(string name, Type[] parameters, Hooks hook)
            {
                System.Reflection.MethodInfo method = type.GetMethod(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, parameters, null);
                if (method != null && method.DeclaringType != typeof(MovementComponent)) { overridden |= hook; }
            }
            Check(nameof(AdvanceTimers), step, Hooks.AdvanceTimers);
            Check(nameof(BeforeAbilities), step, Hooks.BeforeAbilities);
            Check(nameof(TakeOver), step, Hooks.TakeOver);
            Check(nameof(BeforeLocomotion), step, Hooks.BeforeLocomotion);
            Check(nameof(Locomotion), step, Hooks.Locomotion);
            Check(nameof(AfterWalking), step, Hooks.AfterWalking);
            Check(nameof(FinishStep), finish, Hooks.FinishStep);
            hooksByType[type] = overridden;
            return overridden;
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
        public virtual void AdvanceTimers(MovementData movementData, Inputs inputs, MovementController controller)
        {
        }

        /// <summary>
        /// Every step, once the timers have advanced and before an ability takes over the character: for what acts on it
        /// whatever has it (gravity, which the abilities that move it without gravity override).
        /// </summary>
        public virtual void BeforeAbilities(MovementData movementData, Inputs inputs, MovementController controller)
        {
        }

        /// <summary>
        /// Carries on with this ability if it's under way, or starts it if the inputs and surroundings call for it, and
        /// returns whether it has the character this step. The mode asks its components in list order, and stops at the
        /// first that has the character: earlier in the list comes first.
        /// </summary>
        public virtual bool TakeOver(MovementData movementData, Inputs inputs, MovementController controller) => false;

        /// <summary>Whether this ability is under way for the character (so no other ability may start).</summary>
        public virtual bool IsActive(MovementController controller) => false;

        /// <summary>
        /// What this ability does with the character while it's under way (see AbilityTraits), so other abilities can
        /// ask the mode about it (ComposedMovementMode.AbilityInProgressWith) rather than list the abilities they mean,
        /// a list a new ability would be missing from. None unless the ability says.
        /// </summary>
        public virtual AbilityTraits Traits => AbilityTraits.None;

        /// <summary>
        /// The states this ability sets and the events raised as each starts and ends (see StateEvent), so the ability
        /// only has to set the state. A static array, so asking allocates nothing. None unless the ability says.
        /// </summary>
        public virtual StateEvent[] StateEvents => System.Array.Empty<StateEvent>();

        /// <summary>
        /// The movement events of a moment this component raises itself (a jump, a bounce, a jump off a wall): those with
        /// no state that starts and ends, so not in its StateEvents. Several components may raise the same one (Jumped,
        /// from the ground and off a rope). Declared so what raises what is known: the hygiene check holds a component to
        /// raising only what it declares, and the moment kit (MomentFixture) tests one.
        /// </summary>
        public virtual MovementEvents OneOffEvents => MovementEvents.None;

        /// <summary>
        /// The names of the values this component publishes for its look (AbilityValues, set during its step), so an
        /// Animator Parameter Translator's inspector can offer them (Shows > Ability Value). Empty for none.
        /// </summary>
        public virtual string[] AbilityValueNames => System.Array.Empty<string>();

        /// <summary>
        /// When no ability has the character this step (none took it over), before it walks, dashes and jumps: for what
        /// changes how it moves on its own (crouching, say).
        /// </summary>
        public virtual void BeforeLocomotion(MovementData movementData, Inputs inputs, MovementController controller)
        {
        }

        /// <summary>
        /// When no ability has the character this step, after BeforeLocomotion: for how it moves by itself (walking on the
        /// ground, steering in the air, the ground's friction). In list order, so what changes the momentum the character
        /// carries (friction) comes before what reads it (walking makes up the difference to its speed).
        /// </summary>
        public virtual void Locomotion(MovementData movementData, Inputs inputs, MovementController controller)
        {
        }

        /// <summary>
        /// When no ability has the character this step, once it has walked: for what acts on top of walking (a dash, which
        /// takes over the character's movement while it lasts; a jump). In list order, so a dash comes before the jump
        /// that can cancel it.
        /// </summary>
        public virtual void AfterWalking(MovementData movementData, Inputs inputs, MovementController controller)
        {
        }

        /// <summary>
        /// How this holds the character's body this step (see BodyPosture), or null if it doesn't: low at the crouch height
        /// (crouched, sliding, rolling), lying (swimming underwater). The first turned-on component, in list order, to hold
        /// it has its way; the mode holds the body so where there's room (else as low as it fits), and stands it back up
        /// where there's room once none does.
        /// </summary>
        public virtual BodyPosture? Posture(MovementController controller) => null;

        /// <summary>
        /// Every step, last of all, whatever has the character (once its posture is set): for limits on the step's
        /// movement (how fast it falls).
        /// </summary>
        public virtual void FinishStep(MovementData movementData, MovementController controller)
        {
        }

        /// <summary>
        /// Ends this ability if it's under way, because another took the character (see ComposedMovementMode.InterruptOthers),
        /// or because this was turned off while it had the character.
        /// </summary>
        public virtual void Interrupt(MovementData movementData, MovementController controller)
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
            return SpacedName(type.Name);
        }

        /// <summary>A class name spaced out at each capital after a small letter, as the inspector names a component (WallRunning: Wall Running).</summary>
        public static string SpacedName(string className)
        {
            var spaced = new System.Text.StringBuilder(className.Length + 4);
            for (int i = 0; i < className.Length; i++)
            {
                if (i > 0 && char.IsUpper(className[i]) && !char.IsUpper(className[i - 1])) { spaced.Append(' '); }
                spaced.Append(className[i]);
            }
            return spaced.ToString();
        }

        /// <summary>Adds what this lets the character do to its capabilities (see MovementMode.MeasureCapabilities).</summary>
        public virtual void MeasureCapabilities(MovementSimulation simulation, MovementCapabilities capabilities)
        {
        }
    }

    /// <summary>The points of a step a component may act at (its hooks): see MovementComponent.Overridden.</summary>
    [Flags]
    internal enum Hooks
    {
        None = 0,
        AdvanceTimers = 1 << 0,
        BeforeAbilities = 1 << 1,
        TakeOver = 1 << 2,
        BeforeLocomotion = 1 << 3,
        Locomotion = 1 << 4,
        AfterWalking = 1 << 5,
        FinishStep = 1 << 6,
    }

    /// <summary>
    /// What an ability does with the character while it's under way (MovementComponent.Traits), for other abilities that
    /// care about it, not which ability it is.
    /// </summary>
    [Flags]
    public enum AbilityTraits
    {
        None = 0,
        /// <summary>
        /// The ability holds the character up: it isn't falling while it's held (by a ledge, a wall, a rope, a cable or
        /// water). A fall is measured from where it was let go (see Rolling).
        /// </summary>
        HoldsUp = 1 << 0,
        /// <summary>
        /// The character holds on to something (a ledge, a crate, a rope or a cable): what would throw it about, a bounce
        /// pad, leaves it be.
        /// </summary>
        HoldsOn = 1 << 1,
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
