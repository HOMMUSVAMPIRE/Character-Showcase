using UnityEngine;

namespace Level
{
    /// <summary>
    /// A large box a character can take hold of and push or pull along the ground (see BasicMovementMode's Pushing &
    /// Pulling settings). It's a kinematic, interpolated Rigidbody, so it's moved only by whoever holds it and by its own
    /// simple gravity (pushed off an edge, it falls), never shoved around by the physics solver. Its moves are swept
    /// against the world first, so it stops at walls instead of passing through them. Its collision is its BoxCollider.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    [DisallowMultipleComponent]
    public class PushableObject : MonoBehaviour
    {
        [Tooltip("How fast it moves when pushed or pulled, as a share of the character's push and pull speed: lower for " +
                 "heavier objects.")]
        [Range(0.05f, 1f)] [SerializeField] private float speedMultiplier = 1f;
        [Tooltip("Whether it can be pulled as well as pushed.")]
        [SerializeField] private bool canBePulled = true;
        [Tooltip("How quickly (m/s²) it falls when there's nothing under it.")]
        [Min(0f)] [SerializeField] private float gravity = 20f;
        [Tooltip("The fastest (m/s) it falls.")]
        [Min(0f)] [SerializeField] private float maxFallSpeed = 30f;
        [Tooltip("The physics layers it collides with when moved.")]
        [SerializeField] private LayerMask collisionMask = ~0;

        // A small gap kept between the box and what it's moved against, and how far below it the ground can be while
        // still holding it up.
        private const float SkinWidth = 0.01f;
        private const float SupportDistance = 0.05f;

        private Rigidbody body;
        private BoxCollider box;
        // Its physics position: Rigidbody.position lags a MovePosition until the physics step runs it, and a character
        // may move this box and then ask about it within one step.
        private Vector3 position;
        private float fallSpeed;
        // When it last moved itself (Time.fixedTime), to know when its Rigidbody has caught up (see FixedUpdate).
        private float lastMoveTime = float.NegativeInfinity;
        private Transform holder;
        private readonly RaycastHit[] hits = new RaycastHit[16];

        /// <summary>Its push and pull speed, as a share of the character's.</summary>
        public float SpeedMultiplier { get => speedMultiplier; set => speedMultiplier = Mathf.Clamp(value, 0.05f, 1f); }
        /// <summary>Whether it can be pulled as well as pushed.</summary>
        public bool CanBePulled { get => canBePulled; set => canBePulled = value; }
        /// <summary>Whether something is holding it (it doesn't fall then: whoever holds it moves it).</summary>
        public bool IsHeld => holder != null;
        /// <summary>Its physics position, including any move made this step.</summary>
        public Vector3 Position => position;
        /// <summary>Its rotation (it doesn't turn).</summary>
        public Quaternion Rotation => body != null ? body.rotation : transform.rotation;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            box = GetComponent<BoxCollider>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            position = body.position;
        }

        /// <summary>Puts the box somewhere else at once (setting its transform's position wouldn't reach its physics pose).</summary>
        public void Teleport(Vector3 newPosition)
        {
            position = newPosition;
            body.position = newPosition;
            transform.position = newPosition;
            fallSpeed = 0f;
        }

        /// <summary>Takes hold of the box (holder: whoever holds it, which its moves ignore), or lets go of it (null).</summary>
        public void SetHolder(Transform newHolder)
        {
            holder = newHolder;
            fallSpeed = 0f;
        }

        /// <summary>
        /// How far the box can move (up to distance) in a direction before it would hit something, other than itself
        /// and whoever holds it.
        /// </summary>
        public float FreeDistance(Vector3 direction, float distance)
        {
            if (distance <= 0f || direction.sqrMagnitude < 1e-8f) { return 0f; }
            GetCastBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation);
            int count = Physics.BoxCastNonAlloc(center, halfExtents, direction.normalized, hits, rotation,
                distance + SkinWidth, collisionMask, QueryTriggerInteraction.Ignore);
            float free = distance;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                Transform other = hit.collider.transform;
                if (other.IsChildOf(transform) || (holder != null && other.IsChildOf(holder))) { continue; }
                // Already overlapping (distance 0): skipped, as the character's sweeps do, so it can move clear instead of locking up.
                if (hit.distance <= 0f) { continue; }
                free = Mathf.Min(free, Mathf.Max(0f, hit.distance - SkinWidth));
            }
            return free;
        }

        /// <summary>Moves the box by delta as far as it can go without hitting anything, returning how far it moved.</summary>
        public Vector3 Move(Vector3 delta)
        {
            float distance = delta.magnitude;
            if (distance < 1e-6f) { return Vector3.zero; }
            Vector3 moved = delta * (FreeDistance(delta, distance) / distance);
            position += moved;
            body.MovePosition(position);
            lastMoveTime = Time.fixedTime;
            return moved;
        }

        /// <summary>Whether there's ground under the box to hold it up.</summary>
        public bool IsSupported => FreeDistance(Vector3.down, SupportDistance) < SupportDistance - 1e-4f;

        private void FixedUpdate()
        {
            // Something else may have moved it (by its transform, say). Only once no move of its own is still on its
            // way: a MovePosition reaches Rigidbody.position at the physics step after it's made, which may be after
            // this one, so a move made in the last step may not have arrived yet.
            if (!IsHeld && lastMoveTime < Time.fixedTime - 1.5f * Time.fixedDeltaTime) { position = body.position; }
            if (IsHeld || gravity <= 0f) { return; }
            if (IsSupported)
            {
                fallSpeed = 0f;
                return;
            }
            fallSpeed = Mathf.Min(fallSpeed + gravity * Time.fixedDeltaTime, maxFallSpeed);
            Vector3 fall = Vector3.down * (fallSpeed * Time.fixedDeltaTime);
            if (Move(fall).sqrMagnitude < fall.sqrMagnitude * 0.99f) { fallSpeed = 0f; } // Landed.
        }

        /// <summary>The box's collider, at its physics position, shrunk by the skin width, for sweeps.</summary>
        private void GetCastBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation)
        {
            rotation = Rotation;
            Vector3 scale = transform.lossyScale;
            center = position + rotation * Vector3.Scale(box.center, scale);
            halfExtents = Vector3.Scale(box.size, scale) * 0.5f;
            halfExtents = new Vector3(Mathf.Abs(halfExtents.x), Mathf.Abs(halfExtents.y), Mathf.Abs(halfExtents.z)) - Vector3.one * SkinWidth;
            halfExtents = Vector3.Max(halfExtents, Vector3.one * SkinWidth);
        }
    }
}
