using UnityEngine;

namespace Character
{
    /// <summary>
    /// The character's body as collision sees it: its collider, its size (as authored, and now, at the character's size
    /// and posture), and the queries that depend on its shape: whether it fits somewhere, sweeping it through the world,
    /// and pushing it out of what has moved into it. The MovementController resolves the character's movement through
    /// these alone (see MovementController.Body), so a body of another shape is another subclass; CapsuleBody is the
    /// upright capsule. Its origin is the character's: the point it stands on, its feet. Its queries skip the character's
    /// own colliders, and what it ignores (MovementController.IgnoredCollisionRoot).
    /// </summary>
    public abstract class CharacterBody
    {
        protected CharacterBody(MovementController owner) => Owner = owner;

        /// <summary>The character whose body this is.</summary>
        protected MovementController Owner { get; }

        /// <summary>The collider the body is, on the character's object; null if it has none yet.</summary>
        public abstract Collider Collider { get; }

        /// <summary>Whether the body's size as authored is known yet (it's read from the collider when first needed).</summary>
        public abstract bool HasAuthoredSize { get; }

        /// <summary>The body's height (m) standing, at the character's authored size (1, 1, 1), which its scale multiplies.</summary>
        public abstract float AuthoredHeight { get; }

        /// <summary>The body's radius (m) across, at the character's authored size, which its scale multiplies.</summary>
        public abstract float AuthoredRadius { get; }

        /// <summary>The body's radius (m) across now, at the character's size and posture. Only while it has a collider.</summary>
        public abstract float Radius { get; }

        /// <summary>The body's height (m) now, at the character's size and posture. Only while it has a collider.</summary>
        public abstract float Height { get; }

        // Its proportions: what the abilities measure the character by (how tall it stands, how far it reaches out in front,
        // behind and to the sides of its feet), rather than the collider's dimensions, so a body of another shape answers
        // them for itself. Its posture (lowered while crouching, say) doesn't change its standing height.

        /// <summary>How tall (m) the character stands, at its size: grip, hang and reach heights are shares of it.</summary>
        public virtual float StandingHeight => AuthoredHeight * Owner.Scale.y;

        /// <summary>How far (m) the body reaches out in front of its feet, horizontally, now: how near it comes to what it faces.</summary>
        public abstract float FrontExtent { get; }

        /// <summary>How far (m) the body reaches out behind its feet, horizontally, now.</summary>
        public abstract float BackExtent { get; }

        /// <summary>How far (m) the body reaches out to either side of its feet, now: half the width it needs to pass between things.</summary>
        public abstract float SideExtent { get; }

        /// <summary>Reads the body's size as authored from its collider, if it hasn't yet (before Awake, too: a prefab asked about).</summary>
        public abstract void RememberAuthoredSize();

        /// <summary>Sizes the collider for the character's scale and the share of its standing height it has (its posture).</summary>
        public abstract void Resize(Vector3 scale, float heightFraction);

        /// <summary>
        /// Whether the body would fit with its feet at feet, at the given scale and share of its standing height, without
        /// overlapping the world (more than the skin width, so a floor it stands on doesn't count).
        /// </summary>
        public abstract bool Fits(Vector3 feet, Vector3 scale, float heightFraction);

        /// <summary>
        /// Sweeps the body as it is now from feet along direction, returning the nearest thing it would hit within distance
        /// (not counting what it starts overlapping). A hit's distance includes the skin width.
        /// </summary>
        public abstract bool Sweep(Vector3 feet, Vector3 direction, float distance, out RaycastHit hit);

        /// <summary>
        /// How far the body, its feet at feet and turned rotation, must move to stop overlapping the world. Only overlaps
        /// deeper than the skin width count, so resting against the floor or a wall isn't being inside it.
        /// </summary>
        public abstract Vector3 Depenetrate(Vector3 feet, Quaternion rotation);

        /// <summary>
        /// The same body for a copy of the character (see MovementController.CopyForSimulation): a collider of its kind
        /// added to the copy's object, and its size as authored (or, given an authored radius and height, a body of that
        /// size standing on its feet). The copy sizes it for itself.
        /// </summary>
        public abstract CharacterBody CopyFor(MovementController copy, float? authoredRadius = null, float? authoredHeight = null);

        /// <summary>
        /// Everything about the body that what the character can do depends on, written in full, for knowing when its
        /// capabilities need measuring again (see MovementController.Capabilities).
        /// </summary>
        public abstract string CapabilitiesKey();
    }
}
