# Replays
A scene with a **Replay Recorder** records every play session of the player's character. Save the run as a replay file, send it in, and play it back to watch exactly what happened: what playtests are for. The pause menu has a **Replays** tab while the scene has a recorder (and only then): Save Replay and Load Replay.

*See also:* [UI Framework](ui.md#the-pause-menu), [Testing](testing.md), [Architecture](architecture.md#what-may-know-about-what).

## Adding it to a scene
**Tools > Meridian Motion > Add Replay Recorder** adds the Replay Recorder prefab (Assets/MeridianMotion/Content/UI) to the open scene. Set Up Scene adds it to the showcase's scenes by itself. From the command line:

```
<Unity> -batchmode -projectPath . -executeMethod MeridianMotion.UI.EditorTools.UISceneSetup.AddReplayRecorderFromCommandLine -scene <path> [-scene <path> ...]
```

The prefab is two components: `ReplayRecorder` (the character's side: it records and plays) and `ReplayMenu` (the pause menu's Replays tab and the dialog at a replay's end).

## Recording
From the player's character's first step, the recorder keeps two things:
- **Each step's input frame:** what the movement was given.
- **A keyframe every Keyframe Seconds:** the character's whole movement state and the level's moving parts' (ropes, pushables), and the level's clock. The first keyframe is the run's start.

Nothing goes to a file until it's saved. A recording that reaches the recorder's Max Minutes stops, keeping its start, until it's saved.

**Save Replay** writes the run to a replay file (`[scene] [date time].mmreplay`) in the Replays folder of the player's data folder, says where in a notification, and starts a new recording. The data folder is Unity's `Application.persistentDataPath`:
- **Windows:** `%USERPROFILE%\AppData\LocalLow\<company>\<product>\Replays`
- **macOS:** `~/Library/Application Support/<company>/<product>/Replays`
- **Linux:** `~/.config/unity3d/<company>/<product>/Replays`

**Playtests save themselves.** In a build, quitting from the pause menu saves the run first if nothing's been saved this session, and so does Load Replay. (`ReplayMenu`'s Save On Quit: In Builds by default, or Always, or Never.) A tester who plays and quits leaves a replay; ask them to send the Replays folder. Closing the window some other way (Alt+F4, the dock) doesn't save.

## Playing a replay
**Load Replay** plays the newest replay saved for the scene: put a tester's file in your own Replays folder to watch it. The pause menu closes, and then:
- the character is put back where the run began, and the level's moving parts as they were;
- the level's clock is offset, so the moving platforms are where they were;
- the character is driven by the recorded frames. The player's input no longer reaches it, but the camera is still yours to move.

At the end, a dialog offers three choices:
- **Continue playing:** from where the replay ended, the character's yours again, and a new recording starts.
- **Replay again:** from its start.
- **Reset level:** the scene again, as the pause menu's Restart does.

**On another build.** The movement is deterministic on one build: the same state and frames give the same steps there (see [Decisions](decisions.md)). On another build, or a tester's build played in the editor, it may drift a little between keyframes, so each keyframe puts the character back exactly where it was. A replay recorded with different movement tuning plays (and logs a warning), its keyframes keeping it on its path.

**What a replay doesn't put back:** the NPCs (they walk their routes as they will), the course timers (which time the replayed run afresh as it crosses their gates), and the camera.

## From code
- `ReplayRecorder`:
  - `Save()` returns the file's path;
  - `PlayFile(path)` or `Play(replay)`, then `PlaybackFinished`;
  - `StopPlayback()` gives the character back, and starts recording, by default;
  - `PlayAgain()`;
  - `StartRecording()` and `StopRecording()`.
- `Replay`, the file:
  - `Load` and `Save`;
  - its `Keyframes` and `Frames`, its `Scene` and `Build`;
  - `Replay.LatestFor(scene)`.

The UI knows nothing of replays. It shows the Replays tab while a game has registered replay controls (`ReplayControls.Register`, an `IReplayControls`), which `ReplayMenu`, in the bridge assembly `MeridianMotion.CharacterUI`, does.
