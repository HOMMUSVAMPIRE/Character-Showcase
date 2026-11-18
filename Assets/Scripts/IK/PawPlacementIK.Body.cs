using UnityEngine;

namespace Character
{
    // Paw placement: the body (the ground's line under it, its pitch and height, and the tail and neck against the pitch).
    public partial class PawPlacementIK
    {
        /// <summary>
        /// The ground under the body, as a line: fitted (least squares) through the ground probed along it; and the pitch
        /// its slope calls for, and the height the body's raised to it.
        /// </summary>
        private void FitGroundLine(ref Frame frame)
        {
            float scale = frame.scale, floor = frame.floor, probeTop = frame.probeTop, middleAlong = frame.middleAlong;
            Vector3 forward = frame.forward;
            // The ground under the body, as a line: fitted (least squares) through the ground probed along it, so on a
            // flight of stairs it runs along the flight, rather than jumping a step as each leg's top crosses a step's edge.
            float sumU = 0f, sumH = 0f, sumUU = 0f, sumUH = 0f;
            int found = 0;
            float reachAlong = bodyProbeReach * scale;
            for (int k = 0; k < BodyProbes; k++)
            {
                float u = Mathf.Lerp(-reachAlong, reachAlong, k / (BodyProbes - 1f));
                Vector3 at = transform.position + forward * (middleAlong + u);
                if (!FindGround(new Vector3(at.x, probeTop, at.z), probeTop - (floor - maxStepDown * scale), out RaycastHit hit)) { continue; }
                float h = Mathf.Clamp(hit.point.y - floor, -maxStepDown * scale, maxStepUp * scale);
                sumU += u; sumH += h; sumUU += u * u; sumUH += u * h;
                probeAlong[found] = u;
                probeHeightFound[found] = h;
                found++;
            }
            float slopeGoal = 0f, groundGoal = 0f;
            if (found >= 2)
            {
                float denominator = found * sumUU - sumU * sumU;
                slopeGoal = denominator > 1e-8f ? (found * sumUH - sumU * sumH) / denominator : 0f;
                groundGoal = (sumH - slopeGoal * sumU) / found; // The line's height under the body's middle.
                // Raised onto the highest ground under it: on stairs the fitted line runs between the steps' edges and
                // their treads, and the body, held over it, would sink its belly into the edges.
                float above = 0f;
                for (int k = 0; k < found; k++) { above = Mathf.Max(above, probeHeightFound[k] - (groundGoal + slopeGoal * probeAlong[k])); }
                groundGoal += above;
            }
            else if (found == 1) { groundGoal = sumH; }
            // The body pitched to the ground's slope and raised or lowered to its line. Pitched by θ about its middle, a
            // leg's top u ahead of it rises u·sin θ and comes back to u·cos θ ahead, where the line is u·cos θ·tan θ =
            // u·sin θ higher: so every leg's top stays as high above the line as animated.
            float pitchGoal = Mathf.Clamp(Mathf.Atan(slopeGoal) * Mathf.Rad2Deg, -maxBodyPitch, maxBodyPitch);
            // (Reworked below once the legs say how far each end must come down to reach.)
            float baseGoal = groundGoal;
            frame.found = found;
            frame.slopeGoal = slopeGoal;
            frame.groundGoal = groundGoal;
            frame.pitchGoal = pitchGoal;
            frame.baseGoal = baseGoal;
        }

