using System.Collections.Generic;
using System.Linq;
using MeridianMotion.Characters;
using MeridianMotion.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools.Utils;
using UnityEngine;

namespace MeridianMotion.Tests.EditMode.Models
{
    /// <summary>
    /// Tests of the Generic-rig character Model Importer (Prepare Generic Rig Models): guessing a setup from the fox's bone names finds the bones
    /// its own setup names; a generic-rig character with a differently named rig (the fox's, its bones and clips renamed in the Rigify
    /// style) is guessed and prepared, with paw placement on its four legs, hitboxes, eyes, a controller from its clips
    /// and a body fitted from its mesh; its character prefab (New Character's) has that body, held over steps; and the
    /// generic-rig character body fit is round, stacked or side by side as the torso's shape asks, above the step height.
    /// </summary>
    [Category(TestArea.Models)]
    [TestTiming]
    public class GenericRigImporterTests
    {
        private const string Folder = GenericRigModelImporter.ModelsFolder + "/ZzTestCritter";
        private const string FoxModelPath = GenericRigModelImporter.ModelsFolder + "/Fox/Fox.fbx";
        private const string FoxSetupPath = GenericRigModelImporter.ModelsFolder + "/Fox/Fox Setup.asset";

        // The fox's bones as a Rigify-style rig names them: nothing in common with its own names but the words.
        private static readonly Dictionary<string, string> Renamed = new()
        {
            { "Hips", "DEF-pelvis" }, { "Spine1", "DEF-spine" }, { "Spine2", "DEF-spine.001" }, { "Neck", "DEF-neck" }, { "Head", "DEF-head" },
            { "Tail1", "DEF-tail" }, { "Tail2", "DEF-tail.001" }, { "Tail3", "DEF-tail.002" }, { "Tail4", "DEF-tail.003" },
            { "FrontUpperLeg_L", "DEF-upper_arm.L" }, { "FrontLowerLeg_L", "DEF-forearm.L" }, { "FrontPaw_L", "DEF-hand.L" },
            { "FrontUpperLeg_R", "DEF-upper_arm.R" }, { "FrontLowerLeg_R", "DEF-forearm.R" }, { "FrontPaw_R", "DEF-hand.R" },
            { "HindUpperLeg_L", "DEF-thigh.L" }, { "HindLowerLeg_L", "DEF-shin.L" }, { "HindFoot_L", "DEF-foot.L" },
            { "HindUpperLeg_R", "DEF-thigh.R" }, { "HindLowerLeg_R", "DEF-shin.R" }, { "HindFoot_R", "DEF-foot.R" },
        };

        // The fox's clips as the renamed generic-rig character's are named, and the slot each should be guessed for.
        private static readonly (string fox, string renamed, string slot)[] RenamedClips =
        {
            ("Idle", "Critter_Idle_Loop", "Idle"), ("Walk", "critter_walk", "Walk"), ("Trot", "Critter-Trot", "Run"),
            ("Gallop", "CritterGallop", "Sprint"), ("Jump Rise", "jump_up", "Jump Rise"), ("Jump Fall", "fall_loop", "Jump Fall"),
            ("Crouch Idle", "sneak_idle", "Crouch Idle"), ("Crouch Walk", "sneak_forward", "Crouch Walk"),
        };

        [TearDown]
        public void RemoveTheCritter() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void GuessingTheFoxsSetup_FindsTheBonesItsOwnSetupNames()
        {
            var fox = AssetDatabase.LoadAssetAtPath<GenericRigSetup>(FoxSetupPath);
            Assume.That(fox, Is.Not.Null, "Test setup: the fox's setup should be there.");
            var guessed = ScriptableObject.CreateInstance<GenericRigSetup>();
            try
            {
                guessed.model = fox.model;
                GenericRigModelImporter.Guess(guessed);
                Assert.That(guessed.rootBone, Is.EqualTo(fox.rootBone));
                Assert.That(guessed.eyeBone, Is.EqualTo(fox.eyeBone));
                Assert.That(guessed.legs.Select(l => (l.upper, l.lower, l.foot)), Is.EqualTo(fox.legs.Select(l => (l.upper, l.lower, l.foot))),
                    "Its legs, front then hind, left then right, as the fox's setup has them.");
                Assert.That(guessed.legs.Select(l => l.bendDirection), Is.EqualTo(fox.legs.Select(l => l.bendDirection)), "Elbows back, stifles forward.");
                Assert.That(guessed.counterPitchBones.Select(b => b.bone), Is.EquivalentTo(fox.counterPitchBones.Select(b => b.bone)));
                Assert.That(guessed.turnBendBones.Select(b => b.bone), Is.EquivalentTo(fox.turnBendBones.Select(b => b.bone)));
            }
            finally
            {
                if (guessed.animationSet != null) { Object.DestroyImmediate(guessed.animationSet); }
                Object.DestroyImmediate(guessed);
            }
        }

