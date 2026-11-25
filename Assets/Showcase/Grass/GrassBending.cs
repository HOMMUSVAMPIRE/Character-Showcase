using UnityEngine;

namespace MeridianMotion.Showcase.Grass
{
    /// <summary>
    /// The bend map: where the grass has been pushed aside (GrassBender: its legs, or a circle at its feet), for every
    /// grass field. A square texture MapSize wide round the camera, at MapResolution (fine enough for a leg), each texel
    /// holding a push (out from what pushed it and partly along its way, 0 to 1) and the height of the bottom of what
    /// pushed it (lawns are stacked, and the map's top-down; a lifted foot lets the grass go). Once a frame, before the first grass
    /// field places its blades, a compute pass (GrassBlades.compute, Bend) lets every push spring back a little and adds
    /// the benders'. The map wraps (a texel stands for wherever in the window the world's x and z, over its size, have
    /// its fraction), so it follows the camera without its contents being moved; texels the window has moved past start
    /// flat.
    /// </summary>
    public static class GrassBending
    {
        /// <summary>The map's size (m) and resolution (texels across): pushes are kept this far round the camera, this finely.</summary>
        public const float MapSize = 24f;
        public const int MapResolution = 512;

        /// <summary>How long a push takes to spring back to about a third (s).</summary>
        public const float SpringBack = 0.7f;

        /// <summary>The most pushers (legs, or circles at feet) it takes in a frame (those of the benders first enabled, if there are more).</summary>
        public const int MostPushers = 128;

        /// <summary>How far past a pusher's radius (m) its push fades out: soft edges, wider than the map's texels.</summary>
        public const float PushFade = 0.12f;

        /// <summary>How much of a moving pusher's push is along its movement rather than out from it, and the speed (m/s) at which all of that is.</summary>
        public const float Drag = 0.45f, DragSpeed = 2.5f;

        private static RenderTexture map;
        private static GraphicsBuffer pushers;
        private static readonly Vector4[] data = new Vector4[3 * MostPushers];
        private static int lastFrame = -1;
        private static Vector2 lastCorner;
        private static Vector4 window;

        /// <summary>The map (null until there's a bender), and its window: its corner (x, z), size, and 1 if it's in use.</summary>
        public static Texture Map => map;
        public static Vector4 Window => window;

        private static class Ids
        {
            public static readonly int Target = Shader.PropertyToID("_BendTarget"), Pushers = Shader.PropertyToID("_Pushers");
            public static readonly int Count = Shader.PropertyToID("_PusherCount"), Step = Shader.PropertyToID("_BendStep");
            public static readonly int Shape = Shader.PropertyToID("_BendShape");
            public static readonly int Window = Shader.PropertyToID("_BendWindow");
        }

        /// <summary>Forgets every push: the map's made again, flat, when next needed (a test's start, say).</summary>
        public static void Clear()
        {
            if (map != null) { map.Release(); Object.DestroyImmediate(map); }
            pushers?.Release();
            map = null;
            pushers = null;
            window.w = 0f;
            lastFrame = -1;
        }

        /// <summary>Updates the map for this frame (once, whichever field asks first): see the class.</summary>
        public static void Update(ComputeShader generator, int kernel, Camera camera)
        {
            if (Time.frameCount == lastFrame) { return; }
            lastFrame = Time.frameCount;
            // Without a bender there's nothing to push, until there's been one; after, the map keeps springing back.
            if (GrassBender.All.Count == 0 && map == null) { window.w = 0f; return; }

            if (map == null)
            {
                map = new RenderTexture(MapResolution, MapResolution, 0, RenderTextureFormat.ARGBHalf)
                {
                    name = "Grass Bend Map", enableRandomWrite = true, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                map.Create();
                pushers = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 3 * MostPushers, 4 * sizeof(float));
                lastCorner = new Vector2(float.MaxValue, float.MaxValue);
            }

            // Round the main camera (the one following the player), or the one rendering; its corner on whole texels.
            Camera centre = Camera.main != null ? Camera.main : camera;
            float texel = MapSize / MapResolution;
            Vector3 at = centre.transform.position;
            var corner = new Vector2(Mathf.Floor((at.x - MapSize / 2f) / texel) * texel, Mathf.Floor((at.z - MapSize / 2f) / texel) * texel);

            int count = 0;
            foreach (GrassBender bender in GrassBender.All)
            {
                Vector3 feet = bender.transform.position;
                if (Mathf.Abs(feet.x - at.x) > MapSize / 2f + 2f || Mathf.Abs(feet.z - at.z) > MapSize / 2f + 2f) { continue; }
                count += bender.AddPushers(data, count, MostPushers);
            }
            pushers.SetData(data, 0, 0, 3 * Mathf.Max(count, 1));

            window = new Vector4(corner.x, corner.y, MapSize, 1f);
            float left = Mathf.Exp(-Time.deltaTime / SpringBack);
            if (lastCorner.x == float.MaxValue) { lastCorner = corner; }
            generator.SetTexture(kernel, Ids.Target, map);
            generator.SetBuffer(kernel, Ids.Pushers, pushers);
            generator.SetVector(Ids.Shape, new Vector4(PushFade, Drag, DragSpeed, 0f));
            generator.SetInt(Ids.Count, count);
            generator.SetVector(Ids.Window, window);
            generator.SetVector(Ids.Step, new Vector4(lastCorner.x, lastCorner.y, left, MapResolution));
            int groups = Mathf.CeilToInt(MapResolution / 8f);
            generator.Dispatch(kernel, groups, groups, 1);
            lastCorner = corner;
        }
    }
}
