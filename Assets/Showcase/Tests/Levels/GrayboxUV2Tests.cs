using System.Collections.Generic;
using MeridianMotion.Showcase.Levels;
using MeridianMotion.Tests;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine;

namespace MeridianMotion.Showcase.Tests.Levels
{
    /// <summary>
    /// Checks that the level builders' boxes and prisms have a UV2 giving each face its own space, for climbable paint
    /// (PaintedClimbableSurface) to paint in: within 0 to 1, no two triangles on the same texel. On a new, unsaved scene.
    /// </summary>
    [Category(TestArea.Levels)]
    [TestTiming]
    public class GrayboxUV2Tests
    {
        private readonly List<Object> created = new();

        [SetUp]
        public void SetUp() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created) { if (obj != null) { Object.DestroyImmediate(obj); } }
            created.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void TheLevelsBoxesAndPrisms_HaveAUV2_GivingEachFaceItsOwnSpace()
        {
            Graybox.Begin();
            var parent = new GameObject("Pieces").transform;
            created.Add(parent.gameObject);
            GameObject box = Graybox.Box(parent, "Box", Vector3.zero, new Vector3(3f, 2f, 1f));
            // A footprint with a corner cut at 45 degrees, as prisms' footprints are.
            Vector2[] shape = { new(0f, 0f), new(4f, 0f), new(4f, 2f), new(2f, 4f), new(0f, 4f) };
            GameObject prism = Graybox.Prism(parent, "Prism", shape, 0f, 3f);
            foreach (GameObject piece in new[] { box, prism })
            {
                Mesh mesh = piece.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.HasVertexAttribute(VertexAttribute.TexCoord1), Is.True, $"{piece.name}'s mesh should have a UV2.");
                AssertNoOverlap(mesh, piece.name);
            }
        }

        /// <summary>Asserts a mesh's UV2 is within 0 to 1 and no two of its triangles cover the same texel (of 256 x 256).</summary>
        private static void AssertNoOverlap(Mesh mesh, string name)
        {
            const int size = 256;
            var uv2 = new List<Vector2>();
            mesh.GetUVs(1, uv2);
            foreach (Vector2 uv in uv2)
            {
                Assert.That(uv.x, Is.InRange(0f, 1f), $"{name}'s UV2 should be within 0 to 1.");
                Assert.That(uv.y, Is.InRange(0f, 1f), $"{name}'s UV2 should be within 0 to 1.");
            }
            var owner = new int[size * size];
            int[] triangles = mesh.triangles;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector2 a = uv2[triangles[t]] * size, b = uv2[triangles[t + 1]] * size, c = uv2[triangles[t + 2]] * size;
                Vector2 v0 = b - a, v1 = c - a;
                float d = v0.x * v1.y - v1.x * v0.y;
                if (Mathf.Abs(d) < 1e-9f) { continue; }
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 v2 = new Vector2(x + 0.5f, y + 0.5f) - a;
                    float wb = (v2.x * v1.y - v1.x * v2.y) / d, wc = (v0.x * v2.y - v2.x * v0.y) / d;
                    // Strictly inside, so texels on the edge two triangles of a face share don't count as both.
                    if (wb <= 1e-3f || wc <= 1e-3f || wb + wc >= 1f - 1e-3f) { continue; }
                    Assert.That(owner[y * size + x], Is.EqualTo(0), $"Two of {name}'s triangles share a texel of its UV2.");
                    owner[y * size + x] = t / 3 + 1;
                }
            }
        }
    }
}
