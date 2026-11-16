using UnityEngine;

namespace Character
{
    // The character's body (see CharacterBody): its size (ApplyScale), its height while crouched (SetHeightFraction), and
    // whether it fits somewhere. Collision goes through it (see MovementController.Collision.cs).
    public partial class MovementController
    {
        private CharacterBody body;

        /// <summary>
        /// The character's body as collision sees it (see CharacterBody): every sweep, overlap and push out of the world
        /// goes through it. An upright capsule (CapsuleBody) of the character's CapsuleCollider.
        /// </summary>
        public CharacterBody Body => body ??= new CapsuleBody(this, capsule);

        private void RememberBaseCapsule() => Body.RememberAuthoredSize();

        /// <summary>
        /// Resizes the character's collision to the given size (see CharacterBody.Resize: a capsule's height scales with y,
        /// and its radius with the larger of x and z). Collision sweeps, ground checks and depenetration all use the body,
        /// so they follow. (A crouched character's body is lower too: see SetHeightFraction.) Doesn't check for room: see
        /// CanFit. Called by ACCharacterController.SetScale.
        /// </summary>
        public void ApplyScale(Vector3 scale)
        {
            Scale = scale;
            Body.Resize(scale, heightFraction);
        }

        // The share of its standing height the character's collider has: below 1 while crouched (see SetHeightFraction).
        private float heightFraction = 1f;

        /// <summary>The share of its standing height the character's collider has now (1 standing; less crouched).</summary>
        public float HeightFraction => heightFraction;

        /// <summary>
        /// Lowers or raises the top of the character's collider, keeping its feet where they are: to the given share of
        /// its standing height (crouching, say). Lowering always works; raising only where there's room above the
        /// character standing at feet (its physics position this step), and otherwise nothing changes and this returns
        /// false. The share stays through changes of size.
        /// </summary>
        public bool SetHeightFraction(float fraction, Vector3 feet)
        {
            fraction = Mathf.Clamp(fraction, 0.1f, 1f);
            if (Mathf.Abs(fraction - heightFraction) < 1e-5f) { return true; }
            if (fraction > heightFraction && !CanFitAt(feet, fraction)) { return false; }
            heightFraction = fraction;
            ApplyScale(Scale);
            return true;
        }

        /// <summary>The tallest step or ledge the character steps up by itself at its authored size (see StepHeight for its current size).</summary>
        public float MaxStepHeight => maxStepHeight;
        /// <summary>The steepest slope, in degrees, the character treats as walkable ground.</summary>
        public float MaxSlopeAngle => maxSlopeAngle;
        /// <summary>The body's radius (m) at the character's authored size (1, 1, 1), which its scale multiplies.</summary>
        public float BaseRadius => Body.AuthoredRadius;
        /// <summary>The body's height (m) at the character's authored size (1, 1, 1), which its scale multiplies.</summary>
        public float BaseHeight => Body.AuthoredHeight;

        /// <summary>
        /// Changes the capsule's size at the character's authored size (its scale then multiplies it), standing on the
        /// character's feet (see CapsuleBody.SetAuthoredSize). Used to fit the collider to the character's model (see
        /// CharacterModelHost). Like growing, it waits for room: if the bigger capsule wouldn't fit where the character
        /// stands, nothing changes and this returns false. False for a body that isn't a capsule.
        /// </summary>
        public bool SetBaseCapsule(float radius, float height)
        {
            if (Body is not CapsuleBody capsuleBody
                || !capsuleBody.SetAuthoredSize(radius, height, rb != null ? rb.position : transform.position, Scale, heightFraction))
            {
                return false;
            }
            ApplyScale(Scale);
            return true;
        }

        /// <summary>
        /// Whether the character would fit where it stands at the given size, without overlapping the world (more than
        /// the skin width, so the floor it stands on doesn't count).
        /// </summary>
        public bool CanFit(Vector3 scale) => Body.Fits(rb != null ? rb.position : transform.position, scale, heightFraction);

        /// <summary>
        /// Whether the character would fit with its feet at the given position, at its size and the given share of its
        /// standing height (1 standing; see SetHeightFraction), without overlapping the world.
        /// </summary>
        public bool CanFitAt(Vector3 feet, float fraction = 1f) => Body.Fits(feet, Scale, Mathf.Clamp(fraction, 0.1f, 1f));
    }
}
