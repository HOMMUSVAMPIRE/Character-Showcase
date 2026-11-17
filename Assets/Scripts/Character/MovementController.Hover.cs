using UnityEngine;

namespace Character
{
    // Hovering over steps (Hover Over Steps): the body held a step's height above the feet, and the feet kept on the ground
    // by probes under them, rather than by the body's bottom resting on it.
    public partial class MovementController
    {
        /// <summary>Whether the character's body is held above its feet, over steps (see Hover Over Steps).</summary>
        public bool HoverOverSteps { get => hoverOverSteps; set => hoverOverSteps = value; }

        /// <summary>
        /// How far (m) the body's bottom is held above the feet at the given size, held the given way: the step height,
        /// hovering over steps, except lying (swimming underwater), when the body lies on its feet; 0 otherwise.
        /// </summary>
        internal float BodyLiftAt(Vector3 scale, BodyPosture held) =>
            hoverOverSteps && held.Kind != BodyPostureKind.Lying ? maxStepHeight * scale.y : 0f;

        /// <summary>How far (m) the body's bottom is held above the feet now (see BodyLiftAt).</summary>
        public float BodyLift => BodyLiftAt(Scale, posture);

        /// <summary>
        /// Where the character's feet go with its body resting at feet (dropped onto something, say): hovering over steps,
        /// onto the ground its probes find under them, up to the body lift above (the body rests a step above the feet);
        /// otherwise, there. For an ability finding where the character will stand (see Ledges).
        /// </summary>
        public Vector3 FeetOnGround(Vector3 feet) =>
            BodyLift > 0f && ProbeGround(feet, SkinWidth, out float offset, out _, out _) && offset > 0f ? feet + Vector3.up * offset : feet;

        /// <summary>
        /// The ground under the feet, from the body lift above them to reach below them, if there is any: its height
        /// relative to the feet (offset: positive above them, onto a step; negative below), and what it is. A ray straight
        /// down under the feet finds it (the highest walkable thing it meets), or, where it meets none, the highest any of
        /// a few more round the feet meets (so a line between two treads or boxes doesn't let the feet fall through), or,
        /// if none does, the ground check's sphere (its bottom at the
        /// body lift) does, resting on walkable ground or a step's edge beside the feet, no higher than them (the feet
        /// just past an edge, still on it). byRay says which found it.
        /// </summary>
        private bool ProbeGround(Vector3 feet, float reachBelow, out float offset, out RaycastHit ground, out bool byRay)
        {
            float lift = BodyLift;
            float top = lift + SkinWidth;
            offset = float.NegativeInfinity;
            ground = default;
            byRay = false;

            float spread = 0.25f * GroundCheckRadius;
            for (int ray = 0; ray < StepEdgeRays.Length; ray++)
            {
                // The middle ray (the first) under the feet decides, if it finds ground: the others only stand in for it
                // where it falls between two treads or boxes. (The highest of them all held the feet up a slope's rise
                // above the ground on its uphill side, and up on a tread the feet had already walked off.)
                if (ray == 1 && byRay) { break; }
                Vector3 origin = feet + StepEdgeRays[ray] * spread + Vector3.up * top;
                int count = Physics.RaycastNonAlloc(origin, Vector3.down, stepHits, top + reachBelow, movementLayerMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = stepHits[i];
                    if (IsOwnCollider(hit.collider.transform) || Vector3.Angle(hit.normal, Vector3.up) > maxSlopeAngle) { continue; }
                    float height = top - hit.distance;
                    if (height > offset)
                    {
                        offset = height;
                        ground = hit;
                        byRay = true;
                    }
                }
            }
            if (byRay) { return true; }

            float r = GroundCheckRadius;
            Vector3 start = feet + Vector3.up * (top + r);
            int found = Physics.SphereCastNonAlloc(start, r, Vector3.down, castHits, top + reachBelow, movementLayerMask, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < found; i++)
            {
                RaycastHit hit = castHits[i];
                if (IsOwnCollider(hit.transform) || hit.distance <= 0f) { continue; }
                // Beside the feet, no higher than them: only the rays step the feet up.
                if (hit.point.y > feet.y + SkinWidth) { continue; }
                if (Vector3.Angle(hit.normal, Vector3.up) > maxSlopeAngle && !IsStepEdge(hit, feet)) { continue; }
                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    ground = hit;
                }
            }
            if (!float.IsFinite(nearest)) { return false; }
            offset = top - nearest;
            return true;
        }

        /// <summary>
        /// Whether a character hovering over steps is on the ground with its feet at position: its probes find ground
        /// within the grounded tolerance, or its body rests on something with ground under its feet within a step's
        /// height and the body lift (a long body over stairs, held up by the steps ahead of its feet). A body resting on
        /// something with nothing under its feet (perched on the edge of a drop) isn't.
        /// </summary>
        private bool CheckGroundedHovering(Vector3 position, out Transform ground)
        {
            ground = null;
            lastGroundedBy = GroundedBy.None;
            if (ProbeGround(position, GroundedTolerance, out _, out RaycastHit hit, out bool byRay))
            {
                ground = hit.transform;
                lastGroundedBy = byRay || Vector3.Angle(hit.normal, Vector3.up) <= maxSlopeAngle ? GroundedBy.Probe : GroundedBy.ProbeOnStepEdge;
                return true;
            }
            if (RestsOnSomething(position, out ground) && ProbeGround(position, StepHeight + BodyLift, out _, out _, out _))
            {
                lastGroundedBy = GroundedBy.BodyRest;
                return true;
            }
            ground = null;
            return false;
        }

