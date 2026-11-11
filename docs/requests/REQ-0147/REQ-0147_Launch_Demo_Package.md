# REQ-0147: Launch Demo Package (Character Roster, Interactions & Store Assets)

**Brackenfield Interactive · Character Showcase**

| Field | Value |
| --- | --- |
| Document ID | REQ-0147 |
| Project | Character Showcase (Unity Asset Store release) |
| Type | Feature / change request |
| Status | **Approved for implementation** |
| Priority | P1, launch blocking |
| Requested by | Priya Natarajan, Lead Developer (priya.natarajan@example.com) |
| Stakeholders | Owen Calder, Lead Artist · Jess Moreau, Producer |
| Implemented by | AI agent pipeline |
| Repository | Tag `testbed-baseline` (head of `ExpansionBranch`). Work on a feature branch, `feature/launch-demo` |
| Created | 2026-11-04 · Last updated 2026-11-10 |
| Target dates | Implementation and assets complete **2026-11-24** · Store submission **2026-12-01** |
| Attachments | In `docs/requests/REQ-0147/`: `00_pipeline_run_sheet.md`, `art-direction_thread_2026-11-10.md`, `email_JMoreau_launch-budget.md`, `asset_manifest.csv` |
| See also | `AGENTS.md`, `README.md` |

### Revision history

| Version | Date | Author | Change |
| --- | --- | --- | --- |
| 0.1 | 2026-11-04 | P. Natarajan | Initial draft (character roster + store assets) |
| 0.2 | 2026-11-06 | P. Natarajan | Added interactions (INT) after demo feedback review with J. Moreau |
| 0.3 | 2026-11-09 | J. Moreau | Budget section, milestone dates |
| 1.0 | 2026-11-10 | P. Natarajan | Approved. Art direction moved to the attached thread; open questions logged in §11 |

---

## 1. Summary

Turn the Character Showcase demo into something that sells the package on the Asset Store. That means three playable characters on a select screen, interactable objects that use the IK system, a full set of store listing assets including a promo video, and a readability pass on the code before buyers see it.

## 2. Background and rationale

We're releasing the controller as a paid Asset Store package. For controller packages, the demo *is* the marketing. Buyers download or watch the demo before they ever read the feature list.

The current demo is a grey mannequin in a test area. Three people outside the team played the current build last week. Summary of their notes:

- All three said the movement felt good. Two said they "didn't know what to do" after a minute.
- One described it as "a tech demo, not something I'd buy for my game".
- Nobody found the IK features. They currently live behind an inspector context menu.
- Nobody realised the character model can be swapped. Model swapping (`CharacterModelDefinition`, runtime `AnimationController.LoadModel`) is one of our strongest selling points, and right now it's invisible.

This request addresses all of that:

- A **character roster** shows off model swapping and per-character movement tuning.
- **Interactions** show off `CharacterIK` and give players a reason to explore.
- **Store assets** are required for submission.
- The **readability pass** is in the README backlog and matters more now, because the code is part of what buyers pay for.

Note: the project started life as a personal portfolio piece before the studio took it on, and the README still reads that way (first person, "for possible employers", personal contact details). Rewriting it for the studio and for buyers is part of CQ-02. The licence for the paid package is being handled separately by J. Moreau; leave `LICENSE` as it is.

## 3. Scope

### 3.1 In scope

- Three playable characters, a character select screen, and per-character movement tuning
- Interactable doors, levers, crates and a relic in the demo
- Camera behaviour during interactions
- Interaction prompts (keyboard and gamepad)
- Store key images and promo video
- Readability pass on runtime code, README updates, and tests for new behaviour

### 3.2 Out of scope (this release)

- New movement abilities (sprint, crouch, climbing). Deferred to v1.1.
- Combat of any kind
- Multiplayer
- Mobile and console builds
- Localisation
- Final store screenshots. Priya captures these in-editor once the demo is assembled (§7, M4).
- Environment art. The demo level stays greybox for 1.0; only the props in §4.2 are new (door frames, walls and floors remain greybox geometry).

## 4. Requirements

Requirement IDs are referenced in commits and PR descriptions. "TBD" items are tracked in §11.

### 4.1 Character roster (CHR)

