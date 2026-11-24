using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A data list of records with a category (IdentifiableElement), which puts a header row (CategoryHeaderPrefab, with a
    /// CategoryDisplay) before the rows each time the category changes, in the order the records come in (UI_Framework_Blueprint,
    /// Generic data lists). Its headers are pooled as its rows are.
    /// </summary>
    public abstract class DataDisplayList_Categorized<T, U> : DataDisplayList<T, U> where T : IdentifiableElement where U : DataDisplay<T>
    {
        [Header("Categories")]
        [Tooltip("The header row put before each category's rows (with a CategoryDisplay on its root)")]
        [SerializeField] protected GameObject CategoryHeaderPrefab;

        private readonly List<CategoryDisplay> headers = new();

        /// <summary>The headers shown, in order.</summary>
        public IEnumerable<CategoryDisplay> ShownHeaders
        {
            get
            {
                foreach (CategoryDisplay header in headers) { if (header != null && header.gameObject.activeSelf) { yield return header; } }
            }
        }

        protected override void OnRowsShown(IReadOnlyList<U> rows)
        {
            Transform parent = itemsParent != null ? itemsParent : transform;
            int used = 0, sibling = 0;
            string category = null;
            foreach (U row in rows)
            {
                if (used == 0 || row.Data.elementType != category)
                {
                    category = row.Data.elementType;
                    CategoryDisplay header = Header(used++, parent);
                    if (header != null)
                    {
                        header.Initialize(category);
                        header.transform.SetSiblingIndex(sibling++);
                    }
                }
                row.transform.SetSiblingIndex(sibling++);
            }
            for (int i = used; i < headers.Count; i++) { UIUtility.SetActive(headers[i], false); }
        }

        // The header at index in the pool, made if there's none yet, and shown.
        private CategoryDisplay Header(int index, Transform parent)
        {
            if (CategoryHeaderPrefab == null) { return null; }
            while (headers.Count <= index) { headers.Add(Instantiate(CategoryHeaderPrefab, parent, false).GetComponent<CategoryDisplay>()); }
            UIUtility.SetActive(headers[index], true);
            return headers[index];
        }
    }
}
