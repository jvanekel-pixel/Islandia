# 05 — Operations: how the build runs itself

This is the loop every autonomous session follows. It is written so that **any session can stop at
any moment and the next one can continue from the repository alone.**

## 1. Actors

| Actor | What it is | What it does |
| :-- | :-- | :-- |
| **Driver** | A Claude Code cloud session on Opus, started by the scheduled task *Island build driver* (or by the owner) | Picks the next work package, builds it, proves it, merges it, records it |
| **CI** | GitHub Actions in this repository | The only place Unity runs by default. Compiles, tests, builds the APK, runs the companion test suites |
| **Owner** | The human | Orientation once, then only the human gates in `00-START-HERE.md` |

There is one driver at a time (work-in-progress limit of 1) until `STATE.md` says otherwise.

## 2. The driver loop

```
start
 ├─ read CLAUDE.md, STATE.md, this file
 ├─ LEASE CHECK (§3) ── held by a live session ──▶ say so and stop
 ├─ adopt a stale work package, or pick the next one (§4)
 ├─ open the branch and a draft PR immediately (this is the lease)
 ├─ build (§5): spec → tests → code → local proof
 ├─ push; wait for CI (§6); fix until green or stop-the-line (§8)
 ├─ self-review (§7); squash-merge
 ├─ update BACKLOG.yaml + STATE.md in the same PR before merging
 ├─ milestone reached? ─▶ write the milestone report and cut the release (§9)
 └─ budget left (§10)? ─▶ loop;  otherwise stop cleanly
```

## 3. Lease — avoiding two drivers on one job

The lease is an **open pull request whose head branch starts with `claude/wp-`**.

1. List open PRs. If none has a `claude/wp-` head, the lease is free.
2. If one exists and its newest commit or comment is **under 90 minutes old**, another driver is
   alive. Stop. Do not start a second work package.
3. If one exists and is **older than 90 minutes**, it is stale. Adopt it: check out the branch, read
   the PR description and its progress comments, continue the work. Do not restart from scratch.
4. While you hold a lease, leave a heartbeat (a push or a one-line PR comment) at least hourly.

## 4. Picking work

`BACKLOG.yaml` is ordered. Take the first package whose `status` is `todo` and whose `deps` are all
`done`. Skip anything `blocked` or `needs-human`. Prefer packages in the current milestone; take
one from a later milestone only when the current one is waiting on a human gate.

If nothing is available: make sure a `human-gate` issue exists explaining exactly what is needed,
pause the scheduled task if you are able to, record that in `STATE.md`, and stop.

## 5. Building one work package

1. **Read** the backlog entry, the architecture sections it cites, and only the blueprint and draft
   files it lists. Read the matching `docs/plan/audit/` file for known defects in those drafts.
2. **Write the acceptance tests first**, at the lowest verification level that can prove each
   criterion (`04-VERIFICATION.md`). A criterion with no automated proof must be tagged
   `needs-device` or `needs-human` in the PR, never silently dropped.
3. **Implement.** Port drafts file by file. Anything that touches a Unity, URP, OpenXR or package
   API you have not seen compile in this repository is unverified: check it against the package
   source in `Library/PackageCache` (CI uploads an index, see `F-07`) or the official docs.
4. **Prove locally** what can be proven without Unity: `make core-test`, `make companion-test`,
   `make lint`. Do this before every push. CI time is the scarce resource: a Unity run costs tens of minutes.
5. **Push** and let CI do the Unity work.

Keep packages small. If one grows past roughly 800 changed lines of hand-written code, split it:
add the new entries to the backlog with a letter suffix on the original id, in the original's place,
and finish the first slice.

## 6. Working with CI

- Open PRs and read check results with the session's built-in GitHub tools, or `gh api` REST calls.
  `gh pr …` and `gh issue …` do not work in cloud sessions (GraphQL is blocked). Measured on
  2026-11-10, these REST paths work from a session and are what the loop runs on:

  | Path | For |
  | :-- | :-- |
  | `actions/runs`, `actions/workflows` | Has CI started, and did it finish |
  | `commits/<sha>/check-runs`, `commits/<sha>/status` | Whether this commit is green |
  | `pulls`, `pulls/<n>`, `pulls/<n>/files`, `pulls/<n>/merge` | Open, review and merge |
  | `issues`, `issues/<n>/labels`, `labels` | Human gates and blocked work |

  `actions/secrets` is blocked by the proxy and always will be. A session cannot tell whether a
  secret exists; it finds out when a workflow runs. Do not treat that 403 as a fault.
