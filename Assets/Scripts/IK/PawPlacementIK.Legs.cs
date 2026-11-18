using UnityEngine;

namespace Character
{
    // Paw placement: the legs (sampling the animated paws, footholds, each paw's ground and lock, and bending the legs).
    public partial class PawPlacementIK
    {
        /// <summary>
        /// Each paw as the animation has it: where it touches the ground, how high it's lifted, and how fast it swings
        /// forward; and the legs' tops (how far ahead of the model's origin they are on average, and how high), about whose
        /// middle the body is pitched. False if no leg can be placed.
        /// </summary>
        private bool SampleAnimatedPaws(ref Frame frame)
        {
            float deltaTime = frame.deltaTime, scale = frame.scale, floor = frame.floor;
            Vector3 up = frame.up, forward = frame.forward;
            // Each paw as the animation has it (its ground is found once the body's pitch is known: see below).
            float probeTop = floor + probeHeight * scale;
            float middleAlong = 0f, topHeight = 0f;
            int count = 0;
            for (int i = 0; i < paws.Length; i++)
            {
                Leg leg = legs[i];
                if (!IsSolvable(leg)) { continue; }
                ref PawState paw = ref paws[i];
                paw.animatedContact = leg.foot.TransformPoint(leg.contact);
                paw.animatedFootRotation = leg.foot.rotation;
                // How high the animation lifts the paw: above the floor, and above the lowest it's had it lately, which a
                // paw's stance may not quite be at the floor (a hind paw rolls a little onto its toes as it pushes off).
                float aboveFloor = paw.animatedContact.y - floor;
                paw.lowestLift = paw.hasLastModelAhead
                    ? Mathf.Min(aboveFloor, paw.lowestLift + liftBaselineRecovery * scale * deltaTime)
                    : aboveFloor;
                paw.lift = Mathf.Min(aboveFloor, aboveFloor - Mathf.Max(paw.lowestLift, 0f));
                float modelAhead = transform.InverseTransformPoint(paw.animatedContact).z;
                paw.forwardSpeed = paw.hasLastModelAhead && deltaTime > 0f ? (modelAhead - paw.lastModelAhead) / deltaTime : 0f;
                paw.lastModelAhead = modelAhead;
                paw.hasLastModelAhead = true;
                middleAlong += Vector3.Dot(leg.upper.position - transform.position, forward);
                topHeight += leg.upper.position.y;
                count++;
            }
            if (count == 0) { return false; }
            middleAlong /= count;
            // The body is pitched about its own middle: the point midway between its legs' tops, at their height. (About a
            // point on the floor, its back rides forward or back along the slope as it pitches: half a metre down a
            // flight of stairs, into the steps.)
            Vector3 pivot = transform.position + forward * middleAlong;
            pivot.y = topHeight / count;
            Vector3 right = Vector3.Cross(up, forward);
            frame.probeTop = probeTop;
            frame.middleAlong = middleAlong;
            frame.count = count;
            frame.pivot = pivot;
            frame.right = right;
            return true;
        }

