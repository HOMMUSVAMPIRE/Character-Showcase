using System.Collections.Generic;
using Level;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// The inspector for a PaintedClimbableSurface: its settings, the Paint button (which switches to the paint tool, see
    /// ClimbablePaintTool), its mask textures and the meshes that need a UV2 for them, and the paint shown as a green
    /// overlay on the surfaces whenever the object is selected.
    /// </summary>
    [CustomEditor(typeof(PaintedClimbableSurface))]
    public class PaintedClimbableSurfaceEditor : Editor
    {
        private static readonly Color PaintColor = new(0.2f, 0.9f, 0.35f, 0.35f);

        // The overlay's quads, rebuilt when the paint (or the object's pose) changes.
        private readonly List<(Vector3 center, Vector3 normal)> cells = new();
        private readonly List<Vector3[]> overlay = new();
        private int overlayVersion = -1;
        private Matrix4x4 overlayPose;
        private Collider[] colliders;

        private PaintedClimbableSurface Surface => (PaintedClimbableSurface)target;

        private void OnEnable() => Undo.undoRedoPerformed += OnUndoRedo;

        private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

        // An undone (or redone) stroke changes the paint, which the masks are painted from.
        private void OnUndoRedo()
        {
            overlayVersion = -1;
            if (Surface != null) { ClimbablePaintAssets.RebuildMasks(Surface); }
            SceneView.RepaintAll();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("speedMultiplier"));

            float cellSize = Surface.CellSize;
            float newCellSize = EditorGUILayout.DelayedFloatField(new GUIContent("Cell Size",
                "How fine the paint is: the size (m) of the cells it's made of. Changing it clears the paint."), cellSize);
            serializedObject.ApplyModifiedProperties();
            if (!Mathf.Approximately(newCellSize, cellSize)
                && (Surface.PaintedCellCount == 0 || EditorUtility.DisplayDialog("Change Cell Size",
                    "Changing the cell size clears the paint. Carry on?", "Clear and Change", "Cancel")))
            {
                Undo.RecordObject(Surface, "Change Climbable Paint Cell Size");
                Surface.CellSize = newCellSize;
                ClimbablePaintAssets.RebuildMasks(Surface);
                EditorUtility.SetDirty(Surface);
            }

            EditorGUILayout.Space();
            bool painting = ToolManager.activeToolType == typeof(ClimbablePaintTool);
            bool paint = GUILayout.Toggle(painting, painting ? "Painting (drag in the Scene view)" : "Paint", "Button", GUILayout.Height(26f));
            if (paint && !painting) { ToolManager.SetActiveTool<ClimbablePaintTool>(); }
            else if (!paint && painting) { ToolManager.RestorePreviousTool(); }
            ClimbablePaintTool.BrushRadius = EditorGUILayout.Slider(
                new GUIContent("Brush Radius", "The brush's radius (m). [ and ] size it in the Scene view."), ClimbablePaintTool.BrushRadius, 0.05f, 10f);
            EditorGUILayout.HelpBox("Drag over the object's colliders to paint them climbable; Shift-drag erases. Only the face " +
                                    "painted is painted (not the sides or back behind it). The paint tool is also in the Scene " +
                                    "view's Tools overlay while this object is selected.", MessageType.None);
            EditorGUILayout.LabelField("Painted cells", Surface.PaintedCellCount.ToString());
            using (new EditorGUI.DisabledScope(Surface.PaintedCellCount == 0))
            {
                if (GUILayout.Button("Clear Paint"))
                {
                    Undo.RecordObject(Surface, "Clear Climbable Paint");
                    Surface.Clear();
                    ClimbablePaintAssets.RebuildMasks(Surface);
                    EditorUtility.SetDirty(Surface);
                }
            }
            if (Surface.GetComponentInChildren<Collider>() == null)
            {
                EditorGUILayout.HelpBox("This object has no colliders to paint.", MessageType.Warning);
            }
            MasksGUI();
        }

        /// <summary>The settings for showing the paint, the masks painted, and which meshes need a UV2 for them.</summary>
        private void MasksGUI()
        {
            EditorGUILayout.Space();
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("paintMasks"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maskSize"));
            bool settingsChanged = EditorGUI.EndChangeCheck();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("masks"), includeChildren: true);
            }
            serializedObject.ApplyModifiedProperties();
            if (settingsChanged) { ClimbablePaintAssets.RebuildMasks(Surface); }
            if (!Surface.PaintsMasks) { return; }

            bool any = false;
            foreach (MeshRenderer renderer in Surface.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (PaintedClimbableSurface.HasMaskMaterial(renderer)) { any = true; break; }
            }
            if (!any)
            {
                EditorGUILayout.HelpBox($"None of this object's materials show climbable areas (a {PaintedClimbableSurface.MaskPropertyName} " +
                                        "texture property, as the levels' concrete has), so the paint isn't shown.", MessageType.Info);
                return;
            }
            List<MeshRenderer> needing = ClimbablePaintAssets.RenderersNeedingUV2(Surface);
            if (needing.Count == 0)
            {
                if (GUILayout.Button("Repaint Masks")) { ClimbablePaintAssets.RebuildMasks(Surface); }
                return;
            }
            EditorGUILayout.HelpBox($"To show the paint, these meshes need a UV2 (a UV set giving each face its own space): " +
                                    $"{string.Join(", ", needing.ConvertAll(r => r.name))}. Unity's cube and meshes in the scene are given " +
                                    "one by the first stroke; an imported model needs Generate Lightmap UVs on in its import settings.",
                                    MessageType.Warning);
            if (GUILayout.Button("Give Meshes a UV2 (changes imported models' settings)"))
            {
                foreach (MeshRenderer renderer in needing) { ClimbablePaintAssets.GiveUV2(renderer, changeImporters: true); }
                ClimbablePaintAssets.RebuildMasks(Surface);
            }
        }

        private void OnSceneGUI() => DrawOverlay();

        /// <summary>Shows the paint: a translucent square on the surface for each painted cell.</summary>
        private void DrawOverlay()
        {
            if (Event.current.type != EventType.Repaint) { return; }
            Matrix4x4 pose = Surface.transform.localToWorldMatrix;
            if (overlayVersion != Surface.Version || pose != overlayPose) { RebuildOverlay(); }
            Handles.color = PaintColor;
            foreach (Vector3[] quad in overlay) { Handles.DrawAAConvexPolygon(quad); }
        }

        private void RebuildOverlay()
        {
            overlayVersion = Surface.Version;
            overlayPose = Surface.transform.localToWorldMatrix;
            overlay.Clear();
            Surface.GetPaintedCells(cells, out Vector3 cellWorldSize);
            colliders = Surface.GetComponentsInChildren<Collider>();
            // Each cell shown on the surface it was painted on (its centre can be just off it), once: two layers of cells
            // either side of a face's plane show as one square.
            var shown = new HashSet<Vector3Int>();
            Quaternion rotation = Surface.transform.rotation;
            foreach (var (center, normal) in cells)
            {
                Vector3 onSurface = ProjectOntoSurface(center, normal);
                Vector3Int rounded = Vector3Int.RoundToInt(onSurface / Mathf.Max(0.01f, 0.25f * cellWorldSize.magnitude));
                if (!shown.Add(rounded)) { continue; }
                // The square's sides along the object's axes across the face, half a cell each way, just off the surface.
                Vector3 local = Quaternion.Inverse(rotation) * normal;
                int axis = Mathf.Abs(local.x) > 0.5f ? 0 : Mathf.Abs(local.y) > 0.5f ? 1 : 2;
                Vector3 u = Vector3.zero, v = Vector3.zero;
                int a = (axis + 1) % 3, b = (axis + 2) % 3;
                u[a] = cellWorldSize[a] * 0.48f;
                v[b] = cellWorldSize[b] * 0.48f;
                u = rotation * u;
                v = rotation * v;
                Vector3 c = onSurface + normal * 0.005f;
                overlay.Add(new[] { c - u - v, c + u - v, c + u + v, c - u + v });
            }
        }

        /// <summary>The point on the object's colliders nearest a cell's centre, coming from the side the cell faces.</summary>
        private Vector3 ProjectOntoSurface(Vector3 center, Vector3 normal)
        {
            Vector3 best = center;
            float bestDistance = float.PositiveInfinity;
            var ray = new Ray(center + normal * 2f, -normal);
            foreach (Collider collider in colliders)
            {
                if (collider == null || !collider.enabled) { continue; }
                if (collider.Raycast(ray, out RaycastHit hit, 4f))
                {
                    float distance = Vector3.Distance(hit.point, center);
                    if (distance < bestDistance) { bestDistance = distance; best = hit.point; }
                }
            }
            return best;
        }
    }

    /// <summary>
    /// The Scene view brush that paints a PaintedClimbableSurface: dragging over the selected object's colliders paints
    /// them climbable, Shift-dragging erases, and [ and ] size the brush. An Editor Tool (in the Scene view's Tools overlay
    /// while such an object is selected, or started from its inspector's Paint button), so while it's active it has the
    /// mouse to itself: clicking and dragging paints rather than selecting. Strokes can be undone.
    /// </summary>
    [EditorTool("Paint Climbable Surface", typeof(PaintedClimbableSurface))]
    public class ClimbablePaintTool : EditorTool
    {
        private const string BrushRadiusPreference = "CharacterShowcase.ClimbablePaint.BrushRadius";
        private static readonly Color BrushColor = new(0.2f, 0.9f, 0.35f, 1f);
        private static readonly Color EraseColor = new(1f, 0.35f, 0.25f, 1f);
        private static readonly int BrushHash = "ClimbablePaintBrush".GetHashCode();

        /// <summary>The brush's radius (m), remembered between sessions.</summary>
        public static float BrushRadius
        {
            get => EditorPrefs.GetFloat(BrushRadiusPreference, 0.5f);
            set => EditorPrefs.SetFloat(BrushRadiusPreference, Mathf.Clamp(value, 0.05f, 10f));
        }

        private GUIContent icon;
        public override GUIContent toolbarIcon => icon ??= MakeIcon();

        private static GUIContent MakeIcon()
        {
            const string tooltip = "Paint Climbable Surface: drag to paint, Shift-drag to erase, [ and ] to size the brush";
            Texture image = EditorGUIUtility.IconContent("Grid.PaintTool")?.image;
            return image != null ? new GUIContent(image, tooltip) : new GUIContent("Paint", tooltip);
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (window is not SceneView || target is not PaintedClimbableSurface surface) { return; }
            Event e = Event.current;
            int control = GUIUtility.GetControlID(BrushHash, FocusType.Passive);
            // Nearest of all at no distance: nothing else (rectangle selection, picking, other handles) gets the mouse.
            if (e.type == EventType.Layout) { HandleUtility.AddControl(control, 0f); }

            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.LeftBracket || e.keyCode == KeyCode.RightBracket))
            {
                BrushRadius *= e.keyCode == KeyCode.RightBracket ? 1.25f : 0.8f;
                e.Use();
                window.Repaint();
            }

            bool hasHit = RaycastSurface(surface, HandleUtility.GUIPointToWorldRay(e.mousePosition), out RaycastHit hit);
            bool erase = e.shift;
            if (hasHit && e.type == EventType.Repaint)
            {
                Handles.color = erase ? EraseColor : BrushColor;
                Handles.DrawWireDisc(hit.point, hit.normal, BrushRadius, 2f);
                Handles.DrawLine(hit.point, hit.point + hit.normal * Mathf.Min(0.5f, BrushRadius));
            }

            // Left button presses and drags are the brush's (Alt-drag still orbits the view), over the surface or not.
            switch (e.GetTypeForControl(control))
            {
                case EventType.MouseDown when e.button == 0 && !e.alt && HandleUtility.nearestControl == control:
                    GUIUtility.hotControl = control;
                    // Unity's cube and the scene's own meshes get a UV2 for the mask without asking (imported models don't:
                    // that changes the model's import settings, so it's the inspector's button).
                    if (surface.PaintsMasks)
                    {
                        foreach (MeshRenderer renderer in ClimbablePaintAssets.RenderersNeedingUV2(surface))
                        {
                            ClimbablePaintAssets.GiveUV2(renderer, changeImporters: false);
                        }
                    }
                    if (hasHit) { Stroke(surface, hit, erase, "Climbable Paint Stroke"); }
                    e.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == control:
                    if (hasHit) { Stroke(surface, hit, erase, "Climbable Paint Stroke"); }
                    e.Use();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == control:
                    GUIUtility.hotControl = 0;
                    // The masks are painted once the stroke's done: painting them is too slow to keep up with the brush.
                    Undo.RecordObject(surface, "Climbable Paint Stroke");
                    ClimbablePaintAssets.RebuildMasks(surface);
                    Undo.IncrementCurrentGroup();
                    e.Use();
                    break;
                case EventType.MouseMove:
                    window.Repaint();
                    break;
            }
        }

        private static void Stroke(PaintedClimbableSurface surface, RaycastHit hit, bool erase, string undoName)
        {
            Undo.RecordObject(surface, undoName);
            int changed = erase ? surface.Erase(hit.point, hit.normal, BrushRadius) : surface.Paint(hit.point, hit.normal, BrushRadius);
            if (changed > 0) { EditorUtility.SetDirty(surface); }
            SceneView.RepaintAll();
        }

        /// <summary>The nearest of the object's colliders (and its children's) under the mouse.</summary>
        private static bool RaycastSurface(PaintedClimbableSurface surface, Ray ray, out RaycastHit nearest)
        {
            nearest = default;
            Physics.SyncTransforms();
            float best = float.PositiveInfinity;
            foreach (Collider collider in surface.GetComponentsInChildren<Collider>())
            {
                if (collider == null || !collider.enabled || collider.isTrigger) { continue; }
                if (collider.Raycast(ray, out RaycastHit hit, 10000f) && hit.distance < best)
                {
                    best = hit.distance;
                    nearest = hit;
                }
            }
            return float.IsFinite(best);
        }
    }
}
