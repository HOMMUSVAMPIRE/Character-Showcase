using TMPro;
using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>A categorized data list's header row: the category's name (DataDisplayList_Categorized).</summary>
    [AddComponentMenu("Meridian Motion/UI/Category Display")]
    public class CategoryDisplay : MonoBehaviour
    {
        [Tooltip("Where the category's name is shown")]
        [SerializeField] private TextMeshProUGUI label;

        /// <summary>The category shown.</summary>
        public string Category { get; private set; }

        /// <summary>Shows the category.</summary>
        public void Initialize(string category)
        {
            Category = category;
            if (label != null) { label.text = category; }
        }
    }
}