        /// <summary>
        /// Each paw moved with its leg's top as the body pitches, and shortened in stride on a slope; its foothold chosen,
        /// and its ground found there.
        /// </summary>
        private void PlacePawsOnTheirGround(ref Frame frame)
        {
            CharacterAnimationState state = frame.state;
            float deltaTime = frame.deltaTime, scale = frame.scale, floor = frame.floor, probeTop = frame.probeTop;
            float middleAlong = frame.middleAlong, slopeGoal = frame.slopeGoal, groundGoal = frame.groundGoal, pitchGoal = frame.pitchGoal;
            Vector3 up = frame.up, forward = frame.forward, right = frame.right, pivot = frame.pivot;
            // Each paw moved with its leg's top as the body pitches (its shoulder or hip, swung about the body's middle,
            // comes back or forward along the slope), so the legs keep the stance the animation gives them, upright under
            // the body; and its ground found there, probed from inside the character's body. (Left where the flat-ground
            // animation puts them, the front paws stood up a slope ahead of the shoulders, on higher ground, their legs
            // folded, and the hind paws behind the hips, on lower, their legs stretched short of it.)
            Quaternion slopePitch = Quaternion.AngleAxis(-pitchGoal, right); // Turning about +right pitches the nose down.
            // Shorter, quicker strides on a slope (see Stride Shortening): each paw drawn in toward its leg's top along the
            // way the body faces, and the Animator played as much faster (from the next frame), while moving.
            float moving = Mathf.Clamp01(state.horizontalSpeed / (strideMovingSpeed * scale));
            float stride = Mathf.Lerp(1f, Mathf.Max(shortestStride, 1f - strideShortening * Mathf.Abs(Mathf.Sin(bodyPitch * Mathf.Deg2Rad))), moving * activeWeight);
            if (animator != null) { animator.speed = 1f / stride; }
            for (int i = 0; i < paws.Length; i++)
            {
                Leg leg = legs[i];
                if (!IsSolvable(leg)) { continue; }
                ref PawState paw = ref paws[i];
                Vector3 top = leg.upper.position;
                paw.shift = Vector3.ProjectOnPlane(pivot + slopePitch * (top - pivot) - top, up)
                            + forward * ((stride - 1f) * Vector3.Dot(paw.animatedContact - top, forward));
                // The ground under the paw's toes, and under the leg's middle joint (knee or elbow) where that leads the
                // toes: a leg leaning forward over an edge (a hind leg, its paw on the last step of a flight as the body
                // goes on onto the landing) puts its paw up on the edge's top, rather than its shin through the edge.
                // Likewise the paw's own joint (wrist or hock) only where it leads the toes: a paw curls back as it swings,
                // its joint ahead, and up a stair the joint would cross into the next step while the toes are over the one
                // below. Behind the toes (a planted paw's wrist, going down a flight, over the step it's come from), it
                // would lift the paw onto that step, folding the leg up under the body.
                Vector3 animated = paw.animatedContact;
                ChooseFoothold(ref paw, animated + paw.shift, leg.foot.position - animated, leg.lower.position - animated, forward,
                    floor + groundGoal, slopeGoal, middleAlong, probeTop, floor, scale, deltaTime);
                Vector3 contact = animated + paw.shift + forward * paw.foothold;
                // A joint behind the toes only needs to clear the ground under it: the paw goes no lower than that
                // ground less the joint's height above the paw (going down a flight, a hind paw put down by the riser
                // behind it is raised only so far as keeps its hock out of the step above, not up onto that step, which
                // folded the leg up under the hips).
                Vector3 moved = paw.shift + forward * paw.foothold;
                Vector3 pawJoint = leg.foot.position + moved, middleJoint = leg.lower.position + moved;
                float pawJointClear = Vector3.Dot(leg.foot.position - animated, forward) > 0f ? 0f : Mathf.Max(leg.foot.position.y - animated.y, 0f);
                float middleJointClear = Vector3.Dot(leg.lower.position - animated, forward) > 0f ? 0f : Mathf.Max(leg.lower.position.y - animated.y, 0f);
                // Where a swinging paw is going: as far ahead of its toes as it's lifted (not planted, as last placed).
                Vector3 lookAhead = forward * (swingLookAhead * scale * (1f - paw.planted));
                FindPawGround(ref paw, contact, pawJoint, pawJointClear, middleJoint, middleJointClear, lookAhead, probeTop, floor, scale, deltaTime);
            }
        }

