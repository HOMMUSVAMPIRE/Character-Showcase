# The Meridian Complex — 05: The Suspension Yard (graybox)

The Suspension Yard, the complex's aerial district, is graybox in `MeridianComplex.unity`, from the sheet
`MeridianComplex-RemainingAreas.png`. Level geometry only, in the placeholder smooth-concrete material. It's built by
`Assets/Editor/Level/MeridianSuspensionYardBuilder.cs`, as part of **Tools > Character Showcase > Level > Build
Meridian Complex**. It's laid out in world coordinates.

## Where it is, and how it joins

South-east of the Concourse, about 125 × 125 m from x 52 to 173 and z −52 to −188. The Concourse's south-east bridge
(+16) carries on along the diagonal to the **arrival tower** (+16).

## What's there

Octagonal tower platforms, each on a 6 m column, on a grid about 50 m apart:

| Tower | Height | Where (world) |
|---|---|---|
| Arrival tower | +16 | (62, −62) |
| North tower, north-east tower | +24 | (112, −62), (165, −62) |
| West tower | +8 | (62, −112) |
| Central hub | +16 | (112, −112), 24 m across, with a rope-anchor gantry to +32 |
| East tower | +16 | (165, −112) |
| South platform | +8 | (112, −165), 24 m across |
| South-east tower | +16 | (165, −165) |

**Bridges** (6 m wide, guardrails) run along the compass directions between them. Between towers of one height they're
**suspended** (no columns): north to north-east (+24), hub to east (+16), east to south-east (+16), and a long one
along the diagonal from the arrival tower to the hub (+16). Where the height changes by 8 m, the bridge is on columns
at the lower height, with a stair up at the higher tower.

**Gaps** for jumps and dashes, open in the guardrails: hub to a dash platform (10 m, +16); south platform to jump
platform A (8 m, +8); A to jump platform B (12 m, +8). **Recovery**: the ground is the recovery level everywhere,
with stairs back up to the west tower, the south platform and jump platform B (all +8).

Under **Future Mechanics**: rope anchors on the +24 towers, the hub's gantry (+32), and markers for rope swings and the
optional higher routes.

## Where the sheet was ambiguous, and what was chosen

- **Layout.** The plan's towers aren't on a grid and its bridges run at all angles. They're on a grid here, with
  bridges along the compass directions, so every bridge meets an octagon's flat square on.
- **Footprint.** The sheet gives 140 × 140 m. It's about 125 × 125, so the yard stays clear of the Fabrication Hall's
  south wall.
- **Bridges that change height.** The elevation shows bridges sagging between towers of different heights; here they
  stay level, with a stair at one end, so every route is walkable now.
- **Ropes.** Rope lengths of 12 to 30 m are planned, but there's nothing to swing on yet: anchors and markers only.
- **Gap distances.** 8, 10 and 12 m, within the sheet's 8 to 16 m: tune them (in the builder) once the character's
  jump and dash are settled.
