using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MeridianMotion.Showcase.Grass
{
    /// <summary>
    /// Grass over a level's lawns, made on the GPU each frame rather than stored. The field holds a density mask over an
    /// area of its own (x and z in its transform's space), one layer for each floor the lawns lie on. Before each camera
    /// renders, it does three things:
    /// <list type="number">
    /// <item>finds which chunks of the mask are in view and within the style's draw distance;</item>
    /// <item>has a compute shader (GrassBlades.compute) place a blade on every cell of a fine grid over those chunks where
    /// the mask has grass, culled by the view and thinned out with distance, into a near list and a far list;</item>
    /// <item>draws each list in one indirect draw (Grass.shader), the near blades curved and the far ones a triangle each.</item>
    /// </list>
    /// A blade is placed by a hash of its cell, so it stays put as the camera moves. The field also colours the lawns
    /// under it (SetLawns), so they match its grass. Its blades sway in the level's wind (GrassWind) and bow away from
    /// what walks through them (GrassBender, GrassBending). The level builders lay the mask
    /// (Graybox.Lawns); the field knows nothing of the level, the character or the camera rig. It has no collider.
    /// Without compute shaders (or indirect draws), it draws nothing, and the lawn under it is all there is.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Meridian Motion/Showcase/Grass Field")]
    public sealed class GrassField : MonoBehaviour
    {
        /// <summary>The mask's chunks are this many texels square: the unit the CPU culls by.</summary>
        public const int ChunkTexels = 32;

        /// <summary>
        /// One floor's mask: its height in the field's space, and at each texel (a row of x at a time from the mask's
        /// corner, 0 to 255) how much grass there is and how near the lawn's edge it is.
        /// </summary>
        [Serializable]
        public sealed class Layer
        {
            [Tooltip("The floor's height (m, in the field's space): where the blades root.")]
            public float height;
            [Tooltip("How much grass there is at each texel (0: none, 255: the style's full density), a row of x at a time from the mask's corner.")]
            public byte[] density;
            [Tooltip("How near each texel is to the lawn's edge (255: at it, 0: well inside), where the style's Edge Length makes the grass longer. Empty: none is.")]
            public byte[] edge;
        }

        [Tooltip("How the grass looks: its blades, colour, spacing and draw distance. Without one, a short lawn.")]
        [SerializeField] private GrassStyle style;
        [Tooltip("The compute shader that places the blades (GrassBlades.compute).")]
        [SerializeField] private ComputeShader generator;
        [Tooltip("The shader the blades are drawn with (Grass.shader).")]
        [SerializeField] private Shader shader;

        [Tooltip("The lawns under the grass, drawn with the Lawn shader: their colours are set from the style.")]
        [SerializeField] private List<Renderer> lawns = new();

        [Tooltip("The mask's corner (m, x and z in the field's space).")]
        [SerializeField] private Vector2 origin;
        [Tooltip("The mask's texel size (m).")]
        [SerializeField] private float texelSize = 0.25f;
        [Tooltip("The mask's size in texels (x, z).")]
        [SerializeField] private Vector2Int size;
        [Tooltip("The mask's layers, one per floor.")]
        [SerializeField] private List<Layer> layers = new();

        /// <summary>The style the field's drawn with: its own, or the defaults.</summary>
        public GrassStyle Style
        {
            get
            {
                if (style != null) { return style; }
                if (defaultStyle == null) { defaultStyle = ScriptableObject.CreateInstance<GrassStyle>(); defaultStyle.hideFlags = HideFlags.HideAndDontSave; }
                return defaultStyle;
            }
            set { style = value; lawnLook = default; }
        }

        private static GrassStyle defaultStyle;

        /// <summary>The mask's layers (read only: set them with SetMask).</summary>
        public IReadOnlyList<Layer> Layers => layers;

        /// <summary>The mask's corner, texel size and size in texels.</summary>
        public (Vector2 origin, float texelSize, Vector2Int size) Mask => (origin, texelSize, size);

        /// <summary>
        /// Whether the near and the far blades are drawn (both, normally): the benchmark turns them off to measure what
        /// each costs (Measure Grass, -grassParts). The blades are still placed.
        /// </summary>
        public static bool DrawNear = true, DrawFar = true;

        /// <summary>
        /// Whether every field skips placing and drawing its blades (they stay ready): how the benchmark measures a frame
        /// without the grass. Turning fields off instead (disabling them) frees what they're drawn with, and turning them
        /// on remakes it all, so a benchmark toggling them each frame would time that rebuild, which a game never does.
        /// </summary>
        public static bool Suspended;

        /// <summary>
        /// What the fields have on the GPU now (bytes): every field's mask, and each camera's blade lists and chunks;
        /// and how many blades the lists hold and were last asked for (most in a frame). For Measure Grass and the memory
        /// diagnostic.
        /// </summary>
        public static (long bytes, long capacity, long asked) GpuMemory()
        {
            long bytes = 0, capacity = 0, asked = 0;
            foreach (GrassField field in live)
            {
                if (field.maskTexture != null) { bytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(field.maskTexture); }
                foreach (Lists l in field.lists.Values)
                {
                    foreach (GraphicsBuffer b in new[] { l.chunks, l.nearBlades, l.farBlades, l.nearArgs, l.farArgs, l.counts })
                    {
                        if (b != null) { bytes += (long)b.count * b.stride; }
                    }
                    capacity += l.nearCapacity + l.farCapacity;
                    asked += l.lastAsked;
                }
            }
            return (bytes, capacity, asked);
        }

        /// <summary>
        /// For tests: how many near and far blades a camera's lists were last asked for (read back a frame or two late),
        /// and how many they hold; or null if the camera hasn't drawn this field.
        /// </summary>
        public (uint near, uint far, int nearCapacity, int farCapacity)? Counts(Camera camera) =>
            lists.TryGetValue(camera, out Lists l) ? (l.lastNear, l.lastFar, l.nearCapacity, l.farCapacity) : null;

        /// <summary>For tests (slow: it waits on the GPU): the roots of the blades in a camera's near list, as last placed.</summary>
        public Vector3[] NearRoots(Camera camera)
        {
            if (!lists.TryGetValue(camera, out Lists l)) { return System.Array.Empty<Vector3>(); }
            var args = new uint[5];
            l.nearArgs.GetData(args);
            int count = (int)Mathf.Min(args[1], l.nearCapacity);
            var blades = new float[count * (BladeStride / sizeof(float))];
            if (count > 0) { l.nearBlades.GetData(blades, 0, 0, blades.Length); }
            var roots = new Vector3[count];
            int floats = BladeStride / sizeof(float);
            for (int i = 0; i < count; i++) { roots[i] = new Vector3(blades[i * floats], blades[i * floats + 1], blades[i * floats + 2]); }
            return roots;
        }

        private static readonly List<GrassField> live = new();

        /// <summary>How many fields are enabled.</summary>
        public static int FieldCount => live.Count;

        /// <summary>Whether this platform can draw the grass (compute shaders and indirect draws).</summary>
        public static bool Supported => SystemInfo.supportsComputeShaders && SystemInfo.supportsIndirectArgumentsBuffer;

        /// <summary>
        /// Gives the field the lawns under its grass (the level builders do, as they add it), drawn with the Lawn shader:
        /// it sets their colours from its style, so they match its blades.
        /// </summary>
        public void SetLawns(IEnumerable<Renderer> lawns)
        {
            this.lawns = new List<Renderer>(lawns);
            lawnLook = default;
        }

        /// <summary>Gives the field the shaders it draws with (the level builders do, as they add it).</summary>
        public void SetShaders(ComputeShader generator, Shader shader)
        {
            this.generator = generator;
            this.shader = shader;
            Rebuild();
        }

        /// <summary>
        /// Sets the field's mask: its corner and texel size (m, in the field's space), its size in texels, and its layers,
        /// each a floor's height and its density (size.x × size.y bytes, a row of x at a time).
        /// </summary>
        public void SetMask(Vector2 origin, float texelSize, Vector2Int size, IEnumerable<Layer> layers)
        {
            this.origin = origin;
            this.texelSize = texelSize;
            this.size = size;
            this.layers = new List<Layer>(layers);
            foreach (Layer layer in this.layers)
            {
                if (layer.density == null || layer.density.Length != size.x * size.y)
                {
                    throw new ArgumentException($"A layer's density has {layer.density?.Length ?? 0} texels, not {size.x} × {size.y}.");
                }
                if (layer.edge != null && layer.edge.Length != 0 && layer.edge.Length != size.x * size.y)
                {
                    throw new ArgumentException($"A layer's edge has {layer.edge.Length} texels, not {size.x} × {size.y}.");
                }
            }
            Rebuild();
        }

        // ---- What it's drawn with: made when it's enabled, freed when it isn't. ----

        /// <summary>A chunk of the mask with grass in it: its corner (in the field's space), its layer and its floor's height.</summary>
        private struct Chunk { public Vector2 corner; public int layer; public float height; public Bounds bounds; }

        /// <summary>A group of chunks (see FindChunks): where its chunks are in the list, and the box round them all.</summary>
        private struct Group { public int first, count; public Bounds bounds; }

        /// <summary>The chunks are culled in groups this many chunks square first.</summary>
        private const int GroupChunks = 4;

        private readonly List<Group> groups = new();

        /// <summary>The transform the chunks' world boxes (Chunk.bounds) and the whole field's (fieldBounds) were worked out for.</summary>
        private Matrix4x4 boundsFor;
        private Bounds fieldBounds;

        /// <summary>
        /// Works out the chunks' world boxes (each its floor, up to nothing: the blades' height is added as they're
        /// culled) and the field's, if the field's moved since (they're worked out once, not each frame: a field can have
        /// over a thousand chunks).
        /// </summary>
        private void UpdateBounds(Matrix4x4 toWorld)
        {
            if (toWorld == boundsFor && chunks.Count > 0 && chunks[0].bounds.size != Vector3.zero) { return; }
            boundsFor = toWorld;
            float chunkSize = ChunkTexels * texelSize;
            for (int i = 0; i < chunks.Count; i++)
            {
                Chunk c = chunks[i];
                c.bounds = WorldBounds(toWorld, c.corner, new Vector2(chunkSize, chunkSize), c.height, 0f);
                chunks[i] = c;
            }
            for (int g = 0; g < groups.Count; g++)
            {
                Group group = groups[g];
                group.bounds = chunks[group.first].bounds;
                for (int i = group.first + 1; i < group.first + group.count; i++) { group.bounds.Encapsulate(chunks[i].bounds); }
                groups[g] = group;
            }
            var extent = new Vector2(size.x * texelSize, size.y * texelSize);
            fieldBounds = WorldBounds(toWorld, origin, extent, layers[0].height, 0f);
            foreach (Layer layer in layers) { fieldBounds.Encapsulate(WorldBounds(toWorld, origin, extent, layer.height, 0f)); }
        }

        /// <summary>A blade, as the compute shader writes it and Grass.shader reads it: 11 floats (Blade, in GrassCommon.hlsl).</summary>
        private const int BladeStride = 11 * sizeof(float);

        private readonly List<Chunk> chunks = new();
        private Vector4[] visible = Array.Empty<Vector4>();
        private readonly Vector4[] planes = new Vector4[6];
        private readonly Plane[] frustum = new Plane[6];
        private Texture2DArray maskTexture;
        private Material material, farMaterial;

        /// <summary>The coarsest grid step a chunk's looked at with (every 8th cell each way: see GrassBlades.compute, Rank).</summary>
        private const int CoarsestStep = 3;

        /// <summary>The visible chunks by grid step (0: every cell, 1: every 2nd, ...), before they go to the GPU in that order.</summary>
        private readonly List<Vector4>[] byStep = { new(), new(), new(), new() };
        private GraphicsBuffer nearIndices, farIndices;
        private int segments;
        private bool ready, stale;

        /// <summary>
        /// A camera's own blade lists, the chunks it sees, and their draw arguments. Each camera has its own (the game's,
        /// the Scene view's), so one camera placing its blades can never change what another draws: a camera drawing
        /// another's blades would show whole stretches of them culled and thinned for somewhere else, for a frame.
        /// </summary>
        private sealed class Lists
        {
            public GraphicsBuffer chunks, nearBlades, farBlades, nearArgs, farArgs, counts;
            public int nearCapacity = StartNear, farCapacity = StartFar;
            public bool readingBack;
            public long lastAsked;
            public readonly MaterialPropertyBlock nearProperties = new(), farProperties = new();

            public Lists(int chunkCount)
            {
                chunks = new GraphicsBuffer(GraphicsBuffer.Target.Structured, chunkCount, 4 * sizeof(float));
                nearArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw, 5, sizeof(uint));
                farArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw, 5, sizeof(uint));
                counts = new GraphicsBuffer(GraphicsBuffer.Target.Raw, 2, sizeof(uint));
                MakeBlades();
            }

            public void MakeBlades()
            {
                nearBlades?.Release();
                farBlades?.Release();
                nearBlades = new GraphicsBuffer(GraphicsBuffer.Target.Structured, nearCapacity, BladeStride);
                farBlades = new GraphicsBuffer(GraphicsBuffer.Target.Structured, farCapacity, BladeStride);
            }

            public void Release()
            {
                foreach (GraphicsBuffer b in new[] { chunks, nearBlades, farBlades, nearArgs, farArgs, counts }) { b?.Release(); }
                chunks = nearBlades = farBlades = nearArgs = farArgs = counts = null;
            }

            /// <summary>
            /// Fits the blade lists to what the frame read back asked for: grown (to the next power of two) if they were
            /// short, up to MostBlades; halved once they've held over four times what was asked for, for ShrinkAfter
            /// seconds together, down to their starting size. So a view over a vast lawn doesn't keep its lists for good.
            /// </summary>
            public void Fit(AsyncGPUReadbackRequest request)
            {
                readingBack = false;
                if (counts == null || request.hasError) { return; }
                Unity.Collections.NativeArray<uint> asked = request.GetData<uint>();
                lastNear = asked[0];
                lastFar = asked[1];
                lastAsked = (long)asked[0] + asked[1];
                float now = Time.realtimeSinceStartup;
                bool remake = false;
                (nearCapacity, nearRoomySince, remake) = FitOne(nearCapacity, asked[0], StartNear, nearRoomySince, now, remake);
                (farCapacity, farRoomySince, remake) = FitOne(farCapacity, asked[1], StartFar, farRoomySince, now, remake);
                if (remake) { MakeBlades(); }
            }

            private static (int capacity, float roomySince, bool remake) FitOne(int capacity, uint asked, int start, float roomySince, float now, bool remake)
            {
                if (asked > capacity && capacity < MostBlades)
                {
                    return (Mathf.Min(MostBlades, Mathf.NextPowerOfTwo((int)asked + 1)), float.MaxValue, true);
                }
                if (capacity > start && asked * 4L < capacity)
                {
                    if (roomySince == float.MaxValue) { return (capacity, now, remake); }
                    if (now - roomySince >= ShrinkAfter) { return (Mathf.Max(start, capacity / 2), float.MaxValue, true); }
                    return (capacity, roomySince, remake);
                }
                return (capacity, float.MaxValue, remake);
            }

            private float nearRoomySince = float.MaxValue, farRoomySince = float.MaxValue;
            public uint lastNear, lastFar;
        }

        /// <summary>A camera's blade lists start this big (near, far), never get bigger than MostBlades, and shrink after ShrinkAfter seconds roomy (see Lists.Fit).</summary>
        private const int StartNear = 65536, StartFar = 131072, MostBlades = 1 << 20;
        private const float ShrinkAfter = 5f;

        private readonly Dictionary<Camera, Lists> lists = new();
        private readonly List<Camera> gone = new();
        private int kernelGenerate, kernelFinish, kernelBend;
        private static readonly uint[] ResetArgs = new uint[5];

        private static class Ids
        {
            public static readonly int Chunks = Shader.PropertyToID("_Chunks"), Mask = Shader.PropertyToID("_Mask");
            public static readonly int NearBlades = Shader.PropertyToID("_NearBlades"), FarBlades = Shader.PropertyToID("_FarBlades");
            public static readonly int NearArgs = Shader.PropertyToID("_NearArgs"), FarArgs = Shader.PropertyToID("_FarArgs"), Counts = Shader.PropertyToID("_Counts");
            public static readonly int NearCapacity = Shader.PropertyToID("_NearCapacity"), FarCapacity = Shader.PropertyToID("_FarCapacity");
            public static readonly int LocalToWorld = Shader.PropertyToID("_LocalToWorld"), CameraPosition = Shader.PropertyToID("_CameraPosition");
            public static readonly int FrustumPlanes = Shader.PropertyToID("_FrustumPlanes");
            public static readonly int MaskOrigin = Shader.PropertyToID("_MaskOrigin"), MaskSize = Shader.PropertyToID("_MaskSize");
            public static readonly int Spacing = Shader.PropertyToID("_Spacing"), BladesPerSide = Shader.PropertyToID("_BladesPerSide");
            public static readonly int Distances = Shader.PropertyToID("_Distances"), Thinning = Shader.PropertyToID("_Thinning");
            public static readonly int Length = Shader.PropertyToID("_Length"), Width = Shader.PropertyToID("_Width"), Bend = Shader.PropertyToID("_Bend");
            public static readonly int Blades = Shader.PropertyToID("_Blades"), Segments = Shader.PropertyToID("_Segments");
            public static readonly int RootColour = Shader.PropertyToID("_RootColour"), TipColour = Shader.PropertyToID("_TipColour");
            public static readonly int ColourVariation = Shader.PropertyToID("_ColourVariation"), ClumpTint = Shader.PropertyToID("_ClumpTint");
            public static readonly int RootShade = Shader.PropertyToID("_RootShade"), Translucency = Shader.PropertyToID("_Translucency");
            public static readonly int SegmentLength = Shader.PropertyToID("_SegmentLength"), Droop = Shader.PropertyToID("_Droop");
            public static readonly int DrawDistance = Shader.PropertyToID("_DrawDistance"), Clumps = Shader.PropertyToID("_Clumps");
            public static readonly int ClumpSize = Shader.PropertyToID("_ClumpSize"), GroundShade = Shader.PropertyToID("_GroundShade");
            public static readonly int ThinFrom = Shader.PropertyToID("_ThinFrom"), DetailDistance = Shader.PropertyToID("_DetailDistance");
            public static readonly int BendMap = Shader.PropertyToID("_BendMap"), BendWindow = Shader.PropertyToID("_BendWindow");
            public static readonly int ChunkOffset = Shader.PropertyToID("_ChunkOffset"), Step = Shader.PropertyToID("_Step"), Tallest = Shader.PropertyToID("_Tallest");
            public static readonly int Wind = Shader.PropertyToID("_Wind"), WindGusts = Shader.PropertyToID("_WindGusts"), Sway = Shader.PropertyToID("_Sway");
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            FindShadersInEditor();
#endif
            Rebuild();
            live.Add(this);
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            live.Remove(this);
            Release();
        }

        // Remade before the next camera renders, not here: OnValidate mustn't destroy what it's replacing.
        private void OnValidate() => stale = true;

        /// <summary>Remakes what the field's drawn with from its mask and shaders.</summary>
        private void Rebuild()
        {
            Release();
            if (!isActiveAndEnabled || !Supported || generator == null || shader == null || layers.Count == 0 || size.x <= 0 || size.y <= 0) { return; }

            FindChunks();
            if (chunks.Count == 0) { return; }
            boundsFor = default;
            // Density in red, the edge in green.
            maskTexture = new Texture2DArray(size.x, size.y, layers.Count, TextureFormat.RG16, false, true)
            {
                name = "Grass Mask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave,
            };
            var texels = new byte[2 * size.x * size.y];
            for (int i = 0; i < layers.Count; i++)
            {
                byte[] density = layers[i].density, edge = layers[i].edge;
                bool hasEdge = edge != null && edge.Length == density.Length;
                for (int k = 0; k < density.Length; k++)
                {
                    texels[2 * k] = density[k];
                    texels[2 * k + 1] = hasEdge ? edge[k] : (byte)0;
                }
                maskTexture.SetPixelData(texels, 0, i);
            }
            maskTexture.Apply(false, true);

            material = new Material(shader) { name = "Grass Blades", hideFlags = HideFlags.HideAndDontSave };
            // Far blades are drawn once, without the depth (and normals) prepass: a pixel or two each, they'd cost as
            // much again in it, for the ambient occlusion of what's near them, which nothing there shows.
            farMaterial = new Material(material) { name = "Grass Blades (far)", hideFlags = HideFlags.HideAndDontSave };
            farMaterial.SetShaderPassEnabled("DepthOnly", false);
            farMaterial.SetShaderPassEnabled("DepthNormals", false);
            visible = new Vector4[chunks.Count];
            kernelGenerate = generator.FindKernel("Generate");
            kernelFinish = generator.FindKernel("Finish");
            kernelBend = generator.FindKernel("Bend");
            segments = 0;
            lawnLook = default;
            ready = true;
        }

        /// <summary>The index buffer of a blade drawn in n segments: a strip of quads up to a tip triangle (see Grass.shader).</summary>
        private static GraphicsBuffer BladeIndices(int n)
        {
            var indices = new List<int>();
            for (int s = 0; s < n - 1; s++)
            {
                int a = 2 * s;
                indices.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
            int last = 2 * (n - 1);
            indices.AddRange(new[] { last, 2 * n, last + 1 });
            var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Index, indices.Count, sizeof(int));
            buffer.SetData(indices);
            return buffer;
        }

        private void Release()
        {
            ready = false;
            foreach (Lists l in lists.Values) { l.Release(); }
            lists.Clear();
            nearIndices?.Release();
            farIndices?.Release();
            nearIndices = farIndices = null;
            if (maskTexture != null) { DestroyImmediate(maskTexture); }
            if (material != null) { DestroyImmediate(material); }
            if (farMaterial != null) { DestroyImmediate(farMaterial); }
            chunks.Clear();
        }

        /// <summary>The chunks of the mask with any grass in them.</summary>
        private void FindChunks()
        {
            chunks.Clear();
            groups.Clear();
            // In groups of GroupChunks × GroupChunks chunks (a layer's), each group's chunks together in the list, so a
            // group out of view or out of reach is passed over without testing its chunks.
            int groupTexels = ChunkTexels * GroupChunks;
            for (int l = 0; l < layers.Count; l++)
            {
                byte[] density = layers[l].density;
                for (int gz = 0; gz < size.y; gz += groupTexels)
                {
                    for (int gx = 0; gx < size.x; gx += groupTexels)
                    {
                        int first = chunks.Count;
                        for (int cz = gz; cz < Mathf.Min(gz + groupTexels, size.y); cz += ChunkTexels)
                        {
                            for (int cx = gx; cx < Mathf.Min(gx + groupTexels, size.x); cx += ChunkTexels)
                            {
                                bool any = false;
                                for (int z = cz; z < Mathf.Min(cz + ChunkTexels, size.y) && !any; z++)
                                {
                                    for (int x = cx; x < Mathf.Min(cx + ChunkTexels, size.x); x++)
                                    {
                                        if (density[z * size.x + x] != 0) { any = true; break; }
                                    }
                                }
                                if (any) { chunks.Add(new Chunk { corner = origin + new Vector2(cx, cz) * texelSize, layer = l, height = layers[l].height }); }
                            }
                        }
                        if (chunks.Count > first) { groups.Add(new Group { first = first, count = chunks.Count - first }); }
                    }
                }
            }
        }

        private static bool Draws(Camera camera) => camera.cameraType is CameraType.Game or CameraType.SceneView;

        /// <summary>The CPU's part of placing and drawing a field's blades for a camera (Measure Grass reports it).</summary>
        public static readonly Unity.Profiling.ProfilerMarker PlacingMarker = new("Grass.Place");

        /// <summary>Places the blades for a camera about to render, and draws them for it (see the class).</summary>
        private void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            using Unity.Profiling.ProfilerMarker.AutoScope placing = PlacingMarker.Auto();
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            try { PlaceAndDraw(camera); }
            finally { PlacingSeconds += (System.Diagnostics.Stopwatch.GetTimestamp() - started) / (double)System.Diagnostics.Stopwatch.Frequency; }
        }

        /// <summary>The CPU time every field has spent placing and drawing blades (s), for Measure Grass to difference.</summary>
        public static double PlacingSeconds;

        private void PlaceAndDraw(Camera camera)
        {
            if (Suspended) { return; }
            if (stale && isActiveAndEnabled) { stale = false; Rebuild(); }
            if (!ready || !Draws(camera)) { return; }
            GrassStyle s = Style;
            UpdateLawns(s);
            float chunkSize = ChunkTexels * texelSize;
            // The grid fits a whole number of cells in a chunk, so every cell is in exactly one chunk.
            // A multiple of the coarsest grid step, so every step's cells fall on a chunk's.
            int coarsest = 1 << CoarsestStep;
            int perSide = Mathf.Max(coarsest, Mathf.CeilToInt(chunkSize / s.SpacingNow / coarsest) * coarsest);
            float spacing = chunkSize / perSide;
            float draw = s.drawDistance, tallest = s.Tallest;

            // Which chunks are in view and near enough.
            GeometryUtility.CalculateFrustumPlanes(camera, frustum);
            Matrix4x4 toWorld = transform.localToWorldMatrix;
            Vector3 eye = camera.transform.position;
            float thinFrom = Mathf.Clamp(s.thinFrom, 0.1f, draw * 0.99f);
            foreach (List<Vector4> step in byStep) { step.Clear(); }
            UpdateBounds(toWorld);
            // Up to the tallest blade, and out past the chunk as far as a pushed blade leans (1.5 of its lengths).
            var grow = new Vector3(3f * tallest, tallest, 3f * tallest);
            var lift = new Vector3(0f, tallest / 2f, 0f);
            for (int i = 0; i < 6; i++) { planes[i] = new Vector4(frustum[i].normal.x, frustum[i].normal.y, frustum[i].normal.z, frustum[i].distance); }
            float drawSquared = draw * draw;
            foreach (Group group in groups)
            {
                Vector3 groupCentre = group.bounds.center + lift, groupExtent = (group.bounds.size + grow) * 0.5f;
                if (SqrDistance(groupCentre, groupExtent, eye) > drawSquared || !InView(groupCentre, groupExtent)) { continue; }
                for (int index = group.first; index < group.first + group.count; index++)
                {
                Chunk c = chunks[index];
                // Tested here, not with Bounds.SqrDistance and GeometryUtility.TestPlanesAABB: those call into the engine
                // (the planes copied over each time), which over a field's thousand-odd chunks was most of its CPU time.
                Vector3 centre = c.bounds.center + lift, extent = (c.bounds.size + grow) * 0.5f;
                float nearest = SqrDistance(centre, extent, eye);
                if (nearest > drawSquared || !InView(centre, extent)) { continue; }
                // The most of its cells kept anywhere in it (at its nearest), and so the coarsest grid that has them all.
                float near = thinFrom / Mathf.Max(Mathf.Sqrt(nearest), 0.01f);
                float keep = Mathf.Clamp(near * near, s.thinTo, 1f);
                int coarser = 0;
                while (coarser < CoarsestStep && Mathf.Pow(0.25f, coarser + 1) >= keep) { coarser++; }
                byStep[coarser].Add(new Vector4(c.corner.x, c.corner.y, c.layer, c.height));
                }
            }
            int count = 0;
            foreach (List<Vector4> step in byStep) { foreach (Vector4 v in step) { visible[count++] = v; } }
            if (count == 0)
            {
                // Nothing in view: nothing's drawn, and the lists say so (not what was in view last time).
                if (lists.TryGetValue(camera, out Lists idle) && nearIndices != null)
                {
                    ResetArgs[0] = (uint)nearIndices.count;
                    idle.nearArgs.SetData(ResetArgs);
                    ResetArgs[0] = (uint)farIndices.count;
                    idle.farArgs.SetData(ResetArgs);
                }
                return;
            }

            if (segments != s.segments)
            {
                segments = s.segments;
                nearIndices?.Release();
                farIndices?.Release();
                nearIndices = BladeIndices(segments);
                farIndices = BladeIndices(1);
            }
            Lists l = ListsFor(camera);
            ResetArgs[0] = (uint)nearIndices.count;
            l.nearArgs.SetData(ResetArgs);
            ResetArgs[0] = (uint)farIndices.count;
            l.farArgs.SetData(ResetArgs);
            l.chunks.SetData(visible, 0, 0, count);

            ComputeShader g = generator;
            g.SetBuffer(kernelGenerate, Ids.Chunks, l.chunks);
            g.SetTexture(kernelGenerate, Ids.Mask, maskTexture);
            g.SetBuffer(kernelGenerate, Ids.NearBlades, l.nearBlades);
            g.SetBuffer(kernelGenerate, Ids.FarBlades, l.farBlades);
            g.SetBuffer(kernelGenerate, Ids.NearArgs, l.nearArgs);
            g.SetBuffer(kernelGenerate, Ids.FarArgs, l.farArgs);
            g.SetInt(Ids.NearCapacity, l.nearCapacity);
            g.SetInt(Ids.FarCapacity, l.farCapacity);
            g.SetMatrix(Ids.LocalToWorld, toWorld);
            g.SetVector(Ids.CameraPosition, eye);
            g.SetVectorArray(Ids.FrustumPlanes, planes);
            g.SetVector(Ids.MaskOrigin, origin);
            g.SetVector(Ids.MaskSize, new Vector2(size.x * texelSize, size.y * texelSize));
            g.SetFloat(Ids.Spacing, spacing);
            g.SetInt(Ids.BladesPerSide, perSide);
            g.SetFloat(Ids.Tallest, tallest);
            g.SetVector(Ids.Distances, new Vector4(draw, s.detailDistance, thinFrom, 0f));
            g.SetVector(Ids.Thinning, new Vector4(s.thinTo, 0f, 0f, 0f));
            g.SetVector(Ids.Length, s.length);
            g.SetVector(Ids.Width, s.width);
            g.SetVector(Ids.Bend, s.bend);
            g.SetVector(Ids.Clumps, new Vector4(s.clumpSize, s.clumpLength, s.clumpLean, s.edgeLength));

            // The wind, and what's been walked through (the bend map, updated once a frame, before any field's blades).
            GrassBending.Update(g, kernelBend, camera);
            g.SetTexture(kernelGenerate, Ids.BendMap, GrassBending.Map != null ? GrassBending.Map : Texture2D.blackTexture);
            g.SetVector(Ids.BendWindow, GrassBending.Window);
            GrassWind wind = GrassWind.Active != null ? GrassWind.Active : GrassWind.Defaults;
            Vector2 blowing = wind.Direction;
            g.SetVector(Ids.Wind, new Vector4(blowing.x, blowing.y, wind.strength, GrassWind.Time));
            g.SetVector(Ids.WindGusts, new Vector4(wind.gustSize, wind.gustSpeed, wind.gustStrength, wind.flutter));
            g.SetVector(Ids.Sway, new Vector4(s.sway, wind.flutterSpeed, 0f, 0f));
            // A dispatch per grid step, each over its chunks, every step-th cell each way.
            int offset = 0;
            for (int step = 0; step <= CoarsestStep; step++)
            {
                int stepCount = byStep[step].Count;
                if (stepCount == 0) { continue; }
                g.SetInt(Ids.ChunkOffset, offset);
                g.SetInt(Ids.Step, 1 << step);
                int groups = Mathf.CeilToInt((perSide >> step) / 8f);
                g.Dispatch(kernelGenerate, groups, groups, stepCount);
                offset += stepCount;
            }

            // Clamp the counts to what the lists hold, keeping what was asked for, so the lists grow if they were short.
            g.SetBuffer(kernelFinish, Ids.NearArgs, l.nearArgs);
            g.SetBuffer(kernelFinish, Ids.FarArgs, l.farArgs);
            g.SetBuffer(kernelFinish, Ids.Counts, l.counts);
            g.Dispatch(kernelFinish, 1, 1, 1);
            if (!l.readingBack)
            {
                l.readingBack = true;
                AsyncGPUReadback.Request(l.counts, l.Fit);
            }

            SetLook(material, s, draw, thinFrom);
            SetLook(farMaterial, s, draw, thinFrom);
            var all = new Bounds(fieldBounds.center + lift, fieldBounds.size + grow);
            if (DrawNear) { Draw(camera, all, l.nearBlades, l.nearArgs, nearIndices, segments, l.nearProperties, material); }
            if (DrawFar) { Draw(camera, all, l.farBlades, l.farArgs, farIndices, 1, l.farProperties, farMaterial); }
        }

        /// <summary>The squared distance from a point to a box (its centre and half size), 0 inside it.</summary>
        private static float SqrDistance(Vector3 centre, Vector3 extent, Vector3 point)
        {
            float dx = Mathf.Max(Mathf.Abs(point.x - centre.x) - extent.x, 0f);
            float dy = Mathf.Max(Mathf.Abs(point.y - centre.y) - extent.y, 0f);
            float dz = Mathf.Max(Mathf.Abs(point.z - centre.z) - extent.z, 0f);
            return dx * dx + dy * dy + dz * dz;
        }

        /// <summary>Whether a box (its centre and half size) is at least partly inside the view's planes (planes, this frame's).</summary>
        private bool InView(Vector3 centre, Vector3 extent)
        {
            for (int i = 0; i < 6; i++)
            {
                Vector4 p = planes[i];
                float reach = extent.x * Mathf.Abs(p.x) + extent.y * Mathf.Abs(p.y) + extent.z * Mathf.Abs(p.z);
                if (p.x * centre.x + p.y * centre.y + p.z * centre.z + p.w < -reach) { return false; }
            }
            return true;
        }

        /// <summary>Gives a blade material the style's values.</summary>
        private static void SetLook(Material m, GrassStyle s, float draw, float thinFrom)
        {
            m.SetColor(Ids.RootColour, s.rootColour);
            m.SetColor(Ids.TipColour, s.tipColour);
            m.SetFloat(Ids.ColourVariation, s.colourVariation);
            m.SetFloat(Ids.ClumpTint, s.clumpTint);
            m.SetFloat(Ids.RootShade, s.rootShade);
            m.SetFloat(Ids.Translucency, s.translucency);
            m.SetFloat(Ids.SegmentLength, s.segmentLength);
            m.SetFloat(Ids.Droop, s.droop);
            m.SetFloat(Ids.DrawDistance, draw);
            m.SetFloat(Ids.DetailDistance, s.detailDistance);
            m.SetFloat(Ids.ThinFrom, thinFrom);
        }

        /// <summary>A camera's lists (see Lists): made the first time it renders, and freed once it's gone.</summary>
        private Lists ListsFor(Camera camera)
        {
            if (lists.TryGetValue(camera, out Lists l)) { return l; }
            gone.Clear();
            foreach (Camera c in lists.Keys) { if (c == null) { gone.Add(c); } }
            foreach (Camera c in gone) { lists[c].Release(); lists.Remove(c); }
            l = new Lists(chunks.Count);
            lists.Add(camera, l);
            return l;
        }

        /// <summary>What the lawns were last given, so they're only given it again when the style's changed.</summary>
        private (Color root, Color tip, float clumpSize, float clumpTint, float groundShade, float thinFrom, float draw) lawnLook;
        private MaterialPropertyBlock lawnProperties;

        /// <summary>Gives the lawns the style's colours, clumps and distances (see SetLawns), if they've changed.</summary>
        private void UpdateLawns(GrassStyle s)
        {
            var look = (s.rootColour, s.tipColour, s.clumpSize, s.clumpTint, s.groundShade, Mathf.Clamp(s.thinFrom, 0.1f, s.drawDistance * 0.99f), s.drawDistance);
            if (look.Equals(lawnLook) || lawns.Count == 0) { return; }
            lawnLook = look;
            lawnProperties ??= new MaterialPropertyBlock();
            lawnProperties.SetColor(Ids.RootColour, s.rootColour);
            lawnProperties.SetColor(Ids.TipColour, s.tipColour);
            lawnProperties.SetFloat(Ids.ClumpSize, s.clumpSize);
            lawnProperties.SetFloat(Ids.ClumpTint, s.clumpTint);
            lawnProperties.SetFloat(Ids.GroundShade, s.groundShade);
            lawnProperties.SetFloat(Ids.ThinFrom, look.Item6);
            lawnProperties.SetFloat(Ids.DrawDistance, s.drawDistance);
            foreach (Renderer lawn in lawns)
            {
                if (lawn != null) { lawn.SetPropertyBlock(lawnProperties); }
            }
        }

        private void Draw(Camera camera, Bounds bounds, GraphicsBuffer blades, GraphicsBuffer args, GraphicsBuffer indices, int n, MaterialPropertyBlock properties, Material drawWith)
        {
            properties.SetBuffer(Ids.Blades, blades);
            properties.SetInt(Ids.Segments, n);
            var parameters = new RenderParams(drawWith)
            {
                camera = camera,
                worldBounds = bounds,
                layer = gameObject.layer,
                shadowCastingMode = Style.castsShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows = true,
                matProps = properties,
            };
            Graphics.RenderPrimitivesIndexedIndirect(parameters, MeshTopology.Triangles, indices, args);
        }

        /// <summary>The world box of a rectangle of the mask (corner and sides in the field's space) on a floor, up to a height above it.</summary>
        private static Bounds WorldBounds(Matrix4x4 toWorld, Vector2 corner, Vector2 side, float floor, float up)
        {
            var b = new Bounds(toWorld.MultiplyPoint3x4(new Vector3(corner.x, floor, corner.y)), Vector3.zero);
            for (int i = 1; i < 8; i++)
            {
                b.Encapsulate(toWorld.MultiplyPoint3x4(new Vector3(corner.x + ((i & 1) != 0 ? side.x : 0f), floor + ((i & 2) != 0 ? up : 0f), corner.y + ((i & 4) != 0 ? side.y : 0f))));
            }
            return b;
        }

#if UNITY_EDITOR
        /// <summary>The field's shaders, found where they are beside this script if it hasn't been given them (one added by hand).</summary>
        private void FindShadersInEditor()
        {
            if (generator != null && shader != null) { return; }
            string folder = System.IO.Path.GetDirectoryName(UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.MonoScript.FromMonoBehaviour(this)));
            if (generator == null) { generator = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(folder + "/GrassBlades.compute"); }
            if (shader == null) { shader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(folder + "/Grass.shader"); }
        }
#endif
    }
}
