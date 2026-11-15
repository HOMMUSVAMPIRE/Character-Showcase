namespace Character
{
    /// <summary>How a character's speeds change with its size. See MovementController.</summary>
    public enum MovementSizeScaling
    {
        /// <summary>Speeds and jump launch speed scale with the square root of height, and timings with it, while gravity
        /// stays the same, as for real animals.</summary>
        Natural = 0,
        /// <summary>Speeds stay as tuned at any size.</summary>
        Unchanged = 1,
        /// <summary>Speeds, accelerations and gravity all scale with height, and timings stay the same, so the character
        /// moves exactly like a scaled copy of itself: the same in relation to its size at any size.</summary>
        Proportional = 2,
    }
}
