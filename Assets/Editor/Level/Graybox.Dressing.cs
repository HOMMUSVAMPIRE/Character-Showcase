using UnityEngine;

namespace CharacterShowcase.EditorTools.Level
{
    // Dressing, after the concept art and the icons and decals sheet: orange banners with the Meridian mark, guide lines
    // on floors, bracketed pads where characters spawn, cyan marks where mechanics to come will be, floor rulers, orange
    // bands up tall towers, and an overhead crane. Painted things are raised a centimetre like the markings, and like them
    // are to see only; the crane, which can be jumped into from a catwalk, has box colliders. Pads and marks that sit on
    // whatever floor is under them find it once the whole level's built (see Defer).
    public static partial class Graybox
    {
        /// <summary>
        /// An orange banner hung from a wall or column face at wallPoint, facing outward: its top at top, length long,
        /// the settings' width, cut to a swallowtail at the bottom, with the Meridian mark (three white slashes) and, if
        /// given, a number under it. Hung from a graphite bar a little off the face. To see only.
        /// </summary>
        public static void Banner(Transform parent, Vector2 wallPoint, Vector2 outward, float top, float length, string number = null)
        {
            GrayboxSettings.DressingSettings d = Settings.dressing;
            var banner = new GameObject($"Banner{(number != null ? $" ({number})" : "")}");
            banner.transform.SetParent(parent, false);
            banner.transform.SetLocalPositionAndRotation(new Vector3(wallPoint.x, 0f, wallPoint.y),
                Quaternion.Euler(0f, Mathf.Atan2(outward.x, outward.y) * Mathf.Rad2Deg, 0f));

            // Laid out with +z out from the face, x across it.
            float w = d.banner.x / 2f, notch = d.banner.y, bottom = top - length, z0 = d.bannerStandOff, z1 = z0 + 0.03f, front = z1 + 0.005f;
            var mesh = new GrayboxMesh();
            mesh.Plate(Surface.Accent, new[] { new Vector2(-w, bottom + notch), new Vector2(w, bottom + notch), new Vector2(w, top), new Vector2(-w, top) }, z0, z1);
            mesh.Plate(Surface.Accent, new[] { new Vector2(-w, bottom), new Vector2(0f, bottom + notch), new Vector2(-w, bottom + notch) }, z0, z1);
            mesh.Plate(Surface.Accent, new[] { new Vector2(w, bottom), new Vector2(w, bottom + notch), new Vector2(0f, bottom + notch) }, z0, z1);
            mesh.Box(Surface.Graphite, new Vector3(-w - 0.1f, top - 0.04f, 0f), new Vector3(w + 0.1f, top + 0.06f, z1 + 0.04f));
            // The mark: three white slashes ("///" to the reader, who faces -z, so seen from in front the banner's +x is
            // to their left), in the upper part of the banner.
            float markHeight = Mathf.Min(1.4f * w, 0.35f * length), markBottom = top - 0.15f * length - markHeight, slash = 0.28f * w;
            for (int i = 0; i < 3; i++)
            {
                float x = 0.75f * w - i * 0.55f * w;
                mesh.Beam(Surface.White, new Vector3(x, markBottom, front), new Vector3(x - 0.45f * w, markBottom + markHeight, front),
                    Vector3.right, slash, 0.01f, ends: true);
            }
            mesh.AddTo(banner, banner.name);
            MakeStatic(banner, occluder: false);
            if (number != null)
            {
                Text(banner.transform, "Banner Number", number, new Vector3(0f, markBottom - 0.6f * w, front + 0.005f), Quaternion.Euler(0f, 180f, 0f),
                    0.6f * w, 1.8f * w, Settings.signs.textColour);
            }
        }

        /// <summary>
        /// A painted guide line on a floor at height through points (closed back to the first if asked for), the
        /// settings' width, in a surface (orange or white, the concept art's path lines). To see only.
        /// </summary>
        public static void GuideLine(Transform parent, string name, Vector2[] points, float height, Surface surface, bool closed = false)
        {
            float w = Settings.dressing.guideLineWidth, t = Settings.markings.thickness;
            var mesh = new GrayboxMesh();
            int segments = closed ? points.Length : points.Length - 1;
            for (int i = 0; i < segments; i++)
            {
                Vector2 p = points[i], q = points[(i + 1) % points.Length], along = (q - p).normalized * (w / 2f);
                // Run on past each corner by half the width, so the corners close (where two meet, they're the same
                // paint, so the overlap never shows).
                mesh.Beam(surface, new Vector3(p.x - along.x, height + t / 2f, p.y - along.y), new Vector3(q.x + along.x, height + t / 2f, q.y + along.y),
                    Vector3.up, t, w, ends: true);
            }
            var line = new GameObject(name);
            line.transform.SetParent(parent, false);
            mesh.AddTo(line, name);
            MakeStatic(line, occluder: false);
        }

