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

**Objects** (static stand-ins): 4 m crates (to be pushable), storage shelves with 2 m aisles (narrow passages),
assembly lines, machinery and tanks in the basements. Markers under **Future Mechanics** for sliding doors, a shutter,
a lift, a ladder, a crouch passage and maintenance access.

## Where the sheets were ambiguous, and what was chosen

- **Size.** The zone sheet gives 120 × 100 m; the other, 80 × 100. **120 × 100**, the detailed sheet's.
- **Loading bay.** One sheet puts it at +4, the other at ground level: it's at +4, with a loading dock ramp from the
  ground, so it's both.
- **Room layout.** The plan's rooms are irregular; they're a regular 3 × 3 grid here, in the plan's positions, with
  three rooms the sheet doesn't name (crate store, parts store, and the south-centre service corridor as a hall).
- **Catwalk height.** The iso view labels upper catwalks +16, the table 12: **+12**, which also matches the
  Concourse's north-east bridge.
- **Doors.** Stairways through walls are open to 6 m above their top step; there are no moving doors yet.
- **Roof.** None: the walls stop at +16, so the hall is lit by the sun and can be seen into.
