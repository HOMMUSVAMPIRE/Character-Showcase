using System.Collections.Generic;
using System.Linq;
using MeridianMotion.Characters;
using MeridianMotion.Showcase.Audio;
using MeridianMotion.Showcase.CharacterAudio;
using MeridianMotion.Tests;
using NUnit.Framework;
using MeridianMotion.Showcase.UIAudio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MeridianMotion.Showcase.Tests.Audio
{
    /// <summary>
    /// Checks of what Create Audio Assets makes (AudioAssets, in the audio's editor tools), once it's been run: every row
    /// of the characters' Sound Sets names a real movement event or state; every level material is mapped to a surface;
    /// every Sound Event is in the library, with an id of its own; and the Audio prefab has a service with the library and a
    /// music player. Skipped where the assets haven't been made. And a showcase scene Set Up Scene has set up has its
    /// audio, once (see ASetUpShowcaseScene_HasItsAudio_Once). EditMode: they read assets and a scene in the editor.
    /// </summary>
    [Category(TestArea.Audio)]
    [TestTiming]
    public class AudioAssetsTests
    {
        private const string Folder = MeridianMotion.Showcase.Audio.EditorTools.AudioAssets.Folder;
        private const string LibraryPath = Folder + "/Sound Library.asset";
        private const string PrefabPath = Folder + "/Audio.prefab";
        private const string MapPath = Folder + "/Surfaces/Surface Map.asset";
        private static readonly string[] SetPaths = { Folder + "/Mannequin Sound Set.asset", Folder + "/Fox Sound Set.asset" };

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assume.That(asset, Is.Not.Null, $"{path} isn't there: run Tools > Meridian Motion > Showcase > Create Audio Assets.");
            return asset;
        }

        [Test]
        public void EverySoundSetRow_NamesARealFlag()
        {
            foreach (string path in SetPaths)
            {
                CharacterSoundSet set = Load<CharacterSoundSet>(path);
                Assert.That(set.Events, Is.Not.Empty, path);
                foreach (EventSound row in set.Events)
                {
                    Assert.That(MovementEvents.TryParse(row.eventName, out _), $"{path}: '{row.eventName}' isn't a movement event");
                    Assert.That(row.sound, Is.Not.Null, $"{path}: '{row.eventName}' has no sound");
                }
                foreach (StateLoop row in set.Loops)
                {
                    Assert.That(CharacterState.TryParse(row.stateName, out _), $"{path}: '{row.stateName}' isn't a state");
                    Assert.That(row.loop != null && row.loop.Loop, $"{path}: '{row.stateName}' has no looping sound");
                }
                Assert.That(set.Footsteps.step, Is.Not.Null, $"{path}: no footstep");
                Assert.That(set.Surfaces, Is.Not.Null, $"{path}: no surface map");
            }
        }

        [Test]
        public void EveryLevelMaterial_IsMapped()
        {
            SurfaceMap map = Load<SurfaceMap>(MapPath);
            string[] materials = AssetDatabase.FindAssets("t:Material Level_", new[] { "Assets/Showcase/Materials/Level" })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            Assume.That(materials, Is.Not.Empty);
            foreach (string path in materials)
            {
                if (Load<Material>(path).name.StartsWith("Level_Water")) { continue; }
                Assert.That(map.SurfaceOfMaterial(Load<Material>(path)), Is.Not.Null, $"{path} isn't mapped");
            }
            Assert.That(map.Fallback, Is.Not.Null);
            Assert.That(map.Grass, Is.Not.Null);
        }

        [Test]
        public void EverySoundEvent_IsInTheLibrary_WithAUniqueNonZeroId()
        {
            SoundLibrary library = Load<SoundLibrary>(LibraryPath);
            var inLibrary = new HashSet<SoundEvent>(library.Sounds);
            var ids = new HashSet<int>();
            foreach (string guid in AssetDatabase.FindAssets("t:SoundEvent", new[] { Folder }))
            {
                var sound = AssetDatabase.LoadAssetAtPath<SoundEvent>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(inLibrary.Contains(sound), $"{sound.name} isn't in the library");
                Assert.That(sound.Id.IsNone, Is.False, $"{sound.name} has no id");
                Assert.That(ids.Add(sound.Id.Value), $"{sound.name}'s id {sound.Id.Value} is another's too");
            }
            Assert.That(library.Buses.Count, Is.EqualTo(3));
        }

        [Test]
        public void TheAudioPrefab_HasAServiceWithTheLibrary_AndAMusicPlayer()
        {
            GameObject prefab = Load<GameObject>(PrefabPath);
            Assert.That(prefab.GetComponent<AudioService>().Library, Is.SameAs(Load<SoundLibrary>(LibraryPath)));
            Assert.That(prefab.GetComponent<MusicPlayer>(), Is.Not.Null);
        }

        /// <summary>
        /// The Tutorial Ring, as Set Up Scene left it, has its audio once: the service and music player, the director (with
        /// its sets, connected to the spawner once each), the level's and the UI's sounds and its music. Running the audio's
        /// step of Set Up Scene again adds nothing. Opened in the editor, not played (a level played in the tests is in
        /// the way of those after it), and closed unsaved.
        /// </summary>
        [Test]
        public void ASetUpShowcaseScene_HasItsAudio_Once()
        {
            const string scenePath = "Assets/Showcase/Scenes/TutorialRing/TutorialRing.unity";
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath), Is.Not.Null);
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            try
            {
                CharacterSpawner spawner = Object.FindAnyObjectByType<CharacterSpawner>(FindObjectsInactive.Include);
                Assume.That(spawner, Is.Not.Null, "the scene's set up");
                CheckOnce(spawner);

                CharacterAudioDirector director = Object.FindAnyObjectByType<CharacterAudioDirector>(FindObjectsInactive.Include);
                var context = new MeridianMotion.EditorTools.CharacterSceneSetup.SetupContext
                {
                    scene = scene,
                    setup = director.transform.parent,
                    spawner = spawner,
                    withUndo = false,
                };
                MeridianMotion.Showcase.Audio.EditorTools.AudioSceneSetup.AddAudio(context);
                CheckOnce(spawner);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        /// <summary>
        /// The clips Tools/Audio/make_sfx.py generates (Assets/Showcase/Audio/Clips) are on their Sound Events (Assign
        /// Generated Clips): every variation of each, a surface's on its surface's Sound Event, a loop's alone and looping.
        /// Skipped where the clips haven't been generated.
        /// </summary>
        [Test]
        public void TheGeneratedClips_AreOnTheirSoundEvents()
        {
            Assume.That(AssetDatabase.IsValidFolder(Folder + "/Clips"), "No generated clips: run Tools/Audio/make_sfx.py.");
            SoundEvent Event(string path) => Load<SoundEvent>($"{Folder}/Sounds/Character/{path}.asset");
            string[] Names(SoundEvent sound) => sound.Clips.Select(c => c != null ? c.name : "(none)").ToArray();

            Assert.That(Names(Event("Footstep (Concrete)")), Is.EqualTo(Enumerable.Range(1, 6).Select(i => $"footstep_concrete_{i:00}")));
            Assert.That(Names(Event("Footstep (Metal)")), Has.All.StartsWith("footstep_metal_"));
            Assert.That(Names(Event("Footstep (Grass)")), Has.All.StartsWith("footstep_grass_"));
            Assert.That(Names(Event("Land (Hard) (Concrete)")), Is.EqualTo(Enumerable.Range(1, 3).Select(i => $"land_hard_concrete_{i:00}")));
            Assert.That(Names(Event("Ledge Grab")), Is.EqualTo(Enumerable.Range(1, 3).Select(i => $"ledge_grab_{i:00}")));
            Assert.That(Names(Event("Splash")), Has.All.StartsWith("splash_"));
            SoundEvent zipline = Event("Zipline Loop");
            Assert.That(Names(zipline), Is.EqualTo(new[] { "zipline_loop" }));
            Assert.That(zipline.Loop, Is.True);
            Assert.That(Names(Event("Slide Loop")), Is.EqualTo(new[] { "slide_concrete_loop" }));
            // Surfaces the generator has no sounds for take the nearest it has.
            Assert.That(Names(Event("Footstep (Glass)")), Has.All.StartsWith("footstep_concrete_"));
            Assert.That(Names(Event("Footstep (Soil)")), Has.All.StartsWith("footstep_grass_"));
        }

        private static void CheckOnce(CharacterSpawner spawner)
        {
            T[] All<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(All<AudioService>().Length, Is.EqualTo(1));
            Assert.That(All<MusicPlayer>().Length, Is.EqualTo(1));
            CharacterAudioDirector[] directors = All<CharacterAudioDirector>();
            Assert.That(directors.Length, Is.EqualTo(1));
            Assert.That(directors[0].DefaultSet, Is.Not.Null);
            Assert.That(All<LevelSounds>().Length, Is.EqualTo(1));
            Assert.That(All<UISounds>().Length, Is.EqualTo(1));
            SceneMusic[] music = All<SceneMusic>();
            Assert.That(music.Length, Is.EqualTo(1));
            Assert.That(music[0].Track, Is.Not.Null);
            Assert.That(Listeners(spawner.OnCharacterSpawned, directors[0], nameof(CharacterAudioDirector.AttachPlayer)), Is.EqualTo(1));
            Assert.That(Listeners(spawner.OnNpcSpawned, directors[0], nameof(CharacterAudioDirector.AttachNpc)), Is.EqualTo(1));
        }

        private static int Listeners(UnityEngine.Events.UnityEventBase spawned, Object target, string method)
        {
            int count = 0;
            for (int i = 0; i < spawned.GetPersistentEventCount(); i++)
            {
                if (spawned.GetPersistentTarget(i) == target && spawned.GetPersistentMethodName(i) == method) { count++; }
            }
            return count;
        }
    }
}
