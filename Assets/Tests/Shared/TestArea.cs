namespace CharacterShowcase.Tests
{
    /// <summary>
    /// The areas tests are grouped in, as the docs are (a fixture says which: [Category(TestArea.Movement)]), so one area
    /// can be run on its own while working on it: the Test Runner window's Category filter, or -testCategory on the command
    /// line. Every fixture is in one (the hygiene check makes sure); a fixture may be in other categories too
    /// (SampleContent, Performance).
    /// </summary>
    public static class TestArea
    {
        /// <summary>Walking, jumping, dashing, collision, size, moving ground, events, components, tuning and capabilities (docs/movement.md).</summary>
        public const string Movement = "Movement";
        /// <summary>Crouching, sliding, rolling, wall running, wall sliding, ledges and vaulting (docs/abilities.md).</summary>
        public const string Abilities = "Abilities";
        /// <summary>Climbing, ladders, pushing and pulling, ropes, ziplines, bounce pads, swimming, diving and wading (docs/traversal.md).</summary>
        public const string Traversal = "Traversal";
        /// <summary>Animation, IK, foot placement and the model's turning (docs/animation.md).</summary>
        public const string Animation = "Animation";
        /// <summary>Character models: hosting, swapping, fitting and preparing them (docs/animation.md).</summary>
        public const string Models = "Models";
        /// <summary>The camera rig (docs/camera.md).</summary>
        public const string Camera = "Camera";
        /// <summary>Spawning the character and setting up scenes (docs/spawning.md).</summary>
        public const string Spawning = "Spawning";
        /// <summary>The levels: specs, the Level Editor, fixtures, climbable paint and timing gates (docs/levels.md, docs/level-specs.md).</summary>
        public const string Levels = "Levels";
    }
}
