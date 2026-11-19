using UnityEngine;

namespace MeridianMotion.PhysicsQueries
{
    /// <summary>
    /// A reused buffer for Unity's non-allocating physics queries (RaycastNonAlloc and the like) that grows when a query
    /// fills it. Unity gives a query's results in no order, so a full buffer may have left out the nearest hit: the
    /// buffer is then doubled (up to MaxSize) and the query run again, so its caller always sees every result. That
    /// allocates only in the step a buffer grows, and once grown it stays grown. Growing, or still being full at
    /// MaxSize, is reported in development builds (and the editor): needing more results than that is a sign of a design
    /// problem (too many colliders in reach, or a query reaching further than it needs), not of a buffer too small.
    /// Its own assembly, referencing nothing, so the camera and the character can both use it.
    /// </summary>
    public abstract class QueryBuffer<T>
    {
        /// <summary>The most results a buffer grows to hold.</summary>
        public const int MaxSize = 128;

        private readonly string name;
        private bool reportedFullAtMax;
        private T[] items;

        protected QueryBuffer(int size, string name)
        {
            items = new T[Mathf.Clamp(size, 1, MaxSize)];
            this.name = name;
        }

        /// <summary>How many results the buffer holds now (it grows when a query fills it).</summary>
        public int Capacity => items.Length;

        /// <summary>A result of the last query (0 up to the count it returned); writable, to compact results in place.</summary>
        public ref T this[int index] => ref items[index];

        /// <summary>The array queries fill now: pass it to a query, then call Retry with what it returned.</summary>
        protected T[] Items => items;

        /// <summary>
        /// Whether a query that found count results must be run again: it filled the buffer, which has grown to hold
        /// more. At MaxSize it doesn't grow, and the results are kept as they are (reported once).
        /// </summary>
        protected bool Retry(int count)
        {
            if (count < items.Length) { return false; }
            if (items.Length >= MaxSize)
            {
                if (Debug.isDebugBuild && !reportedFullAtMax)
                {
                    reportedFullAtMax = true;
                    Debug.LogWarning($"[{name}] A query filled its buffer at its largest ({MaxSize} results): the nearest may have been " +
                                     "left out. Too many colliders are in its reach, or it reaches further than it needs to.");
                }
                return false;
            }
            int grown = Mathf.Min(items.Length * 2, MaxSize);
            if (Debug.isDebugBuild)
            {
                Debug.LogWarning($"[{name}] A query filled its buffer ({items.Length} results), so it may have left out the nearest: " +
                                 $"grown to {grown}, and run again.");
            }
            items = new T[grown];
            return true;
        }
    }

    /// <summary>A growing buffer of hits, for rays and shape casts (see QueryBuffer).</summary>
    public sealed class RaycastHitBuffer : QueryBuffer<RaycastHit>
    {
        /// <param name="size">How many hits it holds to start with.</param>
        /// <param name="name">What it's called in a report that it filled (its owner's type and field, say).</param>
        public RaycastHitBuffer(int size, string name) : base(size, name) { }

        public int Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction triggers)
        {
            int count;
            do { count = Physics.RaycastNonAlloc(origin, direction, Items, maxDistance, layerMask, triggers); } while (Retry(count));
            return count;
        }

        public int SphereCast(Vector3 origin, float radius, Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction triggers)
        {
            int count;
            do { count = Physics.SphereCastNonAlloc(origin, radius, direction, Items, maxDistance, layerMask, triggers); } while (Retry(count));
            return count;
        }

        public int BoxCast(Vector3 center, Vector3 halfExtents, Vector3 direction, Quaternion orientation, float maxDistance, int layerMask,
            QueryTriggerInteraction triggers)
        {
            int count;
            do { count = Physics.BoxCastNonAlloc(center, halfExtents, direction, Items, orientation, maxDistance, layerMask, triggers); }
            while (Retry(count));
            return count;
        }

        public int CapsuleCast(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float maxDistance, int layerMask,
            QueryTriggerInteraction triggers)
        {
            int count;
            do { count = Physics.CapsuleCastNonAlloc(point1, point2, radius, direction, Items, maxDistance, layerMask, triggers); }
            while (Retry(count));
            return count;
        }
    }

    /// <summary>A growing buffer of colliders, for overlap queries (see QueryBuffer).</summary>
    public sealed class ColliderBuffer : QueryBuffer<Collider>
    {
        /// <param name="size">How many colliders it holds to start with.</param>
        /// <param name="name">What it's called in a report that it filled (its owner's type and field, say).</param>
        public ColliderBuffer(int size, string name) : base(size, name) { }

        public int OverlapSphere(Vector3 position, float radius, int layerMask, QueryTriggerInteraction triggers)
        {
            int count;
            do { count = Physics.OverlapSphereNonAlloc(position, radius, Items, layerMask, triggers); } while (Retry(count));
            return count;
        }

        public int OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, int layerMask, QueryTriggerInteraction triggers)
        {
            int count;
            do { count = Physics.OverlapBoxNonAlloc(center, halfExtents, Items, orientation, layerMask, triggers); } while (Retry(count));
            return count;
        }

        public int OverlapCapsule(Vector3 point0, Vector3 point1, float radius, int layerMask, QueryTriggerInteraction triggers)
        {
            int count;
            do { count = Physics.OverlapCapsuleNonAlloc(point0, point1, radius, Items, layerMask, triggers); } while (Retry(count));
            return count;
        }
    }
}
