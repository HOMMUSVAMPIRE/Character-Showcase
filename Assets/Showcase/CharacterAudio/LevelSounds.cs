using MeridianMotion.Levels;
using MeridianMotion.Showcase.Audio;
using UnityEngine;

namespace MeridianMotion.Showcase.CharacterAudio
{
    /// <summary>
    /// Plays the level's sounds from its elements' events: the player reaching a checkpoint or respawning at one (not an
    /// NPC: the director says who the player is), a shortcut gate opening (where it is), and the course timer's splits and
    /// finishes. It listens while it's enabled and stops as it goes, so a restarted scene doesn't hear the old one's.
    /// Without an audio service in the scene, it does nothing.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Meridian Motion/Audio/Level Sounds")]
    public class LevelSounds : MonoBehaviour
    {
        [Tooltip("The level's sounds")]
        [SerializeField] private LevelSoundSet sounds;
        [Tooltip("The scene's character audio director, which says who the player is")]
        [SerializeField] private CharacterAudioDirector director;

        private CourseTimer[] timers = new CourseTimer[0];

        /// <summary>The level's sounds.</summary>
        public LevelSoundSet Sounds { get => sounds; set => sounds = value; }
        /// <summary>The director, which says who the player is.</summary>
        public CharacterAudioDirector Director { get => director; set => director = value; }

        private void OnEnable()
        {
            Checkpoint.Reached += HandleReached;
            Checkpoint.Respawned += HandleRespawned;
            ShortcutGate.AnyOpened += HandleGateOpened;
            timers = FindObjectsByType<CourseTimer>(FindObjectsSortMode.None);
            foreach (CourseTimer timer in timers)
            {
                timer.SplitPassed += HandleSplit;
                timer.RunFinished += HandleFinished;
            }
        }

        private void OnDisable()
        {
            Checkpoint.Reached -= HandleReached;
            Checkpoint.Respawned -= HandleRespawned;
            ShortcutGate.AnyOpened -= HandleGateOpened;
            foreach (CourseTimer timer in timers)
            {
                if (timer == null) { continue; }
                timer.SplitPassed -= HandleSplit;
                timer.RunFinished -= HandleFinished;
            }
            timers = new CourseTimer[0];
        }

        private bool IsPlayer(IRespawnable who) =>
            director != null && director.Player != null && who is Component component && component != null
            && component.transform == director.Player;

        private void Play(SoundEvent sound, Vector3 position)
        {
            AudioService service = AudioService.Instance;
            if (service != null && sound != null) { service.Play(sound, position); }
        }

        private void HandleReached(IRespawnable who, Checkpoint point)
        {
            if (sounds != null && IsPlayer(who)) { Play(sounds.CheckpointReached, point.transform.position); }
        }

        private void HandleRespawned(IRespawnable who, Checkpoint point)
        {
            if (sounds != null && IsPlayer(who)) { Play(sounds.Respawn, who.Feet); }
        }

        private void HandleGateOpened(ShortcutGate gate)
        {
            if (sounds != null) { Play(sounds.GateOpened, gate.transform.position); }
        }

        private void HandleSplit(int index, float time, float best)
        {
            if (sounds != null) { Play(sounds.CourseSplit, transform.position); }
        }

        private void HandleFinished(CourseRun run)
        {
            if (sounds != null) { Play(sounds.CourseFinished, transform.position); }
        }
    }
}
