using MeridianMotion.Characters;
using MeridianMotion.EditorTools;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MeridianMotion.Showcase.TutorialRing.EditorTools
{
    /// <summary>
    /// Adds the Tutorial Ring's game to its scene as Set Up Scene sets it up (registered with
    /// CharacterSceneSetup.AfterSettingUp, as the showcase HUD is, so nothing that ships names it): a "Tutorial Ring"
    /// object in the setup with its TutorialRingSetup (the lessons), TutorialPromptPresenter (the Lesson Prompt),
    /// TutorialRingDevKeys and HeightShade (darker low down); the scene's denser haze (SetHaze); the spawner's On Character Spawned connected to TutorialRingSetup.CharacterSpawned; and the
    /// spawner's Tuning Preset set to the tutorial's one jump. Only for the Tutorial Ring's scene (ScenePath). The scene is
    /// built from its spec, so this is what puts them back after each rebuild.
    /// </summary>
    [InitializeOnLoad]
    public static class TutorialRingSceneSetup
    {
        /// <summary>The Tutorial Ring's scene.</summary>
        public const string ScenePath = "Assets/Showcase/Scenes/TutorialRing/TutorialRing.unity";
        /// <summary>The name of the object its game is on, in the scene's setup.</summary>
        public const string ObjectName = "Tutorial Ring";
        /// <summary>
        /// The scene's haze (exponential fog density): denser than the other levels' (the level builder's), so the open
        /// ground the ring stands over fades into it rather than ending in a hard edge against the sky.
        /// </summary>
        public const float HazeDensity = 0.003f;
        /// <summary>The haze's colour, and the sky's below the horizon, so the two meet without a line.</summary>
        public static readonly Color HazeColour = new(0.72f, 0.79f, 0.86f);

        static TutorialRingSceneSetup() => CharacterSceneSetup.AfterSettingUp += AddTutorial;

        /// <summary>
        /// The scene's haze (HazeDensity, HazeColour), and its sky below the horizon the haze's colour. Fog is a scene's own
        /// setting, and its sky material its own (beside it, in its Environment folder), so no other level changes.
        /// </summary>
        public static void SetHaze(Scene scene)
        {
            if (SceneManager.GetActiveScene() != scene) { return; }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = HazeDensity;
            RenderSettings.fogColor = HazeColour;
            Material sky = RenderSettings.skybox;
            if (sky != null && sky.HasProperty("_GroundColor") && AssetDatabase.GetAssetPath(sky).StartsWith(System.IO.Path.GetDirectoryName(ScenePath).Replace('\\', '/') + "/"))
            {
                sky.SetColor("_GroundColor", HazeColour);
                EditorUtility.SetDirty(sky);
            }
        }

        /// <summary>Adds the tutorial's game to the Tutorial Ring's scene, as Set Up Scene has just set it up (see the class).</summary>
        public static void AddTutorial(CharacterSceneSetup.SetupContext context)
        {
            if (context.scene.path != ScenePath) { return; }
            var catalog = AssetDatabase.LoadAssetAtPath<TutorialLessonCatalog>(TutorialRingAssets.CatalogPath);
            var preset = AssetDatabase.LoadAssetAtPath<MovementTuningPreset>(TutorialRingAssets.PresetPath);
            var prompt = AssetDatabase.LoadAssetAtPath<GameObject>(TutorialRingAssets.PromptPath);
            if (catalog == null || preset == null || prompt == null)
            {
                context.notes.Add("The Tutorial Ring's game wasn't added: its assets are missing (Tools > Meridian Motion > Showcase > Tutorial Ring > Create Tutorial Ring Assets makes them).");
                return;
            }
            Transform holder = context.setup.Find(ObjectName);
            if (holder == null)
            {
                holder = new GameObject(ObjectName).transform;
                holder.SetParent(context.setup, false);
                if (context.withUndo) { Undo.RegisterCreatedObjectUndo(holder.gameObject, "Set Up Scene"); }
            }
            GameObject obj = holder.gameObject;
            TutorialRingSetup setup = Need<TutorialRingSetup>(obj, context.withUndo);
            TutorialPromptPresenter presenter = Need<TutorialPromptPresenter>(obj, context.withUndo);
            TutorialRingDevKeys keys = Need<TutorialRingDevKeys>(obj, context.withUndo);
            // Darker low down, untinted at the walls' tops (the defaults are the ring's: see HeightShade).
            Need<HeightShade>(obj, context.withUndo);
            SetHaze(context.scene);
            setup.Catalog = catalog;
            setup.Presenter = presenter;
            presenter.PromptPrefab = prompt;
            if (context.inputReader != null && context.inputReader.playerInput != null) { presenter.Actions = context.inputReader.playerInput.actions; }
            keys.Setup = setup;
            EditorUtility.SetDirty(setup);
            EditorUtility.SetDirty(presenter);
            EditorUtility.SetDirty(keys);

            CharacterSpawner spawner = context.spawner;
            if (spawner != null)
            {
                if (context.withUndo) { Undo.RecordObject(spawner, "Set Up Scene"); }
                spawner.TuningPreset = preset;
                if (!Connected(spawner, setup)) { UnityEventTools.AddPersistentListener(spawner.OnCharacterSpawned, setup.CharacterSpawned); }
                EditorUtility.SetDirty(spawner);
            }
            context.notes.Add("Added the Tutorial Ring's game (its lessons and prompts, the session log, the development keys) and its character's tuning (one jump).");
        }

        private static bool Connected(CharacterSpawner spawner, TutorialRingSetup setup)
        {
            for (int i = 0; i < spawner.OnCharacterSpawned.GetPersistentEventCount(); i++)
            {
                if (spawner.OnCharacterSpawned.GetPersistentTarget(i) == setup &&
                    spawner.OnCharacterSpawned.GetPersistentMethodName(i) == nameof(TutorialRingSetup.CharacterSpawned)) { return true; }
            }
            return false;
        }

        private static T Need<T>(GameObject obj, bool withUndo) where T : Component
        {
            if (obj.TryGetComponent(out T existing)) { return existing; }
            return withUndo ? Undo.AddComponent<T>(obj) : obj.AddComponent<T>();
        }
    }
}
