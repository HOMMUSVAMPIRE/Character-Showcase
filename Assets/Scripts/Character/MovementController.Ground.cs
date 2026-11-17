using UnityEngine;

namespace Character
{
    // The ground: being carried by moving ground, and whether the character is grounded (a step's edge counts).
    public partial class MovementController
    {
        /// <summary>
        /// Carries the character with the ground they stood on last step (position, and yaw if enabled), then checks
        /// whether they are grounded. If the carried position isn't grounded (e.g. they just jumped), the carry is
        /// skipped and the ground's velocity is added to their inertia instead, so they keep its momentum.
        /// Returns whether the character is grounded, and sets groundTransform.
        /// </summary>
        private bool ApplyGroundCarry(ref MovementData data)
        {
            GroundVelocity = Vector3.zero;
            Vector3 carry = Vector3.zero;
            Quaternion turn = Quaternion.identity;

            if (moveWithGround && carrierGround != null)
            {
                GetGroundPose(carrierGround, out Vector3 groundPosition, out Quaternion groundRotation);
                carry = groundPosition + groundRotation * carrierLocalPosition - data.initialPosition;
                if (rotateWithGround) { turn = YawOnly(groundRotation * Quaternion.Inverse(carrierRotation)); }
                if (carry.sqrMagnitude > maxGroundCarryPerStep * maxGroundCarryPerStep)
                {
                    carry = Vector3.zero; // Teleported: don't drag the character across the level.
                    turn = Quaternion.identity;
                }
            }

            Vector3 carriedPosition = data.initialPosition + carry;
            // Static ground still produces a tiny non-zero carry from floating point error; don't apply that.
            bool groundMoved = carry.sqrMagnitude > 1e-10f || Quaternion.Angle(Quaternion.identity, turn) > 1e-3f;
            if (!groundMoved)
            {
                return CheckGrounded(data.initialPosition, out groundTransform);
            }

            if (CheckGrounded(carriedPosition, out groundTransform))
            {
                data.initialPosition = carriedPosition;
                data.initialRotation = turn * data.initialRotation;
                pendingGroundRotation = turn * pendingGroundRotation;
                GroundVelocity = carry / data.deltaTime;
                return true;
            }

            // Left the moving ground: keep moving at its velocity rather than stopping dead.
            if (inheritGroundVelocity)
            {
                data.initialInertia += carry / data.deltaTime;
            }
            return CheckGrounded(data.initialPosition, out groundTransform);
        }

        /// <summary>Records the character's pose relative to their ground at the end of the step, for the next carry.</summary>
        private void RememberGround(MovementData data)
        {
            carrierGround = data.initialGrounded ? groundTransform : null;
            if (carrierGround == null) { return; }

            GetGroundPose(carrierGround, out Vector3 groundPosition, out Quaternion groundRotation);
            Vector3 finalPosition = data.initialPosition + data.finalMovementVector;
            carrierLocalPosition = Quaternion.Inverse(groundRotation) * (finalPosition - groundPosition);
            carrierRotation = groundRotation;
        }

        /// <summary>
        /// The ground's physics pose. For ground with a Rigidbody, use the body's pose rather than its transform, which
        /// may be an interpolated render pose that lags the simulation.
        /// </summary>
        private static void GetGroundPose(Transform ground, out Vector3 position, out Quaternion rotation)
        {
            if (ground.TryGetComponent(out Rigidbody body))
            {
                position = body.position;
                rotation = body.rotation;
            }
            else
            {
                position = ground.position;
                rotation = ground.rotation;
            }
        }

        /// <summary>The part of a rotation that turns about the world up axis.</summary>
        private static Quaternion YawOnly(Quaternion rotation)
        {
            Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-6f) { return Quaternion.identity; }
            return Quaternion.Euler(0f, Vector3.SignedAngle(Vector3.forward, forward, Vector3.up), 0f);
        }

        /// <summary>
        /// Returns the ground yaw applied to the character since the last call, and resets it. Used by visuals that
        /// smooth their own rotation, so they turn with spinning ground instead of lagging behind it.
        /// </summary>
        public Quaternion ConsumeGroundRotation()
        {
            Quaternion turn = pendingGroundRotation;
            pendingGroundRotation = Quaternion.identity;
            return turn;
        }

