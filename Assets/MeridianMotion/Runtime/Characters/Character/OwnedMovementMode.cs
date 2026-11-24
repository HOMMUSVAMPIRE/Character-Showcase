using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// Holds a movement mode made for one character alone (a copy with a tuning preset applied, as a CharacterSpawner
    /// with a Tuning Preset gives each character it spawns), and destroys it with the character, so copies don't pile up
    /// as characters come and go.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Meridian Motion/Characters/Owned Movement Mode")]
    public class OwnedMovementMode : MonoBehaviour
    {
        /// <summary>The character's own movement mode, destroyed with it.</summary>
        public MovementMode Mode { get; set; }

        private void OnDestroy()
        {
            if (Mode != null) { Destroy(Mode); }
        }
    }
}
