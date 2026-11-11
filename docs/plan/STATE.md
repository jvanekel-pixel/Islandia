# STATE

*The one file to read to know where the project stands. Updated in every pull request.*

| | |
| :-- | :-- |
| **Stage** | 2 — Construction |
| **Current milestone** | M1 Factory online · M2 started on the engine-free track |
| **In flight** | Nothing. Every pull request opened so far is merged and `main` is clean |
| **Repository** | https://github.com/jvanekel-pixel/Islandia (public) |
| **Driver** | **Running.** The scheduled task *Islandia build driver* fires every four hours at :43 past, into one long-lived session, and takes up to two work packages per run. It is working: `F-03`, `F-04`, `F-07b`, `F-07c`, `C-02a` and three automation fixes came from it on 2026-11-11. Pause or re-point it at <https://claude.ai/code/routines> |
| **Last updated** | 2026-11-11, 18:00 UTC (`F-04` done; this file corrected) |
| **Target** | Quest 2 · public repository · StreamBox native in-world · **a gift, never published** |

## Open human gates

| Gate | What is needed | Issue |
| :-- | :-- | :-- |
| HG-0 | **Done** 2026-11-10, Unity included | — |
| — | **How CC0 pack archives reach the ingestion pipeline.** `RT-06` fired: the sources ADR-11 names have no download URL that can be pinned, so unattended fetching may not be possible. `C-02c` is `needs-human` until this is answered. Nothing else is blocked by it — `C-02b` and `C-03` both proceed | [#25](https://github.com/jvanekel-pixel/Islandia/issues/25) |

If `F-02` fails because `UNITY_LICENSE`, `UNITY_EMAIL` or `UNITY_PASSWORD` is missing, that is the
tail of HG-0, not a new problem: open a `human-gate` issue pointing at `00-START-HERE.md` §2 step 4
and carry on with packages that do not need Unity (`F-01`, `F-08`, `SB-01a`, `I-01`, `I-04`).

## Blocked

**Nothing.** 

The scheduled driver's 403s of 2026-11-10 are fixed: the task was recreated bound to a session that
already has the repository, and it has been running since. The note that used to live here described
a fault that no longer exists, which is `RK-10` — plan documents rotting — happening to the file
whose whole job is to be true.

## Waiting on a device (`needs-device`) or a person (`needs-human`)

Collected here as packages merge; they become the checklist for the next human gate.

| What | From | Why it needs a person |
| :-- | :-- | :-- |
| Add `.claude/settings.json` | `F-01` | A session cannot write its own permission file, which is right. **This now matters more than it looked:** without it the driver also cannot merge its own pull requests, so every work package stops at a green PR for the owner to merge. `00-START-HERE.md` §9 has the file and both choices |

## Next up

Take the first `todo` package in `BACKLOG.yaml` whose dependencies are `done` — `make next` prints
them. As of this writing: **`F-05`** (OpenXR and Meta features), **`V-07b`** (tier settings applied
at runtime), **`C-02b`** (the Blender normalisation pipeline), **`C-03`** (parametric generators),
`I-04`, `I-05`.

**21 of 116 packages are done.** `F-01`–`F-04`, `F-07a`–`F-07c`, `F-08`, `F-13`, `V-02`, `V-06`,
`V-07a`, `V-08`, `SB-01a`–`SB-01d`, `C-01a`, `C-01b`, `C-02a`, `I-01`.

### What exists now

- **The Unity project**, generated and committed by its own workflow: editor `6000.3.28f1`, 67
  packages locked, a second import that changes nothing, and every ADR-03 rendering decision set in
  code and asserted from the committed assets (`F-04`).
- **Five CI workflows.** `checks` (L0/L1, every push), `content` (Blender, path-filtered),
  `unity-tests` (EditMode and PlayMode), `unity-package-sources` and `unity-configure` (both
  generate; both commit only on dispatch). `auto-merge` lands anything labelled `awaiting-merge`
  once its checks pass.
- **StreamBox reachable end to end** from `Island.Core`: pairing, browsing, playing, progress, music
  and remote control, all proven against the mock gateway in CI.
- **The content pipeline**, less its input: catalogue, schema, validator, licence ledger, honest
  count, and a pinned headless Blender proven to script, measure, export GLB and render a thumbnail
  with no GPU.

### Three things a new session should know

1. **Label last.** `awaiting-merge` means *land this now*; do not push to a branch after labelling
   its pull request. Auto-merge has squashed a PR mid-push and stranded the rest.
2. **Branch from `main`, not from an unmerged branch.** Squash-merging the parent leaves the child
   conflicted.
3. **A workflow's own commit triggers nothing.** After dispatching `unity-configure` or
   `unity-create-project`, dispatch `unity-tests` by hand — pushes made with `GITHUB_TOKEN` do not
   start workflow runs.

### Open, not blocking

- [#25](https://github.com/jvanekel-pixel/Islandia/issues/25) `human-gate` — how CC0 pack archives
  reach the pipeline. `C-02c` waits on it; `C-02b` and `C-03` do not.
- [#21](https://github.com/jvanekel-pixel/Islandia/issues/21),
  [#15](https://github.com/jvanekel-pixel/Islandia/issues/15) `infra` — two defects in the mock
  StreamBox gateway, both found by CI, neither urgent.
- The owner is considering whether to spend a small budget on asset production. Nothing waits on it.

## Recently merged

- `F-02` Unity licence activation in CI — **working** (2026-11-10)
- `V-02` Terrain model in Core — heightfield, brushes, dirty regions (2026-11-10)
- `SB-01b` StreamBox models and tolerant JSON mapping (2026-11-10)
- `SB-01a` StreamBox contract snapshot and mock gateway (2026-11-10)
- `F-02` Unity licence workflow — built and merged; cannot pass until the secrets exist (2026-11-10, PR #3)
- `F-08` Island.Core package and dotnet test harness (2026-11-10, PR #2)
- `F-01` Repository skeleton, Makefile, lint, meta generator (2026-11-10, PR #1)
- Planning bundle, driver prompt and kickoff state (2026-11-10)

## Measured facts

Filled in as they are measured; planning numbers elsewhere are hypotheses until they appear here.

| Fact | Value | Source |
| :-- | :-- | :-- |
| Local gate `make check` | 7 lint checks, 134 tooling tests, 312 core tests, 30 contract tests, 11 end-to-end — about 40 s | everything merged to 2026-11-11 |
| Unity EditMode tests in CI | **334** — the 312 `Island.Core` ones plus 22 asserting ADR-03 | F-04, F-07b |
| .NET SDK in a cloud session | not present; installs to `~/.dotnet` in ~40 s | F-08 |
| Unity editor pinned | `6000.3.28f1` — the newest 6000.3 tag with an Android module | F-02 |
| Unity licence in CI | **works.** Seat request with account credentials; no licence file. 104 s end to end | F-02 |
| Editor version CI really runs | `6000.3.28f1`, reported by the editor itself | F-02 |
| ffmpeg in a cloud session | present, so the mock serves a genuinely decodable stream | SB-01a |
| Terrain on disk | ~129 KB before packing; whole island well under 400 KB | V-06 |
| GitHub REST from a session | runs, check-runs, status, pulls, issues all readable; `actions/secrets` blocked | F-01 |
| Driver self-merge | **Refused** by the permission classifier unless `.claude/settings.json` allows it. See `05-OPERATIONS.md` §7 step 4 | F-01 |
| Unity editor version pinned | — | F-03 |
| CI: warm test run / Android build | — | S-05 |
| Runner minutes per Unity package | — | S-05 |
| Device strings and tier | — | HG-1 |
| Frame time, empty island | — | HG-1 |
| Video backend that works on the headset | — | HG-1 (V-14) |
| StreamBox commit the contract snapshot matches | 9ebfdd6 (read during planning) | SB-01 |
