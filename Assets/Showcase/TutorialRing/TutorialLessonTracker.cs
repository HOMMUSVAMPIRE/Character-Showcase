using System;
using System.Collections.Generic;
using MeridianMotion.Characters;
using MeridianMotion.Levels;
using UnityEngine;

namespace MeridianMotion.Showcase.TutorialRing
{
    /// <summary>
    /// Which lesson of the Tutorial Ring the player's character is in, and which it has done: on the character (added by
    /// TutorialRingSetup), following its Lesson Zones (LevelPresence's VolumeEntered and VolumeExited) and its movement
    /// events (MMotionMotor.MovementEventRaised).
    /// - Zones are a stack: the one entered last is current, and leaving it goes back to the one under it.
    /// - A lesson's event (TutorialLesson.clearsOn) in its zone does it, once a session; a lesson with none is done on
    ///   leaving its zone.
    /// - Doing it the first time shows its follow-up, if it has one, until the character leaves the zone; otherwise the
    ///   prompt clears. Coming back into a zone shows its prompt again (done or not), cleared by its event as before.
    /// Changed is raised whenever what should be shown changes (Showing, Current). While the scene's ReplayRecorder plays a
    /// replay on the character, it's ignored (the replay's zones and events aren't the player's), and once it stops, the
    /// zones are taken again from where the character is (its LevelPresence).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Meridian Motion/Showcase/Tutorial Lesson Tracker")]
    public class TutorialLessonTracker : MonoBehaviour
    {
        /// <summary>What a lesson's zone is showing.</summary>
        public enum Shown
        {
            /// <summary>Nothing: no lesson, or its prompt cleared.</summary>
            Nothing,
            /// <summary>The lesson's prompt.</summary>
            Prompt,
            /// <summary>The lesson's follow-up, once it's done.</summary>
            FollowUp,
        }

        [Tooltip("The lessons the zones name")]
        [SerializeField] private TutorialLessonCatalog catalog;

        private readonly List<TutorialLesson> stack = new();
        private readonly HashSet<string> completed = new();
        private MMotionMotor motor;
        private ReplayRecorder replays;
        private bool replaying;

        /// <summary>Raised when the character comes into a lesson's zone (it becomes Current).</summary>
        public event Action<TutorialLesson> Entered;
        /// <summary>Raised when the character leaves a lesson's zone.</summary>
        public event Action<TutorialLesson> Left;
        /// <summary>Raised the first time a lesson is done, this session.</summary>
        public event Action<TutorialLesson> Completed;
        /// <summary>Raised whenever what should be shown changes (Current, Showing).</summary>
        public event Action Changed;

        /// <summary>The lessons the zones name.</summary>
        public TutorialLessonCatalog Catalog { get => catalog; set => catalog = value; }
        /// <summary>The lesson whose zone the character came into last (and is still in), or null.</summary>
        public TutorialLesson Current => stack.Count > 0 ? stack[^1] : null;
        /// <summary>The lesson whose zone the character was in last, still in it or not (null before any).</summary>
        public TutorialLesson Last { get; private set; }
        /// <summary>What the current lesson is showing.</summary>
        public Shown Showing { get; private set; }
        /// <summary>The ids of the lessons done this session.</summary>
        public IReadOnlyCollection<string> CompletedIds => completed;
        /// <summary>The character it follows.</summary>
        public MMotionMotor Character => motor;

        /// <summary>Whether a lesson's been done this session.</summary>
        public bool IsCompleted(string lessonId) => completed.Contains(lessonId);

        /// <summary>Follows this character's zones and events (its own, on the same object, by default).</summary>
        public void Follow(MMotionMotor character)
        {
            Unfollow();
            motor = character;
            if (motor == null) { return; }
            replays = FindAnyObjectByType<ReplayRecorder>();
            motor.MovementEventRaised += OnMovementEvent;
            LevelPresence.VolumeEntered += OnVolumeEntered;
            LevelPresence.VolumeExited += OnVolumeExited;
        }

