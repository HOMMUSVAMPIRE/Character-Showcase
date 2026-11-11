using System.Collections.Generic;
using System.Linq;
using Character;
using CharacterShowcase.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CharacterShowcase.Tests.EditMode
{
    /// <summary>
    /// Checks of what Prepare Character Models leaves behind for every model in Assets/Models/CharacterModels, run in
    /// the editor without entering Play mode: each is ready to use with the shared animation, its avatar's reference
    /// pose is a T-pose with nothing rotated above its skeleton, and its stored ground offset matches a fresh
    /// measurement. A model added without being prepared, or a change to preparing that breaks one of these, fails
    /// here. Also checks the pieces of preparing that don't need a model file: ground offsets counting meshes that
    /// aren't skinned, what makes a model due to be prepared again, and sizing previews like loaded models.
    /// </summary>
    public class PreparedModelTests
    {
        // The category of checks of the package's own models and animations, which a team using its own can leave out.
        public const string SampleContent = "SampleContent";
        private const string NoModels = "(no prepared models)";

        /// <summary>Every prepared model's source, by path, or a placeholder (skipped) if there are none.</summary>
        private static IEnumerable<string> PreparedSources()
        {
            string[] paths = AssetDatabase.IsValidFolder(CharacterModelImporter.ModelsFolder)
                ? AssetDatabase.FindAssets($"t:{nameof(DirectCharacterModelSource)}", new[] { CharacterModelImporter.ModelsFolder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .OrderBy(path => path, System.StringComparer.Ordinal)
                    .ToArray()
                : System.Array.Empty<string>();
            return paths.Length > 0 ? paths : new[] { NoModels };
        }

        private static DirectCharacterModelSource Load(string path)
        {
            if (path == NoModels) { Assert.Ignore($"There are no prepared models in {CharacterModelImporter.ModelsFolder}."); }
            var source = AssetDatabase.LoadAssetAtPath<DirectCharacterModelSource>(path);
            Assert.That(source, Is.Not.Null, $"Test setup: no model source at '{path}'.");
            return source;
        }

        private readonly List<Object> created = new();

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created) { if (obj != null) { Object.DestroyImmediate(obj); } }
            created.Clear();
        }

        [Test]
        public void EveryModelFile_HasAPreparedSource()
        {
            foreach (string modelPath in CharacterModelImporter.FindModelFiles())
            {
                string sourcePath = CharacterModelImporter.DefinitionPathFor(modelPath, CharacterModelImporter.DisplayName(modelPath));
                Assert.That(AssetDatabase.LoadAssetAtPath<DirectCharacterModelSource>(sourcePath), Is.Not.Null,
                    $"'{modelPath}' has no source at '{sourcePath}': run Tools > Character Showcase > Prepare Character Models.");
            }
        }

        [Test, Category(SampleContent)]
        public void EveryPreparedModel_IsReady_AndPlaysTheSharedAnimation([ValueSource(nameof(PreparedSources))] string path)
        {
            DirectCharacterModelSource source = Load(path);
            Assert.That(CharacterModelImporter.Validate(source), Is.Null);
            Assert.That(CharacterModelImporter.ControllerOf(source),
                Is.SameAs(AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterModelImporter.ControllerPath)));
        }

        [Test, Category(SampleContent)]
        public void EveryPreparedModel_HasNothingRotatedAboveItsSkeleton([ValueSource(nameof(PreparedSources))] string path)
        {
            DirectCharacterModelSource source = Load(path);
            Assert.That(CharacterModelImporter.RotatedAboveSkeleton(source.ModelPrefab, source.Avatar), Is.Null,
                "Rotated or unevenly scaled nodes above the skeleton distort the model when the character is stretched.");
        }

        [Test, Category(SampleContent)]
        public void EveryPreparedModel_HasGroundOffsets_MatchingAFreshMeasurement([ValueSource(nameof(PreparedSources))] string path)
        {
            DirectCharacterModelSource source = Load(path);
            Assert.That(CharacterModelImporter.TryMeasureGroundOffset(source.ModelPrefab, CharacterModelImporter.ControllerOf(source),
                source.Avatar, out float measured), Is.True, "Its standing pose should be measurable.");
            Assert.That(source.GroundOffset, Is.EqualTo(measured).Within(1e-4f),
                "Its Ground Offset is out of date: run Prepare Character Models.");
            Assert.That(CharacterModelImporter.TryMeasureGroundOffsets(source.ModelPrefab, CharacterModelImporter.ControllerOf(source),
                source.Avatar, out _, out float animated), Is.True);
            Assert.That(source.AnimatedGroundOffset, Is.EqualTo(animated).Within(1e-4f), "Its Animated Ground Offset is out of date.");
            Assert.That(source.MeasureGroundOnLoad, Is.False, "Measured once here, it needn't be measured each time it loads.");
            Assert.That(CharacterModelImporter.TryMeasureStanding(source.ModelPrefab, CharacterModelImporter.ControllerOf(source),
                source.Avatar, out _, out _, out StandingPose pose), Is.True);
            Assert.That(source.FacingCorrection, Is.EqualTo(pose.FacingCorrection), "Its Facing Correction is out of date.");
            Assert.That(Mathf.Abs(measured), Is.LessThan(0.1f * CharacterModelSource.MeasureHeight(source.ModelPrefab)),
                "The soles should be near the model's origin; far from it, the model is probably placed wrongly in its file.");
        }

        /// <summary>
        /// Humanoid animation plays relative to the avatar's reference pose, which preparing makes a T-pose: spine
        /// straight up, arms straight out to the sides, legs straight down. Posed as the avatar stores it, the model
        /// should stand that way.
        /// </summary>
        [Test, Category(SampleContent)]
        public void EveryPreparedModel_HasATPose_AsItsReferencePose([ValueSource(nameof(PreparedSources))] string path)
        {
            DirectCharacterModelSource source = Load(path);
            Animator prefabAnimator = source.ModelPrefab.GetComponentInChildren<Animator>(true);
            Avatar avatar = source.Avatar != null ? source.Avatar : prefabAnimator.avatar;
            HumanDescription description = avatar.humanDescription;

            GameObject model = Track(Object.Instantiate(source.ModelPrefab));
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var transforms = new Dictionary<string, Transform>();
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) { transforms.TryAdd(t.name, t); }
            foreach (SkeletonBone bone in description.skeleton)
            {
                if (!transforms.TryGetValue(bone.name, out Transform t) || t == model.transform) { continue; }
                t.SetLocalPositionAndRotation(bone.position, bone.rotation);
            }
            var boneNames = description.human.ToDictionary(h => h.humanName, h => h.boneName);
            Vector3 Bone(HumanBodyBones bone)
            {
                Assert.That(boneNames.TryGetValue(HumanTrait.BoneName[(int)bone], out string name) && transforms.ContainsKey(name), Is.True,
                    $"Test setup: the avatar should map {bone}.");
                return transforms[name].position;
            }
            Vector3 left = Vector3.ProjectOnPlane(Bone(HumanBodyBones.LeftUpperLeg) - Bone(HumanBodyBones.RightUpperLeg), Vector3.up).normalized;
            void Points(HumanBodyBones from, HumanBodyBones to, Vector3 direction, string what)
            {
                float angle = Vector3.Angle(Bone(to) - Bone(from), direction);
                Assert.That(angle, Is.LessThan(2f), $"{what} should point {direction}, but is {angle:0.0} degrees off.");
            }
            Points(HumanBodyBones.Hips, HumanBodyBones.Head, Vector3.up, "The spine");
            Points(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftHand, left, "The left arm");
            Points(HumanBodyBones.RightUpperArm, HumanBodyBones.RightHand, -left, "The right arm");
            Points(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftFoot, Vector3.down, "The left leg");
            Points(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightFoot, Vector3.down, "The right leg");
        }

        [TestCase(0f, 0f)]
        [TestCase(3f, 0f)]
        [TestCase(44f, 0f)]
        [TestCase(46f, -90f)]
        [TestCase(-91f, 90f)]
        [TestCase(178f, 180f)]
        [TestCase(-179f, 180f)]
        public void TheFacingCorrection_UndoesTheFacing_ToTheNearestQuarterTurn(float facing, float correction)
        {
            Assert.That(new StandingPose(0f, 0f, facing).FacingCorrection, Is.EqualTo(correction));
        }

        /// <summary>A small hierarchy: root, a node above the skeleton posed as given, hips under it, and a bone under those.</summary>
        private (Transform root, Transform node, Transform hips, Transform[] all) Armature(Quaternion rotation, Vector3 scale)
        {
            var root = Track(new GameObject("Root")).transform;
            var node = new GameObject("Armature").transform;
            node.SetParent(root, false);
            node.SetLocalPositionAndRotation(new Vector3(0.1f, 0.2f, -0.3f), rotation);
            node.localScale = scale;
            var hips = new GameObject("Hips").transform;
            hips.SetParent(node, false);
            hips.SetLocalPositionAndRotation(new Vector3(0.05f, 0.9f, 0.02f), Quaternion.Euler(80f, 10f, -5f));
            var spine = new GameObject("Spine").transform;
            spine.SetParent(hips, false);
            spine.SetLocalPositionAndRotation(new Vector3(0f, 0.15f, 0.01f), Quaternion.Euler(-20f, 5f, 0f));
            return (root, node, hips, new[] { node, hips, spine });
        }

        private static readonly object[] StraightenedArmatures =
        {
            new object[] { new Vector3(-90f, 0f, 0f), new Vector3(1f, 1f, 1f) },     // Blender's axis conversion.
            new object[] { new Vector3(-90f, 0f, 0f), new Vector3(100f, 100f, 100f) }, // In centimetres.
            new object[] { new Vector3(-90f, 0f, 0f), new Vector3(1f, 1.25f, 0.8f) },  // Unevenly scaled too.
            new object[] { new Vector3(37f, -20f, 5f), new Vector3(2f, 2f, 2f) },      // Any turn, evenly scaled.
        };

        /// <summary>
        /// Straightening the nodes above the skeleton takes their rotation out without moving, turning, scaling or
        /// shearing anything under them, even when a node is unevenly scaled (its scale's axes swap with the turn).
        /// </summary>
        [TestCaseSource(nameof(StraightenedArmatures))]
        public void StraighteningAboveTheSkeleton_KeepsEverythingUnderItWhereItWas(Vector3 turn, Vector3 scale)
        {
            (Transform root, Transform node, Transform hips, Transform[] all) = Armature(Quaternion.Euler(turn), scale);
            Matrix4x4[] before = all.Skip(1).Select(t => t.localToWorldMatrix).ToArray();

            Assert.That(CharacterModelImporter.StraightenAboveSkeleton(root, hips), Is.True);
            Assert.That(Quaternion.Angle(node.localRotation, Quaternion.identity), Is.LessThan(1e-3f), "The node should be unrotated.");
            Matrix4x4[] after = all.Skip(1).Select(t => t.localToWorldMatrix).ToArray();
            for (int i = 0; i < before.Length; i++)
            {
                for (int element = 0; element < 16; element++)
                {
                    Assert.That(after[i][element], Is.EqualTo(before[i][element]).Within(1e-4f), $"'{all[i + 1].name}' moved (matrix element {element}).");
                }
            }
        }

        /// <summary>An unevenly scaled node turned other than by quarter turns can't be straightened without shearing, so it's left.</summary>
        [Test]
        public void StraighteningAboveTheSkeleton_LeavesWhatItCantStraighten()
        {
            (Transform root, Transform node, Transform hips, Transform[] all) = Armature(Quaternion.Euler(30f, 0f, 0f), new Vector3(1f, 1.25f, 1f));
            Quaternion rotation = node.localRotation;
            Assert.That(CharacterModelImporter.StraightenAboveSkeleton(root, hips), Is.False);
            Assert.That(node.localRotation, Is.EqualTo(rotation));
        }

        /// <summary>A mesh carried by a bone (a shoe, say) reaching below the skinned feet sets the ground offset.</summary>
        [Test]
        public void GroundOffsets_CountMeshesCarriedByBones()
        {
            DirectCharacterModelSource source = Load(PreparedSources().First());
            RuntimeAnimatorController controller = CharacterModelImporter.ControllerOf(source);
            Assert.That(CharacterModelImporter.TryMeasureGroundOffset(source.ModelPrefab, controller, source.Avatar, out float bare), Is.True);

            GameObject model = Track(Object.Instantiate(source.ModelPrefab));
            Transform foot = model.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.LeftFoot);
            GameObject shoe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(shoe.GetComponent<Collider>());
            // A small block carried by the foot, hanging 30 cm below it: wherever the standing pose puts the foot, the
            // block goes with it, well below the skinned soles.
            shoe.transform.SetParent(foot, true);
            shoe.transform.position = foot.position + Vector3.down * 0.3f;
            shoe.transform.localScale = Vector3.Scale(Vector3.one * 0.02f, new Vector3(1f / foot.lossyScale.x, 1f / foot.lossyScale.y, 1f / foot.lossyScale.z));

            Assert.That(CharacterModelImporter.TryMeasureGroundOffset(model, controller, source.Avatar, out float shod), Is.True);
            Assert.That(shod, Is.LessThan(bare - 0.15f), "A mesh carried by the foot reaching below the skinned soles should set the offset.");
        }

        /// <summary>
        /// A model is due to be prepared again when the animation its ground offset was measured with changes: the
        /// record of what it was prepared against changes with its controller, and stays the same while nothing does.
        /// </summary>
        [Test, Category(SampleContent)]
        public void WhatAModelWasPreparedAgainst_ChangesWithItsAnimation()
        {
            string modelPath = CharacterModelImporter.FindModelFiles().FirstOrDefault();
            var shared = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterModelImporter.ControllerPath);
            if (modelPath == null || shared == null) { Assert.Ignore("Needs a model file and the package's shared Animator Controller."); }
            Assert.That(CharacterModelImporter.RecordFor(modelPath, shared), Is.EqualTo(CharacterModelImporter.RecordFor(modelPath, shared)),
                "Nothing changed, so it shouldn't need preparing again.");

            const string folder = "Assets/__PreparedModelTests";
            // Busy, so the model postprocessor leaves these temporary imports alone.
            using (CharacterModelImporter.Busy())
            {
                try
                {
                    AssetDatabase.CreateFolder("Assets", "__PreparedModelTests");
                    string copyPath = folder + "/Copy.controller";
                    Assert.That(AssetDatabase.CopyAsset(CharacterModelImporter.ControllerPath, copyPath), Is.True);
                    var copy = AssetDatabase.LoadAssetAtPath<AnimatorController>(copyPath);
                    CharacterModelImporter.PreparedRecord before = CharacterModelImporter.RecordFor(modelPath, copy);
                    Assert.That(before.File, Is.EqualTo(CharacterModelImporter.RecordFor(modelPath, shared).File));

                    copy.AddParameter("Changed", AnimatorControllerParameterType.Float);
                    EditorUtility.SetDirty(copy);
                    AssetDatabase.SaveAssets();
                    CharacterModelImporter.PreparedRecord after = CharacterModelImporter.RecordFor(modelPath, copy);
                    Assert.That(after.File, Is.EqualTo(before.File), "The model file hasn't changed.");
                    Assert.That(after.Animation, Is.Not.EqualTo(before.Animation), "A changed controller should make the model due again.");
                }
                finally
                {
                    AssetDatabase.DeleteAsset(folder);
                }
            }
        }

        /// <summary>Previews are sized as the model is when it loads, Fit To Height included.</summary>
        [Test, Category(SampleContent)]
        public void Previews_AreSizedLikeTheLoadedModel_FitToHeightIncluded()
        {
            DirectCharacterModelSource prepared = Load(PreparedSources().First());
            var source = Track(ScriptableObject.CreateInstance<DirectCharacterModelSource>());
            source.ModelPrefab = prepared.ModelPrefab;
            source.Height = ModelHeight.FitToHeight;
            source.FitHeight = 1.2f;

            var previews = new List<CharacterModelSource.PreviewMesh>();
            source.GetPreviewMeshes(previews);
            Assert.That(previews, Is.Not.Empty);
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            foreach (CharacterModelSource.PreviewMesh preview in previews)
            {
                Bounds bounds = preview.mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 local = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    float y = preview.matrix.MultiplyPoint3x4(local).y;
                    bottom = Mathf.Min(bottom, y);
                    top = Mathf.Max(top, y);
                }
            }
            Assert.That(top - bottom, Is.EqualTo(1.2f).Within(0.01f), "A model fitted to 1.2 m should preview 1.2 m tall.");
            Assert.That(source.StandingHeightFor(source.OwnHeight()), Is.EqualTo(1.2f).Within(1e-4f),
                "Its height, as the Character Model window lists it, should be the fitted one.");
        }
    }
}
