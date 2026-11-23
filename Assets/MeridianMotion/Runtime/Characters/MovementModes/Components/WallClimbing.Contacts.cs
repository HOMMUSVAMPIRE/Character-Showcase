using System;
using MeridianMotion.Levels;
using MeridianMotion.PhysicsQueries;
using UnityEngine;
using static MeridianMotion.Characters.MovementRules;

namespace MeridianMotion.Characters
{
    // WallClimbing's contact search: finding the climbable surface nearest the character's grip, and whether it can climb to or hold on there.
    public partial class WallClimbing
    {
        // Reused for every climbing probe (several per physics step), so they don't allocate. Movement modes are shared
        // assets and run on the main thread only, one character at a time.
        private static readonly ColliderBuffer climbOverlaps = new(16, "WallClimbing.climbOverlaps");

        /// <summary>
        /// The surface nearest the character's grip, if it's climbable (all of it, or painted climbable there): the collider, the point on it nearest the grip, its
        /// normal there (toward the grip; round an edge or corner, it points from the edge), and how far it is. Also how
        /// far the nearest unclimbable surface is (infinity if there's none in reach).
        /// </summary>
        private struct ClimbContact
        {
            public Collider collider;
            // The climbing speed multiplier there (see ClimbableSurface and PaintedClimbableSurface)
            public float speedMultiplier;
            public Vector3 point;
            public Vector3 normal;
            public float distance;
            public float unclimbableDistance;
            // Whether the point is on an edge or corner of the surface, not a face (the normal points from the edge)
            public bool atEdge;
            // The ladder it's on, if it's one's (its front face, on its middle at the grip's height); null otherwise
            public ClimbableLadder ladder;
        }

        /// <summary>
        /// Finds the surface nearest the character's grip (with its feet at feet), within reach, among the climbable
        /// ones, and how far the nearest unclimbable one is. hintNormal is the way the surface is expected to face, for
        /// colliders whose nearest point can't be found directly (see MMotionMotor.ClosestPointOn). False if
        /// nothing climbable is within reach.
        /// </summary>
        private bool ProbeClimbableSurface(MMotionMotor motor, Vector3 feet, Vector3 hintNormal, float reach,
            out ClimbContact contact, float paintTolerance = 0f)
        {
            contact = new ClimbContact { distance = float.PositiveInfinity, unclimbableDistance = float.PositiveInfinity };
            Vector3 grip = feet + Vector3.up * (wallClimbGripHeight * motor.Body.StandingHeight);
            int count = motor.OverlapSphere(grip, reach, climbOverlaps);
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider candidate = climbOverlaps[i];
                // Looked up once (it's a search up the collider's parents): kept below as whether it's a ladder at all.
                ClimbableLadder candidateLadder = ClimbableLadder.Of(candidate);
                ClimbableLadder ladder = candidateLadder;
                Vector3 point, normal;
                float distance, speedMultiplier = 1f;
                bool atEdge = false;
                if (ladder != null && OnLadder(motor, ladder, grip, out point, out distance))
                {
                    // A ladder is climbed on its front face, on its middle: always square on to it.
                    normal = ladder.Forward;
                }
                else
                {
                    ladder = null;
                    if (!MMotionMotor.ClosestPointOn(candidate, grip, -hintNormal, reach, out point)) { continue; }
                    distance = Vector3.Distance(grip, point);
                    normal = (grip - point) / Mathf.Max(distance, 1e-6f);
                    atEdge = IsAtEdge(candidate, grip, normal, distance, reach);
                    // A collider's nearest point decides it: where that isn't climbable (an unpainted part of a painted
                    // one, a ladder from behind or past its ends), the collider is as unclimbable there as a plain wall.
                    if (candidateLadder != null
                        || !ClimbableSurface.IsClimbableAt(candidate, point, normal, atEdge, paintTolerance, out speedMultiplier))
                    {
                        contact.unclimbableDistance = Mathf.Min(contact.unclimbableDistance, distance);
                        continue;
                    }
                }
                // The surface held (facing as hintNormal) wins a near tie: in an inside corner, both walls are at the
                // holding distance, and without this the character kept turning back to the first wall it held, never
                // moving on along the other.
                float score = Vector3.Dot(normal, hintNormal) > 0.95f ? distance - motor.Skin : distance;
                if (score >= bestScore) { continue; }
                bestScore = score;
                contact.collider = candidate;
                contact.speedMultiplier = speedMultiplier;
                contact.point = point;
                contact.distance = distance;
                contact.normal = normal;
                contact.atEdge = atEdge;
                contact.ladder = ladder;
            }
            return contact.collider != null;
        }

