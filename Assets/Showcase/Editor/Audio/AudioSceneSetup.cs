using MeridianMotion.Characters;
using MeridianMotion.EditorTools;
using MeridianMotion.Showcase.CharacterAudio;
using MeridianMotion.Showcase.UIAudio;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

namespace MeridianMotion.Showcase.Audio.EditorTools
{
    /// <summary>
    /// Adds the audio to the showcase's scenes as Set Up Scene sets them up (registered with
    /// CharacterSceneSetup.AfterSettingUp, so nothing that ships names the audio): in the setup, the Audio prefab (the
    /// service and the music player), and an "Audio" object with the Character Audio Director (connected to the spawner's
    /// On Character Spawned and On Npc Spawned), the Level Sounds, the UI Sounds and the scene's Scene Music (its track,
    /// by the scene's name: AudioAssets.SceneTracks). Each once. Scenes outside Assets/Showcase get nothing; without the
    /// audio's assets (Create Audio Assets), it says so and adds nothing.
    /// </summary>
    [InitializeOnLoad]
    public static class AudioSceneSetup
    {
        /// <summary>Where the showcase's scenes are: only they get the audio.</summary>
        public const string ShowcaseFolder = "Assets/Showcase/";
        /// <summary>The name of the setup's object holding the scene's audio components.</summary>
        public const string AudioObjectName = "Audio";

        static AudioSceneSetup() => CharacterSceneSetup.AfterSettingUp += AddAudio;

        /// <summary>Adds the audio to a showcase scene Set Up Scene has just set up (see the class).</summary>
        public static void AddAudio(CharacterSceneSetup.SetupContext context)
        {
            if (string.IsNullOrEmpty(context.scene.path) || !context.scene.path.StartsWith(ShowcaseFolder)) { return; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AudioAssets.PrefabPath);
            var mannequin = AssetDatabase.LoadAssetAtPath<CharacterSoundSet>(AudioAssets.MannequinSetPath);
            if (prefab == null || mannequin == null)
            {
                context.notes.Add("No audio: its assets aren't there (Tools > Meridian Motion > Showcase > Create Audio Assets).");
                return;
            }

            if (context.setup.GetComponentInChildren<AudioService>(true) == null)
            {
                var service = (GameObject)PrefabUtility.InstantiatePrefab(prefab, context.setup);
                if (context.withUndo) { Undo.RegisterCreatedObjectUndo(service, "Set Up Scene"); }
            }

            Transform audio = context.setup.Find(AudioObjectName);
            if (audio == null)
            {
                audio = new GameObject(AudioObjectName).transform;
                audio.SetParent(context.setup, false);
                if (context.withUndo) { Undo.RegisterCreatedObjectUndo(audio.gameObject, "Set Up Scene"); }
            }
            CharacterAudioDirector director = Component<CharacterAudioDirector>(audio);
            director.DefaultSet = mannequin;
            var fox = AssetDatabase.LoadAssetAtPath<CharacterSoundSet>(AudioAssets.FoxSetPath);
            director.ByPrefab = fox != null
                ? new[] { new CharacterAudioDirector.PrefabSoundSet { prefabName = "Fox", set = fox } }
                : new CharacterAudioDirector.PrefabSoundSet[0];
            LevelSounds level = Component<LevelSounds>(audio);
            level.Sounds = AssetDatabase.LoadAssetAtPath<LevelSoundSet>(AudioAssets.LevelSetPath);
            level.Director = director;
            Component<UISounds>(audio).Sounds = AssetDatabase.LoadAssetAtPath<UISoundSet>(AudioAssets.UISetPath);
            string trackPath = AudioAssets.TrackPathFor(context.scene.name);
            var track = trackPath != null ? AssetDatabase.LoadAssetAtPath<MusicTrack>(trackPath) : null;
            if (track != null) { Component<SceneMusic>(audio).Track = track; }
            foreach (Component component in audio.GetComponents<Component>()) { EditorUtility.SetDirty(component); }

            if (context.spawner != null)
            {
                CharacterSpawner spawner = context.spawner;
                if (context.withUndo) { Undo.RecordObject(spawner, "Set Up Scene"); }
                if (!Connected(spawner.OnCharacterSpawned, director, nameof(CharacterAudioDirector.AttachPlayer)))
                {
                    UnityEventTools.AddPersistentListener(spawner.OnCharacterSpawned, director.AttachPlayer);
                }
                if (!Connected(spawner.OnNpcSpawned, director, nameof(CharacterAudioDirector.AttachNpc)))
                {
                    UnityEventTools.AddPersistentListener(spawner.OnNpcSpawned, director.AttachNpc);
                }
                EditorUtility.SetDirty(spawner);
            }
            context.notes.Add(track != null ? $"The audio, with its music ({track.name})." : "The audio (no music for this scene).");
        }

        private static T Component<T>(Transform on) where T : Component =>
            on.TryGetComponent(out T found) ? found : on.gameObject.AddComponent<T>();

        private static bool Connected(UnityEventBase spawned, Object target, string method)
        {
            for (int i = 0; i < spawned.GetPersistentEventCount(); i++)
            {
                if (spawned.GetPersistentTarget(i) == target && spawned.GetPersistentMethodName(i) == method) { return true; }
            }
            return false;
        }
    }
}
