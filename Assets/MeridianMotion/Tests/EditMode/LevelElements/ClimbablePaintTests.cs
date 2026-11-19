using System.Collections.Generic;
using MeridianMotion.Levels.EditorTools;
using MeridianMotion.Levels;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine;

namespace MeridianMotion.Tests.EditMode.LevelElements
{
    /// <summary>
    /// Checks of showing painted climbable areas: the concrete shader has a climbable mask, sampled in UV2, and a climbable
    /// twin of each of its look's settings; the materials that show climbable areas share the 1-pixel black mask that
    /// means nothing's painted; painting a face paints it, and only it, in its renderer's mask, given to the renderer's
    /// material (and taken off when the masks are turned off), and a renderer whose material shows no climbable areas
    /// gets none; and paint is for the face painted (not the back of a thin wall), and erased as painted. On a new, unsaved
    /// scene. (That the level builders' pieces have a UV2 to paint in: GrayboxUV2Tests, with the level builders.)
    /// </summary>
    [Category(TestArea.Levels)]
    [TestTiming]
    public class ClimbablePaintTests
    {
        private const string ShaderPath = "Assets/Showcase/Materials/NoiseConcrete.shadergraph";
        private static string NoMaskPath => ClimbablePaintAssets.NoMaskPath;

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
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                // Material files only: a shader graph holds a material of its own, which isn't saved.
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".mat")) { continue; }
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || !material.HasProperty(PaintedClimbableSurface.MaskProperty)) { continue; }
                Assert.That(material.GetTexture(PaintedClimbableSurface.MaskProperty), Is.SameAs(none), $"{material.name} should show the unpainted mask.");
                checkedMaterials++;
            }
            // The materials showing climbable areas are the showcase's levels' (NoiseConcrete's): a project without them has none to check.
            if (checkedMaterials == 0) { Assert.Ignore("No material in the project shows climbable areas (the showcase's levels' do)."); }
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
        public void Paint_IsForTheFacePainted_AndErasedAsPainted()
        {
            // A thin wall, 4 x 4 m and 0.2 m thick, its front face (facing -Z) at z = 0.
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(wall);
            wall.transform.SetPositionAndRotation(new Vector3(0f, 2f, 0.1f), Quaternion.identity);
            wall.transform.localScale = new Vector3(4f, 4f, 0.2f);
            var painted = wall.AddComponent<PaintedClimbableSurface>();
            var front = new Vector3(0f, 2f, 0f);
            var back = new Vector3(0f, 2f, 0.2f);
            Assert.That(painted.Paint(front, Vector3.back, 0.5f), Is.GreaterThan(0));
            Assert.That(painted.IsPaintedAt(front, Vector3.back, atEdge: false), Is.True, "The point painted should be painted.");
            Assert.That(painted.IsPaintedAt(front + Vector3.right * 0.3f, Vector3.back, atEdge: false), Is.True, "The brush's area should be painted.");
            Assert.That(painted.IsPaintedAt(front + Vector3.right * 1.2f, Vector3.back, atEdge: false), Is.False, "Beyond the brush shouldn't be painted.");
            Assert.That(painted.IsPaintedAt(back, Vector3.forward, atEdge: false), Is.False, "The back of a thin wall shouldn't be painted with its front.");

            Assert.That(painted.Erase(front, Vector3.back, 0.2f), Is.GreaterThan(0));
            Assert.That(painted.IsPaintedAt(front, Vector3.back, atEdge: false), Is.False, "An erased point shouldn't be painted.");
            Assert.That(painted.IsPaintedAt(front + Vector3.right * 0.4f, Vector3.back, atEdge: false), Is.True, "Erasing should leave the paint beyond it.");
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
    }
}
