using UnityEditor;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Regenerates the mannequin's clips and controller from its file when it changes (a mannequin regenerated with
    /// Blender by hand, say), before the changed models are prepared: registered with CharacterModelPostprocessor, which
    /// knows nothing of the model pipeline.
    /// </summary>
    [InitializeOnLoad]
    internal static class MannequinRegeneration
    {
        static MannequinRegeneration()
        {
            CharacterModelPostprocessor.BeforePreparing += () =>
            {
                if (MannequinGenerator.NeedsRebuild()) { MannequinGenerator.Generate(blenderPath: null); }
            };
        }
    }
}
