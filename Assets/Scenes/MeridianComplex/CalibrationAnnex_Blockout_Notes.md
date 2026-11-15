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
| Ledges | 1.0, 1.5, 2.0, 2.5, 3.0, 4.0 m | For jump height, vaulting and ledge climbing from the floor |
| Slopes | 10°, 20°, 30°, 40°, 50°, 60° | Ramps 3 m high up to a deck; the 60° one is past the 55° slope limit |
| Tunnel | 3.0, 2.5, 2.0, 1.5, 1.0 m clear | 3 m wide, the ceiling dropping every 6 m: for capsule clearance, crouching and sliding |
| Jump distances | 2, 4, 6, 8, 10, 12, 16, 20 m | A runway at +4 with a landing across each gap; fall short onto the floor |
| Jump lane | ±0 to +16 | Blocks 2 m higher each, 2 m apart |
| Wall run | 12 and 16 m gaps at +8 | A pair of platforms against each face of a wall to +20 (see below) |
| Ledge climb | 3, 4 and 6 m rises, ±0 to +12 | Shelves up the arrival platform's south face, and a climbable panel (see below) |

### Ledge climb

The arrival platform's south face (z −74, x −74 to −57) is 12 m high, more than a ledge climb takes in one go (a jump
catches ledges up to about 4.6 m above the feet: hands reach 1.4–2.6 m, and a jump rises 2 m). Four lanes go up it,
each 3 m wide and 1 m apart, from the west (x −73):

- **Panel:** a climbable panel from the floor to +12, climbed hand over hand and over its top onto the platform.
- **3 m, 4 m, 6 m:** shelves 2 m deep stepping up the face, each rise the lane's measure (the first, from the floor, is
  5 cm less): +3, +6, +9 then the platform; +4, +8 then the platform; +6 then the platform. Each is jump, catch the
  ledge, climb. 3 m is comfortably in reach, 4 m near the limit, 6 m out of reach: the bracketing failure, as the
  step and slope tests have one. A rise needs to be over 2.2 m for there to be room to hang below the ledge.

Each shelf is labelled with its height on top, each lane with its rise on the floor in front. The platform's guardrail
is open over each lane, so the climb ends on the platform.

### Wall run

A wall (x −79 to −78, z −118 to −98) rises to +20, with a pair of platforms at +8 against each face, the gap between
them beside it. A wall run carries about 9 m/s for 1.2 s, some 13 m from a jump off the start; a sprint jump about 6 m.

- **West face, 16 m** (the sheet's): more than a wall run carries, so it takes a wall run and a jump (or a dash) off it.
  Up by the stair at its west end.
- **East face, 12 m:** a wall run alone crosses it; a sprint jump doesn't. Up by a ladder on the start's east face.

The platforms reach the wall (they used to stop 1 m short of it), so sprinting off the start's end beside the
wall with a jump starts the run the same way every time. A ruler on the floor under each gap, along the wall, labelled
every 2 m, reads where a run fell short. Neither finish has a way down but dropping to the floor.

Every test can be reached on foot from the floor, and the floor is the recovery level for all of them.

## Where the sheet was ambiguous, and what was chosen

- **Where it joins.** The Concourse plan's south arrow was labelled "Calibration Annex (SW)", and the Concourse took
  its south exit (+8) for the annex. The new sheet puts the annex in the south-west district, so it joins the
  south-west exit (+12) instead, and the south exit is a future connection again.
- **Size.** The plan's dimensions say about 80 × 100 m, its table 80 × 80. It's 85 × 100.
- **The tests.** The sheet lists measured jump distances, step heights, slopes, ledge heights and tight spaces
  without giving values. The values are chosen to bracket the character's limits (0.35 m step, 55° slope); change
  them at the top of the builder.
- **The sheet's future mechanics.** It marked the ledge-climb face and the wall-run gap for mechanics to come. Now
  they exist: the face is climbed in lanes graded by rise (a single 12 m face can't be ledge-climbed), and the wall
  run has a second, shorter gap on the wall's other face, so the pair brackets what a wall run carries alone.
