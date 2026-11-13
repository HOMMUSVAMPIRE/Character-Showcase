using System.Collections.Generic;
using CharacterShowcase.EditorTools.Level;
using Level;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CharacterShowcase.Tests.EditMode
{
    /// <summary>
    /// Checks of showing painted climbable areas: the concrete shader has a climbable mask, sampled in UV2, and a climbable
    /// twin of each of its look's settings; the materials that show climbable areas share the 1-pixel black mask that
    /// means nothing's painted; painting a face paints it, and only it, in its renderer's mask, given to the renderer's
    /// material (and taken off when the masks are turned off), and a renderer whose material shows no climbable areas
    /// gets none; and the level's boxes and prisms have a UV2 giving each face its own space. On a new, unsaved scene.
    /// </summary>
    public class ClimbablePaintTests
    {
        private const string ShaderPath = "Assets/Materials/NoiseConcrete.shadergraph";
        private const string NoMaskPath = "Assets/Materials/ClimbableMask_None.png";

        // The look settings the shader has a climbable twin of (_ClimbableTint for _Tint, and so on).
        private static readonly string[] LookSettings =
        {
            "_Tint", "_BumpStrength", "_Grain", "_Pitting", "_PitSize", "_StainStrength", "_StainThreshold", "_StainSoftness",
            "_Panels", "_PanelSize", "_SeamWidth", "_PanelVariation", "_Metallic", "_Smoothness", "_Emission",
        };

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

        private static Shader ConcreteShader()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null) { Assert.Ignore($"The concrete shader isn't at {ShaderPath}."); }
            return shader;
        }

        [Test]
        public void TheConcreteShader_HasAClimbableMask_AndAClimbableTwinOfEachLookSetting()
        {
            Shader shader = ConcreteShader();
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, "The shader should compile.");
            int mask = shader.FindPropertyIndex(PaintedClimbableSurface.MaskPropertyName);
            Assert.That(mask, Is.GreaterThanOrEqualTo(0), "The shader should have a climbable mask.");
            Assert.That(shader.GetPropertyType(mask), Is.EqualTo(ShaderPropertyType.Texture));
            Assert.That(shader.GetPropertyTextureDimension(mask), Is.EqualTo(TextureDimension.Tex2D));
            foreach (string setting in LookSettings)
            {
                string twin = "_Climbable" + setting.TrimStart('_');
                Assert.That(shader.FindPropertyIndex(twin), Is.GreaterThanOrEqualTo(0), $"The shader should have {twin}, the climbable look's {setting}.");
            }
        }

        [Test]
        public void MaterialsThatShowClimbableAreas_ShareTheUnpaintedMask_OnePixelOfBlack()
        {
            var none = AssetDatabase.LoadAssetAtPath<Texture2D>(NoMaskPath);
            Assert.That(none, Is.Not.Null, $"The unpainted mask should be at {NoMaskPath}, as a 2D texture.");
            Assert.That(none.width, Is.EqualTo(1));
            Assert.That(none.height, Is.EqualTo(1));
            int checkedMaterials = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials" }))
            {
                // Material files only: a shader graph holds a material of its own, which isn't saved.
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".mat")) { continue; }
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || !material.HasProperty(PaintedClimbableSurface.MaskProperty)) { continue; }
                Assert.That(material.GetTexture(PaintedClimbableSurface.MaskProperty), Is.SameAs(none), $"{material.name} should show the unpainted mask.");
                checkedMaterials++;
            }
            Assert.That(checkedMaterials, Is.GreaterThan(0), "The levels' materials should show climbable areas.");
        }

        /// <summary>A 4 x 4 x 1 m box at the origin, its mesh Unity's cube with a UV2, in the given material.</summary>
        private (GameObject box, MeshRenderer renderer, Mesh mesh) Box(Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(box);
            box.transform.localScale = new Vector3(4f, 4f, 1f);
            var filter = box.GetComponent<MeshFilter>();
            Mesh mesh = Object.Instantiate(filter.sharedMesh);
            created.Add(mesh);
            Unwrapping.GenerateSecondaryUVSet(mesh);
            filter.sharedMesh = mesh;
            var renderer = box.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return (box, renderer, mesh);
        }

        [Test]
        public void PaintingAFace_PaintsItInTheMask_AndNotTheOtherFaces_AndTheMaskGoesToTheMaterial()
        {
            var material = new Material(ConcreteShader());
            created.Add(material);
            var (box, renderer, mesh) = Box(material);
            var surface = box.AddComponent<PaintedClimbableSurface>();
            surface.MaskSize = 128;
            surface.Paint(new Vector3(0f, 0f, -0.5f), Vector3.back, 10f); // The whole front face.

            List<PaintedClimbableSurface.RendererMask> masks = surface.RebuildMasks();
            Assert.That(masks.Count, Is.EqualTo(1), "The box's renderer should get a mask.");
            Texture2D texture = masks[0].texture;
            Assert.That(texture.width, Is.EqualTo(128));
            byte[] pixels = texture.GetPixelData<byte>(0).ToArray();

            var vertices = new List<Vector3>();
            var uv2 = new List<Vector2>();
            mesh.GetVertices(vertices);
            mesh.GetUVs(1, uv2);
            int[] triangles = mesh.triangles;
            int front = 0, other = 0;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 normal = Vector3.Cross(vertices[triangles[t + 1]] - vertices[triangles[t]], vertices[triangles[t + 2]] - vertices[triangles[t]]).normalized;
                Vector2 middle = (uv2[triangles[t]] + uv2[triangles[t + 1]] + uv2[triangles[t + 2]]) / 3f * 128f;
                byte value = pixels[Mathf.FloorToInt(middle.y) * 128 + Mathf.FloorToInt(middle.x)];
                if (Vector3.Dot(normal, Vector3.back) > 0.99f) { Assert.That(value, Is.EqualTo(255), "The painted face should be white in the mask."); front++; }
                else { Assert.That(value, Is.EqualTo(0), $"A face facing {normal} wasn't painted and should be black in the mask."); other++; }
            }
            Assert.That(front, Is.EqualTo(2), "Test setup: the front face should be two triangles.");
            Assert.That(other, Is.EqualTo(10));

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, 0);
            Assert.That(block.GetTexture(PaintedClimbableSurface.MaskProperty), Is.SameAs(texture), "The mask should be given to the renderer's material.");

            surface.PaintsMasks = false;
            renderer.GetPropertyBlock(block, 0);
            Assert.That(block.isEmpty, Is.True, "With masks off, the material should show its own (unpainted) mask.");
        }

        [Test]
        public void ARendererWhoseMaterialShowsNoClimbableAreas_GetsNoMask()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            created.Add(material);
            var (box, _, _) = Box(material);
            var surface = box.AddComponent<PaintedClimbableSurface>();
            surface.Paint(new Vector3(0f, 0f, -0.5f), Vector3.back, 10f);
            Assert.That(surface.RebuildMasks(), Is.Empty);
            Assert.That(surface.IsPaintedAt(new Vector3(0f, 0f, -0.5f), Vector3.back, atEdge: false), Is.True, "It should still be climbable.");
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
