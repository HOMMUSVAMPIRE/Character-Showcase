# The Meridian Complex — The Kinetic Gardens (graybox)

The Kinetic Gardens, the complex's open-air traversal district, is graybox in `MeridianComplex.unity`, alongside the
Concourse, from the blockout plan `MeridianComplex-KineticGardens.png`. Level geometry only, in the placeholder smooth-concrete material (see Concourse_Blockout_Notes.md). The
scene is built by **Tools > Character Showcase > Level > Build Meridian Complex**, which builds the gardens with
`Assets/Editor/Level/MeridianKineticGardensBuilder.cs`; rebuilding replaces the scene. The gardens share the
Concourse's world coordinates.

## Where it is, and how it joins the Concourse

The Concourse plan puts the gardens to its **north-west**, joined at its north-west exit; the gardens plan's own
coordinates (x and z from −200 to −80) would put it south-west. The Concourse's description wins: the district is
laid out in the plan's coordinates under a group moved by (60, 0, 288), so it sits north-west of the Concourse, its
south entrance at world (−80, 88), its centre at (−80, 148). Pieces can still be checked against the plan by their
plan coordinates.

The gardens plan gives its connection to the Concourse as its **main entrance, on its south side, at ±0**. Two
routes join them (under the gardens, "Approach from the Concourse"), both coming down onto a **forecourt** in front of
the entrance (44 × 24 m, at ±0):

- **Gentle route (the main way in).** From a gap in the Concourse's north-west guardrail at +8, just north-east of
  its +20 terrace, down a 1:6 ramp (the plan's beginner grade, about 9.5°) to a landing at +4, and another 1:6 ramp
  to the forecourt: 8 m wide, 52 m long, no stairs. Its position and width are set in MeridianConcourseBuilder
  (GardensRouteStart and GardensRouteWidth), which leaves the gap in the guardrail for it.
- **High route.** Stairs down from the end of the Concourse's north-west bridge at +20 (the exit the Concourse plan
  gives for the gardens), with landings at +14 and +8, in the Concourse's stair profile. Coming this way you go up to
  +20 and down into the gardens; it's also the "return" climb from the gardens' +20 viewpoint.

The two routes run side by side along the north-west diagonal, about 5 m apart. Remove the high route (and end the
+20 bridge some other way) if only one route is wanted.

That placement also fits the gardens plan's other link: its upper bridge east at +12 heads toward the Ascension
Spire, which the Concourse puts to its north.

## What's there (plan coordinates)

Bands rise south to north, as in the plan: A ±0–4 (z −200 to −176), B +4–8, C +8–12, D +12–16, E +16–20 (z −108 to −80).

| Piece | Height | Where |
|---|---|---|
| District floor and boundary wall | ±0, wall 4 m | 120 × 120 m, open on the south (x −150 to −130) for the main entrance |
| A2 South-East Platform | +4 | up a 1:6 ramp (24 m) from the entrance plaza |
| B1 West Terrace (route 2) | +4 | up a stair from the entrance plaza |
| B2 Stepping Platform | +4 | across an 8 m gap from B1 |
| B3 Central Walk (route 3) | +8 | up a stair from B2; 10 m across from C1 (advanced jump) |
| C1 West Terrace | +8 | up a stair from B1 |
| C2 East Terrace | +12 | up a stair from B3, and from A2 by a stair along the east wall; the **upper bridge to the Ascension Spire** leaves it east at +12 (plan: (−80, −140, +12)) |
| D1 West Terrace | +12 | up a stair from C1 |
| D2 Central Platform | +12 | up a broad 1:4 ramp (12 m wide) from B3 |
| D3 Dash Island | +12 | between D1 and D2, across gaps of 10 and 12 m (advanced) |
| E1 North-West Terrace (route 5) | +16 | up a stair from D1 |
| Narrow Beam | +16 | 1 m wide, 12 m from E1 out over the gap to E2 (advanced: drop to E2 at its end) |
| Wall-Run Wall (planned) | to +20 | 12 m long, beside the beam |
| E2 North Platform (route 4) | +12 | a 4 m jump from D2 |
| E3 Goal Viewpoint (route 6) | +20 | north-east: up a stair from E2, a landing at +16, and a stair into it; or up a ledge chain of columns (+14, +16, +18, 2 m apart). (E2 is a 4 m jump from D2, so E3 takes at least that jump.) |

Markers under **Connections**: To Concourse (S, ±0), To Ascension Spire (E, +12), where the spire's west platform
meets the bridge, and the two service-network access points at (−143.6, −173) and (−100, −120), where the Service
Network's stairwells come up through the floor (see ServiceNetwork_Blockout_Notes.md; the plan's (−160, −180) is under
the ramp up to A2, so that one's moved). **Route Markers** mark the plan's route points 2 to 6.

**Beginner route** (stairs, ramps, gaps up to 8 m): entrance → west stair → B1 (2) → B2 → B3 (3) → broad ramp → D2 → E2
(4) → stairs → E3 (6). **Advanced route** (gaps of 10 to 12 m, a beam, a ledge chain): B1 → C1 → D1 → E1 (5) → beam →
E2 (4) → ledge chain → E3 (6), with the dash island and the B3–C1 gap as shortcuts. Every platform can be left by stair
or ramp, or by dropping to the floor below (±0), which is the recovery level everywhere.

## Where the plan was ambiguous, and what was chosen

- **Stair rise.** The plan's stairs rise 0.4 m a step, more than the character's 0.35 m step height. They rise 4 m in
  12 steps (0.33 m) instead, keeping the plan's 0.8 m run.
- **Detail.** The plan is denser than this: it's simplified to the labelled platforms, the route points and enough
  pieces to make each route continuous, keeping the plan's bands, heights, gap widths, ramp slopes and recovery widths.
  Platforms are rectangles; the plan's irregular outlines and smaller blocks aren't copied.
- **Jump heights.** The plan doesn't say how high a jump goes, so nothing asks for one: every rise is by stair, ramp or
  (on the advanced route) a drop, except the ledge chain's 2 m steps, which assume a jump of at least that. Tune the
  gaps and the chain (constants at the top of the builder) once the character's jump is settled.
- **Ledge thickness.** Ledges are the platforms' own edges (solid to the ground), so at least 0.5 m thick everywhere.
- **Service network.** Built: its stairwells come up through the floor at the two access points.
- **Floor.** The district floor sits 5 cm above the Concourse's ground placeholder, so the two don't flicker where they
  overlap.
