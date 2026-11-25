# Tutorial Ring mastery routes: design

Date: 2026-11-25. Status: for approval (after the builder check).

## Why

The Tutorial Ring's agility course (its four timed stretches) ends three of them in long runs of plain deck: Course 1
(Rigging Yard) 46 m, Course 2 (Run Wall) 68 m and Course 3 (The Cut) 57 m, measured from the spec (each face's
trailing "Lane" platform). The user wants those turned into places that show off mastery of the movement: each gets a
slower, safe **main route** and a harder **mastery route** that saves time on the course timer when flown cleanly. A
player who fails the mastery route falls into the main route and finishes slower; nobody is sent back. The lessons
(where people are still learning) get none.

## Goals

- In each of Course 1, 2 and 3's trailing stretch: a main route a newer player can always finish, and a mastery route
  over it that is clearly faster for one who chains the course's harder moves.
- A fall from anywhere on a mastery route lands on the main route, on floor, with no hazard and no respawn.
- The mastery routes use the movement's fast tech as the user describes it, which stays as it is: chained ground dashes
  (about 15 m/s average: Dashing's Dash Speed, Dash Duration and Dash Cooldown), slide-hopping (Crouching's Slide Start
  Speed), and the longest one-jump gap: **dash, jump at the end of the dash (within coyote time), then air dash just past
  the jump's peak** (the air dash resets the fall speed).
- That combo measured like the movement's other moves and added to the movement envelope, so its gaps are sized and
  lock-checked from it.
- Each stretch's routes have their own entry move and character, fitting the stretch's theme.

## Non-goals

- Anything in the lessons (Lessons 1 to 4), including Lessons 1's 55 m stretch.
- Course 4 (Finale): its trailing stretch is 37 m, under the 40 m threshold.
- Changing the movement: dash chaining and slide-hopping stay as they are.
- New level pieces: everything is built from the Graybox pieces the generator already uses.

## Anti-goals (it fails if...)

- ...a mastery route relies on jumping in the air (no air jumps: the Tutorial Ring's tuning has one jump), including the
  extra jump a dash off a ledge currently gives back. That's going away.
- ...a fall from a mastery route can land somewhere that isn't the main route (a gap's floor, a hazard, behind a wall).
- ...the main route can be done as fast as the mastery route (by dash-chaining through it, say). The main route has to
  cost time even for an expert, or the mastery route saves nothing.
- ...a mastery gap can be crossed with an easier move than intended (the locks hold each to its move).
- ...a newer player can't tell which way is the main route.

## Constraints

- The generator is the source: `Tools/LevelSpec/examples/tutorial_ring.py` writes `Assets/Showcase/Levels/TutorialRing.level.json`,
  which the Unity build reads (LevelSpecTools), then Set Up Scene. Every change goes into the generator, then the spec is
  regenerated, the level rebuilt and set up. The kept post-processing profile stays as tuned (`keepPostProcessing`).
- Every crossing is a `Crossing` the generator lock-checks against `Tools/LevelSpec/envelope.json` (`locks.check_all`):
  a gap is sized inside its move's reach (at most `locks.INTENDED_SHARE`, 95%) and beyond the next easier move's (by
  `locks.OTHER_SHARE`, 90%). The mastery routes' crossings are lock-checked the same way, and as hard as the course's
  last stretch: at the cap, `locks.INTENDED_SHARE` (0.95), since they're the expert's line.
- The lane: 12 m wide (`LANE`), decks at `FLOOR` (10 m), the ring's walls 17 m over the decks (`WALL_HEIGHT`), the roof's
  ribs above. Each stretch's trailing deck ends at the face's end, where the corner leads to the next stretch's face and
  its timing gate. Both routes must rejoin before the face's end.
- The envelope is written by `MovementEnvelopeWriter` from the default character's `MMotionMotor.Capabilities`, which
  `StandardMovementMode.MeasureCapabilities` measures (`StandardMovementMode.Capabilities.cs`, in the package). The new
  combo is measured there, as "sprint jump and air dash" is. The default character has two jumps; the combo must be
  measured with one (no second press of Jump), as the Tutorial Ring's character plays it.
