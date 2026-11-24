using System;
using MeridianMotion.Levels;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// The level's clock during and after a replay (LevelClock.TickSource): the tick the replay's keyframe had, then on
    /// from it a tick a step, so the platforms are where they were when it was recorded. It wraps whatever source there
    /// was (Unity's fixed steps, a test rig's), offsetting it: SyncTo sets the offset at a step (until the first, the
    /// tick's held at the one given, so the step's platforms are placed for it), Remove puts the source back.
    /// </summary>
    public static class ReplayClock
    {
        private static Func<long> previous;
        private static long offset, held;
        private static bool installed, pending;

        /// <summary>Whether it's the level's clock now.</summary>
        public static bool Installed => installed && LevelClock.TickSource == (Func<long>)Tick;

        /// <summary>Makes it the level's clock, reading tick until the next SyncTo (called at the step that tick is for).</summary>
        public static void Install(long tick)
        {
            if (!Installed)
            {
                previous = LevelClock.TickSource;
                LevelClock.TickSource = Tick;
                installed = true;
            }
            held = tick;
            pending = true;
        }

        /// <summary>At a step: the level's tick is tick now, and goes on a tick a step from it.</summary>
        public static void SyncTo(long tick)
        {
            if (!Installed) { return; }
            offset = tick - Base();
            pending = false;
        }

        /// <summary>Puts back the clock there was before it.</summary>
        public static void Remove()
        {
            if (!installed) { return; }
            if (LevelClock.TickSource == (Func<long>)Tick) { LevelClock.TickSource = previous; }
            previous = null;
            installed = false;
            pending = false;
        }

        private static long Tick() => pending ? held : Base() + offset;

        private static long Base() => previous?.Invoke() ?? LevelClock.UnityTick;
    }
}
