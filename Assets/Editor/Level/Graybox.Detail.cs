using System.Collections.Generic;
using UnityEngine;

namespace CharacterShowcase.EditorTools.Level
{
    // Architectural detail: what makes a plain solid read as a built thing (pilasters on long walls, frames round
    // openings, a coping along a platform's edge, a chamfered column). All of it is to see only, with no collider of its
    // own, so it never changes how the character moves: a column keeps the box collider a plain one had.
    public static partial class Graybox
    {
        /// <summary>
        /// A wall's detail, on both its faces (thickness apart, centred on the line from a along dir): concrete
        /// pilasters along long walls, centred on the wall and clear of its ends and openings; round each opening a
        /// graphite frame with an orange lip along its inside edge (jambs, a header unless it's open to the wall's top,
        /// and a sill unless it's a doorway down to the wall's foot); and a light over each opening with a header (see
        /// DoorLight). One object, named for the wall, with no collider; its sizes are the settings' (Walls).
        /// </summary>
        private static void WallDetail(Transform parent, string name, Vector2 a, Vector2 dir, float length, float y0, float y1, float thickness,
            List<(float from, float to, float bottom, float top)> openings)
        {
            GrayboxSettings.WallSettings w = Settings.walls;
            float PilasterSpacing = w.pilasterSpacing, PilasterWidth = w.pilasterWidth, PilasterDepth = w.pilasterDepth;
            float FrameWidth = w.frameWidth, FrameLip = w.frameLip, FrameDepth = w.frameDepth;
            var mesh = new GrayboxMesh();
            bool any = false;
            float face = thickness / 2f;
            // A box on both faces of the wall: along it from s to e, from bottom to top, standing depth proud.
            void OnFaces(Surface surface, float s, float e, float bottom, float top, float depth, bool topFace = true, bool bottomFace = true)
            {
                s = Mathf.Max(s, 0f);
                e = Mathf.Min(e, length);
                if (e - s < 0.01f || top - bottom < 0.01f) { return; }
                mesh.Box(surface, new Vector3(face, bottom, s), new Vector3(face + depth, top, e), topFace, bottomFace);
                mesh.Box(surface, new Vector3(-face - depth, bottom, s), new Vector3(-face, top, e), topFace, bottomFace);
                any = true;
            }

            if (length >= w.pilasterMinWallLength)
            {
                int count = Mathf.FloorToInt((length - 2f) / PilasterSpacing) + 1;
                float first = (length - (count - 1) * PilasterSpacing) / 2f;
                for (int i = 0; i < count; i++)
                {
                    float p0 = first + i * PilasterSpacing - PilasterWidth / 2f, p1 = p0 + PilasterWidth;
                    if (openings.TrueForAll(o => p1 < o.from - 0.5f || p0 > o.to + 0.5f))
                    {
                        OnFaces(Surface.Concrete, p0, p1, y0, y1, PilasterDepth, bottomFace: false);
                    }
                }
            }

            foreach ((float s, float e, float bottom, float top) in openings)
            {
                if (e - s < 0.1f || top - bottom < 0.1f) { continue; }
                bool header = top < y1 - FrameWidth, sill = bottom > y0 + FrameWidth;
                float jambTop = header ? top + FrameWidth : y1, jambBottom = sill ? bottom - FrameWidth : Mathf.Max(bottom, y0);
                float lipTop = header ? top + FrameLip : y1, lipBottom = sill ? bottom - FrameLip : jambBottom;
                // Jambs, where there's wall beside the opening for them.
                if (s >= FrameWidth)
                {
                    OnFaces(Surface.Accent, s - FrameLip, s, lipBottom, lipTop, FrameDepth, topFace: !header, bottomFace: false);
                    OnFaces(Surface.Graphite, s - FrameWidth, s - FrameLip, jambBottom, jambTop, FrameDepth);
                }
                if (e <= length - FrameWidth)
                {
                    OnFaces(Surface.Accent, e, e + FrameLip, lipBottom, lipTop, FrameDepth, topFace: !header, bottomFace: false);
                    OnFaces(Surface.Graphite, e + FrameLip, e + FrameWidth, jambBottom, jambTop, FrameDepth);
                }
                if (header)
                {
                    OnFaces(Surface.Accent, s, e, top, top + FrameLip, FrameDepth);
                    OnFaces(Surface.Graphite, s - FrameLip, e + FrameLip, top + FrameLip, top + FrameWidth, FrameDepth);
                    // A light over the doorway, where there's wall above the frame for it.
                    Vector2 light = Settings.fixtures.doorLightSize;
                    float lightBottom = top + FrameWidth + Settings.fixtures.doorLightGap;
                    if (lightBottom + light.y < y1)
                    {
                        DoorLight(mesh, face, (s + e) / 2f, lightBottom, light);
                        if (Settings.lights.fixturesCastLight)
                        {
                            Vector2 at = a + dir * ((s + e) / 2f), normal = new Vector2(dir.y, -dir.x);
                            foreach (float side in new[] { -1f, 1f })
                            {
                                FixtureLight(parent, at + normal * (side * (face + 0.3f)), lightBottom, LightType.Point, Vector3.zero);
                            }
                        }
                    }
                }
                if (sill)
                {
                    OnFaces(Surface.Accent, s, e, bottom - FrameLip, bottom, FrameDepth);
                    OnFaces(Surface.Graphite, s - FrameLip, e + FrameLip, bottom - FrameWidth, bottom - FrameLip, FrameDepth);
                }
            }
            if (!any) { return; }

            var detail = new GameObject($"{name} (Detail)");
            detail.transform.SetParent(parent, false);
            detail.transform.SetLocalPositionAndRotation(new Vector3(a.x, 0f, a.y), Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 0f));
            mesh.AddTo(detail, detail.name);
            MakeStatic(detail, occluder: false);
        }

