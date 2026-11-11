using System.Collections.Generic;
using UnityEngine;
using static CharacterShowcase.EditorTools.Level.Graybox;

namespace CharacterShowcase.EditorTools.Level
{
    /// <summary>
    /// Builds the graybox of the Meridian Complex's first area, The Concourse (the central hub), into the complex's
    /// scene (see MeridianComplexBuilder): level geometry only, in the material it's given, from the blockout diagram
    /// MeridianComplex-Concourse.png. Laid out in metres (1 unit = 1 m), with the origin at the centre of the plaza
    /// floor, +Z north and +X east.
    ///
    /// The Concourse is under "01 Concourse", grouped by kind (plaza, terrace ring, upper terraces, stairs and ramps,
    /// bridges, guardrails, landmarks), with empty markers where routes leave for the complex's other areas, so later
    /// areas can be built alongside it and joined at those markers. How the diagram was read, and where it was
    /// ambiguous, is in Concourse_Blockout_Notes.md beside the scene.
    /// </summary>
    public static class MeridianConcourseBuilder
    {
        // ---- The layout (see the notes) ----

        private const float PlazaHalf = 20f;       // The open plaza: an octagon 40 m across its flats, at ±0.
        private const float CoreHalf = 37.5f;      // The Concourse core: an octagon 75 m across its flats.
        private const float Terrace = 8f;          // The main terrace ring, between the two octagons.
        private const float MaxRise = 0.3f;        // Each step at most this high (under the character's 0.35 m).
        private const float Run = 0.4f;            // Each step this deep (the diagram's 0.6 doesn't fit the ring).
        private const float BroadStairWidth = 12f; // Primary stairs.
        private const float StairWidth = 8f;       // Stairs up to the upper terraces.
        private const float BridgeRun = 20f;       // Bridges run this far out, to where the next area joins.

        /// <summary>
        /// The gentle route to the Kinetic Gardens (built with the gardens) leaves the ring's north-west edge at +8 this
        /// wide, its middle this far north-east of the diagonal: clear of the stair up to +20 and beside the +20 terrace.
        /// </summary>
        public const float GardensRouteWidth = 8f;
        private const float GardensRouteOffset = 13.25f;

        /// <summary>Where the gentle route to the Kinetic Gardens leaves the ring (world x, z; at +8): the middle of its top edge.</summary>
        public static Vector2 GardensRouteStart =>
            new Vector2(-1f, 1f).normalized * CoreHalf + new Vector2(1f, 1f).normalized * GardensRouteOffset;

        private static readonly float Tan225 = Mathf.Tan(22.5f * Mathf.Deg2Rad);
        private static readonly float Sqrt2 = Mathf.Sqrt(2f);
        // Half the length of each octagon's sides.
        private static float PlazaSide => PlazaHalf * Tan225;
        private static float CoreSide => CoreHalf * Tan225;

        /// <summary>Builds the Concourse under the complex's root, all in the given material.</summary>
        public static void BuildInto(Transform complex, Material material)
        {
            Begin(material);
            StepRise = MaxRise;
            StepRun = Run;
            BridgeLength = BridgeRun;

            var concourse = Group(complex, "01 Concourse");
            BuildPlaza(Group(concourse, "Central Plaza (±0)"));
            var ring = Group(concourse, "Terrace Ring (+8)");
            var upper = Group(concourse, "Upper Terraces");
            var stairs = Group(concourse, "Stairs and Ramps");
            var bridges = Group(concourse, "Bridges");
            var rails = Group(concourse, "Guardrails");
            var landmarks = Group(concourse, "Landmarks");
            var connections = Group(concourse, "Connections");
            BuildCardinals(ring, stairs, upper, bridges, rails, landmarks, connections);
            BuildDiagonals(ring, stairs, upper, bridges, rails, connections);
        }

        // ---- The areas ----

        /// <summary>The open plaza: the ground's floor, with a two-tier octagonal dais at its centre and four columns around it.</summary>
        private static void BuildPlaza(Transform plaza)
        {
            Prism(plaza, "Dais (Lower)", Octagon(4.5f), 0f, 0.6f);
            Prism(plaza, "Dais (Upper)", Octagon(3f), 0f, 1.2f);
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -9f : 9f, z = (i & 2) == 0 ? -9f : 9f;
                Box(plaza, $"Column {i + 1}", new Vector3(x, 2f, z), new Vector3(2f, 4f, 2f), 45f);
            }
        }

