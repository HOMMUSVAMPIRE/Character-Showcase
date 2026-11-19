using MeridianMotion.EditorTools;
using UnityEditor;

namespace MeridianMotion.Showcase.Pipeline
{
    /// <summary>
    /// Regenerates the mannequin's clips and controller from its file when it changes (a mannequin regenerated with
    /// Blender by hand, say), before the changed models are prepared: registered with HumanoidModelPostprocessor, which
    /// knows nothing of the model pipeline.
    /// </summary>
    [InitializeOnLoad]
    internal static class MannequinRegeneration
    {
        static MannequinRegeneration()
        {
            HumanoidModelPostprocessor.BeforePreparing += () =>
            {
                if (MannequinGenerator.NeedsRebuild()) { MannequinGenerator.Generate(blenderPath: null); }
            };
        }
    }
}