        /// <summary>
        /// A platform: a block (x from x0 to x1, z from z0 to z1, from y0 to top) with a coping round its top edge (see
        /// Coping). For platforms without guardrails; a guardrail's own fascia marks the edge it stands on.
        /// </summary>
        public static GameObject Platform(Transform parent, string name, float x0, float x1, float z0, float z1, float y0, float top,
            Surface surface = Surface.Concrete, Surface coping = Surface.Graphite)
        {
            GameObject block = Block(parent, name, x0, x1, z0, z1, y0, top, surface);
            Coping(parent, $"{name} (Coping)", Rect(x0, z0, x1, z1), top, Mathf.Min(Settings.platforms.copingHeight, (top - y0) / 4f), coping);
            return block;
        }

        /// <summary>
        /// A band (graphite, unless another surface's given) round the top edge of a solid with a convex footprint, from height below its top up to just
        /// under its top, standing a few centimetres proud of its sides, so the edge reads from a distance (and a ledge to catch
        /// is easy to see). The bands run on past each corner to close it. One object, with no collider.
        /// </summary>
        public static void Coping(Transform parent, string name, Vector2[] shape, float top, float height, Surface surface = Surface.Graphite)
        {
            float CopingDepth = Settings.platforms.copingDepth, CopingGap = Settings.platforms.copingGap;
            if (height < 2f * CopingGap) { return; }
            var mesh = new GrayboxMesh();
            Vector2 centroid = Vector2.zero;
            foreach (Vector2 p in shape) { centroid += p; }
            centroid /= shape.Length;
            for (int i = 0; i < shape.Length; i++)
            {
                Vector2 p = shape[i], q = shape[(i + 1) % shape.Length];
                Vector2 along = (q - p).normalized, outward = new Vector2(along.y, -along.x);
                if (Vector2.Dot(outward, (p + q) / 2f - centroid) < 0f) { outward = -outward; }
                Vector2 from = p + outward * (CopingDepth / 2f) - along * CopingDepth, to = q + outward * (CopingDepth / 2f) + along * CopingDepth;
                // Stopping just short of the top: where a stair or ramp arrives at the edge, the band's top is inside
                // it, rather than in the plane of its top step, where the two would flicker.
                float y = top - CopingGap - height / 2f;
                mesh.Beam(surface, new Vector3(from.x, y, from.y), new Vector3(to.x, y, to.y), Vector3.up, height - CopingGap, CopingDepth, ends: true);
            }
            var coping = new GameObject(name);
            coping.transform.SetParent(parent, false);
            mesh.AddTo(coping, name);
            MakeStatic(coping, occluder: false);
        }

        /// <summary>
        /// A column with chamfered corners (a footprint of size across and along, turned yaw, from y0 to y1), with its
        /// top left open where something stands on it; with accent, an orange strip up the middle of each face, the
        /// style sheet's column. Its collider is the plain box of its footprint, as a box column's was.
        /// </summary>
        public static GameObject Column(Transform parent, string name, Vector2 centre, Vector2 size, float y0, float y1, float yaw = 0f,
            Surface surface = Surface.Steel, bool accent = false, bool capped = true)
        {
            var column = new GameObject(name);
            column.transform.SetParent(parent, false);
            column.transform.SetLocalPositionAndRotation(new Vector3(centre.x, y0, centre.y), Quaternion.Euler(0f, yaw, 0f));
            GrayboxSettings.ColumnSettings settings = Settings.columns;
            float hx = size.x / 2f, hz = size.y / 2f, c = settings.chamfer * Mathf.Min(size.x, size.y), h = y1 - y0;
            var mesh = new GrayboxMesh();
            mesh.Prism(surface, new[]
            {
                new Vector2(-hx + c, -hz), new Vector2(hx - c, -hz), new Vector2(hx, -hz + c), new Vector2(hx, hz - c),
                new Vector2(hx - c, hz), new Vector2(-hx + c, hz), new Vector2(-hx, hz - c), new Vector2(-hx, -hz + c),
            }, 0f, h, top: capped);
            if (accent)
            {
                float proud = settings.accentStripProud, widest = settings.accentStripWidth;
                float sx = Mathf.Min(widest, 0.25f * (size.x - 2f * c)), sz = Mathf.Min(widest, 0.25f * (size.y - 2f * c));
                float b = settings.accentStripSpan.x * h, t = settings.accentStripSpan.y * h;
                mesh.Box(Surface.Accent, new Vector3(hx, b, -sz / 2f), new Vector3(hx + proud, t, sz / 2f));
                mesh.Box(Surface.Accent, new Vector3(-hx - proud, b, -sz / 2f), new Vector3(-hx, t, sz / 2f));
                mesh.Box(Surface.Accent, new Vector3(-sx / 2f, b, hz), new Vector3(sx / 2f, t, hz + proud));
                mesh.Box(Surface.Accent, new Vector3(-sx / 2f, b, -hz - proud), new Vector3(sx / 2f, t, -hz));
            }
            mesh.AddTo(column, name);
            var collider = column.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, h / 2f, 0f);
            collider.size = new Vector3(size.x, h, size.y);
            MakeStatic(column);
            return column;
        }
    }
}
