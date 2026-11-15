# The Meridian Complex — 04: The Fabrication Hall (graybox)

The Fabrication Hall, the complex's industrial district, is graybox in `MeridianComplex.unity`, from the sheets
`MeridianComplex-ZoneLayouts.png` (its detailed layout) and `MeridianComplex-RemainingAreas.png`. Level geometry only,
in the placeholder smooth-concrete material. It's built by `Assets/Editor/Level/MeridianFabricationHallBuilder.cs`, as
part of **Tools > Character Showcase > Level > Build Meridian Complex**.

The hall is laid out in its own coordinates (its middle at 0, +Z north) under a group at world (126, 0, 0): 120 × 100 m,
from x 66 to 186.

## Where it is, and how it joins

East of the Concourse, as in the connectivity diagram. Three ways in:

- **Main entrance (+8)**, from the Concourse's east bridge, "To Fabrication Hall (E, +8)" (it was a future
  connection), by a short walkway to a doorway in the west wall, onto the entrance gallery.
- **Catwalk (+12)**, from the Concourse's north-east bridge (the Concourse plan's "Fabrication Hall (NE)"), via a
  landing cut square to the bridge and a walkway to the north catwalk.
- **Basement (−8)**, from the Service Network's tunnel, at the maintenance room.

Also a ground-level service door on the south, and a loading dock up a 1:6 ramp to the loading bay on the west.

## What's there (hall coordinates)

A 3 × 3 grid of rooms with 1 m walls to +16 and no roof, as the sheet's numbered locations:

| | West | Centre | East |
|---|---|---|---|
| North | Crate store (+4) | 2 Storage (+8) | Parts store (+8) |
| Middle | 3 Loading bay (+4), crossed by the entrance gallery (+8) | 1 Main workshop (+8), 48 × 48 m | 4 Assembly wing (+4) |
| South | 6 Maintenance (−8) | 5 Service corridor (±0) | 7 Utility room (−8) |

Stairs (6 m wide) join every room to its neighbours that change floor height: gallery down to the loading bay, loading
bay down to maintenance, workshop down to the service corridor, the corridor down to both basements, and the assembly
wing up to the workshop and the parts store. Same-floor rooms have 4 × 6 m doorways.

**Catwalks at +12**: one right across the north rooms, with stairs down into storage and the parts store; a spur
through the storage wall into a ring round the workshop, which has a stair up from its floor. Guardrails along them.

**Objects**: 4 m crates, storage shelves with 2 m aisles (narrow passages), assembly lines, machinery and tanks in
the basements. Markers under **Future Mechanics** for what doesn't exist yet: the sliding doors (storage, crate store)
and the service door's shutter.

## Mechanics (under **Mechanics**; Set Up Scene makes them work)

- **Pushable crates** (4 m, speed 0.8): the loading bay's four, the workshop's three, and three in the crate store. The
  crate store's stack (two static crates, +4 to +12) is too tall to climb from the floor; the crate 4 m east of it
  (at −44, 46), too far to jump across from, pushed west against it is a step half way (two 4 m climbs, each a jump
  and a ledge grab). From the stack's top, a 2 m jump south through a gap in the north catwalk's guardrail (at
  x −52) lands on the catwalk (+12). The other two crate-store crates can be pushed there too. The parts store's two
  crates stay put: one is a foothold against the catwalk's edge.
- **Lift: assembly wing to catwalk.** A 4 × 4 m platform in the wing's north-east corner (x 53–57, z 22–26), open to
  the sky, rising 8 m from +4 to +12 (8 s there and back, 1.5 s pauses). At the top, a railed spur (x 53–57) runs north
  through the parts store wall (opened above +12 for it) to a gap in the north catwalk's south guardrail.
- **Ladder: workshop to ring.** On the workshop floor (+8) at (17, −14), up the inner face of the ring's east side to
  +12, through a 1.6 m gap in the ring's guardrail.
- **Maintenance access: catwalk to the roofline.** A steel platform at +16 in the parts store's north-east corner
  (x 53–59, z 42–49), hung between the north and east walls and level with their tops, railed on its open sides. A
  ladder on its south face at x 56 climbs to it from the catwalk's east end, through gaps in the catwalk's and the
  platform's guardrails; from the platform the walls' 1 m tops can be walked along.
- **Crouch passage** (no fixture): the west storage shelf by the wall (z 31) has a 2.4 m gap through it at x −17 under a
  low beam 1.2 m clear (crouch, slide or roll), a shortcut between the aisle by the wall and the one north of it.

## Where the sheets were ambiguous, and what was chosen

- **Size.** The zone sheet gives 120 × 100 m; the other, 80 × 100. **120 × 100**, the detailed sheet's.
- **Loading bay.** One sheet puts it at +4, the other at ground level: it's at +4, with a loading dock ramp from the
  ground, so it's both.
- **Room layout.** The plan's rooms are irregular; they're a regular 3 × 3 grid here, in the plan's positions, with
  three rooms the sheet doesn't name (crate store, parts store, and the south-centre service corridor as a hall).
- **Catwalk height.** The iso view labels upper catwalks +16, the table 12: **+12**, which also matches the
  Concourse's north-east bridge.
- **Doors.** Stairways through walls are open to 6 m above their top step; there are no moving doors yet (the sliding
  doors and shutter stay future markers).
- **Roof.** None: the walls stop at +16, so the hall is lit by the sun and can be seen into.
