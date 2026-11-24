using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeridianMotion.UI.EditorTools
{
    // Remaking a prefab in place (ReferenceAssets/OpenTasks.md, H5): Rebuild UI Assets makes each prefab afresh and puts
    // what's made into the prefab that's there, so its GUID and its root's file ID stay and what names it (the UI Manager,
    // the window catalog, a scene) still does.
    public static partial class UIAssets
    {
        [MenuItem("Tools/Meridian Motion/Utilities/Rebuild UI Assets", priority = 62)]
        public static void RebuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Rebuild UI Assets",
                    "Remake every UI prefab (but the UI Manager) from UIAssets, replacing its contents: hand edits to them are lost. " +
                    "What names them keeps working.", "Rebuild", "Cancel")) { return; }
            Create(rebuild: true);
        }

        /// <summary>
        /// Replaces root's contents with made's (a prefab's root, opened for editing, and a fresh one): its children are
        /// made's, its components made's (those of a type it has kept, so their file IDs stay, and set to made's; the rest
        /// added or removed), its RectTransform's values made's. References to made (its object and its components) are
        /// pointed at root's. Root itself, and so its file ID, stays. made is left empty, for the caller to destroy.
        /// </summary>
        public static void ReplaceContents(GameObject root, GameObject made)
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--) { Object.DestroyImmediate(root.transform.GetChild(i).gameObject); }

            var map = new Dictionary<Object, Object> { [made] = root, [made.transform] = root.transform };
            var pairs = new List<(Component from, Component to)>();
            var kept = new HashSet<Component>();
            Component[] existing = root.GetComponents<Component>();
            foreach (Component from in made.GetComponents<Component>())
            {
                if (from is Transform) { continue; }
                foreach (Component candidate in existing)
                {
                    if (candidate != null && candidate.GetType() == from.GetType() && !kept.Contains(candidate))
                    {
                        kept.Add(candidate);
                        pairs.Add((from, candidate));
                        break;
                    }
                }
                if (pairs.Count == 0 || pairs[^1].from != from) { pairs.Add((from, null)); }
            }
            // Those not kept go first (a GameObject has one Graphic: an Image goes before a shape's added), each pass
            // removing what nothing still requires.
            var leaving = new List<Component>();
            foreach (Component component in existing) { if (component != null && !(component is Transform) && !kept.Contains(component)) { leaving.Add(component); } }
            for (int pass = 0; pass < 4 && leaving.Count > 0; pass++)
            {
                for (int i = leaving.Count - 1; i >= 0; i--)
                {
                    if (CanRemove(leaving[i], leaving)) { Object.DestroyImmediate(leaving[i], true); leaving.RemoveAt(i); }
                }
            }
            for (int i = 0; i < pairs.Count; i++)
            {
                (Component from, Component to) = pairs[i];
                if (to == null)
                {
                    to = root.GetComponent(from.GetType()) is Component added && !kept.Contains(added) && added.GetType() == from.GetType()
                        ? added // Added already, required by one added before it.
                        : root.AddComponent(from.GetType());
                    kept.Add(to);
                    pairs[i] = (from, to);
                }
                map[from] = to;
            }

            if (made.transform is RectTransform madeRect && root.transform is RectTransform rootRect)
            {
                rootRect.anchorMin = madeRect.anchorMin;
                rootRect.anchorMax = madeRect.anchorMax;
                rootRect.pivot = madeRect.pivot;
                rootRect.anchoredPosition = madeRect.anchoredPosition;
                rootRect.sizeDelta = madeRect.sizeDelta;
            }
            root.transform.localScale = made.transform.localScale;
            root.layer = made.layer;
            while (made.transform.childCount > 0) { made.transform.GetChild(0).SetParent(root.transform, false); }
            foreach ((Component from, Component to) in pairs) { EditorUtility.CopySerialized(from, to); }

            // Everything now under root that named made's root or its components names root's.
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) { continue; }
                var fields = new SerializedObject(component);
                SerializedProperty property = fields.GetIterator();
                bool changed = false;
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null) { continue; }
                    if (map.TryGetValue(property.objectReferenceValue, out Object to))
                    {
                        property.objectReferenceValue = to;
                        changed = true;
                    }
                }
                if (changed) { fields.ApplyModifiedPropertiesWithoutUndo(); }
            }
        }

        // Whether nothing else on its object (but what's leaving too) requires its type.
        private static bool CanRemove(Component component, List<Component> leaving)
        {
            foreach (Component other in component.GetComponents<Component>())
            {
                if (other == null || other == component || leaving.Contains(other)) { continue; }
                foreach (RequireComponent require in (RequireComponent[])Attribute.GetCustomAttributes(other.GetType(), typeof(RequireComponent), true))
                {
                    foreach (Type type in new[] { require.m_Type0, require.m_Type1, require.m_Type2 })
                    {
                        if (type != null && type.IsInstanceOfType(component)) { return false; }
                    }
                }
            }
            return true;
        }
    }
}
