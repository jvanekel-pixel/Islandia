# STATE

*The one file to read to know where the project stands. Updated in every pull request.*

| | |
| :-- | :-- |
| **Stage** | 1 — Inception |
| **Current milestone** | M1 Factory online |
| **In flight** | `F-01` is built, green and reviewed — [PR #1](https://github.com/jvanekel-pixel/Islandia/pull/1), labelled `awaiting-merge`. Merge it when convenient; nothing waits on it |
| **Repository** | https://github.com/jvanekel-pixel/Islandia (public) |
| **Driver** | **Paused 2026-11-10.** Scheduled task exists but is misconfigured — see below |
| **Last updated** | 2026-11-10 (planning bundle, revised for the owner's decisions) |
| **Target** | Quest 2 · public repository · StreamBox native in-world |

## Open human gates

| Gate | What is needed | Issue |
| :-- | :-- | :-- |
| HG-0 | Done 2026-11-10, except the Unity licence secrets (owner was adding them) | — |

If `F-02` fails because `UNITY_LICENSE`, `UNITY_EMAIL` or `UNITY_PASSWORD` is missing, that is the
tail of HG-0, not a new problem: open a `human-gate` issue pointing at `00-START-HERE.md` §2 step 4
and carry on with packages that do not need Unity (`F-01`, `F-08`, `SB-01`, `I-01`, `I-04`).

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

`F-08` (no Unity) → `F-02` (needs the licence secrets) → `F-03`.

The owner hit trouble activating a Unity Personal licence in Unity Hub on 2026-11-10 — a known
flaky area, and the only route, since manual `.alf` activation does not cover Personal. `F-08` was
moved ahead of `F-02` so the build keeps moving meanwhile. If the secrets are still absent when
`F-02` comes up, follow its note: open a `human-gate` issue, set it `needs-human`, and carry on.
Packages that need no Unity at all: `F-08`, `SB-01`, `I-01`, `I-04`, `I-05`.

**Note on `F-10` (cloud environment):** the kickoff session did not propose one. Its own container
had a second, unrelated repository attached, and an environment proposed from there would have
covered that project too. Propose it from a session that has only Islandia. The Default environment
has unrestricted network access and no setup script, so nothing is blocked meanwhile — the only
cost is reinstalling the .NET SDK and the companion toolchain each session.

## Recently merged

- `F-01` Repository skeleton, Makefile, lint, meta generator (2026-11-10)
- Planning bundle, driver prompt and kickoff state (2026-11-10)

## Measured facts

Filled in as they are measured; planning numbers elsewhere are hypotheses until they appear here.

| Fact | Value | Source |
| :-- | :-- | :-- |
| Local gate `make check` | 4 lint checks + 22 tooling tests, under 1 s | F-01 |
| GitHub REST from a session | runs, check-runs, status, pulls, issues all readable; `actions/secrets` blocked | F-01 |
| Driver self-merge | **Refused** by the permission classifier unless `.claude/settings.json` allows it. See `05-OPERATIONS.md` §7 step 4 | F-01 |
| Unity editor version pinned | — | F-03 |
| CI: warm test run / Android build | — | S-05 |
| Runner minutes per Unity package | — | S-05 |
| Device strings and tier | — | HG-1 |
| Frame time, empty island | — | HG-1 |
| Video backend that works on the headset | — | HG-1 (V-14) |
| StreamBox commit the contract snapshot matches | 9ebfdd6 (read during planning) | SB-01 |
