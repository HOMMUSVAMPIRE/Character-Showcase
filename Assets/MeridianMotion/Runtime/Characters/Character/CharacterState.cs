namespace MeridianMotion.Characters
{
    /// <summary>
    /// What the character is doing during a physics step, as flags: one for each ability's state, which its component
    /// sets and its StateEvents turn into movement events (see MovementComponent.StateEvents). 256 flags (a Flags256: see
    /// CharacterState.Flags.cs for how they combine): the package's in bits 0 to 127, a project's own abilities' from 128.
    /// Each flag is a bit number (XBit, a constant, for a switch) and its value (X); each has a bit of its own (the hygiene
    /// check makes sure), and a new one takes the next unused bit. MovementStep's are [NonSerialized]: they're only the step's.
    /// </summary>
    public partial struct CharacterState
    {
        /// <summary>No state.</summary>
        public static readonly CharacterState None = default;
        public const int MovingBit = 0;
        public static readonly CharacterState Moving = Bit(MovingBit);
        public const int GroundedBit = 1;
        public static readonly CharacterState Grounded = Bit(GroundedBit);
        public const int JumpingBit = 2;
        public static readonly CharacterState Jumping = Bit(JumpingBit);
        public const int DashingBit = 3;
        public static readonly CharacterState Dashing = Bit(DashingBit);
        public const int RunningBit = 4;
        public static readonly CharacterState Running = Bit(RunningBit);
        public const int DeadBit = 5;
        public static readonly CharacterState Dead = Bit(DeadBit);
        // Crouched (low and slow), not counting the low stance of a slide or roll
        public const int CrouchingBit = 6;
        public static readonly CharacterState Crouching = Bit(CrouchingBit);
        public const int SlidingBit = 7;
        public static readonly CharacterState Sliding = Bit(SlidingBit);
        public const int RollingBit = 8;
        public static readonly CharacterState Rolling = Bit(RollingBit);
        public const int WallRunningBit = 9;
        public static readonly CharacterState WallRunning = Bit(WallRunningBit);
        // Hanging from a ledge
        public const int LedgeHangingBit = 10;
        public static readonly CharacterState LedgeHanging = Bit(LedgeHangingBit);
        // Climbing up from a ledge onto it
        public const int LedgeClimbingBit = 11;
        public static readonly CharacterState LedgeClimbing = Bit(LedgeClimbingBit);
        // Holding a large object to push or pull it
        public const int GrabbingBit = 12;
        public static readonly CharacterState Grabbing = Bit(GrabbingBit);
        // Sliding slowly down a wall it ran into head-on
        public const int WallSlidingBit = 13;
        public static readonly CharacterState WallSliding = Bit(WallSlidingBit);
        // Vaulting over something too low to grab and too high to step onto, caught in mid-air
        public const int VaultingBit = 14;
        public static readonly CharacterState Vaulting = Bit(VaultingBit);
        // Stepping up onto the top of something low and deep, reached from mid-air or sprinting (see Vaulting)
        public const int SteppingUpBit = 15;
        public static readonly CharacterState SteppingUp = Bit(SteppingUpBit);
        // Climbing a climbable surface (see MeridianMotion.Levels.ClimbableSurface), moving over it in any direction
        public const int WallClimbingBit = 16;
        public static readonly CharacterState WallClimbing = Bit(WallClimbingBit);
        // Hanging from a rope (see MeridianMotion.Levels.SwingRope) and swinging on it
        public const int RopeSwingingBit = 17;
        public static readonly CharacterState RopeSwinging = Bit(RopeSwingingBit);
        // Hanging from a zipline's cable (see MeridianMotion.Levels.ZiplineCable) and riding down it
        public const int ZipliningBit = 18;
        public static readonly CharacterState Ziplining = Bit(ZipliningBit);
        // Swimming at the surface of deep water (see MeridianMotion.Levels.SwimmableWater)
        public const int SwimmingBit = 19;
        public static readonly CharacterState Swimming = Bit(SwimmingBit);
        // Walking through water too shallow to swim in, slowed by it (see Wading)
        public const int WadingBit = 20;
        public static readonly CharacterState Wading = Bit(WadingBit);
        // Swimming under the surface (Swimming is set too: see Swimming)
        public const int DivingBit = 21;
        public static readonly CharacterState Diving = Bit(DivingBit);
        // New Ability (NewAbilityWindow) adds new abilities' states above this line, each the next unused bit.
    }
}
