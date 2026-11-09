using UnityEngine;

namespace CameraSystem
{
    /// <summary>
    /// Observes a target transform from the outside and derives everything the camera needs to know about it
    /// (smoothed position, velocity, landings) without depending on the target's movement code.
    /// </summary>
    public class CameraTargetTracker
    {
        private Vector3 previousSample, currentSample;
        private float previousSampleTime, currentSampleTime;
        private bool hasSamples;

        // Per-frame fallback sampling for targets that are not moved during FixedUpdate.
        private Vector3 lastFramePosition;

        private float previousVerticalSpeed;

        // If the target's Rigidbody already interpolates, its transform is smooth every frame and must not be
        // interpolated again (that would interpolate already-interpolated samples, adding lag and wobble).
        private Rigidbody body;

        /// <summary>True if the tracker is interpolating between physics steps itself this frame.</summary>
        public bool SelfInterpolating { get; private set; }

        /// <summary>Render-time position of the target (interpolated if enabled).</summary>
        public Vector3 Position { get; private set; }
        /// <summary>Estimated world-space velocity of the target.</summary>
        public Vector3 Velocity { get; private set; }
        public float HorizontalSpeed => new Vector2(Velocity.x, Velocity.z).magnitude;
        /// <summary>True for the frame in which the target teleported. Smoothing should snap.</summary>
        public bool TeleportedThisFrame { get; private set; }
        /// <summary>Downward speed at the moment of landing, or 0 if the target didn't land this frame.</summary>
        public float LandingSpeedThisFrame { get; private set; }
        /// <summary>
        /// Rough guess at whether the target is on the ground: not moving vertically by more than walking up or
        /// down a moderate slope would explain.
        /// </summary>
        public bool ProbablyGrounded => Mathf.Abs(Velocity.y) < 0.75f + HorizontalSpeed * 0.5f;

        public void Reset(Transform target)
        {
            Vector3 p = target != null ? target.position : Vector3.zero;
            previousSample = currentSample = lastFramePosition = Position = p;
            previousSampleTime = currentSampleTime = Time.fixedTime;
            Velocity = Vector3.zero;
            previousVerticalSpeed = 0f;
            hasSamples = target != null;
            body = target != null ? target.GetComponentInParent<Rigidbody>() : null;
            TeleportedThisFrame = true;
            LandingSpeedThisFrame = 0f;
        }

        /// <summary>Call once per frame from LateUpdate.</summary>
        public void Update(Transform target, CameraFollowSettings settings, LandingSettings landing, float deltaTime)
        {
            TeleportedThisFrame = false;
            LandingSpeedThisFrame = 0f;
            if (target == null) { return; }
            if (!hasSamples) { Reset(target); }

            Vector3 now = target.position;

            bool rigidbodyInterpolates = body != null && body.interpolation != RigidbodyInterpolation.None;
            SelfInterpolating = settings.interpolateTarget && !rigidbodyInterpolates;

            if (SelfInterpolating)
            {
                // A new physics step has run since our newest sample: shift the samples along.
                if (!Mathf.Approximately(Time.fixedTime, currentSampleTime))
                {
                    previousSample = currentSample;
                    previousSampleTime = currentSampleTime;
                    currentSample = now;
                    currentSampleTime = Time.fixedTime;

                    float stepTime = Mathf.Max(currentSampleTime - previousSampleTime, 1e-4f);
                    Velocity = (currentSample - previousSample) / stepTime;
                }
                else if ((now - currentSample).sqrMagnitude > 1e-10f)
                {
                    // Moved outside of physics (e.g. teleported from Update): trust the new position.
                    currentSample = now;
                }

                if ((currentSample - previousSample).sqrMagnitude > settings.teleportDistance * settings.teleportDistance)
                {
                    previousSample = currentSample;
                    Velocity = Vector3.zero;
                    TeleportedThisFrame = true;
                }

                // Render time is between the last two physics steps (like Rigidbody interpolation).
                float alpha = Mathf.Clamp01((Time.time - currentSampleTime) / Mathf.Max(Time.fixedDeltaTime, 1e-4f));
                Position = Vector3.Lerp(previousSample, currentSample, alpha);
            }
            else
            {
                Vector3 delta = now - lastFramePosition;
                if (delta.sqrMagnitude > settings.teleportDistance * settings.teleportDistance)
                {
                    TeleportedThisFrame = true;
                    Velocity = Vector3.zero;
                }
                else if (deltaTime > 1e-5f)
                {
                    Velocity = delta / deltaTime;
                }
                Position = now;
            }
            lastFramePosition = now;

            // Landing: was falling fast, and vertical speed has suddenly mostly stopped.
            float vy = Velocity.y;
            if (!TeleportedThisFrame && previousVerticalSpeed < -landing.minImpactSpeed && vy > previousVerticalSpeed * 0.35f)
            {
                LandingSpeedThisFrame = -previousVerticalSpeed;
            }
            previousVerticalSpeed = vy;
        }
    }
}
