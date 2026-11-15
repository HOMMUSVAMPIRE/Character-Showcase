namespace Character
{
    /// <summary>
    /// An enum which stores animation-relevant stateful information about a character
    /// </summary>
    [System.Flags]
    public enum CharacterState
    {
        Idle = 0,
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
        // Climbing a climbable surface (see Level.ClimbableSurface), moving over it in any direction
        WallClimbing = 65536,
        // Hanging from a rope (see Level.Rope) and swinging on it
        RopeSwinging = 131072,
        // Hanging from a zipline's cable (see Level.Zipline) and riding down it
        Ziplining = 262144,
    }
}
