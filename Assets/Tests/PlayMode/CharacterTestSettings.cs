using UnityEngine;

namespace CharacterShowcase.Tests
{
    /// <summary>
    /// Which character the Play mode tests run on. Without one of these anywhere in the project, they run on the
    /// package's own Character prefab; with one, on the prefab it names: a team's own character, with its own model,
    /// animations, collider and movement tuning. The tests read what they need from the character (its size, step
    /// height, speeds), so they hold for any of them.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterTestSettings", menuName = "Character Showcase/Tests/Character Test Settings")]
    public class CharacterTestSettings : ScriptableObject
    {
        [Tooltip("The character prefab to test: it needs an ACCharacterController, MovementController, CharacterModelHost " +
                 "and AnimationController, as the package's Character prefab has")]
        public GameObject characterPrefab;
    }
}
