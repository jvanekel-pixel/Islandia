# Islandia — AGENTS.md

Static instructions for every agent that works in this repository. **Nothing live goes in this
file or in `CLAUDE.md`** (which only imports it): current state lives in `HANDOFF.md`,
`project.json` and `docs/plan/STATE.md`.

## Project Goal

Build **Islandia**, a single-player Meta Quest 2 island sandbox in Unity — sculpt, plant, build,
ride trains and roller coasters, paint anything, befriend a cat, and watch the owner's StreamBox
media in-world — plus a small web companion for importing models. It is a private gift, built
headlessly by autonomous sessions and CI, never published and never sold.

## Tool Roles & Routing

| Tool | Role | Limits |
| :-- | :-- | :-- |
| **Claude Code** | Primary code editor. Follows `routing` in `project.json` and the operating contract below | One work package per pull request; never merges its own pull request |
| **Antigravity** | Architecture, security and phase-close QoL audits | Reads and reports. Findings go to `BACKLOG.md` or an issue. Never opens the Unity Editor, never hand-writes Unity YAML, never pushes to `main` |
| **Local Ollama** | Read-only log checks, compression and briefs | Writes no code and no plan files |
| **Command Center MCP** | Call `get_project_context` before starting and `log_session` when done | If the server is not connected (cloud sessions have no `.mcp.json`; it is git-ignored), skip both calls and work from the files |

Whatever the tool: this repository is public and holds no personal data. Never give any tool the
StreamBox server address, a token, or anything from the owner's media library.

## Token Policy & Guardrails

- **Static prefix.** `AGENTS.md` and `CLAUDE.md` do not change between sessions, so the prompt
  cache holds. Live state is written to `HANDOFF.md` and `project.json` only.
- **Diff-only and quiet checks.** Show changes as diffs, not whole files. Send check output to a
  file under `_logs/` and summarise failures in **30 lines or fewer**.
- **Fresh chat per phase.** One phase of `PLAN.md` per conversation. QoL ideas and anything out of
  scope are parked in `BACKLOG.md` as one line, not built.
- **Two-Strike Rule.** After two failed attempts at the *same error*, stop editing and follow the
  `two-strike-recovery` skill. A strike-out is one failed attempt at the work package; the
  contract's stop-the-line rule (three failed attempts at the same package) still applies on top.
- **Session Stop Contract.** Before ending: overwrite `HANDOFF.md` (25 lines or fewer), update
  `project.json`, and check off `- [x]` in `PLAN.md` for work that is verified done — in addition
  to the contract's own trail (`docs/plan/BACKLOG.yaml` and `docs/plan/STATE.md`).

### One source of truth

The Command Center files are a thin view of the plan under `docs/plan/`, never a second plan.
Where they disagree, `docs/plan/` wins and the view is corrected in the same change.

| Command Center file | Mirrors | What belongs only in the view |
| :-- | :-- | :-- |
| `PLAN.md` | `docs/plan/BACKLOG.yaml` — phase N is milestone MN, one task per work package | Nothing |
| `HANDOFF.md`, `project.json` | `docs/plan/STATE.md` | Routing, budget and health fields |
| `SPEC.md` | `02-ARCHITECTURE.md` and `03-ROADMAP.md` | Nothing |
| `DECISIONS.md` (root) | `docs/plan/DECISIONS.md` and the ADRs — a one-line index | The decision template |
| `BACKLOG.md` (root) | — | Parked ideas. Work packages are never added here |
| `INFO-REQUESTS.md` | `human-gate` issues and the "With the owner now" table in `STATE.md` | Nothing |

## Compute & Budget Tier

**Subscription-First — $0 extra API.** All agent work runs on the owner's existing Claude
subscription; CI runs on free GitHub-hosted runners (the repository is public). No paid API, asset,
service or dependency, ever. The codebase holds no PII, so local-only AI is not required.

## Verification Commands

| Purpose | Command | Notes |
| :-- | :-- | :-- |
| **Everything provable without Unity** | `make check` | Lint, tooling tests, `Island.Core` tests, mock-gateway contract tests, StreamBox end-to-end. About a minute. Run before every push |
| Quiet form | `make check > _logs/check.log 2>&1; tail -30 _logs/check.log` | `_logs/*.log` is git-ignored |
| Static checks only | `make lint` | Plan, privacy, markdown, meta, layering, content, workflows |
| Typecheck / compile | `make core-build` | Compiles `Island.Core` as Unity will (netstandard2.1) |
| Core tests only | `make core-test` | Engine-free C#, seconds |
| Unity EditMode and PlayMode tests | CI only — workflow `unity-tests` | `make unity-test` only prints where it runs |
| Android build (IL2CPP, ARM64 APK) | CI only — workflow `android-build` | `make build` only prints where it runs |
| First-time setup in a session | `make deps` and `make mock-install` | Needs Python 3, Node and a .NET 9 SDK |
| What to take next | `make next` | First unblocked work packages |

## Islandia Operating Contract

*Carried over unchanged from the original `CLAUDE.md`. It governs every session.*

