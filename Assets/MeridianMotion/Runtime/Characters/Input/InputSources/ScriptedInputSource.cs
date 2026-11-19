using System;
using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// Drives a character from a script: a list of timed steps (each held for its seconds: a move input, a look and the
    /// buttons held), or a recording of input frames (Play: see InputRecording), from the first step it's asked for.
    /// When the script ends the character stands idle, or it starts over (Loop). Its frames are the same however often
    /// it's played, so a scripted character moves the same each time.
    /// </summary>
    [AddComponentMenu("Meridian Motion/Characters/Scripted Input Source")]
    public class ScriptedInputSource : CharacterInputSource
    {
        /// <summary>One part of a script: what the input does, held for a time.</summary>
        [Serializable]
        public struct TimedStep
        {
            [Tooltip("How long (seconds) this part is held.")]
            [Min(0f)] public float seconds;
            [Tooltip("The move input: x right, y forward (each -1 to 1), relative to the look.")]
            public Vector2 move;
            [Tooltip("The way the input looks: degrees about up from +Z (the move input is relative to it).")]
            public float lookYaw;
            [Tooltip("The way the input looks: degrees up (negative: down), for steering underwater.")]
            public float lookPitch;
            [Tooltip("The buttons held. A button held in one part and the next is held on, not pressed again: leave a part " +
                     "between with it up to press it again.")]
            public InputButtons held;
        }

        [Header("Script")]
        [Tooltip("The script, played in order from the first step the character takes with it. Ignored while a recording " +
                 "is being played (Play).")]
        [SerializeField] private List<TimedStep> steps = new();
        [Tooltip("Start over at the end, rather than standing idle.")]
        [SerializeField] private bool loop;

        private InputRecording recording;
        private bool started;
        private uint firstTick;
        // Each timed step's end, in physics steps from the start (worked out once, so the steps are exact).
        private readonly List<int> stepEnds = new();
        private float endsWorkedOutFor = -1f;

        /// <summary>The timed steps (change them before playing, or call Restart after).</summary>
        public List<TimedStep> Steps => steps;
        /// <summary>Whether it starts over at the end.</summary>
        public bool Loop { get => loop; set => loop = value; }
        /// <summary>Whether the script (or recording) has run out (never while looping).</summary>
        public bool Finished { get; private set; }

        /// <summary>Plays a recording's frames, oldest first, from the next step the character takes, instead of the timed steps.</summary>
        public void Play(InputRecording frames)
        {
            recording = frames;
            Restart();
        }

        /// <summary>Starts the script (or recording) again from its beginning, at the next step.</summary>
        public void Restart()
        {
            started = false;
            Finished = false;
            endsWorkedOutFor = -1f;
        }

        public override CharacterInputFrame FrameFor(uint tick, MMotionMotor character)
        {
            if (!started)
            {
                started = true;
                firstTick = tick;
            }
            long index = (long)tick - firstTick;
            var idle = new CharacterInputFrame { tick = tick };
            if (recording != null)
            {
                if (recording.Count == 0) { Finished = true; return idle; }
                if (index >= recording.Count)
                {
                    if (!loop) { Finished = true; return idle; }
                    index %= recording.Count;
                }
                CharacterInputFrame frame = recording[(int)index];
                frame.tick = tick;
                return frame;
            }
            return Timed(index, tick, idle);
        }

        private CharacterInputFrame Timed(long index, uint tick, CharacterInputFrame idle)
        {
            WorkOutEnds();
            int total = stepEnds.Count > 0 ? stepEnds[^1] : 0;
            if (total == 0) { Finished = true; return idle; }
            if (index >= total)
            {
                if (!loop) { Finished = true; return idle; }
                index %= total;
            }
            for (int i = 0; i < stepEnds.Count; i++)
            {
                if (index >= stepEnds[i]) { continue; }
                TimedStep step = steps[i];
                return new CharacterInputFrame { tick = tick, move = step.move, lookYaw = step.lookYaw, lookPitch = step.lookPitch, held = step.held };
            }
            return idle;
        }

        private void WorkOutEnds()
        {
            float stepLength = Time.fixedDeltaTime;
            if (endsWorkedOutFor == stepLength) { return; }
            endsWorkedOutFor = stepLength;
            stepEnds.Clear();
            int end = 0;
            foreach (TimedStep step in steps)
            {
                end += Mathf.RoundToInt(step.seconds / stepLength);
                stepEnds.Add(end);
            }
        }
    }
}
