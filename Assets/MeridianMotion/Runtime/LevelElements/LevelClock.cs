using System;
using UnityEngine;

namespace MeridianMotion.Levels
{
    /// <summary>
    /// The level's clock, in physics steps: what the mechanics that move a character (moving platforms, pushables, ropes)
    /// go by, so where they are is a function of the step, not of when each was enabled. Two machines on the same tick,
    /// or a character replayed against a past step, see them in the same place. By default Unity's fixed steps since the
    /// game started; something else can set it (TickSource: a networked game's tick, the tests' rig). A character keeps
    /// its own clock (MMotionMotor.StepIndex); a networked game sets both from the same tick.
    /// </summary>
    public static class LevelClock
    {
        /// <summary>
        /// Gives the current tick instead of Unity's fixed steps, if set (a networked game's tick, a test's own clock).
        /// Set it back to null to go back to Unity's.
        /// </summary>
        public static Func<long> TickSource;

        /// <summary>The current physics step: TickSource's, else Unity's fixed steps since the game started.</summary>
        public static long Tick => TickSource?.Invoke() ?? UnityTick;

        /// <summary>Unity's fixed steps since the game started (its fixed time, in steps).</summary>
        public static long UnityTick => Time.fixedDeltaTime > 0f ? (long)Math.Round(Time.fixedTimeAsDouble / Time.fixedDeltaTime) : 0;

        /// <summary>The length (s) of a step.</summary>
        public static float StepLength => Time.fixedDeltaTime;

        /// <summary>The time (s) at a tick: its steps of StepLength, worked out in double so it's exact for a long session.</summary>
        public static double SecondsAt(long tick) => tick * (double)StepLength;
    }
}
