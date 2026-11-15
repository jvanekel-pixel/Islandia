---
name: two-strike-recovery
description: Stop and escalate after two consecutive failed attempts at the same error. Use when a fix for the same failing command, test or CI check has failed twice in a row - stop editing, record the evidence in HANDOFF.md, and hand off instead of trying a third variation.
---

# Two-strike recovery

**Trigger:** two consecutive attempts to fix the *same error* have failed — the same command or
check, the same failure. A different error resets the count.

1. **Stop editing.** No third variation, no widening of the change, and never weaken, skip or
   delete the failing test or threshold to get green.
2. **Record the evidence in `HANDOFF.md`** (still 25 non-empty lines or fewer):
   - the exact command that fails;
   - the error, cut to **30 lines or fewer** (the full output stays in `_logs/`);
   - the files touched in both attempts;
   - what each attempt changed and why it did not work, one line each.
3. **Leave the tree honest.** Revert or stash the failed edits unless they are a real partial
   improvement; say which in the handoff. Push the branch so nothing lives only in the session.
4. **Escalate — choose one:**
   - **Fresh chat:** a new session starts from `HANDOFF.md` with clean context.
   - **Audit:** ask for an architecture review of the area (the auditor role in `AGENTS.md`).
   - **Human Task Card:** if the cause is something only the owner can supply, use the
     `human-task-card` skill.
5. **Update `project.json`:** `nextAction` names the escalation; set `health` to what was observed.

A strike-out counts as one failed attempt at the work package. The operating contract's
stop-the-line rule still applies: after three failed attempts at the same package, open an issue
labelled `blocked`, set the package to `blocked` in `docs/plan/BACKLOG.yaml`, and take the next one.
