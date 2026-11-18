using UnityEngine;

namespace Level
{
    /// <summary>
    /// A platform that travels back and forth along an offset and/or spins, a kinematic Rigidbody. Where it is is a function
    /// of the level's clock (LevelClock), not of when it was enabled: one enabled late is where an early one is, and two
    /// machines on the same tick see it in the same place. Each physics step it
    /// moves first, before the characters (see FixedUpdate), so a character standing on it rides it exactly; each frame
    /// it's drawn between its last two physics poses, as the characters' interpolated Rigidbodies are (see Update), so
    /// it's smooth in step with them.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    // Before the characters' steps (ACCharacterController's FixedUpdate, at the default order): see FixedUpdate.
    [DefaultExecutionOrder(MoveOrder)]
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

        /// <summary>
        /// The script execution order platforms move at: before anything that steps a character in FixedUpdate (which
        /// must come after it, at a higher order).
        /// </summary>
        public const int MoveOrder = -100;

        private Rigidbody body;
        private Vector3 startPosition;
        private Quaternion startRotation;
        // Its last two physics poses, drawn between (see Update).
        private Vector3 previousPosition, currentPosition;
        private Quaternion previousRotation, currentRotation;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            // Interpolated here (see Update): Unity's interpolation doesn't smooth a Rigidbody whose pose is set, only one
            // moved by MovePosition, which would leave it a step late (see FixedUpdate).
            body.interpolation = RigidbodyInterpolation.None;
            startPosition = previousPosition = currentPosition = body.position;
            startRotation = previousRotation = currentRotation = body.rotation;
        }

        private void OnEnable()
        {
            // Where the clock has it now, rather than jumping there at its next step.
            if (body == null) { return; }
            PoseAt(LevelClock.Tick, out currentPosition, out currentRotation);
            previousPosition = currentPosition;
            previousRotation = currentRotation;
            body.position = currentPosition;
            body.rotation = currentRotation;
            transform.SetPositionAndRotation(currentPosition, currentRotation);
        }

        /// <summary>Where the platform is at a tick of the level's clock: its start pose, moved along its trip and spun for the time since tick 0.</summary>
        public void PoseAt(long tick, out Vector3 position, out Quaternion rotation)
        {
            float elapsed = (float)LevelClock.SecondsAt(tick);
            position = startPosition + startRotation * travel * TravelFraction(elapsed / period + phaseOffset);
            rotation = startRotation * Quaternion.Euler(spinDegreesPerSecond * elapsed);
        }

        private void OnDisable()
        {
            // Held where physics has it: a drawn pose left on the Transform would reach physics at its next step.
            if (body != null) { transform.SetPositionAndRotation(currentPosition, currentRotation); }
        }

        /// <summary>
        /// Moves the platform to where it is at the end of this physics step, at once, before the characters step. A
        /// character's step reads the platform (to ride it) and queries its collider (to stand on it) where it is when the
        /// character steps, and the character moves at the physics step. Moved by MovePosition, which only takes it there
        /// at the physics step too, the platform was still where it was a step before when the characters read it, so they
        /// rode it a step behind: on a platform rising at 1 m/s, a step's 2 cm low, and as high above it falling, flipping
        /// from one to the other as it turned round. Set straight away (Rigidbody.position), its collider is at its new
        /// pose for their queries. Its Transform is set to it too, over the pose it was drawn at, which would otherwise
        /// reach physics at the step (Unity syncs changed Transforms to their Rigidbodies before it simulates).
        /// </summary>
        private void FixedUpdate()
        {
            previousPosition = currentPosition;
            previousRotation = currentRotation;
            PoseAt(LevelClock.Tick, out currentPosition, out currentRotation);
            body.position = currentPosition;
            body.rotation = currentRotation;
            transform.SetPositionAndRotation(currentPosition, currentRotation);
        }

        /// <summary>
        /// Draws the platform between its last two physics poses, as far between them as the time since the last physics
        /// step is through a step: as Unity draws an interpolated Rigidbody (the characters on it), so the two move
        /// together. Early in the frame (MoveOrder), before anything reads where it's drawn (foot and paw placement).
        /// </summary>
        private void Update()
        {
            float t = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(previousPosition, currentPosition, t),
                Quaternion.Slerp(previousRotation, currentRotation, t));
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
