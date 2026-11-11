# The Meridian Complex — 07: The Calibration Annex (graybox)

The Calibration Annex, the complex's measured test ground, is graybox in `MeridianComplex.unity`, from the sheet
`MeridianComplex-RemainingAreas.png`. Level geometry only, in the placeholder smooth-concrete material. It's built by
`Assets/Editor/Level/MeridianCalibrationAnnexBuilder.cs`, as part of **Tools > Character Showcase > Level > Build
Meridian Complex**. It's laid out in world coordinates.

## Where it is, and how it joins

South-west of the Concourse, a walled yard from x −140 to −55 and z −155 to −55. The Concourse's south-west bridge
(+12), now "To Calibration Annex (SW, +12)", carries on along the diagonal to the **arrival platform** (+12) in the
yard's north-east corner, cut square to the bridge, and down a stair (landing at +6) to the floor. Ground entrances on
the north and east.

## The tests

Each is a row of measured variations, named with its measure:

| Test | Measures | Notes |
|---|---|---|
| Steps | 0.10, 0.20, 0.30, 0.35, 0.40, 0.50 m | Single steps, either side of the character's 0.35 m step height |
| Ledges | 1.0, 1.5, 2.0, 2.5, 3.0, 4.0 m | For jump height, and ledge climbing (future) |
| Slopes | 10°, 20°, 30°, 40°, 50°, 60° | Ramps 3 m high up to a deck; the 60° one is past the 55° slope limit |
| Tunnel | 3.0, 2.5, 2.0, 1.5, 1.0 m clear | 3 m wide, the ceiling dropping every 6 m: for capsule clearance and crouching (future) |
| Jump distances | 2, 4, 6, 8, 10, 12, 16, 20 m | A runway at +4 with a landing across each gap; fall short onto the floor |
| Jump lane | ±0 to +16 | Blocks 2 m higher each, 2 m apart |
| Wall run | 16 m gap at +8 | Two platforms beside a wall to +20 (future) |
| Ledge climb | to +12 | The arrival platform's south face (future) |

Every test can be reached on foot from the floor, and the floor is the recovery level for all of them.

## Where the sheet was ambiguous, and what was chosen

- **Where it joins.** The Concourse plan's south arrow was labelled "Calibration Annex (SW)", and the Concourse took
  its south exit (+8) for the annex. The new sheet puts the annex in the south-west district, so it joins the
  south-west exit (+12) instead, and the south exit is a future connection again.
- **Size.** The plan's dimensions say about 80 × 100 m, its table 80 × 80. It's 85 × 100.
- **The tests.** The sheet lists measured jump distances, step heights, slopes, ledge heights and tight spaces
  without giving values. The values are chosen to bracket the character's limits (0.35 m step, 55° slope); change
  them at the top of the builder.
