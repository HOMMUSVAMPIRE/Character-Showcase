using UnityEngine;

namespace Level
{
    /// <summary>
    /// A platform that travels back and forth along an offset and/or spins, moved as a kinematic, interpolated
    /// Rigidbody during the physics step. That is what characters standing on it expect: they read its physics pose
    /// each step to ride along, and interpolation keeps it visually smooth in step with them.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class MovingPlatform : MonoBehaviour
    {
        [Tooltip("How far the platform travels from its starting position: meters along the platform's own axes " +
                 "(its rotation applies, its scale doesn't). Zero = no travel.")]
        public Vector3 travel = new Vector3(0f, 0f, 4f);
        [Tooltip("Seconds for a full trip out and back.")]
        [Min(0.1f)] public float period = 6f;
        [Tooltip("Ease in and out at each end of the trip rather than moving at constant speed.")]
        public bool smooth = true;
        [Tooltip("Seconds to wait at each end of the trip.")]
        [Min(0f)] public float pauseAtEnds = 0.75f;
        [Tooltip("Degrees per second the platform spins about each of its local axes.")]
        public Vector3 spinDegreesPerSecond = Vector3.zero;
        [Tooltip("Offset into the cycle, from 0 to 1, so several platforms can move out of step.")]
        [Range(0f, 1f)] public float phaseOffset = 0f;

        private Rigidbody body;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private float elapsed;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            startPosition = body.position;
            startRotation = body.rotation;
        }

        private void FixedUpdate()
        {
            elapsed += Time.fixedDeltaTime;
            body.MovePosition(startPosition + startRotation * travel * TravelFraction(elapsed / period + phaseOffset));
            body.MoveRotation(startRotation * Quaternion.Euler(spinDegreesPerSecond * elapsed));
        }

        /// <summary>0 at the start of the trip, 1 at the far end, pausing at each end.</summary>
        private float TravelFraction(float cycles)
        {
            float t = Mathf.Repeat(cycles, 1f);
            float pause = Mathf.Clamp01(pauseAtEnds / period); // Fraction of the cycle spent paused (both ends).
            float moving = (1f - 2f * pause) / 2f;               // Fraction spent travelling each way.
            float x;
            if (t < pause) { x = 0f; }
            else if (t < pause + moving) { x = (t - pause) / moving; }
            else if (t < 2f * pause + moving) { x = 1f; }
            else { x = 1f - (t - 2f * pause - moving) / moving; }
            return smooth ? Mathf.SmoothStep(0f, 1f, x) : x;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 start = Application.isPlaying ? startPosition : transform.position;
            Quaternion rotation = Application.isPlaying ? startRotation : transform.rotation;
            Vector3 end = start + rotation * travel;
            Gizmos.color = new Color(1f, 0.8f, 0.2f);
            Gizmos.DrawLine(start, end);
            Gizmos.DrawWireCube(end, transform.lossyScale);
        }
    }
}
