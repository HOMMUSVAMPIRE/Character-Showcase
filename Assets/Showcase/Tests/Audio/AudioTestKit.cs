using System;
using System.Collections.Generic;
using MeridianMotion.Showcase.Audio;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeridianMotion.Showcase.Tests.Audio
{
    /// <summary>
    /// What the audio's tests make in code: sound events with short silent clips, buses, a library of them, and a service
    /// to play them (with a listener at the origin). Everything it makes is destroyed on Dispose.
    /// </summary>
    public sealed class AudioTestKit : IDisposable
    {
        private readonly List<Object> made = new();

        /// <summary>The default bus: an effects bus, paused with the game.</summary>
        public AudioBus Effects { get; }
        /// <summary>The library every sound the kit makes is added to.</summary>
        public SoundLibrary Library { get; }

        private readonly List<SoundEvent> sounds = new();
        private readonly List<AudioBus> buses = new();

        /// <summary>The scene's audio listener, at the origin (every scene has one; Unity complains otherwise).</summary>
        public AudioListener Listener { get; }

        public AudioTestKit()
        {
            Listener = Track(new GameObject("Test Listener")).AddComponent<AudioListener>();
            Library = Track(ScriptableObject.CreateInstance<SoundLibrary>());
            Effects = Bus("Effects", pausesWithGame: true);
        }

        /// <summary>A sound with this id and so many one-second silent clips, fixed volume 1 and pitch 1 (so tests can
        /// compare volumes), on the effects bus, 2D unless said.</summary>
        public SoundEvent Sound(int id, int clips = 1, bool loop = false, Spatial spatial = Spatial.TwoD, int priority = 128,
                                AudioBus bus = null)
        {
            SoundEvent sound = Track(ScriptableObject.CreateInstance<SoundEvent>());
            sound.name = $"Test Sound {id}";
            var list = new AudioClip[clips];
            for (int i = 0; i < clips; i++) { list[i] = Track(AudioClip.Create($"Test Clip {id}.{i}", 44100, 1, 44100, false)); }
            sound.Clips = list;
            sound.AssignId(new SoundId(id));
            sound.Volume = Vector2.one;
            sound.Pitch = Vector2.one;
            sound.Loop = loop;
            sound.Spatial = spatial;
            sound.Priority = priority;
            sound.MaxInstances = 8;
            sound.Cooldown = 0f;
            sound.Bus = bus != null ? bus : Effects;
            sounds.Add(sound);
            Library.Set(sounds.ToArray(), buses.ToArray());
            return sound;
        }

        /// <summary>A bus of this name, added to the library.</summary>
        public AudioBus Bus(string name, bool pausesWithGame)
        {
            AudioBus bus = Track(ScriptableObject.CreateInstance<AudioBus>());
            bus.name = name;
            bus.BusName = name;
            bus.PausesWithGame = pausesWithGame;
            buses.Add(bus);
            Library.Set(sounds.ToArray(), buses.ToArray());
            return bus;
        }

        /// <summary>Keeps an object to destroy on Dispose.</summary>
        public T Track<T>(T obj) where T : Object
        {
            made.Add(obj);
            return obj;
        }

        public void Dispose()
        {
            for (int i = made.Count - 1; i >= 0; i--)
            {
                if (made[i] != null) { Object.Destroy(made[i]); }
            }
            made.Clear();
        }
    }
}
