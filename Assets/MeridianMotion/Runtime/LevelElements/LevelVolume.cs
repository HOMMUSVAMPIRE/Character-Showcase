using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.Levels
{
    /// <summary>
    /// A box of the level that notices a character's feet in it: the base of checkpoints, hazards, shortcut triggers and
    /// lesson zones. It has no collider: the character has none of its own to meet a trigger (only its model's hitboxes,
    /// and none without a model), and a trigger's callbacks come at the physics step, not the character's. Instead, after
    /// each of a character's steps, its LevelPresence asks which volumes hold its feet (from its physics pose) and tells
    /// those it entered and left (Entered, Exited), so what a volume does to a character happens between its steps, the
    /// same in a replay and with or without a model. The box is Center and Size in the object's own space (its rotation and
    /// scale apply).
    /// </summary>
    public abstract class LevelVolume : MonoBehaviour
    {
        [Tooltip("The middle of the box, in the object's own space (m).")]
        [SerializeField] private Vector3 center = new(0f, 1f, 0f);
        [Tooltip("The size of the box, in the object's own space (m).")]
        [SerializeField] private Vector3 size = new(2f, 2f, 2f);

        private static readonly List<LevelVolume> active = new();
        private ulong stableId;

        /// <summary>Every enabled volume, in StableId order (the same on every machine that loaded the level).</summary>
        public static IReadOnlyList<LevelVolume> All => active;

        /// <summary>The middle of the box, in the object's own space.</summary>
        public Vector3 Center { get => center; set => center = value; }
        /// <summary>The size of the box, in the object's own space.</summary>
        public Vector3 Size { get => size; set => size = new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z)); }

        /// <summary>Whether a point (in the world) is in the box.</summary>
        public bool Contains(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point) - center;
            Vector3 half = size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        /// <summary>The first enabled volume of type T holding the point (in StableId order), or null.</summary>
        public static T At<T>(Vector3 point) where T : LevelVolume
        {
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i] is T volume && volume.Contains(point)) { return volume; }
            }
            return null;
        }

        /// <summary>A character's feet came into the box (after its step).</summary>
        public virtual void Entered(IRespawnable who, LevelPresence presence) { }

        /// <summary>A character's feet left the box (after its step), or the box was turned off with them in it.</summary>
        public virtual void Exited(IRespawnable who, LevelPresence presence) { }

        protected virtual void OnEnable()
        {
            stableId = StableId.Of(transform);
            int at = 0;
            while (at < active.Count && active[at].stableId <= stableId) { at++; }
            active.Insert(at, this);
        }

        protected virtual void OnDisable() => active.Remove(this);

        /// <summary>The colour its box is drawn in, in the editor.</summary>
        protected virtual Color GizmoColor => new(0.3f, 0.8f, 1f);

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Color color = GizmoColor;
            Gizmos.color = new Color(color.r, color.g, color.b, 0.08f);
            Gizmos.DrawCube(center, size);
            Gizmos.color = color;
            Gizmos.DrawWireCube(center, size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
