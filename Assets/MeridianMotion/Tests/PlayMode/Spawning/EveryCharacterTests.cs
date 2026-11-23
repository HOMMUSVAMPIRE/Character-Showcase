using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MeridianMotion.Characters;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace MeridianMotion.Tests.Spawning
{
    /// <summary>
    /// Every character prefab in the project (one with an MMotionCharacter: the Character prefab, the fox, and any
    /// made with New Character) spawns, stands on the ground and walks: so a character made by hand or by New Character is
    /// tested from the moment it's in the project.
    /// </summary>
    [Category(TestArea.Spawning)]
    public class EveryCharacterTests : CharacterRigFixture
    {
        private static IEnumerable<string> CharacterPrefabs()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.FindAssets("t:Prefab").Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                .Where(path => path.StartsWith("Assets/") && UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path)?.GetComponent<MMotionCharacter>() != null)
                .OrderBy(path => path, System.StringComparer.Ordinal);
#else
            return Enumerable.Empty<string>();
#endif
        }

        [UnityTest]
        public IEnumerator EveryCharacterPrefab_SpawnsStandsAndWalks([ValueSource(nameof(CharacterPrefabs))] string path)
        {
            yield return rig.StandOnFloor(CharacterTestRig.LoadPrefab(path));
            Assert.That(rig.Grounded, Is.True, $"{path} should stand on the floor.");
            Vector3 start = rig.Position;
            const float seconds = 1.5f;
            yield return rig.Run(seconds, _ => CharacterTestRig.Move(Vector3.forward));
            float walked = Vector3.ProjectOnPlane(rig.Position - start, Vector3.up).magnitude;
            Assert.That(walked, Is.GreaterThan(0.5f * rig.WalkSpeed * seconds), $"{path} should walk (it went {walked:0.00} m).");
            Assert.That(rig.Grounded, Is.True);
        }
    }
}
