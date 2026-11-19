using System.Collections.Generic;
using System.Linq;
using System.Text;
using MeridianMotion.Characters;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.EditorTools
{
    public static partial class GenericRigModelImporter
    {
        // ---- Guessing a setup from the model's names ----

        // Words in bone names: a foot, the end of a leg; and bones that aren't really the skeleton's (helpers, ends).
        private static readonly string[] FootWords = { "foot", "feet", "paw", "hoof", "hand", "ankle", "wrist" };
        private static readonly string[] NotBoneWords = { "toe", "toes", "finger", "end", "nub", "tip", "ik", "target", "pole", "twist", "roll", "ctrl", "control", "helper" };
        private static readonly string[] FrontWords = { "front", "fore", "forearm", "arm", "hand", "wrist", "shoulder" };
        private static readonly string[] HindWords = { "hind", "rear", "back", "thigh", "hip" };
        private static readonly string[] RootWords = { "hips", "hip", "pelvis", "pelvic" };

        /// <summary>
        /// The slots of the generic rig layout (Assets/Animation/Generic Rig Layout.asset), each with the words a clip's name has
        /// to have to play it (any one set of them), most particular first; the slots are filled in this order, each clip
        /// once, so "Crouch Walk" is taken before "Walk" and "Swim Idle" before "Idle".
        /// </summary>
        private static readonly (string slot, string[][] words)[] SlotWords =
        {
            ("Swim Idle", new[] { new[] { "swim", "idle" }, new[] { "tread" }, new[] { "float" } }),
            ("Swim", new[] { new[] { "swim" }, new[] { "paddle" } }),
            ("Crouch Idle", new[] { new[] { "crouch", "idle" }, new[] { "sneak", "idle" }, new[] { "stalk", "idle" } }),
            ("Crouch Walk", new[] { new[] { "crouch" }, new[] { "sneak" }, new[] { "stalk" }, new[] { "crawl" } }),
            ("Jump Fall", new[] { new[] { "fall" }, new[] { "jump", "down" }, new[] { "air" } }),
            ("Jump Rise", new[] { new[] { "jump", "up" }, new[] { "jump", "rise" }, new[] { "jump", "start" }, new[] { "rise" }, new[] { "jump" } }),
            ("Turn Step", new[] { new[] { "turn" } }),
            ("Sprint", new[] { new[] { "gallop" }, new[] { "sprint" }, new[] { "dash" }, new[] { "run", "fast" } }),
            ("Run", new[] { new[] { "run" }, new[] { "trot" }, new[] { "jog" }, new[] { "canter" } }),
            ("Walk", new[] { new[] { "walk" } }),
            ("Idle", new[] { new[] { "idle" }, new[] { "stand" }, new[] { "breathe" }, new[] { "breath" }, new[] { "rest" } }),
        };

        /// <summary>
        /// Fills a new setup from its model's bone and clip names, as most rigs name them (Hips or Pelvis; Thigh, Shin and
        /// Foot, or UpperLeg, LowerLeg and Paw; Front/Fore and Hind/Back/Rear; _L, .L, Left, L_ and the like), and where
        /// names don't say, from where the bones are (a foot ahead of the hips is a front one; left of it, a left one):
        /// - its legs: each foot (a Foot, Paw, Hoof or Hand bone), the bone above it its lower bone and the one above
        ///   that its upper; its middle joint's bend as the model stands, and its paw's contact under its toes;
        /// - its root bone (the hips), its eyes on its head bone, its neck and tail turned against the pitch, its spine,
        ///   neck, head and tail bent into turns;
        /// - an animation set starting from the generic rig layout, its slots filled with the clips whose names say them
        ///   (Idle, Walk, Run or Trot, Gallop or Sprint, Jump, Fall, Crouch, Swim...), from its model and its folder.
        /// Its hitboxes go on every bone carrying enough of its mesh. What it can't find is listed in Unfound when it's
        /// prepared.
        /// </summary>
        public static void Guess(GenericRigSetup setup)
        {
            GameObject model = setup.model;
            if (model == null) { return; }
            Transform[] bones = SkeletonOf(model);
            Vector3 Local(Transform t) => model.transform.InverseTransformPoint(t.position);

            // Its feet, then its legs above them.
            var legs = new List<(GenericRigSetup.Leg leg, bool front, bool left)>();
            var feet = bones.Where(b => HasAny(Tokens(b.name), FootWords) && !HasAny(Tokens(b.name), NotBoneWords)
                                        && b.parent != null && b.parent.parent != null)
                .ToList();
            // Only the topmost of a foot's bones (a Foot over a Paw counts once).
            feet = feet.Where(f => !feet.Any(other => other != f && f.IsChildOf(other))).ToList();
            Transform rootGuess = bones.Where(b => HasAny(Tokens(b.name), RootWords) && !HasAny(Tokens(b.name), NotBoneWords))
                .OrderBy(Depth).FirstOrDefault();
            foreach (Transform foot in feet)
            {
                Transform lower = foot.parent, upper = lower.parent;
                string[] words = Tokens(foot.name).Concat(Tokens(lower.name)).Concat(Tokens(upper.name)).ToArray();
                Vector3 footAt = Local(foot);
                bool? namedFront = HasAny(words, new[] { "front", "fore", "forearm" }) ? true : HasAny(words, new[] { "hind", "rear", "back" }) ? false : null;
                bool front = namedFront ?? (HasAny(words, FrontWords) && !HasAny(words, HindWords)
                    || (!HasAny(words, FrontWords) && !HasAny(words, HindWords) && rootGuess != null && footAt.z > Local(rootGuess).z));
                bool left = SideOf(words) ?? footAt.x < 0f;
                legs.Add((new GenericRigSetup.Leg
                {
                    upper = upper.name, lower = lower.name, foot = foot.name,
                    bendDirection = BendOf(Local(upper), Local(lower), footAt, front),
                    contact = ContactOf(model, foot),
                    wallClearance = new LegPlacementIK.Leg().wallClearance,
                }, front, left));
            }
            // Front legs first, then hind; left before right (as the fox's).
            setup.legs = legs.OrderBy(l => l.front ? 0 : 1).ThenBy(l => l.left ? 0 : 1).Select(l => l.leg).ToArray();

            // Its root: the hips, else where its legs meet, else the skeleton's top.
            Transform root = rootGuess ?? CommonAncestor(legs.Select(l => Bone(model, l.leg.upper)).Where(b => b != null).ToList(), model.transform)
                             ?? model.GetComponentInChildren<SkinnedMeshRenderer>(true)?.rootBone;
            setup.rootBone = root != null ? root.name : "";

            // Its head and eyes, neck, spine and tail.
            Transform head = Named(bones, "head");
            setup.eyeBone = head != null ? head.name : "";
            if (head != null)
            {
                Bounds? headMesh = MeshBoundsOf(model, b => b == head || b.IsChildOf(head));
                Vector3 at = Local(head);
                setup.eyePosition = headMesh is Bounds mesh
                    ? new Vector3(0f, mesh.center.y, 0.5f * (at.z + mesh.max.z))
                    : new Vector3(0f, at.y, at.z);
            }
            Transform neck = Named(bones, "neck");
            Transform[] tail = bones.Where(b => Tokens(b.name).Contains("tail") && !HasAny(Tokens(b.name), NotBoneWords)).OrderBy(Depth).ToArray();
            var counterPitch = new List<GenericRigSetup.CounterPitchBone>();
            // The tail mostly going up (pitched up a slope with the body, it would swing down into it), not going down; the
            // neck by half (the head looks ahead).
            if (tail.Length > 0) { counterPitch.Add(new GenericRigSetup.CounterPitchBone { bone = tail[0].name, noseUpShare = 0.8f, noseDownShare = 0f }); }
            if (neck != null) { counterPitch.Add(new GenericRigSetup.CounterPitchBone { bone = neck.name, noseUpShare = 0.5f, noseDownShare = 0.5f }); }
            setup.counterPitchBones = counterPitch.ToArray();

            // Bent into a turn: the spine (between its root and its neck or head) toward it, then the neck and head, and the
            // tail's first two bones away.
            var bend = new List<GenericRigSetup.TurnBendBone>();
            Transform spineTop = neck != null ? neck : head;
            if (root != null && spineTop != null && spineTop.IsChildOf(root))
            {
                var spine = new List<Transform>();
                for (Transform b = spineTop.parent; b != null && b != root; b = b.parent) { spine.Insert(0, b); }
                foreach (Transform b in spine) { bend.Add(new GenericRigSetup.TurnBendBone { bone = b.name, share = 0.55f / spine.Count }); }
            }
            if (neck != null) { bend.Add(new GenericRigSetup.TurnBendBone { bone = neck.name, share = 0.25f }); }
            if (head != null) { bend.Add(new GenericRigSetup.TurnBendBone { bone = head.name, share = 0.2f }); }
            float[] tailShares = { -0.4f, -0.3f };
            for (int i = 0; i < tail.Length && i < tailShares.Length; i++) { bend.Add(new GenericRigSetup.TurnBendBone { bone = tail[i].name, share = tailShares[i] }); }
            setup.turnBendBones = bend.ToArray();
            setup.hitboxBones = System.Array.Empty<string>();

            setup.animationSet = GuessAnimationSet(model, setup.name);
        }

        /// <summary>An animation set starting from the generic rig layout, its slots filled from its clips' names (see SlotWords).</summary>
        private static CharacterAnimationSet GuessAnimationSet(GameObject model, string name)
        {
            var layout = AssetDatabase.LoadAssetAtPath<AnimatorLayout>(StarterLayoutPath);
            if (layout == null) { return null; }
            string modelPath = AssetDatabase.GetAssetPath(model);
            IEnumerable<AnimationClip> clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>();
            string folder = FolderOf(modelPath);
            clips = clips.Concat(AssetDatabase.FindAssets("t:AnimationClip", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>()));
            List<AnimationClip> unused = clips.Where(c => !c.name.StartsWith("__preview__")).Distinct().OrderBy(c => c.name, System.StringComparer.Ordinal).ToList();
            var slots = new List<CharacterAnimationSet.SlotClip>();
            foreach (var (slot, wordSets) in SlotWords)
            {
                if (layout.clips.All(s => s.name != slot)) { continue; }
                foreach (string[] words in wordSets)
                {
                    AnimationClip clip = unused.Where(c => words.All(Tokens(c.name).Contains)).OrderBy(c => c.name.Length).FirstOrDefault();
                    if (clip == null) { continue; }
                    slots.Add(new CharacterAnimationSet.SlotClip { slot = slot, clip = clip });
                    unused.Remove(clip);
                    break;
                }
            }
            var set = ScriptableObject.CreateInstance<CharacterAnimationSet>();
            set.layout = layout;
            set.clips = slots.ToArray();
            set.notes = "Made by Prepare Generic Rig Models from its clips' names: check each slot plays the clip it should.";
            return set;
        }

        // ---- Names ----

        /// <summary>
        /// The words of a bone's or clip's name, lower case: split at anything that isn't a letter or digit, between a
        /// lower-case letter and a capital, and between letters and digits, after any "prefix:" or "Armature|" before it
        /// ("mixamorig:LeftUpLeg" is left, up, leg; "FrontPaw_L" front, paw, l; "thigh.L" thigh, l).
        /// </summary>
        internal static string[] Tokens(string name)
        {
            int cut = Mathf.Max(name.LastIndexOf(':'), name.LastIndexOf('|'));
            if (cut >= 0) { name = name.Substring(cut + 1); }
            var words = new List<string>();
            var word = new StringBuilder();
            void End()
            {
                if (word.Length > 0) { words.Add(word.ToString().ToLowerInvariant()); }
                word.Clear();
            }
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (!char.IsLetterOrDigit(c)) { End(); continue; }
                if (word.Length > 0)
                {
                    char last = word[word.Length - 1];
                    bool split = (char.IsLower(last) && char.IsUpper(c)) || (char.IsLetter(last) != char.IsLetter(c))
                                 // An acronym before a word: "LThigh" is L, Thigh.
                                 || (char.IsUpper(last) && char.IsUpper(c) && i + 1 < name.Length && char.IsLower(name[i + 1]));
                    if (split) { End(); }
                }
                word.Append(c);
            }
            End();
            return words.ToArray();
        }

        private static bool HasAny(string[] tokens, string[] words) => tokens.Any(words.Contains);

        /// <summary>Which side the words say (true left, false right), or null if they don't.</summary>
        private static bool? SideOf(string[] tokens)
        {
            if (tokens.Any(t => t == "l" || t == "left" || t == "lf" || t == "lt")) { return true; }
            if (tokens.Any(t => t == "r" || t == "right" || t == "rt")) { return false; }
            return null;
        }

        /// <summary>The topmost bone with the word in its name (not a helper or end bone), or null.</summary>
        private static Transform Named(Transform[] bones, string word) =>
            bones.Where(b => Tokens(b.name).Contains(word) && !HasAny(Tokens(b.name), NotBoneWords)).OrderBy(Depth).FirstOrDefault();

        private static int Depth(Transform t)
        {
            int depth = 0;
            for (; t.parent != null; t = t.parent) { depth++; }
            return depth;
        }

        /// <summary>The bones of the model's skeleton: every transform its skinned meshes are skinned to, and those between them.</summary>
        private static Transform[] SkeletonOf(GameObject model)
        {
            var skinned = new HashSet<Transform>(model.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.bones).Where(b => b != null));
            return model.GetComponentsInChildren<Transform>(true)
                .Where(t => skinned.Contains(t) || skinned.Any(s => s.IsChildOf(t) && t != model.transform && t.GetComponent<SkinnedMeshRenderer>() == null))
                .ToArray();
        }

        private static Transform CommonAncestor(List<Transform> bones, Transform top)
        {
            if (bones.Count == 0) { return null; }
            for (Transform candidate = bones[0].parent; candidate != null && candidate != top; candidate = candidate.parent)
            {
                if (bones.All(b => b.IsChildOf(candidate))) { return candidate; }
            }
            return null;
        }

        /// <summary>
        /// The way a leg's middle joint points as the model stands (straight ahead or behind, in the model's space): the
        /// way it's bent off the line from its hip to its foot; a leg standing straight bends as a front leg's elbow does
        /// (back) or a hind leg's stifle (forward).
        /// </summary>
        private static Vector3 BendOf(Vector3 upper, Vector3 lower, Vector3 foot, bool front)
        {
            Vector3 along = foot - upper;
            Vector3 off = lower - (upper + Vector3.Project(lower - upper, along));
            if (off.magnitude > 0.05f * along.magnitude && Mathf.Abs(off.z) > 1e-4f) { return off.z > 0f ? Vector3.forward : Vector3.back; }
            return front ? Vector3.back : Vector3.forward;
        }

        /// <summary>Where a paw touches the ground standing, in the model's space: under its furthest-forward toe bone, or under the foot.</summary>
        private static Vector3 ContactOf(GameObject model, Transform foot)
        {
            Vector3 at = model.transform.InverseTransformPoint(foot.position);
            float ahead = at.z;
            foreach (Transform toe in foot.GetComponentsInChildren<Transform>(true))
            {
                ahead = Mathf.Max(ahead, model.transform.InverseTransformPoint(toe.position).z);
            }
            return new Vector3(at.x, 0f, ahead);
        }

        /// <summary>The bounds (in the model's space, in its bind pose) of the mesh the bones that pass carry, or null if none.</summary>
        internal static Bounds? MeshBoundsOf(GameObject model, System.Func<Transform, bool> carriedBy) => MeshBoundsOf(model, (bone, _) => carriedBy(bone));

        /// <summary>
        /// The bounds (in the model's space, in its bind pose) of the mesh's points that pass, each with the bone carrying
        /// it most (null for none) and where it is, or null if none pass.
        /// </summary>
        internal static Bounds? MeshBoundsOf(GameObject model, System.Func<Transform, Vector3, bool> carriedBy)
        {
            Bounds? bounds = null;
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null) { continue; }
                Transform[] bones = renderer.bones;
                Matrix4x4[] bindposes = mesh.bindposes;
                Vector3[] vertices = mesh.vertices;
                BoneWeight[] weights = mesh.boneWeights;
                for (int v = 0; v < vertices.Length; v++)
                {
                    int index = v < weights.Length ? weights[v].boneIndex0 : -1;
                    Transform bone = index >= 0 && index < bones.Length ? bones[index] : null;
                    // In the model's space, in the bind pose: through the bone's bind pose where it's skinned to one.
                    Vector3 point = bone != null && index < bindposes.Length
                        ? model.transform.InverseTransformPoint(bone.TransformPoint(bindposes[index].MultiplyPoint3x4(vertices[v])))
                        : model.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertices[v]));
                    if (!carriedBy(bone, point)) { continue; }
                    if (bounds is Bounds b) { b.Encapsulate(point); bounds = b; }
                    else { bounds = new Bounds(point, Vector3.zero); }
                }
            }
            return bounds;
        }
    }
}
