# The Meridian Complex — 05: The Suspension Yard (graybox)

The Suspension Yard, the complex's aerial district, is graybox in `MeridianComplex.unity`, from the sheet
`MeridianComplex-RemainingAreas.png`. Level geometry only, in the placeholder smooth-concrete material. It's built by
`Assets/Showcase/Editor/Levels/MeridianSuspensionYardBuilder.cs`, as part of **Tools > Meridian Motion > Showcase > Levels > Build
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
| Central hub | +16 | (112, −112), 24 m across |
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

**Rope swings** (under Rope Swings, built by `RopeChain`): two chains of ropes, each hanging from one steel gantry beam
along the crossing, carried by arms from graphite masts on the ground beside it (one at each rope but the first and
last, so nothing stands by the decks). Both are open in the guardrails, with hazard stripes, on the flats they leave
from, and the same from either end, so they're swung along either way:

| Swing | From, to (world) | Ropes | Anchors |
|---|---|---|---|
| North tower to north-east tower (+24) | the north tower's north-east flat (116.8, −55.5) to the north-east tower's north-west flat (160.2, −55.5): 43.4 m, beside their bridge, 6.5 m north of its middle | 5, at x 119.8, 129.2, 138.5, 147.8, 157.2 (z −55.5), 9.34 m apart | +36; masts to the north (z −52.7), away from the bridge |
| Central hub to south-east tower (+16) | the hub's south-east flat (120.5, −120.5) to the south-east tower's north-west flat (159.3, −159.3): 55.0 m along the diagonal, where no bridge goes | 6, from (122.6, −122.6) to (157.2, −157.2), 9.79 m apart | +28; masts to the north-east |

Every rope is 14 m long (from 12 m above the decks to 2 m below them). The first and last are 3 m out from the decks'
edges and the rest evenly between, no more than 10 m apart. Why, with the current tuning (sprint 10 m/s, a 2 m jump
under gravity 40, hands caught 2.1 m above the feet, 8 m/s up off a rope):

- A sprint jump off the edge has the hands 3.8 to 4.1 m above the deck 2 to 4 m out, so the first rope is caught about
  8 m below its anchor. A walk-off (no jump) still catches it, lower down.
- Catching hold keeps only the speed across the rope, so each rope is caught at about running speed, its falling speed
  lost. Let go of on the forward swing, with the jump off, the character flies 10 m or more on, its hands 4 to 7 m
  above the deck when they reach the next rope (well within its span), and lands on the far deck from the last.
- Simulated with the rope code's steps (gravity, damping, the 70° limit), every rope has about 0.8 s or more of its
  swing to let go in and still reach the next rope, without pumping; pumping the swing widens that. The highest a jump
  off takes the feet is about 8.5 m above the deck, so the head stays under the beam (12.3 m up).
- A missed rope drops to the ground (the recovery level), or onto the bridge beside the north swing.

Under **Future Mechanics**: the marker for the optional higher route above +24.

## Where the sheet was ambiguous, and what was chosen

- **Layout.** The plan's towers aren't on a grid and its bridges run at all angles. They're on a grid here, with
  bridges along the compass directions, so every bridge meets an octagon's flat square on.
- **Footprint.** The sheet gives 140 × 140 m. It's about 125 × 125, so the yard stays clear of the Fabrication Hall's
  south wall.
- **Bridges that change height.** The elevation shows bridges sagging between towers of different heights; here they
  stay level, with a stair at one end, so every route is walkable now.
- **Ropes.** The sheet plans rope anchors on the +24 towers, a gantry over the hub to +32 and rope lengths of 12 to
  30 m. A single rope there couldn't cross 37 to 55 m between towers, so each crossing is a chain of 14 m ropes,
  with the anchors 12 m above the decks (a run's speed lifts a swing only about 1.25 m whatever the rope's length,
  so longer ropes carry the character only a little further, and need taller masts). The posts on the +24 towers and the hub's gantry gave way to the chains'
  gantries. The north towers' swing runs beside their bridge rather than replacing it, so the bridge is still the way
  back for those who'd rather walk; the hub's runs where there wasn't a way across before.
- **The higher route.** Still a marker: nothing reaches above +24 yet (a zipline or bounce pad off the north-east
  tower would be the way to make it).
- **Gap distances.** 8, 10 and 12 m, within the sheet's 8 to 16 m: tune them (in the builder) once the character's
  jump and dash are settled.