        /// <summary>
        /// How far the feet must rise to be out of the ground they're in (the ground its probes find above them), as far
        /// as the body has room overhead: none if they're on or above it.
        /// </summary>
        private Vector3 OutOfTheGround(Vector3 feet, ref CollisionFlags collisions)
        {
            if (!ProbeGround(feet, 0f, out float offset, out _, out _) || offset <= SkinWidth) { return Vector3.zero; }
            if (Body.Sweep(feet, BodyRotation, Vector3.up, offset + SkinWidth, out RaycastHit above)) { offset = Mathf.Min(offset, Mathf.Max(0f, above.distance - SkinWidth)); }
            collisions |= CollisionFlags.Below;
            return Vector3.up * offset;
        }

        /// <summary>
        /// The vertical part of a step for a character hovering over steps, after its horizontal part (to pos): rising,
        /// its body is swept up (a ceiling stops it); otherwise, if it's on the ground or falling, its feet go to the
        /// ground its probes find (up onto a step, down a slope or a step, landing), within its fall this step (and, on
        /// the ground, the ground snap's reach, as the standing body's has), as far as its body has room to move there.
        /// Its body meeting something its feet have no ground under slides it off (an edge it's perched on) or down (a
        /// slope too steep to stand on). Records the step it made, if one, and the ground snap.
        /// </summary>
        private Vector3 ResolveVerticalHovering(Vector3 pos, float vert, float horizDist, ref MovementData data, ref Vector3 inertia)
        {
            bool grounded = data.initialGrounded;
            if (vert > 0f || (!grounded && vert >= 0f))
            {
                // Rising (a ceiling stops it), or held in the air (hanging, climbing, swimming up to its depth): only
                // never leaving the feet in the ground (walking up a beach, swimming), as the standing body's bottom,
                // pushed up by the ground, didn't.
                Vector3 risen = vert > 0f ? ResolveVertical(pos, vert, ref inertia, ref data.collisions) : Vector3.zero;
                return risen + OutOfTheGround(pos + risen, ref data.collisions);
            }

            float fall = -vert;
            float reach = fall;
            if (grounded)
            {
                // A step's height down, standing still too: feet that end up past a step's edge (stopping there) step
                // down onto the step below, as walking off it does, rather than perching on the edge over it.
                reach = Mathf.Max(reach, StepHeight + SkinWidth);
            }
            bool hasGround = ProbeGround(pos, reach, out float offset, out RaycastHit ground, out _);
            float target = hasGround ? offset : -fall;

            float dy = target;
            bool bodyMet = false;
            RaycastHit met = default;
            if (dy > 0f)
            {
                // Up onto a step, as far as there's room overhead.
                if (Body.Sweep(pos, BodyRotation, Vector3.up, dy + SkinWidth, out RaycastHit above)) { dy = Mathf.Min(dy, Mathf.Max(0f, above.distance - SkinWidth)); }
            }
            else if (dy < 0f && Body.Sweep(pos, BodyRotation, Vector3.down, -dy + SkinWidth, out met))
            {
                bodyMet = true;
                dy = -Mathf.Max(0f, met.distance - SkinWidth);
            }
            Vector3 moved = Vector3.up * dy;

            if (hasGround)
            {
                // On the ground (or held up over it by the body: a long body over stairs).
                data.collisions |= CollisionFlags.Below;
                inertia.y = Mathf.Max(inertia.y, 0f);
                if (grounded)
                {
                    // Further than walking along what it's on would take it (by more than the skin width): a step.
                    float slope = Vector3.Angle(ground.normal, Vector3.up);
                    float alongSlope = slope <= maxSlopeAngle ? horizDist * Mathf.Tan(slope * Mathf.Deg2Rad) : 0f;
                    if (Mathf.Abs(dy) > alongSlope + SkinWidth)
                    {
                        float step = dy - Mathf.Sign(dy) * alongSlope;
                        data.stepChange += step;
                        if (step > 0f) { data.resolution.steppedUp = step; }
                    }
                    if (dy < 0f)
                    {
                        data.resolution.snapped = true;
                        data.resolution.groundSnap = -dy;
                    }
                }
                return moved;
            }

            if (!bodyMet) { return moved; }
            // The body met something with no ground under the feet.
            data.collisions |= CollisionFlags.Below;
            float remaining = fall + dy;
            if (remaining <= 1e-6f) { return moved; }
            Vector3 slide;
            if (Vector3.Angle(met.normal, Vector3.up) > maxSlopeAngle)
            {
                // Too steep to stand on: slides down it, as the standing body does (see ResolveVertical).
                RemoveInertiaInto(ref inertia, met.normal);
                slide = Vector3.ProjectOnPlane(Vector3.down * remaining, met.normal);
            }
            else
            {
                // Perched on something (an edge) with its feet over nothing: tips off it, away from where it meets it.
                Vector3 away = pos - met.point;
                away.y = 0f;
                if (away.sqrMagnitude < 1e-8f) { away = -(BodyRotation * Vector3.forward); away.y = 0f; }
                slide = away.normalized * remaining;
            }
            float slideDistance = slide.magnitude;
            if (slideDistance > 1e-6f)
            {
                Vector3 direction = slide / slideDistance;
                Vector3 from = pos + moved;
                if (Body.Sweep(from, BodyRotation, direction, slideDistance + SkinWidth, out RaycastHit blocked))
                {
                    slide = direction * Mathf.Max(0f, blocked.distance - SkinWidth);
                }
                moved += slide;
            }
            return moved;
        }
    }
}
