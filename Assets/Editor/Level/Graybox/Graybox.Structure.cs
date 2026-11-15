using UnityEngine;

namespace CharacterShowcase.EditorTools.Level
{
    // Structure, after the style sheet's structural elements: I-beam columns, trusses, braced frames and brackets, in
    // steel. Each is one mesh. What can be bumped into has one box collider round the whole of it (the space a frame or
    // truss closes off, so nothing slips between its members); brackets, tucked under what they hold, have none.
    public static partial class Graybox
    {
        /// <summary>
        /// A steel I-beam standing as a column (a footprint of size across and along, turned yaw, from y0 to y1): its
        /// flanges across the ends of its footprint, its web between. Its collider is the plain box of its footprint, as
        /// a box column's was.
        /// </summary>
        [Piece]
        public static GameObject IBeamColumn(Transform parent, string name, Vector2 centre, Vector2 size, float y0, float y1, float yaw = 0f)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(IBeamColumn), parent, name, centre, size, y0, y1, yaw);
            var column = new GameObject(name);
            column.transform.SetParent(parent, false);
            column.transform.SetLocalPositionAndRotation(new Vector3(centre.x, y0, centre.y), Quaternion.Euler(0f, yaw, 0f));
            float hx = size.x / 2f, hz = size.y / 2f, h = y1 - y0;
            float flange = Settings.structure.iBeamFlangeAndWeb.x * size.y, web = Settings.structure.iBeamFlangeAndWeb.y * size.x;
            var mesh = new GrayboxMesh();
            mesh.Box(Surface.Steel, new Vector3(-hx, 0f, -hz), new Vector3(hx, h, -hz + flange), bottom: false);
            mesh.Box(Surface.Steel, new Vector3(-hx, 0f, hz - flange), new Vector3(hx, h, hz), bottom: false);
            // The web's ends meet the flanges.
            mesh.Box(Surface.Steel, new Vector3(-web / 2f, 0f, -hz + flange), new Vector3(web / 2f, h, hz - flange), bottom: false, ends: false);
            mesh.AddTo(column, name);
            var collider = column.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, h / 2f, 0f);
            collider.size = new Vector3(size.x, h, size.y);
            MakeStatic(column, occluder: false);
            return column;
        }

        /// <summary>
        /// A steel truss from a to b (on the ground plane), from y0 up to y1 (the settings' thickness): top and bottom chords,
        /// posts dividing it into roughly square bays, and a diagonal across each, leaning alternately (a Warren truss).
        /// Its top is left open, for a truss under a deck. One box collider round the whole of it.
        /// </summary>
        [Piece]
        public static GameObject Truss(Transform parent, string name, Vector2 a, Vector2 b, float y0, float y1)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Truss), parent, name, a, b, y0, y1);
            float thickness = Settings.structure.trussThickness, chord = Settings.structure.trussChord;
            float length = Vector2.Distance(a, b), half = length / 2f, t = thickness / 2f;
            Vector2 dir = (b - a) / length, middle = (a + b) / 2f;
            var truss = new GameObject(name);
            truss.transform.SetParent(parent, false);
            truss.transform.SetLocalPositionAndRotation(new Vector3(middle.x, 0f, middle.y), Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 0f));

            var mesh = new GrayboxMesh();
            mesh.Box(Surface.Steel, new Vector3(-t, y1 - chord, -half), new Vector3(t, y1, half), top: false);
            mesh.Box(Surface.Steel, new Vector3(-t, y0, -half), new Vector3(t, y0 + chord, half));
            int bays = Mathf.Max(1, Mathf.RoundToInt(length / (y1 - y0)));
            float Z(int i) => Mathf.Lerp(-half + chord / 2f, half - chord / 2f, (float)i / bays);
            for (int i = 0; i <= bays; i++)
            {
                // Posts a little narrower than the chords, so their sides don't share a plane.
                mesh.Box(Surface.Steel, new Vector3(-0.8f * t, y0 + chord, Z(i) - chord / 2f), new Vector3(0.8f * t, y1 - chord, Z(i) + chord / 2f),
                    top: false, bottom: false);
            }
            for (int i = 0; i < bays; i++)
            {
                bool rising = i % 2 == 0;
                var low = new Vector3(0f, y0 + chord, rising ? Z(i) : Z(i + 1));
                var high = new Vector3(0f, y1 - chord, rising ? Z(i + 1) : Z(i));
                mesh.Beam(Surface.Steel, low, high, Vector3.right, 1.4f * t, 0.7f * chord);
            }
            mesh.AddTo(truss, name);
            var collider = truss.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, (y0 + y1) / 2f, 0f);
            collider.size = new Vector3(thickness, y1 - y0, length);
            MakeStatic(truss, occluder: false);
            return truss;
        }

        /// <summary>
        /// A steel braced frame standing between a and b (on the ground plane), from y0 up to y1: a post at each end, a
        /// beam across their tops (its top left open, for a frame under a deck), and cross bracing between them. One box
        /// collider round the whole of it, the slice of space it closes off.
        /// </summary>
        [Piece]
        public static GameObject BracedFrame(Transform parent, string name, Vector2 a, Vector2 b, float y0, float y1)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(BracedFrame), parent, name, a, b, y0, y1);
            float post = Settings.structure.framePost, brace = Settings.structure.frameBrace;
            float length = Vector2.Distance(a, b), half = length / 2f, p = post / 2f;
            Vector2 dir = (b - a) / length, middle = (a + b) / 2f;
            var frame = new GameObject(name);
            frame.transform.SetParent(parent, false);
            frame.transform.SetLocalPositionAndRotation(new Vector3(middle.x, 0f, middle.y), Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 0f));

            var mesh = new GrayboxMesh();
            mesh.Box(Surface.Steel, new Vector3(-p, y0, -half), new Vector3(p, y1 - post, -half + post), top: false, bottom: false);
            mesh.Box(Surface.Steel, new Vector3(-p, y0, half - post), new Vector3(p, y1 - post, half), top: false, bottom: false);
            mesh.Box(Surface.Steel, new Vector3(-p, y1 - post, -half), new Vector3(p, y1, half), top: false);
            float inner = half - post;
            mesh.Beam(Surface.Steel, new Vector3(0f, y0, -inner), new Vector3(0f, y1 - post, inner), Vector3.right, 1.2f * p, brace);
            mesh.Beam(Surface.Steel, new Vector3(0f, y0, inner), new Vector3(0f, y1 - post, -inner), Vector3.right, 1.2f * p, brace);
            mesh.AddTo(frame, name);
            var collider = frame.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, (y0 + y1) / 2f, 0f);
            collider.size = new Vector3(post, y1 - y0, length);
            MakeStatic(frame, occluder: false);
            return frame;
        }

        /// <summary>
        /// A steel knee bracket under something held out from a wall: a plate on the wall face at wallPoint, and a strut
        /// from low on it out to the underside, the settings' bracket size along outward. To see only: no collider.
        /// </summary>
        [Piece]
        public static void Bracket(Transform parent, Vector2 wallPoint, Vector2 outward, float underside)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Bracket), parent, wallPoint, outward, underside);
            float size = Settings.structure.bracketSize;
            var bracket = new GameObject("Bracket");
            bracket.transform.SetParent(parent, false);
            bracket.transform.SetLocalPositionAndRotation(new Vector3(wallPoint.x, 0f, wallPoint.y),
                Quaternion.Euler(0f, Mathf.Atan2(outward.x, outward.y) * Mathf.Rad2Deg, 0f));
            var mesh = new GrayboxMesh();
            mesh.Box(Surface.Steel, new Vector3(-0.15f, underside - size, 0f), new Vector3(0.15f, underside, 0.04f), top: false);
            mesh.Beam(Surface.Steel, new Vector3(0f, underside - size + 0.08f, 0.04f), new Vector3(0f, underside, size), Vector3.right, 0.12f, 0.1f);
            mesh.AddTo(bracket, "Bracket");
            MakeStatic(bracket, occluder: false);
        }
    }
}
