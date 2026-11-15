using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// A level spec built in an unsaved scene for the level editor to show and edit: as a build makes it (the same
    /// pieces in the same order, deferred touches last) but without the environment or bakes, and knowing which
    /// objects each piece made, so one piece can be rebuilt by itself when it changes (Rebuild), an object in the
    /// Scene view traced to its piece (PieceOf), and the plan drawn from what's there (Footprints).
    /// </summary>
    public sealed class LevelSpecPreview
    {
        /// <summary>One thing to draw on the plan: an object's outline on the ground, how high its top is, and whose it is.</summary>
        public readonly struct Footprint
        {
            public readonly Vector2[] corners;
            public readonly float top;
            public readonly JsonObject piece;

            public Footprint(Vector2[] corners, float top, JsonObject piece)
            {
                this.corners = corners;
                this.top = top;
                this.piece = piece;
            }
        }

        public Transform Root { get; private set; }
        public Scene Scene { get; private set; }
        /// <summary>What went wrong building each piece that failed (they're skipped, so the rest still shows).</summary>
        public readonly Dictionary<JsonObject, string> Problems = new();
        public readonly List<Footprint> Footprints = new();
        public Bounds PlanBounds { get; private set; }

        private LevelSpec spec;
        private Material[] palette;
        private Material concrete;
        private readonly Dictionary<JsonObject, List<GameObject>> made = new();
        private readonly Dictionary<GameObject, JsonObject> owners = new();
        private readonly HashSet<Transform> known = new();
        // The pieces that put off finishing touches (see Graybox.Defer): what they place looks at what's round it.
        private readonly HashSet<JsonObject> deferOwners = new();

        /// <summary>Whether the preview's still in the open scene (it's gone once another scene's opened).</summary>
        public bool IsOpen => Root != null && Scene.IsValid() && Scene.isLoaded;

        /// <summary>Builds spec in a new, unsaved scene (replacing what's open: offer to save it first).</summary>
        public static LevelSpecPreview Build(LevelSpec spec)
        {
            var preview = new LevelSpecPreview { spec = spec };
            preview.Scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            preview.palette = GrayboxScene.Palette();
            preview.concrete = preview.palette[(int)Graybox.Surface.Concrete];
            preview.Root = new GameObject(spec.Name).transform;
            preview.BuildAll();
            return preview;
        }

        private void BuildAll()
        {
            known.Clear();
            Note(Root);
            Graybox.ClearDeferred();
            foreach (LevelSpec.Part part in spec.Parts)
            {
                Graybox.UsePalette(palette);
                Graybox.Begin(concrete);
                foreach (JsonObject piece in part.Pieces) { BuildTracked(piece); }
            }
            Graybox.UsePalette(palette);
            Graybox.Begin(concrete);
            RunDeferred();
            Refresh();
        }

        private void BuildTracked(JsonObject piece)
        {
            Graybox.DeferOwner = piece;
            Track(piece, () =>
            {
                try
                {
                    LevelSpec.BuildPiece(piece, Root);
                    Problems.Remove(piece);
                }
                catch (InvalidOperationException e) { Problems[piece] = e.Message; }
            });
        }

        private void Note(Transform t)
        {
            foreach (Transform each in t.GetComponentsInChildren<Transform>(true)) { known.Add(each); }
        }

        /// <summary>Runs build, and puts down every object it made as owner's.</summary>
        private void Track(JsonObject owner, Action build)
        {
            build();
            if (owner == null) { return; }
            if (!made.TryGetValue(owner, out List<GameObject> list)) { made[owner] = list = new List<GameObject>(); }
            foreach (Transform t in Root.GetComponentsInChildren<Transform>(true))
            {
                if (known.Add(t))
                {
                    list.Add(t.gameObject);
                    owners[t.gameObject] = owner;
                }
            }
        }

        /// <summary>The piece that made obj (or the nearest object above it that a piece made), or null.</summary>
        public JsonObject PieceOf(GameObject obj)
        {
            for (Transform t = obj != null ? obj.transform : null; t != null && t != Root; t = t.parent)
            {
                if (owners.TryGetValue(t.gameObject, out JsonObject piece)) { return piece; }
            }
            return null;
        }

        /// <summary>What a piece made (the objects at the top of what it made: their children are theirs too).</summary>
        public List<GameObject> ObjectsOf(JsonObject piece)
        {
            if (!made.TryGetValue(piece, out List<GameObject> list)) { return new List<GameObject>(); }
            var set = new HashSet<GameObject>(list.Where(o => o != null));
            return set.Where(o => o.transform.parent == null || !set.Contains(o.transform.parent.gameObject)).ToList();
        }

        /// <summary>Pieces that made something, all of which has since been deleted from the scene (not by the preview).</summary>
        public List<JsonObject> DeletedPieces() =>
            made.Where(m => m.Value.Count > 0 && m.Value.All(o => o == null)).Select(m => m.Key).ToList();

        /// <summary>The world bounds of what a piece made (renderers and colliders), if it made anything to see.</summary>
        public bool BoundsOf(JsonObject piece, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            if (!made.TryGetValue(piece, out List<GameObject> list)) { return false; }
            foreach (GameObject o in list.Where(o => o != null))
            {
                foreach (Renderer r in o.GetComponents<Renderer>())
                {
                    if (any) { bounds.Encapsulate(r.bounds); } else { bounds = r.bounds; any = true; }
                }
            }
            if (!any)
            {
                foreach (GameObject o in list.Where(o => o != null))
                {
                    if (any) { bounds.Encapsulate(o.transform.position); } else { bounds = new Bounds(o.transform.position, Vector3.one); any = true; }
                }
            }
            return any;
        }

        /// <summary>The Transform a piece's parent path names in the preview (the root if it can't be found).</summary>
        public Transform ParentOf(JsonObject piece)
        {
            string path = new[] { "parent", "bridges", "lighting", "rails" }.Select(k => piece.TryGetValue(k, out object v) ? v as string : null)
                .FirstOrDefault(v => v != null) ?? "";
            try { return LevelSpec.Resolve(path, Root); }
            catch (FormatException) { return Root; }
        }

        /// <summary>
        /// Every group's path in the preview (the root as ""), in order: where a piece can be put. Groups are what the
        /// Group, Placed and Turned pieces made.
        /// </summary>
        public List<string> GroupPaths()
        {
            var paths = new List<string> { "" };
            foreach (LevelSpec.Part part in spec.Parts)
            {
                foreach (JsonObject piece in part.Pieces)
                {
                    if (piece["piece"] is "Group" or "Placed" or "Turned")
                    {
                        foreach (GameObject o in ObjectsOf(piece)) { paths.Add(LevelSpec.PathOf(o.transform, Root)); }
                    }
                }
            }
            return paths;
        }

        /// <summary>
        /// Builds one piece again (after its settings changed), with every piece that put off finishing touches until
        /// the level was built (lawns, trees, spawn pads: they look at what's round them), in the build's order, each
        /// one's objects put back where they were among their siblings: the level ends up as a full build of the
        /// changed spec would make it. A group, or a State entry, changes what comes after it, so the whole preview's
        /// built again for those. Returns whether it rebuilt only those pieces.
        /// </summary>
        public bool Rebuild(JsonObject piece)
        {
            if (!spec.Parts.Any(p => p.Pieces.Contains(piece)) || piece["piece"] is "Group" or "Placed" or "Turned" or LevelSpec.StatePiece)
            {
                RebuildAll();
                return false;
            }
            RebuildPieces(new[] { piece });
            return true;
        }

        /// <summary>Takes away what a removed piece made (and redoes the finishing touches that might have seen it).</summary>
        public void Remove(JsonObject piece)
        {
            List<GameObject> old = ObjectsOf(piece);
            Forget(piece);
            foreach (GameObject o in old) { Object.DestroyImmediate(o); }
            Problems.Remove(piece);
            deferOwners.Remove(piece);
            RebuildPieces(Array.Empty<JsonObject>());
        }

        private void RebuildPieces(IEnumerable<JsonObject> changed)
        {
            var rebuild = new HashSet<JsonObject>(changed);
            rebuild.UnionWith(deferOwners);
            // Where each piece's objects were, to put the new ones back there.
            var was = new Dictionary<JsonObject, List<(Transform parent, int index)>>();
            foreach (JsonObject p in rebuild)
            {
                was[p] = ObjectsOf(p).Select(o => (o.transform.parent, o.transform.GetSiblingIndex())).ToList();
            }
            foreach (JsonObject p in rebuild)
            {
                List<GameObject> old = ObjectsOf(p);
                Forget(p);
                foreach (GameObject o in old) { Object.DestroyImmediate(o); }
            }
            Physics.SyncTransforms();

            // In the build's order, with the dimensions each part's State entries set as it goes.
            Graybox.ClearDeferred();
            foreach (LevelSpec.Part part in spec.Parts)
            {
                Graybox.UsePalette(palette);
                Graybox.Begin(concrete);
                foreach (JsonObject p in part.Pieces)
                {
                    if (rebuild.Contains(p)) { BuildTracked(p); }
                    else if (p["piece"] as string == LevelSpec.StatePiece) { LevelSpec.BuildPiece(p, Root); }
                }
            }
            Graybox.UsePalette(palette);
            Graybox.Begin(concrete);
            RunDeferred();

            // Back where they were: the new objects are at the ends of their parents, so putting them back in order
            // of where the old ones were puts each in its place.
            var moves = new List<(Transform t, int index)>();
            foreach ((JsonObject p, List<(Transform parent, int index)> places) in was)
            {
                List<GameObject> now = ObjectsOf(p);
                for (int i = 0; i < Math.Min(now.Count, places.Count); i++)
                {
                    if (now[i].transform.parent == places[i].parent) { moves.Add((now[i].transform, places[i].index)); }
                }
            }
            foreach ((Transform t, int index) in moves.OrderBy(m => m.index)) { t.SetSiblingIndex(Mathf.Min(index, t.parent.childCount - 1)); }
            Refresh();
        }

        private void RunDeferred()
        {
            Graybox.RunDeferred((owner, place) =>
            {
                if (owner is JsonObject piece) { deferOwners.Add(piece); }
                Track(owner as JsonObject, place);
            });
            Graybox.DeferOwner = null;
        }

        private void Forget(JsonObject piece)
        {
            if (!made.TryGetValue(piece, out List<GameObject> list)) { return; }
            foreach (GameObject o in list)
            {
                if (o == null) { continue; }
                owners.Remove(o);
                foreach (Transform t in o.GetComponentsInChildren<Transform>(true)) { known.Remove(t); }
            }
            made.Remove(piece);
        }

        /// <summary>Builds the whole preview again, in the same scene (for a new spec, or changes that reach past one piece).</summary>
        public void RebuildAll(LevelSpec newSpec = null)
        {
            if (newSpec != null) { spec = newSpec; }
            if (Root != null) { Object.DestroyImmediate(Root.gameObject); }
            made.Clear();
            owners.Clear();
            known.Clear();
            deferOwners.Clear();
            Problems.Clear();
            Root = new GameObject(spec.Name).transform;
            SceneManager.MoveGameObjectToScene(Root.gameObject, Scene);
            BuildAll();
        }

        // ---- The plan ----

        /// <summary>Works out every object's outline for the plan: each visible mesh's box, from above.</summary>
        private void Refresh()
        {
            Footprints.Clear();
            bool any = false;
            var all = new Bounds();
            foreach ((JsonObject piece, List<GameObject> list) in made)
            {
                foreach (GameObject o in list)
                {
                    if (o == null || o.name == "Grass Tufts" || !o.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null) { continue; }
                    if (!o.TryGetComponent(out MeshRenderer renderer) || !renderer.enabled) { continue; }
                    // Lettering is drawn by its sign or floor.
                    if (o.GetComponents<Component>().Any(c => c.GetType().Name.StartsWith("TextMeshPro"))) { continue; }
                    Bounds b = filter.sharedMesh.bounds;
                    Matrix4x4 m = o.transform.localToWorldMatrix;
                    Vector3 Corner(float x, float z) => m.MultiplyPoint3x4(new Vector3(x, b.center.y, z));
                    var corners = new[] { Corner(b.min.x, b.min.z), Corner(b.max.x, b.min.z), Corner(b.max.x, b.max.z), Corner(b.min.x, b.max.z) }
                        .Select(c => new Vector2(c.x, c.z)).ToArray();
                    Footprints.Add(new Footprint(corners, renderer.bounds.max.y, piece));
                    if (any) { all.Encapsulate(renderer.bounds); } else { all = renderer.bounds; any = true; }
                }
            }
            // Low things first, so what's higher is drawn over them.
            Footprints.Sort((a, b) => a.top.CompareTo(b.top));
            PlanBounds = any ? all : new Bounds(Vector3.zero, new Vector3(20f, 1f, 20f));
        }
    }
}