        /// <summary>
        /// The body's pitch and height: lowered as far as its planted legs need to reach their paws' ground (each end as
        /// far as its own legs need), tilted along the line through the two, no lower than keeps the legs' tops clear of
        /// the ground; eased.
        /// </summary>
        private void PitchAndLevelBody(ref Frame frame)
        {
            float deltaTime = frame.deltaTime, scale = frame.scale, floor = frame.floor, middleAlong = frame.middleAlong;
            float slopeGoal = frame.slopeGoal, pitchGoal = frame.pitchGoal, baseGoal = frame.baseGoal;
            int found = frame.found;
            Vector3 forward = frame.forward, right = frame.right, pivot = frame.pivot;
            // Then lowered as far as a planted leg, pitched so, would have to reach further down to its paw's ground than
            // the animation reaches (a lifted paw lowers it as it comes down): the leg's top where pitching takes it,
            // and its paw where the animation has it, on its ground.
            // Each end lowered only as far as its own planted legs need: a hind paw on a low tread drops the hips, not the
            // shoulders (which would take the front legs into the steps above). The legs ahead of the middle and behind
            // it each give how much further down they'd have to reach than animated; the body is tilted and lowered
            // along the line through the two.
            Quaternion pitchTo = Quaternion.AngleAxis(-pitchGoal, right); // Turning about +right pitches the nose down.
            float plantedLift = plantedHeight * scale, liftedLift = Mathf.Max(liftedHeight * scale, plantedLift + 1e-3f);
            float reachAhead = 0f, reachBehind = 0f, alongAhead = 0f, alongBehind = 0f, topAhead = 0f, topBehind = 0f;
            int ahead = 0, behind = 0;
            for (int i = 0; i < paws.Length; i++)
            {
                Leg leg = legs[i];
                if (!IsSolvable(leg)) { continue; }
                ref PawState paw = ref paws[i];
                // Planted while low and not swinging forward (its speed is in the model's own units, as its scale is 1).
                paw.planted = Mathf.Min(1f - Mathf.InverseLerp(plantedLift, liftedLift, paw.lift),
                                        1f - Mathf.InverseLerp(0f, swingSpeed, paw.forwardSpeed));
                Vector3 top = leg.upper.position;
                float animatedReach = top.y - paw.animatedContact.y;
                float pitchedTop = pivot.y + (pitchTo * (top - pivot)).y + baseGoal;
                float wantedReach = pitchedTop - (paw.animatedContact.y + paw.offset);
                float legReach = Mathf.Min(0f, (animatedReach - wantedReach) * paw.planted);
                float along = Vector3.Dot(top - transform.position, forward) - middleAlong;
                float topAboveFloor = pitchedTop - floor;
                if (along >= 0f) { reachAhead = Mathf.Min(reachAhead, legReach); alongAhead += along; topAhead += topAboveFloor; ahead++; }
                else { reachBehind = Mathf.Min(reachBehind, legReach); alongBehind += along; topBehind += topAboveFloor; behind++; }
            }
            float maxDrop = maxBodyDrop * scale;
            reachAhead = Mathf.Max(reachAhead, -maxDrop);
            reachBehind = Mathf.Max(reachBehind, -maxDrop);
            float reach = Mathf.Min(reachAhead, reachBehind);
            if (ahead > 0 && behind > 0)
            {
                alongAhead /= ahead;
                alongBehind /= behind;
                // But never so low that the legs' tops come nearer the ground under them than Leg Top Clearance (raised
                // that far, if need be): where a flight of stairs meets the landing at its top, lowering the hips for the
                // hind paws on the last steps would take the thighs into the landing's edge.
                float clearance = legTopClearance * scale;
                if (GroundNear(alongAhead, found, scale, out float groundAhead))
                {
                    reachAhead = Mathf.Max(reachAhead, groundAhead + clearance - topAhead / ahead);
                }
                if (GroundNear(alongBehind, found, scale, out float groundBehind))
                {
                    reachBehind = Mathf.Max(reachBehind, groundBehind + clearance - topBehind / behind);
                }
                float tilt = (reachAhead - reachBehind) / Mathf.Max(alongAhead - alongBehind, 0.05f * scale);
                pitchGoal = Mathf.Clamp(Mathf.Atan(slopeGoal + tilt) * Mathf.Rad2Deg, -maxBodyPitch, maxBodyPitch);
                // Lowered under the middle as far as either end needs, with the tilt the pitch can take (Max Body Pitch
                // may not let it take all of it: on a steep slope, lowering the middle for a tilt it didn't get dropped
                // the front end onto its folded legs and left the hind legs stretched short of the ground).
                float applied = Mathf.Tan(pitchGoal * Mathf.Deg2Rad) - slopeGoal;
                reach = Mathf.Min(reachAhead - applied * alongAhead, reachBehind - applied * alongBehind);
            }
            float heightGoal = baseGoal + reach;
            bodyPitch = bodySmoothTime > 0f
                ? Mathf.SmoothDamp(bodyPitch, pitchGoal, ref bodyPitchVelocity, bodySmoothTime, Mathf.Infinity, deltaTime)
                : pitchGoal;
            // The line is found in the world each frame, so it rises smoothly up a flight of stairs, though the
            // animations' floor (the character's feet) jumps a step at a time: the body follows it as it is. Only how far
            // the legs need it lowered below it is eased, as paws plant and lift. (Easing the body's whole height would
            // trail a climb: up a steep flight it rises two metres a second, and eased over a tenth of one, it hung
            // a third of a metre below where it should be, its legs folded into the steps.)
            if (!hasBodyLevel || bodySmoothTime <= 0f)
            {
                bodyLevel = reach;
                bodyLevelVelocity = 0f;
                hasBodyLevel = true;
            }
            else
            {
                bodyLevel = Mathf.SmoothDamp(bodyLevel, reach, ref bodyLevelVelocity, bodySmoothTime, Mathf.Infinity, deltaTime);
            }
            heightGoal = baseGoal + bodyLevel;
            bodyHeight = Mathf.Clamp(heightGoal, -maxStepDown * scale, maxStepUp * scale);
        }

