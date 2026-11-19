using System;
using System.IO;
using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// Records the input frames a character is given (see InputRecording), the latest Keep Seconds of them, so when
    /// something goes wrong in play the input that led to it can be saved and replayed: its context menu's Save Recording
    /// (or SaveRecording) writes them to a file under the player's data folder, and logs where. Add it to a character
    /// (beside its MMotionCharacter, which feeds it each step) and play.
    /// </summary>
    [AddComponentMenu("Meridian Motion/Characters/Character Input Recorder")]
    public class CharacterInputRecorder : MonoBehaviour
    {
        [Header("Recording")]
        [Tooltip("How many seconds of input are kept: the latest, the oldest dropped as new steps come.")]
        [Min(1f)] [SerializeField] private float keepSeconds = 300f;

        private InputRecording recording;

        /// <summary>The frames recorded so far (the latest Keep Seconds of them); null before the character first steps.</summary>
        public InputRecording Recording => recording;

        /// <summary>The folder recordings are saved in: Input Recordings, in the player's data folder (Application.persistentDataPath).</summary>
        public static string Folder => Path.Combine(Application.persistentDataPath, "Input Recordings");

        /// <summary>Adds a step's frame (the character's MMotionCharacter calls it each step).</summary>
        public void Record(in CharacterInputFrame frame)
        {
            if (recording == null)
            {
                // Made at the first step, once the tuning it's recorded with is set.
                var movement = GetComponent<MMotionMotor>();
                int capacity = Mathf.CeilToInt(keepSeconds / Time.fixedDeltaTime);
                recording = new InputRecording(capacity, Time.fixedDeltaTime, movement != null ? movement.TuningFingerprint() : 0);
            }
            recording.Add(frame);
        }

        /// <summary>Saves what's been recorded to a new file in Folder (named for the character and the time), and returns its path.</summary>
        [ContextMenu("Save Recording")]
        public string SaveRecording()
        {
            if (recording == null || recording.Count == 0)
            {
                Debug.LogWarning($"[{nameof(CharacterInputRecorder)}] Nothing recorded yet on '{name}': play first.", this);
                return null;
            }
            string path = Path.Combine(Folder, $"{name} {DateTime.Now:yyyy-MM-dd HH-mm-ss}{InputRecording.FileExtension}");
            recording.Save(path);
            Debug.Log($"[{nameof(CharacterInputRecorder)}] Saved {recording.Count} steps of '{name}''s input to {path}", this);
            return path;
        }
    }
}
