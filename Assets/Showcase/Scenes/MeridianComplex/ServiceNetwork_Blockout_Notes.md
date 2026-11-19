# The Meridian Complex — 06: The Service Network (graybox)

The Service Network, the complex's underground maintenance level, is graybox in `MeridianComplex.unity`, from the
sheet `MeridianComplex-RemainingAreas.png`. Level geometry only, in the placeholder smooth-concrete material, plus
simple lighting (below). It's built by `Assets/Showcase/Editor/Levels/MeridianServiceNetworkBuilder.cs`, as part of **Tools >
Meridian Motion > Showcase > Levels > Build Meridian Complex**. It's laid out in world coordinates.

## Where it is, and how it joins

Its floor is at **−12**, under the Kinetic Gardens and on east past the Ascension Spire, with a long service tunnel
south to the Fabrication Hall. It comes up at:

- the **Kinetic Gardens' two service-network markers**, by stairwells through the gardens' floor (the south one is
  moved, see below);
- the **Ascension Spire**, by a stairwell just west of its south wing, at world (23, 132);
- the **Fabrication Hall's basement**: the tunnel's last stretch climbs a stair to a doorway in the hall's west wall,
  into the maintenance room at −8.

Where it comes up, the ground (and the gardens' floor) is open. Each stairwell has a guardrail round it, except on the
side its stair comes up on.

## What's there

| Piece | Size | Notes |
|---|---|---|
| Main corridors | 6 m wide, 6 m high | West, under the gardens; east, past the spire |
| North corridor | 6 m wide | Up to the gardens' north access |
| Pump room, junction room, spire access room | 16, 12 and 12 m across, 8 m high | Where corridors meet |
| Service tunnel | 4 m wide, 4 m high | About 190 m, east and then south to the hall |
| Ventilation shafts | 8 × 8 m | Two, open from the network up to +16 above the ground, a ladder up each (below) |

Everything's built from axis-aligned rooms and passages: floors, ceilings and walls are generated from them, open
wherever two meet.

**Lighting**: plain point lights along the passages and in the rooms (no shadows), and a baked reflection probe in
each, so the network reflects itself instead of the sky.

## Mechanics

The shafts' ladders are fixtures (made to work by Set Up Scene), under **Mechanics**, built by `BuildShaftClimbs`.
Inside, the shafts are open to the sky, so each ladder (−12 to +16, 28 m) is climbed off over the shaft's wall onto a
steel deck at +16 built against the chimney outside, on two I-beam columns, guarded but for the ladder's top:

- **West shaft**: the ladder's on the shaft's north wall, at (14, 112), the only wall solid all the way up (the main
  corridor comes in on its west side, the spire access room on its east). Its deck (x 6 to 18.5, z 112.5 to 115.5)
  reaches west to the Ascension Spire's skyway (+16), which runs past 3.5 m west of the shaft: the skyway's east
  guardrail (1.1 m, the spire's) is between them, hopped or vaulted. It works both ways: down from the skyway into
  the network, or up out of it.
- **East shaft**: the ladder's on the shaft's west wall, at (56, 60) (the service tunnel comes in on its north and
  south sides). Nothing's at +16 near it, so its deck (x 51.5 to 55.5, z 56 to 64) is the top of a **zipline** south
  to the landing at the end of the Concourse's north-east bridge (+12), outside the Fabrication Hall: from 3.3 m over
  the deck's north end, about 2.2 m over its open south edge (as the playground's zipline tower's is, so walking off
  it catches the cable), over the landing's north guardrail with the rider's feet about 1 m clear, to 3.4 m over the
  landing at (52, 40). A way out of the network, one way.

Under **Future Mechanics**, still markers, for mechanics that don't exist yet: a ceiling climb in the pump room
(climbing only handles surfaces leaning out up to 30°), a crawl space from the junction room (there's no crawl; a
crouch passage would only run beside the main corridor, to the same places), and a hidden route in the spire access
room.

## Where the sheet was ambiguous, and what was chosen

- **Where it is.** The sheet gives a 120 × 140 m network without saying where in the complex. It's placed under the
  gardens and the spire, where the gardens' plan has its service access points, with a tunnel to the hall's basement.
- **The gardens' south access.** The gardens plan puts it at plan (−160, −180), under the ramp up to A2: its stairwell
  is moved 16 m east and 7 m north, to plan (−143.6, −173), between the ramp, B1 and B2. The gardens' marker moved
  with it.
- **The plan's grid of rooms.** Simplified to corridors and a few rooms; the plan's crawl spaces and hidden routes are
  markers.
- **Tunnel height 4 to 8 m.** Corridors are 6 m high, rooms 8, the service tunnel 4.
- **Lighting.** The sheet asks for clear navigation lighting, so there are lights. Underground, the sky still tints
  the floors a little, because without baked GI nothing blocks the sky's ambient light: fine for a graybox.
