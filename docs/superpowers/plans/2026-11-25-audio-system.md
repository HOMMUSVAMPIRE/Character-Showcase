# Audio System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Looping music per scene, sound effects for the character's interactions with the level, and UI sounds, on a
pooled audio core that knows nothing of Meridian Motion and is ready for netcode later.

**Architecture:** A core assembly (`MeridianMotion.Showcase.Audio`, no references) plays `SoundRequest` values through
one `AudioService` with a fixed voice pool, buses and filters, plus a crossfading `MusicPlayer`. Two bridges turn the
game's events into requests: `MeridianMotion.Showcase.CharacterAudio` (the motor's events and state, the water's splashes,
the level's checkpoints and timers, surfaces from tags, the grass and materials) and `MeridianMotion.Showcase.UIAudio`
(generic hooks the UI framework gains). Editor tools make the assets (empty slots) and add it all in Set Up Scene.

**Tech Stack:** Unity (version from `ProjectSettings/ProjectVersion.txt`), built-in `AudioSource`/`AudioClip` only, uGUI,
NUnit PlayMode tests on `CharacterTestRig`.

**Spec:** `docs/superpowers/specs/2026-11-25-audio-system-design.md` (read it first; this plan argues from it).

## Global Constraints

- Everything lives in `Assets/Showcase` except the UI hooks (Task 11) and `TestArea.Audio` (Task 1). Nothing that ships
  references a showcase assembly.
- `MeridianMotion.Showcase.Audio` references nothing. `MeridianMotion.Showcase.CharacterAudio` references only
  MeridianMotion.Showcase.Audio, MeridianMotion.Characters, MeridianMotion.Levels, MeridianMotion.Showcase.Grass.
  `MeridianMotion.Showcase.UIAudio` references only MeridianMotion.Showcase.Audio, MeridianMotion.UI, UnityEngine.UI.
  The full table is the spec's Assemblies section; every assembly goes into AGENTS.md's assembly table and
  `Tools/CI/check_project_hygiene.py`'s `ALLOWED_REFERENCES` in the same task that creates it.
- Namespaces match assemblies. Components: `[AddComponentMenu("Meridian Motion/Audio/<Name>")]`. Assets:
  `[CreateAssetMenu(menuName = "Meridian Motion/Audio/<Name>")]`. Tools: Tools > Meridian Motion > Showcase > ....
- `[Tooltip]` on every serialized field (written for a designer), grouped with `[Header]`; `/// <summary>` on classes and
  non-trivial methods; comments say why.
- No allocation per sound or per frame in the service, the pool, the bridges or the relays (reuse arrays; structs).
- Audio never writes to the movement, the level or the UI; its randomness is its own `System.Random`.
- Every new file and folder under `Assets/` gets a `.meta` with a fresh GUID (`python3 -c "import uuid; print(uuid.uuid4().hex)"`), copied from a neighbour of the same type.
- Every PlayMode fixture has `[TestTiming]` and `[Category(TestArea.Audio)]` (UI hook tests in the package:
  `TestArea.UI`). Character fixtures derive from `CharacterRigFixture`.
- Docs never quote a value that lives in a setting: they name the setting and its asset.
- Git: work on the current branch (`AudioVisualPolish`), never `main`. **Don't commit or push unless the user asks.**
  Each task ends with a checkpoint instead.
- Verification: `python3 Tools/CI/check_project_hygiene.py` must pass at every checkpoint. Unity tests:
  `python3 Tools/run_tests.py --area Audio` (and the areas a task names), with the editor closed. If Unity can't be run,
  say so; don't call untested code working.

## Review Focus

1. **A character despawned (or its object destroyed) while a loop plays:** the loop stops at once, its handle goes stale,
   nothing logs. Test in Task 8.
