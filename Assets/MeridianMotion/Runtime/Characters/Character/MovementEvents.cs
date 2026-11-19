namespace MeridianMotion.Characters
{
    /// <summary>
    /// Things that can happen to a character during a physics step, for animation, effects and sound to react to.
    /// Movement modes report the ones they cause (jumps and dashes); the MMotionMotor detects the rest.
    /// 256 flags (a Flags256: see MovementEvents.Flags.cs for how they combine): the package's in bits 0 to 127, a project's
    /// own abilities' from 128. Each flag is a bit number (XBit, a constant, for a switch) and its value (X); each has a
    /// bit of its own (the hygiene check makes sure), and a new one takes the next unused bit.
    /// </summary>
    public partial struct MovementEvents
    {
        /// <summary>No event.</summary>
        public static readonly MovementEvents None = default;
        // A jump from the ground, or just after walking off it (coyote time), or off a rope or a zipline
        public const int JumpedBit = 0;
        public static readonly MovementEvents Jumped = Bit(JumpedBit);
        // A jump in mid-air (a double jump)
        public const int AirJumpedBit = 1;
        public static readonly MovementEvents AirJumped = Bit(AirJumpedBit);
        // Touched down after being in the air
        public const int LandedBit = 2;
        public static readonly MovementEvents Landed = Bit(LandedBit);
        // Left the ground, by jumping or walking off an edge
        public const int LeftGroundBit = 3;
        public static readonly MovementEvents LeftGround = Bit(LeftGroundBit);
        public const int DashStartedBit = 4;
        public static readonly MovementEvents DashStarted = Bit(DashStartedBit);
        // A dash finished, or was cancelled into a jump
        public const int DashEndedBit = 5;
        public static readonly MovementEvents DashEnded = Bit(DashEndedBit);
        // Started touching a ceiling
        public const int HitCeilingBit = 6;
        public static readonly MovementEvents HitCeiling = Bit(HitCeilingBit);
        // Started touching a wall
        public const int HitWallBit = 7;
        public static readonly MovementEvents HitWall = Bit(HitWallBit);
        // The starts and ends of the abilities below are found from the character's state (each ability's component
        // declares its states' events: see MovementComponent.StateEvents), so it only has to set the state.
        public const int CrouchStartedBit = 8;
        public static readonly MovementEvents CrouchStarted = Bit(CrouchStartedBit);
        public const int CrouchEndedBit = 9;
        public static readonly MovementEvents CrouchEnded = Bit(CrouchEndedBit);
        public const int SlideStartedBit = 10;
        public static readonly MovementEvents SlideStarted = Bit(SlideStartedBit);
        public const int SlideEndedBit = 11;
        public static readonly MovementEvents SlideEnded = Bit(SlideEndedBit);
        public const int RollStartedBit = 12;
        public static readonly MovementEvents RollStarted = Bit(RollStartedBit);
        public const int RollEndedBit = 13;
        public static readonly MovementEvents RollEnded = Bit(RollEndedBit);
        public const int WallRunStartedBit = 14;
        public static readonly MovementEvents WallRunStarted = Bit(WallRunStartedBit);
        public const int WallRunEndedBit = 15;
        public static readonly MovementEvents WallRunEnded = Bit(WallRunEndedBit);
        // Jumped off a wall during a wall run, wall slide or wall climb (reported by the movement mode)
        public const int WallJumpedBit = 16;
        public static readonly MovementEvents WallJumped = Bit(WallJumpedBit);
        // Caught hold of a ledge in mid-air
        public const int LedgeGrabbedBit = 17;
        public static readonly MovementEvents LedgeGrabbed = Bit(LedgeGrabbedBit);
        // Stopped hanging from a ledge: let go, or started climbing up
        public const int LedgeReleasedBit = 18;
        public static readonly MovementEvents LedgeReleased = Bit(LedgeReleasedBit);
        public const int LedgeClimbStartedBit = 19;
        public static readonly MovementEvents LedgeClimbStarted = Bit(LedgeClimbStartedBit);
        // Finished climbing up onto a ledge
        public const int LedgeClimbEndedBit = 20;
        public static readonly MovementEvents LedgeClimbEnded = Bit(LedgeClimbEndedBit);
        // Took hold of an object to push or pull
        public const int GrabStartedBit = 21;
        public static readonly MovementEvents GrabStarted = Bit(GrabStartedBit);
        // Let go of an object
        public const int GrabEndedBit = 22;
        public static readonly MovementEvents GrabEnded = Bit(GrabEndedBit);
        // Started or stopped sliding down a wall (jumping off one is a WallJumped)
        public const int WallSlideStartedBit = 23;
        public static readonly MovementEvents WallSlideStarted = Bit(WallSlideStartedBit);
        public const int WallSlideEndedBit = 24;
        public static readonly MovementEvents WallSlideEnded = Bit(WallSlideEndedBit);
        // Started or finished vaulting over something
        public const int VaultStartedBit = 25;
        public static readonly MovementEvents VaultStarted = Bit(VaultStartedBit);
        public const int VaultEndedBit = 26;
        public static readonly MovementEvents VaultEnded = Bit(VaultEndedBit);
        // Started or finished stepping up onto something low and deep
        public const int StepUpStartedBit = 27;
        public static readonly MovementEvents StepUpStarted = Bit(StepUpStartedBit);
        public const int StepUpEndedBit = 28;
        public static readonly MovementEvents StepUpEnded = Bit(StepUpEndedBit);
        // Took hold of a climbable surface, and let go of it (jumping off one is a WallJumped; climbing up over its top
        // onto a ledge is a LedgeClimbStarted)
        public const int WallClimbStartedBit = 29;
        public static readonly MovementEvents WallClimbStarted = Bit(WallClimbStartedBit);
        public const int WallClimbEndedBit = 30;
        public static readonly MovementEvents WallClimbEnded = Bit(WallClimbEndedBit);
        // Caught hold of a rope, and let go of it (jumping off one is a Jumped too)
        public const int RopeGrabbedBit = 31;
        public static readonly MovementEvents RopeGrabbed = Bit(RopeGrabbedBit);
        public const int RopeReleasedBit = 32;
        public static readonly MovementEvents RopeReleased = Bit(RopeReleasedBit);
        // Caught hold of a zipline's cable, and let go of it (at its end, or jumping off: a Jumped too)
        public const int ZiplineStartedBit = 33;
        public static readonly MovementEvents ZiplineStarted = Bit(ZiplineStartedBit);
        public const int ZiplineEndedBit = 34;
        public static readonly MovementEvents ZiplineEnded = Bit(ZiplineEndedBit);
        // Launched by a bounce pad (see MeridianMotion.Levels.LaunchPad), reported by the movement mode
        public const int BouncedBit = 35;
        public static readonly MovementEvents Bounced = Bit(BouncedBit);
        // Started swimming (got into water deep enough to swim in), and stopped (climbed or jumped out, or found its feet
        // in shallower water: jumping out is a Jumped too)
        public const int SwimStartedBit = 36;
        public static readonly MovementEvents SwimStarted = Bit(SwimStartedBit);
        public const int SwimEndedBit = 37;
        public static readonly MovementEvents SwimEnded = Bit(SwimEndedBit);
        // Started or stopped wading (walking through water too shallow to swim in)
        public const int WadeStartedBit = 38;
        public static readonly MovementEvents WadeStarted = Bit(WadeStartedBit);
        public const int WadeEndedBit = 39;
        public static readonly MovementEvents WadeEnded = Bit(WadeEndedBit);
        // Dived under the water's surface, and stopped swimming underwater (back up at the surface, or out of the water)
        public const int DiveStartedBit = 40;
        public static readonly MovementEvents DiveStarted = Bit(DiveStartedBit);
        public const int DiveEndedBit = 41;
        public static readonly MovementEvents DiveEnded = Bit(DiveEndedBit);
        // New Ability (NewAbilityWindow) adds new abilities' events above this line, each the next unused bit.
    }

    /// <summary>
    /// A state of the character's and the events raised as it starts and stops being in it (see
    /// MovementComponent.StateEvents): an ability sets the state, and the events follow from it.
    /// </summary>
    public readonly struct StateEventPair
    {
        public readonly CharacterState state;
        public readonly MovementEvents started, ended;

        public StateEventPair(CharacterState state, MovementEvents started, MovementEvents ended)
        {
            this.state = state;
            this.started = started;
            this.ended = ended;
        }

        /// <summary>Adds this state's start or end to the step's events, if it started or ended this step.</summary>
        public void Detect(MovementStep data)
        {
            bool was = data.previousState.HasAny(state), now = data.state.HasAny(state);
            if (!was && now) { data.events |= started; }
            if (was && !now) { data.events |= ended; }
        }
    }
}
