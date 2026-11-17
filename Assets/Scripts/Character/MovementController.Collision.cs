using UnityEngine;

namespace Character
{
    // Collision: resolving the step's motion against the world with sweeps of the character's body (see CharacterBody):
    // sliding, stepping up, depenetrating.
    public partial class MovementController
    {
        /// <summary>
        /// Resolve the character's intended motion against the world: push out of anything that moved into the
        /// character, sweep its body, slide along walls and walkable slopes, step up onto low ledges (stairs), slide
        /// off surfaces too steep to stand on, and stick to descending slopes. Momentum that a collision blocks is
        /// removed, and data.collisions records which sides were blocked. A body that turns with the character (see
        /// CharacterBody.TurnsWithFacing) turns only as far as it has room to.
        /// </summary>
        public bool CheckMovementValid(ref MovementData data)
        {
            resolvingRotation = data.initialRotation;
            try { return ResolveStep(ref data); }
            finally { resolvingRotation = null; }
        }

        // Halvings of a blocked turn made to find how far of it there's room for (to within 1/32 of it).
        private const int TurnRoomSteps = 5;

        /// <summary>
        /// The way the character can turn to, from from toward to, with its body (one that turns with it) at pos: all the
        /// way where there's room, otherwise as far as there is. Turning sweeps nothing (a turn's sweep isn't worth it): the
        /// body is checked for room facing the new way, and the turn is halved until it fits. Already without room as it
        /// faces (pushed into something), it turns freely, so it isn't stuck facing that way.
        /// </summary>
        private Quaternion TurnWithRoom(Vector3 pos, Quaternion from, Quaternion to)
        {
            if (Quaternion.Angle(from, to) < 0.01f || Body.Fits(pos, to, Scale, posture) || !Body.Fits(pos, from, Scale, posture)) { return to; }
            float fits = 0f, blocked = 1f;
            for (int i = 0; i < TurnRoomSteps; i++)
            {
                float half = 0.5f * (fits + blocked);
                if (Body.Fits(pos, Quaternion.Slerp(from, to, half), Scale, posture)) { fits = half; }
                else { blocked = half; }
            }
            return Quaternion.Slerp(from, to, fits);
        }

