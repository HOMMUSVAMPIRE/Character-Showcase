using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A row of ability tiles (ReferenceAssets/OpenTasks.md, H6): SetAbilities gives it the abilities (an ID and an icon
    /// name each), a tile each from its Tile Prefab, in order (tiles are reused, never destroyed); SetState sets one's
    /// state. It knows nothing of the abilities: the game drives it.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/HUD/Ability Bar")]
    public class AbilityBar : MonoBehaviour
    {
        [Tooltip("A tile (an AbilityTile on its root)")]
        [SerializeField] private AbilityTile tilePrefab;
        [Tooltip("Where the tiles go (a layout group); its own transform if empty")]
        [SerializeField] private Transform tilesParent;

        private readonly List<AbilityTile> tiles = new();

        /// <summary>A tile (an AbilityTile on its root).</summary>
        public AbilityTile TilePrefab { get => tilePrefab; set => tilePrefab = value; }

        /// <summary>The tiles shown, in order.</summary>
        public IEnumerable<AbilityTile> Tiles
        {
            get { foreach (AbilityTile tile in tiles) { if (tile != null && tile.gameObject.activeSelf) { yield return tile; } } }
        }

        /// <summary>Shows a tile for each ability (its ID and icon name), in order, each Ready; the rest hidden.</summary>
        public void SetAbilities(IReadOnlyList<(string id, string iconName)> abilities)
        {
            Transform parent = tilesParent != null ? tilesParent : transform;
            while (tiles.Count < abilities.Count && tilePrefab != null) { tiles.Add(Instantiate(tilePrefab, parent, false)); }
            for (int i = 0; i < tiles.Count; i++)
            {
                bool used = i < abilities.Count;
                UIUtility.SetActive(tiles[i], used);
                if (!used) { continue; }
                tiles[i].transform.SetSiblingIndex(i);
                tiles[i].ID = abilities[i].id;
                tiles[i].IconName = abilities[i].iconName;
                tiles[i].SetState(AbilityTile.State.Ready);
            }
        }

        /// <summary>The tile of an ability, or null.</summary>
        public AbilityTile Tile(string id)
        {
            foreach (AbilityTile tile in Tiles) { if (tile.ID == id) { return tile; } }
            return null;
        }

        /// <summary>Sets an ability's state (cooling down: the fraction of its cooldown left).</summary>
        public void SetState(string id, AbilityTile.State state, float cooldownLeft = 0f) => Tile(id)?.SetState(state, cooldownLeft);
    }
}
