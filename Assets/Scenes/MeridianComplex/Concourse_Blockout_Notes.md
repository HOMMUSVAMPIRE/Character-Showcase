# The Meridian Complex — 01: The Concourse (graybox)

The Concourse, the complex's central hub, is graybox in `MeridianComplex.unity`, alongside the Kinetic Gardens, from
the blockout diagram `MeridianComplex-Concourse.png`. It's level geometry only (no character, camera or other game
setup), in one placeholder material (see below). The scene is built by **Tools > Character Showcase > Level > Build Meridian
Complex** (`Assets/Editor/Level/MeridianComplexBuilder.cs`, which builds the Concourse with
`MeridianConcourseBuilder.cs`). Rebuilding replaces the scene: once it's being worked on by hand, stop rebuilding it,
or change the builders and rebuild.

Units are metres (1 unit = 1 m). The origin is the centre of the plaza floor; +Z is north, +X east.

## What's there

| Part | Height | Notes |
|---|---|---|
| Central plaza | ±0 | An octagon 40 m across its flats, open, with a two-tier octagonal dais (0.6 and 1.2 m) at its centre and four 2 × 2 × 4 m columns around it. |
| Terrace ring | +8 | The band between the plaza and the core's edge, an octagon 75 m across its flats: the main terrace level, all the way round. Guardrails (1.1 m) along its drops. |
| Broad stairs | ±0 to +8 | 12 m wide, up from the plaza to the ring, on the north, east and south sides. |
| North terrace | +16 | Beyond the ring to the north: the north broad stair carries on from a landing at +8 up to it. |
| Gate towers | to +36 | Two 6 × 6 m masses either side of the north terrace: the hub's tallest landmarks, framing the way north. |
| Upper terraces | NE +12, SE +16, SW +12, NW +20 | On the ring's corners, each up an 8 m wide stair from the ring. NW is beyond the core (see below). |
| Bridges | as their terrace | 20 m out from each exit, on columns, with guardrails, to where the next area joins: N (+16, 12 m wide), NE (+12), SE (+16), SW (+12), NW (+20), all 8 m wide, and E (+8, 10 m) and S (+8, 8 m). |
| West ramp | +8 to ±0 | 1:4 (about 14°), 12 m wide, down from the ring to the ground outside on the west. |
| Ground (placeholder) | ±0 | 440 × 440 m, a little over the complex's footprint, built by MeridianComplexBuilder (not part of the Concourse), open where the Service Network and the Fabrication Hall's basements go below it. |

Routes out end at empty markers under **Connections**, named for where they go: Ascension Spire (N), Fabrication
Hall (NE and E), Suspension Yard (SE), Calibration Annex (SW), Kinetic Gardens (NW, two: +8 and +20), and unnamed
future connections (S and W).

All the other areas are built, in the same scene, and join at those markers: see each one's notes beside this one.
The Kinetic Gardens' main way in is a gentle route down 1:6 ramps from a gap in the ring's north-west guardrail at +8,
just north-east of the +20 terrace; its high route is the stairs down from the end of the north-west +20 bridge.

Every stair step is at most 0.3 m high (under the character's 0.35 m step height) and 0.4 m deep, and every step is
solid to the ground.

## The placeholder material

All the level geometry, in both areas, uses `Materials/Placeholder_SmoothConcrete.mat`: URP Lit, untextured, a warm
cream (base colour 0.78, 0.72, 0.60), non-metallic, smoothness 0.2, for smooth matte concrete. The builder makes it
the first time and keeps it after that, so edit it by hand freely (rebuilding won't reset it); delete it to have the
builder make it afresh. Swap it for real materials once the layout is settled.

## The environment (sunny midday)

Under **Environment** in the scene, with its assets in `Environment/` beside it, all for URP:

- **Sun**: a directional light 65° up in the south-south-east, warm white, intensity 2, soft realtime shadows. It's
  the scene's sun (Lighting > Environment), so the sky's sun disk follows it.
- **Sky**: `Sky_SunnyMidday.mat`, Unity's procedural skybox. It lights the scene's ambient and default reflections,
  baked into `MeridianComplex/LightingData.asset` (the scene's lighting settings, `MeridianComplex_Lighting`, have
  baked and realtime GI off, so nothing's lightmapped). Generate Lighting again after changing the sky.
- **Haze**: exponential fog, density 0.0015, pale blue.
- **Post-processing**: a global volume with `MeridianComplex_PostProcessing.asset`, ACES tonemapping, light bloom, and
  a little more contrast and saturation. It shows in the Scene view with its effects on, and in any camera with
  post-processing on.

Shadows reach 150 m from the camera (raised from 50 in `Assets/Settings/PC_RPAsset.asset`, the PC quality level, so
the terraces' shadows don't drop out across the plaza; the mobile asset is still 50).

## Where the diagram was ambiguous, and what was chosen

- **Stair run.** The diagram's stairs (0.3 m rise, 0.6 m run) would need 16 m of run to climb 8 m: more than the 17.5 m
  ring between a 40 m plaza and a 75 m core, leaving no terrace behind them, and the drawn stairs are far shorter than
  their rise needs. Steps are 0.3 m by **0.4 m** (about 37°) instead, so a broad stair from the plaza to +8 takes
  10.8 m of the ring.
- **Plaza size.** The plan draws the plaza's flat floor about 30 m across, with the stairs inside it; the key dimensions
  and replication notes say at least 40 × 40 m open. **40 m open**, with the stairs in the ring instead.
- **Which terrace is at which height.** Read from the plan's labels round the ring: N +16, NE +12, E +8, SE +16, S +8,
  SW +12, W +8, NW +20, with +8 ("main terrace level") as the whole ring and the higher ones as upper terraces on it, as
  the "multiple circulation layers: ground, +8, +12/+16, +20" note suggests.
- **North +16.** Two flights of 8 m (with a 4 m landing at +8) don't fit in the ring, so the north terrace and its
  stair's second flight stand beyond the core's north edge (to about z = 56), much as the plan's north block sticks out.
- **North-west +20.** Twelve metres above the ring is too high for one flight in a corner: up to a landing at +14 within
  the ring, then on up past the core's corner to a terrace outside it (to about 51 m out along the diagonal).
- **West.** The plan shows no stair from the plaza on the west; the iso view calls out a 1:4 ramp from ±0 to +8. The
  west side has no stair from the plaza; the ramp is outside, down from the ring to the ground, as the route back to ±0
  from the west.
- **Exits.** The plan's south arrow is labelled "Calibration Annex (SW)". Its later sheet puts the annex in the
  south-west, so the annex joins the south-west exit (+12), and the south exit (+8) is a future connection. The east
  exit (+8) goes to the Fabrication Hall, as the later connectivity diagram shows; the west ramp's foot has no
  destination named.
- **+36.** The key dimensions give the hub's highest accessible point as about +36; nothing in the plan says how it's
  reached. The two gate towers rise to +36 as landmarks, but **aren't reachable**: decide where +36 is reached, and
  how, before building it.
- **Guardrails.** Along the ring's drops to the plaza and to the ground, round the upper terraces and the north
  terrace, and along the bridges, with gaps where stairs and bridges meet them. Not on the sides of stairs over open
  ground (the north and north-west upper flights) or the west ramp, which are solid to the ground.
- **Bridge widths.** 8 m for the corners and the south, 10 m east, 12 m north (the diagram gives 8 to 12).
