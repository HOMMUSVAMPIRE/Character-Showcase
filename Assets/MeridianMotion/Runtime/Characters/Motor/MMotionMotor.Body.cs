using UnityEngine;

namespace MeridianMotion.Characters
{
    // The character's body (see CharacterBody): its size (ApplyScale), its posture (SetPosture), and whether it fits
    // somewhere. Collision goes through it (see MMotionMotor.Collision.cs).
    public partial class MMotionMotor
    {
        private CharacterBody body;

        /// <summary>
        /// The character's body as the movement sees it (see CharacterBody): every sweep, overlap and push out of the world
        /// goes through it. Shaped as one of Unity's primitive colliders (PrimitiveBody), keeping the character's Body
        /// Collider (if it has one) in its shape. The movement doesn't need the collider.
        /// </summary>
        public CharacterBody Body => body ??= new PrimitiveBody(this, bodyCollider);

        // A collider of each of the body's parts' shapes on a hidden object of the character's, turned off but while it's
        // used, so nothing meets it: pushing the body out of the world (Physics.ComputePenetration) needs a collider, turned
        // on, and the character may have none. One for each part, so a body of parts of several kinds doesn't swap them.
        private Collider[] queryColliders = new Collider[1];
        private GameObject queryHolder;

        /// <summary>
        /// A collider shaped as the given body shape, at the character's origin, turned off: for the queries that need a
        /// collider rather than a shape (see PrimitiveBody.Depenetrate), which turn it on only while they use it
        /// (Physics.ComputePenetration ignores a collider that's turned off), so nothing else meets it. Made the first
        /// time it's needed (and again for a shape of another kind).
        /// </summary>
        internal Collider QueryCollider(BodyShape shape, int part = 0)
        {
            if (part >= queryColliders.Length) { System.Array.Resize(ref queryColliders, part + 1); }
            Collider query = queryColliders[part];
            if (!shape.IsKindOf(query))
            {
                if (query != null)
                {
                    if (Application.isPlaying) { Destroy(query); } else { DestroyImmediate(query); }
                }
                if (queryHolder == null)
                {
                    queryHolder = new GameObject("Body Query") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
                    queryHolder.transform.SetParent(transform, false);
                }
                query = (Collider)queryHolder.AddComponent(shape.ColliderType);
                query.enabled = false;
                if (query is CapsuleCollider capsule) { capsule.direction = shape.direction; }
                queryColliders[part] = query;
            }
            PrimitiveBody.Write(query, shape);
            return query;
        }

        private void RememberBaseSize() => Body.RememberAuthoredSize();

        /// <summary>
        /// After the character's collider has been replaced (a capsule for a box, say): makes its body again, shaped as the
        /// given collider (or the first primitive collider on it) as authored, in the posture it has.
        /// </summary>
        public void RebuildBody(Collider primitive = null)
        {
            // Its extra parts (see BodyDefinition's Extra Parts) stay its parts.
            BodyShape[] parts = body is PrimitiveBody had ? System.Linq.Enumerable.ToArray(had.AuthoredExtraParts) : null;
            bodyCollider = primitive;
            body = null;
            if (parts != null && parts.Length > 0) { ((PrimitiveBody)Body).SetExtraParts(parts); }
            // Its own shape from now on, not the definition's.
            shapedFromDefinition = true;
            RememberBaseSize();
            ApplyScale(Scale);
        }

        /// <summary>
        /// Resizes the character's body (and its collider, if any) to the given size (see PrimitiveBody: a capsule's height
        /// scales with y, and its radius with the larger of x and z). Collision sweeps, ground checks and depenetration all
        /// use the body, so they follow. (Its posture shapes it too: see SetPosture.) Doesn't check for room: see CanFit.
        /// Called by MMotionCharacter.SetScale.
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

        /// <summary>
        /// The character's body as authored, at its authored size (1, 1, 1), standing (see BodyShape): its Body
        /// Definition's (before the character wakes, for a prefab asked about), or the shape it's been given since (see
        /// SetBody), or its Body Collider's as authored (or, without one, an upright capsule 2 m tall).
        /// </summary>
        public BodyShape AuthoredBody =>
            bodyDefinition != null && !shapedFromDefinition ? bodyDefinition.Shape
            : Body is PrimitiveBody primitive ? primitive.AuthoredShape
            : BodyShape.Capsule(Body.AuthoredRadius, Body.AuthoredHeight);

        /// <summary>The character's Body Definition: what its body is shaped from as it wakes (null: its Body Collider as authored).</summary>
        public BodyDefinition BodyDefinition => bodyDefinition;

