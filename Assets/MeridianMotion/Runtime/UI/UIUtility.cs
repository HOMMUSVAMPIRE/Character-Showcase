using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// The small helpers the UI is built on (UI_Framework_Blueprint, Building it, step 1), each safe to call on a null or
    /// destroyed object: a window often outlives what it shows, or is shown something not there yet.
    /// </summary>
    public static class UIUtility
    {
        /// <summary>Shows or hides a component's GameObject; nothing for a null or destroyed one.</summary>
        public static void SetActive(Component component, bool active)
        {
            if (component != null) { SetActive(component.gameObject, active); }
        }

        /// <summary>Shows or hides a GameObject (only if it isn't already); nothing for a null or destroyed one.</summary>
        public static void SetActive(GameObject gameObject, bool active)
        {
            if (gameObject != null && gameObject.activeSelf != active) { gameObject.SetActive(active); }
        }

        /// <summary>How many a collection holds: 0 for a null one.</summary>
        public static int CountOrZero<T>(this ICollection<T> collection) => collection?.Count ?? 0;

        /// <summary>
        /// Where a world position is in a canvas's rect, in its local space (for anchoring a label to something in the
        /// world): projected through the camera that sees the world, then into the rect, through the canvas's own camera
        /// unless it's an overlay. A point behind the camera comes out mirrored, as Unity projects it: check
        /// IsInFrontOf first where that matters. Zero without a camera or a rect.
        /// </summary>
        public static Vector2 WorldToCanvas(Camera camera, Vector3 worldPosition, RectTransform canvasRect)
        {
            if (camera == null || canvasRect == null) { return Vector2.zero; }
            Vector3 screen = camera.WorldToScreenPoint(worldPosition);
            Canvas canvas = canvasRect.GetComponentInParent<Canvas>();
            Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, canvasCamera, out Vector2 local);
            return local;
        }

        /// <summary>Whether a world position is in front of the camera (where WorldToCanvas means something).</summary>
        public static bool IsInFrontOf(Camera camera, Vector3 worldPosition) =>
            camera != null && camera.WorldToScreenPoint(worldPosition).z > 0f;
    }
}
