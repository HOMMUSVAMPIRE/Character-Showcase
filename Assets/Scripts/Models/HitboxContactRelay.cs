using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// On the Rigidbody a model's hitboxes move with (the character's): Unity tells a Rigidbody's object of the collisions
    /// of the colliders under it, not those colliders' own objects, so what touches a solid hitbox (see
    /// HitboxResponse.Collide) is passed on from here to the hitbox it touched. Added by CharacterHitboxes as its model
    /// is put under the character. (What passes into a hitbox that's a trigger is told to the hitbox itself.)
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class HitboxContactRelay : MonoBehaviour
    {
        // What's touching a hitbox now, and which: a collision's end has no contacts to say which it was.
        private readonly Dictionary<Collider, BoneHitbox> touching = new();

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.contactCount == 0 || !collision.GetContact(0).thisCollider.TryGetComponent(out BoneHitbox hitbox)) { return; }
            touching[collision.collider] = hitbox;
            hitbox.Report(collision.collider, true);
        }

        private void OnCollisionExit(Collision collision)
        {
            if (touching.Remove(collision.collider, out BoneHitbox hitbox) && hitbox != null) { hitbox.Report(collision.collider, false); }
        }
    }
}
