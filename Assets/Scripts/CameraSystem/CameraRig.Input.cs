using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CameraSystem
{
    // The camera rig: what input drives (looking around, zooming, aiming), and recentering behind the target.
    public partial class CameraRig
    {
        private float MaxDistance => activeZone != null && activeZone.limitDistance
            ? Mathf.Clamp(activeZone.maxDistance, zoom.minDistance, zoom.maxDistance)
            : zoom.maxDistance;

        private void HandleZoomInput(CameraInputFrame frame, float dt)
        {
            if (zoom.enabled && Mathf.Abs(frame.zoomDelta) > 1e-5f)
            {
                bool canSwitch = zoom.zoomIntoFirstPerson && !ModeLockedByZone;
                if (activeMode == CameraMode.ThirdPerson)
                {
                    bool atMinimum = targetDistance <= zoom.minDistance + 1e-3f;
                    if (frame.zoomDelta < 0f && atMinimum && canSwitch && modes.allowFirstPerson)
                    {
                        SetMode(CameraMode.FirstPerson);
                    }
                    else
                    {
                        targetDistance = Mathf.Clamp(targetDistance + frame.zoomDelta, zoom.minDistance, MaxDistance);
                    }
                }
                else if (frame.zoomDelta > 0f && canSwitch && modes.allowThirdPerson)
                {
                    targetDistance = zoom.minDistance;
                    SetMode(CameraMode.ThirdPerson);
                }
            }

            float goal = Mathf.Min(targetDistance, MaxDistance);
            distance = zoom.smoothTime > 0f
                ? Mathf.SmoothDamp(distance, goal, ref distanceVelocity, zoom.smoothTime, Mathf.Infinity, dt)
                : goal;
        }

        private void HandleAimInput(CameraInputFrame frame, float dt)
        {
            bool aiming = false;
            if (aim.enabled)
            {
                if (aim.inputMode == AimInputMode.Toggle)
                {
                    if (frame.aimPressed) { aimToggled = !aimToggled; }
                    aiming = aimToggled;
                }
                else
                {
                    aiming = frame.aimHeld;
                }
            }
            else
            {
                aimToggled = false;
            }

            float goal = aiming ? 1f : 0f;
            aimProgress = aim.blendTime > 0f ? Mathf.MoveTowards(aimProgress, goal, dt / aim.blendTime) : goal;
        }

        private float AimBlend => Mathf.SmoothStep(0f, 1f, aimProgress);

        private float ClampPitch(float value)
        {
            float t = ThirdPersonBlend;
            float min = Mathf.Lerp(firstPerson.minPitch, thirdPerson.minPitch, t);
            float max = Mathf.Lerp(firstPerson.maxPitch, thirdPerson.maxPitch, t);
            return Mathf.Clamp(value, min, max);
        }

        private void UpdateLook(CameraInputFrame frame, float tpBlend, float dt)
        {
            Vector2 degrees = frame.lookDegrees * Mathf.Lerp(1f, aim.sensitivityMultiplier, AimBlend);

            if (look.smoothing > 0f && dt > 0f)
            {
                // Smooth the rotation *rate* so the result is frame-rate independent.
                Vector2 rate = degrees / dt;
                smoothedLookRate = Vector2.Lerp(smoothedLookRate, rate, 1f - Mathf.Exp(-dt / look.smoothing));
                degrees = smoothedLookRate * dt;
            }
            else
            {
                smoothedLookRate = Vector2.zero;
            }

            yaw = Mathf.Repeat(yaw + degrees.x, 360f);
            pitch = ClampPitch(pitch + degrees.y);
            timeSinceLookInput = frame.hasLookInput ? 0f : timeSinceLookInput + dt;
        }

        private void UpdateRecenter(float dt)
        {
            bool active = recenter.enabled
                && activeMode == CameraMode.ThirdPerson
                && timeSinceLookInput >= recenter.delay
                && aimProgress <= 0f
                && tracker.HorizontalSpeed >= recenter.minTargetSpeed;

            float headingYaw = yaw;
            if (active)
            {
                if (recenter.heading == RecenterHeading.MovementDirection)
                {
                    Vector3 v = tracker.Velocity;
                    if (new Vector2(v.x, v.z).sqrMagnitude < 0.01f) { active = false; }
                    else { headingYaw = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg; }
                }
                else
                {
                    Vector3 f = Vector3.ProjectOnPlane(target.forward, Vector3.up);
                    if (f.sqrMagnitude < 1e-4f) { active = false; }
                    else { headingYaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg; }
                }

                // Don't spin the camera around when the target walks back toward it.
                if (Mathf.Abs(Mathf.DeltaAngle(yaw, headingYaw)) > recenter.maxTurnAngle) { active = false; }
            }

            if (!active)
            {
                recenterYawVelocity = recenterPitchVelocity = 0f;
                return;
            }

            yaw = Mathf.Repeat(Mathf.SmoothDampAngle(yaw, headingYaw, ref recenterYawVelocity, recenter.smoothTime, Mathf.Infinity, dt), 360f);
            if (recenter.recenterPitch)
            {
                pitch = ClampPitch(Mathf.SmoothDamp(pitch, recenter.restingPitch, ref recenterPitchVelocity, recenter.smoothTime, Mathf.Infinity, dt));
            }
        }
    }
}
