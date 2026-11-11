using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Tests with models made differently from the prepared ones, to check the character copes with how models come:
    /// made facing another way, away from their origin, or with their armature scaled unevenly. The variants are built
    /// at runtime from the character's own model: posed in its reference pose, with the node above its skeleton changed, and given an
    /// avatar built from that, as importing such a file would. Their sources haven't been measured, so they measure
    /// their models as they load.
    /// </summary>
    public class ModelVariantTests
    {
        private CharacterTestRig rig;
        private readonly List<Object> created = new();

        [SetUp]
        public void SetUp() => rig = new CharacterTestRig();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            rig.Dispose();
            foreach (Object obj in created) { if (obj != null) { Object.Destroy(obj); } }
            created.Clear();
            yield return null;
        }

        /// <summary>
        /// A source for a copy of the basis source's model, posed in its avatar's reference pose with the node above its
        /// skeleton changed, and an avatar built from that.
        /// </summary>
        private DirectCharacterModelSource Variant(DirectCharacterModelSource basis, Action<Transform> changeArmature)
        {
            GameObject template = Object.Instantiate(basis.ModelPrefab);
            created.Add(template);
            template.SetActive(false); // Only a template to copy.
            template.name = basis.ModelPrefab.name;
            HumanDescription description = template.GetComponentInChildren<Animator>(true).avatar.humanDescription;
            Dictionary<string, Transform> transforms = template.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name, t => t);
            foreach (SkeletonBone bone in description.skeleton)
            {
                if (!transforms.TryGetValue(bone.name, out Transform t) || t == template.transform) { continue; }
                t.SetLocalPositionAndRotation(bone.position, bone.rotation);
                t.localScale = bone.scale;
            }
            string hips = description.human.First(h => h.humanName == HumanTrait.BoneName[(int)HumanBodyBones.Hips]).boneName;
            Transform armature = transforms[hips].parent;
            Assert.That(armature, Is.Not.EqualTo(template.transform), "Test setup: the model needs a node above its skeleton.");
            changeArmature(armature);
            description.skeleton = template.GetComponentsInChildren<Transform>(true)
                .Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale })
                .ToArray();
            Avatar avatar = AvatarBuilder.BuildHumanAvatar(template, description);
            created.Add(avatar);
            Assert.That(avatar.isValid && avatar.isHuman, Is.True, "Test setup: the variant's avatar should build.");

            var source = ScriptableObject.CreateInstance<DirectCharacterModelSource>();
            created.Add(source);
            source.ModelPrefab = template;
            source.Avatar = avatar;
            source.Translator = basis.Translator;
            return source;
        }

        private Animator animator;

        /// <summary>Spawns the character on a floor, loads the variant, and stands it idle a moment at the given scale.</summary>
        private IEnumerator StandVariant(Action<Transform> changeArmature, Vector3 scale)
        {
            rig.Floor();
            rig.SpawnCharacter(Vector3.zero);
            yield return rig.WaitForModel();
            yield return new WaitForFixedUpdate();
            // Variants of the character's own model, which has to be a humanoid its source references directly.
            if (rig.Models.ModelSource is not DirectCharacterModelSource own || own.ModelPrefab == null
                || rig.Models.Animator == null || !rig.Models.Animator.isHuman)
            {
                Assert.Ignore("The character's model isn't a humanoid its source references directly.");
                yield break;
            }
            DirectCharacterModelSource variant = Variant(own, changeArmature);
            bool loaded = false;
            yield return CharacterTestRig.WaitFor(rig.Models.LoadModelAsync(variant), l => loaded = l);
            Assert.That(loaded, Is.True);
            Assert.That(rig.Controller.SetScale(scale, immediately: true), Is.True);
            rig.Body.interpolation = RigidbodyInterpolation.None;
            animator = rig.Models.Animator;
            animator.enabled = false;
            yield return rig.Run(1f, _ => CharacterTestRig.Idle(), _ =>
            {
                rig.Animation.UpdateAnimator(rig.Movement.lastFrameMovementData, Time.fixedDeltaTime);
                animator.Update(Time.fixedDeltaTime);
            });
        }

        private Vector3 Bone(HumanBodyBones bone) => animator.GetBoneTransform(bone).position;

        /// <summary>The lowest and highest points of the model's meshes as posed now.</summary>
        private (float bottom, float top) Extent()
        {
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            var baked = new Mesh();
            foreach (SkinnedMeshRenderer skinned in rig.Models.Model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skinned.BakeMesh(baked, false);
                Matrix4x4 toWorld = Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
                foreach (Vector3 vertex in baked.vertices)
                {
                    float y = toWorld.MultiplyPoint3x4(vertex).y;
                    bottom = Mathf.Min(bottom, y);
                    top = Mathf.Max(top, y);
                }
            }
            Object.Destroy(baked);
            return (bottom, top);
        }

        /// <summary>
        /// Humanoid animation keeps the facing a model was made with, so a model made facing another way would play
        /// facing that way: its source turns it to face forward. (Made away from its origin, the animation centres it.)
        /// </summary>
        [UnityTest]
        public IEnumerator AModelMadeFacingAnotherWay_OrOffCentre_FacesForward_AndStandsOnTheSpot([Values(90f, 180f, -90f)] float madeFacing)
        {
            yield return StandVariant(armature =>
            {
                armature.localRotation = Quaternion.Euler(0f, madeFacing, 0f) * armature.localRotation;
                armature.localPosition += new Vector3(0.3f, 0f, 0.2f);
            }, Vector3.one);

            Vector3 left = Vector3.ProjectOnPlane(Bone(HumanBodyBones.LeftUpperLeg) - Bone(HumanBodyBones.RightUpperLeg), Vector3.up);
            float facing = Vector3.SignedAngle(rig.Character.transform.forward, Vector3.Cross(Vector3.up, left), Vector3.up);
            Assert.That(Mathf.Abs(facing), Is.LessThan(5f), $"It should face the character's forward, but faces {facing:0} degrees from it.");
            Vector3 feet = (Bone(HumanBodyBones.LeftFoot) + Bone(HumanBodyBones.RightFoot)) / 2f - rig.Position;
            Assert.That(new Vector2(feet.x, feet.z).magnitude, Is.LessThan(0.03f), "Its feet should be under the character.");
            Assert.That(Mathf.Abs(Extent().bottom), Is.LessThan(0.002f), "Its soles should be on the floor.");
        }

        private static readonly Vector3[] Stretches = { new(1f, 2f, 1f), new(1f, 0.5f, 1f), new(2f, 1f, 0.5f) };

        /// <summary>
        /// An armature scaled unevenly (taller than wide, say) stretches the model, and stretching the character
        /// stretches it the same way on top: it stays in proportion, with its feet on the ground.
        /// </summary>
        [UnityTest]
        public IEnumerator AModelWithAnUnevenlyScaledArmature_StaysInProportion_WhenStretched([ValueSource(nameof(Stretches))] Vector3 stretch)
        {
            void Taller(Transform armature) => armature.localScale = new Vector3(1f, 1.25f, 1f);
            yield return StandVariant(Taller, Vector3.one);
            (float bottom, float top) normal = Extent();
            float normalHips = Bone(HumanBodyBones.Hips).y;
            rig.Dispose();
            yield return null;
            rig = new CharacterTestRig();

            yield return StandVariant(Taller, stretch);
            (float bottom, float top) stretched = Extent();
            Assert.That(stretched.top / stretch.y, Is.EqualTo(normal.top).Within(normal.top * 0.005f), "It should be as tall, for its stretch.");
            Assert.That(Bone(HumanBodyBones.Hips).y / stretch.y, Is.EqualTo(normalHips).Within(normalHips * 0.005f), "Its hips should be as high, for its stretch.");
            Assert.That(Mathf.Abs(stretched.bottom), Is.LessThan(0.002f * stretch.y), "Its soles should be on the floor.");
        }
    }
}
