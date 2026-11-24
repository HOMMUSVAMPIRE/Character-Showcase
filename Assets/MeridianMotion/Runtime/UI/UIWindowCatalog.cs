using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A project's own window prefabs, which UIManager.Open&lt;T&gt; finds by the component on their root, so a project adds
    /// windows without a field on the manager for each (the blueprint's manager has one per window: kept for the
    /// framework's own, the dialogs and blockers, and here for the rest). The UIManager names one catalog.
    /// </summary>
    [CreateAssetMenu(fileName = "UI Window Catalog", menuName = "Meridian Motion/UI/Window Catalog")]
    public class UIWindowCatalog : ScriptableObject
    {
        [Tooltip("The window prefabs (each with a RootUIPrefab on its root, and its own window component)")]
        [SerializeField] private List<GameObject> windows = new();

        /// <summary>The window prefabs.</summary>
        public List<GameObject> Windows => windows;

        /// <summary>The first window prefab whose root has a T, or null.</summary>
        public GameObject PrefabFor<T>() where T : Component
        {
            foreach (GameObject window in windows)
            {
                if (window != null && window.GetComponent<T>() != null) { return window; }
            }
            return null;
        }
    }
}
