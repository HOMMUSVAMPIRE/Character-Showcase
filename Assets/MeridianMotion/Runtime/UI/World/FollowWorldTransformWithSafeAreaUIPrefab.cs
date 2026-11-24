using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A world-anchored window that stays on the screen (UI_Framework_Blueprint, Window classes): followed past the
    /// screen's edge, it's kept inside the safe area (Screen.safeArea: clear of a notch or rounded corners), Safe Area
    /// Padding in from it, pointing the way to its target from the edge.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Follow World Transform With Safe Area UI Prefab")]
    public class FollowWorldTransformWithSafeAreaUIPrefab : FollowWorldTransformUIPrefab
    {
        [Tooltip("How far (canvas units) inside the safe area's edge it's kept")]
        [SerializeField] protected Vector2 safeAreaPadding = new(16f, 16f);

        protected override void UpdatePosition()
        {
            base.UpdatePosition();
            if (followTransform != null && OnScreen) { AssureOnScreen(); }
        }

        /// <summary>Moves it inside the safe area (less the padding), if it's outside.</summary>
        public void AssureOnScreen()
        {
            var canvas = transform.parent as RectTransform;
            var rect = (RectTransform)transform;
            if (canvas == null) { return; }
            Canvas root = canvas.GetComponentInParent<Canvas>();
            Camera camera = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
            Rect safe = Screen.safeArea;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, safe.min, camera, out Vector2 min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, safe.max, camera, out Vector2 max);
            Vector2 size = rect.rect.size, pivot = rect.pivot;
            Vector2 low = min + safeAreaPadding + Vector2.Scale(size, pivot);
            Vector2 high = max - safeAreaPadding - Vector2.Scale(size, Vector2.one - pivot);
            Vector3 at = rect.localPosition;
            rect.localPosition = new Vector3(Mathf.Clamp(at.x, low.x, Mathf.Max(low.x, high.x)), Mathf.Clamp(at.y, low.y, Mathf.Max(low.y, high.y)), at.z);
        }
    }
}
