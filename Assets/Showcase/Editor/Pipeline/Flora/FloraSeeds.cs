using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.Showcase.Pipeline
{
    /// <summary>
    /// The seeds the flora's variants are generated from (ReferenceAssets/OpenTasks.md, P12): for each kind (oak, birch,
    /// round bush...), one seed per variant, in slots: Tree_Oak_01 is the first seed's, Tree_Oak_02 the second's. Give a
    /// slot another seed and regenerate the kind (its Regenerate button, or Generate Flora): the slot's model is made again
    /// in place, keeping its GUID, so every level using it shows the new one without a rebuild. Shorten a kind's list and
    /// its surplus variants are deleted; lengthen it and new ones are made (the levels pick them up when rebuilt, as the
    /// pieces pick among every variant of a kind). Generate Flora makes this asset where it's missing, with the seeds the
    /// first flora was made with (Tools/Flora/generate_flora.py, SEEDS).
    /// </summary>
    [CreateAssetMenu(menuName = "Meridian Motion/Showcase/Flora Seeds", fileName = "Flora Seeds")]
    public sealed class FloraSeeds : ScriptableObject
    {
        /// <summary>A kind's variants' seeds.</summary>
        [Serializable]
        public sealed class Kind
        {
            [Tooltip("The kind, as the generator knows it: oak, pine, birch, dead, bush (round bushes), flowering (its seeds go to the colours in order: pink, white, yellow, purple), fern. (The ivy and vines aren't generated: they're grown over the walls, on the GPU.)")]
            public string kind;
            [Tooltip("One seed per variant, in slots: the first is [Name]_01's. A slot's seed changed makes that variant again in place.")]
            public int[] seeds;
        }

        [Tooltip("Each kind's variants' seeds.")]
        public List<Kind> kinds = new()
        {
            new Kind { kind = "oak", seeds = new[] { 0, 1, 2 } },
            new Kind { kind = "birch", seeds = new[] { 0, 1 } },
            new Kind { kind = "pine", seeds = new[] { 0, 1, 2 } },
            new Kind { kind = "dead", seeds = new[] { 0, 1 } },
            new Kind { kind = "bush", seeds = new[] { 0, 1, 2 } },
            new Kind { kind = "flowering", seeds = new[] { 0, 1, 2, 3 } },
            new Kind { kind = "fern", seeds = new[] { 0, 1 } },
        };

        /// <summary>The seeds as the generator script takes them (--seeds): "oak=0,1,2;birch=0,1".</summary>
        public string Argument() => string.Join(";", kinds.Where(k => !string.IsNullOrEmpty(k.kind))
            .Select(k => $"{k.kind.Trim()}={string.Join(",", k.seeds ?? Array.Empty<int>())}"));

        /// <summary>
        /// Where a kind's variants are (their category folder and name, by slot): each slot's name, for as many as the
        /// kind's seeds and, past them, the names its surplus variants would have (to delete).
        /// </summary>
        public static string VariantName(string kind, int slot) => kind switch
        {
            "oak" => $"Trees/Tree_Oak_{slot + 1:00}",
            "pine" => $"Trees/Tree_Pine_{slot + 1:00}",
            "birch" => $"Trees/Tree_Birch_{slot + 1:00}",
            "dead" => $"Trees/Tree_Dead_{slot + 1:00}",
            "bush" => $"Bushes/Bush_Round_{slot + 1:00}",
            "flowering" => slot < FloweringColours.Length ? $"Bushes/Bush_Flowering_{FloweringColours[slot]}" : null,
            "fern" => $"Bushes/Fern_{slot + 1:00}",
            _ => null,
        };

        /// <summary>The flowering bushes' colours, in the order their seeds go to them.</summary>
        public static readonly string[] FloweringColours = { "Pink", "White", "Yellow", "Purple" };

        /// <summary>The seeds of a kind (an empty list if it isn't listed).</summary>
        public int[] SeedsOf(string kind) => kinds.FirstOrDefault(k => k.kind == kind)?.seeds ?? Array.Empty<int>();
    }

    /// <summary>The Flora Seeds inspector: its seeds, and a button per kind that regenerates just that kind's variants.</summary>
    [CustomEditor(typeof(FloraSeeds))]
    public sealed class FloraSeedsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var seeds = (FloraSeeds)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Regenerate (in Blender, a minute or so)", EditorStyles.boldLabel);
            foreach (FloraSeeds.Kind kind in seeds.kinds.ToList())
            {
                if (string.IsNullOrEmpty(kind.kind)) { continue; }
                if (GUILayout.Button($"Regenerate {kind.kind} ({kind.seeds?.Length ?? 0} variants)"))
                {
                    AssetDatabase.SaveAssets();
                    if (BlenderRunner.FindOrAsk("Generate Flora", "the flora", "flora models", out string blender))
                    {
                        FloraGenerator.Generate(blender, new[] { kind.kind });
                    }
                    GUIUtility.ExitGUI();
                }
            }
            if (GUILayout.Button("Picture the Variants"))
            {
                FloraPictures.Picture();
                GUIUtility.ExitGUI();
            }
        }
    }
}
