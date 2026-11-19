using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// A part of a model that carries on from the model it replaces on the same character, rather than starting afresh
    /// (feet already planted stay planted, rather than fading back in on the new model). CharacterModelSlot calls it on
    /// every component of a model it swaps in, with the model going away, before the new model's first update (and
    /// before its Awake: models are staged inactive). A component finds what it continues from in the old model itself,
    /// and does nothing if it finds nothing.
    /// </summary>
    public interface IModelHandover
    {
        /// <summary>Carries on from the old model's state, as it was this frame.</summary>
        void ContinueFrom(GameObject oldModel);
    }
}