        /// <summary>
        /// Each leg bent to put its paw on its ground, lifted as animated, turned as animated (tilted to the ground when
        /// planted), the body pitched by pitch.
        /// </summary>
        private void SolveLegs(ref Frame frame, Quaternion pitch)
        {
            float deltaTime = frame.deltaTime, scale = frame.scale;
            Vector3 up = frame.up, forward = frame.forward;
            for (int i = 0; i < paws.Length; i++)
            {
                Leg leg = legs[i];
                if (!IsSolvable(leg)) { continue; }
                ref PawState paw = ref paws[i];
                Quaternion tilt = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(up, paw.normal), paw.planted * alignToGround * activeWeight);
                Quaternion footRotation = tilt * paw.animatedFootRotation;
                // Locked (or not) fully placed, then faded in over the animation's paw, so a paw put down while placement
                // fades in (landing) is locked where placement puts it, not where it is part of the way there.
                Vector3 contact = Vector3.Lerp(paw.animatedContact, LockPaw(ref paw, paw.animatedContact + paw.shift + forward * paw.foothold + up * paw.offset, scale, deltaTime), activeWeight);
                Vector3 footTarget = contact - footRotation * Vector3.Scale(leg.contact, leg.foot.lossyScale);
                // The bend direction turned with the body's pitch, as the leg's joints are.
                Vector3 bend = leg.bendDirection == Vector3.zero ? Vector3.zero : pitch * transform.TransformDirection(leg.bendDirection);
                SolveTwoBone(leg.upper, leg.lower, leg.foot, footTarget, bend);
                leg.foot.rotation = footRotation;
            }
        }

        /// <summary>
        /// Finds the ground under a paw and eases toward it, and the ground's normal. Eased in the world, not from the
        /// animations' floor: that rises and falls with the character (walking up a ramp, say), and a paw planted on still
        /// ground would trail it by as much as it moved in the easing's time, standing above the ramp.
        /// </summary>
        private void FindPawGround(ref PawState paw, Vector3 contact, Vector3 joint, float jointClear, Vector3 middleJoint,
            float middleJointClear, Vector3 lookAhead, float top, float floor, float scale, float deltaTime)
        {
            float stepUp = maxStepUp * scale, stepDown = maxStepDown * scale;
            // Under the paw's contact (its toes), and under its joint and the leg's middle joint, less how high above the
            // paw each may stay (see Solve): the highest.
            paw.rayOrigin = new Vector3(contact.x, top, contact.z);
            paw.hit = FindGround(paw.rayOrigin, paw.rayOrigin.y - (floor - stepDown), out RaycastHit hit);
            float groundGoal = paw.hit ? hit.point.y : floor;
            for (int j = 0; j < 2; j++)
            {
                Vector3 at = j == 0 ? joint : middleJoint;
                float clear = j == 0 ? jointClear : middleJointClear;
                var origin = new Vector3(at.x, top, at.z);
                if (FindGround(origin, origin.y - (floor - stepDown), out RaycastHit jointHit) && (!paw.hit || jointHit.point.y - clear > groundGoal))
                {
                    groundGoal = jointHit.point.y - clear;
                    if (!paw.hit) { hit = jointHit; }
                    paw.hit = true;
                }
            }
            // Swinging, the ground just ahead of its toes too, if it's a step up: higher than the ground under the toes,
            // carried on along its slope, reaches there (the next step's edge, which it's coming up onto; not a ramp's rise).
            if (lookAhead.sqrMagnitude > 1e-10f)
            {
                var ahead = new Vector3(contact.x + lookAhead.x, top, contact.z + lookAhead.z);
                float alongSlope = paw.hit && hit.normal.y > 0.1f ? -(hit.normal.x * lookAhead.x + hit.normal.z * lookAhead.z) / hit.normal.y : 0f;
                float stepAbove = Mathf.Max(groundGoal, (paw.hit ? hit.point.y : groundGoal) + alongSlope) + SwingStepThreshold * scale;
                if (FindGround(ahead, ahead.y - (floor - stepDown), out RaycastHit aheadHit) && aheadHit.point.y > stepAbove)
                {
                    groundGoal = aheadHit.point.y;
                    if (!paw.hit) { hit = aheadHit; paw.hit = true; }
                }
            }
            Vector3 normalGoal = Vector3.up;
            paw.groundTransform = null;
            if (paw.hit)
            {
                paw.hitPoint = hit.point;
                paw.groundTransform = hit.transform;
                normalGoal = hit.normal;
            }
            // Carried with what it stands on (a moving platform), so the easing smooths only the ground changing under the
            // paw, not the platform's travel, which it would trail.
            if (paw.hasGround && paw.groundTransform != null && paw.groundTransform == paw.lastGroundTransform)
            {
                paw.ground += paw.groundTransform.position.y - paw.lastGroundHeight;
            }
            paw.lastGroundTransform = paw.groundTransform;
            paw.lastGroundHeight = paw.groundTransform != null ? paw.groundTransform.position.y : 0f;
            if (!paw.hasGround || pawSmoothTime <= 0f)
            {
                paw.ground = groundGoal;
                paw.groundVelocity = 0f;
                paw.hasGround = true;
            }
            else
            {
                paw.ground = Mathf.SmoothDamp(paw.ground, groundGoal, ref paw.groundVelocity, pawSmoothTime, Mathf.Infinity, deltaTime);
            }
            // Eased, but never into the ground: a paw swinging up a slope, or onto a stair, would trail below it by as much
            // as the ground rises in the easing's time. Its animated lift keeps it that much above wherever it's placed.
            float lowest = groundGoal - Mathf.Max(paw.lift, 0f);
            if (paw.ground < lowest)
            {
                paw.ground = lowest;
                paw.groundVelocity = Mathf.Max(paw.groundVelocity, 0f);
            }
            paw.offset = Mathf.Clamp(paw.ground - floor, -stepDown, stepUp);
            paw.normal = pawSmoothTime > 0f ? Vector3.Slerp(paw.normal, normalGoal, 1f - Mathf.Exp(-deltaTime / pawSmoothTime)) : normalGoal;
        }