You are building **Islandia**, a Meta Quest 2 (standalone Android VR) island sandbox in Unity, plus
a small web companion, with the owner's StreamBox media service playing natively in the world. **No human is watching this session and nobody will ever open the Unity Editor
GUI.** The owner checks in only at the human gates listed in `docs/plan/00-START-HERE.md`.

**It is a gift.** The owner is making it for their partner, who will be the one wearing the
headset and who is not a developer. It is never published and never sold. Where a judgement call
is close, take the one that makes this a nicer thing to be given: the first five minutes matter
more than the feature list, and twenty things that belong together beat sixty that do not.

### Read first, every session

1. `docs/plan/STATE.md` — where the project is right now, what is in flight, what is blocked.
2. `docs/plan/05-OPERATIONS.md` — the loop you run (pick → build → verify → merge → record).
3. The backlog entry for the work package you pick, in `docs/plan/BACKLOG.yaml`, and only the
   blueprint/draft files it references. Do not load the whole blueprint; it is 12k lines of code.

### Order of authority

When two sources disagree, the higher one wins. If you must contradict a higher one, change it in
the same PR and log why in `docs/plan/DECISIONS.md`.

1. `docs/plan/02-ARCHITECTURE.md` — locked decisions, budgets, constants.
2. `docs/plan/BACKLOG.yaml` — scope and acceptance criteria per work package.
3. `docs/plan/04-VERIFICATION.md` — what "done" means and how it is proven.
4. `docs/blueprint/` — the original design document. **Reference only.** It has known errors
   (`docs/plan/01-AUDIT.md`).
5. `reference/drafts/` — code extracted from the blueprint. **Never compiled, never run.** Treat it
   as a detailed sketch by a colleague who could not test anything: mine it for structure and
   intent, port it file by file behind tests, and expect invented APIs.

### Hard rules

- **Headless only.** Every project, scene, asset, setting and build step is produced by code run
  with `-batchmode -executeMethod` or by a script. If a step needs a click, it is not done.
  Never write instructions that tell someone to use a menu or the Inspector.
- **Never hand-write Unity YAML** (`.unity`, `.prefab`, `.asset`, `ProjectSettings/*.asset`).
  Generate it with editor code in CI. Hand-written `.meta` files for scripts and folders are fine
  and expected (`tools/gen_meta.py`), so GUIDs stay stable.
- **Verify, then claim.** A work package is done when its acceptance tests pass in CI on the merge
  commit — not when the code is written. Report failures as failures.
- **Never fake a device result.** Anything that needs a headset is recorded as `needs-device` and
  goes into the next human gate. Do not mark an on-device gate passed from a simulation.
- **Budgets are law.** Draw-call, triangle, memory and per-frame-allocation budgets live in
  `02-ARCHITECTURE.md` and are enforced by tests. A change that breaks a budget is a failing change.
- **Do not weaken a gate to get green.** If a test or threshold is wrong, fix it in its own commit
  and record the reason in `DECISIONS.md`. Deleting or skipping a failing test is never the fix.
- **One work package per pull request.** Branch `claude/wp-<ID>-<slug>`. Small, reviewable,
  squash-merged. Use the session's GitHub tools or `gh api`; `gh pr` and `gh issue` do not work here.
- **Never merge your own pull request.** Label it `work-package` and `awaiting-merge`; the
  repository's auto-merge workflow lands it once the checks are green. The permission classifier
  refuses a self-merge and that refusal is not to be worked around.
- **Secrets** live in GitHub Actions secrets or the cloud environment only. Never print, commit or
  echo them. Never put keys in environment variables through tooling; the owner enters them.
- **Nothing may cost money.** No paid asset, service or dependency. Account-bound things (hosting)
  are gated behind feature flags and the project builds and passes CI without them.
- **The Quest 2 is the target.** Budgets in `02-ARCHITECTURE.md` ADR-10 are Quest 2 budgets.
  Nothing may depend on a newer headset.
- **This repository is public.** Never commit a server address, account name, token, keystore, or
  anything from the owner's media library. Never attach a self-hosted runner.
- **StreamBox is a separate project** (`docs/plan/07-STREAMBOX.md`). Never push to it, open pull
  requests on it, or copy from it beyond the client contract. Changes it would need go to the
  owner as a `human-gate` issue.
- **Stop the line** after three failed attempts at the same work package: write the diagnosis to an
  issue labelled `blocked`, set the package to `blocked` in the backlog, and take the next
  unblocked package. If nothing is unblocked, open a `human-gate` issue and stop.
- **Leave the trail.** Before ending a session: push, update `STATE.md`, and make sure the next
  session could continue from the repository alone.

### Conventions

- C# namespaces `Island.*`. Engine-free logic in `Packages/com.island.core` (no `UnityEngine`
  reference, tested with `dotnet test`); Unity-facing code in `Assets/Island/`.
- Runtime is composed in code from a single boot scene. No scene-serialised references to wire.
- Every feature ships with: save/load support, undo support where it edits the world, a budget
  ledger entry, and tests at the lowest verification level that can prove it.
- Commit messages: `<WP-ID>: imperative summary`. PR body lists acceptance criteria with evidence.

### Commands

The canonical commands are defined by `Makefile` once milestone M1 lands (`make help`). Until then
see `docs/plan/BACKLOG.yaml` → work packages `F-*`.
