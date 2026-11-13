using UnityEngine;

namespace CharacterShowcase.EditorTools.Level
{
    // Foliage, after the concept art and the style sheet's planters: low-poly trees (a tapering trunk under stacked,
    // faceted canopy tiers), shrubs, concrete planters with an orange band, and trees scattered over open ground. All
    // seeded (the settings' seed), so every build grows the same ones. Trunks and planters have box colliders; canopies
    // and shrubs, above head height or soft, have none.
    public static partial class Graybox
    {
        /// <summary>
        /// A tree standing on a floor at "at", floor: a trunk and a canopy of three faceted tiers, its sizes and turn
        /// picked by random (within the settings' ranges). Its trunk has a box collider.
        /// </summary>
        [Piece]
        public static void Tree(Transform parent, Vector2 at, float floor, System.Random random)
        {
            // A seed of its own, drawn from the random it was given, so a level spec can record it (see SeededRandom).
            if (random is not SeededRandom) { random = new SeededRandom(random.Next()); }
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Tree), parent, at, floor, random);
            GrayboxSettings.FoliageSettings f = Settings.foliage;
            float Range(Vector2 range) => Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
            float trunk = Range(f.trunkHeight), radius = Range(f.canopyRadius), turn = (float)random.NextDouble() * 360f;
            var tree = new GameObject("Tree");
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = new Vector3(at.x, floor, at.y);
            var mesh = new GrayboxMesh();
            mesh.Frustum(Surface.Soil, Vector3.zero, 0.22f, 0.14f, 0f, trunk + 0.3f, 6, turn, top: false);
            mesh.Frustum(Surface.Foliage, Vector3.zero, 0.4f * radius, radius, trunk - 0.2f, trunk + 0.5f * radius, 7, turn, top: false, bottom: true);
            mesh.Frustum(Surface.Foliage, Vector3.zero, radius, 0.75f * radius, trunk + 0.5f * radius, trunk + 1.1f * radius, 7, turn + 25f, top: false);
            mesh.Frustum(Surface.Foliage, Vector3.zero, 0.75f * radius, 0.05f, trunk + 1.1f * radius, trunk + 1.9f * radius, 7, turn + 50f, top: false);
            mesh.AddTo(tree, tree.name);
            AddBox(tree, new Vector3(0f, trunk / 2f, 0f), new Vector3(0.44f, trunk, 0.44f));
            MakeStatic(tree, occluder: false);
        }

        /// <summary>A shrub in mesh, on a floor at centre: two faceted tiers, its size picked by random.</summary>
        private static void Shrub(GrayboxMesh mesh, Vector3 centre, System.Random random)
        {
            GrayboxSettings.FoliageSettings f = Settings.foliage;
            float r = Mathf.Lerp(f.shrubRadius.x, f.shrubRadius.y, (float)random.NextDouble()), turn = (float)random.NextDouble() * 360f;
            mesh.Frustum(Surface.Foliage, centre, 0.6f * r, r, 0f, 0.6f * r, 6, turn, top: false);
            mesh.Frustum(Surface.Foliage, centre, r, 0.1f, 0.6f * r, 1.3f * r, 6, turn + 30f, top: false);
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
            mesh.Box(Surface.Concrete, new Vector3(x0, 0f, z0), new Vector3(x1, h, z0 + rim), bottom: false);
            mesh.Box(Surface.Concrete, new Vector3(x0, 0f, z1 - rim), new Vector3(x1, h, z1), bottom: false);
            mesh.Box(Surface.Concrete, new Vector3(x0, 0f, z0 + rim), new Vector3(x0 + rim, h, z1 - rim), bottom: false, ends: false);
            mesh.Box(Surface.Concrete, new Vector3(x1 - rim, 0f, z0 + rim), new Vector3(x1, h, z1 - rim), bottom: false, ends: false);
            mesh.Box(Surface.Soil, new Vector3(x0 + rim, 0f, z0 + rim), new Vector3(x1 - rim, soil, z1 - rim), bottom: false, sides: false, ends: false);
            mesh.Box(Surface.Accent, new Vector3(x0 - 0.01f, 0f, z0 - 0.01f), new Vector3(x1 + 0.01f, 0.1f, z1 + 0.01f), top: false, bottom: false);
            int shrubs = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(x1 - x0, z1 - z0) / 1.2f));
            bool alongX = x1 - x0 >= z1 - z0;
            for (int i = 0; i < shrubs; i++)
            {
                float f = (i + 0.5f) / shrubs;
                Vector3 at = alongX ? new Vector3(Mathf.Lerp(x0 + 0.5f, x1 - 0.5f, f), soil, (z0 + z1) / 2f) : new Vector3((x0 + x1) / 2f, soil, Mathf.Lerp(z0 + 0.5f, z1 - 0.5f, f));
                if (tree && Mathf.Abs(f - 0.5f) < 0.2f) { continue; }
                Shrub(mesh, at, random);
            }
            mesh.AddTo(planter, name);
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
                GrayboxSettings.FoliageSettings f = Settings.foliage;
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
        /// down stay paved). Each lawn has grass tufts over it and, along its edges, the odd shrub. Raised a little, with
        /// no collider. Placed once the level's built (see Defer), before any trees put off after it; in chunks, so each
        /// mesh stays small and is culled on its own. spacing scales the cells (bigger for wide, sparse ground) and
        /// density the tufts.
        /// </summary>
        [Piece]
        public static void Lawns(Transform parent, UnityEngine.Rect area, float[] floors, int salt, float spacing = 1f, float density = 1f)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Lawns), parent, area, floors, salt, spacing, density);
            Defer(() =>
            {
                GrayboxSettings.FoliageSettings f = Settings.foliage;
                var random = new System.Random(f.seed + 100 + salt);
                float cell = f.lawnCell * spacing, half = cell / 2f, edge = f.lawnMargins.x, clear = f.lawnMargins.y, t = f.lawnThickness;
                int nx = Mathf.FloorToInt(area.width / cell), nz = Mathf.FloorToInt(area.height / cell);
                Transform lawns = Group(parent, "Lawns");
                bool FloorAt(float x, float z, float height) =>
                    FloorUnder(parent, new Vector2(x, z), height, out float under) && Mathf.Abs(under - height) < 0.06f;
                foreach (float floor in floors)
                {
                    var grass = new bool[nx, nz];
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
                        }
                    }
                    bool Grass(int i, int j) => i >= 0 && j >= 0 && i < nx && j < nz && grass[i, j];

                    const int chunk = 16;
                    for (int ci = 0; ci < nx; ci += chunk)
                    {
                        for (int cj = 0; cj < nz; cj += chunk)
                        {
                            var mesh = new GrayboxMesh();
                            var tufts = new GrayboxMesh();
                            bool any = false, anyTufts = false;
                            for (int i = ci; i < Mathf.Min(ci + chunk, nx); i++)
                            {
                                for (int j = cj; j < Mathf.Min(cj + chunk, nz); j++)
                                {
                                    if (!grass[i, j]) { continue; }
                                    any = true;
                                    float x0 = area.xMin + i * cell, z0 = area.yMin + j * cell, x1 = x0 + cell, z1 = z0 + cell, top = floor + t;
                                    mesh.Quad(Surface.Grass, Vector3.up, new(x0, top, z0), new(x1, top, z0), new(x1, top, z1), new(x0, top, z1));
                                    // Sides only at the lawn's edge, where the cell beside it is paving.
                                    if (!Grass(i - 1, j)) { mesh.Quad(Surface.Grass, Vector3.left, new(x0, floor, z0), new(x0, top, z0), new(x0, top, z1), new(x0, floor, z1)); }
                                    if (!Grass(i + 1, j)) { mesh.Quad(Surface.Grass, Vector3.right, new(x1, floor, z0), new(x1, floor, z1), new(x1, top, z1), new(x1, top, z0)); }
                                    if (!Grass(i, j - 1)) { mesh.Quad(Surface.Grass, Vector3.back, new(x0, floor, z0), new(x1, floor, z0), new(x1, top, z0), new(x0, top, z0)); }
                                    if (!Grass(i, j + 1)) { mesh.Quad(Surface.Grass, Vector3.forward, new(x0, floor, z1), new(x0, top, z1), new(x1, top, z1), new(x1, floor, z1)); }

                                    float perCell = f.tuftDensity * density * cell * cell;
                                    int count = Mathf.FloorToInt(perCell) + (random.NextDouble() < perCell - Mathf.Floor(perCell) ? 1 : 0);
                                    for (int k = 0; k < count; k++)
                                    {
                                        Tuft(tufts, new Vector3(x0 + (float)random.NextDouble() * cell, top, z0 + (float)random.NextDouble() * cell), random);
                                        anyTufts = true;
                                    }
                                    bool border = !Grass(i - 1, j) || !Grass(i + 1, j) || !Grass(i, j - 1) || !Grass(i, j + 1);
                                    if (border && random.NextDouble() < f.borderShrubChance)
                                    {
                                        Shrub(mesh, new Vector3((x0 + x1) / 2f, top, (z0 + z1) / 2f), random);
                                    }
                                }
                            }
                            if (!any) { continue; }
                            var lawn = new GameObject($"Lawn (+{floor:0})");
                            lawn.transform.SetParent(lawns, false);
                            mesh.AddTo(lawn, lawn.name);
                            MakeStatic(lawn, occluder: false);
                            if (anyTufts) { GrassTufts(lawn.transform, tufts); }
                        }
                    }
                }
            });
        }

        /// <summary>
        /// A lawn chunk's grass tufts, as an object of their own under it: casting no shadow (unless the settings say),
        /// and, through a LOD group, not drawn beyond about the settings' tuft draw distance (with a 60° field of view, at
        /// the LOD bias of the quality level the level's built with). The lawn itself is still drawn.
        /// </summary>
        private static void GrassTufts(Transform lawn, GrayboxMesh tufts)
        {
            GrayboxSettings.FoliageSettings f = Settings.foliage;
            var grass = new GameObject("Grass Tufts");
            grass.transform.SetParent(lawn, false);
            tufts.AddTo(grass, grass.name, uv2: false);
            var renderer = grass.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = f.tuftsCastShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            var lod = grass.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.5f, new Renderer[] { renderer }) });
            lod.RecalculateBounds();
            // A LOD's drawn while the group's size over the height of the view at its distance (times the quality
            // settings' LOD bias, 2 on PC here) is above its threshold: so the threshold is that ratio at the draw distance.
            float viewHeight = 2f * f.tuftDrawDistance * Mathf.Tan(30f * Mathf.Deg2Rad);
            lod.SetLODs(new[] { new LOD(Mathf.Clamp(lod.size / viewHeight * QualitySettings.lodBias, 0.001f, 1f), new Renderer[] { renderer }) });
            MakeStatic(grass, occluder: false);
        }

        /// <summary>A grass tuft in mesh, on a lawn at "at": the settings' number of thin blades, each leaning its own way and seen from both sides.</summary>
        private static void Tuft(GrayboxMesh mesh, Vector3 at, System.Random random)
        {
            GrayboxSettings.FoliageSettings f = Settings.foliage;
            for (int b = 0; b < f.tuftBlades; b++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f, height = Mathf.Lerp(f.tuftHeight.x, f.tuftHeight.y, (float)random.NextDouble());
                var across = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.03f;
                var lean = new Vector3(-across.z, 0f, across.x).normalized * ((float)random.NextDouble() * 0.12f);
                Vector3 root = at + new Vector3(((float)random.NextDouble() - 0.5f) * 0.12f, 0f, ((float)random.NextDouble() - 0.5f) * 0.12f);
                Vector3 p = root - across, q = root + across, tip = root + lean + Vector3.up * height;
                Vector3 normal = Vector3.Cross(q - p, tip - p).normalized;
                foreach (Vector3 side in new[] { normal, -normal })
                {
                    int i = mesh.Face(side, new[] { p, q, tip });
                    mesh.Triangle(Surface.Grass, i, i + 1, i + 2, side);
                }
            }
        }
    }
}
