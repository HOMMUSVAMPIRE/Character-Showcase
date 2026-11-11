using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class __Audit
{
    const float Cell = 0.25f, Step = 0.36f, Head = 1.8f, MaxSlope = 55f;
    const float X0 = -150f, X1 = 195f, Z0 = -200f, Z1 = 215f;
    public static void Run()
    {
        CharacterShowcase.EditorTools.Level.MeridianComplexBuilder.Build();
        EditorSceneManager.OpenScene(CharacterShowcase.EditorTools.Level.MeridianComplexBuilder.ScenePath);
        Physics.SyncTransforms();
        var log = new System.Text.StringBuilder();
        log.AppendLine("MESH COLLIDERS " + Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Length);
        log.AppendLine("BOX COLLIDERS " + Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Length);
        // Prism coverage: footprint vs boxes.
        int prisms = 0, boxes = 0, bad = 0;
        foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            var cols = new List<BoxCollider>();
            foreach (Transform c in mf.transform) if (c.name == "Collider") cols.Add(c.GetComponent<BoxCollider>());
            if (cols.Count == 0) continue;
            prisms++; boxes += cols.Count;
            var pts = new List<Vector2>(); float ymin = 1e9f, ymax = -1e9f;
            foreach (var v in mf.sharedMesh.vertices) { var w = mf.transform.TransformPoint(v); pts.Add(new Vector2(w.x, w.z)); ymin = Mathf.Min(ymin, w.y); ymax = Mathf.Max(ymax, w.y); }
            var hull = Hull(pts);
            Vector2 mid = Vector2.zero; foreach (var p in hull) mid += p; mid /= hull.Count;
            float sign = SignedDistance(hull, mid) < 0 ? 1f : -1f;
            Vector2 lo = new(1e9f, 1e9f), hi = new(-1e9f, -1e9f);
            foreach (var p in hull) { lo = Vector2.Min(lo, p); hi = Vector2.Max(hi, p); }
            int miss = 0, over = 0;
            if (mf.name.Contains("Summit Tower") || mf.name.Contains("Landing (+12)"))
            {
                log.AppendLine($"DEBUG {mf.name}: hull {lo}..{hi} y {ymin}..{ymax}");
                foreach (var c in cols) log.AppendLine($"   box bounds {c.bounds.min} .. {c.bounds.max}  size {c.size} lossy {c.transform.lossyScale}");
            }
            for (float x = lo.x - 0.5f; x <= hi.x + 0.5f; x += 0.05f) for (float z = lo.y - 0.5f; z <= hi.y + 0.5f; z += 0.05f)
            {
                var q = new Vector2(x, z);
                float d = sign * SignedDistance(hull, q);
                if (Mathf.Abs(d) < 0.02f) continue;
                foreach (float y in new[] { ymin + 0.05f, (ymin + ymax) / 2f, ymax - 0.05f })
                {
                    var p3 = new Vector3(x, y, z);
                    bool inBox = false;
                    foreach (var c in cols) if ((c.ClosestPoint(p3) - p3).sqrMagnitude < 1e-8f) { inBox = true; break; }
                    if (d < 0 && !inBox) miss++;
                    if (d > 0 && inBox) over++;
                }
            }
            string path = mf.name; var t = mf.transform.parent; while (t != null && t.parent != null) { path = t.name + "/" + path; t = t.parent; }
            log.AppendLine((miss + over == 0 ? "ok   " : "BAD  ") + $"[sign {sign}] " + $"{path}: {cols.Count} boxes, uncovered {miss}, overhang {over}");
            if (miss + over > 0) bad++;
        }
        log.AppendLine($"PRISMS {prisms}, boxes {boxes}, bad {bad}");
        // Walkability.
        int nx = Mathf.CeilToInt((X1 - X0) / Cell), nz = Mathf.CeilToInt((Z1 - Z0) / Cell);
        const int K = 6;
        var h = new float[nx, nz, K]; var cnt = new byte[nx, nz];
        var hits = new RaycastHit[32];
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
        {
            float x = X0 + (i + 0.5f) * Cell, z = Z0 + (j + 0.5f) * Cell;
            int n = Physics.RaycastNonAlloc(new Vector3(x, 80f, z), Vector3.down, hits, 100f);
            int c = 0;
            for (int k = 0; k < n && c < K; k++)
            {
                var hit = hits[k];
                if (Vector3.Angle(hit.normal, Vector3.up) > MaxSlope) continue;
                if (Physics.Raycast(hit.point + Vector3.up * 0.05f, Vector3.up, Head)) continue;
                h[i, j, c++] = hit.point.y;
            }
            cnt[i, j] = (byte)c;
        }
        var seen = new bool[nx, nz, K];
        var qu = new Queue<(int, int, int)>();
        int si = (int)((0f - X0) / Cell), sj = (int)((-10f - Z0) / Cell);
        for (int k = 0; k < cnt[si, sj]; k++) if (Mathf.Abs(h[si, sj, k]) < 0.2f) { seen[si, sj, k] = true; qu.Enqueue((si, sj, k)); }
        int reached = 0;
        while (qu.Count > 0)
        {
            var (i, j, k) = qu.Dequeue(); reached++;
            float y = h[i, j, k];
            for (int d = 0; d < 4; d++)
            {
                int a = i + (d == 0 ? 1 : d == 1 ? -1 : 0), b = j + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (a < 0 || b < 0 || a >= nx || b >= nz) continue;
                for (int m = 0; m < cnt[a, b]; m++) { if (seen[a, b, m] || Mathf.Abs(h[a, b, m] - y) > Step) continue; seen[a, b, m] = true; qu.Enqueue((a, b, m)); }
            }
        }
        foreach (var probe in new[] { new Vector3(50, 0, 38), new Vector3(56, 0, 38), new Vector3(45, 0, 42), new Vector3(60, 0, 38) })
        {
            var all = Physics.RaycastAll(probe + Vector3.up * 80f, Vector3.down, 100f);
            var sb2 = new System.Text.StringBuilder();
            foreach (var hit in all) sb2.Append($"{hit.point.y:0.00}({hit.collider.name}/{hit.collider.transform.parent?.name}) ");
            log.AppendLine($"PROBE {probe}: {sb2}");
        }
        log.AppendLine($"REACHED {reached} cells (before: 3280699)");
        bool Reach(Vector3 p)
        {
            int ci = (int)((p.x - X0) / Cell), cj = (int)((p.z - Z0) / Cell), rr = 4;
            for (int i = ci - rr; i <= ci + rr; i++) for (int j = cj - rr; j <= cj + rr; j++)
            { if (i < 0 || j < 0 || i >= nx || j >= nz) continue; for (int k = 0; k < cnt[i, j]; k++) if (seen[i, j, k] && Mathf.Abs(h[i, j, k] - p.y) < 0.1f) return true; }
            return false;
        }
        var targets = new (string, Vector3, bool)[]
        {
            ("Concourse NW +20 terrace", new Vector3(-32, 20, 32), true), ("Concourse NE +12 terrace", new Vector3(30, 12, 30), true),
            ("Concourse SE +16 terrace", new Vector3(30, 16, -30), true), ("Concourse ring corner NW", new Vector3(-22, 8, 22), true),
            ("Concourse plaza dais top", new Vector3(0, 1.2f, 0), false),
            ("Gardens E1 (+16)", new Vector3(-108, 16, 193), true), ("Gardens E3 (jump)", new Vector3(-30, 20, 198), false),
            ("Spire west platform", new Vector3(10, 12, 148), true), ("Spire core top", new Vector3(40, 20, 140), true),
            ("Spire gallery", new Vector3(70, 20, 160), true), ("Spire switchback landing", new Vector3(68, 38, 197), true),
            ("Spire summit", new Vector3(48, 56, 180), true), ("Spire balcony (climb)", new Vector3(31, 40, 180), false),
            ("Hall workshop", new Vector3(120, 8, 4), true), ("Hall NE landing", new Vector3(50, 12, 38), true),
            ("Hall catwalk north", new Vector3(100, 12, 38), true), ("Hall maintenance", new Vector3(80, -8, -30), true),
            ("Yard arrival", new Vector3(62, 16, -62), true), ("Yard north +24", new Vector3(112, 24, -62), true),
            ("Yard NE +24", new Vector3(165, 24, -62), true), ("Yard hub", new Vector3(112, 16, -108), true),
            ("Yard west +8", new Vector3(62, 8, -112), true), ("Yard east", new Vector3(165, 16, -112), true),
            ("Yard south +8", new Vector3(112, 8, -165), true), ("Yard SE", new Vector3(165, 16, -165), true),
            ("Yard jump B", new Vector3(65, 8, -165), true), ("Yard jump A (jump)", new Vector3(87, 8, -165), false),
            ("Yard dash (jump)", new Vector3(92.9f, 16, -131.1f), false),
            ("Network spire access", new Vector3(24, -12, 108), true), ("Network hall stair top", new Vector3(65.5f, -8, -40), true),
            ("Annex arrival", new Vector3(-62, 12, -62), true), ("Annex runway", new Vector3(-110, 4, -118), true),
        };
        foreach (var (name, p, expected) in targets) { bool r = Reach(p); log.AppendLine($"{(r == expected ? "ok  " : "FAIL")} {name}: reachable={r} expected={expected}"); }
        System.IO.File.WriteAllText("/private/tmp/claude-501/-Users-adalynnskidmore-Documents-GitHub-Character-Showcase/bcf08348-0d09-48a8-9ab1-d4efdc036009/scratchpad/audit2.txt", log.ToString());
    }
    static float SignedDistance(List<Vector2> hull, Vector2 q)
    {
        float worst = -1e9f;
        for (int i = 0; i < hull.Count; i++)
        {
            Vector2 a = hull[i], b = hull[(i + 1) % hull.Count], d = (b - a).normalized, nrm = new Vector2(d.y, -d.x);
            worst = Mathf.Max(worst, Vector2.Dot(nrm, q - a));
        }
        return worst; // hull is clockwise-in-x/z with outward normals (see Hull)
    }
    static List<Vector2> Hull(List<Vector2> pts)
    {
        var p = new List<Vector2>(pts); p.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        var h = new List<Vector2>();
        float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        foreach (var x in p) { while (h.Count >= 2 && Cross(h[^2], h[^1], x) <= 1e-6f) h.RemoveAt(h.Count - 1); h.Add(x); }
        int lower = h.Count + 1;
        for (int i = p.Count - 2; i >= 0; i--) { var x = p[i]; while (h.Count >= lower && Cross(h[^2], h[^1], x) <= 1e-6f) h.RemoveAt(h.Count - 1); h.Add(x); }
        h.RemoveAt(h.Count - 1);
        h.Reverse(); // counter-clockwise -> clockwise, so (d.y, -d.x) points outward
        return h;
    }
}