        // Whether the body has been shaped from the Body Definition (as the character woke), so the body's own shape is
        // the authored one from then on.
        private bool shapedFromDefinition;

        /// <summary>
        /// Shapes the body from the Body Definition, if there is one (as the character wakes, without waiting for room:
        /// it's where it's been put), otherwise remembers the Body Collider's shape as authored.
        /// </summary>
        private void ShapeFromDefinition()
        {
            if (bodyDefinition != null && !shapedFromDefinition && SetBody(bodyDefinition.Shape, checkRoom: false, bodyDefinition.ExtraParts)) { shapedFromDefinition = true; }
            else { RememberBaseSize(); }
        }

        /// <summary>
        /// Gives the character another body as authored (see BodyShape; its scale then multiplies it), its feet staying
        /// where they are: the body a model gives it (see CharacterModelSlot), say. A body of another kind than the
        /// character's collider (if it has one) replaces the collider with one of its kind (keeping its material, layers and
        /// settings), so it keeps following the body. Like growing,
        /// it waits for room: unless checkRoom is false, if the new body would reach somewhere the current one doesn't and
        /// wouldn't fit there, nothing changes and this returns false. False for a body without a size, or a character
        /// whose body isn't a primitive. Given extra parts (see BodyDefinition's Extra Parts: none makes it one shape),
        /// they're its parts beside the shape from then on (and are part of the room it needs); not given, it keeps the
        /// ones it has.
        /// </summary>
        public bool SetBody(BodyShape shape, bool checkRoom = true, System.Collections.Generic.IReadOnlyList<BodyShape> extraParts = null)
        {
            if (!shape.HasSize || Body is not PrimitiveBody primitive) { return false; }
            Vector3 feet = PhysicsPosition;
            Quaternion rotation = BodyRotation;
            BodyShape[] hadParts = System.Linq.Enumerable.ToArray(primitive.AuthoredExtraParts);
            if (extraParts != null) { primitive.SetExtraParts(extraParts); }
            if (primitive.Collider != null && !shape.IsKindOf(primitive.Collider))
            {
                if (checkRoom && !primitive.FitsAs(shape, feet, rotation, Scale, posture))
                {
                    primitive.SetExtraParts(hadParts);
                    return false;
                }
                var replaced = new PrimitiveBody(this, ReplaceCollider(primitive.Collider, shape));
                replaced.SetExtraParts(primitive.AuthoredExtraParts);
                primitive = replaced;
                body = primitive;
            }
            if (!primitive.SetAuthoredShape(shape, feet, rotation, Scale, posture, checkRoom))
            {
                primitive.SetExtraParts(hadParts);
                return false;
            }
            ApplyScale(Scale);
            return true;
        }

        /// <summary>A collider of the body's kind in place of the old one, with its settings; the old one is removed.</summary>
        private Collider ReplaceCollider(Collider old, BodyShape shape)
        {
            var replacement = (Collider)old.gameObject.AddComponent(shape.ColliderType);
            replacement.sharedMaterial = old.sharedMaterial;
            replacement.isTrigger = old.isTrigger;
            replacement.includeLayers = old.includeLayers;
            replacement.excludeLayers = old.excludeLayers;
            replacement.layerOverridePriority = old.layerOverridePriority;
            replacement.providesContacts = old.providesContacts;
            if (replacement is CapsuleCollider capsule) { capsule.direction = shape.direction; }
            // Off at once, as Destroy only removes it at the end of the frame.
            old.enabled = false;
            if (Application.isPlaying) { Destroy(old); } else { DestroyImmediate(old); }
            bodyCollider = replacement;
            return replacement;
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

        /// <summary>
        /// Where the character's feet are in the physics simulation: its Rigidbody's position, which the transform lags
        /// behind while it's interpolated. What anything deciding how to move it (an AI input source) should read.
        /// </summary>
        public Vector3 PhysicsPosition => rb != null ? rb.position : transform.position;

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
        /// way it moves, swimming underwater (see MovementStep.targetPitch). Only the body pitches: the character itself
        /// (its Rigidbody and transform) only ever turns about the vertical, so its facing, its ground probe and gravity
        /// stay as they are. The model is tilted to match (see MMotionAnimator).
        /// </summary>
        public float BodyPitch => bodyPitch;

        /// <summary>The way a body facing facing (a turn about the vertical) is turned, pitched pitch degrees nose up about its feet.</summary>
        public static Quaternion Attitude(Quaternion facing, float pitch) =>
            pitch == 0f ? facing : facing * Quaternion.AngleAxis(-pitch, Vector3.right);
    }
}
