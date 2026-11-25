using MeridianMotion.Levels;
using MeridianMotion.Levels.EditorTools;
using MeridianMotion.Showcase.Ivy;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.Showcase.Levels
{
    // Flora on walls (ReferenceAssets/OpenTasks.md, P9, Q2, Q10): ivy climbing a wall from its foot, and vines hanging
    // from its top, both grown as the level's built and drawn on the GPU (the level's IvyField). Only on plain walls: a point whose wall
    // is climbable or marked for a move (a climbable panel, a wall-run or wall-slide wall, a ladder: by their fixture
    // names, as the game setup knows them, or the components it gives them) gets none, so the flora never says "climb
    // here" where a character can't, or hides where it can. Placed once the level's built (see Defer), so the walls'
    // colliders are there to check. No colliders of their own. Without the ivy's shaders or the flora's prefabs, nothing.
    public static partial class Graybox
    {
        /// <summary>
        /// Ivy on a wall's face, along it from a to b (points on the face, in parent's space), the face looking outward, from
        /// y0 up to (at most) y1: grown on the GPU (ReferenceAssets/OpenTasks.md, Q2), as the grass is. The face's ivy is
        /// grown as the level's built (IvyGrowth: stems climbing from roots along its foot, cover how close together, to
        /// heights the foliage's Ivy Growth picks), its mask and stems added to the level's Ivy Field (one per level, under
        /// its root, made by the first Ivy piece), which places the leaves before each camera renders. Seeded from the
        /// level's flora seed, the settings' seed and salt. Only on plain walls (see the file's top): the growth stops at a
        /// climbable or marked wall, or where there's no wall, and goes behind what stands proud of the face (a pilaster)
        /// with no leaves on it.
        /// </summary>
        [Piece]
        public static void Ivy(Transform parent, Vector2 a, Vector2 b, Vector2 outward, float y0, float y1, int salt, float cover = 0.6f)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Ivy), parent, a, b, outward, y0, y1, salt, cover);
            Defer(() =>
            {
                IvyGrowth.Settings settings = Settings.foliage.ivyGrowth;
                settings.cover = Mathf.Clamp01(settings.cover * cover);
                GrowOnWall(parent, a, b, outward, y0, y1, (int)Mix((uint)FloraSeed, (uint)(Settings.foliage.seed + 300 + salt)), settings, hangs: false);
            });
        }

        /// <summary>Where the ivy's shaders and style are (Create Grass Assets makes the style).</summary>
        public const string IvyFolder = "Assets/Showcase/Ivy/";
        public const string IvyStylePath = IvyFolder + "Styles/Ivy.asset";

        /// <summary>The material the ivy's stems are drawn with: the flora's stem bark (Generate Flora makes it).</summary>
        public const string IvyStemMaterialPath = "Assets/Showcase/Models/Flora/Materials/M_Bark_Stem.mat";

        /// <summary>The level's ivy field's name (see IvyFieldFor).</summary>
        public const string IvyFieldName = "Ivy Field";

        /// <summary>
        /// How far out from a face (m) its wall test starts, and how much nearer something has to be to stand proud of it
        /// (a pilaster), or further to be a hole in it (an opening, a recess).
        /// </summary>
        private const float IvyProbe = 0.3f, IvyProud = 0.04f, IvyRecess = 0.1f;

        private static bool warnedNoIvy;

        /// <summary>
        /// Grows a wall's ivy (or, hangs, its vines) over the face from a to b, y0 to y1 (in parent's space), looking
        /// outward, from seed, as settings say, and adds it to the level's ivy field.
        /// </summary>
        private static void GrowOnWall(Transform parent, Vector2 a, Vector2 b, Vector2 outward, float y0, float y1, int seed, IvyGrowth.Settings settings, bool hangs)
        {
            float length = Vector2.Distance(a, b);
            if (length < 0.1f || y1 - y0 < 0.1f || settings.cover <= 0f) { return; }
            IvyField field = IvyFieldFor(parent);
            if (field == null) { return; }
            Vector2 way = (b - a) / length;
            Vector3 cornerHere = new(a.x, y0, a.y), alongHere = new(way.x, 0f, way.y), outHere = new Vector3(outward.x, 0f, outward.y).normalized;
            Vector3 cornerWorld = parent.TransformPoint(cornerHere), alongWorld = parent.TransformDirection(alongHere).normalized;
            Vector3 upWorld = parent.TransformDirection(Vector3.up).normalized, outWorld = parent.TransformDirection(outHere).normalized;

            IvyGrowth.Surface WallAt(Vector2 uv)
            {
                Vector3 at = cornerWorld + alongWorld * uv.x + upWorld * uv.y + outWorld * IvyProbe;
                if (!Physics.Raycast(at, -outWorld, out RaycastHit hit, IvyProbe + IvyRecess, ~0, QueryTriggerInteraction.Ignore)) { return IvyGrowth.Surface.None; }
                if (IsClimbableOrMarked(hit.collider.transform)) { return IvyGrowth.Surface.None; }
                return hit.distance < IvyProbe - IvyProud ? IvyGrowth.Surface.Proud : IvyGrowth.Surface.Face;
            }

            var size = new Vector2(length, y1 - y0);
            IvyGrowth.Grown grown = hangs ? IvyGrowth.Hang(size, seed, settings, WallAt) : IvyGrowth.Grow(size, seed, settings, WallAt);
            Transform f = field.transform;
            field.AddFace(IvyField.Face.From(grown, f.InverseTransformPoint(cornerWorld), f.InverseTransformDirection(alongWorld).normalized,
                f.InverseTransformDirection(upWorld).normalized, f.InverseTransformDirection(outWorld).normalized, size, hangs));
            EnsureWind(parent);
        }

        /// <summary>
        /// The level's ivy field (an IvyField named IvyFieldName under parent's root), made with the ivy's style, shaders
        /// and stem material if it hasn't one. Without the ivy's shaders, none (with one warning): no ivy.
        /// </summary>
        private static IvyField IvyFieldFor(Transform parent)
        {
            Transform root = parent.root;
            foreach (IvyField existing in root.GetComponentsInChildren<IvyField>(true)) { if (existing.name == IvyFieldName) { return existing; } }
            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(IvyFolder + "IvyLeaves.compute");
            var leaves = AssetDatabase.LoadAssetAtPath<Shader>(IvyFolder + "Ivy.shader");
            var far = AssetDatabase.LoadAssetAtPath<Shader>(IvyFolder + "IvyFar.shader");
            if (compute == null || leaves == null || far == null)
            {
                if (!warnedNoIvy) { warnedNoIvy = true; Debug.LogWarning($"[Graybox] No ivy shaders in '{IvyFolder}': the Ivy pieces grow nothing."); }
                return null;
            }
            var obj = new GameObject(IvyFieldName);
            obj.transform.SetParent(root, false);
            var field = obj.AddComponent<IvyField>();
            field.Style = AssetDatabase.LoadAssetAtPath<IvyStyle>(IvyStylePath);
            field.SetLook(compute, leaves, far, AssetDatabase.LoadAssetAtPath<Material>(IvyStemMaterialPath));
            return field;
        }

        /// <summary>
        /// Vines hanging from a wall's top at top, along it from a to b (points on the face, in parent's space), the face
        /// looking outward, none hanging further than drop: grown on the GPU as the ivy is (ReferenceAssets/OpenTasks.md,
        /// Q10), from roots along the top (cover how close together, times the foliage's Vine Growth's), each strand
        /// hanging to a length its Height picks, its leaves thinning and shrinking toward its tip (IvyGrowth.Hang), added
        /// to the level's Ivy Field as a hanging face. Seeded as the ivy is, with its own salt. Only on plain walls (see the
        /// file's top).
        /// </summary>
        [Piece]
        public static void Vines(Transform parent, Vector2 a, Vector2 b, Vector2 outward, float top, float drop, int salt, float cover = 0.5f)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Vines), parent, a, b, outward, top, drop, salt, cover);
            Defer(() =>
            {
                IvyGrowth.Settings settings = Settings.foliage.vineGrowth;
                settings.cover = Mathf.Clamp01(settings.cover * cover);
                GrowOnWall(parent, a, b, outward, top - drop, top, (int)Mix((uint)FloraSeed, (uint)(Settings.foliage.seed + 400 + salt)), settings, hangs: true);
            });
        }

        /// <summary>Whether a wall (a collider's object, or one it's under) is climbable or marked for a move.</summary>
        internal static bool IsClimbableOrMarked(Transform wall)
        {
            for (Transform t = wall; t != null; t = t.parent)
            {
                string n = t.name;
                if (n.StartsWith(LevelFixtureSetup.ClimbablePrefix, System.StringComparison.Ordinal) ||
                    n.StartsWith(LevelFixtureSetup.WallRunWallPrefix, System.StringComparison.Ordinal) ||
                    n.StartsWith(LevelFixtureSetup.WallSlideWallPrefix, System.StringComparison.Ordinal) ||
                    n.StartsWith(LevelFixtureSetup.LadderPrefix, System.StringComparison.Ordinal)) { return true; }
                if (t.GetComponent<ClimbableSurface>() != null || t.GetComponent<PaintedClimbableSurface>() != null ||
                    t.GetComponent<TraversableWall>() != null || t.GetComponent<ClimbableLadder>() != null) { return true; }
            }
            return false;
        }
    }
}
