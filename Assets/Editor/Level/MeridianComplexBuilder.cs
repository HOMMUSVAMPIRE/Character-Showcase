using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// Builds the Meridian Complex's graybox as one scene (see GrayboxScene for the build and its environment), under a
    /// "Meridian Complex" root: its seven areas, each by its own builder (01 Concourse, 02 Kinetic Gardens, 03
    /// Ascension Spire, 04 Fabrication Hall, 05 Suspension Yard, 06 Service Network, 07 Calibration Annex), joined at
    /// their connections, on a placeholder ground. Rebuilding replaces the scene and its environment assets, so edits
    /// made to them by hand are lost: once it's being worked on by hand, stop rebuilding it. Try changes to the
    /// builders' pieces in the Level Playground (LevelPlaygroundBuilder) first: it builds in a moment.
    /// </summary>
    public static class MeridianComplexBuilder
    {
        public const string ScenePath = "Assets/Scenes/MeridianComplex/MeridianComplex.unity";

        private const float GroundHalf = 220f;

        internal static readonly GrayboxScene Scene = new("Meridian Complex", ScenePath,
            ("01 Concourse", MeridianConcourseBuilder.BuildInto),
            ("02 Kinetic Gardens", MeridianKineticGardensBuilder.BuildInto),
            ("03 Ascension Spire", MeridianAscensionSpireBuilder.BuildInto),
            ("04 Fabrication Hall", MeridianFabricationHallBuilder.BuildInto),
            ("05 Suspension Yard", MeridianSuspensionYardBuilder.BuildInto),
            ("06 Service Network", MeridianServiceNetworkBuilder.BuildInto),
            ("07 Calibration Annex", MeridianCalibrationAnnexBuilder.BuildInto),
            ("the ground", BuildGround));

        /// <summary>
        /// Builds the level in the background (see LevelBuildWindow), a step at a time between editor frames, with a
        /// window showing its progress, so the editor doesn't freeze for the whole build. Opens the scene when it's done.
        /// </summary>
        [MenuItem("Tools/Character Showcase/Level/Build Meridian Complex")]
        public static void BuildFromMenu() => LevelBuildWindow.BuildFromMenu(Scene);

        /// <summary>For -executeMethod: the whole build at once, waiting for the lighting bake.</summary>
        public static void BuildFromCommandLine() => Scene.Build();

        /// <summary>
        /// A placeholder ground at ±0 under the whole complex (a little over its 400 × 400 m, for the districts that
        /// reach past it), open where the Service Network and the Fabrication Hall's basements go below it.
        /// </summary>
        private static void BuildGround(Transform complex, Material material)
        {
            Graybox.Begin(material);
            var openings = new List<UnityEngine.Rect>(MeridianServiceNetworkBuilder.GroundOpenings);
            openings.AddRange(MeridianFabricationHallBuilder.GroundOpenings);
            Graybox.Ground(complex, GroundHalf, openings);
        }
    }
}
