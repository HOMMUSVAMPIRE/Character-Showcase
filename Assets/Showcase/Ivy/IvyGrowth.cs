using System;
using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.Showcase.Ivy
{
    /// <summary>
    /// Grows ivy over a wall face, as a level's built (ReferenceAssets/OpenTasks.md, Q2): stems climbing from roots along
    /// the face's foot, wandering and branching, each to a height of its own; then a mask of where its leaves are (dense
    /// along and round the stems, thinning toward their tips, so the growth's front is uneven and fades out) and where its
    /// stems are. In the face's own space: u along it, v up it, in metres from its corner. Seeded, so the same seed grows
    /// the same ivy. What's on the face at each point (wallAt) decides where it may go: a stem stops where there's no wall
    /// to climb (or it mustn't: a climbable or marked wall), and carries on behind something standing proud of the face (a
    /// pilaster), where no leaves grow. Vines (Hang) are grown the same way upside down: from roots along the face's top,
    /// hanging, with little wander and few branches, their leaves thinning toward their tips.
    /// </summary>
    public static class IvyGrowth
    {
        /// <summary>What's at a point on a face, for the ivy.</summary>
        public enum Surface
        {
            /// <summary>No wall it may grow on: nothing, or a wall that's climbable or marked for a move. Stems stop.</summary>
            None,
            /// <summary>The face itself: stems and leaves.</summary>
            Face,
            /// <summary>Something standing proud of the face (a pilaster, a frame): stems go on behind it, no leaves.</summary>
            Proud,
        }

        /// <summary>How the ivy grows: see each setting.</summary>
        [Serializable]
        public struct Settings
        {
            [Tooltip("How much of the wall's foot has roots (0 to 1): more, closer together, for a fuller wall. An Ivy piece's Cover multiplies it.")]
            [Range(0f, 1f)] public float cover;
            [Tooltip("How high the stems climb, as a share of the face's height: each root's picked between these.")]
            public Vector2 height;
            [Tooltip("How far (m) leaves spread either side of a main stem; a branch's are less.")]
            [Min(0.05f)] public float spread;
            [Tooltip("How often a stem branches, per metre of it.")]
            [Min(0f)] public float branching;
            [Tooltip("How much a stem wanders from straight up (0: not at all).")]
            [Range(0f, 1f)] public float wander;
            [Tooltip("How fine the mask of where the leaves are is (m a texel): finer follows the stems more closely, and takes more memory.")]
            [Min(0.03f)] public float texel;

            /// <summary>The settings ivy is grown with unless given others.</summary>
            public static Settings Defaults => new() { cover = 1f, height = new Vector2(0.45f, 1f), spread = 0.45f, branching = 0.9f, wander = 0.5f, texel = 0.1f };

            /// <summary>
            /// The settings vines are hung with unless given others (Hang: height is then how far down they hang, as a share
            /// of the face's): narrower, straighter, and hardly branching, so they read as strands, not a second ivy.
            /// </summary>
            public static Settings VineDefaults => new() { cover = 1f, height = new Vector2(0.35f, 1f), spread = 0.2f, branching = 0.25f, wander = 0.15f, texel = 0.1f };
        }

        /// <summary>What a face grew: its mask (width by height texels, density and stems, 0 to 255) and its stems.</summary>
        public sealed class Grown
        {
            public int width, height;
            public float texel;
            public byte[] density, stem;
            /// <summary>Each stem's points: u, v (m) and its radius (m), from its root (or where it branched) to its tip.</summary>
            public readonly List<List<Vector3>> stems = new();
        }

        /// <summary>How far a stem grows a step (m).</summary>
        private const float StepLength = 0.06f;

        /// <summary>The thickest a main stem is (m); each branch is thinner, by BranchThinning.</summary>
        private const float StemRadius = 0.018f, BranchThinning = 0.65f;

        /// <summary>The most generations of branches a stem has (a branch of a branch is the second): ivy's, and a vine's.</summary>
        private const int MostGenerations = 2, MostVineGenerations = 1;

        /// <summary>How much of a vine, from its tip, its leaves thin out over.</summary>
        private const float VineThinning = 0.55f;

        /// <summary>How much closer a vine's roots are than ivy's at the same cover: strands are thin, so there are more.</summary>
        private const float VineRootCloser = 0.45f;

        /// <summary>
        /// Grows ivy over a face size wide and tall (m), from seed, as settings say; wallAt (if given) says what's on the
        /// face at each point (u, v): see Surface.
        /// </summary>
        public static Grown Grow(Vector2 size, int seed, Settings settings, Func<Vector2, Surface> wallAt = null) =>
            GrowOver(size, seed, settings, wallAt, hanging: false);

        /// <summary>
        /// Hangs vines over a face size wide and tall (m), from roots along its top (v = size.y), from seed, as settings say
        /// (its height the share of the face they hang down); wallAt as Grow's. In the face's space, as Grow's.
        /// </summary>
        public static Grown Hang(Vector2 size, int seed, Settings settings, Func<Vector2, Surface> wallAt = null) =>
            GrowOver(size, seed, settings, wallAt, hanging: true);

        /// <summary>
        /// Grow, or Hang: a hanging face's stems are grown climbing a face turned upside down (its top as the foot), then
        /// turned back, so both share the one grower.
        /// </summary>
        private static Grown GrowOver(Vector2 size, int seed, Settings settings, Func<Vector2, Surface> wallAt, bool hanging)
        {
            var random = new System.Random(seed);
            float Next() => (float)random.NextDouble();
            float texel = Mathf.Max(0.03f, settings.texel);
            var grown = new Grown
            {
                texel = texel,
                width = Mathf.Max(1, Mathf.CeilToInt(size.x / texel)),
                height = Mathf.Max(1, Mathf.CeilToInt(size.y / texel)),
            };
            grown.density = new byte[grown.width * grown.height];
            grown.stem = new byte[grown.width * grown.height];
            if (settings.cover <= 0f) { return grown; }

            // Roots along the foot, closer together the more it covers (quickly at first: half covers about as fully as the
            // prototype's look the user chose, Q1), each nudged at random.
            float rootSpacing = Mathf.Lerp(4f, 0.8f, Mathf.Sqrt(Mathf.Clamp01(settings.cover))) * (hanging ? VineRootCloser : 1f);
            Func<Vector2, Surface> grownOn = wallAt == null || !hanging ? wallAt : uv => wallAt(new Vector2(uv.x, size.y - uv.y));
            int generations = hanging ? MostVineGenerations : MostGenerations;
            var paths = new List<(List<Vector3> points, int generation)>();
            for (float u = rootSpacing * 0.5f; u < size.x; u += rootSpacing)
            {
                float root = Mathf.Clamp(u + (Next() - 0.5f) * 0.7f * rootSpacing, 0.05f, size.x - 0.05f);
                float top = size.y * Mathf.Lerp(settings.height.x, settings.height.y, Next());
                GrowStem(new Vector2(root, 0.02f), 0f, top, top * 1.4f, 0, generations, size, settings, random, grownOn, paths);
            }
            if (hanging)
            {
                // Turned back the right way up: grown down from the top.
                foreach ((List<Vector3> points, int _) in paths)
                {
                    for (int i = 0; i < points.Count; i++) { points[i] = new Vector3(points[i].x, size.y - points[i].y, points[i].z); }
                }
            }

            // The mask: leaves dense along and round the stems, less toward their tips; stems where they are.
            var density = new float[grown.width * grown.height];
            var stem = new float[grown.width * grown.height];
            foreach ((List<Vector3> points, int generation) in paths)
            {
                float reach = settings.spread * Mathf.Pow(0.75f, generation);
                // Ivy's front fades over its last stretch; a vine's leaves thin over most of its lower half, to a bare tip.
                float frontLength = hanging ? Mathf.Max(0.6f, VineThinning * points.Count * StepLength) : 0.6f;
                for (int i = 0; i < points.Count; i += 2)
                {
                    // Toward the tip the leaves are fewer and closer in: the growth's front, uneven and fading out.
                    float toTip = points.Count > 1 ? (float)(points.Count - 1 - i) / (points.Count - 1) : 1f;
                    float front = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(toTip * points.Count * StepLength / frontLength));
                    Stamp(density, grown, new Vector2(points[i].x, points[i].y), reach * Mathf.Lerp(0.45f, 1f, front), 0.7f * Mathf.Lerp(0.5f, 1f, front));
                    StampMax(stem, grown, new Vector2(points[i].x, points[i].y), 0.06f);
                }
            }
            for (int y = 0; y < grown.height; y++)
            {
                for (int x = 0; x < grown.width; x++)
                {
                    int k = y * grown.width + x;
                    if (density[k] <= 0f) { continue; }
                    // Leaves only on the face itself: none on what stands proud of it, nor past where the wall stops.
                    if (wallAt != null && wallAt(new Vector2((x + 0.5f) * texel, (y + 0.5f) * texel)) != Surface.Face) { continue; }
                    grown.density[k] = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(density[k]));
                    grown.stem[k] = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(stem[k]));
                }
            }
            foreach ((List<Vector3> points, int _) in paths) { if (points.Count > 1) { grown.stems.Add(points); } }
            return grown;
        }

        /// <summary>
        /// One stem, from start, leaning angle (radians from straight up), climbing to top (m up the face) or until it's
        /// length long, branching as it goes (each branch a stem of its own, a generation on); added to paths.
        /// </summary>
        private static void GrowStem(Vector2 start, float angle, float top, float length, int generation, int generations, Vector2 size, Settings settings,
            System.Random random, Func<Vector2, Surface> wallAt, List<(List<Vector3>, int)> paths)
        {
            float Next() => (float)random.NextDouble();
            var points = new List<Vector3>();
            paths.Add((points, generation));
            float radius = StemRadius * Mathf.Pow(BranchThinning, generation);
            Vector2 at = start;
            float grown = 0f, own = angle;
            while (grown < length && at.y < top && at.x > 0f && at.x < size.x && at.y < size.y)
            {
                if (wallAt != null && wallAt(at) == Surface.None) { break; }
                points.Add(new Vector3(at.x, at.y, radius * Mathf.Lerp(1f, 0.4f, grown / length)));
                // Wandering, and turned back toward its way: a main stem's straight up; a branch's out to the side at
                // first, turning up soon (ivy climbs: branches that ran on sideways read as vines strung along the wall).
                angle += (Next() - 0.5f) * 0.6f * settings.wander;
                own = Mathf.Lerp(own, 0f, generation == 0 ? 0f : 0.06f);
                angle = Mathf.Lerp(angle, own, 0.08f);
                at += new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * StepLength;
                grown += StepLength;
                if (generation < generations && Next() < settings.branching * StepLength * Mathf.Pow(0.7f, generation))
                {
                    float side = Next() < 0.5f ? -1f : 1f;
                    float turn = side * Mathf.Lerp(0.5f, 1.0f, Next());
                    GrowStem(at, angle + turn, Mathf.Min(size.y, top + Next() * 0.4f), (length - grown) * 0.4f + Next() * 0.8f, generation + 1,
                        generations, size, settings, random, wallAt, paths);
                }
            }
        }

        /// <summary>Adds a soft disc (radius, at most amount at its middle) to a mask.</summary>
        private static void Stamp(float[] mask, Grown g, Vector2 at, float radius, float amount)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt((at.x - radius) / g.texel)), x1 = Mathf.Min(g.width - 1, Mathf.CeilToInt((at.x + radius) / g.texel));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((at.y - radius) / g.texel)), y1 = Mathf.Min(g.height - 1, Mathf.CeilToInt((at.y + radius) / g.texel));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float d = Vector2.Distance(new Vector2((x + 0.5f) * g.texel, (y + 0.5f) * g.texel), at) / radius;
                    if (d < 1f) { mask[y * g.width + x] += amount * (1f - d * d); }
                }
            }
        }

        /// <summary>Sets a mask to at least a soft disc's value (radius, 1 at its middle).</summary>
        private static void StampMax(float[] mask, Grown g, Vector2 at, float radius)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt((at.x - radius) / g.texel)), x1 = Mathf.Min(g.width - 1, Mathf.CeilToInt((at.x + radius) / g.texel));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((at.y - radius) / g.texel)), y1 = Mathf.Min(g.height - 1, Mathf.CeilToInt((at.y + radius) / g.texel));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float d = Vector2.Distance(new Vector2((x + 0.5f) * g.texel, (y + 0.5f) * g.texel), at) / radius;
                    int k = y * g.width + x;
                    if (d < 1f) { mask[k] = Mathf.Max(mask[k], 1f - d); }
                }
            }
        }

        /// <summary>Every how many of a stem's grown points are kept when it's saved (Pack): the mesh smooths between them.</summary>
        private const int PackEvery = 4;

        /// <summary>A saved point's position's unit (m): 1/256, so a face can be 256 m wide.</summary>
        private const float PackUnit = 1f / 256f, RadiusUnit = 0.0001f;

        /// <summary>
        /// Stems packed for saving with a level (IvyField.Face.stems): every PackEvery-th point (and each stem's last), as
        /// u and v in PackUnits (two bytes each) and its radius in RadiusUnits (a byte), each stem after its point count
        /// (two bytes). About a fiftieth of the points saved as vectors, which made a level's scene tens of megabytes.
        /// </summary>
        public static byte[] Pack(List<List<Vector3>> stems)
        {
            var bytes = new List<byte>();
            void Short(int v) { v = Mathf.Clamp(v, 0, ushort.MaxValue); bytes.Add((byte)(v & 0xff)); bytes.Add((byte)(v >> 8)); }
            foreach (List<Vector3> stem in stems)
            {
                var kept = new List<Vector3>();
                for (int i = 0; i < stem.Count; i += PackEvery) { kept.Add(stem[i]); }
                if ((stem.Count - 1) % PackEvery != 0) { kept.Add(stem[stem.Count - 1]); }
                if (kept.Count < 2) { continue; }
                Short(kept.Count);
                foreach (Vector3 p in kept)
                {
                    Short(Mathf.RoundToInt(p.x / PackUnit));
                    Short(Mathf.RoundToInt(p.y / PackUnit));
                    bytes.Add((byte)Mathf.Clamp(Mathf.RoundToInt(p.z / RadiusUnit), 1, 255));
                }
            }
            return bytes.ToArray();
        }

        /// <summary>Stems as Pack saved them: each stem's points (u, v, radius).</summary>
        public static List<List<Vector3>> Unpack(byte[] bytes)
        {
            var stems = new List<List<Vector3>>();
            if (bytes == null) { return stems; }
            int at = 0;
            int Short() { int v = bytes[at] | (bytes[at + 1] << 8); at += 2; return v; }
            while (at + 2 <= bytes.Length)
            {
                int count = Short();
                if (at + 5 * count > bytes.Length) { break; }
                var stem = new List<Vector3>(count);
                for (int i = 0; i < count; i++)
                {
                    float u = Short() * PackUnit, v = Short() * PackUnit;
                    stem.Add(new Vector3(u, v, bytes[at++] * RadiusUnit));
                }
                stems.Add(stem);
            }
            return stems;
        }

        /// <summary>
        /// Stamps stems into a mask's stem channel (width by height texels, texel apart, 0 to 255): near a stem, more.
        /// The leaves stand further out over one (IvyLeaves.compute). Made again from the saved stems as a field's built,
        /// so a level saves only its masks' density.
        /// </summary>
        public static byte[] StemChannel(List<List<Vector3>> stems, int width, int height, float texel)
        {
            var g = new Grown { width = width, height = height, texel = texel };
            var mask = new float[width * height];
            foreach (List<Vector3> stem in stems)
            {
                for (int i = 0; i + 1 < stem.Count; i++)
                {
                    Vector2 a = stem[i], b = stem[i + 1];
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / StepLength));
                    for (int k = 0; k <= steps; k++) { StampMax(mask, g, Vector2.Lerp(a, b, k / (float)steps), 0.06f); }
                }
            }
            var bytes = new byte[mask.Length];
            for (int k = 0; k < mask.Length; k++) { bytes[k] = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(mask[k])); }
            return bytes;
        }

        /// <summary>
        /// Stems as a mesh, in the space the face's corner, along, up and outward are in: each a flat ribbon on the wall
        /// (StemStandOff out from it), as wide as the stem, through its points and smoothed between them (a Catmull-Rom
        /// curve, StemSmoothing points to a span), its uv along it in metres, with no vertex colour (so the flora's bark
        /// shader doesn't sway it). Wound to face outward, whichever way along and up turn. Null if there are none.
        /// </summary>
        public static Mesh StemMesh(IEnumerable<IReadOnlyList<Vector3>> stems, Vector3 corner, Vector3 along, Vector3 up, Vector3 outward, string name = "Ivy Stems")
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uvs = new List<Vector2>();
            var colours = new List<Color>();
            var triangles = new List<int>();
            Vector3 On(Vector2 uv) => corner + along * uv.x + up * uv.y + outward * StemStandOff;
            // Unity's front faces are clockwise to the viewer: so as seen from outward, which depends on along and up.
            // Unflipped, a ribbon's triangles go counterclockwise in (along, up), so they face outward only where
            // along × up points out (found by the prototype's stems not showing at all: Q1).
            bool flip = Vector3.Dot(Vector3.Cross(along, up), outward) < 0f;
            foreach (IReadOnlyList<Vector3> given in stems)
            {
                if (given.Count < 2) { continue; }
                var points = new List<Vector3>();
                for (int i = 0; i + 1 < given.Count; i++)
                {
                    Vector3 p0 = given[Mathf.Max(i - 1, 0)], p1 = given[i], p2 = given[i + 1], p3 = given[Mathf.Min(i + 2, given.Count - 1)];
                    for (int k = 0; k < StemSmoothing; k++)
                    {
                        float t = k / (float)StemSmoothing, t2 = t * t, t3 = t2 * t;
                        points.Add(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3));
                    }
                }
                points.Add(given[given.Count - 1]);
                float run = 0f;
                int first = vertices.Count;
                for (int i = 0; i < points.Count; i++)
                {
                    Vector2 here = points[i], ahead = points[Mathf.Min(i + 1, points.Count - 1)], behind = points[Mathf.Max(i - 1, 0)];
                    Vector2 way = (ahead - behind).normalized;
                    Vector2 side = new Vector2(-way.y, way.x) * Mathf.Max(points[i].z, 0.002f);
                    if (i > 0) { run += Vector2.Distance(points[i], points[i - 1]); }
                    vertices.Add(On(here - side));
                    vertices.Add(On(here + side));
                    Vector3 tangent = (along * way.x + up * way.y).normalized;
                    for (int s = 0; s < 2; s++)
                    {
                        normals.Add(outward);
                        tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, 1f));
                        uvs.Add(new Vector2(s, run * 4f));
                        colours.Add(new Color(0f, 0f, 0f, 0f));
                    }
                    if (i > 0)
                    {
                        int a = first + 2 * (i - 1);
                        triangles.AddRange(flip ? new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 } : new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                    }
                }
            }
            if (vertices.Count == 0) { return null; }
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>How many mesh points a stem has between two saved ones (about every 12 cm, as the prototype's, Q1).</summary>
        private const int StemSmoothing = 2;

        /// <summary>How far out from the wall a stem's ribbon is (m): off it, so it doesn't fight it for depth.</summary>
        private const float StemStandOff = 0.008f;
    }
}
