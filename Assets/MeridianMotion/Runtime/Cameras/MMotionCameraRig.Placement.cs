using System;
using System.Collections.Generic;
using MeridianMotion.PhysicsQueries;
using Unity.Profiling;
using UnityEngine.InputSystem;
using UnityEngine;

namespace MeridianMotion.Cameras
{
    // The camera rig: placing the third-person camera behind its shoulder, and keeping it out of walls.
    public partial class MMotionCameraRig
    {
        private Vector3 ComputeThirdPersonPosition(Vector3 pivot, Quaternion lookRotation, Quaternion yawRotation, float dt)
        {
            // The sweeps start from the pivot, unless the probe there would be inside something: then from just clear of it.
            probeRadius = collision.probeRadius * SizeScale;
            if (collision.enabled) { pivot = ProbeStart(pivot, ref probeRadius); }
            float aimBlend = AimBlend;
            Vector3 size = TargetSize;
            float shoulderX = thirdPerson.shoulderOffset.x * size.x * Mathf.Lerp(1f, aim.shoulderMultiplier, aimBlend);
            Vector3 right = yawRotation * Vector3.right;
            Vector3 lift = Vector3.up * (thirdPerson.shoulderOffset.y * size.y);

            // Pick the side, auto-swapping temporarily if the preferred shoulder is pressed against a wall.
            float preferredSide = rightShoulder ? 1f : -1f;
            float side = preferredSide;
            if (thirdPerson.autoSwapWhenBlocked && collision.enabled && shoulderX > 1e-3f)
            {
                float preferredFree = SweepFraction(pivot, pivot + right * (shoulderX * preferredSide) + lift);
                float otherFree = SweepFraction(pivot, pivot - right * (shoulderX * preferredSide) + lift);
                if (autoSwapped)
                {
                    // Hysteresis: only return once the preferred side is clearly free again (or no worse than the other).
                    if (preferredFree > thirdPerson.autoSwapReturnFree || preferredFree >= otherFree) { autoSwapped = false; }
                }
                else
                {
                    // Swap as soon as the preferred side is noticeably blocked and the other is clearly freer. A shoulder
                    // only partly blocked is no good either: it leaves the camera's backward sweep grazing the wall, which
                    // pulls the camera in to its minimum distance. (Swapping only once the side was half blocked left a
                    // target moving along a wall, a wall run say, framed from inches behind its head, against the wall.)
                    autoSwapped = preferredFree < thirdPerson.autoSwapBlockedFree && otherFree > preferredFree + thirdPerson.autoSwapFreerBy;
                }
                if (autoSwapped) { side = -preferredSide; }
            }
            else
            {
                autoSwapped = false;
            }

            shoulderSide = thirdPerson.shoulderSwapTime > 0f
                ? Mathf.SmoothDamp(shoulderSide, side, ref shoulderVelocity, thirdPerson.shoulderSwapTime, Mathf.Infinity, dt)
                : side;

            Vector3 desiredShoulder = pivot + right * (shoulderX * shoulderSide) + lift;
            Vector3 shoulder = pivot + (desiredShoulder - pivot) * SweepFraction(pivot, desiredShoulder);
            debugShoulder = shoulder;

            // Pull the camera in front of anything between the shoulder point and the desired camera position.
            // Zoom works in the target's size, so a bigger target is framed the same way from further away.
            float desiredDistance = distance * size.y * Mathf.Lerp(1f, aim.distanceMultiplier, aimBlend);
            Vector3 back = lookRotation * Vector3.back;
            float allowed = desiredDistance;
            if (collision.enabled && Sweep(shoulder, back, desiredDistance, out float hitDistance))
            {
                allowed = Mathf.Max(collision.minDistance * size.y, hitDistance);
            }

            if (allowed < collisionDistance || collision.returnSmoothTime <= 0f)
            {
                collisionDistance = allowed; // Pull in instantly so geometry never clips into view.
                collisionVelocity = 0f;
            }
            else
            {
                collisionDistance = Mathf.SmoothDamp(collisionDistance, allowed, ref collisionVelocity, collision.returnSmoothTime, Mathf.Infinity, dt);
            }

            return shoulder + back * collisionDistance;
        }

        private static float Square(float value) => value * value;

        /// <summary>Fraction (0-1) of the way from 'from' to 'to' the collision probe can travel.</summary>
        private float SweepFraction(Vector3 from, Vector3 to)
        {
            if (!collision.enabled) { return 1f; }
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 1e-5f) { return 1f; }
            return Sweep(from, delta / length, length, out float hit) ? Mathf.Clamp01(hit / length) : 1f;
        }