        [Test]
        public void AGenericRigWithDifferentlyNamedBones_IsGuessedAndPrepared()
        {
            string modelPath = MakeCritter();
            GenericRigSetup setup = GenericRigModelImporter.LoadOrMakeSetup(modelPath);

            Assert.That(setup.rootBone, Is.EqualTo("DEF-pelvis"));
            Assert.That(setup.eyeBone, Is.EqualTo("DEF-head"));
            Assert.That(setup.legs.Select(l => l.foot), Is.EqualTo(new[] { "DEF-hand.L", "DEF-hand.R", "DEF-foot.L", "DEF-foot.R" }));
            Assert.That(setup.legs.Select(l => l.upper), Is.EqualTo(new[] { "DEF-upper_arm.L", "DEF-upper_arm.R", "DEF-thigh.L", "DEF-thigh.R" }));
            Assert.That(setup.animationSet, Is.Not.Null, "Its animation set is made from its clips' names.");
            foreach (var (_, renamed, slot) in RenamedClips)
            {
                Assert.That(setup.animationSet.ClipForSlot(slot)?.name, Is.EqualTo(renamed), $"Its {slot}.");
            }

            GenericRigModelImporter.Result result = GenericRigModelImporter.Prepare(setup);
            Assert.That(result.Succeeded, Is.True, result.ToString());
            Assert.That(setup.missingBones, Is.Empty, "Everything its setup names is found.");
            GameObject model = result.source.ModelPrefab;
            Assert.That(model.GetComponent<LegPlacementIK>(), Is.Not.Null, "It has paw placement.");
            Assert.That(new SerializedObject(model.GetComponent<LegPlacementIK>()).FindProperty("legs").arraySize, Is.EqualTo(4));
            Assert.That(model.GetComponentsInChildren<BoneHitbox>(true), Is.Not.Empty, "It has hitboxes.");
            Transform eyes = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == CharacterModelSlot.EyesName);
            Assert.That(eyes, Is.Not.Null);
            Assert.That(eyes.parent.name, Is.EqualTo("DEF-head"), "Its eyes are on its head.");
            Assert.That(model.GetComponent<Animator>().runtimeAnimatorController, Is.SameAs(setup.animationSet.controller), "It plays its set's controller.");

            BodyDefinition body = result.source.BodyDefinition;
            Assert.That(body, Is.Not.Null, "Its body is fitted from its mesh.");
            Assert.That(body.ExtraParts, Is.Not.Empty, "Its body is of several parts (its torso's capsules and its head).");
            float step = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSceneSetup.CharacterPrefabPath).GetComponent<MMotionMotor>().MaxStepHeight;
            foreach (BodyShape part in new[] { body.Shape }.Concat(body.ExtraParts))
            {
                Assert.That(part.kind, Is.Not.EqualTo(BodyShapeKind.Box), "Rounded: no corners to catch on things.");
                Assert.That(part.center.y - part.radius, Is.GreaterThanOrEqualTo(step - 1e-4f), "Held a step above its feet.");
            }