        private bool ResolveStep(ref MovementData data)
        {
            data.collisions = CollisionFlags.None;
            Vector3 pos = data.initialPosition;
            Vector3 inertia = data.finalInertia;

            // Sweeps can't see anything they start inside of, so first push out of anything that moved into the
            // character since the last step (a moving wall, a platform's side, a lowering ceiling).
            Vector3 depenetration = Body.Depenetrate(pos, data.initialRotation);
            if (depenetration != Vector3.zero)
            {
                pos += depenetration;
                Vector3 pushDirection = depenetration.normalized;
                data.collisions |= pushDirection.y > 0.7f ? CollisionFlags.Below
                    : pushDirection.y < -0.7f ? CollisionFlags.Above : CollisionFlags.Sides;
                RemoveInertiaInto(ref inertia, pushDirection);
            }

            // Turning, a body that isn't round about the feet swings into somewhere else: only as far as there's room. It
            // moves facing the way it ends up.
            if (Body.TurnsWithFacing) { data.targetRotation = TurnWithRoom(pos, data.initialRotation, data.finalRotation); }
            resolvingRotation = data.finalRotation;

            Vector3 move = data.finalMovementVector;
            if (move.sqrMagnitude < 1e-12f && depenetration == Vector3.zero) { return true; }

            // Split into horizontal (slide/step-up) and vertical (gravity/jump) components so they resolve independently.
            Vector3 horiz = new Vector3(move.x, 0f, move.z);
            float vert = move.y;

            Vector3 horizResolved = ResolveHorizontal(pos, horiz, data.initialGrounded, ref inertia, ref data.collisions);
            pos += horizResolved;

            Vector3 vertResolved = ResolveVertical(pos, vert, ref inertia, ref data.collisions);
            pos += vertResolved;

            Vector3 totalResolved = depenetration + horizResolved + vertResolved;

            // Snap to ground while grounded so the character sits on the floor and follows descending slopes.
            // Allowed drop is the larger of:
            //  - GroundedTolerance: the gap CheckGrounded already counts as "on the ground". Grounded gravity is tiny
            //    (inertia is reset each grounded frame), so without closing this gap the character hovers above the
            //    floor for a while after landing.
            //  - The most a walkable slope could drop over the horizontal distance moved this frame.
            //  - While walking, the step height, so the character walks down stairs instead of floating off each step.
            // Anything further is a ledge, and gravity handles it (so walking off a ledge isn't a teleport).
            if (data.initialGrounded && vert <= 0f)
            {
                float horizDist = new Vector2(horizResolved.x, horizResolved.z).magnitude;
                float maxSlopeDrop = Mathf.Min(groundSnapDistance * HeightScale, horizDist * Mathf.Tan(maxSlopeAngle * Mathf.Deg2Rad));
                float maxDrop = Mathf.Max(GroundedTolerance, maxSlopeDrop) + SkinWidth;
                if (horizDist > 1e-4f * HeightScale) { maxDrop = Mathf.Max(maxDrop, StepHeight + SkinWidth); }
                if (Body.Sweep(pos, BodyRotation, Vector3.down, maxDrop + SkinWidth, out var snapHit)
                    && (Vector3.Angle(snapHit.normal, Vector3.up) <= maxSlopeAngle || IsStepEdge(snapHit, pos)))
                {
                    // Negative when the ground is closer than the skin width (rolling over a step's edge can close the
                    // gap): then this lifts the character back to it, as nothing else would. Depenetration ignores
                    // overlaps shallower than the skin, so without this the character stays sunk into the ground.
                    float drop = snapHit.distance - SkinWidth;
                    if (drop <= maxDrop)
                    {
                        totalResolved += Vector3.down * drop;
                        data.collisions |= CollisionFlags.Below;
                    }
                }
            }

            data.ForceMovementVector(totalResolved, inertia);
            return true;
        }

        /// <summary>Removes the part of the character's momentum heading into a surface with the given normal.</summary>
        private static void RemoveInertiaInto(ref Vector3 inertia, Vector3 surfaceNormal)
        {
            float into = Vector3.Dot(inertia, surfaceNormal);
            if (into < 0f) { inertia -= surfaceNormal * into; }
        }

        /// <summary>
        /// Resolve horizontal motion: sweep forward, slide along walls/walkable slopes, attempt step-up for low ledges.
        /// </summary>
        private Vector3 ResolveHorizontal(Vector3 feetPos, Vector3 horizMove, bool grounded, ref Vector3 inertia, ref CollisionFlags collisions)
        {
            float mag = horizMove.magnitude;
            if (mag < 1e-6f) { return Vector3.zero; }
            Vector3 dir = horizMove / mag;

            if (!Body.Sweep(feetPos, BodyRotation, dir, mag + SkinWidth, out var hit))
            {
                return horizMove;
            }

            float travel = Mathf.Max(0f, hit.distance - SkinWidth);
            Vector3 traveled = dir * travel;
            Vector3 remaining = horizMove - traveled;

            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);

            // Walkable slope (ramp): project remaining motion onto the slope and continue up/down it.
            if (slopeAngle <= maxSlopeAngle && hit.normal.y > 0.01f)
            {
                Vector3 slid = Vector3.ProjectOnPlane(remaining, hit.normal);
                return traveled + slid;
            }

            // Stair / low ledge: try to step up and over.
            if (grounded)
            {
                Vector3 stepped = TryStepUp(feetPos, dir, mag);
                if (stepped.sqrMagnitude > traveled.sqrMagnitude) { return stepped; }
            }

