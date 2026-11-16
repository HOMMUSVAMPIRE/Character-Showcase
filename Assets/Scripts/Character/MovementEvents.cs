namespace Character
{
    /// <summary>
    /// Things that can happen to a character during a physics step, for animation, effects and sound to react to.
    /// Movement modes report the ones they cause (jumps and dashes); the MovementController detects the rest.
    /// 64 bits wide, as the first 31 ran out. Unity doesn't save fields of enums wider than 32 bits (it drops them
    /// silently), so a serialized set of these is kept as a long (see AnimatorParameterTranslator.EventTrigger).
    /// </summary>
    [System.Flags]
    public enum MovementEvents : long
    {
        None = 0,
        // A jump from the ground, or just after walking off it (coyote time), or off a rope or a zipline
        Jumped = 1,
        // A jump in mid-air (a double jump)
        AirJumped = 2,
        // Touched down after being in the air
        Landed = 4,
        // Left the ground, by jumping or walking off an edge
        LeftGround = 8,
        DashStarted = 16,
        // A dash finished, or was cancelled into a jump
        DashEnded = 32,
        // Started touching a ceiling
        HitCeiling = 64,
        // Started touching a wall
        HitWall = 128,
        // The starts and ends of the abilities below are found by the MovementController, from the character's state
        // (see MovementController.StateEvents), so every movement mode raises them alike.
        CrouchStarted = 256,
        CrouchEnded = 512,
        SlideStarted = 1024,
        SlideEnded = 2048,
        RollStarted = 4096,
        RollEnded = 8192,
        WallRunStarted = 16384,
        WallRunEnded = 32768,
        // Jumped off a wall during a wall run, wall slide or wall climb (reported by the movement mode)
        WallJumped = 65536,
        // Caught hold of a ledge in mid-air
        LedgeGrabbed = 131072,
        // Stopped hanging from a ledge: let go, or started climbing up
        LedgeReleased = 262144,
        LedgeClimbStarted = 524288,
        // Finished climbing up onto a ledge
        LedgeClimbEnded = 1048576,
        // Took hold of an object to push or pull
        GrabStarted = 2097152,
        // Let go of an object
        GrabEnded = 4194304,
        // Started or stopped sliding down a wall (jumping off one is a WallJumped)
        WallSlideStarted = 8388608,
        WallSlideEnded = 16777216,
        // Started or finished vaulting over something
        VaultStarted = 33554432,
        VaultEnded = 67108864,
        // Started or finished stepping up onto something low and deep
        StepUpStarted = 134217728,
        StepUpEnded = 268435456,
        // Took hold of a climbable surface, and let go of it (jumping off one is a WallJumped; climbing up over its top
        // onto a ledge is a LedgeClimbStarted)
        WallClimbStarted = 536870912,
        WallClimbEnded = 1073741824,
        // Caught hold of a rope, and let go of it (jumping off one is a Jumped too)
        RopeGrabbed = 1L << 31,
        RopeReleased = 1L << 32,
        // Caught hold of a zipline's cable, and let go of it (at its end, or jumping off: a Jumped too)
        ZiplineStarted = 1L << 33,
        ZiplineEnded = 1L << 34,
        // Launched by a bounce pad (see Level.BouncePad), reported by the movement mode
        Bounced = 1L << 35,
        // Started swimming (got into water deep enough to swim in), and stopped (climbed or jumped out, or found its feet
        // in shallower water: jumping out is a Jumped too)
        SwimStarted = 1L << 36,
        SwimEnded = 1L << 37,
        // Started or stopped wading (walking through water too shallow to swim in)
        WadeStarted = 1L << 38,
        WadeEnded = 1L << 39,
    }
}
