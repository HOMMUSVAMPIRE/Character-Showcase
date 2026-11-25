using System.Collections.Generic;
using MeridianMotion.Showcase.Audio;
using MeridianMotion.Showcase.CharacterAudio;
using UnityEngine;

namespace MeridianMotion.Showcase.Tests.Audio
{
    /// <summary>
    /// What the character's sound tests share, beside the character's test rig: an audio service on the kit's library
    /// (recording what it played), a surface map with Concrete (the fallback), Metal and Grass, and a sound set to fill
    /// with rows. Made in code; disposed with the kit.
    /// </summary>
    public sealed class CharacterAudioRig : System.IDisposable
    {
        public readonly AudioTestKit kit = new();
        public readonly AudioService service;
        public readonly List<SoundRequest> played = new();
        public readonly SoundSurface concrete, metal, grass;
        public readonly SurfaceMap map;
        public readonly CharacterSoundSet set;
        private int nextId = 100;

        public CharacterAudioRig()
        {
            var obj = kit.Track(new GameObject("Test Audio"));
            obj.SetActive(false);
            service = obj.AddComponent<AudioService>();
            service.Library = kit.Library;
            service.VoiceCount = 32;
            obj.SetActive(true);
            service.Played += played.Add;

            concrete = Surface("Concrete");
            metal = Surface("Metal");
            grass = Surface("Grass");
            map = kit.Track(ScriptableObject.CreateInstance<SurfaceMap>());
            map.Set(new SurfaceMap.MaterialSurface[0], concrete, grass);
            set = kit.Track(ScriptableObject.CreateInstance<CharacterSoundSet>());
            set.Surfaces = map;
        }

        private SoundSurface Surface(string name)
        {
            SoundSurface surface = kit.Track(ScriptableObject.CreateInstance<SoundSurface>());
            surface.SurfaceName = name;
            return surface;
        }

        /// <summary>A new sound (its own id), 2D so distance doesn't matter, as many at once as asked.</summary>
        public SoundEvent Sound(bool loop = false)
        {
            SoundEvent sound = kit.Sound(nextId++, loop: loop);
            sound.MaxInstances = 16;
            return sound;
        }

        /// <summary>Maps a material to a surface (the floor's, say).</summary>
        public void Map(Material material, SoundSurface surface) =>
            map.Set(new[] { new SurfaceMap.MaterialSurface { material = material, surface = surface } }, concrete, grass);

        /// <summary>How many times this sound was played.</summary>
        public int Count(SoundEvent sound)
        {
            int count = 0;
            foreach (SoundRequest request in played) { if (request.sound == sound.Id) { count++; } }
            return count;
        }

        public void Dispose() => kit.Dispose();
    }
}