        // How much better (m nearer the body's line) a foothold must be than where the animation has the paw, for each
        // metre it's moved, for the paw to be moved there: on even ground, where every foothold is as good, it stays put.
        private const float FootholdMovePenalty = 0.25f;
        // How far (m, for the model at its authored size) ahead of its contact a paw's toes reach as it rolls onto them to
        // push off: a foothold with the ground higher there is against a riser.
        private const float ToeClearance = 0.1f;

        /// <summary>
        /// Chooses where a paw is put down (paw.foothold, m ahead of where the animation has it, eased): there, Foothold
        /// Reach ahead or behind, whichever has its ground nearest the body's line (the line at lineAtMiddle under the
        /// body's middle, rising slope per metre ahead), so the leg stays nearest the length the animation gives it.
        /// </summary>
        private void ChooseFoothold(ref PawState paw, Vector3 at, Vector3 toJoint, Vector3 toMiddleJoint, Vector3 forward,
            float lineAtMiddle, float slope, float middleAlong, float top, float floor, float scale, float deltaTime)
        {
            // A paw put down stays where it was (its lock holds it), so it isn't given a foothold, nor its grounds probed
            // for one; a swinging one eases to its next foothold.
            if (paw.planted >= 0.5f && paw.hasGround) { return; }
            float reach = footholdReach * scale;
            float goal = 0f;
            if (reach > 0f)
            {
                float best = float.PositiveInfinity, depth = top - (floor - maxStepDown * scale);
                for (int k = -1; k <= 1; k++)
                {
                    float offset = k * reach;
                    Vector3 point = at + forward * offset;
                    if (!FindGround(new Vector3(point.x, top, point.z), depth, out RaycastHit hit)) { continue; }
                    float along = Vector3.Dot(point - transform.position, forward) - middleAlong;
                    float cost = Mathf.Abs(hit.point.y - (lineAtMiddle + slope * along)) + FootholdMovePenalty * Mathf.Abs(offset);
                    // A foothold no better than the best already, before the joints' checks (which only add to it), can't
                    // be chosen: their probes are spared.
                    if (cost >= best) { continue; }
                    // Not where the leg's joints would be in the ground there (going down a flight, a hind paw put down by
                    // the riser behind it has its hock in the step above).
                    // Nor with its toes against a riser ahead (up a flight, a paw put down at the back of a tread pokes its
                    // toes into the step above).
                    if (InGround(point, toJoint, hit.point.y, top, depth) || InGround(point, toMiddleJoint, hit.point.y, top, depth)
                        || InGround(point, forward * (ToeClearance * scale), hit.point.y, top, depth))
                    {
                        cost += 1f;
                    }
                    if (cost < best) { best = cost; goal = offset; }
                }
            }
            paw.foothold = footholdSmoothTime > 0f
                ? Mathf.SmoothDamp(paw.foothold, goal, ref paw.footholdVelocity, footholdSmoothTime, Mathf.Infinity, deltaTime)
                : goal;
        }

