using System.Collections.Generic;
using MeridianMotion.Showcase.Grass;
using UnityEngine;

namespace MeridianMotion.Showcase.CharacterAudio
{
    /// <summary>
    /// Finds the surface under a character, from the ground it stands on and where its feet are. In order: a Surface Tag
    /// on the ground or above it; grass, if the grass says there's enough under the feet (lawns have no collider, so the
    /// ground under a lawn is the floor beneath it); the material of the first renderer on the ground or above it (the
    /// graybox's colliders are on child objects with no renderer), as the Surface Map says; else the map's fallback.
    /// What a piece of ground is (its tag or material) is worked out once and kept; the grass is asked each time, since it
    /// depends on where the feet are.
    /// </summary>
    public sealed class SurfaceFinder
    {
        /// <summary>What a piece of ground is: its surface, and whether a tag says so (which grass doesn't override).</summary>
        private readonly struct Known
        {
            public readonly SoundSurface surface;
            public readonly bool tagged;

            public Known(SoundSurface surface, bool tagged)
            {
                this.surface = surface;
                this.tagged = tagged;
            }
        }

        private readonly SurfaceMap map;
        private readonly Dictionary<Transform, Known> known = new(128);

        public SurfaceFinder(SurfaceMap map) => this.map = map;

        /// <summary>The surface under feet at this position, standing on this ground (null ground: none, in the air).</summary>
        public SoundSurface Find(Transform ground, Vector3 feet)
        {
            if (map == null) { return null; }
            Known here = default;
            if (ground != null && !known.TryGetValue(ground, out here))
            {
                SoundSurface tag = TagOf(ground);
                here = tag != null ? new Known(tag, true) : new Known(MaterialOf(ground), false);
                known[ground] = here;
            }
            if (here.tagged) { return here.surface; }
            if (map.Grass != null && map.GrassCover > 0f && GrassField.CoverAtAny(feet) >= map.GrassCover) { return map.Grass; }
            return here.surface != null ? here.surface : map.Fallback;
        }

        private static SoundSurface TagOf(Transform ground)
        {
            for (Transform t = ground; t != null; t = t.parent)
            {
                if (t.TryGetComponent(out SurfaceTag tag) && tag.Surface != null) { return tag.Surface; }
            }
            return null;
        }

        private SoundSurface MaterialOf(Transform ground)
        {
            for (Transform t = ground; t != null; t = t.parent)
            {
                if (t.TryGetComponent(out Renderer renderer)) { return map.SurfaceOfMaterial(renderer.sharedMaterial); }
            }
            return null;
        }
    }
}