            // Wall: slide along it in the horizontal plane, losing the momentum heading into it.
            Vector3 wallNormal = new Vector3(hit.normal.x, 0f, hit.normal.z);
            if (wallNormal.sqrMagnitude < 1e-6f) { return traveled; }
            wallNormal.Normalize();
            collisions |= CollisionFlags.Sides;
            RemoveInertiaInto(ref inertia, wallNormal);
            Vector3 slide = Vector3.ProjectOnPlane(remaining, wallNormal);
            slide.y = 0f;
            float slideMag = slide.magnitude;
            if (slideMag < 1e-6f) { return traveled; }

            // Make sure the slide direction isn't itself blocked (a corner).
            if (Body.Sweep(feetPos + traveled, BodyRotation, slide / slideMag, slideMag + SkinWidth, out var slideHit))
            {
                Vector3 cornerNormal = new Vector3(slideHit.normal.x, 0f, slideHit.normal.z);
                if (cornerNormal.sqrMagnitude > 1e-6f) { RemoveInertiaInto(ref inertia, cornerNormal.normalized); }
                float slideTravel = Mathf.Max(0f, slideHit.distance - SkinWidth);
                return traveled + (slide / slideMag) * slideTravel;
            }
            return traveled + slide;
        }

        /// <summary>
        /// Attempt the classic step-up: lift the body by maxStepHeight, move forward, then drop back down.
        /// Returns Vector3.zero if the step cannot be made (headroom blocked, no floor, or floor too steep).
        /// </summary>
        private Vector3 TryStepUp(Vector3 feetPos, Vector3 dir, float distance)
        {
            // 1) Make sure there's headroom to lift up.
            float stepHeight = StepHeight;
            if (Body.Sweep(feetPos, BodyRotation, Vector3.up, stepHeight + SkinWidth, out _)) { return Vector3.zero; }
            Vector3 raised = feetPos + Vector3.up * stepHeight;

            // 2) Sweep forward from the raised position.
            if (Body.Sweep(raised, BodyRotation, dir, distance + SkinWidth, out var fwdHit))
            {
                float travel = Mathf.Max(0f, fwdHit.distance - SkinWidth);
                if (travel < 1e-4f * HeightScale) { return Vector3.zero; }
                raised += dir * travel;
            }
            else
            {
                raised += dir * distance;
            }

            // 3) Drop back down and make sure we land on walkable ground.
            if (!Body.Sweep(raised, BodyRotation, Vector3.down, stepHeight + SkinWidth, out var downHit)) { return Vector3.zero; }

            // 4) Make sure we land on the step, not perched on its edge. Moving only this step's distance forward, the
            // capsule's rounded bottom often comes down on the step's edge, well ahead of its axis. The ground check
            // (a slightly smaller sphere) then sees that edge as too steep to stand on, and the character slides back
            // off it, so steps a large fraction of the capsule's radius tall were never climbed. Instead, go on far
            // enough that the edge is within StepLandingAngle of straight below, for both spheres. Measured from the
            // contact point, not the hit normal, which for an edge can be either face's.
            float landingReach = Mathf.Min(SweepRadius, GroundCheckRadius) * Mathf.Sin(StepLandingAngle * Mathf.Deg2Rad);
            float ahead = Vector3.Dot(downHit.point - raised, dir);
            if (ahead > landingReach)
            {
                float extra = ahead - landingReach;
                if (Body.Sweep(raised, BodyRotation, dir, extra + SkinWidth, out _)) { return Vector3.zero; }
                raised += dir * extra;
                if (!Body.Sweep(raised, BodyRotation, Vector3.down, stepHeight + SkinWidth, out downHit)) { return Vector3.zero; }
            }
            if (Vector3.Angle(downHit.normal, Vector3.up) > maxSlopeAngle) { return Vector3.zero; }

            float drop = Mathf.Max(0f, downHit.distance - SkinWidth);
            Vector3 landed = raised - Vector3.up * drop;
            // Only accept if we actually ended up above the starting foot height (otherwise this isn't a step up).
            if (landed.y <= feetPos.y + 1e-4f * HeightScale) { return Vector3.zero; }
            // Nor if the step is taller than the step height. Judged by where the capsule touched down (the step's top,
            // or its edge, which is as high), not where its feet end up: perched part way up a taller step's edge, the
            // feet can be under the step height, and from there the character would ride up over the edge.
            if (downHit.point.y - feetPos.y > stepHeight + 1e-3f * HeightScale) { return Vector3.zero; }
            return landed - feetPos;
        }

