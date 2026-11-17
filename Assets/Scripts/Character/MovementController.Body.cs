using UnityEngine;

namespace Character
{
    // The character's body (see CharacterBody): its size (ApplyScale), its posture (SetPosture), and whether it fits
    // somewhere. Collision goes through it (see MovementController.Collision.cs).
    public partial class MovementController
    {
        private CharacterBody body;

        /// <summary>
        /// The character's body as collision sees it (see CharacterBody): every sweep, overlap and push out of the world
        /// goes through it. One of Unity's primitive colliders (PrimitiveBody): the character's Body Collider.
        /// </summary>
        public CharacterBody Body => body ??= new PrimitiveBody(this, bodyCollider);

        private void RememberBaseSize() => Body.RememberAuthoredSize();

        /// <summary>
        /// After the character's collider has been replaced (a capsule for a box, say): makes its body again, from the
        /// first primitive collider on it, as authored, in the posture it has.
        /// </summary>
        public void RebuildBody(Collider primitive = null)
        {
            bodyCollider = primitive;
            body = null;
            RememberBaseSize();
            ApplyScale(Scale);
        }

        /// <summary>
        /// Resizes the character's collision to the given size (see PrimitiveBody: a capsule's height scales with y, and
        /// its radius with the larger of x and z). Collision sweeps, ground checks and depenetration all use the body, so
        /// they follow. (Its posture shapes it too: see SetPosture.) Doesn't check for room: see CanFit. Called by
        /// ACCharacterController.SetScale.
        /// </summary>
        public void ApplyScale(Vector3 scale)
        {
            Scale = scale;
            Body.Resize(scale, posture);
        }

        // How the character's body is held: standing, low (crouched), lying (swimming underwater). See SetPosture.
        private BodyPosture posture = BodyPosture.Standing;

        /// <summary>How the character's body is held now (standing; low while crouched; lying underwater).</summary>
        public BodyPosture Posture => posture;

        /// <summary>
        /// Changes how the character's body is held (see BodyPosture), keeping its feet where they are. Lowering it (from
        /// standing, or lower still) always works; anything else only where the body fits that way with its feet at feet
        /// (its physics position this step), and otherwise nothing changes and this returns false. The posture holds
        /// through changes of size.
        /// </summary>
        public bool SetPosture(BodyPosture newPosture, Vector3 feet)
        {
            if (newPosture.Equals(posture)) { return true; }
            if (!newPosture.IsLowerThan(posture) && !CanFitAt(feet, newPosture)) { return false; }
            posture = newPosture;
            ApplyScale(Scale);
            return true;
        }

        /// <summary>The tallest step or ledge the character steps up by itself at its authored size (see StepHeight for its current size).</summary>
        public float MaxStepHeight => maxStepHeight;
        /// <summary>The steepest slope, in degrees, the character treats as walkable ground.</summary>
        public float MaxSlopeAngle => maxSlopeAngle;
        /// <summary>The radius (m) of the body's footprint at the character's authored size (1, 1, 1), which its scale multiplies.</summary>
        public float BaseRadius => Body.AuthoredRadius;
        /// <summary>The body's height (m) at the character's authored size (1, 1, 1), which its scale multiplies.</summary>
        public float BaseHeight => Body.AuthoredHeight;

        /// <summary>
        /// Changes the body's size at the character's authored size (its scale then multiplies it), standing on the
        /// character's feet (see PrimitiveBody.SetAuthoredSize): its footprint's radius and its height. Used to fit the
        /// collider to the character's model (see CharacterModelHost). Like growing, it waits for room: if the bigger body
        /// wouldn't fit where the character stands, nothing changes and this returns false. False for a body that can't
        /// be refitted so (a capsule lying as authored, or one that isn't a primitive).
        /// </summary>
        public bool SetBaseSize(float radius, float height)
        {
            if (Body is not PrimitiveBody primitive
                || !primitive.SetAuthoredSize(radius, height, PhysicsPosition, BodyRotation, Scale, posture))
            {
                return false;
            }
            ApplyScale(Scale);
            return true;
        }

        /// <summary>
        /// Whether the character would fit where it stands at the given size, in its posture, without overlapping the
        /// world (more than the skin width, so the floor it stands on doesn't count).
        /// </summary>
        public bool CanFit(Vector3 scale) => Body.Fits(PhysicsPosition, BodyRotation, scale, posture);

        /// <summary>Whether the character would fit standing with its feet at the given position, at its size, without overlapping the world.</summary>
        public bool CanFitAt(Vector3 feet) => CanFitAt(feet, BodyPosture.Standing);

        /// <summary>
        /// Whether the character would fit with its feet at the given position, at its size, held the given way (see
        /// BodyPosture) and turned as it is, without overlapping the world.
        /// </summary>
        public bool CanFitAt(Vector3 feet, BodyPosture held) => Body.Fits(feet, BodyRotation, Scale, held);

        // The character's physics pose (the Rigidbody's, which the transform lags behind while it's interpolated).
        private Vector3 PhysicsPosition => rb != null ? rb.position : transform.position;

        // Set while collision resolves a step: the way the body faces as it moves (the step's turn, as far as it has room),
        // and how far it's pitched.
        private Quaternion? resolvingRotation;
        private float? resolvingPitch;

        /// <summary>
        /// The way the body is turned for queries: its facing (as the step turns it while collision resolves it, else the
        /// physics pose's) pitched by its pitch (see BodyPitch).
        /// </summary>
        internal Quaternion BodyRotation => Attitude(resolvingRotation ?? (rb != null ? rb.rotation : transform.rotation), resolvingPitch ?? bodyPitch);

        // How far (degrees, positive nose up) the body is pitched: see BodyPitch.
        private float bodyPitch;

        /// <summary>
        /// How far (degrees, positive nose up) the character's body is pitched about its feet, from level: tilted along the
        /// way it moves, swimming underwater (see MovementData.targetPitch). Only the body pitches: the character itself
        /// (its Rigidbody and transform) only ever turns about the vertical, so its facing, its ground probe and gravity
        /// stay as they are. The model is tilted to match (see AnimationController).
        /// </summary>
        public float BodyPitch => bodyPitch;

        /// <summary>The way a body facing facing (a turn about the vertical) is turned, pitched pitch degrees nose up about its feet.</summary>
        public static Quaternion Attitude(Quaternion facing, float pitch) =>
            pitch == 0f ? facing : facing * Quaternion.AngleAxis(-pitch, Vector3.right);
    }
}
