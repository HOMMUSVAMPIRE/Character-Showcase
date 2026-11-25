using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.Showcase.Grass
{
    /// <summary>
    /// Something that pushes the grass aside as it goes through it: a character, say (Set Up Scene gives the showcase's
    /// characters one, through GrassBenders). It pushes with its legs: the capsule colliders under it on the Hitbox Layer
    /// (a character's hitboxes, on its model's bones, moving as it's animated) whose middles are in the lowest Leg Share of
    /// its height, its lower legs and feet, however many legs it has. The grass near each bows out from it, and partly
    /// along the way it's going, while it's down among the blades, and springs back once it's gone (GrassBending). With
    /// no such capsules (a character without a model, or before its model's loaded) it pushes with a circle at its feet
    /// (its transform's position), Radius wide. Only seen: the grass has no collider, and the movement never reads it.
    /// </summary>
    [AddComponentMenu("Meridian Motion/Showcase/Grass Bender")]
    public sealed class GrassBender : MonoBehaviour
    {
        [Tooltip("Without leg capsules (see Hitbox Layer): how far from its feet (m) it pushes the grass aside.")]
        [Min(0.05f)] public float radius = 0.35f;
        [Tooltip("The layer of the capsules it pushes with: its hitboxes' (a character's, on its model's bones). It looks for them under it as its model loads.")]
        public string hitboxLayer = "CharacterHitbox";
        [Tooltip("Which capsules push: those whose middles are in this lowest share of its height (its lower legs and feet).")]
        [Range(0.05f, 1f)] public float legShare = 0.25f;

        /// <summary>The benders enabled, in the order they were.</summary>
        public static IReadOnlyList<GrassBender> All => all;

        private static readonly List<GrassBender> all = new();

        /// <summary>How often (s) it looks again for its legs: models load, and are swapped, after it's added.</summary>
        private const float LookAgainEvery = 0.5f;

        private readonly List<CapsuleCollider> legs = new();
        private readonly List<(Vector3 a, Vector3 b)> lastEnds = new();
        private float lookedAt = float.NegativeInfinity;
        private int lastFrame = -1;
        private Vector3 lastFeet;

        /// <summary>The capsules it pushes with now (empty: the circle at its feet).</summary>
        public IReadOnlyList<CapsuleCollider> Legs => legs;

        private void OnEnable()
        {
            all.Add(this);
            lookedAt = float.NegativeInfinity;
        }

        private void OnDisable() => all.Remove(this);

        /// <summary>
        /// Adds what it pushes with this frame to pushers, as GrassBending reads them (three each: one end and its radius,
        /// the other end and how far its bottom is below its line, its velocity in x and z), at most most of them in all; returns how many it added.
        /// </summary>
        internal int AddPushers(Vector4[] pushers, int count, int most)
        {
            float now = Time.realtimeSinceStartup;
            if (now - lookedAt > LookAgainEvery || legs.Exists(l => l == null)) { FindLegs(); lookedAt = now; }
            float dt = Time.deltaTime;
            bool fresh = lastFrame != Time.frameCount - 1;
            lastFrame = Time.frameCount;
            int added = 0;
            if (legs.Count == 0)
            {
                Vector3 feet = transform.position;
                Vector3 velocity = fresh || dt <= 0f ? Vector3.zero : (feet - lastFeet) / dt;
                lastFeet = feet;
                if (count + added >= most) { return 0; }
                Write(pushers, count, feet, feet, radius, 0f, velocity);
                return 1;
            }
            while (lastEnds.Count < legs.Count) { lastEnds.Add(default); }
            for (int i = 0; i < legs.Count && count + added < most; i++)
            {
                (Vector3 a, Vector3 b, float r) = Ends(legs[i]);
                Vector3 velocity = fresh || dt <= 0f ? Vector3.zero : ((a + b) - (lastEnds[i].a + lastEnds[i].b)) / (2f * dt);
                lastEnds[i] = (a, b);
                Write(pushers, count + added, a, b, r, r, velocity);
                added++;
            }
            return added;
        }

        /// <summary>A pusher: its ends, its radius across, and how far its bottom is below its line (a capsule's radius; 0 for the feet's circle, whose bottom is the feet).</summary>
        private static void Write(Vector4[] pushers, int index, Vector3 a, Vector3 b, float r, float below, Vector3 velocity)
        {
            pushers[3 * index] = new Vector4(a.x, a.y, a.z, r);
            pushers[3 * index + 1] = new Vector4(b.x, b.y, b.z, below);
            pushers[3 * index + 2] = new Vector4(velocity.x, velocity.z, 0f, 0f);
        }

        /// <summary>A capsule's two ends (the middles of its round caps) and its radius, in the world.</summary>
        private static (Vector3 a, Vector3 b, float r) Ends(CapsuleCollider capsule)
        {
            Transform t = capsule.transform;
            Vector3 scale = t.lossyScale;
            Vector3 axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
            float across = capsule.direction == 0 ? Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))
                : capsule.direction == 1 ? Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) : Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            float r = capsule.radius * across;
            Vector3 middle = t.TransformPoint(capsule.center);
            Vector3 worldAxis = t.TransformVector(axis);
            float half = Mathf.Max(0f, worldAxis.magnitude * capsule.height * 0.5f - r);
            Vector3 along = worldAxis.normalized * half;
            return (middle - along, middle + along, r);
        }

        /// <summary>Its legs: the capsules under it on the hitbox layer whose middles are in the lowest Leg Share of them all.</summary>
        private void FindLegs()
        {
            legs.Clear();
            lastEnds.Clear();
            int layer = LayerMask.NameToLayer(hitboxLayer);
            if (layer < 0) { return; }
            // From each capsule's own ends, not its bounds: a collider's bounds aren't brought up to date until the physics
            // next syncs, so one just added (a model just loaded) would read as wherever it was.
            float bottom = float.MaxValue, top = float.MinValue;
            var found = new List<(CapsuleCollider capsule, float middle)>();
            foreach (CapsuleCollider capsule in GetComponentsInChildren<CapsuleCollider>())
            {
                if (capsule.gameObject.layer != layer || !capsule.enabled) { continue; }
                (Vector3 a, Vector3 b, float r) = Ends(capsule);
                found.Add((capsule, (a.y + b.y) / 2f));
                bottom = Mathf.Min(bottom, Mathf.Min(a.y, b.y) - r);
                top = Mathf.Max(top, Mathf.Max(a.y, b.y) + r);
            }
            if (found.Count == 0) { return; }
            float lowest = bottom + (top - bottom) * legShare;
            foreach ((CapsuleCollider capsule, float middle) in found)
            {
                if (middle <= lowest) { legs.Add(capsule); }
            }
        }
    }
}
