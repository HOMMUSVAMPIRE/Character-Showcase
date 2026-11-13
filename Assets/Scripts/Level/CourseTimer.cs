using UnityEngine;
using UnityEngine.Events;

namespace Level
{
    /// <summary>How a timing gate counts toward its course.</summary>
    public enum TimingGateRole
    {
        /// <summary>A lap's gate: each pass ends the lap under way (if any) and starts the next.</summary>
        StartAndFinish,
        /// <summary>Starts the run.</summary>
        Start,
        /// <summary>Ends the run under way.</summary>
        Finish,
    }

    /// <summary>
    /// Times runs over a course marked by timing gates (see TimingGate): laps through one start-and-finish gate, or runs
    /// from a start gate to a finish gate (with the speed over the course's distance, if it has one). Only its racer
    /// counts (set with SetRacer, as a character spawner's On Character Spawned can; with none, anything that passes
    /// does). It shows the time under way, the last and the best through On Display, as a line of text for a sign.
    /// Times are game time, measured on the physics step a gate's passed.
    /// </summary>
    public class CourseTimer : MonoBehaviour
    {
        [Tooltip("The course's name, shown with its times.")]
        [SerializeField] private string courseName = "Lap";
        [Tooltip("The course's length in metres, for showing a run's average speed. 0 shows no speed.")]
        [Min(0f)] [SerializeField] private float distance;
        [Tooltip("Passes this soon (seconds) after a run starts don't end it: the same gate crossed again at once, or a racer turning back through it.")]
        [Min(0f)] [SerializeField] private float minimumRunTime = 1f;
        [Tooltip("What's timed: only it counts when it passes a gate. Empty, anything that passes does.")]
        [SerializeField] private Transform racer;
        [Tooltip("Called with the course's times as a line of text, about ten times a second while a run's under way and when one ends.")]
        [SerializeField] private UnityEvent<string> onDisplay = new();

        private const float DisplayInterval = 0.1f;
        private float startTime;
        private float nextDisplay;

        /// <summary>The course's name.</summary>
        public string CourseName { get => courseName; set => courseName = value; }
        /// <summary>The course's length in metres (0: none).</summary>
        public float Distance { get => distance; set => distance = Mathf.Max(0f, value); }
        /// <summary>What's timed (null: anything).</summary>
        public Transform Racer => racer;
        /// <summary>Called with the course's times as a line of text.</summary>
        public UnityEvent<string> OnDisplay => onDisplay;
        /// <summary>Whether a run's under way.</summary>
        public bool IsRunning { get; private set; }
        /// <summary>How many runs (laps) have been finished.</summary>
        public int Completed { get; private set; }
        /// <summary>The last finished run's time in seconds, or 0 if none has finished.</summary>
        public float LastTime { get; private set; }
        /// <summary>The best finished run's time in seconds, or 0 if none has finished.</summary>
        public float BestTime { get; private set; }
        /// <summary>The time of the run under way, or 0.</summary>
        public float CurrentTime => IsRunning ? Time.time - startTime : 0f;

        /// <summary>Sets what's timed (a character spawner's On Character Spawned can call this), and resets any run under way.</summary>
        public void SetRacer(Transform newRacer)
        {
            racer = newRacer;
            IsRunning = false;
            Show();
        }

        /// <summary>
        /// A gate of this course was passed by passer (the object it detected): starts, ends or laps the run, as the
        /// gate's role says, if passer is the racer (or part of it).
        /// </summary>
        public void Pass(TimingGateRole role, Transform passer)
        {
            if (racer != null && (passer == null || (passer != racer && !passer.IsChildOf(racer)))) { return; }
            float now = Time.time;
            bool canEnd = IsRunning && now - startTime >= minimumRunTime;
            switch (role)
            {
                case TimingGateRole.StartAndFinish:
                    if (canEnd) { Finish(now); }
                    if (!IsRunning || canEnd) { Begin(now); }
                    break;
                case TimingGateRole.Start:
                    Begin(now);
                    break;
                case TimingGateRole.Finish:
                    if (canEnd) { Finish(now); }
                    break;
            }
            Show();
        }

        private void Begin(float now)
        {
            IsRunning = true;
            startTime = now;
        }

        private void Finish(float now)
        {
            IsRunning = false;
            LastTime = now - startTime;
            BestTime = Completed == 0 ? LastTime : Mathf.Min(BestTime, LastTime);
            Completed++;
        }

        private void Start() => Show();

        private void Update()
        {
            if (!IsRunning || Time.time < nextDisplay) { return; }
            Show();
        }

        /// <summary>The course's times as a line of text: the run under way (or the last), the last and best, and speed.</summary>
        public string Describe()
        {
            string Clock(float seconds) => seconds <= 0f ? "--:--.--" : $"{(int)(seconds / 60f)}:{seconds % 60f:00.00}";
            string text = $"{courseName.ToUpperInvariant()}  {Clock(IsRunning ? CurrentTime : LastTime)}";
            if (Completed > 0)
            {
                text += $"\nLAST {Clock(LastTime)}  BEST {Clock(BestTime)}";
                if (distance > 0f) { text += $"  {distance / LastTime:0.0} m/s"; }
            }
            return text;
        }

        private void Show()
        {
            nextDisplay = Time.time + DisplayInterval;
            onDisplay?.Invoke(Describe());
        }
    }
}
