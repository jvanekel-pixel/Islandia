# DECISIONS — index

A short index so no session re-litigates what is settled. **This is not the log.** The append-only
log with full reasoning and evidence is `docs/plan/DECISIONS.md`; the decisions themselves are the
ADRs in `02-ARCHITECTURE.md`. A new decision gets its full entry there and one line here.

## Template

```markdown
## YYYY-MM-DD — <decision in one line>

**Decided by:** <owner | session | audit>
**Context:** <what forced the choice>
**Decision:** <what was chosen>
**Alternatives rejected:** <what else was considered, and why not>
**Consequences:** <what this locks in, and what would reopen it>
**Evidence:** <test, measurement, pull request or file>
```

## 2026-11-15 — Command Center files are a view of `docs/plan/`, not a second plan

**Decided by:** onboarding session, at the owner's request.
**Context:** The repository already had a complete plan system (`docs/plan/`) that an autonomous
driver and seven lint checks depend on. The Command Center standard adds root files with
overlapping jobs.
**Decision:** `docs/plan/` stays the source of truth. `PLAN.md` mirrors `docs/plan/BACKLOG.yaml`
(phase N = milestone MN, one task per work package); `HANDOFF.md` and `project.json` mirror
`docs/plan/STATE.md`; this file indexes the log; root `BACKLOG.md` holds parked ideas only.
`CLAUDE.md` became `@AGENTS.md` and the original contract moved into `AGENTS.md` unchanged.
**Alternatives rejected:** Replacing `docs/plan/` (breaks the driver and the lints); a 3–5 phase
plan (would not map onto the backlog, so the two would drift).
**Consequences:** Sessions update both at stop. The Two-Strike Rule sits under the contract's
three-attempt stop-the-line rule. Nothing checks the mirror automatically yet (`BACKLOG.md`).
**Evidence:** `make check` passes with the new files in place.

## Standing decisions

| Area | Decision | Where |
| :-- | :-- | :-- |
| Purpose | A private gift for one person; never published, never sold | "What this is"; log 2026-11-10 |
| Target device | Quest 2 only: 72 Hz, budgets of ≤ 140 draws, ≤ 711K triangles, 368 MB | ADR-02, ADR-10 |
| Engine | Unity `6000.3.28f1` pinned; URP; packages follow the editor, lockfile committed | ADR-01; log 2026-11-10, 2026-11-11 |
| XR stack | Unity OpenXR + Meta OpenXR, XR Interaction Toolkit, XR Hands; no Meta XR Core SDK | ADR-01 |
| Rendering | The blueprint's rendering baseline, set in code and asserted from disk | ADR-03; log 2026-11-11 |
| Workflow | Headless and code-first; no Editor GUI; Unity YAML generated, never hand-written | ADR-06 |
| Layering | Engine-free `Island.Core` under `dotnet test`; Runtime, XR and Editor assemblies above it | ADR-07 |
| JSON | `Island.Core` parses JSON with Newtonsoft.Json | Log 2026-11-10 |
| Storage | Versioned, sectioned, checksummed binary saves; atomic writes; one file per island plus a world index | ADR-09, ADR-24 |
| Undo | Two lists, not a ring buffer; stable 64-bit instance ids | Log 2026-11-11 |
| Content | Free premade packs only, uploaded by the owner to draft releases; pinned headless Blender 4.5.16 LTS; catalogue ids are hashes | ADR-11; log 2026-11-10, 2026-11-11 |
| Import | The headset is the trust boundary; GLB gatekeeper in Core; glTFast loader; no `.blend` | ADR-12 |
| Companion | Static web converter plus a thin relay that never parses files; free-tier hosting only | ADR-13 |
| Media | StreamBox is the media source, native in-world; no Spotify, no web browser, no DRM | ADR-14, ADR-15, ADR-23 |
| Auth | None of its own. The only sign-in is StreamBox device pairing; the token stays in the platform keystore on the headset | ADR-21, ADR-23 |
| Features | Trains and roller coasters are the heart; one stray cat per island replaces pets; generic avatar; paint anything | ADR-16, ADR-24 to ADR-27 |
| Repository and CI | Public repository, GitHub-hosted runners, GameCI, trunk-based, no self-hosted runner, no Git LFS | ADR-19 |
| Unity licence in CI | Activated by seat request with account credentials, not a licence file | Log 2026-11-10 |
| Merging | Finished work is labelled `awaiting-merge` and landed by the auto-merge workflow; sessions never self-merge | Log 2026-11-10 |
| Distribution | Signed APK installed over USB with `adb`; package id `dev.islandia.app` is final | ADR-20, ADR-22 |
| Privacy | No accounts, analytics or telemetry; a privacy lint runs on every change | ADR-21 |
| Models | Opus plans and writes the code; Haiku for sub-agent work (`routing` in `project.json`) | Owner, 2026-11-11 and 2026-11-15 |
| `.env.example` | Names and empty placeholders only; the project reads no `.env` file. Sessions cannot read or write `.env*` files, so it is changed through the GitHub API or by the owner | Owner, 2026-11-15 |
| Lifecycle | Risk-first stage-gate (M0 to M14) with continuous flow and a work-in-progress limit of one | `03-ROADMAP.md` §1 |
