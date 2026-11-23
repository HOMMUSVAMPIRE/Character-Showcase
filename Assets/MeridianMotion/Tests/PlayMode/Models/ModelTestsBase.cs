using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MeridianMotion.Characters;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace MeridianMotion.Tests.Models
{
    /// <summary>
    /// What the character model's fixtures share (ModelTests, ModelFitTests): Unity's frames, objects made by a test and
    /// cleaned up after it (Track), and the model sources they load (a plain cube's, the character's own, fitted to a
    /// height, or one that never manages to make a model).
    /// </summary>
    public abstract class ModelTestsBase : CharacterRigFixture
    {
        // Its tests need Unity's frames (the model's, animation's or IK's per-frame updates): see CharacterRigFixture.MovementOnly.
        protected override bool MovementOnly => false;

        protected readonly List<Object> created = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Object obj in created) { if (obj != null) { Object.Destroy(obj); } }
            created.Clear();
            yield return null;
        }

        protected T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        /// <summary>A source whose model is a plain cube: no Animator, no humanoid rig.</summary>
        protected PrefabModelSource CubeSource()
        {
            GameObject template = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            template.name = "Cube Model";
            Object.Destroy(template.GetComponent<Collider>());
            template.SetActive(false); // Only a template to copy; copies are activated below.
            var source = Track(ScriptableObject.CreateInstance<PrefabModelSource>());
            source.ModelPrefab = template;
            return source;
        }

        /// <summary>Never manages to create a model.</summary>
        protected class FailingSource : CharacterModelSource
        {
            public override async Awaitable<LoadedCharacterModel> CreateModelAsync(CharacterModelContext context, CancellationToken cancellationToken)
            {
                await Awaitable.NextFrameAsync(cancellationToken);
                return null;
            }
        }

        /// <summary>
        /// The character's own model's source, which these tests copy to try its settings out on the same model; skips the
        /// test if the source doesn't reference its model directly (it loads it from somewhere else, say).
        /// </summary>
        protected PrefabModelSource OwnSource()
        {
            if (rig.Models.ModelSource is PrefabModelSource own && own.ModelPrefab != null) { return own; }
            Assert.Ignore("The character's model source doesn't reference its model directly.");
            return null;
        }

        /// <summary>A source for the character's own model, fitted to the given height.</summary>
        protected PrefabModelSource Fitted(float height)
        {
            PrefabModelSource own = OwnSource();
            var fitted = Track(ScriptableObject.CreateInstance<PrefabModelSource>());
            fitted.ModelPrefab = own.ModelPrefab;
            fitted.Translator = own.Translator;
            fitted.AnimatorController = own.AnimatorController;
            fitted.Avatar = own.Avatar;
            fitted.GroundOffset = own.GroundOffset;
            fitted.AnimatedGroundOffset = own.AnimatedGroundOffset;
            fitted.MeasureGroundOnLoad = own.MeasureGroundOnLoad;
            fitted.Height = ModelHeight.FitToHeight;
            fitted.FitHeight = height;
            return fitted;
        }

        /// <summary>A source for the character's own model (or the given prefab), as its own, but a separate asset.</summary>
        protected PrefabModelSource OwnCopy(GameObject prefab = null)
        {
            PrefabModelSource own = OwnSource();
            var copy = Track(ScriptableObject.CreateInstance<PrefabModelSource>());
            copy.ModelPrefab = prefab != null ? prefab : own.ModelPrefab;
            copy.Translator = own.Translator;
            copy.AnimatorController = own.AnimatorController;
            copy.Avatar = own.Avatar;
            copy.GroundOffset = own.GroundOffset;
            copy.AnimatedGroundOffset = own.AnimatedGroundOffset;
            copy.MeasureGroundOnLoad = false;
            return copy;
        }
    }
}