- Subscribe to the PR's activity if the tool is available so check results wake the session. If
  not, poll at intervals matched to the job (Unity jobs take 20–45 minutes; do not poll every
  minute). Ending the session while CI runs is fine — the lease protocol resumes it.
- **At most four Unity CI runs per work package.** If the fourth is red, stop the line (§8).
- Never push a "try this" commit to see what Unity says when the answer is discoverable by reading
  the failing log or the package source.
- CI failures that are infrastructure, not code (licence activation, runner disk, registry rate
  limits): retry once, then open an issue labelled `infra` and continue with non-Unity packages.

## 7. Review before merge

All required checks green on the head commit, then:

1. Re-read the full diff as a reviewer. Check it against the acceptance criteria one by one and
   paste the evidence (test names, CI run link, numbers) into the PR body.
2. For packages marked `risk: high`, get an independent read: hand the diff — not your reasoning —
   to a fresh subagent and ask for correctness defects only. Fix what is real.
3. Confirm the Definition of Done in `04-VERIFICATION.md` §6.
4. Squash-merge through the REST API. Delete nothing by force; never rewrite `main`.

## 8. Stop the line

A work package is `blocked` after **three failed attempts** or **four red Unity runs**. Then:

1. Open an issue labelled `blocked` with: what was tried, the exact failure, your best diagnosis,
   and the cheapest fallback you can see (the risk register in `06-RISKS.md` lists pre-agreed ones).
2. If a pre-agreed fallback exists, take it in a new work package and note it in `DECISIONS.md`.
3. Otherwise mark the package `blocked`, leave the draft PR open with the `blocked` label, and move
   on. An open blocked PR does not hold the lease: rename its branch prefix to `claude/parked-`.

Things that are never a reason to stop: a failing test you can read, a missing tool you can
install, a rate limit you can wait out. Things that always are: a missing licence, credential,
purchase, account, or a result that needs a headset.

## 9. Milestones and releases

When the last package of a milestone merges:

1. Write `docs/reports/M<n>-<slug>.md`: what now exists, the evidence, what is `needs-device`,
   known defects, and what changed in the plan.
2. Run the milestone retro package (`*-RETRO`): update `06-RISKS.md`, re-order the backlog, and
   delete or rewrite anything in the plan the milestone proved wrong.
3. Cut the release by adding `releases/M<n>.yaml` in that PR. The `release` workflow tags the
   commit and publishes a GitHub Release with the APK and the report. (Sessions cannot push tags.)
4. If the milestone has a human gate, open its issue from the template in `00-START-HERE.md`,
   assign the owner, and keep working on anything that does not depend on the answer.

## 10. Session budget

A driver session completes **up to three work packages**, then stops, even if more are available.
The scheduled task starts the next one. This bounds the cost of a session that has gone wrong and
keeps context fresh.

Stop earlier if: the lease is held; only human gates remain; you have hit a usage limit warning.

## 11. Changing the plan

The plan will be wrong in places. Fix it rather than work around it.

- Splitting, adding and re-ordering work packages: do it freely, in the PR that needs it.
- Changing an acceptance criterion, a budget, or anything in `02-ARCHITECTURE.md`: allowed, but as
  its own commit with an entry in `DECISIONS.md` giving the evidence.
- Removing a feature from scope, adding a paid dependency, changing target devices, anything
  that spends the owner's money, or anything that would change the StreamBox project: **not yours
  to decide.** Open a `human-gate` issue.

## 12. Files that hold the state

| File | Holds | Updated |
| :-- | :-- | :-- |
| `docs/plan/STATE.md` | Current milestone, in-flight package, blocked list, open human gates, last ten merges | Every PR |
| `docs/plan/BACKLOG.yaml` | Every work package and its status | Every PR |
| `docs/plan/DECISIONS.md` | Append-only log of decisions and plan changes, with evidence | When they happen |
| `docs/plan/06-RISKS.md` | Live risk register | Milestone retros, and when a risk fires |
| `docs/reports/` | Milestone reports, device-gate reports | Milestones, gates |

## 13. The scheduled task

Created once at kickoff (`docs/plan/prompts/kickoff.md`). Settings:

- **Prompt:** the contents of `docs/plan/prompts/driver.md`, verbatim.
- **Fires:** every 2 hours, starting a **new session** each time, in this repository's environment.
- **Model:** Opus.
- **Notifications:** push on completion.

If the session tooling cannot create it, the owner creates it at `claude.ai/code/routines` by
pasting that prompt; that is covered in the orientation checklist.

To pause the build: disable the task. To resume: enable it. Nothing else needs to be remembered.
