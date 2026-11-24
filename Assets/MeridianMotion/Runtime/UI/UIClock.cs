using System;
using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// The time every UI timer goes by (the close animation's safety limit, notifications' durations): real time, unscaled,
    /// so the UI keeps running while the game is paused (Time.timeScale 0). A test sets TimeSource to drive it itself, as
    /// the character tests own the level's clock, and sets it back to null after.
    /// </summary>
    public static class UIClock
    {
        /// <summary>What Now reads, in seconds; null for Time.unscaledTime.</summary>
        public static Func<float> TimeSource { get; set; }

        /// <summary>The UI's time now, in seconds.</summary>
        public static float Now => TimeSource != null ? TimeSource() : Time.unscaledTime;
    }
}
