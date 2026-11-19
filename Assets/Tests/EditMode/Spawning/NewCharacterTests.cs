using System.Collections.Generic;
using Character;
using CharacterShowcase.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.Tests.EditMode.Spawning
{
    /// <summary>
    /// Tests of New Character (NewCharacter): it makes a prefab variant of Character.prefab wearing the chosen model, in
    /// its own folder; its movement is a copy of the default's (its own to tune) or the default itself, shared; with an
    /// animation set, its model is a source of its own playing the set's controller (the shared source left as it is);
    /// and answers it can't make are refused, saying why.
    /// </summary>
    [Category(TestArea.Spawning)]
    [TestTiming]
    public class NewCharacterTests
    {
        private const string Name = "Zz New Character Test";
        private bool madeCharactersFolder;

        [SetUp]
        public void NoteTheFolder() => madeCharactersFolder = !AssetDatabase.IsValidFolder(NewCharacter.CharactersFolder);

        [TearDown]
        public void RemoveWhatItMade()
        {
            AssetDatabase.DeleteAsset(NewCharacter.FolderOf(Name));
            AssetDatabase.DeleteAsset("Assets/ZzNewCharacterTests");
            if (madeCharactersFolder) { AssetDatabase.DeleteAsset(NewCharacter.CharactersFolder); }
        }

        private static GameObject CharacterPrefab => AssetDatabase.LoadAssetAtPath<GameObject>(SceneGameSetup.CharacterPrefabPath);
        private static CharacterModelSource Mannequin => AssetDatabase.LoadAssetAtPath<CharacterModelSource>("Assets/Models/CharacterModels/Mannequin/Mannequin.asset");

        [Test]
        public void ItMakes_AVariantOfTheCharacterPrefab_WearingTheModel_WithItsOwnMovement()
        {
            Assume.That(Mannequin, Is.Not.Null, "Test setup: the mannequin's source should be there.");
            var answers = new NewCharacter.Answers { name = Name, modelSource = Mannequin, movement = NewCharacter.Movement.CopyTheDefault };
            Assert.That(NewCharacter.Problem(answers), Is.Null);
            var made = new List<string>();
            GameObject prefab = NewCharacter.Make(answers, made);

            Assert.That(AssetDatabase.GetAssetPath(prefab), Is.EqualTo($"{NewCharacter.FolderOf(Name)}/{Name}.prefab"));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(prefab), Is.SameAs(CharacterPrefab), "It's a variant of Character.prefab.");
            Assert.That(prefab.GetComponent<CharacterModelHost>().ModelSource, Is.SameAs(Mannequin));
            MovementMode mode = prefab.GetComponent<MovementController>().movementHandler;
            Assert.That(AssetDatabase.GetAssetPath(mode), Is.EqualTo($"{NewCharacter.FolderOf(Name)}/{Name} Movement.asset"), "Its movement is its own copy.");
            Assert.That(mode, Is.Not.SameAs(CharacterPrefab.GetComponent<MovementController>().movementHandler));
            Assert.That(made, Is.Not.Empty, "It lists what it made.");
        }

        [Test]
        public void SharingTheDefaultMovement_UsesTheDefaultsAsset()
        {
            GameObject prefab = NewCharacter.Make(new NewCharacter.Answers { name = Name, modelSource = Mannequin, movement = NewCharacter.Movement.ShareTheDefault }, new List<string>());
            Assert.That(prefab.GetComponent<MovementController>().movementHandler, Is.SameAs(CharacterPrefab.GetComponent<MovementController>().movementHandler));
        }

        [Test]
        public void WithAnAnimationSet_ItsModelIsItsOwn_PlayingTheSetsController()
        {
            AssetDatabase.CreateFolder("Assets", "ZzNewCharacterTests");
            var set = ScriptableObject.CreateInstance<CharacterAnimationSet>();
            set.layout = AssetDatabase.LoadAssetAtPath<AnimatorLayout>(CharacterModelImporter.LayoutPath);
            AssetDatabase.CreateAsset(set, "Assets/ZzNewCharacterTests/Set.asset");
            RuntimeAnimatorController shared = Mannequin.AnimatorController;

            GameObject prefab = NewCharacter.Make(new NewCharacter.Answers { name = Name, modelSource = Mannequin, animationSet = set }, new List<string>());
            CharacterModelSource own = prefab.GetComponent<CharacterModelHost>().ModelSource;
            Assert.That(own, Is.Not.SameAs(Mannequin), "Its model is a source of its own.");
            Assert.That(own.AnimatorController, Is.SameAs(set.controller).And.Not.Null, "It plays the set's controller.");
            Assert.That(Mannequin.AnimatorController, Is.SameAs(shared), "The shared source is left as it was.");
        }

        [Test]
        public void AnswersItCantMake_AreRefused_SayingWhy()
        {
            Assert.That(NewCharacter.Problem(new NewCharacter.Answers { name = "", modelSource = Mannequin }), Does.Contain("name"));
            Assert.That(NewCharacter.Problem(new NewCharacter.Answers { name = Name }), Does.Contain("model"));
            NewCharacter.Make(new NewCharacter.Answers { name = Name, modelSource = Mannequin }, new List<string>());
            Assert.That(NewCharacter.Problem(new NewCharacter.Answers { name = Name, modelSource = Mannequin }), Does.Contain("already"));
        }
    }
}
