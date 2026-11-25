using System;
using UnityEngine;

namespace MeridianMotion.Showcase.CharacterAudio
{
    /// <summary>
    /// Gives each character the scene's spawner spawns its sounds: a CharacterSounds with the Sound Set for its prefab (by
    /// its name), else the default. (The package's Character prefab can't hold the showcase's sounds, so they're added as
    /// characters spawn.) Set Up Scene connects the spawner's On Character Spawned to AttachPlayer and On Npc Spawned to
    /// AttachNpc. The last player attached is the scene's player, whose checkpoints LevelSounds plays.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Meridian Motion/Audio/Character Audio Director")]
    public class CharacterAudioDirector : MonoBehaviour
    {
        /// <summary>A Sound Set for the characters of one prefab.</summary>
        [Serializable]
        public struct PrefabSoundSet
        {
            [Tooltip("The start of the spawned characters' names (their prefab's name: Fox)")]
            public string prefabName;
            [Tooltip("Their Sound Set")]
            public CharacterSoundSet set;
        }

        [Tooltip("The Sound Set of characters no entry below is for")]
        [SerializeField] private CharacterSoundSet defaultSet;
        [Tooltip("The Sound Sets of the characters of particular prefabs")]
        [SerializeField] private PrefabSoundSet[] byPrefab = new PrefabSoundSet[0];

        /// <summary>The default Sound Set.</summary>
        public CharacterSoundSet DefaultSet { get => defaultSet; set => defaultSet = value; }
        /// <summary>The Sound Sets by prefab.</summary>
        public PrefabSoundSet[] ByPrefab { get => byPrefab; set => byPrefab = value ?? new PrefabSoundSet[0]; }
        /// <summary>The scene's player's character (the last attached as the player), or null.</summary>
        public Transform Player { get; private set; }

        /// <summary>Gives the player's character its sounds (the spawner's On Character Spawned).</summary>
        public void AttachPlayer(Transform character)
        {
            if (character == null) { return; }
            if (Player != null && Player != character && Player.TryGetComponent(out CharacterSounds previous)) { previous.IsPlayer = false; }
            Attach(character).IsPlayer = true;
            Player = character;
        }

        /// <summary>Gives an NPC its sounds (the spawner's On Npc Spawned).</summary>
        public void AttachNpc(Transform character, string route)
        {
            CharacterSounds sounds = Attach(character);
            if (sounds != null) { sounds.IsPlayer = false; }
        }

        private CharacterSounds Attach(Transform character)
        {
            if (character == null) { return null; }
            if (!character.TryGetComponent(out CharacterSounds sounds)) { sounds = character.gameObject.AddComponent<CharacterSounds>(); }
            sounds.SoundSet = SetFor(character.name);
            return sounds;
        }

        /// <summary>The Sound Set for a character of this name.</summary>
        public CharacterSoundSet SetFor(string characterName)
        {
            foreach (PrefabSoundSet entry in byPrefab)
            {
                if (!string.IsNullOrEmpty(entry.prefabName) && entry.set != null && characterName.StartsWith(entry.prefabName, StringComparison.Ordinal))
                {
                    return entry.set;
                }
            }
            return defaultSet;
        }
    }
}
