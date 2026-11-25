# Tutorial Ring Mastery Routes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** In the Tutorial Ring's Course 1–3 trailing stretches, a slow main route through an undercroft and a fast
mastery line on its roof (falls drop into the main route), with the dash, coyote-jump, air-dash move measured into the
envelope.

**Architecture:** The package measures the new move (`StandardMovementMode.MeasureCapabilities`), the envelope writer
writes it, and the levelspec lock check knows it. The generator (`tutorial_ring.py`) gives stretches 1–3 the room left
on their face and fills it with an undercroft (main route: baffles, hurdles, a pit) whose roof is the mastery line
(catwalks, gaps over the main route), entered by a ledge grab from the stretch's last ledge, and checks locks, falls and
times. Then the spec is regenerated, the level rebuilt and set up.

**Tech Stack:** C# (Unity 6000.6, the movement simulation), Python 3 (the levelspec tool and generator), NUnit/unittest.

**Spec:** `docs/superpowers/specs/2026-11-25-tutorial-ring-mastery-routes-design.md`

## Global Constraints

- No air jumps anywhere in a mastery route or in the new measurement (Max Jumps 1 on the measuring copy; the jump only within coyote time).
- Lock shares at most `locks.INTENDED_SHARE` (0.95); other moves at most `locks.OTHER_SHARE` (0.90) of a need.
- The new move is expert tech, not taught: existing crossings aren't re-checked against it; nothing outside Course 1–3's trailing stretches changes.
- Every fall through a roof opening lands on main route floor; the roof is out of the main route's reach except at the entry.
- The main route has standing headroom everywhere; no crawls; time sinks go down (pits), not up.
- The generator is the source; regenerate the spec, rebuild (keeps the tuned post-processing), Set Up Scene.
- Hygiene check passes; Python tests pass; the affected Unity areas pass (Movement, Levels); commits only if the user asks.

## Review Focus

1. A fall from a catwalk's edge (not only through an opening) must land on main route floor, never on a baffle's top or in a wall gap.
2. The main route must stay finishable by a newer player: every pit has its ladder or crate; no move beyond what the lessons teach is needed.
3. A player entering the undercroft from the stretch's "Below" deck must not be able to get onto the roof from inside (baffle tops, ladder tops, crates).
4. The ring walls' dressing (windows, bands, lamps, banners) beside the undercroft mustn't cut through the roof or catwalks.
5. The new measured move must not include an air jump even on the default character (two jumps).

---

### Task 1: Measure "dash, jump and air dash"

**Files:** `Assets/MeridianMotion/Runtime/Characters/MovementModes/StandardMovementMode.Capabilities.cs`; a PlayMode test in `Assets/MeridianMotion/Tests/PlayMode/Movement/` (the capability tests' fixture: find the one testing "sprint jump and air dash"); `Tools/LevelSpec/levelspec/locks.py`; `Tools/LevelSpec/tests/test_locks.py`; `Tools/LevelSpec/envelope.json` (rewritten by Write Movement Envelope); docs/movement.md (the envelope's moves).

**Interfaces:** Produces the envelope jump `"dash, jump and air dash"` (distance from the lip, one jump), and `locks.MEASURED["gap"]["dash, jump and air dash"]`.

- [ ] Write the failing PlayMode test: the default character's capabilities list "dash, jump and air dash", longer than "sprint jump and air dash"; measured with one jump (its value is the same with Max Jumps set to 1 on the rig).
- [ ] Run it: fails (no such move).
- [ ] Implement: a lip version of the measurement (a run-up to an edge over a drop, the gap measured from the edge to a landing at the same height), a ground dash before the edge, Jump within coyote time after leaving it, one air dash after the jump's peak; search the dash, jump and air-dash timings; on a copy with Max Jumps 1 (no air jumps). Add only if longer than "sprint jump and air dash".
- [ ] Run it: passes. Run Write Movement Envelope; check `envelope.json` gains the move.
- [ ] Python: `locks.MEASURED` gains it; `_ruled_out`'s no-run-up rule covers it (it needs the run-up). Test in `test_locks.py`. Run the levelspec tests.
- [ ] Hygiene; `MovementEnvelopeTests` (Levels area) pass.

### Task 2: The undercroft and mastery routes in the generator

**Files:** `Tools/LevelSpec/examples/tutorial_ring.py`; `Tools/LevelSpec/tests/test_tutorial_ring.py`.

**Interfaces:** Stretches 1–3 (`stretch_0..2`) take the room left on their face (`room`, metres from their start to the face's end) and fill it; `mastery_route(st, z, end, entry_top, line)` builds an undercroft from z to end and its roof line; module-level `MASTERY` (per stretch: the openings and main route floor spans, the two routes' time estimates) for the tests.

- [ ] Tests first (`test_tutorial_ring.py`): Courses 1–3 have mastery crossings (an entry climb locked to "jump to the ledge", and gaps locked to their moves), none in the lessons; every roof opening and catwalk edge's fall footprint is over main route floor; the roof's top is out of reach (over jump-to-the-ledge / OTHER_SHARE) from the main route's deck and from every hurdle's top; every pit has a way out (a ladder or a crate); each stretch's mastery route is at least `MASTERY_MARGIN` seconds faster than its main route's best case; the lock check passes; no "Lane" filler is left on faces 7, 6 and 5.
- [ ] Run them: fail.
- [ ] Implement, as the design says (each stretch's measurements from the envelope and the shares; the time estimate's speeds read from the movement tuning by name, `tuning()`): the undercroft (main deck, baffles to the roof's underside, hurdles of `VAULT_WALL`, a pit with a ladder (Courses 1, 3) or a crate (Course 2) out), the roof catwalks (inside the walls' faces, no skirts), the openings, the entry from the last ledge, each stretch's line (Course 1: dash run, sprint-jump-and-air-dash gap, the new move's gap; Course 2: a wall run along a marked outer wall over an opening, a slide beam, the new move's gap; Course 3: a wall-jump chimney to a higher tier, slide beams, the new move's gap), the exit; the ring walls told (no windows beside the undercroft; bands follow the decks).
- [ ] Run them: pass. Write the spec: `python3 Tools/LevelSpec/examples/tutorial_ring.py`; the levelspec tests pass.

### Task 3: Build, look, test, document

**Files:** the rebuilt `TutorialRing` scene; docs/showcase/levels.md (the Tutorial Ring's mastery routes).

- [ ] Rebuild from the spec (LevelSpecTools.BuildFromCommandLine), Set Up Scene; the post-processing profile unchanged.
- [ ] Pictures of each stretch's both routes (a temporary editor script, deleted after); fix what looks wrong in the generator, regenerate.
- [ ] Run the Levels, Movement and Audio areas; the Tutorial Ring's tests.
- [ ] Docs.
