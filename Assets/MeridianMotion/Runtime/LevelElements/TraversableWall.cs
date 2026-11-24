using UnityEngine;

namespace MeridianMotion.Levels
{
    /// <summary>
    /// Marks colliders a character can wall-run along, or slide down (see the movement mode's WallRunning and
    /// WallSliding components): this object's colliders and those of its children, so one on a group marks the whole
    /// group. A wall without one can't be run along or slid down (unless the character's Wall Run On Any Wall or Wall
    /// Slide On Any Wall is on), so a level says where these moves go, as it does for climbing with a ClimbableSurface.
    /// A wall is for one move, never both: a wall to run along and a wall to kick up are different promises to the
    /// player, and its look says which. Disable the component to unmark it for a while.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Meridian Motion/Levels/Traversable Wall")]
    public class TraversableWall : MonoBehaviour
    {
        /// <summary>The move a traversable wall is for.</summary>
        public enum WallMove
        {
            /// <summary>Running along it.</summary>
            WallRun,
            /// <summary>Sliding down it, and so jumping off it.</summary>
            WallSlide,
        }

        [Tooltip("What a character can do on it: run along it (Wall Run), or slide down it and so jump off it (Wall Slide)")]
        [SerializeField] private WallMove move = WallMove.WallRun;

        /// <summary>The move it's for.</summary>
        public WallMove Move { get => move; set => move = value; }

        /// <summary>The traversable wall a collider belongs to (on it or a parent, and enabled), or null if it isn't one.</summary>
        public static TraversableWall Of(Collider collider)
        {
            if (collider == null) { return null; }
            TraversableWall wall = collider.GetComponentInParent<TraversableWall>();
            return wall != null && wall.isActiveAndEnabled ? wall : null;
        }

        /// <summary>Whether a character can wall-run along a collider.</summary>
        public static bool AllowsWallRun(Collider collider) => Of(collider) is { Move: WallMove.WallRun };

        /// <summary>Whether a character can slide down a collider.</summary>
        public static bool AllowsWallSlide(Collider collider) => Of(collider) is { Move: WallMove.WallSlide };
    }
}
