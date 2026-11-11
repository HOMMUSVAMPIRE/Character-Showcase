# Pipeline run sheet: REQ-0147

**Prepared by:** Priya Natarajan · **For:** AI agent pipeline run, REQ-0147 Launch Demo Package
**Dispatch starts:** Tue 2026-11-10, 17:00 ET

Read REQ-0147 first. This sheet only covers the practical details of the run.

**This pass is planning only.** I review the plan before anything is dispatched, so during this pass nothing is run, generated, committed, pushed or sent: don't call the agents, edit the repo or write to `ESCALATIONS.md`. Describe what the pipeline will do once it's dispatched, and plan every time from the dispatch start above.

## Repository

- The request documents, including this sheet, are in `docs/requests/REQ-0147/`.
- `AGENTS.md` at the repo root has the project rules.
- Once dispatched, the pipeline branches `feature/launch-demo` from the tag `testbed-baseline` (the head of `ExpansionBranch`), pushes it, and opens a PR against `ExpansionBranch`.
- CI (the `Tests` workflow) runs on the PR and reports results there. A new push cancels the run in progress. Pushes that only change Markdown don't trigger a run on `main`, but once the PR contains code, every push to it starts a run.

## What the pipeline has once dispatched

- A shell on the pipeline box (`pipe-01`), not on my machine, with git, Python 3 and ImageMagick. The repo is checked out there once, at `~/work/Character-Showcase`. Agents read and write files in that checkout; unless you set up something else, they all share it.
- Local models run on my machine's GPU; `pipe-01` calls them over the LAN.
- Pexal3D, Tralis and Kling return a download link for each finished job. Links expire 24 hours after the job finishes, and nothing is downloaded automatically.
- No Unity Editor and no Blender. Anything that needs either goes to me (see "People" below).

## Agents available

These are the only agents available for this run.

| Agent | Runs | What it's for | Takes | Gives back | Typical turnaround |
| --- | --- | --- | --- | --- | --- |
| Qwen 2.8 Flash | Local | Small fast text/code model; noticeably weaker than GLM 5.2 on code | Text | Text / code | Seconds to a few minutes |
| GLM 5.2 | Local | General code and text model; main local model for code edits; can review images | Text, images | Text / code | Minutes |
| Illustrius | Local | 2D illustration; concept art; icons and portraits. Does not make 3D meshes | Text prompt, optional reference images | PNG, up to 2048 × 2048 | ~1 min per image |
| Pexal3D | Cloud | 3D mesh generation, standard detail; low cost per asset | Text prompt and/or up to 4 reference images | Textured mesh (FBX or GLB) | ~5 min per asset |
| DeepSeek 4.1 | API | Strongest code model on the list; complex multi-file code work; billed per token | Text | Text / code | Minutes |
| Kling | API | Image and video generation only. Cannot review or describe images; billed per output | Text prompt and/or reference images | PNG, or 1080p video in 5 s clips | ~2 min per image, ~10 min per clip |
| Tralis | API | 3D mesh generation, high detail; highest cost per asset on the list | Text prompt and/or up to 4 reference images | Textured mesh (FBX or GLB) | ~45 min per asset |

Notes:
- Qwen 2.8 Flash and GLM 5.2 share the workstation GPU. Switching between them unloads one and loads the other, which takes about 4 minutes.
- Local jobs run on my dev machine. Please follow Jess's request about working hours (see her email).
- Costs and budget caps are in Jess's email.
- IT is pushing a driver and OS update to the studio machines on Wed 11 Nov. My machine, and so the local models, will be off from 19:00 Wed until about 07:00 Thu. `pipe-01` and the cloud/API services aren't affected.
- Context windows as we run them (prompt and reply together): Qwen 2.8 Flash 8k tokens, GLM 5.2 32k. DeepSeek 4.1 has 128k.

## People

These are the rules once the plan is running. In the plan, say which `ESCALATIONS.md` entries the pipeline will add, for whom, and when each goes in.

- **Questions, approvals and editor work:** add them to `ESCALATIONS.md` on the feature branch, one entry each, saying what you need, by when, and what you'll do if there's no answer. I check it at 09:00 and 17:00 ET on weekdays.
- **Small calls:** if something is my call (code, technical choices, which agent does what) and it's reversible and costs under $25 within the caps, make the call yourself and log it in `ESCALATIONS.md` as a decision. Don't wait for me. Budget calls still go to Jess and art calls to Owen, and anything you need me to do goes in as a normal entry.
- **Editor time:** about 2 hours a day from M2 onward, and full days on 11-24 and 11-25. Anything that needs Unity or Blender has to fit in that time. Before M2 I'm on other work. If you need me in Unity or Blender before the 20th, put it in `ESCALATIONS.md` by Thu 12 Nov 09:00 with a rough estimate so I can block the time. After that I can't promise anything before M2. I work in my own clone and pull from GitHub.
- **Budget questions:** Jess, through `ESCALATIONS.md`. She usually replies within a working day.
- **Owen** is out 11-16 to 11-20. Don't plan on reaching him that week.

## What I need back from this planning pass

1. **Job list:** every agent job, with the agent, what it's given (a short brief, plus the files or earlier outputs it's pointed at), its planned start time (ET), which jobs it waits for and why, and its estimated cost.
2. **Flowchart** of the order of operations, with planned times, showing which jobs run one after another and which run in parallel, the key decision points and the hand-offs. Mermaid is fine.
3. **Planned `ESCALATIONS.md` entries:** for each, who it's for, when it goes in, what it asks or records, and what the pipeline does if there's no answer.
4. **Planned `asset_manifest.csv`:** the quality tier and source service for every asset.
5. **Draft scene-assembly hand-off note** (REQ-0147 §6), as it would read at M3.
6. **Overview:** which agent does what and why, what's left open, the main risks and what's done about them, and estimated spend against each budget line.
