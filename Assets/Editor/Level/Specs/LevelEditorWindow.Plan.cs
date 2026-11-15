using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CharacterShowcase.EditorTools.Level
{
    // The plan of the level, picking and dragging pieces on it, and the Scene view's panel and handle.
    public partial class LevelEditorWindow
    {
        // ---- The plan ----

        private float Snap(float v) => snap ? Mathf.Round(v / snapStep) * snapStep : v;

        private void FitPlanTo(LevelSpecPreview built)
        {
            if (built == null) { return; }
            Bounds b = built.PlanBounds;
            planCentre = new Vector2(b.center.x, b.center.z);
            Rect r = position;
            float width = Mathf.Max(200f, r.width - ListWidth - InspectorWidth);
            planScale = Mathf.Clamp(Mathf.Min(width / Mathf.Max(1f, b.size.x), (r.height - 40f) / Mathf.Max(1f, b.size.z)) * 0.9f, 0.05f, 60f);
        }

        private Vector2 ToPlan(Rect r, Vector2 world) =>
            new(r.width / 2f + (world.x - planCentre.x) * planScale, r.height / 2f - (world.y - planCentre.y) * planScale);

        private Vector2 ToWorld(Rect r, Vector2 local) =>
            new(planCentre.x + (local.x - r.width / 2f) / planScale, planCentre.y - (local.y - r.height / 2f) / planScale);

        private JsonObject PickAt(Vector2 world)
        {
            if (preview == null) { return null; }
            for (int i = preview.Footprints.Count - 1; i >= 0; i--)
            {
                if (Inside(preview.Footprints[i].corners, world)) { return preview.Footprints[i].piece; }
            }
            return null;
        }

        /// <summary>How high the top of what's at a point on the plan is (0 if nothing's there): where a piece placed there stands.</summary>
        private float TopAt(Vector2 world)
        {
            if (preview == null) { return 0f; }
            for (int i = preview.Footprints.Count - 1; i >= 0; i--)
            {
                if (Inside(preview.Footprints[i].corners, world)) { return Mathf.Max(0f, preview.Footprints[i].top); }
            }
            return 0f;
        }

        private static bool Inside(Vector2[] quad, Vector2 p)
        {
            bool? sign = null;
            for (int i = 0; i < quad.Length; i++)
            {
                Vector2 a = quad[i], b = quad[(i + 1) % quad.Length];
                float cross = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
                if (Mathf.Abs(cross) < 1e-6f) { continue; }
                if (sign == null) { sign = cross > 0; }
                else if (sign != cross > 0) { return false; }
            }
            return true;
        }

        private void Plan(Rect rect)
        {
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.13f, 0.14f, 0.16f) : new Color(0.93f, 0.94f, 0.95f));
            if (preview == null || !preview.IsOpen)
            {
                var box = new Rect(rect.center.x - 170f, rect.center.y - 40f, 340f, 80f);
                GUI.Label(new Rect(box.x, box.y, box.width, 36f), "Build the preview to see the level and edit it on the plan.", new GUIStyle(EditorStyles.wordWrappedLabel) { alignment = TextAnchor.MiddleCenter });
                if (GUI.Button(new Rect(box.center.x - 60f, box.y + 40f, 120f, 24f), "Preview")) { BuildPreview(); }
                return;
            }
            Event e = Event.current;
            Vector2 local = e.mousePosition - rect.position;
            bool over = rect.Contains(e.mousePosition);
            switch (e.type)
            {
                case EventType.ScrollWheel when over:
                {
                    Vector2 before = ToWorld(rect, local);
                    planScale = Mathf.Clamp(planScale * Mathf.Exp(-e.delta.y * 0.08f), 0.05f, 80f);
                    planCentre += before - ToWorld(rect, local);
                    e.Use();
                    break;
                }
                case EventType.MouseDown when over && e.button == 1:
                    // Right: a drag pans; a click (no drag) opens the menu.
                    planPanning = true;
                    rightDown = e.mousePosition;
                    rightDragged = false;
                    e.Use();
                    break;
                case EventType.MouseDown when over && (e.button == 2 || (e.button == 0 && e.alt)):
                    planPanning = true;
                    rightDragged = true;
                    e.Use();
                    break;
                case EventType.MouseDown when over && e.button == 0 && placing != null:
                {
                    Vector2 w = ToWorld(rect, local);
                    PlacePiece(placing, new Vector3(w.x, TopAt(w), w.y));
                    e.Use();
                    break;
                }
                case EventType.MouseDown when over && e.button == 0:
                {
                    JsonObject picked = PickAt(ToWorld(rect, local));
                    if (picked != null && picked == selected)
                    {
                        planDragging = true;
                        dragStart = ToWorld(rect, local);
                        dragDelta = Vector2.zero;
                    }
                    else { Select(picked, true); }
                    e.Use();
                    break;
                }
                case EventType.MouseDrag when planPanning:
                    planCentre += new Vector2(-e.delta.x, e.delta.y) / planScale;
                    if ((e.mousePosition - rightDown).sqrMagnitude > 16f) { rightDragged = true; }
                    e.Use();
                    break;
                case EventType.MouseDrag when planDragging:
                {
                    Vector2 d = ToWorld(rect, local) - dragStart;
                    dragDelta = new Vector2(Snap(d.x), Snap(d.y));
                    e.Use();
                    break;
                }
                case EventType.MouseUp when planPanning:
                    planPanning = false;
                    if (!rightDragged && e.button == 1)
                    {
                        Vector2 w = ToWorld(rect, local);
                        JsonObject under = PickAt(w);
                        if (under != null && under != selected) { Select(under, true); }
                        PieceMenu(new Vector3(w.x, TopAt(w), w.y));
                    }
                    e.Use();
                    break;
                case EventType.MouseUp when planDragging:
                    planDragging = false;
                    if (dragDelta != Vector2.zero) { MoveSelectedBy(new Vector3(dragDelta.x, 0f, dragDelta.y)); }
                    dragDelta = Vector2.zero;
                    e.Use();
                    break;
            }
            if (e.type == EventType.Repaint) { DrawPlan(rect); }
            if (over) { EditorGUIUtility.AddCursorRect(rect, planPanning ? MouseCursor.Pan : planDragging ? MouseCursor.MoveArrow : placing != null ? MouseCursor.ArrowPlus : MouseCursor.Arrow); }

            // The scale, and how to use it.
            var hint = new Rect(rect.x + 8f, rect.yMax - 20f, rect.width - 16f, 18f);
            GUI.Label(hint, $"{Mathf.RoundToInt(rect.width / planScale)} m across  ·  click to pick, drag the picked piece to move it  ·  right-click to add, duplicate or delete  ·  right-drag to pan, wheel to zoom", EditorStyles.miniLabel);
            if (GUI.Button(new Rect(rect.xMax - 44f, rect.y + 6f, 38f, 18f), "Fit", EditorStyles.miniButton)) { FitPlanTo(preview); }
        }

        private void DrawPlan(Rect rect)
        {
            if (lineMaterial == null)
            {
                lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                lineMaterial.SetInt("_ZWrite", 0);
                lineMaterial.SetInt("_Cull", 0);
            }
            GUI.BeginClip(rect);
            lineMaterial.SetPass(0);
            GL.PushMatrix();
            bool dark = EditorGUIUtility.isProSkin;

            // The grid: 10 m, every fifth line stronger.
            float step = planScale < 0.6f ? 50f : 10f;
            Vector2 min = ToWorld(rect, new Vector2(0f, rect.height)), max = ToWorld(rect, new Vector2(rect.width, 0f));
            GL.Begin(GL.LINES);
            for (float x = Mathf.Floor(min.x / step) * step; x <= max.x; x += step)
            {
                GL.Color(Mathf.Abs(x % (step * 5f)) < 0.01f ? new Color(0.5f, 0.5f, 0.5f, 0.35f) : new Color(0.5f, 0.5f, 0.5f, 0.15f));
                Vector2 a = ToPlan(rect, new Vector2(x, min.y)), b = ToPlan(rect, new Vector2(x, max.y));
                GL.Vertex3(a.x, a.y, 0f); GL.Vertex3(b.x, b.y, 0f);
            }
            for (float z = Mathf.Floor(min.y / step) * step; z <= max.y; z += step)
            {
                GL.Color(Mathf.Abs(z % (step * 5f)) < 0.01f ? new Color(0.5f, 0.5f, 0.5f, 0.35f) : new Color(0.5f, 0.5f, 0.5f, 0.15f));
                Vector2 a = ToPlan(rect, new Vector2(min.x, z)), b = ToPlan(rect, new Vector2(max.x, z));
                GL.Vertex3(a.x, a.y, 0f); GL.Vertex3(b.x, b.y, 0f);
            }
            GL.End();

            // Every object, shaded by how high it is.
            Bounds all = preview.PlanBounds;
            float low = all.min.y, high = Mathf.Max(all.max.y, low + 1f);
            Color lowColour = dark ? new Color(0.22f, 0.24f, 0.27f) : new Color(0.86f, 0.87f, 0.89f);
            Color highColour = dark ? new Color(0.62f, 0.66f, 0.71f) : new Color(0.33f, 0.36f, 0.41f);
            var accent = new Color(0.88f, 0.48f, 0.18f);
            GL.Begin(GL.QUADS);
            foreach (LevelSpecPreview.Footprint f in preview.Footprints)
            {
                if (f.piece == selected) { continue; }
                GL.Color(Color.Lerp(lowColour, highColour, Mathf.InverseLerp(low, high, f.top)));
                foreach (Vector2 c in f.corners) { Vector2 p = ToPlan(rect, c); GL.Vertex3(p.x, p.y, 0f); }
            }
            Vector2 offset = new(dragDelta.x, dragDelta.y);
            GL.Color(new Color(accent.r, accent.g, accent.b, 0.55f));
            foreach (LevelSpecPreview.Footprint f in preview.Footprints.Where(f => f.piece == selected))
            {
                foreach (Vector2 c in f.corners) { Vector2 p = ToPlan(rect, c + offset); GL.Vertex3(p.x, p.y, 0f); }
            }
            GL.End();
            // Outlines: faint for everything, orange for the picked piece.
            GL.Begin(GL.LINES);
            foreach (LevelSpecPreview.Footprint f in preview.Footprints)
            {
                bool picked = f.piece == selected;
                GL.Color(picked ? accent : new Color(0f, 0f, 0f, dark ? 0.35f : 0.18f));
                for (int i = 0; i < 4; i++)
                {
                    Vector2 a = ToPlan(rect, f.corners[i] + (picked ? offset : Vector2.zero)), b = ToPlan(rect, f.corners[(i + 1) % 4] + (picked ? offset : Vector2.zero));
                    GL.Vertex3(a.x, a.y, 0f); GL.Vertex3(b.x, b.y, 0f);
                }
            }
            GL.End();
            GL.PopMatrix();
            if (selected != null && preview.BoundsOf(selected, out Bounds sb))
            {
                Vector2 p = ToPlan(rect, new Vector2(sb.center.x, sb.max.z) + offset);
                GUI.Label(new Rect(p.x - 150f, p.y - 20f, 300f, 18f), RowLabel(selected) + (dragDelta != Vector2.zero ? $"  (+{dragDelta.x:0.##}, +{dragDelta.y:0.##})" : ""),
                    new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.LowerCenter, normal = { textColor = accent } });
            }
            GUI.EndClip();
        }

        // ---- The Scene view's panel ----

        /// <summary>A small panel in the Scene view's corner: add a piece (click to place it), duplicate or delete the picked one.</summary>
        private void ScenePanel(SceneView view)
        {
            Handles.BeginGUI();
            var box = new Rect(10f, view.position.height - 78f, 360f, 50f);
            GUI.Box(box, GUIContent.none, EditorStyles.helpBox);
            GUILayout.BeginArea(new Rect(box.x + 6f, box.y + 4f, box.width - 12f, box.height - 8f));
            GUILayout.Label(placing != null ? $"Click to place the new {placing} (Esc cancels)"
                : selected != null ? $"Level Editor: {RowLabel(selected)}" : $"Level Editor: {spec.Name}", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (EditorGUILayout.DropdownButton(new GUIContent("Add"), FocusType.Passive, EditorStyles.miniPullDown, GUILayout.Width(60f)))
                {
                    var menu = new GenericMenu();
                    AddKinds(menu, "", StartPlacing);
                    menu.ShowAsContext();
                }
                using (new EditorGUI.DisabledScope(selected == null))
                {
                    if (GUILayout.Button("Duplicate", EditorStyles.miniButton, GUILayout.Width(70f))) { Duplicate(); }
                    if (GUILayout.Button("Delete", EditorStyles.miniButton, GUILayout.Width(60f))) { Delete(); }
                }
                if (placing != null && GUILayout.Button("Cancel", EditorStyles.miniButton, GUILayout.Width(60f))) { placing = null; Repaint(); }
            }
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        // ---- Moving ----

        /// <summary>Moves the picked piece by a world offset (in its parent's coordinates), as one edit.</summary>
        private void MoveSelectedBy(Vector3 world)
        {
            if (selected == null) { return; }
            Vector3 local = preview != null && preview.IsOpen ? preview.ParentOf(selected).InverseTransformVector(world) : world;
            if (!LevelSpecEdit.Translate(selected, local.x, local.y, local.z)) { return; }
            Edited("Move Piece", selected, all: selected["piece"] is "Group" or "Placed" or "Turned");
        }

        private void OnSceneGUI(SceneView view)
        {
            if (spec == null || preview == null || !preview.IsOpen) { return; }
            Event e = Event.current;

            // Delete and Duplicate (keys and the Edit menu) on the picked piece's objects edit the spec, not just the scene.
            if ((e.type == EventType.ValidateCommand || e.type == EventType.ExecuteCommand) && selected != null
                && Selection.activeGameObject != null && preview.PieceOf(Selection.activeGameObject) == selected
                && Command(e.commandName, e.type == EventType.ExecuteCommand))
            {
                e.Use();
                return;
            }

            // Placing: a click puts the new piece where the click lands on the level (or the ground plane).
            if (placing != null)
            {
                int id = GUIUtility.GetControlID(FocusType.Passive);
                if (e.type == EventType.Layout) { HandleUtility.AddDefaultControl(id); }
                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { placing = null; e.Use(); Repaint(); }
                else if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
                {
                    Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                    Physics.SyncTransforms();
                    Vector3 at = Physics.Raycast(ray, out RaycastHit hit, 5000f, ~0, QueryTriggerInteraction.Ignore) ? hit.point
                        : new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float d) ? ray.GetPoint(d) : view.pivot;
                    PlacePiece(placing, at);
                    e.Use();
                }
            }
            ScenePanel(view);
            if (selected == null) { return; }
            if (e.rawType == EventType.MouseUp && handleOffset != Vector3.zero) { handleCommitPending = true; }
            if (!preview.BoundsOf(selected, out Bounds b)) { return; }
            if (handleOffset == Vector3.zero) { handleBase = b.center; }
            Handles.color = new Color(0.88f, 0.48f, 0.18f);
            Handles.DrawWireCube(handleBase + handleOffset, b.size);
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(handleBase + handleOffset, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Vector3 offset = moved - handleBase;
                offset = new Vector3(Snap(offset.x), Snap(offset.y), Snap(offset.z));
                Vector3 step = offset - handleOffset;
                // Shown moving now; the piece is rebuilt where it ends up when the handle's let go.
                foreach (GameObject o in preview.ObjectsOf(selected)) { o.transform.position += step; }
                handleOffset = offset;
            }
            if (handleCommitPending && GUIUtility.hotControl == 0)
            {
                handleCommitPending = false;
                Vector3 offset = handleOffset;
                handleOffset = Vector3.zero;
                if (offset != Vector3.zero) { MoveSelectedBy(offset); }
            }
        }
    }
}
