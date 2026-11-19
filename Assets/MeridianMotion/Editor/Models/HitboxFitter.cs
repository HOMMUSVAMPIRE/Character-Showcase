using System.Collections.Generic;
using System.Linq;
using MeridianMotion.Characters;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    /// <summary>
    /// Fits a model's hitboxes (see CharacterHitboxes): a capsule on each of its body's bones, fitted to the part of its
    /// mesh that bone carries. Each vertex goes to the bone weighted most on it, or, if that bone gets no hitbox, to its
    /// nearest ancestor that does (a hand's fingers to the hand). The vertices are taken in their bind pose, in the bone's
    /// own space (through the mesh's bind poses), so the model's pose doesn't matter. Each capsule runs along whichever
    /// of the bone's mesh's principal axes, or the way to a child bone with a hitbox, makes it smallest, and covers most of
    /// it: its ends trimmed of the furthest few vertices, its radius reaching most of them, so it's a little inside the
    /// skin rather than a box round everything. Run by Prepare Humanoid Models (and the model generators) as they save a model's
    /// prefab, and by Fit Hitboxes (Tools > Meridian Motion) for every model there is.
    /// </summary>
    public static class HitboxFitter
    {
        private const string LogName = "Fit Hitboxes";

        /// <summary>
        /// For a model that isn't a humanoid, with no bones listed: the share of its vertices a bone must carry to get a
        /// hitbox of its own (fewer go to its nearest ancestor that gets one).
        /// </summary>
        public const float MinimumMeshShare = 0.015f;

        // Fewer vertices than this can't be fitted: they go to the bone's ancestor's hitbox.
        private const int MinimumVertices = 8;
        // The share of a bone's vertices its capsule's radius reaches, and of those beyond each of its ends left out.
        private const float RadiusShare = 0.9f, EndTrim = 0.02f;

        /// <summary>A humanoid's body bones, given a hitbox each (any it doesn't have are left out).</summary>
        public static readonly HumanBodyBones[] HumanoidBones =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
        };

        [MenuItem("Tools/Meridian Motion/Prepare/Fit Hitboxes", priority = 34)]
        public static void FitAllFromMenu()
        {
            int fitted = FitAll(out int failed);
            EditorUtility.DisplayDialog(LogName, $"Fitted the hitboxes of {fitted} model(s)" + (failed > 0 ? $"; {failed} couldn't be (see the Console)." : "."), "OK");
        }

        /// <summary>For -executeMethod: fits every model's hitboxes (see FitAll), and exits, with an error code if any failed.</summary>
        public static void FitAllFromCommandLine()
        {
            FitAll(out int failed);
            CommandLine.Finish(failed == 0);
        }

        /// <summary>
        /// Fits the hitboxes of every model a model source names (each PrefabModelSource's model prefab: the
        /// prepared humanoids, the mannequin and the fox), keeping each one's settings. Returns how many were fitted.
        /// </summary>
        public static int FitAll(out int failed)
        {
            var prefabs = new SortedSet<string>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(PrefabModelSource)}"))
            {
                var source = AssetDatabase.LoadAssetAtPath<PrefabModelSource>(AssetDatabase.GUIDToAssetPath(guid));
                if (source != null && source.ModelPrefab != null) { prefabs.Add(AssetDatabase.GetAssetPath(source.ModelPrefab)); }
            }
            int fitted = 0;
            failed = 0;
            foreach (string path in prefabs)
            {
                if (FitPrefab(path)) { fitted++; }
                else { failed++; }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[{LogName}] Fitted the hitboxes of {fitted} model(s)" + (failed > 0 ? $"; {failed} failed." : "."));
            return fitted;
        }

        /// <summary>Fits the hitboxes of the model prefab at the path (see Fit), and saves it. Returns whether it could.</summary>
        public static bool FitPrefab(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (!Fit(root)) { return false; }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return true;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>
        /// Fits hitboxes to the model (an instance, or a prefab's contents being edited), replacing any it has: on the bones
        /// listed (by name), or else on the bones its CharacterHitboxes lists, or else on its body's bones (see
        /// CharacterHitboxes' Hitbox Bones). Its CharacterHitboxes is added if it has none, its settings taken from
        /// settingsFrom's, if given (the model's earlier prefab). Returns whether any hitboxes could be fitted.
        /// </summary>
        public static bool Fit(GameObject model, IReadOnlyList<string> boneNames = null, CharacterHitboxes settingsFrom = null)
        {
            if (!model.TryGetComponent(out CharacterHitboxes hitboxes)) { hitboxes = model.AddComponent<CharacterHitboxes>(); }
            var settings = new SerializedObject(hitboxes);
            if (settingsFrom != null && settingsFrom != hitboxes)
            {
                var from = new SerializedObject(settingsFrom);
                foreach (string field in new[] { "response", "reportStaticContacts", "hitboxBones" }) { settings.CopyFromSerializedProperty(from.FindProperty(field)); }
            }
            if (boneNames != null)
            {
                SerializedProperty listed = settings.FindProperty("hitboxBones");
                listed.arraySize = boneNames.Count;
                for (int i = 0; i < boneNames.Count; i++) { listed.GetArrayElementAtIndex(i).stringValue = boneNames[i]; }
            }
            settings.ApplyModifiedPropertiesWithoutUndo();

            foreach (BoneHitbox old in model.GetComponentsInChildren<BoneHitbox>(true)) { Object.DestroyImmediate(old.gameObject); }

            SkinnedMeshRenderer[] renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh != null).ToArray();
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"[{LogName}] '{model.name}' has no skinned mesh to fit hitboxes to.", model);
                hitboxes.SetHitboxes(Enumerable.Empty<BoneHitbox>());
                return false;
            }
            HashSet<Transform> chosen = ChooseBones(model, hitboxes.HitboxBones, renderers);
            Dictionary<Transform, List<Vector3>> points = CollectPoints(chosen, renderers);

            var made = new List<BoneHitbox>();
            foreach (Transform bone in OrderedByDepth(chosen))
            {
                if (!points.TryGetValue(bone, out List<Vector3> carried) || carried.Count < MinimumVertices) { continue; }
                made.Add(MakeHitbox(bone, carried, ChildDirections(bone, chosen, renderers)));
            }
            hitboxes.SetHitboxes(made);
            EditorUtility.SetDirty(hitboxes);
            if (made.Count == 0) { Debug.LogWarning($"[{LogName}] No bone of '{model.name}' carries enough of its mesh for a hitbox.", model); }
            return made.Count > 0;
        }

        // ---- Which bones ----

        private static HashSet<Transform> ChooseBones(GameObject model, IReadOnlyList<string> names, SkinnedMeshRenderer[] renderers)
        {
            var chosen = new HashSet<Transform>();
            Transform[] all = model.GetComponentsInChildren<Transform>(true);
            if (names != null && names.Count > 0)
            {
                foreach (string name in names)
                {
                    Transform bone = all.FirstOrDefault(t => t.name == name);
                    if (bone != null) { chosen.Add(bone); }
                    else { Debug.LogWarning($"[{LogName}] '{model.name}' has no bone '{name}' to give a hitbox.", model); }
                }
                return chosen;
            }
            Animator animator = model.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman)
            {
                foreach (HumanBodyBones human in HumanoidBones)
                {
                    Transform bone = animator.GetBoneTransform(human);
                    if (bone != null) { chosen.Add(bone); }
                }
                return chosen;
            }
            // Not a humanoid: every bone carrying enough of the mesh.
            var counts = new Dictionary<Transform, int>();
            int total = 0;
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                Transform[] bones = renderer.bones;
                foreach (BoneWeight weight in renderer.sharedMesh.boneWeights)
                {
                    Transform bone = weight.boneIndex0 < bones.Length ? bones[weight.boneIndex0] : null;
                    if (bone == null) { continue; }
                    counts[bone] = counts.TryGetValue(bone, out int n) ? n + 1 : 1;
                    total++;
                }
            }
            int needed = Mathf.Max(MinimumVertices, Mathf.CeilToInt(MinimumMeshShare * total));
            foreach (var (bone, count) in counts.Select(p => (p.Key, p.Value))) { if (count >= needed) { chosen.Add(bone); } }
            return chosen;
        }

        /// <summary>The bone itself if chosen, else its nearest chosen ancestor (null if none).</summary>
        private static Transform OwnerOf(Transform bone, HashSet<Transform> chosen)
        {
            for (Transform t = bone; t != null; t = t.parent) { if (chosen.Contains(t)) { return t; } }
            return null;
        }

        private static IEnumerable<Transform> OrderedByDepth(IEnumerable<Transform> bones) =>
            bones.OrderBy(b => { int depth = 0; for (Transform t = b; t != null; t = t.parent) { depth++; } return depth; }).ThenBy(b => b.name);

        // ---- The mesh each carries ----

        /// <summary>Each chosen bone's vertices, in its own space in the bind pose.</summary>
        private static Dictionary<Transform, List<Vector3>> CollectPoints(HashSet<Transform> chosen, SkinnedMeshRenderer[] renderers)
        {
            var points = new Dictionary<Transform, List<Vector3>>();
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                Mesh mesh = renderer.sharedMesh;
                Transform[] bones = renderer.bones;
                Matrix4x4[] bindposes = mesh.bindposes;
                Vector3[] vertices = mesh.vertices;
                BoneWeight[] weights = mesh.boneWeights;
                if (weights.Length != vertices.Length) { continue; }
                for (int v = 0; v < vertices.Length; v++)
                {
                    int index = weights[v].boneIndex0;
                    if (index >= bones.Length || bones[index] == null) { continue; }
                    Transform owner = OwnerOf(bones[index], chosen);
                    if (owner == null) { continue; }
                    if (!points.TryGetValue(owner, out List<Vector3> list)) { points[owner] = list = new List<Vector3>(); }
                    list.Add(ToBoneSpace(owner, vertices[v], renderer, bones, bindposes));
                }
            }
            return points;
        }

        /// <summary>
        /// A vertex of the renderer's mesh in the bone's space, in the bind pose: through the bone's bind pose if the
        /// renderer is skinned to it, else (a bone carrying none of the mesh itself) through the model's pose now.
        /// </summary>
        private static Vector3 ToBoneSpace(Transform bone, Vector3 vertex, SkinnedMeshRenderer renderer, Transform[] bones, Matrix4x4[] bindposes)
        {
            int index = System.Array.IndexOf(bones, bone);
            if (index >= 0 && index < bindposes.Length) { return bindposes[index].MultiplyPoint3x4(vertex); }
            return bone.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
        }

        /// <summary>The ways (in the bone's space, in the bind pose) to each of its nearest descendants with a hitbox.</summary>
        private static List<Vector3> ChildDirections(Transform bone, HashSet<Transform> chosen, SkinnedMeshRenderer[] renderers)
        {
            var directions = new List<Vector3>();
            foreach (Transform child in chosen)
            {
                if (child == bone || child.parent == null || OwnerOf(child.parent, chosen) != bone) { continue; }
                Vector3 toChild = BindPositionIn(bone, child, renderers);
                if (toChild.sqrMagnitude > 1e-10f) { directions.Add(toChild.normalized); }
            }
            return directions;
        }

        /// <summary>Where the other bone's origin is in the bone's space, in the bind pose (or, if unskinned, as posed now).</summary>
        private static Vector3 BindPositionIn(Transform bone, Transform other, SkinnedMeshRenderer[] renderers)
        {
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                Transform[] bones = renderer.bones;
                int a = System.Array.IndexOf(bones, bone), b = System.Array.IndexOf(bones, other);
                Matrix4x4[] bindposes = renderer.sharedMesh.bindposes;
                if (a < 0 || b < 0 || a >= bindposes.Length || b >= bindposes.Length) { continue; }
                // The other bone's origin in the mesh's space, then in this bone's.
                Vector3 inMesh = bindposes[b].inverse.MultiplyPoint3x4(Vector3.zero);
                return bindposes[a].MultiplyPoint3x4(inMesh);
            }
            return bone.InverseTransformPoint(other.position);
        }

        // ---- The capsule ----

        private static BoneHitbox MakeHitbox(Transform bone, List<Vector3> points, List<Vector3> childDirections)
        {
            Vector3 centroid = Vector3.zero;
            foreach (Vector3 p in points) { centroid += p; }
            centroid /= points.Count;

            var axes = new List<Vector3>(PrincipalAxes(points, centroid));
            axes.AddRange(childDirections);
            Capsule best = default;
            bool found = false;
            foreach (Vector3 axis in axes)
            {
                Capsule fitted = FitAlong(points, centroid, axis);
                if (!found || fitted.Volume < best.Volume) { best = fitted; found = true; }
            }

            var holder = new GameObject($"Hitbox ({bone.name})");
            holder.transform.SetParent(bone, false);
            holder.transform.localPosition = best.center;
            holder.transform.localRotation = Quaternion.FromToRotation(Vector3.up, best.axis);
            holder.transform.localScale = Vector3.one;
            var capsule = holder.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.center = Vector3.zero;
            capsule.radius = best.radius;
            capsule.height = best.height;
            return holder.AddComponent<BoneHitbox>();
        }

        private struct Capsule
        {
            public Vector3 center, axis;
            public float radius, height;
            public float Volume => Mathf.PI * radius * radius * Mathf.Max(0f, height - 2f * radius) + 4f / 3f * Mathf.PI * radius * radius * radius;
        }

        /// <summary>The capsule along axis covering the points: trimmed of the furthest beyond each end, reaching most of them round.</summary>
        private static Capsule FitAlong(List<Vector3> points, Vector3 centroid, Vector3 axis)
        {
            axis.Normalize();
            var along = new float[points.Count];
            var across = new float[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 offset = points[i] - centroid;
                along[i] = Vector3.Dot(offset, axis);
                across[i] = (offset - along[i] * axis).magnitude;
            }
            System.Array.Sort(along);
            System.Array.Sort(across);
            float start = Share(along, EndTrim), end = Share(along, 1f - EndTrim);
            float radius = Mathf.Max(1e-6f, Share(across, RadiusShare));
            return new Capsule
            {
                center = centroid + axis * (0.5f * (start + end)),
                axis = axis,
                radius = radius,
                height = Mathf.Max(end - start, 2f * radius),
            };
        }

        /// <summary>The value the given share of the way through sorted values.</summary>
        private static float Share(float[] sorted, float share) => sorted[Mathf.Clamp(Mathf.RoundToInt(share * (sorted.Length - 1)), 0, sorted.Length - 1)];

        /// <summary>The points' three principal axes (the eigenvectors of their covariance), by Jacobi rotations.</summary>
        private static IEnumerable<Vector3> PrincipalAxes(List<Vector3> points, Vector3 centroid)
        {
            var a = new float[3, 3];
            foreach (Vector3 p in points)
            {
                Vector3 d = p - centroid;
                for (int i = 0; i < 3; i++) { for (int j = 0; j < 3; j++) { a[i, j] += d[i] * d[j]; } }
            }
            var v = new float[3, 3] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
            for (int sweep = 0; sweep < 20; sweep++)
            {
                for (int p = 0; p < 2; p++)
                {
                    for (int q = p + 1; q < 3; q++)
                    {
                        if (Mathf.Abs(a[p, q]) < 1e-12f) { continue; }
                        float theta = 0.5f * Mathf.Atan2(2f * a[p, q], a[q, q] - a[p, p]);
                        float c = Mathf.Cos(theta), s = Mathf.Sin(theta);
                        for (int k = 0; k < 3; k++)
                        {
                            float akp = a[k, p], akq = a[k, q];
                            a[k, p] = c * akp - s * akq;
                            a[k, q] = s * akp + c * akq;
                        }
                        for (int k = 0; k < 3; k++)
                        {
                            float apk = a[p, k], aqk = a[q, k];
                            a[p, k] = c * apk - s * aqk;
                            a[q, k] = s * apk + c * aqk;
                        }
                        for (int k = 0; k < 3; k++)
                        {
                            float vkp = v[k, p], vkq = v[k, q];
                            v[k, p] = c * vkp - s * vkq;
                            v[k, q] = s * vkp + c * vkq;
                        }
                    }
                }
            }
            for (int column = 0; column < 3; column++)
            {
                var axis = new Vector3(v[0, column], v[1, column], v[2, column]);
                if (axis.sqrMagnitude > 1e-8f) { yield return axis.normalized; }
            }
        }
    }
}
