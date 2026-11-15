using TMPro;
using UnityEngine;

namespace CharacterShowcase.EditorTools.Level
{
    // Markings and wayfinding, after the style sheet's surface markings and signage: hazard stripes along edges to jump
    // from, corner brackets and painted values round test lanes, signs naming where each bridge goes, and each area's
    // number and name at its entrance. Markings are raised a centimetre (thick paint, so they never flicker against the
    // surface) and, like the lettering, are to see only. Sign panels and their posts have box colliders.
    public static partial class Graybox
    {
        /// <summary>
        /// Hazard stripes along an edge from a to b on a surface at height, reaching inward (a direction across the edge,
        /// onto the surface) the settings' width: diagonal bands, orange and graphite in turn, cut square at the ends.
        /// One object, with no collider.
        /// </summary>
        [Piece]
        public static void HazardStripes(Transform parent, Vector2 a, Vector2 b, Vector2 inward, float height)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(HazardStripes), parent, a, b, inward, height);
            GrayboxSettings.MarkingSettings m = Settings.markings;
            float length = Vector2.Distance(a, b), width = m.hazardWidth, stripe = m.stripeWidth;
            if (length < 0.1f) { return; }
            Vector2 along = (b - a) / length;
            inward = (inward - Vector2.Dot(inward, along) * along).normalized;
            var mesh = new GrayboxMesh();
            // In the strip's own coordinates (x along the edge, y in from it), each band is where c0 ≤ x - y ≤ c1.
            Vector2[] strip = Rect(0f, 0f, length, width);
            int band = 0;
            for (float c0 = -width; c0 < length; c0 += stripe, band++)
            {
                Vector2[] shape = Clip(Clip(strip, 1f, -1f, c0 + stripe), -1f, 1f, -c0);
                if (shape.Length < 3) { continue; }
                for (int i = 0; i < shape.Length; i++) { shape[i] = a + along * shape[i].x + inward * shape[i].y; }
                mesh.Prism(band % 2 == 0 ? Surface.Accent : Surface.Graphite, shape, height, height + m.thickness);
            }
            var stripes = new GameObject("Hazard Stripes");
            stripes.transform.SetParent(parent, false);
            mesh.AddTo(stripes, stripes.name);
            MakeStatic(stripes, occluder: false);
        }

        /// <summary>
        /// Orange corner brackets on the floor at height round an axis-aligned footprint (x from x0 to x1, z from z0 to
        /// z1), the settings' gap out from it: the style sheet's "[ ]" marking a test lane. One object, with no collider.
        /// </summary>
        [Piece]
        public static void CornerBrackets(Transform parent, float x0, float x1, float z0, float z1, float height)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(CornerBrackets), parent, x0, x1, z0, z1, height);
            GrayboxSettings.MarkingSettings m = Settings.markings;
            float arm = m.cornerBracket.x, w = m.cornerBracket.y, g = m.cornerGap, y1 = height + m.thickness;
            var mesh = new GrayboxMesh();
            foreach ((float x, float sx) in new[] { (x0 - g, 1f), (x1 + g, -1f) })
            {
                foreach ((float z, float sz) in new[] { (z0 - g, 1f), (z1 + g, -1f) })
                {
                    // One arm along x the full length, the other along z from beyond the first's width, so they don't overlap.
                    Vector3 p = new(x, height, z), q = new(x + sx * arm, y1, z + sz * w);
                    mesh.Box(Surface.Accent, Vector3.Min(p, q), Vector3.Max(p, q), bottom: false);
                    p = new Vector3(x, height, z + sz * w);
                    q = new Vector3(x + sx * w, y1, z + sz * arm);
                    mesh.Box(Surface.Accent, Vector3.Min(p, q), Vector3.Max(p, q), bottom: false);
                }
            }
            var brackets = new GameObject("Corner Brackets");
            brackets.transform.SetParent(parent, false);
            mesh.AddTo(brackets, brackets.name);
            MakeStatic(brackets, occluder: false);
        }

        /// <summary>
        /// Lettering painted on the floor at height (centred at "at"), reading the right way up for someone standing on
        /// the side away from "facing" and looking along it (yaw: facing north reads from the south).
        /// </summary>
        [Piece]
        public static TextMeshPro FloorLabel(Transform parent, string text, Vector2 at, float height, Vector2 facing)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(FloorLabel), parent, text, at, height, facing);
            float yaw = Mathf.Atan2(facing.x, facing.y) * Mathf.Rad2Deg;
            float lift = Settings.markings.thickness;
            return Text(parent, $"Label ({text})", text, new Vector3(at.x, height + lift, at.y), Quaternion.Euler(90f, yaw, 0f),
                Settings.markings.labelHeight, Settings.markings.labelHeight * text.Length, Settings.markings.labelColour);
        }

        /// <summary>
        /// A line of lettering: a TextMeshPro object at position, turned to rotation (it reads from the side its forward
        /// points away from), its capitals about height tall (smaller if it must be to fit), centred in a box width wide.
        /// To see only.
        /// </summary>
        private static TextMeshPro Text(Transform parent, string name, string text, Vector3 position, Quaternion rotation, float height,
            float width, Color colour, FontStyles style = FontStyles.Bold)
        {
            var label = new GameObject(name);
            label.transform.SetParent(parent, false);
            var tmp = label.AddComponent<TextMeshPro>();
            tmp.rectTransform.SetLocalPositionAndRotation(position, rotation);
            tmp.rectTransform.sizeDelta = new Vector2(Mathf.Max(width, height), height / 0.7f);
            tmp.text = text;
            // A TextMeshPro's font size is ten to a unit, for a line about that tall: capitals are about 0.7 of it. Text
            // too long for its box is shrunk to fit it (a long area name on a sign).
            tmp.fontSize = height / 0.7f * 10f;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMax = tmp.fontSize;
            tmp.fontSizeMin = tmp.fontSize * 0.25f;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.color = colour;
            tmp.ForceMeshUpdate();
            return tmp;
        }

        /// <summary>
        /// The gantry over the start of a bridge (from start along direction, width wide, its deck at height): a post up
        /// from each guardrail, a beam across their tops, and a sign panel hung from it naming where the bridge goes,
        /// facing whoever comes onto the bridge, with an orange band and chevrons pointing on along it. The posts and
        /// panel have box colliders.
        /// </summary>
        private static void BridgeGantry(Transform parent, string destination, Vector2 start, Vector2 direction, float width, float height)
        {
            GrayboxSettings.SignSettings s = Settings.signs;
            Vector2 at = start + direction * 2f;
            float yaw = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            var gantry = new GameObject($"Sign Gantry ({destination})");
            gantry.transform.SetParent(parent, false);
            gantry.transform.SetLocalPositionAndRotation(new Vector3(at.x, height, at.y), Quaternion.Euler(0f, yaw, 0f));

            // Laid out with +z along the bridge, x across it; the panel's face is toward -z, where people come from.
            float half = width / 2f, p = s.gantryPost / 2f, top = s.gantryHeight;
            float panelWidth = Mathf.Min(s.gantryPanel.y, width - 1f), panelTop = top - s.gantryPost - 0.1f, panelBottom = panelTop - s.gantryPanel.x;
            const float depth = 0.12f, band = 0.08f;
            var mesh = new GrayboxMesh();
            foreach (float x in new[] { -half, half })
            {
                mesh.Box(Surface.Graphite, new Vector3(x - p, 0f, -p), new Vector3(x + p, top - s.gantryPost, p), bottom: false);
            }
            mesh.Box(Surface.Graphite, new Vector3(-half - p, top - s.gantryPost, -p), new Vector3(half + p, top, p));
            // Hangers from the beam to the panel.
            foreach (float x in new[] { -panelWidth / 3f, panelWidth / 3f })
            {
                mesh.Box(Surface.Steel, new Vector3(x - 0.02f, panelTop, -0.02f), new Vector3(x + 0.02f, top - s.gantryPost, 0.02f), top: false, bottom: false);
            }
            mesh.Box(Surface.Graphite, new Vector3(-panelWidth / 2f, panelBottom, -depth / 2f), new Vector3(panelWidth / 2f, panelTop, depth / 2f));
            mesh.Box(Surface.Accent, new Vector3(-panelWidth / 2f, panelBottom, -depth / 2f - 0.01f), new Vector3(panelWidth / 2f, panelBottom + band, -depth / 2f));
            // Chevrons at the panel's right, pointing on along the bridge (upward on the sign).
            float cx = panelWidth / 2f - 0.45f, cy = (panelBottom + band + panelTop) / 2f, z = -depth / 2f - 0.01f;
            for (int i = 0; i < 2; i++)
            {
                float y = cy - 0.12f + i * 0.2f;
                var tip = new Vector3(cx, y + 0.12f, z);
                mesh.Beam(Surface.Accent, new Vector3(cx - 0.2f, y - 0.05f, z), tip, Vector3.forward, 0.02f, 0.07f, ends: true);
                mesh.Beam(Surface.Accent, new Vector3(cx + 0.2f, y - 0.05f, z), tip, Vector3.forward, 0.02f, 0.07f, ends: true);
            }
            mesh.AddTo(gantry, gantry.name);
            foreach (float x in new[] { -half, half }) { AddBox(gantry, new Vector3(x, (top - s.gantryPost) / 2f, 0f), new Vector3(s.gantryPost, top - s.gantryPost, s.gantryPost)); }
            AddBox(gantry, new Vector3(0f, (panelBottom + top) / 2f, 0f), new Vector3(width + s.gantryPost, top - panelBottom, depth));
            MakeStatic(gantry, occluder: false);

            Text(gantry.transform, "Sign Text", destination.ToUpperInvariant(), new Vector3(-0.45f, cy, z - 0.005f), Quaternion.identity,
                s.gantryTextHeight, panelWidth - 1.2f, s.textColour);
        }

        /// <summary>
        /// An area's sign: its number and name in large lettering on a graphite panel, beside the style sheet's three
        /// orange slashes. On a wall (its back on the wall face at "at", the panel's bottom at bottom), or, free-standing,
        /// on two legs from bottom - the settings' lift. Facing along facing (it reads from the side facing points to).
        /// The panel (and legs) have box colliders.
        /// </summary>
        [Piece]
        public static void AreaSign(Transform parent, Vector2 at, float bottom, Vector2 facing, string number, string name, bool freeStanding = false)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(AreaSign), parent, at, bottom, facing, number, name, freeStanding);
            GrayboxSettings.SignSettings s = Settings.signs;
            float yaw = Mathf.Atan2(-facing.x, -facing.y) * Mathf.Rad2Deg;
            var sign = new GameObject($"Area Sign ({number} {name})");
            sign.transform.SetParent(parent, false);
            sign.transform.SetLocalPositionAndRotation(new Vector3(at.x, bottom, at.y), Quaternion.Euler(0f, yaw, 0f));

            // Laid out with its face toward -z (the reader), x across it, from y 0 up.
            Vector2 size = s.areaSignSize;
            float w = size.x / 2f, h = size.y, depth = freeStanding ? 0.15f : 0.08f, back = freeStanding ? depth / 2f : 0f, face = back - depth;
            var mesh = new GrayboxMesh();
            mesh.Box(Surface.Graphite, new Vector3(-w, 0f, face), new Vector3(w, h, back));
            mesh.Box(Surface.Accent, new Vector3(-w, 0f, face - 0.01f), new Vector3(w, 0.1f, face));
            // Three slashes at the left.
            for (int i = 0; i < 3; i++)
            {
                float x = -w + 0.35f + i * 0.28f;
                mesh.Beam(Surface.Accent, new Vector3(x, 0.3f, face - 0.01f), new Vector3(x + 0.3f, h - 0.25f, face - 0.01f), Vector3.right, 0.12f, 0.02f, ends: true);
            }
            if (freeStanding)
            {
                float lift = s.areaSignLift;
                foreach (float x in new[] { -w + 0.3f, w - 0.3f })
                {
                    mesh.Box(Surface.Graphite, new Vector3(x - 0.06f, -lift, -0.06f), new Vector3(x + 0.06f, 0f, 0.06f), top: false, bottom: false);
                    AddBox(sign, new Vector3(x, -lift / 2f, 0f), new Vector3(0.12f, lift, 0.12f));
                }
            }
            mesh.AddTo(sign, sign.name);
            AddBox(sign, new Vector3(0f, h / 2f, (face + back) / 2f), new Vector3(size.x, h, depth));
            MakeStatic(sign, occluder: false);

            float textLeft = -w + 1.4f, textWidth = size.x - 1.6f;
            Text(sign.transform, "Area Number", number, new Vector3(textLeft + textWidth / 2f, h * 0.62f, face - 0.015f), Quaternion.identity,
                s.areaSignTextHeights.x, textWidth, s.textColour);
            Text(sign.transform, "Area Name", name.ToUpperInvariant(), new Vector3(textLeft + textWidth / 2f, h * 0.24f, face - 0.015f), Quaternion.identity,
                s.areaSignTextHeights.y, textWidth, s.textColour);
        }

        /// <summary>A box collider on obj, centred at centre (its local space), of size.</summary>
        private static void AddBox(GameObject obj, Vector3 centre, Vector3 size)
        {
            var box = obj.AddComponent<BoxCollider>();
            box.center = centre;
            box.size = size;
        }

        /// <summary>Where a connection marker's route goes: "To Fabrication Hall (E, +8)" is the Fabrication Hall; null if it doesn't say.</summary>
        private static string Destination(string connection)
        {
            if (!connection.StartsWith("To ")) { return null; }
            string place = connection.Substring(3);
            int bracket = place.IndexOf(" (", System.StringComparison.Ordinal);
            return bracket > 0 ? place.Substring(0, bracket) : place;
        }
    }
}
