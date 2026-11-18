namespace Character
{
    /// <summary>
    /// The names of the base layer's states in the shared Animator Controller (Mannequin.controller, shared by the humanoid
    /// models), built from its layout (Mannequin Layout.asset): one place for the tests, which check the character plays
    /// them. A state renamed in the layout is renamed here too.
    /// </summary>
    public static class MannequinAnimatorStates
    {
        /// <summary>Walking, running and standing, on the ground (its blend tree by speed).</summary>
        public const string Locomotion = "Locomotion";
        /// <summary>In the air: rising, the apex and falling.</summary>
        public const string Airborne = "Airborne";
        /// <summary>Taking off on a jump.</summary>
        public const string Jump = "Jump";
        /// <summary>Landing.</summary>
        public const string Land = "Land";
        /// <summary>Dashing.</summary>
        public const string Dash = "Dash";
        /// <summary>Crouched, still or walking.</summary>
        public const string Crouch = "Crouch";
        /// <summary>Sliding along the ground.</summary>
        public const string Slide = "Slide";
        /// <summary>Rolling.</summary>
        public const string Roll = "Roll";
        /// <summary>Running along a wall.</summary>
        public const string WallRun = "Wall Run";
        /// <summary>Sliding down a wall.</summary>
        public const string WallSlide = "Wall Slide";
        /// <summary>Climbing a climbable surface (its blend tree by the climb's direction).</summary>
        public const string WallClimb = "Wall Climb";
        /// <summary>Climbing a ladder.</summary>
        public const string Ladder = "Ladder";
        /// <summary>Stepping onto a climbable surface from the ground.</summary>
        public const string ClimbEnter = "Climb Enter";
        /// <summary>Stepping off a climbable surface's bottom onto the ground.</summary>
        public const string ClimbExit = "Climb Exit";
        /// <summary>Hanging from a ledge.</summary>
        public const string Hang = "Hang";
        /// <summary>Climbing up from a ledge onto it.</summary>
        public const string Climb = "Climb";
        /// <summary>Vaulting over something low.</summary>
        public const string Vault = "Vault";
        /// <summary>Holding an object to push or pull it.</summary>
        public const string Push = "Push";
        /// <summary>Swinging on a rope.</summary>
        public const string RopeSwing = "Rope Swing";
        /// <summary>Riding a zipline.</summary>
        public const string Zipline = "Zipline";
        /// <summary>Swimming at the surface.</summary>
        public const string Swim = "Swim";
        /// <summary>Swimming under the surface.</summary>
        public const string SwimUnderwater = "Swim Underwater";
    }
}