        /// <summary>
        /// Whether a joint, its animated offset from the paw (toJoint) above a paw put down at point, on ground at
        /// groundHeight, would be in the ground there.
        /// </summary>
        private bool InGround(Vector3 point, Vector3 toJoint, float groundHeight, float top, float depth)
        {
            Vector3 joint = point + toJoint;
            joint.y = groundHeight + Mathf.Max(toJoint.y, 0f);
            return FindGround(new Vector3(joint.x, top, joint.z), depth, out RaycastHit hit) && hit.point.y > joint.y;
        }

        /// <summary>
        /// Holds a planted paw where it was put down (see PlantLock), within Max Lock Distance, blended in and out over where
        /// it's placed. It's locked on the ground at the paw's place across it, and its height follows the ground where it
        /// was put down (and the animation's lift above it), so on a slope it stays on the ground there.
        /// </summary>
        private Vector3 LockPaw(ref PawState paw, Vector3 placed, float scale, float deltaTime)
        {
            if (pawLock <= 0f) { return placed; }
            var groundPoint = new Vector3(placed.x, paw.ground, placed.z);
            paw.plantLock.Update(placed, groundPoint, paw.groundTransform, paw.planted, maxLockDistance * scale, lockBlendTime, deltaTime);
            if (paw.plantLock.weight <= 0f) { return placed; }
            // Held where it was put down, as high above the ground there as the animation has it above its ground now: on
            // a slope, the ground where the animation has the paw now isn't as high as where it was put down.
            Vector3 lockAt = paw.plantLock.Position;
            lockAt.y += placed.y - paw.ground;
            return Vector3.Lerp(placed, lockAt, paw.plantLock.weight * pawLock);
        }

        /// <summary>Whether a leg's paw is locked where it was put down now (see Paw Lock), and how far the lock is blended in.</summary>
        public float LockWeight(int leg) => leg >= 0 && leg < paws.Length && paws[leg].plantLock.locked ? paws[leg].plantLock.weight : 0f;

        /// <summary>
        /// Bends a two-bone chain so its end (the end bone's start) reaches the target, or as near as the chain reaches:
        /// the middle joint placed by the law of cosines, on the side `bend` points to across the chain (or, with no
        /// bend given, the side it's bent to already), then the lower bone turned to the target.
        /// </summary>
        private void SolveTwoBone(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 bend)
        {
            Vector3 a = upper.position, b = lower.position, c = end.position;
            float upperLength = Vector3.Distance(a, b), lowerLength = Vector3.Distance(b, c);
            Vector3 toTarget = target - a;
            if (upperLength < 1e-5f || lowerLength < 1e-5f || toTarget.sqrMagnitude < 1e-10f) { return; }
            Vector3 along = toTarget.normalized;
            float reach = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(upperLength - lowerLength) + StraightLegMargin,
                upperLength + lowerLength - StraightLegMargin);

            // The side the middle joint goes, square to the chain: the bend direction, else where it is now, else across
            // the model's back.
            Vector3 side = Vector3.ProjectOnPlane(bend != Vector3.zero ? bend : b - a, along);
            if (side.sqrMagnitude < 1e-8f) { side = Vector3.ProjectOnPlane(b - a, along); }
            if (side.sqrMagnitude < 1e-8f) { side = Vector3.ProjectOnPlane(-transform.forward, along); }
            side.Normalize();

            float x = (upperLength * upperLength - lowerLength * lowerLength + reach * reach) / (2f * reach);
            float y = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - x * x));
            Vector3 middle = a + along * x + side * y;
            upper.rotation = Quaternion.FromToRotation(b - a, middle - a) * upper.rotation;
            Vector3 from = end.position - lower.position, to = a + along * reach - lower.position;
            if (from.sqrMagnitude > 1e-10f && to.sqrMagnitude > 1e-10f)
            {
                lower.rotation = Quaternion.FromToRotation(from, to) * lower.rotation;
            }
        }
    }
}
