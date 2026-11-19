using System;
using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// A movement mode made of a list of MovementComponents, each one part of the character's movement (an ability, with
    /// its settings), which can be added, removed, turned off and reordered on the asset. The mode runs them at each point
    /// of a physics step, and gives them what the rest of its movement knows (its gravity, the jump and crouch presses,
    /// whether an ability is in progress). A new kind of movement is a new list of components, reusing the same ones;
    /// what isn't any one part's (the character's posture, the presses several parts read) stays in the mode itself
    /// (see StandardMovementMode).
    /// </summary>
    public abstract class AbilityMovementMode : MovementMode
    {
        [Tooltip("The parts of the character's movement, each with its own settings. Where several could take over the " +
                 "character in a step, the one higher in the list does")]
        [SerializeReference] private List<MovementComponent> components;

        /// <summary>A new mode starts with the default components (DefaultComponents), in their order.</summary>
        protected AbilityMovementMode()
        {
            components = new List<MovementComponent>(DefaultComponents());
        }

        /// <summary>The components a new mode of this type starts with, in order.</summary>
        protected abstract IEnumerable<MovementComponent> DefaultComponents();

        /// <summary>Replaces the mode's components with the ones a new mode of this type starts with, at their defaults.</summary>
        public void ResetComponents()
        {
            components = new List<MovementComponent>(DefaultComponents());
            foreach (MovementComponent component in components) { component.Mode = this; }
        }

        /// <summary>The mode's components, in order (turned-off ones included; an entry is null if its type is missing).</summary>
        public IReadOnlyList<MovementComponent> Components => components;

        /// <summary>The mode's component of type T, if it has one and it's turned on (or either way, if includeDisabled).</summary>
        public T Get<T>(bool includeDisabled = false) where T : MovementComponent
        {
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i] is T component && (includeDisabled || component.Enabled)) { return component; }
            }
            return null;
        }

        /// <summary>The mode's component of the given type, if it has one and it's turned on (or either way, if includeDisabled).</summary>
        public MovementComponent Get(Type type, bool includeDisabled = false)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (component != null && component.GetType() == type && (includeDisabled || component.Enabled)) { return component; }
            }
            return null;
        }

        /// <summary>
        /// The settings of the mode's component of type T, turned on or not, or T's defaults if the mode has none: for
        /// what's shaped by a component's settings whether or not it's used (see MovementComponent.SettingsOf).
        /// </summary>
        public T SettingsOf<T>() where T : MovementComponent, new() => Get<T>(includeDisabled: true) ?? DefaultsOf<T>.Instance;

        /// <summary>A component type's default settings, shared, for SettingsOf: never changed.</summary>
        internal static class DefaultsOf<T> where T : MovementComponent, new()
        {
            /// <summary>The shared instance holding T's default settings.</summary>
            public static readonly T Instance = new();
        }

        /// <summary>Adds a component of type T at the end of the list (the lowest priority), or returns the one the mode has.</summary>
        public T Add<T>() where T : MovementComponent, new()
        {
            T component = Get<T>(includeDisabled: true);
            if (component != null) { return component; }
            component = new T { Mode = this };
            components.Add(component);
            return component;
        }

        /// <summary>
        /// Puts a component into the list at index (0 = the highest priority), if the mode has none of its type yet,
        /// returning whether it did.
        /// </summary>
        public bool Insert(int index, MovementComponent component)
        {
            if (component == null) { return false; }
            foreach (MovementComponent other in components)
            {
                if (other != null && other.GetType() == component.GetType()) { return false; }
            }
            component.Mode = this;
            components.Insert(Mathf.Clamp(index, 0, components.Count), component);
            return true;
        }

        /// <summary>The component types a new mode of this type starts with, in order.</summary>
        public IEnumerable<Type> DefaultComponentTypes()
        {
            foreach (MovementComponent component in DefaultComponents()) { yield return component.GetType(); }
        }

        /// <summary>The default components (DefaultComponents) the mode doesn't have, in their order.</summary>
        public List<Type> MissingDefaultComponentTypes()
        {
            var missing = new List<Type>();
            foreach (Type type in DefaultComponentTypes())
            {
                if (Get(type, includeDisabled: true) == null) { missing.Add(type); }
            }
            return missing;
        }

        /// <summary>
        /// Adds each default component (DefaultComponents) the mode doesn't have, at its defaults, turned on or off as
        /// given, at its default place: just after the nearest component above it in the default order that the mode has
        /// (or, with none, just before the nearest below it), so the rest of the list keeps the order it was given.
        /// Returns the types it added, in their order. A component added to the code's list (a new ability) reaches an
        /// asset made before it this way: a tuning preset only sets values, so it can't add one (see MovementTuningPreset).
        /// </summary>
        public List<Type> AddMissingDefaultComponents(bool enabled)
        {
            var added = new List<Type>();
            List<MovementComponent> defaults = new(DefaultComponents());
            for (int i = 0; i < defaults.Count; i++)
            {
                MovementComponent component = defaults[i];
                if (Get(component.GetType(), includeDisabled: true) != null) { continue; }
                component.Enabled = enabled;
                Insert(DefaultPlaceFor(defaults, i), component);
                added.Add(component.GetType());
            }
            return added;
        }

        // Where the default component at index of defaults goes in this mode's list: after the nearest default above it
        // that the mode has, else before the nearest below it, else at the end.
        private int DefaultPlaceFor(List<MovementComponent> defaults, int index)
        {
            for (int above = index - 1; above >= 0; above--)
            {
                int at = IndexOf(defaults[above].GetType());
                if (at >= 0) { return at + 1; }
            }
            for (int below = index + 1; below < defaults.Count; below++)
            {
                int at = IndexOf(defaults[below].GetType());
                if (at >= 0) { return at; }
            }
            return components.Count;
        }

        // The index of the mode's component of the given type, or -1.
        private int IndexOf(Type type)
        {
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i] != null && components[i].GetType() == type) { return i; }
            }
            return -1;
        }

        /// <summary>
        /// Removes the mode's component of type T, returning whether it had one. Remove a component only while it doesn't
        /// have a character (or turn it off instead, which ends what it's doing: see AdvanceComponentTimers).
        /// </summary>
        public bool Remove<T>() where T : MovementComponent
        {
            T component = Get<T>(includeDisabled: true);
            return component != null && components.Remove(component);
        }

        /// <summary>Removes the mode's component of the given type, returning whether it had one (as Remove&lt;T&gt;).</summary>
        public bool Remove(Type type)
        {
            MovementComponent component = Get(type, includeDisabled: true);
            return component != null && components.Remove(component);
        }

        /// <summary>The character's gravity (m/s²), at its size.</summary>
        public abstract float GravityAcceleration(MMotionMotor controller);

        /// <summary>Whether the jump button press is fresh enough to start something, and hasn't started anything already.</summary>
        public abstract bool HasFreshJumpPress(StepInputs inputs, MMotionMotor controller);

        /// <summary>Uses up a fresh press of Crouch (within the mode's crouch input buffer), returning whether there was one.</summary>
        public abstract bool ConsumeCrouchPress(StepInputs inputs, MMotionMotor controller);

        /// <summary>How quickly (m/s²) the character's horizontal speed changes in the air, at its size.</summary>
        public abstract float AirAcceleration(MMotionMotor controller);

        /// <summary>
        /// The speed (m/s) walking moves the character at for these inputs, at full movement input and its authored size
        /// (crouched, sprinting or walking).
        /// </summary>
        public abstract float MoveSpeed(StepInputs inputs, MMotionMotor controller);

        /// <summary>Whether an ability has the character, so no other may start: any turned-on component under way.</summary>
        public bool AbilityInProgress(MMotionMotor controller) => AbilityInProgressExcept(controller, null, null);

        /// <summary>
        /// Whether an ability other than a T has the character: for an ability that may take over from a T (a ledge
        /// grabbed out of a slide down its wall).
        /// </summary>
        public bool AbilityInProgressExcept<T>(MMotionMotor controller) where T : MovementComponent =>
            AbilityInProgressExcept(controller, typeof(T), null);

        /// <summary>Whether an ability other than a T1 or a T2 has the character (see AbilityInProgressExcept&lt;T&gt;).</summary>
        public bool AbilityInProgressExcept<T1, T2>(MMotionMotor controller) where T1 : MovementComponent where T2 : MovementComponent =>
            AbilityInProgressExcept(controller, typeof(T1), typeof(T2));

        /// <summary>Whether an ability other than a T1, a T2 or a T3 has the character (see AbilityInProgressExcept&lt;T&gt;).</summary>
        public bool AbilityInProgressExcept<T1, T2, T3>(MMotionMotor controller)
            where T1 : MovementComponent where T2 : MovementComponent where T3 : MovementComponent =>
            AbilityInProgressExcept(controller, typeof(T1), typeof(T2), typeof(T3));

        private bool AbilityInProgressExcept(MMotionMotor controller, Type except, Type orExcept, Type orAlsoExcept = null)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (component == null || !component.Enabled) { continue; }
                Type type = component.GetType();
                if (type == except || type == orExcept || type == orAlsoExcept) { continue; }
                if (component.IsActive(controller)) { return true; }
            }
            return false;
        }

        /// <summary>
        /// Whether a turned-on ability with any of the given traits has the character (see AbilityTraits): for an ability
        /// that cares what the one under way does with the character (holds it up, say), not which ability it is.
        /// </summary>
        public bool AbilityInProgressWith(AbilityTraits traits, MMotionMotor controller)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (component == null || !component.Enabled || (component.Traits & traits) == 0) { continue; }
                if (component.IsActive(controller)) { return true; }
            }
            return false;
        }

        /// <summary>
        /// Adds the starts and ends of every component's states (see MovementComponent.StateEvents). Turned-off components
        /// too: one turned off while it had the character lets go of it, and that ends its state.
        /// </summary>
        public override void DetectStateEvents(MovementStep data)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (component == null) { continue; }
                StateEventPair[] stateEvents = component.StateEvents;
                for (int e = 0; e < stateEvents.Length; e++) { stateEvents[e].Detect(data); }
            }
        }

        /// <summary>Ends every ability under way but the given one, which is taking the character.</summary>
        public void InterruptOthers(MovementStep movementData, MMotionMotor controller, MovementComponent except)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (component != null && component != except && component.Enabled) { component.Interrupt(movementData, controller); }
            }
        }

        /// <summary>
        /// Every step, before anything moves the character: each turned-on component's timers (see
        /// MovementComponent.AdvanceTimers). A component turned off while it had the character lets go of it here.
        /// </summary>
        /// <summary>
        /// Whether a component is called at a hook: turned on, and overriding it (one that doesn't would do nothing: see
        /// MovementComponent.Overridden).
        /// </summary>
        private static bool Calls(MovementComponent component, Hooks hook) =>
            component != null && component.Enabled && (component.Overridden & hook) != 0;

        protected void AdvanceComponentTimers(MovementStep movementData, StepInputs inputs, MMotionMotor controller)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (component == null) { continue; }
                component.Mode = this;
                if (component.Enabled)
                {
                    if ((component.Overridden & Hooks.AdvanceTimers) != 0) { using (component.Marker.Auto()) { component.AdvanceTimers(movementData, inputs, controller); } }
                }
                else if (component.IsActive(controller)) { component.Interrupt(movementData, controller); }
            }
        }

        /// <summary>
        /// Every step, once the timers have advanced, before an ability takes over: each turned-on component's
        /// BeforeAbilities (gravity), in order.
        /// </summary>
        protected void ComponentsBeforeAbilities(MovementStep movementData, StepInputs inputs, MMotionMotor controller)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (Calls(component, Hooks.BeforeAbilities)) { using (component.Marker.Auto()) { component.BeforeAbilities(movementData, inputs, controller); } }
            }
        }

        /// <summary>
        /// Asks the turned-on components, in order, to take over the character (see MovementComponent.TakeOver), returning
        /// whether one has it this step.
        /// </summary>
        protected bool ComponentsTakeOver(MovementStep movementData, StepInputs inputs, MMotionMotor controller)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (!Calls(component, Hooks.TakeOver)) { continue; }
                bool hasCharacter;
                using (component.Marker.Auto()) { hasCharacter = component.TakeOver(movementData, inputs, controller); }
                if (hasCharacter) { return true; }
            }
            return false;
        }

        /// <summary>
        /// When no ability has the character, before it walks: each turned-on component's BeforeLocomotion, in order.
        /// </summary>
        protected void ComponentsBeforeLocomotion(MovementStep movementData, StepInputs inputs, MMotionMotor controller)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (Calls(component, Hooks.BeforeLocomotion)) { using (component.Marker.Auto()) { component.BeforeLocomotion(movementData, inputs, controller); } }
            }
        }

        /// <summary>
        /// When no ability has the character, after BeforeLocomotion: each turned-on component's Locomotion (friction,
        /// walking, air steering), in order.
        /// </summary>
        protected void ComponentsLocomotion(MovementStep movementData, StepInputs inputs, MMotionMotor controller)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (Calls(component, Hooks.Locomotion)) { using (component.Marker.Auto()) { component.Locomotion(movementData, inputs, controller); } }
            }
        }

        /// <summary>When no ability has the character, once it has walked: each turned-on component's AfterLocomotion, in order.</summary>
        protected void ComponentsAfterWalking(MovementStep movementData, StepInputs inputs, MMotionMotor controller)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (Calls(component, Hooks.AfterLocomotion)) { using (component.Marker.Auto()) { component.AfterLocomotion(movementData, inputs, controller); } }
            }
        }

        /// <summary>Every step, last of all: each turned-on component's FinishStep (the fall speed limit), in order.</summary>
        protected void ComponentsFinishStep(MovementStep movementData, MMotionMotor controller)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (Calls(component, Hooks.FinishStep)) { using (component.Marker.Auto()) { component.FinishStep(movementData, controller); } }
            }
        }

        /// <summary>
        /// How the first turned-on component (in list order) to hold the character's body holds it this step, or null if
        /// none does (see MovementComponent.Posture).
        /// </summary>
        protected BodyPosture? ComponentPosture(MMotionMotor controller)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (component == null || !component.Enabled) { continue; }
                BodyPosture? held = component.Posture(controller);
                if (held.HasValue) { return held; }
            }
            return null;
        }

        /// <summary>Each turned-on component, once the step's movement is resolved (see MovementComponent.AfterMovementResolved).</summary>
        public override void AfterMovementResolved(MovementStep movementData, MMotionMotor controller)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (component == null || !component.Enabled) { continue; }
                component.Mode = this;
                using (component.Marker.Auto()) { component.AfterMovementResolved(movementData, controller); }
            }
        }

        /// <summary>What each turned-on component lets the character do (see MovementComponent.MeasureCapabilities).</summary>
        public override void MeasureCapabilities(MovementSimulation simulation, MovementCapabilities capabilities)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (component == null || !component.Enabled) { continue; }
                component.Mode = this;
                component.MeasureCapabilities(simulation, capabilities);
            }
        }

        /// <summary>
        /// Points each component at this mode and warns of a component type listed more than once.
        /// </summary>
        protected virtual void OnValidate()
        {
            components ??= new List<MovementComponent>();
            var seen = new HashSet<Type>();
            foreach (MovementComponent component in components)
            {
                if (component == null) { continue; }
                component.Mode = this;
                if (!seen.Add(component.GetType()))
                {
                    Debug.LogWarning($"{name} has more than one {component.GetType().Name}: a mode should have one of each component.", this);
                }
            }
        }
    }
}
