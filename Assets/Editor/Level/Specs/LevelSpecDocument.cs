using UnityEngine;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// The level editor's spec as Unity can undo it: its JSON, which every edit records (Undo.RecordObject) before
    /// changing, so Edit > Undo and Redo step through the edits. Never saved: the spec file is what's kept.
    /// </summary>
    public sealed class LevelSpecDocument : ScriptableObject
    {
        [Tooltip("The spec being edited, as JSON.")]
        [TextArea] public string json;
        [Tooltip("The spec file it was opened from (Assets/Levels/….level.json), or empty for a new one not yet saved.")]
        public string path;
    }
}