        private void Unfollow()
        {
            if (motor != null) { motor.MovementEventRaised -= OnMovementEvent; }
            LevelPresence.VolumeEntered -= OnVolumeEntered;
            LevelPresence.VolumeExited -= OnVolumeExited;
            motor = null;
        }

        private void Start()
        {
            if (motor == null && TryGetComponent(out MMotionMotor own)) { Follow(own); }
        }

        private void OnDestroy() => Unfollow();

        /// <summary>Whether a replay is playing on the character (what happens then isn't the player's doing).</summary>
        private bool Replaying => replays != null && replays.IsPlaying;

        private void Update()
        {
            bool now = Replaying;
            if (replaying && !now) { Resync(); }
            replaying = now;
        }

        /// <summary>
        /// Takes the lesson zones again from where the character is (its LevelPresence, put back by a replay's keyframes
        /// without their zones being entered or left), each showing its prompt; nothing's done by it.
        /// </summary>
        public void Resync()
        {
            stack.Clear();
            if (motor != null && catalog != null)
            {
                foreach (LevelVolume volume in motor.levelPresence.inside)
                {
                    TutorialLesson lesson = volume is LessonZone zone ? catalog.Find(zone.LessonId) : null;
                    if (lesson != null) { stack.Add(lesson); }
                }
            }
            if (stack.Count > 0) { Last = stack[^1]; }
            Showing = stack.Count > 0 ? Shown.Prompt : Shown.Nothing;
            Changed?.Invoke();
        }

        private void OnVolumeEntered(IRespawnable who, LevelVolume volume)
        {
            if (!ReferenceEquals(who, motor) || Replaying || volume is not LessonZone zone) { return; }
            Enter(zone.LessonId);
        }

        private void OnVolumeExited(IRespawnable who, LevelVolume volume)
        {
            if (!ReferenceEquals(who, motor) || Replaying || volume is not LessonZone zone) { return; }
            Leave(zone.LessonId);
        }

        private void OnMovementEvent(MovementEvents raised, MovementStep step)
        {
            if (!Replaying) { Heard(raised, step != null ? step.state : CharacterState.None); }
        }

        /// <summary>The character came into a lesson's zone (by its id): it's current, showing its prompt.</summary>
        public void Enter(string lessonId)
        {
            TutorialLesson lesson = catalog != null ? catalog.Find(lessonId) : null;
            if (lesson == null)
            {
                Debug.LogWarning($"[TutorialLessonTracker] A lesson zone names '{lessonId}', which isn't in the catalog.", this);
                return;
            }
            stack.Remove(lesson);
            stack.Add(lesson);
            Last = lesson;
            Showing = Shown.Prompt;
            Entered?.Invoke(lesson);
            Changed?.Invoke();
        }

        /// <summary>The character left a lesson's zone (by its id): the one under it (if any) is current again, showing its prompt.</summary>
        public void Leave(string lessonId)
        {
            int at = stack.FindLastIndex(l => l.id == lessonId);
            if (at < 0) { return; }
            TutorialLesson lesson = stack[at];
            bool wasCurrent = at == stack.Count - 1;
            stack.RemoveAt(at);
            if (lesson.ClearingEvent.IsEmpty) { Complete(lesson); }
            Left?.Invoke(lesson);
            if (wasCurrent) { Showing = stack.Count > 0 ? Shown.Prompt : Shown.Nothing; }
            Changed?.Invoke();
        }

        /// <summary>A movement event was raised (in the character's state then): it may do the current lesson.</summary>
        public void Heard(MovementEvents raised, CharacterState state)
        {
            TutorialLesson lesson = Current;
            if (lesson == null || Showing != Shown.Prompt || !lesson.ClearedBy(raised, state)) { return; }
            bool first = Complete(lesson);
            Showing = first && !string.IsNullOrEmpty(lesson.followUp) ? Shown.FollowUp : Shown.Nothing;
            Changed?.Invoke();
        }

        private bool Complete(TutorialLesson lesson)
        {
            if (!completed.Add(lesson.id)) { return false; }
            Completed?.Invoke(lesson);
            return true;
        }
    }
}
