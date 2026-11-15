using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Character;
using UnityEditor;
using UnityEngine;

namespace CharacterShowcase.EditorTools
{
    /// <summary>
    /// Writes Tools/LevelSpec/envelope.json, the limits the levelspec tool's movement checks hold levels to, from what the
    /// default character can do: the Character prefab, with the collider its model gives it (see
    /// CharacterModelHost.ColliderFor), measured by running its movement (MovementController.Capabilities, through
    /// MovementSimulation). The levelspec tool is Python with nothing else to install, so it can't run the movement
    /// itself: this file is how it knows. Never edit it by hand; write it again after changing the tuning
    /// (MovementEnvelopeTests fails while it's out of date). The ramp gradient is the levels' own rule (GrayboxSettings'
    /// Max Ramp Gradient), not the character's.
    /// </summary>
    public static class MovementEnvelopeWriter
    {
        public const string EnvelopePath = "Tools/LevelSpec/envelope.json";
        public const string CharacterPrefabPath = "Assets/Prefabs/Character.prefab";
        // The level generation settings (the level editor assembly's GrayboxSettings, read by name: this assembly can't
        // reference it).
        private const string GrayboxSettingsPath = "Assets/Editor/Level/GrayboxSettings.asset";
        private const float DefaultRampGradient = 0.25f;

        [MenuItem("Tools/Character Showcase/Level/Specs/Write Movement Envelope")]
        private static void WriteFromMenu()
        {
            string json = Write();
            EditorUtility.DisplayDialog("Write Movement Envelope", $"Wrote '{EnvelopePath}' from the Character prefab's movement:\n\n{json}", "OK");
        }

        /// <summary>For -executeMethod.</summary>
        public static void WriteFromCommandLine()
        {
            Write();
            if (Application.isBatchMode) { EditorApplication.Exit(0); }
        }

        /// <summary>Measures the default character and writes the envelope, returning what it wrote.</summary>
        public static string Write()
        {
            string json = Envelope();
            File.WriteAllText(EnvelopePath, json);
            Debug.Log($"[Movement envelope] Wrote '{EnvelopePath}' from '{CharacterPrefabPath}': {Capabilities()}");
            return json;
        }

        /// <summary>What the default character (the Character prefab, with its model's collider) can do.</summary>
        public static MovementCapabilities Capabilities()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            var movement = prefab.GetComponent<MovementController>();
            float radius = movement.BaseRadius, height = movement.BaseHeight;
            if (prefab.TryGetComponent(out CharacterModelHost host)) { host.ColliderFor(host.ModelSource, movement, out radius, out height); }
            return MovementSimulation.Measure(movement, radius, height);
        }

        /// <summary>The envelope's JSON for the default character now.</summary>
        public static string Envelope()
        {
            MovementCapabilities c = Capabilities();
            float ramp = DefaultRampGradient;
            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(GrayboxSettingsPath);
            if (settings != null)
            {
                SerializedProperty gradient = new SerializedObject(settings).FindProperty("stairs.maxRampGradient");
                if (gradient != null) { ramp = gradient.floatValue; }
            }
            string N(float v) => float.IsNaN(v) ? "null" : v.ToString("0.##", CultureInfo.InvariantCulture);
            string Moves(IEnumerable<MovementCapabilities.Move> moves, string key) =>
                string.Join(",\n", moves.Select(m => $"    {{\"{key}\": {N(m.Reach)}, \"move\": \"{m.Name}\"}}"));
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"about\": \"What the default character can do (the Character prefab, with its model's collider), measured by running its movement " +
                      "(MovementController.Capabilities): the limits the movement checks hold a level's gaps, climbs, stairs, ramps and clearances to. " +
                      "Written by Tools > Character Showcase > Level > Specs > Write Movement Envelope: don't edit it. The ramp gradient is the levels' rule " +
                      "(GrayboxSettings' Max Ramp Gradient), not the character's limit.\",\n");
            sb.Append($"  \"stepHeight\": {N(c.StepHeight)},\n");
            sb.Append($"  \"walkableSlope\": {N(c.WalkableSlopeAngle)},\n");
            sb.Append($"  \"maxRampGradient\": {N(ramp)},\n");
            sb.Append($"  \"crouchHeight\": {N(c.CrouchHeight)},\n");
            sb.Append($"  \"standHeight\": {N(c.StandHeight)},\n");
            sb.Append($"  \"minJumpHeight\": {N(c.MinJumpHeight)},\n");
            sb.Append($"  \"maxJumpHeight\": {N(c.MaxJumpHeight)},\n");
            sb.Append($"  \"jumps\": [\n{Moves(c.Jumps, "distance")}\n  ],\n");
            sb.Append($"  \"climbs\": [\n{Moves(c.Climbs, "height")}\n  ],\n");
            sb.Append($"  \"slideLength\": {N(c.SlideLength)},\n");
            sb.Append($"  \"rollLength\": {N(c.RollLength)},\n");
            sb.Append($"  \"wallRunLength\": {N(c.WallRunLength)},\n");
            sb.Append($"  \"wallRunDrop\": {N(c.WallRunDrop)}\n");
            sb.Append("}\n");
            return sb.ToString();
        }
    }
}
