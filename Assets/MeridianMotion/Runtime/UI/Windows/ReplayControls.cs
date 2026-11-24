namespace MeridianMotion.UI
{
    /// <summary>
    /// What the pause menu's Replays tab drives (PauseMenu): a game's replays, registered with ReplayControls by
    /// whatever records them. The UI knows nothing of what a replay is: it shows the tab while something's registered,
    /// asks it what can be done, and tells it what was pressed.
    /// </summary>
    public interface IReplayControls
    {
        /// <summary>Whether there's a run to save (Save Replay is greyed out without one).</summary>
        bool CanSave { get; }

        /// <summary>Whether there's a replay to play (Load Replay is greyed out without one).</summary>
        bool CanLoad { get; }

        /// <summary>A line about the replays now, shown on the tab ("Recording: 2:31", "Last saved: ...").</summary>
        string Status { get; }

        /// <summary>Save Replay: saves the run under way.</summary>
        void SaveReplay();

        /// <summary>Load Replay: plays a replay (the pause menu closes first).</summary>
        void LoadReplay();

        /// <summary>Called by the pause menu's Quit just before the game quits (a last chance to save).</summary>
        void BeforeQuit();
    }

    /// <summary>The replay controls registered now (IReplayControls), if any: the pause menu's Replays tab shows only while there are.</summary>
    public static class ReplayControls
    {
        /// <summary>The replay controls registered, or null.</summary>
        public static IReplayControls Current { get; private set; }

        /// <summary>Registers replay controls (the last registered wins).</summary>
        public static void Register(IReplayControls controls) => Current = controls;

        /// <summary>Takes them off, if they're the ones registered.</summary>
        public static void Unregister(IReplayControls controls)
        {
            if (Current == controls) { Current = null; }
        }
    }
}
