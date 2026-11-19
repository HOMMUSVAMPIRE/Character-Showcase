using MeridianMotion.PhysicsQueries;
using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// An order for things the movement chooses between that doesn't depend on the order Unity gives them in (a query's
    /// hits come in no order, and the level's ropes in the order they were enabled), so a tie between two equally near
    /// hits (two coplanar floors, a corner of two walls) or two equally near ropes goes the same way on any machine, and
    /// however the scene was built. By position first (a hit's point, then its normal), then the collider's world bounds
    /// (where it is, then its size), then its name. Never by instance ID: those differ between machines and runs.
    /// </summary>
    public static class StableOrder
    {
        /// <summary>Compares two points: by x, then y, then z.</summary>
        public static int Compare(Vector3 a, Vector3 b)
        {
            int c = a.x.CompareTo(b.x);
            if (c != 0) { return c; }
            c = a.y.CompareTo(b.y);
            return c != 0 ? c : a.z.CompareTo(b.z);
        }

        /// <summary>Compares two hits: by point, then normal, then the collider hit (see Compare(Collider, Collider)).</summary>
        public static int Compare(in RaycastHit a, in RaycastHit b)
        {
            int c = Compare(a.point, b.point);
            if (c != 0) { return c; }
            c = Compare(a.normal, b.normal);
            return c != 0 ? c : Compare(a.collider, b.collider);
        }

        /// <summary>Compares two colliders: by their bounds' centre, then size, then name (allocating only to compare names, on an exact tie of the rest).</summary>
        public static int Compare(Collider a, Collider b)
        {
            if (a == b) { return 0; }
            if (a == null) { return -1; }
            if (b == null) { return 1; }
            Bounds ba = a.bounds, bb = b.bounds;
            int c = Compare(ba.center, bb.center);
            if (c != 0) { return c; }
            c = Compare(ba.size, bb.size);
            return c != 0 ? c : string.CompareOrdinal(a.name, b.name);
        }

        /// <summary>
        /// Whether a candidate hit at value (a distance, a height) should replace the chosen one at best: when it's better
        /// (less, or more if higherWins), or exactly as good and first in the stable order.
        /// </summary>
        public static bool Better(float value, float best, in RaycastHit candidate, in RaycastHit chosen, bool higherWins = false) =>
            higherWins ? value > best || (value == best && Compare(candidate, chosen) < 0)
                       : value < best || (value == best && Compare(candidate, chosen) < 0);

        /// <summary>Sorts the first count colliders of a buffer into the stable order, in place (a few, so by insertion). Allocates nothing.</summary>
        public static void Sort(ColliderBuffer colliders, int count)
        {
            for (int i = 1; i < count; i++)
            {
                Collider item = colliders[i];
                int j = i - 1;
                while (j >= 0 && Compare(colliders[j], item) > 0)
                {
                    colliders[j + 1] = colliders[j];
                    j--;
                }
                colliders[j + 1] = item;
            }
        }
    }
}
