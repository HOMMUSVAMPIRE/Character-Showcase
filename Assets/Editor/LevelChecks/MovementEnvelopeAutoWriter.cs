using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MeridianMotion.Characters;
using MeridianMotion.EditorTools;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.Showcase.LevelChecks
{
    /// <summary>
    /// Writes Tools/LevelSpec/envelope.json again (see MovementEnvelopeWriter) whenever something it's measured from is
    /// saved in the editor: a movement mode, the Character prefab, a character model source, or the level generation
    /// settings (for the ramp gradient). Retuning the movement then changes the limits the levels are checked against
    /// along with it, and the log says what changed, so a retune that changes what the levels allow doesn't go unseen.
    /// Not in batch mode (the command line and the test runs): there MovementEnvelopeTests catches an envelope left out of
    /// date (by an edit made outside the editor, say), rather than this hiding it.
    /// </summary>
    public class MovementEnvelopeAutoWriter : AssetPostprocessor
    {
        private const string GrayboxSettingsPath = "Assets/Editor/Level/LevelBuilderSettings.asset";
        // A saved change can import several assets at once: they're measured once, after the import.
        private static bool queued;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (Application.isBatchMode || queued || !imported.Any(MeasuredFrom)) { return; }
            queued = true;
            EditorApplication.delayCall += WriteWhenReady;
        }

        /// <summary>Whether the envelope is measured from the asset at path.</summary>
        private static bool MeasuredFrom(string path)
        {
            if (path == MovementEnvelopeWriter.CharacterPrefabPath || path == GrayboxSettingsPath) { return true; }
            if (!path.EndsWith(".asset", StringComparison.Ordinal)) { return false; }
            Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
            return type != null && (typeof(AbilityMovementMode).IsAssignableFrom(type) || typeof(CharacterModelSource).IsAssignableFrom(type));
        }

        /// <summary>Writes the envelope once the editor isn't busy (compiling, importing, or in play mode), if it changed.</summary>
        private static void WriteWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += WriteWhenReady;
                return;
            }
            queued = false;
            try
            {
                string before = File.Exists(MovementEnvelopeWriter.EnvelopePath) ? File.ReadAllText(MovementEnvelopeWriter.EnvelopePath) : "";
                string now = MovementEnvelopeWriter.Envelope();
                if (now == before) { return; }
                File.WriteAllText(MovementEnvelopeWriter.EnvelopePath, now);
                Debug.Log($"[Movement envelope] The character's tuning changed what it can do, so '{MovementEnvelopeWriter.EnvelopePath}' " +
                          $"(the limits the levels' movement checks use) was written again:\n{Changes(before, now)}\n" +
                          "Check the levels still suit it (the levelspec tool's movement checks grade them against it).");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Movement envelope] Couldn't write '{MovementEnvelopeWriter.EnvelopePath}' again: {e.Message}. " +
                                 "Write it by hand (Tools > Meridian Motion > Showcase > Levels > Specs > Write Movement Envelope).");
            }
        }

        // One value of the envelope: "key": value, or a move, {"distance": value, "move": "name"}.
        private static readonly Regex Value = new(@"^\s*""(\w+)"": (-?[\d.]+|null),?$", RegexOptions.Multiline);
        private static readonly Regex Move = new(@"\{""(\w+)"": (-?[\d.]+|null), ""move"": ""([^""]+)""\}");

        /// <summary>What's different between two envelopes, a line each: changed, added and removed values and moves.</summary>
        private static string Changes(string before, string now)
        {
            Dictionary<string, string> Values(string json)
            {
                var values = new Dictionary<string, string>();
                foreach (Match m in Value.Matches(json)) { values[m.Groups[1].Value] = m.Groups[2].Value; }
                foreach (Match m in Move.Matches(json)) { values[$"{m.Groups[3].Value} ({m.Groups[1].Value})"] = m.Groups[2].Value; }
                return values;
            }
            Dictionary<string, string> was = Values(before), isNow = Values(now);
            var changes = new StringBuilder();
            foreach (KeyValuePair<string, string> value in isNow)
            {
                if (!was.TryGetValue(value.Key, out string old)) { changes.AppendLine($"  added {value.Key}: {value.Value}"); }
                else if (old != value.Value) { changes.AppendLine($"  {value.Key}: {old} -> {value.Value}"); }
            }
            foreach (string key in was.Keys.Where(k => !isNow.ContainsKey(k))) { changes.AppendLine($"  removed {key} (was {was[key]})"); }
            return changes.Length > 0 ? changes.ToString().TrimEnd() : "  (only its layout)";
        }
    }
}
