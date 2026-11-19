using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// An AI that walks (or sprints) a character along a route of points, in order, facing each as it goes, and stands
    /// idle at the end (or goes round again: Loop). On the ground it jumps where the way ahead needs it: where the ground
    /// drops away ahead (a gap) while the next point isn't below, and where something ahead is taller than a step but
    /// within what the character can get up (its MovementCapabilities.MoveUp: a jump, or a jump to a ledge it climbs).
    /// It holds Jump while the character rises, for the full height. Not pathfinding: the route is the way.
    /// </summary>
    [AddComponentMenu("Meridian Motion/Characters/Waypoint Input Source")]
    public class WaypointInputSource : CharacterInputSource
    {
        [Header("Route")]
        [Tooltip("The points to go to, in world space, in order. A point counts as reached within Arrive Distance of it, " +
                 "across the ground (its height doesn't count).")]
        [SerializeField] private List<Vector3> waypoints = new();
        [Tooltip("Go round the route again from the first point after the last, rather than standing idle there.")]
        [SerializeField] private bool loop;
        [Tooltip("Sprint along the route, rather than walk.")]
        [SerializeField] private bool sprint;
        [Tooltip("How close (m, across the ground, at the character's authored size) counts as having reached a point.")]
        [Min(0.05f)] [SerializeField] private float arriveDistance = 0.4f;

        [Header("Jumping")]
        [Tooltip("How far ahead the way is checked for a gap or something to jump up, in seconds at the character's speed " +
                 "(at least Min Look Ahead).")]
        [Min(0f)] [SerializeField] private float lookAheadSeconds = 0.1f;
        [Tooltip("The least distance ahead (m, at the character's authored size) the way is checked.")]
        [Min(0.05f)] [SerializeField] private float minLookAhead = 0.4f;

        private int next;
        private bool rising;

        /// <summary>The route's points, in world space (change it, then call Restart).</summary>
        public List<Vector3> Waypoints => waypoints;
        /// <summary>Whether it goes round again after the last point.</summary>
        public bool Loop { get => loop; set => loop = value; }
        /// <summary>Whether it sprints, rather than walks.</summary>
        public bool Sprint { get => sprint; set => sprint = value; }
        /// <summary>The index of the point it's going to; Waypoints.Count once it's reached the last (not looping).</summary>
        public int NextWaypoint => next;
        /// <summary>Whether it's reached the last point (never while looping).</summary>
        public bool Arrived => next >= waypoints.Count;

        /// <summary>Sets the route (copied), and starts it from its first point.</summary>
        public void SetRoute(IEnumerable<Vector3> points, bool goRound = false, bool sprinting = false)
        {
            waypoints.Clear();
            waypoints.AddRange(points);
            loop = goRound;
            sprint = sprinting;
            Restart();
        }

        /// <summary>Starts the route again from its first point.</summary>
        public void Restart()
        {
            next = 0;
            rising = false;
        }

        public override CharacterInputFrame FrameFor(uint tick, MMotionMotor character)
        {
            var frame = new CharacterInputFrame { tick = tick };
            if (character == null || waypoints.Count == 0 || Arrived) { return frame; }
            float scale = character.HeightScale;
            Vector3 feet = character.PhysicsPosition;
            Vector3 toNext = Across(waypoints[next] - feet);
            // Reached: on to the next (past as many as are already within reach, going round at most once), or done.
            for (int passed = 0; passed < waypoints.Count && toNext.magnitude <= arriveDistance * scale; passed++)
            {
                next++;
                if (next >= waypoints.Count)
                {
                    if (!loop) { return frame; }
                    next = 0;
                }
                toNext = Across(waypoints[next] - feet);
            }
            if (toNext.magnitude <= arriveDistance * scale) { return frame; }

            Vector3 direction = toNext.normalized;
            frame.lookYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            frame.move = new Vector2(0f, 1f);
            if (sprint) { frame.held |= InputButtons.Sprint; }
            if (WantsJump(character, feet, direction, scale)) { frame.held |= InputButtons.Jump; }
            return frame;
        }

        private static Vector3 Across(Vector3 v) => new(v.x, 0f, v.z);

        /// <summary>Whether to hold Jump this step: to start a jump the way ahead needs, or to keep rising in one.</summary>
        private bool WantsJump(MMotionMotor character, Vector3 feet, Vector3 direction, float scale)
        {
            MovementStep last = character.lastStep;
            if (last == null) { return false; }
            // Held while rising from its jump (for the jump's full height), then let go.
            if (rising)
            {
                rising = last.finalVelocity.y > 0f && !last.state.HasAny(CharacterState.Grounded);
                if (rising) { return true; }
            }
            if (!last.state.HasAny(CharacterState.Grounded)) { return false; }

            float step = character.MaxStepHeight * scale;
            float speed = Across(last.finalVelocity).magnitude;
            float ahead = Mathf.Max(minLookAhead * scale, speed * lookAheadSeconds);
            Vector3 up = Vector3.up;
            bool jump = false;

            // A gap: no ground within a step's drop ahead, while the next point isn't below (going down, it walks off).
            Vector3 probe = feet + direction * ahead + up * step;
            bool nextIsBelow = next < waypoints.Count && waypoints[next].y < feet.y - step;
            if (!nextIsBelow && !character.Raycast(probe, Vector3.down, 2f * step, out _)) { jump = true; }

            // Something to get up: in the way, above a step high, its top within what the character can get up.
            Vector3 knee = feet + up * (step + 0.05f * scale);
            if (!jump && character.Raycast(knee, direction, ahead, out RaycastHit wall))
            {
                float reach = 4f * character.Body.StandingHeight;
                Vector3 above = wall.point + direction * (0.05f * scale) + up * reach;
                if (character.Raycast(above, Vector3.down, reach, out RaycastHit top))
                {
                    float height = top.point.y - feet.y;
                    jump = height > step && character.Capabilities.MoveUp(height) != null;
                }
            }
            rising = jump;
            return jump;
        }
    }
}
