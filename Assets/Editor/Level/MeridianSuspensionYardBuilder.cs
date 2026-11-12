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
    /// columns with a stair at one end where they change height. Gaps of 8 to 12 m for jumps and dashes, rope
    /// anchors for the swings the sheet plans, and stairs up from the ground (the recovery level) to the +8 towers.
    /// The Concourse's south-east bridge (+16) joins the arrival tower. How the sheet was read is in
    /// SuspensionYard_Blockout_Notes.md beside the scene.
    /// </summary>
    public static class MeridianSuspensionYardBuilder
    {
        // ---- The sheet's dimensions (tunable: see the notes) ----

        private const float BridgeWidth = 6f;
        private const float ColumnWidth = 6f;
        private const float AnchorFrame = 32f;  // The rope-anchor gantry's top.

        private sealed class Tower
        {
            public string Name;
            public Vector2 Centre;
            public float Apothem, Height;
            public readonly List<(Vector2 centre, float width)> Openings = new();
            public Vector2 Flat(Vector2 direction) => Centre + direction.normalized * Apothem;
        }

        private static readonly Vector2 N = Vector2.up, S = Vector2.down, E = Vector2.right, W = Vector2.left;
        private static readonly Vector2 SE = new Vector2(1f, -1f).normalized, SW = new Vector2(-1f, -1f).normalized, NW = new Vector2(-1f, 1f).normalized;

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

            // Rope anchors: posts on the +24 towers, and a gantry over the hub to +32, for the swings to come.
            foreach (Tower tower in new[] { north, northEast })
            {
                BoxXZ(future, "Rope Anchor (+24)", tower.Centre + new Vector2(0f, -tower.Apothem + 2f), new Vector2(1f, 1f), tower.Height, tower.Height + 4f, 0f, Surface.Graphite);
            }
            Marker(future, "Rope Swing (future): North Tower to North-East Tower, +24", (north.Centre + northEast.Centre) / 2f, 24f);
            foreach (float side in new[] { -1f, 1f })
            {
                BoxXZ(future, "Anchor Gantry Column", hub.Centre + new Vector2(side * 7f, 0f), new Vector2(2f, 2f), hub.Height, AnchorFrame, 0f, Surface.Steel);
            }
            BoxXZ(future, "Anchor Gantry Beam (+32)", hub.Centre, new Vector2(16f, 2f), AnchorFrame - 1f, AnchorFrame, 0f, Surface.Steel);
            Marker(future, "Rope Swing (future): Hub Gantry, +32", hub.Centre, AnchorFrame);
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

        /// <summary>A stair from the ground up to a tower's flat.</summary>
        private static void Recovery(Transform stairs, Tower tower, Vector2 direction, float width)
        {
            Vector2 flat = tower.Flat(direction);
            Flight(stairs, $"Stair (Ground to {tower.Name})", flat + direction * FlightLength(0f, tower.Height), -direction, width, 0f, tower.Height);
            tower.Openings.Add((flat, width));
        }
    }
}
