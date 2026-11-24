using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// A window anchored to something in the world (UI_Framework_Blueprint, Window classes): a name tag, a hover label. Each
    /// LateUpdate (after the camera has moved, so it doesn't trail it by a frame) it puts itself where its target's
    /// position (plus World Position Offset) shows on its canvas, plus UI Position Offset; hidden while the target's
    /// behind the camera; and closed once its target is destroyed (Destroy On Follow Target Null). A window of its own
    /// derives it and calls Initialize from its static Open. On the InWorld canvas by default, behind the windows.
    /// </summary>
    [AddComponentMenu("Meridian Motion/UI/Follow World Transform UI Prefab")]
    public class FollowWorldTransformUIPrefab : RootUIPrefab
    {
        [Header("Following")]
        [Tooltip("What it follows (set by Initialize)")]
        [SerializeField] protected Transform followTransform;
        [Tooltip("Where on its target it's anchored, from the target's position (m, in the world): above a head, say")]
        [SerializeField] protected Vector3 worldPositionOffset = new(0f, 2f, 0f);
        [Tooltip("How far from that point it's put on the canvas (canvas units)")]
        [SerializeField] protected Vector2 uiPositionOffset;
        [Tooltip("Whether it closes once its target is destroyed")]
        [SerializeField] protected bool destroyOnFollowTargetNull = true;

        // Its content's visibility, kept apart from its object's (which the UIManager's hiding uses).
        private CanvasGroup group;
        private Camera viewCamera;
        private bool following;

        /// <summary>What it follows.</summary>
        public Transform FollowTransform => followTransform;
        /// <summary>Whether it's shown (its target's in front of the camera).</summary>
        public bool OnScreen { get; private set; }

        // A label's defaults, as it's added in the editor: on the InWorld canvas, not taking Back or the pointer.
        protected virtual void Reset()
        {
            ParentCanvas = UICanvas.InWorld;
            CloseOnCancelButton = false;
            NeedsCursor = false;
        }

        protected override void Awake()
        {
            base.Awake();
            group = GetComponent<CanvasGroup>();
            if (group == null) { group = gameObject.AddComponent<CanvasGroup>(); }
        }

        /// <summary>Starts following the target, as seen by the camera (the main camera, if null), and puts it there at once.</summary>
        protected void Initialize(Transform target, Camera camera)
        {
            followTransform = target;
            viewCamera = camera;
            following = true;
            UpdatePosition();
        }

        protected virtual void LateUpdate()
        {
            if (following && !IsClosing) { UpdatePosition(); }
        }

        /// <summary>Puts it where its target shows on its canvas (see the class); closes it if the target's gone.</summary>
        protected virtual void UpdatePosition()
        {
            if (followTransform == null)
            {
                if (destroyOnFollowTargetNull) { Close(); }
                return;
            }
            Camera camera = viewCamera != null ? viewCamera : Camera.main;
            var canvas = transform.parent as RectTransform;
            Vector3 world = followTransform.position + worldPositionOffset;
            OnScreen = camera != null && canvas != null && UIUtility.IsInFrontOf(camera, world);
            group.alpha = OnScreen ? 1f : 0f;
            group.blocksRaycasts = OnScreen;
            if (!OnScreen) { return; }
            transform.localPosition = UIUtility.WorldToCanvas(camera, world, canvas) + uiPositionOffset;
        }
    }
}
