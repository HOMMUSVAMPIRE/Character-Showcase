using System.Collections.Generic;
using System.Linq;
using Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    public static partial class CreatureModelImporter
    {
        /// <summary>The Animator parameter a creature's standing turn blends by (how fast it turns, degrees a second).</summary>
        public const string TurnSpeedParameter = "TurnSpeed";

        // ---- The model prefab ----

        /// <summary>
        /// The creature's model prefab, made anew from its model: its Animator playing the controller (no root motion: the
        /// movement moves it), an Eyes point on its eyes bone, its paw placement and its hitboxes (with the settings its
        /// earlier prefab's had).
        /// </summary>
        private static GameObject SaveModelPrefab(CreatureSetup setup, string modelPath, AnimatorController controller, Result result)
        {
            string prefabPath = ModelPrefabPathFor(modelPath);
            var earlier = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            CharacterHitboxes earlierHitboxes = earlier != null ? earlier.GetComponent<CharacterHitboxes>() : null;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(setup.model);
            if (model == null)
            {
                result.problem = $"its model '{modelPath}' couldn't be made into a prefab.";
                return null;
            }
            try
            {
                if (PrefabUtility.IsPartOfPrefabInstance(model)) { PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); }
                model.name = $"{NameOf(modelPath)} Model";
                Animator animator = model.GetComponent<Animator>();
                if (animator == null) { animator = model.AddComponent<Animator>(); }
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false; // Movement is driven by MovementController, not animation.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                Transform head = Bone(model, setup.eyesBone);
                if (head != null)
                {
                    var eyes = new GameObject(CharacterModelHost.EyesName).transform;
                    eyes.SetParent(head, false);
                    eyes.position = model.transform.TransformPoint(setup.eyesPosition);
                }
                else { result.notes.Add($"it has no eyes bone ('{setup.eyesBone}') to put its eyes on: the camera follows its default eye height"); }
                AddPawPlacement(model, setup, result);
                HitboxFitter.Fit(model, setup.hitboxBones, earlierHitboxes);
                return PrefabUtility.SaveAsPrefabAsset(model, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        /// <summary>The model's bone (any transform under it) of that name, or null.</summary>
        internal static Transform Bone(GameObject model, string name) =>
            string.IsNullOrEmpty(name) ? null : model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        /// <summary>
        /// Gives the model paw placement (PawPlacementIK): its legs from the setup, each paw touching the floor at its
        /// contact, set on the model as it stands, in its bind pose; and the bones turned against the pitch and into turns.
        /// Its other settings are PawPlacementIK's defaults: the model prefab is made anew each time, so tune them there.
        /// Without its root bone or every bone of every leg, it has none (half its legs placed would look worse than none).
        /// </summary>
        private static void AddPawPlacement(GameObject model, CreatureSetup setup, Result result)
        {
            if (setup.legs.Length == 0) { result.notes.Add("its setup has no legs: it has no paw placement"); return; }
            var legs = new PawPlacementIK.Leg[setup.legs.Length];
            for (int i = 0; i < legs.Length; i++)
            {
                CreatureSetup.Leg leg = setup.legs[i];
                legs[i] = new PawPlacementIK.Leg
                {
                    upper = Bone(model, leg.upper), lower = Bone(model, leg.lower), foot = Bone(model, leg.foot),
                    bendDirection = leg.bendDirection, wallClearance = leg.wallClearance,
                };
                if (legs[i].upper == null || legs[i].lower == null || legs[i].foot == null)
                {
                    result.notes.Add($"it's missing a bone of the leg {leg.upper} > {leg.lower} > {leg.foot}: it has no paw placement");
                    return;
                }
            }
            Transform root = Bone(model, setup.rootBone);
            if (root == null) { result.notes.Add($"it has no root bone ('{setup.rootBone}'): it has no paw placement"); return; }
            if (!model.TryGetComponent(out PawPlacementIK paws)) { paws = model.AddComponent<PawPlacementIK>(); }
            paws.SetLegs(root, legs);
            paws.SetCounterPitchBones(setup.counterPitchBones
                .Select(c => new PawPlacementIK.CounterPitchBone { bone = Bone(model, c.bone), noseUpShare = c.noseUpShare, noseDownShare = c.noseDownShare, groundClearance = c.groundClearance })
                .Where(c => c.bone != null)
                .ToArray());
            paws.SetTurnBendBones(setup.turnBendBones
                .Select(b => new PawPlacementIK.TurnBendBone { bone = Bone(model, b.bone), share = b.share })
                .Where(b => b.bone != null)
                .ToArray());
            for (int i = 0; i < legs.Length; i++) { paws.SetContact(i, setup.legs[i].contact); }
        }

        /// <summary>What the setup names that the model doesn't have (or that the setup doesn't name at all), for its Unfound.</summary>
        private static string[] Unfound(CreatureSetup setup, GameObject model)
        {
            var unfound = new List<string>();
            void Need(string what, string bone)
            {
                if (string.IsNullOrEmpty(bone)) { unfound.Add(what); }
                else if (Bone(model, bone) == null) { unfound.Add($"{what} ('{bone}' isn't one of its bones)"); }
            }
            Need("its Root Bone", setup.rootBone);
            if (setup.legs.Length == 0) { unfound.Add("its Legs"); }
            for (int i = 0; i < setup.legs.Length; i++)
            {
                Need($"leg {i + 1}'s Upper", setup.legs[i].upper);
                Need($"leg {i + 1}'s Lower", setup.legs[i].lower);
                Need($"leg {i + 1}'s Foot", setup.legs[i].foot);
            }
            Need("its Eyes Bone", setup.eyesBone);
            foreach (CreatureSetup.CounterPitchBone bone in setup.counterPitchBones) { Need("a Counter Pitch Bone", bone.bone); }
            foreach (CreatureSetup.TurnBendBone bone in setup.turnBendBones) { Need("a Turn Bend Bone", bone.bone); }
            foreach (string bone in setup.hitboxBones) { Need("a Hitbox Bone", bone); }
            if (setup.animationSet != null && setup.animationSet.layout != null)
            {
                foreach (ClipSlot slot in setup.animationSet.layout.clips)
                {
                    bool filled = setup.animationSet.ClipFor(slot.name) != null || slot.candidates?.Any(c => c != null) == true;
                    if (!filled && string.IsNullOrEmpty(slot.fallbackSlot) && slot.name == "Idle") { unfound.Add("an Idle clip (its Animation Set's Idle role)"); }
                }
            }
            return unfound.ToArray();
        }

        // ---- The translator, the body and the model source ----

        /// <summary>
        /// The creature's Animator parameter translator, made if missing (then left as it's changed): the default floats
        /// and bools, and its turn speed, which its standing turn blends by.
        /// </summary>
        private static AnimatorParameterTranslator LoadOrMakeTranslator(string modelPath, Result result)
        {
            string path = TranslatorPathFor(modelPath);
            var translator = AssetDatabase.LoadAssetAtPath<AnimatorParameterTranslator>(path);
            if (translator != null) { return translator; }
            translator = ScriptableObject.CreateInstance<AnimatorParameterTranslator>();
            translator.FloatParameters = AnimatorParameterTranslator.DefaultFloatParameters()
                .Append(new AnimatorParameterTranslator.FloatParameter("Turn Speed", TurnSpeedParameter, smoothed: true)).ToArray();
            AssetDatabase.CreateAsset(translator, path);
            result.notes.Add($"made its translator, '{path}'");
            return translator;
        }

        /// <summary>
        /// The creature's body, fitted from its mesh if it has none (then left as it's tuned, not fitted again): capsules
        /// along its torso and a sphere for its head (BodyFitting.Creature), held a step above its feet.
        /// </summary>
        private static BodyDefinition LoadOrFitBody(CreatureSetup setup, string modelPath, GameObject modelPrefab, Result result)
        {
            string path = BodyPathFor(modelPath);
            var body = AssetDatabase.LoadAssetAtPath<BodyDefinition>(path);
            if (body != null) { return body; }
            BodyShape[] parts = FitBody(setup, modelPrefab);
            body = ScriptableObject.CreateInstance<BodyDefinition>();
            body.Shape = parts[0];
            body.ExtraParts.AddRange(parts.Skip(1));
            body.RefitOnPrepare = false;
            AssetDatabase.CreateAsset(body, path);
            result.notes.Add($"fitted its body, '{path}' ({parts.Length} part(s)), to tune there");
            return body;
        }

        /// <summary>
        /// A creature's body fitted from its mesh, in its bind pose (see BodyFitting.Creature): its torso the mesh carried by
        /// the bones from its root bone up to its eyes bone (not its neck: the head's sphere reaches back over it), and its
        /// legs' upper bones above their top joints (its haunches and shoulders); its head the mesh its eyes bone and those
        /// under it carry. Without them, the whole mesh is its torso. Held as far
        /// above its feet as the Character prefab steps up.
        /// </summary>
        internal static BodyShape[] FitBody(CreatureSetup setup, GameObject model)
        {
            Transform root = Bone(model, setup.rootBone), head = Bone(model, setup.eyesBone);
            var torsoBones = new HashSet<Transform>();
            if (root != null && head != null)
            {
                torsoBones.Add(root);
                for (Transform bone = head.parent; bone != null && bone != model.transform; bone = bone.parent)
                {
                    if (bone == root || bone.IsChildOf(root) || root.IsChildOf(bone)) { torsoBones.Add(bone); }
                    if (bone == root) { break; }
                }
                torsoBones.RemoveWhere(b => b.name.ToLowerInvariant().Contains("neck"));
                if (torsoBones.Count == 0) { torsoBones.Add(root); }
            }
            // The haunches and shoulders, above the legs' top joints, are the torso's too: most rigs skin a rump and a chest
            // to the legs' upper bones, which would leave a body too short and too high without them.
            var haunches = new Dictionary<Transform, float>();
            foreach (CreatureSetup.Leg leg in setup.legs)
            {
                Transform upper = Bone(model, leg.upper);
                if (upper != null) { haunches[upper] = model.transform.InverseTransformPoint(upper.position).y; }
            }
            Bounds? torso = torsoBones.Count > 0
                ? MeshBoundsOf(model, (b, point) => b != null && (torsoBones.Contains(b) || (haunches.TryGetValue(b, out float joint) && point.y >= joint)))
                : null;
            Bounds? headBounds = head != null ? MeshBoundsOf(model, b => b != null && (b == head || b.IsChildOf(head))) : null;
            Bounds body = torso ?? MeshBoundsOf(model, _ => true) ?? new Bounds(Vector3.up, Vector3.one);
            return BodyFitting.Creature(body, headBounds ?? default, headBounds.HasValue, CharacterStepHeight());
        }

        /// <summary>How tall a step the Character prefab steps up by itself (its Max Step Height): how far a creature's body is held above its feet.</summary>
        private static float CharacterStepHeight()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SceneGameSetup.CharacterPrefabPath);
            return prefab != null && prefab.TryGetComponent(out MovementController movement) ? movement.MaxStepHeight : 0f;
        }

        /// <summary>
        /// The creature's model source: its model prefab and translator, kept at its model's size, standing on its feet at
        /// its origin facing forward (nothing to measure as it loads), with its body.
        /// </summary>
        private static DirectCharacterModelSource SaveSource(string modelPath, GameObject modelPrefab, AnimatorParameterTranslator translator, BodyDefinition body)
        {
            string path = SourcePathFor(modelPath);
            var source = AssetDatabase.LoadAssetAtPath<DirectCharacterModelSource>(path);
            if (source == null)
            {
                source = ScriptableObject.CreateInstance<DirectCharacterModelSource>();
                AssetDatabase.CreateAsset(source, path);
            }
            source.ModelPrefab = modelPrefab;
            source.Translator = translator;
            source.Height = ModelHeight.KeepModelSize;
            source.Scale = 1f;
            source.MeasureGroundOnLoad = false;
            source.GroundOffset = 0f;
            source.AnimatedGroundOffset = 0f;
            source.FacingCorrection = 0f;
            source.BodyDefinition = body;
            source.RecordMeasurements(CharacterModelSource.MeasureHeight(modelPrefab, modelPrefab.transform));
            EditorUtility.SetDirty(source);
            return source;
        }

        /// <summary>The setup of the creature whose model prefab a model source wears, or null.</summary>
        public static CreatureSetup SetupOf(CharacterModelSource source)
        {
            string prefabPath = ModelPrefabPathOf(source);
            if (!InCreaturesFolder(prefabPath)) { return null; }
            return AssetDatabase.FindAssets("t:" + nameof(CreatureSetup), new[] { FolderOf(prefabPath) })
                .Select(guid => AssetDatabase.LoadAssetAtPath<CreatureSetup>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(setup => setup != null && setup.model != null && ModelPrefabPathFor(AssetDatabase.GetAssetPath(setup.model)) == prefabPath);
        }

        /// <summary>A creature's eye height (m, at its model's size): its setup's eyes, else its model prefab's Eyes point's, else 0.</summary>
        public static float EyeHeightOf(CharacterModelSource source)
        {
            CreatureSetup setup = SetupOf(source);
            if (setup != null && !string.IsNullOrEmpty(setup.eyesBone)) { return setup.eyesPosition.y; }
            GameObject prefab = (source as DirectCharacterModelSource)?.ModelPrefab;
            Transform eyes = prefab != null ? prefab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == CharacterModelHost.EyesName) : null;
            return eyes != null ? prefab.transform.InverseTransformPoint(eyes.position).y : 0f;
        }
    }
}
