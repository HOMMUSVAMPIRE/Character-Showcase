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
    public sealed class LevelEditorWindow : EditorWindow
    {
        private const float ListWidth = 290f, InspectorWidth = 340f;

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

        [MenuItem("Tools/Character Showcase/Level/Level Editor", priority = 20)]
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
                EditorGUILayout.HelpBox("Open a level spec to edit it (Assets/Levels: export the levels' specs from Tools > Character Showcase > Level > Specs), or start a new one.", MessageType.Info);
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
                        EditorApplication.ExecuteMenuItem("Tools/Character Showcase/Set Up Scene…");
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
            LevelSpecEdit.Translate(copy, 2f, 0f, 2f);
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

        // ---- The inspector ----

        private void Inspector()
        {
            inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);
            EditorGUIUtility.labelWidth = 110f;
            if (selected == null && selectedPart >= 0 && selectedPart < spec.Parts.Count) { PartInspector(spec.Parts[selectedPart]); }
            else if (selected != null) { PieceInspector(selected); }
            else { EditorGUILayout.HelpBox("Pick a piece in the list, on the plan, or in the Scene view.", MessageType.None); }
            EditorGUILayout.EndScrollView();
        }

        private void PartInspector(LevelSpec.Part part)
        {
            EditorGUILayout.LabelField("Part", EditorStyles.boldLabel);
            string name = EditorGUILayout.DelayedTextField("Name", part.Name);
            if (name != part.Name && !string.IsNullOrWhiteSpace(name))
            {
                part.Name = name;
                Edited("Rename Part", all: false);
            }
            EditorGUILayout.LabelField("Pieces", part.Pieces.Count.ToString());
            EditorGUILayout.HelpBox("A part is built as one step of the level's build, starting from the generation settings' step size and deck dimensions.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                int i = spec.Parts.IndexOf(part);
                if (GUILayout.Button("Move Up") && i > 0) { (spec.Parts[i], spec.Parts[i - 1]) = (spec.Parts[i - 1], spec.Parts[i]); selectedPart = i - 1; Edited("Move Part", all: true); }
                if (GUILayout.Button("Move Down") && i < spec.Parts.Count - 1) { (spec.Parts[i], spec.Parts[i + 1]) = (spec.Parts[i + 1], spec.Parts[i]); selectedPart = i + 1; Edited("Move Part", all: true); }
                if (GUILayout.Button("Delete")) { DeletePart(part); }
            }
        }

        private void PieceInspector(JsonObject piece)
        {
            string kind = piece["piece"] as string;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(kind, EditorStyles.boldLabel);
                if (GUILayout.Button(new GUIContent("Frame", "Show it in the Scene view and the plan"), GUILayout.Width(52f))) { Frame(); }
                if (GUILayout.Button(new GUIContent("Duplicate", "Duplicate it (Ctrl/Cmd+D)"), GUILayout.Width(70f))) { Duplicate(); }
                if (GUILayout.Button(new GUIContent("Delete", "Delete it (Delete)"), GUILayout.Width(52f))) { Delete(); return; }
            }
            string problem = preview != null && preview.Problems.TryGetValue(piece, out string p) ? p : LevelSpec.Problem(piece);
            if (problem != null) { EditorGUILayout.HelpBox(problem, MessageType.Error); }

            if (kind == LevelSpec.StatePiece)
            {
                EditorGUILayout.HelpBox("Sets these for the pieces after it in its part.", MessageType.None);
                foreach ((string key, _, _) in LevelSpec.StateFields)
                {
                    bool has = piece.TryGetValue(key, out object v);
                    float value = has ? (float)(double)v : 0f;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        float changed = EditorGUILayout.DelayedFloatField(ObjectNames.NicifyVariableName(key), value);
                        if (EditorGUI.EndChangeCheck()) { piece[key] = (double)changed; Edited("Edit State", piece, all: true); }
                        if (has && GUILayout.Button("×", GUILayout.Width(20f))) { piece.Remove(key); Edited("Edit State", piece, all: true); }
                    }
                }
                return;
            }
            MethodInfo method = LevelSpecEdit.MethodOf(piece);
            if (method == null) { return; }
            foreach (ParameterInfo parameter in method.GetParameters()) { Setting(piece, parameter); }
            if (textError != null) { EditorGUILayout.HelpBox(textError, MessageType.Error); }
        }

        private void Setting(JsonObject piece, ParameterInfo p)
        {
            Type t = p.ParameterType;
            bool has = piece.ContainsKey(p.Name);
            object value = LevelSpecEdit.Get(piece, p);
            var label = new GUIContent(ObjectNames.NicifyVariableName(p.Name),
                LevelSpec.Describe(t) + (p.HasDefaultValue ? $" (default {LevelSpecJson.Write(LevelSpec.Encode(LevelSpec.DefaultOf(p), null))})" : ""));
            bool all = piece["piece"] is "Group" or "Placed" or "Turned";
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(false))
                {
                    GUI.color = has || !p.HasDefaultValue ? Color.white : new Color(1f, 1f, 1f, 0.6f);
                    EditorGUI.BeginChangeCheck();
                    object changed = Field(label, t, value, p, piece);
                    GUI.color = Color.white;
                    if (EditorGUI.EndChangeCheck() && changed != null)
                    {
                        if (changed is JsonText text) { piece[p.Name] = text.json; LevelSpecEdit.Tidy(piece); }
                        else { LevelSpecEdit.Set(piece, p, changed); }
                        Edited($"Edit {ObjectNames.NicifyVariableName(p.Name)}", piece, all);
                    }
                }
                if (has && p.HasDefaultValue && GUILayout.Button(new GUIContent("×", "Back to its default"), GUILayout.Width(20f)))
                {
                    piece.Remove(p.Name);
                    Edited($"Reset {ObjectNames.NicifyVariableName(p.Name)}", piece, all);
                }
            }
        }

        /// <summary>A setting edited as JSON text, applied once it reads.</summary>
        private sealed class JsonText
        {
            public object json;
        }

        private object Field(GUIContent label, Type t, object value, ParameterInfo p, JsonObject piece)
        {
            if (t == typeof(Transform))
            {
                string current = value as string ?? "";
                List<string> paths = preview != null && preview.IsOpen ? preview.GroupPaths() : new List<string> { "" };
                if (!paths.Contains(current)) { paths.Insert(0, current); }
                int i = EditorGUILayout.Popup(label, paths.IndexOf(current), paths.Select(x => x == "" ? "(the root)" : x.Replace("/", " ∕ ")).ToArray());
                return i >= 0 && paths[i] != current ? paths[i] : null;
            }
            if (t == typeof(float)) { return EditorGUILayout.DelayedFloatField(label, value is float f ? f : 0f); }
            if (t == typeof(int)) { return EditorGUILayout.DelayedIntField(label, value is int n ? n : 0); }
            if (t == typeof(bool)) { return EditorGUILayout.Toggle(label, value is true); }
            if (t == typeof(string)) { return EditorGUILayout.DelayedTextField(label, value as string ?? ""); }
            if (t.IsEnum) { return EditorGUILayout.EnumPopup(label, value as Enum ?? (Enum)Enum.GetValues(t).GetValue(0)); }
            if (t == typeof(Vector2))
            {
                var v = value is Vector2 v2 ? v2 : Vector2.zero;
                float[] c = Numbers(label, new[] { v.x, v.y }, "X", "Z");
                return c == null ? null : new Vector2(c[0], c[1]);
            }
            if (t == typeof(Vector3))
            {
                var v = value is Vector3 v3 ? v3 : Vector3.zero;
                float[] c = Numbers(label, new[] { v.x, v.y, v.z }, "X", "Y", "Z");
                return c == null ? null : new Vector3(c[0], c[1], c[2]);
            }
            if (t == typeof(UnityEngine.Rect))
            {
                var r = value is UnityEngine.Rect rect ? rect : default;
                float[] c = Numbers(label, new[] { r.xMin, r.yMin, r.xMax, r.yMax }, "X0", "Z0", "X1", "Z1");
                return c == null ? null : UnityEngine.Rect.MinMaxRect(c[0], c[1], c[2], c[3]);
            }
            // Lists, cells, tuples and randoms: as JSON.
            string key = p.Name;
            string shown = textEdits.TryGetValue(key, out string edit) ? edit
                : piece.TryGetValue(p.Name, out object json) ? LevelSpecJson.Write(json) : LevelSpecJson.Write(LevelSpec.Encode(value, null));
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField(label);
                string typed = EditorGUILayout.TextArea(shown, new GUIStyle(EditorStyles.textArea) { wordWrap = true }, GUILayout.MinHeight(34f));
                if (typed != shown) { textEdits[key] = typed; }
                if (textEdits.ContainsKey(key) && GUILayout.Button("Apply", GUILayout.Width(60f)))
                {
                    try
                    {
                        object parsed = LevelSpecJson.Parse(typed);
                        LevelSpec.Decode(parsed, t, null);
                        textEdits.Remove(key);
                        textError = null;
                        GUI.changed = true;
                        return new JsonText { json = parsed };
                    }
                    catch (Exception e) when (e is FormatException or InvalidCastException)
                    {
                        textError = $"{ObjectNames.NicifyVariableName(p.Name)}: {(e is FormatException ? e.Message : $"that isn't {LevelSpec.Describe(t)}.")}";
                    }
                }
            }
            return null;
        }

        /// <summary>A row of number fields that change once typed (not each keystroke); null if none did.</summary>
        private static float[] Numbers(GUIContent label, float[] values, params string[] names)
        {
            Rect r = EditorGUILayout.GetControlRect();
            r = EditorGUI.PrefixLabel(r, label);
            float w = r.width / values.Length;
            float[] result = null;
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 16f;
            for (int i = 0; i < values.Length; i++)
            {
                EditorGUI.BeginChangeCheck();
                float v = EditorGUI.DelayedFloatField(new Rect(r.x + i * w, r.y, w - 2f, r.height), names[i], values[i]);
                if (EditorGUI.EndChangeCheck())
                {
                    result ??= (float[])values.Clone();
                    result[i] = v;
                }
            }
            EditorGUIUtility.labelWidth = labelWidth;
            EditorGUI.indentLevel = indent;
            return result;
        }

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
