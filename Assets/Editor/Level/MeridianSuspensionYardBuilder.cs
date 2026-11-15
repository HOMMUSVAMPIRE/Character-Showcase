using System.Collections.Generic;
using UnityEngine;
using static CharacterShowcase.EditorTools.Level.Graybox;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// Builds the graybox of the Meridian Complex's Suspension Yard (05), the aerial district south-east of the
    /// Concourse, into the complex's scene (see MeridianComplexBuilder), from the blockout sheet
    /// MeridianComplex-RemainingAreas.png: level geometry only, in the material it's given. Laid out in world
    /// coordinates.
    ///
    /// Octagonal tower platforms at +8, +16 and +24, each on a 6 m column, on a grid about 50 m apart, joined by 6 m
    /// bridges along the compass directions: suspended where they join towers of one height (no columns), on
    /// columns with a stair at one end where they change height. Gaps of 8 to 12 m for jumps and dashes, two
    /// chains of rope swings (north to north-east tower, and hub to south-east tower) on gantries, and stairs up from
    /// the ground (the recovery level) to the +8 towers.
    /// The Concourse's south-east bridge (+16) joins the arrival tower. How the sheet was read is in
    /// SuspensionYard_Blockout_Notes.md beside the scene.
    /// </summary>
    public static class MeridianSuspensionYardBuilder
    {
        // ---- The sheet's dimensions (tunable: see the notes) ----

        private const float BridgeWidth = 6f;
        private const float ColumnWidth = 6f;

        // The rope swings (see RopeChain), worked out for the movement's tuning (BasicMovementMode.asset): change them
        // with it, and check the arithmetic in the notes again.
        private const float RopeRise = 12f;       // The anchors, above the decks the swing goes between.
        private const float RopeDrop = 2f;        // How far below those decks the ropes hang.
        private const float RopeReach = 3f;       // The first and last rope, out from the decks' edges.
        private const float RopeMaxSpacing = 10f; // The furthest apart ropes are along a chain.
        private const float RopeMastOffset = 2.8f; // The gantry's masts, beside the line of ropes.

        private sealed class Tower
        {
            public string Name;
            public Vector2 Centre;
            public float Apothem, Height;
            public readonly List<(Vector2 centre, float width)> Openings = new();
            public Vector2 Flat(Vector2 direction) => Centre + direction.normalized * Apothem;
        }

        private static readonly Vector2 N = Vector2.up, S = Vector2.down, E = Vector2.right, W = Vector2.left;
        private static readonly Vector2 NE = new Vector2(1f, 1f).normalized, SE = new Vector2(1f, -1f).normalized, SW = new Vector2(-1f, -1f).normalized, NW = new Vector2(-1f, 1f).normalized;

        /// <summary>Builds the Suspension Yard under the complex's root, all in the given material.</summary>
        public static void BuildInto(Transform complex, Material material)
        {
            Begin(material);
            Transform yard = Group(complex, "05 Suspension Yard");
            Transform towers = Group(yard, "Towers"), bridges = Group(yard, "Bridges"), stairs = Group(yard, "Stairs");
            Transform rails = Group(yard, "Guardrails"), connections = Group(yard, "Connections"), future = Group(yard, "Future Mechanics");

            Tower T(string name, float x, float z, float apothem, float height) => new() { Name = name, Centre = new Vector2(x, z), Apothem = apothem, Height = height };
            Tower arrival = T("Arrival Tower (+16)", 62f, -62f, 10f, 16f);
            Tower north = T("North Tower (+24)", 112f, -62f, 8f, 24f);
            Tower northEast = T("North-East Tower (+24)", 165f, -62f, 8f, 24f);
            Tower hub = T("Central Hub (+16)", 112f, -112f, 12f, 16f);
            Tower west = T("West Tower (+8)", 62f, -112f, 8f, 8f);
            Tower east = T("East Tower (+16)", 165f, -112f, 8f, 16f);
            Tower south = T("South Platform (+8)", 112f, -165f, 12f, 8f);
            Tower southEast = T("South-East Tower (+16)", 165f, -165f, 8f, 16f);
            // Jump and dash platforms, across measured gaps.
            Tower dash = T("Dash Platform (+16)", 0f, 0f, 5f, 16f);
            dash.Centre = hub.Centre + SW * (hub.Apothem + 10f + 5f);
            Tower jumpA = T("Jump Platform A (+8)", south.Centre.x - south.Apothem - 8f - 5f, south.Centre.y, 5f, 8f);
            Tower jumpB = T("Jump Platform B (+8)", jumpA.Centre.x - 5f - 12f - 5f, south.Centre.y, 5f, 8f);
            var all = new[] { arrival, north, northEast, hub, west, east, south, southEast, dash, jumpA, jumpB };

            // From the Concourse's south-east bridge (+16, 8 m wide) on to the arrival tower.
            Vector2 concourse = SE * 57.5f;
            Vector2 arrivalFlat = arrival.Flat(NW);
            Walkway(Group(bridges, "Bridge (from the Concourse)"), rails, concourse, SE, Vector2.Distance(concourse, arrivalFlat), 8f, 16f);
            arrival.Openings.Add((arrivalFlat, 8f));
            Marker(connections, "To Concourse (NW, +16)", arrivalFlat, 16f);
            // Standing on the arrival tower, facing those arriving, beside the way across it.
            AreaSign(towers, arrival.Centre + NW * 3f + new Vector2(1f, 1f).normalized * 5f, arrival.Height + Settings.signs.areaSignLift, NW,
                "05", "Suspension Yard", freeStanding: true);

            Link(bridges, stairs, rails, arrival, north, E);
            Link(bridges, stairs, rails, north, northEast, E);
            Link(bridges, stairs, rails, north, hub, S);
            Link(bridges, stairs, rails, arrival, west, S);
            Link(bridges, stairs, rails, arrival, hub, SE);
            Link(bridges, stairs, rails, west, hub, E);
            Link(bridges, stairs, rails, hub, east, E);
            Link(bridges, stairs, rails, northEast, east, S);
            Link(bridges, stairs, rails, hub, south, S);
            Link(bridges, stairs, rails, east, southEast, S);
            Link(bridges, stairs, rails, south, southEast, E);

            // The gaps: left open in the guardrails on both sides.
            Gap(future, hub, dash, SW, "Gap (10 m, +16): Hub to Dash Platform");
            Gap(future, south, jumpA, W, "Gap (8 m, +8): South Platform to Jump Platform A");
            Gap(future, jumpA, jumpB, W, "Gap (12 m, +8): Jump Platform A to B");

            // The rope swings: chains of ropes from tower to tower, swung along either way (see RopeChain). North to
            // north-east (+24) beside their bridge, 6.5 m north of its middle (clear of its rails), from the north tower's
            // north-east flat to the north-east tower's north-west flat, the gantry's masts to the north, away from the
            // bridge; and the hub to the south-east tower (+16), along the diagonal over the middle of the yard, where no
            // bridge goes.
            Transform swings = Group(yard, "Rope Swings");
            RopeChain(swings, north, northEast, E, new Vector2(0f, 6.5f), N, "Rope Swing (North Tower to North-East Tower, +24)");
            RopeChain(swings, hub, southEast, SE, Vector2.zero, NE, "Rope Swing (Central Hub to South-East Tower, +16)");

            // Stairs up from the ground (the recovery level) to the +8 towers.
            Recovery(stairs, west, W, BridgeWidth);
            Recovery(stairs, south, S, BridgeWidth);
            Recovery(stairs, jumpB, S, 4f);

            foreach (Tower tower in all)
            {
                Transform t = Group(towers, tower.Name);
                Vector2[] deck = Offset(Octagon(tower.Apothem), tower.Centre);
                Prism(t, "Deck", deck, tower.Height - Deck, tower.Height);
                Column(t, "Column", tower.Centre, new Vector2(ColumnWidth, ColumnWidth), 0f, tower.Height - Deck, accent: true, capped: false);
                // A banner down the tall columns' south faces (the concept art's yard).
                if (tower.Height >= 16f)
                {
                    Banner(t, tower.Centre + Vector2.down * (ColumnWidth / 2f), Vector2.down, tower.Height - Deck - 0.6f, Mathf.Min(8f, tower.Height - 6f), "05");
                }
                RailAround(rails, deck, tower.Height, tower.Openings.ToArray());
                // A floodlight mast in the middle of each large tower's deck, facing its first way in.
                if (tower.Apothem >= 8f && tower.Openings.Count > 0)
                {
                    Floodlight(t, tower.Centre, tower.Height, (tower.Openings[0].centre - tower.Centre).normalized);
                }
            }

            Marker(future, "Optional Higher Route (future): above +24", northEast.Centre, 32f);
            // Lawn and trees (sparser) over the recovery level below the towers, where they don't crowd a column, stair or
            // walkway.
            UnityEngine.Rect ground = UnityEngine.Rect.MinMaxRect(30f, -200f, 200f, -54f);
            Lawns(yard, ground, new[] { 0f }, 6, spacing: 2f, density: 0.5f);
            ScatterTrees(yard, ground, 0f, 5, spread: 1.6f);
        }

        /// <summary>
        /// A bridge from tower a to tower b along a compass direction: suspended if they're the same height, otherwise
        /// on columns at the lower height with a stair up at the higher tower's end.
        /// </summary>
        private static void Link(Transform bridges, Transform stairs, Transform rails, Tower a, Tower b, Vector2 direction)
        {
            Vector2 start = a.Flat(direction), end = b.Flat(-direction);
            float length = Vector2.Distance(start, end);
            Transform group = Group(bridges, $"Bridge ({a.Name} to {b.Name})");
            if (Mathf.Approximately(a.Height, b.Height))
            {
                Walkway(group, rails, start, direction, length, BridgeWidth, a.Height, suspended: true);
            }
            else
            {
                float low = Mathf.Min(a.Height, b.Height), high = Mathf.Max(a.Height, b.Height), flight = FlightLength(low, high);
                if (b.Height > a.Height)
                {
                    Walkway(group, rails, start, direction, length - flight, BridgeWidth, low);
                    Flight(stairs, $"Stair ({a.Name} to {b.Name})", end - direction * flight, direction, BridgeWidth, low, high);
                }
                else
                {
                    Flight(stairs, $"Stair ({a.Name} to {b.Name})", start + direction * flight, -direction, BridgeWidth, low, high);
                    Walkway(group, rails, start + direction * flight, direction, length - flight, BridgeWidth, low);
                }
            }
            a.Openings.Add((start, BridgeWidth));
            b.Openings.Add((end, BridgeWidth));
        }

        /// <summary>A measured gap between two platforms' facing flats, open in both their guardrails.</summary>
        private static void Gap(Transform markers, Tower a, Tower b, Vector2 direction, string name)
        {
            float side = 2f * Mathf.Min(a.Apothem, b.Apothem) * Mathf.Tan(22.5f * Mathf.Deg2Rad);
            a.Openings.Add((a.Flat(direction), 2f * a.Apothem * Mathf.Tan(22.5f * Mathf.Deg2Rad)));
            b.Openings.Add((b.Flat(-direction), side));
            Marker(markers, name, (a.Flat(direction) + b.Flat(-direction)) / 2f, a.Height);
            // Hazard stripes along both flats.
            Vector2 across = new Vector2(direction.y, -direction.x).normalized;
            foreach ((Tower tower, Vector2 flat) in new[] { (a, direction), (b, -direction) })
            {
                Vector2 centre = tower.Flat(flat);
                float half = tower.Apothem * Mathf.Tan(22.5f * Mathf.Deg2Rad);
                HazardStripes(markers, centre - across * half, centre + across * half, -flat, tower.Height);
            }
        }

        /// <summary>
        /// A chain of rope swings from tower a to tower b (of one height) along direction, on the line through a's centre
        /// offset by offset (b's centre is on it too, since the towers are lined up along direction): open in both
        /// towers' guardrails on the flats the line crosses, with hazard stripes along them, and ropes along the line
        /// between, the first and last RopeReach out from those flats and the rest evenly between, no more than
        /// RopeMaxSpacing apart. They hang from one steel beam along the line, RopeRise above the decks, carried by arms
        /// from graphite masts on the ground beside it (toward side), one at each rope but the first and last, so nothing
        /// stands near the decks. Nothing swung on a rope rises above its anchor, and a jump off one (simulated) takes the
        /// feet no more than about 8.5 m above the deck, the head well under the beam, so the beam is never in the way.
        ///
        /// Why these numbers (with the tuning in BasicMovementMode.asset: sprint 10 m/s, a jump of 2 m under gravity 40,
        /// hands 2.1 m above the feet): a sprint jump off the edge has the hands 3.8 to 4.1 m above the deck between 2
        /// and 4 m out, so the first rope, RopeReach out and hanging RopeDrop below the deck, is caught there about 8 m
        /// below its anchor. Catching hold keeps only the speed across the rope (falling speed is lost), so every rope is
        /// caught at about the speed of a run, and a release on the forward swing with the jump off (8 m/s up) carries the
        /// character 10 m or more on, still well above the deck, to the next. Simulated with the rope code's steps, every
        /// rope in these chains has about 0.8 s or more of its swing to let go in and still reach the next (or the far
        /// deck), without pumping the swing; pumping widens it. The chains are the same from either end, so they're
        /// swung along either way.
        /// </summary>
        private static void RopeChain(Transform parent, Tower a, Tower b, Vector2 direction, Vector2 offset, Vector2 side, string name)
        {
            Transform chain = Group(parent, name);
            Vector2 d = direction.normalized, through = a.Centre + offset;
            (Vector2 start, Vector2 startFlat) = Exit(a, through, d);
            (Vector2 end, Vector2 endFlat) = Exit(b, through, -d);
            float height = a.Height, length = Vector2.Distance(start, end);
            float span = length - 2f * RopeReach;
            int count = Mathf.CeilToInt(span / RopeMaxSpacing - 1e-3f) + 1;
            float spacing = span / (count - 1);

            // Open where the line leaves a and meets b, with hazard stripes along the edges.
            foreach ((Tower tower, Vector2 flat) in new[] { (a, startFlat), (b, endFlat) })
            {
                float half = tower.Apothem * Mathf.Tan(22.5f * Mathf.Deg2Rad);
                Vector2 centre = tower.Centre + flat * tower.Apothem, across = new Vector2(flat.y, -flat.x);
                tower.Openings.Add((centre, 2f * half));
                HazardStripes(chain, centre - across * half, centre + across * half, -flat, height);
            }

            // The beam along the line, the ropes from its underside, and the masts and arms that carry it.
            float anchor = height + RopeRise, beam = anchor + Settings.mechanics.ropeBracket, beamTop = beam + 0.4f;
            float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, sideYaw = Mathf.Atan2(side.x, side.y) * Mathf.Rad2Deg;
            Vector2 first = start + d * RopeReach;
            BoxXZ(chain, "Rope Gantry Beam", first + d * (span / 2f), new Vector2(0.6f, span + 1f), beam, beamTop, yaw, Surface.Steel);
            for (int i = 0; i < count; i++)
            {
                Vector2 at = first + d * (i * spacing);
                Rope(chain, new Vector3(at.x, anchor, at.y), RopeRise + RopeDrop);
                if (i == 0 || i == count - 1) { continue; }
                BoxXZ(chain, "Rope Gantry Mast", at + side * RopeMastOffset, new Vector2(0.8f, 0.8f), 0f, beamTop, yaw, Surface.Graphite);
                BoxXZ(chain, "Rope Gantry Arm", at + side * (RopeMastOffset / 2f), new Vector2(0.6f, RopeMastOffset), beam, beamTop, sideYaw, Surface.Steel);
            }
        }

        /// <summary>
        /// Where the line through "through" along direction leaves a tower's deck (an octagon, its flats facing the compass
        /// points), and the outward direction of the flat it leaves by.
        /// </summary>
        private static (Vector2 point, Vector2 flat) Exit(Tower tower, Vector2 through, Vector2 direction)
        {
            Vector2 relative = through - tower.Centre, flat = direction;
            float best = float.MaxValue;
            for (int k = 0; k < 8; k++)
            {
                float angle = 45f * k * Mathf.Deg2Rad;
                var normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float facing = Vector2.Dot(normal, direction);
                if (facing < 1e-4f) { continue; }
                float t = (tower.Apothem - Vector2.Dot(normal, relative)) / facing;
                if (t < best) { best = t; flat = normal; }
            }
            return (through + direction * best, flat);
        }

        /// <summary>A stair from the ground up to a tower's flat.</summary>
        private static void Recovery(Transform stairs, Tower tower, Vector2 direction, float width)
        {
            Vector2 flat = tower.Flat(direction);
            Flight(stairs, $"Stair (Ground to {tower.Name})", flat + direction * FlightLength(0f, tower.Height), -direction, width, 0f, tower.Height);
            tower.Openings.Add((flat, width));
        }
    }
}
