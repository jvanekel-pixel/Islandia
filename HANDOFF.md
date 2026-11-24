# Session Handoff (Overwrite Every Session — <=25 Lines)

- **Updated:** 2026-11-24
- **Current Phase:** Phase 1: Factory Online
- **Active Routing:** balanced
- **What Changed:** The project moves from cloud sessions to Claude Code on the owner's PC (`docs/plan/DECISIONS.md`, 2026-11-24). `F-10` is now the local workstation (`docs/plan/LOCAL-SETUP.md`), `F-11` the loop rehearsal from a local session (schedule optional), `S-01` Unity in batchmode on the PC. Unity may run locally for speed once `S-01` shows the Personal licence is shared safely with CI; CI on the merge commit still proves done. `05-OPERATIONS.md`, `prompts/driver.md` and `AGENTS.md` now say `gh` works in full locally.
- **Checks Run:** `make check` — see the pull request for this change.
- **Blocked On Human:** None. On the PC the owner signs in to `gh` (and Unity Hub, if they want local Unity) and generates `.mcp.json`; see `INFO-REQUESTS.md`.
- **Next Action:** `F-10` from a local session: fill in `LOCAL-SETUP.md`, run `make deps`, `make mock-install`, `make check`. Then `S-01`, `S-02`–`S-05`, `F-11`, `M1-RETRO`.