            GameObject prefab = NewCharacter.SaveVariant("Critter", Folder + "/Critter Character.prefab", result.source, null);
            var movement = new SerializedObject(prefab.GetComponent<MMotionMotor>());
            Assert.That(movement.FindProperty("bodyDefinition").objectReferenceValue, Is.SameAs(body), "Its character moves its body.");
            Assert.That(movement.FindProperty("hoverOverSteps").boolValue, Is.True, "Held above its legs over steps.");
            var host = new SerializedObject(prefab.GetComponent<CharacterModelSlot>());
            Assert.That(host.FindProperty("bodyFit").enumValueIndex, Is.EqualTo((int)BodyFit.Off), "Its body as defined, not fitted to the model.");
            Assert.That(host.FindProperty("defaultEyeHeight").floatValue, Is.EqualTo(setup.eyePosition.y));
        }

        [Test]
        public void TheGenericRigBodyFit_IsRoundAndAboveTheStep()
        {
            // A torso deeper than it's wide: capsules stacked, as round as it's wide, from the step to its back.
            BodyShape[] deep = BodyFitting.GenericRig(new Bounds(new Vector3(0f, 0.6f, 0f), new Vector3(0.3f, 0.6f, 1f)), default, false, 0.35f);
            Assert.That(deep.Length, Is.EqualTo(2));
            Assert.That(deep.All(p => p.kind == BodyShapeKind.Capsule && p.direction == 2 && Mathf.Approximately(p.radius, 0.15f) && Mathf.Approximately(p.height, 1f)));
            Assert.That(deep.Min(p => p.center.y - p.radius), Is.EqualTo(0.35f).Within(1e-4f), "Its bottom a step above its feet.");
            Assert.That(deep.Max(p => p.center.y + p.radius), Is.EqualTo(0.9f).Within(1e-4f), "Its top at its back.");

            // A torso wider than it's deep (1 m across, 0.4 m deep): side by side, enough to fill its width.
            BodyShape[] flat = BodyFitting.GenericRig(new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 0.4f, 1f)), default, false, 0.3f);
            Assert.That(flat.Length, Is.EqualTo(3));
            Assert.That(flat.All(p => Mathf.Approximately(p.center.y, 0.5f) && Mathf.Approximately(p.radius, 0.2f)));
            Assert.That(flat.Min(p => p.center.x - p.radius), Is.EqualTo(-0.5f).Within(1e-4f));
            Assert.That(flat.Max(p => p.center.x + p.radius), Is.EqualTo(0.5f).Within(1e-4f));

            // A head: a sphere as round as it's wide, resting on its underside.
            BodyShape[] withHead = BodyFitting.GenericRig(new Bounds(new Vector3(0f, 0.6f, 0f), new Vector3(0.3f, 0.3f, 1f)),
                new Bounds(new Vector3(0f, 0.9f, 0.7f), new Vector3(0.2f, 0.5f, 0.3f)), true, 0.35f);
            BodyShape head = withHead.Last();
            Assert.That(head.kind, Is.EqualTo(BodyShapeKind.Sphere));
            Assert.That(head.radius, Is.EqualTo(0.1f).Within(1e-4f));
            Assert.That(head.center, Is.EqualTo(new Vector3(0f, 0.75f, 0.7f)).Using(Vector3EqualityComparer.Instance));
        }

        /// <summary>
        /// The renamed generic-rig character: the fox's model with its bones renamed (Renamed), saved as a prefab in its own generic-rig character
        /// folder, and its clips copied with their curves bound to the renamed bones (RenamedClips). Returns the model's path.
        /// </summary>
        private static string MakeCritter()
        {
            using (HumanoidModelImporter.Busy())
            {
                if (!AssetDatabase.IsValidFolder(Folder)) { AssetDatabase.CreateFolder(GenericRigModelImporter.ModelsFolder, "ZzTestCritter"); }
                var fox = AssetDatabase.LoadAssetAtPath<GameObject>(FoxModelPath);
                Assume.That(fox, Is.Not.Null, "Test setup: the fox's model should be there.");
                var critter = (GameObject)PrefabUtility.InstantiatePrefab(fox);
                string path = Folder + "/Critter.prefab";
                try
                {
                    PrefabUtility.UnpackPrefabInstance(critter, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    foreach (Transform bone in critter.GetComponentsInChildren<Transform>(true))
                    {
                        if (Renamed.TryGetValue(bone.name, out string name)) { bone.name = name; }
                    }
                    PrefabUtility.SaveAsPrefabAsset(critter, path);
                }
                finally { Object.DestroyImmediate(critter); }

                AnimationClip[] foxClips = AssetDatabase.LoadAllAssetsAtPath(FoxModelPath).OfType<AnimationClip>().ToArray();
                foreach (var (foxName, renamed, _) in RenamedClips)
                {
                    AnimationClip from = foxClips.First(c => c.name == foxName);
                    var clip = new AnimationClip { name = renamed, frameRate = from.frameRate };
                    foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(from))
                    {
                        EditorCurveBinding to = binding;
                        to.path = string.Join("/", binding.path.Split('/').Select(part => Renamed.TryGetValue(part, out string name) ? name : part));
                        AnimationUtility.SetEditorCurve(clip, to, AnimationUtility.GetEditorCurve(from, binding));
                    }
                    AnimationUtility.SetAnimationClipSettings(clip, AnimationUtility.GetAnimationClipSettings(from));
                    AssetDatabase.CreateAsset(clip, $"{Folder}/{renamed}.anim");
                }
                AssetDatabase.SaveAssets();
                return path;
            }
        }
    }
}
