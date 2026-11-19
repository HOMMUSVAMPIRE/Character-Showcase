using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MeridianMotion.Showcase.Levels
{
    // Routes: flights of stairs, ramps, bridges and walkways.
    public static partial class Graybox
    {
        // ---- Pieces ----

        /// <summary>
        /// A straight flight of solid steps from base (its bottom edge's middle) along direction, rising from y0 to y1,
        /// each at most StepRise high and StepRun deep, and solid down to floor (the ground, unless it's below ground).
        /// It's one object: one mesh of the whole flight (see FlightMesh) and a box collider per step. Returns its
        /// length along the direction.
        /// </summary>
        [Piece]
        public static float Flight(Transform parent, string name, Vector2 basePoint, Vector2 direction, float width, float y0, float y1,
            float floor = 0f)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Flight), parent, name, basePoint, direction, width, y0, y1, floor);
            int steps = Mathf.CeilToInt((y1 - y0) / StepRise - 1e-4f);
            float rise = (y1 - y0) / steps;
            float yaw = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            // Solid to the ground, so a flight over open ground stands on something.
            float bottom = Mathf.Min(floor, y0);

            // Laid out facing north (+z up the flight) from its base, and turned to face the direction.
            var flight = new GameObject(name);
            flight.transform.SetParent(parent, false);
            flight.transform.SetLocalPositionAndRotation(new Vector3(basePoint.x, 0f, basePoint.y), Quaternion.Euler(0f, yaw, 0f));
            if (steps < 1) { return 0f; }
            FlightMesh(steps, width, StepRun, y0, rise, bottom).AddTo(flight, name);
            for (int i = 0; i < steps; i++)
            {
                float top = y0 + rise * (i + 1);
                var step = flight.AddComponent<BoxCollider>();
                step.center = new Vector3(0f, (bottom + top) / 2f, (i + 0.5f) * StepRun);
                step.size = new Vector3(width, top - bottom, StepRun);
            }
            MakeStatic(flight);
            return steps * StepRun;
        }

        /// <summary>
        /// The visible solid of a flight of steps, up +z from z = 0 and centred on x = 0: its steps' treads (each with an
        /// orange nosing strip along its front edge) and risers (the first running down to the bottom), its back, and its
        /// two sides, each side one stair-shaped face. It has no underside, since a flight always stands on its floor.
        /// That's 10 triangles a step and 2 more, with no faces hidden inside it and none overlapping (where a box per
        /// step's sides met in one plane and flickered).
        /// </summary>
        private static GrayboxMesh FlightMesh(int steps, float width, float run, float y0, float rise, float bottom)
        {
            float half = width / 2f, length = steps * run;
            var mesh = new GrayboxMesh();
            const GrayboxMaterial concrete = GrayboxMaterial.Concrete;

            for (int i = 0; i < steps; i++)
            {
                float z0 = i * run, zn = z0 + Settings.stairs.nosingDepth, z1 = z0 + run, top = y0 + rise * (i + 1), below = i == 0 ? bottom : top - rise;
                mesh.Quad(GrayboxMaterial.Accent, Vector3.up, new Vector3(-half, top, z0), new Vector3(half, top, z0), new Vector3(half, top, zn), new Vector3(-half, top, zn));
                mesh.Quad(concrete, Vector3.up, new Vector3(-half, top, zn), new Vector3(half, top, zn), new Vector3(half, top, z1), new Vector3(-half, top, z1));
                mesh.Quad(concrete, Vector3.back, new Vector3(-half, below, z0), new Vector3(half, below, z0), new Vector3(half, top, z0), new Vector3(-half, top, z0));
            }
            float summit = y0 + rise * steps;
            mesh.Quad(concrete, Vector3.forward, new Vector3(-half, bottom, length), new Vector3(half, bottom, length), new Vector3(half, summit, length),
                new Vector3(-half, summit, length));

            // Each side's outline: the bottom front corner, then each step's front top corner (P) and back top corner
            // (Q), then the bottom back corner. It's triangulated with the fewest triangles (as many as its corners, less
            // two) and no extra vertices: each step after the first gives the triangle under its tread and beside its
            // riser (Q before, P, Q after), and what's left, under the line of back corners along the stair's slope, is
            // fanned from the bottom back corner, which sees all of it.
            foreach (float x in new[] { -half, half })
            {
                Vector3 normal = new Vector3(Mathf.Sign(x), 0f, 0f);
                var outline = new List<Vector3> { new(x, bottom, 0f) };
                for (int i = 0; i < steps; i++)
                {
                    float top = y0 + rise * (i + 1);
                    outline.Add(new Vector3(x, top, i * run));
                    outline.Add(new Vector3(x, top, (i + 1) * run));
                }
                outline.Add(new Vector3(x, bottom, length));
                int first = mesh.Face(normal, outline);
                int front = first, back = first + outline.Count - 1;
                // P(i): step i's front top corner, steps counted from 0; Q(i): step i's back top corner, counted from 1.
                int P(int i) => first + 1 + 2 * i;
                int Q(int i) => first + 2 * i;
                for (int i = 1; i < steps; i++) { mesh.Triangle(concrete, Q(i), P(i), Q(i + 1), normal); }
                mesh.Triangle(concrete, back, front, P(0), normal);
                mesh.Triangle(concrete, back, P(0), Q(1), normal);
                for (int i = 1; i < steps; i++) { mesh.Triangle(concrete, back, Q(i), Q(i + 1), normal); }
            }
            return mesh;
        }

        /// <summary>How long a flight from y0 to y1 is, in the current stair profile.</summary>
        public static float FlightLength(float y0, float y1) => Mathf.CeilToInt(Mathf.Abs(y1 - y0) / StepRise - 1e-4f) * StepRun;

        /// <summary>A ramp from start along direction, falling (or rising) from y0 to y1 over its length: a tilted slab, with a support under its high end.</summary>
        [Piece]
        public static void Ramp(Transform parent, string name, Vector2 start, Vector2 direction, float width, float length, float y0, float y1)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Ramp), parent, name, start, direction, width, length, y0, y1);
            Transform ramp = Group(parent, name);
            float yaw = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            float slope = Mathf.Atan2(y0 - y1, length) * Mathf.Rad2Deg;
            float slab = Mathf.Sqrt(length * length + (y1 - y0) * (y1 - y0));
            Vector2 middle = start + direction * (length / 2f);
            // Its top surface meets y0 at the start and y1 at the end: the slab's centre is half its thickness below.
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(slope, 0f, 0f);
            Vector3 centre = new Vector3(middle.x, (y0 + y1) / 2f, middle.y) - rotation * Vector3.up * (Deck / 2f);
            GameObject deck = Box(ramp, "Ramp", centre, new Vector3(width, Deck, slab));
            deck.transform.localRotation = rotation;
            float spacing = Settings.decks.rampSupportSpacing;
            Vector2 support = Settings.decks.walkwayColumnSize;
            for (float d = 0.75f * spacing; d < length - 2f; d += spacing)
            {
                float y = Mathf.Lerp(y0, y1, d / length) - Deck;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector2 at = start + direction * d + new Vector2(direction.y, -direction.x) * (side * (width / 2f - support.x));
                    if (y > 0.5f) { IBeamColumn(ramp, "Support", at, support, 0f, y, yaw); }
                }
            }
        }

        /// <summary>
        /// A bridge out from start along direction at the given height, with guardrails along both sides, columns
        /// down to the ground, a marker at its far end where the next area joins, a cyan accent post at each corner of
        /// its deck, so the way from one area to another reads from a distance, and a sign gantry over its start naming
        /// where it goes (when its name says: "To [place] (...)").
        /// </summary>
        [Piece]
        public static void Bridge(Transform bridges, Transform rails, Transform connections, string name, Vector2 start, Vector2 direction,
            float width, float height)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Bridge), bridges, rails, connections, name, start, direction, width, height);
            Transform bridge = Group(bridges, name.Replace("To ", "Bridge to ").Replace("Future Connection", "Bridge"));
            Vector2 end = Walkway(bridge, rails, start, direction, BridgeLength, width, height);
            Marker(connections, name, end, height);
            string destination = Destination(name);
            if (destination != null) { BridgeGantry(bridge, destination, start, direction, width, height); }
            Vector2 side = new Vector2(direction.y, -direction.x) * (width / 2f);
            // Set in from the bridge's ends a little, so the posts' faces aren't in the plane of the top rails' ends.
            float inset = Settings.fixtures.accentPost.x / 2f + 0.02f;
            foreach (Vector2 at in new[] { start + direction * inset, end - direction * inset })
            {
                AccentPost(bridge, at + side, height);
                AccentPost(bridge, at - side, height);
            }
        }

        /// <summary>
        /// A deck from start along direction for length at the given height, with guardrails along both sides, and
        /// I-beam columns down to the ground; or, if it's suspended (hung between the things at its ends), a truss
        /// under each side instead. Returns its far end.
        /// </summary>
        [Piece]
        public static Vector2 Walkway(Transform parent, Transform rails, Vector2 start, Vector2 direction, float length, float width,
            float height, bool suspended = false)
        {
            using LevelSpecRecorder.Scope recording = LevelSpecRecorder.Call(nameof(Walkway), parent, rails, start, direction, length, width, height, suspended);
            float yaw = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            Vector2 end = start + direction * length, side = new Vector2(direction.y, -direction.x) * (width / 2f);
            BoxXZ(parent, "Deck", start + direction * (length / 2f), new Vector2(width, length), height - Deck, height, yaw);
            LevelBuilderSettings.DeckSettings decks = Settings.decks;
            if (!suspended)
            {
                for (float d = decks.walkwayColumnSpacing / 2f; d < length; d += decks.walkwayColumnSpacing)
                {
                    foreach (float s in new[] { -1f, 1f })
                    {
                        IBeamColumn(parent, "Column", start + direction * d + side * (s * decks.walkwayColumnSpread), decks.walkwayColumnSize, 0f,
                            height - Deck, yaw);
                    }
                }
            }
            else
            {
                // Just in from the deck's edges, so the edge is still a ledge with something to hang against.
                Vector2 inset = side.normalized * (width / 2f - decks.trussInset);
                float depth = Mathf.Min(decks.trussMaxDepth, length / 6f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Truss(parent, "Truss", start + inset * s, end + inset * s, height - Deck - depth, height - Deck);
                }
            }
            Rail(rails, start + side, end + side, height);
            Rail(rails, start - side, end - side, height);
            return end;
        }
    }
}
