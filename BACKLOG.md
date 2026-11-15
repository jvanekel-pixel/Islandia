# BACKLOG — parked ideas

One line each. Out-of-scope QoL ideas, tech debt and nice-to-haves go here so they are not built
mid-phase. **Work packages do not live here** — they live in `docs/plan/BACKLOG.yaml`. Promoting a
line means adding a work package there, with acceptance criteria.

## Command Center upkeep

- [ ] Add a lint that fails when a `PLAN.md` checkbox disagrees with the package's status in `docs/plan/BACKLOG.yaml` (or generate the task lists from it)
- [ ] Add a lint that fails when `HANDOFF.md` exceeds 25 non-empty lines or `project.json` is not valid against schema version 1
- [ ] Add a `make quiet-check` target that writes to `_logs/check.log` and prints at most 30 lines
- [ ] Name `AGENTS.md` beside `CLAUDE.md` in `README.md`, `make help` and the three prompts in `docs/plan/prompts/`
- [ ] Teach the driver loop in `05-OPERATIONS.md` the session stop contract, so the two trails are written in one step

## Tech debt noticed during the audit

- [ ] `docs/plan/STATE.md` counts have drifted: it says 155 tooling and 363 core tests (now 159 and 364) and "six CI workflows" (ten workflow files exist)
- [ ] `03-ROADMAP.md` §5 says "134 in all, 21 done"; the backlog has 138 live packages, 28 done
- [ ] `dotnet test` warns that the `Microsoft.NET.Test.Sdk` reference in the core test project is outdated
- [ ] Mock gateway: four declared music routes are unserved and the contract tests do not notice (issue 21)
- [ ] Mock gateway: the no-ffmpeg path does not work as documented (issue 15)
- [ ] Asset wave 1 release notes link a `mini-forest` pack whose archive was never uploaded

## Nice-to-haves the architecture already defers

- [ ] HRTF spatialiser plugin, after a device gate (ADR-14)
- [ ] Composition-layer quad for sharper video, as an isolated spike after a device gate (ADR-15)
- [ ] Application SpaceWarp, if device data asks for it (ADR-18)
- [ ] Measure on a Quest 3, 3S or Pro so their tier rows stop being "unverified" (ADR-02)
- [ ] Server-side Blender converter in a locked-down per-job container (ADR-13)
- [ ] Internet radio tuner (`A-03`) — only if the owner asks