| ID | Requirement |
| --- | --- |
| CHR-01 | Three playable characters: **Knight**, **Scout** and **Heavy**. Visual descriptions and direction come from Owen. See the attached art-direction thread. |
| CHR-02 | A character select screen is shown when the demo starts. It's navigable with keyboard/mouse and gamepad, and shows each character's portrait and name. Whether players can return to select mid-demo is TBD (Q-03). |
| CHR-03 | Per-character movement tuning. **Knight:** current defaults. **Scout:** dash travels noticeably further (roughly +30% as a starting point), 2 air dashes. **Heavy:** slower (roughly −20% walk speed, tune by feel), no air dashes. Tuning values are starting points, not specs. |
| CHR-04 | All characters use the existing animation set (idle, walk, run, rise, fall, dash), and `FootPlacementIK` works on all three. |
| CHR-05 | Switching characters uses the existing model loading path, with no visible hitch. |
| CHR-06 | Per J. Moreau: the Heavy may reuse the Knight's model where possible, to reduce cost. See thread for the art lead's view. |

### 4.2 Interactions (INT)

| ID | Requirement |
| --- | --- |
| INT-01 | Interactions are triggered by the existing `Interact` input action. |
| INT-02 | **Doors:** the character reaches for the handle and pushes the door open. Whether doors close again is TBD (Q-04). |
| INT-03 | **Levers:** two-handed pull. At least one lever in the demo should drive something in the level (e.g. start a moving platform). |
| INT-04 | **Crates:** two sizes. Small crates can be carried by anyone; large crates by the Heavy only. Carried crates are held in front and dropped with a second press. Non-Heavy characters attempting a large crate need some feedback (TBD, Q-05). |
| INT-05 | **Relic:** the goal of the demo. Picking it up completes the demo loop. What happens afterwards is TBD (Q-03). |
| INT-06 | The camera frames interactions so they read well, without taking control away from the player for more than about a second. |
| INT-07 | Interaction prompts appear near interactable objects, with keyboard and gamepad glyphs matching the active device. |

### 4.3 Store listing assets (MKT)

Image sizes are from Unity's Asset Store publishing guidelines and must be exact. Check the Publisher Portal before final export in case they have changed.

| ID | Asset | Spec |
| --- | --- | --- |
| MKT-01 | Icon | 160 × 160 px. **No text.** |
| MKT-02 | Card | 420 × 280 px. Only the asset title and publisher name are allowed. |
| MKT-03 | Cover | 1950 × 1300 px. Only the title, publisher name and a tagline are allowed. |
| MKT-04 | Social media | 1200 × 630 px. **No text.** |
| MKT-05 | Key art subject | Owen's direction: the Knight holding the Relic. Used across MKT-02 to MKT-04, cropped and adapted as needed. |
| MKT-06 | Promo video | About 30 seconds, showing the character select screen and interactions. Unity also asks technical/scripting packages to include a video covering setup and an overview of functionality. Whether the promo can serve both purposes is open (Q-06). |
| MKT-07 | Screenshots | Captured in-editor by P. Natarajan after scene assembly (M4). Not part of the pipeline's deliverables, but the pipeline must deliver everything the demo scene needs to be assembled. |

Publisher name for all store text: **Brackenfield Interactive**. Working title: **"Showcase Character Controller"**. Tagline: TBD (Q-07).

### 4.4 Code quality and documentation (CQ)

| ID | Requirement |
| --- | --- |
| CQ-01 | Readability pass over `Assets/Scripts`, per the README's "Refactor for improved readability" backlog item. Scope is at the implementer's discretion. **No behaviour changes.** Public API changes are acceptable because we haven't shipped 1.0 yet, but they must be listed in the PR. |
| CQ-02 | README rewritten for the studio and for buyers (see the note in §2), and updated for the character roster, interactions and the demo flow. |
| CQ-03 | New behaviour is covered by PlayMode tests. The full suite passes in CI (`Tests` workflow). |

### 4.5 Non-functional (NFR)

| ID | Requirement |
| --- | --- |
| NFR-01 | Unity 6000.6.7f1, URP. No new packages without approval from P. Natarajan. |
| NFR-02 | Demo holds 60 fps at 1080p on our min-spec machine (GTX 1060 6 GB, i5-8400, 16 GB RAM). |
| NFR-03 | Poly and texture budgets: per art lead (see thread and `asset_manifest.csv`). |
| NFR-06 | Each 3D asset is assigned a quality tier in `asset_manifest.csv`: **Hero** (seen in close-up or in marketing; highest detail) or **Standard** (seen at gameplay distance). |
| NFR-04 | All generated content must be cleared for commercial redistribution as part of a paid Asset Store package, under the generating service's terms. Record which service produced each asset in `asset_manifest.csv`. |
| NFR-05 | No third-party IP, trademarks or recognisable likenesses in any asset. |