2. **A scene with no `AudioService`** (the package's tests, a scene not set up): `CharacterAudio`, `LevelSounds`,
   `UISounds`, `SoundEmitter` and `SceneMusic` do nothing and log nothing. Tests in Tasks 5, 8, 12.
3. **Many characters at once** (NPC crowds) exhausting the pool: lower-priority sounds drop or are stolen, never an
   error, and `Play` allocates nothing. Tests in Task 2.
4. **Pausing mid-sound:** effects and effect loops pause and resume where they were; music and UI sounds go on while
   paused (UI's bus doesn't pause with the game). Tests in Tasks 2 and 12.
5. **Restarting the scene** (the pause menu's Restart reloads it): no listener left on a static event
   (`RootUIPrefab.AnyOpened`, `Checkpoint.Reached`, ...) from the old scene, so nothing plays twice. Tests in Tasks 10 and 12.

---

## File Structure

```
Assets/Showcase/Audio/                      MeridianMotion.Showcase.Audio (core)
  MeridianMotion.Showcase.Audio.asmdef
  SoundRequest.cs        SoundId, EmitterId, SoundFlags, SoundRequest
  SoundHandle.cs         SoundHandle
  SoundEvent.cs          SoundEvent (asset), ClipOrder, Spatial
  ClipPicker.cs          choosing a clip without repeating
  AudioBus.cs            AudioBus (asset)
  SoundLibrary.cs        SoundLibrary (asset)
  ISoundFilter.cs        ISoundFilter
  VoicePool.cs           Voice, VoicePool
  SoundRateLimiter.cs    per-sound cooldown and instance counts
  AudioVolumes.cs        saved bus volumes
  AudioService.cs        AudioService (+ AudioService.Voices.cs for fades, pause, the per-frame tick)
  MusicTrack.cs          MusicTrack (asset)
  MusicPlayer.cs         MusicPlayer
  SceneMusic.cs          SceneMusic
  SoundEmitter.cs        SoundEmitter
  Sounds/ Music/ Buses/  (made by Create Audio Assets, Task 13)
Assets/Showcase/CharacterAudio/             MeridianMotion.Showcase.CharacterAudio (the bridge)
  SoundSurface.cs  SurfaceTag.cs  SurfaceMap.cs  SurfaceFinder.cs
  CharacterSoundSet.cs   EventSound, StateLoop, FootstepSettings, SurfaceSound, StrengthFrom
  CharacterAudio.cs      (+ CharacterAudio.Loops.cs, CharacterAudio.Footsteps.cs)
  FootstepCadence.cs
  CharacterAudioDirector.cs
  LevelSounds.cs  LevelSoundSet.cs
Assets/Showcase/UIAudio/                    MeridianMotion.Showcase.UIAudio
  UISoundSet.cs  UISounds.cs  UISoundRelay.cs
Assets/Showcase/Editor/Audio/               MeridianMotion.Showcase.Audio.EditorTools
  AudioAssets.cs  AudioAssets.Content.cs  SoundEventEditor.cs  SoundLibraryEditor.cs  SoundFlagNameDrawer.cs  AudioSceneSetup.cs
Assets/Showcase/Tests/Audio/                MeridianMotion.Showcase.Tests.Audio (PlayMode)
  AudioTestKit.cs  SoundLibraryTests.cs  AudioServiceTests.cs  AudioVolumeTests.cs  MusicPlayerTests.cs
  SoundEmitterTests.cs  SurfaceTests.cs  CharacterAudioTests.cs  FootstepTests.cs  DirectorAndLevelTests.cs
  UISoundTests.cs  AudioAssetsTests.cs  AudioReadsOnlyTests.cs
Modified:
  Assets/Showcase/Grass/GrassField.cs (+ GrassField.Cover.cs)       Task 6
  Assets/Showcase/Tests/Grass/GrassCoverTests.cs (new)              Task 6
  Assets/MeridianMotion/Tests/Shared/TestArea.cs                     Task 1
  Assets/MeridianMotion/Runtime/UI/RootUIPrefab.cs, UIManager.Back.cs, Notifications/Notifications.cs   Task 11
  Assets/MeridianMotion/Tests/PlayMode/UI/UIHookTests.cs (new)        Task 11
  Tools/CI/check_project_hygiene.py, Tools/run_tests.py, AGENTS.md, .gitattributes, docs/   as each task says
```

---

### Task 1: The core's values and assets

**Files:**
- Create: `Assets/Showcase/Audio/MeridianMotion.Showcase.Audio.asmdef` (`references: []`, `autoReferenced: true`, as `MeridianMotion.Showcase.Grass.asmdef`)
- Create: `Assets/Showcase/Audio/SoundRequest.cs`, `SoundHandle.cs`, `SoundEvent.cs`, `ClipPicker.cs`, `AudioBus.cs`, `SoundLibrary.cs`, `ISoundFilter.cs`
- Create: `Assets/Showcase/Tests/Audio/MeridianMotion.Showcase.Tests.Audio.asmdef` (as `MeridianMotion.Showcase.Tests.Grass.asmdef`: test runner, nunit, `UNITY_INCLUDE_TESTS`; references for now MeridianMotion.Showcase.Audio and MeridianMotion.Tests.Shared only: each later task adds the assemblies it tests, until it matches the spec's table)
- Create: `Assets/Showcase/Tests/Audio/AudioTestKit.cs`, `SoundLibraryTests.cs`
- Modify: `Assets/MeridianMotion/Tests/Shared/TestArea.cs` (add `public const string Audio = "Audio";` with a summary: "The showcase's audio: its core, the character's and the UI's sounds.")
- Modify: `Tools/CI/check_project_hygiene.py` `ALLOWED_REFERENCES` (both new assemblies), `Tools/run_tests.py` `CHANGE_RULES` (`("Assets/Showcase/Audio/", ("Audio",), "the audio core")`), AGENTS.md (folder table row, assembly table rows)
- Modify: `.gitattributes`: `Assets/Showcase/Audio/**/*.wav`, `*.ogg`, `*.mp3` with `filter=lfs diff=lfs merge=lfs -text`

**Interfaces:**
- Produces (namespace `MeridianMotion.Showcase.Audio`):
  - `readonly struct SoundId : IEquatable<SoundId> { int Value; bool IsNone => Value == 0; static SoundId None; }`
  - `readonly struct EmitterId : IEquatable<EmitterId> { int Value; static EmitterId None; }`
  - `[Flags] enum SoundFlags : byte { None = 0, Follow = 1, LocalOnly = 2 }`
  - `struct SoundRequest { SoundId sound; Vector3 position; float strength; float pitchScale; EmitterId emitter; SoundFlags flags; static SoundRequest At(SoundId, Vector3, float strength = 1f) }` (pitchScale defaults to 1 in `At`)
  - `enum ClipOrder { RandomNoRepeat, Random, InOrder }`, `enum Spatial { TwoD, ThreeD }`
  - `class SoundEvent : ScriptableObject` with serialized `clips` (`AudioClip[]`), `order`, `volume` (`Vector2`, 0..1, default (0.9,1)), `pitch` (`Vector2`, default (0.95,1.05)), `bus` (`AudioBus`), `spatial` (default ThreeD), `minDistance` (1), `maxDistance` (30), `loop` (false), `maxInstances` (4), `cooldown` (seconds, 0.05), `priority` (0..255, 128), `id` (int, `[HideInInspector]`-free but read-only in the inspector: Task 13's editor). Properties for each; `SoundId Id`; `bool HasClips`.
  - `sealed class ClipPicker { ClipPicker(int seed); AudioClip Pick(SoundEvent sound); }` (keeps the last index per sound in a `Dictionary<SoundEvent,int>` sized once; InOrder cycles)
  - `class AudioBus : ScriptableObject` with `busName`, `defaultVolume` (0..1, 1), `pausesWithGame` (bool), `mixerGroup` (`AudioMixerGroup`, optional)
  - `class SoundLibrary : ScriptableObject` with `sounds` (`SoundEvent[]`), `buses` (`AudioBus[]`); `SoundEvent Find(SoundId id)` (dictionary built on first use), `IReadOnlyList<AudioBus> Buses`, `int NextFreeId()` (max id + 1, for the editor)
  - `interface ISoundFilter { bool Allow(ref SoundRequest request); }`

- [ ] **Step 1: Write the failing tests** in `SoundLibraryTests.cs` (`[Category(TestArea.Audio)] [TestTiming]`), with `AudioTestKit` making assets in code (`AudioTestKit.Sound(int id, int clips = 1, bool loop = false)`, using `AudioClip.Create("t", 4410, 1, 44100, false)`, and `AudioTestKit.Bus(string name, bool pausesWithGame)`; destroys what it made on `Dispose`):
  - `Find_ReturnsTheSoundWithThatId_AndNullForNoneOrUnknown`: library of ids 3 and 7: `Find(new SoundId(7))` is that asset; `Find(SoundId.None)` and `Find(new SoundId(9))` are null.
  - `NextFreeId_IsOneMoreThanTheHighest`: ids 3, 7 → 8; empty → 1.
  - `RandomNoRepeat_NeverPicksTheSameClipTwiceInARow`: 3 clips, 200 picks, no two neighbours equal, every clip picked.
  - `InOrder_CyclesThroughTheClips`: picks are 0,1,2,0,1.
  - `ASoundWithNoClips_PicksNull`: `Pick` returns null; `HasClips` false.
  - `SoundRequestAt_HasPitchScaleOne`: `SoundRequest.At(...)`'s `pitchScale` is 1.
- [ ] **Step 2: Run them to see them fail:** `python3 Tools/run_tests.py --filter SoundLibraryTests` → compile errors (types missing).
- [ ] **Step 3: Write the types above** in their files.
- [ ] **Step 4: Run them to see them pass:** same command → all PASS.
- [ ] **Step 5: Checkpoint:** `python3 Tools/CI/check_project_hygiene.py` → "Project hygiene checks passed." (it checks the new asmdefs against `ALLOWED_REFERENCES`, the fixture's `[TestTiming]` and area, the metas).

### Task 2: The audio service and its voice pool

**Files:**
- Create: `Assets/Showcase/Audio/VoicePool.cs`, `SoundRateLimiter.cs`, `AudioService.cs`, `AudioService.Voices.cs`
- Test: `Assets/Showcase/Tests/Audio/AudioServiceTests.cs`

**Interfaces:**
- Consumes: Task 1's types.
- Produces:
  - `readonly struct SoundHandle { bool IsValid; static SoundHandle None; }` (index + generation; in `SoundHandle.cs`)
  - `class AudioService : MonoBehaviour` (`[AddComponentMenu("Meridian Motion/Audio/Audio Service")]`, `[DisallowMultipleComponent]`):
    - serialized `library` (`SoundLibrary`), `voiceCount` (int, `[Min(4)]`, 32), `seed` (int, 0 = from the clock)
    - `static AudioService Instance` (set in `OnEnable`, cleared in `OnDisable` if it's this)
    - `SoundHandle Play(in SoundRequest request)`; `SoundHandle Play(SoundEvent sound, Vector3 position, float strength = 1f, EmitterId emitter = default)`
    - `void Stop(SoundHandle handle, float fadeSeconds = 0f)`; `bool IsPlaying(SoundHandle handle)`; `void SetPosition(SoundHandle handle, Vector3 position)`; `void SetStrength(SoundHandle handle, float strength)` (for loops whose volume follows speed)
    - `void AddFilter(ISoundFilter filter)`; `void RemoveFilter(ISoundFilter filter)`
    - `event Action<SoundRequest> Played` (raised for each request that got a voice; for tests and later netcode)
    - `int PlayingCount` (voices in use), `SoundLibrary Library { get; set; }`
    - `Transform Listener { get; set; }` (null: finds the scene's `AudioListener` once, again only if it's gone)
  - `VoicePool`: `VoicePool(Transform parent, int count)` makes `count` child `AudioSource`s (`playOnAwake = false`); `bool TryAcquire(int priority, out int index)` (a free voice, else the lowest-priority, then oldest, voice whose priority is ≤ `priority`, its generation bumped; else false); `void Release(int index)`.
  - `SoundRateLimiter`: `bool Allows(SoundEvent sound, float now, int playing)` (false inside `cooldown` of the last start, or when `playing >= maxInstances`); `void Started(SoundEvent sound, float now)`.

`Play` order: request's sound resolved (`library.Find`, or the `SoundEvent` overload's own) → no clips: return `None`
silently → filters (`Allow`, in order; any false: `None`) → 3D and listener farther than `maxDistance`: `None` → rate
limiter → pool → set the source (clip from `ClipPicker`, `volume = Random(volume) * strength * bus volume`,
`pitch = Random(pitch) * pitchScale`, `spatialBlend` 0 or 1, distances, `loop`, `outputAudioMixerGroup`, position) →
`Play()` → `Played`. The per-frame tick (`Update`, unscaled time) frees finished one-shots, advances fades, moves
`Follow` voices (stored target position), and pauses/unpauses voices of buses with `pausesWithGame` while
`Time.timeScale == 0`. Effects' fades use unscaled time.

- [ ] **Step 1: Write the failing tests** (`[Category(TestArea.Audio)] [TestTiming]`; each makes an `AudioService` on a new object with `AudioTestKit`'s library, voiceCount 4, and an `AudioListener` at the origin):
  - `Play_TakesAVoice_AndRaisesPlayed`: one request → `PlayingCount == 1`, `Played` raised once with that request's sound.
  - `NeverMoreVoicesThanThePool`: 10 requests of 10 different sounds of equal priority → `PlayingCount == 4`.
  - `AFullPool_StealsTheLowestPriorityOldest_ForAHigherPriority`: 4 sounds at priority 100, then one at 200 → it plays, and the first-started's handle is no longer `IsPlaying`.
  - `AFullPool_DropsALowerPriority`: 4 at 200, one at 100 → returns `SoundHandle.None`, no `Played`.
  - `MaxInstances_AndCooldown_Hold`: a sound with maxInstances 2, cooldown 0: 3 plays → 2 voices; with cooldown 1s and maxInstances 9: two plays in one frame → 1 voice.
  - `ASoundBeyondItsMaxDistance_IsNotStarted`: 3D sound, maxDistance 10, at 20 m → `None`; same as 2D → plays.
  - `AFilterSayingNo_StopsIt_AndOneChangingTheRequest_ChangesWhatPlays`: a filter returning false → `None`; a filter setting `strength = 0.5f` → the voice's volume is half that of the unfiltered play (pitch and volume ranges set to fixed values).
  - `AStaleHandle_DoesNothing`: stop a handle, play another sound into the same voice, `Stop(old)` → the new one still plays.
  - `Stop_WithAFade_FadesOutThenFrees`: loop sound, `Stop(h, 0.2f)` → playing after 0.1 s (unscaled), freed by 0.4 s.
  - `Pausing_PausesEffectBuses_NotOthers`: one sound on a bus with pausesWithGame, one without; `Time.timeScale = 0` for a frame → the first's source `isPlaying` false, the second's true; back to 1 → both play, and the first's `timeSamples` didn't go back. (Restore `Time.timeScale` in `TearDown`.)
  - `Play_AllocatesNothing`: after warming up, `Assert.That(() => service.Play(request), Is.Not.AllocatingGCMemory())`.
  - `NoClips_IsSilent_AndLogsNothing`: a clipless sound → `None`; `LogAssert.NoUnexpectedReceived()`.
- [ ] **Step 2: Run:** `python3 Tools/run_tests.py --filter AudioServiceTests` → FAIL (types missing).
- [ ] **Step 3: Write `SoundHandle`, `VoicePool`, `SoundRateLimiter`, `AudioService` (+ `.Voices.cs`)** as above. `Played` is invoked with the request by value (no closure).
- [ ] **Step 4: Run:** same → PASS.
- [ ] **Step 5: Checkpoint:** hygiene check passes.

### Task 3: Bus volumes, saved

**Files:**
- Create: `Assets/Showcase/Audio/AudioVolumes.cs`
- Modify: `Assets/Showcase/Audio/AudioService.cs` (owns an `AudioVolumes`, loads in `Awake`)
- Test: `Assets/Showcase/Tests/Audio/AudioVolumeTests.cs`

**Interfaces:**
- Produces: `AudioService.SetVolume(AudioBus bus, float volume)` (clamped 0..1, applied to its playing voices at once, saved), `float GetVolume(AudioBus bus)` (saved, else `defaultVolume`); serialized `volumeKeyPrefix` (string, "MeridianMotion.Audio.Volume.") on the service; `AudioVolumes(string prefix)` with `float Load(AudioBus bus)`, `void Save(AudioBus bus, float volume)` (key: prefix + `busName`, `PlayerPrefs.SetFloat`).

- [ ] **Step 1: Write the failing tests** (a test prefix "MeridianMotion.Audio.Test.", keys deleted in `TearDown`):
  - `AnUnsavedBus_HasItsDefaultVolume`: defaultVolume 0.7 → `GetVolume` 0.7.
  - `SetVolume_IsSaved_AndReadByANewService`: set 0.25, destroy, make a new service with the same prefix → 0.25.
  - `SetVolume_ChangesWhatsPlaying`: a voice at full; `SetVolume(bus, 0.5f)` → its source volume halves.
  - `SetVolume_Clamps`: 3 → 1, -1 → 0.
- [ ] **Step 2: Run:** `python3 Tools/run_tests.py --filter AudioVolumeTests` → FAIL.
- [ ] **Step 3: Write `AudioVolumes` and the service's two methods.** Each voice keeps its unbussed volume so a bus change rescales without re-rolling.
- [ ] **Step 4: Run:** → PASS. Also `--filter AudioServiceTests` → still PASS.
- [ ] **Step 5: Checkpoint:** hygiene check.

### Task 4: Music

**Files:**
- Create: `Assets/Showcase/Audio/MusicTrack.cs`, `MusicPlayer.cs`, `SceneMusic.cs`
- Test: `Assets/Showcase/Tests/Audio/MusicPlayerTests.cs`

**Interfaces:**
- Consumes: `AudioService.GetVolume(AudioBus)`.
- Produces:
  - `class MusicTrack : ScriptableObject`: `intro` (optional `AudioClip`), `loop` (`AudioClip`), `volume` (0..1, 0.8), `bus` (`AudioBus`).
  - `class MusicPlayer : MonoBehaviour` (`[RequireComponent(typeof(AudioService))]`, on the service's object; two child `AudioSource`s, 2D, `ignoreListenerPause = true`): `void Play(MusicTrack track, float crossfadeSeconds = 1.5f)` (the same track already playing: nothing), `void Stop(float fadeSeconds = 1f)`, `MusicTrack Current`, `bool IsLooping` (the loop clip is the one playing). Fades by unscaled time; volume = track volume × bus volume, followed each frame.
  - `class SceneMusic : MonoBehaviour`: `track`, `crossfade` (seconds, 1.5); in `Start`, `AudioService.Instance?.GetComponent<MusicPlayer>()?.Play(track, crossfade)`.

With an intro: source plays the intro, and the same source's loop is scheduled at `AudioSettings.dspTime + intro length`
on the *other* source (`PlayScheduled`, `loop = true`) so there's no gap; crossfades then use the remaining source.
The builder may instead use one source per track plus one for the scheduled loop (three sources) if that's simpler;
the tests decide.

- [ ] **Step 1: Write the failing tests:**
  - `ATrackWithoutIntro_Loops`: `Play(track)` → a source is playing `loop` with `loop == true`; `IsLooping`.
  - `ATrackWithAnIntro_GoesOnToItsLoop`: intro 0.2 s, loop 0.5 s → after 0.4 s (real time) the loop clip is playing, `IsLooping`.
  - `ACrossfade_EndsWithOneSourcePlaying_TheNewTrack`: play A, then B with 0.2 s → after 0.5 s only B's source plays, `Current == B`.
  - `PlayingTheSameTrack_DoesNotRestartIt`: play A, wait, play A → the source's `timeSamples` kept going.
  - `MusicGoesOn_WhileThePauseMenuPauses`: `Time.timeScale = 0` and `AudioListener.pause = true` for 0.3 s → still `isPlaying`, fades still advance.
  - `SceneMusic_WithNoService_DoesNothing`: a `SceneMusic` alone → no exception, no log.
- [ ] **Step 2: Run:** `--filter MusicPlayerTests` → FAIL.
- [ ] **Step 3: Write the three classes.**
- [ ] **Step 4: Run:** → PASS.
- [ ] **Step 5: Checkpoint:** hygiene check.

### Task 5: Sound emitters on level pieces

**Files:**
- Create: `Assets/Showcase/Audio/SoundEmitter.cs`
- Test: `Assets/Showcase/Tests/Audio/SoundEmitterTests.cs`

**Interfaces:**
- Produces: `class SoundEmitter : MonoBehaviour` (`[AddComponentMenu("Meridian Motion/Audio/Sound Emitter")]`): `sound` (`SoundEvent`), `playOnEnable` (bool, true), `strength` (0..1, 1); `void Play()` (at its position, `SoundFlags.Follow` when the sound loops), `void Stop(float fadeSeconds = 0.2f)`; stops its loop in `OnDisable`.

- [ ] **Step 1: Write the failing tests:**
  - `PlayOnEnable_PlaysAtItsPosition`: emitter at (3,0,0) → `Played` raised with position (3,0,0).
  - `ALoopingEmitter_StopsWhenDisabled`: loop sound, disable → its handle not playing by the next frame.
  - `ALoopingEmitter_FollowsItsObject`: move the object 5 m → the voice's source is within 0.01 m of it the frame after.
  - `WithNoService_DoesNothing`: no `AudioService` in the scene → enable/disable, no exception, no log.
- [ ] **Step 2: Run:** `--filter SoundEmitterTests` → FAIL.
- [ ] **Step 3: Write `SoundEmitter`.**
- [ ] **Step 4: Run:** → PASS.
- [ ] **Step 5: Checkpoint:** hygiene check.

### Task 6: Asking the grass how much grass is somewhere

**Files:**
- Create: `Assets/Showcase/Grass/GrassField.Cover.cs` (`GrassField` is `sealed`, not partial: make it `public sealed partial class GrassField` in `GrassField.cs`, its only change)
- Test: `Assets/Showcase/Tests/Grass/GrassCoverTests.cs` (`[Category(TestArea.Levels)] [TestTiming]`, as `GrassFieldTests`)

**Interfaces:**
- Produces (namespace `MeridianMotion.Showcase.Grass`):
  - `float CoverAt(Vector3 worldPosition, float below = 0.5f)` (instance): 0..1, the density mask's value (`Layer.density[i] / 255f`) at the position, in the field's space (its transform, as the compute shader's `_LocalToWorld`), from the layer whose `height` is the highest at or below the position's height and no more than `below` metres under it; 0 outside the mask or with no such layer. No allocation.
  - `static float CoverAtAny(Vector3 worldPosition, float below = 0.5f)`: the highest `CoverAt` over the live fields (`live`).

- [ ] **Step 1: Write the failing tests** (a field made in code with `SetMask(origin (0,0), texelSize 0.5, size (8,8), one layer at height 0 with density 255 in texels x<4, 0 elsewhere)`):
  - `CoverAt_IsTheMasksDensity`: (0.5, 0, 0.5) → 1; (3.5, 0, 0.5) → 0.
  - `CoverAt_OutsideTheMask_IsZero`: (-1, 0, 0) → 0.
  - `CoverAt_TooFarAboveTheLayer_IsZero`: (0.5, 2, 0.5) → 0; (0.5, 0.3, 0.5) → 1.
  - `CoverAt_FollowsTheFieldsTransform`: field moved by (10,0,0) → (10.5, 0, 0.5) → 1.
  - `CoverAtAny_TakesTheHighestOfTheLiveFields`.
  - `CoverAt_AllocatesNothing`.
  These need no compute shaders (`Assume` nothing); they use only the CPU mask.
- [ ] **Step 2: Run:** `python3 Tools/run_tests.py --filter GrassCoverTests` → FAIL.
- [ ] **Step 3: Write `CoverAt`, `CoverAtAny`.** Read how `SetMask` and the compute shader map world to texel (`GrassField.cs`, `GrassBlades.compute`) and do the same on the CPU.
- [ ] **Step 4: Run:** → PASS; `--filter "GrassFieldTests|GrassBendingTests"` → still PASS.
- [ ] **Step 5: Checkpoint:** hygiene check. Docs: add `CoverAt` to the grass section of `docs/showcase/levels.md` (where the grass is described: a sentence on the query and what reads it).

### Task 7: Surfaces

**Files:**
- Create: `Assets/Showcase/CharacterAudio/MeridianMotion.Showcase.CharacterAudio.asmdef` (references: MeridianMotion.Showcase.Audio, MeridianMotion.Characters, MeridianMotion.Levels, MeridianMotion.Showcase.Grass)
- Create: `Assets/Showcase/CharacterAudio/SoundSurface.cs`, `SurfaceTag.cs`, `SurfaceMap.cs`, `SurfaceFinder.cs`
- Modify: hygiene `ALLOWED_REFERENCES`, AGENTS.md tables, `Tools/run_tests.py` `CHANGE_RULES` (`("Assets/Showcase/CharacterAudio/", ("Audio",), "the character's sounds")`), the test asmdef's references
- Test: `Assets/Showcase/Tests/Audio/SurfaceTests.cs`

**Interfaces:**
- Produces (namespace `MeridianMotion.Showcase.CharacterAudio`):
  - `class SoundSurface : ScriptableObject { string SurfaceName; }`
  - `class SurfaceTag : MonoBehaviour { SoundSurface Surface; }` (`[AddComponentMenu("Meridian Motion/Audio/Surface Tag")]`)
  - `class SurfaceMap : ScriptableObject`: `MaterialSurface[] byMaterial` (`[Serializable] struct MaterialSurface { Material material; SoundSurface surface; }`), `SoundSurface fallback`, `SoundSurface grass`, `float grassCover` (0..1, 0.3: the least grass cover that counts as grass); `SoundSurface SurfaceOfMaterial(Material m)` (null if unmapped).
  - `sealed class SurfaceFinder`: `SurfaceFinder(SurfaceMap map)`; `SoundSurface Find(Transform ground, Vector3 feet)`. Order: `SurfaceTag` on `ground` or its parents → `GrassField.CoverAtAny(feet) >= map.grassCover` → the first `Renderer` on `ground` or its parents, its `sharedMaterial` mapped → `fallback`. A null `ground` → fallback. The tag and material answer is cached per `Transform` instance (a `Dictionary<Transform, SoundSurface>`, presized), the grass asked every time (it's position-dependent).

- [ ] **Step 1: Write the failing tests:**
  - `ATag_WinsOverEverything`: a cube with a mapped material and a `SurfaceTag(Metal)` on its parent, ground = the cube → Metal.
  - `AColliderChildWithNoRenderer_UsesItsParentsMaterial`: parent with `MeshRenderer` (material mapped to Concrete), child "Collider" with a `BoxCollider` → Concrete (the graybox layout).
  - `GrassUnderTheFeet_IsGrass_EvenOnAConcreteFloor`: a grass field (as Task 6's) over the floor → Grass; off the mask → Concrete.
  - `AnUnmappedMaterial_OrNoGround_IsTheFallback`.
  - `TheAnswerIsCached_PerGround`: change the renderer's material after the first `Find` → still the first answer.
- [ ] **Step 2: Run:** `--filter SurfaceTests` → FAIL.
- [ ] **Step 3: Write the four classes.**
- [ ] **Step 4: Run:** → PASS.
- [ ] **Step 5: Checkpoint:** hygiene check (the new assembly against its references).

### Task 8: The character's one-shots and loops

**Files:**
- Create: `Assets/Showcase/CharacterAudio/CharacterSoundSet.cs`, `CharacterAudio.cs`, `CharacterAudio.Loops.cs`
- Test: `Assets/Showcase/Tests/Audio/CharacterAudioTests.cs` (derives from `CharacterRigFixture`), `AudioReadsOnlyTests.cs`

**Interfaces:**
- Consumes: `AudioService`, `SoundRequest`, `SurfaceFinder`; `MMotionMotor.MovementEventRaised` (`Action<MovementEvents, MovementStep>`), `MMotionMotor.lastStep` (`.state`, `.finalVelocity`, `.initialGroundTransform`), `MMotionMotor.groundTransform`, `MMotionMotor.Scale` (check the exact scale property in `MMotionMotor.cs`; the pitch scale is `1 / sqrt(scale)`, clamped 0.5..2), `MovementEvents.TryParse(string, out MovementEvents)`, `CharacterState.TryParse(string, out CharacterState)`, `WaterEffects.Splashed` (`Action<WaterEffectContext>`: read its position and strength fields in `IWaterEffect.cs`).
- Produces:
  - `class CharacterSoundSet : ScriptableObject`: `EventSound[] events`, `StateLoop[] loops`, `FootstepSettings footsteps` (Task 9 fills its use), `SurfaceMap surfaces`, `SoundEvent splash`, `float windSpeed` (fall speed m/s for the wind loop, 12), `SoundEvent wind`.
  - `[Serializable] class EventSound { string eventName; SoundEvent sound; SurfaceSound[] bySurface; StrengthFrom strengthFrom; float fullStrengthSpeed = 10f; float minSpeed = 0f; }` (`minSpeed`: m/s at authored size, the speed its `strengthFrom` reads; several rows may share an event, and the one with the highest `minSpeed` the speed meets plays: that's how Land and Land (Hard) split)
  - `enum StrengthFrom { Full, FallSpeed, Speed, SpeedIntoWall }`
  - `[Serializable] class StateLoop { string stateName; SoundEvent loop; bool onlyWhileMoving; float fullStrengthSpeed = 6f; float fadeOut = 0.25f; }`
  - `[Serializable] struct SurfaceSound { SoundSurface surface; SoundEvent sound; }`
  - `class CharacterAudio : MonoBehaviour` (`[AddComponentMenu("Meridian Motion/Audio/Character Audio")]`): `CharacterSoundSet SoundSet { get; set; }` (setting it re-reads the rows), `EmitterId Emitter` (from `GetInstanceID()` at `Awake`). Subscribes in `OnEnable`, unsubscribes and stops every loop (no fade) in `OnDisable`. Events: the row whose parsed flag equals the event raised → its sound for the surface (`SurfaceFinder.Find(step.initialGroundTransform, feet)`, the row's `bySurface` match, else `sound`) → strength by `StrengthFrom` (speed divided by `fullStrengthSpeed * motor speed scale`, clamped 0..1) → `Play`. Of the rows for the event, the one with the highest `minSpeed` (scaled) the speed meets plays; none met: nothing. Loops (in `Update`): per row, in its state (and moving, if `onlyWhileMoving`) and no handle → `Play` with `Follow`; each frame `SetPosition` and `SetStrength`; out of it → `Stop(handle, fadeOut)`. Wind: in the air and falling faster than `windSpeed` (scaled) → the wind loop, strength from fall speed. Splash: `WaterEffects.Splashed` → `splash` at its position and strength.
  - Row names that don't parse: one `Debug.LogWarning` per name per set, naming the set and the name, then ignored.

- [ ] **Step 1: Write the failing tests** in `CharacterAudioTests` (each `rig.Floor()`, an `AudioService` from `AudioTestKit`, a set made in code with test sounds, `CharacterAudio` added to `rig.Character`; assertions on the service's `Played` and `IsPlaying`):
  - `Landing_PlaysTheLandSound_ForTheSurfaceUnderIt`: floor material mapped to Metal, `Landed` row with a Metal override → after a drop from 2 m, one `Played` with the Metal sound.
  - `AHardLanding_PlaysTheHardRow`: two `Landed` rows, minSpeed 0 and 8 → a drop from 1 m plays the first; from 8 m (over 8 m/s on landing) the second, once, and not the first.
  - `Jumping_PlaysItsSoundOnce`: one jump → one `Played` for the `Jumped` row.
  - `AStateLoop_StartsAndStops_WithTheState`: a `Crouching` loop row (easy to drive on the rig: `CharacterTestRig.Crouch`) → playing while crouched, not playing 0.5 s after standing.
  - `DespawningMidLoop_StopsTheLoopAtOnce`: crouch, then `Object.Destroy(rig.Character)` → by the next frame `PlayingCount == 0`; no log.
  - `ARowWhoseNameDoesntParse_WarnsOnce`: row "NotAnEvent" → `LogAssert.Expect(LogType.Warning, new Regex("NotAnEvent"))` once over two `SoundSet` sets.
  - `WithNoService_ItDoesNothing`: no `AudioService` → jump and land, no exception, no log.
  - `ASmallerCharacter_PlaysHigher`: `rig.Controller.SetScale(0.5f)` → the request's `pitchScale` > 1.
- [ ] **Step 2: Write `AudioReadsOnlyTests.AudioDoesNotChangeTheMovement`:** on the rig, run a scripted 4 s course (walk, jump, crouch, land) twice from the same spawn, once without and once with `CharacterAudio` and a service; compare `rig.Movement.Summarize()` (check its exact name in `MMotionMotor`, the compact state the determinism tests compare) after each step: identical.
- [ ] **Step 3: Run:** `--filter "CharacterAudioTests|AudioReadsOnlyTests"` → FAIL.
- [ ] **Step 4: Write `CharacterSoundSet`, `CharacterAudio`, `CharacterAudio.Loops.cs`.** Parsed flags are cached in arrays when the set is assigned; the event handler loops over them (no LINQ, no allocation).
- [ ] **Step 5: Run:** → PASS.
- [ ] **Step 6: Checkpoint:** hygiene check.

### Task 9: Footsteps

**Files:**
- Create: `Assets/Showcase/CharacterAudio/FootstepCadence.cs`, `CharacterAudio.Footsteps.cs`
- Modify: `CharacterSoundSet.cs` (`FootstepSettings`)
- Test: `Assets/Showcase/Tests/Audio/FootstepTests.cs` (derives from `CharacterRigFixture`)

**Interfaces:**
- Produces:
  - `[Serializable] class FootstepSettings { SoundEvent step; SoundEvent crouchedStep; SurfaceSound[] bySurface; SurfaceSound[] crouchedBySurface; float strideLength = 0.75f (m at authored size, one footfall); float walkSpeed = 2f; float runSpeed = 6f; float minSpeed = 0.3f; }`
  - `struct FootstepCadence { bool Advance(float distance, float stride); void Reset(); }`: true each time the accumulated distance passes `stride` (the remainder kept); `Reset` sets it to half a stride, so the first step comes soon after starting to move.
  - In `CharacterAudio.Update`: grounded (`CharacterState.Grounded` in `lastStep.state`), not in a takeover state that has its own sound (sliding, rolling, wall running, climbing, swimming, wading: skip while any of those flags is set), horizontal speed over `minSpeed` (scaled) → advance by horizontal distance this frame with stride × `HeightScale`; on a step, play the surface's step (crouched rows while `Crouching`) at the feet, strength `InverseLerp(walkSpeed, runSpeed, speed / SpeedScale)` remapped to 0.4..1. Leaving the ground resets the cadence. Landing doesn't play a footstep (the land sound does).

- [ ] **Step 1: Write the failing tests:**
  - `FootstepCadence_StepsEveryStride_KeepingTheRemainder` (plain test): `Reset()` (half a stride), stride 1, five `Advance(0.4f, 1f)` calls → `[false, true, false, true, false]` (0.9, 1.3→0.3, 0.7, 1.1→0.1, 0.5).
  - `WalkingOnTheGround_StepsAtTheStrideSpacing`: walk 6 m at walk speed → footstep `Played` count within ±1 of `6 / strideLength`.
  - `RunningIsLouderThanWalking`: average request strength running > walking.
  - `StandingStill_OrInTheAir_NoSteps`.
  - `OnGrass_PlaysTheGrassStep`: a grass field (Task 6's test field) on the floor → steps use the Grass row.
  - `Sliding_PlaysNoSteps`.
- [ ] **Step 2: Run:** `--filter FootstepTests` → FAIL.
- [ ] **Step 3: Write `FootstepCadence` and the footstep part.**
- [ ] **Step 4: Run:** → PASS; `--filter CharacterAudioTests` → PASS.
- [ ] **Step 5: Checkpoint:** hygiene check.

### Task 10: Attaching to spawned characters, and the level's sounds

**Files:**
- Create: `Assets/Showcase/CharacterAudio/CharacterAudioDirector.cs`, `LevelSounds.cs`, `LevelSoundSet.cs`
- Test: `Assets/Showcase/Tests/Audio/DirectorAndLevelTests.cs`

**Interfaces:**
- Consumes: `Checkpoint.Reached` / `Checkpoint.Respawned` (static `Action<IRespawnable, Checkpoint>`), `ShortcutGate.AnyOpened` (static `Action<ShortcutGate>`), `CourseTimer.SplitPassed` (`Action<int, float, float>`) and `RunFinished` (`Action<CourseRun>`) (instance events: `LevelSounds` finds the scene's `CourseTimer`s in `OnEnable`, `FindObjectsByType`, once).
- Produces:
  - `class CharacterAudioDirector : MonoBehaviour` (`[AddComponentMenu("Meridian Motion/Audio/Character Audio Director")]`): `CharacterSoundSet defaultSet`; `PrefabSoundSet[] byPrefab` (`[Serializable] struct PrefabSoundSet { string prefabName; CharacterSoundSet set; }`, matched by the spawned object's name starting with `prefabName`); `void AttachPlayer(Transform character)`, `void AttachNpc(Transform character, string route)`: adds a `CharacterAudio` (or reuses one) with the chosen set. `Transform Player` (the last attached player: what `LevelSounds` treats as the local player).
  - `class LevelSoundSet : ScriptableObject`: `checkpointReached`, `respawn`, `gateOpened`, `courseSplit`, `courseFinished` (`SoundEvent`s).
  - `class LevelSounds : MonoBehaviour` (`[AddComponentMenu("Meridian Motion/Audio/Level Sounds")]`): `LevelSoundSet sounds`, `CharacterAudioDirector director`. Subscribes in `OnEnable`, unsubscribes in `OnDisable`. Checkpoint and respawn sounds only when the `IRespawnable` is the director's `Player` (compare its transform; check `IRespawnable`'s members in `MeridianMotion.Levels`); gate at the gate's position; course sounds 2D.

- [ ] **Step 1: Write the failing tests:**
  - `AttachPlayer_AddsCharacterAudio_WithTheDefaultSet` and `AttachNpc_ChoosesTheSetByPrefabName` ("Fox" → the fox set).
  - `AttachingTwice_KeepsOneCharacterAudio`.
  - `ACheckpointReachedByThePlayer_Plays_ByAnNpc_DoesNot`: raise via a `Checkpoint` in a test scene (or invoke through the level's path the TutorialRing tests use: read `Assets/Showcase/Tests/TutorialRing` for how they trigger checkpoints) → one `Played` for the player, none for an NPC.
  - `AGateOpening_PlaysAtTheGate`.
  - `AfterLevelSoundsIsDestroyed_NothingIsListening`: destroy it, raise `Checkpoint.Reached` → no `Played`, no exception (the Restart case: a reloaded scene must not hear the old one's listener).
- [ ] **Step 2: Run:** `--filter DirectorAndLevelTests` → FAIL.
- [ ] **Step 3: Write the three classes.**
- [ ] **Step 4: Run:** → PASS.
- [ ] **Step 5: Checkpoint:** hygiene check.

### Task 11: Generic hooks in the UI framework (the package)

**Files:**
- Modify: `Assets/MeridianMotion/Runtime/UI/RootUIPrefab.cs` (add `public static event Action<RootUIPrefab> AnyOpened, AnyClosed;`, invoked right after `OnOpened?.Invoke(this)` (line ~131) and `OnClosed?.Invoke(this)` (line ~236))
- Modify: `Assets/MeridianMotion/Runtime/UI/UIManager.Back.cs` (`public static event Action BackPressed;`, invoked in `HandleBack` when it gives the key to the top window or the fallback, not when it returns early)
- Modify: `Assets/MeridianMotion/Runtime/UI/Notifications/Notifications.cs` (`public static event Action<NotificationEntry> AnyShown;`, invoked by `ShowNotification` with the entry it returns, when non-null)
- Create: `Assets/MeridianMotion/Tests/PlayMode/UI/UIHookTests.cs` (derives from `UITestsBase`, `[Category(TestArea.UI)]`; read `WindowLifecycleTests.cs` and `NotificationTests.cs` for how they open windows and show notifications)
- Modify: `docs/ui.md` (a short section naming the four hooks: what raises each, and that they're for anything that reacts to the UI, sound for instance)

**Interfaces:**
- Produces: `RootUIPrefab.AnyOpened`, `RootUIPrefab.AnyClosed`, `UIManager.BackPressed`, `Notifications.AnyShown` (exact signatures above). Each summary says what raises it, in the house style.

- [ ] **Step 1: Write the failing tests:**
  - `AnyOpened_AndAnyClosed_AreRaisedWithTheWindow_OnceEach`.
  - `AnyOpened_IsRaisedAgain_WhenAHiddenWindowIsShown` (matches `OnOpened`).
  - `BackPressed_IsRaisedWhenBackGoesToAWindow_NotWhileATextFieldHasFocus`.
  - `AnyShown_IsRaisedWithTheEntry`.
- [ ] **Step 2: Run:** `python3 Tools/run_tests.py --filter UIHookTests` → FAIL.
- [ ] **Step 3: Add the four events.**
- [ ] **Step 4: Run:** → PASS; then `python3 Tools/run_tests.py --area UI` → all PASS.
- [ ] **Step 5: Checkpoint:** hygiene check (it checks docs/ui.md's links too).

### Task 12: UI sounds

**Files:**
- Create: `Assets/Showcase/UIAudio/MeridianMotion.Showcase.UIAudio.asmdef` (references: MeridianMotion.Showcase.Audio, MeridianMotion.UI, UnityEngine.UI)
- Create: `Assets/Showcase/UIAudio/UISoundSet.cs`, `UISounds.cs`, `UISoundRelay.cs`
- Modify: hygiene `ALLOWED_REFERENCES`, AGENTS.md tables, `Tools/run_tests.py` `CHANGE_RULES` (`("Assets/Showcase/UIAudio/", ("Audio", "UI"), "the UI's sounds")`), the test asmdef's references
- Test: `Assets/Showcase/Tests/Audio/UISoundTests.cs`

**Interfaces:**
- Consumes: Task 11's hooks; `TabGroup.SelectionChanged` (`UnityEvent`).
- Produces (namespace `MeridianMotion.Showcase.UIAudio`):
  - `class UISoundSet : ScriptableObject`: `hover`, `click`, `denied`, `toggleOn`, `toggleOff`, `slider`, `tab`, `windowOpen`, `windowClose`, `back`, `notification` (`SoundEvent`s, all 2D on the UI bus).
  - `class UISounds : MonoBehaviour` (`[AddComponentMenu("Meridian Motion/Audio/UI Sounds")]`): `UISoundSet sounds`. Subscribes to the four hooks in `OnEnable`, unsubscribes in `OnDisable`. On `AnyOpened`: plays `windowOpen`, and for each `Selectable` under the window (`GetComponentsInChildren<Selectable>(true)`; this one allocation is per window opened, not per frame, and acceptable) without a `UISoundRelay`, adds one; hooks each `Toggle.onValueChanged`, `Slider.onValueChanged` and `TabGroup.SelectionChanged` once (the relay marks it done). `void Play(SoundEvent sound)` for the relays.
  - `class UISoundRelay : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, ISubmitHandler, ISelectHandler`: hover on pointer enter / select (when interactable; select only from navigation, not the click that follows a pointer enter in the same frame), click on click/submit of a `Button` (interactable), denied on click of a non-interactable one.

- [ ] **Step 1: Write the failing tests** (a `UIManager` set up as `UITestsBase` does: read `Assets/Showcase/Tests/HUD/ShowcaseHUDTests.cs` for a showcase test making the UI; an `AudioService` with test sounds):
  - `OpeningAWindow_PlaysOpen_AndAddsRelaysToItsButtons`.
  - `ClickingAButton_PlaysClick_AGreyedOutOne_PlaysDenied` (`ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler)`).
  - `HoveringPlaysHover_OncePerEnter`.
  - `TogglingPlaysOnThenOff`; `ATabChange_PlaysTab`; `Back_PlaysBack`; `ANotification_PlaysNotification`.
  - `UISounds_PlayWhileTheGameIsPaused` (`Time.timeScale = 0`; the UI bus has pausesWithGame off → the voice plays).
  - `ReopeningAWindow_DoesNotDoubleTheRelays` (one relay per object; one click → one `Played`).
  - `AfterUISoundsIsDestroyed_NothingIsListening` (open a window → no `Played`, no exception).
  - `WithNoService_ItDoesNothing`.
- [ ] **Step 2: Run:** `--filter UISoundTests` → FAIL.
- [ ] **Step 3: Write the three classes.**
- [ ] **Step 4: Run:** → PASS.
- [ ] **Step 5: Checkpoint:** hygiene check.

### Task 13: Create Audio Assets, and the inspectors

**Files:**
- Create: `Assets/Showcase/Editor/Audio/MeridianMotion.Showcase.Audio.EditorTools.asmdef` (Editor only; references as the spec's table)
- Create: `AudioAssets.cs` (menu, `CreateFromCommandLine`), `AudioAssets.Content.cs` (the lists below), `SoundEventEditor.cs`, `SoundLibraryEditor.cs`, `SoundFlagNameDrawer.cs`
- Modify: hygiene `ALLOWED_REFERENCES`, AGENTS.md tables and its Generated assets table (row: the audio's assets, `AudioAssets` (Create Audio Assets), only where missing, then hand-edited), `Tools/run_tests.py` `CHANGE_RULES` (`("Assets/Showcase/Editor/Audio/", ("Audio",), "the audio's editor tools")`)
- Test: `Assets/Showcase/Tests/Audio/AudioAssetsTests.cs` (`#if UNITY_EDITOR`, `AssetDatabase`; `Assume` the assets exist)

**Interfaces:**
- Produces:
  - `AudioAssets.Create()` (Tools > Meridian Motion > Showcase > Create Audio Assets) and `public static void CreateFromCommandLine()` (exits with 0, or 1 on error, as `GrassAssets.CreateFromCommandLine` does: copy its shape). Makes, **only where missing**, under `Assets/Showcase/Audio/`:
    - `Buses/Music.asset`, `Effects.asset` (pausesWithGame), `UI.asset`.
    - `Surfaces/` the seven Sound Surfaces, and `Surfaces/Surface Map.asset`, mapping every `Assets/Showcase/Materials/Level/Level_*.mat` (and `(Moving)`) by the spec's Sound content groups, fallback Concrete, grass Grass.
    - `Sounds/Character/`, `Sounds/Level/`, `Sounds/UI/`: one empty Sound Event per row of the spec's Sound content tables, named as the table (`Footstep (Concrete)` etc. for each "by surface" row's surfaces), with bus, 2D/3D, loop and priority set: footsteps 100, one-shots 128, loops 96, UI 200, level 160. The sets' rows point at them; Land (Hard)'s row has Min Speed 8.
    - `Music/` the four Music Tracks, empty, on the Music bus.
    - `Mannequin Sound Set.asset`, `Fox Sound Set.asset` (same rows; the fox's may use the same sounds), `Level Sound Set.asset`, `UI Sound Set.asset`.
    - `Sound Library.asset` with all of the above Sound Events (ids from `NextFreeId`) and the buses.
    - `Audio.prefab`: `AudioService` (library set) + `MusicPlayer`.
    Each name and path is a `const` in `AudioAssets` (the paths are the showcase's, so `"Assets/..."` strings are allowed here: it's not shipped code).
  - `SoundEventEditor`: the default inspector, `id` read-only, a Preview button (plays a clip in the editor with a temporary hidden `AudioSource`), and a warning box when no Sound Library lists the event.
  - `SoundLibraryEditor`: a "Find Every Sound Event" button adding every `SoundEvent` in the project and giving those with id 0 the next free ids. An `AssetPostprocessor` does the same for new Sound Events, so ids are never 0 and never reused.
  - `SoundFlagNameDrawer`: a `PropertyAttribute` `[SoundFlagName(FlagKind.Event | FlagKind.State)]` on the rows' `eventName`/`stateName` (the attribute lives in the bridge assembly; the drawer here), drawn as a dropdown of `MovementEvents.Named` / `CharacterState.Named`.

- [ ] **Step 1: Write the failing tests** in `AudioAssetsTests`:
  - `EverySoundSetRow_NamesARealFlag` (parse every row of the two sets).
  - `EveryLevelMaterial_IsMapped` (every `Level_*.mat` has a surface in the map).
  - `EverySoundEvent_IsInTheLibrary_WithAUniqueNonZeroId`.
  - `TheAudioPrefab_HasAServiceWithTheLibrary_AndAMusicPlayer`.
- [ ] **Step 2: Run** `<Unity> -batchmode -projectPath . -executeMethod <AudioAssets's full name>.CreateFromCommandLine -quit` (the editor closed) → exit 0; then `python3 Tools/run_tests.py --filter AudioAssetsTests` → PASS. (Before running Create, the tests `Assume`-skip: write them first, see them skip, then create.)
- [ ] **Step 3: Run Create again** → nothing changes (`git status` shows no modified assets).
- [ ] **Step 4: Checkpoint:** hygiene check (metas for every generated asset).

### Task 14: Set Up Scene, and the scenes

**Files:**
- Create: `Assets/Showcase/Editor/Audio/AudioSceneSetup.cs` (`[InitializeOnLoad]`, `CharacterSceneSetup.AfterSettingUp += AddAudio`, shaped as `GrassSceneSetup`)
- Modify (regenerated): the four showcase scenes by Set Up Scene

**Interfaces:**
- Consumes: `CharacterSceneSetup.SetupContext` (`scene`, `setup`, `spawner`, `withUndo`, `notes`), Task 13's paths.
- Produces: `AudioSceneSetup.AddAudio(CharacterSceneSetup.SetupContext context)`: for a scene under `Assets/Showcase/`, in the setup object, once each: the Audio prefab instance; an "Audio" child with `CharacterAudioDirector` (default set: the mannequin's; byPrefab "Fox" → the fox's) connected with `UnityEventTools.AddPersistentListener` to `spawner.OnCharacterSpawned` → `AttachPlayer` and `spawner.OnNpcSpawned` → `AttachNpc` (unless already connected); `LevelSounds` (the level set, the director); `SceneMusic` with the track named for the scene (`const` map: scene name → track path; none: no `SceneMusic`); `UISounds` (the UI set) beside the UI Manager if there is one (`UIManager` in the scene). A note in `context.notes`.

- [ ] **Step 1: Write the test:** in `AudioAssetsTests` (editor-only), `SetUpScene_AddsTheAudio_Once`: make a new scene under a temp folder named as a showcase scene (or call `AddAudio` with a context built for a new scene whose path starts with `Assets/Showcase/`, as `UISceneSetupTests` does in `Assets/MeridianMotion/Tests/EditMode/UI` — read it), call it twice → one `AudioService`, one director, the spawner's events each have one listener to it.
- [ ] **Step 2: Run:** `--filter AudioAssetsTests` → FAIL; write `AudioSceneSetup`; → PASS.
- [ ] **Step 3: Run Set Up Scene on each showcase scene** (`LevelPlayground`, `MeridianComplex`, `MeridianBasin`, `TutorialRing`) through its command-line entry point (docs/spawning.md names it), then open one in the editor and press Play: no errors in the console; the Audio objects are there.
- [ ] **Step 4: Run** `python3 Tools/run_tests.py --area Spawning Levels Audio UI` → PASS.
- [ ] **Step 5: Checkpoint:** hygiene check.

### Task 15: Docs, and the full run

**Files:**
- Create: `audio.md` in `docs/showcase` (new): what it is; the assemblies diagram and the core class diagram (copied from the spec, trimmed); adding things (the spec's table); the Sound content lists, by where they live (the assets made by Create Audio Assets), not their values; filling a slot; the netcode notes; UI sounds and the hooks.
- Modify: `README.md` (one line under the showcase's features), `docs/troubleshooting.md` (No sound: no `AudioListener`, bus at 0 in saved volumes, a Sound Event not in the library or with no clips, the pool full: the service's Voice Count), `docs/testing.md` (the `Audio` area), `docs/architecture.md` (the audio's assemblies, one paragraph, linking audio.md), `docs/glossary.md` (Sound Event, Sound Set, Bus, Voice), AGENTS.md (make sure the folder table, assembly table and Generated assets rows from Tasks 1, 7, 12 and 13 are all there and read as the rest do).

- [ ] **Step 1: Write the docs.**
- [ ] **Step 2: Run** `python3 Tools/CI/check_project_hygiene.py` → passes (it checks every doc link and heading).
- [ ] **Step 3: Run both full suites:** `python3 Tools/run_tests.py --all` → all PASS (report any failure with its output; don't call it done otherwise).
- [ ] **Step 4: Play each showcase scene** for a minute (Play in the editor): walk, run, jump, land on concrete and grass, swim, zipline, open the pause menu and click about. No errors or warnings in the console. (With empty slots it's silent: to hear it, drop any clip into Footstep (Concrete) and a Music Track.)
- [ ] **Step 5: Tell the user** what was done, what was run and its results, and that committing waits for them.
