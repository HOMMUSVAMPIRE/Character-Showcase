using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MeridianMotion.UI
{
    /// <summary>
    /// Rebuilds nested layout groups in the right order (UI_Framework_Blueprint, Widget kit): uGUI lays out a parent before
    /// the children it sizes by, a frame late, so a list's rows that size themselves show wrongly for a frame. The deepest
    /// groups are rebuilt first, then each parent; RebuildLayoutAsync does it again after a frame, once the text has been
    /// measured.
    /// </summary>
    public static class LayoutRebuildHelper
    {
        /// <summary>Rebuilds every layout group under root (root included), deepest first. Nothing for null.</summary>
        public static void RebuildLayout(RectTransform root)
        {
            if (root == null) { return; }
            var groups = new List<(int depth, RectTransform rect)>();
            foreach (LayoutGroup group in root.GetComponentsInChildren<LayoutGroup>())
            {
                int depth = 0;
                for (Transform t = group.transform; t != null && t != root; t = t.parent) { depth++; }
                groups.Add((depth, (RectTransform)group.transform));
            }
            groups.Sort((a, b) => b.depth.CompareTo(a.depth));
            foreach ((_, RectTransform rect) in groups) { LayoutRebuilder.ForceRebuildLayoutImmediate(rect); }
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }

        /// <summary>Rebuilds the layout now, and again after a frame (see the class).</summary>
        public static async Awaitable RebuildLayoutAsync(RectTransform root)
        {
            RebuildLayout(root);
            await Awaitable.NextFrameAsync();
            if (root != null) { RebuildLayout(root); }
        }
    }
}
