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
    /// <summary>
    /// Edits a level spec (Assets/Levels/….level.json) and shows it as it changes: its parts and pieces in a list,
    /// the selected piece's settings, and a plan of the level, over a preview built in an unsaved scene (see
    /// LevelSpecPreview), where each edit rebuilds just the piece it changed.
    ///
    /// Pieces are picked in the list, on the plan or in the Scene view (each selects the other), moved by dragging on
    /// the plan or with the Scene view's move handle (snapping to the grid), and added, duplicated, deleted and
    /// reordered with the list's buttons. Edit > Undo and Redo step through the edits. Save writes the spec; Build
    /// Level builds it as its builder would (scene, environment, bakes), then Set Up Scene makes it playable; Schematic
    /// opens its schematic with the movement checks (python3 Tools/LevelSpec/levelspec).
    /// </summary>
    public sealed partial class LevelEditorWindow : EditorWindow
    {
        private const float ListWidth = 290f, InspectorWidth = 340f;
        // How far a duplicate is put from the piece it copies (m, along both x and z), so it's seen beside it, not on it.
        private const float DuplicateOffset = 2f;

        [SerializeField] private LevelSpecDocument doc;
        [SerializeField] private string savedJson;
        [SerializeField] private int selectedPart = -1, selectedPiece = -1;
        [SerializeField] private Vector2 planCentre;
        [SerializeField] private float planScale = 3f;
        [SerializeField] private bool snap = true;
        [SerializeField] private float snapStep = 0.5f;
        [SerializeField] private string filter = "";
        [SerializeField] private Vector2 listScroll, inspectorScroll;

        [NonSerialized] private LevelSpec spec;
        [NonSerialized] private LevelSpecPreview preview;
        [NonSerialized] private JsonObject selected;
        [NonSerialized] private string loadedJson;
        [NonSerialized] private readonly Dictionary<string, string> textEdits = new();
        [NonSerialized] private string textError;
        [NonSerialized] private bool planDragging, planPanning;
        [NonSerialized] private Vector2 dragStart, dragDelta;
        [NonSerialized] private Vector3 handleOffset, handleBase;
        [NonSerialized] private bool handleCommitPending;
        [NonSerialized] private Material lineMaterial;
        // A piece waiting to be placed by a click on the plan or in the Scene view (its kind), or null.
        [NonSerialized] private string placing;
        [NonSerialized] private Vector2 rightDown;
        [NonSerialized] private bool rightDragged;
        [NonSerialized] private string notice;

        /// <summary>The pieces in the Add menus, by what they're for (any piece not listed goes under Other).</summary>
        private static readonly (string group, string[] pieces)[] Kinds =
        {
            ("Groups", new[] { "Group", "Placed", "Turned" }),
            ("Solids", new[] { "Block", "Platform", "Prism", "BoxXZ", "Box", "Wall", "Column", "IBeamColumn", "SlabWithHoles", "Ground", "Cells", "ShaftWalls" }),
            ("Stairs and decks", new[] { "Flight", "Ramp", "Walkway", "Bridge" }),
            ("Mechanics", new[] { "MovingPlatform", "PushableCrate", "TimingGate", "LowBeam" }),
            ("Rails and structure", new[] { "Rail", "RailAround", "Truss", "BracedFrame", "Bracket", "OverheadCrane", "Trim" }),
            ("Markings and signs", new[] { "FloorLabel", "AreaSign", "Banner", "GuideLine", "HazardStripes", "CornerBrackets", "Ruler", "TowerBands", "Coping" }),
            ("Lights and utilities", new[] { "Bollard", "Bollards", "Floodlight", "AccentPost", "CellLighting", "CellUtilities", "UtilityStation" }),
            ("Foliage", new[] { "Tree", "Planter", "Lawns", "ScatterTrees" }),
            ("Markers", new[] { "SpawnMarker", "Marker" }),
        };

        /// <summary>Opens the Level Editor window.</summary>
        [MenuItem("Tools/Meridian Motion/Showcase/Levels/Level Editor", priority = 20)]
        public static void ShowWindow() => GetWindow<LevelEditorWindow>("Level Editor").Show();

        /// <summary>Opens the editor on a spec file.</summary>
        public static void Open(string path)
        {
            LevelEditorWindow window = GetWindow<LevelEditorWindow>("Level Editor");
            window.Load(path);
            window.Show();
        }

        private bool Dirty => spec != null && doc != null && doc.json != savedJson;

        // ---- For tests and tools: what the buttons do, without the window ----

        /// <summary>The spec being edited, as JSON (null if none's open).</summary>
        public string Json => spec != null ? doc.json : null;
        /// <summary>The preview, if it's built and still open.</summary>
        public LevelSpecPreview CurrentPreview => preview != null && preview.IsOpen ? preview : null;
        /// <summary>Opens a spec file (as Open does).</summary>
        public void OpenSpec(string path) => Load(path);
        /// <summary>Builds (or rebuilds) the preview (as Preview does).</summary>
        public void Preview() => BuildPreview();
        /// <summary>Picks the first piece with this name (or label); returns whether there was one.</summary>
        public bool SelectPiece(string name)
        {
            JsonObject piece = spec?.Parts.SelectMany(p => p.Pieces).FirstOrDefault(p =>
                (p.TryGetValue("name", out object n) && n as string == name) || (p.TryGetValue("label", out object l) && l as string == name));
            if (piece != null) { Select(piece, false); }
            return piece != null;
        }
        /// <summary>Moves the picked piece by a world offset, as dragging it does.</summary>
        public void MovePicked(Vector3 offset) => MoveSelectedBy(offset);
        /// <summary>The picked piece, or null.</summary>
        public JsonObject Picked => selected;
        /// <summary>
        /// Adds a piece of a kind with its origin at a world point (as clicking to place it does): in the picked group,
        /// or the picked piece's group, after the picked piece. Returns it.
        /// </summary>
        public JsonObject AddPieceAt(string kind, Vector3 world) => PlacePiece(kind, world);
        /// <summary>Runs an edit command on the picked piece, as its key does: "Delete" (or "SoftDelete") or "Duplicate". Returns whether it did.</summary>
        public bool RunCommand(string command) => Command(command, true);
        /// <summary>Takes out of the spec any piece whose objects were deleted from the scene (not with the editor), as the editor does when the hierarchy changes.</summary>
        public void SyncWithScene() => OnHierarchyChanged();

        private void OnEnable()
        {
            minSize = new Vector2(900f, 420f);
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += OnSelectionChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            // After a script reload: the spec's still in the document; the preview has to be built again.
            if (doc != null && !string.IsNullOrEmpty(doc.json)) { TryParse(doc.json); }
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Selection.selectionChanged -= OnSelectionChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        private void OnDestroy()
        {
            if (doc != null) { DestroyImmediate(doc); }
        }

        // ---- The spec ----

        private bool TryParse(string json)
        {
            try
            {
                spec = LevelSpec.FromJson(json);
                loadedJson = json;
                selected = selectedPart >= 0 && selectedPart < spec.Parts.Count && selectedPiece >= 0 && selectedPiece < spec.Parts[selectedPart].Pieces.Count
                    ? spec.Parts[selectedPart].Pieces[selectedPiece] : null;
                return true;
            }
            catch (FormatException e)
            {
                EditorUtility.DisplayDialog("Level Editor", $"That isn't a level spec the editor can open:\n\n{e.Message}", "OK");
                return false;
            }
        }

        private void Load(string path)
        {
            if (Dirty && !EditorUtility.DisplayDialog("Level Editor", $"Discard the unsaved changes to '{spec.Name}'?", "Discard", "Cancel")) { return; }
            string json = File.ReadAllText(path);
            selectedPart = selectedPiece = -1;
            if (!TryParse(json)) { return; }
            if (doc == null) { doc = CreateInstance<LevelSpecDocument>(); doc.hideFlags = HideFlags.DontSave; }
            Undo.ClearUndo(doc);
            doc.json = spec.ToJson();
            doc.path = path.Replace('\\', '/');
            savedJson = doc.json;
            loadedJson = doc.json;
            selected = null;
            preview = null;
            FitPlanTo(null);
            Repaint();
        }

        private void NewLevel()
        {
            string path = EditorUtility.SaveFilePanelInProject("New Level Spec", "NewLevel.level", "json", "Where to keep the new level's spec.", LevelSpecTools.SpecFolder);
            if (string.IsNullOrEmpty(path)) { return; }
            string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));
            string scene = $"Assets/Scenes/{name}/{name}.unity";
            var json = $"{{\"format\": \"{LevelSpec.Format}\", \"version\": {LevelSpec.Version}, \"name\": {LevelSpecJson.Write(ObjectNames.NicifyVariableName(name))}, " +
                       $"\"scene\": {LevelSpecJson.Write(scene)}, \"parts\": [{{\"name\": \"Main\", \"pieces\": [" +
                       "{\"piece\": \"Group\", \"parent\": \"\", \"name\": \"Main\"}," +
                       "{\"piece\": \"SpawnMarker\", \"parent\": \"Main\", \"name\": \"Start\", \"at\": [0, -4], \"height\": 0.1, \"yaw\": 0}," +
                       "{\"piece\": \"Ground\", \"parent\": \"\", \"half\": 50, \"openings\": []}]}]}";
            File.WriteAllText(path, LevelSpec.FromJson(json).ToJson());
            AssetDatabase.ImportAsset(path);
            Load(path);
        }

        private void Save()
        {
            if (spec == null) { return; }
            if (string.IsNullOrEmpty(doc.path))
            {
                doc.path = EditorUtility.SaveFilePanelInProject("Save Level Spec", spec.Name + ".level", "json", "", LevelSpecTools.SpecFolder);
                if (string.IsNullOrEmpty(doc.path)) { return; }
            }
            File.WriteAllText(doc.path, doc.json);
            if (doc.path.StartsWith("Assets/")) { AssetDatabase.ImportAsset(doc.path); }
            savedJson = doc.json;
        }

        /// <summary>Records the document for undo, as an undo step of its own (two edits in one frame are still two steps).</summary>
        private void Record(string what)
        {
            Undo.IncrementCurrentGroup();
            Undo.RecordObject(doc, what);
            Undo.SetCurrentGroupName(what);
        }

        /// <summary>Records an edit for undo and keeps the document in step; then rebuilds what it touched.</summary>
        private void Edited(string what, JsonObject piece = null, bool all = false)
        {
            Record(what);
            doc.json = spec.ToJson();
            loadedJson = doc.json;
            if (preview != null && preview.IsOpen)
            {
                if (all || piece == null) { preview.RebuildAll(spec); }
                else { preview.Rebuild(piece); }
            }
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnUndoRedo()
        {
            if (doc == null || doc.json == loadedJson) { return; }
            if (!TryParse(doc.json)) { return; }
            if (preview != null && preview.IsOpen) { preview.RebuildAll(spec); }
            Repaint();
        }

        // ---- The preview ----

        private void BuildPreview()
        {
            if (spec == null) { return; }
            if (preview == null || !preview.IsOpen)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
                preview = LevelSpecPreview.Build(spec);
            }
            else { preview.RebuildAll(spec); }
            FitPlanTo(preview);
        }

        private void BuildLevel()
        {
            if (Dirty)
            {
                int choice = EditorUtility.DisplayDialogComplex("Build Level", $"Save the changes to '{spec.Name}' first?", "Save and Build", "Cancel", "Build Without Saving");
                if (choice == 1) { return; }
                if (choice == 0) { Save(); }
            }
            // The preview's scene is never saved: leave it without asking, for the build's new scene.
            if (preview != null && preview.IsOpen) { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
            preview = null;
            LevelBuildWindow.BuildFromMenu(LevelSpecTools.SceneFor(spec));
        }

        private void OpenSchematic()
        {
            string folder = Path.Combine("Temp", "LevelEditor");
            Directory.CreateDirectory(folder);
            string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(string.IsNullOrEmpty(doc.path) ? spec.Name : doc.path));
            string json = Path.Combine(folder, name + ".level.json"), html = Path.GetFullPath(Path.Combine(folder, name + ".schematic.html"));
            File.WriteAllText(json, doc.json);
            string python = Application.platform == RuntimePlatform.WindowsEditor ? "python" : "python3";
            var info = new ProcessStartInfo(python, $"\"{Path.Combine("Tools", "LevelSpec", "levelspec")}\" schematic \"{json}\" \"{html}\"")
            {
                UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true,
                WorkingDirectory = Path.GetFullPath("."),
            };
            try
            {
                using Process process = Process.Start(info);
                string errors = process.StandardError.ReadToEnd();
                process.WaitForExit(60000);
                if (process.ExitCode != 0) { throw new InvalidOperationException(errors); }
                Application.OpenURL("file://" + html);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Schematic", $"Couldn't draw the schematic with {python} (Tools/LevelSpec/levelspec):\n\n{e.Message}", "OK");
            }
        }

        // ---- Selection ----

        private void Select(JsonObject piece, bool inScene)
        {
            selected = piece;
            selectedPart = piece == null ? selectedPart : spec.Parts.FindIndex(p => p.Pieces.Contains(piece));
            selectedPiece = piece == null || selectedPart < 0 ? -1 : spec.Parts[selectedPart].Pieces.IndexOf(piece);
            textEdits.Clear();
            textError = null;
            handleOffset = Vector3.zero;
            if (inScene && piece != null && preview != null && preview.IsOpen)
            {
                Object[] objects = preview.ObjectsOf(piece).Cast<Object>().ToArray();
                if (objects.Length > 0) { Selection.objects = objects; }
            }
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnSelectionChanged()
        {
            if (preview == null || !preview.IsOpen || Selection.activeGameObject == null) { return; }
            JsonObject piece = preview.PieceOf(Selection.activeGameObject);
            if (piece != null && piece != selected) { Select(piece, false); }
        }

        private void Frame()
        {
            if (selected != null && preview != null && preview.IsOpen && preview.BoundsOf(selected, out Bounds b))
            {
                SceneView.lastActiveSceneView?.Frame(b, false);
                planCentre = new Vector2(b.center.x, b.center.z);
            }
        }

        // ---- The window ----

        private void OnGUI()
        {
            Toolbar();
            if (spec == null)
            {
                EditorGUILayout.HelpBox("Open a level spec to edit it (Assets/Levels: export the levels' specs from Tools > Meridian Motion > Showcase > Levels > Specs), or start a new one.", MessageType.Info);
                foreach (string path in SpecFiles())
                {
                    if (GUILayout.Button(path, EditorStyles.linkLabel)) { Load(path); }
                }
                if (GUILayout.Button("New Level…", GUILayout.Width(140f))) { NewLevel(); }
                return;
            }
            Keys();
            // The panels fill the rest of the window, laid out from its size (the same in every pass of the GUI, so the
            // list's buttons are always in view), under the toolbar and any message.
            float top = EditorStyles.toolbar.fixedHeight > 0f ? EditorStyles.toolbar.fixedHeight : 21f;
            if (placing != null || notice != null)
            {
                var message = new Rect(4f, top + 2f, position.width - 8f, 36f);
                EditorGUI.HelpBox(message, placing != null
                    ? $"Click on the plan or in the Scene view to place the new {placing}. Enter places it at the middle of the plan; Esc cancels."
                    : notice, placing != null ? MessageType.Info : MessageType.Warning);
                if (notice != null && GUI.Button(new Rect(message.xMax - 24f, message.y + 9f, 18f, 18f), "×", EditorStyles.miniButton)) { notice = null; }
                top = message.yMax + 2f;
            }
            var area = new Rect(0f, top, position.width, Mathf.Max(100f, position.height - top));
            var list = new Rect(area.x, area.y, ListWidth, area.height);
            var inspector = new Rect(area.xMax - InspectorWidth, area.y, InspectorWidth, area.height);
            var plan = new Rect(list.xMax + 1f, area.y, inspector.x - list.xMax - 2f, area.height);
            GUILayout.BeginArea(list);
            PieceList();
            GUILayout.EndArea();
            Plan(plan);
            GUILayout.BeginArea(inspector);
            Inspector();
            GUILayout.EndArea();
            EditorGUI.DrawRect(new Rect(list.xMax, area.y, 1f, area.height), new Color(0f, 0f, 0f, 0.3f));
            EditorGUI.DrawRect(new Rect(inspector.x - 1f, area.y, 1f, area.height), new Color(0f, 0f, 0f, 0.3f));
        }

        private static IEnumerable<string> SpecFiles() =>
            Directory.Exists(LevelSpecTools.SpecFolder)
                ? Directory.GetFiles(LevelSpecTools.SpecFolder, "*.level.json").Select(p => p.Replace('\\', '/')).OrderBy(p => p)
                : Enumerable.Empty<string>();

        private void Toolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (EditorGUILayout.DropdownButton(new GUIContent("Open"), FocusType.Passive, EditorStyles.toolbarDropDown, GUILayout.Width(60f)))
                {
                    var menu = new GenericMenu();
                    foreach (string path in SpecFiles()) { menu.AddItem(new GUIContent(Path.GetFileName(path)), false, () => Load(path)); }
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Open File…"), false, () =>
                    {
                        string path = EditorUtility.OpenFilePanel("Open Level Spec", LevelSpecTools.SpecFolder, "json");
                        if (!string.IsNullOrEmpty(path)) { Load(path); }
                    });
                    menu.AddItem(new GUIContent("New Level…"), false, NewLevel);
                    menu.ShowAsContext();
                }
                if (spec != null)
                {
                    GUILayout.Label($"{spec.Name}{(Dirty ? " *" : "")}", EditorStyles.toolbarButton, GUILayout.MaxWidth(240f));
                    using (new EditorGUI.DisabledScope(!Dirty))
                    {
                        if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(44f))) { Save(); }
                        if (GUILayout.Button("Revert", EditorStyles.toolbarButton, GUILayout.Width(50f)) &&
                            EditorUtility.DisplayDialog("Level Editor", "Discard the unsaved changes?", "Discard", "Cancel"))
                        {
                            Record("Revert Level");
                            doc.json = savedJson;
                            OnUndoRedo();
                        }
                    }
                    GUILayout.Space(10f);
                    bool open = preview != null && preview.IsOpen;
                    if (GUILayout.Button(new GUIContent(open ? "Rebuild Preview" : "Preview", "Build the level in an unsaved scene to edit it (no bakes)."), EditorStyles.toolbarButton, GUILayout.Width(100f)))
                    {
                        BuildPreview();
                    }
                    if (GUILayout.Button(new GUIContent("Build Level", "Build the level's scene as its builder would: environment, lighting and occlusion."), EditorStyles.toolbarButton, GUILayout.Width(76f)))
                    {
                        BuildLevel();
                    }
                    if (GUILayout.Button(new GUIContent("Set Up Scene…", "Make the open scene playable: the character spawner, camera and the level's mechanics."), EditorStyles.toolbarButton, GUILayout.Width(92f)))
                    {
                        EditorApplication.ExecuteMenuItem("Tools/Meridian Motion/Set Up Scene…");
                    }
                    if (GUILayout.Button(new GUIContent("Schematic", "Open the level's schematic, with the movement checks (needs python3)."), EditorStyles.toolbarButton, GUILayout.Width(70f)))
                    {
                        OpenSchematic();
                    }
                }
                GUILayout.FlexibleSpace();
                snap = GUILayout.Toggle(snap, new GUIContent("Snap", "Moves snap to the grid."), EditorStyles.toolbarButton, GUILayout.Width(40f));
                snapStep = Mathf.Max(0.05f, EditorGUILayout.FloatField(snapStep, EditorStyles.toolbarTextField, GUILayout.Width(36f)));
                GUILayout.Label("m", EditorStyles.miniLabel, GUILayout.Width(14f));
            }
        }

        /// <summary>Where new pieces go: after the selected one, in its part (or at the end of the selected part, or the last).</summary>
        private (LevelSpec.Part part, int index) Insertion()
        {
            if (selected != null)
            {
                LevelSpec.Part part = spec.Parts.First(p => p.Pieces.Contains(selected));
                return (part, part.Pieces.IndexOf(selected) + 1);
            }
            if (spec.Parts.Count == 0) { spec.Parts.Add(new LevelSpec.Part { Name = "Main" }); }
            LevelSpec.Part target = spec.Parts[Mathf.Clamp(selectedPart, 0, spec.Parts.Count - 1)];
            return (target, target.Pieces.Count);
        }
    }
}