        /// <summary>
        /// A spawn pad under a spawn marker at "at", height (the marker's feet), facing yaw: a white bracketed square
        /// on whatever floor is under it, and cyan chevrons ahead of it, the way the character starts facing. Placed once
        /// the level's built (see Defer).
        /// </summary>
        private static void SpawnPad(Transform parent, Vector2 at, float height, float yaw)
        {
            Defer(() =>
            {
                if (!FloorUnder(parent, at, height, out float floor)) { return; }
                var pad = new GameObject("Spawn Pad");
                pad.transform.SetParent(parent, false);
                pad.transform.SetLocalPositionAndRotation(new Vector3(at.x, floor, at.y), Quaternion.Euler(0f, yaw, 0f));
                float half = Settings.dressing.spawnPadSize / 2f, t = Settings.markings.thickness;
                var mesh = new GrayboxMesh();
                Brackets(mesh, Surface.White, half, 0.35f * half, 0.07f, t);
                for (int i = 0; i < 2; i++)
                {
                    float z = half + 0.35f + i * 0.35f;
                    var tip = new Vector3(0f, t / 2f, z + 0.25f);
                    mesh.Beam(Surface.Cyan, new Vector3(-0.35f, t / 2f, z), tip, Vector3.up, t, 0.08f, ends: true);
                    mesh.Beam(Surface.Cyan, new Vector3(0.35f, t / 2f, z), tip, Vector3.up, t, 0.08f, ends: true);
                }
                mesh.AddTo(pad, pad.name);
                MakeStatic(pad, occluder: false);
            });
        }

        /// <summary>
        /// Where a mechanic to come will be (a marker named "[Mechanic] (future): …"), a cyan bracketed square on the
        /// floor under the marker, with the mechanic's name in cyan beside it, the decals sheet's interact marking.
        /// Placed once the level's built (see Defer), and only where there's a floor under the marker.
        /// </summary>
        private static void FutureMark(Transform parent, string name, Vector2 at, float height)
        {
            int cut = name.IndexOf(" (future)", System.StringComparison.Ordinal);
            if (cut <= 0) { return; }
            string mechanic = name.Substring(0, cut).ToUpperInvariant();
            Defer(() =>
            {
                if (!FloorUnder(parent, at, height, out float floor)) { return; }
                var mark = new GameObject($"Future Mark ({mechanic})");
                mark.transform.SetParent(parent, false);
                mark.transform.localPosition = new Vector3(at.x, floor, at.y);
                float half = Settings.dressing.futureMarkSize / 2f, t = Settings.markings.thickness;
                var mesh = new GrayboxMesh();
                Brackets(mesh, Surface.Cyan, half, 0.4f * half, 0.06f, t);
                mesh.AddTo(mark, mark.name);
                MakeStatic(mark, occluder: false);
                Text(mark.transform, "Future Mark Label", mechanic, new Vector3(0f, t + 0.002f, -half - 0.35f), Quaternion.Euler(90f, 0f, 0f),
                    0.22f, 0.22f * mechanic.Length, new Color(0.33f, 0.78f, 0.84f));
            });
        }

