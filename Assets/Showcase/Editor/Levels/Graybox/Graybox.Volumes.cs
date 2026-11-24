using MeridianMotion.Levels.EditorTools;
using UnityEngine;

namespace MeridianMotion.Showcase.Levels
{
    // Volumes and gates: checkpoints, hazards, lesson zones, and shortcut gates with their triggers. As the other
    // mechanics, fixtures named for what they are with their settings after the name, which LevelFixtureSetup turns into
    // the level elements (Checkpoint, HazardVolume, LessonZone, ShortcutGate, ShortcutTrigger). The volumes have no
    // collider (a character's feet are checked against them after each step) and nothing to see but a checkpoint's line.
    public static partial class Graybox
    {
        /// <summary>How a checkpoint's name starts.</summary>
        public const string CheckpointPrefix = LevelFixtureSetup.CheckpointPrefix;
        /// <summary>How a hazard volume's name starts.</summary>
        public const string HazardPrefix = LevelFixtureSetup.HazardPrefix;
        /// <summary>How a lesson zone's name starts.</summary>
        public const string LessonZonePrefix = LevelFixtureSetup.LessonZonePrefix;
        /// <summary>How a shortcut gate's name starts.</summary>
        public const string ShortcutGatePrefix = LevelFixtureSetup.ShortcutGatePrefix;
        /// <summary>How a shortcut trigger's name starts.</summary>
        public const string ShortcutTriggerPrefix = LevelFixtureSetup.ShortcutTriggerPrefix;

        /// <summary>
        /// A checkpoint: a character's feet coming into its box (size across, high and along, standing on feet, turned yaw
        /// degrees) make feet where it respawns, facing yaw. start: the level's start (one per level). Marked on the
        /// floor by a cyan line across the box's middle, unless marked is false. No collider.
        /// </summary>
        [Piece]
        public static GameObject Checkpoint(Transform parent, string id, Vector3 feet, float yaw, Vector3 size, bool start = false, bool marked = true)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Checkpoint), parent, id, feet, yaw, size, start, marked);
            var checkpoint = new GameObject($"{CheckpointPrefix} | id={id} | start={(start ? 1 : 0)} | size={F(size)}");
            checkpoint.transform.SetParent(parent, false);
            checkpoint.transform.SetLocalPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f));
            float w = size.x / 2f, line = Settings.mechanics.edgeBand;
            if (marked)
            {
                var mesh = new GrayboxMesh();
                mesh.Box(GrayboxMaterial.Cyan, new Vector3(-w, 0.005f, -line), new Vector3(w, 0.02f, line), bottom: false);
                mesh.AddTo(checkpoint, checkpoint.name);
                MakeStatic(checkpoint, occluder: false);
            }
            return checkpoint;
        }

        /// <summary>A hazard: a character's feet coming into the box (size, around centre) respawn it at its checkpoint. Nothing to see, no collider.</summary>
        [Piece]
        public static GameObject HazardVolume(Transform parent, string name, Vector3 centre, Vector3 size)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(HazardVolume), parent, name, centre, size);
            var hazard = new GameObject($"{HazardPrefix} ({name}) | size={F(size)}");
            hazard.transform.SetParent(parent, false);
            hazard.transform.localPosition = centre;
            return hazard;
        }

        /// <summary>A lesson zone: the box (size, around centre, turned yaw degrees) in which lesson's prompt shows. Nothing to see, no collider.</summary>
        [Piece]
        public static GameObject LessonZone(Transform parent, string lesson, Vector3 centre, Vector3 size, float yaw = 0f)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(LessonZone), parent, lesson, centre, size, yaw);
            var zone = new GameObject($"{LessonZonePrefix} | lesson={lesson} | size={F(size)}");
            zone.transform.SetParent(parent, false);
            zone.transform.SetLocalPositionAndRotation(centre, Quaternion.Euler(0f, yaw, 0f));
            return zone;
        }

        /// <summary>
        /// A shortcut gate: a steel slab (size, around centre, turned yaw degrees) with orange bands, closed until its
        /// trigger opens it, then moving by travel (in its own axes) over time seconds. A box collider; not static.
        /// </summary>
        [Piece]
        public static GameObject ShortcutGate(Transform parent, string id, Vector3 centre, Vector3 size, Vector3 travel, float time, float yaw = 0f)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(ShortcutGate), parent, id, centre, size, travel, time, yaw);
            var gate = new GameObject($"{ShortcutGatePrefix} | id={id} | travel={F(travel)} | time={F(time)}");
            gate.transform.SetParent(parent, false);
            gate.transform.SetLocalPositionAndRotation(centre, Quaternion.Euler(0f, yaw, 0f));
            Vector3 h = size / 2f;
            float band = Settings.mechanics.edgeBand;
            var mesh = new GrayboxMesh();
            mesh.Box(GrayboxMaterial.Steel, -h, h);
            foreach (float y in new[] { -h.y * 0.5f, h.y * 0.5f })
            {
                mesh.Box(GrayboxMaterial.Accent, new Vector3(-h.x - 0.01f, y - band, -h.z - 0.01f), new Vector3(h.x + 0.01f, y + band, h.z + 0.01f), top: false, bottom: false);
            }
            mesh.AddTo(gate, gate.name);
            gate.AddComponent<BoxCollider>().size = size;
            return gate;
        }

        /// <summary>What opens a shortcut gate: a box (size, around centre, turned yaw degrees) on the gate's far side. Nothing to see, no collider.</summary>
        [Piece]
        public static GameObject ShortcutTrigger(Transform parent, string gate, Vector3 centre, Vector3 size, float yaw = 0f)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(ShortcutTrigger), parent, gate, centre, size, yaw);
            var trigger = new GameObject($"{ShortcutTriggerPrefix} | gate={gate} | size={F(size)}");
            trigger.transform.SetParent(parent, false);
            trigger.transform.SetLocalPositionAndRotation(centre, Quaternion.Euler(0f, yaw, 0f));
            return trigger;
        }
    }
}
