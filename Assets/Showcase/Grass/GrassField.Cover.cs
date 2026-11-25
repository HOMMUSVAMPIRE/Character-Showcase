using UnityEngine;

namespace MeridianMotion.Showcase.Grass
{
    public sealed partial class GrassField
    {
        // How far (m) below a floor a point still counts as on it: a character's feet rest a hair into the ground.
        private const float CoverFloorTolerance = 0.05f;

        /// <summary>
        /// How much grass there is at a point (0 to 1: the mask's density at its texel), on the floor at or below it and no
        /// more than <paramref name="below"/> metres under it; 0 outside the mask or with no such floor. In the field's own
        /// space, as its blades are placed. Read from the mask on the CPU, so it's cheap and needs no compute shaders: for
        /// anything that cares whether there's grass somewhere (the footsteps on it: lawns have no collider of their own).
        /// </summary>
        public float CoverAt(Vector3 worldPosition, float below = 0.5f)
        {
            if (layers == null || layers.Count == 0 || texelSize <= 0f) { return 0f; }
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            int x = Mathf.FloorToInt((local.x - origin.x) / texelSize);
            int z = Mathf.FloorToInt((local.z - origin.y) / texelSize);
            if (x < 0 || z < 0 || x >= size.x || z >= size.y) { return 0f; }

            // The highest floor at or below the point, within reach.
            Layer floor = null;
            for (int i = 0; i < layers.Count; i++)
            {
                Layer layer = layers[i];
                if (layer.height > local.y + CoverFloorTolerance || local.y - layer.height > below) { continue; }
                if (floor == null || layer.height > floor.height) { floor = layer; }
            }
            if (floor == null || floor.density == null) { return 0f; }
            return floor.density[z * size.x + x] / 255f;
        }

        /// <summary>The most grass any live (enabled) field has at a point (see CoverAt).</summary>
        public static float CoverAtAny(Vector3 worldPosition, float below = 0.5f)
        {
            float most = 0f;
            for (int i = 0; i < live.Count; i++)
            {
                float cover = live[i].CoverAt(worldPosition, below);
                if (cover > most) { most = cover; }
            }
            return most;
        }
    }
}