        private enum Cardinal { North, East, South, West }

        /// <summary>
        /// The four sides of the ring facing the compass points, each built facing north and turned into place: the
        /// ring at +8, a broad stair up from the plaza (north, east and south), and the route out.
        /// </summary>
        private static void BuildCardinals(Transform ring, Transform stairs, Transform upper, Transform bridges, Transform rails,
            Transform landmarks, Transform connections)
        {
            foreach (Cardinal side in new[] { Cardinal.North, Cardinal.East, Cardinal.South, Cardinal.West })
            {
                float turn = (int)side * 90f;
                Transform R = Turned(ring, $"{side} Terrace", turn), S = Turned(stairs, $"{side}", turn), B = Turned(bridges, $"{side}", turn);
                Transform G = Turned(rails, $"{side}", turn), C = Turned(connections, $"{side}", turn);
                float a = PlazaSide, b = CoreSide;
                bool broadStair = side != Cardinal.West;
                float stairEnd = broadStair
                    ? PlazaHalf + Flight(S, "Broad Stair (±0 to +8)", new Vector2(0f, PlazaHalf), Vector2.up, BroadStairWidth, 0f, Terrace)
                    : PlazaHalf;

                if (!broadStair)
                {
                    // West: no stair from the plaza (as drawn); the ring is solid here.
                    BoxXZ(R, "Terrace", new Vector2(0f, (PlazaHalf + CoreHalf) / 2f), new Vector2(2f * a, CoreHalf - PlazaHalf), 0f, Terrace);
                    Rail(G, new Vector2(-a, PlazaHalf), new Vector2(a, PlazaHalf), Terrace);
                }
                else
                {
                    float cheek = (2f * a - BroadStairWidth) / 2f, cheekX = BroadStairWidth / 2f + cheek / 2f;
                    float cheekEnd = side == Cardinal.North ? CoreHalf : stairEnd;
                    BoxXZ(R, "Cheek (Left)", new Vector2(-cheekX, (PlazaHalf + cheekEnd) / 2f), new Vector2(cheek, cheekEnd - PlazaHalf), 0f, Terrace);
                    BoxXZ(R, "Cheek (Right)", new Vector2(cheekX, (PlazaHalf + cheekEnd) / 2f), new Vector2(cheek, cheekEnd - PlazaHalf), 0f, Terrace);
                    Rail(G, new Vector2(-a, PlazaHalf), new Vector2(a, PlazaHalf), Terrace, (new Vector2(0f, PlazaHalf), BroadStairWidth));
                    if (side != Cardinal.North)
                    {
                        BoxXZ(R, "Terrace", new Vector2(0f, (stairEnd + CoreHalf) / 2f), new Vector2(2f * a, CoreHalf - stairEnd), 0f, Terrace);
                    }
                }

                switch (side)
                {
                    case Cardinal.North:
                        BuildNorth(R, S, upper, B, G, landmarks, C, stairEnd);
                        break;
                    case Cardinal.East:
                    case Cardinal.South:
                    {
                        // A bridge out at +8: east to the Fabrication Hall, south to an area still to be named.
                        float width = side == Cardinal.East ? 10f : 8f;
                        string name = side == Cardinal.East ? "To Fabrication Hall (E, +8)" : "Future Connection (S, +8)";
                        Bridge(B, G, C, name, new Vector2(0f, CoreHalf), Vector2.up, width, Terrace);
                        Rail(G, new Vector2(-b, CoreHalf), new Vector2(b, CoreHalf), Terrace, (new Vector2(0f, CoreHalf), width));
                        break;
                    }
                    case Cardinal.West:
                    {
                        // The 1:4 ramp, down from the ring to the ground outside: the route back to ±0 from the west.
                        const float width = 12f, length = Terrace * 4f;
                        Ramp(S, "Ramp (1:4, +8 to ±0)", new Vector2(0f, CoreHalf), Vector2.up, width, length, Terrace, 0f);
                        Rail(G, new Vector2(-b, CoreHalf), new Vector2(b, CoreHalf), Terrace, (new Vector2(0f, CoreHalf), width));
                        Marker(C, "Future Connection (W, ±0)", new Vector2(0f, CoreHalf + length), 0f);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// The north side: the broad stair goes on past the ring, from a landing at +8 up to a terrace at +16 beyond the
        /// core, flanked by two towers rising to +36, and on north by bridge to the Ascension Spire.
        /// </summary>
        private static void BuildNorth(Transform ring, Transform stairs, Transform upper, Transform bridges, Transform rails,
            Transform landmarks, Transform connections, float firstFlightEnd)
        {
            const float landingDepth = 4f, top = 16f, terraceDepth = 10f, terraceHalf = 12f;
            float landingEnd = firstFlightEnd + landingDepth;
            BoxXZ(ring, "Landing (+8)", new Vector2(0f, (firstFlightEnd + landingEnd) / 2f), new Vector2(BroadStairWidth, landingDepth), 0f, Terrace);
            float secondEnd = landingEnd + Flight(stairs, "Broad Stair (+8 to +16)", new Vector2(0f, landingEnd), Vector2.up, BroadStairWidth, Terrace, top);
            // The ring's back edge either side of the second flight.
            Rail(rails, new Vector2(-CoreSide, CoreHalf), new Vector2(CoreSide, CoreHalf), Terrace, (new Vector2(0f, CoreHalf), BroadStairWidth));

            Transform terrace = Group(upper, "North Terrace (+16)");
            Vector2[] shape = Rect(new Vector2(0f, secondEnd + terraceDepth / 2f), new Vector2(2f * terraceHalf, terraceDepth));
            Prism(terrace, "Terrace", shape, 0f, top);
            RailAround(rails, shape, top, (new Vector2(0f, secondEnd), BroadStairWidth), (new Vector2(0f, secondEnd + terraceDepth), BroadStairWidth));
            Bridge(bridges, rails, connections, "To Ascension Spire (N, +16)", new Vector2(0f, secondEnd + terraceDepth), Vector2.up, BroadStairWidth, top);

            // Landmarks: the tallest masses in the hub, framing the way north (not reachable; see the notes).
            foreach (float x in new[] { -terraceHalf - 3f, terraceHalf + 3f })
            {
                BoxXZ(landmarks, x < 0f ? "Gate Tower (West, +36)" : "Gate Tower (East, +36)",
                    new Vector2(x, secondEnd + terraceDepth / 2f), new Vector2(6f, 6f), 0f, 36f);
            }
        }

        private enum Diagonal { NorthEast, SouthEast, SouthWest, NorthWest }

        /// <summary>
        /// The four corners of the ring, each built as the north-east one and turned into place: the ring at +8, a
        /// stair from it up to an upper terrace (+12, +16, +12, +20), and a bridge out at that height.
        /// </summary>
        private static void BuildDiagonals(Transform ring, Transform stairs, Transform upper, Transform bridges, Transform rails, Transform connections)
        {
            foreach (Diagonal corner in new[] { Diagonal.NorthEast, Diagonal.SouthEast, Diagonal.SouthWest, Diagonal.NorthWest })
            {
                float turn = (int)corner * 90f;
                Transform R = Turned(ring, $"{corner} Terrace", turn), S = Turned(stairs, $"{corner}", turn);
                Transform U = Turned(upper, $"{corner} Terrace", turn), B = Turned(bridges, $"{corner}", turn);
                Transform G = Turned(rails, $"{corner}", turn), C = Turned(connections, $"{corner}", turn);

                // The ring's corner: between the plaza's corner and the core's, beside the sides facing north and east.
                Vector2[] corner8 = CornerBand(PlazaHalf * Sqrt2, CoreHalf * Sqrt2);
                Prism(R, "Terrace", corner8, 0f, Terrace);
                Vector2 plazaA = new(PlazaSide, PlazaHalf), plazaB = new(PlazaHalf, PlazaSide);
                Rail(G, plazaA, plazaB, Terrace); // Along the drop to the plaza.

                Vector2 outward = new Vector2(1f, 1f).normalized;
                float height = corner switch { Diagonal.SouthEast => 16f, Diagonal.NorthWest => 20f, _ => 12f };
                string name = corner switch
                {
                    Diagonal.NorthEast => "To Fabrication Hall (NE, +12)",
                    Diagonal.SouthEast => "To Suspension Yard (SE, +16)",
                    Diagonal.SouthWest => "To Calibration Annex (SW, +12)",
                    _ => "To Kinetic Gardens (NW, +20)",
                };
                // Along the diagonal: the stair starts a little in from the plaza's edge, on the ring.
                float u0 = PlazaHalf + (height <= 12f ? 2.5f : 0.5f);
                Vector2 Along(float u) => outward * u;
                Vector2 coreCorner = Along(CoreHalf);
                Vector2 coreA = new(CoreSide, CoreHalf), coreB = new(CoreHalf, CoreSide);

                if (corner != Diagonal.NorthWest)
                {
                    float uTop = u0 + Flight(S, $"Stair (+8 to +{height:0})", Along(u0), outward, StairWidth, Terrace, height);
                    Vector2[] terrace = Clip(corner8, -1f, -1f, -uTop * Sqrt2); // The corner beyond the stair's top.
                    Prism(U, $"Upper Terrace (+{height:0})", terrace, 0f, height);
                    RailAround(G, terrace, height, (Along(uTop), StairWidth), (coreCorner, StairWidth));
                    Bridge(B, G, C, name, coreCorner, outward, StairWidth, height);
                }
                else
                {
                    // +20 is too high to reach within the ring in one flight: up to a landing at +14, then on up
                    // beyond the core to a terrace outside it.
                    const float landing = 14f, landingDepth = 3f, terraceDepth = 12f, terraceHalf = 9f;
                    float u1 = u0 + Flight(S, "Stair (+8 to +14)", Along(u0), outward, StairWidth, Terrace, landing);
                    Vector2 landingCentre = Along(u1 + landingDepth / 2f);
                    BoxXZ(S, "Landing (+14)", landingCentre, new Vector2(StairWidth, landingDepth), 0f, landing, 45f);
                    float u2 = u1 + landingDepth;
                    float u3 = u2 + Flight(S, "Stair (+14 to +20)", Along(u2), outward, StairWidth, landing, height);
                    // The ring's outer edge, either side of the stair, with a gap for the gentle route to the gardens
                    // (in this group's frame, north-east of the diagonal is toward coreB).
                    Vector2 gardensRoute = coreCorner + new Vector2(1f, -1f).normalized * GardensRouteOffset;
                    Rail(G, coreA, coreB, Terrace, (coreCorner, StairWidth), (gardensRoute, GardensRouteWidth));
                    Marker(C, "To Kinetic Gardens (NW, +8)", gardensRoute, Terrace);
                    Vector2[] terrace = DiagonalRect(u3, u3 + terraceDepth, terraceHalf);
                    Prism(U, $"Upper Terrace (+{height:0})", terrace, 0f, height);
                    Vector2 far = Along(u3 + terraceDepth);
                    RailAround(G, terrace, height, (Along(u3), StairWidth), (far, StairWidth));
                    Bridge(B, G, C, name, far, outward, StairWidth, height);
                }
            }
        }

        // ---- Shapes particular to the Concourse ----

        /// <summary>
        /// The north-east corner of the ring between two octagons (x + z from inner to outer), bounded by the sides
        /// facing north and east (x and z at least the plaza's side half-length, at most the core's half-width).
        /// </summary>
        private static Vector2[] CornerBand(float inner, float outer)
        {
            float a = PlazaSide;
            Vector2[] shape = Rect(new Vector2((a + CoreHalf) / 2f, (a + CoreHalf) / 2f), new Vector2(CoreHalf - a, CoreHalf - a));
            shape = Clip(shape, -1f, -1f, -inner);
            return Clip(shape, 1f, 1f, outer);
        }

        /// <summary>A rectangle along the north-east diagonal, from u0 to u1 out from the centre, halfWidth either side of it.</summary>
        private static Vector2[] DiagonalRect(float u0, float u1, float halfWidth)
        {
            Vector2 along = new Vector2(1f, 1f).normalized, across = new Vector2(1f, -1f).normalized;
            return new[] { along * u0 - across * halfWidth, along * u0 + across * halfWidth, along * u1 + across * halfWidth, along * u1 - across * halfWidth };
        }
    }
}