        /// <summary>
        /// Whether a grip is on a ladder: in front of its front face (it's only climbed from the front), between its
        /// bottom and top, and no further to its side than the character's radius past its edge. Gives the point on its
        /// front face, on its middle at the grip's height, and how far in front of the face the grip is.
        /// </summary>
        private static bool OnLadder(MMotionMotor motor, ClimbableLadder ladder, Vector3 grip, out Vector3 point, out float distance)
        {
            float up = ladder.HeightOf(grip);
            distance = ladder.InFrontOf(grip);
            point = ladder.FrontBottom + ladder.Up * up;
            return distance > -motor.Skin && up >= 0f && up <= ladder.Height
                   && Mathf.Abs(ladder.SideOf(grip)) <= 0.5f * ladder.Width + motor.Body.SideExtent;
        }

        /// <summary>On a ladder, the move this step back toward its middle (at most step long); zero off one.</summary>
        private static Vector3 LadderCentring(ClimbableLadder ladder, Vector3 feet, float step)
        {
            if (ladder == null) { return Vector3.zero; }
            return -ladder.Right * Mathf.Clamp(ladder.SideOf(feet), -step, step);
        }

        /// <summary>
        /// Whether the point on a collider nearest the grip is on an edge or corner of it rather than a face: on a face, a
        /// ray from the grip back along the normal meets the surface square on; round an edge it meets one of the faces
        /// either side, at an angle, or misses.
        /// </summary>
        private static bool IsAtEdge(Collider collider, Vector3 grip, Vector3 normal, float distance, float reach) =>
            !collider.Raycast(new Ray(grip, -normal), out RaycastHit square, distance + reach) || Vector3.Dot(square.normal, normal) < 0.996f;

        /// <summary>
        /// Whether the character, with its feet at feet, would still be on the climbable area (see CanHoldAt), its grip
        /// no further from the surface than from (where it is now) or the edge tolerance allows.
        /// </summary>
        private bool CanClimbTo(MMotionMotor motor, Vector3 feet, Vector3 hintNormal, float reach, ClimbContact from, out ClimbContact contact) =>
            ProbeClimbableSurface(motor, feet, hintNormal, reach, out contact) && CanHoldAt(motor, contact, from.distance);

        /// <summary>
        /// Whether a contact is one the character can climb on: steep enough, with its grip near enough the surface
        /// (within the edge tolerance of its holding distance, or no further than allowedDistance: having just taken
        /// hold from a little way off, it's drawn in as it climbs), and nothing unclimbable nearer.
        /// </summary>
        private bool CanHoldAt(MMotionMotor motor, ClimbContact contact, float allowedDistance = 0f) =>
            contact.collider != null && SteepEnoughToClimb(contact.normal)
            && contact.distance <= Mathf.Max(WallClimbHoldDistance(motor) + WallClimbEdgeTolerance * motor.HeightScale, allowedDistance + 1e-4f)
            && !IsUnclimbableNearer(motor, contact);

        /// <summary>
        /// Whether something unclimbable ends the climbable area at a contact: nearer than the climbable surface, or round
        /// an edge, close beyond it (see WallClimbCornerClearance).
        /// </summary>
        private static bool IsUnclimbableNearer(MMotionMotor motor, ClimbContact contact) =>
            contact.unclimbableDistance < (contact.atEdge
                ? contact.distance + WallClimbCornerClearance * motor.HeightScale
                : contact.distance - WallClimbUnclimbableMargin * motor.Skin);

        /// <summary>Whether a surface with this normal is steep enough to climb: within the lean back and overhang of upright.</summary>
        private bool SteepEnoughToClimb(Vector3 normal) =>
            normal.y <= Mathf.Sin(wallClimbMaxLeanBack * Mathf.Deg2Rad) && normal.y >= -Mathf.Sin(wallClimbMaxOverhang * Mathf.Deg2Rad);

        /// <summary>How far the character's grip is held from the surface: its collider just clear of it.</summary>
        private static float WallClimbHoldDistance(MMotionMotor motor) => motor.Body.FrontExtent + motor.Skin;

        /// <summary>How far from the grip a surface being climbed is looked for.</summary>
        private float WallClimbReach(MMotionMotor motor) =>
            motor.Body.FrontExtent + Mathf.Max(wallClimbStartReach, 2f * WallClimbEdgeTolerance) * motor.HeightScale + 2f * motor.Skin;
    }
}
