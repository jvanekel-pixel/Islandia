# STATE

*The one file to read to know where the project stands. Updated in every pull request.*

| | |
| :-- | :-- |
| **Stage** | 2 — Construction |
| **Current milestone** | M1 Factory online · M2 started on the engine-free track |
| **In flight** | **Two pull requests stand between `main` and everything built today.** [#16](https://github.com/jvanekel-pixel/Islandia/pull/16) is the CI checks workflow, green and independent. [#19](https://github.com/jvanekel-pixel/Islandia/pull/19) is the whole stack — auto-merge, the gift re-plan, the Unity-project workflow, `V-07a`, `C-01a`, `C-01b` and the auto-merge fix — because auto-merge collapsed each stacked PR into its base branch rather than into `main`. #10 is superseded by #19 |
| **Repository** | https://github.com/jvanekel-pixel/Islandia (public) |
| **Driver** | Paused. Being driven by an interactive session for now |
| **Last updated** | 2026-11-11 (`F-04` done — ADR-03 is set in code and asserted) |
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

**The scheduled driver cannot reach the repository.** Five runs between 06:49 and 14:49 on
2026-11-10 each ended in about eleven seconds: clone, push and the GitHub API all returned 403,
because the task was created with an empty repository list and neither `create_trigger` nor
`update_trigger` can set one. The guard in the prompt worked — no run did work it would have lost,
and the whole night cost about forty cents — but each one sent the owner a notification, so the
task is paused until it is fixed.

Two fields, both in the task's **Edit** dialog at <https://claude.ai/code/routines>:

| Field | Where | Set it to |
| :-- | :-- | :-- |
| Repository | Advanced settings → General tab | `jvanekel-pixel/Islandia` |
| Model | the model selector on the prompt box | Opus — runs so far defaulted to Sonnet |

Then re-enable the task. A session cannot set either: there is no documented programmatic way to
give a scheduled task a repository, and a session may not change a task's model on its own.

Worth revisiting once a real run has happened: every two hours × three packages is a fast burn of
the owner's usage. Tune the cadence from what one full run actually costs.

## Waiting on a device (`needs-device`) or a person (`needs-human`)

Collected here as packages merge; they become the checklist for the next human gate.

| What | From | Why it needs a person |
| :-- | :-- | :-- |
| Add `.claude/settings.json` | `F-01` | A session cannot write its own permission file, which is right. **This now matters more than it looked:** without it the driver also cannot merge its own pull requests, so every work package stops at a green PR for the owner to merge. `00-START-HERE.md` §9 has the file and both choices |

## Next up

**Merge PR #10.** Then the `unity-create-project` workflow can be dispatched to finish `F-03`'s
second half (generate the project, commit `ProjectSettings` and the manifest), and `F-04` → `F-05`
follow.

Done so far: `F-01`, `F-02`, `F-08`, `F-13`, `SB-01a`, `SB-01b`, `V-02`, `V-06`, `V-07a`, `C-01a`,
`C-01b`, `V-08`, `SB-01c`, `SB-01d`, `I-01`, `F-03`, `C-02a`.

**The Unity project is on `main`** (`5ae50c2`), generated and committed by its own workflow: editor
`6000.3.28f1`, 67 packages locked at resolver-chosen versions, and a second import that changes
nothing.

**The editor runs the tests** (`F-07b`): 312 EditMode tests green in Unity, the same 312 that
`dotnet test` runs, so `Island.Core` is now proven on both runtimes. Its first run earned its
keep by finding a cross-runtime difference `dotnet test` cannot see — cancellation arrives as
`TaskCanceledException` in Unity and as its base class under .NET 9.

`F-04`, `F-05` and `V-07b` are unblocked, and their `[L2]` criteria now have somewhere to be proven.

Test counts: **323 Island.Core** (312 fast, 11 end-to-end against the mock gateway) **· 114 tooling
· 30 StreamBox contract.**

**CI is green on the chain**, end-to-end suite included: the `checks` workflow was folded into it,
so the nine packages waiting to land are verified by a run rather than by a session's word.

**CI now runs the non-Unity checks** (`F-07a`, PR #16) — before this, nothing did. Its first run
went red on a real portability bug (issue #15), which is the best argument for it there is.

**Auto-merge merged four pull requests on zero verification** before that landed, because "every
check passed" is true of an empty set. The merges are sound but the mechanism was not; it now
requires at least one check to have succeeded. See `DECISIONS.md`, 2026-11-10.

**The content pipeline is ready for assets.** The catalogue, its schema, the validator, the licence
ledger and the honest count all exist and are tested; `C-02b` (normalise) and `C-03` (parametric
generators) are what fill it, and the Blender toolchain they both need is proven (`C-02a`).

Unblocked and needing no Unity: `C-03` (parametric generators, though its thumbnails need Blender),
`I-04` (the browser converter — it reads the same `contract/import/limits.v1.json` the gatekeeper
does). `V-07b` waits on the Unity project.

StreamBox is now reachable end to end from `Island.Core`: pairing, browsing, playing, progress,
music and remote control, all proven against the mock gateway in CI. What remains for media is
`SB-02`, the player backend — and that is the one thing that cannot be settled without the headset
(`RT-16`, the `V-14` probe).

`C-02b` (the normalisation pipeline) and `C-03` (parametric generators) are the next content work,
and both are unblocked now that `C-02a` has pinned and proven headless Blender. `C-02c` — getting
the pack archives in the first place — is `needs-human`: see the gate above.

**`F-03`'s second half cannot run yet for a mechanical reason:** `unity-create-project.yml` is not
registered with Actions, because a workflow only becomes dispatchable once it has run or reached the
default branch. Dispatching it returns 404 until the chain lands on `main`.

**Open question from the owner, 2026-11-10:** whether to spend a small budget on asset production
(a rented GPU, generated assets, or bought base meshes). This cuts across ADR-11, ADR-24 and the
"nothing may cost money" rule, which currently forbids all three. Nothing is decided and no money
has been spent; see the assessment recorded in `DECISIONS.md` when it lands.

`F-10` stays `needs-human`: it needs an environment card only the owner can approve, and nothing
waits on it.

**The Unity track is parked**, waiting on issue #4: the licence secrets are not set, so `F-02`
cannot pass and `F-03` to `F-07` cannot start. Nothing else is blocked by it.

The owner hit trouble activating a Unity Personal licence in Unity Hub on 2026-11-10 — a known
flaky area, and the only route, since manual `.alf` activation does not cover Personal. `F-08` was
moved ahead of `F-02` so the build keeps moving meanwhile. If the secrets are still absent when
`F-02` comes up, follow its note: open a `human-gate` issue, set it `needs-human`, and carry on.
Packages that need no Unity at all: `F-08`, `SB-01a`, `I-01`, `I-04`, `I-05`.

**Note on `F-10` (cloud environment):** the kickoff session did not propose one. Its own container
had a second, unrelated repository attached, and an environment proposed from there would have
covered that project too. Propose it from a session that has only Islandia. The Default environment
has unrestricted network access and no setup script, so nothing is blocked meanwhile — the only
cost is reinstalling the .NET SDK and the companion toolchain each session.

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
| Local gate `make check` | 5 lint checks, 27 tooling tests, 117 core tests, 30 contract tests — about 12 s | F-01, F-08, SB-01a/b, V-02, V-06 |
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
