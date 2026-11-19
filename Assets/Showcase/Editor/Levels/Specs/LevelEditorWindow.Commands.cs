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

namespace MeridianMotion.Showcase.Levels
{
    // Keyboard shortcuts and commands (duplicate, delete, reorder, add a part), and following edits made in the hierarchy.
    public partial class LevelEditorWindow
    {
        // ---- Keys and commands ----

        /// <summary>The window's keys: Delete deletes the picked piece, Ctrl/Cmd+D duplicates it, Esc stops placing, Enter places at the plan's middle.</summary>
        private void Keys()
        {
            Event e = Event.current;
            if (spec == null) { return; }
            if ((e.type == EventType.ValidateCommand || e.type == EventType.ExecuteCommand) && !EditorGUIUtility.editingTextField
                && Command(e.commandName, e.type == EventType.ExecuteCommand))
            {
                e.Use();
                return;
            }
            if (e.type != EventType.KeyDown || EditorGUIUtility.editingTextField) { return; }
            if (e.keyCode == KeyCode.Escape && placing != null) { placing = null; e.Use(); }
            else if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && placing != null)
            {
                PlacePiece(placing, new Vector3(planCentre.x, 0f, planCentre.y));
                e.Use();
            }
            else if ((e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && selected != null) { Delete(); e.Use(); }
        }

        /// <summary>
        /// An edit command for the picked piece ("Delete", "SoftDelete" or "Duplicate", as Unity sends for its keys and
        /// menu items): whether it's one the editor takes (when only checking), or runs it.
        /// </summary>
        private bool Command(string name, bool run)
        {
            if (selected == null || spec == null) { return false; }
            switch (name)
            {
                case "Delete":
                case "SoftDelete":
                    if (run) { Delete(); }
                    return true;
                case "Duplicate":
                    if (run) { Duplicate(); }
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Pieces whose objects were all deleted from the scene some other way (the Hierarchy's Delete) are taken out of
        /// the spec too, so the spec and the scene agree.
        /// </summary>
        private void OnHierarchyChanged()
        {
            if (spec == null || preview == null || !preview.IsOpen) { return; }
            List<JsonObject> gone = preview.DeletedPieces().Where(p => spec.Parts.Any(part => part.Pieces.Contains(p))).ToList();
            if (gone.Count == 0) { return; }
            foreach (JsonObject piece in gone)
            {
                spec.Parts.First(part => part.Pieces.Contains(piece)).Pieces.Remove(piece);
                if (selected == piece) { selected = null; }
            }
            bool all = gone.Any(p => p["piece"] is "Group" or "Placed" or "Turned");
            foreach (JsonObject piece in gone.Where(_ => !all)) { preview.Remove(piece); }
            Record(gone.Count == 1 ? "Delete Piece" : "Delete Pieces");
            doc.json = spec.ToJson();
            loadedJson = doc.json;
            if (all) { preview.RebuildAll(spec); }
            notice = $"Took {string.Join(", ", gone.Select(RowLabel))} out of the spec: its objects were deleted from the scene. Undo it with the Level Editor's undo (Edit > Undo).";
            Repaint();
        }

        private void Duplicate()
        {
            if (selected == null) { return; }
            JsonObject copy = selected.Clone();
            foreach (string key in new[] { "name", "label" })
            {
                if (copy.TryGetValue(key, out object v) && v is string s) { copy[key] = s + " (Copy)"; }
            }
            LevelSpecEdit.Translate(copy, DuplicateOffset, 0f, DuplicateOffset);
            (LevelSpec.Part part, int index) = Insertion();
            part.Pieces.Insert(index, copy);
            Select(copy, false);
            Edited("Duplicate Piece", copy, all: copy["piece"] is "Group" or "Placed" or "Turned");
            Select(copy, true);
        }

        private void Delete()
        {
            if (selected == null) { return; }
            notice = null;
            LevelSpec.Part part = spec.Parts.First(p => p.Pieces.Contains(selected));
            JsonObject piece = selected;
            int index = part.Pieces.IndexOf(piece);
            part.Pieces.Remove(piece);
            bool all = piece["piece"] is "Group" or "Placed" or "Turned" or LevelSpec.StatePiece;
            if (!all && preview != null && preview.IsOpen) { preview.Remove(piece); }
            Select(part.Pieces.Count > 0 ? part.Pieces[Mathf.Min(index, part.Pieces.Count - 1)] : null, false);
            Record("Delete Piece");
            doc.json = spec.ToJson();
            loadedJson = doc.json;
            if (all && preview != null && preview.IsOpen) { preview.RebuildAll(spec); }
            Repaint();
        }

        private void MoveSelected(int by)
        {
            LevelSpec.Part part = spec.Parts.First(p => p.Pieces.Contains(selected));
            int i = part.Pieces.IndexOf(selected), j = i + by;
            if (j < 0 || j >= part.Pieces.Count) { return; }
            (part.Pieces[i], part.Pieces[j]) = (part.Pieces[j], part.Pieces[i]);
            selectedPiece = j;
            // Order matters to groups, State entries and names (paths); otherwise the level looks the same.
            bool all = new[] { part.Pieces[i], part.Pieces[j] }.Any(p => p["piece"] is "Group" or "Placed" or "Turned" or LevelSpec.StatePiece);
            Record("Move Piece");
            doc.json = spec.ToJson();
            loadedJson = doc.json;
            if (all && preview != null && preview.IsOpen) { preview.RebuildAll(spec); }
            Repaint();
        }

        private void AddPart()
        {
            int n = spec.Parts.Count + 1;
            string name = $"Part {n}";
            while (spec.Parts.Any(p => p.Name == name)) { name = $"Part {++n}"; }
            spec.Parts.Add(new LevelSpec.Part { Name = name });
            selected = null;
            selectedPart = spec.Parts.Count - 1;
            Edited("Add Part", all: false);
        }
    }
}
