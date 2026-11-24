using System;
using System.Collections.Generic;
using UnityEngine;

namespace MeridianMotion.UI
{
    /// <summary>
    /// The icons, drawn in code (ReferenceAssets/OpenTasks.md, H4): each a name and what draws it with a UIIconPainter, on
    /// the 24-unit grid (docs/ui.md, Icons, has the spec). A name never changes once an icon's in use (it's saved on every
    /// UIIcon that shows it, and names its sprite in a theme's overrides); a new icon is a new name. Two sets: the system
    /// set (markers, alerts, tools, doors, arrows) and the ability set (simple figures for what the character does).
    /// </summary>
    public static partial class UIIconLibrary
    {
        // Stroke weights (grid units): most strokes, bold ones (arrows, glyphs) and fine ones (cables, ropes).
        private const float Stroke = 2f, Bold = 2.75f, Fine = 1.6f, Limb = 2.5f, HeadRadius = 2.2f;

        // ---- The system set ----
        public const string Pin = "pin", Waypoint = "waypoint", Alert = "alert", Help = "help", Info = "info", Plus = "plus",
            Gear = "gear", Wrench = "wrench", Clipboard = "clipboard", Database = "database", Download = "download",
            Lock = "lock", Unlock = "unlock", Elevator = "elevator", Stairs = "stairs", Door = "door", Check = "check",
            Cross = "cross", Warning = "warning", Crate = "crate", Chevrons = "chevrons", ArrowUp = "arrow-up",
            ArrowDown = "arrow-down", ArrowLeft = "arrow-left", ArrowRight = "arrow-right", Emblem = "emblem";

        // ---- The ability set ----
        public const string Grab = "grab", Push = "push", Ladder = "ladder", Run = "run", Sprint = "sprint",
            Zipline = "zipline", Rope = "rope", WallRun = "wall-run", Dash = "dash", Crouch = "crouch", Slide = "slide", Roll = "roll";

        private static readonly (string name, Action<UIIconPainter> draw)[] Icons =
        {
            (Pin, DrawPin), (Waypoint, DrawWaypoint), (Alert, DrawAlert), (Help, DrawHelp), (Info, DrawInfo), (Plus, DrawPlus),
            (Gear, DrawGear), (Wrench, DrawWrench), (Clipboard, DrawClipboard), (Database, DrawDatabase), (Download, DrawDownload),
            (Lock, DrawLock), (Unlock, DrawUnlock), (Elevator, DrawElevator), (Stairs, DrawStairs), (Door, DrawDoor),
            (Check, DrawCheck), (Cross, DrawCross), (Warning, DrawWarning), (Crate, DrawCrate), (Chevrons, DrawChevrons),
            (ArrowUp, p => DrawArrow(p, 90f)), (ArrowDown, p => DrawArrow(p, -90f)), (ArrowLeft, p => DrawArrow(p, 180f)),
            (ArrowRight, p => DrawArrow(p, 0f)), (Emblem, DrawEmblem),
            (Grab, DrawGrab), (Push, DrawPush), (Ladder, DrawLadder), (Run, DrawRun), (Sprint, DrawSprint), (Zipline, DrawZipline),
            (Rope, DrawRope), (WallRun, DrawWallRun), (Dash, DrawDash), (Crouch, DrawCrouch), (Slide, DrawSlide), (Roll, DrawRoll),
        };

        private static Dictionary<string, Action<UIIconPainter>> byName;

        /// <summary>Every icon's name, in the library's order (the system set, then the ability set).</summary>
        public static IReadOnlyList<string> Names
        {
            get
            {
                var names = new List<string>(Icons.Length);
                foreach ((string name, _) in Icons) { names.Add(name); }
                return names;
            }
        }

        /// <summary>Whether there's an icon of this name.</summary>
        public static bool Has(string name) => name != null && Lookup().ContainsKey(name);

        /// <summary>Draws the named icon with painter. False (drawing nothing) if there's no icon of that name.</summary>
        public static bool Draw(string name, UIIconPainter painter)
        {
            if (name == null || !Lookup().TryGetValue(name, out Action<UIIconPainter> draw)) { return false; }
            draw(painter);
            return true;
        }

        // The icons by name (a later one of a name, a mistake the tests catch, replaces an earlier).
        private static Dictionary<string, Action<UIIconPainter>> Lookup()
        {
            if (byName != null) { return byName; }
            byName = new Dictionary<string, Action<UIIconPainter>>(Icons.Length);
            foreach ((string name, Action<UIIconPainter> draw) in Icons) { byName[name] = draw; }
            return byName;
        }

        private static Vector2 V(float x, float y) => new(x, y);
    }
}
