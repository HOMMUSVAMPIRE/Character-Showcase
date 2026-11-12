using System.Collections.Generic;
using UnityEngine;

namespace CharacterShowcase.EditorTools.Level
{
    // Lighting, after the style sheet's lighting elements: ceiling panels and strip lights underground (with the lights
    // that light it), and in daylight door lights, bollards, floodlights and cyan accent posts, which glow (the Light
    // and Cyan surfaces) but cast no light unless the settings say so (Lights: fixtures cast light), since in the midday
    // sun they'd add real-time lights for little. Fixtures are to see only, but for bollards and floodlight masts,
    // which stand where they can be walked into and have a box collider.
    public static partial class Graybox
    {
        /// <summary>
        /// The light an enclosed network of cells (see Cells) needs to be found your way round. In each closed cell,
        /// plain point lights along its length under its ceiling (no shadows), each at a ceiling panel in a room, or
        /// along a strip light down the middle of a corridor (a cell narrower than the settings' corridor width); and a
        /// baked reflection probe, so it reflects itself instead of the sky above. Open cells (stairwells and shafts)
        /// are lit from above.
        /// </summary>
        public static void CellLighting(Transform lighting, IEnumerable<Cell> cells)
        {
            GrayboxSettings.LightSettings lights = Settings.lights;
            GrayboxSettings.FixtureSettings fixtures = Settings.fixtures;
            foreach (Cell cell in cells)
            {
                if (cell.Open) { continue; }
                UnityEngine.Rect a = cell.Area;
                float height = cell.Ceiling - cell.Floor;
                bool alongX = a.width >= a.height;
                float length = alongX ? a.width : a.height, across = alongX ? a.height : a.width;
                bool corridor = across < fixtures.corridorWidth;
                int count = Mathf.Max(1, Mathf.RoundToInt(length / lights.spacing));
                var mesh = new GrayboxMesh();
                // A box under the ceiling (centred at "at", sized along and across the cell), from drop below it up to it.
                void UnderCeiling(Surface surface, Vector2 at, float along, float width, float dropTop, float dropBottom)
                {
                    Vector2 half = alongX ? new Vector2(along, width) / 2f : new Vector2(width, along) / 2f;
                    mesh.Box(surface, new Vector3(at.x - half.x, cell.Ceiling - dropBottom, at.y - half.y),
                        new Vector3(at.x + half.x, cell.Ceiling - dropTop, at.y + half.y), top: false);
                }
                if (corridor)
                {
                    UnderCeiling(Surface.Light, a.center, length - 1f, fixtures.stripLightSize.x, 0f, fixtures.stripLightSize.y);
                }
                for (int i = 0; i < count; i++)
                {
                    float f = (i + 0.5f) / count;
                    Vector2 at = alongX ? new Vector2(Mathf.Lerp(a.xMin, a.xMax, f), a.center.y) : new Vector2(a.center.x, Mathf.Lerp(a.yMin, a.yMax, f));
                    if (!corridor)
                    {
                        // A graphite housing, its glowing face hanging just below it.
                        Vector2 panel = fixtures.ceilingPanelSize;
                        float housing = fixtures.ceilingPanelDrop * 0.6f;
                        UnderCeiling(Surface.Graphite, at, panel.y + 0.1f, panel.x + 0.1f, 0f, housing);
                        UnderCeiling(Surface.Light, at, panel.y, panel.x, housing, fixtures.ceilingPanelDrop);
                    }
                    var lamp = new GameObject($"Light ({cell.Name})");
                    lamp.transform.SetParent(lighting, false);
                    lamp.transform.localPosition = new Vector3(at.x, cell.Ceiling - 0.5f, at.y);
                    var light = lamp.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = lights.colour;
                    light.intensity = lights.intensity;
                    light.range = Mathf.Max(lights.spacing, across) * lights.rangeFactor;
                    light.shadows = LightShadows.None;
                    light.lightmapBakeType = LightmapBakeType.Realtime;
                }
                var fittings = new GameObject($"Light Fixtures ({cell.Name})");
                fittings.transform.SetParent(lighting, false);
                mesh.AddTo(fittings, fittings.name);
                MakeStatic(fittings, occluder: false);

                var probeObject = new GameObject($"Reflection Probe ({cell.Name})");
                probeObject.transform.SetParent(lighting, false);
                probeObject.transform.localPosition = new Vector3(a.center.x, cell.Floor + height / 2f, a.center.y);
                var probe = probeObject.AddComponent<ReflectionProbe>();
                probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
                probe.size = new Vector3(a.width + 1f, height + 1f, a.height + 1f);
                probe.boxProjection = true;
                probe.resolution = 64;
                probe.importance = 2;
            }
        }

        /// <summary>
        /// A door light, in a wall's detail mesh (its local x across the wall, z along it): a graphite housing on each
        /// face of the wall, centred along it at along and from bottom up, with a glowing face.
        /// </summary>
        private static void DoorLight(GrayboxMesh mesh, float face, float along, float bottom, Vector2 size)
        {
            const float housing = 0.08f, glow = 0.02f, rim = 0.02f;
            float s = along - size.x / 2f, e = along + size.x / 2f, top = bottom + size.y;
            foreach (float side in new[] { 1f, -1f })
            {
                float x0 = side * face, x1 = side * (face + housing), x2 = side * (face + housing + glow);
                mesh.Box(Surface.Graphite, new Vector3(Mathf.Min(x0, x1), bottom, s), new Vector3(Mathf.Max(x0, x1), top, e));
                mesh.Box(Surface.Light, new Vector3(Mathf.Min(x1, x2), bottom + rim, s + rim), new Vector3(Mathf.Max(x1, x2), top - rim, e - rim));
            }
        }

