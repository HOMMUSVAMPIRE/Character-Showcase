using System;
using UnityEngine;

namespace MeridianMotion.Showcase.CharacterAudio
{
    /// <summary>
    /// Which surface each of the levels' materials is (Level_Concrete: Concrete, Level_Steel: Metal), the surface ground
    /// with grass on it is, and the surface for anything else. What SurfaceFinder reads.
    /// </summary>
    [CreateAssetMenu(menuName = "Meridian Motion/Audio/Surface Map", fileName = "Surface Map")]
    public class SurfaceMap : ScriptableObject
    {
        /// <summary>A material and the surface it is.</summary>
        [Serializable]
        public struct MaterialSurface
        {
            [Tooltip("A material ground is drawn with")]
            public Material material;
            [Tooltip("The surface it is")]
            public SoundSurface surface;
        }

        [Tooltip("The materials and the surfaces they are")]
        [SerializeField] private MaterialSurface[] byMaterial = new MaterialSurface[0];
        [Tooltip("The surface of ground whose material isn't listed, and of no ground at all")]
        [SerializeField] private SoundSurface fallback;
        [Tooltip("The surface of ground with grass on it (a lawn: they have no collider, so the grass is asked)")]
        [SerializeField] private SoundSurface grass;
        [Tooltip("How much grass (0 to 1, the grass's density) counts as grass underfoot")]
        [Range(0f, 1f)] [SerializeField] private float grassCover = 0.3f;

        /// <summary>The materials and their surfaces.</summary>
        public MaterialSurface[] ByMaterial => byMaterial;
        /// <summary>The surface of anything else.</summary>
        public SoundSurface Fallback => fallback;
        /// <summary>The surface of ground with grass on it.</summary>
        public SoundSurface Grass => grass;
        /// <summary>How much grass counts as grass underfoot.</summary>
        public float GrassCover { get => grassCover; set => grassCover = Mathf.Clamp01(value); }

        /// <summary>The surface a material is, or null if it isn't listed.</summary>
        public SoundSurface SurfaceOfMaterial(Material material)
        {
            if (material == null) { return null; }
            foreach (MaterialSurface entry in byMaterial)
            {
                if (entry.material == material) { return entry.surface; }
            }
            return null;
        }

        /// <summary>Sets what's in it (its maker, Create Audio Assets, and tests).</summary>
        public void Set(MaterialSurface[] materials, SoundSurface fallbackSurface, SoundSurface grassSurface)
        {
            byMaterial = materials ?? new MaterialSurface[0];
            fallback = fallbackSurface;
            grass = grassSurface;
        }
    }
}
