# The Meridian Complex — 03: The Ascension Spire (graybox)

The Ascension Spire, the complex's vertical-traversal district, is graybox in `MeridianComplex.unity`, from the sheets
`MeridianComplex-ZoneLayouts.png` (its detailed layout) and `MeridianComplex-RemainingAreas.png`. Level geometry only,
in the placeholder smooth-concrete material. It's built by `Assets/Editor/Level/MeridianAscensionSpireBuilder.cs`, as
part of **Tools > Character Showcase > Level > Build Meridian Complex**.

The spire is laid out in its own coordinates, centred on the central core, under a group at world (48, 0, 148).

## Where it is, and how it joins

North of the Concourse, placed so the end of its **west platform** (+12) meets the end of the Kinetic Gardens' east
bridge, "To Ascension Spire (E, +12)", at world (−2, 148): the gardens plan's upper bridge east. The Concourse's
**north bridge** (+16) carries on as a **skyway**, 8 m wide and 61 m long, north to the west platform, down a short
stair to its +12. So from the Concourse you reach the spire at +16, and the gardens at +12, in one place. The
**entry** (±0) is at the south end, at ground level, reached on foot from the ground round the Concourse.

## What's there (spire coordinates)

| Piece | Height | Notes |
|---|---|---|
| 2 Central core | +20 | An octagon 36 m across (the sheets' "core diameter 20" is taken as the tower; see below), with guardrails |
| 3 West platform, 4 East platform | +12 | 12 m wide, out to ±50; each up a 6 m stair to the core |
| 5 Entry bridge | ±0 | A low forecourt at the south end, between two pylons (12 m) |
| South terrace | +8 | 28 × 28 m, up a stair from the entry, and on up a stair to the core |
| 1 Summit tower | +56 | 28 × 28 m, north of the core, with a crown of posts and beams to +60 |
| Tower gallery | +20 | From the core's north-east face round to the foot of the summit stair |
| Switchback stair | +20 to +56 | Up the tower's east side: north to a landing at +38, then south beside the tower to a landing at +56 and the summit |
| Narrow balcony | +40 | On the tower's west face: reached only by climbing (future) |

Markers: To Kinetic Gardens (W, +12); a future connection off the east platform (E, +12), toward the Fabrication
Hall (the sheets' secondary path between them); Entry (S, ±0). Under **Future Mechanics**: ladders (core to the
balcony, 24 m; west platform to core), the tower's west face as a climbable wall, a wall run along the south terrace,
and a rope from the summit to the east platform. The Service Network's spire stairwell comes up just west of the
south wing, at world (23, 132).

## Where the sheets were ambiguous, and what was chosen

- **Size.** The zone sheet gives 110 × 130 m and a summit at +60; the remaining-areas sheet 90 × 120 and a summit at
  +56. It's about 100 × 120, with the summit floor at **+56** and its crown at +60.
- **Core diameter 20.** Too small for a core with four routes onto it: taken as the tower's core, and the central
  core platform is 36 m across, as the plan draws it. The tower itself is 28 m square (the sheets give a main tower of
  30 × 30).
- **Getting to the summit.** The sheets plan ladders, climbable walls and wall runs (all future). So the summit can
  be reached now, a switchback stair climbs the tower's outside: 120 steps of 0.3 m, in two flights of 24 m. The
  balcony at +40 is left for climbing.
- **The Concourse's +16 exit.** The spire's entry is at ±0 and its platforms at +12, so the Concourse's +16 bridge
  can't meet either directly: the skyway runs on to the west platform, which is also where the gardens' bridge ends.
- **Service band (−12 to 0).** The spire has no basement of its own; the Service Network comes up beside it.