## 5. Budget

Full details are in J. Moreau's email (attached). Summary:

| Line | Cap | Notes |
| --- | --- | --- |
| Asset generation (3D, image and video services) | **$650** | Regenerations count. Any overrun needs Producer approval *before* the spend. |
| Code model API usage | **$150** | Per-token billing. |
| Local models | n/a | They run on the studio workstation; see the scheduling note in J. Moreau's email. |

## 6. Deliverables

- [ ] Code on `feature/launch-demo`, with a PR against `ExpansionBranch` referencing requirement IDs
- [ ] Character models, definitions and per-character movement settings (CHR-01 to CHR-06)
- [ ] Character portraits for the select screen
- [ ] Interaction system and prop models (INT-01 to INT-07)
- [ ] Interaction prompt glyphs (keyboard and gamepad)
- [ ] Store key images MKT-01 to MKT-04
- [ ] Promo video MKT-06
- [ ] Updated README, and PlayMode tests passing in CI
- [ ] `asset_manifest.csv` completed: quality tier (NFR-06), source service, status and licence note for every asset
- [ ] A hand-off note for P. Natarajan listing every manual editor step needed for scene assembly
- [ ] A summary of the work, and of how and in what order it was carried out

## 7. Milestones

| ID | Date | Milestone | Owner |
| --- | --- | --- | --- |
| M1 | 2026-11-13 | Art direction locked | O. Calder |
| M2 | 2026-11-20 | Characters and interactions functional in the demo | Pipeline |
| M3 | 2026-11-24 | All code and assets delivered; PR open | Pipeline |
| M4 | 2026-11-25 | Scene assembled, screenshots captured | P. Natarajan |
| — | 2026-11-26 – 27 | Studio closed (holiday) | |
| M5 | 2026-12-01 | Asset Store submission | J. Moreau |

## 8. Acceptance criteria

1. All three characters are selectable and playable in the demo, with visibly different movement as described in CHR-03.
2. Every interaction type in INT-02 to INT-05 can be performed by an appropriate character, and the crate restriction behaves as specified.
3. Store images match the exact sizes and text rules in §4.3.
4. The CI `Tests` workflow is green on the PR.
5. No behaviour regressions from CQ-01. Existing tests keep passing unchanged, or each change is justified in the PR.
6. Spend is within the caps in §5, or each overrun has written Producer approval.
7. Owen (or a delegate if Owen is unavailable) has signed off on character visuals.

## 9. Assumptions

- Generated character models will work with the project's existing Humanoid animation set.
- P. Natarajan is available for in-editor tasks about 2 hours a day from M2 onward, and full days on 11-24 and 11-25.
- The Asset Store review timeline after submission is outside our control and isn't planned for here.

## 10. Risks

| Risk | Likelihood | Impact | Mitigation |
| --- | --- | --- | --- |
| Art lead out of office 11-16 to 11-20 (during M2) | Certain | Medium | Lock direction by M1. See thread re: delegate. |
| Generated assets need multiple regenerations | Medium | Medium | Budget contingency is limited. Prioritise per Producer email. |
| Holiday compresses the end of the schedule | Certain | Medium | M3 is the hard deadline for pipeline work. |
| Refactor (CQ-01) introduces regressions | Low | High | No behaviour changes; CI must be green. |

## 11. Open questions

| ID | Question | Owner | Status |
| --- | --- | --- | --- |
| Q-01 | Can the Heavy reuse the Knight's model? (Producer and art lead disagree; see thread.) | J. Moreau / O. Calder | Open |
| Q-02 | Who signs off on character visuals while Owen is out? | O. Calder | Open |
| Q-03 | Can players return to character select? What happens after the Relic is collected? | P. Natarajan | Open. Implementer's call if not resolved by M2. |
| Q-04 | Do doors close again? | P. Natarajan | Open |
| Q-05 | Feedback when a non-Heavy character tries a large crate? | O. Calder | Open |
| Q-06 | Can the promo video also serve as the setup/overview video Unity asks for? | J. Moreau | Open |
| Q-07 | Store tagline | J. Moreau | Open |

## 12. Approval

| Role | Name | Decision | Date |
| --- | --- | --- | --- |
| Lead Developer | Priya Natarajan | Approved | 2026-11-10 |
| Producer | Jess Moreau | Approved | 2026-11-09 |
| Lead Artist | Owen Calder | Approved with comments (see thread) | 2026-11-10 |
