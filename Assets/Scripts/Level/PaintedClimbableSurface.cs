using System;
using System.Collections.Generic;
using UnityEngine;

namespace Level
{
    /// <summary>
    /// Makes parts of this object's colliders (and its children's) climbable: only the regions painted onto them, rather
    /// than the whole of them (for that, see ClimbableSurface). Paint is painted in the Scene view (select the object and
    /// press Paint in its inspector: drag to paint, Shift-drag to erase), or with Paint and Erase from code.
    ///
    /// Paint is kept as cells of a grid in the object's own space, each cell the cell size across (in metres, at the
    /// object's size when it was first painted: resizing the object afterwards stretches its paint with it), and each
    /// cell painted for one of the six directions the object's own axes point (the way the surface faced where it was
    /// painted). So painting the front of a wall doesn't paint its sides or its back, however close to them, and the
    /// edges of painted regions are as fine as the cells. A curved surface is painted for whichever axis it faces most.
    ///
    /// The paint can be shown too: for each of the object's mesh renderers with a material that has a climbable mask
    /// (a texture property named _ClimbableMask, as the levels' concrete shader has), a mask texture is painted in the
    /// mesh's UV2 (white where climbable), and given to those materials (as per-renderer data, so the material is still
    /// shared), which draw their climbable look there. The mesh needs a UV2 giving each face its own space (the levels'
    /// meshes have one). The masks are optional: without one, a material shows its own mask, by default the 1-pixel black
    /// texture that means nothing's painted.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class PaintedClimbableSurface : MonoBehaviour, ISerializationCallbackReceiver
    {
        /// <summary>The name of the texture property a material shows climbable areas with, white where they are.</summary>
        public const string MaskPropertyName = "_ClimbableMask";
        public static readonly int MaskProperty = Shader.PropertyToID(MaskPropertyName);

        /// <summary>A mesh renderer of the object's, and the mask texture painted for it.</summary>
        [Serializable]
        public struct RendererMask
        {
            [Tooltip("The renderer the mask is for")]
            public Renderer renderer;
            [Tooltip("The mask, in the renderer's mesh's UV2: white where climbable")]
            public Texture2D texture;
        }

        [Tooltip("How fast a character climbs on the painted regions, as a share of its climbing speed.")]
        [Range(0.1f, 2f)] [SerializeField] private float speedMultiplier = 1f;
        [Tooltip("How fine the paint is: the size (m) of the cells it's made of. Changing it clears the paint.")]
        [Min(0.02f)] [SerializeField] private float cellSize = 0.25f;
        [Header("Showing the Paint")]
        [Tooltip("Whether painting also paints mask textures, for materials that show climbable areas (see the class's " +
                 "summary). Off, the materials show no paint.")]
        [SerializeField] private bool paintMasks = true;
        [Tooltip("The size (pixels, each way) of the mask texture painted for each renderer")]
        [Range(16, 2048)] [SerializeField] private int maskSize = 512;
        [Tooltip("The mask textures painted, one per renderer. Made by painting; a mask can be left out (or cleared) to " +
                 "show none on that renderer.")]
        [SerializeField] private List<RendererMask> masks = new();

        // The object's scale when it was first painted, which the grid's cells are the cell size across at.
        [SerializeField] [HideInInspector] private Vector3 gridScale = Vector3.one;
        // The painted cells, packed (see Key), in no particular order.
        [SerializeField] [HideInInspector] private List<long> cells = new();

        // The cells as a set, for looking them up: built when first needed, and again after loading or an undo.
        private HashSet<long> lookup;

        // Each cell's index along each axis is packed into 20 bits, offset to be positive, with its face in the lowest 3.
        private const int IndexBits = 20;
        private const int IndexOffset = 1 << (IndexBits - 1);
        private const long IndexMask = (1L << IndexBits) - 1;
        // The most cells one Paint or Erase looks at, so a huge brush on a tiny grid can't hang the editor (a 10 m brush
        // on 0.25 m cells looks at about 20,000).
        private const int MaxCellsPerStroke = 3000000;

        /// <summary>How fast a character climbs on the painted regions, as a share of its climbing speed.</summary>
        public float SpeedMultiplier { get => speedMultiplier; set => speedMultiplier = Mathf.Clamp(value, 0.1f, 2f); }

        /// <summary>The size (m) of the cells paint is made of. Setting it clears the paint.</summary>
        public float CellSize
        {
            get => cellSize;
            set
            {
                value = Mathf.Max(0.02f, value);
                if (Mathf.Approximately(value, cellSize)) { return; }
                cellSize = value;
                Clear();
            }
        }

        /// <summary>How many cells are painted.</summary>
        public int PaintedCellCount => cells.Count;

        /// <summary>Goes up every time the paint changes (painting, erasing, clearing, undoing), for whatever shows it.</summary>
        public int Version { get; private set; }

        /// <summary>Whether painting paints mask textures for materials to show (see the class's summary).</summary>
        public bool PaintsMasks { get => paintMasks; set { paintMasks = value; ApplyMasks(); } }

        /// <summary>The size (pixels, each way) of each mask texture.</summary>
        public int MaskSize { get => maskSize; set => maskSize = Mathf.Clamp(value, 16, 2048); }

        /// <summary>The mask textures painted, one per renderer (see RebuildMasks).</summary>
        public IReadOnlyList<RendererMask> Masks => masks;

        /// <summary>Removes all the paint.</summary>
        public void Clear()
        {
            cells.Clear();
            lookup = null;
            Version++;
        }

        /// <summary>
        /// Paints the surface at a point (in world space) climbable: every cell within radius (m) of it on the surface's
        /// plane there (normal: the way the surface faces, in world space). Returns how many cells were newly painted.
        /// </summary>
        public int Paint(Vector3 point, Vector3 normal, float radius) => Stroke(point, normal, radius, erase: false);

        /// <summary>Erases paint from the surface at a point, as Paint paints it. Returns how many cells were erased.</summary>
        public int Erase(Vector3 point, Vector3 normal, float radius) => Stroke(point, normal, radius, erase: true);

        /// <summary>
        /// Whether a point on the surface (in world space), facing normal there, is painted climbable. At an edge or corner
        /// (atEdge: the normal points out from the edge, between the faces either side), the point has to be painted on
        /// every face it's between, so the edge is climbed round only where the face round it is painted too. Paint
        /// within tolerance (m) of the point counts too.
        /// </summary>
        public bool IsPaintedAt(Vector3 point, Vector3 normal, bool atEdge, float tolerance = 0f)
        {
            if (cells.Count == 0) { return false; }
            EnsureLookup();
            Vector3 grid = ToGrid(point);
            Vector3 local = Quaternion.Inverse(transform.rotation) * normal;
            int reach = tolerance > 0f ? Mathf.CeilToInt(tolerance / cellSize) : 0;
            if (!atEdge) { return IsPaintedNear(grid, DominantFace(local), reach); }
            bool any = false;
            for (int axis = 0; axis < 3; axis++)
            {
                // Faces the normal leans toward by more than a sliver: the faces either side of the edge.
                float along = local[axis] / Mathf.Max(local.magnitude, 1e-6f);
                if (Mathf.Abs(along) < 0.05f) { continue; }
                any = true;
                if (!IsPaintedNear(grid, axis * 2 + (along > 0f ? 0 : 1), reach)) { return false; }
            }
            return any;
        }

        /// <summary>
        /// Each painted cell's centre (in world space) and the way it faces (in world space, unit length), and the size
        /// of a cell (m, at the object's size now), for showing the paint.
        /// </summary>
        public void GetPaintedCells(List<(Vector3 center, Vector3 normal)> results, out Vector3 cellWorldSize)
        {
            results.Clear();
            Vector3 scale = AbsScale(transform.lossyScale);
            cellWorldSize = new Vector3(cellSize * scale.x / gridScale.x, cellSize * scale.y / gridScale.y, cellSize * scale.z / gridScale.z);
            foreach (long key in cells)
            {
                Unpack(key, out Vector3Int index, out int face);
                Vector3 center = FromGrid(new Vector3(index.x + 0.5f, index.y + 0.5f, index.z + 0.5f));
                results.Add((center, transform.rotation * FaceDirection(face)));
            }
        }

        private int Stroke(Vector3 point, Vector3 normal, float radius, bool erase)
        {
            if (normal.sqrMagnitude < 1e-8f || radius <= 0f) { return 0; }
            normal.Normalize();
            // The grid is set to the object's size when it's first painted, so its cells are the cell size across then.
            if (cells.Count == 0) { gridScale = AbsScale(transform.lossyScale); }
            EnsureLookup();

            int face = DominantFace(Quaternion.Inverse(transform.rotation) * normal);
            Vector3 center = ToGrid(point);
            // The brush's reach in grid cells along each axis (the object's scale now may differ from the grid's).
            Vector3 scale = AbsScale(transform.lossyScale);
            Vector3 cellWorld = new Vector3(cellSize * scale.x / gridScale.x, cellSize * scale.y / gridScale.y, cellSize * scale.z / gridScale.z);
            Vector3Int min = Vector3Int.FloorToInt(center - new Vector3(radius / cellWorld.x, radius / cellWorld.y, radius / cellWorld.z));
            Vector3Int max = Vector3Int.FloorToInt(center + new Vector3(radius / cellWorld.x, radius / cellWorld.y, radius / cellWorld.z));
            // Cells on the surface's plane: whose centres are within half a cell (corner to corner) of it. Only those near
            // it are looked at: across the face (the two grid axes other than the one it faces along), and for each, the
            // few cells along that axis either side of where the plane crosses it.
            int across = face / 2, a = (across + 1) % 3, b = (across + 2) % 3;
            long count = (long)(max[a] - min[a] + 1) * (max[b] - min[b] + 1) * 3;
            if (count > MaxCellsPerStroke) { return 0; }
            float halfCell = 0.5f * cellWorld.magnitude;
            Vector3 origin = FromGrid(Vector3.zero);
            Vector3 step = Vector3.zero;
            step[across] = 1f;
            float stepIntoPlane = Vector3.Dot(FromGrid(step) - origin, normal);
            if (Mathf.Abs(stepIntoPlane) < 1e-8f) { return 0; }

            int changed = 0;
            var cell = new Vector3Int();
            for (int i = min[a]; i <= max[a]; i++)
            for (int j = min[b]; j <= max[b]; j++)
            {
                var onAxes = new Vector3();
                onAxes[a] = i + 0.5f;
                onAxes[b] = j + 0.5f;
                // Where the plane crosses this column of cells (in cells along the axis the face faces along).
                float crossing = -Vector3.Dot(FromGrid(onAxes) - point, normal) / stepIntoPlane;
                int middle = Mathf.FloorToInt(crossing);
                for (int k = middle - 1; k <= middle + 1; k++)
                {
                    cell[a] = i;
                    cell[b] = j;
                    cell[across] = k;
                    Vector3 offset = FromGrid(new Vector3(cell.x + 0.5f, cell.y + 0.5f, cell.z + 0.5f)) - point;
                    if (Mathf.Abs(Vector3.Dot(offset, normal)) > halfCell) { continue; }
                    if (Vector3.ProjectOnPlane(offset, normal).magnitude > radius) { continue; }
                    long key = Key(cell, face);
                    if (erase ? lookup.Remove(key) : lookup.Add(key)) { changed++; }
                }
            }
            if (changed > 0)
            {
                cells.Clear();
                cells.AddRange(lookup);
                Version++;
            }
            return changed;
        }

        private bool IsPaintedNear(Vector3 grid, int face, int reach)
        {
            Vector3Int cell = Vector3Int.FloorToInt(grid);
            if (lookup.Contains(Key(cell, face))) { return true; }
            for (int x = -reach; x <= reach; x++)
            for (int y = -reach; y <= reach; y++)
            for (int z = -reach; z <= reach; z++)
            {
                if (lookup.Contains(Key(cell + new Vector3Int(x, y, z), face))) { return true; }
            }
            return false;
        }

        private void EnsureLookup() => lookup ??= new HashSet<long>(cells);

        /// <summary>A point in world space, in grid cells (from the object's origin, along its axes).</summary>
        private Vector3 ToGrid(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            return new Vector3(local.x * gridScale.x, local.y * gridScale.y, local.z * gridScale.z) / cellSize;
        }

        private Vector3 FromGrid(Vector3 grid)
        {
            Vector3 scaled = grid * cellSize;
            return transform.TransformPoint(new Vector3(scaled.x / gridScale.x, scaled.y / gridScale.y, scaled.z / gridScale.z));
        }

        private static Vector3 AbsScale(Vector3 scale) => new(
            Mathf.Max(Mathf.Abs(scale.x), 1e-6f), Mathf.Max(Mathf.Abs(scale.y), 1e-6f), Mathf.Max(Mathf.Abs(scale.z), 1e-6f));

        /// <summary>The face (0 to 5: +x, -x, +y, -y, +z, -z) a direction in the object's space points most along.</summary>
        private static int DominantFace(Vector3 local)
        {
            Vector3 a = new(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
            int axis = a.x >= a.y && a.x >= a.z ? 0 : a.y >= a.z ? 1 : 2;
            return axis * 2 + (local[axis] >= 0f ? 0 : 1);
        }

        private static Vector3 FaceDirection(int face)
        {
            Vector3 direction = Vector3.zero;
            direction[face / 2] = face % 2 == 0 ? 1f : -1f;
            return direction;
        }

        private static long Key(Vector3Int index, int face) =>
            face
            | ((index.x + IndexOffset) & IndexMask) << 3
            | ((index.y + IndexOffset) & IndexMask) << (3 + IndexBits)
            | ((index.z + IndexOffset) & IndexMask) << (3 + 2 * IndexBits);

        private static void Unpack(long key, out Vector3Int index, out int face)
        {
            face = (int)(key & 7);
            index = new Vector3Int(
                (int)((key >> 3) & IndexMask) - IndexOffset,
                (int)((key >> (3 + IndexBits)) & IndexMask) - IndexOffset,
                (int)((key >> (3 + 2 * IndexBits)) & IndexMask) - IndexOffset);
        }

        // Reused for each renderer's mesh and material data while painting masks.
        private static readonly List<Vector3> meshVertices = new();
        private static readonly List<Vector2> meshUV2 = new();
        private static readonly List<int> meshTriangles = new();
        private static readonly List<Material> rendererMaterials = new();
        private MaterialPropertyBlock block;

        /// <summary>
        /// Whether a renderer can show a mask: a mesh renderer whose mesh can be read and has a UV2, with a material that
        /// has a climbable mask.
        /// </summary>
        public static bool CanShowMask(Renderer renderer) => MaskMesh(renderer) != null && HasMaskMaterial(renderer);

        /// <summary>Whether any of a renderer's materials has a climbable mask property.</summary>
        public static bool HasMaskMaterial(Renderer renderer)
        {
            if (renderer == null) { return false; }
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material != null && material.HasProperty(MaskProperty)) { return true; }
            }
            return false;
        }

