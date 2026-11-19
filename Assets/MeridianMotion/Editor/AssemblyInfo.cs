using System.Runtime.CompilerServices;

// The model-generation pipeline (the mannequin's and the fox's generators: Assets/Showcase/Editor/Pipeline) is
// an editor assembly of its own, kept out of the package, as are the level checks (the movement envelope the level specs
// are checked against: Assets/Showcase/Editor/LevelChecks). Both use the character editor's internal helpers (CommandLine).
[assembly: InternalsVisibleTo("MeridianMotion.Showcase.Pipeline")]
[assembly: InternalsVisibleTo("MeridianMotion.Showcase.LevelChecks")]
