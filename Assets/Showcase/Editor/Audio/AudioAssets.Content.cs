using MeridianMotion.Showcase.CharacterAudio;

namespace MeridianMotion.Showcase.Audio.EditorTools
{
    public static partial class AudioAssets
    {
        /// <summary>A surface, and the level materials (Level_[name].mat, and their (Moving) variants) that are it.</summary>
        private static readonly (string surface, string[] materials)[] Surfaces =
        {
            ("Concrete", new[] { "Concrete", "Asphalt", "Panel", "Plate", "Graphite", "Light", "Accent", "Cyan", "White" }),
            ("Metal", new[] { "Steel" }),
            ("Glass", new[] { "Glass" }),
            ("Soil", new[] { "Soil", "Ground" }),
            ("Grass", new[] { "Grass" }),
            ("Foliage", new[] { "Foliage" }),
        };

        /// <summary>The surface ground falls back to (unmapped materials, no ground), and the one for grass underfoot.</summary>
        private const string FallbackSurface = "Concrete", GrassSurface = "Grass";

        /// <summary>A one-shot of the character's: its Sound Event, the event that plays it, whether it has a sound per
        /// surface, and what its strength comes from.</summary>
        private readonly struct OneShot
        {
            public readonly string sound, eventName;
            public readonly bool bySurface;
            public readonly StrengthFrom strength;
            public readonly float minSpeed;

            public OneShot(string sound, string eventName, bool bySurface = false, StrengthFrom strength = StrengthFrom.Full,
                           float minSpeed = 0f)
            {
                this.sound = sound;
                this.eventName = eventName;
                this.bySurface = bySurface;
                this.strength = strength;
                this.minSpeed = minSpeed;
            }
        }

        // The hard landing's Min Speed (m/s at the authored size): about a 4 m fall at the default tuning's gravity. Tune it
        // on the Sound Set, not here.
        private const float HardLandingSpeed = 16f;

        private static readonly OneShot[] OneShots =
        {
            new("Jump", "Jumped", bySurface: true),
            new("Air Jump", "AirJumped"),
            new("Wall Jump", "WallJumped"),
            new("Land", "Landed", bySurface: true, strength: StrengthFrom.FallSpeed),
            new("Land (Hard)", "Landed", bySurface: true, strength: StrengthFrom.FallSpeed, minSpeed: HardLandingSpeed),
            new("Dash", "DashStarted"),
            new("Hit Wall", "HitWall", bySurface: true, strength: StrengthFrom.SpeedIntoWall),
            new("Hit Ceiling", "HitCeiling"),
            new("Slide Start", "SlideStarted", bySurface: true, strength: StrengthFrom.Speed),
            new("Roll", "RollStarted", bySurface: true),
            new("Crouch", "CrouchStarted"),
            new("Stand", "CrouchEnded"),
            new("Ledge Grab", "LedgeGrabbed"),
            new("Ledge Climb", "LedgeClimbStarted"),
            new("Ledge Drop", "LedgeReleased"),
            new("Grab", "GrabStarted"),
            new("Vault", "VaultStarted", strength: StrengthFrom.Speed),
            new("Step Up", "StepUpStarted", bySurface: true),
            new("Wall Run Start", "WallRunStarted", strength: StrengthFrom.Speed),
            new("Wall Climb Start", "WallClimbStarted"),
            new("Rope Grab", "RopeGrabbed", strength: StrengthFrom.Speed),
            new("Rope Release", "RopeReleased"),
            new("Zipline Attach", "ZiplineStarted"),
            new("Zipline Detach", "ZiplineEnded"),
            new("Bounce", "Bounced"),
            new("Dive", "DiveStarted"),
            new("Surface", "DiveEnded"),
        };

        /// <summary>A loop of the character's: its Sound Event, the state it plays in, and whether only while moving.</summary>
        private static readonly (string sound, string state, bool moving)[] Loops =
        {
            ("Slide Loop", "Sliding", false),
            ("Wall Run Loop", "WallRunning", false),
            ("Wall Slide Loop", "WallSliding", false),
            ("Climb Loop", "WallClimbing", true),
            ("Rope Creak Loop", "RopeSwinging", false),
            ("Zipline Loop", "Ziplining", false),
            ("Swim Loop", "Swimming", true),
            ("Wade Loop", "Wading", true),
            ("Underwater Loop", "Diving", false),
        };

        // Each kind of sound's priority (0 to 255) when every voice is busy: the UI's first, footsteps last.
        private const int UIPriority = 200, LevelPriority = 160, OneShotPriority = 128, FootstepPriority = 100, LoopPriority = 96;
    }
}
