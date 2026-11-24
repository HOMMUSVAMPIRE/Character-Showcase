using MeridianMotion.UI;
using TMPro;
using UnityEngine;

namespace MeridianMotion.Showcase.HUD
{
    /// <summary>
    /// An NPC's name tag: a world label (FollowWorldTransformUIPrefab) over the NPC, showing its name, closed when the NPC
    /// goes. Its prefab allows several, on the InWorld canvas, taking neither Back nor the pointer.
    /// </summary>
    [AddComponentMenu("Meridian Motion/Showcase/NPC Label")]
    public class NpcLabel : FollowWorldTransformUIPrefab
    {
        [Tooltip("Where the NPC's name is shown")]
        [SerializeField] private TextMeshProUGUI nameText;

        /// <summary>The name shown.</summary>
        public string Name { get; private set; }

        /// <summary>Opens a name tag (the prefab) over the NPC, showing its name. Null without a UIManager or a prefab.</summary>
        public static NpcLabel Open(GameObject prefab, Transform npc, string npcName)
        {
            if (UIManager.instance == null || prefab == null || npc == null) { return null; }
            NpcLabel label = UIManager.instance.OpenUI<NpcLabel>(prefab);
            if (label == null) { return null; }
            label.Name = npcName ?? "";
            if (label.nameText != null) { label.nameText.text = label.Name; }
            label.Initialize(npc, null);
            return label;
        }
    }
}
