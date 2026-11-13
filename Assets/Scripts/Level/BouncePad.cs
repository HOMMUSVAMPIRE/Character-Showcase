using UnityEngine;

namespace Level
{
    /// <summary>
    /// A bounce pad (see BasicMovementMode's Bounce Pads settings): a character standing on it (any collider on this
    /// object or its children) is launched along its up, rising as high as its launch height above the pad (higher, holding
    /// Jump). It keeps a share of the character's own horizontal speed, so running across one carries the run on.
    ///
    /// For show, it can squash something (its springy top, say) down and let it spring back on each launch.
    /// </summary>
    [DisallowMultipleComponent]
    public class BouncePad : MonoBehaviour
    {
        [Tooltip("How high (m) the pad launches a character: how far above the pad its feet rise (before the character's " +
                 "own multiplier, and holding Jump)")]
        [Min(0f)] [SerializeField] private float launchHeight = 6f;
        [Tooltip("The share of the character's own horizontal speed it keeps through the launch: 1 carries a run on over " +
                 "the pad; 0 sends it straight along the pad's up")]
        [Range(0f, 1f)] [SerializeField] private float keepMomentum = 1f;
        [Tooltip("Something squashed down on each launch and left to spring back, for show (the pad's springy top, say). Optional")]
        [SerializeField] private Transform spring;
        [Tooltip("How far the spring is squashed on a launch, as a share of its height")]
        [Range(0f, 1f)] [SerializeField] private float squash = 0.5f;
        [Tooltip("Seconds the spring takes to spring back after a launch")]
        [Min(0.01f)] [SerializeField] private float springBackTime = 0.35f;

        private Vector3 springScale = Vector3.one;
        private bool hasSpringScale;
        private float launchedAt = float.NegativeInfinity;

        /// <summary>How high (m) the pad launches a character above itself.</summary>
        public float LaunchHeight { get => launchHeight; set => launchHeight = Mathf.Max(0f, value); }
        /// <summary>The share of a character's horizontal speed it keeps through the launch.</summary>
        public float KeepMomentum { get => keepMomentum; set => keepMomentum = Mathf.Clamp01(value); }
        /// <summary>Something squashed on each launch, for show (null for nothing).</summary>
        public Transform Spring { get => spring; set { RestoreSpring(); spring = value; hasSpringScale = false; } }
        /// <summary>The way the pad launches characters: its up (unit length).</summary>
        public Vector3 LaunchDirection => transform.up;

        /// <summary>Raised when the pad launches a character, for effects and sound.</summary>
        public event System.Action Launched;

        /// <summary>The bounce pad a collider (or its object) belongs to (on it or a parent, and enabled), or null if none.</summary>
        public static BouncePad Of(Transform part)
        {
            if (part == null) { return null; }
            BouncePad pad = part.GetComponentInParent<BouncePad>();
            return pad != null && pad.isActiveAndEnabled ? pad : null;
        }

        /// <summary>Tells the pad it launched a character: it squashes its spring and raises Launched.</summary>
        public void NotifyLaunched()
        {
            launchedAt = Time.time;
            Launched?.Invoke();
        }

        private void Update()
        {
            if (spring == null) { return; }
            if (!hasSpringScale)
            {
                springScale = spring.localScale;
                hasSpringScale = true;
            }
            // Squashed at once, then springing back with a little overshoot, as a spring would.
            float t = (Time.time - launchedAt) / springBackTime;
            float amount = t >= 1f || t < 0f ? 0f : squash * Mathf.Cos(t * Mathf.PI * 1.5f) * (1f - t);
            spring.localScale = new Vector3(springScale.x, springScale.y * (1f - amount), springScale.z);
        }

        private void OnDisable() => RestoreSpring();

        private void RestoreSpring()
        {
            if (spring != null && hasSpringScale) { spring.localScale = springScale; }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f);
            Vector3 up = LaunchDirection;
            float speedUp = Mathf.Max(up.y, 0.2f);
            Gizmos.DrawLine(transform.position, transform.position + up * (launchHeight / speedUp));
        }
    }
}
