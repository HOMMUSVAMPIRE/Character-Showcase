using MeridianMotion.PhysicsQueries;
using UnityEngine;

namespace MeridianMotion.Characters
{
    // The ground: being carried by moving ground, and whether the character is grounded (a step's edge counts); moving the
    // character at once (Teleport, Respawn).
    public partial class MMotionMotor : MeridianMotion.Levels.IRespawnable
    {
        // Max Slope Angle's cosine, worked out again only when the setting changes (walkableAngle is what it was for).
        [System.NonSerialized] private float walkableAngle = -1f, walkableCos;

        /// <summary>
        /// Whether ground with this normal (a unit vector: a hit's) is walkable: no steeper than Max Slope Angle. By its
        /// up component against the angle's cosine, which is the same test as the angle between it and up (no acos).
        /// </summary>
        public bool IsWalkable(Vector3 normal)
        {
            if (maxSlopeAngle != walkableAngle)
            {
                walkableAngle = maxSlopeAngle;
                walkableCos = Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad);
            }
            return normal.y >= walkableCos;
        }

        /// <summary>
        /// Carries the character with the ground they stood on last step (position, and yaw if enabled), then checks
        /// whether they are grounded. If the carried position isn't grounded (e.g. they just jumped), the carry is
        /// skipped and the ground's velocity is added to their inertia instead, so they keep its momentum.
        /// Returns whether the character is grounded, and sets groundTransform.
        /// </summary>
        private bool ApplyGroundCarry(MovementStep data)
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

        /// <summary>
        /// Puts the character's feet at feet at once, between steps (turned to rotation, if given): a teleport. It forgets
        /// the ground it stood on, which would otherwise carry it back on the next step: the carry follows where the
        /// character last stood on its ground, so a teleport shorter than Max Ground Carry Per Step, made standing, was
        /// undone.
        /// </summary>
        public void Teleport(Vector3 feet, Quaternion? rotation = null)
        {
            carrierGround = null;
            Quaternion turn = rotation ?? (rb != null ? rb.rotation : transform.rotation);
            if (rb != null)
            {
                rb.position = feet;
                rb.rotation = turn;
            }
            transform.SetPositionAndRotation(feet, turn);
        }

        /// <summary>
        /// Puts the character back at feet, facing rotation, between steps, as if it had just arrived there standing still
        /// (IRespawnable: a checkpoint's respawn). Unlike Teleport, which keeps them, it ends every ability under way
        /// (each lets go of what it held: a rope, a crate, a zipline, a ledge) and drops the character's momentum, any
        /// force or velocity change waiting for the next step, and the ground it stood on; crouched, it stands up as soon
        /// as there's room. Its jumps and air dashes come back as they do on landing, on its first step on the ground there. The last step's state is kept, so the next
        /// step raises the ends of the states it was in (WallRunEnded, say), as an ability ending any other way does.
        /// </summary>
        public void Respawn(Vector3 feet, Quaternion rotation)
        {
            if (movementMode is AbilityMovementMode mode)
            {
                // Abilities let go between steps as they do when another takes over; they're given the last step's data
                // (a fresh record before the first step, so none has to cope with null).
                mode.InterruptOthers(lastStep ?? new MovementStep(), this, except: null);
            }
            if (lastStep != null)
            {
                // The next step starts from the last one's final momentum and velocity: none.
                lastStep.velocityOverride = Vector3.zero;
                lastStep.directMovementVector = Vector3.zero;
            }
            // Crouched, it stands up as soon as there's room (as a press of Crouch would ask it to).
            if (crouch.IsCrouched) { crouch.wantsToStand = true; }
            pendingVelocityChange = Vector3.zero;
            pendingForce = Vector3.zero;
            velocityOverride = null;
            GroundVelocity = Vector3.zero;
            if (rb != null && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; }
            Teleport(feet, rotation);
            // Called after a step (a hazard's respawn), the step's own move is still to be carried out at the physics step
            // (CompleteMovement's Rigidbody.Move), which would take the character back where it was: move it here instead.
            if (rb != null) { rb.Move(feet, rotation); }
        }

        /// <summary>Where the character's feet are: its Rigidbody's position (the physics pose, not the interpolated transform).</summary>
        public Vector3 Feet => rb != null ? rb.position : transform.position;

