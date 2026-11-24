using MeridianMotion.Characters;
using UnityEngine;

namespace MeridianMotion.Showcase.TutorialRing
{
    /// <summary>
    /// The Tutorial Ring's game, in its scene (added by Set Up Scene, with the spawner's On Character Spawned connected to
    /// CharacterSpawned): the player's character, as it spawns, is given a TutorialLessonTracker (following the lessons'
    /// zones and its events) and a PlaytestSessionLog (written beside each replay the scene's ReplayRecorder saves), and
    /// the prompt presenter shows its lessons. The character's tuning (one jump) is the spawner's Tuning Preset.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Meridian Motion/Showcase/Tutorial Ring Setup")]
    public class TutorialRingSetup : MonoBehaviour
    {
        [Tooltip("The lessons the level's zones name")]
        [SerializeField] private TutorialLessonCatalog catalog;
        [Tooltip("What shows the lessons' prompts")]
        [SerializeField] private TutorialPromptPresenter presenter;

        /// <summary>The lessons the level's zones name.</summary>
        public TutorialLessonCatalog Catalog { get => catalog; set => catalog = value; }
        /// <summary>What shows the lessons' prompts.</summary>
        public TutorialPromptPresenter Presenter { get => presenter; set => presenter = value; }
        /// <summary>The player's character's lessons (null before it spawns).</summary>
        public TutorialLessonTracker Lessons { get; private set; }
        /// <summary>The player's character's session log (null before it spawns).</summary>
        public PlaytestSessionLog Session { get; private set; }

        /// <summary>For the spawner's On Character Spawned: sets up the player's character (an NPC's is left alone).</summary>
        public void CharacterSpawned(Transform character)
        {
            if (character == null || !character.TryGetComponent(out MMotionMotor motor)) { return; }
            if (character.TryGetComponent(out MMotionCharacter controller) && controller.ControlledBy != ControlledBy.LocalPlayer) { return; }
            Lessons = character.TryGetComponent(out TutorialLessonTracker lessons) ? lessons : character.gameObject.AddComponent<TutorialLessonTracker>();
            Lessons.Catalog = catalog;
            Lessons.Follow(motor);
            Session = character.TryGetComponent(out PlaytestSessionLog log) ? log : character.gameObject.AddComponent<PlaytestSessionLog>();
            Session.Begin(Lessons, FindAnyObjectByType<ReplayRecorder>());
            if (presenter != null) { presenter.Show(Lessons); }
        }
    }
}