        // Reused for every collision sweep, so sweeping doesn't allocate a new array each time.
        private readonly RaycastHitBuffer sweepHits = new(32, "MMotionCameraRig.sweepHits");
        private readonly ColliderBuffer probeOverlaps = new(32, "MMotionCameraRig.probeOverlaps");
        // The collision probe's radius this frame: the Probe Radius at the target's size, or less where there's no room for
        // it (see ProbeStart).
        private float probeRadius;

        // Passes made to push the probe's start out of what it overlaps, for a corner (two walls, or a wall and a ceiling).
        private const int ProbeClearingPasses = 3;
        // Halvings of the probe, at most, where even pushed out it doesn't fit (between walls closer than it's wide).
        private const int ProbeShrinkSteps = 4;

        /// <summary>
        /// Where the collision probe's sweeps start, and how big it is: the pivot, unless the probe there would be inside
        /// something (the target is closer to a wall or ceiling than the probe is wide: a slim target against a wall, or in
        /// a tight space), in which case it's pushed out of it, as little as it needs to be. A sweep starting inside
        /// something reads as fully blocked, so the camera was snapped in to its nearest and its shoulder swapped back and
        /// forth as the target moved along a wall. Where even pushed out it doesn't fit, the probe is made smaller.
        /// What the target is made of doesn't count.
        /// </summary>
        private Vector3 ProbeStart(Vector3 pivot, ref float radius)
        {
            Vector3 origin = pivot;
            // A little further than it has to, so the sweeps from it don't start touching what it was pushed out of.
            float margin = 0.1f * radius;
            for (int pass = 0; pass < ProbeClearingPasses; pass++)
            {
                int count = probeOverlaps.OverlapSphere(origin, radius, collision.layers, collision.triggerInteraction);
                bool pushed = false;
                for (int i = 0; i < count; i++)
                {
                    Collider other = probeOverlaps[i];
                    // Only convex colliders have a nearest point to be pushed away from; anything else, the probe shrinks for.
                    if (IsTargets(other.transform) || !HasClosestPoint(other)) { continue; }
                    Vector3 away = origin - other.ClosestPoint(origin);
                    float distance = away.magnitude;
                    // Inside it (the pivot in a wall): nowhere to push to.
                    if (distance < 1e-5f || distance >= radius) { continue; }
                    origin += away / distance * (radius + margin - distance);
                    pushed = true;
                }
                if (!pushed) { break; }
            }
            for (int step = 0; step < ProbeShrinkSteps && ProbeOverlaps(origin, radius); step++) { radius *= 0.5f; }
            return origin;
        }

        /// <summary>Whether a probe this big at origin overlaps anything that isn't the target.</summary>
        private bool ProbeOverlaps(Vector3 origin, float radius)
        {
            int count = probeOverlaps.OverlapSphere(origin, radius, collision.layers, collision.triggerInteraction);
            for (int i = 0; i < count; i++)
            {
                if (!IsTargets(probeOverlaps[i].transform)) { return true; }
            }
            return false;
        }

        private bool IsTargets(Transform t) => target != null && t.IsChildOf(target);

        private static bool HasClosestPoint(Collider c) =>
            c is BoxCollider || c is SphereCollider || c is CapsuleCollider || c is MeshCollider { convex: true };

        /// <summary>Sphere-casts the collision probe, ignoring the target's own colliders. Returns the safe travel distance.</summary>
        private bool Sweep(Vector3 origin, Vector3 direction, float maxDistance, out float safeDistance)
        {
            safeDistance = maxDistance;
            // Each sweep stops this far short of what it hits, so a point placed by one sweep (the shoulder point,
            // pulled in from a wall beside the target) isn't left touching the wall when the next sweep starts from it.
            // A probe starting in contact hits at distance 0, which reads as fully blocked: the camera's backward sweep
            // from a shoulder against a wall running alongside snapped the camera in to just behind the target's head
            // (as a wall run began, say), however clear the view behind was.
            float margin = 0.1f * probeRadius;
            int count = sweepHits.SphereCast(origin, probeRadius, direction, maxDistance, collision.layers, collision.triggerInteraction);
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = sweepHits[i];
                if (IsTargets(hit.transform)) { continue; }
                // A distance of 0 means the probe started inside geometry: treat as fully blocked.
                float d = hit.distance > 0f ? Mathf.Max(0f, hit.distance - margin) : 0f;
                if (d < safeDistance) { safeDistance = d; found = true; }
            }
            return found;
        }
    }
}
