using System;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace MeridianMotion.Tests
{
    /// <summary>
    /// Records the animation tests, for comparing the animation before and after a change to how it's driven or built (the
    /// Animator's parameters, its controller), which a test passing or failing can miss. Off unless asked for, by an
    /// environment variable naming a folder:
    /// - MERIDIAN_MOTION_RECORD_ANIMATOR: every Animator parameter's value after each step a test steps the Animator by
    ///   hand (AnimationTestsBase.StepAnimator), a file per test. Stepped by hand with the physics, the values are the same
    ///   on every run, so two runs' files can be diffed: any difference is the change's.
    /// - MERIDIAN_MOTION_CAPTURE_POSES: a picture of the character's model as each animation test ends, from its right side,
    ///   behind on its left and in front on its right, a PNG per test, to look at side by side.
    /// </summary>
    public static class AnimationRecorder
    {
        public const string ParametersVariable = "MERIDIAN_MOTION_RECORD_ANIMATOR";
        public const string PosesVariable = "MERIDIAN_MOTION_CAPTURE_POSES";
        private const int PoseSize = 384;

        private static string Folder(string variable)
        {
            string folder = Environment.GetEnvironmentVariable(variable);
            return string.IsNullOrEmpty(folder) ? null : folder;
        }

        /// <summary>Whether parameters are being recorded (MERIDIAN_MOTION_RECORD_ANIMATOR is set).</summary>
        public static bool RecordingParameters => Folder(ParametersVariable) != null;

        private static string FileName(string extension)
        {
            var name = new StringBuilder();
            foreach (char c in TestContext.CurrentContext.Test.FullName) { name.Append(char.IsLetterOrDigit(c) || c == '.' || c == '_' ? c : '_'); }
            return name.Append(extension).ToString();
        }

        /// <summary>Appends every parameter's value, as a line for the step, to the test's file (if recording).</summary>
        public static void RecordParameters(Animator animator, int step)
        {
            string folder = Folder(ParametersVariable);
            if (folder == null || animator == null) { return; }
            var line = new StringBuilder().Append(step.ToString(CultureInfo.InvariantCulture));
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                line.Append(' ').Append(parameter.name).Append('=');
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float: line.Append(animator.GetFloat(parameter.nameHash).ToString("R", CultureInfo.InvariantCulture)); break;
                    case AnimatorControllerParameterType.Int: line.Append(animator.GetInteger(parameter.nameHash)); break;
                    case AnimatorControllerParameterType.Bool: line.Append(animator.GetBool(parameter.nameHash) ? 1 : 0); break;
                    default: line.Append('-'); break; // Triggers are used up as they're read: not compared.
                }
            }
            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
                line.Append(" layer").Append(layer).Append('=').Append(state.fullPathHash).Append('@')
                    .Append(state.normalizedTime.ToString("R", CultureInfo.InvariantCulture));
            }
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, FileName(".txt")), line.Append('\n').ToString());
        }

        /// <summary>Starts the test's parameter file afresh (if recording), so a test run again doesn't add to the last run's.</summary>
        public static void BeginTest()
        {
            string folder = Folder(ParametersVariable);
            if (folder == null) { return; }
            string path = Path.Combine(folder, FileName(".txt"));
            if (File.Exists(path)) { File.Delete(path); }
        }

        /// <summary>Saves a picture of the model under root from three sides (if capturing poses).</summary>
        public static void CapturePose(GameObject root)
        {
            string folder = Folder(PosesVariable);
            if (folder == null || root == null) { return; }
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { return; }
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) { bounds.Encapsulate(renderer.bounds); }
            float reach = Mathf.Max(bounds.extents.magnitude, 0.5f);

            var cameraObject = new GameObject("Pose Capture Camera");
            var lightObject = new GameObject("Pose Capture Light");
            var texture = new RenderTexture(PoseSize, PoseSize, 24);
            var sheet = new Texture2D(PoseSize * 3, PoseSize, TextureFormat.RGB24, false);
            try
            {
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.55f, 0.6f, 0.65f);
                camera.fieldOfView = 35f;
                camera.targetTexture = texture;
                Vector3 forward = root.transform.forward;
                // From its right, from behind on its left, and from in front on its right: facing a wall (climbing, sliding
                // down it), the views from in front are blocked by it, so two of the three see it from the side or behind.
                Vector3[] from = { Quaternion.AngleAxis(90f, Vector3.up) * forward, Quaternion.AngleAxis(-135f, Vector3.up) * forward, Quaternion.AngleAxis(45f, Vector3.up) * forward };
                for (int i = 0; i < from.Length; i++)
                {
                    camera.transform.position = bounds.center + from[i] * (reach / Mathf.Tan(0.5f * camera.fieldOfView * Mathf.Deg2Rad));
                    camera.transform.LookAt(bounds.center);
                    camera.Render();
                    RenderTexture.active = texture;
                    sheet.ReadPixels(new Rect(0, 0, PoseSize, PoseSize), PoseSize * i, 0);
                    RenderTexture.active = null;
                }
                sheet.Apply();
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, FileName(".png")), sheet.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(lightObject);
                UnityEngine.Object.DestroyImmediate(sheet);
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
