using UnityEngine;

namespace Character
{
    /// <summary>
    /// The character's body as collision sees it: its collider, its size (as authored, and now, at the character's size
    /// and posture), and the queries that depend on its shape: whether it fits somewhere, sweeping it through the world,
    /// and pushing it out of what has moved into it. The MovementController resolves the character's movement through
    /// these alone (see MovementController.Body), so a body of another kind is another subclass; PrimitiveBody is one of
    /// Unity's primitive colliders (a capsule, box or sphere). Its origin is the character's: the point it stands on, its
    /// feet, though its shape may sit off it (centred above the feet, lying ahead of them). It turns with the character.
    /// Its queries skip the character's own colliders, and what it ignores (MovementController.IgnoredCollisionRoot).
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

        /// <summary>
        /// The radius (m) of the body's footprint, at the character's authored size, which its scale multiplies (see
        /// FootprintRadius).
        /// </summary>
        public abstract float AuthoredRadius { get; }

        /// <summary>
        /// The radius (m) of the body's footprint now, at the character's size and posture: the ground check under its feet
        /// is about this wide, and small clearances (the skin width) shrink with it. Only while it has a collider.
        /// </summary>
        public abstract float FootprintRadius { get; }

        /// <summary>The body's height (m) now, top to bottom, at the character's size and posture. Only while it has a collider.</summary>
        public abstract float Height { get; }

        /// <summary>
        /// Whether turning the character turns its body into somewhere else (it isn't round about the feet: lying, a box,
        /// a shape off to one side), so that a turn needs room (see MovementController.CheckMovementValid).
        /// </summary>
        public abstract bool TurnsWithFacing { get; }

        /// <summary>
        /// Whether the body's bottom is flat (a box's), so it rests on whatever's under any of it as on level ground,
        /// rather than round (a capsule's), which rests on an edge only as far as it rolls onto it. Stepping up, a flat
        /// body is on the step once it's over its top (see MovementController.TryStepUp).
        /// </summary>
        public virtual bool FlatBottomed => false;

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

        /// <summary>Shapes the collider for the character's scale and posture. Doesn't check for room: see Fits.</summary>
        public abstract void Resize(Vector3 scale, BodyPosture posture);

        /// <summary>
        /// Whether the body would fit with its feet at feet, turned rotation, at the given scale and posture, without
        /// overlapping the world (more than the skin width, so a floor it stands on doesn't count).
        /// </summary>
        public abstract bool Fits(Vector3 feet, Quaternion rotation, Vector3 scale, BodyPosture posture);

        /// <summary>
        /// Sweeps the body as it is now, turned rotation, from feet along direction, returning the nearest thing it would
        /// hit within distance (not counting what it starts overlapping). A hit's distance includes the skin width.
        /// </summary>
        public abstract bool Sweep(Vector3 feet, Quaternion rotation, Vector3 direction, float distance, out RaycastHit hit);

        /// <summary>
        /// How far the body, its feet at feet and turned rotation, must move to stop overlapping the world. Only overlaps
        /// deeper than the skin width count, so resting against the floor or a wall isn't being inside it.
        /// </summary>
        public abstract Vector3 Depenetrate(Vector3 feet, Quaternion rotation);

        /// <summary>
        /// The same body for a copy of the character (see MovementController.CopyForSimulation): a collider of its kind
        /// added to the copy's object, and its shape as authored (or, given a shape as authored, that body, of its kind:
        /// the one a model would give the character, say). The copy sizes it for itself.
        /// </summary>
        public abstract CharacterBody CopyFor(MovementController copy, BodyShape? authoredShape = null);

        /// <summary>
        /// Everything about the body that what the character can do depends on, written in full, for knowing when its
        /// capabilities need measuring again (see MovementController.Capabilities).
        /// </summary>
        public abstract string CapabilitiesKey();
    }
}
