using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.Showcase.Audio.EditorTools
{
    public static partial class AudioAssets
    {
        /// <summary>Where Tools/Audio/make_sfx.py's clips are generated (its --out).</summary>
        public const string ClipFolder = Folder + "/Clips";

        /// <summary>Each of the Sound Sets' surfaces, and the generator's surface its sounds are taken from (it makes
        /// concrete, metal and grass: the rest take the nearest).</summary>
        private static readonly (string surface, string generated)[] GeneratedSurfaces =
        {
            ("Concrete", "concrete"), ("Metal", "metal"), ("Glass", "concrete"),
            ("Soil", "grass"), ("Grass", "grass"), ("Foliage", "grass"),
        };

        /// <summary>The character's Sound Events that have a generated sound per surface: the Sound Event's name before
        /// " (Surface)", and the generator's sound (surfaces/[surface]/[sound]_[surface]_NN.wav).</summary>
        private static readonly (string sound, string generated)[] GeneratedBySurface =
        {
            ("Footstep", "footstep"), ("Footstep (Crouched)", "crouch_step"), ("Jump", "jump"), ("Land", "land"),
            ("Land (Hard)", "land_hard"), ("Slide Start", "slide_start"), ("Roll", "roll"), ("Step Up", "step_up"),
        };

        /// <summary>The character's other Sound Events with a generated sound: its name, and the generator's file stem in
        /// its folder (Clips/[folder]/[stem]_NN.wav, or [stem].wav for a loop). Hit Wall has one per surface, all the same.</summary>
        private static readonly (string sound, string folder, string stem)[] GeneratedSingles =
        {
            ("Air Jump", "movement", "air_jump"), ("Wall Jump", "movement", "wall_jump"),
            ("Dash", "surfaces/concrete", "dash_concrete"), ("Hit Ceiling", "movement", "ceiling_hit"),
            ("Crouch", "movement", "crouch"), ("Stand", "movement", "stand"),
            ("Ledge Grab", "traversal", "ledge_grab"), ("Ledge Climb", "traversal", "climb_up"),
            ("Ledge Drop", "traversal", "ledge_drop"), ("Grab", "traversal", "push_grab"), ("Vault", "traversal", "vault"),
            ("Wall Run Start", "traversal", "wall_run_start"), ("Wall Climb Start", "traversal", "wall_climb_start"),
            ("Rope Grab", "traversal", "rope_grab"), ("Rope Release", "traversal", "rope_release"),
            ("Zipline Attach", "traversal", "zipline_on"), ("Zipline Detach", "traversal", "zipline_off"),
            ("Bounce", "traversal", "bounce_pad"), ("Splash", "water", "splash"), ("Dive", "water", "dive_under"),
            ("Surface", "water", "resurface"),
            ("Slide Loop", "surfaces/concrete", "slide_concrete_loop"), ("Wall Run Loop", "traversal", "wall_run_loop"),
            ("Zipline Loop", "traversal", "zipline_loop"),
        };

        // The generator's advice for playing its sounds: pitch varied by about 4% each way, volume by about 10% (its
        // levels are balanced against each other, so no sound needs its own volume).
        private static readonly Vector2 GeneratedPitch = new(0.96f, 1.04f), GeneratedVolume = new(0.9f, 1f);

        [MenuItem("Tools/Meridian Motion/Showcase/Assign Generated Clips")]
        private static void AssignFromMenu()
        {
            int assigned = AssignGeneratedClips();
            Debug.Log(assigned > 0 ? $"[Audio] Gave {assigned} Sound Events their generated clips." :
                "[Audio] No Sound Event without clips has generated clips to take (Tools/Audio/make_sfx.py makes them).");
        }

        /// <summary>For -executeMethod: assigns the generated clips (see AssignGeneratedClips), then exits.</summary>
        public static void AssignClipsFromCommandLine()
        {
            int code = 0;
            try { AssignFromMenu(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            if (Application.isBatchMode) { EditorApplication.Exit(code); }
        }

        /// <summary>
        /// Gives each of the character's Sound Events with no clips the clips Tools/Audio/make_sfx.py generated for it (in
        /// Clips/), with the generator's pitch and volume ranges. A Sound Event that has clips already is left as it is,
        /// so clips put on by hand stay. How many it gave clips to.
        /// </summary>
        public static int AssignGeneratedClips()
        {
            if (!AssetDatabase.IsValidFolder(ClipFolder)) { return 0; }
            int assigned = 0;
            foreach ((string sound, string generated) in GeneratedBySurface)
            {
                foreach ((string surface, string from) in GeneratedSurfaces)
                {
                    if (Assign($"{sound} ({surface})", $"surfaces/{from}", $"{generated}_{from}")) { assigned++; }
                }
            }
            foreach ((string sound, string folder, string stem) in GeneratedSingles)
            {
                if (sound == "Hit Wall") { continue; }
                if (Assign(sound, folder, stem)) { assigned++; }
            }
            foreach ((string surface, string _) in GeneratedSurfaces)
            {
                if (Assign($"Hit Wall ({surface})", "movement", "wall_hit")) { assigned++; }
            }
            AssetDatabase.SaveAssets();
            return assigned;
        }

        private static bool Assign(string soundName, string folder, string stem)
        {
            var sound = AssetDatabase.LoadAssetAtPath<SoundEvent>($"{SoundFolder}/Character/{soundName}.asset");
            if (sound == null || sound.HasClips) { return false; }
            AudioClip[] clips = GeneratedClips($"{ClipFolder}/{folder}", stem);
            if (clips.Length == 0) { return false; }
            sound.Clips = clips;
            sound.Pitch = GeneratedPitch;
            sound.Volume = GeneratedVolume;
            EditorUtility.SetDirty(sound);
            return true;
        }

        /// <summary>A generated sound's clips in a folder: [stem]_NN in order, or [stem] alone (a loop).</summary>
        private static AudioClip[] GeneratedClips(string folder, string stem)
        {
            if (!AssetDatabase.IsValidFolder(folder)) { return new AudioClip[0]; }
            var variation = new Regex("^" + Regex.Escape(stem) + @"_\d\d$");
            var found = new List<AudioClip>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/') != folder) { continue; }
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null && (clip.name == stem || variation.IsMatch(clip.name))) { found.Add(clip); }
            }
            return found.OrderBy(c => c.name, StringComparer.Ordinal).ToArray();
        }
    }
}
