using UnityEngine;

namespace Character
{
    /// <summary>How the ground check found the character on the ground (see MovementController.CheckGrounded).</summary>
    public enum GroundedBy
    {
        /// <summary>It wasn't on the ground.</summary>
        None,
        /// <summary>The ground check's sphere, under its feet, found walkable ground.</summary>
        Probe,
        /// <summary>The ground check's sphere found a step's edge (too steep to stand on, but with a tread below it).</summary>
        ProbeOnStepEdge,
        /// <summary>The sphere found nothing, but the body itself rests on walkable ground or a step's edge.</summary>
        BodyRest,
    }

    /// <summary>
    /// What collision did to the character in a step (see MovementData.resolution): how it pushed the body out of the
    /// world, how it found it on the ground, how far it stepped it up, and how it snapped it to the ground. Changes
    /// nothing: it's there to see why the character moved as it did (the tests measure jitter by it).
    /// </summary>
    [System.Serializable]
    public struct StepResolution
    {
        /// <summary>How the step's ground check found the character on the ground, as the step started.</summary>
        public GroundedBy groundedBy;
        /// <summary>How far (m) the body was pushed out of what had moved into it (or it into), before moving: zero for nothing.</summary>
        public Vector3 depenetration;
        /// <summary>How far (m) collision stepped the body up onto a step, moving: 0 for none.</summary>
        public float steppedUp;
        /// <summary>Whether the ground snap moved the body (see groundSnap).</summary>
        public bool snapped;
        /// <summary>
        /// How far (m) the ground snap moved the body down onto the ground: negative where it lifted it back up out of
        /// the ground (closer than the skin width, rolling over a step's edge, say). 0 unless snapped.
        /// </summary>
        public float groundSnap;
    }
}
