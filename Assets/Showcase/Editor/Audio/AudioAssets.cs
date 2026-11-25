using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeridianMotion.Showcase.CharacterAudio;
using MeridianMotion.Showcase.UIAudio;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.Showcase.Audio.EditorTools
{
    /// <summary>
    /// Makes the audio's assets where they're missing (Tools > Meridian Motion > Showcase > Create Audio Assets; then
    /// they're hand-edited): the buses (Music, Effects, UI), the surfaces and the Surface Map (from the levels' materials),
    /// an empty Sound Event for every sound the game plays (the slots: drop clips into them), the characters', the level's
    /// and the UI's sound sets pointing at them, a Music Track per showcase scene, the Sound Library of them all (each
    /// Sound Event given its id), and the Audio prefab (an AudioService with the library, and a MusicPlayer). What's there
    /// already is left as it is, so running it again changes nothing.
    /// </summary>
    public static partial class AudioAssets
    {
        /// <summary>Where the audio's assets are.</summary>
        public const string Folder = "Assets/Showcase/Audio";
        public const string BusFolder = Folder + "/Buses";
        public const string SurfaceFolder = Folder + "/Surfaces";
        public const string SoundFolder = Folder + "/Sounds";
        public const string MusicFolder = Folder + "/Music";
        public const string SurfaceMapPath = SurfaceFolder + "/Surface Map.asset";
        public const string LibraryPath = Folder + "/Sound Library.asset";
        public const string MannequinSetPath = Folder + "/Mannequin Sound Set.asset";
        public const string FoxSetPath = Folder + "/Fox Sound Set.asset";
        public const string LevelSetPath = Folder + "/Level Sound Set.asset";
        public const string UISetPath = Folder + "/UI Sound Set.asset";
        public const string PrefabPath = Folder + "/Audio.prefab";
        public const string LevelMaterialFolder = "Assets/Showcase/Materials/Level";

        /// <summary>Each showcase scene's music (its scene's name: its track's).</summary>
        public static readonly (string scene, string track)[] SceneTracks =
        {
            ("LevelPlayground", "Playground Theme"),
            ("MeridianComplex", "Complex Theme"),
            ("MeridianBasin", "Basin Theme"),
            ("TutorialRing", "Tutorial Ring Theme"),
        };

        /// <summary>The path of a scene's music track, or null if it has none.</summary>
        public static string TrackPathFor(string sceneName)
        {
            foreach ((string scene, string track) in SceneTracks)
            {
                if (scene == sceneName) { return $"{MusicFolder}/{track}.asset"; }
            }
            return null;
        }

        [MenuItem("Tools/Meridian Motion/Showcase/Create Audio Assets")]
        private static void CreateFromMenu()
        {
            int made = Create();
            Debug.Log(made > 0 ? $"[Audio] Made {made} audio assets in {Folder}." : "[Audio] The audio's assets are all there already.");
        }

        /// <summary>For -executeMethod: makes what's missing (see the class), then exits.</summary>
        public static void CreateFromCommandLine()
        {
            int code = 0;
            try { CreateFromMenu(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            if (Application.isBatchMode) { EditorApplication.Exit(code); }
        }

        private static int made;

        /// <summary>Makes whichever of the assets are missing: how many it made.</summary>
        public static int Create()
        {
            made = 0;
            foreach (string folder in new[] { BusFolder, SurfaceFolder, SoundFolder + "/Character", SoundFolder + "/Level", SoundFolder + "/UI", MusicFolder })
            {
                Directory.CreateDirectory(folder);
            }
            AssetDatabase.Refresh();

            AudioBus music = Asset<AudioBus>($"{BusFolder}/Music.asset", bus => { bus.BusName = "Music"; bus.PausesWithGame = false; bus.DefaultVolume = 0.8f; });
            AudioBus effects = Asset<AudioBus>($"{BusFolder}/Effects.asset", bus => { bus.BusName = "Effects"; bus.PausesWithGame = true; });
            AudioBus ui = Asset<AudioBus>($"{BusFolder}/UI.asset", bus => { bus.BusName = "UI"; bus.PausesWithGame = false; });

            var surfaces = Surfaces.ToDictionary(s => s.surface, s => Asset<SoundSurface>($"{SurfaceFolder}/{s.surface}.asset", surface => surface.SurfaceName = s.surface));
            SurfaceMap map = Asset<SurfaceMap>(SurfaceMapPath, newMap => newMap.Set(MaterialSurfaces(surfaces), surfaces[FallbackSurface], surfaces[GrassSurface]));

            Asset<CharacterSoundSet>(MannequinSetPath, set => FillCharacterSet(set, effects, surfaces, map, walkCadence: 2.2f, runCadence: 3f));
            Asset<CharacterSoundSet>(FoxSetPath, set => FillCharacterSet(set, effects, surfaces, map, walkCadence: 4f, runCadence: 6f));
            Asset<LevelSoundSet>(LevelSetPath, set =>
            {
                set.CheckpointReached = Sound("Level", "Checkpoint Reached", effects, LevelPriority, Spatial.TwoD);
                set.Respawn = Sound("Level", "Respawn", effects, LevelPriority, Spatial.TwoD);
                set.GateOpened = Sound("Level", "Shortcut Gate Opened", effects, LevelPriority, Spatial.ThreeD);
                set.CourseSplit = Sound("Level", "Course Split", effects, LevelPriority, Spatial.TwoD);
                set.CourseFinished = Sound("Level", "Course Finished", effects, LevelPriority, Spatial.TwoD);
            });
            Asset<UISoundSet>(UISetPath, set =>
            {
                SoundEvent UISound(string name) => Sound("UI", "UI " + name, ui, UIPriority, Spatial.TwoD);
                set.Hover = UISound("Hover");
                set.Click = UISound("Click");
                set.Denied = UISound("Denied");
                set.ToggleOn = UISound("Toggle On");
                set.ToggleOff = UISound("Toggle Off");
                set.Slider = UISound("Slider");
                set.Tab = UISound("Tab");
                set.WindowOpen = UISound("Window Open");
                set.WindowClose = UISound("Window Close");
                set.Back = UISound("Back");
                set.Notification = UISound("Notification");
            });
            foreach ((string _, string track) in SceneTracks)
            {
                Asset<MusicTrack>($"{MusicFolder}/{track}.asset", newTrack => newTrack.Bus = music);
            }

            SoundLibrary library = Asset<SoundLibrary>(LibraryPath, _ => { });
            FillLibrary(library);
            library.Set(library.Sounds.ToArray(), new[] { music, effects, ui });
            EditorUtility.SetDirty(library);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                var obj = new GameObject("Audio");
                obj.SetActive(false);
                obj.AddComponent<AudioService>().Library = library;
                obj.AddComponent<MusicPlayer>();
                obj.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(obj, PrefabPath);
                UnityEngine.Object.DestroyImmediate(obj);
                made++;
            }
            AssetDatabase.SaveAssets();
            AssignGeneratedClips();
            return made;
        }

        /// <summary>
        /// Puts every Sound Event in the project into a library, giving each without an id, or with another's (a duplicate),
        /// a new one (SoundLibrary.AssignIds: ids are never changed or reused, since a request names its sound by it). What
        /// the library's inspector's Find Every Sound Event does.
        /// </summary>
        public static void FillLibrary(SoundLibrary library)
        {
            var found = new List<SoundEvent>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(SoundEvent)))
            {
                var sound = AssetDatabase.LoadAssetAtPath<SoundEvent>(AssetDatabase.GUIDToAssetPath(guid));
                if (sound != null) { found.Add(sound); }
            }
            found.Sort((a, b) => string.CompareOrdinal(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b)));
            foreach (SoundEvent changed in library.AssignIds(found)) { EditorUtility.SetDirty(changed); }
            EditorUtility.SetDirty(library);
        }

        private static SurfaceMap.MaterialSurface[] MaterialSurfaces(Dictionary<string, SoundSurface> surfaces)
        {
            var entries = new List<SurfaceMap.MaterialSurface>();
            foreach ((string surface, string[] materials) in Surfaces)
            {
                foreach (string material in materials)
                {
                    foreach (string path in new[] { $"{LevelMaterialFolder}/Level_{material}.mat", $"{LevelMaterialFolder}/Level_{material} (Moving).mat" })
                    {
                        var found = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (found != null) { entries.Add(new SurfaceMap.MaterialSurface { material = found, surface = surfaces[surface] }); }
                    }
                }
            }
            return entries.ToArray();
        }

        private static void FillCharacterSet(CharacterSoundSet set, AudioBus effects, Dictionary<string, SoundSurface> surfaces,
                                             SurfaceMap map, float walkCadence, float runCadence)
        {
            set.Surfaces = map;
            set.Events = OneShots.Select(shot =>
            {
                SurfaceSound[] bySurface = shot.bySurface ? BySurface(shot.sound, effects, surfaces, OneShotPriority) : new SurfaceSound[0];
                return new EventSound
                {
                    eventName = shot.eventName,
                    sound = shot.bySurface ? bySurface[0].sound : Sound("Character", shot.sound, effects, OneShotPriority, Spatial.ThreeD),
                    bySurface = bySurface,
                    strengthFrom = shot.strength,
                    minSpeed = shot.minSpeed,
                };
            }).ToArray();
            set.Loops = Loops.Select(loop => new StateLoop
            {
                stateName = loop.state,
                loop = Sound("Character", loop.sound, effects, LoopPriority, Spatial.ThreeD, looping: true),
                onlyWhileMoving = loop.moving,
            }).ToArray();
            SurfaceSound[] steps = BySurface("Footstep", effects, surfaces, FootstepPriority);
            SurfaceSound[] crouchedSteps = BySurface("Footstep (Crouched)", effects, surfaces, FootstepPriority);
            set.Footsteps = new FootstepSettings
            {
                step = steps[0].sound,
                bySurface = steps,
                crouchedStep = crouchedSteps[0].sound,
                crouchedBySurface = crouchedSteps,
                walkCadence = walkCadence,
                runCadence = runCadence,
            };
            set.Splash = Sound("Character", "Splash", effects, OneShotPriority, Spatial.ThreeD);
            set.Wind = Sound("Character", "Wind Loop", effects, LoopPriority, Spatial.TwoD, looping: true);
        }

        /// <summary>A sound per surface (the fallback's first), named "Sound (Surface)".</summary>
        private static SurfaceSound[] BySurface(string name, AudioBus bus, Dictionary<string, SoundSurface> surfaces, int priority)
        {
            return Surfaces.OrderBy(s => s.surface == FallbackSurface ? 0 : 1).Select(s => new SurfaceSound
            {
                surface = surfaces[s.surface],
                sound = Sound("Character", $"{name} ({s.surface})", bus, priority, Spatial.ThreeD),
            }).ToArray();
        }

        /// <summary>A Sound Event, made empty if it's missing, in Sounds/[group].</summary>
        private static SoundEvent Sound(string group, string name, AudioBus bus, int priority, Spatial spatial, bool looping = false) =>
            Asset<SoundEvent>($"{SoundFolder}/{group}/{name}.asset", sound =>
            {
                sound.Bus = bus;
                sound.Priority = priority;
                sound.Spatial = spatial;
                sound.Loop = looping;
            });

        /// <summary>The asset at a path: made (and set up) if it's missing, else as it is.</summary>
        private static T Asset<T>(string path, Action<T> setUp) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) { return asset; }
            asset = ScriptableObject.CreateInstance<T>();
            setUp(asset);
            AssetDatabase.CreateAsset(asset, path);
            made++;
            return asset;
        }
    }
}
