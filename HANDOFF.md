# Session Handoff (Overwrite Every Session — <=25 Lines)

- **Updated:** 2026-11-24
- **Current Phase:** Phase 1: Factory Online
- **Active Routing:** balanced
- **What Changed:** `F-09` — milestone releases and gate templates. Adding `releases/M<n>.yaml` (with `docs/reports/M<n>-*.md`) on main makes auto-merge dispatch the new `release` workflow, which tags the commit and publishes a GitHub Release with the APK and report (a draft if a private-use pack is admitted). A pull request that changes the release route runs a real rehearsal, then deletes it. Issue templates for HG-1, HG-R, HG-2, HG-3, HG-4, `blocked`, `infra`; `make lint-release` checks them. `android-build` is now also callable (`workflow_call`).
- **Checks Run:** `make check` — pass (8 lint, 183 tooling, 404 core, 30 contract, 11 end-to-end). Release rehearsal and the Android build run in CI on the F-09 pull request.
- **Blocked On Human:** None. Still open in `INFO-REQUESTS.md`: generate `.mcp.json` from Command Center; switch the driver back on when ready.
- **Next Action:** M1 spikes `S-01`–`S-05` and `F-11`, then `M1-RETRO` (first real release); engine-free `C-02b`, `RC-01`, `RC-02`, `PNT-01` in parallel.