        /// <summary>Four corner brackets of a square half wide each way, arms arm long and width wide, raised t, in mesh's local x and z.</summary>
        private static void Brackets(GrayboxMesh mesh, Surface surface, float half, float arm, float width, float t)
        {
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    Vector3 corner = new(sx * half, 0f, sz * half);
                    Vector3 a = corner, b = corner + new Vector3(-sx * arm, t, -sz * width);
                    mesh.Box(surface, Vector3.Min(a, b), Vector3.Max(a, b), bottom: false);
                    a = corner + new Vector3(0f, 0f, -sz * width);
                    b = corner + new Vector3(-sx * width, t, -sz * arm);
                    mesh.Box(surface, Vector3.Min(a, b), Vector3.Max(a, b), bottom: false);
                }
            }
        }

        /// <summary>
        /// The floor under a point (in parent's space) at about height: the top of whatever collider is within a metre
        /// below it, found once the level's built. False where there's nothing (a marker in mid-air).
        /// </summary>
        private static bool FloorUnder(Transform parent, Vector2 at, float height, out float floor)
        {
            Vector3 origin = parent.TransformPoint(new Vector3(at.x, height + 0.5f, at.y));
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1.5f))
            {
                floor = parent.InverseTransformPoint(hit.point).y;
                return true;
            }
            floor = 0f;
            return false;
        }

        /// <summary>
        /// A ruler painted on a floor at height from start along direction for length: a white baseline, a tick every
        /// so far (longer every metre, to one side), and the distance from the start every few metres, reading the way
        /// along it. The decals sheet's measuring scale. To see only.
        /// </summary>
        public static void Ruler(Transform parent, string name, Vector2 start, Vector2 direction, float length, float height)
        {
            GrayboxSettings.DressingSettings d = Settings.dressing;
            float t = Settings.markings.thickness, y = height + t / 2f;
            Vector2 side = new(direction.y, -direction.x);
            var mesh = new GrayboxMesh();
            Vector2 end = start + direction * length;
            mesh.Beam(Surface.White, new Vector3(start.x, y, start.y), new Vector3(end.x, y, end.y), Vector3.up, t, 0.06f, ends: true);
            for (float u = 0f; u <= length + 1e-3f; u += d.rulerTicks.x)
            {
                bool metre = Mathf.Abs(u - Mathf.Round(u)) < 1e-3f;
                Vector2 a = start + direction * u, b = a + side * (metre ? 0.45f : 0.22f);
                mesh.Beam(Surface.White, new Vector3(a.x, y, a.y), new Vector3(b.x, y, b.y), Vector3.up, t, 0.04f, ends: true);
            }
            var ruler = new GameObject(name);
            ruler.transform.SetParent(parent, false);
            mesh.AddTo(ruler, name);
            MakeStatic(ruler, occluder: false);
            for (float u = 0f; u <= length + 1e-3f; u += d.rulerTicks.y)
            {
                FloorLabel(ruler.transform, $"{u:0}", start + direction * u + side * 0.8f, height, direction);
            }
        }

        /// <summary>
        /// Orange bands up the faces of a tall axis-aligned mass (x from x0 to x1, z from z0 to z1), from y0 to y1, centred
        /// on each face whose outward direction is listed: the concept art's accent stripes up the Ascension Spire. To see only.
        /// </summary>
        public static void TowerBands(Transform parent, float x0, float x1, float z0, float z1, float y0, float y1, params Vector2[] faces)
        {
            Vector2 band = Settings.dressing.towerBand;
            var mesh = new GrayboxMesh();
            foreach (Vector2 f in faces)
            {
                if (f.x != 0f)
                {
                    float x = f.x > 0f ? x1 : x0 - band.y, zc = (z0 + z1) / 2f;
                    mesh.Box(Surface.Accent, new Vector3(x, y0, zc - band.x / 2f), new Vector3(x + band.y, y1, zc + band.x / 2f));
                }
                else
                {
                    float z = f.y > 0f ? z1 : z0 - band.y, xc = (x0 + x1) / 2f;
                    mesh.Box(Surface.Accent, new Vector3(xc - band.x / 2f, y0, z), new Vector3(xc + band.x / 2f, y1, z + band.y));
                }
            }
            var bands = new GameObject("Tower Bands");
            bands.transform.SetParent(parent, false);
            mesh.AddTo(bands, bands.name);
            MakeStatic(bands, occluder: false);
        }

        /// <summary>
        /// An overhead crane spanning from a to b (on the ground plane, each end on a runway rail whose top is at
        /// railTop): an orange girder on graphite end trucks, a trolley at its middle, and a hook block hanging below on
        /// its cables, its bottom at hookBottom. Box colliders round the girder and trucks and the hook block.
        /// </summary>
        public static void OverheadCrane(Transform parent, Vector2 a, Vector2 b, float railTop, float hookBottom)
        {
            float length = Vector2.Distance(a, b), half = length / 2f;
            Vector2 dir = (b - a) / length, middle = (a + b) / 2f;
            var crane = new GameObject("Overhead Crane");
            crane.transform.SetParent(parent, false);
            crane.transform.SetLocalPositionAndRotation(new Vector3(middle.x, railTop, middle.y), Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 0f));

            // Laid out along +z (the girder), from the rails' top up.
            var mesh = new GrayboxMesh();
            foreach (float s in new[] { -1f, 1f })
            {
                mesh.Box(Surface.Graphite, new Vector3(-1f, 0f, s * half - 0.4f), new Vector3(1f, 0.55f, s * half + 0.4f), bottom: false);
            }
            mesh.Box(Surface.Accent, new Vector3(-0.4f, 0.15f, -half + 0.4f), new Vector3(0.4f, 1.15f, half - 0.4f), ends: false);
            mesh.Box(Surface.Graphite, new Vector3(-0.7f, 1.15f, -0.7f), new Vector3(0.7f, 1.7f, 0.7f), bottom: false);
            float hook = hookBottom - railTop;
            foreach (float x in new[] { -0.15f, 0.15f })
            {
                mesh.Tube(Surface.Steel, new Vector3(x, 0.15f, 0f), new Vector3(x, hook + 0.6f, 0f), 0.025f, 6);
            }
            mesh.Box(Surface.Accent, new Vector3(-0.3f, hook + 0.25f, -0.25f), new Vector3(0.3f, hook + 0.6f, 0.25f));
            mesh.Box(Surface.Steel, new Vector3(-0.06f, hook, -0.06f), new Vector3(0.06f, hook + 0.25f, 0.06f), top: false);
            mesh.AddTo(crane, crane.name);
            AddBox(crane, new Vector3(0f, 0.85f, 0f), new Vector3(2f, 1.7f, length + 0.8f));
            AddBox(crane, new Vector3(0f, hook + 0.3f, 0f), new Vector3(0.6f, 0.6f, 0.5f));
            MakeStatic(crane, occluder: false);
        }
    }
}