        /// <summary>
        /// Checks whether the character is on the ground via spherecast downwards (gravity is always down: other
        /// directions are a future change, in the README).
        /// </summary>
        /// <param name="position"></param>
        /// <param name="groundTransform"></param>
        /// <returns></returns>
        public bool CheckGrounded(Vector3 position, out Transform groundTransform)
        {
            groundTransform = null;

            float r = GroundCheckRadius;
            // Start with the bottom of the sphere just above the feet so we can detect ground slightly below them,
            // and reject things above them. (2 cm at the authored size, scaled with it.)
            float startLift = 0.02f * ClearanceScale;
            Vector3 start = position + Vector3.up * (r + startLift);
            // Sweep the sphere's bottom from startLift above the feet to GroundedTolerance below them. Distance is
            // measured from the sphere's bottom, so the radius must NOT be added here — doing so made the character
            // count as grounded up to (r + tolerance) above the floor, cancelling their fall while still in the air.
            float castDist = startLift + GroundedTolerance;

            int count = Physics.SphereCastNonAlloc(start, r, Vector3.down, castHits, castDist, movementLayerMask, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = castHits[i];
                if (IsOwnCollider(hit.transform)) { continue; }
                if (hit.distance <= 0f) { continue; }
                if (Vector3.Angle(hit.normal, Vector3.up) > maxSlopeAngle && !IsStepEdge(hit, position)) { continue; }
                if (hit.distance < best)
                {
                    best = hit.distance;
                    // The object actually hit (its Rigidbody's transform, if it has one) - not the hierarchy root,
                    // which for a platform parented under a static "Environment" object would never move.
                    groundTransform = hit.transform;
                }
            }
            return float.IsFinite(best);
        }

        // Reused by IsStepEdge, which runs while castHits is being read.
        private readonly RaycastHit[] stepHits = new RaycastHit[8];

        /// <summary>
        /// Whether a contact too steep to stand on is the edge of a step the character stands at the top of or is walking
        /// down: one below its middle, with walkable ground no further than the step height straight below its feet.
        /// Stairs' treads are shallower than the capsule is wide, so its rounded bottom rests on their edges (their
        /// nosings), whose contact normals are steep. Counted as too steep, walking down stairs whose steps neared the
        /// step height lifted the character off the ground at the top step: it floated out over the stairs in coyote
        /// time, landed a few steps down carrying its walk as momentum on top of walking, and went again. The edge of
        /// a drop taller than a step still isn't ground, so a character perched on a ledge's edge slides off it.
        /// </summary>
        private bool IsStepEdge(RaycastHit hit, Vector3 feet)
        {
            float radius = Body.Collider != null ? Body.FootprintRadius : 0.5f * HeightScale;
            if (hit.point.y > feet.y + radius) { return false; }
            float lift = SkinWidth;
            float reach = lift + StepHeight + GroundedTolerance + SkinWidth;
            // A few rays around the feet rather than one: on stairs whose treads are a multiple of a step's movement, a
            // single ray down the character's middle could land exactly on the line between two treads, and hit the
            // riser's face there (its normal sideways). Walking down a long flight, that came round now and then, and
            // the character fell off the stairs mid-flight.
            float spread = 0.25f * radius;
            for (int ray = 0; ray < StepEdgeRays.Length; ray++)
            {
                Vector3 origin = feet + StepEdgeRays[ray] * spread + Vector3.up * lift;
                int count = Physics.RaycastNonAlloc(origin, Vector3.down, stepHits, reach, movementLayerMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    RaycastHit below = stepHits[i];
                    if (IsOwnCollider(below.collider.transform)) { continue; }
                    if (Vector3.Angle(below.normal, Vector3.up) <= maxSlopeAngle) { return true; }
                }
            }
            return false;
        }

        // Where IsStepEdge looks for ground, around the feet (horizontal offsets, as shares of its spread).
        private static readonly Vector3[] StepEdgeRays = { Vector3.zero, Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
    }
}
