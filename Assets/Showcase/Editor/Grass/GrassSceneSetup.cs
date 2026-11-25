using System.Linq;
using MeridianMotion.Characters;
using MeridianMotion.EditorTools;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

namespace MeridianMotion.Showcase.Grass.EditorTools
{
    /// <summary>
    /// Makes the showcase's characters push its grass aside, as Set Up Scene sets a scene up (registered with
    /// CharacterSceneSetup.AfterSettingUp, as the showcase's HUD is, so nothing that ships names the grass): for a scene
    /// in Assets/Showcase with grass, a "Grass Benders" object in the setup, with the spawner's On Character Spawned
    /// connected to its AddBender and On Npc Spawned to its AddNpcBender. Scenes without grass get nothing.
    /// </summary>
    [InitializeOnLoad]
    public static class GrassSceneSetup
    {
        /// <summary>Where the showcase's scenes are: only they get the benders.</summary>
        public const string ShowcaseFolder = "Assets/Showcase/";

        static GrassSceneSetup() => CharacterSceneSetup.AfterSettingUp += AddBenders;

        /// <summary>Adds the benders to a showcase scene with grass that Set Up Scene has just set up (see the class).</summary>
        public static void AddBenders(CharacterSceneSetup.SetupContext context)
        {
            if (string.IsNullOrEmpty(context.scene.path) || !context.scene.path.StartsWith(ShowcaseFolder)) { return; }
            bool grass = context.scene.GetRootGameObjects().Any(o => o.GetComponentInChildren<GrassField>(true) != null);
            if (!grass || context.spawner == null) { return; }

            GrassBenders benders = context.setup.GetComponentInChildren<GrassBenders>(true);
            if (benders == null)
            {
                var obj = new GameObject("Grass Benders", typeof(GrassBenders));
                obj.transform.SetParent(context.setup, false);
                if (context.withUndo) { Undo.RegisterCreatedObjectUndo(obj, "Set Up Scene"); }
                benders = obj.GetComponent<GrassBenders>();
            }
            CharacterSpawner spawner = context.spawner;
            if (context.withUndo) { Undo.RecordObject(spawner, "Set Up Scene"); }
            if (!Connected(spawner.OnCharacterSpawned, benders, nameof(GrassBenders.AddBender)))
            {
                UnityEventTools.AddPersistentListener(spawner.OnCharacterSpawned, benders.AddBender);
            }
            if (!Connected(spawner.OnNpcSpawned, benders, nameof(GrassBenders.AddNpcBender)))
            {
                UnityEventTools.AddPersistentListener(spawner.OnNpcSpawned, benders.AddNpcBender);
            }
            EditorUtility.SetDirty(spawner);
            context.notes.Add("The characters push the grass aside (Grass Benders).");
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
