using TMPro;
using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A marker over a place in the world (ReferenceAssets/OpenTasks.md, H6): a diamond with the distance to it, put where
    /// the place shows on its canvas; when the place is off the screen (or behind the camera), held at the edge of the
    /// safe area (less its padding) in its direction, with its Arrow pointing the way. It follows a transform or a fixed
    /// position (Open), and measures from its Distance From (the camera, if none). A world label (safe area) it is.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/HUD/World Marker")]
    public class WorldMarker : FollowWorldTransformWithSafeAreaUIPrefab
    {
        [Header("Marker")]
        [Tooltip("Shows the distance to it")]
        [SerializeField] private TMP_Text distanceText;
        [Tooltip("Points the way to it while it's held at the screen's edge (its up is its forward)")]
        [SerializeField] private RectTransform arrow;
        [Tooltip("How the distance is written ({0}: metres)")]
        [SerializeField] private string distanceFormat = "{0:0} m";

        private Vector3 position;
        private bool fixedPosition;
        private Camera markerCamera;
        private CanvasGroup canvasGroup;

        /// <summary>Whether it's held at the screen's edge (its place is off the screen).</summary>
        public bool Clamped { get; private set; }
        /// <summary>The way its arrow points, in degrees (0: right, anticlockwise), while it's clamped.</summary>
        public float ArrowAngle { get; private set; }
        /// <summary>What it measures the distance from (null: the camera).</summary>
        public Transform DistanceFrom { get; set; }
        /// <summary>The distance shown, in metres.</summary>
        public float Distance { get; private set; }
        /// <summary>The place it marks.</summary>
        public Vector3 WorldPosition => fixedPosition || followTransform == null ? position : followTransform.position + worldPositionOffset;

        /// <summary>Opens a marker (the window catalog's) over a fixed place, as the camera (the main one, if null) sees it.</summary>
        public static WorldMarker Open(Vector3 place, Camera camera = null)
        {
            WorldMarker marker = UIManager.instance != null ? UIManager.instance.Open<WorldMarker>() : null;
            if (marker != null) { marker.Mark(place, camera); }
            return marker;
        }

        /// <summary>Opens a marker over a transform (plus its World Position Offset).</summary>
        public static WorldMarker Open(Transform target, Camera camera = null)
        {
            WorldMarker marker = UIManager.instance != null ? UIManager.instance.Open<WorldMarker>() : null;
            if (marker != null) { marker.Mark(target, camera); }
            return marker;
        }

        /// <summary>Marks a fixed place.</summary>
        public void Mark(Vector3 place, Camera camera = null)
        {
            position = place;
            fixedPosition = true;
            markerCamera = camera;
            destroyOnFollowTargetNull = false;
            Initialize(null, camera);
        }

        /// <summary>Marks a transform.</summary>
        public void Mark(Transform target, Camera camera = null)
        {
            fixedPosition = false;
            markerCamera = camera;
            Initialize(target, camera);
        }

        /// <summary>
        /// Where a marker goes in a rect (the safe area, in its canvas's space) for a point (its place on the canvas):
        /// the point, if it's inside and in front; else the rect's edge, on the line from the rect's centre to the point
        /// (away from it, if the place is behind the camera), with the angle of that line.
        /// </summary>
        public static Vector2 EdgePoint(Vector2 point, Rect area, bool behind, out bool clamped, out float angle)
        {
            Vector2 centre = area.center, way = point - centre;
            if (behind) { way = -way; }
            if (way.sqrMagnitude < 0.0001f) { way = Vector2.down; }
            angle = Mathf.Atan2(way.y, way.x) * Mathf.Rad2Deg;
            clamped = behind || !area.Contains(point);
            if (!clamped) { return point; }
            float scale = Mathf.Min(Mathf.Abs(way.x) > 0.0001f ? area.width / 2f / Mathf.Abs(way.x) : float.MaxValue,
                Mathf.Abs(way.y) > 0.0001f ? area.height / 2f / Mathf.Abs(way.y) : float.MaxValue);
            return centre + way * scale;
        }

        protected override void UpdatePosition()
        {
            if (!fixedPosition && followTransform == null)
            {
                if (destroyOnFollowTargetNull) { Close(); }
                return;
            }
            Camera camera = markerCamera != null ? markerCamera : Camera.main;
            var canvas = transform.parent as RectTransform;
            if (camera == null || canvas == null) { return; }
            if (canvasGroup == null) { canvasGroup = GetComponent<CanvasGroup>(); }
            if (canvasGroup != null) { canvasGroup.alpha = 1f; }

            Vector3 world = WorldPosition;
            Vector3 viewport = camera.WorldToViewportPoint(world);
            bool behind = viewport.z < 0f;
            Vector2 screen = new(viewport.x * camera.pixelWidth + camera.pixelRect.x, viewport.y * camera.pixelHeight + camera.pixelRect.y);
            Canvas root = canvas.GetComponentInParent<Canvas>();
            Camera canvasCamera = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, canvasCamera, out Vector2 point);
            Rect safe = SafeArea(canvas, canvasCamera);
            Vector2 at = EdgePoint(point + uiPositionOffset, safe, behind, out bool clamped, out float angle);
            Clamped = clamped;
            ArrowAngle = angle;
            transform.localPosition = new Vector3(at.x, at.y, 0f);
            if (arrow != null)
            {
                arrow.gameObject.SetActive(clamped);
                arrow.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            }
            Vector3 from = DistanceFrom != null ? DistanceFrom.position : camera.transform.position;
            Distance = Vector3.Distance(from, world);
            if (distanceText != null) { distanceText.text = string.Format(distanceFormat, Distance); }
        }

        // The screen's safe area in the canvas's space, less the padding.
        private Rect SafeArea(RectTransform canvas, Camera canvasCamera)
        {
            Rect safe = Screen.safeArea;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, safe.min, canvasCamera, out Vector2 min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, safe.max, canvasCamera, out Vector2 max);
            min += safeAreaPadding;
            max -= safeAreaPadding;
            return Rect.MinMaxRect(min.x, min.y, Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }
    }
}
