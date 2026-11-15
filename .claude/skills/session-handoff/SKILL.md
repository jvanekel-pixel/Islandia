---
name: session-handoff
description: Start and stop a work session on Islandia without drift. Use at the start of every session to load only the context needed, and before ending one to verify, check off PLAN.md, overwrite HANDOFF.md and update project.json.
---

# Session handoff

## At start

1. If the Command Center MCP server is connected, call `get_project_context`. If not, skip it.
2. Read `AGENTS.md`, `HANDOFF.md` and `project.json`.
3. Read **only the active phase** in `PLAN.md` — the phase whose number is `phase` in
   `project.json`. Do not load the other phases.
4. Read `docs/plan/STATE.md`, then the backlog entry in `docs/plan/BACKLOG.yaml` for the package
   named in `nextAction`. `make next` lists what is unblocked.
5. If `HANDOFF.md` and `docs/plan/STATE.md` disagree, `STATE.md` wins. Fix the handoff at stop.

## At stop

1. Run the quiet check and read only the tail:
   `make check > _logs/check.log 2>&1; tail -30 _logs/check.log`
   Report failures in 30 lines or fewer. Never report a check you did not run.
2. Update the trail the contract requires: the package's `status` in `docs/plan/BACKLOG.yaml` and
   `docs/plan/STATE.md`, in the same pull request as the work.
3. In `PLAN.md`, change `- [ ]` to `- [x]` only for packages that are `done` in the backlog.
4. Overwrite `HANDOFF.md` — keep the seven field labels exactly, **25 non-empty lines or fewer**.
5. Update `project.json`: `phase` (first phase with an unchecked task), `nextAction`,
   `blockedOnHuman`, `lastSessionAt` (ISO-8601, UTC), and `health.tests` / `health.build` from the
   commands actually run — `passing`, `failing` or `unknown`, nothing else.
6. Commit, push, and call `log_session` if the MCP server is connected.

Never write live state into `AGENTS.md` or `CLAUDE.md`.
