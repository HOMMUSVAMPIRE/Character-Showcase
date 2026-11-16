using System;
using System.Collections.Generic;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// A movement mode made of a list of MovementComponents, each one part of the character's movement (an ability, with
    /// its settings), which can be added, removed, turned off and reordered on the asset. The mode runs them at each point
    /// of a physics step, and gives them what the rest of its movement knows (its gravity, the jump and crouch presses,
    /// whether an ability is in progress). A new kind of movement is a new list of components, reusing the same ones;
    /// what isn't any one part's (the character's posture, the presses several parts read) stays in the mode itself
    /// (see BasicMovementMode).
    /// </summary>
    public abstract class ComposedMovementMode : MovementMode
    {
        [Tooltip("The parts of the character's movement, each with its own settings. Where several could take over the " +
                 "character in a step, the one higher in the list does")]
        [SerializeReference] private List<MovementComponent> components;

        protected ComposedMovementMode()
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

        /// <summary>
        /// Removes the mode's component of type T, returning whether it had one. Remove a component only while it doesn't
        /// have a character (or turn it off instead, which ends what it's doing: see AdvanceComponentTimers).
        /// </summary>
        public bool Remove<T>() where T : MovementComponent
        {
            T component = Get<T>(includeDisabled: true);
            return component != null && components.Remove(component);
        }

        /// <summary>The character's gravity (m/s²), at its size.</summary>
        public abstract float GravityAcceleration(MovementController controller);

        /// <summary>Whether the jump button press is fresh enough to start something, and hasn't started anything already.</summary>
        public abstract bool IsNewJumpPress(Inputs inputs, MovementController controller);

        /// <summary>Uses up a fresh press of Crouch (within the mode's crouch input buffer), returning whether there was one.</summary>
        public abstract bool ConsumeCrouchPress(Inputs inputs, MovementController controller);

        /// <summary>How quickly (m/s²) the character's horizontal speed changes in the air, at its size.</summary>
        public abstract float AirAcceleration(MovementController controller);

        /// <summary>
        /// The speed (m/s) walking moves the character at for these inputs, at full movement input and its authored size
        /// (crouched, sprinting or walking).
        /// </summary>
        public abstract float MoveSpeed(Inputs inputs, MovementController controller);

        /// <summary>Whether an ability has the character, so no other may start: any turned-on component under way.</summary>
        public bool AbilityInProgress(MovementController controller) => AbilityInProgressExcept(controller, null, null);

        /// <summary>
        /// Whether an ability other than a T has the character: for an ability that may take over from a T (a ledge
        /// grabbed out of a slide down its wall).
        /// </summary>
        public bool AbilityInProgressExcept<T>(MovementController controller) where T : MovementComponent =>
            AbilityInProgressExcept(controller, typeof(T), null);

        /// <summary>Whether an ability other than a T1 or a T2 has the character (see AbilityInProgressExcept&lt;T&gt;).</summary>
        public bool AbilityInProgressExcept<T1, T2>(MovementController controller) where T1 : MovementComponent where T2 : MovementComponent =>
            AbilityInProgressExcept(controller, typeof(T1), typeof(T2));

        private bool AbilityInProgressExcept(MovementController controller, Type except, Type orExcept)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (component == null || !component.Enabled) { continue; }
                Type type = component.GetType();
                if (type == except || type == orExcept) { continue; }
                if (component.IsActive(controller)) { return true; }
            }
            return false;
        }

        /// <summary>Ends every ability under way but the given one, which is taking the character.</summary>
        public void InterruptOthers(ref MovementData movementData, MovementController controller, MovementComponent except)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (component != null && component != except && component.Enabled) { component.Interrupt(ref movementData, controller); }
            }
        }

        /// <summary>
        /// Every step, before anything moves the character: each turned-on component's timers (see
        /// MovementComponent.AdvanceTimers). A component turned off while it had the character lets go of it here.
        /// </summary>
        protected void AdvanceComponentTimers(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (component == null) { continue; }
                component.Mode = this;
                if (component.Enabled) { component.AdvanceTimers(ref movementData, inputs, controller); }
                else if (component.IsActive(controller)) { component.Interrupt(ref movementData, controller); }
            }
        }

        /// <summary>
        /// Every step, once the timers have advanced, before an ability takes over: each turned-on component's
        /// BeforeAbilities (gravity), in order.
        /// </summary>
        protected void ComponentsBeforeAbilities(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (component != null && component.Enabled) { component.BeforeAbilities(ref movementData, inputs, controller); }
            }
        }

        /// <summary>
        /// Asks the turned-on components, in order, to take over the character (see MovementComponent.TakeOver), returning
        /// whether one has it this step.
        /// </summary>
        protected bool ComponentsTakeOver(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (component != null && component.Enabled && component.TakeOver(ref movementData, inputs, controller)) { return true; }
            }
            return false;
        }

        /// <summary>
        /// When no ability has the character, before it walks: each turned-on component's BeforeLocomotion, in order.
        /// </summary>
        protected void ComponentsBeforeLocomotion(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (component != null && component.Enabled) { component.BeforeLocomotion(ref movementData, inputs, controller); }
            }
        }

        /// <summary>
        /// When no ability has the character, after BeforeLocomotion: each turned-on component's Locomotion (friction,
        /// walking, air steering), in order.
        /// </summary>
        protected void ComponentsLocomotion(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (component != null && component.Enabled) { component.Locomotion(ref movementData, inputs, controller); }
            }
        }

        /// <summary>When no ability has the character, once it has walked: each turned-on component's AfterWalking, in order.</summary>
        protected void ComponentsAfterWalking(ref MovementData movementData, Inputs inputs, MovementController controller)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (component != null && component.Enabled) { component.AfterWalking(ref movementData, inputs, controller); }
            }
        }

        /// <summary>Every step, last of all: each turned-on component's FinishStep (the fall speed limit), in order.</summary>
        protected void ComponentsFinishStep(ref MovementData movementData, MovementController controller)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (component != null && component.Enabled) { component.FinishStep(ref movementData, controller); }
            }
        }

        /// <summary>Whether a turned-on component keeps the character low this step (see MovementComponent.KeepsLow).</summary>
        protected bool ComponentKeepsLow(MovementController controller)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (component != null && component.Enabled && component.KeepsLow(controller)) { return true; }
            }
            return false;
        }

        /// <summary>Each turned-on component, once the step's movement is resolved (see MovementComponent.AfterMovementResolved).</summary>
        public override void AfterMovementResolved(MovementData movementData, MovementController controller)
        {
            for (int i = 0; i < components.Count; i++)
            {
                MovementComponent component = components[i];
                if (component == null || !component.Enabled) { continue; }
                component.Mode = this;
                component.AfterMovementResolved(movementData, controller);
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
