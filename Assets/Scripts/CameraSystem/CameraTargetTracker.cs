using UnityEngine;

namespace CameraSystem
{
    /// <summary>
    /// Observes a target transform from the outside and derives everything the camera needs to know about it
    /// (smoothed position, velocity, landings) without depending on the target's movement code.
    /// </summary>
    public class CameraTargetTracker
    {
        // Samples of the target's *physics* pose, taken once per physics step. Velocity and landings are measured
        // from these, never from per-frame transform deltas: if the target only moves on physics steps, per-frame
        // deltas alternate between spikes and zero, which reads as constant hard landings and bogus speeds.
        private Vector3 previousSample, currentSample;
        private float previousSampleTime, currentSampleTime;
        private bool hasSamples;

        private float previousVerticalSpeed;

        // If the target has a Rigidbody, its position is the physics pose (the transform may be interpolated).
        // If that Rigidbody also interpolates, the transform is already smooth and must not be interpolated again.
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
        /// down a moderate slope would explain (allowing 0.75 m/s more for a target of normal size, scaled with its size).
        /// </summary>
        public bool ProbablyGrounded => Mathf.Abs(Velocity.y) < 0.75f * sizeScale + HorizontalSpeed * 0.5f;

        // The target's size relative to normal (see CameraRig.SizeScale), which the distances and speeds here scale with.
        private float sizeScale = 1f;

        public void Reset(Transform target)
        {
            body = target != null ? target.GetComponentInParent<Rigidbody>() : null;
            Vector3 p = target != null ? target.position : Vector3.zero;
            previousSample = currentSample = body != null ? body.position : p;
            Position = p;
            previousSampleTime = currentSampleTime = Time.fixedTime;
            Velocity = Vector3.zero;
            previousVerticalSpeed = 0f;
            hasSamples = target != null;
            TeleportedThisFrame = true;
            LandingSpeedThisFrame = 0f;
        }

        /// <summary>
        /// Call once per frame from LateUpdate. sizeScale is the target's size relative to normal: the teleport distance
        /// and the landing speed threshold scale with it, as the target's own movement does.
        /// </summary>
        public void Update(Transform target, CameraFollowSettings settings, LandingSettings landing, float sizeScale = 1f)
        {
            this.sizeScale = Mathf.Max(sizeScale, 1e-4f);
            TeleportedThisFrame = false;
            LandingSpeedThisFrame = 0f;
            if (target == null) { return; }
            if (!hasSamples) { Reset(target); }

            // A physics step has run since the newest sample: take a new one and update velocity and landings.
            if (!Mathf.Approximately(Time.fixedTime, currentSampleTime))
            {
                previousSample = currentSample;
                previousSampleTime = currentSampleTime;
                currentSample = body != null ? body.position : target.position;
                currentSampleTime = Time.fixedTime;

                float teleportDistance = settings.teleportDistance * this.sizeScale;
                if ((currentSample - previousSample).sqrMagnitude > teleportDistance * teleportDistance)
                {
                    previousSample = currentSample;
                    Velocity = Vector3.zero;
                    TeleportedThisFrame = true;
                }
                else
                {
                    float stepTime = Mathf.Max(currentSampleTime - previousSampleTime, 1e-4f);
                    Velocity = (currentSample - previousSample) / stepTime;

                    // Landing: was falling fast, and vertical speed has suddenly mostly stopped.
                    if (previousVerticalSpeed < -landing.minImpactSpeed * this.sizeScale && Velocity.y > previousVerticalSpeed * 0.35f)
                    {
                        LandingSpeedThisFrame = -previousVerticalSpeed;
                    }
                }
                previousVerticalSpeed = Velocity.y;
            }

            bool rigidbodyInterpolates = body != null && body.interpolation != RigidbodyInterpolation.None;
            SelfInterpolating = settings.interpolateTarget && !rigidbodyInterpolates;

            if (SelfInterpolating)
            {
                // Render time is between the last two physics steps (like Rigidbody interpolation).
                float alpha = Mathf.Clamp01((Time.time - currentSampleTime) / Mathf.Max(Time.fixedDeltaTime, 1e-4f));
                Position = Vector3.Lerp(previousSample, currentSample, alpha);
            }
            else
            {
                Position = target.position;
            }
        }
    }
}
