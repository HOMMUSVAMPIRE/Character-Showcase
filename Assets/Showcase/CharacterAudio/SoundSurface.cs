using UnityEngine;

namespace MeridianMotion.Showcase.CharacterAudio
{
    /// <summary>
    /// A kind of ground, as far as sound goes: Concrete, Metal, Grass. A character's Sound Set gives its footsteps (and its
    /// landings, jumps and the rest) a sound per surface; the Surface Map says which surface a piece of ground is.
    /// </summary>
    [CreateAssetMenu(menuName = "Meridian Motion/Audio/Sound Surface", fileName = "Sound Surface")]
    public class SoundSurface : ScriptableObject
    {
        [Tooltip("The surface's name, for the inspector and the logs")]
        [SerializeField] private string surfaceName = "Concrete";

        /// <summary>The surface's name.</summary>
        public string SurfaceName { get => surfaceName; set => surfaceName = value; }
    }
}
