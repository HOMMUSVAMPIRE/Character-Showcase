using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MeridianMotion.Characters;
using MeridianMotion.Levels;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeridianMotion.Tests
{
    // The rig's world: boxes, a floor, ladders, ropes, ziplines, water, bounce pads, pushable boxes, moving platforms,
    // checkpoints, hazards and shortcut gates.
    public sealed partial class CharacterTestRig
    {
        public GameObject Box(string name, Vector3 center, Vector3 size, Quaternion? rotation = null)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetPositionAndRotation(center, rotation ?? Quaternion.identity);
            box.transform.localScale = size;
            created.Add(box);
            return box;
        }

        /// <summary>A box the character can climb (a ClimbableSurface), of the given climbing speed multiplier.</summary>
        public ClimbableSurface ClimbableBox(string name, Vector3 center, Vector3 size, Quaternion? rotation = null, float speedMultiplier = 1f)
        {
            var surface = Box(name, center, size, rotation).AddComponent<ClimbableSurface>();
            surface.SpeedMultiplier = speedMultiplier;
            return surface;
        }

        /// <summary>
        /// A ladder of the given height standing at bottom (the middle of its foot), facing -Z (climbed from that side,
        /// toward +Z, as the test camera looks), unless turned otherwise. It builds its own mesh and collider.
        /// </summary>
        public ClimbableLadder Ladder(Vector3 bottom, float height, Quaternion? rotation = null, float width = 0.6f)
        {
            var obj = new GameObject("Ladder");
            created.Add(obj);
            obj.transform.SetPositionAndRotation(bottom, rotation ?? Quaternion.Euler(0f, 180f, 0f));
            var ladder = obj.AddComponent<ClimbableLadder>();
            ladder.Height = height;
            ladder.Width = width;
            ladder.Rebuild();
            return ladder;
        }

        /// <summary>A bounce pad: a box of the given size whose top is at topCenter, launching characters the given height.</summary>
        public LaunchPad BouncePad(Vector3 topCenter, Vector3 size, float launchHeight, Quaternion? rotation = null)
        {
            Quaternion turn = rotation ?? Quaternion.identity;
            GameObject box = Box("Bounce Pad", topCenter - turn * Vector3.up * (size.y / 2f), size, turn);
            var pad = box.AddComponent<LaunchPad>();
            pad.LaunchHeight = launchHeight;
            return pad;
        }

        /// <summary>A zipline from start to end (world space), drawing its own cable, with the given top speed.</summary>
        public ZiplineCable Zipline(Vector3 start, Vector3 end, float maxSpeed = 16f)
        {
            var obj = new GameObject("Zipline");
            created.Add(obj);
            obj.transform.position = start;
            var line = obj.AddComponent<ZiplineCable>();
            line.End = end;
            line.MaxSpeed = maxSpeed;
            line.Rebuild();
            return line;
        }

        /// <summary>A rope hanging from anchor, the given length, drawing itself.</summary>
        public SwingRope Rope(Vector3 anchor, float length)
        {
            needsUnityPhysicsSteps = true;
            var obj = new GameObject("Rope");
            created.Add(obj);
            obj.transform.position = anchor;
            var rope = obj.AddComponent<SwingRope>();
            rope.Length = length;
            rope.Rebuild();
            return rope;
        }

        /// <summary>
        /// A body of water whose surface is at surfaceCentre, size across (x), deep (y) and long (z), drawing its own
        /// surface. It has no collider: build what's under it (a floor) separately.
        /// </summary>
        public SwimmableWater Water(Vector3 surfaceCentre, Vector3 size)
        {
            var obj = new GameObject("Water");
            created.Add(obj);
            obj.transform.position = surfaceCentre;
            var water = obj.AddComponent<SwimmableWater>();
            water.Size = size;
            water.Rebuild();
            return water;
        }

        /// <summary>A large flat floor whose top surface is at the given height, 100 m across unless given a width.</summary>
        public GameObject Floor(float topHeight = 0f, float width = 100f) =>
            Box("Floor", new Vector3(0f, topHeight - 0.5f, 0f), new Vector3(width, 1f, width));

        /// <summary>
        /// A wall a metre thick straight ahead of the character (+Z), its face gap past the front of its body, standing on
        /// the ground (y 0) as tall as height and as wide as width, centred on the character. Returns the face's z.
        /// </summary>
        public float WallAhead(float gap = 0.1f, float height = 30f, float width = 10f)
        {
            float face = Position.z + FrontExtent + gap;
            Box("Wall", new Vector3(Position.x, height / 2f, face + 0.5f), new Vector3(width, height, 1f));
            return face;
        }

        /// <summary>
        /// A wall a metre thick on the character's right (+X), its face gap past the side of its body, standing on the
        /// ground (y 0) as tall as height, running length along Z from behind metres behind the character. Returns the
        /// face's x.
        /// </summary>
        public float WallBeside(float gap = 0.1f, float height = 40f, float length = 60f, float behind = 1f)
        {
            float face = Position.x + SideExtent + gap;
            Box("Wall", new Vector3(face + 0.5f, height / 2f, Position.z - behind + length / 2f), new Vector3(1f, height, length));
            return face;
        }

        /// <summary>A box the character can grab, push and pull (a PushableBlock), resting with its bottom at center.y - size.y / 2.</summary>
        public PushableBlock PushableBox(Vector3 center, Vector3 size, float speedMultiplier = 1f, bool canBePulled = true)
        {
            needsUnityPhysicsSteps = true;
            GameObject box = Box("Pushable Box", center, size);
            box.AddComponent<Rigidbody>();
            var pushable = box.AddComponent<PushableBlock>();
            pushable.SpeedMultiplier = speedMultiplier;
            pushable.CanBePulled = canBePulled;
            return pushable;
        }

        /// <summary>A checkpoint whose respawn point (its position) is feet, facing yaw degrees, its box size big standing on it.</summary>
        public Checkpoint Checkpoint(string id, Vector3 feet, Vector3 size, float yaw = 0f, bool isStart = false)
        {
            var holder = new GameObject($"Checkpoint {id}");
            holder.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f));
            created.Add(holder);
            var checkpoint = holder.AddComponent<Checkpoint>();
            checkpoint.Id = id;
            checkpoint.IsStart = isStart;
            checkpoint.Center = new Vector3(0f, size.y / 2f, 0f);
            checkpoint.Size = size;
            return checkpoint;
        }

        /// <summary>A hazard volume (see HazardVolume) of the given size around center.</summary>
        public HazardVolume Hazard(Vector3 center, Vector3 size)
        {
            var holder = new GameObject("Hazard Volume");
            holder.transform.position = center;
            created.Add(holder);
            var hazard = holder.AddComponent<HazardVolume>();
            hazard.Center = Vector3.zero;
            hazard.Size = size;
            return hazard;
        }

        /// <summary>A shortcut gate: a box (a kinematic Rigidbody) of the given size around center, opening by travel over openTime seconds.</summary>
        public ShortcutGate Gate(string id, Vector3 center, Vector3 size, Vector3 travel, float openTime = 0.5f)
        {
            needsUnityPhysicsSteps = true;
            GameObject box = Box($"Shortcut Gate {id}", center, size);
            box.AddComponent<Rigidbody>();
            var gate = box.AddComponent<ShortcutGate>();
            gate.Id = id;
            gate.Travel = travel;
            gate.OpenTime = openTime;
            return gate;
        }

        /// <summary>A shortcut trigger (a volume of the given size around center) that opens the gate.</summary>
        public ShortcutTrigger GateTrigger(ShortcutGate gate, Vector3 center, Vector3 size)
        {
            var holder = new GameObject("Shortcut Trigger");
            holder.transform.position = center;
            created.Add(holder);
            var trigger = holder.AddComponent<ShortcutTrigger>();
            trigger.Gate = gate;
            trigger.Center = Vector3.zero;
            trigger.Size = size;
            return trigger;
        }

        public TickedPlatform Platform(Vector3 topCenter, Vector3 size, Vector3 travel, float period, Vector3 spin)
        {
            needsUnityPhysicsSteps = true;
            GameObject box = Box("Platform", topCenter - Vector3.up * (size.y / 2f), size);
            box.AddComponent<Rigidbody>();
            var platform = box.AddComponent<TickedPlatform>();
            platform.travel = travel;
            platform.roundTripTime = period;
            platform.spinDegreesPerSecond = spin;
            platform.easeAtEnds = false;
            platform.pauseAtEnds = 0f;
            return platform;
        }
    }
}