        /// <summary>A renderer's mesh, if it's a readable mesh with a UV2 (null if not).</summary>
        public static Mesh MaskMesh(Renderer renderer)
        {
            if (renderer is not MeshRenderer || !renderer.TryGetComponent(out MeshFilter filter)) { return null; }
            Mesh mesh = filter.sharedMesh;
            return mesh != null && mesh.isReadable && mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord1) ? mesh : null;
        }

        /// <summary>
        /// Paints the mask textures again from the paint: one for each of the object's renderers that can show one (see
        /// CanShowMask), made if it hasn't one yet (as a texture in memory: the editor saves it as an asset), and gives
        /// them to the materials. Each texel is white where the point of the mesh it covers is painted climbable. With
        /// PaintsMasks off, the masks are taken off the materials instead. Returns the renderers' masks that changed.
        /// </summary>
        public List<RendererMask> RebuildMasks()
        {
            var changed = new List<RendererMask>();
            if (!paintMasks) { ApplyMasks(); return changed; }
            masks.RemoveAll(m => m.renderer == null);
            foreach (Renderer renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!CanShowMask(renderer)) { continue; }
                int index = masks.FindIndex(m => m.renderer == renderer);
                RendererMask mask = index >= 0 ? masks[index] : new RendererMask { renderer = renderer };
                if (mask.texture == null)
                {
                    mask.texture = new Texture2D(maskSize, maskSize, TextureFormat.R8, false, true)
                    {
                        name = $"{renderer.name} Climbable Mask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                    };
                }
                else if (mask.texture.width != maskSize || mask.texture.height != maskSize || mask.texture.format != TextureFormat.R8)
                {
                    mask.texture.Reinitialize(maskSize, maskSize, TextureFormat.R8, false);
                }
                PaintMask(renderer, mask.texture);
                if (index >= 0) { masks[index] = mask; } else { masks.Add(mask); }
                changed.Add(mask);
            }
            ApplyMasks();
            return changed;
        }

        /// <summary>Paints a renderer's mask: its mesh's triangles, drawn in UV2, white where the point covered is painted.</summary>
        private void PaintMask(Renderer renderer, Texture2D texture)
        {
            Mesh mesh = MaskMesh(renderer);
            int size = texture.width;
            var values = new byte[size * size];
            var covered = new bool[size * size];
            mesh.GetVertices(meshVertices);
            mesh.GetUVs(1, meshUV2);
            renderer.GetSharedMaterials(rendererMaterials);
            Matrix4x4 toWorld = renderer.transform.localToWorldMatrix;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                // Only the parts of the mesh drawn with a material that shows the mask.
                Material material = sub < rendererMaterials.Count ? rendererMaterials[sub] : null;
                if (material == null || !material.HasProperty(MaskProperty)) { continue; }
                mesh.GetTriangles(meshTriangles, sub);
                for (int t = 0; t + 2 < meshTriangles.Count; t += 3)
                {
                    int i0 = meshTriangles[t], i1 = meshTriangles[t + 1], i2 = meshTriangles[t + 2];
                    Vector3 a = toWorld.MultiplyPoint3x4(meshVertices[i0]), b = toWorld.MultiplyPoint3x4(meshVertices[i1]), c = toWorld.MultiplyPoint3x4(meshVertices[i2]);
                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    if (normal.sqrMagnitude < 1e-14f) { continue; }
                    normal.Normalize();
                    RasterizeTriangle(meshUV2[i0] * size, meshUV2[i1] * size, meshUV2[i2] * size, size, (wa, wb, wc, texel) =>
                    {
                        covered[texel] = true;
                        if (values[texel] == 0 && IsPaintedAt(a * wa + b * wb + c * wc, normal, atEdge: false)) { values[texel] = 255; }
                    });
                }
            }
            // Spread each chart's edge texels a few texels out, so filtering at the chart's edges doesn't blend in the
            // black between charts.
            Dilate(values, covered, size, passes: 3);
            texture.SetPixelData(values, 0);
            texture.Apply(false);
        }

        private static void RasterizeTriangle(Vector2 a, Vector2 b, Vector2 c, int size, Action<float, float, float, int> texel)
        {
            Vector2 v0 = b - a, v1 = c - a;
            float d = v0.x * v1.y - v1.x * v0.y;
            if (Mathf.Abs(d) < 1e-12f) { return; }
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)))), x1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)))), y1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                Vector2 v2 = new Vector2(x + 0.5f, y + 0.5f) - a;
                float wb = (v2.x * v1.y - v1.x * v2.y) / d, wc = (v0.x * v2.y - v2.x * v0.y) / d, wa = 1f - wb - wc;
                if (wa < -1e-4f || wb < -1e-4f || wc < -1e-4f) { continue; }
                texel(wa, wb, wc, y * size + x);
            }
        }

        private static void Dilate(byte[] values, bool[] covered, int size, int passes)
        {
            var next = new List<(int, byte)>();
            for (int pass = 0; pass < passes; pass++)
            {
                next.Clear();
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    if (covered[i]) { continue; }
                    bool found = false;
                    byte value = 0;
                    if (x > 0 && covered[i - 1]) { found = true; value = Math.Max(value, values[i - 1]); }
                    if (x < size - 1 && covered[i + 1]) { found = true; value = Math.Max(value, values[i + 1]); }
                    if (y > 0 && covered[i - size]) { found = true; value = Math.Max(value, values[i - size]); }
                    if (y < size - 1 && covered[i + size]) { found = true; value = Math.Max(value, values[i + size]); }
                    if (found) { next.Add((i, value)); }
                }
                foreach ((int i, byte value) in next) { values[i] = value; covered[i] = true; }
            }
        }

        /// <summary>Gives each renderer's mask to its materials that show one (or, with PaintsMasks off, takes them off).</summary>
        public void ApplyMasks()
        {
            block ??= new MaterialPropertyBlock();
            foreach (RendererMask mask in masks)
            {
                if (mask.renderer == null) { continue; }
                SetMask(mask.renderer, paintMasks && isActiveAndEnabled ? mask.texture : null);
            }
        }

        private void SetMask(Renderer renderer, Texture texture)
        {
            renderer.GetSharedMaterials(rendererMaterials);
            for (int i = 0; i < rendererMaterials.Count; i++)
            {
                if (rendererMaterials[i] == null || !rendererMaterials[i].HasProperty(MaskProperty)) { continue; }
                if (texture == null)
                {
                    // No mask: the material's own (by default, the black texture that means nothing's painted).
                    renderer.SetPropertyBlock(null, i);
                    continue;
                }
                renderer.GetPropertyBlock(block, i);
                block.SetTexture(MaskProperty, texture);
                renderer.SetPropertyBlock(block, i);
            }
        }

        // Per-renderer data isn't saved: it's given again whenever the object is enabled, and taken off when it's not.
        private void OnEnable() => ApplyMasks();

        private void OnDisable()
        {
            block ??= new MaterialPropertyBlock();
            foreach (RendererMask mask in masks)
            {
                if (mask.renderer != null) { SetMask(mask.renderer, null); }
            }
        }

        public void OnBeforeSerialize() { }

        // Loading, and undoing a stroke, replaces the cells: the lookup is rebuilt from them when next needed.
        public void OnAfterDeserialize()
        {
            lookup = null;
            Version++;
        }
    }
}
