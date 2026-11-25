using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GrassField = MeridianMotion.Showcase.Grass.GrassField;
using GrassStyle = MeridianMotion.Showcase.Grass.GrassStyle;
using GrassWind = MeridianMotion.Showcase.Grass.GrassWind;

namespace MeridianMotion.Showcase.Levels
{
    // Foliage, after the concept art and the style sheet's planters: lawns with grass, trees and shrubs (the flora's
    // leaf-card prefabs, Graybox.Flora.cs; faceted shapes without them), concrete planters with an orange band, and trees
    // scattered over open ground. All seeded (the settings' seed), so every build grows the same ones. Trunks and
    // planters have box colliders; canopies and shrubs, above head height or soft, have none.
    public static partial class Graybox
    {
        /// <summary>
        /// A tree standing on a floor at "at", floor: one of the settings' trees (a flora prefab), or without them a trunk
        /// and a canopy of three faceted tiers; its sizes and turn picked by random (within the settings' ranges: the
        /// trunk's height and the canopy's radius give how tall it is). Its trunk has a box collider (on the "Tree"
        /// object, the flora inside it).
        /// </summary>
        [Piece]
        public static void Tree(Transform parent, Vector2 at, float floor, System.Random random)
        {
            // A seed of its own, drawn from the random it was given, so a level spec can record it (see SeededRandom).
            if (random is not SeededRandom) { random = new SeededRandom(random.Next()); }
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Tree), parent, at, floor, random);
            LevelBuilderSettings.FoliageSettings f = Settings.foliage;
            float Range(Vector2 range) => Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
            float trunk = Range(f.trunkHeight), radius = Range(f.canopyRadius), turn = (float)random.NextDouble() * 360f;
            GameObject flora = PickFlora(f.trees, random);
            var tree = new GameObject("Tree");
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = new Vector3(at.x, floor, at.y);
            if (flora != null)
            {
                AddBox(tree, new Vector3(0f, trunk / 2f, 0f), new Vector3(0.44f, trunk, 0.44f));
                PlaceFlora(flora, tree.transform, Vector3.zero, turn, trunk + 1.9f * radius);
                return;
            }
            var mesh = new GrayboxMesh();
            mesh.Frustum(GrayboxMaterial.Soil, Vector3.zero, 0.22f, 0.14f, 0f, trunk + 0.3f, 6, turn, top: false);
            mesh.Frustum(GrayboxMaterial.Foliage, Vector3.zero, 0.4f * radius, radius, trunk - 0.2f, trunk + 0.5f * radius, 7, turn, top: false, bottom: true);
            mesh.Frustum(GrayboxMaterial.Foliage, Vector3.zero, radius, 0.75f * radius, trunk + 0.5f * radius, trunk + 1.1f * radius, 7, turn + 25f, top: false);
            mesh.Frustum(GrayboxMaterial.Foliage, Vector3.zero, 0.75f * radius, 0.05f, trunk + 1.1f * radius, trunk + 1.9f * radius, 7, turn + 50f, top: false);
            mesh.AddTo(tree, tree.name);
            AddBox(tree, new Vector3(0f, trunk / 2f, 0f), new Vector3(0.44f, trunk, 0.44f));
            MakeStatic(tree, occluder: false);
        }

        /// <summary>
        /// A shrub on a floor at centre, its size picked by random: one of variants (flora prefabs), gathered into placed to
        /// place once what it stands in is made (PlaceShrubs); or without them, two faceted tiers in mesh.
        /// </summary>
        private static void Shrub(GrayboxMesh mesh, Vector3 centre, System.Random random, string[] variants, List<ShrubPlacement> placed)
        {
            LevelBuilderSettings.FoliageSettings f = Settings.foliage;
            float r = Mathf.Lerp(f.shrubRadius.x, f.shrubRadius.y, (float)random.NextDouble()), turn = (float)random.NextDouble() * 360f;
            GameObject flora = PickFlora(variants, random);
            if (flora != null)
            {
                placed.Add(new ShrubPlacement { prefab = flora, at = centre, turn = turn, height = ShrubHeight * r });
                return;
            }
            mesh.Frustum(GrayboxMaterial.Foliage, centre, 0.6f * r, r, 0f, 0.6f * r, 6, turn, top: false);
            mesh.Frustum(GrayboxMaterial.Foliage, centre, r, 0.1f, 0.6f * r, 1.3f * r, 6, turn + 30f, top: false);
        }

        /// <summary>
        /// A concrete planter on a floor at floor (x from x0 to x1, z from z0 to z1): a rim the settings' height with an
        /// orange band round its foot, soil inside, shrubs along it, and a tree in the middle if asked for. One box
        /// collider round the planter (it's too high to step onto), and the tree's trunk.
        /// </summary>
        [Piece]
        public static void Planter(Transform parent, string name, float x0, float x1, float z0, float z1, float floor, bool tree, System.Random random)
        {
            // A seed of its own, drawn from the random it was given, so a level spec can record it (see SeededRandom).
            if (random is not SeededRandom) { random = new SeededRandom(random.Next()); }
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Planter), parent, name, x0, x1, z0, z1, floor, tree, random);
            float h = Settings.foliage.planterHeight, rim = 0.15f, soil = h - 0.12f;
            var planter = new GameObject(name);
            planter.transform.SetParent(parent, false);
            planter.transform.localPosition = new Vector3(0f, floor, 0f);
            var mesh = new GrayboxMesh();
            // The rim's four sides, the east and west between the north and south, so none overlap.
            mesh.Box(GrayboxMaterial.Concrete, new Vector3(x0, 0f, z0), new Vector3(x1, h, z0 + rim), bottom: false);
            mesh.Box(GrayboxMaterial.Concrete, new Vector3(x0, 0f, z1 - rim), new Vector3(x1, h, z1), bottom: false);
            mesh.Box(GrayboxMaterial.Concrete, new Vector3(x0, 0f, z0 + rim), new Vector3(x0 + rim, h, z1 - rim), bottom: false, ends: false);
            mesh.Box(GrayboxMaterial.Concrete, new Vector3(x1 - rim, 0f, z0 + rim), new Vector3(x1, h, z1 - rim), bottom: false, ends: false);
            mesh.Box(GrayboxMaterial.Soil, new Vector3(x0 + rim, 0f, z0 + rim), new Vector3(x1 - rim, soil, z1 - rim), bottom: false, sides: false, ends: false);
            mesh.Box(GrayboxMaterial.Accent, new Vector3(x0 - 0.01f, 0f, z0 - 0.01f), new Vector3(x1 + 0.01f, 0.1f, z1 + 0.01f), top: false, bottom: false);
            int shrubs = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(x1 - x0, z1 - z0) / 1.2f));
            var planted = new List<ShrubPlacement>();
            bool alongX = x1 - x0 >= z1 - z0;
            for (int i = 0; i < shrubs; i++)
            {
                float f = (i + 0.5f) / shrubs;
                Vector3 at = alongX ? new Vector3(Mathf.Lerp(x0 + 0.5f, x1 - 0.5f, f), soil, (z0 + z1) / 2f) : new Vector3((x0 + x1) / 2f, soil, Mathf.Lerp(z0 + 0.5f, z1 - 0.5f, f));
                if (tree && Mathf.Abs(f - 0.5f) < 0.2f) { continue; }
                Shrub(mesh, at, random, Settings.foliage.plantedShrubs, planted);
            }
            mesh.AddTo(planter, name);
            PlaceShrubs(planted, planter.transform);
            AddBox(planter, new Vector3((x0 + x1) / 2f, h / 2f, (z0 + z1) / 2f), new Vector3(x1 - x0, h, z1 - z0));
            MakeStatic(planter, occluder: false);
            if (tree) { Tree(parent, new Vector2((x0 + x1) / 2f, (z0 + z1) / 2f), floor + soil, random); }
        }

        /// <summary>
        /// Trees scattered over open ground on a floor at floor within area (x and z, in parent's space): about the
        /// settings' spacing apart (times spread: more for sparser trees), nudged at random, each only where there's floor under it and nothing else within the
        /// settings' clearance (from just above the floor to above head height), so they never crowd a route, a stair or
        /// a platform. Placed once the level's built (see Defer), seeded from the settings' seed and salt.
        /// </summary>
        [Piece]
        public static void ScatterTrees(Transform parent, UnityEngine.Rect area, float floor, int salt, float spread = 1f)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(ScatterTrees), parent, area, floor, salt, spread);
            Defer(() =>
            {
                LevelBuilderSettings.FoliageSettings f = Settings.foliage;
                var random = new System.Random(f.seed + salt);
                Transform trees = Group(parent, "Trees");
                float spacing = f.scatterSpacing * spread, c = f.scatterClearance;
                for (float x = area.xMin + spacing / 2f; x < area.xMax; x += spacing)
                {
                    for (float z = area.yMin + spacing / 2f; z < area.yMax; z += spacing)
                    {
                        Vector2 at = new(x + ((float)random.NextDouble() - 0.5f) * 0.6f * spacing, z + ((float)random.NextDouble() - 0.5f) * 0.6f * spacing);
                        if (!FloorUnder(parent, at, floor, out float under) || Mathf.Abs(under - floor) > 0.1f) { continue; }
                        Vector3 centre = parent.TransformPoint(new Vector3(at.x, floor + 0.3f + 2.5f, at.y));
                        if (Physics.CheckBox(centre, new Vector3(c, 2.5f, c), parent.rotation, ~0, QueryTriggerInteraction.Ignore)) { continue; }
                        Tree(trees, at, floor, random);
                    }
                }
            });
        }

        /// <summary>
        /// Lawns over the open floor within area (x and z, in parent's space) at each of floors (the ground, and the
        /// tops of platforms at those heights): square cells (the settings' size) laid wherever there's floor at that
        /// height under the cell and a margin round it (so they keep back from platforms' edges) and nothing standing
        /// within the settings' clearance (so paving is left round walls, stairs, rails and columns, and routes up and
        /// down stay paved). Each lawn has, along its edges, the odd shrub, and grass over it: a GrassField ("Grass", in
        /// the Lawns group) whose blades are made on the GPU from a mask of the lawns, one layer per floor (see LawnMask).
        /// Raised a little, with no collider, their tops cut across the cells' corners rather than round them in steps
        /// and their edges bevelled (see LawnSquare), drawn with the lawn material (coloured by the field to match its
        /// grass). Placed once the level's built (see Defer), before any trees put off after it; the lawns in chunks, so
        /// each mesh stays small and is culled on its own. spacing scales the cells (bigger
        /// for wide, sparse ground) and density the grass (1 is its style's full density).
        /// </summary>
        [Piece]
        public static void Lawns(Transform parent, UnityEngine.Rect area, float[] floors, int salt, float spacing = 1f, float density = 1f)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Lawns), parent, area, floors, salt, spacing, density);
            Defer(() =>
            {
                LevelBuilderSettings.FoliageSettings f = Settings.foliage;
                var random = new System.Random(f.seed + 100 + salt);
                float cell = f.lawnCell * spacing, half = cell / 2f, edge = f.lawnMargins.x, clear = f.lawnMargins.y, t = f.lawnThickness;
                int nx = Mathf.FloorToInt(area.width / cell), nz = Mathf.FloorToInt(area.height / cell);
                Transform lawns = Group(parent, "Lawns");
                var masks = new List<(float height, bool[,] grass, List<Vector3> bushes)>();
                var lawnRenderers = new List<MeshRenderer>();
                bool FloorAt(float x, float z, float height) =>
                    FloorUnder(parent, new Vector2(x, z), height, out float under) && Mathf.Abs(under - height) < 0.06f;
                foreach (float floor in floors)
                {
                    var grass = new bool[nx, nz];
                    // The border bushes on this floor's lawns (x, z, and their footprint's radius): no grass under them.
                    var bushes = new List<Vector3>();
                    bool anyGrass = false;
                    for (int i = 0; i < nx; i++)
                    {
                        for (int j = 0; j < nz; j++)
                        {
                            float x = area.xMin + (i + 0.5f) * cell, z = area.yMin + (j + 0.5f) * cell, reach = half + edge;
                            if (!FloorAt(x, z, floor)) { continue; }
                            if (!FloorAt(x - reach, z - reach, floor) || !FloorAt(x + reach, z - reach, floor) ||
                                !FloorAt(x - reach, z + reach, floor) || !FloorAt(x + reach, z + reach, floor)) { continue; }
                            Vector3 centre = parent.TransformPoint(new Vector3(x, floor + 0.08f + 1.2f, z));
                            if (Physics.CheckBox(centre, new Vector3(half + clear, 1.2f, half + clear), parent.rotation, ~0, QueryTriggerInteraction.Ignore)) { continue; }
                            grass[i, j] = true;
                            anyGrass = true;
                        }
                    }
                    if (anyGrass) { masks.Add((floor + t, grass, bushes)); }
                    bool Grass(int i, int j) => i >= 0 && j >= 0 && i < nx && j < nz && grass[i, j];

                    // In chunks of the squares between the cells' middles (see LawnSquare), which reach half a cell past
                    // the cells on every side; the shrubs by the cells themselves.
                    const int chunk = 16;
                    float top = floor + t;
                    for (int ci = 0; ci <= nx; ci += chunk)
                    {
                        for (int cj = 0; cj <= nz; cj += chunk)
                        {
                            var mesh = new GrayboxMesh();
                            var bordered = new List<ShrubPlacement>();
                            bool any = false;
                            for (int si = ci; si < Mathf.Min(ci + chunk, nx + 1); si++)
                            {
                                for (int sj = cj; sj < Mathf.Min(cj + chunk, nz + 1); sj++)
                                {
                                    any |= LawnSquare(mesh, Grass, si, sj, area.min, cell, floor, top);
                                }
                            }
                            for (int i = ci; i < Mathf.Min(ci + chunk, nx); i++)
                            {
                                for (int j = cj; j < Mathf.Min(cj + chunk, nz); j++)
                                {
                                    if (!grass[i, j]) { continue; }
                                    bool border = !Grass(i - 1, j) || !Grass(i + 1, j) || !Grass(i, j - 1) || !Grass(i, j + 1);
                                    if (border && random.NextDouble() < f.borderShrubChance)
                                    {
                                        Shrub(mesh, new Vector3(area.xMin + (i + 0.5f) * cell, top, area.yMin + (j + 0.5f) * cell), random, f.shrubs, bordered);
                                    }
                                }
                            }
                            if (!any) { continue; }
                            var lawn = new GameObject($"Lawn (+{floor:0})");
                            lawn.transform.SetParent(lawns, false);
                            mesh.AddTo(lawn, lawn.name);
                            MakeStatic(lawn, occluder: false);
                            lawnRenderers.Add(lawn.GetComponent<MeshRenderer>());
                            PlaceShrubs(bordered, lawn.transform);
                            foreach (ShrubPlacement b in bordered) { bushes.Add(new Vector3(b.at.x, b.at.z, ShrubFootprint * b.height)); }
                        }
                    }
                }
                if (masks.Count > 0) { AddGrass(lawns, area.min, cell, nx, nz, masks, density, lawnRenderers); }
            });
        }

        /// <summary>The mask's texel size (m): fine enough for its edges to follow the lawns' cells.</summary>
        public const float GrassTexel = 0.25f;

        /// <summary>How far in from a lawn's edge (m) its grass thins out, so it doesn't stop in a hard line.</summary>
        public const float GrassEdgeFade = 0.5f;

        /// <summary>How far in from a lawn's edge (m) the grass is longer (the style's Edge Length), most at the edge.</summary>
        public const float GrassEdgeReach = 1f;

        /// <summary>How far out a lawn's edge slopes down to the floor (m): a small bevel, not a step.</summary>
        public const float LawnBevel = 0.06f;

        /// <summary>Where the grass's shaders, styles and the lawn's material are.</summary>
        private const string GrassFolder = "Assets/Showcase/Grass/";

        /// <summary>The style lawns' grass is given (Create Grass Assets makes it); without it, the style's defaults.</summary>
        public const string LawnStylePath = GrassFolder + "Styles/Lawn.asset";

        /// <summary>The lawns' material, the Lawn shader (Create Grass Assets makes it); without it, the palette's Grass.</summary>
        public const string LawnMaterialPath = GrassFolder + "Lawn.mat";

        /// <summary>
        /// A GrassField ("Grass") in lawns, over the lawns' cells (cell wide, nx by nz from corner, in lawns' space), with a
        /// layer for each floor that has any (its lawns' top, and which cells are lawn: see LawnMask), and the lawns under
        /// it (given the lawn material, so the field can colour them to match its grass).
        /// </summary>
        private static void AddGrass(Transform lawns, Vector2 corner, float cell, int nx, int nz, List<(float height, bool[,] grass, List<Vector3> bushes)> masks, float density,
            List<MeshRenderer> lawnRenderers)
        {
            var size = new Vector2Int(Mathf.CeilToInt(nx * cell / GrassTexel), Mathf.CeilToInt(nz * cell / GrassTexel));
            var layers = new List<GrassField.Layer>();
            foreach ((float height, bool[,] grass, List<Vector3> bushes) in masks)
            {
                (byte[] densityMask, byte[] edgeMask) = LawnMask(grass, cell, size, density);
                ClearUnder(densityMask, size, corner, bushes);
                layers.Add(new GrassField.Layer { height = height, density = densityMask, edge = edgeMask });
            }
            // The level's wind, beside its first lawns.
            if (lawns.root.GetComponentInChildren<GrassWind>(true) == null)
            {
                new GameObject("Grass Wind", typeof(GrassWind)).transform.SetParent(lawns.parent, false);
            }
            var obj = new GameObject("Grass");
            obj.transform.SetParent(lawns, false);
            var field = obj.AddComponent<GrassField>();
            field.SetMask(corner, GrassTexel, size, layers);
            field.Style = AssetDatabase.LoadAssetAtPath<GrassStyle>(LawnStylePath);
            var lawnMaterial = AssetDatabase.LoadAssetAtPath<Material>(LawnMaterialPath);
            if (lawnMaterial != null)
            {
                Material palette = For(GrayboxMaterial.Grass);
                foreach (MeshRenderer renderer in lawnRenderers)
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) { if (materials[i] == palette) { materials[i] = lawnMaterial; } }
                    renderer.sharedMaterials = materials;
                }
                field.SetLawns(lawnRenderers);
            }
            field.SetShaders(AssetDatabase.LoadAssetAtPath<ComputeShader>(GrassFolder + "GrassBlades.compute"),
                AssetDatabase.LoadAssetAtPath<Shader>(GrassFolder + "Grass.shader"));
        }

        /// <summary>
        /// The shape of the lawn in a square between four cells' middles, as marching squares cut it: in unit coordinates
        /// (0 to 1 across the square, from its corner cell), the corners that are lawn and the points halfway along the
        /// sides where it changes, in order round the square (always convex). By which corners are lawn: bit 0 the
        /// square's corner cell, then round it (+x, then +x and +z, then +z). Where only two opposite corners are, the
        /// lawn joins them across the middle.
        /// </summary>
        private static readonly (Vector2 at, bool middle)[][] SquareShapes = MakeSquareShapes();

        private static (Vector2, bool)[][] MakeSquareShapes()
        {
            var corners = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            var shapes = new (Vector2, bool)[16][];
            for (int c = 0; c < 16; c++)
            {
                var shape = new List<(Vector2, bool)>();
                for (int k = 0; k < 4; k++)
                {
                    bool here = (c & (1 << k)) != 0, next = (c & (1 << ((k + 1) % 4))) != 0;
                    if (here) { shape.Add((corners[k], false)); }
                    if (here != next) { shape.Add(((corners[k] + corners[(k + 1) % 4]) / 2f, true)); }
                }
                shapes[c] = shape.ToArray();
            }
            return shapes;
        }

        /// <summary>Which corners of square (si, sj) are lawn (see SquareShapes): its corner cell is (si - 1, sj - 1).</summary>
        private static int SquareCase(System.Func<int, int, bool> lawn, int si, int sj) =>
            (lawn(si - 1, sj - 1) ? 1 : 0) | (lawn(si, sj - 1) ? 2 : 0) | (lawn(si, sj) ? 4 : 0) | (lawn(si - 1, sj) ? 8 : 0);

        /// <summary>
        /// The lawn in square (si, sj), between the middles of cells (si - 1, sj - 1) and (si, sj) (cell wide, from corner,
        /// lawn saying which cells are lawn): its top at top, cut as SquareShapes says, so its edges run along the cells'
        /// sides and across their corners rather than round them in steps; and where its edge crosses the square, a bevel
        /// down to the floor. Whether there was any.
        /// </summary>
        private static bool LawnSquare(GrayboxMesh mesh, System.Func<int, int, bool> lawn, int si, int sj, Vector2 corner, float cell, float floor, float top)
        {
            int c = SquareCase(lawn, si, sj);
            if (c == 0) { return false; }
            (Vector2 at, bool middle)[] shape = SquareShapes[c];
            Vector2 origin = corner + new Vector2((si - 0.5f) * cell, (sj - 0.5f) * cell);
            Vector3 Point(Vector2 unit, float height, Vector2 outward = default) =>
                new(origin.x + unit.x * cell + outward.x, height, origin.y + unit.y * cell + outward.y);

            var points = new Vector3[shape.Length];
            for (int k = 0; k < shape.Length; k++) { points[k] = Point(shape[k].at, top); }
            int first = mesh.Face(Vector3.up, points);
            for (int k = 1; k + 1 < shape.Length; k++) { mesh.Triangle(GrayboxMaterial.Grass, first, first + k, first + k + 1, Vector3.up); }

            // The edge: between two middles in a row, sloping out and down to the floor, away from the corners that aren't lawn.
            Vector2 outside = Vector2.zero;
            for (int k = 0; k < 4; k++) { if ((c & (1 << k)) == 0) { outside += new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }[k]; } }
            for (int k = 0; k < shape.Length; k++)
            {
                (Vector2 a, bool am) = shape[k];
                (Vector2 b, bool bm) = shape[(k + 1) % shape.Length];
                if (!am || !bm) { continue; }
                Vector2 along = (b - a).normalized, outward = new(along.y, -along.x);
                // Toward the nearest corner that isn't lawn (in a saddle, each edge has one beside it).
                Vector2 mid = (a + b) / 2f, nearest = Vector2.zero;
                float best = float.MaxValue;
                for (int q = 0; q < 4; q++)
                {
                    if ((c & (1 << q)) != 0) { continue; }
                    Vector2 cq = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }[q];
                    if ((cq - mid).sqrMagnitude < best) { best = (cq - mid).sqrMagnitude; nearest = cq; }
                }
                if (Vector2.Dot(outward, nearest - mid) < 0f) { outward = -outward; }
                Vector3 normal = new Vector3(outward.x * (top - floor), LawnBevel, outward.y * (top - floor)).normalized;
                mesh.Quad(GrayboxMaterial.Grass, normal, Point(a, top), Point(b, top), Point(b, floor, outward * LawnBevel), Point(a, floor, outward * LawnBevel));
            }
            return true;
        }

        /// <summary>Whether a point (in unit coordinates) is in a convex shape from SquareShapes (or on its edge).</summary>
        private static bool InShape((Vector2 at, bool middle)[] shape, Vector2 p)
        {
            if (shape.Length < 3) { return false; }
            float sign = 0f;
            for (int k = 0; k < shape.Length; k++)
            {
                Vector2 a = shape[k].at, b = shape[(k + 1) % shape.Length].at;
                float cross = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
                if (Mathf.Abs(cross) < 1e-6f) { continue; }
                if (sign == 0f) { sign = Mathf.Sign(cross); }
                else if (Mathf.Sign(cross) != sign) { return false; }
            }
            return true;
        }

        /// <summary>A shrub is this many times its radius (the foliage's Shrub Radius) tall, and its footprint's radius this much of its height.</summary>
        private const float ShrubHeight = 2f, ShrubFootprint = 0.45f;

        /// <summary>
        /// Clears a mask's grass under bushes (x, z, in the lawns' space, and their footprint's radius): none within most of
        /// the footprint, thinning back in toward its edge, so a bush stands clear of the grass rather than in it.
        /// </summary>
        public static void ClearUnder(byte[] density, Vector2Int size, Vector2 corner, List<Vector3> bushes)
        {
            foreach (Vector3 bush in bushes)
            {
                float radius = bush.z;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((bush.x - radius - corner.x) / GrassTexel)), x1 = Mathf.Min(size.x - 1, Mathf.CeilToInt((bush.x + radius - corner.x) / GrassTexel));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((bush.y - radius - corner.y) / GrassTexel)), z1 = Mathf.Min(size.y - 1, Mathf.CeilToInt((bush.y + radius - corner.y) / GrassTexel));
                for (int z = z0; z <= z1; z++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = new Vector2(corner.x + (x + 0.5f) * GrassTexel - bush.x, corner.y + (z + 0.5f) * GrassTexel - bush.y).magnitude / radius;
                        float keep = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, d));
                        int k = z * size.x + x;
                        density[k] = (byte)Mathf.RoundToInt(density[k] * keep);
                    }
                }
            }
        }

        /// <summary>
        /// A floor's grass mask: size texels (GrassTexel square), each 0 to 255. A texel's in the lawn if it's inside the
        /// lawn's top (see LawnSquare: so the grass follows its cut corners). Its density is that, blurred and kept inside
        /// the lawn, so the grass thins out over GrassEdgeFade in from the lawn's edge, times density (at most 1). Its edge
        /// is how near the lawn's edge it is: 255 at it, falling to nothing GrassEdgeReach in.
        /// </summary>
        public static (byte[] density, byte[] edge) LawnMask(bool[,] grass, float cell, Vector2Int size, float density)
        {
            int nx = grass.GetLength(0), nz = grass.GetLength(1);
            bool Lawn(int i, int j) => i >= 0 && j >= 0 && i < nx && j < nz && grass[i, j];
            var coverage = new float[size.x * size.y];
            for (int z = 0; z < size.y; z++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    // Which square the texel's middle is in, and where in it.
                    float u = (x + 0.5f) * GrassTexel / cell + 0.5f, v = (z + 0.5f) * GrassTexel / cell + 0.5f;
                    int si = Mathf.FloorToInt(u), sj = Mathf.FloorToInt(v);
                    int c = SquareCase(Lawn, si, sj);
                    coverage[z * size.x + x] = c != 0 && InShape(SquareShapes[c], new Vector2(u - si, v - sj)) ? 1f : 0f;
                }
            }
            // Box blurs, in x then z: inside the lawn they're 1, at its edge a half, outside less.
            int fade = Mathf.Max(1, Mathf.RoundToInt(GrassEdgeFade / GrassTexel / 2f));
            int reach = Mathf.Max(1, Mathf.RoundToInt(GrassEdgeReach / GrassTexel));
            float[] near = Blur(Blur(coverage, size, fade, 1, 0), size, fade, 0, 1);
            float[] wide = Blur(Blur(coverage, size, reach, 1, 0), size, reach, 0, 1);
            var densityMask = new byte[size.x * size.y];
            var edgeMask = new byte[size.x * size.y];
            float most = Mathf.Clamp01(density);
            for (int k = 0; k < densityMask.Length; k++)
            {
                if (coverage[k] <= 0f) { continue; }
                densityMask[k] = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01((near[k] - 0.5f) * 2f) * most);
                edgeMask[k] = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01((1f - wide[k]) * 2f));
            }
            return (densityMask, edgeMask);
        }

        private static float[] Blur(float[] values, Vector2Int size, int r, int dx, int dz)
        {
            var result = new float[values.Length];
            for (int z = 0; z < size.y; z++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    float sum = 0f;
                    for (int k = -r; k <= r; k++)
                    {
                        int sx = Mathf.Clamp(x + k * dx, 0, size.x - 1), sz = Mathf.Clamp(z + k * dz, 0, size.y - 1);
                        // Beyond the mask's edge is no lawn.
                        bool outside = x + k * dx < 0 || x + k * dx >= size.x || z + k * dz < 0 || z + k * dz >= size.y;
                        sum += outside ? 0f : values[sz * size.x + sx];
                    }
                    result[z * size.x + x] = sum / (2 * r + 1);
                }
            }
            return result;
        }
    }
}
