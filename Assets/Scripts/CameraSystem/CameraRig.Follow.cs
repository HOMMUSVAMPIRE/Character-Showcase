using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CameraSystem
{
    // The camera rig: following the target, and what's layered on top (the landing dip, head bob, the lens, keeping the
    // target visible).
    public partial class CameraRig
    {
        private void UpdateFollow(float tpBlend, float dt)
        {
            Vector3 goal = tracker.Position;
            float horizontalDamping = Mathf.Lerp(0f, thirdPerson.horizontalDamping, tpBlend);
            float verticalDamping = Mathf.Lerp(firstPerson.verticalDamping, thirdPerson.verticalDamping, tpBlend);

            Vector2 current = new Vector2(followPosition.x, followPosition.z);
            Vector2 goalH = new Vector2(goal.x, goal.z);
            current = horizontalDamping > 0f
                ? Vector2.SmoothDamp(current, goalH, ref followHorizontalVelocity, horizontalDamping, Mathf.Infinity, dt)
                : goalH;
            current = goalH + Vector2.ClampMagnitude(current - goalH, follow.maxHorizontalLag * SizeScale);

            float y = verticalDamping > 0f
                ? Mathf.SmoothDamp(followPosition.y, goal.y, ref followVerticalVelocity, verticalDamping, Mathf.Infinity, dt)
                : goal.y;
            float verticalLag = follow.maxVerticalLag * SizeScale;
            y = Mathf.Clamp(y, goal.y - verticalLag, goal.y + verticalLag);

            followPosition = new Vector3(current.x, y, current.y);
        }

        private void UpdateLanding(float dt)
        {
            if (!landing.enabled)
            {
                landingOffset = landingVelocity = 0f;
                return;
            }

            // Judged as for a target of normal size (speeds divided by its size), and the dip scaled back up to its size.
            float size = SizeScale;
            float impact = tracker.LandingSpeedThisFrame / size - landing.minImpactSpeed;
            if (tracker.LandingSpeedThisFrame > 0f && impact > 0f)
            {
                float dip = Mathf.Min(impact * landing.dipPerSpeed, landing.maxDip) * size;
                // For a roughly critically damped spring kicked with velocity v, the peak displacement is about
                // v / (omega * e), so choose v to make the peak match the requested dip.
                landingVelocity -= dip * Mathf.Sqrt(landing.springStiffness) * (float)Math.E;
                shaker.AddTrauma(impact * landing.traumaPerSpeed);
            }

            // Semi-implicit Euler with substeps so the spring stays stable at low frame rates.
            const float maxStep = 1f / 120f;
            float remaining = dt;
            while (remaining > 1e-6f)
            {
                float step = Mathf.Min(remaining, maxStep);
                float acceleration = -landing.springStiffness * landingOffset - landing.springDamping * landingVelocity;
                landingVelocity += acceleration * step;
                landingOffset += landingVelocity * step;
                remaining -= step;
            }
            landingOffset = Mathf.Max(landingOffset, -landing.maxDip * 1.5f * size);
        }

        private Vector3 UpdateHeadBob(float tpBlend, float dt)
        {
            float scale = Mathf.Lerp(1f, headBob.applyInThirdPerson ? headBob.thirdPersonScale : 0f, tpBlend);
            if (!headBob.enabled || scale <= 0f)
            {
                bobWeight = bobWeightVelocity = 0f;
                return Vector3.zero;
            }

            float speed = tracker.HorizontalSpeed;
            bool walking = tracker.ProbablyGrounded && RelativeSpeed > 0.1f;
            float goalWeight = walking ? Mathf.Clamp01(RelativeSpeed / headBob.fullAmplitudeSpeed) : 0f;
            bobWeight = headBob.fadeTime > 0f
                ? Mathf.SmoothDamp(bobWeight, goalWeight, ref bobWeightVelocity, headBob.fadeTime, Mathf.Infinity, dt)
                : goalWeight;

            // A bigger target takes longer strides, so it bobs fewer times per meter, and further.
            Vector3 size = TargetSize;
            if (walking) { bobPhase += speed * headBob.cyclesPerMeter / size.y * dt * Mathf.PI * 2f; }
            bobPhase = Mathf.Repeat(bobPhase, Mathf.PI * 2f);

            // One side-to-side sway per cycle, and one up/down bounce per footstep (two per cycle).
            Vector3 local = new Vector3(
                Mathf.Sin(bobPhase) * headBob.amplitude.x,
                Mathf.Cos(bobPhase * 2f) * headBob.amplitude.y,
                0f);
            return Quaternion.Euler(0f, yaw, 0f) * Vector3.Scale(local, size) * (bobWeight * scale);
        }

        private void UpdateLens(float tpBlend, float dt)
        {
            float fov = Mathf.Lerp(firstPerson.fieldOfView, thirdPerson.fieldOfView, tpBlend);
            float aimBlend = AimBlend;
            fov = Mathf.Lerp(fov, aim.fieldOfView, aimBlend);

            float speedGoal = speedFov.enabled
                ? Mathf.InverseLerp(speedFov.minSpeed, speedFov.maxSpeed, RelativeSpeed) * speedFov.extraFov
                : 0f;
            speedFovAmount = speedFov.smoothTime > 0f
                ? Mathf.SmoothDamp(speedFovAmount, speedGoal, ref speedFovVelocity, speedFov.smoothTime, Mathf.Infinity, dt)
                : speedGoal;
            fov += speedFovAmount * (1f - aimBlend);

            zoneFov = Mathf.SmoothDamp(zoneFov, activeZone != null ? activeZone.fovOffset : 0f, ref zoneFovVelocity, 0.3f, Mathf.Infinity, dt);
            fov += zoneFov;

            cam.fieldOfView = Mathf.Clamp(fov, 1f, 179f);
            // Clip planes follow the target's size: near, so a small target's close-up camera doesn't cut into it, and
            // far for a bigger one, so it sees as far for its size. (Not nearer for a small one: the world stays in view.)
            cam.nearClipPlane = Mathf.Lerp(firstPerson.nearClipPlane, thirdPerson.nearClipPlane, tpBlend) * SizeScale;
            cam.farClipPlane = baseFarClipPlane * Mathf.Max(1f, SizeScale);
        }

        private void UpdateTargetVisibility(float tpBlend, Vector3 cameraPosition, Vector3 pivot)
        {
            TargetHideMode mode = TargetHideMode.None;
            if (tpBlend < 0.1f)
            {
                mode = firstPerson.hideTarget;
            }
            else if (thirdPerson.hideWhenClose != TargetHideMode.None
                     && (cameraPosition - pivot).sqrMagnitude < Square(thirdPerson.hideDistance * SizeScale))
            {
                mode = thirdPerson.hideWhenClose;
            }
            visibility.RefreshIfChanged();
            visibility.Apply(mode);
        }
    }
}