- AGENTS.md's rules: docs name settings rather than quoting values; tests read tuning from settings; the hygiene check
  passes; the level is rebuilt and looked at after a builder change.

## Approaches considered

| | Approach | Trade-off | Verdict |
| --- | --- | --- | --- |
| A | Stacked: main route at deck level, mastery line high along the lane above it; any miss drops onto the main route | Needs height (there's 17 m); falls land naturally | Combined with C |
| B | Side by side: the lane split lengthwise, a mastery half whose falls slope into the main half | Simpler, but each half ~5 m: cramped wall runs and dash lines | Rejected |
| C | Cut-through: the main route forced round a big central structure, the mastery route straight over or through it | Clear saving, but one obstacle per stretch | Combined with A |

**Chosen by the user: A and C together.** Each stretch gets a long central structure (an "undercroft"): at deck level the
main route winds through it, slowly; on its top (its roof, out of the main route's reach: see The entry) the mastery
line runs straight along the lane. The mastery line's gaps are openings in the undercroft's roof, so a miss falls through onto the main route
below.

## The design

### The shape of each stretch

```
side view, along the lane (not to scale)

   entry: ledge grab   mastery line (roof of the undercroft, ~+6.5..+7.5 m)       exit
   ─────┐    ┌──────────┐   gap   ┌──────────┐    gap    ┌──────────┐        ┌─────────
        │    │  catwalk │  (hole) │ catwalk  │   (hole)  │ catwalk  │  drop  │
        │    └──┬───────┘         └───┬──────┘           └───┬──────┘   ↓    │
   deck │  undercroft: the main route winds round baffles, hurdles, pits     │  deck
   ═════╧════════════════════════════════════════════════════════════════════╧═════
                       ↑ a miss falls through a hole onto the main route
```

```
top view (the lane is 12 m wide)

   wall ═══════════════════════════════════════════════════════════════════
         main route →  ┌─────┐       ┌─────┐       ┌─────┐
        ───────────────┘     │  ┌────┘     │  ┌────┘     │  ┌────────────→
                       baffle│  │    baffle│  │    baffle│  │
        ─────────────────────┘  └──────────┘  └──────────┘  └─────
   wall ═══════════════════════════════════════════════════════════════════
         (the mastery line runs straight down the middle, above all of it)
```

- **The undercroft** spans the stretch's trailing deck, the lane's full width, with its roof at the mastery line's
  height. The trailing deck is the face-level "Lane" platform `build()` adds after the stretch, not stretch content, so the
  undercroft is built in the stretch (filling its face to the end) or by new face-level code; either way the ring walls'
  plan (`WALL_PLAN`: bands, windows, lamps) is told about it. Inside, the main route is made to cost time (the builder
  picks the mix per stretch; each stretch has at least one sink no dash or slide gets through):
  - **baffles**: walls across the lane from alternating sides, so the route zig-zags (longer, and segments too short
    for more than a dash or two);
  - **time sinks that go down, not up** (so the roof stays low and the main route never gets near the mastery line): a
    pit in the deck (the decks stand `FLOOR` over the ground, so there's room) dropped into, and climbed out of by a
    ladder up a wall too tall to ledge-grab (over the envelope's jump-to-the-ledge reach, by the locks' rule), or by a
    crate pushed under a ledge (as Course 4's crate climb). No crawl: a dash goes through a low passage crouched, and
    rolls chain through it, so it costs an expert nothing.
  - **hurdles** (the user's idea): low walls across the main route's open runs, the vault lesson's kind (`VAULT_WALL`
    high, under the envelope's jump height and over its step height). Vaulting one keeps only Vaulting's Vault Speed
    Retention of the character's speed; jumping clearly over keeps it all. They're spaced closer than a dash covers in a
    dash and its cooldown, so chained dashes run into them: they break up dash spam, and reward reading them and jumping.
    The time check counts a vault per hurdle against the main route's best (the expert jumps them, so it's the jump's
    time, not the vault's, that counts for the best case).
  It's lit and has headroom (over standing height everywhere), so it's never confusing: it's the obvious way ahead.
- **The mastery line** is on the undercroft's roof: catwalks along the lane's middle with gaps between them, and each
  gap is an opening through the roof over the main route. Its moves are the stretch's own (below), sized near their
  limit. Falling through any opening lands on the main route's floor (each opening's footprint is entirely over main
  route floor: no baffle, ladder or crate under it).
- **The entry** is the stretch's last ledge (each of stretches 1 to 3 ends with one, +2.8, +3.2 and +3.65 m over the
  deck, then a short "Below" deck, just before the trailing stretch). The mastery line's first edge is above and just
  beyond the ledge's top, its height over the ledge top locked to "jump to the ledge" (over a plain jump up's reach, by
  the locks' rule), so only a deliberate ledge grab off the ledge top gets up. That sets the roof's height (the ledge's
  top plus that climb, about 6.5 to 7.5 m over the deck), which is out of reach from the main route's deck (over the
  envelope's jump-to-the-ledge reach), so the only way up is the entry. The stretch's own moves (the bounce pad, the
  chimney) come later, on the mastery line.
- **The exit** drops back to the deck before the face's end (a drop the envelope allows without harm, or a roll), where
  the main route comes out too.
- **The saving**: the mastery route's time is at least a set margin under the main route's best (with dash chaining
  through its open parts). The generator checks it (see Checks).

### Course 1, Rigging Yard (46 m trailing, the course's first and easiest stretch)

- **Theme:** rigging: ropes and lines.
- **Entry:** the ledge grab from its last Ledge Top (above).
- **Mastery line:** a dash-chain run along a catwalk (slide-hopping works too), then a sprint jump and air dash gap at
  its reach (the locks' cap), then a dash, coyote jump and air dash gap. No rope: a rope crossing's time has no measured
  figure for the time check, and Course 1's stretch is the shortest, so it can't afford the ropes' slowness. It keeps the
  rigging look (rigging on the walls and roof, not to use).
- **Main route:** the undercroft with baffles, hurdles, and a pit with a ladder out.
- **Exit:** a drop and roll to the deck.

### Course 2, Run Wall (68 m trailing, the longest)

- **Theme:** wall runs.
- **Entry:** the ledge grab from its last Ledge Top.
- **Mastery line:** a bounce pad on the first catwalk launching to wall runs along a marked stretch of the ring wall at the mastery line's height, over roof openings
  (as the stretch's own bounce-into-wall-run, but longer: near the envelope's wall run length), chained into a wall jump
  to a catwalk; then a slide under a low beam on the catwalk (slide-hopping pays here); then a dash, coyote jump and air
  dash gap, the stretch's longest.
- **Main route:** the undercroft with baffles, hurdles, and a pit climbed out of with a crate under a ledge.
- **Exit:** a drop and roll.

### Course 3, The Cut (57 m trailing)

- **Theme:** the corridor: walls close together.
- **Entry:** the ledge grab from its last Ledge Top.
- **Mastery line:** a wall-jump chimney (two marked walls a corridor's width apart, as the stretch's corridor) up from
  the first catwalk to a higher one, then a catwalk with low beams to slide under in a row (slide-hopping, slide length from the envelope),
  then the course's hardest gap: dash, coyote jump and air dash at near the measured reach.
- **Main route:** the undercroft with baffles, hurdles, and a pit with a ladder out.
- **Exit:** a drop and roll.

### The new envelope move

- Measured in `StandardMovementMode.MeasureCapabilities` as a new jump, named "dash, jump and air dash" (the generator's
  `reach()` and `locks.MEASURED` read it by name; `_ruled_out`'s no-run-up rule must cover it too, since its name has no
  "sprint"), beside "sprint jump and air dash". **Off a lip**, as the user plays it: a sprinting run-up to an edge, a
  ground dash off it, Jump pressed within coyote time, then one air dash after the jump's peak, searching the timings for
  the furthest gap, measured from the lip, at the same height. The simulation's flat floor gains a lip for it.
- **No air jumps in it.** The extra jump the movement gives back now (walking or dashing off any ledge leaves a jump
  usable in the air: `Jumping`'s coyote rule, `MovementRules.TryUseJump`) is going away and must never be measured or
  relied on: the measurement turns air jumps off on its copy (Max Jumps 1) and only tries the jump within coyote time.
  Fixing that bug isn't part of this work.
- Only where the character can dash and jump. The envelope's `jumps` list gains it; `MovementEnvelopeWriter` writes it.
- For a gap to lock to it, it must reach at least `sprint jump and air dash / OTHER_SHARE / INTENDED_SHARE` (about 14 m
  now). If it comes out shorter, its gaps are sized for "sprint jump and air dash" at the cap instead, and the combo is
  just the fastest way across them. If it's no longer than "sprint jump and air dash" at all, it's left out, as moves are
  when they're not possible.
- **It's expert tech, not taught** (the user's call): the existing crossings stay locked only against the moves the
  lessons teach, so the new move may skip existing obstacles (Course 1's ropes and zipline, say). Nothing outside the
  three trailing stretches changes.

### Checks the generator runs

- **Locks:** each mastery crossing is a `Crossing` with its move (rope, wall run, sprint jump and air dash, dash jump
  and air dash), lock-checked as the existing ones are.
- **Falls land on the main route:** every roof opening's footprint is over main route floor (the generator knows both).
- **Time saved:** a route-time estimate per stretch, from the envelope and the settings (path lengths and move speeds:
  the mastery route at sprint speed with its moves' durations; the main route's open runs at the dash-chain speed, plus
  its time sinks' durations: ladder climbing at the ladder's climb speed, ledge climbs, the crate, a jump per hurdle). The
  mastery route must be at least a margin faster (a constant in the generator). The estimate is conservative: it gives
  the main route its best case and the mastery route a plain one.
- **Headroom:** the main route has standing headroom under the roof everywhere.
- **Out of reach:** the roof's underside and edges are out of the main route's reach (over jump-to-the-ledge) everywhere
  but the entry.

### Tests

- The generator's Python tests (`Tools/LevelSpec/tests/test_tutorial_ring.py`): each of Course 1 to 3 has a mastery
  route (its crossings named, their locks passing), every roof opening is over main route floor, the time check passes,
  and the lessons have none.
- The envelope: the new move is in `envelope.json`, longer than "sprint jump and air dash"; `MovementEnvelopeTests`
  pass with it.
- The capability: a PlayMode test (Movement area) that the character crosses a gap just under the measured "dash, jump
  and air dash" reach that way, and not one just over it.
- The Tutorial Ring's tests pass, and the level's look is checked after the rebuild (pictures from both routes).

### Docs

- docs/showcase/levels.md's Tutorial Ring section: the mastery routes, what each asks for.
- docs/movement.md (or the envelope's docs): the new measured move.

## Calls left to the builder

- Each stretch's exact measurements (catwalk widths, gap lengths, pit depths), sized from the envelope as the existing
  crossings are; the shares are at most `locks.INTENDED_SHARE` (0.95), the cap.
- The main route's time-sink mix per stretch, within the design above.
- The time margin's value.
- How the mastery line is signposted (orange edges as the rest of the course has).

## Decided in review

- The new move is expert tech, not taught: it may skip existing obstacles; nothing outside the three stretches changes.
- Each stretch's last ledge is its mastery entry; the bounce pad and chimney move onto the mastery lines.

## Calls made in review (the designer's, open to change)

- The time sinks go down into pits (ladders, a crate), not up over walls: up-and-over walls tall enough to force a
  ladder would need a roof over 6.5 m and put the mastery line in reach.
- No crawl as a time sink (a dash and rolls get through), and no rope on Course 1's mastery line (no timing figure;
  the stretch is short).
- The new move measured off a lip with air jumps off; if it's under the lock's 14 m, its gaps fall back to sprint jump
  and air dash at the cap.
- The extra-jump bug isn't fixed here.
