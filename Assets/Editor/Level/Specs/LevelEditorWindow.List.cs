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
    // The window's list of parts and pieces, the add-piece menu, and placing new pieces.
    public partial class LevelEditorWindow
    {
        // ---- The list ----

        private static string RowLabel(JsonObject piece)
        {
            string kind = piece["piece"] as string;
            string name = new[] { "name", "label", "text", "course" }.Select(k => piece.TryGetValue(k, out object v) ? v as string : null).FirstOrDefault(v => !string.IsNullOrEmpty(v));
            return name == null ? kind : $"{kind}  {name}";
        }

        private void PieceList()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (EditorGUILayout.DropdownButton(new GUIContent("Add", "Add a piece: pick its kind, then click where it goes on the plan or in the Scene view."),
                        FocusType.Passive, EditorStyles.toolbarDropDown, GUILayout.Width(48f)))
                {
                    var menu = new GenericMenu();
                    AddKinds(menu, "", StartPlacing);
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Part"), false, AddPart);
                    menu.ShowAsContext();
                }
                using (new EditorGUI.DisabledScope(selected == null))
                {
                    if (GUILayout.Button(new GUIContent("Duplicate", "Duplicate the picked piece (Ctrl/Cmd+D)"), EditorStyles.toolbarButton)) { Duplicate(); }
                    if (GUILayout.Button(new GUIContent("Delete", "Delete the picked piece (Delete)"), EditorStyles.toolbarButton)) { Delete(); }
                    if (GUILayout.Button(new GUIContent("▲", "Move it up its part"), EditorStyles.toolbarButton, GUILayout.Width(22f))) { MoveSelected(-1); }
                    if (GUILayout.Button(new GUIContent("▼", "Move it down its part"), EditorStyles.toolbarButton, GUILayout.Width(22f))) { MoveSelected(1); }
                }
                GUILayout.FlexibleSpace();
            }
            filter = EditorGUILayout.TextField(filter, EditorStyles.toolbarSearchField);
            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            var row = new GUIStyle(EditorStyles.label) { richText = false, clipping = TextClipping.Clip };
            for (int p = 0; p < spec.Parts.Count; p++)
            {
                LevelSpec.Part part = spec.Parts[p];
                Rect header = GUILayoutUtility.GetRect(new GUIContent(part.Name), EditorStyles.boldLabel);
                if (selected == null && selectedPart == p) { EditorGUI.DrawRect(header, new Color(0.24f, 0.48f, 0.9f, 0.35f)); }
                GUI.Label(header, $"{part.Name}   ({part.Pieces.Count})", EditorStyles.boldLabel);
                if (Event.current.type == EventType.MouseDown && header.Contains(Event.current.mousePosition))
                {
                    selected = null;
                    selectedPart = p;
                    selectedPiece = -1;
                    if (Event.current.button == 1)
                    {
                        var menu = new GenericMenu();
                        AddKinds(menu, "Add Piece/", StartPlacing);
                        menu.AddItem(new GUIContent("Add Part"), false, AddPart);
                        int index = p;
                        menu.AddItem(new GUIContent("Delete Part"), false, () => DeletePart(spec.Parts[index]));
                        menu.ShowAsContext();
                    }
                    Event.current.Use();
                }
                foreach (JsonObject piece in part.Pieces)
                {
                    string label = RowLabel(piece);
                    if (!string.IsNullOrEmpty(filter) && label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) { continue; }
                    Rect r = GUILayoutUtility.GetRect(new GUIContent(label), row);
                    if (piece == selected) { EditorGUI.DrawRect(r, new Color(0.24f, 0.48f, 0.9f, 0.45f)); }
                    bool bad = preview != null && preview.Problems.ContainsKey(piece);
                    row.normal.textColor = bad ? new Color(0.95f, 0.35f, 0.3f) : EditorStyles.label.normal.textColor;
                    GUI.Label(new Rect(r.x + 12f, r.y, r.width - 12f, r.height), label, row);
                    if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                    {
                        Select(piece, true);
                        if (Event.current.button == 1) { PieceMenu(null); }
                        else if (Event.current.clickCount == 2) { Frame(); }
                        Event.current.Use();
                    }
                }
                GUILayout.Space(4f);
            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>The picked piece's menu (right-click): add a piece after it (at world, if given), duplicate it, delete it, move it.</summary>
        private void PieceMenu(Vector3? world)
        {
            var menu = new GenericMenu();
            if (world.HasValue)
            {
                Vector3 at = world.Value;
                AddKinds(menu, "Add Here/", kind => PlacePiece(kind, at));
            }
            else { AddKinds(menu, "Add Piece/", StartPlacing); }
            if (selected != null)
            {
                string label = RowLabel(selected);
                menu.AddSeparator("");
                menu.AddItem(new GUIContent($"Duplicate {label}"), false, Duplicate);
                menu.AddItem(new GUIContent($"Delete {label}"), false, Delete);
                menu.AddItem(new GUIContent("Move Up"), false, () => MoveSelected(-1));
                menu.AddItem(new GUIContent("Move Down"), false, () => MoveSelected(1));
                menu.AddItem(new GUIContent("Frame"), false, Frame);
            }
            menu.ShowAsContext();
        }

        /// <summary>Every kind of piece, in groups, under prefix, each calling pick with its name.</summary>
        private static void AddKinds(GenericMenu menu, string prefix, Action<string> pick)
        {
            var listed = new HashSet<string>();
            foreach ((string group, string[] pieces) in Kinds)
            {
                foreach (string piece in pieces.Where(LevelSpec.Pieces.ContainsKey))
                {
                    listed.Add(piece);
                    menu.AddItem(new GUIContent($"{prefix}{group}/{ObjectNames.NicifyVariableName(piece)}"), false, () => pick(piece));
                }
            }
            foreach (string piece in LevelSpec.Pieces.Keys.Where(k => !listed.Contains(k)).OrderBy(k => k))
            {
                menu.AddItem(new GUIContent($"{prefix}Other/{ObjectNames.NicifyVariableName(piece)}"), false, () => pick(piece));
            }
            menu.AddItem(new GUIContent($"{prefix}State (step size, decks, rails)"), false, () => pick(LevelSpec.StatePiece));
        }

        /// <summary>
        /// Starts adding a piece of a kind: the next click on the plan or in the Scene view places it. Without a preview
        /// (nowhere to click), or for a State entry (which has no place), it's added at once, at the middle of the plan.
        /// </summary>
        private void StartPlacing(string kind)
        {
            if (kind == LevelSpec.StatePiece || preview == null || !preview.IsOpen)
            {
                PlacePiece(kind, new Vector3(planCentre.x, 0f, planCentre.y));
                return;
            }
            placing = kind;
            notice = null;
            Repaint();
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Adds a piece of a kind with its origin at a world point (snapped): in the picked group (or the picked
        /// piece's group), after the picked piece in its part. Picks it, and returns it.
        /// </summary>
        private JsonObject PlacePiece(string kind, Vector3 world)
        {
            placing = null;
            string parent = "";
            Transform frame = preview != null && preview.IsOpen ? preview.Root : null;
            if (selected != null)
            {
                if (selected["piece"] is "Group" or "Placed" or "Turned" && preview != null && preview.IsOpen && preview.ObjectsOf(selected).Count > 0)
                {
                    frame = preview.ObjectsOf(selected)[0].transform;
                    parent = LevelSpec.PathOf(frame, preview.Root);
                }
                else
                {
                    parent = selected.TryGetValue("parent", out object p) ? p as string ?? "" : "";
                    if (preview != null && preview.IsOpen) { frame = preview.ParentOf(selected); }
                }
            }
            Vector3 at = new Vector3(Snap(world.x), Snap(world.y), Snap(world.z));
            if (frame != null) { at = frame.InverseTransformPoint(at); }
            JsonObject piece = LevelSpecEdit.NewPiece(kind, parent, new Vector3(Snap(at.x), Snap(at.y), Snap(at.z)));
            (LevelSpec.Part part, int index) = Insertion();
            part.Pieces.Insert(index, piece);
            Select(piece, false);
            Edited($"Add {ObjectNames.NicifyVariableName(kind)}", piece, all: kind is "Group" or "Placed" or "Turned" or LevelSpec.StatePiece);
            Select(piece, true);
            return piece;
        }

        private void DeletePart(LevelSpec.Part part)
        {
            if (part.Pieces.Count > 0 && !EditorUtility.DisplayDialog("Level Editor", $"Delete the part '{part.Name}' and its {part.Pieces.Count} pieces?", "Delete", "Cancel")) { return; }
            int i = spec.Parts.IndexOf(part);
            spec.Parts.Remove(part);
            selected = null;
            selectedPart = Mathf.Min(i, spec.Parts.Count - 1);
            Edited("Delete Part", all: true);
        }
    }
}
