# The Meridian Complex — 06: The Service Network (graybox)

The Service Network, the complex's underground maintenance level, is graybox in `MeridianComplex.unity`, from the
sheet `MeridianComplex-RemainingAreas.png`. Level geometry only, in the placeholder smooth-concrete material, plus
simple lighting (below). It's built by `Assets/Editor/Level/MeridianServiceNetworkBuilder.cs`, as part of **Tools >
Character Showcase > Level > Build Meridian Complex**. It's laid out in world coordinates.

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
| Ventilation shafts | 8 × 8 m | Two, open from the network up to +16 above the ground |

Everything's built from axis-aligned rooms and passages: floors, ceilings and walls are generated from them, open
wherever two meet.

**Lighting**: plain point lights along the passages and in the rooms (no shadows), and a baked reflection probe in
each, so the network reflects itself instead of the sky. Under **Future Mechanics**: ladders up the shafts, a ceiling
climb, a crawl space and a hidden route.

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
