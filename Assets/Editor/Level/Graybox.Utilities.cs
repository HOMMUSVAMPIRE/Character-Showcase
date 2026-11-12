using System.Collections.Generic;
using UnityEngine;

namespace CharacterShowcase.EditorTools.Level
{
    // Utilities, after the style sheet's utility and industrial details: pipe runs on hangers along underground
    // ceilings, and utility stations on walls (a cabinet, a conduit up from it, a vent at its top). Pipes run well above
    // head height and have no collider; a station's cabinet stands out far enough to walk into, so it has one.
    public static partial class Graybox
    {
        /// <summary>
        /// Pipe runs along the ceilings of an enclosed network's closed cells (see Cells): in each, a steel main pipe and
        /// an orange second pipe beside it along its length, near its south or west wall, held up on hangers. The sizes
        /// are the settings' (Utilities). Stairwells and shafts, open to the sky, have none.
        /// </summary>
        public static void CellUtilities(Transform parent, IEnumerable<Cell> cells)
        {
            GrayboxSettings.UtilitySettings u = Settings.utilities;
            foreach (Cell cell in cells)
            {
                UnityEngine.Rect a = cell.Area;
                bool alongX = a.width >= a.height;
                float length = alongX ? a.width : a.height;
                if (cell.Open || length < 3f) { continue; }

                float main = u.pipeDiameters.x / 2f, second = u.pipeDiameters.y / 2f;
                float y = cell.Ceiling - u.pipeDrop;
                // Distances out from the wall, and positions along the cell, to points.
                Vector3 At(float along, float fromWall, float height) =>
                    alongX ? new Vector3(a.xMin + along, height, a.yMin + fromWall) : new Vector3(a.xMin + fromWall, height, a.yMin + along);
                float mainOut = u.pipeWallOffset, secondOut = mainOut + main + second + 0.12f;
                var mesh = new GrayboxMesh();
                mesh.Tube(Surface.Steel, At(0f, mainOut, y), At(length, mainOut, y), main, u.pipeSides, ends: true);
                mesh.Tube(Surface.Accent, At(0f, secondOut, y), At(length, secondOut, y), second, u.pipeSides, ends: true);
                int hangers = Mathf.Max(1, Mathf.FloorToInt(length / u.hangerSpacing));
                for (int i = 0; i < hangers; i++)
                {
                    float along = (i + 0.5f) * length / hangers;
                    // A rod down from the ceiling to a bar under both pipes.
                    foreach (float rod in new[] { mainOut - main - 0.04f, secondOut + second + 0.04f })
                    {
                        Vector3 bottom = At(along, rod, y - main - 0.03f), top = At(along, rod, cell.Ceiling);
                        mesh.Box(Surface.Graphite, bottom - new Vector3(0.015f, 0f, 0.015f), top + new Vector3(0.015f, 0f, 0.015f), top: false);
                    }
                    Vector3 from = At(along, mainOut - main - 0.055f, y - main - 0.06f), to = At(along, secondOut + second + 0.055f, y - main - 0.03f);
                    mesh.Box(Surface.Graphite, Vector3.Min(from, to) - (alongX ? new Vector3(0.03f, 0f, 0f) : new Vector3(0f, 0f, 0.03f)),
                        Vector3.Max(from, to) + (alongX ? new Vector3(0.03f, 0f, 0f) : new Vector3(0f, 0f, 0.03f)));
                }
                var pipes = new GameObject($"Pipes ({cell.Name})");
                pipes.transform.SetParent(parent, false);
                mesh.AddTo(pipes, pipes.name);
                MakeStatic(pipes, occluder: false);
            }
        }

        /// <summary>
        /// A utility station on a wall face at wallPoint, facing outward, on a floor at floor: a steel cabinet with an
        /// orange band (with a box collider), a graphite conduit up from it, and a vent with steel slats at its top. The
        /// sizes are the settings' (Utilities).
        /// </summary>
        public static GameObject UtilityStation(Transform parent, Vector2 wallPoint, Vector2 outward, float floor)
        {
            GrayboxSettings.UtilitySettings u = Settings.utilities;
            var station = new GameObject("Utility Station");
            station.transform.SetParent(parent, false);
            station.transform.SetLocalPositionAndRotation(new Vector3(wallPoint.x, floor, wallPoint.y),
                Quaternion.Euler(0f, Mathf.Atan2(outward.x, outward.y) * Mathf.Rad2Deg, 0f));

            // Laid out with +z out from the wall, x along it.
            Vector3 cabinet = u.utilityPanelSize;
            float lift = u.utilityPanelLift, cabinetTop = lift + cabinet.y, w = cabinet.x / 2f;
            var mesh = new GrayboxMesh();
            mesh.Box(Surface.Steel, new Vector3(-w, lift, 0f), new Vector3(w, cabinetTop, cabinet.z));
            mesh.Box(Surface.Accent, new Vector3(-w, cabinetTop - 0.2f * cabinet.y, cabinet.z), new Vector3(w, cabinetTop - 0.14f * cabinet.y, cabinet.z + 0.01f));

            Vector2 vent = u.ventSize;
            float conduit = u.conduit.x / 2f, ventBottom = u.conduit.y - vent.y / 2f, ventTop = ventBottom + vent.y;
            mesh.Tube(Surface.Graphite, new Vector3(0f, cabinetTop, conduit + 0.01f), new Vector3(0f, ventBottom, conduit + 0.01f), conduit, 6);
            const float frame = 0.06f, slat = 0.02f, edge = 0.04f;
            mesh.Box(Surface.Graphite, new Vector3(-vent.x / 2f, ventBottom, 0f), new Vector3(vent.x / 2f, ventTop, frame));
            for (float y = ventBottom + edge + u.ventSlatSpacing / 2f; y < ventTop - edge; y += u.ventSlatSpacing)
            {
                mesh.Box(Surface.Steel, new Vector3(-vent.x / 2f + edge, y - slat / 2f, frame), new Vector3(vent.x / 2f - edge, y + slat / 2f, frame + 0.03f));
            }
            mesh.AddTo(station, station.name);

            var collider = station.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, (lift + cabinetTop) / 2f, cabinet.z / 2f);
            collider.size = new Vector3(cabinet.x, cabinet.y, cabinet.z);
            MakeStatic(station, occluder: false);
            return station;
        }
    }
}
