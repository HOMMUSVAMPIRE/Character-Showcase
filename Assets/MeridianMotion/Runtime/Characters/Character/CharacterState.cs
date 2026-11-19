namespace MeridianMotion.Characters
{
    /// <summary>
    /// What the character is doing during a physics step, as flags: one for each ability's state, which its component
    /// sets and its StateEvents turn into movement events (see MovementComponent.StateEvents). 64 bits wide, so there's
    /// room for abilities to come; each member is one bit of its own (the hygiene check makes sure), and a new one takes
    /// the next unused bit. Unity doesn't save fields of enums wider than 32 bits (it drops them silently), so a field of
    /// this type is never serialized (MovementStep's are [NonSerialized]: they're only the step's).
    /// </summary>
    [System.Flags]
    public enum CharacterState : long
    {
        None = 0,
        Moving = 1,
        Grounded = 2,
        Jumping = 4,
        Dashing = 8,
        Running = 16,
        Dead = 32,
        // Crouched (low and slow), not counting the low stance of a slide or roll
        Crouching = 64,
        Sliding = 128,
        Rolling = 256,
        WallRunning = 512,
        // Hanging from a ledge
        LedgeHanging = 1024,
        // Climbing up from a ledge onto it
        LedgeClimbing = 2048,
        // Holding a large object to push or pull it
        Grabbing = 4096,
        // Sliding slowly down a wall it ran into head-on
        WallSliding = 8192,
        // Vaulting over something too low to grab and too high to step onto, caught in mid-air
        Vaulting = 16384,
        // Stepping up onto the top of something low and deep, reached from mid-air or sprinting (see Vaulting)
        SteppingUp = 32768,
        // Climbing a climbable surface (see MeridianMotion.Levels.ClimbableSurface), moving over it in any direction
        WallClimbing = 65536,
        // Hanging from a rope (see MeridianMotion.Levels.SwingRope) and swinging on it
        RopeSwinging = 131072,
        // Hanging from a zipline's cable (see MeridianMotion.Levels.ZiplineCable) and riding down it
        Ziplining = 262144,
        // Swimming at the surface of deep water (see MeridianMotion.Levels.SwimmableWater)
        Swimming = 524288,
        // Walking through water too shallow to swim in, slowed by it (see Wading)
        Wading = 1048576,
        // Swimming under the surface (Swimming is set too: see Swimming)
        Diving = 2097152,
        // New Ability (NewAbilityWindow) adds new abilities' states above this line, each the next unused bit.
    }
}
