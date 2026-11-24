using System;
using System.IO;
using MeridianMotion.Levels;
using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// Records the local player's run in the scene, and plays a recorded run back (a Replay). Put it in a scene (the Replay
    /// Recorder prefab); it records the player's character (its own Character, else the spawner's player) from its first
    /// step: each step's input frame, and a keyframe of its state and the level's every Keyframe Seconds (the first at
    /// the start). Nothing's written to a file until Save, which also starts a new recording. Play puts the character and
    /// the level back as the replay began and drives the character with its frames (the player's input stops reaching
    /// it; the camera's is still the player's), snapping to each keyframe as it comes, then raises PlaybackFinished; the
    /// character stands idle until StopPlayback gives it back to the player (and records from there) or PlayAgain.
    /// </summary>
    [AddComponentMenu("Meridian Motion/Characters/Replay Recorder")]
    public class ReplayRecorder : MonoBehaviour
    {
        [Tooltip("The character recorded and replayed (none: the scene's spawner's player character, when it spawns)")]
        [SerializeField] private MMotionCharacter character;
        [Tooltip("Whether it records from the character's first step (else from StartRecording)")]
        [SerializeField] private bool recordOnStart = true;
        [Tooltip("How often (seconds) a keyframe of the character's and the level's state is kept, which a replay snaps to (so it stays on its path on another build)")]
        [Min(0.1f)] [SerializeField] private float keyframeSeconds = 2f;
        [Tooltip("The longest a recording runs (minutes): at it, the recording stops, keeping its start")]
        [Min(1f)] [SerializeField] private float maxMinutes = 60f;

        private Replay recording;
        private bool recordingWanted;
        private bool warnedFull;
        private Replay playing;
        private int playedFrames;
        private bool finishedPending, finished;
        private ScriptedInputSource source;

        /// <summary>Whether a replay's been saved this run of the game (any scene): reset as the game starts.</summary>
        public static bool SavedThisSession { get; private set; }
        /// <summary>The path of the last replay saved this run, or null.</summary>
        public static string LastSavedPath { get; private set; }

        /// <summary>Raised when a replay's played to its end (the character then stands idle: see StopPlayback).</summary>
        public event Action<ReplayRecorder> PlaybackFinished;
        /// <summary>Raised with the path of a replay it's saved.</summary>
        public event Action<string> Saved;

        /// <summary>The character recorded and replayed (null until there is one).</summary>
        public MMotionCharacter Character => character;
        /// <summary>Whether it's recording (wants to, and isn't playing).</summary>
        public bool IsRecording => recordingWanted && playing == null;
        /// <summary>Whether a replay's playing (or has played to its end and not been stopped).</summary>
        public bool IsPlaying => playing != null;
        /// <summary>Whether the replay playing has reached its end.</summary>
        public bool PlaybackComplete => playing != null && finished;
        /// <summary>The replay playing, or null.</summary>
        public Replay Playing => playing;
        /// <summary>How many steps the recording under way holds.</summary>
        public int RecordedFrames => recording?.Frames?.Count ?? 0;
        /// <summary>How long the recording under way is, in seconds.</summary>
        public float RecordedSeconds => recording?.Seconds ?? 0f;
        /// <summary>The scene's name, as a replay records it.</summary>
        public string SceneName => gameObject.scene.name;
        /// <summary>How many steps the replay playing has stepped.</summary>
        public int PlayedFrames => playedFrames;
        /// <summary>What drives the character while a replay plays (its frames), or null before the first Play.</summary>
        public ICharacterInputSource PlaybackSource => source;
        /// <summary>The keyframe interval, in seconds.</summary>
        public float KeyframeSeconds { get => keyframeSeconds; set => keyframeSeconds = Mathf.Max(0.1f, value); }

        /// <summary>Forgets that anything's been saved this run (SavedThisSession, LastSavedPath), as when the game starts: for tests.</summary>
        public static void ForgetSessionSaves() => ResetSession();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            SavedThisSession = false;
            LastSavedPath = null;
        }

        private void Awake() => recordingWanted = recordOnStart;

        private void Start()
        {
            if (character != null)
            {
                Attach(character);
                return;
            }
            var spawner = FindAnyObjectByType<CharacterSpawner>();
            if (spawner == null) { return; }
            spawner.CharacterSpawned += OnSpawned;
            if (spawner.PlayerCharacter != null) { OnSpawned(spawner.PlayerCharacter); }
        }

        private void OnDestroy()
        {
            var spawner = FindAnyObjectByType<CharacterSpawner>();
            if (spawner != null) { spawner.CharacterSpawned -= OnSpawned; }
            Detach();
            ReplayClock.Remove();
        }

        private void Update()
        {
            if (!finishedPending) { return; }
            finishedPending = false;
            PlaybackFinished?.Invoke(this);
        }

        /// <summary>Records (or plays) this character from now: what Start does with its Character, or the spawner's player.</summary>
        public void Attach(MMotionCharacter target)
        {
            if (target == character && target != null && attached) { return; }
            Detach();
            character = target;
            if (character == null) { return; }
            character.BeforeStep += OnBeforeStep;
            attached = true;
        }

        private bool attached;

        private void Detach()
        {
            if (character != null && attached) { character.BeforeStep -= OnBeforeStep; }
            attached = false;
        }

        private void OnSpawned(GameObject spawned)
        {
            var target = spawned != null ? spawned.GetComponent<MMotionCharacter>() : null;
            if (target != null && target.ControlledBy == ControlledBy.LocalPlayer) { Attach(target); }
        }

        // ---- Recording ----

        /// <summary>Starts a new recording, from the character's next step (the one under way is dropped).</summary>
        public void StartRecording()
        {
            recording = null;
            warnedFull = false;
            recordingWanted = true;
        }

        /// <summary>Stops recording (what's recorded is kept, for Save).</summary>
        public void StopRecording() => recordingWanted = false;

        /// <summary>The recording under way, as a replay (null if nothing's recorded).</summary>
        public Replay CurrentRun()
        {
            if (recording == null || recording.Frames == null || recording.Frames.Count == 0) { return null; }
            recording.Recorded = DateTime.UtcNow;
            return recording;
        }

        /// <summary>
        /// Saves the recording under way as a replay (to path; else a new file in Replay.Folder, named for the scene and
        /// the time) and starts a new one. Returns the path, or null if there was nothing to save.
        /// </summary>
        public string Save(string path = null)
        {
            Replay run = CurrentRun();
            if (run == null) { return null; }
            path ??= Replay.NewPath(SceneName);
            run.Save(path);
            SavedThisSession = true;
            LastSavedPath = path;
            Debug.Log($"[{nameof(ReplayRecorder)}] Saved {run.Seconds:0.0} s of '{SceneName}' to {path}", this);
            if (playing == null) { StartRecording(); }
            Saved?.Invoke(path);
            return path;
        }

        // ---- Playback ----

        /// <summary>The newest replay saved for this scene (in Replay.Folder), or null.</summary>
        public string LatestReplayPath() => Replay.LatestFor(SceneName);

        /// <summary>Plays the replay file at path (see Play). False if there's no character, or it can't be read.</summary>
        public bool PlayFile(string path)
        {
            Replay replay;
            try { replay = Replay.Load(path); }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException)
            {
                Debug.LogError($"[{nameof(ReplayRecorder)}] Couldn't read the replay '{path}': {exception.Message}", this);
                return false;
            }
            return Play(replay);
        }

        /// <summary>
        /// Plays a replay: the recording under way stops (and is dropped), the character and the level are put back as at
        /// its first keyframe, the level's clock is set to its tick, and the character's driven by its frames. False if
        /// there's no character, or the replay has no keyframe or no frames.
        /// </summary>
        public bool Play(Replay replay)
        {
            if (character == null || character.Motor == null || replay == null || replay.Keyframes.Count == 0 || replay.Frames == null || replay.Frames.Count == 0)
            {
                Debug.LogWarning($"[{nameof(ReplayRecorder)}] Nothing to play" + (character == null ? ": there's no character to play it on." : ": the replay's empty."), this);
                return false;
            }
            if (replay.Scene != SceneName) { Debug.LogWarning($"[{nameof(ReplayRecorder)}] This replay was recorded in '{replay.Scene}', not '{SceneName}': it may not play true.", this); }
            if (replay.Frames.Tuning != 0 && replay.Frames.Tuning != character.Motor.TuningFingerprint())
            {
                Debug.LogWarning($"[{nameof(ReplayRecorder)}] This replay was recorded with other movement tuning: its keyframes keep it close, but between them it may differ.", this);
            }
            recording = null;
            playing = replay;
            playedFrames = 0;
            finished = finishedPending = false;
            Replay.Keyframe start = replay.Keyframes[0];
            Restore(start);
            ReplayClock.Install(start.LevelTick);
            if (source == null) { source = gameObject.AddComponent<ScriptedInputSource>(); }
            source.Play(replay.Frames);
            character.InputSource = source;
            return true;
        }

        /// <summary>Plays the replay playing (or last played) again from its start.</summary>
        public bool PlayAgain() => playing != null && Play(playing);

        /// <summary>
        /// Stops a replay: the character's the player's again, where it is, and (recordAfter) a new recording starts from
        /// there.
        /// </summary>
        public void StopPlayback(bool recordAfter = true)
        {
            if (playing == null) { return; }
            playing = null;
            finished = finishedPending = false;
            if (character != null && ReferenceEquals(character.InputSource, source)) { character.InputSource = null; }
            if (recordAfter) { StartRecording(); }
            else { recordingWanted = false; }
        }

        // ---- Each step ----

        private void OnBeforeStep(MMotionCharacter stepping, in CharacterInputFrame frame)
        {
            if (playing != null) { StepPlayback(); }
            else if (recordingWanted) { StepRecording(frame); }
        }

        private void StepPlayback()
        {
            if (finished) { return; }
            if (playedFrames >= playing.Frames.Count)
            {
                finished = finishedPending = true;
                return;
            }
            if (playedFrames == 0) { ReplayClock.SyncTo(playing.Keyframes[0].LevelTick); }
            else
            {
                Replay.Keyframe keyframe = KeyframeAt(playing, playedFrames);
                if (keyframe != null)
                {
                    Restore(keyframe);
                    ReplayClock.SyncTo(keyframe.LevelTick);
                    // The keyframe's the recorded character's own state, its input source (the player's: none) with it.
                    character.InputSource = source;
                }
            }
            playedFrames++;
        }

        private void StepRecording(in CharacterInputFrame frame)
        {
            if (recording == null)
            {
                int capacity = Mathf.CeilToInt(maxMinutes * 60f / Time.fixedDeltaTime);
                recording = new Replay
                {
                    Scene = SceneName,
                    Build = Replay.ThisBuild,
                    Frames = new InputRecording(capacity, Time.fixedDeltaTime, character.Motor.TuningFingerprint()),
                };
                recording.Keyframes.Add(Capture(0));
            }
            InputRecording frames = recording.Frames;
            if (frames.Count >= frames.Capacity)
            {
                if (!warnedFull) { Debug.LogWarning($"[{nameof(ReplayRecorder)}] The recording reached its Max Minutes and stopped: save it, and a new one starts.", this); }
                warnedFull = true;
                return;
            }
            int every = Mathf.Max(1, Mathf.RoundToInt(keyframeSeconds / Time.fixedDeltaTime));
            if (frames.Count > 0 && frames.Count % every == 0) { recording.Keyframes.Add(Capture(frames.Count)); }
            frames.Add(frame);
        }

        private Replay.Keyframe Capture(int frame) => new()
        {
            Frame = frame,
            LevelTick = LevelClock.Tick,
            Character = StateSerializer.WriteCharacter(character.Motor),
            World = StateSerializer.WriteWorld(),
        };

        private void Restore(Replay.Keyframe keyframe)
        {
            if (keyframe.World != null && keyframe.World.Length > 0) { StateSerializer.ReadWorld(keyframe.World); }
            StateSerializer.ReadCharacter(character.Motor, keyframe.Character);
        }

        private static Replay.Keyframe KeyframeAt(Replay replay, int frame)
        {
            foreach (Replay.Keyframe keyframe in replay.Keyframes)
            {
                if (keyframe.Frame == frame) { return keyframe; }
                if (keyframe.Frame > frame) { break; }
            }
            return null;
        }
    }
}
