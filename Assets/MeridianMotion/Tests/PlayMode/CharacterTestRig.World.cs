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
    // The rig's world: boxes, a floor, ladders, ropes, ziplines, water, bounce pads, pushable boxes and moving platforms.
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
