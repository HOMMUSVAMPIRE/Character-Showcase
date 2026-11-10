using UnityEngine;

namespace Character
{
    /// <summary>
    /// Translates a character's animation state and movement events into a particular model's animations: for example
    /// Animator parameters and triggers, or a different animation system entirely. Each CharacterModelSource names
    /// the translator that suits its model, so swapping a model swaps how it's animated too.
    ///
    /// Translators are shared assets, so the per-character work is done by the driver each one creates for a model.
    /// </summary>
    public abstract class CharacterAnimationTranslator : ScriptableObject
    {
        /// <summary>
        /// Creates the object that animates one loaded model. Return null if this translator can't drive the model
        /// (for example, it has no Animator); the character then simply isn't animated.
        /// </summary>
        /// <param name="model">The loaded and set up model instance</param>
        /// <param name="animator">The model's Animator, or null if it has none</param>
        public abstract ICharacterAnimationDriver CreateDriver(GameObject model, Animator animator);
    }

    /// <summary>Animates one model instance, created for it by a CharacterAnimationTranslator.</summary>
    public interface ICharacterAnimationDriver
    {
        /// <summary>Called every rendered frame with the character's latest animation state.</summary>
        void UpdateAnimation(in CharacterAnimationState state, float deltaTime);

        /// <summary>Called for each movement event (one flag at a time), with the state at the moment it happened.</summary>
        void HandleMovementEvent(MovementEvents movementEvent, in CharacterAnimationState state);
    }
}
