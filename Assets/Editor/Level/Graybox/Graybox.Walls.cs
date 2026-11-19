using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.Showcase.Levels
{
    // Walls with openings, and guardrails.
    public static partial class Graybox
    {
        /// <summary>
        /// A wall of panelled concrete along the line from a to b (centred on it), from y0 to y1, with openings (centre
        /// on the line, width, and the opening's bottom and top: the wall's filled in below and above each), pilasters
        /// along it and frames round its openings (see WallDetail).
        /// </summary>
        [Piece]
        public static void Wall(Transform parent, string name, Vector2 a, Vector2 b, float y0, float y1, float thickness,
            params (Vector2 centre, float width, float bottom, float top)[] openings)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Wall), parent, name, a, b, y0, y1, thickness, openings);
            float length = Vector2.Distance(a, b);
            if (length < 0.05f) { return; }
            Vector2 dir = (b - a) / length;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            void Piece(string piece, float s, float e, float bottom, float top)
            {
                if (e - s < 0.05f || top - bottom < 0.05f) { return; }
                BoxXZ(parent, piece, a + dir * ((s + e) / 2f), new Vector2(thickness, e - s), bottom, top, yaw, GrayboxMaterial.Panel);
            }
            var cuts = new List<(float from, float to, float bottom, float top)>();
            foreach ((Vector2 centre, float width, float bottom, float top) in openings)
            {
                float t = Vector2.Dot(centre - a, dir);
                cuts.Add((Mathf.Clamp(t - width / 2f, 0f, length), Mathf.Clamp(t + width / 2f, 0f, length), bottom, top));
            }
            cuts.Sort((x, y) => x.from.CompareTo(y.from));
            float from = 0f;
            foreach ((float cutFrom, float cutTo, float bottom, float top) in cuts)
            {
                Piece(name, from, cutFrom, y0, y1);
                Piece(name + " (Below Opening)", cutFrom, cutTo, y0, bottom);
                Piece(name + " (Above Opening)", cutFrom, cutTo, top, y1);
                from = Mathf.Max(from, cutTo);
            }
            Piece(name, from, length, y0, y1);
            WallDetail(parent, name, a, dir, length, y0, y1, thickness, cuts);
        }

        /// <summary>Guardrails along every edge of a shape at the given height, leaving gaps (centre, width) where routes go through.</summary>
        [Piece]
        public static void RailAround(Transform rails, Vector2[] shape, float height, params (Vector2 centre, float width)[] gaps)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(RailAround), rails, shape, height, gaps);
            for (int i = 0; i < shape.Length; i++) { Rail(rails, shape[i], shape[(i + 1) % shape.Length], height, gaps); }
        }

        /// <summary>
        /// A guardrail from a to b on a surface at the given height, leaving gaps (centre, width) that lie along it: one
        /// object for each run between gaps (see GuardrailRun).
        /// </summary>
        [Piece]
        public static void Rail(Transform rails, Vector2 a, Vector2 b, float height, params (Vector2 centre, float width)[] gaps)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Rail), rails, a, b, height, gaps);
            float length = Vector2.Distance(a, b);
            if (length < 0.05f) { return; }
            Vector2 dir = (b - a) / length;
            var cuts = new List<(float from, float to)>();
            foreach ((Vector2 centre, float width) in gaps)
            {
                float t = Vector2.Dot(centre - a, dir);
                Vector2 off = centre - (a + dir * t);
                if (off.magnitude > 0.1f || t < -width / 2f || t > length + width / 2f) { continue; }
                cuts.Add((t - width / 2f, t + width / 2f));
            }
            cuts.Sort((x, y) => x.from.CompareTo(y.from));
            float from = 0f;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            void Piece(float s, float e)
            {
                if (e - s < 0.2f) { return; }
                GuardrailRun(rails, a + dir * ((s + e) / 2f), e - s, height, yaw);
            }
            foreach ((float cutFrom, float cutTo) in cuts)
            {
                Piece(from, Mathf.Clamp(cutFrom, 0f, length));
                from = Mathf.Max(from, Mathf.Clamp(cutTo, 0f, length));
            }
            Piece(from, length);
        }

        /// <summary>
        /// One straight run of guardrail, centred on centre along yaw, on a surface at the given height: graphite posts at
        /// most the settings' post spacing apart under an orange top rail, a graphite mid rail, and an orange fascia band along the edge
        /// it stands on, just below the surface and standing a little proud of the face under it. One mesh, and one box
        /// collider the full height and length (RailThickness thick), so nothing can slip between the rails: the
        /// collision is that of the solid guardrails the level had before.
        /// </summary>
        private static void GuardrailRun(Transform parent, Vector2 centre, float length, float height, float yaw)
        {
            var rail = new GameObject("Guardrail");
            rail.transform.SetParent(parent, false);
            rail.transform.SetLocalPositionAndRotation(new Vector3(centre.x, height, centre.y), Quaternion.Euler(0f, yaw, 0f));

            // Laid out along +z, centred; the top rail sits on the posts. The mid rail stops inside the end posts and is
            // narrower than them (as they are than the top rail), so no two faces share a plane but where they meet face
            // to face.
            LevelBuilderSettings.RailSettings r = Settings.rails;
            float post = r.postSize / 2f, topRail = r.topRailSize.x / 2f, cap = r.topRailSize.y, midRail = r.midRailSize.x / 2f;
            float half = length / 2f, top = RailHeight, mid = top * r.midRailHeight, fascia = RailThickness / 2f + r.fasciaProud;
            var mesh = new GrayboxMesh();
            mesh.Box(GrayboxMaterial.Accent, new Vector3(-topRail, top - cap, -half), new Vector3(topRail, top, half));
            mesh.Box(GrayboxMaterial.Graphite, new Vector3(-midRail, mid, -half + post), new Vector3(midRail, mid + r.midRailSize.y, half - post));
            int bays = Mathf.Max(1, Mathf.CeilToInt(length / r.postSpacing - 1e-3f));
            for (int i = 0; i <= bays; i++)
            {
                float z = Mathf.Lerp(-half + post, half - post, (float)i / bays);
                // No top (under the top rail) or bottom (on the surface).
                mesh.Box(GrayboxMaterial.Graphite, new Vector3(-post, 0f, z - post), new Vector3(post, top - cap, z + post), top: false, bottom: false);
            }
            mesh.Box(GrayboxMaterial.Accent, new Vector3(-fascia, -r.fasciaDrop.y, -half), new Vector3(fascia, -r.fasciaDrop.x, half));
            mesh.AddTo(rail, "Guardrail");

            var collider = rail.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, top / 2f, 0f);
            collider.size = new Vector3(RailThickness, top, length);
            MakeStatic(rail, occluder: false);
        }
    }
}
