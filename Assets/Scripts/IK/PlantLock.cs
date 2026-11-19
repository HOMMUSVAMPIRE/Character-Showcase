using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// Holds a planted foot or paw where it was put down (HumanoidFootPlacementIK's Foot Lock, LegPlacementIK's Foot Lock). It
    /// locks as the animation fully plants the limb, holding the point in the space of the ground under it (so it rides
    /// moving ground). It lets go as the limb lifts, or if the animation's limb gets too far from the lock, so the leg is
    /// never stretched to hold it. It's blended in and out over its blend time. How the held point is blended over where
    /// the limb is placed (across the ground only, or with its height) is the placement's own.
    /// </summary>
    public struct PlantLock
    {
        /// <summary>Whether the limb is locked now.</summary>
        public bool locked;
        /// <summary>The ground it was put down on (null for none: held in the world).</summary>
        public Transform ground;
        /// <summary>Where it's held, in the space of its ground (or the world).</summary>
        public Vector3 point;
        /// <summary>How far the lock is blended in, from 0 to 1.</summary>
        public float weight;

        /// <summary>Where the lock holds the limb now, in the world (it moves with the ground it was put down on).</summary>
        public readonly Vector3 Position => ground != null ? ground.TransformPoint(point) : point;

        /// <summary>
        /// Updates the lock for this frame. placed is where the limb is placed now; lockAt is where a new lock would hold
        /// it, on groundUnder; planted is how planted the animation has it (1 down, 0 lifted); maxDistance is how far
        /// across the ground the placed limb may get from the lock before it lets go.
        /// </summary>
        public void Update(Vector3 placed, Vector3 lockAt, Transform groundUnder, float planted, float maxDistance,
            float blendTime, float deltaTime)
        {
            if (locked)
            {
                Vector3 held = Position;
                var apart = new Vector2(held.x - placed.x, held.z - placed.z);
                if (planted < 0.5f || apart.magnitude > maxDistance) { locked = false; }
            }
            else if (planted >= 0.999f && weight <= 0f)
            {
                locked = true;
                ground = groundUnder;
                point = ground != null ? ground.InverseTransformPoint(lockAt) : lockAt;
            }
            weight = Mathf.MoveTowards(weight, locked ? 1f : 0f, deltaTime / blendTime);
        }

        /// <summary>Lets go at once (leaving the ground, say), unblended.</summary>
        public void Release()
        {
            locked = false;
            weight = 0f;
        }
    }
}
