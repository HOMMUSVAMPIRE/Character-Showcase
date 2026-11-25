using MeridianMotion.Levels;
using MeridianMotion.Levels.EditorTools;
using UnityEngine;

namespace MeridianMotion.Showcase.Levels
{
    // Flora on walls (ReferenceAssets/OpenTasks.md, P9): ivy climbing a wall from its foot, and vines hanging from its top,
    // the flora's prefabs (Graybox.Flora.cs) laid along a wall's face in columns. Only on plain walls: a column whose wall
    // is climbable or marked for a move (a climbable panel, a wall-run or wall-slide wall, a ladder: by their fixture
    // names, as the game setup knows them, or the components it gives them) gets none, so the flora never says "climb
    // here" where a character can't, or hides where it can. Placed once the level's built (see Defer), so the walls'
    // colliders are there to check. No colliders of their own. Without the flora's prefabs, nothing.
    public static partial class Graybox
    {
        /// <summary>How far off a wall's face (m) its ivy and vines stand, so their leaves never cut into it.</summary>
        private const float WallFloraStandOff = 0.02f;

        /// <summary>How much of a column's panels overlap the one below (m): ivy reads as one growth up the wall.</summary>
        private const float IvyOverlap = 0.4f;

        /// <summary>
        /// Ivy on a wall's face, along it from a to b (points on the face, in parent's space), the face looking outward:
        /// in columns of the foliage's ivy panels, each column from y0 up to a height picked at random between halfway and
        /// y1, covering about cover of the face (the rest bare, in gaps a panel wide). Seeded from the settings' seed and
        /// salt. Only on plain walls (see the file's top).
        /// </summary>
        [Piece]
        public static void Ivy(Transform parent, Vector2 a, Vector2 b, Vector2 outward, float y0, float y1, int salt, float cover = 0.6f)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Ivy), parent, a, b, outward, y0, y1, salt, cover);
            Defer(() => AlongWall(parent, a, b, outward, Settings.foliage.seed + 300 + salt, cover, Settings.foliage.ivy, "Ivy",
                (group, pick, bounds, slot, random) =>
                {
                    float top = Mathf.Lerp(y0 + 0.5f * (y1 - y0), y1, (float)random.NextDouble());
                    float bottom = y0;
                    // Stacked from the foot up, each panel overlapping the one below; the last cut to the column's top by
                    // its scale (never past y1).
                    while (bottom < top - 0.3f)
                    {
                        FloraPick panel = bottom > y0 ? PickFlora(Settings.foliage.ivy, random) : pick;
                        if (panel.prefab == null) { return; }
                        Bounds pb = Flora(panel.prefab.name).bounds;
                        float scale = Mathf.Min(1f, (top - bottom) / Mathf.Max(pb.size.y, 0.1f));
                        if (scale < 0.4f) { return; }
                        if (!OnPlainWall(parent, slot, outward, bottom + 0.5f * scale * pb.size.y)) { return; }
                        OnWall(group, panel, pb, slot, outward, bottom, scale, alignTop: false);
                        bottom += scale * pb.size.y - IvyOverlap;
                    }
                }));
        }

        /// <summary>
        /// Vines hanging from a wall's top at top, along it from a to b (points on the face, in parent's space), the face
        /// looking outward: the foliage's vines, none hanging further than drop (shrunk to it), covering about cover of the
        /// face. Seeded from the settings' seed and salt. Only on plain walls (see the file's top).
        /// </summary>
        [Piece]
        public static void Vines(Transform parent, Vector2 a, Vector2 b, Vector2 outward, float top, float drop, int salt, float cover = 0.5f)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Vines), parent, a, b, outward, top, drop, salt, cover);
            Defer(() => AlongWall(parent, a, b, outward, Settings.foliage.seed + 400 + salt, cover, Settings.foliage.vines, "Vines",
                (group, pick, bounds, slot, random) =>
                {
                    float scale = Mathf.Min(1f, drop / Mathf.Max(bounds.size.y, 0.1f));
                    if (!OnPlainWall(parent, slot, outward, top - 0.3f)) { return; }
                    OnWall(group, pick, bounds, slot, outward, top, scale, alignTop: true);
                }));
        }

        /// <summary>Places one column of wall flora: its group, its first pick and that pick's bounds, where along the wall, and the random.</summary>
        private delegate void WallColumn(Transform group, FloraPick pick, Bounds bounds, Vector2 slot, System.Random random);

        /// <summary>
        /// Walks a wall's face from a to b in columns a picked plant wide, each placed by column (or skipped, about 1 - cover
        /// of them), under a group named name.
        /// </summary>
        private static void AlongWall(Transform parent, Vector2 a, Vector2 b, Vector2 outward, int seed, float cover, string[] kinds, string name, WallColumn column)
        {
            var random = new System.Random(seed);
            float length = Vector2.Distance(a, b);
            if (length < 0.1f || kinds == null || kinds.Length == 0) { return; }
            Vector2 along = (b - a) / length;
            Transform group = null;
            float at = 0f;
            while (at < length)
            {
                FloraPick pick = PickFlora(kinds, random);
                if (pick.prefab == null) { return; }
                Bounds bounds = Flora(pick.prefab.name).bounds;
                float width = Mathf.Max(bounds.size.x * pick.width, 0.3f);
                bool placed = random.NextDouble() < cover;
                if (placed && at + width <= length + 0.1f)
                {
                    group ??= Group(parent, name);
                    column(group, pick, bounds, a + along * (at + width / 2f), random);
                }
                at += placed ? width * 0.85f : width;
            }
        }

        /// <summary>
        /// Places a wall plant (pick, bounds its first LOD's in its own space) on a wall's face at slot (in the group's
        /// parent's space), its leaves outward, scale times its size: its foot at height (or, alignTop, its top), its back
        /// WallFloraStandOff off the face, centred on slot along it.
        /// </summary>
        private static void OnWall(Transform group, FloraPick pick, Bounds bounds, Vector2 slot, Vector2 outward, float height, float scale, bool alignTop)
        {
            // The prefabs' leaves stand off their wall on one side of it (the generator's wall is their z = 0): that side
            // turned to face outward, and their back (the other side's extent) put against the face.
            float side = bounds.center.z >= 0f ? 1f : -1f;
            Vector2 facing = outward.normalized * side;
            float turn = Mathf.Atan2(facing.x, facing.y) * Mathf.Rad2Deg;
            float back = side > 0f ? bounds.min.z : bounds.max.z;
            var anchor = new Vector3(bounds.center.x, alignTop ? bounds.max.y : bounds.min.y, back);
            GameObject plant = PlaceFlora(pick, group, Vector3.zero, turn, scale * bounds.size.y);
            Vector3 offset = plant.transform.localRotation * Vector3.Scale(plant.transform.localScale, anchor);
            Vector3 target = new Vector3(slot.x, height, slot.y) + new Vector3(outward.x, 0f, outward.y).normalized * WallFloraStandOff;
            plant.transform.localPosition = target - offset;
        }

        /// <summary>
        /// Whether there's a plain wall's face at slot (in parent's space) at height, looking outward: something solid
        /// within half a metre behind the point, neither climbable nor marked for a move (see the file's top).
        /// </summary>
        private static bool OnPlainWall(Transform parent, Vector2 slot, Vector2 outward, float height)
        {
            Vector3 dir = parent.TransformDirection(new Vector3(-outward.x, 0f, -outward.y).normalized);
            Vector3 from = parent.TransformPoint(new Vector3(slot.x, height, slot.y)) - dir * 0.3f;
            if (!Physics.Raycast(from, dir, out RaycastHit hit, 0.8f, ~0, QueryTriggerInteraction.Ignore)) { return false; }
            return !IsClimbableOrMarked(hit.collider.transform);
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
