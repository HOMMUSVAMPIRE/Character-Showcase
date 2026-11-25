using UnityEngine;

namespace MeridianMotion.Showcase.CharacterAudio
{
    /// <summary>
    /// Says what surface an object (and everything under it) is, for its sound, whatever its material: on a piece of
    /// ground the Surface Map gets wrong, or one with several materials.
    /// </summary>
    [AddComponentMenu("Meridian Motion/Audio/Surface Tag")]
    public class SurfaceTag : MonoBehaviour
    {
        [Tooltip("The surface this object, and everything under it, is")]
        [SerializeField] private SoundSurface surface;

        /// <summary>The surface this object is.</summary>
        public SoundSurface Surface { get => surface; set => surface = value; }
    }
}
