using UnityEngine;

namespace MeridianMotion.Showcase.Grass
{
    /// <summary>
    /// Gives characters a GrassBender as they spawn, so they push the grass aside with their legs: its AddBender and AddNpcBender are
    /// what a spawner's On Character Spawned and On Npc Spawned call (Set Up Scene connects them in the showcase's
    /// levels with grass, GrassSceneSetup). It knows nothing of characters: only the Transform it's given.
    /// </summary>
    [AddComponentMenu("Meridian Motion/Showcase/Grass Benders")]
    public sealed class GrassBenders : MonoBehaviour
    {
        [Tooltip("For a character without leg capsules (its hitboxes): how far from its feet (m) it pushes the grass aside.")]
        [Min(0.05f)] public float radius = 0.35f;
        [Tooltip("The layer of the characters' hitboxes, whose lowest capsules (their lower legs and feet) push the grass.")]
        public string hitboxLayer = "CharacterHitbox";

        /// <summary>Gives a character (its root, at its feet) a grass bender, if it hasn't one.</summary>
        public void AddBender(Transform character)
        {
            if (character == null || character.GetComponent<GrassBender>() != null) { return; }
            GrassBender bender = character.gameObject.AddComponent<GrassBender>();
            bender.radius = radius;
            bender.hitboxLayer = hitboxLayer;
        }

        /// <summary>As AddBender, for an NPC spawned with its name (On Npc Spawned).</summary>
        public void AddNpcBender(Transform npc, string npcName) => AddBender(npc);
    }
}
