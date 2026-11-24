using MeridianMotion.Characters;
using MeridianMotion.EditorTools;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

namespace MeridianMotion.Showcase.HUD.EditorTools
{
    /// <summary>
    /// Adds the showcase's HUD to the showcase's own scenes as Set Up Scene sets them up (registered with
    /// CharacterSceneSetup.AfterSettingUp, as the UI's editor tools add the UI, so nothing that ships names the HUD): for a
    /// scene in Assets/Showcase, an instance of the Showcase HUD prefab in the setup, with the spawner's On Character Spawned
    /// connected to its ShowControlsHint and its On Npc Spawned to its AddNpcLabel. Other scenes (a project's own) get no
    /// HUD.
    /// </summary>
    [InitializeOnLoad]
    public static class ShowcaseHUDSetup
    {
        /// <summary>Where the showcase's scenes are: only they get the HUD.</summary>
        public const string ShowcaseFolder = "Assets/Showcase/";

        static ShowcaseHUDSetup() => CharacterSceneSetup.AfterSettingUp += AddHUD;

        /// <summary>Whether a scene (by its path) is the showcase's own.</summary>
        public static bool IsShowcaseScene(string path) => !string.IsNullOrEmpty(path) && path.StartsWith(ShowcaseFolder);

        /// <summary>Adds the HUD to a showcase scene Set Up Scene has just set up (see the class).</summary>
        public static void AddHUD(CharacterSceneSetup.SetupContext context)
        {
            if (!IsShowcaseScene(context.scene.path)) { return; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShowcaseHUDAssets.HUDPath);
            if (prefab == null || prefab.GetComponent<ShowcaseHUD>() == null)
            {
                context.notes.Add($"No showcase HUD was added: there's no '{ShowcaseHUDAssets.HUDPath}' (Tools > Meridian Motion > Showcase > Create Showcase HUD makes it).");
                return;
            }
            var hud = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, context.setup)).GetComponent<ShowcaseHUD>();
            CharacterSpawner spawner = context.spawner;
            if (spawner != null)
            {
                if (context.withUndo) { Undo.RecordObject(spawner, "Set Up Scene"); }
                Connect(spawner.OnCharacterSpawned, hud, hud.ShowControlsHint);
                if (!Connected(spawner.OnNpcSpawned, hud, nameof(ShowcaseHUD.AddNpcLabel)))
                {
                    UnityEventTools.AddPersistentListener(spawner.OnNpcSpawned, hud.AddNpcLabel);
                }
                EditorUtility.SetDirty(spawner);
            }
            context.notes.Add("Added the showcase's HUD (lap times, NPC name tags, a controls hint).");
            // Playtests are run in the showcase's levels: each records its sessions (the pause menu's Replays tab).
            if (MeridianMotion.UI.EditorTools.UISceneSetup.AddReplayRecorder(context.scene, context.setup, context.withUndo) != null)
            {
                context.notes.Add("The scene has a Replay Recorder (each play session recorded; the pause menu's Replays tab).");
            }
        }

        private static void Connect(UnityEvent<Transform> spawned, Object target, UnityAction<Transform> method)
        {
            if (!Connected(spawned, target, method.Method.Name)) { UnityEventTools.AddPersistentListener(spawned, method); }
        }

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
