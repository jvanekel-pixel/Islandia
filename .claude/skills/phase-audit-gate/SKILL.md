---
name: phase-audit-gate
description: Audit a finished phase of PLAN.md before moving to the next. Use when every task in the active phase is checked off - verify tests and build, review the diff, report at most five findings by severity, and record the result.
---

# Phase audit gate

Run when all tasks in the active phase of `PLAN.md` are `- [x]`. In this project a phase is a
milestone, so this sits beside the milestone's `M<n>-RETRO` package and does not replace it.

1. **Verify.** Run `make check > _logs/check.log 2>&1; tail -30 _logs/check.log` and
   `make core-build`. Read the latest `unity-tests` and `android-build` workflow results for `main`
   — they run only in CI. Anything not run or not read is `unknown`, never `passing`.
2. **Review the diff.** `git diff --stat <phase-start>..HEAD`, then read the changed areas. Check
   against the phase's `Done when:` line, the budgets in `02-ARCHITECTURE.md` ADR-10, the layering
   rule (ADR-07), and the hard rules in `AGENTS.md` — especially: no weakened gate, no hand-written
   Unity YAML, nothing private committed, no faked device result.
3. **Report at most five findings**, most severe first, each as: severity (`critical`, `high`,
   `medium`, `low`), file and line, what is wrong, and the concrete fix. If there are none, say so.
4. **Park the rest.** Further findings and QoL ideas go to `BACKLOG.md`, one line each. A finding
   that needs work becomes a work package in `docs/plan/BACKLOG.yaml`, not an edit made during the
   audit.
5. **Record.** Update `project.json` → `health.tests`, `health.build` and `health.lastAuditedPhase`
   (the phase number, only if no `critical` or `high` finding is open). Overwrite `HANDOFF.md`.
6. A `critical` or `high` finding keeps the phase open: do not advance `phase` in `project.json`.

Device and human criteria (`needs-device`, `needs-human`) are listed as outstanding, not audited as
passed.
