using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// One of a model's hitboxes (see CharacterHitboxes): a capsule collider on one of its bones, covering that part of its
    /// body (an upper arm, the head, the chest), moving with the bone as the model is animated. It passes what meets it on
    /// to its CharacterHitboxes: what passes into it as a trigger here, and what touches it as a solid through the
    /// character's HitboxContactRelay (Unity tells a Rigidbody's object of its colliders' collisions, not theirs). The
    /// movement never uses it (see CharacterBody).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleCollider))]
    // Added by fitting hitboxes (HitboxFitter), not by hand: hidden from Add Component.
    [AddComponentMenu("")]
    public sealed class BoneHitbox : MonoBehaviour
    {
        private CapsuleCollider capsule;

        /// <summary>The model's hitboxes this is one of (set as they wake).</summary>
        public CharacterHitboxes Owner { get; internal set; }

        /// <summary>The hitbox's collider.</summary>
        public CapsuleCollider Collider => capsule != null ? capsule : capsule = GetComponent<CapsuleCollider>();

        /// <summary>The bone the hitbox is on (the part of the body it covers).</summary>
        public Transform Bone => transform.parent;

        // Detecting (a trigger), what passes into it. (Colliding, what touches it comes through HitboxContactRelay.)
        private void OnTriggerEnter(Collider other) => Report(other, true);
        private void OnTriggerExit(Collider other) => Report(other, false);

        /// <summary>Passes on what started or stopped meeting the hitbox to its owner.</summary>
        internal void Report(Collider other, bool started)
        {
            if (Owner != null) { Owner.Report(this, other, started); }
        }
    }
}