        /// <summary>
        /// Where the character is in the level's volumes (checkpoints, hazards, shortcut triggers, lesson zones): those its
        /// feet are in and the checkpoint it last reached. Part of its state, so it's saved, restored and replayed with it.
        /// Updated after each step (MMotionCharacter.StepMovement, LevelPresence.AfterStep).
        /// </summary>
        public MeridianMotion.Levels.LevelPresence levelPresence { get; } = new();

        /// <summary>Records the character's pose relative to their ground at the end of the step, for the next carry.</summary>
        private void RememberGround(MovementStep data)
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
            if (BodyLift > 0f) { return CheckGroundedHovering(position, out groundTransform); }
            groundTransform = null;
            lastGroundedBy = GroundedBy.None;

            float r = GroundCheckRadius;
            // Start with the bottom of the sphere just above the feet so we can detect ground slightly below them,
            // and reject things above them. (2 cm at the authored size, scaled with it.)
            float startLift = 0.02f * ClearanceScale;
            Vector3 start = position + Vector3.up * (r + startLift);
            // Sweep the sphere's bottom from startLift above the feet to GroundedTolerance below them. Distance is
            // measured from the sphere's bottom, so the radius must NOT be added here — doing so made the character
            // count as grounded up to (r + tolerance) above the floor, cancelling their fall while still in the air.
            float castDist = startLift + GroundedTolerance;

            int count = castHits.SphereCast(start, r, Vector3.down, castDist, movementLayerMask, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            RaycastHit bestHit = default;
            bool bestOnEdge = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = castHits[i];
                if (IsOwnCollider(hit.transform)) { continue; }
                if (hit.distance <= 0f) { continue; }
                bool steep = !IsWalkable(hit.normal);
                if (steep && !IsStepEdge(hit, position)) { continue; }
                // The nearest; of two as near, the first in the stable order (see StableOrder).
                if (StableOrder.Better(hit.distance, best, hit, bestHit))
                {
                    best = hit.distance;
                    bestHit = hit;
                    bestOnEdge = steep;
                    // The object actually hit (its Rigidbody's transform, if it has one) - not the hierarchy root,
                    // which for a platform parented under a static "Environment" object would never move.
                    groundTransform = hit.transform;
                }
            }
            if (float.IsFinite(best))
            {
                lastGroundedBy = bestOnEdge ? GroundedBy.ProbeOnStepEdge : GroundedBy.Probe;
                return true;
            }
            if (!RestsOnSomething(position, out groundTransform)) { return false; }
            lastGroundedBy = GroundedBy.BodyRest;
            return true;
        }

        // How the latest ground check found the character on the ground (see StepResolution.groundedBy).
        private GroundedBy lastGroundedBy;

        /// <summary>
        /// Whether the body itself, its feet at position, rests on walkable ground or a step's edge (within the grounded
        /// tolerance below it), for when the ground check's sphere, a little narrower, finds nothing. Walking down stairs
        /// whose treads are shallower than the body is wide, the body rolls over each step's edge on its rounded bottom,
        /// and the ground snap rests it on that edge (see IsStepEdge), its feet above the tread below. For a slim body
        /// (one fitted to its model), the edge was beyond the narrower sphere, and the tread beyond the tolerance below
        /// it, so the character left the ground for a step there, and sprinting, flew on down the stairs.
        /// </summary>
        private bool RestsOnSomething(Vector3 position, out Transform groundTransform)
        {
            groundTransform = null;
            if (!Body.Sweep(position, BodyRotation, Vector3.down, SkinWidth + GroundedTolerance, out RaycastHit rest)) { return false; }
            if (IsOwnCollider(rest.transform)) { return false; }
            if (!IsWalkable(rest.normal) && !IsStepEdge(rest, position)) { return false; }
            groundTransform = rest.transform;
            return true;
        }

        // Reused by IsStepEdge, which runs while castHits is being read.
        private readonly RaycastHitBuffer stepHits = new(8, "MMotionMotor.stepHits");

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
            float radius = Body.FootprintRadius;
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
                int count = stepHits.Raycast(origin, Vector3.down, reach, movementLayerMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    RaycastHit below = stepHits[i];
                    if (IsOwnCollider(below.collider.transform)) { continue; }
                    if (IsWalkable(below.normal)) { return true; }
                }
            }
            return false;
        }

        // Where IsStepEdge looks for ground, around the feet (horizontal offsets, as shares of its spread).
        private static readonly Vector3[] StepEdgeRays = { Vector3.zero, Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
    }
}
