using UnityEngine;

namespace MeridianMotion.Levels
{
    /// <summary>
    /// A part of the level that teaches something (a tutorial's lesson): a volume named by its lesson's id, which a
    /// game's own code listens for (LevelPresence.VolumeEntered and VolumeExited, or LevelPresence.IsIn) to show the
    /// lesson's prompt while a character is in it. It does nothing to the character itself.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Meridian Motion/Levels/Lesson Zone")]
    public class LessonZone : LevelVolume
    {
        [Tooltip("The lesson this zone teaches: its id in the game's list of lessons.")]
        [SerializeField] private string lessonId = "";

        /// <summary>The lesson this zone teaches (its id).</summary>
        public string LessonId { get => lessonId; set => lessonId = value ?? ""; }

        protected override Color GizmoColor => new(0.7f, 0.4f, 1f);
    }
}
