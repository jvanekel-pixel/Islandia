# Kickoff prompt

Send this once, as the first message of a new Claude Code cloud session on this repository
(model: Opus, permission mode: Auto).

---

You are starting the autonomous build of this repository. Nobody will be watching after this
session, so set things up to run without me.

1. Read `CLAUDE.md`, then `docs/plan/00-START-HERE.md`, `01-AUDIT.md`, `02-ARCHITECTURE.md`,
   `03-ROADMAP.md`, `04-VERIFICATION.md`, `05-OPERATIONS.md`, `06-RISKS.md` and `07-STREAMBOX.md`. Skim
   `BACKLOG.yaml` to the end of milestone M1. Do not read `docs/blueprint/` or `reference/drafts/`
   yet.
2. Apply any decision changes I have written below this prompt to `docs/plan/DECISIONS.md` and to
   the files they affect, as the first commit.
3. Check what you have: can you push branches, open and merge pull requests, read check results,
   create issues and labels, and create a scheduled task? For anything you cannot do, open an
   issue labelled `human-gate` telling me exactly what to click, and carry on with the rest.
4. Propose the dedicated cloud environment for this repository (work package `F-10` describes what
   it needs). I will approve the card.
5. Create the scheduled task described in `docs/plan/05-OPERATIONS.md` §13 using the prompt in
   `docs/plan/prompts/driver.md`, on Opus, every two hours, new session per run. If you cannot,
   open a `human-gate` issue with the steps for me to create it.
6. Then act as the first driver: run the loop in `05-OPERATIONS.md` starting with `F-01`, and stop
   after three work packages.

Before you stop, make sure `docs/plan/STATE.md` is current and tell me in five lines: what merged,
what is in flight, what is blocked, which human-gate issues are open, and what the next session
will do.

My decision changes (leave empty to accept everything in `00-START-HERE.md` §3):

