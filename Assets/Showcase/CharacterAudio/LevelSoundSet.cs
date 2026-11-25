using MeridianMotion.Showcase.Audio;
using UnityEngine;

namespace MeridianMotion.Showcase.CharacterAudio
{
    /// <summary>The level's sounds: checkpoints, respawning, shortcut gates and the course timer (LevelSounds plays them).</summary>
    [CreateAssetMenu(menuName = "Meridian Motion/Audio/Level Sound Set", fileName = "Level Sound Set")]
    public class LevelSoundSet : ScriptableObject
    {
        [Header("Checkpoints")]
        [Tooltip("The player reaching a checkpoint")]
        [SerializeField] private SoundEvent checkpointReached;
        [Tooltip("The player respawned at a checkpoint")]
        [SerializeField] private SoundEvent respawn;

        [Header("Gates")]
        [Tooltip("A shortcut gate opening, where it is")]
        [SerializeField] private SoundEvent gateOpened;

        [Header("Course timer")]
        [Tooltip("A split passed")]
        [SerializeField] private SoundEvent courseSplit;
        [Tooltip("A run finished")]
        [SerializeField] private SoundEvent courseFinished;

        /// <summary>The player reaching a checkpoint.</summary>
        public SoundEvent CheckpointReached { get => checkpointReached; set => checkpointReached = value; }
        /// <summary>The player respawned.</summary>
        public SoundEvent Respawn { get => respawn; set => respawn = value; }
        /// <summary>A gate opening.</summary>
        public SoundEvent GateOpened { get => gateOpened; set => gateOpened = value; }
        /// <summary>A split passed.</summary>
        public SoundEvent CourseSplit { get => courseSplit; set => courseSplit = value; }
        /// <summary>A run finished.</summary>
        public SoundEvent CourseFinished { get => courseFinished; set => courseFinished = value; }
    }
}
