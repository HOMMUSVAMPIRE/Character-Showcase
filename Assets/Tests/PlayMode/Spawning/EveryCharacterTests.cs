using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CharacterShowcase.Tests.Spawning
{
    /// <summary>
    /// Every character prefab in the project (one with an ACCharacterController: the Character prefab, the fox, and any
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
                .Where(path => path.StartsWith("Assets/") && UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path)?.GetComponent<ACCharacterController>() != null)
                .OrderBy(path => path, System.StringComparer.Ordinal);
#else
            return Enumerable.Empty<string>();
#endif
        }

        [UnityTest]
        public IEnumerator EveryCharacterPrefab_SpawnsStandsAndWalks([ValueSource(nameof(CharacterPrefabs))] string path)
        {
            rig.Floor();
            yield return rig.SpawnSettled(new Vector3(0f, 0.05f, 0f), CharacterTestRig.LoadPrefab(path));
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
