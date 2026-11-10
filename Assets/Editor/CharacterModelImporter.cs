using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Imports and prepares every rigged model in Assets/Models/CharacterModels so the character can use it:
    /// - configures it as a Humanoid, so the mannequin's animations retarget onto it
    /// - puts the avatar's reference pose into a T-pose, so models rigged in an A-pose (arms angled down) don't play
    ///   every animation with their arms raised
    /// - saves a model prefab next to it ("[name] Model.prefab"): the model with an Animator using the shared Animator
    ///   Controller, CharacterIK and FootPlacementIK
    /// - saves a Character Model Definition next to it ("[name].asset") naming the prefab and the shared translator,
    ///   which the Character Model window (or an AnimationController) can then use
    /// Each subfolder can hold one model, or several (variants of one model with different materials, say); each model
    /// file becomes its own choice. Safe to run repeatedly: IK settings and edits to the definitions are kept.
    /// </summary>
    public static class CharacterModelImporter
    {
        public const string ModelsFolder = "Assets/Models/CharacterModels";
        // Shared by every model: Humanoid animation plays on any Humanoid model. Built by MannequinGenerator.
        public const string ControllerPath = "Assets/Animation/Mannequin.controller";
        public const string TranslatorPath = "Assets/Animation/Mannequin Animator Translator.asset";

        // Models outside this height range (in meters) get their definition's Scale set to bring them to TypicalHeight,
        // since that usually means the file's units were lost (a 180 m or 1.8 cm character).
        private const float MinHeight = 1f;
        private const float MaxHeight = 2.5f;
        private const float TypicalHeight = 1.8f;

        /// <summary>The outcome of preparing one model file.</summary>
        public sealed class Result
        {
            public string modelPath;
            public string name;
            public CharacterModelDefinition definition;
            // Why the model couldn't be prepared, or null if it was
            public string problem;
            public float height;
            public bool Succeeded => problem == null;

            public override string ToString() => Succeeded
                ? $"'{name}' ({modelPath}): ready, {height:0.00} m tall, definition '{AssetDatabase.GetAssetPath(definition)}'"
                : $"'{name}' ({modelPath}): not prepared. {problem}";
        }

        [MenuItem("Tools/Character Showcase/Prepare Character Models", priority = 2)]
        public static void PrepareAllFromMenu()
        {
            List<Result> results = PrepareAll();
            if (results == null || Application.isBatchMode) { return; }
            string summary = results.Count == 0
                ? $"No model files were found in {ModelsFolder}. Put each model in a subfolder there."
                : string.Join("\n\n", results.Select(r => r.ToString()));
            EditorUtility.DisplayDialog("Prepare Character Models", summary, "OK");
        }

        /// <summary>Prepares every model file in the models folder. Returns null if the shared animation assets are missing.</summary>
        public static List<Result> PrepareAll()
        {
            if (!LoadSharedAssets(out AnimatorController controller, out AnimatorParameterTranslator translator)) { return null; }
            var results = new List<Result>();
            try
            {
                string[] paths = FindModelFiles().ToArray();
                for (int i = 0; i < paths.Length; i++)
                {
                    if (!Application.isBatchMode)
                    {
                        EditorUtility.DisplayProgressBar("Prepare Character Models", paths[i], (float)i / paths.Length);
                    }
                    results.Add(Prepare(paths[i], controller, translator));
                }
            }
            finally
            {
                if (!Application.isBatchMode) { EditorUtility.ClearProgressBar(); }
            }
            AssetDatabase.SaveAssets();

            // Check every prepared model once all of them are saved, the same way the Character Model window does, in
            // case preparing a later model affected an earlier one.
            foreach (Result result in results.Where(r => r.Succeeded))
            {
                string problem = Validate(result.definition);
                if (problem == null && ControllerOf(result.definition) != controller) { problem = $"It doesn't use the shared Animator Controller '{ControllerPath}'."; }
                if (problem != null) { result.problem = problem; }
            }

            var log = new StringBuilder($"[CharacterModelImporter] Prepared {results.Count(r => r.Succeeded)} of {results.Count} models in {ModelsFolder}:");
            foreach (Result result in results) { log.Append("\n  - ").Append(result); }
            if (results.All(r => r.Succeeded)) { Debug.Log(log); } else { Debug.LogWarning(log); }
            return results;
        }

        public static bool LoadSharedAssets(out AnimatorController controller, out AnimatorParameterTranslator translator)
        {
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            translator = AssetDatabase.LoadAssetAtPath<AnimatorParameterTranslator>(TranslatorPath);
            if (controller != null && translator != null && controller.layers.Length > 0) { return true; }
            Debug.LogError($"[CharacterModelImporter] '{ControllerPath}' or '{TranslatorPath}' is missing or empty. Run Tools > Character " +
                           "Showcase > Generate Mannequin first: the mannequin's animations are the ones every model plays.");
            return false;
        }

        /// <summary>Every model file (anything Unity imports as a model) in the models folder, sorted by path.</summary>
        public static IEnumerable<string> FindModelFiles() =>
            AssetDatabase.FindAssets("", new[] { ModelsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => AssetImporter.GetAtPath(path) is ModelImporter)
                .Distinct()
                .OrderBy(path => path, System.StringComparer.Ordinal);

        /// <summary>
        /// The name a model goes by: its file name, or "Folder (file)" when its folder holds several models, so variants
        /// of one model stay told apart.
        /// </summary>
        public static string DisplayName(string modelPath)
        {
            string folder = Path.GetDirectoryName(modelPath)?.Replace('\\', '/');
            string file = Path.GetFileNameWithoutExtension(modelPath);
            int modelsInFolder = FindModelFiles().Count(p => Path.GetDirectoryName(p)?.Replace('\\', '/') == folder);
            return modelsInFolder > 1 ? $"{Path.GetFileName(folder)} ({file})" : file;
        }

        public static string ModelPrefabPathFor(string modelPath, string name) =>
            $"{Path.GetDirectoryName(modelPath)?.Replace('\\', '/')}/{name} Model.prefab";

        public static string DefinitionPathFor(string modelPath, string name) =>
            $"{Path.GetDirectoryName(modelPath)?.Replace('\\', '/')}/{name}.asset";

        /// <summary>Imports one model as a Humanoid and saves its model prefab and definition.</summary>
        public static Result Prepare(string modelPath, AnimatorController controller, AnimatorParameterTranslator translator)
        {
            var result = new Result { modelPath = modelPath, name = DisplayName(modelPath) };
            if (AssetImporter.GetAtPath(modelPath) is not ModelImporter importer)
            {
                result.problem = "It isn't a model file.";
                return result;
            }
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null || modelAsset.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
            {
                result.problem = "It has no skinned mesh, so it isn't rigged. Rig it (Mixamo can) and export it with its skeleton.";
                return result;
            }

            ConfigureImporter(importer);
            // Only set the reference pose of avatars made here: an avatar copied from another, or configured by hand in
            // the Rig tab, is left as it is.
            if (importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel) { EnforceTPose(importer, modelPath); }

            modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Avatar avatar = AvatarOf(importer, modelPath);
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                result.problem = "Unity couldn't map its skeleton to a Humanoid. Select the model, open the Rig tab and press " +
                                 "Configure to see which bones are missing, then run this again.";
                return result;
            }

            string prefabPath = ModelPrefabPathFor(modelPath, result.name);
            GameObject modelPrefab = SaveModelPrefab(modelAsset, avatar, controller, prefabPath);
            // Read the prefab back as it was saved, and save it once more if its Animator didn't keep the avatar or the
            // controller, so a model is never reported ready without them.
            string saveProblem = SavedAnimatorProblem(prefabPath, avatar, controller);
            if (saveProblem != null)
            {
                modelPrefab = SaveModelPrefab(modelAsset, avatar, controller, prefabPath);
                saveProblem = SavedAnimatorProblem(prefabPath, avatar, controller);
            }
            if (saveProblem != null)
            {
                result.problem = $"Its model prefab '{prefabPath}' didn't keep its setup: {saveProblem}";
                return result;
            }
            result.height = MeasureHeight(modelPrefab);

            string definitionPath = DefinitionPathFor(modelPath, result.name);
            var definition = AssetDatabase.LoadAssetAtPath<CharacterModelDefinition>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<CharacterModelDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
                if (result.height > 0f && (result.height < MinHeight || result.height > MaxHeight))
                {
                    definition.Scale = TypicalHeight / result.height;
                    Debug.LogWarning($"[CharacterModelImporter] '{result.name}' is {result.height:0.00} m tall, so its definition " +
                                     $"scales it by {definition.Scale:0.###} to {TypicalHeight} m. Change Scale on '{definitionPath}' if needed.");
                }
            }
            definition.ModelPrefab = modelPrefab;
            if (definition.Translator == null) { definition.Translator = translator; }
            EditorUtility.SetDirty(definition);
            result.definition = definition;
            return result;
        }

        /// <summary>
        /// Why the definition can't be used as the character's model, or null if it can: it must name a model prefab with
        /// a valid Humanoid avatar, an Animator Controller and a translator, as Prepare sets it up.
        /// </summary>
        public static string Validate(CharacterModelDefinition definition)
        {
            if (definition == null) { return "There is no definition."; }
            if (definition.ModelPrefab == null) { return "It names no model prefab."; }
            var animator = definition.ModelPrefab.GetComponentInChildren<Animator>(true);
            if (animator == null) { return "Its model has no Animator."; }
            Avatar avatar = definition.Avatar != null ? definition.Avatar : animator.avatar;
            if (avatar == null || !avatar.isValid || !avatar.isHuman) { return "Its model has no valid Humanoid avatar."; }
            RuntimeAnimatorController controller = ControllerOf(definition);
            if (controller == null) { return "Its model has no Animator Controller."; }
            if (controller is AnimatorController editable && editable.layers.Length == 0) { return "Its Animator Controller is empty."; }
            if (definition.Translator == null) { return "It names no animation translator."; }
            return null;
        }

        /// <summary>The Animator Controller the definition's model plays: the definition's override, or its prefab's.</summary>
        public static RuntimeAnimatorController ControllerOf(CharacterModelDefinition definition)
        {
            if (definition == null) { return null; }
            if (definition.AnimatorController != null) { return definition.AnimatorController; }
            Animator animator = definition.ModelPrefab != null ? definition.ModelPrefab.GetComponentInChildren<Animator>(true) : null;
            return animator != null ? animator.runtimeAnimatorController : null;
        }

        /// <summary>
        /// Why the saved model prefab's Animator isn't set up with the given avatar and controller, or null if it is.
        /// Reimports the prefab first, so this checks what's on disk rather than what's in memory.
        /// </summary>
        private static string SavedAnimatorProblem(string prefabPath, Avatar avatar, AnimatorController controller)
        {
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Animator animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;
            if (animator == null) { return "it has no Animator."; }
            if (animator.avatar != avatar) { return "its Animator has no avatar, or a different one."; }
            if (animator.runtimeAnimatorController != controller) { return $"its Animator doesn't use '{ControllerPath}'."; }
            return null;
        }

        private static void ConfigureImporter(ModelImporter importer)
        {
            bool changed = false;
            void Set<T>(T current, T wanted, System.Action<T> apply)
            {
                if (!Equals(current, wanted)) { apply(wanted); changed = true; }
            }
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }
            Set(importer.importCameras, false, v => importer.importCameras = v);
            Set(importer.importLights, false, v => importer.importLights = v);
            if (changed) { importer.SaveAndReimport(); }
        }

        private static Avatar AvatarOf(ModelImporter importer, string modelPath) =>
            importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther
                ? importer.sourceAvatar
                : AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();

        /// <summary>
        /// Humanoid animation is stored relative to each avatar's T-pose, so an avatar whose reference pose isn't a T-pose
        /// plays every animation offset by the difference: a model rigged with its arms angled down holds them that much
        /// higher in every clip. This straightens the model's spine up, its arms out to the sides and its legs down, from
        /// its rest pose, and stores that as the avatar's reference pose (as the Rig tab's Enforce T-Pose does).
        ///
        /// The rest pose comes from the skin's bind poses rather than the pose the file's skeleton is saved in, which
        /// can be a frame of an animation: Blender's FBX export leaves the skeleton in the last clip's pose. An avatar
        /// made from a posed skeleton stores its own clips relative to that pose, so they look right on that model but
        /// lean or twist on every other one.
        /// Returns whether the reference pose changed.
        /// </summary>
        private static bool EnforceTPose(ModelImporter importer, string modelPath)
        {
            // Unity maps the bones automatically on import without saving the mapping, so read it from the avatar.
            HumanDescription description = importer.humanDescription;
            if (description.human == null || description.human.Length == 0)
            {
                Avatar avatar = AvatarOf(importer, modelPath);
                if (avatar == null || !avatar.isHuman) { return false; }
                description = avatar.humanDescription;
            }
            if (description.human == null || description.human.Length == 0 || description.skeleton == null) { return false; }

            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Dictionary<string, Transform> transforms = new();
                foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                {
                    transforms.TryAdd(t.name, t);
                }
                RestoreBindPose(instance);
                var boneNames = description.human.ToDictionary(h => h.humanName, h => h.boneName);
                Transform Bone(HumanBodyBones bone) =>
                    boneNames.TryGetValue(HumanTrait.BoneName[(int)bone], out string name) && transforms.TryGetValue(name, out Transform t) ? t : null;

                Transform leftLeg = Bone(HumanBodyBones.LeftUpperLeg), rightLeg = Bone(HumanBodyBones.RightUpperLeg);
                if (leftLeg == null || rightLeg == null) { return false; }
                // The character's left, from its hips, so this works whichever way the model faces.
                Vector3 left = Vector3.ProjectOnPlane(leftLeg.position - rightLeg.position, Vector3.up).normalized;
                if (left == Vector3.zero) { return false; }

                // Parents before children, so each bone is aimed after its parent has moved it.
                // The spine straight up, as the mannequin's is: its clips lean the torso relative to a straight spine,
                // so a model resting with a curved or tilted spine would carry that into every pose.
                Transform[] spine =
                {
                    Bone(HumanBodyBones.Hips), Bone(HumanBodyBones.Spine), Bone(HumanBodyBones.Chest),
                    Bone(HumanBodyBones.UpperChest), Bone(HumanBodyBones.Neck), Bone(HumanBodyBones.Head),
                };
                Transform[] chain = spine.Where(t => t != null).ToArray();
                for (int i = 0; i + 1 < chain.Length; i++) { Aim(chain[i], chain[i + 1], Vector3.up); }
                Aim(Bone(HumanBodyBones.LeftUpperArm), Bone(HumanBodyBones.LeftLowerArm), left);
                Aim(Bone(HumanBodyBones.LeftLowerArm), Bone(HumanBodyBones.LeftHand), left);
                Aim(Bone(HumanBodyBones.LeftHand), Bone(HumanBodyBones.LeftMiddleProximal), left);
                Aim(Bone(HumanBodyBones.RightUpperArm), Bone(HumanBodyBones.RightLowerArm), -left);
                Aim(Bone(HumanBodyBones.RightLowerArm), Bone(HumanBodyBones.RightHand), -left);
                Aim(Bone(HumanBodyBones.RightHand), Bone(HumanBodyBones.RightMiddleProximal), -left);
                Aim(leftLeg, Bone(HumanBodyBones.LeftLowerLeg), Vector3.down);
                Aim(Bone(HumanBodyBones.LeftLowerLeg), Bone(HumanBodyBones.LeftFoot), Vector3.down);
                Aim(rightLeg, Bone(HumanBodyBones.RightLowerLeg), Vector3.down);
                Aim(Bone(HumanBodyBones.RightLowerLeg), Bone(HumanBodyBones.RightFoot), Vector3.down);

                bool changed = false;
                SkeletonBone[] skeleton = description.skeleton;
                for (int i = 0; i < skeleton.Length; i++)
                {
                    if (!transforms.TryGetValue(skeleton[i].name, out Transform t) || t == instance.transform) { continue; }
                    if (Quaternion.Angle(skeleton[i].rotation, t.localRotation) > 0.01f
                        || Vector3.Distance(skeleton[i].position, t.localPosition) > 1e-4f) { changed = true; }
                    skeleton[i].rotation = t.localRotation;
                    skeleton[i].position = t.localPosition;
                }
                bool mappingWasSaved = importer.humanDescription.human != null && importer.humanDescription.human.Length > 0;
                if (!changed && mappingWasSaved) { return false; }
                description.skeleton = skeleton;
                importer.humanDescription = description;
                importer.SaveAndReimport();
                return changed;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// Puts every bone the meshes are skinned to back where it was when the meshes were bound to it: world pose =
        /// renderer's transform x inverse bind pose. Parents first, so placing a bone doesn't move one already placed.
        /// Bones no mesh uses keep their pose relative to their parent.
        /// </summary>
        private static void RestoreBindPose(GameObject instance)
        {
            var rest = new Dictionary<Transform, Matrix4x4>();
            foreach (SkinnedMeshRenderer skinned in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Matrix4x4[] bindposes = skinned.sharedMesh != null ? skinned.sharedMesh.bindposes : null;
                if (bindposes == null) { continue; }
                Transform[] bones = skinned.bones;
                Matrix4x4 meshToWorld = skinned.transform.localToWorldMatrix;
                for (int i = 0; i < bones.Length && i < bindposes.Length; i++)
                {
                    if (bones[i] != null) { rest.TryAdd(bones[i], meshToWorld * bindposes[i].inverse); }
                }
            }
            foreach (Transform bone in instance.GetComponentsInChildren<Transform>(true)) // Parents before children.
            {
                if (!rest.TryGetValue(bone, out Matrix4x4 world)) { continue; }
                bone.SetPositionAndRotation(world.GetColumn(3), world.rotation);
            }
        }

        /// <summary>Turns the bone so its child lies in the given direction from it.</summary>
        private static void Aim(Transform bone, Transform child, Vector3 direction)
        {
            if (bone == null || child == null) { return; }
            Vector3 current = child.position - bone.position;
            if (current.sqrMagnitude < 1e-8f) { return; }
            bone.rotation = Quaternion.FromToRotation(current, direction) * bone.rotation;
        }

        /// <summary>
        /// Saves the model as a prefab (a variant of the model file, so it follows changes to the file) with its Animator,
        /// CharacterIK and FootPlacementIK. IK settings come from an earlier run if there was one, otherwise from the
        /// mannequin's model prefab, so tuning done there carries over to new models.
        /// </summary>
        private static GameObject SaveModelPrefab(GameObject modelAsset, Avatar avatar, AnimatorController controller, string prefabPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var mannequin = AssetDatabase.LoadAssetAtPath<GameObject>(MannequinGenerator.ModelPrefabPath);
            string ikSettings = SettingsOf<CharacterIK>(existing) ?? SettingsOf<CharacterIK>(mannequin);
            string footPlacementSettings = SettingsOf<FootPlacementIK>(existing) ?? SettingsOf<FootPlacementIK>(mannequin);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            try
            {
                model.name = Path.GetFileNameWithoutExtension(prefabPath).Replace(" Model", "");
                model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); // Its origin is at its feet.
                model.transform.localScale = Vector3.one;

                Animator animator = model.GetComponent<Animator>();
                if (animator == null) { animator = model.AddComponent<Animator>(); }
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false; // Movement is driven by MovementController, not animation.
                // Keep animating while hidden in first person, so the shadow the camera keeps still animates.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                // IK layered over the animations. Lives on the Animator's GameObject, as OnAnimatorIK requires.
                // (Not `??`: it bypasses Unity's null check, so a "missing" component could be returned instead of added.)
                if (!model.TryGetComponent(out CharacterIK ik)) { ik = model.AddComponent<CharacterIK>(); }
                if (ikSettings != null) { EditorJsonUtility.FromJsonOverwrite(ikSettings, ik); }
                // Automatic foot placement on uneven ground, fed through CharacterIK's procedural channel.
                if (!model.TryGetComponent(out FootPlacementIK footPlacement)) { footPlacement = model.AddComponent<FootPlacementIK>(); }
                if (footPlacementSettings != null) { EditorJsonUtility.FromJsonOverwrite(footPlacementSettings, footPlacement); }

                return PrefabUtility.SaveAsPrefabAsset(model, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        private static string SettingsOf<T>(GameObject gameObject) where T : Component =>
            gameObject != null && gameObject.TryGetComponent(out T component) ? EditorJsonUtility.ToJson(component) : null;

        /// <summary>The model's height in meters, from its meshes in their bind pose.</summary>
        public static float MeasureHeight(GameObject modelPrefab)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            try
            {
                float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    // A skinned mesh's renderer bounds are padded, so measure the mesh itself. Its vertices are in the
                    // bind pose, relative to the renderer's transform, which is how the model sits in the file.
                    Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                        : renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
                    if (mesh == null) { continue; }
                    Bounds local = mesh.bounds;
                    Matrix4x4 toWorld = renderer.transform.localToWorldMatrix;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3 point = local.center + Vector3.Scale(local.extents,
                            new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                        float y = toWorld.MultiplyPoint3x4(point).y;
                        bottom = Mathf.Min(bottom, y);
                        top = Mathf.Max(top, y);
                    }
                }
                return top > bottom ? top - bottom : 0f;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