        /// <summary>A light for a fixture in daylight, if the settings say fixtures cast light: the settings' intensity and range, no shadows.</summary>
        private static void FixtureLight(Transform parent, Vector2 at, float y, LightType type, Vector3 rotation)
        {
            if (!Settings.lights.fixturesCastLight) { return; }
            var lamp = new GameObject("Fixture Light");
            lamp.transform.SetParent(parent, false);
            lamp.transform.SetLocalPositionAndRotation(new Vector3(at.x, y, at.y), Quaternion.Euler(rotation));
            var light = lamp.AddComponent<Light>();
            light.type = type;
            light.color = Settings.lights.colour;
            light.intensity = Settings.lights.fixtureLight.x;
            light.range = Settings.lights.fixtureLight.y;
            light.spotAngle = Settings.lights.floodlightAngle;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;
        }

        /// <summary>
        /// Bollards along each edge of a shape on the ground at floor, at most the settings' bollard spacing apart and
        /// spread evenly along it, leaving gaps (centre, width) clear where routes go through.
        /// </summary>
        public static void Bollards(Transform parent, Vector2[] shape, float floor, params (Vector2 centre, float width)[] gaps)
        {
            Transform group = Group(parent, "Bollards");
            for (int i = 0; i < shape.Length; i++)
            {
                Vector2 p = shape[i], q = shape[(i + 1) % shape.Length];
                float length = Vector2.Distance(p, q);
                int count = Mathf.Max(1, Mathf.FloorToInt(length / Settings.fixtures.bollardSpacing));
                for (int k = 0; k < count; k++)
                {
                    Vector2 at = Vector2.Lerp(p, q, (k + 0.5f) / count);
                    bool clear = true;
                    foreach ((Vector2 centre, float width) in gaps) { if (Vector2.Distance(at, centre) < width / 2f + 0.5f) { clear = false; } }
                    if (clear) { Bollard(group, at, floor); }
                }
            }
        }

        /// <summary>A bollard standing at floor: an octagonal graphite post with a glowing band near its top, and a box collider.</summary>
        public static GameObject Bollard(Transform parent, Vector2 at, float floor)
        {
            var bollard = new GameObject("Bollard");
            bollard.transform.SetParent(parent, false);
            bollard.transform.localPosition = new Vector3(at.x, floor, at.y);
            Vector2 size = Settings.fixtures.bollardSize;
            float h = size.x;
            Vector2[] shape = Octagon(size.y / 2f);
            var mesh = new GrayboxMesh();
            mesh.Prism(Surface.Graphite, shape, 0f, 0.72f * h, top: false);
            mesh.Prism(Surface.Light, shape, 0.72f * h, 0.86f * h, top: false);
            mesh.Prism(Surface.Graphite, shape, 0.86f * h, h);
            mesh.AddTo(bollard, "Bollard");
            var collider = bollard.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, h / 2f, 0f);
            collider.size = new Vector3(size.y, h, size.y);
            FixtureLight(parent, at, floor + 0.8f * h, LightType.Point, Vector3.zero);
            MakeStatic(bollard, occluder: false);
            return bollard;
        }

        /// <summary>
        /// A floodlight on a mast standing at floor, its head facing along facing: a graphite mast (with a box collider)
        /// and head, the head's glowing face to the front.
        /// </summary>
        public static GameObject Floodlight(Transform parent, Vector2 at, float floor, Vector2 facing)
        {
            float yaw = Mathf.Atan2(facing.x, facing.y) * Mathf.Rad2Deg;
            var flood = new GameObject("Floodlight");
            flood.transform.SetParent(parent, false);
            flood.transform.SetLocalPositionAndRotation(new Vector3(at.x, floor, at.y), Quaternion.Euler(0f, yaw, 0f));
            float mast = Settings.fixtures.floodlightMastHeight, m = 0.08f;
            Vector2 head = Settings.fixtures.floodlightHeadSize;
            const float depth = 0.25f, glow = 0.02f, rim = 0.04f;
            var mesh = new GrayboxMesh();
            mesh.Box(Surface.Graphite, new Vector3(-m, 0f, -m), new Vector3(m, mast, m), bottom: false);
            float y0 = mast - head.y / 2f, y1 = mast + head.y / 2f;
            mesh.Box(Surface.Graphite, new Vector3(-head.x / 2f, y0, m), new Vector3(head.x / 2f, y1, m + depth));
            mesh.Box(Surface.Light, new Vector3(-head.x / 2f + rim, y0 + rim, m + depth), new Vector3(head.x / 2f - rim, y1 - rim, m + depth + glow));
            mesh.AddTo(flood, "Floodlight");
            var collider = flood.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, mast / 2f, 0f);
            collider.size = new Vector3(2f * m, mast, 2f * m);
            FixtureLight(flood.transform, Vector2.up * (m + depth + 0.1f), mast, LightType.Spot, new Vector3(35f, 0f, 0f));
            MakeStatic(flood, occluder: false);
            return flood;
        }

        /// <summary>
        /// A cyan accent post standing at the given height (on a guardrail's line), rising the settings' height above
        /// the rail: the style sheet's accent light. To see only: no collider (the guardrail's covers it).
        /// </summary>
        public static void AccentPost(Transform parent, Vector2 at, float height)
        {
            var post = new GameObject("Accent Post (Cyan)");
            post.transform.SetParent(parent, false);
            post.transform.localPosition = new Vector3(at.x, height, at.y);
            float w = Settings.fixtures.accentPost.x / 2f;
            var mesh = new GrayboxMesh();
            mesh.Box(Surface.Cyan, new Vector3(-w, 0f, -w), new Vector3(w, RailHeight + Settings.fixtures.accentPost.y, w), bottom: false);
            mesh.AddTo(post, post.name);
            MakeStatic(post, occluder: false);
        }
    }
}
