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
    /// A blade is placed by a hash of its cell, so it stays put as the camera moves. The level builders lay the mask
    /// (Graybox.Lawns); the field knows nothing of the level, the character or the camera rig. It has no collider.
    /// Without compute shaders (or indirect draws), it draws nothing, and the lawn under it is all there is.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Meridian Motion/Showcase/Grass Field")]
    public sealed class GrassField : MonoBehaviour
    {
        /// <summary>The mask's chunks are this many texels square: the unit the CPU culls by.</summary>
        public const int ChunkTexels = 32;

        /// <summary>One floor's mask: its height in the field's space, and its density (0 to 255) a texel at a time, row by row in x.</summary>
        [Serializable]
        public sealed class Layer
        {
            [Tooltip("The floor's height (m, in the field's space): where the blades root.")]
            public float height;
            [Tooltip("How much grass there is at each texel (0: none, 255: the style's full density), a row of x at a time from the mask's corner.")]
            public byte[] density;
        }

        [Tooltip("How the grass looks: its blades, colour, spacing and draw distance. Without one, a short lawn.")]
        [SerializeField] private GrassStyle style;
        [Tooltip("The compute shader that places the blades (GrassBlades.compute).")]
        [SerializeField] private ComputeShader generator;
        [Tooltip("The shader the blades are drawn with (Grass.shader).")]
        [SerializeField] private Shader shader;

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
            set => style = value;
        }

        private static GrassStyle defaultStyle;

        /// <summary>The mask's layers (read only: set them with SetMask).</summary>
        public IReadOnlyList<Layer> Layers => layers;

        /// <summary>The mask's corner, texel size and size in texels.</summary>
        public (Vector2 origin, float texelSize, Vector2Int size) Mask => (origin, texelSize, size);

        /// <summary>Whether this platform can draw the grass (compute shaders and indirect draws).</summary>
        public static bool Supported => SystemInfo.supportsComputeShaders && SystemInfo.supportsIndirectArgumentsBuffer;

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
            }
            Rebuild();
        }

        // ---- What it's drawn with: made when it's enabled, freed when it isn't. ----

        /// <summary>A chunk of the mask with grass in it: its corner (in the field's space), its layer and its floor's height.</summary>
        private struct Chunk { public Vector2 corner; public int layer; public float height; }

        /// <summary>A blade, as the compute shader writes it and Grass.shader reads it: 8 floats.</summary>
        private const int BladeStride = 8 * sizeof(float);

        private readonly List<Chunk> chunks = new();
        private Vector4[] visible = Array.Empty<Vector4>();
        private readonly Vector4[] planes = new Vector4[6];
        private readonly Plane[] frustum = new Plane[6];
        private Texture2DArray maskTexture;
        private Material material;
        private GraphicsBuffer chunkBuffer, nearBlades, farBlades, nearArgs, farArgs, counts, nearIndices, farIndices;
        private int nearCapacity = 65536, farCapacity = 131072, segments;
        private MaterialPropertyBlock nearProperties, farProperties;
        private bool readingBack, ready, stale;
        private int kernelGenerate, kernelFinish;
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
            public static readonly int ColourVariation = Shader.PropertyToID("_ColourVariation");
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            FindShadersInEditor();
#endif
            Rebuild();
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
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
            maskTexture = new Texture2DArray(size.x, size.y, layers.Count, TextureFormat.R8, false, true)
            {
                name = "Grass Mask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave,
            };
            for (int i = 0; i < layers.Count; i++) { maskTexture.SetPixelData(layers[i].density, 0, i); }
            maskTexture.Apply(false, true);

            material = new Material(shader) { name = "Grass Blades", hideFlags = HideFlags.HideAndDontSave };
            chunkBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, chunks.Count, 4 * sizeof(float));
            visible = new Vector4[chunks.Count];
            nearArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw, 5, sizeof(uint));
            farArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw, 5, sizeof(uint));
            counts = new GraphicsBuffer(GraphicsBuffer.Target.Raw, 2, sizeof(uint));
            MakeBladeBuffers();
            nearProperties = new MaterialPropertyBlock();
            farProperties = new MaterialPropertyBlock();
            kernelGenerate = generator.FindKernel("Generate");
            kernelFinish = generator.FindKernel("Finish");
            segments = 0;
            ready = true;
        }

        private void MakeBladeBuffers()
        {
            nearBlades?.Release();
            farBlades?.Release();
            nearBlades = new GraphicsBuffer(GraphicsBuffer.Target.Structured, nearCapacity, BladeStride);
            farBlades = new GraphicsBuffer(GraphicsBuffer.Target.Structured, farCapacity, BladeStride);
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
            foreach (GraphicsBuffer b in new[] { chunkBuffer, nearBlades, farBlades, nearArgs, farArgs, counts, nearIndices, farIndices }) { b?.Release(); }
            chunkBuffer = nearBlades = farBlades = nearArgs = farArgs = counts = nearIndices = farIndices = null;
            if (maskTexture != null) { DestroyImmediate(maskTexture); }
            if (material != null) { DestroyImmediate(material); }
            chunks.Clear();
        }

        /// <summary>The chunks of the mask with any grass in them.</summary>
        private void FindChunks()
        {
            chunks.Clear();
            for (int l = 0; l < layers.Count; l++)
            {
                byte[] density = layers[l].density;
                for (int cz = 0; cz < size.y; cz += ChunkTexels)
                {
                    for (int cx = 0; cx < size.x; cx += ChunkTexels)
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
            }
        }

        private static bool Draws(Camera camera) => camera.cameraType is CameraType.Game or CameraType.SceneView;

        /// <summary>Places the blades for a camera about to render, and draws them for it (see the class).</summary>
        private void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (stale && isActiveAndEnabled) { stale = false; Rebuild(); }
            if (!ready || !Draws(camera)) { return; }
            GrassStyle s = Style;
            float chunkSize = ChunkTexels * texelSize;
            // The grid fits a whole number of cells in a chunk, so every cell is in exactly one chunk.
            int perSide = Mathf.Max(1, Mathf.CeilToInt(chunkSize / s.spacing));
            float spacing = chunkSize / perSide;
            float draw = s.drawDistance, tallest = s.length.y * 1.2f;

            // Which chunks are in view and near enough.
            GeometryUtility.CalculateFrustumPlanes(camera, frustum);
            Matrix4x4 toWorld = transform.localToWorldMatrix;
            Vector3 eye = camera.transform.position;
            int count = 0;
            foreach (Chunk c in chunks)
            {
                Bounds b = WorldBounds(toWorld, c.corner, new Vector2(chunkSize, chunkSize), c.height, tallest);
                if (b.SqrDistance(eye) > draw * draw || !GeometryUtility.TestPlanesAABB(frustum, b)) { continue; }
                visible[count++] = new Vector4(c.corner.x, c.corner.y, c.layer, c.height);
            }
            if (count == 0) { return; }
            for (int i = 0; i < 6; i++) { planes[i] = new Vector4(frustum[i].normal.x, frustum[i].normal.y, frustum[i].normal.z, frustum[i].distance); }

            if (segments != s.segments)
            {
                segments = s.segments;
                nearIndices?.Release();
                farIndices?.Release();
                nearIndices = BladeIndices(segments);
                farIndices = BladeIndices(1);
            }
            ResetArgs[0] = (uint)nearIndices.count;
            nearArgs.SetData(ResetArgs);
            ResetArgs[0] = (uint)farIndices.count;
            farArgs.SetData(ResetArgs);
            chunkBuffer.SetData(visible, 0, 0, count);

            ComputeShader g = generator;
            g.SetBuffer(kernelGenerate, Ids.Chunks, chunkBuffer);
            g.SetTexture(kernelGenerate, Ids.Mask, maskTexture);
            g.SetBuffer(kernelGenerate, Ids.NearBlades, nearBlades);
            g.SetBuffer(kernelGenerate, Ids.FarBlades, farBlades);
            g.SetBuffer(kernelGenerate, Ids.NearArgs, nearArgs);
            g.SetBuffer(kernelGenerate, Ids.FarArgs, farArgs);
            g.SetInt(Ids.NearCapacity, nearCapacity);
            g.SetInt(Ids.FarCapacity, farCapacity);
            g.SetMatrix(Ids.LocalToWorld, toWorld);
            g.SetVector(Ids.CameraPosition, eye);
            g.SetVectorArray(Ids.FrustumPlanes, planes);
            g.SetVector(Ids.MaskOrigin, origin);
            g.SetVector(Ids.MaskSize, new Vector2(size.x * texelSize, size.y * texelSize));
            g.SetFloat(Ids.Spacing, spacing);
            g.SetInt(Ids.BladesPerSide, perSide);
            g.SetVector(Ids.Distances, new Vector4(draw, s.detailDistance, Mathf.Min(s.thinFrom, draw * 0.99f), 0f));
            g.SetVector(Ids.Thinning, new Vector4(s.thinTo, 0f, 0f, 0f));
            g.SetVector(Ids.Length, s.length);
            g.SetVector(Ids.Width, s.width);
            g.SetVector(Ids.Bend, s.bend);
            int groups = Mathf.CeilToInt(perSide / 8f);
            g.Dispatch(kernelGenerate, groups, groups, count);

            // Clamp the counts to what the lists hold, keeping what was asked for, so the lists grow if they were short.
            g.SetBuffer(kernelFinish, Ids.NearArgs, nearArgs);
            g.SetBuffer(kernelFinish, Ids.FarArgs, farArgs);
            g.SetBuffer(kernelFinish, Ids.Counts, counts);
            g.Dispatch(kernelFinish, 1, 1, 1);
            if (!readingBack)
            {
                readingBack = true;
                AsyncGPUReadback.Request(counts, GrowIfShort);
            }

            material.SetColor(Ids.RootColour, s.rootColour);
            material.SetColor(Ids.TipColour, s.tipColour);
            material.SetFloat(Ids.ColourVariation, s.colourVariation);
            var extent = new Vector2(size.x * texelSize, size.y * texelSize);
            Bounds all = WorldBounds(toWorld, origin, extent, layers[0].height, tallest);
            foreach (Layer layer in layers) { all.Encapsulate(WorldBounds(toWorld, origin, extent, layer.height, tallest)); }
            Draw(camera, all, nearBlades, nearArgs, nearIndices, segments, nearProperties);
            Draw(camera, all, farBlades, farArgs, farIndices, 1, farProperties);
        }

        private void Draw(Camera camera, Bounds bounds, GraphicsBuffer blades, GraphicsBuffer args, GraphicsBuffer indices, int n, MaterialPropertyBlock properties)
        {
            properties.SetBuffer(Ids.Blades, blades);
            properties.SetInt(Ids.Segments, n);
            var parameters = new RenderParams(material)
            {
                camera = camera,
                worldBounds = bounds,
                layer = gameObject.layer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                matProps = properties,
            };
            Graphics.RenderPrimitivesIndexedIndirect(parameters, MeshTopology.Triangles, indices, args);
        }

        /// <summary>Grows the blade lists if the last frame asked for more blades than they hold (they never shrink).</summary>
        private void GrowIfShort(AsyncGPUReadbackRequest request)
        {
            readingBack = false;
            if (!ready || request.hasError) { return; }
            Unity.Collections.NativeArray<uint> asked = request.GetData<uint>();
            bool grow = false;
            const int most = 1 << 22;
            if (asked[0] > nearCapacity && nearCapacity < most) { nearCapacity = Mathf.Min(most, Mathf.NextPowerOfTwo((int)asked[0] + 1)); grow = true; }
            if (asked[1] > farCapacity && farCapacity < most) { farCapacity = Mathf.Min(most, Mathf.NextPowerOfTwo((int)asked[1] + 1)); grow = true; }
            if (grow) { MakeBladeBuffers(); }
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