        /// <summary>
        /// The body pitched about its middle, then raised or lowered; the tail and neck turned back against the pitch.
        /// Returns the pitch, which the legs' bend directions turn with.
        /// </summary>
        private Quaternion PoseBody(ref Frame frame)
        {
            float scale = frame.scale;
            Vector3 up = frame.up, forward = frame.forward, right = frame.right, pivot = frame.pivot;
            float pitched = bodyPitch * activeWeight;
            Quaternion pitch = Quaternion.AngleAxis(-pitched, right);
            rootBone.SetPositionAndRotation(pivot + pitch * (rootBone.position - pivot) + up * (bodyHeight * activeWeight),
                pitch * rootBone.rotation);
            foreach (CounterPitchBone counter in counterPitchBones)
            {
                float share = pitched >= 0f ? counter?.noseUpShare ?? 0f : counter?.noseDownShare ?? 0f;
                if (counter?.bone == null || share <= 0f) { continue; }
                counter.bone.rotation = Quaternion.AngleAxis(pitched * share, right) * counter.bone.rotation;
            }
            foreach (CounterPitchBone counter in counterPitchBones)
            {
                if (counter?.bone == null || counter.groundClearance <= 0f) { continue; }
                KeepTipClear(counter.bone, counter.groundClearance * scale * activeWeight, right, up, forward);
            }
            return pitch;
        }

        /// <summary>
        /// The highest ground (m above the floor) probed under the body (this frame's body probes) within a probe's spacing
        /// of a point along it (m ahead of its middle); false if none was found there.
        /// </summary>
        private bool GroundNear(float along, int found, float scale, out float ground)
        {
            float window = 2f * bodyProbeReach * scale / (BodyProbes - 1);
            ground = float.NegativeInfinity;
            for (int k = 0; k < found; k++)
            {
                if (Mathf.Abs(probeAlong[k] - along) <= window) { ground = Mathf.Max(ground, probeHeightFound[k]); }
            }
            return ground > float.NegativeInfinity;
        }

        /// <summary>
        /// Turns the bone up (about right, at the bone) as far as keeps the end of its chain (past its first child's first
        /// child's ..., its last bone) clearance above the ground under it, keeping how far the end is from it.
        /// </summary>
        private void KeepTipClear(Transform bone, float clearance, Vector3 right, Vector3 up, Vector3 forward)
        {
            Transform last = bone;
            for (Transform next = FirstBone(last); next != null; next = FirstBone(last)) { last = next; }
            if (last == bone) { return; }
            // The chain's end: as far past its last bone as that is past the one before (a bone's origin is where it
            // starts, and the last one's mesh reaches on past it: the tail's tip).
            Vector3 end = last.position + (last.position - last.parent.position);
            Vector3 toTip = Vector3.ProjectOnPlane(end - bone.position, right);
            float reach = toTip.magnitude;
            if (reach < 1e-4f) { return; }
            bool hasGround = FindGround(end + up * reach, 2f * reach + clearance, out RaycastHit ground);
            if (!hasGround) { return; }
            float shortfall = ground.point.y + clearance - end.y;
            if (shortfall <= 0f) { return; }
            float height = Vector3.Dot(toTip, up);
            float raised = Mathf.Asin(Mathf.Clamp(Mathf.Min(height + shortfall, reach) / reach, -1f, 1f))
                           - Mathf.Asin(Mathf.Clamp(height / reach, -1f, 1f));
            // About right, a positive turn raises what's behind the bone; what's ahead of it, the other way.
            float sign = Vector3.Dot(toTip, forward) < 0f ? 1f : -1f;
            bone.rotation = Quaternion.AngleAxis(sign * raised * Mathf.Rad2Deg, right) * bone.rotation;
        }

        /// <summary>The bone's first child that's a bone (not its hitbox: see Hitbox), or null.</summary>
        private static Transform FirstBone(Transform bone)
        {
            for (int i = 0; i < bone.childCount; i++)
            {
                Transform child = bone.GetChild(i);
                if (!child.TryGetComponent(out Hitbox _)) { return child; }
            }
            return null;
        }
    }
}