        /// <summary>
        /// Resolve vertical motion: sweep up/down and clamp at the nearest contact, losing the momentum heading into
        /// it (so a jump doesn't keep pushing into a ceiling). Landing on a surface too steep to stand on slides the
        /// character down it rather than letting them rest there.
        /// </summary>
        private Vector3 ResolveVertical(Vector3 feetPos, float verticalDelta, ref Vector3 inertia, ref CollisionFlags collisions)
        {
            if (Mathf.Abs(verticalDelta) < 1e-6f) { return Vector3.zero; }
            Vector3 dir = verticalDelta > 0f ? Vector3.up : Vector3.down;
            float mag = Mathf.Abs(verticalDelta);
            if (!Body.Sweep(feetPos, BodyRotation, dir, mag + SkinWidth, out var hit)) { return dir * mag; }
            float travel = Mathf.Max(0f, hit.distance - SkinWidth);
            Vector3 traveled = dir * travel;

            collisions |= verticalDelta > 0f ? CollisionFlags.Above : CollisionFlags.Below;
            bool steep = Vector3.Angle(hit.normal, Vector3.up) > maxSlopeAngle;
            if (verticalDelta < 0f && !steep)
            {
                // Walkable ground just stops the fall. (Removing only the part heading into its surface would turn
                // some of the fall into sliding down a ramp.)
                inertia.y = Mathf.Max(inertia.y, 0f);
                return traveled;
            }
            RemoveInertiaInto(ref inertia, hit.normal);

            // Too steep to stand on (including the edge of a ledge caught by the capsule's rounded bottom): slide the
            // rest of the fall along the surface. The momentum kept above runs along the surface too, so the slide
            // speeds up like a fall would.
            if (verticalDelta < 0f)
            {
                Vector3 slide = Vector3.ProjectOnPlane(dir * (mag - travel), hit.normal);
                float slideMag = slide.magnitude;
                if (slideMag > 1e-6f)
                {
                    Vector3 slideDir = slide / slideMag;
                    if (Body.Sweep(feetPos + traveled, BodyRotation, slideDir, slideMag + SkinWidth, out var slideHit))
                    {
                        slide = slideDir * Mathf.Max(0f, slideHit.distance - SkinWidth);
                    }
                    traveled += slide;
                }
            }
            return traveled;
        }

        /// <summary>
        /// A collider's physics pose. Colliders on an interpolated Rigidbody follow its physics pose, not their
        /// transform, which may hold a smoothed render pose that lags the simulation.
        /// </summary>
        internal static void GetColliderPose(Collider collider, out Vector3 position, out Quaternion rotation)
        {
            Transform colliderTransform = collider.transform;
            Rigidbody body = collider.attachedRigidbody;
            if (body == null)
            {
                position = colliderTransform.position;
                rotation = colliderTransform.rotation;
                return;
            }

            Transform bodyTransform = body.transform;
            Quaternion toBodySpace = Quaternion.Inverse(bodyTransform.rotation);
            position = body.position + body.rotation * (toBodySpace * (colliderTransform.position - bodyTransform.position));
            rotation = body.rotation * (toBodySpace * colliderTransform.rotation);
        }

        /// <summary>
        /// Something the character's collision ignores, as it does its own colliders: an object it's holding and moving
        /// along with itself (see GrabState), which would otherwise block it. Null for nothing.
        /// </summary>
        public Transform IgnoredCollisionRoot { get; set; }

        /// <summary>Whether a collider is the character's own (or ignored: see IgnoredCollisionRoot), so collision skips it.</summary>
        internal bool IsOwnCollider(Transform other) =>
            other.IsChildOf(transform) || (IgnoredCollisionRoot != null && other.IsChildOf(IgnoredCollisionRoot));
    }
}
