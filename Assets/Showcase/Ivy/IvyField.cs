using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MeridianMotion.Showcase.Ivy
{
    /// <summary>
    /// Ivy over a level's walls, placed on the GPU before each camera renders, as the grass is (ReferenceAssets/OpenTasks.md,
    /// category Q). It holds the wall faces the ivy grows on (rectangles in its space: a corner, the ways along, up and
    /// out, a size), each with its mask (IvyGrowth: how dense the leaves are, and where the stems are) and its stems.
    /// Per camera:
    /// <list type="bullet">
    /// <item>the faces' chunks that are in view, within the style's Draw Distance and turned toward the camera (a face's
    /// back is never seen, so the ivy behind a wall costs nothing) go to IvyLeaves.compute, which places a leaf on every
    /// cell of a fine grid over them where the mask has growth, thinned with distance by a rank on each cell (so nothing
    /// pops), into a near and a far list, drawn with an indirect draw each (near: an ivy leaf's outline; far: a diamond);</item>
    /// <item>the stems, in blocks, those within the style's Stem Distance and turned toward it;</item>
    /// <item>the far layer: a quad over each face, coloured where the mask says the ivy grows, which comes in where the
    /// leaves thin out and stop, so a far wall still carries its ivy (IvyFar.shader).</item>
    /// </list>
    /// How it looks is its Ivy Style, read every frame. No collider: it's only seen.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Meridian Motion/Showcase/Ivy Field")]
    public sealed class IvyField : MonoBehaviour
    {
        /// <summary>
        /// A wall face, in the field's space: where it is, its mask's density (width by height texels: see IvyGrowth.Grown)
        /// and its stems, packed (IvyGrowth.Pack). The mask's stem channel is stamped again from the stems as the field's
        /// built (IvyGrowth.StemChannel), so it isn't saved.
        /// </summary>
        [Serializable]
        public sealed class Face
        {
            public Vector3 corner, along, up, outward;
            public Vector2 size;
            public int width, height;
            public byte[] density;
            public byte[] stems;
            /// <summary>Whether its growth hangs from its top (vines: IvyGrowth.Hang), rather than climbing from its foot.</summary>
            public bool hangs;

            /// <summary>A face from what IvyGrowth grew on it (hangs, if it was hung).</summary>
            public static Face From(IvyGrowth.Grown grown, Vector3 corner, Vector3 along, Vector3 up, Vector3 outward, Vector2 size, bool hangs = false) => new()
            {
                corner = corner, along = along, up = up, outward = outward, size = size, hangs = hangs,
                width = grown.width, height = grown.height, density = grown.density, stems = IvyGrowth.Pack(grown.stems),
            };
        }

        [Tooltip("How the ivy looks and how much there is (Create > Meridian Motion > Showcase > Ivy Style). Without one, the style's defaults.")]
        [SerializeField] private IvyStyle style;

        [Header("Drawn with")]
        [Tooltip("The compute shader that places the leaves (IvyLeaves.compute, beside this script).")]
        [SerializeField] private ComputeShader generator;
        [Tooltip("The leaves' shader (Ivy.shader, beside this script).")]
        [SerializeField] private Shader shader;
        [Tooltip("The far layer's shader (IvyFar.shader, beside this script).")]
        [SerializeField] private Shader farShader;
        [Tooltip("The material the stems are drawn with (the flora's stem bark).")]
        [SerializeField] private Material stemMaterial;

        [SerializeField, HideInInspector] private List<Face> faces = new();

        /// <summary>About how wide a chunk is (m), in its face's space: made a whole multiple of 8 cells (see Rebuild).</summary>
        private const float ChunkSize = 2f;

        /// <summary>How wide a block of stems is (m), in its face's space: each block is a mesh, drawn when it's near.</summary>
        private const float StemBlockSize = 8f;

        /// <summary>
        /// How wide a block of the far layer is (m), along its face: each block is a mesh, drawn only when some of it is
        /// as far off as the far layer fades in (nearer, every pixel of it would be clipped, but each still shaded:
        /// close up, a whole screen of them).
        /// </summary>
        private const float FarBlockSize = 16f;

        /// <summary>How far past its wall (m) a chunk's leaves can reach: its box is grown by this, for culling.</summary>
        private const float LeafReach = 0.8f;

        /// <summary>The coarsest grid step a far chunk is looked at with (every 2^n-th cell each way).</summary>
        private const int CoarsestStep = 3;

        /// <summary>A leaf's floats (IvyCommon.hlsl's Leaf), and a face's (IvyFace).</summary>
        private const int LeafStride = 14 * sizeof(float), FaceStride = 20 * sizeof(float);

        /// <summary>How many leaves a camera's near and far lists each hold to start with.</summary>
        public const int StartingLeaves = 1 << 15;

        /// <summary>The lists' sizes to start with, the most they grow to, and how long (s) they hold over four times what's asked before halving.</summary>
        private const int StartNear = StartingLeaves, StartFar = StartingLeaves, MostLeaves = 1 << 21;
        private const float ShrinkAfter = 5f;

        /// <summary>While set, no field places or draws anything (for a benchmark: turning them off would remake them).</summary>
        public static bool Suspended;

        /// <summary>The parts of the ivy a field draws (see Drawn).</summary>
        [Flags]
        public enum Parts { None = 0, Leaves = 1, Stems = 2, FarLayer = 4, All = Leaves | Stems | FarLayer }

        /// <summary>
        /// Which parts every field draws (all of them, but for a benchmark timing each part alone: Measure Ivy). Leaves
        /// left out aren't placed either.
        /// </summary>
        public static Parts Drawn = Parts.All;

        private static readonly List<IvyField> live = new();
        private static IvyStyle defaults;

        private struct Chunk { public int face; public Vector2Int cell; public Bounds bounds; public Vector3 outward; }

        /// <summary>A face's chunks (from first, count of them), with their world box and its plane (its corner and outward, in the world): culled whole before its chunks are.</summary>
        private struct FaceChunks { public int first, count; public Bounds bounds; public Vector3 corner, outward; }
        private struct StemBlock { public Mesh mesh; public Bounds bounds; public Vector3 outward; }
        private struct FarBlock { public Mesh mesh; public Bounds bounds; public Vector3 outward; }

        private readonly List<Chunk> chunks = new();
        private readonly List<FaceChunks> faceChunks = new();
        private readonly List<StemBlock> stemBlocks = new();
        private readonly List<FarBlock> farBlocks = new();
        private readonly Dictionary<Camera, Lists> lists = new();
        private readonly List<Vector4>[] byStep = { new(), new(), new(), new() };
        private readonly Plane[] frustum = new Plane[6];
        private readonly Vector4[] planes = new Vector4[6];
        private Vector4[] visible;
        private Texture2DArray maskTexture;
        private GraphicsBuffer faceBuffer, nearIndices, farIndices;
        private Material material, farMaterial, farLayerMaterial;
        private int kernelGenerate, kernelFinish, cellsPerSide;
        private float builtSpacing;
        private bool ready, stale;
        private Bounds allBounds;
        private static readonly uint[] ResetArgs = new uint[5];

        private sealed class Lists
        {
            public GraphicsBuffer chunks, near, far, nearArgs, farArgs, counts;
            public int nearCapacity = StartNear, farCapacity = StartFar;
            public bool readingBack, nothingInView;
            public int lastNear, lastFar;
            public float roomySince = -1f;
            public readonly MaterialPropertyBlock nearProperties = new(), farProperties = new();

            public Lists(int chunkCount)
            {
                chunks = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(chunkCount, 1), 4 * sizeof(float));
                nearArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw, 5, sizeof(uint));
                farArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw, 5, sizeof(uint));
                counts = new GraphicsBuffer(GraphicsBuffer.Target.Raw, 2, sizeof(uint));
                MakeLeaves();
            }

            public long Bytes => (long)(nearCapacity + farCapacity) * LeafStride + (chunks?.count ?? 0) * 16L;

            public void MakeLeaves()
            {
                near?.Release();
                far?.Release();
                near = new GraphicsBuffer(GraphicsBuffer.Target.Structured, nearCapacity, LeafStride);
                far = new GraphicsBuffer(GraphicsBuffer.Target.Structured, farCapacity, LeafStride);
            }

            public void Release()
            {
                foreach (GraphicsBuffer b in new[] { chunks, near, far, nearArgs, farArgs, counts }) { b?.Release(); }
                chunks = near = far = nearArgs = farArgs = counts = null;
            }

            /// <summary>
            /// Fits the leaf lists to what the frame read back asked for, as the grass's are (GrassField's Lists.Fit):
            /// grown to the next power of two if they were short, up to MostLeaves; halved once they've held over four
            /// times what was asked for ShrinkAfter seconds together, down to their starting size.
            /// </summary>
            public void Fit(AsyncGPUReadbackRequest request)
            {
                readingBack = false;
                // Asked for while something was in view, but nothing is now: what it says is stale (see BeforeCamera).
                if (request.hasError || near == null || nothingInView) { return; }
                Unity.Collections.NativeArray<uint> asked = request.GetData<uint>();
                lastNear = (int)asked[0];
                lastFar = (int)asked[1];
                bool remake = false;
                if (lastNear > nearCapacity && nearCapacity < MostLeaves) { nearCapacity = Mathf.Min(MostLeaves, Mathf.NextPowerOfTwo(lastNear + lastNear / 4)); remake = true; }
                if (lastFar > farCapacity && farCapacity < MostLeaves) { farCapacity = Mathf.Min(MostLeaves, Mathf.NextPowerOfTwo(lastFar + lastFar / 4)); remake = true; }
                bool roomy = (nearCapacity > StartNear && 4 * lastNear < nearCapacity) || (farCapacity > StartFar && 4 * lastFar < farCapacity);
                float now = Time.realtimeSinceStartup;
                if (!roomy || remake) { roomySince = -1f; }
                else if (roomySince < 0f) { roomySince = now; }
                else if (now - roomySince > ShrinkAfter)
                {
                    if (nearCapacity > StartNear && 4 * lastNear < nearCapacity) { nearCapacity /= 2; }
                    if (farCapacity > StartFar && 4 * lastFar < farCapacity) { farCapacity /= 2; }
                    roomySince = -1f;
                    remake = true;
                }
                if (remake) { MakeLeaves(); }
            }
        }

        private static class Ids
        {
            public static readonly int Chunks = Shader.PropertyToID("_Chunks"), Faces = Shader.PropertyToID("_Faces"), Mask = Shader.PropertyToID("_Mask");
            public static readonly int NearLeaves = Shader.PropertyToID("_NearLeaves"), FarLeaves = Shader.PropertyToID("_FarLeaves");
            public static readonly int NearArgs = Shader.PropertyToID("_NearArgs"), FarArgs = Shader.PropertyToID("_FarArgs"), Counts = Shader.PropertyToID("_Counts");
            public static readonly int NearCapacity = Shader.PropertyToID("_NearCapacity"), FarCapacity = Shader.PropertyToID("_FarCapacity");
            public static readonly int CameraPosition = Shader.PropertyToID("_CameraPosition"), FrustumPlanes = Shader.PropertyToID("_FrustumPlanes");
            public static readonly int Spacing = Shader.PropertyToID("_Spacing"), CellsPerSide = Shader.PropertyToID("_CellsPerSide");
            public static readonly int ChunkOffset = Shader.PropertyToID("_ChunkOffset"), Step = Shader.PropertyToID("_Step");
            public static readonly int Distances = Shader.PropertyToID("_Distances"), Leaf = Shader.PropertyToID("_Leaf"), Spread = Shader.PropertyToID("_Spread"), Vines = Shader.PropertyToID("_Vines");
            public static readonly int Leaves = Shader.PropertyToID("_Leaves"), Far = Shader.PropertyToID("_Far"), IvyMask = Shader.PropertyToID("_IvyMask");
            public static readonly int DarkColour = Shader.PropertyToID("_DarkColour"), LightColour = Shader.PropertyToID("_LightColour");
            public static readonly int ColourVariation = Shader.PropertyToID("_ColourVariation"), PaletteBlend = Shader.PropertyToID("_PaletteBlend");
            public static readonly int LeafLuma = Shader.PropertyToID("_LeafLuma"), Wrap = Shader.PropertyToID("_Wrap"), Translucency = Shader.PropertyToID("_Translucency");
            public static readonly int ShadowFloor = Shader.PropertyToID("_ShadowFloor"), Gloss = Shader.PropertyToID("_Gloss"), Fold = Shader.PropertyToID("_Fold");
            public static readonly int Flutter = Shader.PropertyToID("_Flutter"), DrawDistance = Shader.PropertyToID("_DrawDistance"), FarFade = Shader.PropertyToID("_FarFade");
        }

        /// <summary>Whether the platform can place the leaves (compute shaders, indirect draws). Without, only the far layer and the stems are drawn.</summary>
        public static bool Supported => SystemInfo.supportsComputeShaders && SystemInfo.supportsIndirectArgumentsBuffer;

        /// <summary>How the ivy looks (its style, or the style's defaults without one).</summary>
        public IvyStyle Style
        {
            get
            {
                if (style != null) { return style; }
                if (defaults == null) { defaults = ScriptableObject.CreateInstance<IvyStyle>(); defaults.hideFlags = HideFlags.HideAndDontSave; }
                return defaults;
            }
            set { style = value; stale = true; }
        }

        /// <summary>The faces the ivy grows on.</summary>
        public IReadOnlyList<Face> Faces => faces;

        /// <summary>The faces the ivy grows on (in the field's space): remade before the next camera renders.</summary>
        public void SetFaces(List<Face> given)
        {
            faces = given ?? new List<Face>();
            stale = true;
        }

        /// <summary>Adds a face (in the field's space): remade before the next camera renders.</summary>
        public void AddFace(Face face)
        {
            faces.Add(face);
            stale = true;
        }

        /// <summary>The shaders and the stems' material it's drawn with (the builders give them; one added by hand finds the shaders beside this script).</summary>
        public void SetLook(ComputeShader compute, Shader leaves, Shader far, Material stems)
        {
            generator = compute;
            shader = leaves;
            farShader = far;
            stemMaterial = stems;
            stale = true;
        }

        /// <summary>How many leaves a camera's last frame asked for, near and far (read back a frame or so late; none at once when nothing's in view).</summary>
        public (int near, int far) Counts(Camera camera) => lists.TryGetValue(camera, out Lists l) ? (l.lastNear, l.lastFar) : (0, 0);

        /// <summary>How many vertices its stems' meshes have.</summary>
        public int StemVertices
        {
            get
            {
                int n = 0;
                foreach (StemBlock b in stemBlocks) { n += b.mesh.vertexCount; }
                return n;
            }
        }

        /// <summary>How many leaves a camera's lists hold, near and far (none before it's been drawn for, or without compute shaders).</summary>
        public (int near, int far)? Capacities(Camera camera) => lists.TryGetValue(camera, out Lists l) ? (l.nearCapacity, l.farCapacity) : null;

        /// <summary>How many of its stems' vertices the last camera it drew for was given (those blocks near enough, in view and facing it).</summary>
        public int StemVerticesDrawn { get; private set; }

        /// <summary>The GPU memory every field's using (bytes): its leaf lists (for every camera) and its masks.</summary>
        public static long GpuMemory()
        {
            long bytes = 0;
            foreach (IvyField f in live)
            {
                foreach (Lists l in f.lists.Values) { bytes += l.Bytes; }
                if (f.maskTexture != null) { bytes += 2L * f.maskTexture.width * f.maskTexture.height * f.maskTexture.depth; }
            }
            return bytes;
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

        private void Rebuild()
        {
            Release();
            stale = false;
            if (!isActiveAndEnabled || faces.Count == 0) { return; }
            IvyStyle s = Style;
            builtSpacing = s.SpacingNow;

            int w = 1, h = 1;
            foreach (Face f in faces) { w = Mathf.Max(w, f.width); h = Mathf.Max(h, f.height); }
            maskTexture = new Texture2DArray(w, h, faces.Count, TextureFormat.RG16, false, true)
            {
                name = "Ivy Mask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave,
            };
            var texels = new byte[2 * w * h];
            var gpuFaces = new float[20 * faces.Count];
            Matrix4x4 toWorld = transform.localToWorldMatrix;
            cellsPerSide = 8 * Mathf.Max(1, Mathf.CeilToInt(ChunkSize / builtSpacing / 8f));
            float chunkMetres = cellsPerSide * builtSpacing;
            allBounds = default;
            for (int i = 0; i < faces.Count; i++)
            {
                Face f = faces[i];
                List<List<Vector3>> stems = IvyGrowth.Unpack(f.stems);
                byte[] stemChannel = IvyGrowth.StemChannel(stems, f.width, f.height, f.size.x / Mathf.Max(f.width, 1));
                Array.Clear(texels, 0, texels.Length);
                for (int y = 0; y < f.height; y++)
                {
                    for (int x = 0; x < f.width; x++)
                    {
                        int k = y * f.width + x, t = y * w + x;
                        texels[2 * t] = f.density != null && k < f.density.Length ? f.density[k] : (byte)0;
                        texels[2 * t + 1] = stemChannel[k];
                    }
                }
                maskTexture.SetPixelData(texels, 0, i);

                Vector3 corner = toWorld.MultiplyPoint3x4(f.corner), along = toWorld.MultiplyVector(f.along).normalized;
                Vector3 up = toWorld.MultiplyVector(f.up).normalized, outward = toWorld.MultiplyVector(f.outward).normalized;
                Vector2 share = new(f.width / (float)w, f.height / (float)h);
                Vector4[] data =
                {
                    new(corner.x, corner.y, corner.z, f.size.x), new(along.x, along.y, along.z, f.size.y), new(up.x, up.y, up.z, i),
                    new(outward.x, outward.y, outward.z, f.hangs ? 1f : 0f), new(share.x, share.y, 0f, 0f),
                };
                for (int d = 0; d < 5; d++) { for (int c = 0; c < 4; c++) { gpuFaces[20 * i + 4 * d + c] = data[d][c]; } }

                // Its chunks, those with any growth, each with its world box (out to as far as a leaf can reach). Each
                // a whole multiple of 8 cells, starting on one (see IvyLeaves.compute, Generate).
                var faceChunk = new FaceChunks { first = chunks.Count, corner = corner, outward = outward };
                for (int cu = 0; cu * builtSpacing < f.size.x; cu += cellsPerSide)
                {
                    for (int cv = 0; cv * builtSpacing < f.size.y; cv += cellsPerSide)
                    {
                        float u = cu * builtSpacing, v = cv * builtSpacing;
                        if (!AnyGrowth(f, u, v, chunkMetres)) { continue; }
                        Bounds b = FaceBounds(corner, along, up, u, v, Mathf.Min(chunkMetres, f.size.x - u), Mathf.Min(chunkMetres, f.size.y - v));
                        b.Expand(2f * LeafReach);
                        chunks.Add(new Chunk { face = i, cell = new Vector2Int(cu, cv), bounds = b, outward = outward });
                        if (allBounds.size == Vector3.zero) { allBounds = b; } else { allBounds.Encapsulate(b); }
                        if (faceChunk.bounds.size == Vector3.zero) { faceChunk.bounds = b; } else { faceChunk.bounds.Encapsulate(b); }
                    }
                }
                faceChunk.count = chunks.Count - faceChunk.first;
                if (faceChunk.count > 0) { faceChunks.Add(faceChunk); }

                AddStemBlocks(f, stems, toWorld, outward);
                AddFarBlocks(f, i, share, s, toWorld, outward);
            }
            maskTexture.Apply(false, true);

            if (farShader != null && farBlocks.Count > 0)
            {
                farLayerMaterial = new Material(farShader) { name = "Ivy Far Layer", hideFlags = HideFlags.HideAndDontSave };
                farLayerMaterial.SetTexture(Ids.IvyMask, maskTexture);
            }

            if (Supported && generator != null && shader != null && chunks.Count > 0)
            {
                faceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, faces.Count, FaceStride);
                faceBuffer.SetData(gpuFaces);
                visible = new Vector4[chunks.Count];
                // The near leaf: a fan of six triangles round its middle (vertex 0); the far: a diamond of two.
                nearIndices = new GraphicsBuffer(GraphicsBuffer.Target.Index, 18, sizeof(int));
                nearIndices.SetData(new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 6, 0, 6, 1 });
                farIndices = new GraphicsBuffer(GraphicsBuffer.Target.Index, 6, sizeof(int));
                farIndices.SetData(new[] { 0, 1, 2, 0, 2, 3 });
                material = new Material(shader) { name = "Ivy Leaves", hideFlags = HideFlags.HideAndDontSave };
                farMaterial = new Material(material) { name = "Ivy Leaves (far)", hideFlags = HideFlags.HideAndDontSave };
                // As the grass's far blades: a pixel or two each, not worth the depth prepass.
                farMaterial.SetShaderPassEnabled("DepthOnly", false);
                farMaterial.SetShaderPassEnabled("DepthNormals", false);
                kernelGenerate = generator.FindKernel("Generate");
                kernelFinish = generator.FindKernel("Finish");
            }
            ready = true;
        }

        /// <summary>A face's stems in blocks (StemBlockSize square, by where each stem starts), a mesh each, with its world box.</summary>
        private void AddStemBlocks(Face f, List<List<Vector3>> stems, Matrix4x4 toWorld, Vector3 outwardWorld)
        {
            var blocks = new Dictionary<Vector2Int, List<IReadOnlyList<Vector3>>>();
            foreach (List<Vector3> stem in stems)
            {
                if (stem.Count < 2) { continue; }
                var key = new Vector2Int(Mathf.FloorToInt(stem[0].x / StemBlockSize), Mathf.FloorToInt(stem[0].y / StemBlockSize));
                if (!blocks.TryGetValue(key, out List<IReadOnlyList<Vector3>> list)) { blocks[key] = list = new List<IReadOnlyList<Vector3>>(); }
                list.Add(stem);
            }
            foreach (List<IReadOnlyList<Vector3>> list in blocks.Values)
            {
                Mesh mesh = IvyGrowth.StemMesh(list, f.corner, f.along.normalized, f.up.normalized, f.outward.normalized);
                if (mesh == null) { continue; }
                Bounds local = mesh.bounds, world = new(toWorld.MultiplyPoint3x4(local.center), Vector3.zero);
                for (int c = 0; c < 8; c++)
                {
                    world.Encapsulate(toWorld.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3((c & 1) != 0 ? 1 : -1, (c & 2) != 0 ? 1 : -1, (c & 4) != 0 ? 1 : -1))));
                }
                stemBlocks.Add(new StemBlock { mesh = mesh, bounds = world, outward = outwardWorld });
            }
        }

        /// <summary>
        /// A face's far layer: quads a little out from the wall, FarBlockSize wide along it, a mesh each, with its world box.
        /// Each quad's uv runs over its share of the face, with the face's mask layer (z), and the share of the layer the
        /// mask fills (uv1).
        /// </summary>
        private void AddFarBlocks(Face f, int layer, Vector2 share, IvyStyle s, Matrix4x4 toWorld, Vector3 outwardWorld)
        {
            Vector3 along = f.along.normalized, up = f.up.normalized * f.size.y, outward = f.outward.normalized;
            Vector3 lift = outward * (s.standOff * 0.5f);
            // Wound to face outward (see IvyGrowth.StemMesh).
            bool flip = Vector3.Dot(Vector3.Cross(f.along, f.up), f.outward) < 0f;
            int[] triangles = flip ? new[] { 0, 2, 1, 0, 3, 2 } : new[] { 0, 1, 2, 0, 2, 3 };
            for (float u0 = 0f; u0 < f.size.x; u0 += FarBlockSize)
            {
                float u1 = Mathf.Min(f.size.x, u0 + FarBlockSize), a = u0 / f.size.x, b = u1 / f.size.x;
                Vector3 p0 = f.corner + along * u0 + lift, p1 = f.corner + along * u1 + lift;
                var mesh = new Mesh { name = "Ivy Far Layer", hideFlags = HideFlags.HideAndDontSave };
                mesh.SetVertices(new[] { p0, p1, p1 + up, p0 + up });
                mesh.SetNormals(new[] { outward, outward, outward, outward });
                mesh.SetUVs(0, new[] { new Vector3(a, 0f, layer), new Vector3(b, 0f, layer), new Vector3(b, 1f, layer), new Vector3(a, 1f, layer) });
                mesh.SetUVs(1, new[] { share, share, share, share });
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                Bounds world = new(toWorld.MultiplyPoint3x4(p0), Vector3.zero);
                world.Encapsulate(toWorld.MultiplyPoint3x4(p1));
                world.Encapsulate(toWorld.MultiplyPoint3x4(p1 + up));
                world.Encapsulate(toWorld.MultiplyPoint3x4(p0 + up));
                farBlocks.Add(new FarBlock { mesh = mesh, bounds = world, outward = outwardWorld });
            }
        }

        /// <summary>
        /// Whether a box is at least partly inside this camera's frustum (planes, set in BeforeCamera), as
        /// GeometryUtility.TestPlanesAABB says, but in C#: called for thousands of chunks a camera, the engine call's cost
        /// was most of what the ivy cost the CPU.
        /// </summary>
        private bool InView(Bounds b)
        {
            Vector3 c = b.center, e = b.extents;
            for (int i = 0; i < 6; i++)
            {
                Vector4 p = planes[i];
                float distance = p.x * c.x + p.y * c.y + p.z * c.z + p.w;
                float reach = Mathf.Abs(p.x) * e.x + Mathf.Abs(p.y) * e.y + Mathf.Abs(p.z) * e.z;
                if (distance + reach < 0f) { return false; }
            }
            return true;
        }

        /// <summary>How far a box's farthest corner is from a point.</summary>
        private static float FarthestDistance(Bounds b, Vector3 from)
        {
            Vector3 d = from - b.center;
            return new Vector3(Mathf.Abs(d.x) + b.extents.x, Mathf.Abs(d.y) + b.extents.y, Mathf.Abs(d.z) + b.extents.z).magnitude;
        }

        private static Bounds FaceBounds(Vector3 corner, Vector3 along, Vector3 up, float u, float v, float du, float dv)
        {
            var b = new Bounds(corner + along * u + up * v, Vector3.zero);
            b.Encapsulate(corner + along * (u + du) + up * v);
            b.Encapsulate(corner + along * u + up * (v + dv));
            b.Encapsulate(corner + along * (u + du) + up * (v + dv));
            return b;
        }

        /// <summary>Whether a face's mask has any growth in the chunk at u, v (m), side wide.</summary>
        private static bool AnyGrowth(Face f, float u, float v, float side)
        {
            if (f.density == null || f.width == 0) { return false; }
            float texel = f.size.x / f.width;
            int x0 = Mathf.FloorToInt(u / texel), x1 = Mathf.Min(f.width - 1, Mathf.CeilToInt((u + side) / texel));
            int y0 = Mathf.FloorToInt(v / texel), y1 = Mathf.Min(f.height - 1, Mathf.CeilToInt((v + side) / texel));
            for (int y = y0; y <= y1; y++) { for (int x = x0; x <= x1; x++) { if (f.density[y * f.width + x] > 0) { return true; } } }
            return false;
        }

        private void Release()
        {
            ready = false;
            foreach (Lists l in lists.Values) { l.Release(); }
            lists.Clear();
            faceBuffer?.Release();
            nearIndices?.Release();
            farIndices?.Release();
            faceBuffer = nearIndices = farIndices = null;
            foreach (StemBlock b in stemBlocks) { if (b.mesh != null) { DestroyImmediate(b.mesh); } }
            stemBlocks.Clear();
            foreach (FarBlock b in farBlocks) { if (b.mesh != null) { DestroyImmediate(b.mesh); } }
            farBlocks.Clear();
            foreach (UnityEngine.Object o in new UnityEngine.Object[] { maskTexture, material, farMaterial, farLayerMaterial })
            {
                if (o != null) { DestroyImmediate(o); }
            }
            maskTexture = null;
            material = farMaterial = farLayerMaterial = null;
            chunks.Clear();
            faceChunks.Clear();
        }

        private void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            IvyStyle s = Style;
            if (stale || (ready && !Mathf.Approximately(builtSpacing, s.SpacingNow))) { Rebuild(); }
            if (!ready || Suspended || camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection) { return; }

            Vector3 eye = camera.transform.position;
            GeometryUtility.CalculateFrustumPlanes(camera, frustum);
            for (int i = 0; i < 6; i++) { planes[i] = new Vector4(frustum[i].normal.x, frustum[i].normal.y, frustum[i].normal.z, frustum[i].distance); }
            float draw = s.drawDistance;
            StemVerticesDrawn = 0;
            if ((Drawn & Parts.FarLayer) != 0) { DrawFarLayer(camera, s, eye); }
            if ((Drawn & Parts.Stems) != 0) { DrawStems(camera, s, eye); }
            if (faceBuffer == null || (Drawn & Parts.Leaves) == 0) { return; }

            foreach (List<Vector4> step in byStep) { step.Clear(); }
            float drawSquared = draw * draw, from = Mathf.Clamp(s.thinFrom, 0.1f, draw * 0.99f);
            foreach (FaceChunks face in faceChunks)
            {
                // A face the camera's behind (its leaves all on the wall's far side), too far, or out of view: none of
                // its chunks need looking at. (A level has thousands of chunks, and a few dozen faces.)
                if (Vector3.Dot(face.outward, eye - face.corner) < -LeafReach) { continue; }
                if (face.bounds.SqrDistance(eye) > drawSquared || !InView(face.bounds)) { continue; }
                for (int k = face.first; k < face.first + face.count; k++)
                {
                    Chunk c = chunks[k];
                    // Turned away from the camera: its leaves are on the wall's far side, never seen.
                    if (Vector3.Dot(c.outward, eye - c.bounds.center) < -c.bounds.extents.magnitude * 0.25f) { continue; }
                    float nearest = c.bounds.SqrDistance(eye);
                    if (nearest > drawSquared || !InView(c.bounds)) { continue; }
                    // The most of its cells kept anywhere in it (at its nearest), and so the coarsest grid that has them all.
                    float near = from / Mathf.Max(Mathf.Sqrt(nearest), 0.01f);
                    float keep = Mathf.Clamp(near * near, s.thinTo, 1f);
                    int coarser = 0;
                    while (coarser < CoarsestStep && Mathf.Pow(0.25f, coarser + 1) >= keep) { coarser++; }
                    byStep[coarser].Add(new Vector4(c.face, c.cell.x, c.cell.y, 0f));
                }
            }
            int count = 0;
            foreach (List<Vector4> step in byStep) { foreach (Vector4 v in step) { visible[count++] = v; } }
            if (count == 0)
            {
                // Nothing placed, so nothing read back: its counts are none now, not what the last frame with leaves
                // asked for (which Counts went on reporting beyond the draw distance), and a read back still on its way
                // is ignored.
                if (lists.TryGetValue(camera, out Lists idle)) { idle.lastNear = idle.lastFar = 0; idle.nothingInView = true; }
                return;
            }

            Lists l = ListsFor(camera);
            l.nothingInView = false;
            ResetArgs[0] = 18;
            l.nearArgs.SetData(ResetArgs);
            ResetArgs[0] = 6;
            l.farArgs.SetData(ResetArgs);
            l.chunks.SetData(visible, 0, 0, count);

            ComputeShader g = generator;
            g.SetBuffer(kernelGenerate, Ids.Chunks, l.chunks);
            g.SetBuffer(kernelGenerate, Ids.Faces, faceBuffer);
            g.SetTexture(kernelGenerate, Ids.Mask, maskTexture);
            g.SetBuffer(kernelGenerate, Ids.NearLeaves, l.near);
            g.SetBuffer(kernelGenerate, Ids.FarLeaves, l.far);
            g.SetBuffer(kernelGenerate, Ids.NearArgs, l.nearArgs);
            g.SetBuffer(kernelGenerate, Ids.FarArgs, l.farArgs);
            g.SetInt(Ids.NearCapacity, l.nearCapacity);
            g.SetInt(Ids.FarCapacity, l.farCapacity);
            g.SetVector(Ids.CameraPosition, eye);
            g.SetVectorArray(Ids.FrustumPlanes, planes);
            g.SetFloat(Ids.Spacing, builtSpacing);
            g.SetInt(Ids.CellsPerSide, cellsPerSide);
            g.SetVector(Ids.Distances, new Vector4(draw, s.detailDistance, from, s.thinTo));
            g.SetVector(Ids.Leaf, new Vector4(s.leafSize.x, s.leafSize.y, s.standOff, s.tilt * Mathf.Deg2Rad));
            g.SetVector(Ids.Spread, new Vector4(s.spread * Mathf.Deg2Rad, 0f, 0f, 0f));
            g.SetVector(Ids.Vines, new Vector4(s.vineStandOut, s.vineFlutter, s.vineTipShrink, 0f));
            int offset = 0;
            for (int step = 0; step <= CoarsestStep; step++)
            {
                int stepCount = byStep[step].Count;
                if (stepCount == 0) { continue; }
                g.SetInt(Ids.ChunkOffset, offset);
                g.SetInt(Ids.Step, 1 << step);
                int groups = Mathf.Max(1, Mathf.CeilToInt((cellsPerSide >> step) / 8f));
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

            SetLook(material, s);
            SetLook(farMaterial, s);
            DrawLeaves(camera, l.near, l.nearArgs, nearIndices, 0, l.nearProperties, material, s);
            DrawLeaves(camera, l.far, l.farArgs, farIndices, 1, l.farProperties, farMaterial, s);
        }

        /// <summary>The stems' blocks that are near enough (the style's Stem Distance), in view and turned toward the camera.</summary>
        private void DrawStems(Camera camera, IvyStyle s, Vector3 eye)
        {
            if (stemMaterial == null || stemBlocks.Count == 0) { return; }
            float reach = s.stemDistance * s.stemDistance;
            var parameters = new RenderParams(stemMaterial)
            {
                camera = camera, layer = gameObject.layer, receiveShadows = true,
                shadowCastingMode = s.castsShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
            };
            Matrix4x4 toWorld = transform.localToWorldMatrix;
            foreach (StemBlock b in stemBlocks)
            {
                if (Vector3.Dot(b.outward, eye - b.bounds.center) < -b.bounds.extents.magnitude * 0.25f) { continue; }
                if (b.bounds.SqrDistance(eye) > reach || !InView(b.bounds)) { continue; }
                Graphics.RenderMesh(parameters, b.mesh, 0, toWorld);
                StemVerticesDrawn += b.mesh.vertexCount;
            }
        }

        /// <summary>
        /// The far layer (see the class), with the style's colours and distances: its blocks in view, turned toward the
        /// camera, and reaching as far off as it fades in.
        /// </summary>
        private void DrawFarLayer(Camera camera, IvyStyle s, Vector3 eye)
        {
            if (farBlocks.Count == 0 || farLayerMaterial == null) { return; }
            Material m = farLayerMaterial;
            m.SetColor(Ids.DarkColour, s.darkColour);
            m.SetColor(Ids.LightColour, s.lightColour);
            m.SetFloat(Ids.PaletteBlend, s.paletteBlend);
            m.SetFloat(Ids.LeafLuma, s.leafLuma);
            m.SetFloat(Ids.Wrap, s.wrap);
            m.SetFloat(Ids.ShadowFloor, s.shadowFloor);
            m.SetFloat(Ids.DrawDistance, s.drawDistance);
            // With no leaves at all (no compute shaders), the far layer is all there is, from the camera out.
            float fade = faceBuffer != null ? s.farFade : 1f;
            m.SetFloat(Ids.FarFade, fade);
            float fadesIn = s.drawDistance * (1f - fade);
            var parameters = new RenderParams(m) { camera = camera, layer = gameObject.layer, receiveShadows = true, shadowCastingMode = ShadowCastingMode.Off };
            Matrix4x4 toWorld = transform.localToWorldMatrix;
            foreach (FarBlock b in farBlocks)
            {
                if (Vector3.Dot(b.outward, eye - b.bounds.center) < -b.bounds.extents.magnitude * 0.25f) { continue; }
                if (FarthestDistance(b.bounds, eye) < fadesIn || !InView(b.bounds)) { continue; }
                Graphics.RenderMesh(parameters, b.mesh, 0, toWorld);
            }
        }

        /// <summary>Gives a leaf material the style's values.</summary>
        private static void SetLook(Material m, IvyStyle s)
        {
            m.SetColor(Ids.DarkColour, s.darkColour);
            m.SetColor(Ids.LightColour, s.lightColour);
            m.SetFloat(Ids.ColourVariation, s.colourVariation);
            m.SetFloat(Ids.PaletteBlend, s.paletteBlend);
            m.SetFloat(Ids.LeafLuma, s.leafLuma);
            m.SetFloat(Ids.Wrap, s.wrap);
            m.SetFloat(Ids.Translucency, s.translucency);
            m.SetFloat(Ids.ShadowFloor, s.shadowFloor);
            m.SetFloat(Ids.Gloss, s.gloss);
            m.SetFloat(Ids.Fold, s.fold);
            m.SetFloat(Ids.Flutter, s.flutter);
        }

        private void DrawLeaves(Camera camera, GraphicsBuffer leaves, GraphicsBuffer args, GraphicsBuffer indices, int far, MaterialPropertyBlock properties, Material drawWith, IvyStyle s)
        {
            properties.SetBuffer(Ids.Leaves, leaves);
            properties.SetInt(Ids.Far, far);
            var parameters = new RenderParams(drawWith)
            {
                camera = camera,
                worldBounds = allBounds,
                layer = gameObject.layer,
                shadowCastingMode = s.castsShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows = true,
                matProps = properties,
            };
            Graphics.RenderPrimitivesIndexedIndirect(parameters, MeshTopology.Triangles, indices, args);
        }

        /// <summary>A camera's lists: made the first time it renders, and freed once it's gone.</summary>
        private Lists ListsFor(Camera camera)
        {
            if (lists.TryGetValue(camera, out Lists l)) { return l; }
            var gone = new List<Camera>();
            foreach (Camera c in lists.Keys) { if (c == null) { gone.Add(c); } }
            foreach (Camera c in gone) { lists[c].Release(); lists.Remove(c); }
            l = new Lists(chunks.Count);
            lists.Add(camera, l);
            return l;
        }

#if UNITY_EDITOR
        /// <summary>The field's shaders, found beside this script if it hasn't been given them (one added by hand).</summary>
        private void FindShadersInEditor()
        {
            if (generator != null && shader != null && farShader != null) { return; }
            string folder = System.IO.Path.GetDirectoryName(UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.MonoScript.FromMonoBehaviour(this)));
            if (generator == null) { generator = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(folder + "/IvyLeaves.compute"); }
            if (shader == null) { shader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(folder + "/Ivy.shader"); }
            if (farShader == null) { farShader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(folder + "/IvyFar.shader"); }
        }
#endif
    }
}
